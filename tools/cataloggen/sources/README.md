# Catalog Sources

Place source catalog files here when running the generator locally. Source datasets are not committed by default; document licensing and exact source URLs before adding any raw data to the repository.

Supported source shape:

- HYG CSV v4.x with `hip`, `ra`, `dec`, `mag`, and optional `ci` columns.
- `ra` is expected in hours and is converted to degrees.

HYG v4.2 is published by Astronomy Nexus and is licensed CC BY-SA 4.0.

For the v1 runtime assets, download HYG v4.2 from the upstream Codeberg mirror:

```bash
curl -L -o hygdata_v42.csv.gz https://codeberg.org/astronexus/hyg/media/branch/main/data/hyg/CURRENT/hyg_v42.csv.gz
gunzip -c hygdata_v42.csv.gz > hygdata_v42.csv
```

The Astronomy Nexus project page also links to the current HYG release and license terms.

`hipparcos-supplement.v1.json` contains the exceptional HIP 55203 entry used by the Modern IAU Ursa Major figure. HYG omits this multiple-star system as a normal catalog row; the supplement records the SIMBAD position and combined photometry so the authored line remains renderable. The generator includes supplemental stars only when they are referenced by a required sky-culture file.

Modern IAU constellation line data is adapted from Stellarium's `modern_iau` sky culture, licensed CC BY-SA 4.0:

```bash
curl -L -o stellarium-modern-iau-index.json https://raw.githubusercontent.com/Stellarium/stellarium/master/skycultures/modern_iau/index.json
```

The runtime copy lives at `assets/astraterra/data/sky-cultures/modern-iau.constellations.v1.json` and is registered through `assets/astraterra/data/sky-cultures.v1.json`.
