# AstraTerra

![astraterra](modicon.png)

AstraTerra replaces the Vintage Story night sky with a real one: over 5,000 catalog stars, five planets on their true orbits, and nine annual meteor showers, for Vintage Story 1.22+. The sky changes with latitude, season, and time; astronomical tools let you observe it, measure it, record your own constellations, and plan when to find them again. This allows to potentially remove maps, and calendars.

Teams could have someone focused on server on keeping track of time, and allow explorers to use navigational instruments to determine their location and path.

Still in alpha. Many item models and recipes are placeholders, along with the UI/UX

Do check the third party notices for the original creators for some placeholder models. If you are not attributed, or if the license was not read correctly, please give us the chance to correct it.

Always looking for contributions, models, code, docs etc.

## Features

### An Earth night sky

![AstraTerra's compact, magnitude-scaled stars over a Vintage Story landscape](docs/screenshots/starfiel.png)

A catalog of more than 5,000 naked-eye stars replaces the vanilla cubemap with a rotating celestial sphere. Brightness and apparent size follow stellar magnitude, and the visible sky changes as you travel north or south and as the world moves through its year. Fifty registered deep-sky photographs reward careful telescope observation.  
Star locations and images are from Stellarium's database.

#### Note

Without a clear date to start from we are just using the current Earth-centered locations.

Right now the star field is only defined by the star catalog configuration,
found within a JSON file in the mod's asset. The current one was build using Stellarium dataset. One could procedurally generate it during world creation, or add/edit stars.

Similarly the deep sky views could be generated or use custom assets, or place a easter egg in your server ;).

### Measure the sky with a Sextant

Hold right click and sight a star, the sun, or the moon to read its angle above the horizon. The sun and moon can be shot whenever they are up, so the Sextant works in daylight too — a daytime moon is a perfectly good sight. While sighting, middle click cycles through angle only, a rose equatorial grid, a cyan azimuthal grid, and both grids. UI/UX may change depending on feedback.

![Sextant usage](docs/screenshots/sextant_azimuthal_grid.png)

### Plan observations with a Calibrated Astrolabe

Hold a written constellation book in your left hand and use the Calibrated Astrolabe to see a constellation's compass direction, altitude, rising or setting state, next transit, and horizon status. Middle click changes the target; scroll forecasts by an hour, or sneak-scroll by seven days.

It also tells the time of night, reading the hour off the sun's real position: the hour of the world day, whether it is daylight, dusk, night, or dawn, and how long until the next sunrise or sunset. The clock follows the forecast, so you can find the hour a constellation is best placed before waiting for it.

An astrolabe is engraved for one latitude, so a new one is blank until you cut its plate: stand under open sky after dusk and sneak-hold right click while it sights the pole. It needs no star catalog and no brass to do this, only the sky. Afterwards every reading answers for the latitude it was cut at — travel far enough and it tells you how far you have strayed, and eventually asks to be recut. Its clock is not engraved and follows you wherever you go.

![Astrolabe usage](docs/screenshots/astrolabe.png)

### Observe and record constellations

![The Brass Telescope scoped view with a recorded constellation](docs/screenshots/create_constellation.png)

The Brass Telescope provides a scoped observation view with several zoom levels. Draw between guide stars, inspect and name saved patterns, or remove individual segments. Constellations are stored in a written vanilla book, so the same journal can be carried, shared, and read by another player. A stronger Precision Telescope and all 88 authored Modern IAU patterns are also included.

### Watch seasonal meteor showers

Nine major annual showers recur at fixed points in the world's year. Their visible rate follows the
published peak strength, how high the radiant is above the horizon, sky darkness, and moonlight.
Short streaks near the radiant and longer streaks farther away preserve the perspective that makes
a real shower appear to radiate from one point in the sky.

### Follow the wandering planets

Mercury, Venus, Mars, Jupiter and Saturn move against the fixed stars on real orbits, solved from
published orbital elements rather than animated along a path. A world year is a year of that motion,
whatever the world's day count, so Jupiter takes about twelve world years to come back around and
Venus swings between the evening and morning sky within a season, brightening and fading as it goes.

Chart Mars over a couple of world weeks and it will turn back on itself. Nobody scripted that
retrograde loop: it is what the sky does when the world overtakes a slower planet on the inside.

