using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Astronomy;

/// <summary>
/// Holds the parent giant that lights this world, and works out what it is doing to the light at a
/// given place and moment.
/// </summary>
/// <remarks>
/// <para>
/// One instance per side. The server needs this as much as the client does -- what lights the
/// ground is what decides whether things spawn on it, whether crops grow, and how cold the night
/// gets -- so the giant is handed to both, and neither is authoritative over the other: they are
/// each running the same arithmetic on the same authored numbers, which is what keeps a dedicated
/// server and its clients agreeing about how dark it is.
/// </para>
/// <para>
/// With no giant registered every method here is the identity, so a planet world, a world whose
/// generator supplied nothing, and a server that has switched this off all take the same path: the
/// vanilla value, returned untouched.
/// </para>
/// </remarks>
public sealed class NearBodyLightController
{
    /// <summary>
    /// What is left of daylight at the bottom of a total eclipse.
    /// </summary>
    /// <remarks>
    /// Not zero, and for the reason a totally eclipsed moon is copper rather than invisible: the
    /// eclipsing body has an atmosphere, and the ring of it around the disc refracts sunlight into
    /// the shadow. A giant is a very large atmosphere. This is what stops totality reading as
    /// somebody switching the world off, and it is small enough that the night rules of the game
    /// still take hold under it.
    /// </remarks>
    public const double EclipseRefractionFloor = 0.05;

    /// <summary>
    /// How finely the cached answer is allowed to age, in world days.
    /// </summary>
    /// <remarks>
    /// <c>GetDayLightStrength</c> is called from the block-light path, which means it is called for
    /// every lit block query the game makes, and the arithmetic behind it is a handful of
    /// transcendentals. None of that changes meaningfully inside a thousandth of a day -- about
    /// twenty seconds of game time -- so the answer is worked out once per tick's worth of time and
    /// per region of ground, and read back after that.
    /// </remarks>
    public const double CacheQuantumDays = 0.001;

    /// <summary>
    /// How far an observer may move before the cached answer is worked out again, in blocks.
    /// </summary>
    /// <remarks>
    /// The giant hangs over one patch of the world, so where the observer stands genuinely changes
    /// how high it is -- but over a chunk's width it changes it by far less than the light scale
    /// can show. A region rather than a block keeps the lighting pass off the trigonometry.
    /// </remarks>
    public const double CacheQuantumBlocks = 128.0;

    private readonly object gate = new();
    private IWorldAccessor? world;
    private System.Func<double, double>? longitudeOf;
    private NearBodyLightSource? source;
    private bool enabled = true;
    private CachedIllumination cached;

    /// <summary>Whether anything here will change a light value.</summary>
    public bool IsActive => enabled && source is not null && world is not null;

    /// <summary>The giant currently lighting this world, if there is one.</summary>
    public NearBodyLightSource? Source => source;

    /// <summary>
    /// Binds this controller to a world and to that side's idea of observer longitude.
    /// </summary>
    /// <param name="boundWorld">The world whose ground is being lit.</param>
    /// <param name="observerLongitude">
    /// Longitude in degrees for a world X, or null to fall back to <see cref="ObserverLongitude"/>.
    /// </param>
    /// <remarks>
    /// The longitude has to be supplied rather than read from <see cref="ObserverLongitude"/>,
    /// which is client-scope state that only the client's installer fills in. A dedicated server
    /// would read zero from it while its own sun was being shifted by longitude -- so the giant
    /// would be pinned to the prime meridian for the code that decides what spawns, while every
    /// client saw it correctly placed. Each side passes its own answer here instead.
    /// </remarks>
    public void Bind(IWorldAccessor boundWorld, System.Func<double, double>? observerLongitude = null)
    {
        ArgumentNullException.ThrowIfNull(boundWorld);
        lock (gate)
        {
            world = boundWorld;
            longitudeOf = observerLongitude;
            cached = default;
        }
    }

    /// <summary>
    /// Sets, replaces, or clears the giant. Passing null returns the world to vanilla light.
    /// </summary>
    public void SetSource(NearBodyLightSource? replacement)
    {
        lock (gate)
        {
            source = replacement;
            cached = default;
        }
    }

