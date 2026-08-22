namespace AstraTerra.Observation;

/// <summary>How close the plate cut into the instrument is to the ground the observer stands on.</summary>
public enum AstrolabePlateFit
{
    /// <summary>Cut for this latitude, near enough that no reading could show the difference.</summary>
    OnPlate,

    /// <summary>Still readable, but the altitudes and transit times have started to lie.</summary>
    Drifting,

    /// <summary>Far enough that the instrument is answering about somewhere the observer is not.</summary>
    OffPlate
}

/// <summary>
/// A real astrolabe is not a general instrument. Its plate is engraved with the horizon and the
/// altitude circles of one latitude, and carrying it somewhere else does not re-engrave it: the
/// rete still turns, the readings still look plausible, and they are quietly wrong. This mod's
/// astrolabe used to read the player's live latitude off their position every frame, which made it
/// a magic device that silently re-cut itself with every step north.
/// </summary>
/// <remarks>
/// So the plate is stored on the itemstack and every reading is answered for <em>it</em>, not for
/// where the player happens to be standing. Travelling does not corrupt the instrument; it just
/// means the instrument is describing the sky over the place it was cut for, and the HUD says by how
/// far. Cutting a new plate is <see cref="CanCalibrate"/>: sighting work that needs a dark sky
/// overhead, and nothing else — no star catalog, no crafting grid, no lost brass.
/// <para>
/// Only latitude is stored. Longitude shifts what time a star transits but not where the horizon
/// and the altitude circles fall, so it stays live: the instrument keeps telling the truth about
/// the hour wherever it is carried, and only its latitude is fixed. That is also how the real
/// object behaves.
/// </para>
/// </remarks>
public static class AstrolabeCalibrationPolicy
{
    /// <summary>
    /// Below this the plate counts as cut for where the player is. A degree of latitude is about
    /// 111 km on Earth and moves a star's altitude by a degree — under the width of the rule, and
    /// well under what anyone reads off a HUD line.
    /// </summary>
    public const double OnPlateToleranceDeg = 1.5;

    /// <summary>
    /// Past this the readings are wrong in ways that matter: a target the plate calls circumpolar
    /// may actually set, and one it calls up may never rise. The HUD stops describing this as drift
    /// and starts asking for a new plate.
    /// </summary>
    public const double OffPlateToleranceDeg = 8.0;

    /// <summary>
    /// How long the sighting takes. Long enough to be a decision rather than a twitch of the mouse,
    /// short enough that recalibrating on arrival is not a chore.
    /// </summary>
    public const float CalibrationSeconds = 3.0f;

    /// <summary>
    /// The same darkness floor the sextant sights stars under. Calibrating means sighting the pole,
    /// so if the sky is too bright to sight a star it is too bright to cut a plate.
    /// </summary>
    public const double DarknessFloor = 0.02;

    /// <summary>How far the observer is from the latitude the plate was cut for.</summary>
    public static double DriftDeg(double plateLatitudeDeg, double observerLatitudeDeg)
        => Math.Abs(observerLatitudeDeg - plateLatitudeDeg);

    public static AstrolabePlateFit Fit(double plateLatitudeDeg, double observerLatitudeDeg)
    {
        var drift = DriftDeg(plateLatitudeDeg, observerLatitudeDeg);
        if (drift <= OnPlateToleranceDeg)
        {
            return AstrolabePlateFit.OnPlate;
        }

        return drift <= OffPlateToleranceDeg ? AstrolabePlateFit.Drifting : AstrolabePlateFit.OffPlate;
    }

    /// <param name="daylightStrength">Vintage Story's day/night term, 1 at noon and 0 at midnight.</param>
    public static bool CanCalibrate(bool canSeeSky, double daylightStrength)
        => canSeeSky && 1.0 - daylightStrength > DarknessFloor;

    /// <summary>
    /// Why the sighting cannot be taken, phrased for the HUD, or null when it can.
    /// </summary>
    /// <remarks>
    /// Two obstacles rather than one message, because they need different fixes: a player under a
    /// roof at midnight and a player in an open field at noon are both blocked, and telling either
    /// of them "you cannot calibrate here" leaves them to guess which.
    /// </remarks>
    public static string? DescribeObstacle(bool canSeeSky, double daylightStrength)
    {
        if (!canSeeSky)
        {
            return "No sky overhead — step out from under cover to sight the pole.";
        }

        return 1.0 - daylightStrength > DarknessFloor
            ? null
            : "Too bright to sight the pole — wait for dusk.";
    }

    public static float Progress(float secondsUsed)
        => Math.Clamp(secondsUsed / CalibrationSeconds, 0f, 1f);

    public static bool IsComplete(float secondsUsed)
        => secondsUsed >= CalibrationSeconds;

    public static string FormatLatitude(double latitudeDeg)
        => Math.Abs(latitudeDeg) < 0.05
            ? "latitude 0.0°"
            : $"latitude {Math.Abs(latitudeDeg):0.0}° {(latitudeDeg > 0 ? "N" : "S")}";

    /// <summary>
    /// The plate's latitude, and — once the two have parted company — which way the player has
    /// walked away from it and whether it is far enough to be worth re-cutting.
    /// </summary>
    public static string DescribePlate(double plateLatitudeDeg, double observerLatitudeDeg)
    {
        var fit = Fit(plateLatitudeDeg, observerLatitudeDeg);
        if (fit == AstrolabePlateFit.OnPlate)
        {
            return $"plate {FormatLatitude(plateLatitudeDeg)}";
        }

        var drift = DriftDeg(plateLatitudeDeg, observerLatitudeDeg);
        var bearing = observerLatitudeDeg > plateLatitudeDeg ? "north" : "south";
        var verdict = fit == AstrolabePlateFit.OffPlate ? ", recut it" : string.Empty;
        return $"plate {FormatLatitude(plateLatitudeDeg)} — you are {drift:0.0}° {bearing} of it{verdict}";
    }
}
