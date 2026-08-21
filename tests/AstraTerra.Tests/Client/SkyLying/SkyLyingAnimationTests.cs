using System.Text.Json;
using AstraTerra.Client.SkyLying;
using AstraTerra.Observation;
using Vintagestory.API.Common;
using Xunit;

namespace AstraTerra.Tests.Client.SkyLying;

public sealed class SkyLyingAnimationTests
{
    /// <summary>
    /// The engine composes a pose about the bone's joint at animation version 0 and about the
    /// cube's own corner at version 1. On the seraph those are different points, so a version 1
    /// clip swings limbs off their sockets -- and a shape whose clips disagree on the version logs
    /// "has mixed animation versions" and blends them wrongly. Vanilla's seraph is all version 0.
    /// </summary>
    [Fact]
    public void Clips_Are_Animation_Version_Zero()
    {
        Assert.Equal(0, SkyLyingAnimation.AnimationVersion);
        Assert.Equal(0, SkyLyingAnimation.Recline().Version);
        Assert.Equal(0, SkyLyingAnimation.Idle(handsBehindHead: true).Version);
        Assert.Equal(0, SkyLyingAnimation.Idle(handsBehindHead: false).Version);
        Assert.Equal(0, SkyLyingAnimation.Rise().Version);
    }

    /// <summary>
    /// A clip is eased in by blending its first keyframe against the pose the seraph is already in,
    /// so a recline whose frame 0 is the finished pose plays as a slide into it: feet planted, hips
    /// in the air, a backbend. Frame 0 has to be the standing pose for the keyframes to drive it.
    /// </summary>
    [Fact]
    public void Recline_Starts_From_Standing_And_Ends_Supine()
    {
        var recline = SkyLyingAnimation.Recline();
        var first = recline.KeyFrames[0];
        var last = recline.KeyFrames[^1];

        Assert.Equal("stargaze-down", recline.Code);
        Assert.Equal(0, first.Frame);
        Assert.All(first.Elements.Values, element =>
        {
            Assert.Equal(0, element.RotationX);
            Assert.Equal(0, element.RotationY);
            Assert.Equal(0, element.RotationZ);
            Assert.Equal(0, element.OffsetX);
            Assert.Equal(0, element.OffsetY);
        });
        Assert.Equal(SkyLyingAnimation.BackPitchZ, last.Elements["LowerTorso"].RotationZ);
        Assert.True(last.Frame < recline.QuantityFrames);

        // Held on the last frame, and the same set of bones throughout: an element missing from a
        // keyframe is interpolated from wherever it is named next, not held where it was.
        Assert.Equal(EnumEntityAnimationEndHandling.Hold, recline.OnAnimationEnd);
        Assert.All(recline.KeyFrames, frame => Assert.Equal(first.Elements.Keys.Order(), frame.Elements.Keys.Order()));
    }

    /// <summary>The idle picks up exactly where the recline puts the body, so the handover does not show.</summary>
    [Fact]
    public void Idle_Begins_In_The_Pose_The_Recline_Ends_In()
    {
        var settled = SkyLyingAnimation.Recline().KeyFrames[^1].Elements;
        var idle = SkyLyingAnimation.Idle(handsBehindHead: true).KeyFrames[0].Elements;

        foreach (var (bone, pose) in settled)
        {
            Assert.Equal(Value(pose.RotationX), Value(idle[bone].RotationX), 3);
            Assert.Equal(Value(pose.RotationY), Value(idle[bone].RotationY), 3);
            Assert.Equal(Value(pose.RotationZ), Value(idle[bone].RotationZ), 3);
            Assert.Equal(Value(pose.OffsetX), Value(idle[bone].OffsetX), 3);
            Assert.Equal(Value(pose.OffsetY), Value(idle[bone].OffsetY), 3);
        }
    }

