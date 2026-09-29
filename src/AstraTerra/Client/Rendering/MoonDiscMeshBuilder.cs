using AstraTerra.Astronomy;
using Vintagestory.API.Client;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Builds a fixed lunar surface portrait and lights that surface from the sun.
/// </summary>
/// <remarks>
/// The texture coordinates never turn with the phase. Only the per-vertex light changes, so Tycho
/// remains where the authored portrait put it while the terminator still points towards the sun.
/// </remarks>
public static class MoonDiscMeshBuilder
{
    /// <summary>Enough cells to keep a curved terminator smooth at the moon's exaggerated size.</summary>
    public const int Subdivisions = 64;

    /// <summary>Faint earthshine retained on the night side.</summary>
    public const float Earthshine = 0.07f;

    /// <summary>A narrow fade at the terminator, in Lambert-light units.</summary>
    public const double TerminatorSoftness = 0.04;

    public static MeshData Build(
        SkyDirection moon,
        SkyDirection sun,
        double moonPhaseExact,
        float radius)
    {
        var mesh = DeepSkyQuadMeshBuilder.Build(
            MoonDiscModel.BuildQuad(moon),
            radius,
            Subdivisions);
        var lighting = ResolveLighting(moon, sun, moonPhaseExact);

        var rowSize = Subdivisions + 1;
        for (var row = 0; row <= Subdivisions; row++)
        {
            var faceY = (row / (double)Subdivisions * 2.0) - 1.0;
            for (var column = 0; column <= Subdivisions; column++)
            {
                var faceX = (column / (double)Subdivisions * 2.0) - 1.0;
                var light = LightAt(faceX, faceY, lighting);
                var shade = (byte)Math.Clamp(Math.Round(light * 255.0), 0.0, 255.0);
                var rgba = ((row * rowSize) + column) * 4;
                mesh.Rgba[rgba] = shade;
                mesh.Rgba[rgba + 1] = shade;
                mesh.Rgba[rgba + 2] = shade;
                mesh.Rgba[rgba + 3] = 255;
            }
        }

        return mesh;
    }

    /// <summary>
    /// The angular radius of the solid core of Vintage Story's sun: <c>sun.png</c> is opaque across
    /// the middle 96 of its 256 pixels, and the game draws that picture 10.24 blocks wide at 50
    /// blocks out.
    /// </summary>
    public const double SunCoreRadiusDeg = 2.2;

    /// <summary>How far past the core the sun's picture takes to fall to a faint glow.</summary>
    public const double SunEdgeFadeDeg = 0.75;

    /// <summary>
    /// Whether any part of a moon at <paramref name="moon"/> lies over the sun's disc, so the pass
    /// that blocks the sun need not run on the other twenty-nine days of the month.
    /// </summary>
    public static bool CanCoverSun(SkyDirection moon, SkyDirection sun)
        => SeparationDeg(moon, sun)
            < (MoonDiscModel.AngularDiameterDeg * 0.5) + SunCoreRadiusDeg + SunEdgeFadeDeg;

    /// <summary>
    /// The same disc as <see cref="Build"/>, coloured black and made opaque only where it lies in
    /// front of the sun.
    /// </summary>
    /// <remarks>
    /// By day the moon is added to the sky rather than painted over it, so its unlit side is sky
    /// blue rather than a black hole -- which is right for the sky, whose light is scattered in the
    /// air in front of the moon, and wrong for the sun, whose light comes from behind it. Drawn over
    /// the sun with ordinary blending, this takes back exactly the sunlight the moon stands in front
    /// of and nothing else, and the additive pass then puts the moon's own lit surface on top.
    /// </remarks>
    public static MeshData BuildSunOccluder(SkyDirection moon, SkyDirection sun, float radius)
    {
        var mesh = DeepSkyQuadMeshBuilder.Build(
            MoonDiscModel.BuildQuad(moon),
            radius,
            Subdivisions);

        var vertexCount = (Subdivisions + 1) * (Subdivisions + 1);
        for (var vertex = 0; vertex < vertexCount; vertex++)
        {
            var xyz = vertex * 3;
            var point = new SkyDirection(mesh.xyz[xyz], mesh.xyz[xyz + 1], mesh.xyz[xyz + 2]);
            var cover = SunCoverage(point, sun);
            var rgba = vertex * 4;
            mesh.Rgba[rgba] = 0;
            mesh.Rgba[rgba + 1] = 0;
            mesh.Rgba[rgba + 2] = 0;
            mesh.Rgba[rgba + 3] = (byte)Math.Clamp(Math.Round(cover * 255.0), 0.0, 255.0);
        }

        return mesh;
    }

