# AstraTerra Catalog Generator

Developer-only tooling for producing versioned runtime star catalog JSON files under `assets/astraterra/data/`.

The scaffold command is:

```bash
python -m cataloggen.main --help
```

To regenerate the committed placeholder assets:

```bash
python -m cataloggen.main --output-dir ../../assets/astraterra/data
```

To generate from a local HYG CSV source file:

```bash
python -m cataloggen.main \
  --hyg-csv sources/hygdata_v42.csv \
  --max-visual-magnitude 6.0 \
  --max-stars 3000 \
  --required-sky-culture-json ../../assets/astraterra/data/sky-cultures/modern-iau.constellations.v1.json \
  --supplement-json sources/hipparcos-supplement.v1.json \
  --output-dir ../../assets/astraterra/data
```

Required sky-culture stars are added after the brightness and count limits so authored line figures remain complete. The supplement supplies exceptional HIP entries that are referenced by a sky culture but absent from HYG.

HYG v4.2 is available from Astronomy Nexus and is licensed CC BY-SA 4.0. Keep source CSV files under `sources/` locally unless the project explicitly decides to commit the source data.
