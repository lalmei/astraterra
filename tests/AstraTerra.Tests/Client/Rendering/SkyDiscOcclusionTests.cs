using AstraTerra.Astronomy;
using AstraTerra.Client.Rendering;
using Xunit;

namespace AstraTerra.Tests.Client.Rendering;

/// <summary>
/// A globe in front of the star field takes the stars behind it out of the batch, because nothing
/// in the sky pass writes depth and every disc over it is drawn at less than full opacity at some
/// point in its day.
/// </summary>
public sealed class SkyDiscOcclusionTests
{
    private static readonly SkyDirection Overhead = new(0.0, 1.0, 0.0);

    private static List<SkyOccultingField> FieldsFor(params SkyOccultingDisc[] discs)
    {
        var fields = new List<SkyOccultingField>();
        SkyDiscOcclusion.BuildFields(discs, fields);
        return fields;
    }

    /// <summary>A star along the centre of a globe is behind rock, and rock passes nothing.</summary>
    [Fact]
    public void A_Star_Behind_The_Middle_Of_A_Globe_Is_Not_Drawn()
    {
        var fields = FieldsFor(new SkyOccultingDisc(Overhead, 7.0));

        Assert.Equal(0.0, SkyDiscOcclusion.GetStarVisibilityFactor(fields, 0.0, 1.0, 0.0));
    }

    [Fact]
    public void A_Star_Clear_Of_The_Limb_Keeps_All_Its_Light()
    {
        var fields = FieldsFor(new SkyOccultingDisc(Overhead, 7.0));
        var justOutside = Direction(altitudeDeg: 90.0 - 4.0);

        Assert.Equal(1.0, SkyDiscOcclusion.GetStarVisibilityFactor(fields, justOutside.X, justOutside.Y, justOutside.Z));
    }

    /// <summary>
    /// The batch is rebuilt in steps as the sky turns, so a perfectly hard limb would blink a star
    /// out between one rebuild and the next.
    /// </summary>
    [Fact]
    public void A_Star_On_The_Limb_Fades_Rather_Than_Blinking_Out()
    {
        var fields = FieldsFor(new SkyOccultingDisc(Overhead, 7.0));
        var onTheLimb = Direction(altitudeDeg: 90.0 - (3.5 * 0.97));

        var factor = SkyDiscOcclusion.GetStarVisibilityFactor(fields, onTheLimb.X, onTheLimb.Y, onTheLimb.Z);

        Assert.InRange(factor, 0.01, 0.99);
    }

    /// <summary>
    /// The bug this exists for: a near body washed out by daylight or by the horizon haze is drawn
    /// at a fraction of full opacity, and the stars behind it used to mix straight back through.
    /// How solidly it draws is not the question -- whether it is there is.
    /// </summary>
    [Fact]
    public void A_Body_Still_Blocks_However_Faintly_It_Is_Drawn()
    {
        var fields = FieldsFor(new SkyOccultingDisc(Overhead, 60.0, Coverage: 1.0));

        Assert.Equal(0.0, SkyDiscOcclusion.GetStarVisibilityFactor(fields, 0.0, 1.0, 0.0));
    }

    /// <summary>
    /// The one thing that does take a globe's hold away: a body the horizon haze has is not drawn,
    /// and something that is not drawn cannot be hiding a star.
    /// </summary>
    [Fact]
    public void A_Body_Lost_To_The_Haze_Stops_Blocking()
    {
        var half = FieldsFor(new SkyOccultingDisc(Overhead, 7.0, Coverage: 0.5));
        var gone = FieldsFor(new SkyOccultingDisc(Overhead, 7.0, Coverage: 0.0));

        Assert.Equal(0.5, SkyDiscOcclusion.GetStarVisibilityFactor(half, 0.0, 1.0, 0.0), 3);
        Assert.Equal(1.0, SkyDiscOcclusion.GetStarVisibilityFactor(gone, 0.0, 1.0, 0.0));
    }

    [Fact]
    public void Overlapping_Globes_Are_Decided_By_The_One_That_Hides_A_Star_Most()
    {
        var fields = FieldsFor(
            new SkyOccultingDisc(Overhead, 7.0, Coverage: 0.25),
            new SkyOccultingDisc(Overhead, 7.0, Coverage: 0.90));

        Assert.Equal(0.10, SkyDiscOcclusion.GetStarVisibilityFactor(fields, 0.0, 1.0, 0.0), 3);
    }

    [Fact]
    public void A_Disc_With_No_Width_Or_No_Direction_Is_Not_A_Field()
    {
        Assert.Empty(FieldsFor(new SkyOccultingDisc(Overhead, 0.0)));
        Assert.Empty(FieldsFor(new SkyOccultingDisc(new SkyDirection(0.0, 0.0, 0.0), 7.0)));
    }

    /// <summary>
    /// The signature is what tells a cached star batch it has gone stale. It has to answer to a
    /// globe that has actually moved, and not to one that has not.
    /// </summary>
    [Fact]
    public void The_Signature_Follows_A_Globe_That_Moves_And_Ignores_One_That_Has_Not()
    {
        var atRest = new[] { new SkyOccultingDisc(Overhead, 7.0) };
        var same = new[] { new SkyOccultingDisc(Overhead, 7.0) };
        var moved = new[] { new SkyOccultingDisc(Direction(altitudeDeg: 80.0), 7.0) };

        Assert.Equal(SkyDiscOcclusion.GetSignature(atRest), SkyDiscOcclusion.GetSignature(same));
        Assert.NotEqual(SkyDiscOcclusion.GetSignature(atRest), SkyDiscOcclusion.GetSignature(moved));
        Assert.NotEqual(SkyDiscOcclusion.GetSignature(atRest), SkyDiscOcclusion.GetSignature([]));
    }

    /// <summary>
    /// A near body drifts a fraction of an arcminute per frame. Rebuilding five thousand star
    /// billboards for that would cost more than the bug did.
    /// </summary>
    [Fact]
    public void The_Signature_Does_Not_Rebuild_The_Sky_For_A_Hundredth_Of_A_Degree()
    {
        var atRest = new[] { new SkyOccultingDisc(Overhead, 7.0) };
        var barelyMoved = new[] { new SkyOccultingDisc(Direction(altitudeDeg: 89.99), 7.0) };

        Assert.Equal(SkyDiscOcclusion.GetSignature(atRest), SkyDiscOcclusion.GetSignature(barelyMoved));
    }

    /// <summary>A direction in the same vertical plane, at the given altitude.</summary>
    private static SkyDirection Direction(double altitudeDeg)
    {
        var radians = altitudeDeg * Math.PI / 180.0;
        return new SkyDirection(Math.Cos(radians), Math.Sin(radians), 0.0);
    }
}