    /// <summary>
    /// Getting up is the recline backwards: the same poses in the opposite order, on a shorter
    /// clock. Stopping the idle on its own instead would ease the supine pose out, which is the
    /// same rigid slide as the bridge it replaced -- fast enough to read as a snap, not as standing.
    /// </summary>
    [Fact]
    public void Rise_Is_The_Recline_Backwards_And_Quicker()
    {
        var recline = SkyLyingAnimation.Recline();
        var rise = SkyLyingAnimation.Rise();

        Assert.Equal("stargaze-up", rise.Code);
        Assert.Equal(recline.KeyFrames.Length, rise.KeyFrames.Length);
        Assert.True(rise.QuantityFrames < recline.QuantityFrames, "getting up is quicker than settling down");
        Assert.Equal(0, rise.KeyFrames[0].Frame);

        // It starts in the pose the idle holds and ends standing, so it meets both without a jump.
        Assert.Equal(SkyLyingAnimation.BackPitchZ, rise.KeyFrames[0].Elements["LowerTorso"].RotationZ);
        Assert.All(rise.KeyFrames[^1].Elements.Values, element =>
        {
            Assert.Equal(0, Value(element.RotationZ));
            Assert.Equal(0, Value(element.OffsetX));
            Assert.Equal(0, Value(element.OffsetY));
        });
        for (var i = 0; i < recline.KeyFrames.Length; i++)
        {
            var mirrored = rise.KeyFrames[^(i + 1)].Elements;
            foreach (var (bone, pose) in recline.KeyFrames[i].Elements)
            {
                Assert.Equal(Value(pose.RotationZ), Value(mirrored[bone].RotationZ), 3);
                Assert.Equal(Value(pose.OffsetX), Value(mirrored[bone].OffsetX), 3);
            }
        }
    }

    [Fact]
    public void Idle_Breathes_And_Loops()
    {
        var idle = SkyLyingAnimation.Idle(handsBehindHead: true);

        Assert.Equal(EnumEntityAnimationEndHandling.Repeat, idle.OnAnimationEnd);
        Assert.True(idle.KeyFrames.Length > 1, "a single keyframe cannot breathe");
        Assert.NotEqual(
            idle.KeyFrames[0].Elements["UpperTorso"].RotationZ,
            idle.KeyFrames[1].Elements["UpperTorso"].RotationZ);
        Assert.True(idle.KeyFrames[^1].Frame < idle.QuantityFrames, "the loop needs frames to wrap through");
    }

    /// <summary>
    /// Arms folded behind the head when the hands are free; left alone when they are not, so a
    /// telescope or sextant keeps its own arm pose.
    /// </summary>
    [Fact]
    public void Only_The_Hands_Free_Idle_Poses_The_Arms()
    {
        var free = SkyLyingAnimation.Idle(handsBehindHead: true).KeyFrames[0].Elements;
        var holding = SkyLyingAnimation.Idle(handsBehindHead: false).KeyFrames[0].Elements;

        Assert.Equal("stargaze", SkyLyingAnimation.Idle(handsBehindHead: true).Code);
        Assert.Equal("stargaze-hold", SkyLyingAnimation.Idle(handsBehindHead: false).Code);
        Assert.DoesNotContain("UpperArmR", holding.Keys);
        Assert.DoesNotContain("LowerArmL", holding.Keys);

        // Mirrored left to right, and within the shoulder's range: the pose this replaced turned
        // the upper arm past 180 degrees, which put the elbow somewhere the forearm could not go.
        Assert.Equal(Value(free["UpperArmR"].RotationX), -Value(free["UpperArmL"].RotationX), 3);
        Assert.Equal(Value(free["UpperArmR"].RotationY), -Value(free["UpperArmL"].RotationY), 3);
        Assert.Equal(Value(free["UpperArmR"].RotationZ), Value(free["UpperArmL"].RotationZ), 3);
        Assert.All(free.Values, element =>
        {
            Assert.InRange(Value(element.RotationX), -140.0, 140.0);
            Assert.InRange(Value(element.RotationY), -140.0, 140.0);
            Assert.InRange(Value(element.RotationZ), -140.0, 140.0);
        });
    }

    /// <summary>
    /// The head and neck are left out on purpose: the engine's head controller points them where
    /// the player is looking, and a clip that also drives them fights the camera.
    /// </summary>
    [Fact]
    public void Clips_Leave_The_Head_To_The_Camera()
    {
        foreach (var clip in new[]
                 {
                     SkyLyingAnimation.Recline(),
                     SkyLyingAnimation.Idle(handsBehindHead: true),
                     SkyLyingAnimation.Idle(handsBehindHead: false),
                     SkyLyingAnimation.Rise()
                 })
        {
            Assert.All(clip.KeyFrames, frame =>
            {
                Assert.DoesNotContain("Head", frame.Elements.Keys);
                Assert.DoesNotContain("Neck", frame.Elements.Keys);
            });
        }
    }

