# Extending AstraTerra

AstraTerra draws a sky from five catalogs, and another mod can replace any of them. That is the whole
of the supported surface: there is no event bus, no registry, and no JSON hook a content pack can drop
a file into. A mod that wants a different sky references the AstraTerra assembly, gets the mod system,
and hands it new catalogs.

[AstraExtera](https://github.com/lalmei/astraextera) is the worked example: it makes the world a moon
of a gas giant, which needs four of the five.

!!! warning "AstraTerra is AGPL-3.0-only"
    Linking against this assembly puts the licence on your mod too. That is a deliberate choice, not
    an oversight.

## The supported surface

Everything below is on `AstraTerra.AstraTerraModSystem`.

```csharp
using AstraTerra;
using AstraTerra.Astronomy;

var astraTerra = api.ModLoader.GetModSystem<AstraTerraModSystem>();
if (astraTerra is null)
{
    return; // AstraTerra is not installed; draw nothing and carry on.
}
```

| Method | Replaces | Empty sky |
| --- | --- | --- |
| `bool ReplaceStarCatalog(StarCatalog)` | the fixed stars, guide-star groups, sky cultures and deep-sky plates | not possible; pass a catalog with no stars |
| `void ReplacePlanetCatalog(PlanetCatalog?)` | the five naked-eye planets and the observer's own orbit | `null` |
| `void ReplaceCometCatalog(CometCatalog?)` | the comet apparitions | `null` |
| `void ReplaceMeteorShowers(IReadOnlyList<MeteorShowerEntry>)` | the annual showers | an empty list |
| `void ReplaceNearBodies(NearBodyCatalog?)` | bodies close enough to draw as a disc | `null`, or `NearBodyCatalog.Empty` |

`ReplaceStarCatalog` is the only one that reports failure: it returns `false` when AstraTerra's own
catalog never loaded, in which case astronomy is disabled for the session and there is nothing to
replace it in.

Each call pushes the new catalog into every consumer that is already built — the star pass, the
constellation overlay, the astrolabe planner, the sextant, the Sky Disc — so the change is visible on
the next frame. Nothing is cached behind them, and there is no separate invalidate step.

Everything else in the assembly is internal in practice, whatever its C# accessibility. `CelestialMath`,
`SkyProjection`, `LatitudeMapper`, `ObserverLongitude` and the renderers are public because the tests
need them, and they change without notice.

## When to call

AstraTerra loads its own catalogs in `AssetsLoaded`, before a client knows which world it is joining, so
a sky derived from the world seed cannot be supplied that early. Let the shipped catalogs load, then
replace them once you know what this save's sky is.

The catalogs reach the client renderers only after `StartClientSide` has built them, and AstraTerra
leaves `ExecuteOrder` at the `ModSystem` default. Give your own mod system a later order so your
`StartClientSide` runs after AstraTerra's:

```csharp
public override double ExecuteOrder() => 1.0;
```

Call on **both sides** where it matters. The star catalog is read by the server too, for the
constellation book service and the `/stars` commands, so a server that replaced the sky only on the
client will validate figures against stars its players cannot see.

`ReplaceNearBodies` is the exception to the ordering rule: the catalog is stored whether or not the
renderer exists yet, and applied when it is built. The near-body pass also needs no star catalog at
all, so a world dominated by the planet it orbits still shows that planet when the star field failed to
load.

## Star catalogs

```csharp
public sealed record StarCatalogEntry(
    int Hip,
    double RightAscensionDeg,
    double DeclinationDeg,
    double VisualMagnitude,
    double? BvColorIndex,
    bool IsGuideStar);

new StarCatalog(stars, guideGroups, skyCultures, deepSkyObjects);
```

Right ascension and declination are **equatorial, geocentric, and constant** — where the body is, with
nothing about the observer in them. Latitude, longitude and the hour enter afterwards, in the
projection. Magnitude is the ordinary astronomical one: smaller is brighter, and the drawn brightness
curve saturates at magnitude 0.4.

!!! danger "HIP ids are save data"
    A constellation is stored in a player's book as edges between `Hip` values. A mod that generates a
    catalog owns the stability of those ids: the same world must number the same stars the same way on
    every load, or every figure a player has drawn will point somewhere else after a restart. Derive
    them from the world seed, never from iteration order.

A replaced catalog usually wants replaced sky cultures too. The shipped `modern_iau` figures are lines
between real HIP numbers, so under a generated sky `.stars build Ori` either fails or draws nonsense.

## Planets, comets and showers

`PlanetCatalog` is six Keplerian elements per body plus per-century rates, with the observer's own orbit
under `Observer` rather than in the `Planets` list — Earth's orbit is subtracted from every other one
rather than drawn. A mod that moves the world to another star has to supply its own or pass `null`; the
shipped elements are this solar system's.

`CometCatalog` entries are authored apparitions, not orbits: a period and first perihelion in world
years, a window half-width, a brightness curve, and a track of right-ascension/declination keyframes
indexed by signed phase against perihelion. Visibility is the window, not a faint magnitude — the
brightness curve never reaches zero, so a comet made faint would still draw.

`MeteorShowerEntry` anchors its peak to **solar longitude**, degrees from the March equinox, never to a
day of the year. `daysPerYear` is world configuration, so 140° is late summer on a 12-day world and a
360-day world alike while day 224 is not.

The shipped showers are named for the constellations their radiants sit in and are the debris of the
shipped comets. Replace the star catalog and you should replace these too, or a radiant will be named
for a figure nobody can point to.

## Near bodies

A near body is the one thing the base mod ships no catalog for. It exists for worlds that are
themselves moons: a parent planet tens of degrees wide, and sibling moons crossing in front of it.

```csharp
public sealed record NearBodyEntry(
    string Id,
    string DisplayName,
    NearBodyKind Kind,
    double AngularDiameterDeg,
    double HourAngleDeg,
    double HourAngleRateDegPerDay,
    double DeclinationDeg,
    double Brightness,
    NearBodyFace Face,
    NearBodyOrbit? Orbit = null);
```

Near bodies are placed by **hour angle**, not right ascension, because they do not keep station with
the stars. A tidally locked world's parent planet sits at one hour angle forever with a zero rate,
hanging at one spot while the star field turns behind it. Declination is the angle out of the
observer's celestial equator, which for a locked world is also its orbital plane: zero puts a body on
the circle the sun travels, where coplanar moons belong.

An hour angle and a rate can only send a body right round the sky, which is what outer siblings do. A
sibling orbiting inside the observer swings back and forth about the parent instead, out to
`asin(DistanceRatio)` and no further, the way Venus is bound to the sun. That is not a rate, so supply
a `NearBodyOrbit` and it is solved per frame; it supersedes the flat hour angle and rate.

`NearBodyFace` is a square RGBA image with the disc centred and everything outside it transparent, plus
`DiscFraction` — how much of the image's half-width is globe rather than ring, which is what the shading
pass needs to know where the terminator stops. Faces are pictures rather than models because a ringed
giant is not a parameter list.

`NearBodyCatalog.HidesVanillaMoon` asks Vintage Story's own moon to stand down. A world that is itself
a moon has none, and leaving the vanilla disc up puts a second unrelated body beside the parent planet.
Only the drawing stops: moonlight, the phase the calendar reports, and the length of the night are
untouched.

Placed near bodies are valid Sextant targets, sorted farthest first so a sibling passing in front of
the parent is drawn over it and one round the far side goes behind it. They are placed every frame
rather than cached, which keeps the sun direction they are lit by exactly current; there are only ever
a handful.

## What you cannot do

- **Add to a catalog.** Every call replaces the whole thing. Read what is there, build a new list, hand
  it back.
- **Ship a catalog as an asset from your own mod.** The loaders resolve `astraterra:` asset paths only.
  Build the catalog in code, or read your own asset and construct the records.
- **Change the sun, the moon or the calendar through AstraTerra.** Those stay Vintage Story's.
  AstraTerra wraps `IGameCalendar.OnGetSolarSphericalCoords` to shift the sun with longitude, and it
  chains rather than replaces. Read
  [Replaceable solar motion and startup order](celestial-model.md#replaceable-solar-motion-and-startup-order)
  before installing a solar delegate of your own. A delegate that also applies longitude will apply it
  twice; such a mod should ask the server to set `LongitudeAwareSun` to `false`.
- **Persist anything through AstraTerra.** Constellation journals live in the player's book item, and
  the astrolabe's cut latitude lives in the astrolabe's own stack attributes. Both are AstraTerra's
  save format and are not a public one.

## Related

- [Architecture](architecture.md) — which folder owns what, and the boundaries between them
- [Celestial Model](celestial-model.md) — the pipeline a replaced catalog feeds into
- [Data Pipeline](data-pipeline.md) — the shape of the shipped catalog assets, which is the shape your records mirror
