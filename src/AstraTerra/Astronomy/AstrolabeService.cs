using AstraTerra.Constellations;

namespace AstraTerra.Astronomy;

public enum AstrolabeMotionState
{
    Rising,
    Culminating,
    Setting,
    BelowHorizon,
    NeverRises
}

public enum AstrolabeHorizonClass
{
    Normal,
    Circumpolar,
    NeverRises
}

public sealed record AstrolabeTarget(
    int ConstellationId,
    string DisplayName,
    double RightAscensionDeg,
    double DeclinationDeg,
    int StarCount);

public sealed record AstrolabeReading(
    AstrolabeTarget Target,
    double AzimuthDeg,
    double AltitudeDeg,
    AstrolabeMotionState MotionState,
    AstrolabeHorizonClass HorizonClass,
    double HoursUntilTransit,
    double SiderealCycleHours);

public static class AstrolabeService
{
    private const double MotionSampleHours = 0.05;
    private const double CulminationWindowHours = 0.5;

    public static IReadOnlyList<AstrolabeTarget> BuildTargets(ConstellationJournal journal, StarCatalog catalog)
    {
        var starsByHip = catalog.Stars.ToDictionary(star => star.Hip);
        var targets = new List<AstrolabeTarget>();

        foreach (var record in journal.Constellations.OrderBy(record => record.Id))
        {
            var stars = record.Edges
                .SelectMany(edge => new[] { edge.A, edge.B })
                .Distinct()
                .Where(starsByHip.ContainsKey)
                .Select(hip => starsByHip[hip])
                .ToList();
            if (stars.Count == 0)
            {
                continue;
            }

            targets.Add(new AstrolabeTarget(
                record.Id,
                ConstellationBookService.FormatDisplayName(record),
                AverageCircularDegrees(stars.Select(star => star.RightAscensionDeg)),
                stars.Average(star => star.DeclinationDeg),
                stars.Count));
        }

        return targets;
    }

    public static AstrolabeReading Read(
        AstrolabeTarget target,
        double latitudeDeg,
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        double longitudeDeg)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        var localSiderealDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg);
        var coordinates = CelestialMath.GetHorizontalCoordinates(
            target.RightAscensionDeg,
            target.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        var horizonClass = ClassifyHorizon(latitudeDeg, target.DeclinationDeg);
        var rotationRateDegPerHour = SiderealRotationRateDegPerHour(daysPerYear, hoursPerDay);
        var siderealCycleHours = 360.0 / rotationRateDegPerHour;
        var hoursUntilTransit = CelestialMath.NormalizeDegrees(target.RightAscensionDeg - localSiderealDeg) / rotationRateDegPerHour;

        var motionState = ClassifyMotion(
            target,
            latitudeDeg,
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg,
            coordinates.AltitudeDeg,
            hoursUntilTransit,
            siderealCycleHours,
            horizonClass);

        return new AstrolabeReading(
            target,
            coordinates.AzimuthDeg,
            coordinates.AltitudeDeg,
            motionState,
            horizonClass,
            hoursUntilTransit,
            siderealCycleHours);
    }

    public static string CompassPoint(double azimuthDeg)
    {
        string[] points = ["N", "NE", "E", "SE", "S", "SW", "W", "NW"];
        var index = (int)Math.Floor((CelestialMath.NormalizeDegrees(azimuthDeg) + 22.5) / 45.0) % points.Length;
        return points[index];
    }

    private static AstrolabeMotionState ClassifyMotion(
        AstrolabeTarget target,
        double latitudeDeg,
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        double longitudeDeg,
        double altitudeDeg,
        double hoursUntilTransit,
        double siderealCycleHours,
        AstrolabeHorizonClass horizonClass)
    {
        if (horizonClass == AstrolabeHorizonClass.NeverRises)
        {
            return AstrolabeMotionState.NeverRises;
        }

        if (altitudeDeg < 0)
        {
            return AstrolabeMotionState.BelowHorizon;
        }

        var hoursFromTransit = Math.Min(hoursUntilTransit, siderealCycleHours - hoursUntilTransit);
        if (hoursFromTransit <= CulminationWindowHours)
        {
            return AstrolabeMotionState.Culminating;
        }

        var futureSiderealDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays + (MotionSampleHours / hoursPerDay),
            daysPerYear,
            hoursPerDay,
            longitudeDeg);
        var futureAltitudeDeg = CelestialMath.ClassifyAltitudeDeg(
            target.RightAscensionDeg,
            target.DeclinationDeg,
            latitudeDeg,
            futureSiderealDeg);
        return futureAltitudeDeg > altitudeDeg
            ? AstrolabeMotionState.Rising
            : AstrolabeMotionState.Setting;
    }

    private static AstrolabeHorizonClass ClassifyHorizon(double latitudeDeg, double declinationDeg)
    {
        var maximumAltitudeDeg = 90.0 - Math.Abs(latitudeDeg - declinationDeg);
        if (maximumAltitudeDeg <= 0)
        {
            return AstrolabeHorizonClass.NeverRises;
        }

        var minimumAltitudeDeg = Math.Abs(latitudeDeg + declinationDeg) - 90.0;
        return minimumAltitudeDeg >= 0
            ? AstrolabeHorizonClass.Circumpolar
            : AstrolabeHorizonClass.Normal;
    }

    private static double SiderealRotationRateDegPerHour(int daysPerYear, double hoursPerDay)
    {
        // 15 deg per solar hour, plus the seasonal drift the sky gains over a year, which makes
        // the sidereal day slightly shorter than the solar day.
        return 15.0 + (360.0 / (daysPerYear * hoursPerDay));
    }

    private static double AverageCircularDegrees(IEnumerable<double> degrees)
    {
        var radians = degrees.Select(value => value * Math.PI / 180.0).ToList();
        var x = radians.Sum(Math.Cos) / radians.Count;
        var y = radians.Sum(Math.Sin) / radians.Count;
        return CelestialMath.NormalizeDegrees(Math.Atan2(y, x) * 180.0 / Math.PI);
    }
}
