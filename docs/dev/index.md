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

## Documentation Site

```bash
make docs-build
make docs-serve
```

The documentation site builds with ProperDocs and the MaterialX theme. The configuration lives in `properdocs.yml`, and the docs toolchain is pinned in `docs/requirements.txt`.

Install [uv](https://docs.astral.sh/uv/) before running the documentation targets. `make docs-build` performs the same strict build used by CI. Pull requests that change the docs or their build configuration are validated without publishing; pushes to `main` publish the result to the [AstraTerra documentation site](https://lalmei.github.io/astraterra/).

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
