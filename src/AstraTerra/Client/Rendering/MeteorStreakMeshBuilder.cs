using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public static class MeteorStreakMeshBuilder
{
    private readonly record struct TrailSection(double Position, double WidthScale, double AlphaScale);

    private static readonly TrailSection[] Sections =
    [
        new(-0.50, 0.05, 0.00),
        new(-0.28, 0.65, 0.20),
        new(0.34, 1.00, 1.00),
        new(0.50, 0.08, 0.00)
    ];

    public static MeshData Build(
        IReadOnlyList<RenderedMeteorStreak> streaks,
        float radius,
        int capacity = MeteorShowerVisualModel.MaximumActiveStreaks)
    {
        ArgumentNullException.ThrowIfNull(streaks);
        if (radius <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        const int verticesPerStreak = 8;
        const int indicesPerStreak = 18;
        var meshData = new MeshData(
            capacity * verticesPerStreak,
            capacity * indicesPerStreak,
            withNormals: false,
            withUv: true,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Triangles
        };

        foreach (var streak in streaks.Take(capacity))
        {
            AddStreak(meshData, streak, radius);
        }

        return meshData;
    }

    private static void AddStreak(MeshData meshData, RenderedMeteorStreak streak, float radius)
    {
        var firstVertex = meshData.VerticesCount;
        var halfWidthTangent = Math.Tan(streak.AngularWidthDeg * Math.PI / 360.0);

        for (var sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
        {
            var section = Sections[sectionIndex];
            var alongRadians = streak.AngularLengthDeg * section.Position * Math.PI / 180.0;
            var center = Normalize(Add(
                Scale(streak.Center, Math.Cos(alongRadians)),
                Scale(streak.Tangent, Math.Sin(alongRadians))));
            var tangent = Normalize(Add(
                Scale(streak.Center, -Math.Sin(alongRadians)),
                Scale(streak.Tangent, Math.Cos(alongRadians))));
            var side = Normalize(Cross(center, tangent));
            var sideAmount = halfWidthTangent * section.WidthScale;
            var left = Normalize(Add(center, Scale(side, -sideAmount)));
            var right = Normalize(Add(center, Scale(side, sideAmount)));
            var alpha = (int)Math.Round(Math.Clamp(streak.Alpha * section.AlphaScale, 0.0, 1.0) * 255.0);
            var color = ColorUtil.ColorFromRgba(220, 235, 255, alpha);
            var u = sectionIndex / (float)(Sections.Length - 1);

            meshData.AddVertexWithFlags(
                (float)left.X * radius,
                (float)left.Y * radius,
                (float)left.Z * radius,
                u,
                0f,
                color,
                0);
            meshData.AddVertexWithFlags(
                (float)right.X * radius,
                (float)right.Y * radius,
                (float)right.Z * radius,
                u,
                1f,
                color,
                0);
        }

        for (var sectionIndex = 0; sectionIndex < Sections.Length - 1; sectionIndex++)
        {
            var left = firstVertex + (sectionIndex * 2);
            var right = left + 1;
            var nextLeft = left + 2;
            var nextRight = left + 3;
            meshData.AddIndex(left);
            meshData.AddIndex(right);
            meshData.AddIndex(nextRight);
            meshData.AddIndex(left);
            meshData.AddIndex(nextRight);
            meshData.AddIndex(nextLeft);
        }
    }

    private static MeteorSkyDirection Add(MeteorSkyDirection first, MeteorSkyDirection second)
        => new(first.X + second.X, first.Y + second.Y, first.Z + second.Z);

    private static MeteorSkyDirection Scale(MeteorSkyDirection value, double factor)
        => new(value.X * factor, value.Y * factor, value.Z * factor);

    private static MeteorSkyDirection Cross(MeteorSkyDirection first, MeteorSkyDirection second)
        => new(
            (first.Y * second.Z) - (first.Z * second.Y),
            (first.Z * second.X) - (first.X * second.Z),
            (first.X * second.Y) - (first.Y * second.X));

    private static MeteorSkyDirection Normalize(MeteorSkyDirection direction)
    {
        var length = Math.Sqrt(
            (direction.X * direction.X) +
            (direction.Y * direction.Y) +
            (direction.Z * direction.Z));
        if (length <= double.Epsilon)
        {
            throw new ArgumentException("Meteor streak directions must be non-zero.");
        }

        return Scale(direction, 1.0 / length);
    }
}
