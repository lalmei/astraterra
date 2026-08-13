namespace AstraTerra.Astronomy;

/// <summary>
/// Six Keplerian elements and the linear rates that carry them forward, as JPL publishes them for
/// the major planets.
/// </summary>
/// <remarks>
/// Rates are per Julian century. The set is accurate to arcminutes across 1800-2050, which is
/// several orders of magnitude better than a billboard in a voxel sky can show; full VSOP87 would be
/// wasted here.
/// </remarks>
public sealed record KeplerianElements(
    double SemiMajorAxisAu,
    double SemiMajorAxisRateAuPerCentury,
    double Eccentricity,
    double EccentricityRatePerCentury,
    double InclinationDeg,
    double InclinationRateDegPerCentury,
    double MeanLongitudeDeg,
    double MeanLongitudeRateDegPerCentury,
    double LongitudeOfPerihelionDeg,
    double LongitudeOfPerihelionRateDegPerCentury,
    double LongitudeOfAscendingNodeDeg,
    double LongitudeOfAscendingNodeRateDegPerCentury
);

/// <summary>A position in the J2000 ecliptic frame, in astronomical units, centred on the sun.</summary>
public readonly record struct EclipticVector(double X, double Y, double Z)
{
    public double Length => Math.Sqrt((X * X) + (Y * Y) + (Z * Z));

    public double EclipticLongitudeDeg => CelestialMath.NormalizeDegrees(Math.Atan2(Y, X) * 180.0 / Math.PI);

    public double EclipticLatitudeDeg => Math.Atan2(Z, Math.Sqrt((X * X) + (Y * Y))) * 180.0 / Math.PI;

    public static EclipticVector operator -(EclipticVector left, EclipticVector right)
        => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
}

/// <summary>
/// Solves an orbit for a heliocentric position. Pure arithmetic, so it can be checked against
/// published positions without a running game.
/// </summary>
/// <remarks>
/// This is JPL's own formulation from <em>Approximate Positions of the Major Planets</em>: advance
/// the elements by their linear rates, take the mean anomaly, solve Kepler's equation, place the body
/// in its orbital plane, then rotate that plane into the ecliptic.
/// </remarks>
public static class KeplerianOrbit
{
    /// <summary>
    /// Newton-Raphson iterations. Converges to well under an arcsecond in about five at planetary
    /// eccentricities; the cap only matters for something far more elongated.
    /// </summary>
    private const int MaximumIterations = 32;

    /// <summary>Convergence threshold on the eccentric anomaly, in degrees. Well below what draws.</summary>
    private const double ConvergenceDeg = 1e-9;

    public static EclipticVector GetHeliocentricPosition(KeplerianElements elements, double julianCenturies)
    {
        ArgumentNullException.ThrowIfNull(elements);

        var semiMajorAxis = elements.SemiMajorAxisAu + (elements.SemiMajorAxisRateAuPerCentury * julianCenturies);
        var eccentricity = elements.Eccentricity + (elements.EccentricityRatePerCentury * julianCenturies);
        var inclination = ToRadians(elements.InclinationDeg + (elements.InclinationRateDegPerCentury * julianCenturies));
        var meanLongitude = elements.MeanLongitudeDeg + (elements.MeanLongitudeRateDegPerCentury * julianCenturies);
        var longitudeOfPerihelion = elements.LongitudeOfPerihelionDeg
            + (elements.LongitudeOfPerihelionRateDegPerCentury * julianCenturies);
        var ascendingNode = ToRadians(elements.LongitudeOfAscendingNodeDeg
            + (elements.LongitudeOfAscendingNodeRateDegPerCentury * julianCenturies));

        var argumentOfPerihelion = ToRadians(longitudeOfPerihelion) - ascendingNode;
        var meanAnomalyDeg = WrapToSignedHalfTurn(meanLongitude - longitudeOfPerihelion);
        var eccentricAnomaly = ToRadians(SolveEccentricAnomalyDeg(meanAnomalyDeg, eccentricity));

        // Position in the orbital plane, with the x axis pointing at perihelion.
        var planeX = semiMajorAxis * (Math.Cos(eccentricAnomaly) - eccentricity);
        var planeY = semiMajorAxis * Math.Sqrt(Math.Max(0.0, 1.0 - (eccentricity * eccentricity))) * Math.Sin(eccentricAnomaly);

        var cosArgument = Math.Cos(argumentOfPerihelion);
        var sinArgument = Math.Sin(argumentOfPerihelion);
        var cosNode = Math.Cos(ascendingNode);
        var sinNode = Math.Sin(ascendingNode);
        var cosInclination = Math.Cos(inclination);
        var sinInclination = Math.Sin(inclination);

        return new EclipticVector(
            (((cosArgument * cosNode) - (sinArgument * sinNode * cosInclination)) * planeX)
                + ((-(sinArgument * cosNode) - (cosArgument * sinNode * cosInclination)) * planeY),
            (((cosArgument * sinNode) + (sinArgument * cosNode * cosInclination)) * planeX)
                + ((-(sinArgument * sinNode) + (cosArgument * cosNode * cosInclination)) * planeY),
            (sinArgument * sinInclination * planeX) + (cosArgument * sinInclination * planeY));
    }

    /// <summary>
    /// Solves <c>M = E - e sin E</c> for the eccentric anomaly, in degrees.
    /// </summary>
    /// <remarks>
    /// There is no closed form, so it is iterated. The starting guess is the mean anomaly itself,
    /// which is within a few degrees for anything as round as a planetary orbit.
    /// </remarks>
    public static double SolveEccentricAnomalyDeg(double meanAnomalyDeg, double eccentricity)
    {
        var eccentricityDeg = 180.0 / Math.PI * eccentricity;
        var eccentricAnomalyDeg = meanAnomalyDeg + (eccentricityDeg * Math.Sin(ToRadians(meanAnomalyDeg)));

        for (var iteration = 0; iteration < MaximumIterations; iteration++)
        {
            var radians = ToRadians(eccentricAnomalyDeg);
            var residualDeg = meanAnomalyDeg - (eccentricAnomalyDeg - (eccentricityDeg * Math.Sin(radians)));
            var stepDeg = residualDeg / (1.0 - (eccentricity * Math.Cos(radians)));
            eccentricAnomalyDeg += stepDeg;
            if (Math.Abs(stepDeg) <= ConvergenceDeg)
            {
                break;
            }
        }

        return eccentricAnomalyDeg;
    }

    /// <summary>
    /// Folds an angle into -180 to 180, which is where the Kepler iteration starts closest to its
    /// answer.
    /// </summary>
    private static double WrapToSignedHalfTurn(double degrees)
    {
        var normalized = CelestialMath.NormalizeDegrees(degrees);
        return normalized > 180.0 ? normalized - 360.0 : normalized;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}
