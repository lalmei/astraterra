# Stellarium Deep-Sky Import

`import_stellarium.py` imports AstraTerra's curated 30-object expansion from a
Stellarium checkout. The source directory must contain Stellarium's
`nebulae/default/textures.json` and its referenced PNG files.

```bash
python tools/deepsky/import_stellarium.py /path/to/stellarium/nebulae/default
```

The importer preserves each source texture's four registered sky corners,
derives the catalog center and descriptive angular size from that spherical
footprint, copies the exact PNG, and is idempotent by object ID.