    /// <summary>Turns the whole effect off without forgetting which giant this world has.</summary>
    public void SetEnabled(bool value)
    {
        lock (gate)
        {
            enabled = value;
            cached = default;
        }
    }

    public void Reset()
    {
        lock (gate)
        {
            world = null;
            longitudeOf = null;
            source = null;
            cached = default;
        }
    }

    /// <summary>
    /// The day-light strength an observer at <paramref name="x"/>, <paramref name="z"/> actually
    /// has, given what vanilla worked out without knowing about the giant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two terms go on in the order the sky applies them. The eclipse comes off the sunlit
    /// value first, because a covered sun is simply less sun. Planetshine is then compared against
    /// what is left rather than added to it -- the same way vanilla compares its moonlight against
    /// its sunlight -- because these are two lights on one landscape, and the brighter one is what
    /// you see by.
    /// </para>
    /// <para>
    /// Both terms are floored at nothing and the result is never allowed above what vanilla would
    /// have returned at noon, so no path through here can brighten a day.
    /// </para>
    /// </remarks>
    public float Apply(IGameCalendar? calendar, double x, double z, float vanillaStrength)
    {
        if (calendar is null || !IsActive)
        {
            return vanillaStrength;
        }

        return Combine(vanillaStrength, IlluminationAt(calendar, x, z));
    }

    /// <summary>
    /// Puts the giant's two terms onto the value vanilla worked out without knowing about it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the sky's own. The eclipse comes off the sunlit value first, because a covered
    /// sun is simply less sun, and what survives totality is the ring of the giant's atmosphere
    /// rather than nothing. Planetshine is then compared against what is left rather than added to
    /// it -- the same way vanilla compares its moonlight against its sunlight -- because these are
    /// two lights on one landscape and the brighter one is what you see by.
    /// </para>
    /// <para>
    /// Nothing here can brighten a day, and the two terms cannot rescue each other: a giant that is
    /// eclipsing the sun is by definition new, so its own light is already zero when the eclipse
    /// term is at its largest.
    /// </para>
    /// </remarks>
    public static float Combine(float vanillaStrength, NearBodyIllumination illumination)
    {
        if (illumination == NearBodyIllumination.None)
        {
            return vanillaStrength;
        }

        var eclipsed = vanillaStrength * (1.0 - illumination.SolarObscuration);
        if (illumination.SolarObscuration > 0.0)
        {
            eclipsed = Math.Max(eclipsed, Math.Min(vanillaStrength, EclipseRefractionFloor));
        }

        return (float)Math.Clamp(
            Math.Max(eclipsed, illumination.PlanetshineStrength),
            0.0,
            Math.Max(vanillaStrength, NearBodyLight.MaxPlanetshineLight));
    }

    /// <summary>
    /// Where the client's own light should be worked out for: wherever the player is standing.
    /// </summary>
    /// <remarks>
    /// The server has no single observer and never asks -- it is handed a position for every query
    /// it makes -- so this is the client's question alone, and it answers the world origin before a
    /// player exists, which is a moment nothing is rendered in anyway.
    /// </remarks>
    public (double x, double z) ObserverPosition()
    {
        IWorldAccessor? boundWorld;
        lock (gate)
        {
            boundWorld = world;
        }

        return boundWorld is Vintagestory.API.Client.IClientWorldAccessor client
            && client.Player?.Entity?.Pos is { } position
                ? (position.X, position.Z)
                : (0.0, 0.0);
    }

