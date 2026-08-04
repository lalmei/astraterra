#!/usr/bin/env python3
"""Import AstraTerra's curated Stellarium deep-sky expansion."""

from __future__ import annotations

import argparse
import itertools
import json
import math
import re
import shutil
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
CATALOG_PATH = REPOSITORY_ROOT / "assets/astraterra/data/deep-sky.v1.json"
TEXTURE_DESTINATION = REPOSITORY_ROOT / "assets/astraterra/textures/environment/deep-sky/stellarium"

# The image filenames and registrations come from Stellarium's
# nebulae/default/textures.json. Display names are intentionally concise and
# player-facing; coordinates, footprint, and brightness are source-derived.
SELECTION = [
    ("M1", "Crab Nebula", "m1dumont.png"),
    ("M2", "Messier 2 globular cluster", "m2.png"),
    ("M3", "Messier 3 globular cluster", "m3.png"),
    ("M4", "Messier 4 globular cluster", "m4.png"),
    ("M5", "Messier 5 globular cluster", "m5.png"),
    ("M6", "Butterfly Cluster", "m6.png"),
    ("M7", "Ptolemy Cluster", "m7.png"),
    ("M10", "Messier 10 globular cluster", "m10.png"),
    ("M11", "Wild Duck Cluster", "m11.png"),
    ("M12", "Messier 12 globular cluster", "m12.png"),
    ("M13", "Hercules Globular Cluster", "m13.png"),
    ("M15", "Great Pegasus Cluster", "m15-vasey.png"),
    ("M22", "Sagittarius Cluster", "m22.png"),
    ("M33", "Triangulum Galaxy", "m33.png"),
    ("M51", "Whirlpool Galaxy", "m51-vasey.png"),
    ("M63", "Sunflower Galaxy", "m63-vasey.png"),
    ("M64", "Black Eye Galaxy", "m64.png"),
    ("M65", "Messier 65 galaxy", "m65.png"),
    ("M66", "Messier 66 galaxy", "m66.png"),
    ("M81", "Bode's Galaxy", "m81.png"),
    ("M82", "Cigar Galaxy", "m82-vasey.png"),
    ("M83", "Southern Pinwheel Galaxy", "m83.png"),
    ("NGC891", "NGC 891 edge-on galaxy", "n891.png"),
    ("M97", "Owl Nebula", "m97dumont.png"),
    ("M101", "Pinwheel Galaxy", "m101-vasey.png"),
    ("M104", "Sombrero Galaxy", "m104.png"),
    ("M106", "Messier 106 galaxy", "m106-vasey.png"),
    ("NGC253", "Sculptor Galaxy", "n253.png"),
    ("NGC4565", "Needle Galaxy", "n4565.png"),
    ("NGC5128", "Centaurus A", "n5128.png"),
]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Import the curated 30-object Stellarium expansion into AstraTerra."
    )
    parser.add_argument(
        "stellarium_nebulae_dir",
        type=Path,
        help="Path to a Stellarium nebulae/default directory containing textures.json and PNG files.",
    )
    return parser.parse_args()


def spherical_center(corners: list[list[float]]) -> tuple[float, float]:
    x = y = z = 0.0
    for right_ascension_deg, declination_deg in corners:
        right_ascension = math.radians(right_ascension_deg)
        declination = math.radians(declination_deg)
        x += math.cos(declination) * math.cos(right_ascension)
        y += math.cos(declination) * math.sin(right_ascension)
        z += math.sin(declination)

    right_ascension_deg = math.degrees(math.atan2(y, x)) % 360.0
    declination_deg = math.degrees(math.atan2(z, math.hypot(x, y)))
    return right_ascension_deg, declination_deg


def angular_separation(first: list[float], second: list[float]) -> float:
    first_ra, first_dec = map(math.radians, first)
    second_ra, second_dec = map(math.radians, second)
    cosine = (
        math.sin(first_dec) * math.sin(second_dec)
        + math.cos(first_dec) * math.cos(second_dec) * math.cos(first_ra - second_ra)
    )
    return math.degrees(math.acos(max(-1.0, min(1.0, cosine))))


