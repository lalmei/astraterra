using AstraTerra.Astronomy;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SightingIdentificationTests
{
    private const double LatitudeDeg = 51.0;
    private const double HoursPerDay = 24.0;

    [Fact]
    public void One_Body_On_Every_Night_Of_The_Record_Is_The_Answer()
    {
        var wanderer = new DriftingEphemeris(rightAscensionAtDayZeroDeg: 60.0, degreesPerDay: 0.5, declinationDeg: 10.0);
        var log = new ObservationLog();
        Sight(log, wanderer, day: 412, siderealAngleDeg: 90.0);
        Sight(log, wanderer, day: 416, siderealAngleDeg: 250.0);

        var matched = SightingIdentification.Match(
            log.Observations,
            [("mars", wanderer), ("saturn", new DriftingEphemeris(200.0, 0.03, -5.0))],
            HoursPerDay);

        Assert.Equal("mars", matched);
    }

    [Fact]
    public void A_Body_That_Fits_One_Night_But_Not_The_Next_Is_Not_The_Answer()
    {
        var wanderer = new DriftingEphemeris(60.0, 0.5, 10.0);
        var impostor = new DriftingEphemeris(60.0, -0.5, 10.0);
        var log = new ObservationLog();
        Sight(log, wanderer, day: 412, siderealAngleDeg: 90.0);
        Sight(log, wanderer, day: 416, siderealAngleDeg: 250.0);

        Assert.Equal("real", SightingIdentification.Match(
            log.Observations,
            [("real", wanderer), ("impostor", impostor)],
            HoursPerDay));
    }

    [Fact]
    public void Two_Bodies_The_Record_Cannot_Tell_Apart_Give_No_Answer()
    {
        var wanderer = new DriftingEphemeris(60.0, 0.5, 10.0);
        var log = new ObservationLog();
        Sight(log, wanderer, day: 412, siderealAngleDeg: 90.0);

        Assert.Null(SightingIdentification.Match(
            log.Observations,
            [("one", wanderer), ("its twin", new DriftingEphemeris(60.0, 0.5, 10.0))],
            HoursPerDay));
    }

    [Fact]
    public void Nothing_That_Fits_Gives_No_Answer()
    {
        var log = new ObservationLog();
        Sight(log, new DriftingEphemeris(60.0, 0.5, 10.0), day: 412, siderealAngleDeg: 90.0);

        Assert.Null(SightingIdentification.Match(
            log.Observations,
            [("elsewhere", new DriftingEphemeris(200.0, 0.5, -30.0))],
            HoursPerDay));
    }

    [Fact]
    public void Entries_Written_Before_The_Book_Kept_The_Skys_Angle_Cannot_Be_Matched()
    {
        var log = new ObservationLog();
        log.Record(34.2, 118.0, 1.4, 412, 21.5, LatitudeDeg, InstrumentResolution.BrassSextantDeg);

        Assert.Null(SightingIdentification.Match(
            log.Observations,
            [("anything", new DriftingEphemeris(60.0, 0.5, 10.0))],
            HoursPerDay));
    }

    private static void Sight(ObservationLog log, ISkyEphemeris body, int day, double siderealAngleDeg)
    {
        var hour = 21.5;
        var position = body.PositionAt(day + (hour / HoursPerDay));
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            position.RightAscensionDeg,
            position.DeclinationDeg,
            LatitudeDeg,
            siderealAngleDeg);

        log.Record(
            horizontal.AltitudeDeg,
            horizontal.AzimuthDeg,
            visualMagnitude: 1.4,
            day: day,
            hour: hour,
            latitudeDeg: LatitudeDeg,
            resolutionDeg: InstrumentResolution.BrassSextantDeg,
            siderealAngleDeg: siderealAngleDeg);
    }

    /// <summary>A body that wanders along the equator at a steady rate, which is enough to match against.</summary>
    private sealed class DriftingEphemeris(double rightAscensionAtDayZeroDeg, double degreesPerDay, double declinationDeg)
        : ISkyEphemeris
    {
        public EquatorialCoordinates PositionAt(double totalDays)
            => new(
                CelestialMath.NormalizeDegrees(rightAscensionAtDayZeroDeg + (degreesPerDay * totalDays)),
                declinationDeg);

        public double MagnitudeAt(double totalDays) => 1.4;
    }
}
