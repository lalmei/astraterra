# AstraTerra Manual Verification

## Setup

- Build and deploy release output: `make deploy`.
- Load AstraTerra in Vintage Story 1.22.2 with the mod enabled.
- Create or open a test world.
- Spawn or craft the Brass Telescope, Precision Telescope, Sextant, and Calibrated Astrolabe.
- Set time/weather as needed so the sky is dark and clear.
- For daytime sky checks, run `.stars daylight-stars on`; turn it back off with `.stars daylight-stars off` before normal play validation.

## Core Smoke Test

- Confirm telescope and sextant items use visible non-placeholder models/textures.
- Hold right click with the Brass Telescope and confirm normal item interaction is suppressed while scoped.
- Confirm the fixed-star sky is visible at night and does not crash with the generated catalog.
- Put a blank normal book in the left hand and keep ink and quill in inventory.
- Use `.stars list` before drawing; expected result: no saved constellations for the held book.
- Scope with the Brass Telescope, switch to Draw mode, and drag from one guide star to another; expected result: a constellation segment is written into the held book and the naming dialog opens.
- Build a known authored sky-culture constellation with `.stars build Ori`.
- Run `.stars list` and `.stars info selected`.
- Confirm `.stars info` includes ID, star count, segment count, month window, season summary, and state.
- If the constellation state is not `below horizon`, look toward its stars while holding the written book in the left hand and confirm saved segments render in the sky.
- Rename and delete the constellation with `.stars name <id> <text>` and `.stars delete <id>`.
- Hold right click with the Sextant, center a visible star, and confirm the on-screen angle above horizon updates.
- Hold the written constellation book in the left hand and the Calibrated Astrolabe in the main hand.
- Hold right click and confirm the astrolabe shows latitude, world day, compass direction, altitude, motion state, and time until transit for a recorded constellation.
- Middle click and confirm the astrolabe cycles through only the constellations in the held book.
- Scroll and confirm the forecast moves by one hour; sneak-scroll and confirm it moves by seven days, never earlier than now or later than one world year.
- Repeat the astrolabe check indoors or during daylight and confirm planning remains available.
- At a high latitude, confirm the astrolabe distinguishes a circumpolar constellation from one that never rises.

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
- Starfield comparison: use `.stars starfield both` to inspect alignment, then verify `vanilla` hides AstraTerra stars and `astraterra` hides the vanilla cubemap without restarting.
- Coordinate grids: use `.stars sky-grid horizontal` and confirm the cyan horizon stays at altitude 0° while its cardinal meridians meet at the zenith. Use `equatorial` and confirm the rose celestial equator and hour circles rotate with time while remaining aligned with catalog stars. Use `both` to compare the frames, then restore `none`.
- Mid-latitude north: not run in this automated pass.
- Mid-latitude south: not run in this automated pass.
- Near-polar: not run in this automated pass.
