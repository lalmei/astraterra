# AstraTerra Player Guide

AstraTerra replaces Vintage Story's night sky with a catalog of more than 5,000 stars. Five planets move among them. Nine meteor showers return each year. Four comets return on their own periods. The sky tilts with latitude, turns with the hour, changes with the season, and keeps a local hour that depends on how far east or west you have walked.

The instruments let you measure what you see, write it down, and plan when to find it again.

Everything below is also written into the Vintage Story handbook. Press **H** and open the **Astronomy** tab. Pressing **H** while hovering an AstraTerra item opens that item's own page.

## What You Can Do

- Watch a catalog sky of more than 5,000 stars that changes as you travel north or south.
- Carry your own hour east and west: world X acts as longitude, so the sun, the daylight and the stars shift together as you travel.
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

### Where you are

Two numbers decide what the sky over you looks like and what hour it is: **latitude**, how far north or south you have walked, and **longitude**, how far east or west.

Latitude is Vintage Story's own. It comes from world Z, it is what tilts the sky, and every instrument here answers for the latitude you are standing at.

Longitude is AstraTerra's, because Vintage Story does not have one. It comes from world X. See [Longitude and the local hour](#longitude-and-the-local-hour) below.

### Limits

On a world whose climate is not set to `realistic`, latitude is stuck at one value everywhere, and the sky does not change as you travel north or south. The equator is not at z = 0 — it sits wherever the world seed put it. Latitude also repeats: keep walking north and you come back to the equator, then to a south pole. Use `.stars debug` if you need the latitude AstraTerra is actually using.

### Latitude and the turning of the night

Walk north and the pole star climbs; walk south and it sinks, while stars you have never seen rise ahead of you. Near the poles some figures never set, and others never rise. Every instrument here answers for the latitude you are standing at.

The whole sky wheels around the pole. A constellation low in the east after dusk is high overhead hours later. Its best moment is its **transit**, when it crosses the meridian at its highest. In the northern hemisphere, Polaris sits near the north celestial pole: it shows north, and its angle above the horizon is your latitude.

The stars also rise a little earlier each night, so the sky tells the season as well as the hour.

### Longitude and the local hour

Vintage Story has no longitude. Its sun reads world Z for latitude and the world clock for the hour, and ignores world X entirely: sunrise happens at the same instant for every player on a server, wherever they stand. The whole world is one time zone.

AstraTerra makes east and west matter. World X becomes longitude, and the sun, the daylight, the star field, the instruments and the hour you are shown all shift together with it. Walk east and the sun rises earlier for you; walk far enough west and it is still morning where your neighbour is having dusk.

**The scale.** Longitude uses the same yardstick as latitude — the world's `polarEquatorDistance`, 50,000 blocks by default. So 90° of longitude is 50,000 blocks, one hour of the sky's rotation is about **8,300 blocks**, and the whole 360° comes back around after 200,000 blocks. This is a walk, not a stroll; a nearby second base is in the same hour as your first. The prime meridian — longitude 0° — is the middle of the map, and east of it is ahead.

**The world's own clock never changes.** There is still one universal time underneath, and it is what the server schedules, saves, and syncs on. Longitude changes the hour you are *shown*, not the hour the world keeps. `/time` still gives an administrator a straight answer.

**What the sky does, the clock does.** The star field, the sextant, the astrolabe's clock and the character panel all take their longitude from the same place, and that place answers zero unless the visible sun is the one AstraTerra shifted. So they never disagree with each other: if longitude is switched off, or another mod takes the sun back, the whole sky and every reading fall back to universal time together rather than drifting east of a sun that never moved.

**A sighting still compares.** A reading written into a book keeps the universal hour along with the longitude you stood at, so two marks taken a thousand blocks apart still line up when the book does its arithmetic.

#### Settings

Both live in `ModConfig/astraterra.json`. On a server the sun setting is the server's to make, and it is sent to every client.

