from dataclasses import dataclass
from typing import Optional


@dataclass(frozen=True)
class HipStar:
    hip: int
    ra_deg: float
    dec_deg: float
    visual_magnitude: float
    bv_color_index: Optional[float]


def select_visible_catalog(stars: list[HipStar], max_visual_magnitude: float) -> list[HipStar]:
    return sorted(
        [star for star in stars if star.visual_magnitude <= max_visual_magnitude],
        key=lambda star: (star.visual_magnitude, star.hip),
    )
