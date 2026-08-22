using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace AstraTerra.Observation;

/// <summary>
/// Where an astrolabe keeps the latitude its plate was cut for.
/// </summary>
/// <remarks>
/// Itemstack attributes, for the same reasons the constellation journal uses them: they persist in
/// the save, they travel with the object through chests and hands and deaths, and the server syncs
/// them to every client that can see the stack. The plate belongs to the instrument, not to the
/// player holding it, so a borrowed astrolabe is still cut for someone else's sky.
/// <para>
/// An astrolabe with no plate attribute is uncalibrated rather than calibrated to the equator.
/// Zero is a real latitude, so absence has to be its own state — which is also what every astrolabe
/// that existed before this feature reads as, and what a freshly crafted one reads as.
/// </para>
/// </remarks>
public static class AstrolabeCalibrationStore
{
    public const string LatitudeAttribute = "astraterraPlateLatitude";

    /// <summary>The world day the plate was cut, kept for the HUD rather than for any calculation.</summary>
    public const string DayAttribute = "astraterraPlateDay";

    public static bool IsCalibrated(ITreeAttribute? attributes)
        => attributes?.HasAttribute(LatitudeAttribute) == true;

    public static bool IsCalibrated(ItemStack? stack)
        => IsCalibrated(stack?.Attributes);

    public static double? ReadLatitude(ITreeAttribute? attributes)
        => IsCalibrated(attributes) ? attributes!.GetDouble(LatitudeAttribute) : null;

    public static double? ReadLatitude(ItemStack? stack)
        => ReadLatitude(stack?.Attributes);

    public static double? ReadDay(ITreeAttribute? attributes)
        => attributes?.HasAttribute(DayAttribute) == true ? attributes.GetDouble(DayAttribute) : null;

    public static double? ReadDay(ItemStack? stack)
        => ReadDay(stack?.Attributes);

    /// <summary>
    /// Cuts a new plate, replacing whatever the instrument was carrying.
    /// </summary>
    /// <remarks>
    /// Clamped because latitude is bounded and a bad value would silently break every reading taken
    /// from it: <see cref="Astronomy.CelestialMath"/> takes a sine of it, so a nonsense latitude
    /// produces a nonsense horizon rather than an error anyone could trace.
    /// </remarks>
    public static void Write(ITreeAttribute attributes, double latitudeDeg, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        attributes.SetDouble(LatitudeAttribute, Math.Clamp(latitudeDeg, -90.0, 90.0));
        attributes.SetDouble(DayAttribute, Math.Max(0.0, totalDays));
    }

    public static void Write(ItemStack stack, double latitudeDeg, double totalDays)
    {
        ArgumentNullException.ThrowIfNull(stack);

        Write(stack.Attributes, latitudeDeg, totalDays);
    }
}
