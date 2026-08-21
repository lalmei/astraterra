#!/usr/bin/env python3
"""Rewrite animation keyframes from Vintage Story's animation version 1 into version 0.

Why this exists
---------------
An animation added to a vanilla shape must declare the same version as every other animation in that
shape, or the engine logs:

    Shape game:entity/humanoid/seraph-faceless has mixed animation versions.
    This will cause incorrect animation blending.

Vanilla's seraph carries 258 animations and none of them declares a version, so ours must not either
-- but modelling tools export version 1 by default, and the two versions compose an element's
transform differently, so dropping the field alone silently moves the pose:

    version 1: T(origin) . S(elemScale) . R(elemRot) . T(-origin+from+offset) . S(poseScale) . R(poseRot)
    version 0: T(origin) . R(elemRot+poseRot) . S(elemScale*poseScale) . T(from+offset-origin)

Version 0 *sums* the element and pose Euler angles and rotates about the rotation origin; version 1
composes the two rotations and translates first. This script solves for the version 0 pose that
produces the identical matrix:

    poseRot' = euler(R(elemRot) . R(poseRot)) - elemRot
    offset'  = origin - from + R(poseRot)^-1 . (from + offset - origin)

and then verifies it by building both matrices with a direct port of the engine's own arithmetic
(Mat4f.Translate / Scale / RotateByXYZ and ShapeElement.GetLocalTransformMatrix, Vintage Story
1.22.7). It refuses to write if the two disagree, so the pose cannot drift by accident.

Only valid where element and pose scales are 1, which is the case for the shapes here; the script
checks and refuses otherwise.

Usage
-----
    tools/convert-animation-version.py <patch.json> <vanilla-shape.json>
"""
import json
import math
import sys


def identity():
    return [1.0 if i % 5 == 0 else 0.0 for i in range(16)]


def translate(m, x, y, z):
    m[12] += m[0] * x + m[4] * y + m[8] * z
    m[13] += m[1] * x + m[5] * y + m[9] * z
    m[14] += m[2] * x + m[6] * y + m[10] * z
    m[15] += m[3] * x + m[7] * y + m[11] * z


def rotate_xyz(m, rx, ry, rz):
    """Port of Mat4f.RotateByXYZ: post-multiplies m by the XYZ rotation."""
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


def rotation(rx, ry, rz):
    m = identity()
    rotate_xyz(m, rx, ry, rz)
    return m


def multiply3(a, b):
    return [
        sum(a[k * 4 + r] * b[c * 4 + k] for k in range(3))
        for c in range(3)
        for r in range(3)
    ]


def euler_from(r):
    """Inverse of rotate_xyz. r is the column-major 3x3 produced by multiply3."""
    at = lambda row, col: r[col * 3 + row]
    y = math.asin(max(-1.0, min(1.0, at(0, 2))))
    return math.atan2(-at(1, 2), at(2, 2)), y, math.atan2(-at(0, 1), at(0, 0))


def local_matrix(version, element, pose):
    """Port of ShapeElement.GetLocalTransformMatrix, scales assumed 1."""
    ox, oy, oz = (v / 16.0 for v in element["origin"])
    fx, fy, fz = (v / 16.0 for v in element["from"])
    tx, ty, tz = pose["offset"]
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


def to_version_zero(element, pose):
    element_rotation = [math.radians(v) for v in element["rotation"]]
    pose_rotation = [math.radians(v) for v in pose["rotation"]]
    combined = multiply3(rotation(*element_rotation), rotation(*pose_rotation))
    solved = [math.degrees(v) for v in euler_from(combined)]
    new_rotation = [s - e for s, e in zip(solved, element["rotation"])]

    origin = [v / 16.0 for v in element["origin"]]
    start = [v / 16.0 for v in element["from"]]
    unrotated = [(-o) + f + t for o, f, t in zip(origin, start, pose["offset"])]
    r = rotation(*pose_rotation)
    # R is orthonormal, so its transpose is its inverse.
    rotated = [sum(r[c * 4 + row] * unrotated[row] for row in range(3)) for c in range(3)]
    new_offset = [w - f + o for w, f, o in zip(rotated, start, origin)]
    return {"rotation": new_rotation, "offset": new_offset}


def collect_elements(shape):
    found = {}

    def walk(elements):
        for element in elements or []:
            if element.get("name"):
                found[element["name"]] = {
                    "rotation": [element.get(f"rotation{axis}", 0) for axis in "XYZ"],
                    "from": element.get("from", [0, 0, 0]),
                    "origin": element.get("rotationOrigin", [0, 0, 0]),
                    "scale": [element.get(f"scale{axis}", 1) for axis in "XYZ"],
                }
            walk(element.get("children"))

    walk(shape.get("elements"))
    return found


def main(patch_path, shape_path, tolerance=1e-4):
    elements = collect_elements(json.load(open(shape_path)))
    patch = json.load(open(patch_path))
    worst = 0.0
    converted = 0

    for operation in patch:
        clip = operation["value"]
        if clip.pop("version", 0) != 1:
            continue
        for keyframe in clip.get("keyframes", []):
            for name, pose in keyframe.get("elements", {}).items():
                element = elements[name]
                if any(abs(s - 1) > 1e-9 for s in element["scale"]):
                    raise SystemExit(f"{name} is scaled; this conversion only handles unit scale.")
                old = {
                    "rotation": [pose.get(f"rotation{axis}", 0) for axis in "XYZ"],
                    "offset": [pose.get(f"offset{axis}", 0) for axis in "XYZ"],
                }
                new = to_version_zero(element, old)
                rounded = {
                    "rotation": [round(v, 4) + 0.0 for v in new["rotation"]],
                    "offset": [round(v, 4) + 0.0 for v in new["offset"]],
                }
                worst = max(
                    worst,
                    max(abs(a - b) for a, b in zip(local_matrix(1, element, old), local_matrix(0, element, rounded))),
                )
                for axis, value in zip("XYZ", rounded["rotation"]):
                    pose[f"rotation{axis}"] = value
                for axis, value in zip("XYZ", rounded["offset"]):
                    pose[f"offset{axis}"] = value
                converted += 1

    if worst > tolerance:
        raise SystemExit(f"conversion deviates by {worst:.3e}, refusing to write")

    with open(patch_path, "w") as handle:
        json.dump(patch, handle, indent=2)
        handle.write("\n")
    print(f"converted {converted} element poses; worst deviation {worst:.3e} blocks")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit(__doc__)
    main(sys.argv[1], sys.argv[2])
