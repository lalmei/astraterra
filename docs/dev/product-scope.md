# AstraTerra Product Scope

## Current Vision

AstraTerra makes the night sky mechanically meaningful. Players should be able to learn the sky, see it change with latitude and season, draw constellations, and use instruments to read useful astronomical information.

## Goals

- Replace the default night starfield with an Earth-based fixed-star sky.
- Make the sky respond to local latitude and seasonal progression.
- Provide handheld telescope observation with zoom, drawing, inspection, and removal modes.
- Provide a sextant readout for a star's angle above the horizon.
- Let players save, name, inspect, and delete local per-world constellations.
- Ship reproducible committed catalog assets and a developer-only generation pipeline.

## Non-Goals For Current Scope

- Multiplayer synchronization of constellation journals.
- Planets, comets, or other moving bodies.
- Milky Way rendering.
- Real star names in normal player UI.
- Import/export of constellation journals.
- Visual regression infrastructure.

## Accepted Tradeoffs

- Brightness is stylized for readability rather than physically faithful.
- The constellation journal is client-local in v1.
- Runtime builds use committed generated assets; catalog generation is an explicit developer task.
- Debug commands expose implementation details such as HIP IDs because they are useful recovery tools.

## Deferred Work

- Multiplayer constellation sync.
- Catalog import/export.
- Moving bodies and event objects.
- Atlas/book/table item for browsing saved constellations.
- Sharing constellation catalogs with other players.
- Tapestry or decorative constellation output.
- More accessibility options and UI polish.
