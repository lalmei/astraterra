from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
OUT_DIR = ROOT / "assets" / "astraterra" / "textures" / "environment"
SIZE = 32
SUPERSAMPLE = 4
ReferenceAlpha = tuple[np.ndarray, int]
RAY_VARIANTS: tuple[tuple[str, int, bool], ...] = (
    ("star-rays-4-smooth.png", 4, False),
    ("star-rays-8-smooth.png", 8, False),
    ("star-rays-12-smooth.png", 12, False),
    ("star-rays-smooth.png", 8, False),
    ("star-rays-4-crisp.png", 4, True),
    ("star-rays-8-crisp.png", 8, True),
    ("star-rays-12-crisp.png", 12, True),
)
REFERENCE_ALPHA_HISTOGRAM: dict[int, int] = {
    0: 204,
    1: 67,
    2: 46,
    3: 40,
    4: 61,
    5: 18,
    6: 20,
    7: 21,
    8: 19,
    9: 14,
    10: 11,
    11: 13,
    12: 8,
    13: 9,
    14: 23,
    15: 7,
    16: 9,
    17: 13,
    18: 7,
    19: 8,
    20: 6,
    21: 8,
    22: 10,
    23: 15,
    24: 7,
    25: 4,
    26: 2,
    27: 6,
    28: 2,
    29: 4,
    30: 3,
    31: 5,
    32: 2,
    33: 7,
    34: 1,
    35: 6,
    36: 4,
    37: 4,
    38: 5,
    39: 7,
    40: 5,
    41: 2,
    43: 2,
    44: 4,
    45: 5,
    46: 2,
    47: 2,
    49: 4,
    50: 7,
    51: 2,
    52: 6,
    53: 3,
    54: 3,
    55: 1,
    56: 1,
    57: 2,
    58: 2,
    59: 2,
    60: 5,
    61: 2,
    62: 2,
    63: 3,
    64: 1,
    66: 2,
    67: 3,
    68: 2,
    69: 2,
    71: 2,
    72: 2,
    73: 3,
    75: 1,
    76: 3,
    77: 1,
    79: 1,
    80: 1,
    81: 1,
    82: 2,
    83: 1,
    84: 1,
    85: 1,
    86: 1,
    87: 1,
    89: 6,
    90: 5,
    91: 2,
    92: 1,
    93: 3,
    95: 1,
    97: 1,
    98: 1,
    99: 3,
    100: 3,
    101: 1,
    102: 1,
    103: 3,
    104: 6,
    105: 1,
    106: 1,
    107: 2,
    108: 2,
    110: 1,
    111: 1,
    112: 1,
    113: 4,
    114: 1,
    115: 1,
    116: 2,
    117: 1,
    119: 2,
    122: 3,
    123: 1,
    124: 2,
    125: 1,
    126: 1,
    128: 1,
    129: 1,
    130: 2,
    131: 1,
    132: 3,
    133: 1,
    135: 3,
    139: 1,
    142: 1,
    144: 3,
    145: 2,
    146: 1,
    148: 2,
    149: 1,
    151: 1,
    152: 1,
    153: 1,
    154: 1,
    155: 1,
    156: 1,
    157: 1,
    158: 1,
    159: 1,
    160: 2,
    162: 1,
    164: 2,
    166: 1,
    167: 1,
    170: 1,
    171: 1,
    175: 1,
    177: 2,
    179: 1,
    180: 2,
    183: 2,
    185: 1,
    186: 1,
    191: 1,
    192: 2,
    194: 2,
    195: 1,
    196: 1,
    199: 1,
    200: 2,
    203: 2,
    204: 2,
    205: 1,
    207: 1,
    209: 1,
    210: 1,
    211: 1,
    212: 2,
    213: 1,
    214: 1,
    215: 1,
    216: 1,
    217: 1,
    218: 2,
    219: 1,
    220: 1,
    221: 2,
    222: 2,
    223: 3,
    224: 1,
    225: 1,
    226: 1,
    227: 2,
    228: 4,
    229: 2,
    230: 35,
}


def normalize(values: np.ndarray) -> np.ndarray:
    values = np.clip(values, 0.0, None)
    max_value = float(values.max())
    if max_value <= 0.0:
        return values
    return values / max_value


