using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AstraTerra.Client.SkyLying;

/// <summary>
/// Lie-down counterpart to vanilla sit-on-G: a remappable hotkey that plays a supine stargaze pose
/// and drops the camera so the player looks straight up from the ground.
/// </summary>
public sealed class SkyLyingController
{
    public const string HotkeyCode = "astraterra-liedown";

    /// <summary>
    /// Vanilla binds X to swapping the off-hand, so this must not default there.
    /// Sit is G; Z is unused on the stock control list.
    /// </summary>
    public const GlKeys DefaultKey = GlKeys.Z;

    private static readonly AnimationMetaData HandsFreeAnimation = CreateAnimation(includeArms: true);

    private static readonly AnimationMetaData HandsOccupiedAnimation = CreateAnimation(includeArms: false);

    /// <summary>
    /// The sit-down-and-recline that runs before the idle. It leaves the arms out, so a held
    /// telescope keeps its own pose on the way down.
    /// </summary>
    private static readonly AnimationMetaData ReclineAnimation = CreateTransition(
        SkyLyingPolicy.AnimationCodeRecline, SkyLyingPolicy.ShapeAnimationRecline);

    /// <summary>
    /// Sitting up and standing again. Started as lying ends, and it outlives that state: the tick
    /// stops it once it has played, which is what hands the seraph back to walking and idling.
    /// </summary>
    private static readonly AnimationMetaData RiseAnimation = CreateTransition(
        SkyLyingPolicy.AnimationCodeRise, SkyLyingPolicy.ShapeAnimationRise);

    private ICoreClientAPI? api;
    private long tickListenerId;
    private bool? playingHandsBehindHead;
    private float? pinnedBodyYaw;
    private long lieDownAt;
    private bool settled;
    private long riseStartedAt;

    public void Start(ICoreClientAPI clientApi)
    {
        api = clientApi;
        clientApi.Input.RegisterHotKey(
            HotkeyCode,
            Lang.Get("astraterra:hotkey-liedown"),
            DefaultKey,
            HotkeyType.CharacterControls);
        RelocateOffhandConflict(clientApi);
        clientApi.Input.SetHotKeyHandler(HotkeyCode, _ => Toggle());
        tickListenerId = clientApi.Event.RegisterGameTickListener(OnGameTick, 20);
    }

    public void Stop()
    {
        if (api is not null && tickListenerId != 0)
        {
            api.Event.UnregisterGameTickListener(tickListenerId);
        }

        StandUp(voluntary: false);
        StopRise(LocalPlayerEntity());
        SkyLyingState.Reset();
        api = null;
        tickListenerId = 0;
    }

    /// <summary>
    /// An earlier default used X, which is vanilla off-hand swap. Anyone still on that leftover
    /// mapping is moved to <see cref="DefaultKey"/>.
    /// </summary>
    public static bool RelocateOffhandConflict(HotKey? hotkey)
    {
        if (hotkey?.CurrentMapping is not { } mapping || mapping.KeyCode != (int)GlKeys.X)
        {
            return false;
        }

        mapping.KeyCode = (int)DefaultKey;
        mapping.Alt = false;
        mapping.Ctrl = false;
        mapping.Shift = false;
        return true;
    }

    private static void RelocateOffhandConflict(ICoreClientAPI clientApi)
        => RelocateOffhandConflict(clientApi.Input.GetHotKeyByCode(HotkeyCode));

    private bool Toggle()
    {
        if (SkyLyingState.IsLying)
        {
            // Standing up on purpose is worth animating; being stood up by walking or dying is not.
            StandUp(voluntary: true);
            return true;
        }

        var entity = LocalPlayerEntity();
        if (entity is null || !CanBegin(entity))
        {
            return false;
        }

        LieDown(entity);
        return true;
    }

    /// <summary>
    /// Snaps the camera to vanilla's zenith pitch. Mouse pitch must change too, or the next
    /// camera tick keeps looking at the horizon from ground height.
    /// </summary>
    public static void AimAtZenith(EntityPos pos, Action<float>? setMousePitch)
    {
        pos.Pitch = SkyLyingPolicy.LookUpPitch;
        setMousePitch?.Invoke(SkyLyingPolicy.LookUpPitch);
    }

