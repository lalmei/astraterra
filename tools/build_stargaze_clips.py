#!/usr/bin/env python3
"""Build the seraph's stargaze animation clips and write them into the shape patch.

Why a generator
---------------
The pose is easier to state as "the hips are on the ground, the head is roughly over
where the player is standing" than as the numbers an animation keyframe actually holds.
Version 0 keyframes -- which is what every clip on the vanilla seraph is, and therefore
what ours must be -- rotate each element about its rotation origin and then translate in
the *rotated* frame, so a hip drop of 13 pixels on a torso pitched onto its back is not
`offsetY: -13`, it is a mix of X and Y that no one should be deriving by hand.

So the keyframes here are written as intent -- torso pitch, where the hip joint should
end up in the world, how far each joint bends -- and the offsets are solved numerically
against a port of the engine's own transform code, then checked: nothing may sink into
the ground, and no joint may pull apart.

Four clips come out of it:

  stargaze-down   the sit-down-and-recline transition, held on its last frame
  stargaze        the supine idle, hands behind the head, breathing
  stargaze-hold   the same body with the arms left alone, for a held instrument
  stargaze-up     getting back up: the recline read backwards, and quicker

Getting up is the recline reversed because that is what getting up is -- sit up, tuck
the legs under, stand -- and because the two then cannot disagree about the pose they
meet in. It runs in three quarters of the time: settling onto the ground is slower
than leaving it.

The transition exists because the engine eases a clip in by blending its first keyframe
against the pose the seraph is already in. A clip whose frame 0 is the finished supine
pose therefore plays as a slide into it: the feet stay planted, the torso pitches back,
and the seraph bridges. Frame 0 of the transition is the standing pose, so the recline
follows the keyframes instead.

    tools/build_stargaze_clips.py            # write the patch
    tools/build_stargaze_clips.py --check    # verify the patch matches, change nothing

Preview what it wrote with tools/preview_seraph_pose.py.
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from preview_seraph_pose import (  # noqa: E402
    PATCH, VANILLA_SHAPE, joint_gaps, joints, solve,
)

try:
    from scipy.optimize import least_squares
except ImportError:  # pragma: no cover - a dev-machine tool, not shipped code
    least_squares = None

SHAPE_FILE = "game:shapes/entity/humanoid/seraph-faceless"

# --- the pose, as intent ----------------------------------------------------------

# Where the hip joint should sit, in pixels, at each step of lying down: forward of the
# player's feet (X, negative is the way the seraph faces) and above the ground (Y).
# Standing hips are at x -1.6, y 15.2.
RECLINE = [
    # frame, torso pitch, hip x, legs
    (0, 0.0, None, {}),
    (5, 14.0, 0.5, {
        "UpperFootR": {"rotationX": -20.0, "rotationY": -22.0, "rotationZ": -74.0},
        "LowerFootR": {"rotationZ": 78.0, "offsetY": 1.0},
        "UpperFootL": {"rotationY": 34.0, "rotationZ": -44.0},
        "LowerFootL": {"rotationZ": 74.0, "offsetY": 1.0},
    }),
    (11, -22.0, -1.0, {
        "UpperFootR": {"rotationX": -12.0, "rotationY": -20.0, "rotationZ": -70.0},
        "LowerFootR": {"rotationZ": 96.0, "offsetY": 1.0},
        "UpperFootL": {"rotationY": 24.0, "rotationZ": -52.0},
        "LowerFootL": {"rotationZ": 84.0, "offsetY": 1.0},
    }),
    (16, -62.0, -4.0, {
        "UpperFootR": {"rotationY": -18.0, "rotationZ": -54.0},
        "LowerFootR": {"rotationZ": 94.0, "offsetY": 1.0},
        "UpperFootL": {"rotationY": 8.0, "rotationZ": -26.0},
        "LowerFootL": {"rotationZ": 48.0, "offsetY": 1.0},
    }),
]

# The supine rest pose every idle keyframe is built from.
REST_PITCH = -90.0
REST_HIP_X = -6.2
REST_LEGS = {
    # Right knee drawn up with the foot flat on the ground, left leg long and turned out:
    # a symmetric pair of straight legs is what reads as a plank.
    "UpperFootR": {"rotationY": -16.0, "rotationZ": -40.0},
    "LowerFootR": {"rotationZ": 86.0, "offsetY": 1.0},
    "UpperFootL": {"rotationY": -16.0, "rotationZ": -4.0},
    "LowerFootL": {"rotationZ": 10.0},
}
REST_CHEST = 6.0

# Hands behind the head: the shoulders turn out so the elbows fall open onto the ground
# beside the head, rather than the arms being raised, which on a seraph on its back
# leaves them waving in the air. The forearm's one-pixel nudge is the same trick every
# vanilla clip with a folded elbow uses -- the cubes meet at the elbow, not at the joint.
ARMS = {
    "UpperArmR": {"rotationX": 60.0, "rotationY": -70.0, "rotationZ": -21.0},
    "LowerArmR": {"rotationX": 80.0, "rotationY": -58.0, "rotationZ": -83.0, "offsetY": 1.0},
    "UpperArmL": {"rotationX": -60.0, "rotationY": 70.0, "rotationZ": -21.0},
    "LowerArmL": {"rotationX": -80.0, "rotationY": 58.0, "rotationZ": -83.0, "offsetY": 1.0},
}

# Breathing: the chest lifts a couple of degrees and the head rocks with it. Small enough
# to read as alive rather than as motion, which is the difference between resting and
# being a prop lying on the floor.
BREATH = [
    (0, 0.0), (14, 2.2), (28, 0.6),
]
IDLE_FRAMES = 42

# Getting up, as a fraction of the time going down takes.
RISE_SCALE = 0.75


# --- solving ----------------------------------------------------------------------

def shape():
    if not VANILLA_SHAPE.exists():
        sys.exit(f"seraph shape not found at {VANILLA_SHAPE} (set GAME_APP)")
    return json.loads(VANILLA_SHAPE.read_text())


def hip_position(model, frame):
    """World position of the hip joint, in pixels: where the legs hang off the pelvis."""
    hinges = joints(model, frame, 0)
    return [(hinges["UpperFootR"][i] + hinges["UpperFootL"][i]) / 2 for i in range(3)]


GROUND = 0.15   # pixels of clearance: resting on the ground, not buried in it


def torso_offsets(model, pitch, hip_x, legs, guess=(0.0, 0.0)):
    """Solve the version 0 offsets that put the hips at `hip_x` with the pose on the ground.

    Height is solved rather than given: what matters is that whatever is lowest -- a heel
    mid-crouch, the shoulder blades once supine -- is the thing touching the ground.
    """
    if least_squares is None:
        sys.exit("This tool needs SciPy: pip install scipy")

    def residual(p):
        frame = {"LowerTorso": {"rotationZ": pitch, "offsetX": p[0], "offsetY": p[1]}, **legs}
        boxes = solve(model, frame, 0)
        lowest = 16 * min(point[1] for corners in boxes.values() for point in corners)
        return [hip_position(model, frame)[0] - hip_x, lowest - GROUND]

    solved = least_squares(residual, list(guess), x_scale=8.0)
    return [round(float(v), 3) for v in solved.x]


def supine(model, chest=REST_CHEST, arms=True):
    frame = {
        "LowerTorso": {"rotationZ": REST_PITCH},
        "UpperTorso": {"rotationZ": chest},
        **{name: dict(values) for name, values in REST_LEGS.items()},
    }
    if arms:
        frame.update({name: dict(values) for name, values in ARMS.items()})
    offsets = torso_offsets(model, REST_PITCH, REST_HIP_X, REST_LEGS, guess=(11.0, -15.0))
    frame["LowerTorso"]["offsetX"], frame["LowerTorso"]["offsetY"] = offsets
    return frame


def recline_frames(model):
    frames = []
    for number, pitch, hip_x, legs in RECLINE:
        frame = {"LowerTorso": {"rotationZ": pitch}, **{n: dict(v) for n, v in legs.items()}}
        if hip_x is not None:
            offsets = torso_offsets(model, pitch, hip_x, legs)
            frame["LowerTorso"]["offsetX"], frame["LowerTorso"]["offsetY"] = offsets
        frames.append((number, frame))
    frames.append((20, supine(model, arms=False)))
    return frames


def rise_frames(recline):
    """The recline backwards on a shorter clock, so its last pose is this one's first."""
    last = recline[-1][0]
    return [
        (round((last - number) * RISE_SCALE), pose)
        for number, pose in reversed(recline)
    ]


