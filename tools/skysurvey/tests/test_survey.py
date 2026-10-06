import unittest

import numpy as np

from skysurvey.survey import (
    REFERENCE_STATS,
    OrientationError,
    Stretch,
    box_mean,
    calibration_error,
    check_orientation,
    cut_tiles,
    downsample,
    fit_stretch,
    galactic_axes,
    measure,
    tile_rows_within,
)


def synthetic_sky(height: int = 256) -> np.ndarray:
    """A linear sky in the renderer's convention: a band, a bright centre and an LMC.

    Deliberately lopsided along both axes so every mirror image is distinguishable from it.
    """
    width = 2 * height
    longitude, latitude = galactic_axes(height, width)
    wrapped = (longitude + 180.0) % 360.0 - 180.0
    band = np.exp(-((latitude / 6.0) ** 2)) * (0.3 + 0.7 * np.exp(-((wrapped / 50.0) ** 2)))
    lmc_dl = (longitude - 280.47 + 180.0) % 360.0 - 180.0
    lmc = 0.6 * np.exp(-((lmc_dl / 4.0) ** 2) - (((latitude + 32.89) / 3.0) ** 2))
    # The sky away from the band is not flat: the disc's light thins towards the poles.
    floor = 0.01 + 0.04 * np.exp(-np.abs(latitude) / 30.0)
    luminance = floor + band + lmc
    colour = np.array([1.1, 1.0, 0.9])
    return (luminance[..., None] * colour).astype(np.float32)


class AxesTests(unittest.TestCase):
    def test_the_first_column_is_longitude_plus_180_and_the_centre_is_zero(self) -> None:
        longitude, latitude = galactic_axes(4, 8)
        self.assertAlmostEqual(longitude[0, 0], 180.0 - 22.5)
        self.assertAlmostEqual(longitude[0, 3] + longitude[0, 4], 0.0)
        self.assertGreater(latitude[0, 0], 0.0)
        self.assertLess(latitude[-1, 0], 0.0)

    def test_box_mean_wraps_across_the_seam(self) -> None:
        image = np.zeros((64, 128, 3), dtype=np.float32)
        image[:, 0] = 1.0
        image[:, -1] = 1.0
        self.assertGreater(box_mean(image, 180.0, 0.0, 3.0, 10.0), 0.5)


class OrientationTests(unittest.TestCase):
    def test_a_correctly_oriented_sky_passes(self) -> None:
        readings = check_orientation(synthetic_sky())
        self.assertGreater(readings["centre"], readings["anticentre"])

    def test_upside_down_is_refused(self) -> None:
        with self.assertRaisesRegex(OrientationError, "upside down"):
            check_orientation(synthetic_sky()[::-1])

    def test_mirrored_is_refused(self) -> None:
        with self.assertRaisesRegex(OrientationError, "mirrored"):
            check_orientation(synthetic_sky()[:, ::-1])

    def test_half_a_turn_out_is_refused(self) -> None:
        with self.assertRaisesRegex(OrientationError, "half a turn"):
            check_orientation(np.roll(synthetic_sky(), 256, axis=1))


class DownsampleTests(unittest.TestCase):
    def test_preserves_mean_light(self) -> None:
        sky = synthetic_sky()
        self.assertAlmostEqual(float(downsample(sky, 4).mean()), float(sky.mean()), places=5)

    def test_refuses_a_factor_that_does_not_divide(self) -> None:
        with self.assertRaises(ValueError):
            downsample(synthetic_sky(), 3)

    def test_downsample_then_stretch_matches_stretched_tiles_better_than_the_reverse(self) -> None:
        """Averaging stretched values comes out darker than the full-resolution tiles."""
        rng = np.random.default_rng(1)
        grainy = synthetic_sky() * rng.exponential(1.0, size=(256, 512, 1)).astype(np.float32)
        stretch = Stretch(black=0.0, white=float(grainy.mean(axis=-1).max()), softening=0.05)
        linear_first = stretch.apply(downsample(grainy, 4)).mean()
        stretched_first = downsample(stretch.apply(grainy), 4).mean()
        full_resolution = stretch.apply(grainy).mean()
        # Both differ from the tiles, but the order matters and the tool has to pick one; this
        # pins which way round each one errs, so a change to the pipeline order shows up here.
        self.assertAlmostEqual(float(stretched_first), float(full_resolution), places=5)
        self.assertGreater(float(linear_first), float(stretched_first))


