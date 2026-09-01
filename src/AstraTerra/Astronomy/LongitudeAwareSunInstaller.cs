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
/// Pure state machine for adding and removing the longitude term around an existing solar delegate.
/// Keeping this separate from the game lifecycle makes startup ordering and restoration testable.
/// </summary>
public sealed class LongitudeAwareSunController
{
    private readonly System.Func<double, double> longitudeProvider;
    private bool lifecycleReady;
    private bool? enabled;
    private SolarSphericalCoordsDelegate? chainedDelegate;
    private SolarSphericalCoordsDelegate? installedDelegate;

    public LongitudeAwareSunController(System.Func<double, double> longitudeProvider)
    {
        this.longitudeProvider = longitudeProvider ?? throw new ArgumentNullException(nameof(longitudeProvider));
    }

    public SunDelegateUpdate Configure(bool longitudeAwareSunEnabled, SolarSphericalCoordsDelegate? current)
    {
        enabled = longitudeAwareSunEnabled;
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
        chainedDelegate = null;
        installedDelegate = null;
        return update;
    }

    private SunDelegateUpdate Reconcile(SolarSphericalCoordsDelegate? current)
    {
        if (!lifecycleReady || enabled is null)
        {
            return new SunDelegateUpdate(SunDelegateUpdateKind.None, current);
        }

        if (!enabled.Value)
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

    private SolarSphericalCoords GetSolarSphericalCoords(
        double posX,
        double posZ,
        float yearRel,
        float dayRel)
    {
        var localDayRel = CelestialMath.ApplyLongitudeToDayRel(dayRel, longitudeProvider(posX));
        return chainedDelegate?.Invoke(posX, posZ, yearRel, localDayRel) ?? default;
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
    private static readonly System.Func<double, IWorldAccessor, double>? CanonicalLongitudeMapper =
        typeof(LatitudeMapper)
            .GetMethod(
                nameof(LatitudeMapper.MapWorldLongitude),
                [typeof(double), typeof(IWorldAccessor)])
            ?.CreateDelegate<System.Func<double, IWorldAccessor, double>>();

    public const string ChannelName = "astraterralongitudesun";

    private readonly ICoreAPI api;
    private readonly LongitudeAwareSunController controller;
    private ICoreClientAPI? clientApi;
    private ICoreServerAPI? serverApi;
    private IServerNetworkChannel? serverChannel;
    private bool serverEnabled;
    private bool disposed;
    private bool waitingWarningLogged;

    private LongitudeAwareSunInstaller(ICoreAPI api)
    {
        this.api = api;
        controller = new LongitudeAwareSunController(x => MapWorldLongitude(x, api.World));
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
        api.Logger.Event("AstraTerra startup step: longitude-aware sun awaiting server configuration");
        return installer;
    }

    public static LongitudeAwareSunInstaller StartServer(ICoreServerAPI api, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(api);

        var installer = new LongitudeAwareSunInstaller(api)
        {
            serverApi = api,
            serverEnabled = enabled
        };

        installer.serverChannel = api.Network.RegisterChannel(ChannelName)
            .RegisterMessageType<LongitudeAwareSunConfigPacket>();
        api.Event.PlayerNowPlaying += installer.OnPlayerNowPlaying;
        api.Event.ServerRunPhase(EnumServerRunPhase.GameReady, installer.OnServerGameReady);
        installer.Apply(installer.controller.Configure(enabled, installer.CurrentDelegate()));
        api.Logger.Event(
            "AstraTerra startup step: longitude-aware sun server policy: enabled={0}",
            enabled);
        return installer;
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

        Apply(controller.Configure(packet.Enabled, CurrentDelegate()));
        api.Logger.Event(
            "AstraTerra startup step: longitude-aware sun server policy received: enabled={0}",
            packet.Enabled);
    }

    private void OnPlayerNowPlaying(IServerPlayer player)
    {
        serverChannel?.SendPacket(
            new LongitudeAwareSunConfigPacket { Enabled = serverEnabled },
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

    // #122 depends on #44, but this branch must still build and behave correctly against main. Use
    // #44's canonical world overload when it is present and the same calculation as a standalone
    // fallback while this PR is reviewed before that prerequisite lands.
    private static double MapWorldLongitude(double x, IWorldAccessor world)
    {
        if (CanonicalLongitudeMapper is not null)
        {
            return CanonicalLongitudeMapper(x, world);
        }

        const double defaultPolarEquatorDistance = 50000.0;
        var configured = world.Config?.GetInt("polarEquatorDistance", -1) ?? -1;
        var polarEquatorDistance = configured > 0 ? configured : defaultPolarEquatorDistance;
        if (configured <= 0)
        {
            var configuredText = world.Config?.GetString("polarEquatorDistance", null);
            if (configuredText is not null
                && int.TryParse(configuredText, out var parsed)
                && parsed > 0)
            {
                polarEquatorDistance = parsed;
            }
        }

        var mapSizeX = world.BlockAccessor.MapSizeX;
        if (mapSizeX <= 0 || polarEquatorDistance <= 0)
        {
            return 0;
        }

        var circumference = polarEquatorDistance * 4.0;
        var delta = PositiveModulo(x - mapSizeX * 0.5, circumference);
        if (delta > circumference * 0.5)
        {
            delta -= circumference;
        }

        var degrees = delta / polarEquatorDistance * 90.0;
        var wrapped = degrees % 360.0;
        if (wrapped <= -180.0)
        {
            return wrapped + 360.0;
        }

        return wrapped > 180.0 ? wrapped - 360.0 : wrapped;
    }

    private static double PositiveModulo(double value, double modulus)
    {
        var wrapped = value % modulus;
        return wrapped < 0 ? wrapped + modulus : wrapped;
    }
}
