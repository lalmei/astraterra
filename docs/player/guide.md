# AstraTerra Player Guide

AstraTerra replaces Vintage Story's night sky with a catalog of more than 5,000 stars. Five planets move among them. Nine meteor showers return each year. Four comets return on their own periods. The sky tilts with latitude, turns with the hour, and changes with the season.

The instruments let you measure what you see, write it down, and plan when to find it again.

Everything below is also written into the Vintage Story handbook. Press **H** and open the **Astronomy** tab. Pressing **H** while hovering an AstraTerra item opens that item's own page.

## What You Can Do

- Watch a catalog sky of more than 5,000 stars that changes as you travel north or south.
- Watch nine annual meteor showers and four returning comets.
- Form a Sky Disc from clay on your first day, mark sunrises and sunsets, and after a year of marks read the length of the year, your latitude, and when the sun will next stand still.
- Cut one constellation into that same disc.
- Use a Brass Telescope to zoom, draw constellation lines into a book, inspect saved figures, and remove segments. A Precision Telescope has ten stronger zoom steps.
- Use a Sextant to read the altitude of the sun, the moon, a star, a planet, or a comet, and sneak to write that reading down.
- Lie on your back with **Z** (remappable as **Lie down**) so the sky fills the view.
- Use a Calibrated Astrolabe to forecast a recorded constellation, a wanderer you have named, or a comet, and to tell the hour from the sun.

Creative inventory also has a **Star Catalog** (all 88 IAU figures), **The Zodiac** (the traditional twelve), and **The Wanderers** (all five planets already named).

## Quick Start

1. In creative, open the **AstraTerra** tab and drag out a Brass Telescope, Sextant, Calibrated Astrolabe, Sky Disc, **Star Catalog**, or **The Zodiac**. Survival players can form a clay disc immediately, and craft the metal instruments later.
2. Wait for a dark, clear night with open sky overhead. Or, on the first evening, just mark sunset with the Sky Disc.
3. Put a blank normal book in your left hand and keep ink and quill in your inventory.
4. Hold right click with the telescope to scope in.
5. Middle click until the scope says **Create Constellation**.
6. Drag between any two visible stars.
7. Run `.stars list` and `.stars info selected` to inspect what you drew.
8. Hold right click with the Sextant on a visible star to read its altitude.
9. Hold the Calibrated Astrolabe with the written book in your left hand and forecast that constellation's next transit.

## The Night Sky

This is Earth's sky. It turns with the hour, tilts with your latitude, and shows different figures in different seasons.

### How to look

Press **Z** to lie on your back, the same kind of toggle as sit-on-G. In first person the camera drops to the grass and looks straight up. Move, jump, or press **Z** again to stand. The binding is remappable under Controls as **Lie down**. **X** stays vanilla off-hand swap.

Telescopes, the Sextant, the Sky Disc, and the Calibrated Astrolabe all still work while you are lying down.

To see stars, draw constellations, or sight anything but the sun and a lit moon, you need:

- a sky dark enough,
- weather clear enough,
- open sky overhead.

A telescope still zooms when those conditions fail; the star work does not. The astrolabe predicts rather than observes, so it works in daylight, indoors, and in bad weather.

### Limits

Vintage Story has latitude. It does not have longitude. The sun's hour depends on Z; walking east or west does not change local solar time. On a world whose climate is not set to realistic, latitude is stuck and the sky does not change as you travel. The equator is not at z = 0. Use `.stars debug` if you need the latitude AstraTerra is actually using.

### Latitude and the turning of the night

Walk north and the pole star climbs; walk south and it sinks, while stars you have never seen rise ahead of you. Near the poles some figures never set, and others never rise. Every instrument here answers for the latitude you are standing at.

The whole sky wheels around the pole. A constellation low in the east after dusk is high overhead hours later. Its best moment is its **transit**, when it crosses the meridian at its highest. In the northern hemisphere, Polaris sits near the north celestial pole: it shows north, and its angle above the horizon is your latitude.

The stars also rise a little earlier each night, so the sky tells the season as well as the hour.

### The Moon