def idle_frames(model, arms):
    return [
        (number, supine(model, chest=REST_CHEST + lift, arms=arms))
        for number, lift in BREATH
    ]


# The engine reads a keyframe in transform groups, not axes: a group counts as posed when
# any one of its axes is named, and it then casts all three off the nullable fields. Naming
# one axis of a group and leaving its siblings out is a null it dereferences, which crashed
# the client the moment the clip was first played -- so a group that is touched is written
# whole. Every one of vanilla's own 258 seraph clips does the same.
GROUPS = [("offsetX", "offsetY", "offsetZ"), ("rotationX", "rotationY", "rotationZ")]


def fill(frames):
    """Give every keyframe the same elements, and every element every axis, whole groups.

    An element missing from a keyframe is not held where it is either -- it is interpolated
    from wherever it is named next, so an incomplete frame 0 would start the recline from the
    supine legs.

    An axis a keyframe does not name is that bone at rest on that axis, which is the same
    thing the C# copy of these clips in SkyLyingAnimation says by leaving it at zero. Writing
    the zeros out keeps the two in step and gives the engine every field it reads.
    """
    touched = sorted({element for _, pose in frames for element in pose})
    frames = [(number, {element: dict(pose.get(element, {})) for element in touched}) for number, pose in frames]

    for _, pose in frames:
        for values in pose.values():
            for axis in (axis for group in GROUPS for axis in group):
                values.setdefault(axis, 0.0)

    return frames


