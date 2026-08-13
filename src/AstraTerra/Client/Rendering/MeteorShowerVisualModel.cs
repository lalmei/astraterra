using AstraTerra.Astronomy;
using AstraTerra.Config;

namespace AstraTerra.Client.Rendering;

public readonly record struct MeteorSkyDirection(double X, double Y, double Z);

public sealed record MeteorStreak(
    string ShowerId,
    MeteorSkyDirection InitialCenter,
    MeteorSkyDirection InitialTangent,
    double RadiantSeparationDeg,
    double AngularLengthDeg,
    double AngularWidthDeg,
    double TravelDeg,
    double DurationSeconds,
    double Brightness,
    double AgeSeconds = 0.0);

public sealed record RenderedMeteorStreak(
    string ShowerId,
    MeteorSkyDirection Center,
    MeteorSkyDirection Tangent,
    double RadiantSeparationDeg,
    double AngularLengthDeg,
    double AngularWidthDeg,
    double Alpha);

/// <summary>
/// Converts an observed meteor rate into short-lived, client-local streaks. The astronomical rate
/// remains in <see cref="MeteorShowerActivity"/>; this class converts that real-world hourly rate
/// into elapsed seconds and gives each meteor a deterministic radiant-relative path.
/// </summary>
public sealed class MeteorShowerVisualModel
{
    /// <summary>
    /// Published ZHR values describe meteors per real observation hour, independent of how quickly
    /// the world calendar advances.
    /// </summary>
    public const double ObservationHourSeconds = 3600.0;
    public const int MaximumActiveStreaks = 16;
    public const double MaximumFrameStepSeconds = 0.25;
    private const double SpawnAccumulatorEpsilon = 1e-12;

    /// <summary>
    /// What is left of a streak arriving head-on at the radiant, where perspective collapses the
    /// whole path to a point.
    /// </summary>
    private const double RadiantHeadOnLengthDeg = 0.65;

    /// <summary>
    /// Length a streak gains once it is seen broadside, 90 deg from its radiant. Real shower meteors
    /// that far out draw something like 10-20 deg; this stays under that, far enough to read as a
    /// streak rather than a dash without becoming the brightest thing in the sky every time.
    /// </summary>
    private const double BroadsideLengthDeg = 8.0;

    private readonly List<MeteorStreak> activeStreaks = [];
    private ulong sequence;
    private double spawnAccumulator;

    public MeteorShowerVisualModel(ulong seed = 1)
    {
        sequence = seed;
    }

    public IReadOnlyList<MeteorStreak> ActiveStreaks => activeStreaks;

    public IReadOnlyList<RenderedMeteorStreak> Advance(
        double deltaSeconds,
        IReadOnlyList<MeteorShowerReading> readings,
        double debugRateMultiplier = 1.0)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var elapsed = double.IsFinite(deltaSeconds)
            ? Math.Clamp(deltaSeconds, 0.0, MaximumFrameStepSeconds)
            : 0.0;

        AgeStreaks(elapsed);

        var rateMultiplier = double.IsFinite(debugRateMultiplier)
            ? Math.Clamp(debugRateMultiplier, 0.0, AstraTerraConfig.MaximumDebugMeteorRateMultiplier)
            : 1.0;
        var observedRate = readings.Sum(reading => Math.Max(0.0, reading.ObservedHourlyRate));
        var spawnRate = observedRate * rateMultiplier;
        if (!double.IsFinite(spawnRate) || spawnRate <= 0.0)
        {
            spawnAccumulator = 0.0;
            return BuildFrames();
        }

