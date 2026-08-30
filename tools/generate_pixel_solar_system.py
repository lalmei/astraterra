"""Normalise the pixel-art solar system into textures the sky renderer can draw.

The art arrives as bodies floating on a wide transparent canvas, at whatever size each
one was drawn. The renderer wants what the photographic set already is: a square picture
whose width is the body's own image width -- the globe alone for a planet, globe and
rings for Saturn -- so that `imageWidthInDiameters` in planets.v1.json sizes both sets
alike. So each body is cropped to its own ink and padded back out to a square.

The moon is the one body the art does not supply whole. It comes as a single full face,
and the sky needs eight, so the other seven are cut from it here: the terminator is an
ellipse across the disc, the unlit side is dimmed to the earthshine the photographic set
keeps, and the cut is quantised to the art's own pixel grid so the shadow's edge is as
blocky as everything else in the picture.

Run: python tools/generate_pixel_solar_system.py
"""

from __future__ import annotations

import math
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "assets" / "astraterra" / "textures" / "environment" / "solar-system-pixel_art"
OUT_DIR = ROOT / "assets" / "astraterra" / "textures" / "environment" / "solar-system-pixel"

# Anything below this is canvas, not body: the art's edges fade out rather than stop.
ALPHA_FLOOR = 16

# Bodies whose picture is the globe alone, so cropping to the ink squares them correctly.
GLOBES: dict[str, str] = {
    "Mercury.png": "mercury.png",
    "venus.png": "venus.png",
    "Mars.png": "mars.png",
    "Jupiter.png": "jupiter.png",
    "Neptune.png": "neptune.png",
    "Io.png": "io.png",
    "Europa.png": "europa.png",
    "ganymede.png": "ganymede.png",
    "titan.png": "titan.png",
    "Tritom.png": "triton.png",
    # The stand-in for the moons the art does not draw: Callisto, and Saturn's four small ones.
    "moon_small.png": "small-moon.png",
}

# Saturn is the exception: its ink is the rings, and the square has to be measured from
# the globe inside them instead. The circle is measured off the art -- centre and
# diameter in source pixels -- and the square is that diameter times the ring span the
# catalogue quotes, so Saturn's globe draws the same width as every other planet's.
SATURN_SOURCE = "Saturn.png"
SATURN_GLOBE_CENTRE = (377.0, 211.5)
SATURN_GLOBE_DIAMETER = 265.0
SATURN_IMAGE_WIDTH_IN_DIAMETERS = 2.27

MOON_SOURCE = "TheMoon.png"

# How much of its own colour the unlit side keeps: earthshine, matched to the mean the
# photographic new moon holds, so a new pixel moon is a disc against the stars rather
# than a hole in them.
EARTHSHINE = 0.10

# The art's own pixel, in source pixels, so the terminator steps in the same blocks the
# picture is drawn in.
MOON_BLOCK = 12

# The moon is drawn far larger than it ships. Eight faces at the art's own 1197 square is six
# megabytes of mod for a body seven degrees wide; halved and halved again it is still twice the
# photographic set's 256, which is what the eyepiece asks of it, and the blocks survive the
# reduction because it is a box average over whole ones.
MOON_SIZE = 512

# The eight faces in the order Vintage Story's moon phase runs, with the file name the
# renderer asks for. The angle is the phase itself: 0 at new, half a turn at full.
MOON_FACES: tuple[tuple[str, float], ...] = (
    ("moon-new.png", 0.0),
    ("moon-waxing-crescent.png", 0.25 * math.pi),
    ("moon-first-quarter.png", 0.5 * math.pi),
    ("moon-waxing-gibbous.png", 0.75 * math.pi),
    ("moon-full.png", math.pi),
    ("moon-waning-gibbous.png", 1.25 * math.pi),
    ("moon-last-quarter.png", 1.5 * math.pi),
    ("moon-waning-crescent.png", 1.75 * math.pi),
)