    private void OnGameTick(float dt)
    {
        FinishRising();
        if (!SkyLyingState.IsLying)
        {
            return;
        }

        var entity = LocalPlayerEntity();
        if (entity is null || ShouldStandUp(entity))
        {
            StandUp(voluntary: false);
            return;
        }

        // The recline plays on its own first. Only once it has reached the ground does the idle
        // take over, so the two are never blended halfway through the motion.
        if (!settled && !SkyLyingPolicy.ShouldSettleIntoIdle(ElapsedMilliseconds() - lieDownAt))
        {
            PinBodyYaw(entity);
            return;
        }

        EnsureAnimation(entity, HandsAreEmpty(entity));
        settled = true;
        PinBodyYaw(entity);
    }

    private void LieDown(EntityPlayer entity)
    {
        SkyLyingState.Begin();
        entity.AnimManager.StopAnimation("sitflooridle");
        entity.AnimManager.StopAnimation("sitidle");
        if (SkyLyingPolicy.ShouldAimAtZenith(IsFirstPerson()))
        {
            AimAtZenith(entity.Pos, pitch =>
            {
                if (api?.World is ClientMain game)
                {
                    game.mousePitch = pitch;
                }
            });
        }

        StartRecline(entity);
        PinBodyYaw(entity);
    }

    private void StartRecline(EntityPlayer entity)
    {
        StopRise(entity);
        lieDownAt = ElapsedMilliseconds();
        settled = false;
        entity.TpAnimManager.UseFpAnmations = false;
        entity.AnimManager.StartAnimation(ReclineAnimation);
        entity.TpAnimManager.StartAnimation(ReclineAnimation);
    }

    private long ElapsedMilliseconds() => api?.World?.ElapsedMilliseconds ?? 0L;

    private void StandUp(bool voluntary)
    {
        var entity = LocalPlayerEntity();
        ReleaseBodyYaw(entity);
        StopStargaze(entity);
        SkyLyingState.End();

        if (entity is not null && SkyLyingPolicy.ShouldPlayRise(voluntary, entity.Alive))
        {
            StartRise(entity);
        }
    }

    private void StartRise(EntityPlayer entity)
    {
        riseStartedAt = ElapsedMilliseconds();
        entity.AnimManager.StartAnimation(RiseAnimation);
        entity.TpAnimManager.StartAnimation(RiseAnimation);
    }

    /// <summary>
    /// The rise suppresses the default animations while it plays, so it has to be taken off again
    /// or the first step after standing up would be a glide.
    /// </summary>
    private void FinishRising()
    {
        if (riseStartedAt == 0L || !SkyLyingPolicy.ShouldFinishRising(ElapsedMilliseconds() - riseStartedAt))
        {
            return;
        }

        riseStartedAt = 0L;
        StopRise(LocalPlayerEntity());
    }

    private void StopRise(EntityPlayer? entity)
    {
        riseStartedAt = 0L;
        if (entity is null)
        {
            return;
        }

        entity.AnimManager.StopAnimation(SkyLyingPolicy.AnimationCodeRise);
        entity.AnimManager.StopAnimation(SkyLyingPolicy.ShapeAnimationRise);
        entity.TpAnimManager.StopAnimation(SkyLyingPolicy.AnimationCodeRise);
        entity.TpAnimManager.StopAnimation(SkyLyingPolicy.ShapeAnimationRise);
    }

    private void EnsureAnimation(EntityPlayer entity, bool handsBehindHead)
    {
        entity.TpAnimManager.UseFpAnmations = false;
        if (playingHandsBehindHead == handsBehindHead
            && IsStargazeActive(entity.AnimManager)
            && IsStargazeActive(entity.TpAnimManager))
        {
            return;
        }

        StopStargaze(entity);
        var animation = handsBehindHead ? HandsFreeAnimation : HandsOccupiedAnimation;
        entity.AnimManager.StartAnimation(animation);
        entity.TpAnimManager.StartAnimation(animation);
        playingHandsBehindHead = handsBehindHead;
    }

    private void StopStargaze(EntityPlayer? entity)
    {
        if (entity is not null)
        {
            StopOn(entity.AnimManager);
            StopOn(entity.TpAnimManager);
        }

        playingHandsBehindHead = null;
        settled = false;
    }

    private void PinBodyYaw(EntityPlayer entity)
    {
        if (!SkyLyingPolicy.ShouldPinBodyYaw(true))
        {
            return;
        }

        pinnedBodyYaw ??= entity.BodyYaw;
        entity.BodyYawLimits = new AngleConstraint(pinnedBodyYaw.Value, SkyLyingPolicy.BodyYawLimitRadians);
    }

    private void ReleaseBodyYaw(EntityPlayer? entity)
    {
        if (entity is not null)
        {
            entity.BodyYawLimits = null;
        }

        pinnedBodyYaw = null;
    }

