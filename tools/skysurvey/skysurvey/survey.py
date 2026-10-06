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

#: How bright the procedural ``milky-way.png`` this replaces measures, as 0..1 stored values.
#: Luminance is the plain mean of the three channels. ``MilkyWayVisibility``'s opacity, twilight
#: floor and moon washout were tuned by eye against that map, so a survey map that matches these
#: numbers drops into the same tuning with no code change. They are fixed here rather than
#: re-measured on each run because the survey map replaces that PNG.
#:
#: Brightness only, not colour. The procedural map was bluish where Gaia measures the real sky as
#: redder, and the colour is the part of the survey worth keeping.
REFERENCE_STATS = {
    "median_luminance": 0.0771,
    "p99_luminance": 0.6137,
    "band_luminance": 0.3604,
    "high_latitude_luminance": 0.0512,
}

#: The procedural map's mean colour, for the report only: it is never fitted.
REFERENCE_MEAN_RGB = (0.1070, 0.1213, 0.1290)

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

    The gamma after the asinh is there because the real sky has more contrast than the model it
    replaces. With asinh alone, a curve that matched the band's brightness left the sky between
    the band and the poles about a quarter too dark, and one that matched the faint sky made the
    band too bright. The gamma lifts the faint end without moving the bright one.

    Saturation pulls each pixel's channel ratios towards grey without touching its intensity, so
    it never moves the brightness calibration. It is needed because the ratios are linear-light
    ratios shown as encoded values, which exaggerates them: at full saturation the bulge comes out
    salmon and the Magellanic Clouds magenta. The eye at night sees the band nearly colourless anyway.
    """

    black: float
    white: float
    softening: float
    gamma: float = 1.0
    saturation: float = 1.0

    def apply(self, image: np.ndarray) -> np.ndarray:
        intensity = luminance(image)
        above_black = np.clip(intensity - self.black, 0.0, None)
        range_ = max(self.white - self.black, np.finfo(np.float32).tiny)
        stretched = np.arcsinh(above_black / (self.softening * range_)) / np.arcsinh(1.0 / self.softening)
        stretched = np.clip(stretched, 0.0, None) ** self.gamma
        with np.errstate(divide="ignore", invalid="ignore"):
            ratios = np.where(intensity[..., None] > 0.0, np.clip(image, 0.0, None) / intensity[..., None], 1.0)
        # Ratios average to one across the channels, so moving them towards one keeps intensity.
        ratios = 1.0 + self.saturation * (ratios - 1.0)
        result = stretched[..., None] * ratios
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
    return {
        "median_luminance": float(np.median(lum)),
        "p99_luminance": float(np.percentile(lum, 99.0)),
        "band_luminance": float(lum[np.abs(latitude) <= 10.0].mean()),
        "high_latitude_luminance": float(lum[np.abs(latitude) > 45.0].mean()),
    }


def mean_rgb(encoded: np.ndarray) -> tuple[float, float, float]:
    red, green, blue = encoded.reshape(-1, 3).mean(axis=0)
    return float(red), float(green), float(blue)


def calibration_error(stats: dict[str, float], reference: dict[str, float] = REFERENCE_STATS) -> float:
    """Worst relative miss across the reference statistics."""
    return max(abs(stats[key] - target) / target for key, target in reference.items())


def fit_stretch(
    image: np.ndarray,
    reference: dict[str, float] = REFERENCE_STATS,
    black_percentiles: np.ndarray | None = None,
    softenings: np.ndarray | None = None,
    gammas: np.ndarray | None = None,
    saturation: float = 1.0,
) -> tuple[Stretch, dict[str, float], float]:
    """Searches black point, softening and gamma for the stretch that best matches the reference.

    The white point is not searched: it is pinned at a high percentile so only the brightest
    star-cloud cores clip. A small grid is enough because the error surface is smooth, and it
    keeps the result reproducible run to run, which an optimiser started from noise would not.
    """
    if black_percentiles is None:
        # Low percentiles only: on the real sky the best black point sits at or near zero, because
        # the reference keeps a glow over the whole sphere.
        black_percentiles = np.linspace(0.0, 30.0, 7)
    if softenings is None:
        softenings = np.geomspace(0.003, 3.0, 30)
    if gammas is None:
        gammas = np.linspace(0.3, 2.0, 35)

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
            # The gamma acts on intensity alone, so one asinh pass serves every gamma: raise the
            # stretched intensity and rescale, rather than rerunning the stretch thirty times.
            base = Stretch(black=float(black), white=white, softening=float(softening), saturation=saturation).apply(image)
            base_intensity = luminance(base)
            for gamma in gammas:
                with np.errstate(divide="ignore", invalid="ignore"):
                    scale = np.where(base_intensity > 0.0, base_intensity ** (gamma - 1.0), 0.0)
                stats = measure(base * scale[..., None])
                error = calibration_error(stats, reference)
                if best is None or error < best[2]:
                    stretch = Stretch(
                        black=float(black),
                        white=white,
                        softening=float(softening),
                        gamma=float(gamma),
                        saturation=saturation,
                    )
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
