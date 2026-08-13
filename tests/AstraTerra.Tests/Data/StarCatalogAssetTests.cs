using System.Text.Json;
using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class StarCatalogAssetTests
{
    [Fact]
    public void StarCatalog_Has_All_Magnitude_Six_Stars_And_Required_Extras()
    {
        using var stream = File.OpenRead("assets/astraterra/data/star-catalog.v1.json");
        using var document = JsonDocument.Parse(stream);
        var stars = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(5044, stars.Count);
        Assert.Equal(2, stars.Count(star => star.GetProperty("visualMagnitude").GetDouble() > 6.0));
    }

    [Fact]
    public void StarCatalog_Has_Unique_HipIds_And_Finite_Coordinates()
    {
        using var stream = File.OpenRead("assets/astraterra/data/star-catalog.v1.json");
        using var document = JsonDocument.Parse(stream);
        var stars = document.RootElement.EnumerateArray().ToList();
        var ids = stars.Select(e => e.GetProperty("hip").GetInt32()).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());

        foreach (var star in stars)
        {
            AssertFinite(star.GetProperty("rightAscensionDeg").GetDouble());
            AssertFinite(star.GetProperty("declinationDeg").GetDouble());
            AssertFinite(star.GetProperty("visualMagnitude").GetDouble());

            if (star.TryGetProperty("bvColorIndex", out var colorIndex) && colorIndex.ValueKind != JsonValueKind.Null)
            {
                AssertFinite(colorIndex.GetDouble());
            }
        }
    }

    [Fact]
    public void StarCatalog_Marks_All_Stars_As_Guide_Stars_For_Drawing()
    {
        using var stream = File.OpenRead("assets/astraterra/data/star-catalog.v1.json");
        using var document = JsonDocument.Parse(stream);
        var stars = document.RootElement.EnumerateArray().ToList();

        Assert.NotEmpty(stars);
        Assert.All(stars, star => Assert.True(star.GetProperty("isGuideStar").GetBoolean()));
    }

    [Fact]
    public void GuideStars_Have_Multiple_Groups_And_Exist_In_The_Background_Catalog()
    {
        using var stars = JsonDocument.Parse(File.OpenRead("assets/astraterra/data/star-catalog.v1.json"));
        using var guide = JsonDocument.Parse(File.OpenRead("assets/astraterra/data/guide-stars.v1.json"));

        var starIds = stars.RootElement.EnumerateArray().Select(e => e.GetProperty("hip").GetInt32()).ToHashSet();
        var groups = guide.RootElement.EnumerateArray().ToList();

        Assert.True(groups.Count > 1);

        var missing = groups
            .SelectMany(group => group.GetProperty("hipIds").EnumerateArray().Select(x => x.GetInt32()))
            .Where(id => !starIds.Contains(id))
            .Distinct()
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void GuideStars_Include_Ursa_Minor_And_Most_Zodiac_Groups()
    {
        using var guide = JsonDocument.Parse(File.OpenRead("assets/astraterra/data/guide-stars.v1.json"));

        var codes = guide.RootElement
            .EnumerateArray()
            .Select(group => group.GetProperty("iauCode").GetString())
            .ToHashSet();

        foreach (var code in new[] { "UMi", "Ari", "Tau", "Gem", "Cnc", "Leo", "Vir", "Lib", "Sco", "Sgr", "Cap", "Aqr", "Psc" })
        {
            Assert.Contains(code, codes);
        }
    }

    [Fact]
    public void ModernIauSkyCulture_Has_Constellation_Line_Data()
    {
        using var skyCulture = JsonDocument.Parse(File.OpenRead("assets/astraterra/data/sky-cultures/modern-iau.constellations.v1.json"));
        var root = skyCulture.RootElement;
        var constellations = root.GetProperty("constellations").EnumerateArray().ToList();

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("modern_iau", root.GetProperty("id").GetString());
        Assert.Equal(88, constellations.Count);

        var orion = constellations.Single(constellation => constellation.GetProperty("iauCode").GetString() == "Ori");
        Assert.Equal("Orion", orion.GetProperty("displayName").GetString());
        Assert.Contains(
            orion.GetProperty("lines").EnumerateArray(),
            line => line.EnumerateArray().Select(point => point.GetInt32()).Contains(27989));

        foreach (var constellation in constellations)
        {
            Assert.False(string.IsNullOrWhiteSpace(constellation.GetProperty("iauCode").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(constellation.GetProperty("displayName").GetString()));
            Assert.All(
                constellation.GetProperty("lines").EnumerateArray(),
                line => Assert.True(line.GetArrayLength() >= 2));
        }
    }

    [Fact]
    public void ModernIauSkyCulture_Builds_A_Complete_Admin_Star_Catalog()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var stars = JsonSerializer.Deserialize<StarCatalogEntry[]>(
            File.ReadAllText("assets/astraterra/data/star-catalog.v1.json"),
            options);
        var culture = JsonSerializer.Deserialize<SkyCultureConstellationSet>(
            File.ReadAllText("assets/astraterra/data/sky-cultures/modern-iau.constellations.v1.json"),
            options);
        Assert.NotNull(stars);
        Assert.NotNull(culture);

        var missingLineStars = culture.Constellations
            .SelectMany(constellation => constellation.Lines)
            .SelectMany(line => line)
            .Distinct()
            .Except(stars.Select(star => star.Hip))
            .ToList();
        Assert.Empty(missingLineStars);

        var journal = StarCatalogJournalBuilder.Build(new StarCatalog(stars, [], [culture]));

        Assert.Equal(88, journal.Constellations.Count);
        Assert.Equal(88, journal.Constellations.Select(record => record.Name).Distinct().Count());
        Assert.All(journal.Constellations, record => Assert.NotEmpty(record.Edges));

        var zodiac = StarCatalogJournalBuilder.BuildZodiac(new StarCatalog(stars, [], [culture]));
        Assert.Equal(
            ["Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo", "Libra", "Scorpius", "Sagittarius", "Capricornus", "Aquarius", "Pisces"],
            zodiac.Constellations.OrderBy(record => record.Id).Select(record => record.Name));
        Assert.All(zodiac.Constellations, record => Assert.NotEmpty(record.Edges));
    }

    [Fact]
    public void SkyCultureManifest_Registers_ModernIau()
    {
        using var manifest = JsonDocument.Parse(File.OpenRead("assets/astraterra/data/sky-cultures.v1.json"));
        var root = manifest.RootElement;
        var paths = root.GetProperty("skyCultureAssetPaths")
            .EnumerateArray()
            .Select(path => path.GetString())
            .ToList();

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Contains("astraterra:data/sky-cultures/modern-iau.constellations.v1.json", paths);
    }

    [Fact]
    public void DeepSkyCatalog_Has_Telescope_Texture_Objects()
    {
        using var deepSky = JsonDocument.Parse(File.OpenRead("assets/astraterra/data/deep-sky.v1.json"));
        var objects = deepSky.RootElement.EnumerateArray().ToList();

        Assert.Equal(50, objects.Count);
        Assert.Contains(objects, entry => entry.GetProperty("id").GetString() == "M45");
        Assert.Contains(objects, entry => entry.GetProperty("id").GetString() == "M1");
        Assert.Contains(objects, entry => entry.GetProperty("id").GetString() == "NGC5128");
        Assert.Equal(
            objects.Count,
            objects.Select(entry => entry.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            objects.Count,
            objects.Select(entry => entry.GetProperty("texturePath").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(
            objects,
            entry =>
            {
                Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("displayName").GetString()));
                AssertFinite(entry.GetProperty("rightAscensionDeg").GetDouble());
                AssertFinite(entry.GetProperty("declinationDeg").GetDouble());
                Assert.InRange(entry.GetProperty("angularSizeDeg").GetDouble(), 0.1, 8.0);
                var worldCoords = entry.GetProperty("worldCoords").EnumerateArray().ToList();
                Assert.Equal(4, worldCoords.Count);
                Assert.All(
                    worldCoords,
                    corner =>
                    {
                        AssertFinite(corner.GetProperty("rightAscensionDeg").GetDouble());
                        AssertFinite(corner.GetProperty("declinationDeg").GetDouble());
                    });
                Assert.InRange(entry.GetProperty("brightness").GetDouble(), 0.0, 1.0);
                Assert.InRange(entry.GetProperty("tintR").GetDouble(), 0.0, 1.0);
                Assert.InRange(entry.GetProperty("tintG").GetDouble(), 0.0, 1.0);
                Assert.InRange(entry.GetProperty("tintB").GetDouble(), 0.0, 1.0);
                var texturePath = entry.GetProperty("texturePath").GetString();
                Assert.StartsWith("astraterra:environment/deep-sky/stellarium/", texturePath);
                var textureName = texturePath!["astraterra:environment/deep-sky/stellarium/".Length..];
                Assert.True(
                    File.Exists(Path.Combine("assets/astraterra/textures/environment/deep-sky/stellarium", textureName + ".png")),
                    $"Deep-sky texture is missing for {entry.GetProperty("id").GetString()}: {textureName}.png");
                var fallbacks = entry.GetProperty("fallbackTexturePaths").EnumerateArray().Select(path => path.GetString()).ToList();
                Assert.Contains("astraterra:environment/deep-sky-cloud", fallbacks);
                Assert.DoesNotContain(
                    fallbacks,
                    path => path?.StartsWith("astraterra:environment/deep-sky/local-fallback/", StringComparison.Ordinal) == true);
            });
    }

    [Fact]
    public void DeepSkyCatalog_Deserializes_Into_The_Shape_The_Game_Loads()
    {
        // The renderer reaches the asset through this type, not through a JsonDocument, so the
        // property names on the entry and on its shared EquatorialCoordinates corners have to keep
        // matching the shipped file.
        var entries = JsonSerializer.Deserialize<DeepSkyObjectEntry[]>(
            File.ReadAllText("assets/astraterra/data/deep-sky.v1.json"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.NotNull(entries);
        var pleiades = Assert.Single(entries!, entry => entry.Id == "M45");
        Assert.Equal(56.75, pleiades.RightAscensionDeg, 4);
        Assert.Equal(4, pleiades.WorldCoords.Count);
        Assert.All(
            entries!,
            entry => Assert.All(
                entry.WorldCoords,
                corner =>
                {
                    AssertFinite(corner.RightAscensionDeg);
                    AssertFinite(corner.DeclinationDeg);
                    Assert.NotEqual(default, corner);
                }));
    }

    private static void AssertFinite(double value)
    {
        Assert.False(double.IsNaN(value));
        Assert.False(double.IsInfinity(value));
    }
}
