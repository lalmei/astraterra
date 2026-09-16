using AstraTerra.Astronomy;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class NearBodyIlluminationCacheTests
{
    [Fact]
    public void Same_Cell_And_Serial_Alternation_Compute_Once_Per_Retained_Key()
    {
        var cache = new NearBodyIlluminationCache(capacity: 2);
        var computes = 0;
        var a = Key(totalDays: 4.0, x: 0.0, z: 0.0);
        var b = Key(totalDays: 4.0, x: 128.0, z: 0.0);

        NearBodyIllumination Lookup(NearBodyIlluminationCacheKey key)
            => cache.GetOrCompute(key, () =>
            {
                computes++;
                return new NearBodyIllumination(key.XCell / 10.0, key.ZCell / 10.0);
            });

        var firstA = Lookup(a);
        Assert.Equal(firstA, Lookup(a));
        Lookup(b);
        Lookup(a);
        Lookup(b);
        Lookup(a);

        Assert.Equal(2, computes);
        Assert.Equal(2, cache.Count);
        Assert.Equal(0, cache.ReadMetrics().Hits); // metrics are opt-in
    }

    [Fact]
    public void Structural_Key_Preserves_Negatives_Boundaries_And_Old_Hash_Collision()
    {
        var zero = Key(totalDays: 7.0, x: 0.0, z: 0.0);
        var negative = Key(totalDays: 7.0, x: -0.001, z: -0.001);
        var boundaryBefore = Key(totalDays: 7.0, x: 127.999, z: -128.0);
        var boundaryAfter = Key(totalDays: 7.0, x: 128.0, z: -127.999);
        var oldCollision = Key(totalDays: 7.0, x: 128.0, z: -31.0 * 128.0);

        Assert.Equal((-1L, -1L), (negative.XCell, negative.ZCell));
        Assert.Equal((0L, -1L), (boundaryBefore.XCell, boundaryBefore.ZCell));
        Assert.Equal((1L, -1L), (boundaryAfter.XCell, boundaryAfter.ZCell));
        Assert.NotEqual(zero, oldCollision);
        Assert.NotEqual(boundaryBefore, boundaryAfter);
    }

    [Fact]
    public void Time_Bucket_Advance_And_Backward_Jump_Do_Not_Reuse_A_Different_Bucket()
    {
        var cache = new NearBodyIlluminationCache();
        var computes = 0;
        var values = new Dictionary<NearBodyIlluminationCacheKey, NearBodyIllumination>();

        NearBodyIllumination Lookup(double totalDays)
        {
            var key = Key(totalDays, 12.0, 24.0);
            return cache.GetOrCompute(key, () =>
            {
                computes++;
                var value = new NearBodyIllumination(key.TimeBucket, 0.0);
                values[key] = value;
                return value;
            });
        }

        var first = Lookup(10.0);
        var later = Lookup(10.0 + (1.5 * NearBodyLightController.CacheQuantumDays));
        var back = Lookup(10.0);

        Assert.NotEqual(first, later);
        Assert.Equal(first, back);
        Assert.Equal(2, computes);
        Assert.Equal(first, values[Key(10.0, 12.0, 24.0)]);
    }

    [Fact]
    public void Eviction_Is_Bounded_And_Evicted_Entries_Recompute()
    {
        var cache = new NearBodyIlluminationCache(capacity: 2);
        var computes = 0;

        NearBodyIllumination Lookup(long cell)
            => cache.GetOrCompute(
                new NearBodyIlluminationCacheKey(0, cell, 0),
                () => new NearBodyIllumination(++computes, 0.0));

        var first = Lookup(0);
        Lookup(1);
        Lookup(0); // make cell 0 the most recently used entry
        Lookup(2); // evicts cell 1
        var recomputed = Lookup(1);

        Assert.Equal(1.0, first.PlanetshineStrength);
        Assert.Equal(4.0, recomputed.PlanetshineStrength);
        Assert.Equal(2, cache.Count);
        Assert.Equal(4, computes);
    }

    [Fact]
    public void Invalidation_Advances_Generation_And_Drops_Entries()
    {
        var cache = new NearBodyIlluminationCache();
        var key = Key(2.0, 0.0, 0.0);
        var first = cache.GetOrCompute(key, () => new NearBodyIllumination(0.2, 0.0));
        var generation = cache.CaptureGeneration();

        cache.Invalidate();

        Assert.True(cache.CaptureGeneration() > generation);
        Assert.Equal(0, cache.Count);
        Assert.NotEqual(first, cache.GetOrCompute(key, () => new NearBodyIllumination(0.8, 0.0)));
    }

    [Fact]
    public void Controller_State_Changes_Clear_Its_Instance_Cache()
    {
        var controller = new NearBodyLightController();
        var key = Key(2.0, 0.0, 0.0);
        var source = new NearBodyLightSource(19.5, 0.0, 0.0, 0.52);

        void Warm()
        {
            controller.Cache.GetOrCompute(key, () => new NearBodyIllumination(0.4, 0.0));
            Assert.Equal(1, controller.Cache.Count);
        }

        Warm();
        controller.SetSource(source);
        Assert.Equal(0, controller.Cache.Count);

        Warm();
        controller.SetSource(null);
        Assert.Equal(0, controller.Cache.Count);

        Warm();
        controller.SetEnabled(false);
        Assert.Equal(0, controller.Cache.Count);

        Warm();
        controller.SetEnabled(true);
        Assert.Equal(0, controller.Cache.Count);

        Warm();
        controller.Invalidate();
        Assert.Equal(0, controller.Cache.Count);

        Warm();
        controller.Reset();
        Assert.Equal(0, controller.Cache.Count);
    }

    [Fact]
    public async Task In_Flight_Old_Generation_Cannot_Publish_Into_New_Generation()
    {
        var cache = new NearBodyIlluminationCache();
        var key = Key(3.0, 0.0, 0.0);
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var computes = 0;

        var task = Task.Run(() => cache.GetOrCompute(key, () =>
        {
            var call = Interlocked.Increment(ref computes);
            if (call == 1)
            {
                started.Set();
                release.Wait();
            }

            return new NearBodyIllumination(call / 10.0, 0.0);
        }));

        Assert.True(started.Wait(TimeSpan.FromSeconds(5)));
        cache.Invalidate();
        release.Set();

        Assert.Equal(new NearBodyIllumination(0.2, 0.0), await task);
        Assert.Equal(new NearBodyIllumination(0.2, 0.0), cache.GetOrCompute(key, () =>
            throw new Xunit.Sdk.XunitException("the stale value was incorrectly published")));
        Assert.Equal(2, computes);
    }

    [Fact]
    public void Controller_Captures_One_Sample_And_Caches_Illumination_Not_Vanilla_Combination()
    {
        var giant = new NearBodyLightSource(19.5, 30.0, 0.0, 0.52);
        var sun = new SkyDirection(0.0, -1.0, 0.0);
        var expected = NearBodyLightController.IlluminationFor(
            giant,
            latitudeDeg: 12.0,
            longitudeDeg: 15.0,
            totalDays: 42.125,
            daysPerYear: 360,
            hoursPerDay: 24.0,
            sunDirection: sun);
        var calendar = TestCalendar.Create(totalDays: 42.125, sunDirection: sun);
        calendar.LatitudeDegrees = 12.0;
        calendar.MutateTotalDaysDuringSunCall = 999.0;
        var world = TestWorld.Create(calendar.Proxy);
        var controller = new NearBodyLightController();
        controller.Bind(world.Proxy, _ => 15.0);
        controller.SetSource(giant);

        var actual = controller.IlluminationAt(calendar.Proxy, x: 15.0, z: 12.0);
        Assert.Equal(expected, actual);
        Assert.Equal(1, calendar.SunPositionCalls);
        Assert.Equal(42.125, calendar.SunPositionTotalDays);

        // The calendar changes after the sample. It remains inside the original time bucket, so a
        // warm query must use the captured key and answer without sampling the game again.
        calendar.TotalDays = 42.125 + 0.0001;
        Assert.Equal(actual, controller.IlluminationAt(calendar.Proxy, x: 15.0, z: 12.0));
        Assert.Equal(1, calendar.SunPositionCalls);

        // The cache stores illumination. Each caller's current vanilla value is still combined
        // independently after the same cached answer is retrieved.
        var dark = controller.Apply(calendar.Proxy, 15.0, 12.0, 0.0f);
        var dim = controller.Apply(calendar.Proxy, 15.0, 12.0, 0.75f);
        Assert.NotEqual(dark, dim);
    }

    [Fact]
    public void Solar_Revision_And_Delegate_Replacement_Invalidate_The_Wired_Controller_Cache()
    {
        var calendar = TestCalendar.Create(10.0, new SkyDirection(0.0, -1.0, 0.0));
        var world = TestWorld.Create(calendar.Proxy);
        var controller = new NearBodyLightController();
        var revision = 0L;
        object solarDelegate = new();
        controller.Bind(world.Proxy, _ => 0.0);
        controller.BindSolarState(() => revision, () => solarDelegate);
        controller.SetSource(new NearBodyLightSource(19.5, 30.0, 0.0, 0.52));

        controller.IlluminationAt(calendar.Proxy, 0.0, 0.0);
        Assert.Equal(1, calendar.SunPositionCalls);

        revision++; // same installed delegate, changed solar policy/tilt state
        controller.IlluminationAt(calendar.Proxy, 0.0, 0.0);
        Assert.Equal(2, calendar.SunPositionCalls);

        solarDelegate = new object(); // another mod replaced the calendar delegate
        controller.IlluminationAt(calendar.Proxy, 0.0, 0.0);
        Assert.Equal(3, calendar.SunPositionCalls);
    }

    [Fact]
    public void Controllers_Keep_Bound_World_Cache_State_Independent()
    {
        var source = new NearBodyLightSource(19.5, 30.0, 0.0, 0.52);
        var firstCalendar = TestCalendar.Create(10.0, new SkyDirection(0.0, -1.0, 0.0));
        var secondCalendar = TestCalendar.Create(10.0, new SkyDirection(0.0, 1.0, 0.0));
        var first = new NearBodyLightController();
        var second = new NearBodyLightController();
        first.Bind(TestWorld.Create(firstCalendar.Proxy).Proxy, _ => 0.0);
        second.Bind(TestWorld.Create(secondCalendar.Proxy).Proxy, _ => 0.0);
        first.SetSource(source);
        second.SetSource(source);

        var firstValue = first.IlluminationAt(firstCalendar.Proxy, 0.0, 0.0);
        var secondValue = second.IlluminationAt(secondCalendar.Proxy, 0.0, 0.0);

        Assert.NotEqual(firstValue, secondValue);
        Assert.Equal(1, firstCalendar.SunPositionCalls);
        Assert.Equal(1, secondCalendar.SunPositionCalls);
    }

    [Fact]
    public void Opt_In_Metrics_Count_Hits_Misses_Computes_And_Compute_Time()
    {
        var cache = new NearBodyIlluminationCache();
        cache.EnableMetrics();
        var key = Key(5.0, 0.0, 0.0);

        cache.GetOrCompute(key, () => new NearBodyIllumination(0.4, 0.0));
        cache.GetOrCompute(key, () => throw new Xunit.Sdk.XunitException("warm hit computed"));

        var metrics = cache.ReadMetrics();
        Assert.Equal(1, metrics.Misses);
        Assert.Equal(1, metrics.Hits);
        Assert.Equal(1, metrics.ComputeCalls);
        Assert.True(metrics.ComputeTicks >= 0);
    }

    private static NearBodyIlluminationCacheKey Key(double totalDays, double x, double z)
        => NearBodyIlluminationCacheKey.From(totalDays, x, z);

    private class TestCalendar : DispatchProxy
    {
        public IGameCalendar Proxy { get; private set; } = null!;
        public double TotalDays { get; set; }
        public SkyDirection SunDirection { get; set; }
        public int SunPositionCalls { get; private set; }
        public double SunPositionTotalDays { get; private set; }
        public double LatitudeDegrees { get; set; }
        public double? MutateTotalDaysDuringSunCall { get; set; }

        public static TestCalendar Create(double totalDays, SkyDirection sunDirection)
        {
            var calendar = DispatchProxy.Create<IGameCalendar, TestCalendar>();
            var implementation = (TestCalendar)(object)calendar;
            implementation.Proxy = calendar;
            implementation.TotalDays = totalDays;
            implementation.SunDirection = sunDirection;
            return implementation;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_TotalDays" => TotalDays,
                "get_DaysPerYear" => 360,
                "get_HoursPerDay" => 24.0f,
                "get_OnGetLatitude" => new GetLatitudeDelegate(_ => LatitudeDegrees / 90.0),
                "GetSunPosition" => GetSunPosition(args),
                _ => DefaultValue(targetMethod?.ReturnType)
            };
        }

        private Vec3f GetSunPosition(object?[]? args)
        {
            SunPositionCalls++;
            SunPositionTotalDays = args is { Length: > 1 } && args[1] is double days ? days : double.NaN;
            if (MutateTotalDaysDuringSunCall is { } replacement)
            {
                TotalDays = replacement;
            }

            return new Vec3f((float)SunDirection.X, (float)SunDirection.Y, (float)SunDirection.Z);
        }
    }

    private class TestWorld : DispatchProxy
    {
        public IWorldAccessor Proxy { get; private set; } = null!;
        public IGameCalendar Calendar { get; private set; } = null!;

        public static TestWorld Create(IGameCalendar calendar)
        {
            var world = DispatchProxy.Create<IWorldAccessor, TestWorld>();
            var implementation = (TestWorld)(object)world;
            implementation.Proxy = world;
            implementation.Calendar = calendar;
            return implementation;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
            => targetMethod?.Name switch
            {
                "get_Calendar" => Calendar,
                "get_SeaLevel" => 0,
                _ => DefaultValue(targetMethod?.ReturnType)
            };
    }

    private static object? DefaultValue(Type? type)
        => type is null || !type.IsValueType ? null : Activator.CreateInstance(type);
}
