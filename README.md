# AstraTerra

![astraterra](modicon.png)

AstraTerra replaces Vintage Story's night sky with a real star catalog: more than 5,000 stars, five planets on Keplerian orbits, nine annual meteor showers, and four comets that return on their own schedule. The sky tilts with latitude, turns with the hour, and changes with the season. For Vintage Story 1.22+.

Form a clay Sky Disc on your first day and mark where the sun rises and sets; after a year of marks it can tell you the length of the year, your latitude, where east or west lies, and when the sun will next stand still. Hold a telescope to zoom, resolve planets and deep-sky objects, and draw constellations into a vanilla book. Sight the sun, moon, a star, a planet, or a comet with a Sextant and sneak to write the reading down. Forecast a recorded figure, a named wanderer, or a comet with a Calibrated Astrolabe, and read the hour off the sun. Lie on your back (default Z) so the sky fills the view.

`.stars calendar clock` or `none` will hide the vanilla date so the Sky Disc is the thing that tells you when the year turns. On a server, one player can keep the year and another can navigate by the instruments.

Still in alpha. Many item models and recipes are placeholders, along with the UI/UX

Do check the third party notices for the original creators for some placeholder models. If you are not attributed, or if the license was not read correctly, please give us the chance to correct it.

Always looking for contributions, models, code, docs etc.

## Features

### An Earth night sky

![AstraTerra's compact, magnitude-scaled stars over a Vintage Story landscape](docs/screenshots/starfiel.png)

Look up. The vanilla cubemap is gone. In its place is a catalog of more than 5,000 stars, brightness and size following magnitude, turning with the hour and tilting as you walk north or south. Fifty deep-sky photographs resolve only under a telescope. Star positions and those plates come from Stellarium.

#### Note

Without a clear date to start from we are just using the current Earth-centered locations.

Right now the star field is only defined by the star catalog configuration,
found within a JSON file in the mod's asset. The current one was build using Stellarium dataset. One could procedurally generate it during world creation, or add/edit stars.

Similarly the deep sky views could be generated or use custom assets, or place a easter egg in your server ;).

### Mark the year on a Sky Disc

Form one from clay and fire it, or craft a copper or bronze one. Stand under open sky as the sun touches the horizon, then sneak and hold right click to scratch that day's mark. Right click holds the disc up to read the band. Scroll turns it in your hand. Sneak and right click the ground to set it down.

After the band reaches an edge and turns back, the disc can tell you the length of the year, your latitude, where east or west lies, and when the sun will next stand still. Clay holds a five-degree notch; metal holds two and a half. The first mark binds the disc to that latitude.

While the disc is held up under the stars, left-drag from star to star to cut one connected constellation into it. Press a figure into raw clay before firing; a fired clay disc will not take another line.

`.stars calendar clock` hides the vanilla date and keeps the hour. `.stars calendar none` hides both.

### Measure the sky with a Sextant

Hold right click and put the centre of your view on the sun, the moon, a star, a planet, or a comet. The readout is its angle above the horizon. The sun and a visibly lit moon can be shot whenever they are up, so the Sextant works in daylight. An invisible new moon is not offered as a target. While sighting, middle click cycles through angle only, a rose equatorial grid, a cyan azimuthal grid, and both grids.

![Sextant usage](docs/screenshots/sextant_azimuthal_grid.png)

**Sneak while sighting to write the reading down.** With a writable book in your left hand and ink and quill in your inventory, the book gains a dated entry: the angle, the bearing, how bright it looked, the day and hour, and the latitude you stood at. It does not record what you sighted. Working that out is the game. `.stars sightings` gathers your entries into the sets it takes to be one body and says how far the nights moved each one; `.stars classify <set> star|wanderer|comet [name]` is where you say what it was. Name a wanderer correctly and your name for it becomes the name every instrument uses.

### Plan observations with a Calibrated Astrolabe

Recover a ruined astrolabe and combine it with a brass plate. Hold the Calibrated Astrolabe in your main hand and a written book in your left hand, then hold right click. Middle click changes the target; scroll forecasts by an hour, or sneak-scroll by seven days. The readout is compass direction, altitude, rising or setting, next transit, and whether the target can never rise where the plate was cut.

A new one reads `no plate` and will not place a star. Stand under open sky after dusk and sneak-hold right click while it sights the pole. Afterwards every position it gives is for that latitude. Travel far enough and it reports the drift; past about eight degrees it asks to be recut. The clock is not engraved and follows you. It still shows if you hold no book.

