namespace AstraTerra.Astronomy;

/// <summary>
/// One annual meteor shower: a fixed radiant, the point in the world's orbit where it peaks, and how
/// strong it gets.
/// </summary>
/// <remarks>
/// A radiant is a fixed right ascension and declination exactly like a star, so it needs no moving
/// body ephemeris — <see cref="CelestialMath.GetHorizontalCoordinates"/> places it directly.
/// </remarks>
/// <param name="Id">Stable key for saves and forecasts. The IAU three-letter shower code.</param>
/// <param name="PeakSolarLongitudeDeg">
/// Degrees of solar longitude at maximum, measured from the March equinox, which is where
/// <see cref="CelestialMath.GetSolarLongitudeDegrees"/> reads zero. Anchoring here rather than on a
/// day of the year is what keeps a shower in its own season on a 12-day world and a 360-day world
/// alike.
/// </param>
/// <param name="WindowHalfWidthDeg">
/// Half the shower's activity period, in degrees of solar longitude. Rate reaches zero this far
/// either side of the peak. Must be greater than zero and no more than 180.
/// </param>
/// <param name="PeakZenithHourlyRate">
/// Published ZHR at maximum: the rate a single observer would see under a perfectly dark sky with
/// the radiant overhead. Real observers always see fewer, which is what
/// <see cref="MeteorShowerActivity"/> works out.
/// </param>
public sealed record MeteorShowerEntry(
    string Id,
    string DisplayName,
    double RightAscensionDeg,
    double DeclinationDeg,
    double PeakSolarLongitudeDeg,
    double WindowHalfWidthDeg,
    double PeakZenithHourlyRate
);
