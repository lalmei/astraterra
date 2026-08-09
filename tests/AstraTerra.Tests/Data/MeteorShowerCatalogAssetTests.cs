using System.Text.Json;
using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class MeteorShowerCatalogAssetTests
{
    private const string AssetPath = "assets/astraterra/data/meteor-showers.v1.json";

    /// <summary>
    /// Published peak solar longitude (deg, equinox 2000.0) and ZHR for each shower, from the IMO
    /// Meteor Shower Calendar working list. Pinned here so a hand edit to the asset has to be a
    /// deliberate one.
    /// </summary>
    public static TheoryData<string, double, double> PublishedShowers => new()
    {
        { "QUA", 283.15, 110.0 },
        { "LYR", 32.32, 18.0 },
        { "ETA", 45.5, 50.0 },
        { "SDA", 125.0, 25.0 },
        { "PER", 140.0, 100.0 },
        { "ORI", 208.0, 20.0 },
        { "LEO", 235.27, 15.0 },
        { "GEM", 262.2, 150.0 },
        { "URS", 270.7, 10.0 },
    };

    [Fact]
    public void MeteorShowerCatalog_Has_The_Major_Annual_Showers()
    {
        var showers = LoadShowers();

        foreach (var id in new[] { "QUA", "LYR", "ETA", "PER", "ORI", "LEO", "GEM", "URS" })
        {
            Assert.Contains(showers, shower => shower.Id == id);
        }

        Assert.Equal(showers.Count, showers.Select(shower => shower.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(showers.Count, showers.Select(shower => shower.DisplayName).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [MemberData(nameof(PublishedShowers))]
    public void MeteorShowerCatalog_Keeps_The_Published_Peak_And_Rate(string id, double peakSolarLongitudeDeg, double peakZenithHourlyRate)
    {
        var shower = LoadShowers().Single(entry => entry.Id == id);

        Assert.Equal(peakSolarLongitudeDeg, shower.PeakSolarLongitudeDeg, 6);
        Assert.Equal(peakZenithHourlyRate, shower.PeakZenithHourlyRate, 6);
    }

    [Fact]
    public void MeteorShowerCatalog_Has_Usable_Radiants_And_Windows()
    {
        var showers = LoadShowers();

        Assert.NotEmpty(showers);
        Assert.All(
            showers,
            shower =>
            {
                Assert.False(string.IsNullOrWhiteSpace(shower.Id));
                Assert.False(string.IsNullOrWhiteSpace(shower.DisplayName));
                Assert.InRange(shower.RightAscensionDeg, 0.0, 360.0);
                Assert.InRange(shower.DeclinationDeg, -90.0, 90.0);
                Assert.InRange(shower.PeakSolarLongitudeDeg, 0.0, 360.0);

                // A window wider than half a turn has no outside, and one of zero width never fires.
                Assert.InRange(shower.WindowHalfWidthDeg, 0.1, 180.0);
                Assert.InRange(shower.PeakZenithHourlyRate, 1.0, 1000.0);
            });
    }

    [Fact]
    public void MeteorShowerCatalog_Spreads_Its_Peaks_Across_The_Whole_Year()
    {
        var seasons = LoadShowers()
            .Select(shower => (int)Math.Floor(shower.PeakSolarLongitudeDeg / 90.0) % 4)
            .Distinct()
            .ToList();

        Assert.Equal(4, seasons.Count);
    }

    [Fact]
    public void MeteorShowerCatalog_Peaks_Are_Far_Enough_Apart_To_Tell_Showers_Apart()
    {
        var showers = LoadShowers();

        foreach (var shower in showers)
        {
            foreach (var other in showers.Where(entry => entry.Id != shower.Id))
            {
                var separationDeg = Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(
                    shower.PeakSolarLongitudeDeg,
                    other.PeakSolarLongitudeDeg));

                Assert.True(
                    separationDeg > 1.0,
                    $"{shower.Id} and {other.Id} peak {separationDeg:0.##} deg apart, which reads as one shower.");
            }
        }
    }

    [Fact]
    public void MeteorShowerCatalog_Deserializes_Through_The_Runtime_Loader()
    {
        using var document = JsonDocument.Parse(File.OpenRead(AssetPath));
        var rawCount = document.RootElement.EnumerateArray().Count();

        var showers = LoadShowers();

        Assert.Equal(rawCount, showers.Count);
        Assert.All(
            showers,
            shower =>
            {
                // A property the loader failed to bind would silently read as zero.
                Assert.NotEqual(0.0, shower.PeakZenithHourlyRate);
                Assert.NotEqual(0.0, shower.WindowHalfWidthDeg);
                Assert.NotEqual(0.0, shower.RightAscensionDeg);
            });
    }

    private static IReadOnlyList<MeteorShowerEntry> LoadShowers()
        => MeteorShowerCatalogLoader.Parse(File.ReadAllText(AssetPath));
}
