# AstraTerra Player Guide

AstraTerra turns the night sky into an Earth-focused starfield for Vintage Story. Stars change with latitude and season, telescope zoom reveals more sky detail, and saved constellation lines can be used as a seasonal memory aid.

## What You Can Do

- Watch an Earth-like fixed-star sky that changes as you travel north or south.
- Use the Brass Telescope to zoom in, draw constellation lines, inspect saved constellations, and remove segments.
- Use the Precision Telescope for stronger zoom.
- Use the Sextant to read the angle of a visible star above the horizon.
- Build authored Modern IAU constellations with commands when you want known star patterns in your journal.

## Items

### Brass Telescope

Hold right click to enter the scoped view. While scoped:

- Scroll to change zoom.
- Middle click to cycle modes.
- In Draw mode, left-drag from one guide star to another to create a segment.
- In Inspect mode, click a saved constellation segment to name or rename it.
- In Remove Segment mode, click a saved segment to delete it.

The telescope still works as a zoom tool when astronomy conditions are not available.

### Precision Telescope

The Precision Telescope uses the same observation workflow as the Brass Telescope, with stronger zoom levels for careful stargazing.

### Sextant

Hold right click with the Sextant and align the center of the screen with a visible star. The on-screen readout displays that star's angle above the horizon in degrees.

If no readable star is near the center, the readout asks you to align with a star. If the sky is blocked or stars are not visible, it reports that instead.

## Observation Conditions

Astronomy interaction works best when:

- it is dark enough to see stars,
- the player has open sky overhead,
- the weather is clear enough,
- the player is looking at visible stars.

The telescope overlay can still open when those conditions are not met, but drawing and star-specific readouts may be unavailable.

## Constellation Visibility

Constellations are visible only while you hold their written AstraTerra constellation book in your left hand. A written book is portable: another player can hold the same book to see and use the same saved constellations.

## Constellation Journal

Saved constellations are written into a blank normal book held in your left hand. You also need ink and quill in your inventory to create, add, remove, rename, or build constellations. Once AstraTerra writes the first constellation into the book, vanilla book editing is locked, but AstraTerra can still update the constellation journal.

AstraTerra includes authored Modern IAU constellation line data. These are normal journal entries once built with commands, so you can inspect, rename, or delete them like hand-drawn constellations.

## Quick Start

1. Craft or spawn a Brass Telescope.
2. Wait for a dark, clear night with open sky overhead.
3. Put a blank normal book in your left hand and keep ink and quill in your inventory.
4. Hold right click with the telescope to scope in.
5. Middle click to switch to Draw mode.
6. Drag between visible guide stars to create a constellation segment.
7. Run `.stars list` and `.stars info selected` to inspect the saved constellation.
8. Use the Sextant on a visible star to read its angle above the horizon.
