using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SkyDiscFaceTests
{
    [Fact]
    public void A_Blank_Disc_Has_Nothing_On_Its_Face()
    {
        Assert.Empty(SkyDiscFace.Read(new SolarBand()));
        Assert.Empty(SkyDiscFace.Read(null));
    }

    [Fact]
    public void Evenings_On_The_Same_Notch_Leave_One_Mark()
    {
        var band = new SolarBand();
        band.Scratch(400, 240.0, SolarEvent.Sunset, 51.0, 0, 0);
        band.Scratch(401, 240.4, SolarEvent.Sunset, 51.0, 0, 0);
        band.Scratch(402, 241.0, SolarEvent.Sunset, 51.0, 0, 0);

        var face = SkyDiscFace.Read(band);

        Assert.Single(face);
        Assert.Equal(240.0, face[0].NotchDeg);
    }

    [Fact]
    public void The_Ends_Of_A_Rim_Are_The_Marks_The_Face_Calls_Out()
    {
        var band = new SolarBand();
        band.Scratch(400, 240.0, SolarEvent.Sunset, 51.0, 0, 0);
        band.Scratch(410, 250.0, SolarEvent.Sunset, 51.0, 0, 0);
        band.Scratch(420, 260.0, SolarEvent.Sunset, 51.0, 0, 0);

        var face = SkyDiscFace.Read(band);

        Assert.Equal([true, false, true], face.Select(mark => mark.IsEdge));
    }

    [Fact]
    public void Each_Rim_Keeps_Its_Own_Ends()
    {
        var band = new SolarBand();
        band.Scratch(400, 240.0, SolarEvent.Sunset, 51.0, 0, 0);
        band.Scratch(400, 100.0, SolarEvent.Sunrise, 51.0, 0, 0);

        var face = SkyDiscFace.Read(band);

        Assert.Equal(2, face.Count);
        Assert.All(face, mark => Assert.True(mark.IsEdge));
        Assert.Contains(face, mark => mark.Event == SolarEvent.Sunrise);
        Assert.Contains(face, mark => mark.Event == SolarEvent.Sunset);
    }

    [Fact]
    public void Two_Discs_Scratched_Alike_Share_One_Name()
    {
        var first = new SolarBand();
        first.Scratch(400, 240.0, SolarEvent.Sunset, 51.0, 0, 0);
        var second = new SolarBand();
        second.Scratch(880, 240.9, SolarEvent.Sunset, 12.0, 900, -400);

        Assert.Equal(
            SkyDiscFace.Key(SkyDiscFace.Read(first)),
            SkyDiscFace.Key(SkyDiscFace.Read(second)));
    }

    [Fact]
    public void A_Scratch_Changes_What_The_Face_Is_Called()
    {
        var band = new SolarBand();
        band.Scratch(400, 240.0, SolarEvent.Sunset, 51.0, 0, 0);
        var before = SkyDiscFace.Key(SkyDiscFace.Read(band));

        band.Scratch(410, 250.0, SolarEvent.Sunset, 51.0, 0, 0);

        Assert.NotEqual(before, SkyDiscFace.Key(SkyDiscFace.Read(band)));
    }
}
