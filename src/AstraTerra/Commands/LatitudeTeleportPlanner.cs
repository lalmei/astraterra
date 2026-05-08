namespace AstraTerra.Commands;

public static class LatitudeTeleportPlanner
{
    public static double FindNearestZForLatitude(
        double targetLatitudeDeg,
        double currentZ,
        double minZ,
        double maxZ,
        Func<double, double> latitudeAtZDeg,
        double coarseStep = 1024.0)
    {
        targetLatitudeDeg = Math.Clamp(targetLatitudeDeg, -90.0, 90.0);
        coarseStep = Math.Max(1.0, coarseStep);

        var bestZ = minZ;
        var bestScore = double.MaxValue;
        for (var z = minZ; z <= maxZ; z += coarseStep)
        {
            Consider(z);
        }

        Consider(maxZ);
        Refine(coarseStep);
        Refine(Math.Max(16.0, coarseStep / 16.0));
        Refine(1.0);
        return Math.Clamp(bestZ, minZ, maxZ);

        void Refine(double step)
        {
            var start = Math.Max(minZ, bestZ - (step * 16.0));
            var end = Math.Min(maxZ, bestZ + (step * 16.0));
            for (var z = start; z <= end; z += step)
            {
                Consider(z);
            }

            Consider(end);
        }

        void Consider(double z)
        {
            var latitudeScore = Math.Abs(latitudeAtZDeg(z) - targetLatitudeDeg);
            var travelScore = Math.Abs(z - currentZ) / Math.Max(1.0, maxZ - minZ);
            var score = latitudeScore + (travelScore * 0.001);
            if (score < bestScore)
            {
                bestScore = score;
                bestZ = z;
            }
        }
    }
}
