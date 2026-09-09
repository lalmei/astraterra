using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace AstraTerra.Astronomy;

[ProtoContract]
public sealed class LongitudeAwareSunConfigPacket
{
    [ProtoMember(1)]
    public bool Enabled;

    /// <summary>
    /// Whether this server lets a generated world run on its own axial tilt rather than Earth's.
    /// </summary>
    /// <remarks>
    /// Carried on the same packet as the longitude term because it is the same kind of setting and
    /// reaches the same wrapper: both change where the sun is, so both are the server's to decide
    /// and neither may differ between a server and its clients. A client that tipped its own world
    /// while the server did not would have summer in a different month from the crops it is
    /// growing.
    /// </remarks>
    [ProtoMember(2)]
    public bool WorldTiltEnabled = true;
}

public enum SunDelegateUpdateKind
{
    None,
    Installed,
    Restored,
    WaitingForBaseDelegate
}

public readonly record struct SunDelegateUpdate(
    SunDelegateUpdateKind Kind,
    SolarSphericalCoordsDelegate? Delegate)
{
    public bool ReplacesCurrent
        => Kind is SunDelegateUpdateKind.Installed or SunDelegateUpdateKind.Restored;
}

/// <summary>
/// Pure state machine for the two things AstraTerra does to the sun: shifting it by the observer's
/// longitude, and running it on a world's own axial tilt instead of Earth's. Keeping this separate
/// from the game lifecycle makes startup ordering and restoration testable.
/// </summary>
/// <remarks>
/// <para>
/// Both live here rather than in two installers because there is one delegate property on the
/// calendar and two wrappers would race for it: whichever installed second would capture the first
/// and, on a tilt substitution -- which replaces the survival delegate's answer rather than
/// adjusting its inputs -- silently drop it. One owner, one wrapper, both terms applied in the
/// order the sky applies them: longitude moves the observer's clock, and the tilt decides where the
/// sun is on that clock.
/// </para>
/// <para>
/// The two are still independently switchable, and either alone is enough to install the wrapper.
/// With neither set the calendar keeps the survival delegate untouched.
/// </para>
/// </remarks>
public sealed class LongitudeAwareSunController
{
    private readonly System.Func<double, double> longitudeProvider;
    private readonly System.Func<double, double>? latitudeProvider;
    private bool lifecycleReady;
    private bool? enabled;
    private bool worldTiltAllowed = true;
    private double? worldTiltDeg;
    private SolarSphericalCoordsDelegate? chainedDelegate;
    private SolarSphericalCoordsDelegate? installedDelegate;

    /// <param name="longitudeProvider">The observer's longitude in degrees for a world X.</param>
    /// <param name="latitudeProvider">
    /// The game's own latitude for a world Z, in its <c>-1..1</c> form. Required only for the tilt
    /// substitution, which has to rebuild the sun's triangle rather than nudge the survival
    /// delegate's inputs; without it the tilt term stays off and the longitude term still works.
    /// </param>
    public LongitudeAwareSunController(
        System.Func<double, double> longitudeProvider,
        System.Func<double, double>? latitudeProvider = null)
    {
        this.longitudeProvider = longitudeProvider ?? throw new ArgumentNullException(nameof(longitudeProvider));
        this.latitudeProvider = latitudeProvider;
    }

    /// <summary>Whether the longitude term is switched on, separately from the tilt.</summary>
    public bool LongitudeEnabled => enabled == true;

    /// <summary>The tilt currently being substituted, or null when the world keeps Earth's.</summary>
    public double? WorldTiltDeg => worldTiltAllowed && latitudeProvider is not null ? worldTiltDeg : null;

    /// <summary>
    /// Whether the delegate the calendar is holding right now is this controller's own wrapper. A
    /// later mod that assigns the property wins, and this reads false again from that moment.
    /// </summary>
    public bool IsInstalledOn(SolarSphericalCoordsDelegate? current)
        => installedDelegate is not null && ReferenceEquals(current, installedDelegate);

