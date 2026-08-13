using System.Text.Json;
using AstraTerra.Infrastructure;
using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

public static class PlanetCatalogLoader
{
    public const string DefaultAssetPath = "astraterra:data/planets.v1.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static PlanetCatalog Load(ICoreAPI api, string assetPath = DefaultAssetPath)
        => JsonAssetLoader.LoadJson<PlanetCatalog>(api, assetPath, Options);

    /// <summary>
    /// Reads catalog JSON without going through the asset system, so the shape of the shipped file
    /// can be checked without a running game.
    /// </summary>
    public static PlanetCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<PlanetCatalog>(json, Options)
               ?? throw new InvalidOperationException("Planet catalog JSON was empty.");
    }
}
