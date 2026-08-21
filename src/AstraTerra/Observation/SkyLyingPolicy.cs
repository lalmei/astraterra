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

    /// <summary>
    /// The recline is driven by its own keyframes, so it has to reach full weight quickly: while a
    /// clip is still easing in, what shows is a blend between it and standing, and a half-eased
    /// recline is the slide into the pose the keyframes exist to avoid.
    /// </summary>
    public const float ReclineEaseInSpeed = 9f;

    /// <summary>
    /// The idle crossfades in as the recline fades out. Both are the same pose by then, but a slow
    /// fade would leave the pair under-weighted for half a second, and an under-weighted supine
    /// pose is a seraph half-way back onto its feet.
    /// </summary>
    public const float AnimationEaseInSpeed = 6f;

    public const float AnimationEaseOutSpeed = 5f;

    /// <summary>
    /// How long the recline runs before the idle takes over: its 21 frames at the engine's 30 a
    /// second. The idle's first frame is the recline's last, so the handover does not show.
    /// </summary>
    public const long ReclineMilliseconds = 700L;

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

    /// <summary>
    /// Sitting down and reclining onto the back. Played once, on its own, before the idle: a clip
    /// that starts from the finished pose eases in as a backbend, feet planted and hips in the air.
    /// </summary>
    public const string ShapeAnimationRecline = "stargaze-down";

    /// <summary>Our own animation code so stopping it cannot cancel a bed sleep.</summary>
    public const string AnimationCode = "astraterra-stargaze";

    /// <summary>Own code for the recline as well, so the two can be started and stopped apart.</summary>
    public const string AnimationCodeRecline = "astraterra-stargaze-down";

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

    /// <summary>
    /// Whether the recline has played out and the idle should take over. Measured from when lying
    /// began, so a frame drop delays the handover rather than cutting the recline short.
    /// </summary>
    public static bool ShouldSettleIntoIdle(long millisecondsLying)
        => millisecondsLying >= ReclineMilliseconds;
}
