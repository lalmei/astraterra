using AstraTerra.Astronomy;
using AstraTerra.Client.Observation;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// A plate arrives with its own stars photographed into it. These pin the handover: the catalog goes
/// quiet under a plate and is untouched a degree away from one.
/// </summary>
public sealed class DeepSkyPlateVisibilityTests
{
    private const float FullRevealFov = DeepSkyPlateVisibility.BrassMaxZoomFovMultiplier;
    private const double FullNaturalDarkness = 1.0;

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
        Assert.Equal(0.21f, DeepSkyPlateVisibility.CalculateOpacity(0.5, FullRevealFov, FullNaturalDarkness), 4);
        Assert.Equal(DeepSkyPlateVisibility.MaximumOpacity, DeepSkyPlateVisibility.CalculateOpacity(5.0, FullRevealFov, FullNaturalDarkness), 4);
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateOpacity(-1.0, FullRevealFov, FullNaturalDarkness), 4);
    }

    [Fact]
    public void Full_Daylight_Suppresses_Plates_And_Star_Handover()
    {
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateOpacity(1.0, FullRevealFov, naturalDarkness: 0.0), 6);
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateAmbientVisibility(0.0), 6);

        var fields = Build(FullRevealFov, naturalDarkness: 0.0, Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));
        Assert.Empty(fields);
        Assert.Equal(1.0, DeepSkyPlateVisibility.GetStarBrightnessFactor(fields, 1, 0, 0), 6);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_Finite_Natural_Darkness_Suppresses_Ambient_Plates(double naturalDarkness)
    {
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateAmbientVisibility(naturalDarkness));
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateOpacity(1.0, FullRevealFov, naturalDarkness), 6);

        var fields = Build(FullRevealFov, naturalDarkness, Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));
        Assert.Empty(fields);
    }

    [Fact]
    public void Ambient_Visibility_Fades_In_Monotonically_After_Twilight_Floor()
    {
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateAmbientVisibility(DeepSkyPlateVisibility.TwilightFloor - 0.01), 6);
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateAmbientVisibility(DeepSkyPlateVisibility.TwilightFloor), 6);

        var early = DeepSkyPlateVisibility.CalculateAmbientVisibility(0.72);
        var later = DeepSkyPlateVisibility.CalculateAmbientVisibility(0.88);
        Assert.True(later > early);
        Assert.Equal(1f, DeepSkyPlateVisibility.CalculateAmbientVisibility(FullNaturalDarkness), 4);
    }

    [Fact]
    public void Forced_Daylight_Stars_Do_Not_Raise_Ambient_Plates()
    {
        // Render darkness would be 1 under forceDaylightStars; ambient visibility still reads noon.
        Assert.Equal(0f, DeepSkyPlateVisibility.CalculateOpacity(1.0, FullRevealFov, naturalDarkness: 0.05), 6);
    }

    [Fact]
    public void Dark_Night_Opacity_Is_Unchanged_By_Ambient_Factor()
    {
        var withoutAmbient = 1.0 * DeepSkyPlateVisibility.BrightnessScale * DeepSkyPlateVisibility.MaxZoomReveal;
        Assert.Equal(
            (float)Math.Clamp(withoutAmbient, 0.0, DeepSkyPlateVisibility.MaximumOpacity),
            DeepSkyPlateVisibility.CalculateOpacity(1.0, FullRevealFov, FullNaturalDarkness),
            4);
    }

    [Fact]
    public void The_Signature_Follows_Ambient_Darkness()
    {
        var one = Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0);

        Assert.NotEqual(
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, naturalDarkness: 0.5),
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, FullNaturalDarkness));
    }

    [Fact]
    public void Zoom_Reveal_Fov_Boundaries_Match_Telescope_Observation_State()
    {
        Assert.Equal(
            DeepSkyPlateVisibility.WideTelescopeFovMultiplier,
            TelescopeObservationState.GetFovMultiplier(TelescopeObservationState.MinZoomStep, TelescopeObservationState.MaxZoomStep));
        Assert.Equal(
            DeepSkyPlateVisibility.BrassMaxZoomFovMultiplier,
            TelescopeObservationState.MaxZoomFovMultiplier);
    }

    [Fact]
    public void Zoom_Reveal_Is_A_Faint_Smudge_At_Wide_Field()
    {
        Assert.Equal(DeepSkyPlateVisibility.MinZoomReveal, DeepSkyPlateVisibility.CalculateZoomReveal(0.45f), 4);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(-1f)]
    [InlineData(0f)]
    public void Zoom_Reveal_Falls_Back_To_Minimum_For_Invalid_Fov(float fovMultiplier)
    {
        Assert.Equal(DeepSkyPlateVisibility.MinZoomReveal, DeepSkyPlateVisibility.CalculateZoomReveal(fovMultiplier));
    }

    [Fact]
    public void Wide_Field_Opacity_Scales_Down_With_Zoom_Reveal()
    {
        var wide = DeepSkyPlateVisibility.CalculateOpacity(1.0, DeepSkyPlateVisibility.WideTelescopeFovMultiplier, FullNaturalDarkness);
        var full = DeepSkyPlateVisibility.CalculateOpacity(1.0, DeepSkyPlateVisibility.BrassMaxZoomFovMultiplier, FullNaturalDarkness);

        Assert.Equal(0.021f, wide, 3);
        Assert.Equal(0.42f, full, 3);
        Assert.True(wide < full / 10f);
    }

    [Fact]
    public void Zoom_Reveal_Increases_As_Field_Of_View_Tightens()
    {
        var wider = DeepSkyPlateVisibility.CalculateZoomReveal(0.35f);
        var tighter = DeepSkyPlateVisibility.CalculateZoomReveal(0.18f);

        Assert.True(tighter > wider);
    }

    [Fact]
    public void Zoom_Reveal_Is_Full_At_Brass_Maximum_Magnification()
    {
        Assert.Equal(DeepSkyPlateVisibility.MaxZoomReveal, DeepSkyPlateVisibility.CalculateZoomReveal(0.12f), 4);
        Assert.Equal(DeepSkyPlateVisibility.MaxZoomReveal, DeepSkyPlateVisibility.CalculateZoomReveal(0.06f), 4);
    }

    [Fact]
    public void Zoom_Reveal_Ramps_With_Ease_In_Quadratic_Between_Wide_And_Brass_Max()
    {
        var reveal = DeepSkyPlateVisibility.CalculateZoomReveal(0.25f);

        Assert.InRange(reveal, 0.30, 0.42);
    }

    [Fact]
    public void Wide_Field_Star_Suppression_Tracks_Zoom_Reveal()
    {
        var wide = Build(0.45f, Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));
        var zoomed = Build(FullRevealFov, Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0));

        var underWide = DeepSkyPlateVisibility.GetStarBrightnessFactor(wide, 1, 0, 0);
        var underZoomed = DeepSkyPlateVisibility.GetStarBrightnessFactor(zoomed, 1, 0, 0);

        Assert.True(underWide > 0.9, $"Wide field took {1 - underWide:0.00} of the star.");
        Assert.Equal(0.0, underZoomed, 6);
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

        Assert.Equal(
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, FullNaturalDarkness),
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, FullNaturalDarkness));
        Assert.NotEqual(
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, FullNaturalDarkness),
            DeepSkyPlateVisibility.GetSignature([one, two], FullRevealFov, FullNaturalDarkness));
        Assert.NotEqual(
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, FullNaturalDarkness),
            DeepSkyPlateVisibility.GetSignature([two], FullRevealFov, FullNaturalDarkness));
    }

    [Fact]
    public void The_Signature_Follows_Telescope_Zoom_Reveal()
    {
        var one = Plate(sizeDeg: 2.0, brightness: 1.0, x: 1, y: 0, z: 0);

        Assert.NotEqual(
            DeepSkyPlateVisibility.GetSignature([one], 0.45f, FullNaturalDarkness),
            DeepSkyPlateVisibility.GetSignature([one], FullRevealFov, FullNaturalDarkness));
    }

    private static IReadOnlyList<DeepSkyPlateField> Build(
        params RenderedDeepSkyObject[] plates)
        => Build(FullRevealFov, FullNaturalDarkness, plates);

    private static IReadOnlyList<DeepSkyPlateField> Build(
        float fovMultiplier,
        params RenderedDeepSkyObject[] plates)
        => Build(fovMultiplier, FullNaturalDarkness, plates);

    private static IReadOnlyList<DeepSkyPlateField> Build(
        float fovMultiplier,
        double naturalDarkness,
        params RenderedDeepSkyObject[] plates)
    {
        var fields = new List<DeepSkyPlateField>();
        DeepSkyPlateVisibility.BuildFields(plates, fovMultiplier, naturalDarkness, fields);
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
