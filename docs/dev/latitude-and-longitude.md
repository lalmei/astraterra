# Latitude And Longitude

Where AstraTerra's sky gets its observer position, and how much of that Vintage Story actually
provides. The short version: **latitude is real and comes from the game; longitude does not exist in
Vintage Story and AstraTerra invents it.**

All findings below are from the shipped assemblies (`VintagestoryLib`, `VSEssentials`,
`VSSurvivalMod`) for the version this mod targets.

## Latitude, as Vintage Story defines it

`IGameCalendar.OnGetLatitude` is a settable delegate taking a world Z and returning **-1 to +1**
(south pole to north pole). Two things assign it:

| Assigned by | Value |
| --- | --- |
| `GameCalendar` constructor | `(posZ) => 0.5` — a constant |
| `VSEssentials`, at world load | The real mapping below |

### The real mapping

Driven by two world-configuration values:

| Config key | Default | Meaning |
| --- | --- | --- |
| `worldClimate` | `realistic` | Whether latitude varies at all |
| `polarEquatorDistance` | `50000` | Blocks from the equator to a pole |

On a **realistic** world, latitude is a triangle wave in Z:

```csharp
// VSEssentials, decompiled
double h = polarEquatorDistance / climateMapScale / climateMapSubScale;   // 32 and 16
double z = posZ / climateMapScale / climateMapSubScale + ZOffset;
return 2.0 / h * (h - Math.Abs(Math.Abs(z / 2.0 - h) % (2.0 * h) - h)) - 1.0;
```

Which works out to:

| Quantity | Default value |
| --- | --- |
| Equator to pole | **50,000 blocks** (`polarEquatorDistance`) |
| Full cycle, equator → N pole → equator → S pole → equator | **200,000 blocks** (`4 x polarEquatorDistance`) |
| Degrees of latitude per block | ~0.0018 |

Latitude **repeats**. Travel far enough north and you arrive back at the equator, then at a south
pole. The world is a set of stacked climate bands, not a globe.

!!! warning "`ZOffset` means the equator is not at z = 0"
    The mapping includes a world-specific `ZOffset` taken from the climate noise generator, so the
    equator sits wherever that seed put it. Never assume `z = 0` is the equator — always ask
    `OnGetLatitude`.

!!! danger "A patchy-climate world has no latitude at all"
    When `worldClimate` is anything other than `realistic`, `getLatitude` short-circuits:

    ```csharp
    if (!latdata.isRealisticClimate) return 0.5;
    ```

    It returns a **constant 0.5** — which AstraTerra maps to 45°N — for every position in the world.
    The sky is then identical everywhere, and the mod's central promise that the stars change as you
    travel north or south silently does not happen. `realistic` is the default, so most worlds are
    fine, but this is worth knowing before debugging a "latitude is stuck" report.

### Hemisphere

Separately, `OnGetHemisphere` / `GetHemisphere(BlockPos)` returns an `EnumHemisphere`, assigned by
`VSSurvivalMod`. AstraTerra does not currently use it; latitude sign already carries the same
information.

## Longitude, which Vintage Story does not have

There is **no longitude concept anywhere in the Vintage Story API**. No delegate, no calendar
property, nothing derived from world X.

The sun looks like it might depend on X — the delegate signature accepts one:

```csharp
SolarSphericalCoordsDelegate(double posX, double posZ, float yearRel, float dayRel)
```

It does not. The survival mod's implementation takes `posX` and never references it:

```csharp
// VSSurvivalMod, decompiled
public SolarSphericalCoords GetSolarSphericalCoords(double posX, double posZ, float yearRel, float dayRel)
{
    double lat = api.World.Calendar.OnGetLatitude.Invoke(posZ) * Math.PI / 2.0;   // posZ
    float hourAngle = (float)Math.PI * 2f * (dayRel - 0.5f);                      // dayRel
    double dec = -EarthAxialTilt * Math.Cos(2f * Math.PI * (yearRel + 0.0274f));  // yearRel
    ...
}
```

Latitude from Z, hour angle from the world clock, declination from the year. **X is ignored.**

The consequence: sunrise happens at the same instant for every player on a server, wherever they
stand. The entire world is one time zone. Daylight strength, which is derived from the sun vector,
inherits this.

## What AstraTerra does with both

