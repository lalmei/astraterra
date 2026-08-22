# AstraTerra Player Guide

AstraTerra turns the night sky into an dynamic starfield for Vintage Story. Stars change with latitude, seasons, (future: longitude) telescope zoom reveals more sky detail, and saved constellation lines can be used as a seasonal memory aid.

## What You Can Do

- Watch an 5000+ fixed-star sky that changes as you travel north or south.
- Watch annual meteor showers whose timing, radiant height, moonlight, and darkness determine how many streaks appear, and comets that return on their own periods with an anti-sunward tail.
- Use the Brass Telescope to zoom in, draw constellation lines, inspect saved constellations, and remove segments.
- Use the Precision Telescope for slight stronger zoom, maybe clear view.
- Use the Sextant to read the angle of a star, a planet, a comet, the sun, or the moon above the horizon, day or night.
- Lie on your back with **Z** to watch the sky from the ground, the same kind of toggle as sit-on-G.
- Use the Calibrated Astrolabe to predict where and when recorded constellations, the planets, and the comets will appear, tell the time of night, and see what day of the year it is.
- Two prebuilt constellation catalogs, that could be used to debug, or used in builds. `Catalog` the 88 Internationally (IAU) defined constalations and the `Zodiac` containes just the 12 constallations in the Zodiac.

### Extension

Right now the star field is only defined by the star catalog configuration,
found within a JSON file in the mod. The current one was build using Stellarium dataset using
One could randomly generate it during world creation, or add/edit stars.

## Items

### Brass Telescope

Hold right click to enter the scoped view. While scoped:

- Scroll to change zoom.
- Middle click to cycle modes.
- In Observe mode, centre a wandering star and press sneak to write it down and name it.
- In Draw mode, left-drag from one guide star to another to create a segment.
- In Inspect mode, click a saved constellation segment to name or rename it.
- In Remove Segment mode, click a saved segment to delete it.

The telescope still works as a zoom tool when astronomy conditions are not available.

#### Identifying a wandering star

A telescope cannot tell you a planet is a planet — through any eyepiece it stays a point of light, exactly like the stars around it. What gives it away is that it *moves*: chart it over a few nights and it will have shifted against the fixed stars behind it, and that is how the ancients picked them out.

When you are satisfied you have found one, centre it in the eyepiece in Observe mode and press sneak. It goes into your book and you are asked what to call it. Sneak rather than a click, because right click is already holding the scope up. As with drawing constellations, you need a book in your left hand and ink and quill in your inventory.

The name is yours. Nothing in the sky will ever tell you it was called Mars — that name only arrives in a book somebody else already wrote.

### Precision Telescope

The Precision Telescope uses the same observation workflow as the Brass Telescope, with stronger zoom levels for careful stargazing.

### Sextant

Hold right click with the Sextant and align the center of the screen with the sun, the moon, or a visible star. The top-center readout names what you sighted and displays its angle above the horizon in degrees. While holding right click, use middle click to cycle through angle only, the rose equatorial grid, the cyan azimuthal grid, and both grids. Releasing right click restores the grid mode selected with `.stars sky-grid`; the Sextant remembers its selected display mode for the rest of the game session.

The sun and the moon can be shot whenever they are above the horizon, so the Sextant works in broad daylight — a daytime moon is a perfectly good sight, and often an easier one than a star. Stars and planets still need a dark enough sky. If several bodies fall inside the sight, the one nearest the center of the screen wins.

Planets sight by whatever *you* call them. With no book naming them the readout says **Wandering star**, because from the ground that is all a planet is: a star that does not keep its place. Put a book that names them in your left hand and the readout uses those names. Since planets are usually among the brightest things up, they are the easiest sights in the sky after the sun and the moon.

If nothing readable is near the center, the readout tells you what is available to aim at. If the sky is blocked overhead it reports that instead.

!!! note "Sun safety"
Sighting the sun currently costs you nothing. A real navigator would use a shade glass.

### Calibrated Astrolabe