    /// <summary>Where the giant is and what it is doing, for this place and this moment.</summary>
    public NearBodyIllumination IlluminationAt(IGameCalendar calendar, double x, double z)
    {
        ArgumentNullException.ThrowIfNull(calendar);
        NearBodyLightSource? giant;
        IWorldAccessor? boundWorld;
        System.Func<double, double>? longitude;
        lock (gate)
        {
            if (!enabled)
            {
                return NearBodyIllumination.None;
            }

            giant = source;
            boundWorld = world;
            longitude = longitudeOf;
            var key = CacheKey(calendar.TotalDays, x, z);
            if (cached.Key == key && cached.Valid)
            {
                return cached.Value;
            }
        }

        if (giant is null || boundWorld is null)
        {
            return NearBodyIllumination.None;
        }

        var value = Compute(giant, boundWorld, longitude, calendar, x, z);
        lock (gate)
        {
            cached = new CachedIllumination(CacheKey(calendar.TotalDays, x, z), value, Valid: true);
        }

        return value;
    }

    /// <summary>
    /// Reads the observer's place and moment off the game, then hands over to
    /// <see cref="IlluminationFor"/>, which is where the actual work is.
    /// </summary>
    private static NearBodyIllumination Compute(
        NearBodyLightSource giant,
        IWorldAccessor boundWorld,
        System.Func<double, double>? longitudeOf,
        IGameCalendar calendar,
        double x,
        double z)
    {
        var sun = calendar.GetSunPosition(new Vec3d(x, boundWorld.SeaLevel, z), calendar.TotalDays)
            .Clone()
            .Normalize();

        return IlluminationFor(
            giant,
            LatitudeMapper.MapGameLatitude(
                z,
                calendar.OnGetLatitude is null ? null : posZ => calendar.OnGetLatitude(posZ)),
            longitudeOf?.Invoke(x) ?? ObserverLongitude.ForObserver(x, boundWorld),
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            new SkyDirection(sun.X, sun.Y, sun.Z));
    }

    /// <summary>
    /// Puts the giant into an observer's sky and asks <see cref="NearBodyLight"/> what that means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The placement is the same one the near-body renderer uses, and deliberately so: the giant
    /// that lights the ground has to be the giant the player can see hanging there, at the same
    /// height and the same phase. Two placements would eventually disagree, and the disagreement
    /// would show up as a full giant over a dark landscape.
    /// </para>
    /// <para>
    /// Longitude is a parameter rather than something read from the world, because the two sides
    /// answer it differently -- see <see cref="Bind"/> -- and because it genuinely changes the
    /// answer: the giant hangs over one patch of ground, so an observer far enough east has it low
    /// or not up at all, and their nights are darker for it.
    /// </para>
    /// </remarks>
    public static NearBodyIllumination IlluminationFor(
        NearBodyLightSource giant,
        double latitudeDeg,
        double longitudeDeg,
        double totalDays,
        int daysPerYear,
        double hoursPerDay,
        SkyDirection sunDirection)
    {
        ArgumentNullException.ThrowIfNull(giant);
        var localSidereal = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            totalDays,
            daysPerYear,
            hoursPerDay,
            longitudeDeg);

        // The giant does not move over the ground, so its right ascension has to run with the
        // sidereal clock to keep it there -- the same correction the renderer applies, and for the
        // same reason.
        var rightAscension = CelestialMath.NormalizeDegrees(
            localSidereal - longitudeDeg - giant.HourAngleDeg);
        var horizontal = CelestialMath.GetHorizontalCoordinates(
            rightAscension,
            giant.DeclinationDeg,
            latitudeDeg,
            localSidereal);
        var (gx, gy, gz) = SkyProjection.GetWorldDirection(horizontal.AzimuthDeg, horizontal.AltitudeDeg);

        return NearBodyLight.Illumination(
            giant,
            new SkyDirection(gx, gy, gz),
            horizontal.AltitudeDeg,
            sunDirection);
    }

    private static long CacheKey(double totalDays, double x, double z)
    {
        var time = (long)Math.Floor(totalDays / CacheQuantumDays);
        var gx = (long)Math.Floor(x / CacheQuantumBlocks);
        var gz = (long)Math.Floor(z / CacheQuantumBlocks);
        unchecked
        {
            var key = time * 1000003L;
            key = (key * 31L) + gx;
            return (key * 31L) + gz;
        }
    }

    private readonly record struct CachedIllumination(long Key, NearBodyIllumination Value, bool Valid);
}
