# AstraTerra

![astraterra](modicon.png)

AstraTerra turns the night sky into an 5000+ for Vintage Story 1.22+ The sky changes with latitude, season, and time; astronomical tools let you observe it, measure it, record your own constellations, and plan when to find them again. This allows to potentially remove maps, and calendars.

Teams could have someone focused on server on keeping track of time, and allow explorers to use navigational instrument to determine their location and path.

Stil in alpha. Many item models and recipes are placeholders, along with the UI/UX

Do check the third party notices for the original creators for some placeholder models. If you are not attributed, or if the license was not read correctly, please give us the chance to correct it.

Always looking for contributions, models, code, docs etc.

## Features

### An Earth night sky

![AstraTerra's compact, magnitude-scaled stars over a Vintage Story landscape](docs/screenshots/starfiel.png)

A catalog of more than 5,000 naked-eye stars replaces the vanilla cubemap with a rotating celestial sphere. Brightness and apparent size follow stellar magnitude, and the visible sky changes as you travel north or south and as the world moves through its year. Fifty registered deep-sky photographs reward careful telescope observation.  
Star locationa and Images are from Stellarium's database.

#### Note

Without a clear date to star from we just using the current Earth-centered locations.

Right now the star field is only defined by the star catalog configuration,
found within a JSON file in the mod's asset. The current one was build using Stellarium dataset. One could procedurally generate it during world creation, or add/edit stars.

Similarly the deep sky views could be generated or use custom assets, or place a easter egg in your server ;).

### Measure the sky with a Sextant

Hold right click and sight a star, the sun, or the moon to read its angle above the horizon. The sun and moon can be shot whenever they are up, so the Sextant works in daylight too — a daytime moon is a perfectly good sight. While sighting, middle click cycles through angle only, a rose equatorial grid, a cyan azimuthal grid, and both grids. UI/UX may change depending on feedback.

![Sextant usage](docs/screenshots/sextant_azimuthal_grid.png)

### Plan observations with a Calibrated Astrolabe

Hold a written constellation book in your left hand and use the Calibrated Astrolabe to see a constellation's compass direction, altitude, rising or setting state, next transit, and horizon status. Middle click changes the target; scroll forecasts by an hour, or sneak-scroll by seven days.

It also tells the time of night, reading the hour off the sun's real position: the hour of the world day, whether it is daylight, dusk, night, or dawn, and how long until the next sunrise or sunset. The clock follows the forecast, so you can find the hour a constellation is best placed before waiting for it.

![Astrolabe usage](docs/screenshots/astrolabe.png)

### Observe and record constellations

![The Brass Telescope scoped view with a recorded constellation](docs/screenshots/create_constellation.png)

The Brass Telescope provides a scoped observation view with several zoom levels. Draw between guide stars, inspect and name saved patterns, or remove individual segments. Constellations are stored in a written vanilla book, so the same journal can be carried, shared, and read by another player. A stronger Precision Telescope and all 88 authored Modern IAU patterns are also included.

### Watch seasonal meteor showers

Nine major annual showers recur at fixed points in the world's year. Their visible rate follows the
published peak strength, how high the radiant is above the horizon, sky darkness, and moonlight.
Short streaks near the radiant and longer streaks farther away preserve the perspective that makes
a real shower appear to radiate from one point in the sky.

## Roadmap

In no order what so ever. Items with an issue have a design sketch and a suggested implementation behind the link; the moving sky objects are grouped under the [Moving Sky Objects](https://github.com/lalmei/astraterra/milestone/1) milestone.

- Ability to lay down looking up.
- Ability to keep astrolabe calibrated to a specific latitude, or recalibrate it to a different lattitude without the star catalog.
- New models astrological instruments.
- Redo recipes for instruments and calibration.
- Recipe for specific lenses, using gem grinding tools (use vanilla if available since it is part of the VS roadmap)
- Milky Way rendering.
- Modify architecture for more easier customization. Such as custom deep sky objects.
- Star Catalog randomizer loot tables for chest and ruins. (i.e. create a star catalog with Maya constellations for a Mayan Ruin, or other sky cultures.) much of the backend is there since sterallarium already contains much of this datasets.
- [Comets](https://github.com/lalmei/astraterra/issues/38). Authored apparitions with an anti-sunward tail.
- [Planets](https://github.com/lalmei/astraterra/issues/37). The five naked-eye wanderers, retrograde motion included.
- New Classes: Astronomer, Surveyer, High-Priest. The Surveyer's tool is sketched in [sextant on land](https://github.com/lalmei/astraterra/issues/41) — sighting a mountain to get its distance and height.

Planets and comets need a [shared ephemeris foundation](https://github.com/lalmei/astraterra/issues/36), since their right ascension and declination change with time. Meteor radiants are fixed like catalog stars and do not need that dependency. [Instrument targeting](https://github.com/lalmei/astraterra/issues/40) will let the astrolabe and sextant point at moving bodies and meteor radiants.

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
