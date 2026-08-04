import unittest

from cataloggen.hipparcos import HipStar
from cataloggen.main import build_parser, include_required_stars


class RequiredStarsTests(unittest.TestCase):
    def test_catalog_generation_is_not_count_limited_by_default(self) -> None:
        args = build_parser().parse_args([])

        self.assertIsNone(args.max_stars)

    def test_required_stars_are_added_beyond_the_selected_limit(self) -> None:
        bright = HipStar(1, 10.0, 20.0, 1.0, 0.2)
        required = HipStar(2, 30.0, 40.0, 7.0, 0.4)

        result = include_required_stars([bright, required], [bright], {2})

        self.assertEqual([1, 2], [star.hip for star in result])

    def test_missing_required_source_is_reported(self) -> None:
        with self.assertRaisesRegex(ValueError, "55203"):
            include_required_stars([], [], {55203})


if __name__ == "__main__":
    unittest.main()
