import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from PIL import Image

from skysurvey import main as cli
from tests.test_survey import synthetic_sky


class EndToEndTests(unittest.TestCase):
    def test_writes_the_global_map_tiles_manifest_and_report(self) -> None:
        with tempfile.TemporaryDirectory() as scratch:
            root = Path(scratch)
            with mock.patch.object(cli, "read_exr", return_value=synthetic_sky(256)):
                cli.main(
                    [
                        "--source", "fake_gal.exr",
                        "--global-width", "256",
                        "--output", str(root / "milky-way.png"),
                        "--tiles-dir", str(root / "tiles"),
                        "--tile-size", "64",
                        "--max-abs-latitude", "45",
                        "--report", str(root / "report.json"),
                    ]
                )

            with Image.open(root / "milky-way.png") as image:
                self.assertEqual(image.size, (256, 128))
                self.assertEqual(image.mode, "RGB")

            manifest = json.loads((root / "tiles" / "tiles.json").read_text())
            self.assertEqual(manifest["gutter"], 1)
            self.assertEqual(len(manifest["tiles"]), 16)
            for tile in manifest["tiles"]:
                with Image.open(root / "tiles" / f"{tile['name']}.jpg") as image:
                    self.assertEqual(image.size, (66, 66))

            report = json.loads((root / "report.json").read_text())
            self.assertEqual(report["tiles"]["count"], 16)
            self.assertTrue(report["tiles"]["ship_in_main_mod"])

    def test_refuses_a_mirrored_source_before_writing_anything(self) -> None:
        with tempfile.TemporaryDirectory() as scratch:
            output = Path(scratch) / "milky-way.png"
            with mock.patch.object(cli, "read_exr", return_value=synthetic_sky(256)[:, ::-1]):
                with self.assertRaises(ValueError):
                    cli.main(["--source", "fake_gal.exr", "--global-width", "256", "--output", str(output)])
            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
