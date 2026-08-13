# AstraTerra Architecture

## Runtime Systems

`Astronomy/` contains the pure sky model: latitude mapping, sidereal time, horizontal-coordinate classification, sky projection, astrolabe planning, sky-culture loading, seasonal constellation summaries, and meteor shower activity.

Position and projection are kept apart on purpose. `ISkyEphemeris` answers *where a body is at a world time* — constant for a catalog star, an orbit for a planet — and `SkyProjection` answers *where that lands for this observer*, identically for every kind of body. `RenderedStar` is the shared `RenderedBody` plus the things only a star has, and anything else that moves is expected to wrap it the same way rather than grow a second projection path. `CachedSkyEphemeris` keeps the first half off the per-frame budget.

The convention here is narrower than "no Vintage Story types", which the asset loaders in this folder have never followed. It is: **the model is pure, and the loaders that feed it are the only exception.** A loader may take `ICoreAPI` to reach the game's asset system, but it must also expose a pure parse entry point over a string so the shipped asset's shape can be tested without a running game — see `MeteorShowerCatalogLoader.Parse`. Everything else in this folder takes plain numbers and returns plain numbers, which is what keeps it testable outside the Vintage Story runtime.

`Client/Rendering/` owns visual presentation. The AstraTerra starfield renders as 3D billboards around the vanilla sun/moon pass. A Harmony prefix on Vintage Story's night-sky pass selects AstraTerra-only, combined, or vanilla-only rendering without shadowing the game's cubemap assets. `MeteorShowerVisualModel` converts the pure observed hourly rate into short-lived radiant-relative streaks, and `MeteorStreakMeshBuilder` batches their tapered sky ribbons for that same pass. Orthographic renderers handle telescope overlay, constellation line overlays, and sextant readouts.

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

Runtime catalog assets are versioned by filename.

`assets/astraterra/data/star_catalog.v1.json` : Contains the defined stars. Modifing this file can change which stars you see.

`assets/astraterra/data/deep-sky.v1.json` : Contains the defined sky images, in our reserved for nebulas, galaxy and other deep sky objects. We use real astronomy photograhs, but these can also be pre-generated.

Sky-culture files are registered through `assets/astraterra/data/sky-cultures.v1.json`, with individual culture files under `assets/astraterra/data/sky-cultures/`.

The Sky-culture allows you to predefined different sets of constalations, which might be useful for Custom Stories, and lore.

## Rendering Baseline

AstraTerra follows the reference sky implementation-style sun/moon render pass:

- patch `SystemRenderSunMoon.OnRenderFrame3D`,
- conditionally allow `SystemRenderNightSky.OnRenderFrame3D` according to `StarfieldMode`,
- render star quads as 3D billboards with `StandardShader`,
- use close sky placement distance around `40f`,
- disable depth test/culling during the star pass,
- use additive/glow blending,
- batch transient meteor ribbons into one updated mesh,
- keep orthographic rendering for overlays and labels.

!!! warning "A mesh that is updated every frame must never change size"
    Vintage Story sizes a mesh's GPU buffers from the vertex count of the first `UploadMesh` and
    never grows them. `UpdateMesh` writes into whatever was allocated, and it tells the draw call
    the *new* index count regardless — so a batch that grew between frames loses its vertices to a
    rejected buffer write and then draws past the end of its own index buffer.

    Both per-frame sky meshes are therefore built at a constant size whatever they are drawing:
    `DeepSkyQuadMeshBuilder` from a fixed subdivision count, and `MeteorStreakMeshBuilder` by
    padding out to `MaximumActiveStreaks` with empty, zero-area streak slots. Any new mesh built
    once per frame and updated in place has to do the same.
    `MeteorStreakMeshBuilderTests.Mesh_Size_Does_Not_Change_With_The_Number_Of_Streaks` pins it.

Brightness is intentionally game-readable rather than physically faithful. Magnitude affects relative brightness, but faint visible stars keep a readable floor. Star cores use compact, vanilla-like apparent diameters; only brighter stars receive a restrained outer glow.

## Test Rules

- Keep astronomy math and constellation graph rules out of renderer classes.
- Use file-based asset validation for committed JSON data.
- Avoid image-diff tests.
- Keep test names behavior-oriented.
