using System.Text.Json;
using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class FoundSkyDiscTests
{
    private const double BronzeNotchDeg = 2.5;
    private const int DaysPerYear = 360;

    [Theory]
    [InlineData(20.0)]
    [InlineData(25.0)]
    [InlineData(45.0)]
    [InlineData(58.0)]
    [InlineData(-45.0)]
    public void A_Found_Disc_Carries_A_Finished_Year(double latitudeDeg)
    {
        var band = FoundSkyDiscBand.Scribe(latitudeDeg, BronzeNotchDeg, DaysPerYear, lastDay: -1);

        Assert.NotNull(band);

        foreach (var solarEvent in new[] { SolarEvent.Sunrise, SolarEvent.Sunset })
        {
            var reading = band!.Read(solarEvent);

            // Both ends found and both seen to turn back. Anything less and the disc is a year of
            // somebody's evenings that never finished, which is not what was buried.
            Assert.True(reading.IsComplete, $"{solarEvent} arc did not close at {latitudeDeg}°.");

            // The latitude is the whole payload: the swing of the band is what a disc knows. Read
            // off a rim of two-and-a-half degree notches it is worth a few degrees either way — the
            // same few degrees a player's own year of scratches is worth, since it is the same rim.
            Assert.NotNull(reading.LatitudeDeg);
            Assert.InRange(reading.LatitudeDeg!.Value, Math.Abs(latitudeDeg) - 6.0, Math.Abs(latitudeDeg) + 6.0);

            // And the year, to within the days a coarse rim cannot separate.
            Assert.NotNull(reading.YearDays);
            Assert.InRange(reading.YearDays!.Value, DaysPerYear * 0.94, DaysPerYear * 1.06);
        }
    }

    [Fact]
    public void The_Middle_Of_Each_Arc_Is_A_Cardinal_Direction()
    {
        var band = FoundSkyDiscBand.Scribe(45.0, BronzeNotchDeg, DaysPerYear, lastDay: -1);

        // The same claim the disc makes about a band its owner scratched: halfway between midsummer
        // and midwinter is due west on the sunset rim and due east on the sunrise one. A found disc
        // that failed this would be describing a different planet.
        Assert.InRange(band!.Read(SolarEvent.Sunset).CardinalNotchDeg!.Value, 268.0, 272.0);
        Assert.InRange(band.Read(SolarEvent.Sunrise).CardinalNotchDeg!.Value, 88.0, 92.0);
    }

    [Fact]
    public void A_Found_Rim_Is_Marked_Like_An_Object_And_Not_Like_A_Log()
    {
        var band = FoundSkyDiscBand.Scribe(45.0, BronzeNotchDeg, DaysPerYear, lastDay: -1);
        var marks = band!.Marks;

        // A mark per notch moved rather than per evening: enough to be a year of watching, few
        // enough to travel in an itemstack. A rim of a thousand scratches is a log book, and no
        // disc has room for one.
        Assert.InRange(marks.Count, 40, 400);

        // Every mark is on this rim's own graduations.
        Assert.All(marks, mark => Assert.Equal(0.0, mark.NotchDeg % BronzeNotchDeg, 6));

        // And the whole record is in the past, before this world's first day.
        Assert.All(marks, mark => Assert.True(mark.Day < 0));
    }

    [Fact]
    public void A_Found_Rim_Fits_In_An_Itemstack()
    {
        var band = FoundSkyDiscBand.Scribe(45.0, BronzeNotchDeg, DaysPerYear, lastDay: -1);
        var json = SolarBandPersistence.Serialize(band!);

        // The band travels in the stack's own attributes, which are saved with the world and sent to
        // every client that sees the disc. A year of marks is worth that; a log book would not be.
        Assert.True(json.Length < 24_000, $"A found rim serialised to {json.Length} characters.");

        // And it survives the trip unchanged, which is the only reason any of it is worth writing.
        Assert.Equal(band!.Marks.Count, SolarBandPersistence.Deserialize(json).Marks.Count);
    }

    [Fact]
    public void A_Found_Disc_Will_Not_Take_The_Finders_Marks()
    {
        var band = FoundSkyDiscBand.Scribe(50.0, BronzeNotchDeg, DaysPerYear, lastDay: -1);

        // The point of the object: it is bound where it was scribed, so a finder who is not there
        // is told so rather than quietly adding their evening to somebody else's year.
        var refused = band!.Scratch(day: 4, azimuthDeg: 270.0, SolarEvent.Sunset, latitudeDeg: 10.0, x: 0, z: 0);
        Assert.Equal(SolarMarkOutcome.WrongPlace, refused.Outcome);

        // Carried home to where it was made, it is still a working disc.
        var taken = band.Scratch(day: 4, azimuthDeg: 270.0, SolarEvent.Sunset, latitudeDeg: 50.0, x: 0, z: 0);
        Assert.Equal(SolarMarkOutcome.Scratched, taken.Outcome);
    }

    [Fact]
    public void A_Latitude_Without_A_Band_Scribes_Nothing()
    {
        // Inside the polar circles the sun stops setting for part of the year. There is no band to
        // find there, so there is no disc to find either.
        Assert.Null(FoundSkyDiscBand.Scribe(89.0, BronzeNotchDeg, DaysPerYear, lastDay: -1));
        Assert.Null(FoundSkyDiscBand.Scribe(45.0, BronzeNotchDeg, daysPerYear: 0, lastDay: -1));
    }

    [Fact]
    public void The_Maker_Engraved_One_Joined_Up_Figure_They_Could_See()
    {
        var catalog = LoadCatalog();
        var candidates = FoundSkyDiscFigure.Candidates(catalog, 45.0);

        Assert.NotEmpty(candidates);

        foreach (var figure in candidates)
        {
            // One figure, joined up: the same rule the disc enforces on its owner.
            Assert.Single(ConstellationGraph.SplitConnectedComponents(figure.Edges));
            Assert.True(figure.Edges.Count >= 3);

            // And a figure whose stars climb out of the horizon murk where the band was scribed.
            foreach (var star in figure.Stars)
            {
                var declination = catalog.Stars.Single(entry => entry.Hip == star).DeclinationDeg;
                Assert.True(
                    90.0 - Math.Abs(45.0 - declination) >= 15.0,
                    $"HIP {star} never clears the horizon at 45°.");
            }
        }
    }

    [Fact]
    public void The_Makers_Sky_Is_The_Makers_Own_Hemisphere()
    {
        var catalog = LoadCatalog();
        var northern = FoundSkyDiscFigure.Candidates(catalog, 55.0).Count;
        var southern = FoundSkyDiscFigure.Candidates(catalog, -55.0).Count;

        Assert.True(northern > 10);
        Assert.True(southern > 10);

        // Neither maker could have engraved the other's pole. Crux at 55° north and Ursa Minor at
        // 55° south are each below somebody's horizon for good.
        Assert.DoesNotContain(FoundSkyDiscFigure.Candidates(catalog, 55.0), figure => figure.Stars.Contains(60718));
        Assert.DoesNotContain(FoundSkyDiscFigure.Candidates(catalog, -55.0), figure => figure.Stars.Contains(11767));
    }

    [Fact]
    public void A_Found_Figure_Is_One_Of_The_Skys_Own()
    {
        var catalog = LoadCatalog();
        var chosen = FoundSkyDiscFigure.Choose(catalog, 45.0, new Random(1337));

        Assert.NotNull(chosen);
        Assert.Contains(
            FoundSkyDiscFigure.Candidates(catalog, 45.0),
            candidate => candidate.Edges.Count == chosen!.Edges.Count
                         && candidate.Stars.ToHashSet().SetEquals(chosen.Stars));
    }

    [Fact]
    public void No_Sky_Still_Leaves_A_Disc_With_Its_Year()
    {
        // A disc whose owner never engraved a figure is an ordinary disc. Losing the catalog must
        // cost the figure and nothing else.
        Assert.Null(FoundSkyDiscFigure.Choose(null, 45.0, new Random(1)));
    }

    [Fact]
    public void Bony_Soil_Gives_Up_A_Disc_With_A_History()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText("assets/astraterra/patches/pan-sky-disc-drop.json"));
        var patch = document.RootElement.EnumerateArray().Single();

        Assert.Equal("game:blocktypes/wood/pan", patch.GetProperty("file").GetString());
        Assert.Equal("add", patch.GetProperty("op").GetString());
        Assert.Equal(
            "/attributes/panningDrops/@(bonysoil|bonysoil-.*)/-",
            patch.GetProperty("path").GetString());

        var drop = patch.GetProperty("value");
        Assert.Equal("astraterra:sky-disc-tinbronze", drop.GetProperty("code").GetString());

        // The mark the drop carries is the one the server looks for, and a disc without it is just
        // a disc.
        Assert.Equal(
            FoundSkyDisc.MarkerAttribute,
            drop.GetProperty("attributes").EnumerateObject().Single().Name);

        // Rare enough to be a find. Around the golden coronet, well under the rusty gear.
        Assert.InRange(drop.GetProperty("chance").GetProperty("avg").GetDouble(), 0.0001, 0.002);
    }

    private static StarCatalog LoadCatalog()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var stars = JsonSerializer.Deserialize<StarCatalogEntry[]>(
            File.ReadAllText("assets/astraterra/data/star-catalog.v1.json"),
            options)!;
        var culture = JsonSerializer.Deserialize<SkyCultureConstellationSet>(
            File.ReadAllText("assets/astraterra/data/sky-cultures/modern-iau.constellations.v1.json"),
            options)!;

        return new StarCatalog(stars, [], [culture]);
    }
}
