using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

public sealed class KeplerianOrbitTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(37.5)]
    [InlineData(180.0)]
    [InlineData(-120.0)]
    public void A_Circular_Orbit_Moves_At_Its_Mean_Rate(double meanAnomalyDeg)
    {
        Assert.Equal(meanAnomalyDeg, KeplerianOrbit.SolveEccentricAnomalyDeg(meanAnomalyDeg, eccentricity: 0), 9);
    }

    [Theory]
    [InlineData(0.0067)]
    [InlineData(0.0934)]
    [InlineData(0.2056)]
    [InlineData(0.85)]
    public void The_Solved_Anomaly_Satisfies_Kepler_Equation(double eccentricity)
    {
        for (var meanAnomalyDeg = -180.0; meanAnomalyDeg <= 180.0; meanAnomalyDeg += 7.5)
        {
            var eccentricAnomalyDeg = KeplerianOrbit.SolveEccentricAnomalyDeg(meanAnomalyDeg, eccentricity);
            var recovered = eccentricAnomalyDeg
                - (180.0 / Math.PI * eccentricity * Math.Sin(eccentricAnomalyDeg * Math.PI / 180.0));

            Assert.Equal(meanAnomalyDeg, recovered, 6);
        }
    }

    [Fact]
    public void A_Body_Stays_Between_Perihelion_And_Aphelion()
    {
        var elements = new KeplerianElements(
            SemiMajorAxisAu: 1.5, SemiMajorAxisRateAuPerCentury: 0,
            Eccentricity: 0.2, EccentricityRatePerCentury: 0,
            InclinationDeg: 5, InclinationRateDegPerCentury: 0,
            MeanLongitudeDeg: 0, MeanLongitudeRateDegPerCentury: 19_000,
            LongitudeOfPerihelionDeg: 30, LongitudeOfPerihelionRateDegPerCentury: 0,
            LongitudeOfAscendingNodeDeg: 45, LongitudeOfAscendingNodeRateDegPerCentury: 0);

        for (var centuries = 0.0; centuries < 0.02; centuries += 0.0002)
        {
            var distance = KeplerianOrbit.GetHeliocentricPosition(elements, centuries).Length;

            Assert.InRange(distance, 1.5 * (1 - 0.2) - 1e-9, 1.5 * (1 + 0.2) + 1e-9);
        }
    }

    [Fact]
    public void An_Inclined_Orbit_Crosses_The_Ecliptic_At_Its_Node()
    {
        var elements = new KeplerianElements(
            SemiMajorAxisAu: 1.0, SemiMajorAxisRateAuPerCentury: 0,
            Eccentricity: 0, EccentricityRatePerCentury: 0,
            InclinationDeg: 20, InclinationRateDegPerCentury: 0,
            MeanLongitudeDeg: 0, MeanLongitudeRateDegPerCentury: 36_000,
            LongitudeOfPerihelionDeg: 0, LongitudeOfPerihelionRateDegPerCentury: 0,
            LongitudeOfAscendingNodeDeg: 0, LongitudeOfAscendingNodeRateDegPerCentury: 0);

        // Mean longitude 0 puts the body at its ascending node, which is on the ecliptic by
        // definition; a quarter turn later it is as far above the plane as its inclination allows.
        var atNode = KeplerianOrbit.GetHeliocentricPosition(elements, 0);
        var quarterTurnLater = KeplerianOrbit.GetHeliocentricPosition(elements, 0.0025);

        Assert.Equal(0.0, atNode.EclipticLatitudeDeg, 6);
        Assert.Equal(0.0, atNode.EclipticLongitudeDeg, 6);
        Assert.Equal(20.0, quarterTurnLater.EclipticLatitudeDeg, 6);
    }
}