    [Fact]
    public void EnsureSupineClips_Replaces_Whatever_The_Shape_Already_Carries()
    {
        var shape = new Shape
        {
            Animations =
            [
                new Animation { Code = "lie", Name = "Lie", QuantityFrames = 50, KeyFrames = [] },
                new Animation
                {
                    Code = "stargaze",
                    Name = "Stargaze",
                    QuantityFrames = 1,
                    Version = 1,
                    KeyFrames =
                    [
                        new AnimationKeyFrame
                        {
                            Frame = 0,
                            Elements = new Dictionary<string, AnimationKeyFrameElement>
                            {
                                ["LowerTorso"] = new AnimationKeyFrameElement { RotationX = -90 }
                            }
                        }
                    ]
                }
            ]
        };

        Assert.True(SkyLyingAnimation.EnsureSupineClips(shape));

        var stargaze = Assert.Single(shape.Animations, animation => animation.Code == "stargaze");
        Assert.Equal(0, stargaze.Version);
        Assert.Equal(0, stargaze.KeyFrames[0].Elements["LowerTorso"].RotationX);
        Assert.Equal(SkyLyingAnimation.BackPitchZ, stargaze.KeyFrames[0].Elements["LowerTorso"].RotationZ);
        Assert.Contains(shape.Animations, animation => animation.Code == "stargaze-hold");
        Assert.Contains(shape.Animations, animation => animation.Code == "stargaze-down");
        Assert.Contains(shape.Animations, animation => animation.Code == "stargaze-up");
        Assert.All(shape.Animations, animation =>
            Assert.Equal(animation.Code == "lie" ? 0u : AnimationMetaData.GetCrc32(animation.Code), animation.CodeCrc32));
    }

    [Fact]
    public void EnsureSupineClips_Ignores_A_Shape_That_Is_Not_A_Seraph()
    {
        Assert.False(SkyLyingAnimation.EnsureSupineClips(null));
        Assert.False(SkyLyingAnimation.EnsureSupineClips(new Shape { Animations = [] }));
    }

    /// <summary>
    /// The clips exist twice over: here, for a shape the asset patch did not reach, and in
    /// assets/astraterra/patches/seraph-stargaze.json for the one it did. Both come out of
    /// tools/build_stargaze_clips.py, and they have drifted apart before -- the shipped patch said
    /// one thing while the code injected another, so what players saw was neither what the patch
    /// nor its tests described. This holds them together.
    /// </summary>
    [Fact]
    public void The_Code_And_The_Shipped_Patch_Describe_The_Same_Clips()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PatchPath));
        var patched = document.RootElement.EnumerateArray()
            .Select(operation => operation.GetProperty("value"))
            .ToDictionary(clip => clip.GetProperty("code").GetString()!);

        foreach (var clip in new[]
                 {
                     SkyLyingAnimation.Recline(),
                     SkyLyingAnimation.Idle(handsBehindHead: true),
                     SkyLyingAnimation.Idle(handsBehindHead: false),
                     SkyLyingAnimation.Rise()
                 })
        {
            var shipped = Assert.Contains(clip.Code, patched);
            Assert.Equal(clip.QuantityFrames, shipped.GetProperty("quantityframes").GetInt32());
            Assert.Equal(clip.OnAnimationEnd.ToString(), shipped.GetProperty("onAnimationEnd").GetString());
            Assert.False(shipped.TryGetProperty("version", out _), $"{clip.Code} declares a version the shape does not");

            var keyframes = shipped.GetProperty("keyframes");
            Assert.Equal(clip.KeyFrames.Length, keyframes.GetArrayLength());
            for (var i = 0; i < clip.KeyFrames.Length; i++)
            {
                Assert.Equal(clip.KeyFrames[i].Frame, keyframes[i].GetProperty("frame").GetInt32());
                var elements = keyframes[i].GetProperty("elements");
                Assert.Equal(clip.KeyFrames[i].Elements.Count, elements.EnumerateObject().Count());
                foreach (var (bone, pose) in clip.KeyFrames[i].Elements)
                {
                    var shippedPose = Assert.Contains(bone, elements.EnumerateObject().ToDictionary(p => p.Name, p => p.Value));
                    Assert.Equal(Value(pose.RotationX), Axis(shippedPose, "rotationX"), 3);
                    Assert.Equal(Value(pose.RotationY), Axis(shippedPose, "rotationY"), 3);
                    Assert.Equal(Value(pose.RotationZ), Axis(shippedPose, "rotationZ"), 3);
                    Assert.Equal(Value(pose.OffsetX), Axis(shippedPose, "offsetX"), 3);
                    Assert.Equal(Value(pose.OffsetY), Axis(shippedPose, "offsetY"), 3);
                    Assert.Equal(Value(pose.OffsetZ), Axis(shippedPose, "offsetZ"), 3);
                }
            }
        }
    }

    private static double Value(double? value) => value ?? 0.0;

    private static double Axis(JsonElement pose, string name)
        => pose.TryGetProperty(name, out var value) ? value.GetDouble() : 0.0;

    private static string PatchPath
        => Path.Combine(RepositoryRoot, "assets", "astraterra", "patches", "seraph-stargaze.json");

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AstraTerra.sln")))
            {
                directory = directory.Parent;
            }

            return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root.");
        }
    }
}
