using AstraTerra.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Xunit;

namespace AstraTerra.Tests.Blocks;

/// <summary>
/// The disc's shape lies flat on the floor of its own model, face up. These check that hanging it
/// puts it flat on the wall it was hung on, face out into the room, at every one of the four walls.
/// </summary>
public sealed class SkyDiscWallMountTests
{
    /// <summary>The disc's own model: a thin plate filling all but a sixteenth of the block.</summary>
    private const float Near = 1f / 16f;
    private const float Far = 15f / 16f;
    private const float Thickness = 0.05f;

    [Theory]
    [InlineData("north")]
    [InlineData("east")]
    [InlineData("south")]
    [InlineData("west")]
    public void A_Hung_Disc_Stands_Upright_And_Fills_The_Wall(string side)
    {
        var corners = Face(side);

        // Upright and centred: the same sixteenth of clearance top and bottom that the flat model
        // has at its edges. A disc still lying flat would put every corner at one height.
        Assert.Equal(Near, corners.Min(corner => corner.Y), 3);
        Assert.Equal(Far, corners.Max(corner => corner.Y), 3);
    }

    [Theory]
    [InlineData("north", 0f)]
    [InlineData("south", 1f)]
    [InlineData("east", 1f)]
    [InlineData("west", 0f)]
    public void A_Hung_Disc_Lies_Against_The_Wall_It_Hangs_On(string side, float wall)
    {
        // The side names the wall: a disc on the north mounting hangs on the wall to its north, so
        // it sits at that edge of its own block and not out in the middle of the room.
        var corners = Face(side);
        var acrossTheRoom = side is "north" or "south"
            ? corners.Select(corner => corner.Z)
            : corners.Select(corner => corner.X);

        var nearest = wall == 0f ? acrossTheRoom.Min() : 1f - acrossTheRoom.Max();

        Assert.InRange(nearest, 0f, SkyDiscWallMount.WallGap + 0.001f);
        Assert.True(nearest > 0f, "A disc flush with the wall plane fights the wall for its pixels.");
    }

    [Theory]
    [InlineData("north", 0f, 0f, 1f)]
    [InlineData("south", 0f, 0f, -1f)]
    [InlineData("east", -1f, 0f, 0f)]
    [InlineData("west", 1f, 0f, 0f)]
    public void A_Hung_Disc_Faces_Out_Into_The_Room(string side, float x, float y, float z)
    {
        // The marked face of the disc's model points up. Hung, it has to point away from the wall:
        // a disc turned the other way is a blank back with the year's marks against the plaster.
        var normal = Transform(side, new Vec4f(0f, 1f, 0f, 0f));

        Assert.Equal(x, normal.X, 3);
        Assert.Equal(y, normal.Y, 3);
        Assert.Equal(z, normal.Z, 3);
    }

    [Fact]
    public void Only_A_Wall_Takes_A_Disc()
    {
        Assert.True(SkyDiscWallMount.IsWall(BlockFacing.NORTH));
        Assert.True(SkyDiscWallMount.IsWall(BlockFacing.EAST));
        Assert.False(SkyDiscWallMount.IsWall(BlockFacing.UP));
        Assert.False(SkyDiscWallMount.IsWall(BlockFacing.DOWN));
        Assert.False(SkyDiscWallMount.IsWall(null));
    }

    /// <summary>The eight corners of the disc's plate, once hung on the given wall.</summary>
    private static IReadOnlyList<Vec4f> Face(string side)
        => (from x in new[] { Near, Far }
            from y in new[] { 0f, Thickness }
            from z in new[] { Near, Far }
            select Transform(side, new Vec4f(x, y, z, 1f))).ToList();

    private static Vec4f Transform(string side, Vec4f point)
        => new Matrixf().Set(SkyDiscWallMount.MountMatrix(side)).TransformVector(point);
}
