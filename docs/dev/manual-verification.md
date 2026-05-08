# AstraTerra Manual Verification

## Setup

- Build and deploy release output: `make deploy`.
- Load AstraTerra in Vintage Story 1.22.2 with the mod enabled.
- Create or open a test world.
- Spawn or craft the Brass Telescope, Precision Telescope, and Sextant.
- Set time/weather as needed so the sky is dark and clear.
- For daytime sky checks, run `.stars daylight-stars on`; turn it back off with `.stars daylight-stars off` before normal play validation.

## Core Smoke Test

- Confirm telescope and sextant items use visible non-placeholder models/textures.
- Hold right click with the Brass Telescope and confirm normal item interaction is suppressed while scoped.
- Confirm the fixed-star sky is visible at night and does not crash with the generated catalog.
- Use `.stars list` before drawing; expected result: no saved constellations.
- Scope with the Brass Telescope, switch to Draw mode, and drag from one guide star to another; expected result: a pale blue constellation segment is created and saved.
- Build a known authored sky-culture constellation with `.stars build Ori`.
- Run `.stars list` and `.stars info selected`.
- Confirm `.stars info` includes ID, star count, segment count, month window, season summary, and state.
- If the constellation state is not `below horizon`, look toward its stars and confirm saved segments render as pale blue overlay lines with brighter endpoint dots.
- Rename and delete the constellation with `.stars name <id> <text>` and `.stars delete <id>`.
- Hold right click with the Sextant, center a visible star, and confirm the on-screen angle above horizon updates.

## Authored Constellation Build

- Run `.stars build Ori`; expected result: a selected constellation named `Orion`.
- Run `.stars build UMa`; expected result: a selected constellation named `Ursa Major`.
- Run `.stars build UMi`; expected result: a selected constellation named `Ursa Minor`.
- Run `.stars build Vir`, `.stars build Sgr`, or another zodiac IAU code; expected result: a selected named constellation using authored sky-culture segments.
- Run `.stars build Missing`; expected result: command reports that the authored constellation was not found and does not mutate the journal.
- Confirm visible authored constellation segments fade out near the visual horizon and do not end in a hard line above terrain.

## Latitude Scenarios

- Use `.stars debug` after each teleport to confirm the mapped latitude.
- If the latitude target is under 24-hour daylight, use `.stars daylight-stars on`.
- Vintage Story controls latitude from world climate settings. Do not assume fixed `/tp` coordinates are equator or pole in every save.
- Use `.stars goto-lat <degrees>` to move to the nearest map position matching the requested latitude.
- Equator target: debug latitude near `lat=0`.
- Mid-latitude north target: debug latitude near `lat=45`.
- Near-polar north target: debug latitude near `lat=80` to `lat=90`.
- Mid-latitude south target: debug latitude near `lat=-45`.
- Equator: confirm both hemispheres become available as the player crosses latitude zero.
- Mid-latitude north: confirm circumpolar behavior and expected seasonal drift.
- Mid-latitude south: confirm southern sky inversion and season labels.
- Near-polar: confirm low-elevation dimming and below-horizon classification.

## Persistence

- Save and reload after selecting a constellation and changing telescope mode/zoom.
- Confirm the constellation journal persists.
- Confirm selected constellation, last mode, and zoom are restored from the per-world client state file.

## Result Log

- Equator: pass in manual in-game check; starfield and authored constellation orientation looked correct.
- Mid-latitude north: not run in this automated pass.
- Mid-latitude south: not run in this automated pass.
- Near-polar: not run in this automated pass.
