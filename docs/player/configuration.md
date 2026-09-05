# AstraTerra Configuration Reference

AstraTerra keeps one settings file, `ModConfig/astraterra.json`, inside your Vintage Story data
folder. A dedicated server has its own copy.

The file is written for you the first time the mod loads and rewritten on every start after that, so
you never have to create it by hand and any key you leave out is filled in with its default. That
rewrite also means the file is the mod's to format: keys are read case-insensitively, but they are
written back in the capitalisation used below, and a key the mod does not recognise is dropped.

Most of these are easier to change with a `.stars` command, which applies the change immediately and
saves it to the same file. The keys with no command take effect only when the mod loads them, so
change those with the game closed.

## Sky

| Key | Values | Default | What it does |
| --- | --- | --- | --- |
| `StarfieldMode` | `astraterra`, `both`, `vanilla` | `astraterra` | Which star field draws. `both` overlays AstraTerra's catalog on Vintage Story's cubemap, which is how you check that the two agree about where a star is. |
| `SkyGridMode` | `none`, `horizontal`, `equatorial`, `both` | `none` | Draws coordinate lines over the sky: the cyan altitude-azimuth grid, the rose right-ascension/declination grid, or both. |
| `SolarSystemArt` | `pixel`, `photo` | `pixel` | Which pictures the planets and their moons are drawn from. Positions, sizes and which moons are out are the same either way; only the photographs draw a planet's phase as its own picture. |
| `MoonArt` | `pixel`, `photo`, `vanilla` | `pixel` | Which picture the moon overhead is drawn from. `vanilla` hands the disc back to Vintage Story. Position, phase, moonlight and the length of the night are the calendar's in every case. |
| `MilkyWayBrightness` | `0.0` to `2.0` | `1.0` | Scales the band's glow on top of the darkness and moonlight it already answers to. `0` switches the band off for a plain star field. |
| `StarBrightnessBias` | a multiplier, `1.0` is unchanged | `1.0` | Scales how brightly the whole star pass draws, before darkness is applied. Raising it brightens faint stars faster than bright ones, because the magnitude curve is already compressed at the bright end. |
| `GuideStarHighlightStrength` | a multiplier | `1.15` | How much brighter a guide star draws in the constellation overlay. Used as the larger of it and `StarBrightnessBias`, so lowering it below the bias does nothing. |

`.stars starfield`, `.stars sky-grid`, `.stars solar-system` and `.stars moon` set the first four
without a restart. `MilkyWayBrightness`, `StarBrightnessBias` and `GuideStarHighlightStrength` have
no command.

## The clock, and where you stand

| Key | Values | Default | What it does |
| --- | --- | --- | --- |
| `LongitudeAwareSun` | `true`, `false` | `true` | Whether world X acts as longitude. Left on, the sun, the daylight, the star field and every instrument shift east and west together, about one hour of sky per 8,300 blocks at the default `polarEquatorDistance`. Set `false` and the world keeps Vintage Story's single time zone, with the whole sky falling back to it. |
| `DisplayedClockTime` | `local`, `universal`, `zones` | `local` | Which hour the character panel and the instruments show. `local` is continuous local solar time, `universal` is the world's own clock, `zones` rounds to whole clock hours so a world day divides into even time zones. |
| `CalendarDisplay` | `full`, `clock`, `none` | `full` | How much of Vintage Story's own date and hour stays in the character panel. `clock` drops the date and keeps the hour; `none` drops both and the panel reads `unreckoned`, which is what makes the Sky Disc an instrument rather than decoration. |

`LongitudeAwareSun` is the server's decision and is sent to every client, so a client's own copy
cannot make its sun disagree with the server's daylight. Both it and `DisplayedClockTime` need a
restart. `CalendarDisplay` is per-client and `.stars calendar` sets it live; reopen the character
panel to see the change.

Longitude changes everything downstream of the sun — crops, temperature, mob spawning, other mods,
and other players on the same server keeping different hours from you. That is the feature, and it is
why there is a switch. See
[Longitude and the local hour](guide.md#longitude-and-the-local-hour).

## Testing

| Key | Values | Default | What it does |
| --- | --- | --- | --- |
| `DebugMeteorRateMultiplier` | `0.0` to `100.0` | `1.0` | Multiplies how often meteors spawn, so a shower does not have to be watched in real time. It scales the spawn rate only, never which shower a streak belongs to or when a shower is active. |

Out-of-range or non-numeric values for this and `MilkyWayBrightness` are clamped on load, with a
warning in the log saying what was used instead.

## Keys with no effect

Four keys are written into the file and read back, but nothing in the current build consumes them:

```json
"SelectionSnapRadiusDeg": 1.0,
"ShowMinimalHud": true,
"ShowReticle": true,
"DebugGuideStarEmphasisDefault": false
```

Setting them changes nothing. They are left in place rather than removed so that an existing file
does not start logging warnings, and they are noted here so nobody spends an evening working out why
`ShowReticle` will not turn the reticle off.

## A full file

This is what a default file looks like after the mod has written it once.

```json
{
  "StarfieldMode": "astraterra",
  "SkyGridMode": "none",
  "SolarSystemArt": "pixel",
  "MoonArt": "pixel",
  "CalendarDisplay": "full",
  "LongitudeAwareSun": true,
  "DisplayedClockTime": "local",
  "StarBrightnessBias": 1.0,
  "MilkyWayBrightness": 1.0,
  "GuideStarHighlightStrength": 1.15,
  "SelectionSnapRadiusDeg": 1.0,
  "ShowMinimalHud": true,
  "ShowReticle": true,
  "DebugGuideStarEmphasisDefault": false,
  "DebugMeteorRateMultiplier": 1.0
}
```

## Not in this file

`.stars render` switches one part of the sky off to measure what it costs. It is deliberately not
saved: everything comes back when you restart the client, because it is a measuring tool rather than
a setting. See the [Command Reference](commands.md#recovery-and-debug-commands).

The scale longitude and latitude are measured on is Vintage Story's, not AstraTerra's:
`polarEquatorDistance` in the world's own configuration, 50,000 blocks by default, sets both.
