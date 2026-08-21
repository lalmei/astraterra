using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Client.SkyLying;

/// <summary>
/// The clips that put the seraph on its back: a recline that gets it there, a supine idle it rests
/// in, with and without the hands behind the head, and a rise that gets it up again.
///
/// Two things about the engine shape all of this.
///
/// The first is animation versions. <c>ShapeElement.GetLocalTransformMatrix</c> composes a pose one
/// way at version 0 and another at version 1: version 0 turns a bone about its rotation origin --
/// its joint -- while version 1 turns it about the corner the cube's own coordinates start from.
/// On the seraph those are not the same point; the shoulder joint sits seven pixels from the upper
/// arm's corner. A version 1 pose therefore swings the arm off the shoulder and the elbow comes
/// apart from the forearm, which is what the earlier version of this pose did. Every one of
/// vanilla's 258 seraph clips is version 0, and so is every clip here.
///
/// The second is that a clip is eased in by blending its first keyframe against whatever pose the
/// seraph is already in. A clip whose frame 0 is the finished supine pose does not play as lying
/// down, it plays as a slide into lying down: the feet stay planted while the torso pitches back,
/// and the seraph bridges. So the recline starts from the standing pose and lowers the hips before
/// it pitches the torso, the way someone actually gets onto the ground.
///
/// The numbers come from <c>tools/build_stargaze_clips.py</c>, which solves the version 0 offsets
/// from where the joints should end up in the world and checks that nothing sinks into the ground
/// and no joint is pulled apart. It writes the same keyframes into
/// <c>assets/astraterra/patches/seraph-stargaze.json</c>; a test holds the two to each other.
/// </summary>
public static class SkyLyingAnimation
{
    /// <summary>
    /// Version 0, like every clip on the shape these are added to. A shape whose clips disagree
    /// logs "has mixed animation versions" and blends them wrongly, and version 0 is the one that
    /// turns a bone about its joint -- see the type comment.
    /// </summary>
    public const int AnimationVersion = 0;

    /// <summary>Frames in the recline. The engine runs 30 a second, so this is just under 0.7s.</summary>
    public const int ReclineFrames = 21;

    /// <summary>Frames in the breathing loop.</summary>
    public const int IdleFrames = 42;

    /// <summary>
    /// Frames in the rise. Getting up is quicker than settling down, so this is three quarters of
    /// the recline -- half a second.
    /// </summary>
    public const int RiseFrames = 16;

    /// <summary>The torso pitch that lays the seraph on its back. Swim uses the opposite sign for a face-down stroke.</summary>
    public const float BackPitchZ = -90f;

    private static readonly PoseFrame[] ReclinePose =
    [
        // Standing. The ease-in blends against this, so it has to be the pose the seraph is in.
        new(0,
        [
            Bone("LowerFootL"),
            Bone("LowerFootR"),
            Bone("LowerTorso"),
            Bone("UpperFootL"),
            Bone("UpperFootR"),
            Bone("UpperTorso"),
        ]),
        // Crouching: hips down, knees folded, weight forward.
        new(5,
        [
            Bone("LowerFootL", oy: 1f, rz: 74f),
            Bone("LowerFootR", oy: 1f, rz: 78f),
            Bone("LowerTorso", ox: -7.136f, oy: 0.175f, rz: 14f),
            Bone("UpperFootL", ry: 34f, rz: -44f),
            Bone("UpperFootR", rx: -20f, ry: -22f, rz: -74f),
            Bone("UpperTorso"),
        ]),
        // Sat on the ground, knees up, already leaning back.
        new(11,
        [
            Bone("LowerFootL", oy: 1f, rz: 84f),
            Bone("LowerFootR", oy: 1f, rz: 96f),
            Bone("LowerTorso", ox: -6.847f, oy: -6.808f, rz: -22f),
            Bone("UpperFootL", ry: 24f, rz: -52f),
            Bone("UpperFootR", rx: -12f, ry: -20f, rz: -70f),
            Bone("UpperTorso"),
        ]),
        // Going down: the back is most of the way to the ground and the left leg is straightening.
        new(16,
        [
            Bone("LowerFootL", oy: 1f, rz: 48f),
            Bone("LowerFootR", oy: 1f, rz: 94f),
            Bone("LowerTorso", ox: -0.583f, oy: -13.849f, rz: -62f),
            Bone("UpperFootL", ry: 8f, rz: -26f),
            Bone("UpperFootR", ry: -18f, rz: -54f),
            Bone("UpperTorso"),
        ]),
        // Settled, and identical to the idle's first frame so the handover is invisible.
        new(20, [.. SupineBody(chest: 6f)]),
    ];