The Moon uses eight faces, with the bright limb turned towards the sun. Vintage Story's moonlight, the calendar's phase, and the length of the night are untouched. A telescope shows a surface rather than a larger smooth disc. Pixel art is the default. `.stars moon photo` uses photographs of the real surface; `.stars moon vanilla` puts Vintage Story's own disc back. The planets are a separate setting: `.stars solar-system`.

## Brass Telescope

A telescope zooms the sky. Under magnification, faint smudges resolve into deep-sky objects, planets stop being points of light, and you can draw constellation lines into a book.

### How to use it

Hold right click to enter the scoped view. While scoped:

- Scroll to change zoom (five steps on the brass telescope).
- Middle click to cycle modes. The name on the scope is the mode you are in.
- **Observe** looks and zooms only.
- **Create Constellation** draws: left-drag from one visible star to another. Any star on screen can start or end a line, not only the named guide stars.
- **Inspect Constellation** names or renames the saved constellation you click.
- **Remove Segment** deletes the segment you click.

Drawing, naming, and removing need a book in your left hand and ink and quill in your inventory.

The telescope still works as a zoom tool when astronomy conditions are not available. Drawing and the deep-sky plates need a dark enough, clear enough sky and open sky overhead.

Fifty deep-sky photographs are invisible to the naked eye. Zoom in on a faint smudge.

Raise the glass on a planet and it becomes a disc: Mars swells towards opposition, Saturn carries rings, Jupiter's four Galilean moons and five of Saturn's moons move on their own orbits and vanish when the planet hides them. Put the telescope down and they are points of light again. `.stars solar-system photo` swaps the pixel-art faces for photographs; position and size stay the same.

### Identifying a wandering star

A telescope cannot tell you a planet is a planet. Through any eyepiece it stays a point of light until you zoom far enough to resolve the disc, and even then the name is not printed on it. What gives it away is that it *moves*.

Sight the light with the Sextant on one night and again on another, sneaking each time so the book holds two dated entries. `.stars sightings` then lays the entries out and says how far apart those nights put them. `.stars classify 2 wanderer Ember` is where you say what you think it was.

The book takes your word for it. If your own entries pin the conclusion to one wandering body, the book asks what you would like to call it, and every instrument uses that name from then on. Being wrong is allowed, and it surfaces later as an instrument forecasting badly from the record you gave it.

## Precision Telescope

Same modes as the Brass Telescope, with ten stronger zoom steps.

## Sky Disc

The Sky Disc scratches where the sun crosses the horizon. After enough marks, the width of that band gives you the length of the year, how far you are from the equator, where east or west lies, and when the sun will next stand still. One connected constellation can also be cut into the same disc.

### How to mark

Stand under open sky as the sun touches the horizon. **Sneak and hold right click**: the disc comes up and tells you the notch the mark would fall on. Keep holding for about a second and it scratches the rim there. Let go early and nothing is marked. Come back another evening and mark again. Sunrises go on one rim, sunsets on the other.

You are marking the sun crossing the *true* horizon, not the moment you clicked. A hill in the way costs you nothing. Missing evenings to rain or sleep costs nothing either: you need enough marks to find the ends of the band, not every day of the year.

### How to read it

**Right click** holds the disc up. The readout shows the sunset band, the sunrise band, and, once a rim has turned back from an edge, when the sun will next stand still. **Scroll** turns the raised disc in your hand.

Two marks bracket a band. Keep marking and the band widens until one evening the mark falls *short* of the edge. Around such an evening several days land on the same notch and the sun appears to stand still.

The disc will not announce a solstice on the day one happens. Only once the band has turned back and reached its far edge does it say anything: the length of the year, how far you are from the equator, and from then on the day the sun will next stand still.

The sun sets due west at the equinoxes and swings evenly either side of that point through the year, so the middle of a finished band of sunsets is due west. The sunrise band gives due east the same way. The disc is never told where north is.

### One constellation on the disc

While the disc is held up under the stars, left-drag from one visible star to another. The line is cut into the disc. No book, ink, or quill. Every later line has to join the figure already there; a line that touches none of it is a second constellation, and there is no room for it.

