using AstraTerra.Commands;
using Xunit;

namespace AstraTerra.Tests.Commands;

public sealed class LatitudeTeleportPlannerTests
{
    [Fact]
    public void FindNearestZForLatitude_Uses_Map_Latitude_Function()
    {
        static double LatitudeAtZ(double z) => ((z - 100000.0) / 100000.0) * 90.0;

        var z = LatitudeTeleportPlanner.FindNearestZForLatitude(
            targetLatitudeDeg: 45,
            currentZ: 100000,
            minZ: 0,
            maxZ: 200000,
            LatitudeAtZ,
            coarseStep: 10000);

        Assert.InRange(z, 149980, 150020);
    }

    [Fact]
    public void FindNearestZForLatitude_Chooses_Nearest_Matching_Band()
    {
        static double LatitudeAtZ(double z) => z < 100000 ? 45.0 : 45.0;

        var z = LatitudeTeleportPlanner.FindNearestZForLatitude(
            targetLatitudeDeg: 45,
            currentZ: 150000,
            minZ: 0,
            maxZ: 200000,
            LatitudeAtZ,
            coarseStep: 10000);

        Assert.InRange(z, 149980, 150020);
    }
}
