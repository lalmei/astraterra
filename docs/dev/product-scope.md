# AstraTerra Product Scope

## Current Vision

AstraTerra makes the night sky mechanically meaningful. Players should be able to learn the sky, see
it change with latitude, longitude and season, draw constellations, and use instruments to measure,
record and predict what they see.

## Goals

- Replace the default night starfield with an Earth-based fixed-star sky, with comparison modes for the combined and vanilla-only skies.
- Make the sky respond to local latitude, local longitude and seasonal progression.
- Move the sun and daylight with world X so the star field and the visible sun stay in step, behind a server-side switch.
- Move the planets on real Keplerian orbits, and the meteor showers and comets on real periods anchored to solar longitude.
- Provide handheld telescope observation with zoom, drawing, inspection, and removal modes, and resolve planets into discs under magnification.
- Provide a sextant readout for a body's angle above the horizon, and let a sighting be written into a book the player can reason from.
- Turn a recovered vanilla astrolabe into a planner for recorded figures, identified wanderers and comets, cut for one latitude.
- Give a world a tier-0 instrument in the Sky Disc: a year of solar horizon marks yielding year length, latitude, the cardinal directions and the next solstice.
- Let players save, name, inspect, and delete constellations in a book that another player can pick up.
- Draw the unresolved glow of the Milky Way behind the catalog stars, from a generated model rather than a photograph.
- Ship reproducible committed catalog assets and a developer-only generation pipeline.
- Let another mod replace any catalog, so a world somewhere other than Earth can still use these instruments.

## Non-Goals For Current Scope

- Real star names in normal player UI. Identification is the player's work; only debug commands expose HIP ids.
- Import and export of constellation journals as files.
- Visual regression infrastructure.
- A second solar or lunar ephemeris. Vintage Story's sun and moon stay the bodies the instruments measure.
- Sharing a constellation catalog between players by any route other than handing over the book.

## Accepted Tradeoffs

- Brightness is stylized for readability rather than physically faithful, and the star curve saturates at magnitude 0.4.
- Resolved planet discs and their moons are drawn 7.5x larger than life, uniformly, so the relationships between them hold.
- Comet tracks and peak magnitudes are authored rather than integrated from an orbit; the periods and parent showers are real.
- Runtime builds use committed generated assets; catalog generation is an explicit developer task.
- Debug commands expose implementation details such as HIP IDs because they are useful recovery tools.
- The moon under `MoonArt=vanilla` stays on the world's universal time, because AstraTerra is not drawing that disc and cannot move it.

## Deferred Work

- Longitude by chronometer ([#124](https://github.com/lalmei/astraterra/issues/124)).
- Moonlight and eclipse darkening following the observer's moon rather than the world's.
- Catalog import/export.
- Event objects beyond comets and meteor showers.
- Graphical rotating rete and placed tabletop astrolabe interface.
- Tapestry or decorative constellation output.
- More accessibility options and UI polish.
