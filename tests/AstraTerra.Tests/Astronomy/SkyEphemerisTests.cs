using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class SkyEphemerisTests
{
    [Fact]
    public void A_Fixed_Body_Reads_The_Same_At_Any_World_Time()
    {
        var radiant = new FixedEphemeris(new EquatorialCoordinates(46.2, 57.4), visualMagnitude: 2.5);

        Assert.Equal(new EquatorialCoordinates(46.2, 57.4), radiant.PositionAt(0));
        Assert.Equal(new EquatorialCoordinates(46.2, 57.4), radiant.PositionAt(4_000));
        Assert.Equal(2.5, radiant.MagnitudeAt(4_000));
    }

    [Fact]
    public void A_Cached_Body_Is_Sampled_Once_Per_World_Minute_However_Many_Frames_Pass()
    {
        var counting = new CountingEphemeris();
        var cached = new CachedSkyEphemeris(counting, hoursPerDay: 24);

        // Roughly a frame apart, well inside one world minute.
        for (var frame = 0; frame < 120; frame++)
        {
            cached.PositionAt(10.0 + (frame * 1e-6));
        }

        Assert.Equal(1, counting.PositionCalls);
    }

    [Fact]
    public void A_Cached_Body_Moves_On_At_The_Next_World_Minute()
    {
        var counting = new CountingEphemeris();
        var cached = new CachedSkyEphemeris(counting, hoursPerDay: 24);
        var oneMinuteDays = 1.0 / (24.0 * 60.0);

        var first = cached.PositionAt(10.0);
        var second = cached.PositionAt(10.0 + (oneMinuteDays * 1.5));

        Assert.Equal(2, counting.PositionCalls);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_Cached_Position_And_Magnitude_Cost_One_Evaluation_Of_The_Same_Instant()
    {
        var counting = new CountingEphemeris();
        var cached = new CachedSkyEphemeris(counting, hoursPerDay: 24);

        var position = cached.PositionAt(10.250_1);
        var magnitude = cached.MagnitudeAt(10.250_2);

        Assert.Equal(1, counting.PositionCalls);
        Assert.Equal(1, counting.MagnitudeCalls);
        Assert.Equal(90.0, position.RightAscensionDeg, 9);
        Assert.Equal(position.RightAscensionDeg, magnitude, 9);
    }

    [Fact]
    public void A_Cached_Body_Reads_The_Same_Whoever_Asks_First_Inside_A_Minute()
    {
        var earlyFirst = new CachedSkyEphemeris(new CountingEphemeris(), hoursPerDay: 24);
        var lateFirst = new CachedSkyEphemeris(new CountingEphemeris(), hoursPerDay: 24);

        // Both instants fall in the same world minute; the sample is taken at its start.
        var early = earlyFirst.PositionAt(10.000_01);
        var late = lateFirst.PositionAt(10.000_5);

        Assert.Equal(early, late);
    }

    [Fact]
    public void A_Cached_Body_Recomputes_When_The_Forecast_Scrolls_Back()
    {
        var counting = new CountingEphemeris();
        var cached = new CachedSkyEphemeris(counting, hoursPerDay: 24);

        cached.PositionAt(10.0);
        var yesterday = cached.PositionAt(9.0);

        Assert.Equal(2, counting.PositionCalls);
        Assert.Equal(counting.PositionAt(9.0), yesterday);
    }

    [Fact]
    public void A_Shorter_World_Day_Still_Samples_On_Its_Own_Minutes()
    {
        var counting = new CountingEphemeris();
        var cached = new CachedSkyEphemeris(counting, hoursPerDay: 8);
        var oneMinuteDays = 1.0 / (8.0 * 60.0);

        cached.PositionAt(3.0);
        cached.PositionAt(3.0 + (oneMinuteDays * 0.5));
        cached.PositionAt(3.0 + (oneMinuteDays * 1.5));

        Assert.Equal(2, counting.PositionCalls);
    }

    [Fact]
    public void A_Cached_Body_Rejects_A_World_With_No_Day_Length()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CachedSkyEphemeris(new CountingEphemeris(), hoursPerDay: 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CachedSkyEphemeris(new CountingEphemeris(), hoursPerDay: 24, samplesPerWorldHour: 0));
    }

    /// <summary>An ephemeris whose position is its own timestamp, so a stale sample is visible.</summary>
    private sealed class CountingEphemeris : ISkyEphemeris
    {
        public int PositionCalls { get; private set; }

        public int MagnitudeCalls { get; private set; }

        public EquatorialCoordinates PositionAt(double totalDays)
        {
            PositionCalls++;
            return new EquatorialCoordinates(CelestialMath.NormalizeDegrees(totalDays * 360.0), totalDays);
        }

        public double MagnitudeAt(double totalDays)
        {
            MagnitudeCalls++;
            return CelestialMath.NormalizeDegrees(totalDays * 360.0);
        }
    }
}
