#!/usr/bin/env python3
"""Render a seraph animation clip to a PNG contact sheet, without starting the game.

Why this exists
---------------
A pose that reads fine as numbers can read as a gymnastic backbend in game: the engine
eases a clip in by blending its first keyframe against the pose the seraph is already
in, so a clip whose frame 0 is the finished pose plays as a linear slide into it -- the
feet stay planted while the torso pitches back, and the seraph bridges. The same blind
spot hides joints: an upper arm rotated past its socket puts the elbow somewhere the
forearm cannot follow, which only shows up as a gap on screen.

So this draws the thing. It ports ShapeElement.GetLocalTransformMatrix for both
animation versions -- the same arithmetic tools/convert-animation-version.py verifies
against -- walks the element tree, and paints the cubes with a depth sort.

    tools/preview_seraph_pose.py --clip stargaze
    tools/preview_seraph_pose.py --clip stargaze --blend        # the ease-in, frame by frame
    tools/preview_seraph_pose.py --clip lie --shape-clip        # a vanilla clip, for reference

--blend is the one that catches bridging: it samples the ease-in from the standing pose
the same way the engine's weighted blend does. Watch the feet.

Reads the mod's own patch by default; --shape-clip reads the vanilla shape instead.
"""
from __future__ import annotations

import argparse
import json
import math
import os
import sys
from pathlib import Path

try:
    from PIL import Image, ImageDraw
except ImportError:  # pragma: no cover - a dev-machine tool, not shipped code
    sys.exit("This tool needs Pillow: pip install pillow")

REPOSITORY = Path(__file__).resolve().parent.parent
GAME_APP = Path(os.environ.get("GAME_APP", "/Applications/Vintage Story.app"))
VANILLA_SHAPE = GAME_APP / "assets/game/shapes/entity/humanoid/seraph-faceless.json"
PATCH = REPOSITORY / "assets/astraterra/patches/seraph-stargaze.json"

# Elements the renderer leaves out: held props and zero-size attachment anchors.
PROPS = {"Knife", "saw", "saw2", "saw3", "saw4", "Shovel1", "Shovel2", "ItemAnchor", "ItemAnchorL"}

COLOURS = {
    "LowerTorso": (128, 150, 96), "UpperTorso": (140, 165, 105),
    "Neck": (190, 140, 80), "Head": (215, 160, 95),
    "UpperArmR": (70, 130, 180), "LowerArmR": (100, 165, 215),
    "UpperArmL": (140, 90, 180), "LowerArmL": (170, 125, 215),
    "UpperFootR": (185, 85, 85), "LowerFootR": (215, 120, 120),
    "UpperFootL": (185, 120, 80), "LowerFootL": (215, 155, 110),
}

FACES = [  # (corner indices, axis, high side) -- corner i has bit k set when it takes `to[k]`
    ((0, 1, 3, 2), 2, 0), ((4, 5, 7, 6), 2, 1),
    ((0, 1, 5, 4), 1, 0), ((2, 3, 7, 6), 1, 1),
    ((0, 2, 6, 4), 0, 0), ((1, 3, 7, 5), 0, 1),
]


# --- engine arithmetic ------------------------------------------------------------
# Ports of Mat4f and ShapeElement.GetLocalTransformMatrix (Vintage Story 1.22.7),
# unit scale only, kept deliberately close to the original shape of the code.

def identity():
    return [1.0 if i % 5 == 0 else 0.0 for i in range(16)]


def translate(m, x, y, z):
    m[12] += m[0] * x + m[4] * y + m[8] * z
    m[13] += m[1] * x + m[5] * y + m[9] * z
    m[14] += m[2] * x + m[6] * y + m[10] * z
    m[15] += m[3] * x + m[7] * y + m[11] * z


def rotate_xyz(m, rx, ry, rz):
    if rx == 0 and ry == 0 and rz == 0:
        return
    sx, cx = math.sin(rx), math.cos(rx)
    sy, cy = math.sin(ry), math.cos(ry)
    sz, cz = math.sin(rz), math.cos(rz)
    a, b = sx * sy, -cx * sy
    cols = (
        (cy * cz, a * cz + cx * sz, b * cz + sx * sz),
        (-cy * sz, cx * cz - a * sz, sx * cz - b * sz),
        (sy, -sx * cy, cx * cy),
    )
    o = m[:]
    for column, (k0, k1, k2) in enumerate(cols):
        for row in range(4):
            m[column * 4 + row] = k0 * o[row] + k1 * o[4 + row] + k2 * o[8 + row]


def multiply(a, b):
    return [
        sum(a[k * 4 + r] * b[c * 4 + k] for k in range(4))
        for c in range(4)
        for r in range(4)
    ]


def apply(m, p):
    return [sum(m[c * 4 + r] * p[c] for c in range(3)) + m[12 + r] for r in range(3)]


