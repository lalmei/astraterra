using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// A plate arrives with its own stars photographed into it. These pin the handover: the catalog goes
/// quiet under a plate and is untouched a degree away from one.
/// </summary>
public sealed class DeepSkyPlateVisibilityTests
{
    [Fact]
    public void A_Star_Away_From_Every_Plate_Keeps_All_Its_Brightness()
    {
        var fields = Build(Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));

        Assert.Equal(1.0, DeepSkyPlateVisibility.GetStarBrightnessFactor(fields, 0, 1, 0), 6);
    }

    [Fact]
    public void A_Star_Under_The_Middle_Of_A_Bright_Plate_Gives_Way_To_It()
    {
        var fields = Build(Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));

        Assert.Equal(0.0, DeepSkyPlateVisibility.GetStarBrightnessFactor(fields, 1, 0, 0), 6);
    }

    /// <summary>
    /// A hard edge would draw a visible disc of missing stars around every plate, so the handover
    /// ramps between the plate's inner field and its border.
    /// </summary>
    [Fact]
    public void The_Handover_Ramps_Out_To_The_Plates_Edge()
    {
        var fields = Build(Plate(sizeDeg: 4.0, brightness: 1.0, x: 1, y: 0, z: 0));

        var deepInside = FactorAt(fields, degreesFromCentre: 0.4);
        var partway = FactorAt(fields, degreesFromCentre: 1.4);
        var atTheEdge = FactorAt(fields, degreesFromCentre: 2.1);

        Assert.Equal(0.0, deepInside, 6);
        Assert.InRange(partway, 0.05, 0.95);
        Assert.Equal(1.0, atTheEdge, 6);
    }

    /// <summary>
    /// A plate low enough to be barely drawn cannot take the stars with it, or a rising nebula would
    /// clear a hole in the sky before it was visible itself.
    /// </summary>
    [Fact]
    public void A_Barely_Drawn_Plate_Barely_Dims_Anything()
    {
        var faint = Build(Plate(sizeDeg: 2.0, brightness: 0.1, x: 1, y: 0, z: 0));
        var bright = Build(Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));

        var underFaint = DeepSkyPlateVisibility.GetStarBrightnessFactor(faint, 1, 0, 0);

        Assert.True(underFaint > 0.85, $"A plate at a tenth brightness took {1 - underFaint:0.00} of the star.");
        Assert.True(underFaint > DeepSkyPlateVisibility.GetStarBrightnessFactor(bright, 1, 0, 0));
    }

    [Fact]
    public void Overlapping_Plates_Are_Decided_By_Whichever_Covers_Most()
    {
        var fields = Build(
            Plate(sizeDeg: 2.0, brightness: 0.3, x: 1, y: 0, z: 0),
            Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));

        Assert.Equal(0.0, DeepSkyPlateVisibility.GetStarBrightnessFactor(fields, 1, 0, 0), 6);
    }

    [Fact]
    public void No_Plates_Leaves_The_Sky_Alone()
    {
        var fields = Build();

        Assert.Equal(1.0, DeepSkyPlateVisibility.GetStarBrightnessFactor(fields, 1, 0, 0), 6);
    }

    [Fact]
    public void A_Plate_Draws_At_Its_Brightness_Up_To_The_Opacity_Ceiling()
    {
        Assert.Equal(0.21f, DeepSkyPlateVisibility.CalculateOpacity(0.5), 4);
        Assert.Equal(DeepSkyPlateVisibility.MaximumOpacity, DeepSkyPlateVisibility.CalculateOpacity(5.0), 4);
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateOpacity(-1.0), 4);
    }

    /// <summary>
    /// The star batch is cached across frames, so it has to be able to tell when the plates under it
    /// have changed.
    /// </summary>
    [Fact]
    public void The_Signature_Follows_Which_Plates_Are_Up()
    {
        var one = Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0);
        var two = Plate(sizeDeg: 2.0, brightness: 1.0, x: 0, y: 1, z: 0, id: "other");

        Assert.Equal(DeepSkyPlateVisibility.GetSignature([one]), DeepSkyPlateVisibility.GetSignature([one]));
        Assert.NotEqual(DeepSkyPlateVisibility.GetSignature([one]), DeepSkyPlateVisibility.GetSignature([one, two]));
        Assert.NotEqual(DeepSkyPlateVisibility.GetSignature([one]), DeepSkyPlateVisibility.GetSignature([two]));
    }

    private static IReadOnlyList<DeepSkyPlateField> Build(params RenderedDeepSkyObject[] plates)
    {
        var fields = new List<DeepSkyPlateField>();
        DeepSkyPlateVisibility.BuildFields(plates, fields);
        return fields;
    }

    /// <summary>What a star keeps, sitting the given angle away from a plate centred on +X.</summary>
    private static double FactorAt(IReadOnlyList<DeepSkyPlateField> fields, double degreesFromCentre)
    {
        var radians = degreesFromCentre * Math.PI / 180.0;
        return DeepSkyPlateVisibility.GetStarBrightnessFactor(fields, Math.Cos(radians), Math.Sin(radians), 0);
    }

    private static RenderedDeepSkyObject Plate(
        double sizeDeg,
        double brightness,
        double x,
        double y,
        double z,
        string id = "plate")
    {
        // Four corners around one centre is all the fade reads; the drawn quad's own shape is the
        // mesh builder's business.
        var corners = new List<DeepSkyDirection>
        {
            new(x, y, z),
            new(x, y, z),
            new(x, y, z),
            new(x, y, z),
        };

        return new RenderedDeepSkyObject(
            id,
            id,
            sizeDeg,
            brightness,
            1f,
            1f,
            1f,
            ["texture"],
            corners);
    }
}
