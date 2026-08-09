using System.Text.Json;
using AstraTerra.Infrastructure;
using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

public static class MeteorShowerCatalogLoader
{
    public const string DefaultAssetPath = "astraterra:data/meteor-showers.v1.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<MeteorShowerEntry> Load(ICoreAPI api, string assetPath = DefaultAssetPath)
        => JsonAssetLoader.LoadJson<MeteorShowerEntry[]>(api, assetPath, Options);

    /// <summary>
    /// Reads catalog JSON without going through the asset system, so the shape of the shipped file
    /// can be checked without a running game.
    /// </summary>
    public static IReadOnlyList<MeteorShowerEntry> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        return JsonSerializer.Deserialize<MeteorShowerEntry[]>(json, Options)
               ?? throw new InvalidOperationException("Meteor shower catalog JSON was empty.");
    }
}