def local_matrix(version, element, pose):
    ox, oy, oz = (v / 16.0 for v in element["origin"])
    fx, fy, fz = (v / 16.0 for v in element["from"])
    tx, ty, tz = (v / 16.0 for v in pose["offset"])
    m = identity()
    translate(m, ox, oy, oz)
    if version == 1:
        rotate_xyz(m, *(math.radians(v) for v in element["rotation"]))
        translate(m, -ox + fx + tx, -oy + fy + ty, -oz + fz + tz)
        rotate_xyz(m, *(math.radians(v) for v in pose["rotation"]))
    else:
        rotate_xyz(m, *(math.radians(e + p) for e, p in zip(element["rotation"], pose["rotation"])))
        translate(m, fx + tx - ox, fy + ty - oy, fz + tz - oz)
    return m


def pose_for(frame, name):
    values = frame.get(name, {})
    return {
        "rotation": [values.get(f"rotation{axis}", 0.0) for axis in "XYZ"],
        "offset": [values.get(f"offset{axis}", 0.0) for axis in "XYZ"],
    }


def joints(shape, frame, version):
    """World position, in pixels, of every element's rotation origin -- its joint."""
    found = {}

    def walk(elements, parent):
        for element in elements or []:
            name = element.get("name", "")
            spec = {
                "rotation": [element.get(f"rotation{axis}", 0.0) for axis in "XYZ"],
                "from": element.get("from", [0, 0, 0]),
                "origin": element.get("rotationOrigin", [0, 0, 0]),
            }
            world = multiply(parent, local_matrix(version, spec, pose_for(frame, name)))
            start, origin = spec["from"], spec["origin"]
            found[name] = [16 * v for v in apply(world, [(origin[k] - start[k]) / 16.0 for k in range(3)])]
            walk(element.get("children"), world)

    walk(shape["elements"], identity())
    return found


def solve(shape, frame, version):
    """World-space corners, in blocks, of every drawable cube in the posed shape."""
    boxes = {}

    def walk(elements, parent):
        for element in elements or []:
            name = element.get("name", "")
            spec = {
                "rotation": [element.get(f"rotation{axis}", 0.0) for axis in "XYZ"],
                "from": element.get("from", [0, 0, 0]),
                "origin": element.get("rotationOrigin", [0, 0, 0]),
            }
            world = multiply(parent, local_matrix(version, spec, pose_for(frame, name)))
            start, end = element.get("from", [0, 0, 0]), element.get("to", [0, 0, 0])
            if name not in PROPS and any(a != b for a, b in zip(start, end)):
                boxes[name] = [
                    apply(world, [((end[k] if (i >> k) & 1 else start[k]) - start[k]) / 16.0 for k in range(3)])
                    for i in range(8)
                ]
            walk(element.get("children"), world)

    walk(shape["elements"], identity())
    return boxes


# --- drawing ----------------------------------------------------------------------

VIEWS = {
    "side": (0, 1, 2, -1),    # from the seraph's left: X right, Y up, depth along Z
    "front": (2, 1, 0, 1),    # facing it: Z right, Y up, depth along X
    "top": (0, 2, 1, -1),     # from above: X right, Z down-screen, depth along Y
}


def draw_pose(draw, boxes, view, origin, scale):
    right, up, depth, sign = VIEWS[view]
    ox, oy = origin
    faces = []
    for name, corners in boxes.items():
        for indices, axis, high in FACES:
            points = [corners[i] for i in indices]
            faces.append((
                sign * sum(p[depth] for p in points) / 4.0,
                name,
                axis,
                high,
                [(ox + p[right] * scale, oy - p[up] * scale) for p in points],
            ))
    faces.sort(key=lambda face: face[0])
    for _, name, axis, high, points in faces:
        base = COLOURS.get(name, (110, 110, 110))
        shade = (0.62, 1.0, 0.78)[axis] * (1.0 if high else 0.82)
        draw.polygon(points, fill=tuple(int(c * shade) for c in base), outline=(20, 20, 22))


def contact_sheet(shape, frames, labels, views, title, cell=(320, 300)):
    width, height = cell
    image = Image.new("RGB", (width * len(frames), height * len(views) + 26), (16, 16, 18))
    draw = ImageDraw.Draw(image)
    draw.text((8, 8), title, fill=(210, 210, 210))

    posed = [solve(shape, frame, version) for frame, version in frames]
    for row, view in enumerate(views):
        right, up, _, _ = VIEWS[view]
        # One scale and centre for the whole row, so movement across the row is readable.
        points = [p for boxes in posed for corners in boxes.values() for p in corners]
        span_x = max(p[right] for p in points) - min(p[right] for p in points)
        span_y = max(p[up] for p in points) - min(p[up] for p in points)
        scale = min((width - 30) / max(span_x, 0.4), (height - 40) / max(span_y, 0.4))
        centre_x = (max(p[right] for p in points) + min(p[right] for p in points)) / 2
        ground = min(p[up] for p in points)
        for column, boxes in enumerate(posed):
            ox = column * width + width / 2 - centre_x * scale
            oy = 26 + row * height + height - 20 + ground * scale
            draw.line([(column * width, oy), (column * width + width, oy)], fill=(60, 60, 66))
            draw_pose(draw, boxes, view, (ox, oy), scale)
            draw.text((column * width + 8, 26 + row * height + 6), f"{view}  {labels[column]}", fill=(120, 120, 128))
            draw.line([(column * width, 26 + row * height), (column * width, 26 + (row + 1) * height)], fill=(45, 45, 50))
    return image


