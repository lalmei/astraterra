using System.Text.Json;
using AstraTerra.Infrastructure;
using Vintagestory.API.Common;

namespace AstraTerra.Astronomy;

public sealed record StarCatalog
{
    public StarCatalog(IReadOnlyList<StarCatalogEntry> stars, IReadOnlyList<GuideStarGroup> guideGroups)
        : this(stars, guideGroups, [], [])
    {
    }

    public StarCatalog(
        IReadOnlyList<StarCatalogEntry> stars,
        IReadOnlyList<GuideStarGroup> guideGroups,
        IReadOnlyList<SkyCultureConstellationSet> skyCultures)
        : this(stars, guideGroups, skyCultures, [])
    {
    }

    public StarCatalog(
        IReadOnlyList<StarCatalogEntry> stars,
        IReadOnlyList<GuideStarGroup> guideGroups,
        IReadOnlyList<SkyCultureConstellationSet> skyCultures,
        IReadOnlyList<DeepSkyObjectEntry> deepSkyObjects)
    {
        Stars = stars;
        GuideGroups = guideGroups;
        SkyCultures = skyCultures;
        DeepSkyObjects = deepSkyObjects;
    }

    public IReadOnlyList<StarCatalogEntry> Stars { get; }

    public IReadOnlyList<GuideStarGroup> GuideGroups { get; }

    public IReadOnlyList<SkyCultureConstellationSet> SkyCultures { get; }

    public IReadOnlyList<DeepSkyObjectEntry> DeepSkyObjects { get; }
}

public static class StarCatalogLoader
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static StarCatalog Load(
        ICoreAPI api,
        string starAssetPath,
        string guideAssetPath,
        string? skyCultureManifestAssetPath = null,
        string? deepSkyAssetPath = null)
    {
        var stars = JsonAssetLoader.LoadJson<StarCatalogEntry[]>(api, starAssetPath, Options);
        var guides = JsonAssetLoader.LoadJson<GuideStarGroup[]>(api, guideAssetPath, Options);
        IReadOnlyList<string> skyCultureAssetPaths = skyCultureManifestAssetPath is null
            ? []
            : JsonAssetLoader.LoadJson<SkyCultureManifest>(api, skyCultureManifestAssetPath, Options).SkyCultureAssetPaths;
        var skyCultures = skyCultureAssetPaths
            .Select(path => JsonAssetLoader.LoadJson<SkyCultureConstellationSet>(api, path, Options))
            .ToList();
        var deepSkyObjects = deepSkyAssetPath is null
            ? []
            : JsonAssetLoader.LoadJson<DeepSkyObjectEntry[]>(api, deepSkyAssetPath, Options);
        return new StarCatalog(stars, guides, skyCultures, deepSkyObjects);
    }
}

public sealed record SkyCultureManifest(
    int SchemaVersion,
    IReadOnlyList<string> SkyCultureAssetPaths
);
