using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Batches every constellation dot into the single mesh the sky pass draws.
/// </summary>
/// <remarks>
/// The dots are not decoration beside the lines — they <em>are</em> the lines, one billboard every
/// 0.65 deg along each edge, which on the authored catalogue is around 3700 of them. Drawn one at a
/// time that was more draw calls than the whole star catalogue, every frame, for as long as a player
/// carried a journal.
/// <para>
/// The geometry itself lives in <see cref="SkyBillboardMeshBuilder"/>, shared with the star batch.
/// All this adds is the fixed dot size and the tint-to-vertex-colour conversion.
/// </para>
/// </remarks>
public static class ConstellationDotMeshBuilder
{
    public const int VerticesPerDot = SkyBillboardMeshBuilder.VerticesPerBillboard;
    public const int IndicesPerDot = SkyBillboardMeshBuilder.IndicesPerBillboard;

    [ThreadStatic]
    private static List<SkyBillboard>? billboardBuffer;

    /// <param name="radius">Distance to the sky sphere the dots sit on.</param>
    /// <param name="angularSizeDeg">Width of one dot, in degrees of sky.</param>
    public static MeshData Build(
        IReadOnlyList<SkyConstellationDot> dots,
        float radius,
        float angularSizeDeg,
        int capacity)
    {
        ArgumentNullException.ThrowIfNull(dots);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(angularSizeDeg);

        var billboards = billboardBuffer ??= new List<SkyBillboard>(4096);
        billboards.Clear();
        for (var index = 0; index < dots.Count; index++)
        {
            var dot = dots[index];
            billboards.Add(new SkyBillboard(
                dot.DirectionX,
                dot.DirectionY,
                dot.DirectionZ,
                angularSizeDeg,
                ColorFromTint(dot.Tint)));
        }

        return SkyBillboardMeshBuilder.Build(billboards, radius, capacity);
    }

    /// <summary>Dots needed for <paramref name="dotCount"/>, rounded up so the mesh is not resized for every edit.</summary>
    public static int GetCapacityFor(int dotCount, int growthStep = 1024)
        => SkyBillboardMeshBuilder.GetCapacityFor(dotCount, growthStep);

    private static int ColorFromTint(ConstellationTint tint)
        => ColorUtil.ColorFromRgba(
            ToByte(tint.R),
            ToByte(tint.G),
            ToByte(tint.B),
            ToByte(tint.A));

    private static int ToByte(float channel) => (int)Math.Round(Math.Clamp(channel, 0f, 1f) * 255f);
}