# --- clips ------------------------------------------------------------------------

def load_clip(code, from_shape, shape, patch_path):
    if from_shape:
        for animation in shape.get("animations", []):
            if animation["code"] == code:
                return animation, animation.get("version", 0)
        raise SystemExit(f"no clip '{code}' in the vanilla shape")

    patch = json.loads(patch_path.read_text())
    for operation in patch:
        clip = operation.get("value", {})
        if clip.get("code") == code:
            return clip, clip.get("version", 0)
    raise SystemExit(f"no clip '{code}' in {patch_path}")


def keyframes(clip):
    return [(kf.get("frame", 0), kf.get("elements", {})) for kf in clip.get("keyframes", [])]


def lerp_frame(a, b, t):
    """Linear blend of two keyframes, which is what the engine does between them."""
    out = {}
    for name in set(a) | set(b):
        first, second = a.get(name, {}), b.get(name, {})
        out[name] = {
            key: first.get(key, 0.0) * (1 - t) + second.get(key, 0.0) * t
            for key in set(first) | set(second)
        }
    return out


def timeline(clip, tween, blend):
    """The frames to draw: the ease-in if asked for, then the clip's own keyframes."""
    frames, labels = [], []
    steps = keyframes(clip)
    if blend:
        for i in range(blend + 1):
            frames.append(lerp_frame({}, steps[0][1], i / blend))
            labels.append(f"ease {i}/{blend}")
    for index, (number, elements) in enumerate(steps):
        frames.append(elements)
        labels.append(f"f{number}")
        if tween and index + 1 < len(steps):
            nxt = steps[index + 1]
            for step in range(1, tween + 1):
                frames.append(lerp_frame(elements, nxt[1], step / (tween + 1)))
                labels.append(f"f{number}+{step}")
    return frames, labels


JOINTS = [("UpperArmR", "LowerArmR"), ("UpperArmL", "LowerArmL"),
          ("UpperFootR", "LowerFootR"), ("UpperFootL", "LowerFootL"),
          ("UpperTorso", "Neck"), ("Neck", "Head")]


def joint_gaps(boxes):
    """How far apart two linked cubes sit, at their closest corners."""
    return {
        f"{parent}->{child}": min(math.dist(a, b) for a in boxes[parent] for b in boxes[child]) * 16
        for parent, child in JOINTS
        if parent in boxes and child in boxes
    }


def diagnose(shape, frames, version, labels):
    """Ground contact and joint continuity: the two things that read as broken on screen.

    Cubes on this rig do not meet exactly even when standing, so a gap is only news when
    it is wider than the same gap in the neutral pose -- that difference is a joint pulled
    apart by the pose, which on screen is a limb that has come off.
    """
    rest = joint_gaps(solve(shape, {}, version))
    print(f"{'frame':>12}  {'lowest px':>9}  widest joint pulled open")
    for frame, label in zip(frames, labels):
        boxes = solve(shape, frame, version)
        lowest = min(p[1] for corners in boxes.values() for p in corners) * 16
        opened = {name: gap - rest.get(name, 0.0) for name, gap in joint_gaps(boxes).items()}
        where = max(opened, key=opened.get)
        flag = "  <-- torn" if opened[where] > 1.0 else ""
        print(f"{label:>12}  {lowest:9.2f}  {opened[where]:5.2f}px {where}{flag}")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--clip", default="stargaze", help="animation code to draw (default: stargaze)")
    parser.add_argument("--shape-clip", action="store_true", help="read the clip from the vanilla shape instead of the mod patch")
    parser.add_argument("--shape", type=Path, default=VANILLA_SHAPE, help="seraph shape JSON")
    parser.add_argument("--patch", type=Path, default=PATCH, help="mod animation patch")
    parser.add_argument("--views", default="side,front,top")
    parser.add_argument("--tween", type=int, default=0, help="extra samples drawn between keyframes")
    parser.add_argument("--blend", type=int, default=0, help="samples of the ease-in from standing")
    parser.add_argument("--out", type=Path, default=REPOSITORY / "dist/pose-preview.png")
    parser.add_argument("--open", action="store_true", help="open the sheet when it is written")
    arguments = parser.parse_args()

    if not arguments.shape.exists():
        sys.exit(f"seraph shape not found at {arguments.shape} (set GAME_APP or pass --shape)")
    shape = json.loads(arguments.shape.read_text())
    clip, version = load_clip(arguments.clip, arguments.shape_clip, shape, arguments.patch)
    frames, labels = timeline(clip, arguments.tween, arguments.blend)
    views = [view.strip() for view in arguments.views.split(",") if view.strip()]

    title = f"{arguments.clip}  (animation version {version}, {len(keyframes(clip))} keyframes)"
    sheet = contact_sheet(shape, [(frame, version) for frame in frames], labels, views, title)
    arguments.out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(arguments.out)
    diagnose(shape, frames, version, labels)
    print(f"wrote {arguments.out}")
    if arguments.open:
        os.system(f'open "{arguments.out}"')


if __name__ == "__main__":
    main()