It aims with your book, not with the sky's own catalog. A wanderer nobody has picked out is not on the list. Comets are listed whether or not they are here.

Every answer also says what it rests on: `from 3 sightings over 11 days` for a body you established yourself, `drawn from 7 stars` for one of your figures, `from the almanac, not your sightings` for a comet. A thin answer that sounds as confident as a thick one is a broken answer, whatever its numbers say.

![Astrolabe usage](docs/screenshots/astrolabe.png)

### Observe and record constellations

![The Brass Telescope scoped view with a recorded constellation](docs/screenshots/create_constellation.png)

Hold right click to scope in. Scroll to zoom (five steps on the brass telescope, ten on the precision one). Middle click cycles modes: **Observe**, **Create Constellation**, **Inspect Constellation**, **Remove Segment**. In Create, left-drag from one visible star to another. Any star on screen can start or end a line. Drawing needs a book in your left hand and ink and quill in your inventory. Hand that book to another player and they see your figures.

`.stars build Ori` writes one of the 88 IAU figures into the held book. Creative inventory also has a full Star Catalog and The Zodiac.

### Lie down and watch the sky

![The seraph lying on its back in a meadow, hands folded behind the head](docs/screenshots/stargazing.png)

Press **Z** to lie on your back on the ground, the same kind of toggle as sit-on-G. The seraph crouches, sits, and lowers itself down over about half a second, then rests with one knee drawn up and empty hands folded behind the head. In first person the camera drops to eye level with the grass and looks straight up, so the sky fills the view. Move, jump, or press **Z** again to stand. Telescopes, the Sextant, the Sky Disc, and the Astrolabe all still work while you are lying down. The binding is remappable under Controls as **Lie down**.

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

> ### Wait for a comet

Four comets return on their real orbital periods. Each is up for a couple of world weeks, and across those nights it crosses the sky, brightens as it rounds perihelion, and fades again.

Its tail points **away from the sun**, never along its motion, and swings right around as it passes perihelion, which is the thing about a comet that most pictures get wrong. Tail length and coma brightness build and fade together, so an apparition arrives gradually rather than switching on.

You will not wait long for the first. **Machholz** arrives a little under two world years in and comes back every five and a bit. **Halley** makes its first pass in your third year, and it is the bright one, the only comet here that outshines every star in the sky. **Tuttle** follows in year four, then every fourteen; **Tempel-Tuttle** in year nine, then every thirty-three.

Do go out for Halley. Its second pass is in world year 78, and the honest advice for anyone who sleeps through the first one is to start a family, raise them well, and impress upon them the importance of looking up.

The Astrolabe lists every comet whether or not it is here. One that is away reads `away, returns in 340 days`, and scrolling the forecast that far ahead turns the line into a real position, so a comet is something you can plan a journey around rather than something you happen to catch.

`.stars comets` reports every comet's state: up now, or how long until it returns.

### Point a telescope at a planet

Raise a telescope on a planet and it stops being a point of light. Each planet is drawn from its own
picture, at the width its globe actually subtends -- so Mars swells towards opposition and shrinks
to a speck on the far side of its orbit. Saturn arrives with its rings.

There are two sets of pictures. Pixel art is what you get, drawn to sit with the game's own art;
`.stars solar-system photo` swaps in photographs of the real surfaces instead, and those go further:
they show the face a planet's phase calls for, so Venus runs through crescent, half and full with the
lit limb turned towards wherever the sun is. Everything else -- where a body is, how wide it draws,
which moons are out -- is the same under either set.

Beside Jupiter are the four moons Galileo saw, and beside Saturn are five of its own. They swing back
and forth on their real orbits, the inner ones over a night and the outer ones over a fortnight, and
one that passes behind its planet is gone until it comes out the other side. None of it is visible to
the naked eye: put the glass down and the planets are points of light again.

### The moon, with a face of its own

Vintage Story's moon steps aside for eight faces of the real one, turning through its phases as
the calendar says it should, with the bright limb pointing at the sun wherever the sun is. The faces
come from the same two sets the planets do -- pixel art by default, photographs of the real surface
under `.stars solar-system photo`. It is drawn smaller than the game's own moon and larger than the real one -- about four times life size, which is
where a moon that reads properly by eye and a moon that fits in an eyepiece meet -- so a telescope
raised on it shows craters and maria rather than a bigger smooth disc. The moonlight, the phase the game reports and the length of the night are untouched.

