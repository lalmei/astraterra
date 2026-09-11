using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>
/// How finely an instrument reads, and what a reading looks like once it has been read that finely.
/// </summary>
/// <remarks>
/// Imprecision is modelled by reading to fewer digits, never by adding noise. A crude instrument
/// finds everything a fine one does; it just takes more nights to see the same displacement, and
/// every reading it gives is the true angle with its tail cut off. That keeps a bad forecast
/// traceable to thin data rather than to bad luck, and it is how an instrument with a scale on it
/// actually behaves: you read the last mark the pointer has passed.
/// </remarks>
public static class InstrumentResolution
{
    /// <summary>A cross-staff or kamal: a stick, a knotted cord, and about a degree.</summary>
    public const double CrossStaffDeg = 1.0;

    /// <summary>A backstaff or quadrant, in wood and brass.</summary>
    public const double QuadrantDeg = 0.25;

    /// <summary>The brass sextant with a vernier scale: one arcminute.</summary>
    public const double BrassSextantDeg = 1.0 / 60.0;

    /// <summary>A mural quadrant, which gets its half-arcminute from being large and immovable.</summary>
    public const double MuralQuadrantDeg = 0.5 / 60.0;

    private const double ArcminuteDeg = 1.0 / 60.0;

    /// <summary>
    /// The attribute an instrument declares its scale in, in degrees. Absent on the brass sextant,
    /// which was the only instrument that measured an angle when the ladder did not exist yet.
    /// </summary>
    private const string ResolutionAttribute = "sightingResolutionDeg";

    /// <summary>
    /// The coarsest scale worth honouring. Past a quadrant of arc an instrument is not reading
    /// coarsely, it is reading nothing, and a typo in an asset should not silently do that.
    /// </summary>
    private const double CoarsestDeg = 90.0;

    /// <summary>
    /// What the instrument in hand reads to, taken from the item rather than assumed.
    /// </summary>
    /// <remarks>
    /// Falls back to the brass sextant's arcminute when the item says nothing, which is what every
    /// sextant in every existing world does: the scale was a constant before there was a ladder to
    /// climb, and an old save has no attribute to read. A value that is not a usable scale — absent,
    /// zero, negative, infinite — is the same case as saying nothing at all, because an instrument
    /// that cannot state its own limit is not evidence that the limit is different.
    /// </remarks>
    public static double OfInstrument(ItemStack? stack)
    {
        var declared = stack?.Collectible?.Attributes?[ResolutionAttribute].AsDouble(0.0) ?? 0.0;

        return double.IsFinite(declared) && declared > 0.0
            ? Math.Clamp(declared, MuralQuadrantDeg, CoarsestDeg)
            : BrassSextantDeg;
    }

    /// <summary>
    /// Cuts an angle down to what the scale can show. Truncation is toward zero, so a reading is
    /// never further from the horizon than the body was.
    /// </summary>
    public static double Truncate(double degrees, double resolutionDeg)
    {
        if (!double.IsFinite(degrees))
        {
            return degrees;
        }

        if (!double.IsFinite(resolutionDeg) || resolutionDeg <= 0.0)
        {
            return degrees;
        }

        // Snap a quotient that floating point has left a hair short of a whole mark, so that an
        // angle sitting exactly on a graduation reads as that graduation rather than the one below.
        var marks = degrees / resolutionDeg;
        var nearestMark = Math.Round(marks);
        if (Math.Abs(marks - nearestMark) < 1e-9)
        {
            marks = nearestMark;
        }

        return Math.Truncate(marks) * resolutionDeg;
    }

    /// <summary>
    /// Writes an angle to exactly the digits the instrument justifies: whole degrees on a stick,
    /// arcminutes on a vernier, tenths of an arcminute on a wall.
    /// </summary>
    public static string Format(double degrees, double resolutionDeg, bool signed = false)
    {
        var truncated = Truncate(degrees, resolutionDeg);
        var sign = truncated < 0 ? "-" : signed ? "+" : string.Empty;
        var magnitude = Math.Abs(truncated);

        if (resolutionDeg >= 1.0)
        {
            return $"{sign}{Math.Truncate(magnitude):0}°";
        }

        var wholeDegrees = Math.Truncate(magnitude);
        var arcminutes = (magnitude - wholeDegrees) * 60.0;

        return resolutionDeg >= ArcminuteDeg
            ? $"{sign}{wholeDegrees:0}° {Math.Round(arcminutes):00}′"
            : $"{sign}{wholeDegrees:0}° {arcminutes:00.0}′";
    }
}
