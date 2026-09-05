# AstraTerra Data Pipeline

Runtime catalog assets are committed under `assets/astraterra/data/`. Builds and tests must not
depend on rerunning the catalog generator: the generators below are run by hand when a catalog needs
to change, and their output is reviewed and committed like any other file.

## Catalog Generator

The developer-only generator lives in `tools/cataloggen/`.

```bash
cd tools/cataloggen
python -m cataloggen.main --help
```

The committed v1 star catalog is generated from local HYG v4.2 source data. Source dataset placement and licensing notes live in `tools/cataloggen/sources/README.md`.

Pass the Modern IAU asset through `--required-sky-culture-json` when regenerating the runtime catalog. This retains every star referenced by an authored figure even when it falls beyond the normal brightness or count cutoff; `hipparcos-supplement.v1.json` supplies exceptional referenced entries absent from HYG.

## Milky Way Texture Generator

The band's glow map has its own generator in `tools/milkywaygen/`, and it needs no source dataset: it integrates a galaxy model along every line of sight rather than resampling a survey or repainting a photograph, so the committed texture carries no third-party licence.

```bash
cd tools/milkywaygen
python -m milkywaygen.main --help
python -m milkywaygen.main            # rewrites assets/astraterra/textures/environment/milky-way.png
python -m unittest tests.test_galaxy
```

Regenerating with the committed defaults reproduces the committed texture; the seed is one of them.

## Pixel-Art Solar System

The sun's family ships in two sets of pictures, and the pixel one is normalised from the drawn art
rather than committed as it arrives. `tools/generate_pixel_solar_system.py` reads
`textures/environment/solar-system-pixel_art/` — bodies floating on a wide transparent canvas — and
writes `textures/environment/solar-system-pixel/`, cropping each body to its own ink and padding it
back out to a square, which is the convention the photographic set already keeps and what makes
`imageWidthInDiameters` size both sets alike.

```bash
python tools/generate_pixel_solar_system.py
```

Two bodies need more than a crop. Saturn's ink is its rings, so its square is measured from the
globe inside them — centre and diameter are constants in the script, taken off the art — and widened
to the ring span the catalogue quotes. The moon arrives as a single full face, so the other seven are
cut from it: an elliptical terminator, an unlit side dimmed to the earthshine the photographic new
moon keeps, and the cut quantised to the art's own pixel grid.

The art draws one face per planet rather than three, so all of a planet's phase faces resolve to the
same picture, and it draws only the larger moons, so Callisto and Saturn's small four share the one
small moon it does draw. `SolarSystemTextures` holds that mapping and
`SolarSystemArtAssetTests` checks that every catalogued body resolves to a square picture that
exists.

## Runtime Assets

- `star-catalog.v1.json`: baked fixed-star catalog.
- `guide-stars.v1.json`: grouped guide-star metadata.
- `sky-cultures.v1.json`: manifest for authored sky cultures.
- `sky-cultures/*.json`: authored constellation line data.
- `deep-sky.v1.json`: telescope-only deep-sky object metadata, including each texture's four Stellarium `worldCoords` corners in texture-coordinate order.
- `meteor-showers.v1.json`: annual meteor showers. Radiant, peak solar longitude, activity half-width, and peak ZHR, hand-authored from the IMO Meteor Shower Calendar working list.
- `comets.v1.json`: authored comet apparitions. Real orbital period and first perihelion in world years, a window half-width, a brightness curve, and a track of right-ascension/declination keyframes indexed by signed phase against perihelion. The periods and the parent showers are real; the tracks and peak magnitudes are authored for the game. The loader rejects a comet that could never be seen rather than letting it fail silently, because on a body due once every thirteen years "never appears" and "not due yet" look identical.
- `textures/environment/milky-way.png`: the galaxy's unresolved glow, equirectangular in galactic coordinates, +90 deg on the first row and longitude running +180 to -180 left to right.
- `planets.v1.json`: the five naked-eye planets and the observer's own orbit. Six Keplerian elements and their per-century rates per body, hand-authored from JPL's *Approximate Positions of the Major Planets* Table 1 (valid 1800–2050), plus a magnitude zero point, a linear phase coefficient, and a tint.

Like the shower catalog, the planet table is hand-authored rather than generated: it is six rows of published constants, and a generator would add a build step without removing a source of error. The error it would not catch is a mistyped digit, so `PlanetCatalogAssetTests.Every_Orbit_Obeys_Kepler_Third_Law` checks each body's semi-major axis against its mean-motion rate — two numbers that are independent in the file and physically locked together — and `PlanetEphemerisTests` checks the resulting positions against real oppositions.

Earth sits under `observer` rather than in the `planets` array, because its orbit is subtracted from every other one rather than drawn.

Shower peaks are recorded as **solar longitude**, not as a day of the year, because `daysPerYear` is world configuration: 140 deg is late summer on a 12-day world and a 360-day world alike, while day 224 is not. `CelestialMath.GetSolarLongitudeDegrees` is the matching read.

Deep-sky textures are rendered as spherical quads. Each of the four registered right-ascension/declination corners is projected independently every frame, preserving the source image's sky position, rotation, and aspect ratio. `angularSizeDeg` remains descriptive metadata and is not used to reconstruct a square billboard.

The curated 30-object Stellarium expansion is reproducible from a Stellarium checkout:

```bash
python tools/deepsky/import_stellarium.py /path/to/stellarium/nebulae/default
```

The importer requires a canonical four-corner texture registration, derives the catalog center and descriptive angular size from the source footprint, and copies the exact registered PNG into the runtime texture directory.

When adding a new runtime data shape, version the filename and add asset tests for schema-level expectations.

## Seraph Poses

The stargaze clips are generated, not hand-written. `tools/build_stargaze_clips.py` states the pose
as intent — torso pitch, where the hip joint should end up, how far each joint bends — solves the
keyframe offsets against a port of the engine's own transform code, checks that nothing sinks into
the ground and no joint is pulled apart, and writes
`assets/astraterra/patches/seraph-stargaze.json`. Getting up is generated as the recline reversed on
a shorter clock, so the two cannot disagree about the pose they meet in. `SkyLyingAnimation` carries
the same keyframes for shapes the patch does not reach, and a test holds the two to each other.

```bash
make pose-build                      # regenerate the clips
make pose-preview CLIP=stargaze-down # draw a clip, frame by frame, and open it
```

`tools/preview_seraph_pose.py` draws a clip as a PNG contact sheet — side, front, and top, with
optional samples between keyframes and of the ease-in — and prints ground contact and joint gaps per
frame. `--blend` is the one that catches a clip which plays as a slide into its own first keyframe
rather than as a movement.

Two engine details govern all of this, and both have bitten this mod:

- **Author version 0.** Vintage Story composes a pose about the bone's rotation origin at animation
  version 0 and about the cube's own corner at version 1. On the seraph those are different points,
  so a version 1 pose swings limbs off their sockets. Every vanilla seraph clip is version 0, and a
  shape whose clips disagree logs `Shape … has mixed animation versions`.
  `tools/convert-animation-version.py` converts an imported version 1 export rather than relabelling
  it, but it preserves the pose as authored — including a pose that was already wrong.
- **Frame 0 is the pose you start from.** A clip is eased in by blending its first keyframe against
  the pose the entity is already in, so a clip that opens on its finished pose plays as a linear
  slide into it. Anything that should read as a movement needs the movement in its keyframes.
