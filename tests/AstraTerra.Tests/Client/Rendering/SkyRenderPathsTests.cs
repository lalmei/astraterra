using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// The point of the toggles is that one path goes dark while the rest keep drawing — otherwise they
/// cannot separate "the stars are expensive" from "the lines are expensive".
/// </summary>
[Collection("SkyRenderPaths")]
public sealed class SkyRenderPathsTests : IDisposable
{
    public SkyRenderPathsTests() => SkyRenderPaths.Reset();

    public void Dispose() => SkyRenderPaths.Reset();

    [Fact]
    public void Everything_Draws_By_Default()
    {
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Stars));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Constellations));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.DeepSky));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Meteors));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.MilkyWay));
    }

    [Fact]
    public void Switching_One_Path_Off_Leaves_The_Others_Drawing()
    {
        SkyRenderPaths.Set(SkyRenderPath.Constellations, on: false);

        Assert.False(SkyRenderPaths.IsEnabled(SkyRenderPath.Constellations));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Stars));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.DeepSky));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Meteors));
    }

    [Fact]
    public void A_Path_Comes_Back()
    {
        SkyRenderPaths.Set(SkyRenderPath.Stars, on: false);
        SkyRenderPaths.Set(SkyRenderPath.Stars, on: true);

        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Stars));
    }

    [Fact]
    public void All_Switches_Everything_At_Once()
    {
        SkyRenderPaths.Set(SkyRenderPath.All, on: false);
        Assert.False(SkyRenderPaths.IsEnabled(SkyRenderPath.Stars));
        Assert.False(SkyRenderPaths.IsEnabled(SkyRenderPath.Meteors));

        SkyRenderPaths.Set(SkyRenderPath.All, on: true);
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Stars));
        Assert.True(SkyRenderPaths.IsEnabled(SkyRenderPath.Meteors));
    }

    [Fact]
    public void The_Report_Names_Every_Path_And_Its_State()
    {
        SkyRenderPaths.Set(SkyRenderPath.DeepSky, on: false);

        var description = SkyRenderPaths.Describe();

        Assert.Contains("stars=on", description, StringComparison.Ordinal);
        Assert.Contains("constellations=on", description, StringComparison.Ordinal);
        Assert.Contains("deepsky=off", description, StringComparison.Ordinal);
        Assert.Contains("meteors=on", description, StringComparison.Ordinal);
        Assert.Contains("milkyway=on", description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("stars", SkyRenderPath.Stars)]
    [InlineData("STARS", SkyRenderPath.Stars)]
    [InlineData("lines", SkyRenderPath.Constellations)]
    [InlineData("constellation", SkyRenderPath.Constellations)]
    [InlineData("deep-sky", SkyRenderPath.DeepSky)]
    [InlineData("meteor", SkyRenderPath.Meteors)]
    [InlineData("milkyway", SkyRenderPath.MilkyWay)]
    [InlineData("milky-way", SkyRenderPath.MilkyWay)]
    [InlineData("all", SkyRenderPath.All)]
    public void The_Parser_Accepts_The_Names_A_Player_Would_Type(string value, SkyRenderPath expected)
    {
        Assert.True(SkyRenderPathParser.TryParse(value, out var path));
        Assert.Equal(expected, path);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("planets")]
    [InlineData("everything")]
    public void The_Parser_Rejects_Anything_Else(string? value)
    {
        Assert.False(SkyRenderPathParser.TryParse(value, out var path));
        Assert.Equal(SkyRenderPath.None, path);
    }
}
