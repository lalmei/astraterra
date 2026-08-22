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

## Releasing

### Bump the version

```bash
make bump-minor-version          # 0.1.56 -> 0.2.0
make bump-patch-version          # 0.1.56 -> 0.1.57
make bump-version VERSION=1.0.0  # explicit
```

The version lives in **two** places — `modinfo.json` and `AstraTerraModMetadata.Version` — and
`BootstrapSmokeTests.Runtime_Version_Stays_In_Sync_With_Modinfo` fails if they drift. Always bump
through the Makefile rather than editing either by hand.

!!! note "The bump targets deploy"
    `bump-version`, and therefore `bump-minor-version` and `bump-patch-version`, chain into
    `deploy`, which installs the zip package into your local Vintage Story Mods folder. Use
    `make bump-version-files VERSION=x.y.z` to update the version without deploying.

### What happens on merge to `main`

Two workflows run from the same push, independently:

| Workflow | Runner | Does |
| --- | --- | --- |
| `release-drafter.yml` | `ubuntu-latest` | Reads the version from `modinfo.json` and creates or renames the **draft** release to `vX.Y.Z`, with notes generated from merged pull requests |
| `ci.yml` | self-hosted macOS | Tests, builds, packages, then uploads `dist/AstraTerra-X.Y.Z.zip` both as a workflow artifact and as an asset on that draft |

Release Drafter owns the notes; CI only ever touches assets (`gh release upload --clobber`), so the
two do not fight. CI waits for the draft to appear, and creates one itself if Release Drafter never
got there, so a build is never stranded without somewhere to land.

**Publishing stays manual.** Review the draft, confirm the attached zip, then publish — which is
what creates the `vX.Y.Z` git tag.

!!! warning "A published release is never modified"
    If `main` moves after `vX.Y.Z` has already been published — that is, someone merged without
    bumping the version — CI logs a warning and leaves the release alone rather than overwriting a
    shipped asset. The package still exists as a workflow artifact. Bump the version and merge again.

!!! note "CI needs the self-hosted runner"
    `ci.yml` runs on `[self-hosted, macOS, astraterra-local]`. Release Drafter does not. If that
    machine is offline when you merge, you will get a correctly versioned draft with no package
    attached, and no build anywhere.

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
