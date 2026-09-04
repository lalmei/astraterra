using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Vintagestory.API.Common;

namespace AstraTerra.Observation;

/// <summary>
/// A disc that was somebody else's: dug out of the ground with a year already on it.
/// </summary>
/// <remarks>
/// Loot tables hand out a fixed itemstack, so what a found disc carries cannot be written into the
/// drop. The drop carries a mark instead, and the first time a server sees a disc wearing that mark
/// it fills the disc in: a whole year of sunrises and sunsets at a latitude that is not the finder's,
/// and one constellation out of the sky the maker had. The mark is struck off in the same breath, so
/// this happens once in the life of the object and never again.
/// <para>
/// The band is bound where it was scribed, which is the point of the object rather than an oversight.
/// A found disc reads out a latitude that is not yours — you learn how far from the equator somebody
/// else lived — and it refuses to take your marks, because the sun does not set where it was made.
/// It is a record, not a head start.
/// </para>
/// </remarks>
public static class FoundSkyDisc
{
    /// <summary>What a loot table writes on a disc to say the disc has a history.</summary>
    public const string MarkerAttribute = "astraterraFoundDisc";

    /// <summary>
    /// How much further than the disc's own tolerance the maker must have lived.
    /// </summary>
    /// <remarks>
    /// A found disc that happened to be scribed within its own drift of the finder would quietly
    /// accept their marks and stop being a record of anywhere. Well clear of the tolerance, so that
    /// what the rim says is unambiguously somebody else's.
    /// </remarks>
    private const double ElsewhereMultiple = 4.0;

    private static StarCatalog? catalog;

    /// <summary>The sky the maker is allowed to have engraved from. Both sides; only the server uses it.</summary>
    public static void Install(StarCatalog? starCatalog) => catalog = starCatalog;

    public static void Reset() => catalog = null;

    /// <summary>Whether this disc has a history that has not been filled in yet.</summary>
    public static bool IsUnfilled(ItemStack? stack)
        => !string.IsNullOrEmpty(stack?.Attributes?.GetString(MarkerAttribute, null));

    /// <summary>
    /// Fills in a found disc, once, on the server.
    /// </summary>
    /// <param name="finderLatitudeDeg">
    /// Where the disc was dug up, which decides only what the maker's latitude may not be.
    /// </param>
    /// <returns>Whether anything was written, and therefore whether the slot wants marking dirty.</returns>
    public static bool TryFill(ItemStack? stack, IWorldAccessor? world, double finderLatitudeDeg)
    {
        if (world is null || world.Side != EnumAppSide.Server || !IsUnfilled(stack))
        {
            return false;
        }

        var notchDeg = SolarBandStore.NotchOf(stack);
        var latitudeDeg = MakerLatitude(world.Rand, finderLatitudeDeg, notchDeg);
        var band = FoundSkyDiscBand.Scribe(
            latitudeDeg,
            notchDeg,
            Math.Max(1, world.Calendar?.DaysPerYear ?? 0),
            // The year it records ran out before this world had a first day. Nothing about the disc
            // is in living memory, which is the one thing every found object has in common.
            lastDay: -1);

        if (band is null)
        {
            return false;
        }

        SolarBandStore.Write(stack!, band);

        // A disc with a band and no figure is a disc whose owner never engraved one, which is a
        // perfectly ordinary thing for a disc to be. Worth having even when the sky is not loaded.
        if (FoundSkyDiscFigure.Choose(catalog, latitudeDeg, world.Rand) is { } figure)
        {
            SkyDiscFigureStore.Write(stack!, figure);
        }

        stack!.Attributes.RemoveAttribute(MarkerAttribute);
        return true;
    }

    /// <summary>
    /// A latitude the maker could have kept a year of evenings at, and the finder plainly is not at.
    /// </summary>
    private static double MakerLatitude(Random random, double finderLatitudeDeg, double notchDeg)
    {
        var elsewhere = SolarBandPolicy.MaxDriftDegFor(notchDeg) * ElsewhereMultiple;
        var span = FoundSkyDiscBand.MaxLatitudeDeg - FoundSkyDiscBand.MinLatitudeDeg;

        for (var attempt = 0; attempt < 8; attempt++)
        {
            var magnitude = FoundSkyDiscBand.MinLatitudeDeg + (random.NextDouble() * span);
            var latitude = random.Next(2) == 0 ? magnitude : -magnitude;
            if (!double.IsFinite(finderLatitudeDeg) || Math.Abs(latitude - finderLatitudeDeg) > elsewhere)
            {
                return latitude;
            }
        }

        // Eight tries only fail near a pole the band cannot reach anyway; the far hemisphere is
        // always elsewhere.
        return finderLatitudeDeg > 0
            ? -FoundSkyDiscBand.MinLatitudeDeg
            : FoundSkyDiscBand.MinLatitudeDeg;
    }
}
