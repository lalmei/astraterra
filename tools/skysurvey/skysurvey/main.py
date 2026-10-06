"""Converts NASA SVS Deep Star Maps 2020 into the Milky Way textures the mod ships.

Source: https://svs.gsfc.nasa.gov/4851 (public domain). Use the ``milkyway_2020_*_gal.exr``
files: galactic coordinates, with the Hipparcos and Tycho stars already removed so they are not
drawn twice over the catalog's own. Keep them under ``sources/``; they are not committed.
"""

from __future__ import annotations

import argparse
import json
from io import BytesIO
from pathlib import Path

import numpy as np
from PIL import Image

from skysurvey.survey import (
    REFERENCE_MEAN_RGB,
    REFERENCE_STATS,
    Stretch,
    calibration_error,
    check_orientation,
    cut_tiles,
    downsample,
    fit_stretch,
    mean_rgb,
    measure,
    to_bytes,
)

#: Measured on the 16k source: 192 tiles are ~43 MiB at JPEG q90 4:4:4, ~33 MiB at q85 and still
#: ~22 MiB at q80 4:2:0. Gaia's resolved stars are close to noise, and noise does not compress.
#: Above this, the tiles go in a separate optional asset mod rather than the main download.
TILE_BUDGET_BYTES = 15 * 1024 * 1024

#: How far the fitted map may miss the reference statistics before the run warns.
CALIBRATION_TOLERANCE = 0.10

#: The global map is fitted on a copy this wide: the statistics are global averages and
#: percentiles, which a 512-wide map already measures to within about a percent, and the fit
#: evaluates some seven thousand candidate stretches.
FIT_WIDTH = 512


def read_exr(path: Path) -> np.ndarray:
    """Reads an RGB OpenEXR file as linear float32, shaped (height, width, 3)."""
    try:
        import OpenEXR
    except ImportError as error:  # pragma: no cover - depends on the developer's environment
        raise SystemExit("Reading EXR needs the OpenEXR package: pip install OpenEXR") from error

    with OpenEXR.File(str(path)) as exr:
        channels = exr.channels()
        if "RGB" in channels:
            pixels = channels["RGB"].pixels
        else:
            pixels = np.stack([channels[name].pixels for name in ("R", "G", "B")], axis=-1)
    return np.asarray(pixels, dtype=np.float32)


def encode_png(encoded: np.ndarray) -> bytes:
    buffer = BytesIO()
    Image.fromarray(to_bytes(encoded), mode="RGB").save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def encode_jpeg(encoded: np.ndarray, quality: int) -> bytes:
    """JPEG at full chroma resolution.

    4:4:4 because a resolved star is about one texel, and chroma subsampling would smear its colour
    into its neighbours. ``optimize`` stays off: Pillow 12.3 with libjpeg-turbo 3 fails with
    "broken data stream" when 4:4:4 and optimised Huffman tables are written to memory together,
    and the tables only save about 8%.
    """
    buffer = BytesIO()
    Image.fromarray(to_bytes(encoded), mode="RGB").save(buffer, format="JPEG", quality=quality, subsampling=0)
    return buffer.getvalue()


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Convert NASA SVS Milky Way maps into AstraTerra textures.")
    parser.add_argument("--source", type=Path, required=True, help="milkyway_2020_<N>k_gal.exr")
    parser.add_argument(
        "--global-width",
        type=int,
        default=4096,
        help="Width of the naked-eye map; the source is box-filtered down to it in linear light.",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("../../assets/astraterra/textures/environment/milky-way.jpg"),
        help="The naked-eye map. JPEG unless the name ends in .png; a 4k PNG of Gaia grain is 16 MiB.",
    )
    parser.add_argument("--tiles-dir", type=Path, help="Also cut scoped tiles from the full-resolution source.")
    parser.add_argument("--tile-size", type=int, default=512)
    parser.add_argument("--max-abs-latitude", type=float, default=33.75)
    parser.add_argument("--jpeg-quality", type=int, default=90, help="Quality of the naked-eye map.")
    parser.add_argument("--tile-jpeg-quality", type=int, default=85, help="Quality of the scoped tiles.")
    parser.add_argument(
        "--saturation",
        type=float,
        default=0.5,
        help="Colour strength, 0 grey to 1 Gaia's linear ratios; brightness is unaffected.",
    )
    parser.add_argument("--report", type=Path, help="Write the calibration and size report as JSON.")
    return parser


