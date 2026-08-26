namespace AstraTerra.Observation;

/// <summary>
/// One scratch as it appears on the disc: which rim it belongs to, where round it sits, and whether
/// it is one of the two marks the band stops at.
/// </summary>
public sealed record SkyDiscFaceMark(SolarEvent Event, double NotchDeg, bool IsEdge);

/// <summary>
/// The band as the face of the disc shows it, rather than as a sentence.
/// </summary>
/// <remarks>
/// A rim carries scratches, not entries: many evenings landing on the same notch leave one mark, and
/// the marks the eye is looking for are the two the band stops at. So the face is the distinct
/// notches of each rim, with the extremes called out — which is also the whole of what the disc
/// knows, drawn instead of said.
/// <para>
/// The extremes are marked whether or not the sun has been seen to turn back off them. Bronze cannot
/// tell a solstice from the edge of a short year of watching, and neither can the face: it shows
/// where the marks stop, and the reading in the holder's hand is what says whether that is a turning
/// point or only where the watching started.
/// </para>
/// </remarks>
public static class SkyDiscFace
{
    /// <summary>
    /// How many notches one rim will draw. A band is at most about a hundred degrees wide, so a rim
    /// cannot hold more than this at the coarsest scratch, and the cap only ever bites on a disc
    /// carrying nonsense.
    /// </summary>
    public const int MaxMarksPerRim = 64;

    /// <summary>The distinct notches of both rims, extremes flagged.</summary>
    public static IReadOnlyList<SkyDiscFaceMark> Read(SolarBand? band)
    {
        if (band is null || band.Marks.Count == 0)
        {
            return [];
        }

        var face = new List<SkyDiscFaceMark>();
        foreach (var solarEvent in new[] { SolarEvent.Sunrise, SolarEvent.Sunset })
        {
            var notches = band.Marks
                .Where(mark => mark.Event == solarEvent)
                .Select(mark => mark.NotchDeg)
                .Distinct()
                .OrderBy(notch => notch)
                .Take(MaxMarksPerRim)
                .ToList();

            if (notches.Count == 0)
            {
                continue;
            }

            var low = notches[0];
            var high = notches[^1];
            face.AddRange(notches.Select(notch =>
                new SkyDiscFaceMark(solarEvent, notch, notch == low || notch == high)));
        }

        return face;
    }

    /// <summary>
    /// A name for exactly what the face looks like, so two discs that would be scratched alike share
    /// one built model and a disc that has not changed is never rebuilt.
    /// </summary>
    public static string Key(IReadOnlyList<SkyDiscFaceMark> face)
        => face.Count == 0
            ? "blank"
            : string.Join(
                ",",
                face.Select(mark => $"{(mark.Event == SolarEvent.Sunset ? 's' : 'r')}{mark.NotchDeg:0.###}{(mark.IsEdge ? "e" : string.Empty)}"));
}
