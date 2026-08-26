using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstraTerra.Observation;

/// <summary>
/// Where a bronze disc keeps its marks.
/// </summary>
/// <remarks>
/// Itemstack attributes, like the astrolabe's plate and the constellation book's journal: the band
/// belongs to the object, not to the player carrying it, so a disc that changes hands arrives with
/// somebody else's year of evenings already on it.
/// </remarks>
public static class SolarBandStore
{
    public const string BandJsonAttribute = "astraterraSkyDiscJson";

    public static SolarBand? Read(ITreeAttribute? attributes)
    {
        var json = attributes?.GetString(BandJsonAttribute, null);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return SolarBandPersistence.Deserialize(json);
        }
        catch
        {
            return null;
        }
    }

    public static SolarBand? Read(ItemStack? stack) => Read(stack?.Attributes);

    public static SolarBand ReadOrEmpty(ItemStack? stack) => Read(stack) ?? new SolarBand();

    public static bool HasMarks(ItemStack? stack) => Read(stack)?.Marks.Count > 0;

    public static void Write(ITreeAttribute attributes, SolarBand band)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(band);

        attributes.SetString(BandJsonAttribute, SolarBandPersistence.Serialize(band));
    }

    public static void Write(ItemStack stack, SolarBand band)
    {
        ArgumentNullException.ThrowIfNull(stack);

        Write(stack.Attributes, band);
    }
}