    public SunDelegateUpdate Configure(
        bool longitudeAwareSunEnabled,
        SolarSphericalCoordsDelegate? current,
        bool worldTiltEnabled = true)
    {
        enabled = longitudeAwareSunEnabled;
        worldTiltAllowed = worldTiltEnabled;
        return Reconcile(current);
    }

    /// <summary>
    /// Sets the world's axial tilt in degrees, or clears it with null to return the sun to Earth's.
    /// </summary>
    /// <remarks>
    /// Reconciling lands the value on <see cref="WorldTilt"/> as well, so the sun a player watches
    /// and the sun an instrument computes cannot come apart: one setter, one number, both readers.
    /// </remarks>
    public SunDelegateUpdate SetWorldTilt(double? obliquityDeg, SolarSphericalCoordsDelegate? current)
    {
        worldTiltDeg = obliquityDeg;
        return Reconcile(current);
    }

    public SunDelegateUpdate MarkLifecycleReady(SolarSphericalCoordsDelegate? current)
    {
        lifecycleReady = true;
        return Reconcile(current);
    }

    public SunDelegateUpdate Reset(SolarSphericalCoordsDelegate? current)
    {
        var update = ReferenceEquals(current, installedDelegate) && chainedDelegate is not null
            ? new SunDelegateUpdate(SunDelegateUpdateKind.Restored, chainedDelegate)
            : new SunDelegateUpdate(SunDelegateUpdateKind.None, current);

        lifecycleReady = false;
        enabled = null;
        worldTiltDeg = null;
        worldTiltAllowed = true;
        WorldTilt.Reset();
        chainedDelegate = null;
        installedDelegate = null;
        return update;
    }

    /// <summary>Whether either term wants the wrapper on the calendar right now.</summary>
    private bool IsWanted => enabled == true || WorldTiltDeg is not null;

    private SunDelegateUpdate Reconcile(SolarSphericalCoordsDelegate? current)
    {
        // The ambient tilt tracks the wrapper rather than the request: until the wrapper is on the
        // calendar the sun in the sky is still vanilla's, and an instrument reading a tilt the sun
        // does not have is an instrument that lies.
        WorldTilt.Set(lifecycleReady && enabled is not null ? WorldTiltDeg : null);
        if (!lifecycleReady || enabled is null)
        {
            return new SunDelegateUpdate(SunDelegateUpdateKind.None, current);
        }

        if (!IsWanted)
        {
            if (ReferenceEquals(current, installedDelegate) && chainedDelegate is not null)
            {
                var restored = chainedDelegate;
                chainedDelegate = null;
                installedDelegate = null;
                return new SunDelegateUpdate(SunDelegateUpdateKind.Restored, restored);
            }

            chainedDelegate = null;
            installedDelegate = null;
            return new SunDelegateUpdate(SunDelegateUpdateKind.None, current);
        }

        if (ReferenceEquals(current, installedDelegate))
        {
            return new SunDelegateUpdate(SunDelegateUpdateKind.None, current);
        }

        if (current is null || IsDegeneratePlaceholder(current))
        {
            return new SunDelegateUpdate(SunDelegateUpdateKind.WaitingForBaseDelegate, current);
        }

        chainedDelegate = current;
        installedDelegate = GetSolarSphericalCoords;
        return new SunDelegateUpdate(SunDelegateUpdateKind.Installed, installedDelegate);
    }

