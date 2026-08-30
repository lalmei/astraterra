# AstraTerra

![astraterra](modicon.png)

AstraTerra replaces the Vintage Story night sky with a real one: over 5,000 catalog stars, five planets on their true orbits, nine annual meteor showers, and comets that return on their own schedule, for Vintage Story 1.22+. The sky changes with latitude, season, and time; astronomical tools let you observe it, measure it, record your own constellations, and plan when to find them again. This allows to potentially remove maps, and calendars.

Teams could have someone focused on server on keeping track of time, and allow explorers to use navigational instruments to determine their location and path.

Still in alpha. Many item models and recipes are placeholders, along with the UI/UX

Do check the third party notices for the original creators for some placeholder models. If you are not attributed, or if the license was not read correctly, please give us the chance to correct it.

Always looking for contributions, models, code, docs etc.

## Features

### An Earth night sky

![AstraTerra's compact, magnitude-scaled stars over a Vintage Story landscape](docs/screenshots/starfiel.png)

A catalog of more than 5,000 naked-eye stars replaces the vanilla cubemap with a rotating celestial sphere. Brightness and apparent size follow stellar magnitude, and the visible sky changes as you travel north or south and as the world moves through its year. Fifty registered deep-sky photographs reward careful telescope observation.  
Star locations and images are from Stellarium's database.

Behind them is the galaxy's own glow: a broad band with a bright core towards Sagittarius, split by the dark dust lanes of the Great Rift, turning with the rest of the sky and leaning at whatever angle your latitude puts it. It is gone in twilight, back once the night is properly dark, and mostly washed out by a bright moon. It is generated from a model of the galaxy rather than photographed, so its shape can be tuned, or switched off entirely with `MilkyWayBrightness`.

#### Note

Without a clear date to start from we are just using the current Earth-centered locations.

Right now the star field is only defined by the star catalog configuration,
found within a JSON file in the mod's asset. The current one was build using Stellarium dataset. One could procedurally generate it during world creation, or add/edit stars.

Similarly the deep sky views could be generated or use custom assets, or place a easter egg in your server ;).

### Measure the sky with a Sextant

Hold right click and sight a star, the sun, or the moon to read its angle above the horizon. The sun and a visibly lit moon can be shot whenever they are up, so the Sextant works in daylight too. A daytime crescent is a perfectly good sight; an invisible new moon is not. While sighting, middle click cycles through angle only, a rose equatorial grid, a cyan azimuthal grid, and both grids. UI/UX may change depending on feedback.

![Sextant usage](docs/screenshots/sextant_azimuthal_grid.png)

**Sneak while sighting to write the reading down.** With a writable book in your left hand and ink and quill in your inventory, the book gains a dated entry: the angle, the bearing, how bright it looked, the day and hour, and the latitude you stood at. It does not record what you sighted. Working that out is the game. `.stars sightings` gathers your entries into the sets it takes to be one body and says how far the nights moved each one; `.stars classify <set> star|wanderer|comet [name]` is where you say what it was. Name a wanderer correctly and your name for it becomes the name every instrument uses.

### Plan observations with a Calibrated Astrolabe

Hold a written constellation book in your left hand and use the Calibrated Astrolabe to see a constellation's compass direction, altitude, rising or setting state, next transit, and horizon status. Middle click changes the target; scroll forecasts by an hour, or sneak-scroll by seven days.

It also tells the time of night, reading the hour off the sun's real position: the hour of the world day, whether it is daylight, dusk, night, or dawn, and how long until the next sunrise or sunset. The clock follows the forecast, so you can find the hour a constellation is best placed before waiting for it.

An astrolabe is engraved for one latitude, so a new one is blank until you cut its plate: stand under open sky after dusk and sneak-hold right click while it sights the pole. It needs no star catalog and no brass to do this, only the sky. Afterwards every reading answers for the latitude it was cut at. Travel far enough and it tells you how far you have strayed, and eventually it asks to be recut. Its clock is not engraved and follows you wherever you go.

Every answer also says what it rests on: `from 3 sightings over 11 days` for a body you established yourself, `drawn from 7 stars` for one of your figures, `from the almanac, not your sightings` for a comet. A thin answer that sounds as confident as a thick one is a broken answer, whatever its numbers say.

