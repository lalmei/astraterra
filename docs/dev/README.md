# AstraTerra Developer Guide

## Build And Test

```bash
make test
make build
make package
```

The Makefile uses the repo-local `.dotnet` path and defaults to a `Release` build. The mod project expects `VINTAGE_STORY` to point at a Vintage Story install directory. If unset, it defaults to `/Applications/Vintage Story.app` on macOS.

For a local Vintage Story smoke test:

```bash
make deploy
```

Then enable AstraTerra in Vintage Story 1.22.2 and run the [manual verification checklist](manual-verification.md).

## Repository Layout

```text
assets/astraterra/        Runtime JSON, language, shapes, recipes, and textures
src/AstraTerra/           Vintage Story code mod
tests/AstraTerra.Tests/   Unit and asset tests
tools/cataloggen/         Developer-only catalog generation tool
docs/player/              Player-facing documentation
docs/dev/                 Developer-facing documentation
```

## Core References

- [Architecture](architecture.md)
- [Data Pipeline](data-pipeline.md)
- [Manual Verification](manual-verification.md)
- [Product Scope](product-scope.md)
- [Reference Sky Rendering Notes](reference-sky-rendering-notes.md)
