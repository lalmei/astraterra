"""Bakes the Milky Way glow into an equirectangular galactic-coordinate texture.

The output is one PNG the sky renderer wraps onto a sphere. The horizontal axis is galactic
longitude, running 180 deg on the left edge through 0 deg at the centre to -180 deg on the
right, so the galactic centre sits in the middle of the image where it can be judged. The
vertical axis is galactic latitude, +90 deg on the first row. The renderer rotates it into
the sky; nothing about the observer, the date or the horizon is baked in here.
"""

from __future__ import annotations

import argparse
from pathlib import Path

import numpy as np
from PIL import Image

from milkywaygen.galaxy import (
    GalaxyModel,
    clumping_field,
    integrate_surface_brightness,
    latitude_taper,
)

#: Per-channel dust opacity. Dust scatters blue light out of the line of sight harder than
#: red, which is why the band turns visibly amber where it is thickest and stays neutral
#: where it is thin. One number per channel is the whole of the reddening model.
CHANNEL_DUST_SCALE = (0.74, 1.0, 1.38)

#: Per-channel bulge strength. The old stars of the bulge are intrinsically redder than the
#: disc's, and this is that difference — separate from reddening, and it survives where
#: there is no dust to redden anything.
CHANNEL_BULGE_SCALE = (1.12, 1.0, 0.84)


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Generate the AstraTerra Milky Way glow texture.")
    parser.add_argument("--width", type=int, default=2048, help="Texture width in pixels; height is half.")
    parser.add_argument("--seed", type=int, default=20260822, help="Seed for the dust clumping field.")
    parser.add_argument(
        "--clumping",
        type=float,
        default=1.0,
        help="How strongly dust clouds vary from the smooth disc. 0 gives a featureless band.",
    )
    parser.add_argument(
        "--shoulder",
        type=float,
        default=0.55,
        help="Brightness that maps to half scale. Lower widens the band and flattens the "
        "bulge; higher leaves a thin bright ridge in a dark sky.",
    )
    parser.add_argument(
        "--gamma",
        type=float,
        default=1.15,
        help="Encoding gamma. The stored value is tone-mapped brightness ** (1/gamma), which "
        "keeps the faint outer band off the bottom of an 8-bit channel.",
    )
    parser.add_argument("--steps", type=int, default=320, help="Ray-march steps per pixel.")
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("../../assets/astraterra/textures/environment/milky-way.png"),
        help="Destination PNG.",
    )
    return parser


def render(
    width: int,
    seed: int,
    clumping: float,
    steps: int,
    shoulder: float,
    gamma: float,
) -> Image.Image:
    height = width // 2
    longitude = np.linspace(180.0, -180.0, width, endpoint=False)[None, :]
    latitude = np.linspace(90.0, -90.0, height)[:, None]

    modulation = None
    if clumping > 0:
        # Clumping is applied to the dust, not to the starlight: clouds block light, and a
        # cloud that both blocked and emitted would show up as a bright knot with a dark
        # core. Exponentiating keeps the density positive and makes the dense side denser
        # than the thin side is thin, which is how real clouds are distributed.
        modulation = np.exp(
            clumping * clumping_field((height, width), seed) * latitude_taper(latitude)
        )

    channels = []
    defaults = GalaxyModel()
    for dust_scale, bulge_scale in zip(CHANNEL_DUST_SCALE, CHANNEL_BULGE_SCALE):
        model = GalaxyModel(
            dust_opacity=defaults.dust_opacity * dust_scale,
            bulge_strength=defaults.bulge_strength * bulge_scale,
            steps=steps,
        )
        channels.append(integrate_surface_brightness(longitude, latitude, model, modulation))

    return encode(np.stack(channels, axis=-1), shoulder=shoulder, gamma=gamma)


def encode(brightness: np.ndarray, shoulder: float, gamma: float) -> Image.Image:
    """Turns physical surface brightness into eight bits per channel.

    Two things have to happen, and the order matters. The disc's light does not fall to zero
    at the galactic poles — you are inside the disc, so some of it arrives from every
    direction — but that pedestal is exactly the part the eye never sees as *the Milky Way*,
    so it is subtracted first and the sphere away from the band goes properly black. What is
    left spans a factor of several hundred between the outer band and the bulge, which no
    additive texture can carry, so it is rolled off rather than clipped: the bulge keeps its
    core, and the faint band it would otherwise crush stays readable.
    """
    pedestal = brightness.reshape(-1, brightness.shape[-1]).min(axis=0)
    above_pedestal = np.clip(brightness - pedestal, 0.0, None)
    if above_pedestal.max() <= 0:
        raise ValueError("The model produced no light; check the galaxy parameters.")

    rolled_off = above_pedestal / (above_pedestal + shoulder)
    encoded = np.clip(rolled_off, 0.0, 1.0) ** (1.0 / gamma)
    return Image.fromarray(np.round(encoded * 255.0).astype(np.uint8), mode="RGB")


def main() -> None:
    args = build_parser().parse_args()
    image = render(
        width=args.width,
        seed=args.seed,
        clumping=args.clumping,
        steps=args.steps,
        shoulder=args.shoulder,
        gamma=args.gamma,
    )
    args.output.parent.mkdir(parents=True, exist_ok=True)
    image.save(args.output, optimize=True)
    print(f"Wrote {args.output} ({image.width}x{image.height})")


if __name__ == "__main__":
    main()