def gaussian(radius: np.ndarray, sigma: float) -> np.ndarray:
    return np.exp(-(radius * radius) / (2.0 * sigma * sigma))


def make_mask(alpha: np.ndarray) -> Image.Image:
    alpha = np.clip(alpha, 0.0, 1.0)
    return Image.fromarray(np.round(alpha * 255.0).astype(np.uint8), mode="L")


def coordinate_grid(size: int = SIZE) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    coords = (np.arange(size, dtype=np.float32) + 0.5 - (size / 2.0)) / (size / 2.0)
    x, y = np.meshgrid(coords, coords)
    radius = np.sqrt((x * x) + (y * y))
    return x, y, radius


def downsample_alpha(alpha: np.ndarray) -> np.ndarray:
    source_size = alpha.shape[0]
    factor = source_size // SIZE
    return alpha.reshape(SIZE, factor, SIZE, factor).mean(axis=(1, 3))


def reference_alpha_distribution() -> ReferenceAlpha:
    visible_values: list[float] = []
    transparent_count = 0
    for alpha, count in sorted(REFERENCE_ALPHA_HISTOGRAM.items()):
        if alpha <= 0:
            transparent_count += count
        else:
            visible_values.extend([alpha / 255.0] * count)

    return np.asarray(visible_values, dtype=np.float32), transparent_count


def match_alpha_distribution(
    alpha: np.ndarray, reference_alpha: ReferenceAlpha
) -> np.ndarray:
    alpha = np.clip(alpha, 0.0, 1.0)
    reference_visible_alpha, reference_transparent_count = reference_alpha
    output = alpha.copy()
    transparent_count = int((output <= 0.0).sum())
    transparent_target = min(
        output.size, max(transparent_count, reference_transparent_count)
    )
    if transparent_target > transparent_count:
        flat = output.reshape(-1)
        visible_indexes = np.flatnonzero(flat > 0.0)
        indexes_to_clear = visible_indexes[
            np.argsort(flat[visible_indexes])[: transparent_target - transparent_count]
        ]
        flat[indexes_to_clear] = 0.0

    visible_mask = output > 0.0
    visible = output[visible_mask]
    if visible.size == 0:
        return output

    ranks = np.argsort(np.argsort(visible, kind="mergesort"), kind="mergesort")
    reference_indexes = np.round(
        ranks * (reference_visible_alpha.size - 1) / max(1, visible.size - 1)
    ).astype(np.int32)
    matched = np.zeros_like(alpha)
    matched[visible_mask] = reference_visible_alpha[reference_indexes]
    return matched


def angular_spikes(
    angle: np.ndarray,
    radius: np.ndarray,
    degrees: tuple[float, ...],
    width: float,
    falloff: float,
) -> np.ndarray:
    spikes = np.zeros_like(radius)
    for value in degrees:
        theta = np.deg2rad(value)
        spikes += np.exp(
            -(np.sin(angle - theta) ** 2) / (2.0 * width * width)
        ) * np.exp(-((radius / falloff) ** 1.65))
    return spikes


