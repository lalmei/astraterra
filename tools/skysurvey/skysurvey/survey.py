"""Turns a linear all-sky survey map into the textures the sky renderer draws.

Every function here works on equirectangular images in **galactic** coordinates, using the same
convention as ``milkywaygen`` and ``MilkyWayRenderModel``: the first row is latitude +90 deg and
the last is -90 deg, the left edge is longitude +180 deg, the centre is 0 deg and the right edge is
-180 deg. Images are float arrays shaped ``(height, width, 3)`` with width twice height.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Iterator

import numpy as np

#: What the committed procedural ``milky-way.png`` measures, as 0..1 stored values. Luminance is
#: the plain mean of the three channels. ``MilkyWayVisibility``'s opacity, twilight floor and
#: moon washout were tuned by eye against this map, so a survey map that matches these numbers
#: drops into the same tuning with no code change. These are fixed here rather than re-measured
#: from the PNG on each run because the survey map replaces that PNG.
REFERENCE_STATS = {
    "mean_red": 0.1070,
    "mean_green": 0.1213,
    "mean_blue": 0.1290,
    "median_luminance": 0.0771,
    "p99_luminance": 0.6137,
    "band_luminance": 0.3604,
    "high_latitude_luminance": 0.0512,
}

#: Where the white point sits, as a luminance percentile of the linear map. Above it the stretch
#: clips, which only touches the cores of the brightest star clouds.
WHITE_PERCENTILE = 99.95

#: Large Magellanic Cloud, the feature that pins down which way up the map is.
LMC_GALACTIC = (280.47, -32.89)


class OrientationError(ValueError):
    """The map is mirrored or upside down relative to the renderer's convention."""


def galactic_axes(height: int, width: int) -> tuple[np.ndarray, np.ndarray]:
    """Longitude and latitude at each pixel centre, as broadcastable row and column vectors."""
    longitude = 180.0 - (np.arange(width) + 0.5) * (360.0 / width)
    latitude = 90.0 - (np.arange(height) + 0.5) * (180.0 / height)
    return longitude[None, :], latitude[:, None]


def luminance(image: np.ndarray) -> np.ndarray:
    return image.mean(axis=-1)


def box_mean(
    image: np.ndarray,
    longitude_deg: float,
    latitude_deg: float,
    half_width_deg: float,
    half_height_deg: float,
) -> float:
    """Mean luminance in a galactic box, with longitude wrapping across +-180 deg."""
    height, width = image.shape[:2]
    longitude, latitude = galactic_axes(height, width)
    delta_longitude = (longitude - longitude_deg + 180.0) % 360.0 - 180.0
    mask = (np.abs(delta_longitude) <= half_width_deg) & (np.abs(latitude - latitude_deg) <= half_height_deg)
    if not mask.any():
        raise ValueError("The box is smaller than one pixel at this resolution.")
    return float(luminance(image)[mask].mean())


def check_orientation(image: np.ndarray) -> dict[str, float]:
    """Refuses a map that is mirrored or upside down, using features that fix both axes.

    The galactic centre has to be brighter than the anticentre: that is the horizontal half-turn.
    The LMC has to be brighter than its reflection across the plane, which is upside-down, and
    brighter than its reflection across the centre line, which is left-right mirroring. A real
    sky passes all three by a wide margin. Each wrong orientation fails at least one.
    """
    lmc_longitude, lmc_latitude = LMC_GALACTIC
    readings = {
        "centre": box_mean(image, 0.0, 0.0, 10.0, 8.0),
        "anticentre": box_mean(image, 180.0, 0.0, 10.0, 8.0),
        "lmc": box_mean(image, lmc_longitude, lmc_latitude, 4.0, 3.0),
        "lmc_flipped_latitude": box_mean(image, lmc_longitude, -lmc_latitude, 4.0, 3.0),
        "lmc_mirrored_longitude": box_mean(image, -lmc_longitude, lmc_latitude, 4.0, 3.0),
    }
    failures = []
    if readings["centre"] <= readings["anticentre"]:
        failures.append("the galactic centre is not brighter than the anticentre (half a turn out?)")
    if readings["lmc"] <= readings["lmc_flipped_latitude"]:
        failures.append("the LMC is not below the plane (upside down?)")
    if readings["lmc"] <= readings["lmc_mirrored_longitude"]:
        failures.append("the LMC is on the wrong side of the centre line (mirrored?)")
    if failures:
        raise OrientationError("; ".join(failures) + f". Readings: {readings}")
    return readings


