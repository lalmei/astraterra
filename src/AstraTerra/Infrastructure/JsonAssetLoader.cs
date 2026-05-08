using System.Reflection;
using System.Text.Json;
using Vintagestory.API.Common;

namespace AstraTerra.Infrastructure;

public static class JsonAssetLoader
{
    public static T LoadJson<T>(ICoreAPI api, string assetPath, JsonSerializerOptions? options = null)
    {
        var asset = ResolveAsset(api, assetPath);
        if (asset is not null)
        {
            return JsonSerializer.Deserialize<T>(asset.ToText(), options)
                   ?? throw new InvalidOperationException($"Asset JSON was empty: {assetPath}");
        }

        var filePath = ResolveFilePath(assetPath);
        if (filePath is not null)
        {
            using var stream = File.OpenRead(filePath);
            return JsonSerializer.Deserialize<T>(stream, options)
                   ?? throw new InvalidOperationException($"Asset JSON was empty: {assetPath}");
        }

        throw new FileNotFoundException($"Asset not found: {assetPath}. Tried asset ids: {string.Join(", ", CandidateAssetPaths(assetPath))}. Tried files: {string.Join(", ", CandidateFilePaths(assetPath))}");
    }

    internal static IReadOnlyList<string> CandidateAssetPaths(string assetPath)
    {
        if (assetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return [assetPath, assetPath[..^".json".Length]];
        }

        return [assetPath, $"{assetPath}.json"];
    }

    private static IAsset? ResolveAsset(ICoreAPI api, string assetPath)
    {
        foreach (var candidate in CandidateAssetPaths(assetPath))
        {
            var asset = api.Assets.TryGet(new AssetLocation(candidate), loadAsset: true);
            if (asset is not null)
            {
                return asset;
            }
        }

        return null;
    }

    private static string? ResolveFilePath(string assetPath)
    {
        foreach (var path in CandidateFilePaths(assetPath))
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    internal static IReadOnlyList<string> CandidateFilePaths(string assetPath)
    {
        var relativePaths = CandidateAssetPaths(assetPath)
            .Select(ToAssetRelativePath)
            .Where(path => path is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roots = new[]
            {
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                AppContext.BaseDirectory,
                Directory.GetCurrentDirectory()
            }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase);

        return roots
            .SelectMany(root => relativePaths.Select(relative => Path.Combine(root, relative)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? ToAssetRelativePath(string assetPath)
    {
        var split = assetPath.Split(':', 2);
        if (split.Length != 2 || string.IsNullOrWhiteSpace(split[0]) || string.IsNullOrWhiteSpace(split[1]))
        {
            return null;
        }

        return Path.Combine("assets", split[0], split[1].Replace('/', Path.DirectorySeparatorChar));
    }
}