def source_brightness(max_brightness: float | None) -> float:
    # Stellarium records surface brightness in visual magnitudes per square
    # arcminute. Convert that monotonically into AstraTerra's bounded opacity.
    source_value = 13.8 if max_brightness is None else max_brightness
    return round(max(0.36, min(0.62, 0.34 + ((14.5 - source_value) * 0.11))), 2)


def build_entry(object_id: str, display_name: str, metadata: dict) -> dict:
    world_coords = metadata["worldCoords"]
    texture_coords = metadata.get("textureCoords")
    if len(world_coords) != 1 or len(world_coords[0]) != 4:
        raise ValueError(f"{object_id} must have one four-corner worldCoords polygon")
    if texture_coords != [[[0, 0], [1, 0], [1, 1], [0, 1]]]:
        raise ValueError(f"{object_id} does not use canonical four-corner texture coordinates")

    corners = world_coords[0]
    center_ra, center_dec = spherical_center(corners)
    angular_size = max(
        angular_separation(first, second)
        for first, second in itertools.combinations(corners, 2)
    )
    texture_stem = Path(metadata["imageUrl"]).stem

    return {
        "id": object_id,
        "displayName": display_name,
        "rightAscensionDeg": round(center_ra, 4),
        "declinationDeg": round(center_dec, 4),
        "angularSizeDeg": round(angular_size, 4),
        "worldCoords": [
            {
                "rightAscensionDeg": right_ascension_deg,
                "declinationDeg": declination_deg,
            }
            for right_ascension_deg, declination_deg in corners
        ],
        "brightness": source_brightness(metadata.get("maxBrightness")),
        "tintR": 1.0,
        "tintG": 1.0,
        "tintB": 1.0,
        "texturePath": f"astraterra:environment/deep-sky/stellarium/{texture_stem}",
        "fallbackTexturePaths": ["astraterra:environment/deep-sky-cloud"],
    }


def serialize_catalog(catalog: list[dict]) -> str:
    serialized = json.dumps(catalog, indent=2)
    return re.sub(
        r'\{\n\s+"rightAscensionDeg": ([^,]+),\n\s+"declinationDeg": ([^\n]+)\n\s+\}',
        r'{ "rightAscensionDeg": \1, "declinationDeg": \2 }',
        serialized,
    ) + "\n"


def main() -> None:
    args = parse_args()
    source_directory = args.stellarium_nebulae_dir.resolve()
    metadata_path = source_directory / "textures.json"
    metadata_document = json.loads(metadata_path.read_text(encoding="utf-8-sig"), strict=False)
    metadata_by_image = {
        entry.get("imageUrl"): entry
        for entry in metadata_document["subTiles"]
        if isinstance(entry, dict) and entry.get("imageUrl")
    }

    selected_ids = {object_id for object_id, _, _ in SELECTION}
    catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    catalog = [entry for entry in catalog if entry["id"] not in selected_ids]

    TEXTURE_DESTINATION.mkdir(parents=True, exist_ok=True)
    for object_id, display_name, image_name in SELECTION:
        metadata = metadata_by_image.get(image_name)
        if metadata is None:
            raise FileNotFoundError(f"Stellarium metadata is missing {image_name}")
        source_texture = source_directory / image_name
        if not source_texture.is_file():
            raise FileNotFoundError(f"Stellarium texture is missing {source_texture}")

        catalog.append(build_entry(object_id, display_name, metadata))
        shutil.copyfile(source_texture, TEXTURE_DESTINATION / image_name)

    CATALOG_PATH.write_text(serialize_catalog(catalog), encoding="utf-8")
    print(f"Imported {len(SELECTION)} Stellarium deep-sky plates; catalog now has {len(catalog)} objects.")


if __name__ == "__main__":
    main()
