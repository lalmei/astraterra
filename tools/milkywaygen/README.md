# AstraTerra Milky Way Generator

Developer-only tooling for producing `assets/astraterra/textures/environment/milky-way.png`: the
unresolved glow the catalog stars are drawn against.

```bash
cd tools/milkywaygen
python -m milkywaygen.main --help
python -m milkywaygen.main            # rewrites the committed texture with the committed defaults
python -m unittest tests.test_galaxy
```

Requires `numpy` and `pillow`. A full-size run marches 320 steps along every one of two million lines
of sight in three colours, which takes a couple of minutes; `--width 512 --steps 160` is enough to
judge a parameter change in seconds.

## What it draws

Nothing here is a photograph or a repaint of one, which is why the committed texture carries no
third-party licence. The map is a galaxy model integrated along each line of sight from the Sun's
place inside it:

- an exponential **disc**, with four logarithmic **spiral arms** riding on it — the arms are what
  give the band bright knots instead of a smooth gradient from Sagittarius to the anticentre;
- a flattened, redder **bulge** at the centre;
- a longer, much flatter layer of **dust** in front of all of it, integrated *as the ray advances*,
  which is what produces the Great Rift rather than a smudge over the middle;
- a band-limited, seamless **clumping field** modulating the dust, so the lanes are cloudy at every
  scale and the map still wraps at galactic longitude 0.

Colour comes from two per-channel numbers in `main.py`: dust extinguishes blue harder than red
(reddening), and the bulge's old stars are intrinsically redder than the disc's.

## The output convention

Equirectangular, in **galactic** coordinates, width twice height:

- the first row is galactic latitude +90 deg, the last is -90 deg;
- the left edge is galactic longitude +180 deg, the centre is 0 deg (the galactic centre, where it
  can be judged), the right edge is -180 deg.

`MilkyWayRenderModel` in the mod wraps this over a sphere and rotates it into equatorial coordinates,
so the convention here and `GalacticFrame` there have to agree. If the band ends up mirrored or
half a turn out, one of those two is what changed.

## Tuning

Every knob is a keyword on `GalaxyModel` or a flag on `main.py`. The ones worth reaching for first:

| Want | Change |
| --- | --- |
| A darker, more dramatic rift | `--clumping` up, or `dust_opacity` up |
| A wider, softer band | `disc_scale_height_kpc` up, or `--shoulder` down |
| Less glare at the galactic centre | `bulge_strength` down |
| A blacker sky away from the band | `--gamma` down towards 1 |
| Different clouds, same galaxy | `--seed` |

The seed is part of the committed defaults, so rerunning with no arguments reproduces the committed
texture rather than a new sky.
