using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class HandbookAssetTests
{
    [Fact]
    public void Handbook_Pages_Declare_The_Astronomy_Category_And_Lang_Keys()
    {
        var lang = LoadLang();

        foreach (var file in HandbookPageFiles)
        {
            using var document = JsonDocument.Parse(File.ReadAllText(file));
            var root = document.RootElement;

            Assert.StartsWith("astraterra-", root.GetProperty("pageCode").GetString());
            Assert.Equal("astraterra", root.GetProperty("categoryCode").GetString());

            foreach (var property in new[] { "title", "text" })
            {
                var key = root.GetProperty(property).GetString()!;

                Assert.StartsWith("game:", key);
                Assert.True(lang.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"Missing lang entry '{key}' for {Path.GetFileName(file)}.");
            }
        }
    }

    [Fact]
    public void Lang_Names_The_Astronomy_Handbook_Tab()
    {
        Assert.Equal("Astronomy", LoadLang()["game:handbook-category-astraterra"]);
    }

    [Fact]
    public void Handbook_Page_Codes_Are_Unique()
    {
        var pageCodes = HandbookPageCodes;

        Assert.Equal(pageCodes.Count, pageCodes.Distinct().Count());
        Assert.Contains("astraterra-overview", pageCodes);
    }

    [Fact]
    public void Handbook_Links_Point_At_Pages_Or_Items_That_Exist()
    {
        var pageCodes = HandbookPageCodes.ToHashSet();
        var itemPageCodes = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "assets/astraterra/itemtypes"), "*.json")
            .Select(ReadCode)
            .Select(code => $"item-astraterra:{code}")
            .ToHashSet();

        foreach (var (key, text) in LoadLang().Where(entry => entry.Key.StartsWith("game:astraterra-handbook-")))
        {
            foreach (Match match in Regex.Matches(text, "handbook://([^\"]+)"))
            {
                var target = match.Groups[1].Value;

                Assert.True(pageCodes.Contains(target) || itemPageCodes.Contains(target),
                    $"Handbook link '{target}' in '{key}' has no target.");
            }
        }
    }

    private static Dictionary<string, string> LoadLang() =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(RepositoryRoot, "assets/astraterra/lang/en.json")))!;

    private static List<string> HandbookPageCodes =>
        HandbookPageFiles.Select(ReadPageCode).ToList();

    private static string ReadPageCode(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));

        return document.RootElement.GetProperty("pageCode").GetString()!;
    }

    private static string ReadCode(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(file));

        return document.RootElement.GetProperty("code").GetString()!;
    }

    private static IEnumerable<string> HandbookPageFiles =>
        Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "assets/astraterra/config/handbook"), "*.json")
            .OrderBy(file => file);

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
