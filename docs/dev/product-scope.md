# AstraTerra Product Scope

## Current Vision

AstraTerra makes the night sky mechanically meaningful. Players should be able to learn the sky, see it change with latitude and season, draw constellations, and use instruments to read useful astronomical information.

## Goals

- Replace the default night starfield with an Earth-based fixed-star sky by default, with comparison modes for the combined and vanilla-only skies.
- Make the sky respond to local latitude and seasonal progression.
- Provide handheld telescope observation with zoom, drawing, inspection, and removal modes.
- Provide a sextant readout for a star's angle above the horizon.
- Turn a recovered vanilla astrolabe into a planner for recorded constellations across time and latitude.
- Let players save, name, inspect, and delete local per-world constellations.
- Draw the unresolved glow of the Milky Way behind the catalog stars, from a generated model rather than a photograph.
- Ship reproducible committed catalog assets and a developer-only generation pipeline.

## Non-Goals For Current Scope

- Multiplayer synchronization of constellation journals.
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
- Event objects beyond comets and meteor showers.
- Graphical rotating rete and placed tabletop astrolabe interface.
- Sharing constellation catalogs with other players.
- Tapestry or decorative constellation output.
- More accessibility options and UI polish.
