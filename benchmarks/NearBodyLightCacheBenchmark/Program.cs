using AstraTerra.Astronomy;

const int regionCount = 32;
const int repetitions = 100_000;
var keys = Enumerable.Range(0, regionCount)
    .Select(index => NearBodyIlluminationCacheKey.From(
        totalDays: 100.25,
        x: index * NearBodyLightController.CacheQuantumBlocks,
        z: (index % 4) * NearBodyLightController.CacheQuantumBlocks))
    .ToArray();
var giant = new NearBodyLightSource(19.5, 30.0, 0.0, 0.52);
var sun = new SkyDirection(0.0, -1.0, 0.0);

// Warm the JIT and the new cache's dictionary before measuring the repeated workload.
RunOld(keys, giant, sun, repetitions: 1_000);
RunNew(keys, giant, sun, repetitions: 1_000);
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var old = Measure(() => RunOld(keys, giant, sun, repetitions));
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
var multiRegion = Measure(() => RunNew(keys, giant, sun, repetitions));

if (old.Checksum != multiRegion.Checksum)
{
    throw new InvalidOperationException("Cache workloads produced different illumination checksums.");
}

Console.WriteLine($"workload=alternating-{regionCount}-regions repetitions={repetitions:N0}");
Console.WriteLine("implementation,compute_calls,allocated_bytes,elapsed_ms,checksum");
Console.WriteLine($"one-entry,{old.ComputeCalls},{old.AllocatedBytes},{old.Elapsed.TotalMilliseconds:F3},{old.Checksum:R}");
Console.WriteLine($"multi-region-64,{multiRegion.ComputeCalls},{multiRegion.AllocatedBytes},{multiRegion.Elapsed.TotalMilliseconds:F3},{multiRegion.Checksum:R}");

static (int Computes, double Checksum) RunOld(
    NearBodyIlluminationCacheKey[] keys,
    NearBodyLightSource giant,
    SkyDirection sun,
    int repetitions)
{
    var last = default(NearBodyIlluminationCacheKey);
    var valid = false;
    var value = NearBodyIllumination.None;
    var gate = new object();
    var computes = 0;
    var checksum = 0.0;
    for (var iteration = 0; iteration < repetitions; iteration++)
    {
        var key = keys[iteration % keys.Length];
        lock (gate)
        {
            if (!valid || key != last)
            {
                value = Compute(key, giant, sun);
                computes++;
                last = key;
                valid = true;
            }

            checksum += value.PlanetshineStrength + value.SolarObscuration;
        }
    }

    return (computes, checksum);
}

static (int Computes, double Checksum) RunNew(
    NearBodyIlluminationCacheKey[] keys,
    NearBodyLightSource giant,
    SkyDirection sun,
    int repetitions)
{
    var cache = new NearBodyIlluminationCache();
    var computes = 0;
    var checksum = 0.0;
    for (var iteration = 0; iteration < repetitions; iteration++)
    {
        var key = keys[iteration % keys.Length];
        if (cache.TryGet(key, out var cached))
        {
            checksum += cached.PlanetshineStrength + cached.SolarObscuration;
            continue;
        }

        var generation = cache.CaptureGeneration();
        var value = Compute(key, giant, sun);
        computes++;
        cache.TryPublish(key, value, generation);
        checksum += value.PlanetshineStrength + value.SolarObscuration;
    }

    return (computes, checksum);
}

static NearBodyIllumination Compute(
    NearBodyIlluminationCacheKey key,
    NearBodyLightSource giant,
    SkyDirection sun)
    => NearBodyLightController.IlluminationFor(
        giant,
        latitudeDeg: key.ZCell * 0.25,
        longitudeDeg: key.XCell * 0.25,
        totalDays: key.TimeBucket * NearBodyLightController.CacheQuantumDays,
        daysPerYear: 360,
        hoursPerDay: 24.0,
        sunDirection: sun);

static Measurement Measure(Func<(int Computes, double Checksum)> workload)
{
    var before = GC.GetAllocatedBytesForCurrentThread();
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    var result = workload();
    var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(started);
    return new Measurement(
        result.Computes,
        GC.GetAllocatedBytesForCurrentThread() - before,
        elapsed,
        result.Checksum);
}

readonly record struct Measurement(int ComputeCalls, long AllocatedBytes, TimeSpan Elapsed, double Checksum);
