# AstraTerra Data Pipeline

Runtime catalog assets are committed under `assets/astraterra/data/`. Builds and tests must not depend on rerunning the catalog generator.
We could do it later, but for now it is an one shot.

## Catalog Generator

The developer-only generator lives in `tools/cataloggen/`.

```bash
cd tools/cataloggen
python -m cataloggen.main --help
```

The committed v1 star catalog is generated from local HYG v4.2 source data. Source dataset placement and licensing notes live in `tools/cataloggen/sources/README.md`.

Pass the Modern IAU asset through `--required-sky-culture-json` when regenerating the runtime catalog. This retains every star referenced by an authored figure even when it falls beyond the normal brightness or count cutoff; `hipparcos-supplement.v1.json` supplies exceptional referenced entries absent from HYG.

## Runtime Assets

- `star-catalog.v1.json`: baked fixed-star catalog.
- `guide-stars.v1.json`: grouped guide-star metadata.
- `sky-cultures.v1.json`: manifest for authored sky cultures.
- `sky-cultures/*.json`: authored constellation line data.
- `deep-sky.v1.json`: telescope-only deep-sky object metadata.

When adding a new runtime data shape, version the filename and add asset tests for schema-level expectations.