Hold the finished disc in either hand and its figure returns to the sky. Press a figure into **raw clay** before you fire it; a fired clay disc will not take another line. Metal can be engraved whenever you like.

### Set it down

Sneak and right click the ground and the disc lies there, face up, scratches and all. An empty hand and a right click pick it back up. Aim at a block when you mean to place it; aiming at the horizon is how you mark.

### Limits

Form a disc on a clay forming surface and fire it in a pit kiln, or craft a copper or bronze one from a metal plate and gold bits. Clay holds a notch of **five degrees**; metal holds **two and a half**. They all find the same solstice. A clay rim means more evenings landing on one notch and a year and a latitude handed back rounder. Past copper the rim stops getting better.

The first mark fixes where the disc belongs. Carry it far enough north or south and it refuses the next mark rather than quietly spoiling the band. A scratch cannot be undone. A clay disc wanders further than a metal one before it objects. Inside the polar circles the sun stops setting for part of the year, and there is no band of sunsets to measure.

Vintage Story's character panel already tells you the day, the month, the year and the hour. `.stars calendar clock` hides the date and keeps the hour. `.stars calendar none` hides both and the panel says `unreckoned`. `.stars calendar full` puts it back. The choice is local and saved. Until you hide the date, the disc is measuring something the game is already giving you for free.

The Astrolabe gets you the same latitude in a few seconds of sighting. The disc is the slow road to it, and it is open on the first evening of a new world.

## Sextant

The Sextant measures the altitude of the sun, the moon, a star, a planet, or a comet above the horizon.

### How to use it

Hold right click and put the centre of your view on the body. The readout at the top names what you sighted and gives its angle above the horizon in degrees. If several bodies fall in the sight, the one nearest the centre wins.

While holding right click, middle click cycles through angle only, the rose equatorial grid, the cyan azimuthal grid, and both grids. Releasing right click restores the grid mode selected with `.stars sky-grid`. The Sextant remembers its own choice for the rest of the session.

**Sneak while sighting to write the reading down.** Hold a writable book in your left hand and keep ink and quill in your inventory. The book gains a dated entry: the angle, the bearing, how bright it looked, the day and hour, and the latitude you stood at.

The entry does not say what you sighted. It records that *something* stood at that angle at that hour. Angles are written to one arcminute.

Once there are two entries, `.stars sightings` lays them out, gathers them into the sets it takes to be the same body, and says how far apart the nights put them. Say what a set was with `.stars classify 2 wanderer Ember` (set number, then star, wanderer, or comet, then an optional name). Nothing grades the answer. Call a set a wanderer and, if exactly one wandering body was in all those places on all those nights, your name for it becomes the name every instrument uses.

### Limits

The sun and a visibly lit moon can be shot whenever they are above the horizon, so the Sextant works in daylight. A daytime crescent is a perfectly good sight. An invisible new moon is not offered as a target. Stars and planets need a dark enough sky.

A planet reads as **Wandering star** unless a book in your left hand names it. Swap books and the sky is renamed at once.

If nothing readable is near the centre, the readout tells you what is available to aim at. If the sky is blocked overhead it reports that instead.

Sighting the sun currently costs you nothing. A real navigator would use a shade glass.

## Calibrated Astrolabe

The astrolabe forecasts where a constellation, a wanderer you have named, or a comet will stand, and it tells the hour from the sun.

### How to get one

Recover a vanilla astrolabe from ruins, then combine it with a brass plate.

### How to use it

Hold it in your main hand and a written constellation book in your left hand, then hold right click to open the planning readout. The clock still shows if you hold no book.

- Middle click selects the next target: each constellation in the book, then the wanderers that book has picked out, then the comets.
- Scroll forecasts an hour at a time.
- Sneak and scroll forecasts seven days at a time.
- Sneak and hold right click cuts or recuts the plate for where you are now.

For the selected target it gives the compass direction, the angle above the horizon, whether it is rising or setting, how long until transit, and whether it is circumpolar or can never rise where the plate was cut.

### Cutting the plate

A new astrolabe is blank: it reads **no plate** and will not place a star. Stand under open sky after dusk and sneak-hold right click for a few seconds while it sights the pole. A bar fills as it works. A roof overhead or a sky still too bright will stop it. You need nothing but the sky for this.

