namespace AstraTerra.Astronomy;

public sealed record ConstellationSeasonInfo(string MonthWindow, string SeasonSummary, string State);

public static class ConstellationSeasonService
{
    private static readonly string[] Months =
    [
        "March", "April", "May", "June", "July", "August",
        "September", "October", "November", "December", "January", "February"
    ];

    public static ConstellationSeasonInfo Describe(
        double averageRightAscensionDeg,
        double averageDeclinationDeg,
        double latitudeDeg,
        int dayOfYear,
        double hourAfterSunset)
    {
        var monthIndex = Mod((int)Math.Round(CelestialMath.NormalizeDegrees(averageRightAscensionDeg) / 30.0), 12);
        var previous = Months[Mod(monthIndex - 1, 12)];
        var current = Months[monthIndex];
        var next = Months[Mod(monthIndex + 1, 12)];
        var localSiderealDeg = CelestialMath.NormalizeDegrees(CelestialMath.GetLocalSiderealAngle(dayOfYear, hourAfterSunset / 24.0, 365) + 180.0);
        var altitude = CelestialMath.ClassifyAltitudeDeg(averageRightAscensionDeg, averageDeclinationDeg, latitudeDeg, localSiderealDeg);
        var hourAngle = CelestialMath.NormalizeDegrees(localSiderealDeg - averageRightAscensionDeg);
        if (hourAngle > 180)
        {
            hourAngle -= 360;
        }

        var state = altitude < 0 ? "below horizon" : Math.Abs(hourAngle) <= 30 ? "culminating" : hourAngle < 0 ? "rising" : "setting";

        return new ConstellationSeasonInfo($"{previous}-{next}", SeasonForMonth(current), state);
    }

    private static string SeasonForMonth(string month) => month switch
    {
        "March" or "April" or "May" => "spring",
        "June" or "July" or "August" => "summer",
        "September" or "October" or "November" => "autumn",
        _ => "winter"
    };

    private static int Mod(int value, int modulus) => ((value % modulus) + modulus) % modulus;
}