    /// <summary>
    /// The wrapper the calendar ends up holding: the observer's own clock, then the sun this
    /// world's axis puts on it.
    /// </summary>
    /// <remarks>
    /// The longitude term is an input shift, so it composes with anything the survival mod does and
    /// is applied first. The tilt term cannot be a shift -- vanilla's sun cannot swing wider than
    /// vanilla's own tilt, whatever you feed it -- so it rebuilds the answer from
    /// <see cref="WorldTilt.SolarSphericalCoords"/> instead, which is the survival mod's own
    /// arithmetic with the tilt left open. With no tilt set the chained delegate answers, and this
    /// is exactly the longitude wrapper it always was.
    /// </remarks>
    private SolarSphericalCoords GetSolarSphericalCoords(
        double posX,
        double posZ,
        float yearRel,
        float dayRel)
    {
        var localDayRel = enabled == true
            ? CelestialMath.ApplyLongitudeToDayRel(dayRel, longitudeProvider(posX))
            : dayRel;

        if (WorldTiltDeg is not { } tiltDeg || latitudeProvider is null)
        {
            return chainedDelegate?.Invoke(posX, posZ, yearRel, localDayRel) ?? default;
        }

        var (zenith, azimuth) = WorldTilt.SolarSphericalCoords(
            latitudeProvider(posZ) * Math.PI / 2.0,
            yearRel,
            localDayRel,
            WorldTilt.ToRadians(tiltDeg));
        return new SolarSphericalCoords((float)zenith, (float)azimuth);
    }

    /// <summary>
    /// The calendar constructor's placeholder sweeps the sun through the Z-Y plane with azimuth
    /// pinned at zero. The survival delegate has replaced it before AstraTerra's lifecycle hooks run.
    /// </summary>
    public static bool IsDegeneratePlaceholder(SolarSphericalCoordsDelegate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        try
        {
            var noon = candidate(0, 0, yearRel: 0.25f, dayRel: 0.5f);
            var midnight = candidate(0, 0, yearRel: 0.25f, dayRel: 0f);
            return noon.AzimuthAngle == 0f
                   && midnight.AzimuthAngle == 0f
                   && Math.Abs(noon.ZenithAngle - midnight.ZenithAngle) > Math.PI * 0.9;
        }
        catch
        {
            // A third-party delegate may reject synthetic coordinates. It is still a real delegate
            // and should be chained rather than mistaken for the calendar placeholder.
            return false;
        }
    }
}

/// <summary>
/// Installs the longitude wrapper after Vintage Story's survival mod has installed its own solar
/// delegate. Client and server each own an instance, so integrated single-player cannot mix sides.
/// </summary>
public sealed class LongitudeAwareSunInstaller : IDisposable
{
    public const string ChannelName = "astraterralongitudesun";

    private readonly ICoreAPI api;
    private readonly LongitudeAwareSunController controller;
    private ICoreClientAPI? clientApi;
    private ICoreServerAPI? serverApi;
    private IServerNetworkChannel? serverChannel;
    private bool serverEnabled;
    private bool serverWorldTiltEnabled = true;
    private bool disposed;
    private bool waitingWarningLogged;

    private LongitudeAwareSunInstaller(ICoreAPI api)
    {
        this.api = api;
        controller = new LongitudeAwareSunController(
            x => LatitudeMapper.MapWorldLongitude(x, api.World),
            // The game's own latitude, in the -1..1 form the survival delegate uses, so the
            // rebuilt sun stands where a custom world-height or latitude mod says it should.
            z => api.World?.Calendar?.OnGetLatitude?.Invoke(z) ?? 0.0);
    }

    public static LongitudeAwareSunInstaller StartClient(ICoreClientAPI api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var installer = new LongitudeAwareSunInstaller(api)
        {
            clientApi = api
        };

        api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<LongitudeAwareSunConfigPacket>()
            .SetMessageHandler<LongitudeAwareSunConfigPacket>(installer.OnServerConfig);
        api.Event.LevelFinalize += installer.OnClientLevelFinalize;
        ObserverLongitude.FollowSun(() => installer.SunFollowsLongitude);
        api.Logger.Event("AstraTerra startup step: longitude-aware sun awaiting server configuration");
        return installer;
    }

