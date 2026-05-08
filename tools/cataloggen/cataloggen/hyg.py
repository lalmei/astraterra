import csv
from pathlib import Path
from typing import Optional

from cataloggen.hipparcos import HipStar


def load_hyg_csv(path: Path) -> list[HipStar]:
    stars: list[HipStar] = []
    with path.open(newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            hip = _parse_int(row.get("hip") or row.get("Hip"))
            mag = _parse_float(row.get("mag") or row.get("Mag"))
            ra_hours = _parse_float(row.get("ra") or row.get("RA"))
            dec_deg = _parse_float(row.get("dec") or row.get("Dec"))
            if hip is None or mag is None or ra_hours is None or dec_deg is None:
                continue

            stars.append(
                HipStar(
                    hip=hip,
                    ra_deg=ra_hours * 15.0,
                    dec_deg=dec_deg,
                    visual_magnitude=mag,
                    bv_color_index=_parse_float(row.get("ci") or row.get("ColorIndex")),
                )
            )

    return stars


def _parse_int(value: Optional[str]) -> Optional[int]:
    if value is None or value.strip() == "":
        return None

    try:
        return int(float(value))
    except ValueError:
        return None


def _parse_float(value: Optional[str]) -> Optional[float]:
    if value is None or value.strip() == "":
        return None

    try:
        return float(value)
    except ValueError:
        return None
