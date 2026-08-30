namespace AstraTerra.Astronomy;

/// <summary>
/// Vector work on the sky sphere: the small set of operations every pass that draws a picture at a
/// direction needs, and nothing else.
/// </summary>
/// <remarks>
/// A picture on the sky is a flat square hung at a direction, so something has to decide which way is
/// up in it. That decision is always the same shape of arithmetic — take a direction in space, flatten
/// it onto the sky where the body is, and build a frame from what is left — whether the thing being
/// oriented is a crescent Venus or the moon.
/// </remarks>
public static class SkyTangent
{
    /// <summary>
    /// The part of <paramref name="towards"/> that lies on the sky at <paramref name="direction"/>:
    /// what a direction in space looks like once it is flattened onto the view.
    /// </summary>
    /// <remarks>
    /// Degenerate when the two are parallel or opposite — the sun directly behind the observer has no
    /// direction on the sky — which is what <paramref name="fallback"/> is for.
    /// </remarks>
    public static SkyDirection Tangent(SkyDirection direction, SkyDirection towards, SkyDirection fallback)
    {
        var projection = (towards.X * direction.X) + (towards.Y * direction.Y) + (towards.Z * direction.Z);
        var tangent = new SkyDirection(
            towards.X - (direction.X * projection),
            towards.Y - (direction.Y * projection),
            towards.Z - (direction.Z * projection));
        return Length(tangent) < 1e-6 ? fallback : Normalize(tangent);
    }

    /// <summary>
    /// The horizontal at a direction: the axis a picture hangs level along when nothing else fixes
    /// its orientation. Looking north, this points east.
    /// </summary>
    public static SkyDirection Horizontal(SkyDirection direction)
    {
        var candidate = Math.Abs(direction.Y) < 0.999
            ? new SkyDirection(0, 1, 0)
            : new SkyDirection(1, 0, 0);
        return Cross(direction, candidate);
    }

    /// <summary>The unit vector at right angles to both, normalized.</summary>
    public static SkyDirection Cross(SkyDirection left, SkyDirection right)
        => Normalize(new SkyDirection(
            (left.Y * right.Z) - (left.Z * right.Y),
            (left.Z * right.X) - (left.X * right.Z),
            (left.X * right.Y) - (left.Y * right.X)));

    public static SkyDirection Negate(SkyDirection direction)
        => new(-direction.X, -direction.Y, -direction.Z);

    public static double Length(SkyDirection direction)
        => Math.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y) + (direction.Z * direction.Z));

    public static SkyDirection Normalize(SkyDirection direction)
    {
        var length = Length(direction);
        return length < 1e-12 ? direction : new SkyDirection(direction.X / length, direction.Y / length, direction.Z / length);
    }

    /// <summary>
    /// A square picture's four corners on the sky sphere, <paramref name="widthDeg"/> across, centred
    /// on the body with its horizontal running along <paramref name="rightAxis"/>.
    /// </summary>
    /// <remarks>
    /// Bottom-left first, then anticlockwise as the image is seen, because that is the winding
    /// <see cref="AstraTerra.Client.Rendering.DeepSkyQuadMeshBuilder"/> reads: it flips V on the way
    /// out, so the first corner it is handed is the one that takes the picture's bottom row.
    /// </remarks>
    public static IReadOnlyList<DeepSkyDirection> BuildQuad(
        SkyDirection direction,
        SkyDirection rightAxis,
        double widthDeg)
    {
        var up = Cross(rightAxis, direction);
        var half = widthDeg * Math.PI / 360.0;
        return
        [
            Corner(direction, rightAxis, up, -half, -half),
            Corner(direction, rightAxis, up, half, -half),
            Corner(direction, rightAxis, up, half, half),
            Corner(direction, rightAxis, up, -half, half)
        ];
    }

    private static DeepSkyDirection Corner(
        SkyDirection direction,
        SkyDirection rightAxis,
        SkyDirection up,
        double right,
        double above)
    {
        var corner = Normalize(new SkyDirection(
            direction.X + (rightAxis.X * right) + (up.X * above),
            direction.Y + (rightAxis.Y * right) + (up.Y * above),
            direction.Z + (rightAxis.Z * right) + (up.Z * above)));
        return new DeepSkyDirection(corner.X, corner.Y, corner.Z);
    }
}
