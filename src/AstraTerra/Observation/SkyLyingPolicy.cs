namespace AstraTerra.Observation;

/// <summary>
/// When the player may lie back to watch the sky, and how far the camera drops.
/// Sitting already halves eye height; lying goes lower so the view is from the ground.
/// </summary>
public static class SkyLyingPolicy
{
    /// <summary>
    /// Vanilla sitting uses 0.5 of standing eye height. Lying uses the same multiplier as a
    /// prone/dead pose so the camera sits just above the ground.
    /// </summary>
    public const double EyeHeightMultiplier = 0.25;

    /// <summary>
    /// Collision shrinks with the pose so the camera is not trapped inside a standing hitbox.
    /// Kept a little taller than the eye so the view does not clip into the block below.
    /// </summary>
    public const double CollisionHeightMultiplier = 0.35;

    public const float AnimationEaseInSpeed = 2f;

    public const float AnimationEaseOutSpeed = 5f;

    /// <summary>
    /// Vanilla's first-person pitch floor: just short of straight up. ClientMain clamps pitch to
    /// this value, which is <c>π/2 + 0.015</c>.
    /// </summary>
    public const float LookUpPitch = 1.5857964f;

    /// <summary>
    /// How far the body may yaw while lying. Matches the sit-on-edge pin so looking around turns
    /// the head and camera, not the whole seraph on the ground.
    /// </summary>
    public const float BodyYawLimitRadians = 0.2f;

    /// <summary>
    /// Shape animation on the seraph. Beds use <c>lie</c>, which rolls onto the side; this clip
    /// keeps the back on the ground so the sky is not blocked. Empty-handed stargazing also puts
    /// the hands behind the head.
    /// </summary>
    public const string ShapeAnimation = "stargaze";

    /// <summary>
    /// Same supine body as <see cref="ShapeAnimation"/>, without the arms, so held instruments can
    /// keep their own poses.
    /// </summary>
    public const string ShapeAnimationHolding = "stargaze-hold";

    /// <summary>Our own animation code so stopping it cannot cancel a bed sleep.</summary>
    public const string AnimationCode = "astraterra-stargaze";

    public static double EyeHeight(double standingEyeHeight)
        => standingEyeHeight * EyeHeightMultiplier;

    public static double CollisionHeight(double standingCollisionHeight)
        => standingCollisionHeight * CollisionHeightMultiplier;

    public static double StepToward(double current, double target, float dt)
    {
        var delta = (target - current) * 5.0 * dt;
        return delta > 0.0
            ? Math.Min(current + delta, target)
            : Math.Max(current + delta, target);
    }

    public static bool CanBegin(
        bool alive,
        bool onGround,
        bool tryingToMove,
        bool flying,
        bool swimming,
        bool mounted,
        bool spectator)
        => alive
           && onGround
           && !tryingToMove
           && !flying
           && !swimming
           && !mounted
           && !spectator;

    public static bool ShouldStandUp(
        bool alive,
        bool onGround,
        bool tryingToMove,
        bool jumping,
        bool flying,
        bool swimming,
        bool mounted)
        => !alive
           || !onGround
           || tryingToMove
           || jumping
           || flying
           || swimming
           || mounted;

    public static bool ShouldLowerEyes(bool isLying, string? entityPlayerUid, string? localPlayerUid)
        => isLying
           && !string.IsNullOrEmpty(entityPlayerUid)
           && string.Equals(entityPlayerUid, localPlayerUid, StringComparison.Ordinal);

    /// <summary>
    /// First person looks straight up at the sky. Third person and the fixed overhead camera keep
    /// whatever pitch they already had.
    /// </summary>
    public static bool ShouldAimAtZenith(bool firstPerson) => firstPerson;

    public static bool ShouldPinBodyYaw(bool isLying) => isLying;

    /// <summary>
    /// Hands go behind the head only when both are empty. A held telescope, sextant, or astrolabe
    /// keeps its own arm pose.
    /// </summary>
    public static bool UseHandsBehindHead(bool rightEmpty, bool leftEmpty)
        => rightEmpty && leftEmpty;

    public static string ShapeAnimationFor(bool handsBehindHead)
        => handsBehindHead ? ShapeAnimation : ShapeAnimationHolding;
}
