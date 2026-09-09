using AstraTerra.Astronomy;
using Xunit;

namespace AstraTerra.Tests.Astronomy;

/// <summary>
/// How the giant's two terms combine with the value vanilla worked out without knowing about it.
/// </summary>
/// <remarks>
/// These go through <see cref="NearBodyLightController.Combine"/> rather than the controller's
/// world-bound entry point, because what is being checked is the arithmetic of putting two lights
/// on one landscape, and that does not need a game to be running.
/// </remarks>
public sealed class NearBodyLightControllerTests
{
    /// <summary>Vanilla's own value at noon, near enough.</summary>
    private const float Noon = 1.0f;

    /// <summary>Vanilla's own value on a moonless night.</summary>
    private const float DarkNight = 0.0f;

    /// <summary>
    /// The night is the case this whole change exists for: vanilla says dark, the giant says
    /// otherwise, and the giant wins.
    /// </summary>
    [Fact]
    public void Planetshine_Lights_A_Night_Vanilla_Left_Dark()
    {
        var lit = NearBodyLightController.Combine(DarkNight, new NearBodyIllumination(0.47, 0.0));

        Assert.Equal(0.47f, lit, 5);
    }

    /// <summary>
    /// And it never brightens a day. Planetshine is compared against the sunlit value rather than
    /// added to it, the same way vanilla compares its own moonlight, so noon under a full giant is
    /// still just noon.
    /// </summary>
    [Fact]
    public void Planetshine_Never_Brightens_A_Day()
    {
        var lit = NearBodyLightController.Combine(Noon, new NearBodyIllumination(0.47, 0.0));

        Assert.Equal(Noon, lit, 5);
    }

    /// <summary>
    /// A total eclipse takes the day down to the light refracted through the giant's own
    /// atmosphere, which is deep twilight rather than nothing -- the reason a totally eclipsed moon
    /// is copper rather than invisible.
    /// </summary>
    [Fact]
    public void A_Total_Eclipse_Leaves_Only_The_Refracted_Ring()
    {
        var eclipsed = NearBodyLightController.Combine(Noon, new NearBodyIllumination(0.0, 1.0));

        Assert.Equal((float)NearBodyLightController.EclipseRefractionFloor, eclipsed, 5);
    }

    /// <summary>
    /// A partial eclipse takes a proportional bite, so the long shoulder either side of totality
    /// reads as a slow dimming rather than a switch.
    /// </summary>
    [Fact]
    public void A_Partial_Eclipse_Dims_In_Proportion()
    {
        var half = NearBodyLightController.Combine(Noon, new NearBodyIllumination(0.0, 0.5));

        Assert.Equal(0.5f, half, 5);
    }

    /// <summary>
    /// During an eclipse the giant is between this world and the sun, so its lit face is turned
    /// away and there is no planetshine to soften the dark. The two terms are consistent with each
    /// other because they come from the same geometry, and this is where that shows.
    /// </summary>
    [Fact]
    public void An_Eclipse_Is_Not_Rescued_By_The_Giants_Own_Light()
    {
        var giant = new NearBodyLightSource(19.5, 0.0, 0.0, 0.52);
        var sunAndGiantTogether = new SkyDirection(0.0, 1.0, 0.0);

        var illumination = NearBodyLight.Illumination(giant, sunAndGiantTogether, 90.0, sunAndGiantTogether);

        Assert.Equal(1.0, illumination.SolarObscuration);
        Assert.Equal(0.0, illumination.PlanetshineStrength, 6);
        Assert.Equal(
            (float)NearBodyLightController.EclipseRefractionFloor,
            NearBodyLightController.Combine(Noon, illumination),
            5);
    }

    /// <summary>
    /// With nothing happening the vanilla value goes through untouched, bit for bit. Every world
    /// that is not a moon world takes this path on every light query the game makes.
    /// </summary>
    [Fact]
    public void Nothing_Happening_Returns_The_Vanilla_Value_Untouched()
    {
        Assert.Equal(0.372f, NearBodyLightController.Combine(0.372f, NearBodyIllumination.None));
        Assert.Equal(DarkNight, NearBodyLightController.Combine(DarkNight, NearBodyIllumination.None));
    }

