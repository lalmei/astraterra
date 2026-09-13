namespace AstraTerra.Constellations;

/// <summary>
/// The prose the prepared books open with.
/// </summary>
/// <remarks>
/// A prepared book is somebody else's finished work, and finished work written by an observer has an
/// observer's voice in it. These are written as the notes of someone who did the watching a long time
/// ago and is handing over what they concluded — which is what the book is, mechanically: a journal
/// arriving already full, from a hand that is not the player's.
/// <para>
/// They describe only what this sky actually does. The sun here rides a real ecliptic — its longitude
/// advances through the year and is rotated into the equatorial frame the catalog's stars live in —
/// so a preface that tells a reader to count along the zodiac to find the sun is describing something
/// they can go outside and check, not decoration.
/// </para>
/// </remarks>
public static class PreparedBookPrefaces
{
    /// <summary>
    /// Written as one paragraph to a line on purpose: the book's own page wraps text to whatever
    /// width it is drawn at, and prose broken by hand arrives broken twice.
    /// </summary>
    public const string Zodiac =
        """
        Twelve figures, and one road between them.

        The sun does not stand still among the stars. It walks eastward through them, a little each day, and in a year it comes back to where it began. The twelve figures written out here are the ones it walks through. That road is the whole reason they are set down together; they have nothing else in common, and are no finer or brighter than figures I have left out.

        You will never see the figure the sun is standing in. It is up when the sun is up, and the daylight drowns it. But the figure that sets after the sun, low in the west, stands one pace ahead of it along the road, and the one that rises before the dawn stands one pace behind. From either, count along the road, and you have the sun's place among the stars without ever having seen it there.

        Knowing that place is knowing the season, and knowing the season is knowing where on the horizon the sun will come up and go down tomorrow. In these northern countries of mine it rises and sets far towards the north in summer and far towards the south in winter, and due east and due west at the two turnings between. Carry this book south of the line the sun stands over at noon and you will find summer and winter have changed places on you; the road is the same road, and only your standing on it has moved.

        There are those who take it further. They hold that the figure the sun stood in on the day a child is born belongs to that child ever after, and will tell you what the child is to become. I set it down because you will be asked about it, and because it is honest to say what a thing is put to. It is not what I measured. What I measured is the road, and the road is what these pages are.
        """;
}
