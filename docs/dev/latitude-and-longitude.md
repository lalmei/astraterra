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
| `MapWorldLongitude` | Invented from `mapSizeZ` | **No** — nothing in the game to agree with |

**Latitude is handled correctly.** `MapGameLatitude` asks the calendar and multiplies by 90, so it
inherits the real mapping, the `ZOffset`, and the repeating bands for free.

The `MapWorldZ` fallback only runs when `OnGetLatitude` is null, which never happens in practice —
`GameCalendar`'s constructor always assigns one. Note its `WorldLatitudeBandSize` of 100,000 is half
Vintage Story's actual 200,000-block cycle, so if it ever did run it would disagree.

**Longitude is invented**, because there is nothing to inherit:

```csharp
// LatitudeMapper.MapWorldLongitude
var polarEquatorDistance = mapSizeZ * 0.5;   // 90 degrees of longitude
```

!!! warning "The longitude scale is derived from the wrong quantity"
    This uses **half the map size** as the equator-to-pole distance. Vintage Story's actual
    equator-to-pole distance is the `polarEquatorDistance` world-config value, defaulting to 50,000
    blocks and **independent of map size**.

    On a 1,024,000-block world that is a factor of roughly **ten**: 90° of latitude covers 50,000
    blocks while 90° of longitude covers 512,000. A degree of longitude and a degree of latitude
    should cover comparable ground on a sphere, and here they do not.

    | | Blocks for 90° | Blocks per hour of sky rotation |
    | --- | --- | --- |
    | Latitude (Vintage Story) | 50,000 | n/a |
    | Longitude (AstraTerra, `mapSizeZ` = 1,024,000) | 512,000 | ~85,300 |
    | Longitude, if it used `polarEquatorDistance` | 50,000 | ~8,300 |

    The second row is why the longitude divergence is currently almost invisible: you would have to
    walk 85,000 blocks east to shift the sky by an hour. Correcting the scale would make it visible
    within a long but ordinary journey — which is exactly why the scale and the behaviour need
    deciding together.

## The open question

Because Vintage Story's sun ignores longitude and AstraTerra's star field does not, travelling east
or west drifts the stars out of step with the sun that is supposed to anchor them. Three clocks are
in play and only two agree:

| Clock | Longitude applied? |
| --- | --- |
| Vintage Story's sun and daylight | No |
| AstraTerra's star field | **Yes** |
| AstraTerra's astrolabe clock | No |

What longitude *should* do — including whether to keep it at all — is an open design decision. See
the tracking issue for the options and the current recommendation.

## Related

- [Celestial Model](celestial-model.md) — how the sidereal angle these feed into becomes a position in the sky
