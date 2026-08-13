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

public enum AstrolabeTargetKind
{
    Constellation,
    Planet
}

/// <summary>
/// Something the astrolabe can be pointed at.
/// </summary>
/// <remarks>
/// Carries an ephemeris rather than a right ascension and declination, because the instrument's whole
/// purpose is answering questions about other times: it scrolls hours and days ahead, and a planet is
/// somewhere else by then. A recorded figure supplies a <see cref="FixedEphemeris"/> and behaves
/// exactly as it always has.
/// </remarks>
/// <param name="SourceId">Stable within its kind: the journal record's id, or the planet's catalog id.</param>
/// <param name="StarCount">Stars in the recorded figure. Zero for anything that is not a constellation.</param>
public sealed record AstrolabeTarget(
    AstrolabeTargetKind Kind,
    string SourceId,
    string DisplayName,
    ISkyEphemeris Ephemeris,
    int StarCount)
{
    /// <summary>A target that does not move, which is every target the astrolabe used to have.</summary>
    public static AstrolabeTarget Fixed(
        AstrolabeTargetKind kind,
        string sourceId,
        string displayName,
        double rightAscensionDeg,
        double declinationDeg,
        int starCount = 0)
        => new(
            kind,
            sourceId,
            displayName,
            // Magnitude is not something the astrolabe reads; it reports position and timing.
            new FixedEphemeris(new EquatorialCoordinates(rightAscensionDeg, declinationDeg), visualMagnitude: 0),
            starCount);
}

/// <param name="Coordinates">Where the target was at the moment read, which for a planet is not where it will be.</param>
public sealed record AstrolabeReading(
    AstrolabeTarget Target,
    EquatorialCoordinates Coordinates,
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

            targets.Add(AstrolabeTarget.Fixed(
                AstrolabeTargetKind.Constellation,
                record.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ConstellationBookService.FormatDisplayName(record),
                AverageCircularDegrees(stars.Select(star => star.RightAscensionDeg)),
                stars.Average(star => star.DeclinationDeg),
                stars.Count));
        }

        return targets;
    }

    /// <summary>
    /// The naked-eye planets as astrolabe targets, in catalog order.
    /// </summary>
    /// <remarks>
    /// Every planet is listed, not only the ones currently up, exactly as a recorded constellation is
    /// listed whether or not it has risen. Telling an observer that Saturn is below the horizon and
    /// transits in nine hours is the instrument working, not failing.
    /// <para>
    /// Each planet gets its own cached ephemeris, so build this once and keep it: the astrolabe asks
    /// about forecast times while the sky renderer asks about now, and two callers sharing one cache
    /// would miss on every frame.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<AstrolabeTarget> BuildPlanetTargets(
        PlanetCatalog planets,
        int daysPerYear,
        double hoursPerDay)
    {
        ArgumentNullException.ThrowIfNull(planets);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(daysPerYear);
        if (hoursPerDay <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursPerDay), hoursPerDay, "Hours per day must be positive.");
        }

        // The catalog's own name is deliberately not carried here. A wanderer has no name until an
        // observer writes one in a book, so the caller resolves it from whatever book is in hand —
        // which also keeps this list valid when that book changes.
        return planets.Planets
            .Select(planet => new AstrolabeTarget(
                AstrolabeTargetKind.Planet,
                planet.Id,
                PlanetJournal.UnidentifiedDisplayName,
                new CachedSkyEphemeris(new PlanetEphemeris(planet, planets.Observer, daysPerYear), hoursPerDay),
                StarCount: 0))
            .ToList();
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

        ArgumentNullException.ThrowIfNull(target);

        var localSiderealDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg);
        var equatorial = target.Ephemeris.PositionAt(totalDays);
        var coordinates = CelestialMath.GetHorizontalCoordinates(
            equatorial.RightAscensionDeg,
            equatorial.DeclinationDeg,
            latitudeDeg,
            localSiderealDeg);
        var horizonClass = ClassifyHorizon(latitudeDeg, equatorial.DeclinationDeg);
        var rotationRateDegPerHour = SiderealRotationRateDegPerHour(daysPerYear, hoursPerDay);
        var siderealCycleHours = 360.0 / rotationRateDegPerHour;

        // The countdown holds the target's right ascension where it is now. A planet drifts under
        // half a degree a day, so over a single night's countdown that is worth a couple of minutes;
        // a comet near perihelion would need the transit solved against its ephemeris instead.
        var hoursUntilTransit = CelestialMath.NormalizeDegrees(equatorial.RightAscensionDeg - localSiderealDeg)
            / rotationRateDegPerHour;

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
            equatorial,
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

        // Sampled through the ephemeris rather than the position already resolved, so a body that
        // moves is compared against where it will actually be.
        var futureTotalDays = totalDays + (MotionSampleHours / hoursPerDay);
        var futureSiderealDeg = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            futureTotalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg);
        var future = target.Ephemeris.PositionAt(futureTotalDays);
        var futureAltitudeDeg = CelestialMath.ClassifyAltitudeDeg(
            future.RightAscensionDeg,
            future.DeclinationDeg,
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
