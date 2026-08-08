# Celestial Model

How AstraTerra turns a world clock into positions in the sky. Every renderer — starfield,
constellation overlay, sky grid, sextant, astrolabe — reads from this one pipeline, so a sign error
here moves the entire sky at once.

## Coordinate Conventions

Get these wrong and everything downstream is subtly mirrored.

| Quantity | Convention |
| --- | --- |
| Altitude | Degrees above the horizon. `0` is the horizon, `+90` the zenith. |
| Azimuth | Degrees clockwise from north. North `0`, east `90`, south `180`, west `270`. |
| World X | East is `+X`. |
| World Y | Up is `+Y`. |
| World Z | **North is `-Z`**, south is `+Z`. |

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
sidereal = seasonalTurns * 360 + (localSolarHours - 12) * 15
```

The sun transits at local noon, so at that instant sidereal time equals the sun's right ascension —
which is what the seasonal term stands in for. Every solar hour after noon adds another 15°.

!!! warning "Sidereal time must increase with time"
    This is the single most load-bearing sign in the mod. Hour angle is `sidereal - rightAscension`,
    so if the sidereal angle *decreases* as the day goes on, the hour angle decreases too and the
    entire sky — stars, constellations, deep-sky objects, grid lines — rotates **west to east**.

    Writing this term as `(12 - localSolarHours) * 15` is exactly that bug. It is easy to miss
    because noon and midnight are the fixed points of the sign flip: at noon the term is `0`, and at
    midnight it is `±180`, which normalises to the same angle either way. A test that only samples
    noon and midnight cannot tell the two versions apart.

    `Stars_Travel_East_To_West_Across_The_Night` in `CelestialMathTests` pins the direction by
    tracking a star's azimuth through transit instead. Keep it.

The seasonal term also means the sidereal day is slightly *shorter* than the solar day. The sky
gains a full extra turn over one world year:

```text
rate = 15 + 360 / (daysPerYear * hoursPerDay)   degrees per hour
```

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

## The Sun And The Moon

The sun and moon are **not** modelled by AstraTerra. They come from Vintage Story itself, via
`IGameCalendar.GetSunPosition` and `GetMoonPosition`. The mod measures the bodies the game actually
draws, rather than a parallel model that could drift out of agreement with the visible daylight.

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

## The Sky Clock

`SkyClock.Read` reports the hour, the phase of the day, and how long until the next horizon
crossing. It takes the sun altitude as a delegate rather than computing one, which keeps it pure and
testable while letting callers feed in Vintage Story's real sun.

| Phase | Sun altitude |
| --- | --- |
| `Day` | above `0°` |
| `Dusk` / `Dawn` | between `-6°` and `0°`, split by whether the sun is climbing |
| `Night` | below `-6°` (civil twilight) |

Sunrise and sunset are found by walking forward at most one world day in 15-minute steps for a
horizon crossing, then bisecting the bracketing interval. Both are nullable: at a polar day or polar
night no crossing exists, and the astrolabe says so rather than inventing an hour.

Clock time itself is `CelestialMath.GetLocalSolarTimeHours`, deliberately *without* a longitude
term, so the astrolabe agrees with the clock the game shows elsewhere.

!!! note "Longitude is applied to the sky, not the clock"
    `GetVanillaAlignedLocalSiderealAngle` shifts the star field by `longitude / 15` hours, so
    travelling east or west rotates the sky. The displayed hour does not shift, because vanilla's
    own clock does not. Note also that the `/ 15` conversion assumes a 24-hour day; on a world with
    a different `hoursPerDay` the longitude offset is scaled accordingly.

## Where The Invariants Are Pinned

| Invariant | Test |
| --- | --- |
| Sky turns east to west | `CelestialMathTests.Stars_Travel_East_To_West_Across_The_Night` |
| Sidereal time advances | `CelestialMathTests.VanillaAlignedSidereal_AdvancesWithTimeOfDay` |
| Sidereal time gains going east | `CelestialMathTests.VanillaAlignedSidereal_AppliesLongitudeOffset` |
| Transit counts down | `AstrolabeServiceTests.Read_Counts_Down_To_The_Next_Transit` |
| Sidereal day is shorter than solar | `AstrolabeServiceTests.SiderealCycle_Runs_Slightly_Shorter_Than_The_Solar_Day` |
| Rising is east of the meridian | `AstrolabeServiceTests.Read_Uses_Live_Sky_Direction_To_Distinguish_Rising_And_Setting` |
| Azimuth is north-referenced | `SkyBodyModelTests.Azimuth_Is_Measured_Clockwise_From_North` |
| Vanilla vectors are converted | `SkyBodyModelTests.Vanilla_Sun_Azimuth_Is_Rotated_From_South_To_North` |
| Day phases and polar cases | `SkyClockTests` |