def make_ray_star(spike_count: int = 8, crisp: bool = False) -> np.ndarray:
    x, y, radius = coordinate_grid(SIZE * SUPERSAMPLE)
    angle = np.arctan2(y, x)
    extra_spike_width = crisp and spike_count > 4
    twelve_spike_width = crisp and spike_count >= 12
    spike_width = (
        (0.034 if twelve_spike_width else (0.028 if extra_spike_width else 0.022))
        if crisp
        else 0.038
    )
    vertical_width = (
        (0.035 if twelve_spike_width else (0.029 if extra_spike_width else 0.023))
        if crisp
        else 0.040
    )
    diagonal_width = (
        (0.038 if twelve_spike_width else (0.031 if extra_spike_width else 0.024))
        if crisp
        else 0.040
    )
    horizontal = np.exp(-(y * y) / (2.0 * spike_width * spike_width)) * np.exp(
        -((np.abs(x) / 0.92) ** 1.18)
    )
    vertical = np.exp(-(x * x) / (2.0 * vertical_width * vertical_width)) * np.exp(
        -((np.abs(y) / 0.72) ** 1.18)
    )
    diagonal = np.exp(
        -(np.sin((angle - (np.pi / 4.0))) ** 2)
        / (2.0 * diagonal_width * diagonal_width)
    ) * np.exp(-((radius / 0.54) ** 1.42))
    counter_diagonal = np.exp(
        -(np.sin((angle + (np.pi / 4.0))) ** 2)
        / (2.0 * diagonal_width * diagonal_width)
    ) * np.exp(-((radius / 0.48) ** 1.42))
    minor_spikes = angular_spikes(
        angle,
        radius,
        (30.0, 60.0, 120.0, 150.0),
        0.042
        if twelve_spike_width
        else (0.028 if extra_spike_width else (0.020 if crisp else 0.034)),
        0.58 if twelve_spike_width else (0.48 if extra_spike_width else 0.46),
    )
    core = gaussian(radius, 0.105 if crisp else 0.14)
    warm_core = gaussian(radius, 0.18 if crisp else 0.24)
    halo = gaussian(radius, 0.36 if crisp else 0.46)
    spike_boost = 1.55 if crisp else 1.0
    spikes = spike_boost * ((1.12 * horizontal) + (0.98 * vertical))
    if spike_count >= 8:
        spikes += (0.22 * diagonal) + (0.18 * counter_diagonal)
    if spike_count >= 12:
        spikes += (0.58 if crisp else 0.26) * minor_spikes
    alpha = (
        normalize((1.42 * core) + (0.26 * warm_core) + (0.07 * halo) + spikes)
        if crisp
        else normalize((1.55 * core) + (0.32 * warm_core) + (0.11 * halo) + spikes)
    )
    alpha = alpha ** (0.34 if twelve_spike_width else (0.46 if crisp else 0.92))
    return normalize(downsample_alpha(alpha))


def make_rotated_ray_star(
    spike_count: int,
    crisp: bool,
    reference_alpha: ReferenceAlpha,
) -> Image.Image:
    ray = make_mask(make_ray_star(spike_count, crisp))
    rotated = ray.rotate(
        45, resample=Image.Resampling.BICUBIC, center=(SIZE / 2.0, SIZE / 2.0)
    )
    ray_alpha = np.asarray(ray, dtype=np.float32) / 255.0
    rotated_alpha = np.asarray(rotated, dtype=np.float32) / 255.0
    rotated_weight = 0.0 if spike_count != 8 else 0.78
    blended_alpha = 1.0 - ((1.0 - ray_alpha) * (1.0 - (rotated_alpha * rotated_weight)))
    target_peak_alpha = 1.0 if crisp else 230.0 / 255.0
    blended_alpha = (
        normalize(blended_alpha) ** (0.44 if crisp else 0.58) * target_peak_alpha
    )
    blended_alpha = match_alpha_distribution(blended_alpha, reference_alpha)
    return make_mask(blended_alpha)


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    reference_alpha = reference_alpha_distribution()
    x, y, radius = coordinate_grid()

    base = gaussian(radius, 0.135)
    halo = gaussian(radius, 0.36)
    laplacian = np.abs(
        ((radius * radius) - (2.0 * 0.16 * 0.16)) / (0.16**4) * gaussian(radius, 0.16)
    )
    ring = normalize(laplacian) * gaussian(radius, 0.42)
    dog = normalize(gaussian(radius, 0.14) - 0.55 * gaussian(radius, 0.32))
    cross = (np.abs(x) + np.abs(y)) * gaussian(radius, 0.22)

    variants: dict[str, np.ndarray] = {
        "star-gaussian-soft.png": normalize((0.95 * base) + (0.20 * halo)),
        "star-dog-crisp.png": normalize((0.90 * dog) + (0.10 * halo)),
        "star-log-ring.png": normalize((0.85 * base) + (0.22 * ring) + (0.08 * halo)),
        "star-derivative-cross.png": normalize(
            (0.75 * base) + (0.28 * cross) + (0.10 * halo)
        ),
    }

    for name, alpha in variants.items():
        alpha = match_alpha_distribution(alpha, reference_alpha)
        image = make_mask(alpha)
        image.save(OUT_DIR / name)

    for name, spike_count, crisp in RAY_VARIANTS:
        make_rotated_ray_star(spike_count, crisp, reference_alpha).save(OUT_DIR / name)


if __name__ == "__main__":
    main()
