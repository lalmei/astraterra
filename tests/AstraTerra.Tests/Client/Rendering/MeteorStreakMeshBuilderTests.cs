using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

public sealed class MeteorStreakMeshBuilderTests
{
    [Fact]
    public void One_Streak_Builds_A_Tapered_Four_Section_Ribbon()
    {
        var streak = new RenderedMeteorStreak(
            "PER",
            new MeteorSkyDirection(0.0, 1.0, 0.0),
            new MeteorSkyDirection(1.0, 0.0, 0.0),
            45.0,
            4.0,
            0.2,
            0.8);

        var mesh = MeteorStreakMeshBuilder.Build([streak], radius: 40.0f, capacity: 1);

        Assert.Equal(8, mesh.VerticesCount);
        Assert.Equal(18, mesh.IndicesCount);
        for (var vertexIndex = 0; vertexIndex < mesh.VerticesCount; vertexIndex++)
        {
            var offset = vertexIndex * 3;
            var radius = Math.Sqrt(
                (mesh.xyz[offset] * mesh.xyz[offset]) +
                (mesh.xyz[offset + 1] * mesh.xyz[offset + 1]) +
                (mesh.xyz[offset + 2] * mesh.xyz[offset + 2]));
            Assert.Equal(40.0, radius, 4);
        }
    }

    [Fact]
    public void Mesh_Capacity_Caps_The_Number_Of_Streaks()
    {
        var streak = new RenderedMeteorStreak(
            "GEM",
            new MeteorSkyDirection(0.0, 1.0, 0.0),
            new MeteorSkyDirection(1.0, 0.0, 0.0),
            35.0,
            3.0,
            0.15,
            1.0);

        var mesh = MeteorStreakMeshBuilder.Build([streak, streak, streak], radius: 39.8f, capacity: 2);

        Assert.Equal(16, mesh.VerticesCount);
        Assert.Equal(36, mesh.IndicesCount);
    }

    /// <summary>
    /// Vintage Story sizes a mesh's GPU buffers on the first upload and never grows them, so a
    /// batch that changed size between frames would overrun the buffer it was first uploaded into.
    /// </summary>
    [Fact]
    public void Mesh_Size_Does_Not_Change_With_The_Number_Of_Streaks()
    {
        var streak = new RenderedMeteorStreak(
            "LYR",
            new MeteorSkyDirection(0.0, 1.0, 0.0),
            new MeteorSkyDirection(1.0, 0.0, 0.0),
            20.0,
            2.5,
            0.12,
            0.9);

        var empty = MeteorStreakMeshBuilder.Build([], radius: 39.8f, capacity: 4);
        var one = MeteorStreakMeshBuilder.Build([streak], radius: 39.8f, capacity: 4);
        var full = MeteorStreakMeshBuilder.Build([streak, streak, streak, streak], radius: 39.8f, capacity: 4);

        Assert.Equal(32, empty.VerticesCount);
        Assert.Equal(32, one.VerticesCount);
        Assert.Equal(32, full.VerticesCount);
        Assert.Equal(72, empty.IndicesCount);
        Assert.Equal(72, one.IndicesCount);
        Assert.Equal(72, full.IndicesCount);
    }

    [Fact]
    public void Unused_Streak_Slots_Are_Transparent_And_Have_No_Area()
    {
        var streak = new RenderedMeteorStreak(
            "ORI",
            new MeteorSkyDirection(0.0, 1.0, 0.0),
            new MeteorSkyDirection(1.0, 0.0, 0.0),
            25.0,
            3.0,
            0.12,
            1.0);

        var mesh = MeteorStreakMeshBuilder.Build([streak], radius: 39.8f, capacity: 2);

        Assert.Equal(16, mesh.VerticesCount);
        for (var vertexIndex = 8; vertexIndex < mesh.VerticesCount; vertexIndex++)
        {
            var offset = vertexIndex * 3;
            Assert.Equal(mesh.xyz[24], mesh.xyz[offset]);
            Assert.Equal(mesh.xyz[25], mesh.xyz[offset + 1]);
            Assert.Equal(mesh.xyz[26], mesh.xyz[offset + 2]);
            Assert.Equal(0, mesh.Rgba[(vertexIndex * 4) + 3]);
        }
    }
}
