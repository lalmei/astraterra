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
  --max-stars 1000 \
  --output-dir ../../assets/astraterra/data
```

HYG v4.2 is available from Astronomy Nexus and is licensed CC BY-SA 4.0. Keep source CSV files under `sources/` locally unless the project explicitly decides to commit the source data.
