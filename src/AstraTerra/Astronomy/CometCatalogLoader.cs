using System.Text.Json;
using AstraTerra.Infrastructure;
using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

public static class CometCatalogLoader
{
    public const string DefaultAssetPath = "astraterra:data/comets.v1.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static CometCatalog Load(ICoreAPI api, string assetPath = DefaultAssetPath)
        => Validate(JsonAssetLoader.LoadJson<CometCatalog>(api, assetPath, Options));

    /// <summary>
    /// Reads catalog JSON without going through the asset system, so the shape of the shipped file
    /// can be checked without a running game.
    /// </summary>
    public static CometCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return Validate(
            JsonSerializer.Deserialize<CometCatalog>(json, Options)
            ?? throw new InvalidOperationException("Comet catalog JSON was empty."));
    }

    /// <summary>
    /// Rejects a catalog that would fail silently rather than loudly.
    /// </summary>
    /// <remarks>
    /// Every one of these produces a comet that is simply never seen, which on a body that shows up
    /// once every thirteen years is indistinguishable from it not being due yet. A server owner who
    /// authored a comet and waited a world decade for nothing deserves the error at load.
    /// </remarks>
    private static CometCatalog Validate(CometCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        foreach (var comet in catalog.Comets)
        {
            if (comet.PeriodYears <= 0)
            {
                throw new InvalidOperationException($"Comet '{comet.Id}' has a period of {comet.PeriodYears} world years.");
            }

            if (comet.WindowHalfWidthYears <= 0)
            {
                throw new InvalidOperationException($"Comet '{comet.Id}' is never visible: its window has no width.");
            }

            // Overlapping windows would make one apparition run into the next, and the phase maths
            // measures against the nearest passage only.
            if (comet.WindowHalfWidthYears * 2.0 >= comet.PeriodYears)
            {
                throw new InvalidOperationException(
                    $"Comet '{comet.Id}' is visible for {comet.WindowHalfWidthYears * 2.0} of its {comet.PeriodYears} world year period, which would run each apparition into the next.");
            }

            if (comet.Path is null || comet.Path.Count == 0)
            {
                throw new InvalidOperationException($"Comet '{comet.Id}' has no authored track.");
            }
        }

        return catalog;
    }
}
