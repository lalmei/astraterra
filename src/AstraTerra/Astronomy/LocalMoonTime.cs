namespace AstraTerra.Astronomy;

/// <summary>
/// The instant the moon is read at, once the sun has a longitude and the moon cannot.
/// </summary>
/// <remarks>
/// <para>
/// Vintage Story's moon is the one body AstraTerra cannot move. <c>GetMoonPosition</c> discards the
/// X it is handed and there is no moon counterpart to <c>OnGetSolarSphericalCoords</c>, so a world
/// with the longitude-aware sun installed would otherwise leave the moon on universal time while
/// the sun, the daylight and the whole star field moved off it -- a full moon rising at noon a few
/// thousand blocks east, and a moon standing against the wrong constellations.
/// </para>
/// <para>
/// The wrapper's shift is a pure offset in time, so the moon at the observer's longitude is simply
/// the game's own moon read a fraction of a day later. That is only honest where AstraTerra draws
/// the moon itself: while Vintage Story is drawing its own disc, the mod keeps measuring the body
/// the game actually puts in the sky.
/// </para>
/// </remarks>
public static class LocalMoonTime
{
    /// <param name="astraTerraDrawsTheMoon">
    /// True while <c>MoonDiscRenderer</c> owns the disc, which is what makes moving it legitimate.
    /// </param>
    public static double MoonTotalDays(
        double totalDays,
        double longitudeDegrees,
        bool astraTerraDrawsTheMoon)
        => astraTerraDrawsTheMoon ? totalDays + (longitudeDegrees / 360.0) : totalDays;
}
