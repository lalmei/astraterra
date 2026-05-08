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

Use `.stars list` to see saved constellations. Use `.stars info selected` after drawing or selecting a constellation to see its star count, segment count, best visibility window, season summary, and current state.

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

The current authored set includes all 88 Modern IAU constellations. Built constellations are saved into the same local journal as hand-drawn constellations.

## Recovery And Debug Commands

```text
.stars connect <hipA> <hipB>
.stars debug
.stars goto-lat <degrees>
.stars daylight-stars on
.stars daylight-stars off
```

`connect` is a recovery path for creating a segment from known HIP star IDs. `debug` shows latitude and sky-orientation diagnostics. `goto-lat` helps test different sky latitudes in the current world. `daylight-stars` is intended for testing and should be turned off for normal play.
