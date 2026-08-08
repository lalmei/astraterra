# AstraTerra Player Guide

AstraTerra turns the night sky into an dynamic starfield for Vintage Story. Stars change with latitude, seasons, (future: longitude) telescope zoom reveals more sky detail, and saved constellation lines can be used as a seasonal memory aid.

## What You Can Do

- Watch an 5000+ fixed-star sky that changes as you travel north or south.
- Use the Brass Telescope to zoom in, draw constellation lines, inspect saved constellations, and remove segments.
- Use the Precision Telescope for slight stronger zoom, maybe clear view.
- Use the Sextant to read the angle of a star, the sun, or the moon above the horizon, day or night.
- Use the Calibrated Astrolabe to predict where and when recorded constellations will appear, tell the time of night, and see what day of the year it is.
- Two prebuilt constellation catalogs, that could be used to debug, or used in builds.

### Extension

Right now the star field is only defined by the star catalog configuration,
found within a JSON file in the mod. The current one was build using Stellarium dataset using
One could randomly generate it during world creation, or add/edit stars.

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

Hold right click with the Sextant and align the center of the screen with the sun, the moon, or a visible star. The top-center readout names what you sighted and displays its angle above the horizon in degrees. While holding right click, use middle click to cycle through angle only, the rose equatorial grid, the cyan azimuthal grid, and both grids. Releasing right click restores the grid mode selected with `.stars sky-grid`; the Sextant remembers its selected display mode for the rest of the game session.

The sun and the moon can be shot whenever they are above the horizon, so the Sextant works in broad daylight — a daytime moon is a perfectly good sight, and often an easier one than a star. Stars still need a dark enough sky. If several bodies fall inside the sight, the one nearest the center of the screen wins.

If nothing readable is near the center, the readout tells you what is available to aim at. If the sky is blocked overhead it reports that instead.

!!! note "Sun safety"
    Sighting the sun currently costs you nothing. A real navigator would use a shade glass.

### Calibrated Astrolabe

Recover a vanilla Astrolabe from ruins, then combine it with a brass plate to calibrate it for AstraTerra. Hold the Calibrated Astrolabe in your main hand and a written constellation book in your left hand, then hold right click to open the planning readout.

The top-center readout shows the selected constellation's compass direction, angle above the horizon, rising or setting state, time until transit, and whether it is circumpolar or can never rise at the current latitude.

Below that, the Astrolabe tells you the time. It reads the hour off the sky the way a nocturnal does — from where the sun actually is — and reports the hour of the world day, whether it is daylight, dusk, night, or dawn, the sun's angle above or below the horizon, and how long until the next sunrise or sunset.

The clock follows your forecast. Scroll ahead six hours and the time advances with the stars, so you can find the hour a constellation reaches its best position before you commit to waiting for it. Near the poles, where the sun may not rise or set at all for a stretch of the year, the readout says so rather than inventing an hour.

The time of day is also shown when no book is held, so the Astrolabe is still useful on its own.

- Middle click to select the next constellation in the held book.
- Scroll to forecast one hour at a time.
- Sneak and scroll to forecast seven days at a time.

Forecasting spans one world year and uses that world's configured year and day lengths. Releasing right click returns the forecast to the current time.

## Observation Conditions

Astronomy interaction works best when:

- it is dark enough to see stars,
- the player has open sky overhead,
- the weather is clear enough,
- the player is looking at visible stars.

The telescope overlay can still open when those conditions are not met, but drawing and star-specific readouts may be unavailable.

The astrolabe is predictive rather than observational, so it works in daylight, indoors, during bad weather, and when its selected constellation is below the horizon.

## Starfield Comparison Modes

Right now available for debug purposes.

Use `.stars starfield astraterra|both|vanilla` to switch the visible starfield immediately:

- `astraterra` shows only AstraTerra's Earth-based star catalog and is the default.
- `both` overlays AstraTerra and Vintage Story stars for direct alignment comparison.
- `vanilla` shows only Vintage Story's original cubemap starfield.

The selected value is saved as `StarfieldMode` in `ModConfig/astraterra.json` and persists across restarts. AstraTerra leaves Vintage Story's original star textures untouched and controls the two render passes at runtime.

## Constellation Visibility

Constellations are visible only while you hold their written AstraTerra constellation book in your left hand. A written book is portable: another player can hold the same book to see and use the same saved constellations.

## Constellation Journal

Saved constellations are written into a blank normal book held in your left hand. You also need ink and quill in your inventory to create, add, remove, rename, or build constellations. Once AstraTerra writes the first constellation into the book, vanilla book editing is locked, but AstraTerra can still update the constellation journal.

AstraTerra includes authored Modern IAU constellation line data. These are normal journal entries once built with commands, so you can inspect, rename, or delete them like hand-drawn constellations.

Use the [Constellation Build Cheat Sheet](constellation-cheat-sheet.md) to find the three-letter code for any of the 88 included patterns.

## Quick Start

1. Craft or spawn a Brass Telescope.
2. Wait for a dark, clear night with open sky overhead.
3. Put a blank normal book in your left hand and keep ink and quill in your inventory.
4. Hold right click with the telescope to scope in.
5. Middle click to switch to Draw mode.
6. Drag between visible guide stars to create a constellation segment.
7. Run `.stars list` and `.stars info selected` to inspect the saved constellation.
8. Use the Sextant on a visible star to read its angle above the horizon.
9. Hold a Calibrated Astrolabe with the written constellation book in your left hand and forecast the constellation's next transit.
