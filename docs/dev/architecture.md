# AstraTerra Architecture

## Runtime Systems

`Astronomy/` contains the pure sky model: latitude mapping, sidereal time, altitude classification, star projection, sky-culture loading, and seasonal constellation summaries. Keep this code testable outside the Vintage Story runtime when possible.

`Client/Rendering/` owns visual presentation. The active starfield renders as 3D billboards around the vanilla sun/moon pass. Orthographic renderers handle telescope overlay, constellation line overlays, and sextant readouts.

`Client/Observation/` owns observation mode state. Telescope behavior uses a small shared state object so item interaction, zoom hooks, and renderers agree on scoped mode and zoom.

`Items/` is the Vintage Story item entry point. Item classes should start and stop observation/readout state, not own sky calculations.

`Constellations/` owns the client-local journal model, graph merge/split behavior, stable saved IDs, and persistence.

`Commands/` owns `.stars` command behavior and debug formatting.

`Config/` owns file-backed mod settings.

`Infrastructure/` is kept narrow: asset loading and small support helpers.

## Asset Layout

```text
assets/astraterra/
├── data/
├── itemtypes/
├── lang/
├── recipes/grid/
├── shapes/item/
└── textures/
```

Runtime catalog assets are versioned by filename. Sky-culture files are registered through `assets/astraterra/data/sky-cultures.v1.json`, with individual culture files under `assets/astraterra/data/sky-cultures/`.

## Rendering Baseline

AstraTerra follows the reference sky implementation-style sun/moon render pass:

- patch `SystemRenderSunMoon.OnRenderFrame3D`,
- render star quads as 3D billboards with `StandardShader`,
- use close sky placement distance around `40f`,
- disable depth test/culling during the star pass,
- use additive/glow blending,
- keep orthographic rendering for overlays and labels.

Brightness is intentionally game-readable rather than physically faithful. Magnitude affects relative brightness, but faint visible stars keep a readable floor.

## Test Rules

- Keep astronomy math and constellation graph rules out of renderer classes.
- Use file-based asset validation for committed JSON data.
- Avoid image-diff tests.
- Keep test names behavior-oriented.