    /// <summary>
    /// How much of the sun's light arrives from <paramref name="point"/>, as a share of its core:
    /// all of it on the disc, fading across the edge of the picture, none beyond.
    /// </summary>
    public static float SunCoverage(SkyDirection point, SkyDirection sun)
    {
        var beyondCore = (SeparationDeg(point, sun) - SunCoreRadiusDeg) / SunEdgeFadeDeg;
        var remaining = 1.0 - Math.Clamp(beyondCore, 0.0, 1.0);
        return (float)(remaining * remaining * (3.0 - (2.0 * remaining)));
    }

    /// <summary>Light at one point of the apparent lunar disc, for focused model tests.</summary>
    public static float LightAt(
        double faceX,
        double faceY,
        SkyDirection moon,
        SkyDirection sun,
        double moonPhaseExact)
        => LightAt(faceX, faceY, ResolveLighting(moon, sun, moonPhaseExact));

    private static float LightAt(double faceX, double faceY, MoonLighting lighting)
    {
        var distanceSquared = (faceX * faceX) + (faceY * faceY);
        if (distanceSquared > 1.0)
        {
            // The texture is transparent outside the disc; keeping the surrounding square white
            // avoids introducing a dark fringe while its alpha is filtered at the limb.
            return 1f;
        }

        var towardObserver = Math.Sqrt(Math.Max(0.0, 1.0 - distanceSquared));
        var lambert = (faceX * lighting.Right)
            + (faceY * lighting.Up)
            + (towardObserver * lighting.TowardObserver);
        var transition = Math.Clamp(
            (lambert + TerminatorSoftness) / (2.0 * TerminatorSoftness),
            0.0,
            1.0);
        var smooth = transition * transition * (3.0 - (2.0 * transition));
        return (float)(Earthshine + ((1.0 - Earthshine) * smooth));
    }

    private static MoonLighting ResolveLighting(
        SkyDirection moon,
        SkyDirection sun,
        double moonPhaseExact)
    {
        var surfaceRight = MoonDiscModel.SurfaceRightAxis(moon);
        var surfaceUp = SkyTangent.Cross(surfaceRight, moon);
        var sunward = SkyTangent.Tangent(moon, sun, fallback: surfaceRight);

        var phase = NormalizePhase(moonPhaseExact);
        var phaseAngle = phase * Math.PI / 4.0;
        var acrossDisc = Math.Abs(Math.Sin(phaseAngle));
        return new MoonLighting(
            Dot(sunward, surfaceRight) * acrossDisc,
            Dot(sunward, surfaceUp) * acrossDisc,
            -Math.Cos(phaseAngle));
    }

    private static double NormalizePhase(double moonPhaseExact)
    {
        if (!double.IsFinite(moonPhaseExact))
        {
            return 4.0;
        }

        var phase = moonPhaseExact % 8.0;
        return phase < 0.0 ? phase + 8.0 : phase;
    }

    private static double SeparationDeg(SkyDirection left, SkyDirection right)
    {
        var length = SkyTangent.Length(left) * SkyTangent.Length(right);
        if (length < 1e-12)
        {
            return 180.0;
        }

        return Math.Acos(Math.Clamp(Dot(left, right) / length, -1.0, 1.0)) * 180.0 / Math.PI;
    }

    private static double Dot(SkyDirection left, SkyDirection right)
        => (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);

    private readonly record struct MoonLighting(double Right, double Up, double TowardObserver);
}
