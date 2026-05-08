from dataclasses import dataclass
from typing import Iterable


@dataclass(frozen=True)
class GuideStarGroup:
    iau_code: str
    display_name: str
    hip_ids: tuple[int, ...]


AUTHORED_GUIDE_STARS: tuple[GuideStarGroup, ...] = (
    GuideStarGroup("And", "Andromeda", (677, 5447, 9640)),
    GuideStarGroup("Aqr", "Aquarius", (106278, 109074, 110003, 110395, 111497)),
    GuideStarGroup("Ari", "Aries", (8832, 8903, 9884, 14838)),
    GuideStarGroup("Cap", "Capricornus", (100064, 100345, 104139, 106985, 107556)),
    GuideStarGroup("CMa", "Canis Major", (32349, 30324, 33579, 34444)),
    GuideStarGroup("Cas", "Cassiopeia", (746, 3179, 4427, 6686, 8886)),
    GuideStarGroup("Cen", "Centaurus", (68702, 68933, 71683)),
    GuideStarGroup("Cnc", "Cancer", (40526, 42911, 43103, 44066)),
    GuideStarGroup("Cru", "Crux", (59747, 60718, 61084, 62434)),
    GuideStarGroup("Cyg", "Cygnus", (95947, 97165, 100453, 102098, 102488)),
    GuideStarGroup("Gem", "Gemini", (31681, 36850, 37826)),
    GuideStarGroup("Leo", "Leo", (49669, 50583, 54872, 57632)),
    GuideStarGroup("Lib", "Libra", (72622, 74785, 76333, 77853)),
    GuideStarGroup("Lyr", "Lyra", (91262, 92420, 93194)),
    GuideStarGroup("Ori", "Orion", (24436, 25336, 25930, 26311, 26727, 27366, 27989)),
    GuideStarGroup("Psc", "Pisces", (7097, 8198, 9487, 11484, 118268)),
    GuideStarGroup("Sgr", "Sagittarius", (88635, 89931, 90185, 90496, 92855, 93506, 95347)),
    GuideStarGroup("Sco", "Scorpius", (78265, 78401, 78820, 80763, 85927, 86228)),
    GuideStarGroup("Tau", "Taurus", (17702, 21421, 25428)),
    GuideStarGroup("UMa", "Ursa Major", (53910, 54061, 58001, 59774, 62956, 65378, 67301)),
    GuideStarGroup("UMi", "Ursa Minor", (11767, 72607, 75097, 77055, 79822, 82080, 85822)),
    GuideStarGroup("Vir", "Virgo", (57757, 60129, 61941, 63090, 63608, 65474)),
)


def build_guide_star_groups(available_hip_ids: Iterable[int]) -> list[GuideStarGroup]:
    available = set(available_hip_ids)
    groups: list[GuideStarGroup] = []

    for group in AUTHORED_GUIDE_STARS:
        hip_ids = tuple(hip for hip in group.hip_ids if hip in available)
        if hip_ids:
            groups.append(GuideStarGroup(group.iau_code, group.display_name, hip_ids))

    return groups