    /// <summary>
    /// Longitude genuinely changes how much light the ground gets, because the giant hangs over one
    /// patch of world rather than over the observer. This is why each side has to supply its own
    /// answer for it: <see cref="ObserverLongitude"/> is client-scope state that a dedicated server
    /// reads as zero, so a server left to that would spawn by the light of a giant standing
    /// somewhere none of its clients can see it.
    /// </summary>
    [Fact]
    public void Longitude_Moves_The_Giant_And_So_Moves_The_Light()
    {
        var giant = new NearBodyLightSource(19.5, 30.0, 0.0, 0.52);
        var sunBelow = new SkyDirection(0.0, -1.0, 0.0);

        var atPrime = NearBodyLightController.IlluminationFor(
            giant, latitudeDeg: 0.0, longitudeDeg: 0.0, totalDays: 10.0, 360, 24.0, sunBelow);
        var halfAWorldEast = NearBodyLightController.IlluminationFor(
            giant, latitudeDeg: 0.0, longitudeDeg: 180.0, totalDays: 10.0, 360, 24.0, sunBelow);

        Assert.True(
            atPrime.PlanetshineStrength > 0.0,
            "the giant should be up over the meridian it was authored against");
        Assert.True(
            halfAWorldEast.PlanetshineStrength < atPrime.PlanetshineStrength,
            "half a world away the giant should be lower or down, and the night darker for it");
    }

    /// <summary>
    /// On a world whose giant hangs where the generator actually puts one -- well off the meridian
    /// -- the night is lit and the noon is left alone. Those are the two ends of the effect,
    /// through the placement the game itself uses.
    /// </summary>
    [Fact]
    public void The_Giant_Lights_Midnight_And_Leaves_Noon_Alone()
    {
        var giant = new NearBodyLightSource(19.5, HourAngleDeg: 40.0, DeclinationDeg: 0.0, Albedo: 0.52);

        var midnight = NearBodyLightController.IlluminationFor(
            giant, 0.0, 0.0, 10.0, 360, 24.0, new SkyDirection(0.0, -1.0, 0.0));
        var noon = NearBodyLightController.IlluminationFor(
            giant, 0.0, 0.0, 10.0, 360, 24.0, new SkyDirection(0.0, 1.0, 0.0));

        Assert.True(midnight.PlanetshineStrength > NearBodyLight.VanillaFullMoonLight);
        Assert.Equal(0.0, noon.SolarObscuration);
        Assert.Equal(Noon, NearBodyLightController.Combine(Noon, noon), 5);
    }

    /// <summary>
    /// And this is what the hour-angle band in <c>NearSky</c> is now for. Put the giant straight
    /// overhead instead and the sun goes behind it at noon every single day of the year -- the
    /// failure mode the old authored declination was guarding against, which is real, and which the
    /// hour angle is what actually prevents now that the declination is honest.
    /// </summary>
    [Fact]
    public void A_Giant_On_The_Meridian_Would_Eclipse_The_Sun_At_Noon_Every_Day()
    {
        var overhead = new NearBodyLightSource(19.5, HourAngleDeg: 0.0, DeclinationDeg: 0.0, Albedo: 0.52);

        var noon = NearBodyLightController.IlluminationFor(
            overhead, 0.0, 0.0, 10.0, 360, 24.0, new SkyDirection(0.0, 1.0, 0.0));

        Assert.Equal(1.0, noon.SolarObscuration);
        Assert.Equal(
            (float)NearBodyLightController.EclipseRefractionFloor,
            NearBodyLightController.Combine(Noon, noon),
            5);
    }

    /// <summary>
    /// A controller with no giant is the identity, which is what a planet world and a switched-off
    /// server both get.
    /// </summary>
    [Fact]
    public void A_Controller_Without_A_Giant_Is_Inactive()
    {
        var controller = new NearBodyLightController();

        Assert.False(controller.IsActive);
        Assert.Null(controller.Source);
        Assert.Equal(0.25f, controller.Apply(calendar: null, 0.0, 0.0, 0.25f));
    }
}
