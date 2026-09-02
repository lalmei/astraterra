# Celestial Model

How AstraTerra turns a world clock into positions in the sky. Every renderer — starfield,
constellation overlay, sky grid, sextant, astrolabe — reads from this one pipeline, so a sign error
here moves the entire sky at once. So it is important we are aligned. Somethings may diverg from vanilla when not defined, and making the assumptions we are in a spherical world.

## Coordinate Conventions

Get these wrong and everything downstream is subtly mirrored.

| Quantity | Convention                                                                   |
| -------- | ---------------------------------------------------------------------------- |
| Altitude | Degrees above the horizon. `0` is the horizon, `+90` the zenith.             |
| Azimuth  | Degrees clockwise from north. North `0`, east `90`, south `180`, west `270`. |
| World X  | East is `+X`.                                                                |
| World Y  | Up is `+Y`.                                                                  |
| World Z  | **North is `-Z`**, south is `+Z`.                                            |

A horizontal coordinate becomes a world direction as:

```text
X = cos(altitude) * sin(azimuth)
Y = sin(altitude)
Z = -cos(altitude) * cos(azimuth)
```

That negated Z is the whole reason north maps to `-Z`. `StarRenderModel`,
`SkyCoordinateGridModel`, and `SkyBodyModel` all use this identical form, and
`SkyBodyModel.FromWorldDirection` is its exact inverse.

## The Pipeline

```text
world clock  ->  local sidereal angle  ->  hour angle  ->  altitude / azimuth  ->  world direction
```

### 1. Local sidereal angle

`CelestialMath.GetVanillaAlignedLocalSiderealAngle` converts the world clock into the angle the sky
has turned:

```text
solarLongitude  = turns since the March equinox * 360
solarRightAscension = eclipticToEquatorial(solarLongitude, obliquity).rightAscension
localDayFraction = wrap(totalDays + longitude / 360)
solarHourAngle = (localDayFraction - 0.5) * 360
sidereal = solarRightAscension + solarHourAngle
```

The sun transits at local noon, so at that instant sidereal time equals the sun's right ascension —
not its ecliptic longitude. Those two angles agree at the equinoxes and solstices but differ by as
much as about 2.47° between them at Earth's obliquity. Treating longitude as right ascension rotated
the stellar sky almost ten clock minutes away from the sun at the worst point in the year.

The daily term is a fraction of a complete rotation rather than `15°` per named clock hour. A
16-hour world and a 30-hour world both turn through 360° per world day, put the sun on the meridian
at their own local noon, and apply longitude as the same fraction of one rotation.

!!! warning "Sidereal time must increase with time"
Hour angle is `sidereal - rightAscension`,
so if the sidereal angle _decreases_ as the day goes on, the hour angle decreases too and the
entire sky — stars, constellations, deep-sky objects, grid lines — rotates **west to east**.

If you would like to make a custome world, solar system you can modify to have the place rotate in a different direction.

The seasonal term also means the sidereal day is slightly _shorter_ than the solar day. Solar right
ascension gains a full extra turn over one world year. Its instantaneous rate varies slightly with
season because the obliquity transform is nonlinear; code that needs a position evaluates the
sidereal angle at the requested timestamp rather than advancing it with a fixed `15°` rate.

### The visible sun is the measurement authority

`CelestialMath.GetVanillaAlignedSolarEquatorialCoordinates` describes the survival sun in the same
equatorial frame as the stars. Its right ascension comes from the ecliptic-to-equatorial rotation.
Its declination mirrors Vintage Story's survival formula, `tilt * sin(solarLongitude)`, with the
same seasonal phase and shared axial tilt.

That declination is intentionally the one the game draws, rather than a second solar ephemeris.
`IGameCalendar.GetSunPosition` remains the authority for the sun's actual world direction, and the
sextant and astrolabe continue to measure that vector directly. Projecting the shared equatorial sun
with the sidereal angle is tested against the survival model to within the brass sextant's
one-arcminute scale across seasons, latitudes, longitudes, day lengths, and year lengths.

This is visual and instrumental agreement with Vintage Story's solar model, not a claim that its
uniform seasonal orbit is a precision Earth ephemeris. In particular, the survival declination
sinusoid differs from the exact obliquity rotation by up to about 0.26°, and the uniformly advancing
season omits the real orbit's equation of centre. Replacing the visible sun with a higher-order
ephemeris would be a separate compatibility decision; the star field must not do so by itself.

### 2. Hour angle to horizontal coordinates

`CelestialMath.GetHorizontalCoordinates` is standard spherical astronomy:

```text
H   = sidereal - rightAscension
alt = asin( sin(dec) sin(lat) + cos(dec) cos(lat) cos(H) )
az  = atan2( -sin(H),  tan(dec) cos(lat) - sin(lat) cos(H) )
```