![Astrolabe usage](docs/screenshots/astrolabe.png)

### Observe and record constellations

![The Brass Telescope scoped view with a recorded constellation](docs/screenshots/create_constellation.png)

The Brass Telescope provides a scoped observation view with several zoom levels. Draw between any two stars you can see, inspect and name saved patterns, or remove individual segments. Constellations are stored in a written vanilla book, so the same journal can be carried, shared, and read by another player. A stronger Precision Telescope and all 88 authored Modern IAU patterns are also included.

### Lie down and watch the sky

![The seraph lying on its back in a meadow, hands folded behind the head](docs/screenshots/stargazing.png)

Press **Z** to lie on your back on the ground, the same kind of toggle as sit-on-G. The seraph crouches, sits, and lowers itself down over about half a second, then rests with one knee drawn up and empty hands folded behind the head. In first person the camera drops to eye level with the grass and looks straight up, so the sky fills the view. Move, jump, or press **Z** again to stand. Telescopes, the Sextant, and the Astrolabe all still work while you are lying down. The binding is remappable under Controls as **Lie down**.

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
photograph, at the width its globe actually subtends -- so Mars swells towards opposition and shrinks
to a speck on the far side of its orbit -- and showing the face its phase calls for: Venus runs
through crescent, half and full, with the lit limb turned towards wherever the sun is. Saturn arrives
with its rings.

Beside Jupiter are the four moons Galileo saw, and beside Saturn are five of its own. They swing back
and forth on their real orbits, the inner ones over a night and the outer ones over a fortnight, and
one that passes behind its planet is gone until it comes out the other side. None of it is visible to
the naked eye: put the glass down and the planets are points of light again.

### The moon, from photographs

Vintage Story's moon steps aside for eight photographs of the real one, turning through its phases as
the calendar says it should, with the bright limb pointing at the sun wherever the sun is. It is drawn
smaller than the game's own moon and larger than the real one -- about four times life size, which is
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