def load(name: str) -> Image.Image:
    return Image.open(SOURCE_DIR / name).convert("RGBA")


def ink_bounds(image: Image.Image) -> tuple[int, int, int, int]:
    """The box the body actually occupies, ignoring the canvas it floats on."""
    alpha = np.asarray(image.getchannel("A"))
    rows = np.where(alpha.max(axis=1) > ALPHA_FLOOR)[0]
    columns = np.where(alpha.max(axis=0) > ALPHA_FLOOR)[0]
    if rows.size == 0 or columns.size == 0:
        raise ValueError("The picture is empty.")

    return int(columns[0]), int(rows[0]), int(columns[-1]) + 1, int(rows[-1]) + 1


def square_around(image: Image.Image, centre: tuple[float, float], side: int) -> Image.Image:
    """A square cut of the source, centred where the body is, padded where the source ends."""
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.paste(
        image,
        (round(side * 0.5 - centre[0]), round(side * 0.5 - centre[1])),
    )
    return square


def square_globe(source: str, destination: str) -> None:
    image = load(source)
    left, top, right, bottom = ink_bounds(image)
    side = max(right - left, bottom - top)
    centre = ((left + right) * 0.5, (top + bottom) * 0.5)
    square_around(image, centre, side).save(OUT_DIR / destination)
    print(f"{destination}: {side}x{side}")


def square_saturn() -> None:
    image = load(SATURN_SOURCE)
    side = round(SATURN_GLOBE_DIAMETER * SATURN_IMAGE_WIDTH_IN_DIAMETERS)
    square_around(image, SATURN_GLOBE_CENTRE, side).save(OUT_DIR / "saturn.png")
    print(f"saturn.png: {side}x{side}")


def lit_mask(side: int, block: float, phase: float) -> np.ndarray:
    """
    Which of the disc is in sunlight, as the sky sees it.

    The terminator is the day-night circle seen edge-on, which is an ellipse across the
    face whose half-width is the cosine of the phase. A waxing face is lit on the right
    of the picture and a waning one on the left, which is the convention the renderer
    turns the picture by so the bright limb points at the sun.
    """
    blocks = max(1, round(side / block))
    axis = (np.arange(blocks) + 0.5) / blocks * 2.0 - 1.0
    x = axis[np.newaxis, :]
    y = axis[:, np.newaxis]

    # The half-chord of the disc at this height: the ellipse is the disc's own circle,
    # squashed by the cosine, so the terminator meets the limb at the poles.
    half_chord = np.sqrt(np.clip(1.0 - y * y, 0.0, None))
    terminator = math.cos(phase) * half_chord
    lit = x > terminator if phase <= math.pi else x < -terminator

    scaled = np.repeat(np.repeat(lit, math.ceil(side / blocks), axis=0), math.ceil(side / blocks), axis=1)
    return scaled[:side, :side]


def cut_moon_faces() -> None:
    image = load(MOON_SOURCE)
    left, top, right, bottom = ink_bounds(image)
    side = max(right - left, bottom - top)
    centre = ((left + right) * 0.5, (top + bottom) * 0.5)
    disc = square_around(image, centre, side).resize((MOON_SIZE, MOON_SIZE), Image.BOX)
    block = MOON_BLOCK * MOON_SIZE / side
    full = np.asarray(disc).astype(np.float32)

    for name, phase in MOON_FACES:
        face = full.copy()
        dim = np.where(lit_mask(MOON_SIZE, block, phase), 1.0, EARTHSHINE)[:, :, np.newaxis]
        face[:, :, :3] *= dim
        Image.fromarray(face.round().clip(0, 255).astype(np.uint8), "RGBA").save(OUT_DIR / name)
        print(f"{name}: {MOON_SIZE}x{MOON_SIZE}")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    for source, destination in GLOBES.items():
        square_globe(source, destination)

    square_saturn()
    cut_moon_faces()


if __name__ == "__main__":
    main()