A negative hour angle puts an object east of the meridian and climbing; a positive one puts it west
and sinking. That single fact drives rising/setting classification and the astrolabe's transit
countdown, which counts down from right ascension to sidereal time:

```text
hoursUntilTransit = normalize(rightAscension - sidereal) / rate
```

### 3. Horizontal coordinates to a drawable body

`SkyProjection.Project` is the last leg, and it is deliberately generic: it takes an
`EquatorialCoordinates` and a visual magnitude and knows nothing about what kind of object it is
placing. It applies the horizon cutoff, the fade band, the magnitude curve, and the world direction,
and returns a `RenderedBody`.

Everything that has a position in the sky goes through it. `RenderedStar` is that body plus a HIP
number, a colour temperature and a guide-star flag; a planet or comet is that body plus its own name
and tint. Deep-sky plates share the horizon handling and the direction but not the magnitude curve,
because their brightness is authored per object — a nebula is a surface brightness spread over
degrees, not a point source.

!!! warning "One projection, or the sky drifts apart"
    A body that moves is drawn against the fixed stars, so the two must be placed by the same
    arithmetic. A second copy of the horizon fade or the magnitude curve for planets would look
    right on its own and wrong next to a star at the same altitude — and nothing would fail.

    `SkyProjectionTests.A_Moving_Body_Lands_Exactly_Where_A_Star_At_The_Same_Position_Does`
    pins the two paths together.

## Bodies That Move

A catalog star's right ascension and declination are constants. A planet's, a comet's and a
radiant's are functions of world time, and that is the only difference between them.

`ISkyEphemeris` is that function, and nothing more:

```csharp
EquatorialCoordinates PositionAt(double totalDays);
double MagnitudeAt(double totalDays);
```

Implementations live in `Astronomy/` and stay pure, which is what lets an orbit be checked against
published positions for real dates without a running game. `FixedEphemeris` covers anything that does
not move, so a caller collecting bodies to sight or to forecast never has to know which kind it holds.

Positions are **geocentric**: they do not depend on where the observer stands. Latitude and longitude
enter afterwards, in the projection.

### From the ecliptic to the equator

