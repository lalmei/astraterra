# AstraTerra Sky Survey Converter

Developer-only tooling that turns NASA's *Deep Star Maps 2020* into the Milky Way textures the mod
draws: the naked-eye `milky-way.png` and, optionally, the high-resolution tiles the telescope loads
(#219).

```bash
cd tools/skysurvey
python -m skysurvey.main --source sources/milkyway_2020_16k_gal.exr \
    --tiles-dir out/milky-way-tiles --report out/report.json
python -m unittest tests.test_survey tests.test_main
```

Requires `numpy`, `pillow` and `OpenEXR`. A 16k run holds the whole source in memory (about 1.6 GB
as float32) and takes a few minutes, most of it writing tiles.

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
3. **Fits one asinh stretch** (Lupton et al. 2004, applied to intensity so each pixel keeps its
   colour) so the result matches the statistics of the procedural map it replaces. Those are
   recorded in `survey.REFERENCE_STATS`, and `MilkyWayVisibility` was tuned against them. The run
   warns if the fit misses them by more than 10%.
4. **Cuts tiles** (`--tiles-dir`) from the full-resolution source: 512 px squares with a one-texel
   gutter so filtering blends across tile seams, covering |b| <= 33.75 deg. That is 192 tiles at
   16k, each 11.25 deg across. Every tile uses the global stretch so the scope shows no grid. A
   `tiles.json` manifest records each tile's galactic bounds.
5. **Reports sizes.** It prints the JPEG total (and what PNG would cost) against the 15 MiB budget
   that decides whether the tiles ship in the main mod or as an optional asset mod.