def downsample(image: np.ndarray, factor: int) -> np.ndarray:
    """Box-filters by an integer factor. Run it on *linear* light, before any stretch.

    Averaging after the stretch would average already-compressed values, and the result would
    come out darker than the full-resolution tiles drawn over it: a visible square at the edge of
    the scope.
    """
    if factor == 1:
        return image
    height, width, channels = image.shape
    if height % factor or width % factor:
        raise ValueError(f"{width}x{height} does not divide by {factor}.")
    return image.reshape(height // factor, factor, width // factor, factor, channels).mean(
        axis=(1, 3), dtype=np.float64
    ).astype(np.float32)


@dataclass(frozen=True)
class Stretch:
    """An asinh stretch on intensity that keeps each pixel's colour (Lupton et al. 2004).

    Stretching each channel separately would push every bright pixel towards white and every faint
    one towards grey. Stretching the intensity and scaling all three channels by the same factor
    keeps the colour Gaia measured, from the amber bulge to the bluer outer disc.
    """

    black: float
    white: float
    softening: float

    def apply(self, image: np.ndarray) -> np.ndarray:
        intensity = luminance(image)
        above_black = np.clip(intensity - self.black, 0.0, None)
        range_ = max(self.white - self.black, np.finfo(np.float32).tiny)
        stretched = np.arcsinh(above_black / (self.softening * range_)) / np.arcsinh(1.0 / self.softening)
        with np.errstate(divide="ignore", invalid="ignore"):
            scale = np.where(intensity > 0.0, stretched / intensity, 0.0)
        result = np.clip(image, 0.0, None) * scale[..., None]
        # Over-range pixels are scaled back as a whole rather than clipped per channel, which
        # would bleach their colour.
        peak = result.max(axis=-1, keepdims=True)
        result = np.where(peak > 1.0, result / np.maximum(peak, 1.0), result)
        return result.astype(np.float32)


def measure(encoded: np.ndarray) -> dict[str, float]:
    """The statistics ``REFERENCE_STATS`` holds, measured on a 0..1 stored image."""
    lum = luminance(encoded)
    _, latitude = galactic_axes(*encoded.shape[:2])
    latitude = np.broadcast_to(latitude, lum.shape)
    mean_rgb = encoded.reshape(-1, 3).mean(axis=0)
    return {
        "mean_red": float(mean_rgb[0]),
        "mean_green": float(mean_rgb[1]),
        "mean_blue": float(mean_rgb[2]),
        "median_luminance": float(np.median(lum)),
        "p99_luminance": float(np.percentile(lum, 99.0)),
        "band_luminance": float(lum[np.abs(latitude) <= 10.0].mean()),
        "high_latitude_luminance": float(lum[np.abs(latitude) > 45.0].mean()),
    }


def calibration_error(stats: dict[str, float], reference: dict[str, float] = REFERENCE_STATS) -> float:
    """Worst relative miss across the reference statistics."""
    return max(abs(stats[key] - target) / target for key, target in reference.items())