| AstraTerra | Source | Agrees with Vintage Story? |
| --- | --- | --- |
| `MapGameLatitude` | `OnGetLatitude`, scaled to degrees | **Yes** — delegates to the game |
| `MapWorldZ` / `MapRepeatingLatitude` | Own triangle wave, `WorldLatitudeBandSize = 100000` | Fallback only, and see below |
| `MapWorldLongitude` | `polarEquatorDistance`, centred on `mapSizeX * 0.5` | **Scale only** — Vintage Story has no longitude origin |

**Latitude is handled correctly.** `MapGameLatitude` asks the calendar and multiplies by 90, so it
inherits the real mapping, the `ZOffset`, and the repeating bands for free.

The `MapWorldZ` fallback only runs when `OnGetLatitude` is null, which never happens in practice —
`GameCalendar`'s constructor always assigns one. Note its `WorldLatitudeBandSize` of 100,000 is half
Vintage Story's actual 200,000-block cycle, so if it ever did run it would disagree.

**Longitude is invented**, because there is nothing to inherit — but it now uses the same
`polarEquatorDistance` scale as latitude:

```csharp
// LatitudeMapper.MapWorldLongitude — polarEquatorDistance from world.Config, default 50_000
var polarEquatorDistance = WorldClimateScale.GetPolarEquatorDistance(world);
```

!!! note "Longitude now matches Vintage Story's latitude scale"
    `MapWorldLongitude` reads `polarEquatorDistance` from the world's synced configuration
    (`world.Config`), falling back to 50,000 blocks when absent. With that fallback value, 90° of
    longitude and 90° of latitude both cover 50,000 blocks, so eastward travel shifts the sky at
    roughly 8,300 blocks per hour of rotation instead of the old map-size-derived scale. World
    presets may configure a different distance, and both axes inherit it together.

    The prime meridian remains at `mapSizeX * 0.5`; only the degrees-per-block scale changed.

## The divergence, and how it was resolved

Vintage Story's own sun ignores longitude, so while AstraTerra's star field shifted with X and the
sun did not, travelling east or west drifted the stars out of step with the sun that is supposed to
anchor them. Three clocks were in play and only two agreed:

| Clock | Longitude applied before | Longitude applied now |
| --- | --- | --- |
| Vintage Story's sun and daylight | No | **Yes**, while `longitudeAwareSun` is on |
| AstraTerra's star field | Yes, always | Yes, but only when the sun does |
| AstraTerra's astrolabe clock | No | **Yes**, as displayed local time |
| The moon | No | **Yes** where AstraTerra draws it; vanilla's own disc cannot be moved |

This was settled on [issue #43](https://github.com/lalmei/astraterra/issues/43) in favour of making
longitude real: AstraTerra installs its own `OnGetSolarSphericalCoords` that uses `posX`, so the sun,
daylight and the stars all shift together. The delegate accepts an X coordinate because longitude was
meant to matter in the original design of this system and the idea was shelved, not rejected.

One amendment came with the decision: **the world's internal clock stays a single universal time**,
and longitude is applied only to the time a player is *shown*. Nothing about scheduling, save data or
multiplayer sync changes.

!!! note "This is a large behavioural change, and deliberately so"
    Day/night timing becomes a function of X for everything downstream of the sun vector — crops,
    temperature, mob spawning, other mods, and other players on the same server. That cost was
    weighed and accepted rather than overlooked; it ships behind a config flag so a server owner can
    keep vanilla's single time zone.

!!! warning "The sky may only use a longitude the sun has"
    Turning the flag off, or another mod taking the solar delegate back, would otherwise recreate
    the very divergence this resolved — a star field shifted east of a sun that never moved. Nothing
    outside `LongitudeAwareSunInstaller` maps observer longitude for itself: the star field, the
    instruments, the recorded sightings and the displayed clock all go through
    `ObserverLongitude.ForObserver`, which answers zero unless AstraTerra's wrapper is the delegate
    the calendar is holding right then.

Shipped: [#44](https://github.com/lalmei/astraterra/issues/44) (the scale above, a prerequisite —
at the old scale longitude would have been too coarse to notice),
[#122](https://github.com/lalmei/astraterra/issues/122) (the longitude-aware sun) and
[#123](https://github.com/lalmei/astraterra/issues/123) (displayed local time, and what a recorded
sighting's hour means once the hour depends on where you stand). Still open:
[#124](https://github.com/lalmei/astraterra/issues/124) (longitude by chronometer).

## Related

- [Celestial Model](celestial-model.md) — how the sidereal angle these feed into becomes a position in the sky