    // Hands behind the head. The shoulders turn out so the elbows fall open onto the ground beside
    // the head rather than the arms being raised, which on a seraph flat on its back leaves them
    // waving in the air. The forearm's one pixel nudge is the trick every vanilla clip with a
    // folded elbow uses: it keeps the cubes meeting at the elbow.
    private static readonly BonePose[] BehindHead =
    [
        Bone("LowerArmL", oy: 1f, rx: -80f, ry: 58f, rz: -83f),
        Bone("LowerArmR", oy: 1f, rx: 80f, ry: -58f, rz: -83f),
        Bone("UpperArmL", rx: -60f, ry: 70f, rz: -21f),
        Bone("UpperArmR", rx: 60f, ry: -70f, rz: -21f),
    ];

    // Breathing: the chest lifts a couple of degrees and settles. Small enough to read as alive
    // rather than as movement, which is the difference between resting and being a dropped prop.
    private static readonly (int Frame, float Chest)[] Breath = [(0, 6f), (14, 8.2f), (28, 6.6f)];

    /// <summary>
    /// The recline, held on its last frame. It leaves the arms alone: during the half second it
    /// takes, a held telescope keeps its own pose, and empty hands are folded behind the head by
    /// the idle that follows.
    /// </summary>
    public static Animation Recline()
        => Clip(
            "StargazeRecline",
            SkyLyingPolicy.ShapeAnimationRecline,
            ReclinePose,
            ReclineFrames,
            EnumEntityAnimationEndHandling.Hold);

    /// <summary>
    /// Getting up: the recline read backwards on a shorter clock, which is both what getting up
    /// looks like -- sit up, tuck the legs under, stand -- and the only way the two cannot disagree
    /// about the pose they meet in. Without it, standing up is the engine easing the supine pose
    /// out, which is the same rigid slide as the bridge, just fast enough to read as a snap.
    /// </summary>
    public static Animation Rise()
        => Clip(
            "StargazeRise",
            SkyLyingPolicy.ShapeAnimationRise,
            [.. ReclinePose.Reverse().Select(frame => new PoseFrame(
                (int)Math.Round((ReclinePose[^1].Frame - frame.Frame) * 0.75), frame.Bones))],
            RiseFrames,
            EnumEntityAnimationEndHandling.Hold);

    /// <summary>
    /// The supine idle: on the back, one knee drawn up, breathing. With the hands free they go
    /// behind the head; with something held, the arms are left to the item's own animation.
    /// </summary>
    public static Animation Idle(bool handsBehindHead)
        => Clip(
            handsBehindHead ? "Stargaze" : "StargazeHold",
            SkyLyingPolicy.ShapeAnimationFor(handsBehindHead),
            [.. Breath.Select(step => new PoseFrame(step.Frame, [.. SupineBody(step.Chest), .. handsBehindHead ? BehindHead : []]))],
            IdleFrames,
            EnumEntityAnimationEndHandling.Repeat);

