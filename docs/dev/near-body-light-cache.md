# Near-body illumination cache

`NearBodyLightController` keeps up to 64 computed `NearBodyIllumination` values at once. A key
contains the quantized time bucket, X cell, and Z cell as separate structural fields. The existing
quantization remains `0.001` world days and `128` blocks; the cache does not move sampling to cell
centres or change the astronomy formulas.

The cache evicts the least-recently-used entry when full. Bind, source, enable, reset, and explicit
solar-policy/tilt invalidation clear the entries and advance a generation. A calculation runs
outside the controller lock and can publish only into the generation it captured, so an old world
or source cannot repopulate a cleared cache. Concurrent duplicate misses are allowed and remain
bounded.

Cache metrics are opt-in through `NearBodyLightController.Cache.EnableMetrics()`. They report hits,
misses, actual compute calls, and elapsed compute ticks without stopwatch or counter work on normal
production hits.

## Repeatable release workload

Run the release benchmark from the repository root:

```sh
make bench
```

It alternates 32 retained regions for 100,000 queries and prints compute calls, bytes allocated,
and elapsed time for a one-entry cache and the 64-entry cache, including cache construction.
Both consume the same illumination values and verify matching checksums. This synthetic workload
calls the real `IlluminationFor` arithmetic with a fixed sun direction; it excludes game calendar
calls, their vector allocations, and the controller lock. Its allocation numbers describe this
benchmark only. The output is evidence
for repeated illumination work, not an FPS claim; render-thread validation still requires a fresh
game restart and a controlled in-game route.

CI runs the same target in a `benchmark` job of its own, on a GitHub-hosted Linux runner beside
the build rather than inside `make test`. The references are managed DLLs and the workload is
arithmetic, so nothing here needs the self-hosted Mac, and a regression shows as its own failure
instead of one more red unit test.

That job fails on counts, never on the clock: the one-entry path must miss on every query, the
bounded cache must compute once per retained region, and the two must agree on the checksum.
Elapsed time and allocations are printed to be read, not asserted -- on a shared runner they
measure the neighbours as much as the cache.

## Local benchmark result

On this checkout in Release mode, 100,000 queries alternating over 32 regions produced matching
illumination checksums:

| Cache | Computations | Allocated bytes | Elapsed |
| --- | ---: | ---: | ---: |
| One entry | 100,000 | 24 | 78.016 ms |
| 64 entries | 32 | 4,504 | 35.660 ms |

These are single-run local measurements, including allocation of the bounded cache. Repeat the
command for comparisons on another machine. No in-game frame-time or FPS validation was performed.