### Hang another world in the sky

Some worlds are not planets. A world that is a moon of a gas giant sees that giant instead of a
moon of its own -- and because such a world is tidally locked to its parent, the giant never rises
and never sets. It hangs at one spot on the sky forever, tens of degrees across, going through its
phases as the sun goes round: full near midnight, dark at noon. Sibling moons drift past it at the
rate the two orbits beat against each other.

AstraTerra does not decide any of that. It draws whatever near bodies a mod hands it through
`ReplaceNearBodies`: each body's face as a picture, how wide it is, where it hangs, and how fast that
drifts. The terminator is not painted into that picture -- the renderer shades the disc per vertex
from the sphere normal and the real sun direction, so a body curves and darkens toward its limb the
way a globe does. A catalog can also ask Vintage Story's own moon to stand down, which a world with
no moon of its own should. Only the drawing stops: moonlight, the moon phase the calendar reports,
and the length of the day are untouched.

A near body can be sighted as well as seen. The sextant reads it in broad daylight, alongside the sun
and the moon and above the dark-sky rule the stars and planets sit under -- a giant filling a fifth
of the sky at noon is the most conspicuous thing that world has, and an instrument that would not
measure it would be refusing the one sight the world offers. The sight latches anywhere on the disc
rather than at its centre, a sibling moon crossing the parent's face takes precedence over the face
behind it, and on a world whose catalog stands Vintage Story's moon down, that moon is no longer
sightable either.