Afterwards every position it gives is for *that* latitude. Travel far enough and it starts reporting how far you have strayed; past about eight degrees it asks to be recut. Until then it is describing the sky over somewhere else. The clock is not engraved and follows you wherever you go.

### Limits

It aims with your book, not with the sky's own catalog. A wandering body nobody has picked out is not on the list at all. Sight one across a few nights, say it wanders, and it appears. Comets are listed whether or not they are here; one that is away reads as the days until it returns. Scroll the forecast that far ahead and the line becomes a real position.

Every answer says what it rests on: `from 3 sightings over 11 days, to 0° 01′` for a wanderer you established yourself, `drawn from 7 stars` for one of your figures, `from the almanac, not your sightings` for a comet. A planet written down under an older version, with no sightings behind it, still aims but says it is thin.

Forecasting spans one world year and follows this world's day and year length. A planet is forecast where it will actually be, not where it is now. Let go and the forecast snaps back to the present.

The clock reads the hour off the sun: the hour of the world day, whether it is daylight, dusk, night, or dawn, the sun's angle, and how long until the next sunrise or sunset. It follows your forecast. Near the poles, where the sun may not rise for a stretch of the year, the readout says so rather than inventing an hour.

## Constellation Journal

A constellation is a set of lines between stars, stored in a vanilla book or cut into a Sky Disc.

Put a blank book in your left hand and keep ink and quill in your inventory. Scope in with a telescope, switch to **Create Constellation**, and drag from one visible star to another.

Constellations in a book are visible only while you hold that book. Hand it to another player and they see your figures, under your names.

Once AstraTerra writes the first constellation into a book, vanilla book editing is locked. The journal keeps working. Ink and quill are needed to create, add, remove, rename, or build.

`.stars build Ori` writes one of the 88 IAU figures into the held book. Built figures are ordinary journal entries afterwards. Use the [Constellation Build Cheat Sheet](constellation-cheat-sheet.md) for the three-letter codes.

See the [Command Reference](commands.md) for the full `.stars` list, including sightings, calendar hiding, solar-system art, and the moon overhead.

## Meteor Showers

Nine showers recur at the same fraction of every configured world year. A shower is worth watching when its season is near the peak, its radiant is above the horizon, and the sky is dark. A bright moon drowns the faint meteors but not the brightest ones.

The strongest catalog shower is the Geminids. On the default 108-day calendar, a useful test is **month 9, day 8 at about 02:00**, from roughly **32.5° north**. The exact count still changes with longitude and moon phase. Use `.stars debug` to check the latitude reported by AstraTerra.

Each streak points back toward its shower's radiant. Streaks close to that point are short; streaks farther across the sky are longer.

## Comets

Four comets return on their real orbital periods, counted in world years: Machholz about every 5, Tuttle about every 14, Tempel-Tuttle about every 33, and Halley about every 75. Each is the parent of a shower already in the sky: the Quadrantids, the Ursids, the Leonids, and both the Eta Aquariids and the Orionids.

An apparition lasts a couple of world weeks. The comet moves along its own track, brightens as it rounds perihelion, and fades again. The tail points **away from the sun**, not along the comet's motion.

The Sextant sights a comet like anything else once it is up. A comet keeps its name without a book.

The Astrolabe lists every comet, including the ones that are years away. One that is away reads `away, returns in 340 days`. Scroll the forecast that far ahead and the line becomes a real position. `.stars comets` reports the whole catalog at once.

Machholz arrives a little under two world years in. Halley makes its first pass in your third year, and it is the bright one. Its second pass is in world year 78.

The periods and the parent showers are real. The tracks across the sky and the peak brightnesses are authored for the game rather than computed from an orbit.

## Starfield Comparison Debug Modes

Use `.stars starfield astraterra|both|vanilla` to switch the visible starfield immediately:

- `astraterra` shows only AstraTerra's catalog and is the default.
- `both` overlays AstraTerra and Vintage Story stars for alignment comparison.
- `vanilla` shows only Vintage Story's original cubemap.

The selected value is saved as `StarfieldMode` in `ModConfig/astraterra.json`.