| Setting | Values | Default | What it does |
| --- | --- | --- | --- |
| `LongitudeAwareSun` | `true`, `false` | `true` | Whether the sun and daylight move with world X at all. Set `false` to keep Vintage Story's single time zone — the stars and instruments follow it back. |
| `DisplayedClockTime` | `local`, `universal`, `zones` | `local` | Which hour you are shown. `local` is continuous local solar time. `universal` is the world's own clock. `zones` rounds to whole clock hours, so a world day divides into even time zones. |

Neither has a command, so change them with the game closed. See the
[Configuration Reference](configuration.md) for the rest of the file.

!!! warning "Longitude changes more than the sky"
    Because daylight now depends on where you stand, everything downstream of the sun follows it — crops, temperature, mob spawning, other mods, and other players on the same server keeping different hours from you. That is the point of the feature, and it is why there is a switch: a server that wants vanilla's one time zone should set `LongitudeAwareSun` to `false`.

### The Moon

The Moon keeps one surface portrait upright while a curved shadow follows the calendar's phase and turns towards the sun. Vintage Story's moonlight and the length of the night are untouched. A telescope shows a surface rather than a larger smooth disc. Pixel art is the default. `.stars moon photo` uses a photograph of the real surface; `.stars moon vanilla` puts Vintage Story's own disc back. The planets are a separate setting: `.stars solar-system`.

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

Keep a **flint or a knife anywhere in your hotbar** — nothing is ever cut into a disc without one,
and without it the disc says so instead of marking.

Stand under open sky as the sun touches the horizon. **Sneak and hold right click**: the disc comes up and tells you the notch the mark would fall on. Keep holding for about a second and it scratches the rim there. Let go early and nothing is marked. Come back another evening and mark again. Sunrises go on one rim, sunsets on the other.

You are marking the sun crossing the *true* horizon, not the moment you clicked. A hill in the way costs you nothing. Missing evenings to rain or sleep costs nothing either: you need enough marks to find the ends of the band, not every day of the year.

### How to read it

**Right click** holds the disc up. The readout shows the sunset band, the sunrise band, and, once a rim has turned back from an edge, when the sun will next stand still. **Scroll** turns the raised disc in your hand.

Two marks bracket a band. Keep marking and the band widens until one evening the mark falls *short* of the edge. Around such an evening several days land on the same notch and the sun appears to stand still.

The disc will not announce a solstice on the day one happens. Only once the band has turned back and reached its far edge does it say anything: the length of the year, how far you are from the equator, and from then on the day the sun will next stand still.

The sun sets due west at the equinoxes and swings evenly either side of that point through the year, so the middle of a finished band of sunsets is due west. The sunrise band gives due east the same way. The disc is never told where north is.

### One constellation on the disc

While the disc is held up under the stars, left-drag from one visible star to another. The line is cut into the disc. No book, ink, or quill — but the same flint or knife the marks need. Every later line has to join the figure already there; a line that touches none of it is a second constellation, and there is no room for it.

Hold the finished disc in either hand and its figure returns to the sky. Press a figure into **raw clay** before you fire it; a fired clay disc will not take another line. Metal can be engraved whenever you like.

### Somebody else's disc

Pan bony soil long enough and one may come up already finished: a tin bronze disc with a whole year of sunrises and sunsets on its rim and one constellation cut into its face. It is a record, not a head start. Hold it up and it reads out a latitude — but not yours. The band was scribed wherever its maker lived, so it tells you how far from the equator somebody else stood, and it refuses your own marks: the sun does not set where this disc was made. Carry it to that latitude and it will take a scratch again.

The figure on it is one its maker could actually see from there, and it returns to the sky in your hands like any other. You can add lines to it, as long as they join what is already cut.

### Set it down

Sneak and right click the ground and the disc lies there, face up, scratches and all. An empty hand and a right click pick it back up. Aim at a block when you mean to place it; aiming at the horizon is how you mark.

A disc also stands in a **mold rack**, five to a rack, and sits in a display case or on a shelf — each one showing its own marks and its own figure.

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

