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

    private ICoreClientAPI? api;
    private long tickListenerId;
    private bool? playingHandsBehindHead;
    private float? pinnedBodyYaw;

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

        StandUp();
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
            StandUp();
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
        if (!SkyLyingState.IsLying)
        {
            return;
        }

        var entity = LocalPlayerEntity();
        if (entity is null || ShouldStandUp(entity))
        {
            StandUp();
            return;
        }

        EnsureAnimation(entity, HandsAreEmpty(entity));
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

        EnsureAnimation(entity, HandsAreEmpty(entity));
        PinBodyYaw(entity);
    }

    private void StandUp()
    {
        var entity = LocalPlayerEntity();
        ReleaseBodyYaw(entity);
        StopStargaze(entity);
        SkyLyingState.End();
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
        manager.StopAnimation(SkyLyingPolicy.ShapeAnimation);
        manager.StopAnimation(SkyLyingPolicy.ShapeAnimationHolding);
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
