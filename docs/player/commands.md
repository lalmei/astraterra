# AstraTerra Command Reference

AstraTerra uses the `.stars` command group.

## Player Commands

```text
.stars list
.stars info [id|name|selected]
.stars name <id|selected> <name>
.stars select <id|name>
.stars delete <id|selected>
```

```text
.stars sightings
.stars classify <set|#entry> <star|wanderer|comet> [name]
```

Use `.stars list` to see constellations in the book held in your left hand. Use `.stars info selected` after drawing or selecting a constellation to see its star count, segment count, best visibility window, season summary, and current state. Mutating commands require ink and quill in your inventory.

## Sightings

`.stars sightings` lists every sighting written down in the book in your left hand, then gathers them
into the sets it thinks are the same body and says what comparing each set shows — whether it held
its place, or how far and how fast it moved.

The book does the arithmetic and stops there. `.stars classify` is where you say what a set was:

```text
.stars classify 2 wanderer Ember
.stars classify #4 comet
```

Calling a set a **wanderer** does one more thing. Your own entries are looked up against the wandering
bodies, and if exactly one of them was in all of those places on all of those nights, the conclusion
binds to it: your name for it is what the Sextant and the Astrolabe call it from then on. If two
bodies fit, or none do, the conclusion still stands but nothing is bound — sight it again and the
record will settle.

Nothing checks your answer against the sky, and that is deliberate: a classification the game has
already graded is a quiz, not a discovery. Being wrong is allowed, and you find out the way an
observer does — by sighting it again and watching your own numbers stop making sense. Changing your
mind rewrites the conclusion and leaves every sighting it rested on untouched.

## Authored Constellations

```text
.stars build <iau-code-or-name>
.stars build <sky-culture-id>:<iau-code-or-name>
```

Examples:

```text
.stars build Ori
.stars build UMi
.stars build modern_iau:Vir
```

The current authored set includes all 88 Modern IAU constellations. Built constellations are saved into the held constellation book like hand-drawn constellations.

See the [Constellation Build Cheat Sheet](constellation-cheat-sheet.md) for every supported code and a copyable command list.

Administrators with the Vintage Story `give` privilege can create a ready-made test book containing all 88 patterns:

```text
/stars give-catalog
```

The same **Star Catalog** and **The Zodiac** books also appear on the **AstraTerra** creative inventory tab, so creative-mode players can drag them without using a command.

The command adds a written book titled **Star Catalog** to the caller's inventory. Put it in the left hand to show every authored constellation or use it with the Calibrated Astrolabe. This admin setup command does not require ink and quill.

To create a smaller book titled **The Zodiac** containing the traditional twelve zodiac constellations in sign order, run:

```text
/stars give-zodiac
```

This book contains Aries, Taurus, Gemini, Cancer, Leo, Virgo, Libra, Scorpius, Sagittarius, Capricornus, Aquarius, and Pisces. Ophiuchus is not included in the traditional twelve-sign set.

To create a book titled **The Wanderers**, which already names all five planets the way our own sky culture named them, run:

```text
/stars give-wanderers
```

Planets are otherwise anonymous: without a book that names them, every instrument calls a planet a *wandering star*, because that is all it looks like from the ground. This book is somebody else's finished work — handy for testing and for creative play, and a shortcut past identifying each planet yourself. Put it in your left hand and the Sextant and Astrolabe will use its names.

## Recovery And Debug Commands

```text
.stars comets
.stars connect <hipA> <hipB>
.stars debug
.stars goto-lat <degrees>
.stars daylight-stars on
.stars daylight-stars off
.stars starfield astraterra
.stars starfield both
.stars starfield vanilla
.stars sky-grid none
.stars sky-grid horizontal
.stars sky-grid equatorial
.stars sky-grid both
.stars render
.stars render stars|constellations|deepsky|meteors|comets|milkyway|all on
.stars render stars|constellations|deepsky|meteors|comets|milkyway|all off
```

`connect` is a recovery path for creating a segment from known HIP star IDs. `debug` shows latitude and sky-orientation diagnostics. `goto-lat` helps test different sky latitudes in the current world. `daylight-stars` is intended for testing and should be turned off for normal play.

`starfield` changes the active sky immediately and saves the choice in `ModConfig/astraterra.json`:

- `astraterra` shows only AstraTerra's catalog-driven stars and is the default.
- `both` shows AstraTerra and the original Vintage Story cubemap together for alignment comparisons.
- `vanilla` shows only the original Vintage Story starfield.

`comets` reports every comet in the catalog: whether it is up now — with its phase through the apparition, magnitude, tail length and position — or how many days until its next one begins. A comet is the one thing in the mod you cannot check by looking up, since the rarest is due about once every seventy-five world years, so this turns the whole catalog into four lines.

`render` switches one part of the sky off so you can see what it costs. If the mod is making your game stutter, this is the fastest way to say *which part*: turn one path off, play for half a minute, and see whether it helps. `.stars render` on its own lists what is drawing.

Unlike `starfield` and `sky-grid`, this is not saved — everything comes back when you restart the client, because it is a measuring tool rather than a setting. The paths are `stars` (including planets), `constellations`, `deepsky` (the telescope's photographic plates), `meteors`, `comets` and `milkyway` (the galaxy's own glow behind everything else), plus `all`.

Every 30 seconds, AstraTerra writes what the sky cost into `client-debug.log` — time per frame, worst frame, draw calls, and which paths were on. If you are reporting a performance problem, that line and the path you found is exactly what makes the report actionable.

`sky-grid` projects debug coordinate lines over the visible sky and saves the choice in `ModConfig/astraterra.json`. `horizontal` draws the observer-local altitude-azimuth grid in cyan. `equatorial` draws the right-ascension/declination grid in rose and rotates it with local sidereal time. `both` overlays the two systems; `none` is the default. While using the Sextant, middle click cycles its temporary display through angle only, equatorial, azimuthal, and both without changing the saved mode.