Recover a vanilla Astrolabe from ruins, then combine it with a brass plate to fit it for AstraTerra. Hold the Calibrated Astrolabe in your main hand and a written constellation book in your left hand, then hold right click to open the planning readout.

#### Cutting its plate

An astrolabe is engraved for one latitude. A newly made one is blank and will not place a star: it reads **no plate** and tells you so. To cut its plate, stand under open sky after dusk, then **sneak and hold right click** for a few seconds while it sights the pole. A bar fills as it works, and if it will not fill the readout says why — a roof overhead or a sky still too bright.

You need nothing but the sky for this. No star catalog, no brass, no crafting grid.

Once cut, every reading the astrolabe gives is answered *for that latitude*, not for wherever you happen to be standing. Walk far enough north or south and the readout starts telling you how far you have strayed from the plate — and past about eight degrees it asks you to recut it. Until you do, its answers are the sky over the place it was engraved for. The instrument is not broken; it is describing somewhere else.

Its clock is not engraved and does not drift: the hour comes from the sun overhead wherever you carry it.

The top-center readout shows the selected target's compass direction, angle above the horizon, rising or setting state, time until transit, and whether it is circumpolar or can never rise at the current latitude.

Middle click cycles through the constellations recorded in the book and then through the five planets, which are marked `· planet` so they are not mistaken for a figure you drew. Every planet is listed whether or not it is currently up — being told that Saturn is below the horizon and transits in nine hours is exactly the sort of thing the instrument is for.

A planet you have not written down reads as **Wandering star**. Swapping books renames the sky immediately, so a borrowed book brings its author's names with it.

Below that, the Astrolabe tells you the time. It reads the hour off the sky the way a nocturnal does — from where the sun actually is — and reports the hour of the world day, whether it is daylight, dusk, night, or dawn, the sun's angle above or below the horizon, and how long until the next sunrise or sunset.

The clock follows your forecast. Scroll ahead six hours and the time advances with the stars, so you can find the hour a constellation reaches its best position before you commit to waiting for it. Near the poles, where the sun may not rise or set at all for a stretch of the year, the readout says so rather than inventing an hour.

The time of day is also shown when no book is held, so the Astrolabe is still useful on its own.

- Middle click to select the next target: the constellations in the held book, then the planets.
- Scroll to forecast one hour at a time.
- Sneak and scroll to forecast seven days at a time.
- Sneak and hold right click to recut the plate for your current latitude.

Forecasting spans one world year and uses that world's configured year and day lengths. Releasing right click returns the forecast to the current time.

A planet is forecast where it will actually be, not where it is now: scroll a season ahead and Mars has moved against the stars behind it. The transit countdown holds the planet's position where the forecast puts it, which is worth a couple of minutes over a night — far below anything you could measure with these instruments.

## Lying Down

Press **Z** to lie on your back on the ground, the same kind of toggle as sit-on-G. In first person the camera drops to eye level with the grass and looks straight up, so the sky fills the view instead of the seraph. Third person and the fixed overhead camera keep their current view; only the body lies down. Looking around turns your view, not the body on the ground. Empty hands rest behind the head. Move, jump, or press **Z** again to stand. The binding is remappable under Controls as **Lie down**. **X** stays vanilla off-hand swap.

Telescopes, the Sextant, and the Calibrated Astrolabe still work while you are lying down.

## Observation Conditions

Astronomy interaction works best when:

- it is dark enough to see stars,
- the player has open sky overhead,
- the weather is clear enough,
- the player is looking at visible stars.

The telescope overlay can still open when those conditions are not met, but drawing and star-specific readouts may be unavailable.

The astrolabe is predictive rather than observational, so it works in daylight, indoors, during bad weather, and when its selected target is below the horizon.

## Meteor Showers

Meteor showers recur at the same fraction of every configured world year rather than on a hard-coded
day number. A shower becomes worth watching when its season is near the peak, its radiant is above
the horizon, and the sky is dark. A bright moon suppresses most faint meteors but does not eliminate
the brightest ones.