    private static void StopOn(IAnimationManager manager)
    {
        manager.StopAnimation(SkyLyingPolicy.AnimationCode);
        manager.StopAnimation(SkyLyingPolicy.AnimationCodeRecline);
        manager.StopAnimation(SkyLyingPolicy.ShapeAnimation);
        manager.StopAnimation(SkyLyingPolicy.ShapeAnimationHolding);
        manager.StopAnimation(SkyLyingPolicy.ShapeAnimationRecline);
    }

    private static bool IsStargazeActive(IAnimationManager manager)
        => manager.IsAnimationActive(SkyLyingPolicy.ShapeAnimation)
           || manager.IsAnimationActive(SkyLyingPolicy.ShapeAnimationHolding)
           || manager.IsAnimationActive(SkyLyingPolicy.AnimationCode);

    private static bool HandsAreEmpty(EntityPlayer entity)
        => SkyLyingPolicy.UseHandsBehindHead(
            entity.RightHandItemSlot?.Empty ?? true,
            entity.LeftHandItemSlot?.Empty ?? true);

    private bool IsFirstPerson()
        => api?.Render.CameraType == EnumCameraMode.FirstPerson;

    private EntityPlayer? LocalPlayerEntity()
        => api?.World?.Player?.Entity as EntityPlayer;

    private static bool CanBegin(EntityPlayer entity)
    {
        var controls = entity.Controls;
        return SkyLyingPolicy.CanBegin(
            alive: entity.Alive,
            onGround: entity.OnGround,
            tryingToMove: controls.TriesToMove,
            flying: controls.IsFlying,
            swimming: entity.Swimming,
            mounted: entity.MountedOn is not null,
            spectator: entity.Player?.WorldData.CurrentGameMode == EnumGameMode.Spectator);
    }

    private static bool ShouldStandUp(EntityPlayer entity)
    {
        var controls = entity.Controls;
        return SkyLyingPolicy.ShouldStandUp(
            alive: entity.Alive,
            onGround: entity.OnGround,
            tryingToMove: controls.TriesToMove,
            jumping: controls.Jump,
            flying: controls.IsFlying,
            swimming: entity.Swimming,
            mounted: entity.MountedOn is not null);
    }

    /// <summary>
    /// The two transitions, which differ only in which clip they play. Both reach full weight fast:
    /// until a clip is eased in, what shows is a blend between it and standing, and a half-weighted
    /// recline is the backbend it exists to replace. Neither weights the arms, so an instrument in
    /// hand keeps its own pose on the way down and back up.
    /// </summary>
    private static AnimationMetaData CreateTransition(string code, string animation)
    {
        var meta = new AnimationMetaData
        {
            Code = code,
            Animation = animation,
            BlendMode = EnumAnimationBlendMode.Average,
            EaseInSpeed = SkyLyingPolicy.ReclineEaseInSpeed,
            EaseOutSpeed = SkyLyingPolicy.AnimationEaseOutSpeed,
            ClientSide = true,
            SupressDefaultAnimation = true,
            HoldEyePosAfterEasein = 99f
        };

        Weight(meta, "LowerTorso");
        Weight(meta, "UpperTorso");
        Weight(meta, "UpperFootR");
        Weight(meta, "UpperFootL");
        Weight(meta, "LowerFootR");
        Weight(meta, "LowerFootL");
        return meta.Init();
    }

    private static AnimationMetaData CreateAnimation(bool includeArms)
    {
        var meta = new AnimationMetaData
        {
            Code = SkyLyingPolicy.AnimationCode,
            Animation = SkyLyingPolicy.ShapeAnimationFor(includeArms),
            BlendMode = EnumAnimationBlendMode.Average,
            EaseInSpeed = SkyLyingPolicy.AnimationEaseInSpeed,
            EaseOutSpeed = SkyLyingPolicy.AnimationEaseOutSpeed,
            ClientSide = true,
            SupressDefaultAnimation = true,
            HoldEyePosAfterEasein = 99f
        };

        Weight(meta, "LowerTorso");
        Weight(meta, "UpperTorso");
        Weight(meta, "UpperFootR");
        Weight(meta, "UpperFootL");
        Weight(meta, "LowerFootR");
        Weight(meta, "LowerFootL");
        if (includeArms)
        {
            Weight(meta, "UpperArmR");
            Weight(meta, "UpperArmL");
            Weight(meta, "LowerArmR");
            Weight(meta, "LowerArmL");
        }

        return meta.Init();
    }

    private static void Weight(AnimationMetaData meta, string bone)
    {
        meta.ElementWeight[bone] = 80;
        meta.ElementBlendMode[bone] = EnumAnimationBlendMode.Average;
    }
}