def fit_stretch(
    image: np.ndarray,
    reference: dict[str, float] = REFERENCE_STATS,
    black_percentiles: np.ndarray | None = None,
    softenings: np.ndarray | None = None,
) -> tuple[Stretch, dict[str, float], float]:
    """Searches black point and softening for the stretch that best matches the reference.

    The white point is not searched: it is pinned at a high percentile so only the brightest
    star-cloud cores clip. A small grid is enough because the error surface is smooth, and it
    keeps the result reproducible run to run, which an optimiser started from noise would not.
    """
    if black_percentiles is None:
        black_percentiles = np.linspace(0.0, 60.0, 31)
    if softenings is None:
        softenings = np.geomspace(0.003, 3.0, 40)

    lum = luminance(image)
    white = float(np.percentile(lum, WHITE_PERCENTILE))
    # Zero is a candidate as well as the percentiles: the reference keeps a faint glow over the
    # whole sky (its median is not black), and even the 0th percentile would take that away.
    blacks = np.concatenate([[0.0], np.percentile(lum, black_percentiles)])

    best: tuple[Stretch, dict[str, float], float] | None = None
    for black in blacks:
        if black >= white:
            continue
        for softening in softenings:
            stretch = Stretch(black=float(black), white=white, softening=float(softening))
            stats = measure(stretch.apply(image))
            error = calibration_error(stats, reference)
            if best is None or error < best[2]:
                best = (stretch, stats, error)

    if best is None:
        raise ValueError("The map has no dynamic range to stretch.")
    return best


def to_bytes(encoded: np.ndarray) -> np.ndarray:
    return np.round(np.clip(encoded, 0.0, 1.0) * 255.0).astype(np.uint8)


@dataclass(frozen=True)
class Tile:
    """One square of the high-resolution map, with a gutter of its neighbours' texels around it.

    The galactic bounds are of the interior only. The gutter is there so linear filtering at a
    tile's edge blends into the next tile instead of clamping, which would show the tile grid.
    """

    row: int
    column: int
    longitude_max_deg: float
    longitude_min_deg: float
    latitude_max_deg: float
    latitude_min_deg: float
    pixels: np.ndarray

    @property
    def name(self) -> str:
        return f"r{self.row:02d}-c{self.column:02d}"


def tile_rows_within(height: int, tile_size: int, max_abs_latitude_deg: float) -> list[int]:
    """Rows of tiles whose whole extent lies within the latitude limit."""
    rows = height // tile_size
    degrees_per_row = 180.0 * tile_size / height
    kept = []
    for row in range(rows):
        top = 90.0 - row * degrees_per_row
        bottom = top - degrees_per_row
        if max(abs(top), abs(bottom)) <= max_abs_latitude_deg + 1e-9:
            kept.append(row)
    return kept


def cut_tiles(
    image: np.ndarray,
    tile_size: int = 512,
    gutter: int = 1,
    max_abs_latitude_deg: float = 33.75,
) -> Iterator[Tile]:
    """Cuts the band into square tiles, each padded with a gutter from its neighbours.

    Longitude wraps, so the tiles at the +-180 deg seam take their gutter from the far edge of the
    map. Latitude does not: past the top or bottom of the map the edge row is repeated.
    """
    height, width = image.shape[:2]
    if height % tile_size or width % tile_size:
        raise ValueError(f"{width}x{height} does not divide into {tile_size}-pixel tiles.")

    degrees_per_pixel = 360.0 / width
    for row in tile_rows_within(height, tile_size, max_abs_latitude_deg):
        top = row * tile_size
        row_indices = np.clip(np.arange(top - gutter, top + tile_size + gutter), 0, height - 1)
        for column in range(width // tile_size):
            left = column * tile_size
            column_indices = np.arange(left - gutter, left + tile_size + gutter) % width
            pixels = image[np.ix_(row_indices, column_indices)]
            yield Tile(
                row=row,
                column=column,
                longitude_max_deg=180.0 - left * degrees_per_pixel,
                longitude_min_deg=180.0 - (left + tile_size) * degrees_per_pixel,
                latitude_max_deg=90.0 - top * degrees_per_pixel,
                latitude_min_deg=90.0 - (top + tile_size) * degrees_per_pixel,
                pixels=pixels,
            )
