using AstraTerra.Client.SkyLying;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class SkyLyingAnimationTests
{
    [Fact]
    public void ToSupine_Drops_The_Side_Roll_And_Keeps_The_Back_Pitch()
    {
        var lie = new Animation
        {
            Code = "lie",
            Name = "Lie",
            QuantityFrames = 50,
            KeyFrames =
            [
                new AnimationKeyFrame
                {
                    Frame = 0,
                    Elements = new Dictionary<string, AnimationKeyFrameElement>
                    {
                        ["LowerTorso"] = new AnimationKeyFrameElement
                        {
                            OffsetX = -3,
                            OffsetZ = -11,
                            RotationX = -112.5,
                            RotationY = 0,
                            RotationZ = -90
                        },
                        ["UpperTorso"] = new AnimationKeyFrameElement { RotationY = -22.5 },
                        ["Head"] = new AnimationKeyFrameElement { RotationY = 22.5 }
                    }
                }
            ]
        };

        var stargaze = SkyLyingAnimation.ToSupine(lie);
        var torso = stargaze.KeyFrames[0].Elements["LowerTorso"];
        var elements = stargaze.KeyFrames[0].Elements;

        Assert.Equal("stargaze", stargaze.Code);
        Assert.Equal(SkyLyingAnimation.AnimationVersion, stargaze.Version);
        Assert.Equal(0, torso.RotationX);
        Assert.Equal(0, torso.RotationY);
        Assert.Equal(SkyLyingAnimation.BackPitchZ, torso.RotationZ);
        Assert.Equal(0, torso.OffsetX);
        Assert.Equal(SkyLyingAnimation.GroundOffsetY, torso.OffsetY);
        Assert.Equal(SkyLyingAnimation.GroundOffsetZ, torso.OffsetZ);
        Assert.Equal(0, elements["UpperTorso"].RotationY);
        Assert.False(elements.ContainsKey("Head"));
        Assert.Equal(0, elements["UpperFootL"].RotationZ);
        Assert.Equal(0, elements["LowerFootL"].RotationZ);
        Assert.Equal(0, elements["UpperFootR"].RotationZ);
        Assert.Equal(0, elements["LowerFootR"].RotationZ);
        Assert.Equal(SkyLyingAnimation.UpperArmRaiseZ, elements["UpperArmR"].RotationZ);
        Assert.Equal(SkyLyingAnimation.UpperArmRWrapY, elements["UpperArmR"].RotationY);
        Assert.Equal(SkyLyingAnimation.UpperArmRBackX, elements["UpperArmR"].RotationX);
        Assert.Equal(SkyLyingAnimation.UpperArmLWrapY, elements["UpperArmL"].RotationY);
        Assert.Equal(SkyLyingAnimation.LowerArmRBendX, elements["LowerArmR"].RotationX);
        Assert.Equal(SkyLyingAnimation.LowerArmLBendX, elements["LowerArmL"].RotationX);
        Assert.Equal(-3, lie.KeyFrames[0].Elements["LowerTorso"].OffsetX);
        Assert.Equal(-112.5, lie.KeyFrames[0].Elements["LowerTorso"].RotationX);
        Assert.Equal(-90, lie.KeyFrames[0].Elements["LowerTorso"].RotationZ);
    }

    [Fact]
    public void ToSupine_Leaves_The_Arms_Free_When_Hands_Are_Occupied()
    {
        var lie = new Animation
        {
            Code = "lie",
            Name = "Lie",
            QuantityFrames = 1,
            KeyFrames =
            [
                new AnimationKeyFrame
                {
                    Frame = 0,
                    Elements = new Dictionary<string, AnimationKeyFrameElement>
                    {
                        ["LowerTorso"] = new AnimationKeyFrameElement { RotationZ = -90 },
                        ["UpperArmR"] = new AnimationKeyFrameElement { RotationZ = -180, OffsetY = 3 }
                    }
                }
            ]
        };

        var holding = SkyLyingAnimation.ToSupine(lie, handsBehindHead: false);
        var arm = holding.KeyFrames[0].Elements["UpperArmR"];

        Assert.Equal("stargaze-hold", holding.Code);
        Assert.Equal(0, arm.RotationZ);
        Assert.Equal(0, arm.OffsetY);
        Assert.Equal(SkyLyingAnimation.GroundOffsetY, holding.KeyFrames[0].Elements["LowerTorso"].OffsetY);
        Assert.Equal(-180, lie.KeyFrames[0].Elements["UpperArmR"].RotationZ);
        Assert.Equal(3, lie.KeyFrames[0].Elements["UpperArmR"].OffsetY);
    }

    [Fact]
    public void EnsureSupineClip_Replaces_A_Sideways_Stargaze_With_The_Unrolled_Lie()
    {
        var lie = SkyLyingAnimation.ToSupine(new Animation
        {
            Code = "lie",
            Name = "Lie",
            QuantityFrames = 1,
            KeyFrames =
            [
                new AnimationKeyFrame
                {
                    Frame = 0,
                    Elements = new Dictionary<string, AnimationKeyFrameElement>
                    {
                        ["LowerTorso"] = new AnimationKeyFrameElement { RotationX = -112.5, RotationZ = -90 }
                    }
                }
            ]
        });
        lie.Code = "lie";

        var shape = new Shape
        {
            Animations =
            [
                lie,
                new Animation
                {
                    Code = "stargaze",
                    Name = "Stargaze",
                    QuantityFrames = 1,
                    KeyFrames =
                    [
                        new AnimationKeyFrame
                        {
                            Frame = 0,
                            Elements = new Dictionary<string, AnimationKeyFrameElement>
                            {
                                ["LowerTorso"] = new AnimationKeyFrameElement { RotationX = -90, RotationZ = 0 }
                            }
                        }
                    ]
                }
            ]
        };

        Assert.True(SkyLyingAnimation.EnsureSupineClip(shape));
        var torso = Assert.Single(shape.Animations, animation => animation.Code == "stargaze")
            .KeyFrames[0].Elements["LowerTorso"];
        Assert.Equal(0, torso.RotationX);
        Assert.Equal(-90, torso.RotationZ);
        Assert.Equal(SkyLyingAnimation.GroundOffsetY, torso.OffsetY);
        Assert.Equal(SkyLyingAnimation.AnimationVersion, Assert.Single(shape.Animations, animation => animation.Code == "stargaze").Version);
        Assert.Contains(shape.Animations, animation => animation.Code == "stargaze-hold");
    }

    [Fact]
    public void EnsureSupineClip_Ignores_A_Shape_Without_The_Bed_Clip()
    {
        Assert.False(SkyLyingAnimation.EnsureSupineClip(null));
        Assert.False(SkyLyingAnimation.EnsureSupineClip(new Shape { Animations = [] }));
    }
}
