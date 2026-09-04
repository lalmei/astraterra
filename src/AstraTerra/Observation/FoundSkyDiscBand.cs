using AstraTerra.Astronomy;

namespace AstraTerra.Observation;

/// <summary>
/// A year of evenings somebody else already spent: the band on a disc that is dug up rather than
/// scribed.
/// </summary>
/// <remarks>
/// Nothing here is invented. The sun's declination through the year comes from the same model the
/// world's own sun is drawn from, so a found disc turns the seasons on the days this world turns
/// them on, and the notches are the notches an observer standing at that latitude would have
/// scratched. What makes it a stranger's disc is only where they stood.
/// <para>
/// A mark goes on when the sun has moved a notch and not before, which is the rule the rim itself
/// imposes: a disc cannot record a difference finer than its own graduations, and an observer with
/// nothing new to scratch does not scratch. That gives a rim of a few dozen marks an arc rather than
/// one per evening, which is both what the object should look like and what fits in a stack.
/// </para>
/// </remarks>
public static class FoundSkyDiscBand
{
    /// <summary>
    /// The band of latitudes a found disc is scribed at.
    /// </summary>
    /// <remarks>
    /// Bounded away from the equator, where the sun's swing is too narrow to be worth a year, and
    /// well short of the polar circles, where for part of the year the sun does not set and a band
    /// of sunsets is not a thing that exists.
    /// </remarks>
    public const double MinLatitudeDeg = 20.0;

    public const double MaxLatitudeDeg = 58.0;

    /// <summary>
    /// How much of a year the record covers.
    /// </summary>
    /// <remarks>
    /// Comfortably more than one, because an end of the band is only a turning point once the marks
    /// have approached it and fallen back, and the sun sits on the same notch for a fortnight either
    /// side of a solstice. A year exactly can leave a rim whose last mark is still on the extreme,
    /// which reads as a band that never finished.
    /// </remarks>
    private const double YearsRecorded = 1.2;

    /// <summary>
    /// Scratches the whole year onto a rim graduated this finely, ending on the given day.
    /// </summary>
    /// <param name="lastDay">
    /// The last day the maker marked, in world days. Negative for a disc whose year ran out before
    /// this world had its first day, which is what a disc found in the ground is.
    /// </param>
    public static SolarBand? Scribe(double latitudeDeg, double notchDeg, int daysPerYear, int lastDay)
    {
        if (daysPerYear <= 0 || !double.IsFinite(latitudeDeg))
        {
            return null;
        }

        // Where the sun stops setting for part of the year there is no band, so there is no disc
        // to find either. The same test the instrument uses on its owner's own latitude.
        if (SolarBandPolicy.SwingDegAt(latitudeDeg) is null)
        {
            return null;
        }

        var cosLatitude = Math.Cos(ToRadians(latitudeDeg));

        var marks = new List<SolarMark>();
        double? lastSunrise = null;
        double? lastSunset = null;
        var firstDay = lastDay - (int)Math.Round(daysPerYear * YearsRecorded);

        for (var day = firstDay; day <= lastDay; day++)
        {
            // The sunrise and sunset arcs are two ends of one day, so they are read at the two ends
            // of it. Within a day the difference is far below a notch; the point is that the disc
            // holds one day's pair, the way an observer's evening and the next dawn belong together.
            AddIfItMoved(marks, ref lastSunrise, day, RiseAzimuth(day + 0.25, daysPerYear, cosLatitude), SolarEvent.Sunrise, notchDeg);
            AddIfItMoved(marks, ref lastSunset, day, SetAzimuth(day + 0.75, daysPerYear, cosLatitude), SolarEvent.Sunset, notchDeg);
        }

        // The place is lost — a disc in the ground says nothing about where it was dug out of. Only
        // the latitude survives, because the latitude is written in the marks themselves.
        return marks.Count == 0
            ? null
            : new SolarBand(latitudeDeg, null, null, marks.OrderBy(mark => mark.Day), notchDeg);
    }

    /// <summary>Where the sun comes up at this latitude on this day, or nothing if it does not.</summary>
    private static double? RiseAzimuth(double totalDays, int daysPerYear, double cosLatitude)
    {
        var declination = CelestialMath
            .GetVanillaAlignedSolarEquatorialCoordinates(totalDays, daysPerYear)
            .DeclinationDeg;
        var ratio = Math.Sin(ToRadians(declination)) / cosLatitude;

        // Above one there is no crossing that day: the sun either stays up or stays down, and an
        // observer has nothing to scratch.
        return Math.Abs(ratio) > 1.0 ? null : ToDegrees(Math.Acos(ratio));
    }

    /// <summary>The same crossing at the other end of the day, on the other side of north.</summary>
    private static double? SetAzimuth(double totalDays, int daysPerYear, double cosLatitude)
        => RiseAzimuth(totalDays, daysPerYear, cosLatitude) is { } rise ? 360.0 - rise : null;

    private static void AddIfItMoved(
        List<SolarMark> marks,
        ref double? lastNotchDeg,
        int day,
        double? azimuthDeg,
        SolarEvent solarEvent,
        double notchDeg)
    {
        if (azimuthDeg is not { } azimuth)
        {
            return;
        }

        var notch = SolarBandPolicy.Notch(azimuth, notchDeg);
        if (lastNotchDeg is { } previous && Math.Abs(previous - notch) < 1e-9)
        {
            return;
        }

        marks.Add(new SolarMark(day, notch, solarEvent));
        lastNotchDeg = notch;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    private static double ToDegrees(double radians) => radians * 180.0 / Math.PI;
}