def clip(name, code, frames, quantity, on_end):
    frames = fill(frames)
    return {
        "name": name,
        "code": code,
        "quantityframes": quantity,
        "onActivityStopped": "EaseOut",
        "onAnimationEnd": on_end,
        "keyframes": [
            {"frame": number, "elements": {
                element: {key: round(value, 4) + 0.0 for key, value in sorted(values.items())}
                for element, values in sorted(pose.items())
            }}
            for number, pose in frames
        ],
    }


def build(model):
    recline = recline_frames(model)
    rise = rise_frames(recline)
    return [
        clip("StargazeDown", "stargaze-down", recline, recline[-1][0] + 1, "Hold"),
        clip("Stargaze", "stargaze", idle_frames(model, arms=True), IDLE_FRAMES, "Repeat"),
        clip("StargazeHold", "stargaze-hold", idle_frames(model, arms=False), IDLE_FRAMES, "Repeat"),
        clip("StargazeUp", "stargaze-up", rise, rise[-1][0] + 1, "Hold"),
    ]


# --- checks -----------------------------------------------------------------------

def verify(model, clips):
    """Nothing sinks into the ground; no joint is pulled apart. Both read as broken."""
    rest = joint_gaps(solve(model, {}, 0))
    problems = []
    for animation in clips:
        for keyframe in animation["keyframes"]:
            boxes = solve(model, keyframe["elements"], 0)
            lowest = 16 * min(p[1] for corners in boxes.values() for p in corners)
            if lowest < -1.5:
                problems.append(f"{animation['code']} f{keyframe['frame']}: sinks {-lowest:.1f}px into the ground")
            for joint, gap in joint_gaps(boxes).items():
                if gap - rest.get(joint, 0.0) > 1.5:
                    problems.append(
                        f"{animation['code']} f{keyframe['frame']}: {joint} pulled {gap - rest[joint]:.1f}px apart")
    return problems


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="verify the patch on disk matches, and write nothing")
    parser.add_argument("--out", type=Path, default=PATCH)
    arguments = parser.parse_args()

    model = shape()
    clips = build(model)
    problems = verify(model, clips)
    for problem in problems:
        print(f"  {problem}", file=sys.stderr)
    if problems:
        sys.exit("pose check failed")

    patch = [{"file": SHAPE_FILE, "op": "add", "path": "/animations/-", "value": animation} for animation in clips]
    rendered = json.dumps(patch, indent=2) + "\n"

    if arguments.check:
        current = arguments.out.read_text() if arguments.out.exists() else ""
        if current != rendered:
            sys.exit(f"{arguments.out} is out of date; run tools/build_stargaze_clips.py")
        print(f"{arguments.out} is up to date")
        return

    arguments.out.write_text(rendered)
    print(f"wrote {arguments.out}: " + ", ".join(
        f"{animation['code']} ({len(animation['keyframes'])} keyframes)" for animation in clips))


if __name__ == "__main__":
    main()