The strongest catalog shower is the Geminids. On the default 108-day calendar its peak lands on **month 12, day 4**, and about **02:00** from roughly **32.5° north** puts the radiant high. The count you actually see still changes with longitude and moon phase. Use `.stars debug` to check the latitude AstraTerra is using.

Each streak points back toward its shower's radiant. Streaks close to that point are short; streaks farther across the sky are longer.

## Comets

Four comets return on their real orbital periods, counted in world years: Machholz about every 5, Tuttle about every 14, Tempel-Tuttle about every 33, and Halley about every 75. Each is the parent of a shower already in the sky: the Quadrantids, the Ursids, the Leonids, and both the Eta Aquariids and the Orionids.

An apparition lasts a couple of world weeks. The comet moves along its own track, brightens as it rounds perihelion, and fades again. The tail points **away from the sun**, not along the comet's motion.

The Sextant sights a comet like anything else once it is up. A comet keeps its name without a book.

The Astrolabe lists every comet, including the ones that are years away. One that is away reads `away, returns in 340 days`. Scroll the forecast that far ahead and the line becomes a real position. `.stars comets` reports the whole catalog at once.

Machholz arrives a little under two world years in. Halley makes its first pass in your third year, and it is the bright one. Its second pass is in world year 78.

The periods and the parent showers are real. The tracks across the sky and the peak brightnesses are authored for the game rather than computed from an orbit.

## When the sky is not doing what you expect

Most of these are the sky behaving correctly. `.stars debug` reports the latitude AstraTerra is
using, how far the sky has turned, and its verdict on whether you can see the sky at all — it is the
first thing to run, and the line to paste into a bug report.

| What you see | Why |
| --- | --- |
| No stars at all | The sky is too bright, too cloudy, or blocked overhead. `.stars debug` says which. |
| Vintage Story's stars, not AstraTerra's | `.stars starfield vanilla` is set. `.stars starfield astraterra` puts it back. |
| The same sky however far north you walk | The world's climate is not `realistic`, so Vintage Story reports one latitude everywhere. Nothing can fix this from inside the mod. |
| A constellation you drew is missing | Its lines live in the book. You only see them while you are holding that book. |
| A constellation never comes up | At your latitude it may never rise. The astrolabe says so outright, and it is answering for the latitude its plate was cut at, not the one you are standing at. |
| A planet called **Wandering star** | Nothing has named it. Identify it with the Sextant, or hold a book that already names it. |
| The astrolabe reads **no plate** | A new one is blank. Sneak-hold right click under open sky after dusk to cut it. |
| The astrolabe's positions are wrong after a long walk | The plate is still cut for where you cut it. Past about eight degrees of latitude it asks to be recut. |
| The Sky Disc will not take a mark | No flint or knife in the hotbar; or the disc has been carried too far from the latitude of its first mark; or you are inside a polar circle, where the sun does not set to be marked. |
| A meteor shower that produces nothing | A shower's rate is meteors per hour of *real* watching. A strong one at 120 an hour averages one streak every 30 real seconds, and a world hour goes by in about two real minutes. |
| Your neighbour's sunrise is not yours | Longitude, working as designed. You are east or west of them. |
| The moon keeps a different hour from everything else | Only under `.stars moon vanilla`. Vintage Story draws that disc, so AstraTerra cannot move it off the world's single time zone. |

## Settings

Most of what you can change is a `.stars` command that takes effect at once and saves itself:
`starfield`, `sky-grid`, `solar-system`, `moon`, `calendar`. Two things have no command because on a
server they are not a per-player choice: `LongitudeAwareSun` and `DisplayedClockTime`. A few more,
including how brightly the Milky Way draws, are only ever edited in the file.

- [Configuration Reference](configuration.md) — every key in `ModConfig/astraterra.json`, its default, and what it does.
- [Command Reference](commands.md) — every `.stars` and `/stars` command.

If you are comparing AstraTerra's sky against Vintage Story's, `.stars starfield both` draws them on
top of each other, which is the fastest way to see whether the two agree about where a star is.