    /// <summary>
    /// Adds the clips to a seraph shape, replacing any the shape already carries. The asset patch
    /// puts the same clips on the vanilla player shape; this covers a shape that patch did not
    /// reach, and keeps the two from drifting by using the same keyframes.
    /// </summary>
    public static bool EnsureSupineClips(Shape? shape)
    {
        // Every seraph rig has 'lie'; a shape without it is not one, and the bone names below
        // would land on nothing.
        if (shape?.Animations is null || Find(shape.Animations, "lie") is null)
        {
            return false;
        }

        Upsert(shape, Recline());
        Upsert(shape, Idle(handsBehindHead: true));
        Upsert(shape, Idle(handsBehindHead: false));
        Upsert(shape, Rise());
        return true;
    }

    /// <summary>
    /// Flat on the back with the hips on the ground and the head roughly over where the player is
    /// standing, one knee drawn up with the foot planted and the other leg long and turned out.
    /// Two straight legs is what reads as a plank.
    /// </summary>
    private static BonePose[] SupineBody(float chest) =>
    [
        Bone("LowerFootL", rz: 10f),
        Bone("LowerFootR", oy: 1f, rz: 86f),
        Bone("LowerTorso", ox: 10.107f, oy: -15.223f, rz: BackPitchZ),
        Bone("UpperFootL", ry: -16f, rz: -4f),
        Bone("UpperFootR", ry: -16f, rz: -40f),
        Bone("UpperTorso", rz: chest),
    ];

    private static Animation Clip(
        string name,
        string code,
        PoseFrame[] frames,
        int quantityFrames,
        EnumEntityAnimationEndHandling onAnimationEnd)
    {
        var animation = new Animation
        {
            Name = name,
            Code = code,
            CodeCrc32 = AnimationMetaData.GetCrc32(code),
            Version = AnimationVersion,
            QuantityFrames = quantityFrames,
            OnActivityStopped = EnumEntityActivityStoppedHandling.EaseOut,
            OnAnimationEnd = onAnimationEnd,
            KeyFrames = new AnimationKeyFrame[frames.Length]
        };

        for (var i = 0; i < frames.Length; i++)
        {
            var elements = new Dictionary<string, AnimationKeyFrameElement>();
            foreach (var bone in frames[i].Bones)
            {
                elements[bone.Name] = new AnimationKeyFrameElement
                {
                    RotationX = bone.RotationX,
                    RotationY = bone.RotationY,
                    RotationZ = bone.RotationZ,
                    OffsetX = bone.OffsetX,
                    OffsetY = bone.OffsetY,
                    OffsetZ = 0
                };
            }

            animation.KeyFrames[i] = new AnimationKeyFrame { Frame = frames[i].Frame, Elements = elements };
        }

        return animation;
    }

    private static BonePose Bone(string name, float rx = 0f, float ry = 0f, float rz = 0f, float ox = 0f, float oy = 0f)
        => new(name, rx, ry, rz, ox, oy);

    private static void Upsert(Shape shape, Animation animation)
    {
        var existing = IndexOf(shape.Animations, animation.Code);
        if (existing >= 0)
        {
            shape.Animations[existing] = animation;
        }
        else
        {
            var next = new Animation[shape.Animations.Length + 1];
            Array.Copy(shape.Animations, next, shape.Animations.Length);
            next[^1] = animation;
            shape.Animations = next;
        }

        shape.AnimationsByCrc32 ??= new Dictionary<uint, Animation>();
        shape.AnimationsByCrc32[animation.CodeCrc32] = animation;
    }

    private static Animation? Find(Animation[] animations, string code)
    {
        foreach (var animation in animations)
        {
            if (string.Equals(animation.Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return animation;
            }
        }

        return null;
    }

    private static int IndexOf(Animation[] animations, string code)
    {
        for (var i = 0; i < animations.Length; i++)
        {
            if (string.Equals(animations[i].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private readonly record struct BonePose(string Name, float RotationX, float RotationY, float RotationZ, float OffsetX, float OffsetY);

    private readonly record struct PoseFrame(int Frame, BonePose[] Bones);
}
