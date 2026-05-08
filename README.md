# AstraTerra

AstraTerra turns the night sky into an Earth-focused starfield for Vintage Story 1.22.2, with latitude-aware seasons, craftable telescopes, authored constellation lines, deep-sky sights, a sextant readout, and a client-local constellation journal.

## Docs

Start here:

- [Player Guide](docs/player/guide.md)
- [Command Reference](docs/player/commands.md)
- [Developer Guide](docs/dev/index.md)
- [Docs Index](docs/index.md)

## Build And Test

```bash
make test
make build
make package
make docs-build
```

For local in-game smoke testing:

```bash
make deploy
```

Then enable AstraTerra in Vintage Story and follow [Manual Verification](docs/dev/manual-verification.md).

## Attribution

Inspired by Minecraft's Spyglass Astronomy.

The Brass Telescope item model is adapted from Fuami's MIT-licensed Spyglass mod. Modern IAU constellation line data and selected deep-sky assets are adapted from Stellarium sources. The Sextant item model transforms and recipe are adapted from the user's downloaded Realistic Surveying package. See `THIRD_PARTY_NOTICES.md`.

Special thanks to AdAstra's LadyLioness.
