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
- Watch the sky for several in-game hours and confirm stars rise in the east and set in the west, in the same direction the vanilla sun travels.
- Set `.stars sky-grid none`, hold right click with the Sextant, and confirm the on-screen angle above horizon updates for a centered visible star with no grid initially. Middle click repeatedly and confirm the display cycles through rose equatorial, cyan azimuthal, both, and angle-only. Release right click and confirm the saved no-grid setting is restored.
- In daylight, hold right click with the Sextant and sight the sun; expected result: the readout names `Sun` and shows its angle above the horizon, with no star reading offered.
- With a daytime moon above the horizon, sight it and confirm the readout names `Moon` and reports a plausible angle.
- Sight the sun near sunrise or sunset and confirm the reported angle approaches zero as it meets the horizon.
- Point away from every body and confirm the readout lists what is available to aim at rather than reporting stars are not visible.
- With the star catalog asset missing or failing to load, confirm the Sextant still sights the sun and moon and offers only those two.
- Hold the Astrolabe steady for a minute of world time and confirm the clock line advances without the frame rate dropping; the sky clock is cached per world minute rather than recomputed each frame.
- Hold the written constellation book in the left hand and the Calibrated Astrolabe in the main hand.
- Hold right click and confirm the astrolabe shows latitude, world day, compass direction, altitude, motion state, and time until transit for a recorded constellation.
- Confirm the astrolabe's clock line shows the hour, the phase of the day, the sun's angle, and the time to the next sunrise or sunset, and that the hour agrees with the game's own clock.
- Scroll the forecast forward and confirm the clock line advances with it, and that the phase changes from day to dusk to night at the appropriate hours.
- Hold the astrolabe with no book and confirm the clock line still shows.
- Middle click and confirm the astrolabe cycles through only the constellations in the held book.
- Scroll and confirm the forecast moves by one hour; sneak-scroll and confirm it moves by seven days, never earlier than now or later than one world year.
- Repeat the astrolabe check indoors or during daylight and confirm planning remains available.
- At a high latitude, confirm the astrolabe distinguishes a circumpolar constellation from one that never rises.
- At an extreme latitude during a polar day or night, confirm the astrolabe reports that the sun does not set or does not rise instead of showing an hour.

## Creative Inventory

- Open the creative inventory and select the **AstraTerra** tab.
- Confirm Brass Telescope, Precision Telescope, Sextant, and Calibrated Astrolabe are present and can be dragged into the player inventory.
- Confirm **Star Catalog** and **The Zodiac** appear on the same tab and can be dragged without using `/stars give-catalog` or `/stars give-zodiac`.

## Authored Constellation Build

- As an administrator with the `give` privilege, run `/stars give-catalog`; expected result: a written book titled `Star Catalog` is added to the inventory with 88 named constellation entries.
- Put `Star Catalog` in the left hand, run `.stars list`, and confirm all 88 authored constellation names are present. Confirm the sky overlay and Calibrated Astrolabe can use the catalog without ink and quill.
- Run `/stars give-zodiac`; expected result: a written book titled `The Zodiac` is added to the inventory with 12 entries ordered Aries, Taurus, Gemini, Cancer, Leo, Virgo, Libra, Scorpius, Sagittarius, Capricornus, Aquarius, and Pisces.
- Put `The Zodiac` in the left hand and confirm `.stars list`, the sky overlay, and the Calibrated Astrolabe use only those 12 constellations. Confirm Ophiuchus is not included.
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

## Multiplayer

Observation state (telescope scope, sextant reading, astrolabe planner) lives in process-wide
statics that drive the local HUD, so it must never be set by another player's interaction. The
hosted-world case is the one that catches regressions, because a hosted world runs its server inside
the host's own client process — a dedicated server hides the bug, since nothing in that process draws.

- Host a world from the game client ("open to LAN" / uPnP) and have a second player join.
- As the joining player, hold right click with a Brass Telescope; expected result: **the host sees no scope overlay and no field-of-view change.** Repeat with the host scoping and the guest observing.
- Repeat both directions with the Sextant; expected result: the other player gets no angle readout.
- Repeat both directions with the Calibrated Astrolabe; expected result: the other player gets no planning readout.
- With both players scoped at once, confirm each sees only their own zoom level and mode, and that scrolling zoom on one does not move the other.
- Repeat the telescope check on a dedicated server with the two players standing next to each other, to confirm a nearby player's interaction does not open the local overlay.

## Result Log

- Equator: pass in manual in-game check; starfield and authored constellation orientation looked correct.
- Starfield comparison: use `.stars starfield both` to inspect alignment, then verify `vanilla` hides AstraTerra stars and `astraterra` hides the vanilla cubemap without restarting.
- Star size comparison: with `.stars starfield both`, confirm typical AstraTerra stars are similarly compact to the vanilla points and that only the brightest catalog stars have a modest outer glow rather than a dominant disc.
- Polaris comparison: with `.stars starfield both`, compare Polaris to a similarly bright vanilla point; confirm its core is no longer undersized while magnitude-six stars remain compact.
- Deep-sky registration: scope a Stellarium-backed object such as M42 and confirm catalog stars embedded in the image coincide with AstraTerra stars across the full image, including its rotation and non-square footprint.
- Coordinate grids: use `.stars sky-grid horizontal` and confirm the cyan horizon stays at altitude 0° while its cardinal meridians meet at the zenith. Use `equatorial` and confirm the rose celestial equator and hour circles rotate with time while remaining aligned with catalog stars. Use `both` to compare the frames, then restore `none`.
- Mid-latitude north: not run in this automated pass.
- Mid-latitude south: not run in this automated pass.
- Near-polar: not run in this automated pass.