> ### New in v0.5.4: the Milky Way, and a sky you work out for yourself
>
> The galaxy's own glow is in the sky. And the instruments stopped handing you answers: what a
> moving body is, and what your astrolabe is engraved for, are now things you establish by
> observing and writing it down.
>
> - **The Milky Way.** A broad band with a bright core towards Sagittarius, split by the dark dust
>   lanes of the Great Rift, turning with the rest of the sky and leaning at whatever angle your
>   latitude puts it. It is generated from a model of the galaxy rather than photographed, so the
>   rift and the amber core fall out of the model rather than being painted. Gone in twilight, back
>   once the night is properly dark, washed out by a bright moon, so it is best near new moon. It
>   costs nothing measurable: 0.16–0.55 ms a frame with it on against 0.50–0.55 with it off, and no
>   extra draw calls. `MilkyWayBrightness` in `ModConfig/astraterra.json` takes `0`, and
>   `.stars render milkyway off` switches it off for a session.
> - **Write down what you saw, not what it was.** Sneak while holding the Sextant up, with a
>   writable book in your left hand and ink and quill in your inventory, and the book gains a dated
>   entry: the angle above the horizon, the bearing, how bright it looked, the day and hour, and the
>   latitude you stood at. The entry does not say what you sighted, and that is the point: it
>   records that *something* stood there at that hour.
> - **Then work it out.** `.stars sightings` lays your entries side by side, gathers them into the
>   sets it takes to be one body, and says how far apart the nights put them.
>   `.stars classify <set> star|wanderer|comet [name]` is where you say what it was. Nothing grades
>   the answer; that is what makes it yours. Call a set a wanderer and, if exactly one of the
>   wandering bodies was in all those places on all those nights, your name for it becomes the name
>   every instrument uses.
> - **Every answer says what it rests on.** `from 3 sightings over 11 days, to 0° 01′` for a
>   wanderer you established yourself, `drawn from 7 stars` for one of your own figures,
>   `from the almanac, not your sightings` for a comet, whose returns are somebody else's
>   arithmetic. A planet written down under an older version, with no sightings behind it, still
>   aims, but it says it is thin.
> - **The astrolabe's plate is yours to cut.** A new one is blank. Stand under open sky after dusk
>   and sneak-hold right click while it sights the pole, and it is engraved for that latitude. No
>   star catalog and no brass, only the sky.
> - **Comets come back on their own schedule.** Four of them, on their real orbital periods. The
>   first is Machholz, a little under two world years into a new world, so a world started today
>   already has one coming. It returns about every five years after that.
> - **An Astronomy tab in the in-game handbook**, so the instruments explain themselves without a
>   second screen.
> - **Fixed: pressing Z could crash the client.** The lie-down clips added in v0.5.3 left half of
>   some keyframes unwritten, and the game reads those fields without checking whether they are
>   there. The first time anyone lay down, the client went down with it. If you are on v0.5.3, this
>   is the update you want.
> - **For modders: the sky is replaceable.** Both the star catalog and the solar system can be
>   swapped for your own, so another mod can ship a different sky rather than patching this one's.
>
> **Two numbers we would like a second opinion on.** How bright the Milky Way should be, and how
> dark the night has to get before it appears. Both are single numbers, easy to move, and better
> judged by someone out there looking than by anyone reading a diff.
> [Open an issue](https://github.com/lalmei/astraterra/issues/new/choose), and screenshots are
> welcome.
>
> **One known problem, found while measuring the Milky Way.** Raising a telescope where many
> deep-sky plates are above the horizon is expensive, around 10 ms a frame with roughly forty plates
> up and the occasional long frame, because every visible plate re-uploads its geometry every frame.
> It is not new and it is not the Milky Way, it is just the first time it has been measured
> properly. Tracked as [#105](https://github.com/lalmei/astraterra/issues/105) and next on the list.

> ### Coming next door: a mod that replaces the sky outright
>
> Now that the star catalog and the solar system can both be swapped, a companion mod is in the
> works that does exactly that: it takes AstraTerra as a dependency and puts its own sky in place of
> this one. If you have been waiting to build a sky of your own, that is the hook to build it on,
> and the interfaces it uses are the ones any mod can use. More when it is ready.

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

> ### Also in v0.5.2: the sky looks different, and we would like to hear about it
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
>   twentieth of a degree. That is under a pixel at the naked eye, but seven to fourteen through a
>   scope, and it arrived as a visible step several times a second.
> - **Scoped stars are the size the naked eye shows them at.** Raising a telescope used to halve every
>   bright star, because the scoped sprite fills less than half the quad the naked-eye one does.
> - **Deep-sky plates now fade the catalogue out underneath them.** A plate is a photograph with its
>   own stars already in it, so over the Pleiades the photograph's stars are the stars; a degree away
>   the sky is the catalogue's again.
> - **Star colours read slightly more saturated.** Colour rides on the mesh vertices now, where the
>   old path applied each tint twice.
>
> If a line looks too heavy or too faint, a star too large under the scope, a plate too empty at its
> edge, or a colour wrong, please say so, with a screenshot if you can. Line width, star size, and
> how hard a plate fades are all single numbers and easy to move.
> [Open an issue](https://github.com/lalmei/astraterra/issues/new/choose).

## Roadmap

In no order what so ever. Items with an issue have a design sketch and a suggested implementation behind the link; the moving sky objects are grouped under the [Moving Sky Objects](https://github.com/lalmei/astraterra/milestone/1) milestone.

- New models astrological instruments.
- Redo recipes for instruments and calibration.
- Recipe for specific lenses, using gem grinding tools (use vanilla if available since it is part of the VS roadmap)
- Modify architecture for more easier customization. Such as custom deep sky objects. The star catalog and the solar system are replaceable as of v0.5.4; deep sky objects are not yet.
- Star Catalog randomizer loot tables for chest and ruins. (i.e. create a star catalog with Maya constellations for a Mayan Ruin, or other sky cultures.) much of the backend is there since sterallarium already contains much of this datasets.
- New Classes: Astronomer, Surveyer, High-Priest. The Surveyer's tool is sketched in [sextant on land](https://github.com/lalmei/astraterra/issues/41), sighting a mountain to get its distance and height.

Meteor radiants are fixed like catalog stars and do not need the [shared ephemeris foundation](https://github.com/lalmei/astraterra/issues/36) that the planets and comets are built on. [Instrument targeting](https://github.com/lalmei/astraterra/issues/40) will let the astrolabe and sextant point at moving bodies and meteor radiants.

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