Planets and comets are computed in the plane they orbit in, not in the plane the sky is drawn in.
`CelestialMath.EclipticToEquatorial` rotates between the two by the obliquity of the ecliptic,
`CelestialMath.MeanObliquityDeg` (23.4392911°, Earth's mean tilt at J2000).

It rotates a unit vector rather than using the textbook
`atan2(sin l cos e - tan b sin e, cos l)`. Same rotation, but that form divides by zero at the
ecliptic poles — which planets never approach and a steeply inclined comet can.

The same constant is what the sun's seasonal declination needs, so both should read it from here
rather than each carrying a tilt of their own.

### The planets

`PlanetEphemeris` solves two orbits per sample — the planet's and the observer's — and subtracts
them. Retrograde motion is not scripted anywhere; it is what that subtraction does when the world
overtakes a slower planet on the inside, and a player charting Mars over a couple of world weeks will
watch it happen.

Elements come from JPL's *Approximate Positions of the Major Planets*, six per body plus per-century
rates, accurate to arcminutes across 1800–2050. `KeplerianOrbit` implements JPL's own formulation:
advance the elements, take the mean anomaly, solve Kepler's equation by Newton–Raphson, place the
body in its orbital plane, rotate that plane into the ecliptic.

Magnitude is `H + 5 log10(r · delta)` plus a linear phase term. Venus swings by more than a magnitude
over a world year, which is the point of modelling it at all.

!!! warning "The star brightness curve saturates before the planets do"
    `SkyProjection.GetBrightnessFromMagnitude` is deliberately compressed for stars and reaches full
    brightness at magnitude 0.4. Every planet but Saturn spends most of its time brighter than that,
    so left alone, Venus at -4.9 and Mars at its dimmest would be drawn identically.

    `PlanetRenderModel.GetBrilliance` keeps responding across the range planets occupy, and the
    renderer spends it on the glow rather than the core. Anything else added to the sky brighter than
    magnitude 0.4 needs the same treatment, or the sky flattens out at the bright end.

### What a telescope resolves

A planet is a point of light to the naked eye and a disc through a glass, and those are two different
drawing problems. `PlanetRenderModel` answers the first; `PlanetDiscRenderModel` answers the second,
and it runs only while the scope is raised — nothing below is computed for a player looking up.

It reads three things off the same ephemeris the sprite already uses:

| Quantity | Where it comes from | What it produces |
| --- | --- | --- |
| Distance | `GetGeocentricPosition(t).Length` | The disc's angular width, so Mars swells towards opposition |
| Phase angle | `PhaseAngleAt(t)` | Which authored face is drawn: full, half or crescent |
| Sun direction | The calendar's own sun | Which way the crescent points |

Faces are pictures, not shaded models, and each is drawn lit from the left. The quad is built with
its horizontal running *away* from the sun's projected direction, so a crescent Venus turns its
lit limb towards the sun wherever the sun happens to be.

The moons of Jupiter and Saturn ride on circular orbits in their parent's equatorial plane, which is
within a few degrees of the ecliptic — so they string out along the ecliptic tangent at the planet,
opened into an ellipse by `moonPlaneTiltDeg` for Saturn and left nearly edge-on for Jupiter. Their
periods are quoted in **real days**, the same clock `WorldEpoch` puts the planets on, so Io still
laps Callisto nine times whatever the world's day length. The third component of the orbit is depth,
and its sign is why it is computed at all: a moon on the far side of the swing, inside its parent's
disc, is dropped rather than drawn — so the count beside Jupiter changes over a night.

!!! warning "Everything resolved is drawn 7.5x larger than life"
    `PlanetDiscRenderModel.DiscExaggeration` is the one departure from the real sky in this pass, and
    it applies to every body and every orbit alike, so the relationships hold: Saturn's globe stays a
    third of its ring span, Callisto stays four times as far out as Io. The factor is fixed by the
    eyepiece rather than by taste — any larger and Callisto's ten-arcminute swing leaves the precision
    telescope's field, which is exactly when a player can no longer count the moons.

    A moon is exempt in one direction only: enlarged with everything else it would still be under a
    pixel, so it is held at `MinimumMoonWidthDeg`, the size the stars beside it draw at.

A planet with a disc drops the sprite that stood in for one — at any magnification worth using, the
sprite is several times wider than the planet and would erase it. What is left is a halo just outside
the disc, which is both what an eyepiece shows and what keeps the planet findable at low power.

### The world epoch

Orbits are published against Julian centuries past J2000, so a world day has to be worth something
in real time. Two decisions in `WorldEpoch` fix it, and both are visible in the sky:

| Decision | Value | Consequence |
| --- | --- | --- |
| A world year is a Julian year | `RealDaysPerWorldYear = 365.25` | Jupiter takes ~12 **world** years on a 12-day world and a 360-day world alike |
| World time zero is the start of the world year | `WorldZeroOffsetDaysFromJ2000 = 78.816 - 0.2226 x 365.25` | The planets share the seasons the rest of the mod already keeps |

!!! warning "The epoch follows the world year, not the equinox and not J2000"
    `GetSolarLongitudeDegrees` reads zero at the **March equinox**, and that equinox falls
    `CelestialMath.SpringEquinoxYearFraction` (about 22%) into a world year rather than at its start.
    Anchoring the planets anywhere else starts them that far round Earth's orbit from where the world
    says its own sun is — every planet in the wrong season, a body at opposition drawn near the sun,
    and nothing failing anywhere.

    The two suns still differ by up to about 4° of ecliptic longitude, because the seasonal model
    advances at a constant rate while the real world speeds up at perihelion. That is the equation of
    centre, it is worth about a quarter of an hour of transit timing, and
    `PlanetEphemerisTests.The_Sun_The_Planets_Are_Measured_From_Tracks_The_Sun_The_Seasons_Use`
    holds it there.

### Sampling: once a world minute, not once a frame

An orbit costs real arithmetic; a planet moves by arcminutes over a world hour. `CachedSkyEphemeris`
wraps any ephemeris and quantizes the sample to the world minute, the same trick
`AstrolabePlannerRenderer` uses for its sunrise search.

Two differences from that one are worth knowing:

- **The world minute is the whole key.** The sky clock also buckets on player position, because
  sunrise depends on latitude. A geocentric position does not, so there is nothing to add here.
- **The sample is taken at the top of the minute**, not at the instant asked for, so every caller
  inside a minute is told the same thing whoever asked first.

Only one sample is remembered. A caller that interleaves times — the astrolabe forecast scrolling
ahead while the sky renders the present — should hold its own instance rather than share one, or
every call misses. That is not hypothetical: `AstrolabePlannerRenderer` and `SextantReadingRenderer`
each build and keep their own, precisely because the astrolabe asks about forecast times while the
sky asks about now.

### Instruments point at an ephemeris, not at a position

`AstrolabeTarget` carries an `ISkyEphemeris` rather than a right ascension and declination, because
the instrument exists to answer questions about *other* times: it scrolls hours and days ahead, and a
planet is somewhere else by then. A recorded constellation supplies a `FixedEphemeris` and behaves
exactly as it did before.

One approximation is left in deliberately. `hoursUntilTransit` holds the target's right ascension
where the reading found it, rather than solving the transit against the ephemeris. A planet drifts
under half a degree a day, so a night's countdown is a couple of minutes out — below anything the
instruments can show. A comet near perihelion would not be, and that is where the solve has to become
iterative.

## Season-Anchored Events

Anything that should happen at the same point in the _year_ — a meteor shower peak, a seasonal
constellation window — must be anchored to **solar longitude**, never to a day of the year.

`CelestialMath.GetSolarLongitudeDegrees` is the seasonal term of the sidereal angle, extracted so the
two cannot drift apart. It is the sun's right ascension at local noon, which is what makes it stand
in for solar longitude:

```text
solarLongitude = ((totalDays - equinoxDay) mod daysPerYear) / daysPerYear * 360
equinoxDay     = daysPerYear * SpringEquinoxYearFraction
```

!!! warning "Day zero of a world year is the first of January, not the equinox"
    Vintage Story runs its own sun as `-tilt x cos(2*pi*(yearRel + 10/365))`
    (`SurvivalCoreSystem.GetSolarSphericalCoords`), so the sun crosses the equator northward about
    22% of the way into the world year — the same place the calendar starts calling the season
    spring. `CelestialMath.SpringEquinoxYearFraction = 0.25 - 10/365` is that anchor.

    Anchoring the year at day zero instead put the whole sky about 80° of solar longitude ahead of
    the world it is drawn over: Betelgeuse came to the meridian at midnight on day 269 — late
    September on a 360-day world — instead of mid-December.
    `CelestialMathTests.Betelgeuse_Comes_To_The_Meridian_At_Midnight_In_December` pins it.

!!! warning "A day of the year is not a season"
`daysPerYear` is **world configuration**. Vintage Story's default is 108, but a world can be
created with 12 or 360. An event pinned to "day 224" therefore lands in a different season on
every world, while 140° of solar longitude is late summer on all of them.

    `MeteorShowerActivityTests` pins it across 12-, 108- and 360-day
    years, and those tests fail loudly if day-of-year anchoring is reintroduced.

Since testing is not automated yet, this kinda error can lead correct on the world you happened to test and wrong everywhere else. Specially if you test in a flat world.

### Measuring distance from an anchor

Use `CelestialMath.ShortestAngularDistanceDegrees`, never a plain subtraction. A window straddling
0°/360° otherwise reads as nearly a full turn wide rather than a few degrees, and the event silently
never fires — or fires all year.

### A short year compresses every window

Because windows are angular, their length in days scales with `daysPerYear`. A 10° window is about
3 days on a 108-day year but **0.33 of a day** on a 12-day year, where it can fall entirely in
daylight and never be observable. That is inherent to anchoring on the angle rather than a defect,
but anything user-facing built on this should account for a window that may be shorter than a single
night.

### Turning a shower rate into visible streaks

`MeteorShowerActivity.ReadAll` is the runtime boundary between the world clock and the client visual:
it combines solar-longitude proximity, radiant altitude, moon phase brightness, and the same natural
darkness used by the starfield.

A published ZHR is meteors per hour of *real* watching, so `MeteorShowerVisualModel` spends it over
3600 real seconds rather than over a world hour. An observed rate of 120 therefore averages one
streak every 30 real seconds — watching a shower is meant to be a patient thing. The two clocks
differ by more than a factor of twenty: a default world hour passes in roughly two real minutes, so
spending the rate over the world hour instead would produce a meteor every second or so and read as
a storm on every ordinary night. `DebugMeteorRateMultiplier` in the client config exists to compress
that wait during development, and it scales only how often meteors spawn, never which shower they
come from.

Streak positions are generated around the radiant on the celestial sphere. Their tangent always
points away from it, and angular length grows with radiant separation. The renderer turns each frame
into a tapered four-section ribbon on the sky sphere and submits all active streaks in one mesh.
Transient positions are client-local visual state; the shared astronomical conditions remain fully
deterministic.

## The Milky Way

The band is the one part of the sky that is not a position. Everything else in the model — a star, a
plate, a planet, a meteor radiant — is projected one body at a time through `SkyProjection`. The
Milky Way is the light of the stars the catalog stops short of, spread over a quarter of the sky at
once, so it is drawn as the sphere itself rather than as an object on it.

### Galactic coordinates, once

`GalacticFrame` holds the J2000 rotation between galactic and equatorial coordinates, and it is the
only place in the mod that knows about it. `MilkyWayRenderModel.ProjectBand` walks a coarse grid in
galactic longitude and latitude, rotates each vertex into the equatorial frame, and hands it to the
same horizontal-coordinate and world-direction code every other body uses.

Two landmarks pin the rotation, in `GalacticFrameTests`: galactic (0, 0) has to land on Sagittarius A*
at 17h 45m, -29°, and galactic (180, 0) on the anticentre at 05h 46m, +29°. Get the rotation wrong by
a sign and the band still looks like a band — it simply rises in the wrong season, over the wrong
horizon, and nothing else in the sky disagrees with it.

### The glow is a texture, the mesh is only a sphere

Every feature of the band — the bulge, the Great Rift, the clouds, the reddening — lives in
`assets/astraterra/textures/environment/milky-way.png`, an equirectangular map in galactic
coordinates. So the mesh carries none of it: 72 by 36 cells is enough that a great circle does not
read as a polygon and that the horizon fade is smooth along it, and the texture is sampled per pixel
regardless.

The map is generated, not photographed. `tools/milkywaygen` integrates an exponential disc with four
logarithmic arms and a flattened bulge along every line of sight from the Sun's place in it, dimmed
by a thinner, flatter dust layer in front, and tone-maps the result. That keeps the asset ours, and
it means the band's shape can be argued with in parameters — a scale height, a dust opacity — rather
than repainted.

### It answers to the night, not to the star bias

`MilkyWayVisibility.CalculateOpacity` is deliberately not the star pass's brightness bias. A star is
a point source and survives a bright sky; surface brightness is the first thing any sky glow takes
away. So the band is gone below `TwilightFloor` darkness, and a full moon removes most of what is
left. A Milky Way that ignored either would read as a decal painted on the sky.

`MilkyWayBrightness` in `ModConfig/astraterra.json` scales the result, and zero switches the band off
entirely.

## The Sun And The Moon

The moon is not modelled by AstraTerra, and the sun's latitude and seasonal motion still come from
Vintage Story. Both are read through `IGameCalendar.GetSunPosition` and `GetMoonPosition`, so the mod
measures the bodies the game actually draws rather than maintaining a second visible-sun model.

When `longitudeAwareSun` is enabled, AstraTerra wraps the solar delegate that Vintage Story's
survival mod installed. The wrapper changes only `dayRel`, by `longitude / 360` of one rotation, and
then calls the captured delegate with the original position and year. The survival delegate therefore
continues to own latitude, axial tilt and seasonal declination while local solar noon shifts with X.
The server's `astraterra.json` is authoritative for this flag and sends the decision to every client;
a remote client's local copy cannot make its sun disagree with the server's daylight calculation.

Both return a unit vector in the same world space described above, with `Y = sin(altitude)`.

!!! warning "Vanilla's vector looks mirrored but is not"
`GetSunPosition` builds Z as `sin(zenith) * cos(azimuth)` with no negation, which reads as if
vanilla referenced its azimuth to `+Z` (south). It does not. Its zenith angle is
`2pi - acos(sin(altitude))`, which lands in the fourth quadrant where **sine is negative**, and
that factor supplies the minus sign. Expanded, vanilla returns exactly

    ```text
    X = cos(altitude) * sin(azimuth)
    Y = sin(altitude)
    Z = -cos(altitude) * cos(azimuth)
    ```

    — the same convention as the table above, with azimuth already measured clockwise from north.
    `SkyBodyModel.FromWorldDirection` therefore needs no rotation, and the same `atan2(X, -Z)`
    serves both vanilla bodies and the mod's own star directions.

    Do not "fix" this by rotating vanilla's azimuth by 180°. Checked against the survival mod's
    `GetSolarSphericalCoords` at 45°N: the sun reads 92.5° (east) at 06:30, 180.0° (south) at noon,
    and 267.5° (west) at 17:30.

Both accept a `totalDays` argument, so positions can be sampled into the future. The astrolabe
relies on this to move its clock along with its forecast.

### Replaceable solar motion and startup order

`IGameCalendar.OnGetSolarSphericalCoords` is a public settable delegate. The calendar constructor
starts with a degenerate placeholder, the survival mod replaces it during client `LevelFinalize` and
server `GameReady`, and any other mod may replace it again.

AstraTerra registers for those same ready stages and defers its assignment until the stage's handlers
have finished. It then captures and chains the delegate it found. Client and server use separate
installer instances, including in integrated single-player, and disabling or disposing the wrapper
restores the captured delegate only when AstraTerra's own wrapper is still installed.

This is cooperative rather than exclusive ownership. A sun mod already installed at the ready stage
is preserved and receives the longitude-shifted `dayRel`; a mod that assigns the property later wins
and clobbers AstraTerra's wrapper. In the latter case the sextant and astrolabe still follow the newly
visible sun because they call `GetSunPosition`, while AstraTerra's longitude-shifted star field no
longer stays synchronized with it. Passing both the shifted time and the original `posX` also means a
third-party delegate that already applies longitude would apply it twice; such a mod should disable
`longitudeAwareSun` on the server or coordinate delegate ownership explicitly.

The base survival delegate's seasonal declination is retained by the wrapper. The separate
[solar-declination issue](https://github.com/lalmei/astraterra/issues/31) tracks what, if anything,
AstraTerra's independent sidereal approximation should say about that motion.

### The moon's picture, and only its picture

`MoonDiscRenderer` draws the moon from the surface portrait the player asked for — pixel art, a
photograph of the real one, or Vintage Story's own disc — and asks the game to put its disc away
except when vanilla was chosen, through the same `moonScale` suppression a moon world uses. Nothing
else changes:
where the moon is, what phase it is in, how bright the night is and how the phase advances are still
the calendar's, read fresh each frame — so anything that moves the game's moon moves this one. The
planets keep `SolarSystemArt`; the moon overhead is `MoonArt`. The surface mesh stays level while
per-vertex lighting turns independently towards the sun. That separation keeps Tycho fixed in the
portrait and gives intermediate calendar phases their own curved terminator instead of rotating and
snapping a pre-phased photograph.

| Question | Answered by |
| --- | --- |
| Where | `GetMoonPosition(pos, totalDays)` |
| Surface | The full portrait selected by `MoonArt` |
| Phase | Continuous `MoonPhaseExact` lighting in `MoonDiscMeshBuilder` |
| Which way up | The portrait stays level through `MoonDiscModel.SurfaceRightAxis`; only its light turns sunward |
| How wide | `AngularDiameterDeg`: 7°, matching Vintage Story's own disc |

Vintage Story's own moon is about **7°** across — 256 quad units at `moonScale * 1.1`, hung at distance
50. `AngularDiameterDeg = 7.0` keeps the replacement photograph at that familiar apparent size. This
is about thirteen times the real moon's half-degree diameter, and it overflows the precision
telescope's field at high magnification, so a scope raised on it shows a crop of the lunar surface.

!!! warning "The night side is dark, and darkness is a hole in a daytime sky"
    The mesh dims the unlit side to faint earthshine, which is what the moon is: after dark it is
    drawn opaque and takes a bite out of the star field behind it. By day the same dark disc would be
    a disc of night punched into a blue sky, so the pass crossfades to additive blending as daylight
    comes up — darkness adds almost nothing, and only the lit face survives, which is also what a
    daytime moon looks like.

    Two draws of one quad, and the reason the moon is a pass of its own rather than part of the star
    pass: the star pass stops at dawn, and the moon does not.

## The Sky Clock

`SkyClock.Read` reports the hour, the phase of the day, and how long until the next horizon
crossing. It takes the sun altitude as a delegate rather than computing one, which keeps it pure and
testable while letting callers feed in Vintage Story's real sun.

| Phase           | Sun altitude                                                 |
| --------------- | ------------------------------------------------------------ |
| `Day`           | above `0°`                                                   |
| `Dusk` / `Dawn` | between `-6°` and `0°`, split by whether the sun is climbing |
| `Night`         | below `-6°` (civil twilight)                                 |

Sunrise and sunset are found by walking forward at most one world day in five-minute steps for a
horizon crossing, then bisecting the bracketing interval. The finer sweep matters near the polar
circles, where the sun can rise and set again inside the old 15-minute interval. Both results are
nullable: at a polar day or polar night no crossing exists, and the astrolabe says so rather than
inventing an hour.

Clock time on the astrolabe is local apparent solar time at the observer's longitude. The world's
internal clock stays universal; `CelestialMath.GetUniversalSolarTimeHours` reads that clock and
`CelestialMath.GetLocalSolarTimeHours` shifts it by `(longitude / 360) * hoursPerDay`. Which hour the
character panel shows is configured separately via `displayedClockTime` in `astraterra.json`:
`local` (the default), `universal`, or `zones`. Zoned time divides the circumference into one
whole-clock-hour step per zone, so custom world-day lengths keep integer clock readings.

!!! note "Longitude is real in the sky and on the displayed clock"
    AstraTerra installs its own `OnGetSolarSphericalCoords` wrapper that shifts the sun — and
    therefore daylight — with world X, chaining whatever delegate the survival mod installed rather
    than replacing it outright. The star field already shifted with longitude; the sun now matches.
    Set `longitudeAwareSun` to `false` in `astraterra.json` to keep vanilla's single time zone.

    See [Latitude And Longitude](latitude-and-longitude.md) for the world-config scale and the
    observer-position pipeline.

## What The Render Thread May Do

The sky pass runs inside Vintage Story's sun/moon render, on the render thread, every frame it draws.
Five thousand stars go through it, so the ordinary costs of comfortable code are not affordable here.

| Rule | Why |
| --- | --- |
| No LINQ on the per-frame path | `Select`/`Where`/`OrderBy` over the catalog cost **12 ms and 440 KiB per frame** — a stutter by itself, and about a gigabyte of garbage a minute |
| Project into reused buffers | `StarRenderModel.ProjectVisibleStars(..., List<RenderedStar> destination)` fills a caller-owned list and sorts it in place; the allocating overload is for callers that are not per-frame |
| Rendered bodies are structs | `RenderedBody` and `RenderedStar` are `readonly record struct`, so a visible sky is not three thousand heap objects a frame |
| Redo work only when it shows | The projection refreshes when the sky has turned or the observer moved `StarRefreshThresholdDeg` (0.05°) — a tenth of a star sprite. At default time speed the sky turns about 0.25° a second, so that is a few refreshes a second rather than sixty |
| Parse nothing per frame | A journal book's JSON is deserialized only when the written text changes |
| Batch, do not loop draw calls | Every sky sprite goes through `SkyBillboardMeshBuilder`: constellation marks (~3700) into one mesh, stars and planets into one mesh **per sprite** — three at most, since only a bright, a faint and a planet sprite are ever in play. Around 6700 draw calls a frame became about four |
| Colour rides on vertices | A per-body colour uniform forces a draw call per body. The batches carry colour in the mesh, which is what collapses them. Note this shades a star once rather than twice — see the warning below |
| `Vec4f` is a class | A tint built per body per frame is thousands of heap objects a second. The shader uploads its uniform the moment it is assigned, so the draw loops keep one mutable instance |
| Recurring logs go to `VerboseDebug` | `Notification` lands in `client-main.log`; a line every five seconds for every skipped frame flooded players' logs |

### Who may draw the sky, and who must ask permission

| Pass | Stage / order | Occluded by terrain? | Gate |
| --- | --- | --- | --- |
| Stars, planets, constellation marks, meteors | Opaque **0.3** (inside vanilla's sun/moon render) | **Yes** — terrain paints over it at 0.37 | none, by design |
| Sky coordinate grid | Opaque **0.96** | No | `SkyExposure` |
| Constellation overlay | Ortho | No | `SkyExposure` |
| Sextant readout | Ortho | No | `SkyExposure` |

!!! warning "The star pass must not be gated on sky exposure"
    Vanilla's own starfield settles this: `SystemRenderNightSky` draws at Opaque **0.1** with the
    depth test off and no exposure check of any kind, and is still not visible from inside a cave.
    The mod's star pass sits in the same part of the frame and gets the same occlusion for free.

    A whole-sky gate cannot express *"sky visible in that direction"*, so it can only be wrong one way
    or the other. Both have shipped: a rain-map check took the sky away from anyone standing under a
    tree (#71), and a light-level threshold took it away from a player standing indoors looking
    straight out of a window. What survives is deliberately weak — **any** sunlight at the eye counts —
    because skylight and line of sight come through the same openings, and it now guards only the
    passes that draw over finished terrain.

    `.stars debug` reports the numbers behind the verdict, so the next report of this comes back as
    `skyExposure=blocked; eyeY=…; rainMapY=…; sunlightAtEye=…` rather than a screenshot.

### Measuring it

Every claim above is checkable from inside the game, in one session, without a rebuild:

```
.stars render                              # what is drawing now
.stars render constellations off           # switch one path off
.stars render stars off                    # ...and another
.stars render all on                       # put it back
```

The toggles are session-scoped on purpose — a measurement tool, not a setting — and cover `stars`
(with the planets that share its billboard path), `constellations`, `deepsky`, `meteors`, `comets`
and `milkyway`. Switching
both `stars` and `constellations` off also skips the projection they share, so its cost shows up too
rather than hiding behind a draw that no longer happens.

Every 30 seconds the debug log then reports what the pass actually cost:

```text
AstraTerra sky cost: frames=1800; ms/frame=0.31 (peak 2.44); drawCalls/frame=2989 (peak 3011);
                     meshUploads=1; meshUpdates=7; paths=stars=on; constellations=off; deepsky=on; meteors=on
```

Draw calls and mesh uploads are counted at the GL call itself, in `SkyPassMetrics`, not inferred from
list lengths — the old line reported "how many dots were built", which stopped being the number of
draw calls the moment those dots were batched. Peaks sit next to means because they answer different
questions: the mean is what the pass costs, the peak is what the player felt.

!!! warning "Batching changed how a star is tinted"
    The old per-star path set the tint as **both** `RgbaTint` and `RgbaLightIn`, and the standard
    shader computes `rgbaTint * applyLight(ambient, rgbaLightIn) * vertexColour` — so the tint was
    applied twice, once directly and once through the light mix, which desaturates it. The batch sets
    both uniforms to white and carries the colour on the vertices, applying it once.

    Star colours therefore read slightly more saturated than they did. That is a look change, not a
    correctness one, and it is the one part of the batching work that has to be judged by eye rather
    than by a counter. Reproducing the old shading exactly would mean re-implementing `applyLight` on
    the CPU, including its point-light term, which would drift with any change to Vintage Story's
    shaders.

!!! warning "This is a player-visible contract, not a micro-optimisation"
    A player reported the mod eating memory, stuttering while moving, and flooding the client log —
    with OpenAL failing to allocate sound sources alongside it. All of it traced back to the star
    projection running the full LINQ chain every frame.
    `StarRenderModelTests.ProjectVisibleStars_Into_A_Reused_Buffer_Allocates_Nothing_Per_Frame` pins
    the allocation, because nothing else fails when it comes back.

## Where The Invariants Are Pinned

| Invariant                                     | Test                                                                                                         |
| --------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Sky turns east to west                        | `CelestialMathTests.Stars_Travel_East_To_West_Across_The_Night`                                              |
| Sidereal time advances                        | `CelestialMathTests.VanillaAlignedSidereal_AdvancesWithTimeOfDay`                                            |
| Sidereal time gains going east                | `CelestialMathTests.VanillaAlignedSidereal_AppliesLongitudeOffset`                                           |
| Transit counts down                           | `AstrolabeServiceTests.Read_Counts_Down_To_The_Next_Transit`                                                 |
| Sidereal day is shorter than solar            | `AstrolabeServiceTests.SiderealCycle_Runs_Slightly_Shorter_Than_The_Solar_Day`                               |
| Rising is east of the meridian                | `AstrolabeServiceTests.Read_Uses_Live_Sky_Direction_To_Distinguish_Rising_And_Setting`                       |
| Azimuth is north-referenced                   | `SkyBodyModelTests.Azimuth_Is_Measured_Clockwise_From_North`                                                 |
| Moving and fixed bodies share one projection  | `SkyProjectionTests.A_Moving_Body_Lands_Exactly_Where_A_Star_At_The_Same_Position_Does`                      |
| The ecliptic is tilted the right way          | `CelestialMathTests.EclipticToEquatorial_Places_The_North_Ecliptic_Pole_In_Draco`                            |
| An ephemeris is sampled once a world minute   | `SkyEphemerisTests.A_Cached_Body_Is_Sampled_Once_Per_World_Minute_However_Many_Frames_Pass`                  |
| Planets are where they really were            | `PlanetEphemerisTests.A_Planet_Is_Where_It_Really_Was_At_A_Historic_Opposition`                              |
| World time zero is the March equinox          | `PlanetEphemerisTests.The_World_Clock_Starts_At_The_March_Equinox`                                           |
| An inner planet stays near the sun            | `PlanetEphemerisTests.An_Inner_Planet_Never_Strays_Far_From_The_Sun`                                         |
| Retrograde motion falls out of the maths      | `PlanetEphemerisTests.Mars_Turns_Back_On_Itself_Without_Anyone_Scripting_It`                                 |
| Jupiter takes ~12 world years on any world    | `PlanetEphemerisTests.Jupiter_Takes_About_Twelve_World_Years_Whatever_A_World_Year_Is`                       |
| Element rates match their semi-major axes     | `PlanetCatalogAssetTests.Every_Orbit_Obeys_Kepler_Third_Law`                                                 |
| Vanilla vectors need no rotation              | `SkyBodyModelTests.Recovers_The_Angles_Vintage_Story_Encoded`                                                |
| The per-frame sky path allocates nothing       | `StarRenderModelTests.ProjectVisibleStars_Into_A_Reused_Buffer_Allocates_Nothing_Per_Frame`                  |
| Constellation marks are one batched draw      | `ConstellationDotMeshBuilderTests`, `BootstrapSmokeTests.Telescope_Deep_Sky_Plates_Render_In_Front_Of_Catalog_Stars` |
| Stars batch by sprite, not one draw each      | `SkyBillboardMeshBuilderTests`, `BootstrapSmokeTests.Stars_Are_Drawn_As_Batches_Rather_Than_One_Quad_Each`   |
| A journal book can be carried in the off-hand | `BookOffhandStorageTests`                                                                                    |
| Draw loops do not allocate a tint per body    | `BootstrapSmokeTests.The_Star_And_Planet_Draw_Loops_Do_Not_Allocate_A_Tint_Per_Body`                         |
| Cost numbers mean what they say               | `SkyPassMetricsTests`                                                                                        |
| One path goes dark, the rest keep drawing     | `SkyRenderPathsTests`                                                                                        |
| The band lands on Sagittarius and the anticentre | `GalacticFrameTests`                                                                                     |
| The band's glow map wraps without a seam      | `MilkyWayRenderModelTests.The_Seam_Column_Repeats_The_First_One_At_The_Far_Edge_Of_The_Map`                  |
| The band goes before the stars do             | `MilkyWayVisibilityTests`                                                                                    |
| A block overhead never hides the whole sky    | `SkyExposureTests.Any_Skylight_At_All_Keeps_The_Sky`                                                        |
| Day phases and polar cases                    | `SkyClockTests`                                                                                              |
| Solar longitude is the sidereal seasonal term | `CelestialMathTests.SolarLongitude_Is_The_Seasonal_Term_Of_The_Sidereal_Angle`                               |
| A full turn per year on any year length       | `CelestialMathTests.SolarLongitude_Runs_A_Full_Turn_Over_A_World_Year_Whatever_Its_Length`                   |
| Angular distance takes the short way round    | `CelestialMathTests.ShortestAngularDistance_Never_Exceeds_Half_A_Turn`                                       |
| A season survives a change of year length     | `MeteorShowerActivityTests.A_Shower_Keeps_Its_Season_On_A_Twelve_Day_Year_And_A_Three_Hundred_Sixty_Day_One` |
| Windows straddling 0°/360° behave normally    | `MeteorShowerActivityTests.A_Window_Straddling_The_Wrap_Behaves_Exactly_Like_One_That_Does_Not`              |