        spawnAccumulator += spawnRate * elapsed / ObservationHourSeconds;
        while (spawnAccumulator >= 1.0 - SpawnAccumulatorEpsilon && activeStreaks.Count < MaximumActiveStreaks)
        {
            spawnAccumulator = Math.Max(0.0, spawnAccumulator - 1.0);
            var spawnSequence = sequence++;

            // Which shower a meteor belongs to is drawn against the astronomical rate, never the
            // debug-scaled one. Sampling against the scaled total would push almost every draw off
            // the end of the table and onto the fallback, so a raised multiplier would quietly
            // attribute the whole sky to the weakest active shower and fly it from that radiant.
            var reading = ChooseReading(readings, observedRate, SampleUnit(spawnSequence, 0));
            var streak = TryCreateStreak(reading, spawnSequence);
            if (streak is not null)
            {
                activeStreaks.Add(streak);
            }
        }

        // Do not bank an unbounded burst while the active-streak cap is occupied.
        spawnAccumulator = Math.Min(spawnAccumulator, 1.0);
        return BuildFrames();
    }

    public void Clear()
    {
        activeStreaks.Clear();
        spawnAccumulator = 0.0;
    }

    /// <summary>
    /// How long a streak draws, from head-on at the radiant to broadside across the sky.
    /// </summary>
    /// <remarks>
    /// Every meteor in a shower travels the same direction, so perspective alone decides what the
    /// observer gets: a point near the radiant and a full path at right angles to it. The exponent
    /// spends most of that growth inside the first 30 deg, which is where the foreshortening actually
    /// lets go.
    /// </remarks>
    public static double CalculateAngularLengthDeg(double radiantSeparationDeg)
    {
        var separation = Math.Clamp(radiantSeparationDeg, 0.0, 90.0) * Math.PI / 180.0;
        return RadiantHeadOnLengthDeg + (BroadsideLengthDeg * Math.Pow(Math.Sin(separation), 0.9));
    }

    private void AgeStreaks(double elapsed)
    {
        for (var index = activeStreaks.Count - 1; index >= 0; index--)
        {
            var aged = activeStreaks[index] with { AgeSeconds = activeStreaks[index].AgeSeconds + elapsed };
            if (aged.AgeSeconds >= aged.DurationSeconds)
            {
                activeStreaks.RemoveAt(index);
            }
            else
            {
                activeStreaks[index] = aged;
            }
        }
    }

    private IReadOnlyList<RenderedMeteorStreak> BuildFrames()
    {
        if (activeStreaks.Count == 0)
        {
            return Array.Empty<RenderedMeteorStreak>();
        }

        var frames = new List<RenderedMeteorStreak>(activeStreaks.Count);
        foreach (var streak in activeStreaks)
        {
            var progress = Math.Clamp(streak.AgeSeconds / streak.DurationSeconds, 0.0, 1.0);
            var travelRadians = streak.TravelDeg * progress * Math.PI / 180.0;
            var center = Normalize(Add(
                Scale(streak.InitialCenter, Math.Cos(travelRadians)),
                Scale(streak.InitialTangent, Math.Sin(travelRadians))));
            var tangent = Normalize(Add(
                Scale(streak.InitialCenter, -Math.Sin(travelRadians)),
                Scale(streak.InitialTangent, Math.Cos(travelRadians))));
            var growth = SmoothStep(Math.Clamp(progress / 0.18, 0.0, 1.0));
            var fadeIn = Math.Clamp(progress / 0.08, 0.0, 1.0);
            var fadeOut = Math.Clamp((1.0 - progress) / 0.72, 0.0, 1.0);
            var alpha = streak.Brightness * fadeIn * Math.Pow(fadeOut, 0.65);

            frames.Add(new RenderedMeteorStreak(
                streak.ShowerId,
                center,
                tangent,
                streak.RadiantSeparationDeg,
                streak.AngularLengthDeg * (0.35 + (0.65 * growth)),
                streak.AngularWidthDeg,
                alpha));
        }

        return frames;
    }

    private static MeteorShowerReading ChooseReading(
        IReadOnlyList<MeteorShowerReading> readings,
        double observedRate,
        double sample)
    {
        var target = sample * observedRate;
        foreach (var reading in readings)
        {
            target -= Math.Max(0.0, reading.ObservedHourlyRate);
            if (target <= 0.0 && reading.ObservedHourlyRate > 0.0)
            {
                return reading;
            }
        }

        return readings.Last(reading => reading.ObservedHourlyRate > 0.0);
    }

    private static MeteorStreak? TryCreateStreak(MeteorShowerReading reading, ulong spawnSequence)
    {
        var radiant = DirectionFromHorizontal(reading.AzimuthDeg, reading.AltitudeDeg);
        var reference = Math.Abs(radiant.Y) < 0.92
            ? new MeteorSkyDirection(0.0, 1.0, 0.0)
            : new MeteorSkyDirection(1.0, 0.0, 0.0);
        var firstBasis = Normalize(Cross(reference, radiant));
        var secondBasis = Normalize(Cross(radiant, firstBasis));

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var positionAngle = SampleUnit(spawnSequence, 1 + (attempt * 2)) * Math.PI * 2.0;
            var separationDeg = 8.0 + (62.0 * Math.Sqrt(SampleUnit(spawnSequence, 2 + (attempt * 2))));
            var separation = separationDeg * Math.PI / 180.0;
            var radial = Add(
                Scale(firstBasis, Math.Cos(positionAngle)),
                Scale(secondBasis, Math.Sin(positionAngle)));
            var center = Normalize(Add(Scale(radiant, Math.Cos(separation)), Scale(radial, Math.Sin(separation))));
            if (center.Y <= Math.Sin(4.0 * Math.PI / 180.0))
            {
                continue;
            }

            var tangent = Normalize(Add(Scale(radiant, -Math.Sin(separation)), Scale(radial, Math.Cos(separation))));
            var lengthDeg = CalculateAngularLengthDeg(separationDeg);
            return new MeteorStreak(
                reading.Shower.Id,
                center,
                tangent,
                separationDeg,
                lengthDeg,
                0.10 + (0.13 * SampleUnit(spawnSequence, 50)),
                lengthDeg * (0.45 + (0.25 * SampleUnit(spawnSequence, 51))),
                0.50 + (0.42 * SampleUnit(spawnSequence, 52)),
                0.68 + (0.32 * SampleUnit(spawnSequence, 53)));
        }

        return null;
    }

    private static MeteorSkyDirection DirectionFromHorizontal(double azimuthDeg, double altitudeDeg)
    {
        var azimuth = azimuthDeg * Math.PI / 180.0;
        var altitude = altitudeDeg * Math.PI / 180.0;
        var horizontal = Math.Cos(altitude);
        return new MeteorSkyDirection(
            horizontal * Math.Sin(azimuth),
            Math.Sin(altitude),
            -horizontal * Math.Cos(azimuth));
    }

    private static double SampleUnit(ulong sequenceValue, int salt)
    {
        var value = sequenceValue + (0x9e3779b97f4a7c15UL * (ulong)(salt + 1));
        value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
        value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
        value ^= value >> 31;
        return (value >> 11) * (1.0 / 9007199254740992.0);
    }

    private static MeteorSkyDirection Add(MeteorSkyDirection first, MeteorSkyDirection second)
        => new(first.X + second.X, first.Y + second.Y, first.Z + second.Z);

    private static MeteorSkyDirection Scale(MeteorSkyDirection value, double factor)
        => new(value.X * factor, value.Y * factor, value.Z * factor);

    private static MeteorSkyDirection Cross(MeteorSkyDirection first, MeteorSkyDirection second)
        => new(
            (first.Y * second.Z) - (first.Z * second.Y),
            (first.Z * second.X) - (first.X * second.Z),
            (first.X * second.Y) - (first.Y * second.X));

    private static MeteorSkyDirection Normalize(MeteorSkyDirection direction)
    {
        var length = Math.Sqrt(
            (direction.X * direction.X) +
            (direction.Y * direction.Y) +
            (direction.Z * direction.Z));
        return length <= double.Epsilon
            ? new MeteorSkyDirection(0.0, 1.0, 0.0)
            : Scale(direction, 1.0 / length);
    }

    private static double SmoothStep(double value)
        => value * value * (3.0 - (2.0 * value));
}