def main(argv: list[str] | None = None) -> None:
    args = build_parser().parse_args(argv)
    source = read_exr(args.source)
    height, width = source.shape[:2]
    print(f"Read {args.source} ({width}x{height})")
    if width != 2 * height:
        raise SystemExit("The source is not a full-sky equirectangular map (width must be twice height).")

    readings = check_orientation(downsample(source, max(1, width // FIT_WIDTH)))
    print(f"Orientation OK: {readings}")

    if width % args.global_width:
        raise SystemExit(f"--global-width {args.global_width} does not divide the source width {width}.")
    global_linear = downsample(source, width // args.global_width)

    # Fitted on every n-th pixel, not on a box-filtered copy. The stretch is not linear, so a
    # grainy pixel stretched on its own comes out darker than the average of its neighbours
    # stretched once, and a fit to an averaged copy misses the real map by a quarter at high
    # latitude. A strided sample keeps the shipped map's own distribution of pixel values.
    stride = max(1, args.global_width // FIT_WIDTH)
    stretch, _, error = fit_stretch(global_linear[::stride, ::stride], saturation=args.saturation)
    global_encoded = stretch.apply(global_linear)
    global_stats = measure(global_encoded)
    error = calibration_error(global_stats)
    print(f"Stretch: {stretch}; worst miss against the reference {error:.1%} (measured on the full map)")
    for key, target in REFERENCE_STATS.items():
        print(f"  {key:>24}: {global_stats[key]:.4f} (reference {target:.4f})")
    global_rgb = mean_rgb(global_encoded)
    print(
        "  mean RGB (not fitted): "
        + ", ".join(f"{value:.4f}" for value in global_rgb)
        + " (procedural map "
        + ", ".join(f"{value:.4f}" for value in REFERENCE_MEAN_RGB)
        + ")"
    )
    if error > CALIBRATION_TOLERANCE:
        print(
            f"WARNING: the stretch misses the reference by more than {CALIBRATION_TOLERANCE:.0%}; "
            "MilkyWayVisibility's opacity tuning may need revisiting."
        )

    if args.output.suffix.lower() == ".png":
        global_bytes = encode_png(global_encoded)
    else:
        global_bytes = encode_jpeg(global_encoded, args.jpeg_quality)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_bytes(global_bytes)
    print(f"Wrote {args.output} ({args.global_width}x{args.global_width // 2}, {len(global_bytes) / 2**20:.1f} MiB)")

    report: dict[str, object] = {
        "source": args.source.name,
        "stretch": stretch.__dict__,
        "calibration_error": error,
        "global_stats": global_stats,
        "global_mean_rgb": global_rgb,
        "global_bytes": len(global_bytes),
    }

    if args.tiles_dir is not None:
        report["tiles"] = write_tiles(args, source, stretch)

    if args.report is not None:
        args.report.write_text(json.dumps(report, indent=2) + "\n")


def write_tiles(args: argparse.Namespace, source: np.ndarray, stretch: Stretch) -> dict[str, object]:
    """Cuts, stretches and writes the scoped tiles with the global map's stretch.

    One stretch for everything: fitting each tile separately would give every tile its own
    brightness, and the scope would show the grid.
    """
    args.tiles_dir.mkdir(parents=True, exist_ok=True)
    manifest = []
    jpeg_bytes = 0
    png_bytes = 0
    for tile in cut_tiles(source, args.tile_size, gutter=1, max_abs_latitude_deg=args.max_abs_latitude):
        encoded = stretch.apply(tile.pixels)
        jpeg = encode_jpeg(encoded, args.tile_jpeg_quality)
        jpeg_bytes += len(jpeg)
        png_bytes += len(encode_png(encoded))
        (args.tiles_dir / f"{tile.name}.jpg").write_bytes(jpeg)
        manifest.append(
            {
                "name": tile.name,
                "longitudeMaxDeg": tile.longitude_max_deg,
                "longitudeMinDeg": tile.longitude_min_deg,
                "latitudeMaxDeg": tile.latitude_max_deg,
                "latitudeMinDeg": tile.latitude_min_deg,
            }
        )

    (args.tiles_dir / "tiles.json").write_text(
        json.dumps(
            {"tileSize": args.tile_size, "gutter": 1, "sourceWidth": source.shape[1], "tiles": manifest},
            indent=2,
        )
        + "\n"
    )
    ship_in_main_mod = jpeg_bytes <= TILE_BUDGET_BYTES
    print(
        f"Wrote {len(manifest)} tiles to {args.tiles_dir}: "
        f"JPEG q{args.tile_jpeg_quality} {jpeg_bytes / 2**20:.1f} MiB (PNG would be {png_bytes / 2**20:.1f} MiB). "
        + ("Within budget: ship in the main mod." if ship_in_main_mod else "Over budget: ship as an optional asset mod.")
    )
    return {
        "count": len(manifest),
        "jpeg_bytes": jpeg_bytes,
        "png_bytes": png_bytes,
        "budget_bytes": TILE_BUDGET_BYTES,
        "ship_in_main_mod": ship_in_main_mod,
    }


if __name__ == "__main__":
    main()