The strongest catalog shower is the Geminids. On the default 108-day calendar, a useful test is
**month 9, day 8 at about 02:00**, from roughly **32.5° north**. This is close to the peak while the
radiant is high; the exact count still changes with longitude and moon phase. Use `.stars debug` to
check the latitude reported by AstraTerra.

Each streak points back toward its shower's radiant. Streaks close to that point are short, while
streaks farther across the sky are longer because the parallel meteor paths are seen in perspective.

## Comets

Four comets return on their real orbital periods, counted in world years so a period means the same
thing whatever your world's day count is: Machholz about every 5 years, Tuttle about every 14,
Tempel-Tuttle about every 33, and Halley about every 75. Each is the parent of a meteor shower
already in the sky — the Quadrantids, the Ursids, the Leonids, and both the Eta Aquariids and the
Orionids.

An apparition lasts a couple of world weeks. Across it the comet moves along its own track against
the stars, and brightens as it rounds perihelion before fading again. Its tail and its coma build and
fade together, so it arrives gradually rather than switching on — for the first nights it is a faint
smudge, and only near perihelion is it the most interesting thing in the sky.

The tail points **away from the sun**, not along the comet's motion, and swings right around as the
comet passes perihelion. Sunlight and the solar wind push it, so it always leans anti-sunward
whichever way the comet is travelling.

The Sextant sights a comet like anything else once it is up, and it keeps its name without a book:
where a planet is a discovery you make by watching a star wander, a comet is an event the sky
announces.

The Astrolabe lists every comet whether it is here or not. One that is away reads
`away, returns in 340 days`, and scrolling the forecast that far ahead turns the line into a real
position — so you can find out where to be, and when, long before it arrives. `.stars comets` reports
the same thing for the whole catalog at once.

The periods and the parent showers are real. The tracks across the sky and the peak brightnesses are
authored for the game rather than computed from an orbit, in the same spirit as the stylised star
brightness: a comet you can predict and watch build is worth more here than an eccentricity solved to
four decimal places.

## Constellation Visibility

Constellations are visible only while you hold their written AstraTerra constellation book in your left hand. A written book is portable: another player can hold the same book to see and use the same saved constellations.

## Constellation Journal

Saved constellations are written into a blank normal book held in your left hand. You also need ink and quill in your inventory to create, add, remove, rename, or build constellations. Once AstraTerra writes the first constellation into the book, vanilla book editing is locked, but AstraTerra can still update the constellation journal.

AstraTerra includes authored Modern IAU constellation line data. These are normal journal entries once built with commands, so you can inspect, rename, or delete them like hand-drawn constellations.

Use the [Constellation Build Cheat Sheet](constellation-cheat-sheet.md) to find the three-letter code for any of the 88 included patterns.

## Quick Start, for now.

1. In creative mode, open the creative inventory and use the **AstraTerra** tab to drag out a Brass Telescope, Sextant, Calibrated Astrolabe, **Star Catalog**, or **The Zodiac**. Survival players can craft the instruments instead.
2. Wait for a dark, clear night with open sky overhead.
3. Put a blank normal book (or a catalog book from the creative tab) in your left hand and keep ink and quill in your inventory.
4. Hold right click with the telescope to scope in.
5. Middle click to switch to Draw mode.
6. Drag between visible guide stars to create a constellation segment.
7. Run `.stars list` and `.stars info selected` to inspect the saved constellation.
8. Use the Sextant on a visible star to read its angle above the horizon.
9. Hold a Calibrated Astrolabe with the written constellation book in your left hand and forecast the constellation's next transit.

## Starfield Comparison Debug Modes

Right now available for debug purposes.

Use `.stars starfield astraterra|both|vanilla` to switch the visible starfield immediately:

- `astraterra` shows only AstraTerra's Earth-based star catalog and is the default.
- `both` overlays AstraTerra and Vintage Story stars for direct alignment comparison.
- `vanilla` shows only Vintage Story's original cubemap starfield.

The selected value is saved as `StarfieldMode` in `ModConfig/astraterra.json` and persists across restarts. AstraTerra leaves Vintage Story's original star textures untouched and controls the two render passes at runtime.
