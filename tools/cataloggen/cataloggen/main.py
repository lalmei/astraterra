import argparse
import json
from pathlib import Path

from cataloggen.guide_stars import build_guide_star_groups
from cataloggen.hyg import load_hyg_csv
from cataloggen.hipparcos import HipStar, select_visible_catalog


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Generate AstraTerra runtime star catalog assets.")
    parser.add_argument("--max-visual-magnitude", type=float, default=6.0)
    parser.add_argument("--max-stars", type=int, default=1000)
    parser.add_argument("--hyg-csv", type=Path, help="Optional HYG CSV source file.")
    parser.add_argument("--output-dir", type=Path, help="Directory that receives star-catalog.v1.json and guide-stars.v1.json.")
    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if args.output_dir is None:
        return

    args.output_dir.mkdir(parents=True, exist_ok=True)
    source_stars = load_hyg_csv(args.hyg_csv) if args.hyg_csv else [HipStar(677, 2.09708, 29.09043, 2.07, 0.15)]
    stars = select_visible_catalog(source_stars, args.max_visual_magnitude)[: args.max_stars]
    guide_groups = build_guide_star_groups(star.hip for star in stars)

    (args.output_dir / "star-catalog.v1.json").write_text(
        json.dumps(
            [
                {
                    "hip": star.hip,
                    "rightAscensionDeg": star.ra_deg,
                    "declinationDeg": star.dec_deg,
                    "visualMagnitude": star.visual_magnitude,
                    "bvColorIndex": star.bv_color_index,
                    "isGuideStar": True,
                }
                for star in stars
            ],
            indent=2,
        )
        + "\n"
    )
    (args.output_dir / "guide-stars.v1.json").write_text(
        json.dumps(
            [
                {
                    "iauCode": group.iau_code,
                    "displayName": group.display_name,
                    "hipIds": list(group.hip_ids),
                }
                for group in guide_groups
            ],
            indent=2,
        )
        + "\n"
    )


if __name__ == "__main__":
    main()
