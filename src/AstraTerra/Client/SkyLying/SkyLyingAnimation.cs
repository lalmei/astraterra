using AstraTerra.Observation;
using Vintagestory.API.Common;

namespace AstraTerra.Client.SkyLying;

/// <summary>
/// The bed <c>lie</c> clip pitches the seraph onto its back with <c>rotationZ: -90</c>, then rolls
/// it onto the side with <c>rotationX: -112.5</c>. Stargazing keeps the back-pitch, drops the roll,
/// and lowers the hips onto the ground. Swim uses the opposite Z (<c>+80</c>) for a face-down
/// stroke, which is how these axes are known.
/// Seraph clips are animation version 0, which applies offsets after rotation — so a Y drop would
/// slide sideways. Version 1 applies the drop first, the same space sit-on-G uses.
/// </summary>
public static class SkyLyingAnimation
{
    /// <summary>
    /// Parent-space drop, applied before the back-pitch when <see cref="AnimationVersion"/> is 1.
    /// Sit-on-G uses the same 13-unit hip drop.
    /// </summary>
    public const float GroundOffsetY = -13f;

    public const float GroundOffsetZ = -11f;

    public const float BackPitchZ = -90f;

    /// <summary>Vanilla ClientAnimator uses the max version of active clips for offset order.</summary>
    public const int AnimationVersion = 1;

    public const float UpperArmRaiseZ = -192f;

    public const float UpperArmRWrapY = -48f;

    public const float UpperArmLWrapY = 48f;

    public const float UpperArmRBackX = 28f;

    public const float UpperArmLBackX = -28f;

    public const float LowerArmRBendX = -58f;

    public const float LowerArmLBendX = 58f;

    public const float LowerArmRWrapY = 28f;

    public const float LowerArmLWrapY = -28f;

    public const float LowerArmWrapZ = 18f;

    public static Animation ToSupine(Animation lie)
        => ToSupine(lie, handsBehindHead: true);

    public static Animation ToSupine(Animation lie, bool handsBehindHead)
    {
        var stargaze = lie.Clone();
        stargaze.Name = handsBehindHead ? "Stargaze" : "StargazeHold";
        stargaze.Code = SkyLyingPolicy.ShapeAnimationFor(handsBehindHead);
        stargaze.CodeCrc32 = AnimationMetaData.GetCrc32(stargaze.Code);
        stargaze.Version = AnimationVersion;
        if (lie.KeyFrames is not { } source)
        {
            return stargaze;
        }

        stargaze.KeyFrames = new AnimationKeyFrame[source.Length];
        for (var i = 0; i < source.Length; i++)
        {
            var frame = new AnimationKeyFrame { Frame = source[i].Frame };
            ApplySupinePose(frame, handsBehindHead);
            stargaze.KeyFrames[i] = frame;
        }

        return stargaze;
    }

    public static bool EnsureSupineClip(Shape? shape)
    {
        if (shape?.Animations is null)
        {
            return false;
        }

        var lie = Find(shape.Animations, "lie");
        if (lie is null)
        {
            return false;
        }

        Upsert(shape, ToSupine(lie, handsBehindHead: true));
        Upsert(shape, ToSupine(lie, handsBehindHead: false));
        return true;
    }

    private static void ApplySupinePose(AnimationKeyFrame frame, bool handsBehindHead)
    {
        var torso = Element(frame, "LowerTorso");
        torso.OffsetX = 0;
        torso.OffsetY = GroundOffsetY;
        torso.OffsetZ = GroundOffsetZ;
        torso.RotationX = 0;
        torso.RotationY = 0;
        torso.RotationZ = BackPitchZ;

        Zero(Element(frame, "UpperTorso"));
        Zero(Element(frame, "UpperFootL"));
        Zero(Element(frame, "LowerFootL"));
        Zero(Element(frame, "UpperFootR"));
        Zero(Element(frame, "LowerFootR"));

        if (handsBehindHead)
        {
            Pose(Element(frame, "UpperArmR"), UpperArmRBackX, UpperArmRWrapY, UpperArmRaiseZ);
            Pose(Element(frame, "UpperArmL"), UpperArmLBackX, UpperArmLWrapY, UpperArmRaiseZ);
            Pose(Element(frame, "LowerArmR"), LowerArmRBendX, LowerArmRWrapY, LowerArmWrapZ);
            Pose(Element(frame, "LowerArmL"), LowerArmLBendX, LowerArmLWrapY, LowerArmWrapZ);
            return;
        }

        Zero(Element(frame, "UpperArmR"));
        Zero(Element(frame, "UpperArmL"));
        Zero(Element(frame, "LowerArmR"));
        Zero(Element(frame, "LowerArmL"));
    }

    private static AnimationKeyFrameElement Element(AnimationKeyFrame frame, string name)
    {
        frame.Elements ??= new Dictionary<string, AnimationKeyFrameElement>();
        if (!frame.Elements.TryGetValue(name, out var element) || element is null)
        {
            element = new AnimationKeyFrameElement();
            frame.Elements[name] = element;
        }

        return element;
    }

    private static void Zero(AnimationKeyFrameElement element)
    {
        element.OffsetX = 0;
        element.OffsetY = 0;
        element.OffsetZ = 0;
        element.RotationX = 0;
        element.RotationY = 0;
        element.RotationZ = 0;
    }

    private static void Pose(AnimationKeyFrameElement element, float rotationX, float rotationY, float rotationZ)
    {
        Zero(element);
        element.RotationX = rotationX;
        element.RotationY = rotationY;
        element.RotationZ = rotationZ;
    }

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
}
