using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace AstraTerra.Blocks;

/// <summary>
/// The one disc a wall mounting holds, and everything the game needs to draw it there.
/// </summary>
/// <remarks>
/// A one-slot container rather than a bare stored stack, because a disc is not interchangeable with
/// another disc: its band of sunsets and its engraved figure live on the itemstack, and the
/// container is what carries an itemstack through saving, chunk unload, and the block being broken
/// with all of that still on it.
/// <para>
/// <see cref="BlockEntityDisplay"/> supplies the rest: it asks the disc for its own marked model
/// through <c>IContainedMeshSource</c>, caches it, and rebuilds it when the slot changes.
/// </para>
/// </remarks>
public sealed class BlockEntitySkyDiscWall : BlockEntityDisplay
{
    private readonly InventoryGeneric inventory;

    public BlockEntitySkyDiscWall()
    {
        inventory = new InventoryDisplayed(this, 1, "skydiscwall-0", null);
    }

    public override InventoryBase Inventory => inventory;

    public override string InventoryClassName => "skydiscwall";

    /// <summary>
    /// The item attribute a hung disc's model is transformed by, for anyone who wants to nudge one.
    /// </summary>
    /// <remarks>
    /// Astra Terra's own discs set nothing here: <see cref="SkyDiscWallMount.MountMatrix"/> already
    /// puts the disc where it belongs. Another mod hanging its own disc-shaped thing can lean on it.
    /// </remarks>
    public override string AttributeTransformCode => "onwallTransform";

    public override int DisplayedItems => 1;

    /// <summary>What is hanging here, or nothing.</summary>
    public ItemStack? Disc => inventory[0]?.Itemstack;

    /// <summary>Hangs one disc out of the slot it is held in. Fails if something already hangs here.</summary>
    public bool TryHang(ItemSlot from)
    {
        if (Disc is not null || from?.Itemstack is null)
        {
            return false;
        }

        if (from.TryPutInto(Api.World, inventory[0], 1) == 0)
        {
            return false;
        }

        from.MarkDirty();
        MarkDirty(true);
        return true;
    }

    /// <summary>
    /// Takes the disc down into a player's hands, or onto the floor if their hands are full.
    /// </summary>
    public bool TryTake(IPlayer? byPlayer)
    {
        if (Disc is not { } stack)
        {
            return false;
        }

        inventory[0].Itemstack = null;
        inventory[0].MarkDirty();

        if (byPlayer?.InventoryManager?.TryGiveItemstack(stack) != true)
        {
            Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
        }

        MarkDirty(true);
        return true;
    }

    protected override float[][] genTransformationMatrices()
        => new[] { SkyDiscWallMount.MountMatrix(Block?.LastCodePart()) };

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (Disc is { } stack)
        {
            dsc.AppendLine(stack.GetName());
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);

        // The marks are part of the model, so a disc arriving from the server has to be redrawn and
        // not merely stored.
        RedrawAfterReceivingTreeAttributes(worldForResolving);
    }
}
