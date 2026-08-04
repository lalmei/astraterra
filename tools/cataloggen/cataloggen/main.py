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
    parser.add_argument(
        "--required-sky-culture-json",
        action="append",
        default=[],
        type=Path,
        help="Sky-culture JSON whose referenced HIP stars must be included even beyond the normal catalog limit.",
    )
    parser.add_argument(
        "--supplement-json",
        action="append",
        default=[],
        type=Path,
        help="Supplemental runtime-format stars for required HIP entries absent from HYG.",
    )
    parser.add_argument("--output-dir", type=Path, help="Directory that receives star-catalog.v1.json and guide-stars.v1.json.")
    return parser


def main() -> None:
    parser = build_parser()
    args = parser.parse_args()

    if args.output_dir is None:
        return

    args.output_dir.mkdir(parents=True, exist_ok=True)
    source_stars = load_hyg_csv(args.hyg_csv) if args.hyg_csv else [HipStar(677, 2.09708, 29.09043, 2.07, 0.15)]
    selected_stars = select_visible_catalog(source_stars, args.max_visual_magnitude)[: args.max_stars]
    source_stars.extend(star for path in args.supplement_json for star in load_supplement_json(path))
    required_hips = load_required_hips(args.required_sky_culture_json)
    stars = include_required_stars(
        source_stars,
        selected_stars,
        required_hips,
    )
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


def load_required_hips(paths: list[Path]) -> set[int]:
    required: set[int] = set()
    for path in paths:
        payload = json.loads(path.read_text(encoding="utf-8"))
        for constellation in payload.get("constellations", []):
            for line in constellation.get("lines", []):
                required.update(int(hip) for hip in line)

    return required


def load_supplement_json(path: Path) -> list[HipStar]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    rows = payload.get("stars", payload)
    return [
        HipStar(
            hip=int(row["hip"]),
            ra_deg=float(row["rightAscensionDeg"]),
            dec_deg=float(row["declinationDeg"]),
            visual_magnitude=float(row["visualMagnitude"]),
            bv_color_index=None if row.get("bvColorIndex") is None else float(row["bvColorIndex"]),
        )
        for row in rows
    ]


def include_required_stars(
    source_stars: list[HipStar],
    selected_stars: list[HipStar],
    required_hips: set[int],
) -> list[HipStar]:
    source_by_hip = {star.hip: star for star in source_stars}
    missing = sorted(required_hips.difference(source_by_hip))
    if missing:
        raise ValueError(f"Required HIP stars are missing from all catalog sources: {missing}")

    selected_by_hip = {star.hip: star for star in selected_stars}
    for hip in required_hips:
        selected_by_hip.setdefault(hip, source_by_hip[hip])

    return sorted(selected_by_hip.values(), key=lambda star: (star.visual_magnitude, star.hip))


if __name__ == "__main__":
    main()