[AstraExtera](https://github.com/lalmei/astraextera) is what fills this in today, from the giant it
authored for your save.

> ### New in v0.6.0
>
> Sky Disc, planets through a telescope, and a way to hide the vanilla date.
>
> - **Sky Disc.** Form one from clay and fire it, or craft copper or bronze. Sneak and hold right
>   click at sunrise or sunset to scratch the sun's place on the horizon. Right click reads the
>   band. After the band turns back from an edge: year length, latitude, east or west, next
>   solstice. Sneak-right-click the ground to set it down. Clay notches are 5°; metal 2.5°.
> - **One constellation on the disc.** Hold it up and drag from star to star. One connected
>   figure. Press it into raw clay before firing; fired clay will not take another line. Hold the
>   disc and the figure returns to the sky.
> - **Hide the date.** `.stars calendar clock` keeps the hour. `.stars calendar none` hides both.
>   Saved locally.
> - **Planets through a telescope.** Raise the glass and they become discs. Mars grows toward
>   opposition. Saturn has rings. Jupiter's four moons and five of Saturn's move on their own
>   orbits and vanish behind the planet. Put the telescope down and they are points of light again.
> - **Moon.** Eight faces, bright limb toward the sun. A telescope shows a surface. Moonlight and
>   the calendar phase are unchanged.
> - **Pixel or photo.** Pixel art is the default. `.stars solar-system photo` swaps in
>   photographs. Positions, sizes, and moon orbits stay the same. Photographs also show Venus's
>   phases.
> - **Other worlds.** A replacement catalog can hang a nearby planet and sibling moons, and the
>   Sextant can sight them in daylight. [AstraExtera](https://github.com/lalmei/astraextera) uses
>   this.
>
> Also fixed: a telescope that stayed scoped after swapping slots, deep-sky plates rebuilding every
> frame, polar sunrises the clock could miss, planets flickering behind their old glow, invisible
> new moons offered to the Sextant, and journals overwritten when they could not be read.

<details>
<summary><strong>🌌 Update v0.5.4</strong></summary>
<br>
Sextant sightings, astrolabe plate cutting, comets, and a handbook tab. If you are on v0.5.3, this
also stops Z from crashing the client.

- **Sextant sightings.** Sneak while holding the Sextant up. Book in the left hand, ink and quill
  in inventory. The book gets a dated entry: altitude, bearing, brightness, day and hour, latitude.
  It does not name what you sighted.
- **Classify later.** `.stars sightings` groups the entries. `.stars classify 2 wanderer Ember` is
  where you say what a set was. If exactly one wanderer fits all those places, that name is what
  the instruments use.
- **Astrolabe provenance.** Each answer says what it rests on: how many sightings, `drawn from N
  stars`, or `from the almanac, not your sightings` for a comet. A planet recorded with no
  sightings still aims, and says so.
- **Cut the plate.** A new astrolabe is blank. After dusk, sneak-hold right click under open sky.
  Readings are for that latitude. Travel far enough and it asks to be recut.
- **Comets.** Four, on their own periods. Machholz first, a little under two world years in, then
  about every five. Up for a couple of world weeks.
- **Handbook.** Press H, Astronomy tab.
- **Crash fix.** Pressing Z on v0.5.3 could crash the client. The lie-down clips had unwritten
  keyframes.
- **Modders.** Star catalog and solar system can be swapped.
  [AstraExtera](https://github.com/lalmei/astraextera) uses this.
- **Milky Way.** Added behind the stars. Off with `MilkyWayBrightness` 0 in
  `ModConfig/astraterra.json`, or `.stars render milkyway off` for the session.

Still tuning brightness and how dark the night has to be before the band shows.
[Open an issue](https://github.com/lalmei/astraterra/issues/new/choose) if you have a look.

Deep-sky plates rebuilding every frame under a telescope was found while measuring this release
([#105](https://github.com/lalmei/astraterra/issues/105)) and fixed in v0.6.0.

</details>

<details>
<summary><strong>⚡ Update v0.5.2: the sky pass got cheap</strong></summary>
<br>
The whole sky used to be drawn one object at a time, every frame. It is now batched, cached, and
measured, and the numbers moved by more than a little.

| | before | after |
| --- | --- | --- |
| Draw calls per frame, carrying a journal | 6,716 | **4** |
| Draw calls per frame, no journal | 2,988 | **3** |
| Record allocations per second | ~400,000 | **none in steady state** |
| Constellation line geometry | 3,728 quads | **148** |
| Star projections per second | 60 | **~5** |

The before figures come from a real `client-main.log` with 2,988 stars and 3,728 constellation
dots visible, where the star projection alone cost over 10 ms a frame and produced about a gigabyte
of garbage a minute, which players saw as stutter and as a client that would not give memory back.

Stars, planets and constellation lines are each one batched mesh now, rebuilt only when the sky has
actually turned, with the turn between rebuilds carried by a single rotation so nothing steps.

You can measure it yourself: `.stars render stars|constellations|deepsky|meteors|comets|milkyway|all on|off`
switches each path off independently, and the debug log reports milliseconds, draw calls and mesh
uploads for the sky pass every 30 seconds.

</details>

## Roadmap

In no order what so ever. Items with an issue have a design sketch and a suggested implementation behind the link; the moving sky objects are grouped under the [Moving Sky Objects](https://github.com/lalmei/astraterra/milestone/1) milestone.

- ~~Seasonal meteor showers.~~ Shipped.
- ~~Planets (named specific wandering bodies).~~ Shipped.
- ~~Ability to lay down looking up.~~ Shipped.
- ~~Ability to keep the astrolabe calibrated to a specific latitude, or recalibrate it to a different latitude without the star catalog.~~ Shipped.
- New models astrological instruments.
- Redo recipes for instruments and calibration.
- Recipe for specific lenses, using gem grinding tools (use vanilla if available since it is part of the VS roadmap)
- Modify architecture for more easier customization. Such as custom deep sky objects. The star catalog and the solar system are replaceable as of v0.5.4; deep sky objects are not yet.
- Star Catalog randomizer loot tables for chest and ruins. (i.e. create a star catalog with Maya constellations for a Mayan Ruin, or other sky cultures.) much of the backend is there since sterallarium already contains much of this datasets.
- ~~[Comets](https://github.com/lalmei/astraterra/issues/38). Authored apparitions with an anti-sunward tail.~~ Shipped.
- New Classes: Astronomer, Surveyer, High-Priest. The Surveyer's tool is sketched in [sextant on land](https://github.com/lalmei/astraterra/issues/41), sighting a mountain to get its distance and height.

~~Comets sit on the same [shared ephemeris foundation](https://github.com/lalmei/astraterra/issues/36) the planets already use, since their right ascension and declination change with time.~~ Shipped. Meteor radiants are fixed like catalog stars and do not need that dependency. ~~[Instrument targeting](https://github.com/lalmei/astraterra/issues/40) for the astrolabe and sextant on planets and comets.~~ Shipped. Meteor radiants are not yet targets.

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

Special thanks for discussion to Vintage Story mod [AdAstra](https://mods.vintagestory.at/show/mod/47577)'s LadyLioness.

Additional Super thanks for ALL the feedback and check by the numerous commenters below.
