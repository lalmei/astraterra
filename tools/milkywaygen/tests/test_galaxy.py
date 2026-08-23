import unittest

import numpy as np

from milkywaygen.galaxy import (
    SUN_RADIUS_KPC,
    GalaxyModel,
    clumping_field,
    integrate_surface_brightness,
    latitude_taper,
)
from milkywaygen.main import encode


def brightness_at(longitude_deg: float, latitude_deg: float, **kwargs) -> float:
    model = GalaxyModel(steps=160, **kwargs)
    return float(
        integrate_surface_brightness(
            np.array([[longitude_deg]]), np.array([[latitude_deg]]), model
        )[0, 0]
    )


class GalaxyModelTests(unittest.TestCase):
    def test_the_plane_is_brighter_than_the_poles(self) -> None:
        self.assertGreater(brightness_at(90.0, 0.0), 5 * brightness_at(90.0, 90.0))

    def test_the_centre_is_brighter_than_the_anticentre(self) -> None:
        self.assertGreater(brightness_at(0.0, 3.0), brightness_at(180.0, 3.0))

    def test_dust_darkens_the_centre_it_sits_in_front_of(self) -> None:
        clear = brightness_at(0.0, 0.0, dust_opacity=0.0)
        dusty = brightness_at(0.0, 0.0)

        self.assertLess(dusty, clear / 2)

    def test_the_observer_sits_inside_the_disc(self) -> None:
        # A model that placed the Sun outside the galaxy would put the whole band on one side of
        # the sky, so this is worth stating: light arrives from l = 180 as well as from l = 0.
        self.assertGreater(SUN_RADIUS_KPC, 0.0)
        self.assertGreater(brightness_at(180.0, 0.0), 0.0)

    def test_the_arms_brighten_some_longitudes_and_not_others(self) -> None:
        longitudes = np.linspace(180.0, -180.0, 180, endpoint=False)[None, :]
        armed = integrate_surface_brightness(
            longitudes, np.array([[0.0]]), GalaxyModel(steps=160)
        )
        smooth = integrate_surface_brightness(
            longitudes, np.array([[0.0]]), GalaxyModel(steps=160, arm_strength=0.0)
        )

        ratio = armed / smooth
        self.assertGreater(ratio.max(), 1.2)
        self.assertLess(ratio.min(), ratio.max() * 0.9)


class ClumpingFieldTests(unittest.TestCase):
    def test_the_field_wraps_in_longitude(self) -> None:
        field = clumping_field((64, 128), seed=7)

        # The map's left and right edges are the same direction on the sky. A field built by
        # interpolating a grid would not know that, and the seam would run down Sagittarius.
        seam_gap = np.abs(field[:, 0] - field[:, -1])
        neighbour_gap = np.abs(np.diff(field, axis=1)).mean()
        self.assertLess(seam_gap.mean(), 3 * neighbour_gap)

    def test_the_field_is_centred_and_scaled(self) -> None:
        field = clumping_field((64, 128), seed=7)

        self.assertAlmostEqual(float(field.mean()), 0.0, places=6)
        self.assertAlmostEqual(float(field.std()), 1.0, places=6)

    def test_the_taper_keeps_structure_near_the_plane(self) -> None:
        self.assertAlmostEqual(float(latitude_taper(np.array(0.0))), 1.0)
        self.assertLess(float(latitude_taper(np.array(80.0))), 0.05)


class EncodingTests(unittest.TestCase):
    def test_the_darkest_direction_encodes_as_black(self) -> None:
        brightness = np.array([[[0.01, 0.01, 0.01], [4.0, 3.6, 3.2]]])

        image = np.asarray(encode(brightness, shoulder=0.55, gamma=1.15))

        self.assertEqual([0, 0, 0], list(image[0, 0]))
        self.assertGreater(int(image[0, 1].min()), 128)

    def test_a_galaxy_with_no_light_is_refused(self) -> None:
        with self.assertRaises(ValueError):
            encode(np.zeros((2, 2, 3)), shoulder=0.55, gamma=1.15)


if __name__ == "__main__":
    unittest.main()
