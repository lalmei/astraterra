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

        var mesh = MeteorStreakMeshBuilder.Build([streak], radius: 40.0f);

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
}