> ### Update v0.5.2 — the sky pass got cheap
>
> The whole sky used to be drawn one object at a time, every frame. It is now batched, cached, and
> measured, and the numbers moved by more than a little.
>
> | | before | after |
> | --- | --- | --- |
> | Draw calls per frame, carrying a journal | 6,716 | **4** |
> | Draw calls per frame, no journal | 2,988 | **3** |
> | Record allocations per second | ~400,000 | **none in steady state** |
> | Constellation line geometry | 3,728 quads | **148** |
> | Star projections per second | 60 | **~5** |
>
> The before figures come from a real `client-main.log` — 2,988 stars and 3,728 constellation dots
> visible — and the star projection alone cost over 10 ms a frame and produced about a gigabyte of
> garbage a minute, which players saw as stutter and as a client that would not give memory back.
>
> Stars, planets and constellation lines are each one batched mesh now, rebuilt only when the sky has
> actually turned, with the turn between rebuilds carried by a single rotation so nothing steps.
>
> You can measure it yourself: `.stars render stars|constellations|deepsky|meteors|all on|off`
> switches each path off independently, and the debug log reports milliseconds, draw calls and mesh
> uploads for the sky pass every 30 seconds.

> ### Also in v0.5.2 — the sky looks different, and we would like to hear about it
>
> Getting the cost down meant changing how several things are drawn. None of it is settled, and the
> judgements behind it are the kind that are better made by people actually observing than by anyone
> reading a diff.
>
> - **Constellation lines are continuous now**, a thin ribbon per edge instead of a trail of dots, and
>   each line stops short of the stars it joins. The dots were sized in degrees, so magnification
>   pulled them apart into a row of specks; a ribbon holds its weight at any zoom and cannot be
>   mistaken for a star.
> - **The sky turns smoothly through a telescope.** It used to be redrawn only once it had moved a
>   twentieth of a degree — under a pixel at the naked eye, but seven to fourteen through a scope,
>   which arrived as a visible step several times a second.
> - **Scoped stars are the size the naked eye shows them at.** Raising a telescope used to halve every
>   bright star, because the scoped sprite fills less than half the quad the naked-eye one does.
> - **Deep-sky plates now fade the catalogue out underneath them.** A plate is a photograph with its
>   own stars already in it, so over the Pleiades the photograph's stars are the stars; a degree away
>   the sky is the catalogue's again.
> - **Star colours read slightly more saturated.** Colour rides on the mesh vertices now, where the
>   old path applied each tint twice.
>
> If a line looks too heavy or too faint, a star too large under the scope, a plate too empty at its
> edge, or a colour wrong — please say so, with a screenshot if you can. Line width, star size, and
> how hard a plate fades are all single numbers and easy to move.
> [Open an issue](https://github.com/lalmei/astraterra/issues/new/choose).

## Roadmap

In no order what so ever. Items with an issue have a design sketch and a suggested implementation behind the link; the moving sky objects are grouped under the [Moving Sky Objects](https://github.com/lalmei/astraterra/milestone/1) milestone.

- Ability to lay down looking up.
- New models astrological instruments.
- Redo recipes for instruments and calibration.
- Recipe for specific lenses, using gem grinding tools (use vanilla if available since it is part of the VS roadmap)
- Milky Way rendering.
- Modify architecture for more easier customization. Such as custom deep sky objects.
- Star Catalog randomizer loot tables for chest and ruins. (i.e. create a star catalog with Maya constellations for a Mayan Ruin, or other sky cultures.) much of the backend is there since sterallarium already contains much of this datasets.
- [Comets](https://github.com/lalmei/astraterra/issues/38). Authored apparitions with an anti-sunward tail.
- New Classes: Astronomer, Surveyer, High-Priest. The Surveyer's tool is sketched in [sextant on land](https://github.com/lalmei/astraterra/issues/41) — sighting a mountain to get its distance and height.

Comets sit on the same [shared ephemeris foundation](https://github.com/lalmei/astraterra/issues/36) the planets already use, since their right ascension and declination change with time. Meteor radiants are fixed like catalog stars and do not need that dependency. [Instrument targeting](https://github.com/lalmei/astraterra/issues/40) will let the astrolabe and sextant point at moving bodies and meteor radiants.

## Docs

Start here:

- [Player Guide](docs/player/guide.md)
- [Command Reference](docs/player/commands.md)
- [Developer Guide](docs/dev/index.md)
- [Docs Index](docs/index.md)

## Build And Test

```bash
make test
make build
make package
make docs-build
```

For local in-game smoke testing:

```bash
make deploy
```

Then enable AstraTerra in Vintage Story and follow [Manual Verification](docs/dev/manual-verification.md).

## Attribution

Inspired by Minecraft's [Spyglass Astronomy](https://github.com/Nettakrim/Spyglass-Astronomy/tree/Main).

The Brass Telescope item model is adapted from Fuami's MIT-licensed [Spyglass](https://mods.vintagestory.at/spyglass) mod. Modern IAU constellation line data and selected deep-sky assets are adapted from [Stellarium](https://github.com/stellarium/stellarium) sources. The Sextant item model transforms and recipe are adapted from the user's downloaded Realistic Surveying package. See `THIRD_PARTY_NOTICES.md`.

Special thanks to Vintage Story Mod [AdAstra's](https://mods.vintagestory.at/show/mod/47577) LadyLioness.