class StretchTests(unittest.TestCase):
    def test_keeps_each_pixels_colour(self) -> None:
        sky = synthetic_sky()
        encoded = Stretch(black=0.0, white=1.0, softening=0.1).apply(sky)
        bright = encoded.mean(axis=-1) > 0.05
        ratios = encoded[bright, 0] / encoded[bright, 2]
        np.testing.assert_allclose(ratios, 1.1 / 0.9, rtol=1e-4)

    def test_saturation_greys_colour_without_moving_intensity(self) -> None:
        sky = synthetic_sky()
        full = Stretch(black=0.0, white=1.0, softening=0.1).apply(sky)
        muted = Stretch(black=0.0, white=1.0, softening=0.1, saturation=0.5).apply(sky)
        grey = Stretch(black=0.0, white=1.0, softening=0.1, saturation=0.0).apply(sky)
        # Pixels past white are scaled back as a whole, by an amount that depends on their colour.
        unclipped = full.max(axis=-1) < 1.0
        np.testing.assert_allclose(muted.mean(axis=-1)[unclipped], full.mean(axis=-1)[unclipped], atol=1e-6)
        bright = full.mean(axis=-1) > 0.05
        self.assertLess(float(np.abs(muted[bright, 0] - muted[bright, 2]).mean()), float(np.abs(full[bright, 0] - full[bright, 2]).mean()))
        np.testing.assert_allclose(grey[..., 0], grey[..., 2], atol=1e-6)

    def test_is_monotonic_and_bounded(self) -> None:
        ramp = np.linspace(0.0, 5.0, 200, dtype=np.float32)[None, :, None].repeat(3, axis=-1)
        encoded = Stretch(black=0.1, white=2.0, softening=0.2).apply(ramp)[0, :, 0]
        self.assertTrue(np.all(np.diff(encoded) >= -1e-7))
        self.assertEqual(float(encoded[0]), 0.0)
        self.assertLessEqual(float(encoded.max()), 1.0)

    def test_gamma_lifts_the_faint_end_and_keeps_the_ends_fixed(self) -> None:
        ramp = np.linspace(0.0, 1.0, 101, dtype=np.float32)[None, :, None].repeat(3, axis=-1)
        plain = Stretch(black=0.0, white=1.0, softening=0.3).apply(ramp)[0, :, 0]
        lifted = Stretch(black=0.0, white=1.0, softening=0.3, gamma=0.7).apply(ramp)[0, :, 0]
        self.assertGreater(float(lifted[10]), float(plain[10]))
        self.assertAlmostEqual(float(lifted[0]), 0.0)
        self.assertAlmostEqual(float(lifted[-1]), float(plain[-1]), places=5)

    def test_a_strided_sample_measures_like_the_full_map_and_a_box_filtered_copy_does_not(self) -> None:
        """Why the tool fits on every n-th pixel: averaging grain first changes what the stretch sees."""
        rng = np.random.default_rng(7)
        grainy = synthetic_sky(256) * rng.exponential(1.0, size=(256, 512, 1)).astype(np.float32)
        stretch = Stretch(black=0.0, white=float(np.percentile(grainy.mean(axis=-1), 99.95)), softening=0.2, gamma=0.6)
        full = measure(stretch.apply(grainy))["median_luminance"]
        strided = measure(stretch.apply(grainy[::4, ::4]))["median_luminance"]
        box = measure(stretch.apply(downsample(grainy, 4)))["median_luminance"]
        self.assertLess(abs(strided - full), abs(box - full))

    def test_fit_lands_closer_to_the_reference_than_a_naive_stretch(self) -> None:
        sky = synthetic_sky()
        naive = calibration_error(measure(Stretch(black=0.0, white=float(sky.max()), softening=1.0).apply(sky)))
        _, stats, error = fit_stretch(sky)
        self.assertLess(error, naive)
        self.assertEqual(set(stats), set(REFERENCE_STATS))


class TileTests(unittest.TestCase):
    def test_the_band_of_a_16k_map_is_192_tiles(self) -> None:
        rows = tile_rows_within(8192, 512, 33.75)
        self.assertEqual(len(rows), 6)
        self.assertEqual(len(rows) * (16384 // 512), 192)

    def test_each_tile_spans_its_share_of_the_sky(self) -> None:
        tiles = list(cut_tiles(synthetic_sky(256), tile_size=64, gutter=1, max_abs_latitude_deg=45.0))
        self.assertEqual(len(tiles), 2 * 8)
        first = tiles[0]
        self.assertAlmostEqual(first.longitude_max_deg - first.longitude_min_deg, 45.0)
        self.assertAlmostEqual(first.latitude_max_deg - first.latitude_min_deg, 45.0)
        self.assertEqual(first.pixels.shape, (66, 66, 3))

    def test_gutters_hold_the_neighbouring_tiles_edge(self) -> None:
        sky = synthetic_sky(256)
        tiles = {(t.row, t.column): t for t in cut_tiles(sky, tile_size=64, gutter=1, max_abs_latitude_deg=45.0)}
        left, right = tiles[(1, 2)], tiles[(1, 3)]
        np.testing.assert_array_equal(left.pixels[1:-1, -1], right.pixels[1:-1, 1])
        np.testing.assert_array_equal(right.pixels[1:-1, 0], left.pixels[1:-1, -2])

    def test_gutters_wrap_across_the_longitude_seam(self) -> None:
        sky = synthetic_sky(256)
        tiles = {(t.row, t.column): t for t in cut_tiles(sky, tile_size=64, gutter=1, max_abs_latitude_deg=45.0)}
        first, last = tiles[(1, 0)], tiles[(1, 7)]
        np.testing.assert_array_equal(first.pixels[1:-1, 0], last.pixels[1:-1, -2])
        np.testing.assert_array_equal(last.pixels[1:-1, -1], first.pixels[1:-1, 1])

    def test_refuses_a_map_that_does_not_divide_into_tiles(self) -> None:
        with self.assertRaises(ValueError):
            list(cut_tiles(synthetic_sky(256), tile_size=100))


if __name__ == "__main__":
    unittest.main()
