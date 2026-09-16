namespace AstraTerra.Astronomy;

/// <summary>A quantized time and world-region identity for a cached near-body answer.</summary>
/// <remarks>
/// The three coordinates remain separate on purpose. The previous single <c>long</c> combined
/// them with unchecked arithmetic, so distinct regions could collide at the same time bucket.
/// </remarks>
public readonly record struct NearBodyIlluminationCacheKey(
    long TimeBucket,
    long XCell,
    long ZCell)
{
    public static NearBodyIlluminationCacheKey From(double totalDays, double x, double z)
        => new(
            (long)Math.Floor(totalDays / NearBodyLightController.CacheQuantumDays),
            (long)Math.Floor(x / NearBodyLightController.CacheQuantumBlocks),
            (long)Math.Floor(z / NearBodyLightController.CacheQuantumBlocks));
}

/// <summary>Optional counters for a bounded near-body illumination cache.</summary>
public readonly record struct NearBodyIlluminationCacheMetrics(
    long Hits,
    long Misses,
    long ComputeCalls,
    long ComputeTicks)
{
    public TimeSpan ComputeTime
        => TimeSpan.FromSeconds(ComputeTicks / (double)System.Diagnostics.Stopwatch.Frequency);
}

/// <summary>
/// A small, generation-aware LRU cache for computed near-body illumination.
/// </summary>
/// <remarks>
/// <para>
/// The capacity is deliberately fixed: a player travelling across a large world cannot turn
/// explored terrain into an ever-growing dictionary. Entries are evicted least-recently-used,
/// with an insertion sequence as the deterministic tie-breaker. Concurrent misses may duplicate
/// computation; they never publish into a generation that has been invalidated.
/// </para>
/// <para>
/// Measurement is opt-in. Production hits and misses take the same cache lock but do not perform
/// stopwatch reads or atomic counter updates unless <see cref="EnableMetrics"/> is enabled.
/// </para>
/// </remarks>
public sealed class NearBodyIlluminationCache
{
    public const int DefaultCapacity = 64;

    private readonly object gate = new();
    private readonly Dictionary<NearBodyIlluminationCacheKey, Entry> entries;
    private readonly int capacity;
    private long generation;
    private long useSequence;
    private bool metricsEnabled;
    private NearBodyIlluminationCacheMetrics metrics;

    public NearBodyIlluminationCache(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Cache capacity must be positive.");
        }

        this.capacity = capacity;
        entries = new Dictionary<NearBodyIlluminationCacheKey, Entry>(capacity);
    }

    public int Capacity => capacity;

    public int Count
    {
        get
        {
            lock (gate)
            {
                return entries.Count;
            }
        }
    }

    public bool MetricsEnabled
    {
        get
        {
            lock (gate)
            {
                return metricsEnabled;
            }
        }
    }

    /// <summary>Turns diagnostics on or off and clears the current counters.</summary>
    public void EnableMetrics(bool enabled = true)
    {
        lock (gate)
        {
            metricsEnabled = enabled;
            metrics = default;
        }
    }

    public NearBodyIlluminationCacheMetrics ReadMetrics()
    {
        lock (gate)
        {
            return metrics;
        }
    }

    /// <summary>Returns the current generation for a miss to capture before computing.</summary>
    public long CaptureGeneration()
    {
        lock (gate)
        {
            return generation;
        }
    }

    public bool TryGet(NearBodyIlluminationCacheKey key, out NearBodyIllumination value)
    {
        lock (gate)
        {
            if (entries.TryGetValue(key, out var entry))
            {
                entries[key] = entry with { LastUsed = ++useSequence };
                if (metricsEnabled)
                {
                    metrics = metrics with { Hits = metrics.Hits + 1 };
                }

                value = entry.Value;
                return true;
            }

            if (metricsEnabled)
            {
                metrics = metrics with { Misses = metrics.Misses + 1 };
            }

            value = default;
            return false;
        }
    }

    /// <summary>
    /// Gets an answer or computes and publishes one for <paramref name="key"/>.
    /// </summary>
    /// <remarks>
    /// The callback must represent the captured state that produced <paramref name="key"/>. The
    /// near-body controller uses the lower-level methods because it must re-snapshot controller
    /// state after an invalidation; this convenience method is useful for pure callers and the
    /// repeatable benchmark.
    /// </remarks>
    public NearBodyIllumination GetOrCompute(
        NearBodyIlluminationCacheKey key,
        Func<NearBodyIllumination> compute)
    {
        ArgumentNullException.ThrowIfNull(compute);
        while (true)
        {
            if (TryGet(key, out var cached))
            {
                return cached;
            }

            var generation = CaptureGeneration();
            var started = MetricsEnabled
                ? System.Diagnostics.Stopwatch.GetTimestamp()
                : 0L;
            var value = compute();
            if (started != 0L)
            {
                RecordCompute(System.Diagnostics.Stopwatch.GetTimestamp() - started);
            }

            if (TryPublish(key, value, generation))
            {
                return value;
            }
        }
    }

    /// <summary>
    /// Publishes a computed value only when the miss belongs to the current generation.
    /// </summary>
    public bool TryPublish(
        NearBodyIlluminationCacheKey key,
        NearBodyIllumination value,
        long expectedGeneration)
    {
        lock (gate)
        {
            if (expectedGeneration != generation)
            {
                return false;
            }

            entries[key] = new Entry(value, ++useSequence);
            if (entries.Count > capacity)
            {
                EvictLeastRecentlyUsed();
            }

            return true;
        }
    }

    /// <summary>Records one actual expensive computation when opt-in metrics are enabled.</summary>
    public void RecordCompute(long elapsedTicks)
    {
        lock (gate)
        {
            if (!metricsEnabled)
            {
                return;
            }

            metrics = metrics with
            {
                ComputeCalls = metrics.ComputeCalls + 1,
                ComputeTicks = metrics.ComputeTicks + Math.Max(0, elapsedTicks)
            };
        }
    }

    /// <summary>
    /// Clears all retained answers and prevents an in-flight old computation from publishing.
    /// </summary>
    public void Invalidate()
    {
        lock (gate)
        {
            entries.Clear();
            generation++;
        }
    }

    private void EvictLeastRecentlyUsed()
    {
        var found = false;
        var leastRecentlyUsedKey = default(NearBodyIlluminationCacheKey);
        var leastRecentlyUsed = long.MaxValue;
        foreach (var pair in entries)
        {
            if (pair.Value.LastUsed < leastRecentlyUsed)
            {
                found = true;
                leastRecentlyUsedKey = pair.Key;
                leastRecentlyUsed = pair.Value.LastUsed;
            }
        }

        if (found)
        {
            entries.Remove(leastRecentlyUsedKey);
        }
    }

    private readonly record struct Entry(NearBodyIllumination Value, long LastUsed);
}
