using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace AstraTerra.Blocks;

/// <summary>
/// A sky disc hung on a wall, the way a painting is hung on a wall.
/// </summary>
/// <remarks>
/// The block is nothing to look at on its own — its shape is empty and everything visible comes from
/// the disc in its block entity. It exists so a finished disc has somewhere to be other than a chest:
/// a band of sunsets a world year in the making, and a constellation cut by hand, are worth a wall.
/// </remarks>
public sealed class BlockSkyDiscWall : Block
{
    private WorldInteraction[] interactions = Array.Empty<WorldInteraction>();

    /// <summary>
    /// Hangs the disc in a slot on the wall face that was clicked, and takes it out of the slot.
    /// </summary>
    /// <remarks>
    /// Server side only. Block placement is the server's to decide, and the disc's marks live on the
    /// itemstack the server holds; a client that placed its own copy would draw a disc that either
    /// is not there or is blank until the server's version of the block entity arrives.
    /// </remarks>
    public static bool TryHang(IWorldAccessor? world, IPlayer? byPlayer, BlockSelection? blockSel, ItemSlot? slot)
    {
        if (world is null || byPlayer is null || slot?.Itemstack is null || !CanHang(blockSel, slot))
        {
            return false;
        }

        var mounting = world.GetBlock(new AssetLocation("astraterra", $"{SkyDiscWallMount.BlockCode}-north"));
        if (mounting is null)
        {
            return false;
        }

        // The game offsets the selection off the clicked face before a block is placed against it;
        // this click arrived as an item's, so nothing has been offset yet.
        var position = blockSel!.Position.AddCopy(blockSel.Face);
        var placement = blockSel.Clone();
        placement.Position = position;
        placement.DidOffset = true;

        var failureCode = string.Empty;

        // The horizontal attachment behaviour picks the variant for the wall and refuses a face that
        // will not hold one, so which of the four is placed is not decided here.
        if (!mounting.TryPlaceBlock(world, byPlayer, new ItemStack(mounting), placement, ref failureCode))
        {
            return false;
        }

        if (world.BlockAccessor.GetBlockEntity(position) is not BlockEntitySkyDiscWall hung
            || !hung.TryHang(slot))
        {
            // Better no mounting than an empty one: an empty one is an invisible block on a wall.
            world.BlockAccessor.SetBlock(0, position);
            return false;
        }

        PlayMountingSound(world, mounting, position, byPlayer);
        return true;
    }

    /// <summary>Whether this disc, aimed at this face, is one that can go on a wall.</summary>
    public static bool CanHang(BlockSelection? blockSel, ItemSlot? slot)
        => SkyDiscWallMount.IsWall(blockSel?.Face)
           && slot?.Itemstack?.Collectible?.Attributes?[SkyDiscWallMount.MountableAttribute]
               .AsBool(false) == true;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api is not ICoreClientAPI)
        {
            return;
        }

        interactions = new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "astraterra:blockhelp-skydiscwall-take",
                MouseButton = EnumMouseButton.Right,
            },
        };
    }

    /// <summary>An empty hand takes the disc back off the wall, and the mounting goes with it.</summary>
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntitySkyDiscWall hung)
        {
            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        if (world.Side == EnumAppSide.Server && !hung.TryTake(byPlayer))
        {
            return false;
        }

        if (world.Side == EnumAppSide.Server)
        {
            world.BlockAccessor.SetBlock(0, blockSel.Position);
        }

        PlayMountingSound(world, this, blockSel.Position, byPlayer);

        return true;
    }

    /// <summary>The bronze-on-stone knock of a disc going up or coming down.</summary>
    private static void PlayMountingSound(IWorldAccessor world, Block mounting, BlockPos pos, IPlayer? byPlayer)
    {
        if (mounting.Sounds?.Place is { } sound)
        {
            world.PlaySoundAt(sound, pos, 0.5, byPlayer);
        }
    }

    /// <summary>Picking the block picks the disc: the mounting is not a thing you can hold.</summary>
    public override ItemStack OnPickBlock(IWorldAccessor world, BlockPos pos)
        => world.BlockAccessor.GetBlockEntity(pos) is BlockEntitySkyDiscWall { Disc: { } disc }
            ? disc.Clone()
            : base.OnPickBlock(world, pos);

    public override string GetPlacedBlockName(IWorldAccessor world, BlockPos pos)
        => world.BlockAccessor.GetBlockEntity(pos) is BlockEntitySkyDiscWall { Disc: { } disc }
            ? disc.GetName()
            : base.GetPlacedBlockName(world, pos);

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(
        IWorldAccessor world,
        BlockSelection selection,
        IPlayer forPlayer)
        => interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
}
