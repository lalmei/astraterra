using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace AstraTerra.Blocks;

/// <summary>
/// Where a hung disc sits against the wall it hangs on, and which walls will take one.
/// </summary>
/// <remarks>
/// Kept apart from the block and the block entity so the geometry can be checked without a running
/// game: the whole of "does the disc end up flat on the wall, face out, the right way up" is four
/// numbers and one matrix, and a matrix that is wrong by a sign is invisible in a code review and
/// obvious in a test.
/// </remarks>
public static class SkyDiscWallMount
{
    /// <summary>The block that is a disc on a wall, less its side.</summary>
    public const string BlockCode = "sky-disc-wall";

    /// <summary>The item attribute that says a disc is finished enough to be worth hanging.</summary>
    public const string MountableAttribute = "wallmountable";

    /// <summary>
    /// How far the disc stands off the wall. Nothing hangs perfectly flush, and a face at exactly
    /// the wall plane fights the wall's own face for the same pixels.
    /// </summary>
    public const float WallGap = 0.01f;

    /// <summary>
    /// The four walls a disc can hang on, named the way the game names them: the side of the disc's
    /// own block that the wall is on. A disc on the north block faces south, out into the room.
    /// </summary>
    public static readonly IReadOnlyList<string> Sides = new[] { "north", "east", "south", "west" };

    /// <summary>How far the whole mounting is turned about the block's upright axis.</summary>
    /// <remarks>
    /// North is the unturned case. The rest follow the game's own turn direction, which is what the
    /// selection boxes in the block's json are rotated by — the two have to agree or the disc is
    /// drawn on one wall and clicked on another.
    /// </remarks>
    public static float YawDeg(string? side) => side switch
    {
        "west" => 90f,
        "south" => 180f,
        "east" => 270f,
        _ => 0f,
    };

    /// <summary>
    /// The disc's model as it hangs: stood upright, turned to the wall, and pushed against it.
    /// </summary>
    /// <remarks>
    /// The disc's shape lies flat on the floor of its own model, face up, filling all but a
    /// sixteenth of the block in both horizontal directions. A quarter turn about x stands that
    /// face up and points it north; the lift by a whole block puts it back inside the block it was
    /// turned out of; the yaw swings it round to whichever wall it was hung on.
    /// <para>
    /// Read the calls bottom to top: the last one written is the first one applied to the model.
    /// </para>
    /// </remarks>
    public static float[] MountMatrix(string? side)
        => Matrixf.Create()
            .Translate(0.5f, 0f, 0.5f)
            .RotateYDeg(YawDeg(side))
            .Translate(-0.5f, 1f, WallGap - 0.5f)
            .RotateXDeg(90f)
            .Values;

    /// <summary>Whether a face that was clicked is a wall rather than a floor or a ceiling.</summary>
    public static bool IsWall(BlockFacing? face) => face is not null && face.IsHorizontal;
}
