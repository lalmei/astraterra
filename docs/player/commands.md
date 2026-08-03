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

Use `.stars list` to see constellations in the book held in your left hand. Use `.stars info selected` after drawing or selecting a constellation to see its star count, segment count, best visibility window, season summary, and current state. Mutating commands require ink and quill in your inventory.

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

## Recovery And Debug Commands

```text
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
```

`connect` is a recovery path for creating a segment from known HIP star IDs. `debug` shows latitude and sky-orientation diagnostics. `goto-lat` helps test different sky latitudes in the current world. `daylight-stars` is intended for testing and should be turned off for normal play.

`starfield` changes the active sky immediately and saves the choice in `ModConfig/astraterra.json`:

- `astraterra` shows only AstraTerra's catalog-driven stars and is the default.
- `both` shows AstraTerra and the original Vintage Story cubemap together for alignment comparisons.
- `vanilla` shows only the original Vintage Story starfield.

`sky-grid` projects debug coordinate lines over the visible sky and saves the choice in `ModConfig/astraterra.json`. `horizontal` draws the observer-local altitude-azimuth grid in cyan. `equatorial` draws the right-ascension/declination grid in rose and rotates it with local sidereal time. `both` overlays the two systems; `none` is the default. While using the Sextant, middle click cycles its temporary display through angle only, equatorial, azimuthal, and both without changing the saved mode.
