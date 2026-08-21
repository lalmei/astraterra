using AstraTerra.Observation;
using Xunit;

namespace AstraTerra.Tests.Observation;

public sealed class SkyLyingPolicyTests
{
    [Fact]
    public void Eye_Height_Is_Lower_Than_Sitting()
    {
        const double standing = 1.7;

        Assert.Equal(0.425, SkyLyingPolicy.EyeHeight(standing));
        Assert.True(SkyLyingPolicy.EyeHeight(standing) < standing * 0.5);
    }

    [Fact]
    public void Collision_Stays_Taller_Than_The_Camera()
    {
        const double standingEye = 1.7;
        const double standingCollision = 1.85;

        Assert.True(SkyLyingPolicy.CollisionHeight(standingCollision) > SkyLyingPolicy.EyeHeight(standingEye));
    }

    [Fact]
    public void StepToward_Uses_The_Vanilla_Sit_Lerp()
    {
        Assert.Equal(0.425, SkyLyingPolicy.StepToward(0.425, 0.425, 0.05f), 6);
        Assert.True(SkyLyingPolicy.StepToward(1.7, 0.425, 0.05f) < 1.7);
        Assert.True(SkyLyingPolicy.StepToward(1.7, 0.425, 0.05f) > 0.425);
        Assert.Equal(0.425, SkyLyingPolicy.StepToward(0.5, 0.425, 10f), 6);
    }

    [Fact]
    public void CanBegin_Requires_Standing_Still_On_Solid_Ground()
    {
        Assert.True(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: true,
            tryingToMove: false,
            flying: false,
            swimming: false,
            mounted: false,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: true,
            tryingToMove: true,
            flying: false,
            swimming: false,
            mounted: false,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: false,
            tryingToMove: false,
            flying: false,
            swimming: false,
            mounted: false,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: true,
            tryingToMove: false,
            flying: true,
            swimming: false,
            mounted: false,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: true,
            tryingToMove: false,
            flying: false,
            swimming: true,
            mounted: false,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: true,
            tryingToMove: false,
            flying: false,
            swimming: false,
            mounted: true,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: false,
            onGround: true,
            tryingToMove: false,
            flying: false,
            swimming: false,
            mounted: false,
            spectator: false));
        Assert.False(SkyLyingPolicy.CanBegin(
            alive: true,
            onGround: true,
            tryingToMove: false,
            flying: false,
            swimming: false,
            mounted: false,
            spectator: true));
    }

    [Fact]
    public void Movement_Jump_Flight_Or_Water_Stands_The_Player_Up()
    {
        Assert.False(SkyLyingPolicy.ShouldStandUp(
            alive: true,
            onGround: true,
            tryingToMove: false,
            jumping: false,
            flying: false,
            swimming: false,
            mounted: false));
        Assert.True(SkyLyingPolicy.ShouldStandUp(
            alive: true,
            onGround: true,
            tryingToMove: true,
            jumping: false,
            flying: false,
            swimming: false,
            mounted: false));
        Assert.True(SkyLyingPolicy.ShouldStandUp(
            alive: true,
            onGround: true,
            tryingToMove: false,
            jumping: true,
            flying: false,
            swimming: false,
            mounted: false));
        Assert.True(SkyLyingPolicy.ShouldStandUp(
            alive: true,
            onGround: false,
            tryingToMove: false,
            jumping: false,
            flying: false,
            swimming: false,
            mounted: false));
    }

    [Fact]
    public void Pose_Is_On_The_Back_Not_The_Bed_Side_Sleep()
    {
        Assert.Equal("stargaze", SkyLyingPolicy.ShapeAnimation);
        Assert.NotEqual("lie", SkyLyingPolicy.ShapeAnimation);
        Assert.Equal("astraterra-stargaze", SkyLyingPolicy.AnimationCode);
    }

    [Fact]
    public void Look_Up_Pitch_Is_The_Vanilla_Zenith_Clamp()
    {
        Assert.Equal(1.5857964f, SkyLyingPolicy.LookUpPitch);
    }

    [Fact]
    public void Eyes_Drop_Only_For_The_Local_Lying_Player()
    {
        Assert.True(SkyLyingPolicy.ShouldLowerEyes(true, "local", "local"));
        Assert.False(SkyLyingPolicy.ShouldLowerEyes(false, "local", "local"));
        Assert.False(SkyLyingPolicy.ShouldLowerEyes(true, "other", "local"));
        Assert.False(SkyLyingPolicy.ShouldLowerEyes(true, null, "local"));
        Assert.False(SkyLyingPolicy.ShouldLowerEyes(true, "local", null));
    }

    [Fact]
    public void Zenith_Aim_Is_First_Person_Only()
    {
        Assert.True(SkyLyingPolicy.ShouldAimAtZenith(firstPerson: true));
        Assert.False(SkyLyingPolicy.ShouldAimAtZenith(firstPerson: false));
    }

    [Fact]
    public void Body_Yaw_Is_Pinned_While_Lying()
    {
        Assert.True(SkyLyingPolicy.ShouldPinBodyYaw(isLying: true));
        Assert.False(SkyLyingPolicy.ShouldPinBodyYaw(isLying: false));
        Assert.Equal(0.2f, SkyLyingPolicy.BodyYawLimitRadians);
    }

    [Fact]
    public void Hands_Go_Behind_The_Head_Only_When_Both_Are_Empty()
    {
        Assert.True(SkyLyingPolicy.UseHandsBehindHead(rightEmpty: true, leftEmpty: true));
        Assert.False(SkyLyingPolicy.UseHandsBehindHead(rightEmpty: false, leftEmpty: true));
        Assert.False(SkyLyingPolicy.UseHandsBehindHead(rightEmpty: true, leftEmpty: false));
        Assert.Equal("stargaze", SkyLyingPolicy.ShapeAnimationFor(handsBehindHead: true));
        Assert.Equal("stargaze-hold", SkyLyingPolicy.ShapeAnimationFor(handsBehindHead: false));
        Assert.Equal("stargaze-down", SkyLyingPolicy.ShapeAnimationRecline);
        Assert.NotEqual(SkyLyingPolicy.AnimationCode, SkyLyingPolicy.AnimationCodeRecline);
    }

    /// <summary>
    /// The recline runs on its own before the idle takes over -- 21 keyframes at the engine's 30 a
    /// second. Handing over early would blend the two halfway through the motion.
    /// </summary>
    [Fact]
    public void The_Idle_Waits_For_The_Recline_To_Play_Out()
    {
        Assert.False(SkyLyingPolicy.ShouldSettleIntoIdle(0));
        Assert.False(SkyLyingPolicy.ShouldSettleIntoIdle(SkyLyingPolicy.ReclineMilliseconds - 1));
        Assert.True(SkyLyingPolicy.ShouldSettleIntoIdle(SkyLyingPolicy.ReclineMilliseconds));
        Assert.True(SkyLyingPolicy.ShouldSettleIntoIdle(10_000));
        Assert.Equal(700L, SkyLyingPolicy.ReclineMilliseconds);
    }

    /// <summary>
    /// Both clips have to reach full weight quickly: an eased-in clip shows as a blend with
    /// standing, and a half-weighted supine pose is a seraph part-way back onto its feet.
    /// </summary>
    [Fact]
    public void The_Clips_Ease_In_Faster_Than_They_Play()
    {
        Assert.True(SkyLyingPolicy.ReclineEaseInSpeed >= SkyLyingPolicy.AnimationEaseInSpeed);
        Assert.True(SkyLyingPolicy.AnimationEaseInSpeed > 2f);
    }
}