    public static LongitudeAwareSunInstaller StartServer(
        ICoreServerAPI api,
        bool enabled,
        bool worldTiltEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(api);

        var installer = new LongitudeAwareSunInstaller(api)
        {
            serverApi = api,
            serverEnabled = enabled,
            serverWorldTiltEnabled = worldTiltEnabled
        };

        installer.serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<LongitudeAwareSunConfigPacket>();
        api.Event.PlayerNowPlaying += installer.OnPlayerNowPlaying;
        api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, installer.OnServerGameReady);
        installer.Apply(
            installer.controller.Configure(enabled, installer.CurrentDelegate(), worldTiltEnabled));
        api.Logger.Event(
            "AstraTerra startup step: longitude-aware sun server policy: enabled={0}; generatedWorldTilt={1}",
            enabled,
            worldTiltEnabled);
        return installer;
    }

    /// <summary>
    /// Whether this side's sun is currently drawn with the longitude term. False before the server
    /// has sent its policy, when that policy is off, and after another mod has taken the delegate.
    /// </summary>
    public bool SunFollowsLongitude
        => !disposed && controller.LongitudeEnabled && controller.IsInstalledOn(CurrentDelegate());

    /// <summary>
    /// Hands this side the world's axial tilt in degrees, or null to keep Earth's. A generated moon
    /// world's tilt is its parent giant's; every other world passes null.
    /// </summary>
    public void SetWorldTilt(double? obliquityDeg)
    {
        if (!disposed)
        {
            Apply(controller.SetWorldTilt(obliquityDeg, CurrentDelegate()));
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (clientApi is not null)
        {
            clientApi.Event.LevelFinalize -= OnClientLevelFinalize;
            ObserverLongitude.Reset();
        }

        if (serverApi is not null)
        {
            serverApi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;
        }

        Apply(controller.Reset(CurrentDelegate()));
        disposed = true;
    }

    private void OnServerConfig(LongitudeAwareSunConfigPacket packet)
    {
        if (disposed)
        {
            return;
        }

        Apply(controller.Configure(packet.Enabled, CurrentDelegate(), packet.WorldTiltEnabled));
        api.Logger.Event(
            "AstraTerra startup step: longitude-aware sun server policy received: enabled={0}; generatedWorldTilt={1}",
            packet.Enabled,
            packet.WorldTiltEnabled);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        serverChannel?.SendPacket(
            new LongitudeAwareSunConfigPacket
            {
                Enabled = serverEnabled,
                WorldTiltEnabled = serverWorldTiltEnabled
            },
            player);
    }

    private void OnClientLevelFinalize()
        => ScheduleLifecycleReady();

    private void OnServerGameReady()
        => ScheduleLifecycleReady();

    private void ScheduleLifecycleReady()
    {
        // SurvivalCoreSystem assigns the base delegate in this same lifecycle event. Deferring one
        // callback lets every handler finish, independent of mod-system registration order.
        api.Event.RegisterCallback(
            _ =>
            {
                if (!disposed)
                {
                    Apply(controller.MarkLifecycleReady(CurrentDelegate()));
                }
            },
            0);
    }

    private SolarSphericalCoordsDelegate? CurrentDelegate()
        => api.World?.Calendar?.OnGetSolarSphericalCoords;

    private void Apply(SunDelegateUpdate update)
    {
        if (update.Kind == SunDelegateUpdateKind.WaitingForBaseDelegate)
        {
            if (!waitingWarningLogged)
            {
                api.Logger.Warning(
                    "AstraTerra could not install the longitude-aware sun because no survival solar delegate was available at the ready lifecycle stage");
                waitingWarningLogged = true;
            }

            return;
        }

        if (!update.ReplacesCurrent || update.Delegate is null || api.World?.Calendar is not { } calendar)
        {
            return;
        }

        calendar.OnGetSolarSphericalCoords = update.Delegate;
        waitingWarningLogged = false;
        if (update.Kind == SunDelegateUpdateKind.Installed)
        {
            api.Logger.Event("AstraTerra startup step: longitude-aware sun installed after survival solar setup");
        }
        else
        {
            api.Logger.Event("AstraTerra startup step: longitude-aware sun removed; prior solar delegate restored");
        }
    }
}
