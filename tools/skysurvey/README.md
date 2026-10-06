# AstraTerra Sky Survey Converter

Developer-only tooling that turns NASA's *Deep Star Maps 2020* into the Milky Way textures the mod
draws: the naked-eye `milky-way.jpg` and, optionally, the high-resolution tiles the telescope loads
(#219).

```bash
cd tools/skysurvey
python -m skysurvey.main --source sources/milkyway_2020_16k_gal.exr \
    --tiles-dir out/milky-way-tiles --report out/report.json
python -m unittest tests.test_survey tests.test_main
```

Requires `numpy`, `pillow` and `OpenEXR`. A 16k run holds the whole source in memory (about 1.6 GB
as float32) and takes about 40 seconds.

## Source

[NASA SVS Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851), public domain. Credit: NASA/Goddard
Space Flight Center Scientific Visualization Studio. Gaia DR2: ESA/Gaia/DPAC.

Use the `milkyway_2020_<N>k_gal.exr` files: galactic coordinates, with the Hipparcos and Tycho stars
already taken out, so the catalog's own stars are not drawn a second time on top of the glow. Put
them in `sources/`, which is gitignored. Only the converted outputs are committed.

## What it does

1. **Checks orientation** before writing anything. The galactic centre has to be brighter than
   the anticentre, and the LMC has to sit below the plane on the correct side. A mirrored or
   upside-down source is refused rather than shipped. The convention is milkywaygen's: first row
   b = +90 deg, left edge l = +180 deg, centre l = 0 deg.
2. **Downsamples in linear light** to the naked-eye width (4096 by default), before any stretch.
   Averaging after the stretch would make the base map darker than the tiles drawn over it.
3. **Fits one stretch**: asinh on intensity (Lupton et al. 2004), then a gamma, so the result
   matches the *brightness* of the procedural map it replaces. Those numbers are recorded in
   `survey.REFERENCE_STATS`, and `MilkyWayVisibility` was tuned against them. The run warns if the
   fit misses by more than 10%, measured on the full output map.
   - The fit samples every n-th pixel rather than a box-filtered copy. The stretch is nonlinear,
     and a fit to averaged grain missed the real map by a quarter at high latitude.
   - Colour is Gaia's, not fitted. `--saturation` (default 0.5) pulls it towards grey without
     changing brightness. Linear-light colour ratios shown as encoded values come out
     oversaturated: a salmon bulge and magenta Magellanic Clouds.
4. **Cuts tiles** (`--tiles-dir`) from the full-resolution source: 512 px squares with a one-texel
   gutter so filtering blends across tile seams, covering |b| <= 33.75 deg. That is 192 tiles at
   16k, each 11.25 deg across. Every tile uses the global stretch so the scope shows no grid. A
   `tiles.json` manifest records each tile's galactic bounds.
5. **Writes JPEG** at full chroma resolution (4:4:4), because a resolved star is one texel and
   chroma subsampling would smear its colour. Optimised Huffman tables stay off: Pillow 12.3 with
   libjpeg-turbo 3 fails on 4:4:4 + optimise when writing to memory.
6. **Reports sizes** against the 15 MiB budget that decides whether tiles ship in the main mod or
   as an optional asset mod.

## Measured on `milkyway_2020_16k_gal.exr` (2026-10-05)

| Output | Size |
| --- | --- |
| 4k global map, PNG | 16.2 MiB, which is why the shipped map is a JPEG |
| 4k global map, JPEG q90 4:4:4 (shipped) | 3.3 MiB |
| 192 tiles, JPEG q85 4:4:4 | 27.7 MiB, over budget: optional asset mod |
| 192 tiles, PNG | 128 MiB |

Fitted stretch: black 0, softening 0.352, gamma 0.70, saturation 0.5. Worst brightness miss 3.3%.
Orientation margins: centre 6.6x the anticentre, and the LMC 7x its upside-down position and 6x its
mirrored one.
