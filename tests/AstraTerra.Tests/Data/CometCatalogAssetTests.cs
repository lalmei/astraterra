using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Data;

public sealed class CometCatalogAssetTests
{
    private const string AssetPath = "assets/astraterra/data/comets.v1.json";

    /// <summary>
    /// Real orbital periods, in years, for the comets the catalog ships. The tracks and the
    /// magnitudes are authored — see <see cref="CometEntry"/> — but the periods are facts, and a
    /// hand edit that quietly makes Halley a decadal visitor should have to be deliberate.
    /// </summary>
    public static TheoryData<string, double> PublishedPeriods => new()
    {
        { "halley", 75.32 },
        { "tempel-tuttle", 33.24 },
        { "tuttle", 13.61 },
        { "machholz", 5.28 },
    };

    [Theory]
    [MemberData(nameof(PublishedPeriods))]
    public void CometCatalog_Keeps_The_Published_Period(string id, double periodYears)
    {
        var comet = Load().Comets.Single(entry => entry.Id == id);

        Assert.Equal(periodYears, comet.PeriodYears, 6);
    }

    [Fact]
    public void CometCatalog_Loads_Through_The_Runtime_Loader()
    {
        var catalog = Load();

        Assert.Equal(1, catalog.SchemaVersion);
        Assert.NotEmpty(catalog.Comets);
        Assert.Equal(catalog.Comets.Count, catalog.Comets.Select(comet => comet.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(catalog.Comets.Count, catalog.Comets.Select(comet => comet.DisplayName).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_Comet_Is_Authored_Well_Enough_To_Be_Seen()
    {
        Assert.All(
            Load().Comets,
            comet =>
            {
                Assert.False(string.IsNullOrWhiteSpace(comet.Id));
                Assert.False(string.IsNullOrWhiteSpace(comet.DisplayName));

                // A window that swallows its own period would run each apparition into the next.
                Assert.InRange(comet.WindowHalfWidthYears, 0.001, comet.PeriodYears / 2.0);
                Assert.True(comet.FirstPerihelionYear >= 0);

                // Brighter at perihelion than at the edges, or it fades in as it arrives.
                Assert.True(comet.PeakMagnitude < comet.EdgeMagnitude, $"{comet.Id} does not brighten");
                Assert.InRange(comet.BrighteningExponent, 0.05, 4.0);
                Assert.InRange(comet.PeakTailLengthDeg, 0.5, 90.0);

                Assert.InRange(comet.TintR, 0f, 1f);
                Assert.InRange(comet.TintG, 0f, 1f);
                Assert.InRange(comet.TintB, 0f, 1f);
            });
    }

    [Fact]
    public void Every_Track_Spans_The_Whole_Apparition()
    {
        Assert.All(
            Load().Comets,
            comet =>
            {
                Assert.NotEmpty(comet.Path);
                Assert.All(
                    comet.Path,
                    keyframe =>
                    {
                        Assert.InRange(keyframe.Phase, -1.0, 1.0);
                        Assert.InRange(keyframe.RightAscensionDeg, 0.0, 360.0);
                        Assert.InRange(keyframe.DeclinationDeg, -90.0, 90.0);
                    });

                // Authored to the edges, so the comet is somewhere real the whole time it is up
                // rather than parked on a held end keyframe for the first half of its window.
                Assert.Equal(-1.0, comet.Path.Min(keyframe => keyframe.Phase), 6);
                Assert.Equal(1.0, comet.Path.Max(keyframe => keyframe.Phase), 6);
            });
    }

    /// <summary>
    /// A comet that never moves is a smudge; the track is what makes an apparition worth watching
    /// across its nights.
    /// </summary>
    [Fact]
    public void Every_Comet_Actually_Crosses_The_Sky()
    {
        Assert.All(
            Load().Comets,
            comet =>
            {
                var ephemeris = new CometEphemeris(comet, daysPerYear: 100);
                var start = ephemeris.PositionAtPhase(-1.0);
                var end = ephemeris.PositionAtPhase(1.0);

                Assert.True(
                    Math.Abs(CelestialMath.ShortestAngularDistanceDegrees(start.RightAscensionDeg, end.RightAscensionDeg)) > 5.0
                    || Math.Abs(start.DeclinationDeg - end.DeclinationDeg) > 5.0,
                    $"{comet.Id} barely moves across its apparition");
            });
    }

    /// <summary>
    /// The rarer the comet, the more it should be worth having waited for.
    /// </summary>
    [Fact]
    public void The_Rarest_Comet_Is_The_Brightest()
    {
        var comets = Load().Comets;
        var rarest = comets.MaxBy(comet => comet.PeriodYears)!;

        Assert.Equal(comets.Min(comet => comet.PeakMagnitude), rarest.PeakMagnitude);
        Assert.Equal(comets.Max(comet => comet.PeakTailLengthDeg), rarest.PeakTailLengthDeg);
    }

    private static CometCatalog Load()
        => CometCatalogLoader.Parse(File.ReadAllText(AssetPath));
}
