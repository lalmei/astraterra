"""The glow of the stars the catalog does not draw, integrated along each line of sight.

AstraTerra's catalog stops at visual magnitude 6, and even a complete catalog would stop
somewhere: past some brightness a star is not a point on the sky any more, it is part of a
haze. That haze is what this module renders. Nothing here is a photograph or a repaint of
one — it is a galaxy (a disc, a bulge and four spiral arms) seen from where the Sun sits in
it, dimmed by a thinner, flatter layer of dust in front.

Distances are kiloparsecs. Brightness is in whatever units the emissivity is written in; the
caller normalizes, because only the shape of the map matters to the renderer.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np

#: Sun's distance from the galactic centre. Sets where the band is brightest and how wide
#: the bulge reads: move the observer in and Sagittarius swells.
SUN_RADIUS_KPC = 8.2

#: Sun's height above the mid-plane. Small, but it is why the band's bright ridge sits a
#: little below b = 0 towards the centre and a little above it towards the anticentre.
SUN_HEIGHT_KPC = 0.02


@dataclass(frozen=True)
class GalaxyModel:
    """Shape parameters of the disc, its arms, the bulge, and the dust in front of them."""

    disc_scale_length_kpc: float = 3.2
    disc_scale_height_kpc: float = 0.34
    bulge_scale_kpc: float = 0.75
    bulge_flattening: float = 0.6
    bulge_strength: float = 12.0

    #: Spiral arms. Without them the band fades smoothly from Sagittarius to the anticentre
    #: and reads as a painted gradient; with them it has the bright knots — a Cygnus, a
    #: Carina — that make one part of the sky worth pointing at over another.
    arm_count: int = 4
    arm_pitch_deg: float = 12.5
    arm_reference_radius_kpc: float = 3.0
    arm_strength: float = 1.3
    arm_width: float = 0.22
    #: Radius inside which the arms are wound too tightly to be arms. Without this the
    #: spiral keeps coiling towards the centre and lands as a picket fence of ridges across
    #: Sagittarius — the bar's territory, drawn as stripes.
    arm_inner_radius_kpc: float = 3.2

    dust_scale_length_kpc: float = 3.5
    dust_scale_height_kpc: float = 0.11
    #: Extinction per unit dust density per kpc. The single number that decides how dark the
    #: Great Rift cuts: at zero the band is a smooth ridge, and the rift is the whole reason
    #: the real one looks split.
    dust_opacity: float = 2.4
    #: How far out the line of sight is followed, and in how many steps. Beyond ~30 kpc the
    #: disc has nothing left to contribute at any longitude.
    max_distance_kpc: float = 30.0
    steps: int = 320

    def arm_factor(self, radius: np.ndarray, azimuth: np.ndarray) -> np.ndarray:
        """How much brighter the disc is here than a featureless disc would be.

        A logarithmic spiral is a straight line in (log radius, azimuth), so the arms are a
        cosine of the difference between the two — no per-arm loop, and no seam where the
        azimuth wraps.
        """
        pitch = np.tan(np.radians(self.arm_pitch_deg))
        phase = np.log(np.maximum(radius, 1e-3) / self.arm_reference_radius_kpc) / pitch - azimuth
        ridge = np.exp(-(1.0 - np.cos(self.arm_count * phase)) / self.arm_width)
        inner_fade = 1.0 - np.exp(-((radius / self.arm_inner_radius_kpc) ** 2))
        return 1.0 + self.arm_strength * ridge * inner_fade

    def emissivity(
        self, radius: np.ndarray, azimuth: np.ndarray, height: np.ndarray
    ) -> np.ndarray:
        """Starlight emitted per unit volume: an armed exponential disc plus a bulge."""
        disc = np.exp(
            -radius / self.disc_scale_length_kpc - np.abs(height) / self.disc_scale_height_kpc
        )
        bulge_radius = np.sqrt(
            radius * radius + (height * height) / (self.bulge_flattening * self.bulge_flattening)
        )
        bulge = self.bulge_strength * np.exp(-((bulge_radius / self.bulge_scale_kpc) ** 1.6))
        return disc * self.arm_factor(radius, azimuth) + bulge

    def dust_density(self, radius: np.ndarray, height: np.ndarray) -> np.ndarray:
        """Dust per unit volume: the same disc, longer and much flatter than the starlight."""
        return np.exp(
            -radius / self.dust_scale_length_kpc - np.abs(height) / self.dust_scale_height_kpc
        )


def integrate_surface_brightness(
    longitude_deg: np.ndarray,
    latitude_deg: np.ndarray,
    model: GalaxyModel,
    dust_modulation: np.ndarray | None = None,
) -> np.ndarray:
    """Marches every pixel's line of sight outwards, accumulating light and losing it to dust.

    The two halves have to happen together, in order: light emitted at 12 kpc is dimmed by
    every dust cloud between there and here, so the extinction has to be integrated as the
    ray advances rather than applied to the total afterwards. Doing it the cheap way puts a
    dark lane over the bulge instead of in front of it, which is the difference between a
    Great Rift and a smudge.
    """
    longitude = np.radians(longitude_deg)
    latitude = np.radians(latitude_deg)

    # Heliocentric galactic-cartesian direction: x towards the centre, y towards l = 90 deg.
    direction_x = np.cos(latitude) * np.cos(longitude)
    direction_y = np.cos(latitude) * np.sin(longitude)
    direction_z = np.sin(latitude)

    step = model.max_distance_kpc / model.steps
    brightness = np.zeros(np.broadcast(longitude_deg, latitude_deg).shape, dtype=np.float64)
    optical_depth = np.zeros_like(brightness)

    for index in range(model.steps):
        distance = (index + 0.5) * step
        x = SUN_RADIUS_KPC - distance * direction_x
        y = -distance * direction_y
        z = SUN_HEIGHT_KPC + distance * direction_z
        radius = np.sqrt(x * x + y * y)
        azimuth = np.arctan2(y, x)

        dust = model.dust_density(radius, z)
        if dust_modulation is not None:
            dust = dust * dust_modulation

        # Half this step's extinction applies to light emitted inside it, the other half to
        # everything behind it. Splitting it keeps the near dust from over-darkening.
        half_step_depth = 0.5 * model.dust_opacity * dust * step
        optical_depth += half_step_depth
        brightness += model.emissivity(radius, azimuth, z) * np.exp(-optical_depth) * step
        optical_depth += half_step_depth

    return brightness


def clumping_field(
    shape: tuple[int, int],
    seed: int,
    slope: float = 1.7,
    largest_feature_cycles: float = 4.0,
    smallest_feature_px: float = 3.0,
) -> np.ndarray:
    """A seamless fractal field with zero mean and unit spread, for breaking up the dust.

    Built by shaping white noise in the Fourier domain rather than by summing octaves of
    interpolated grids: the transform is periodic in both axes, so the map wraps at l = 0
    without a seam — and a seam there would run a bright line straight down Sagittarius.
    """
    rng = np.random.default_rng(seed)
    height, width = shape
    noise = np.fft.rfft2(rng.normal(size=shape))

    rows = np.fft.fftfreq(height)[:, None] * height
    columns = np.fft.rfftfreq(width)[None, :] * width
    frequency = np.sqrt(rows * rows + columns * columns)
    frequency[0, 0] = 1.0

    spectrum = frequency ** (-slope)
    # Band-limited on both sides. Without the low end a 1/f field is mostly one enormous
    # blob, which does not read as cloud at all: it reads as one half of the sky being
    # dimmer than the other.
    spectrum *= 1.0 - np.exp(-((frequency / largest_feature_cycles) ** 2))
    # Roll the finest scales off rather than letting them alias into single-pixel speckle,
    # which reads as film grain over the sky instead of as cloud.
    spectrum *= np.exp(-((frequency * smallest_feature_px / width) ** 2))
    spectrum[0, 0] = 0.0

    field = np.fft.irfft2(noise * spectrum, s=shape)
    spread = field.std()
    return field / spread if spread > 0 else field


def latitude_taper(latitude_deg: np.ndarray, scale_deg: float = 22.0) -> np.ndarray:
    """Fades the clumping out away from the plane, where there is no dust to clump.

    Also a projection fix: an equirectangular map stretches horizontally towards the poles,
    so an isotropic field drawn on it turns into visible streaks at high latitude. Both
    reasons point the same way — the structure belongs near b = 0.
    """
    return np.exp(-np.abs(latitude_deg) / scale_deg)
