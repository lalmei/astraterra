using AstraTerra.Astronomy;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed class SextantReadingRenderer : IRenderer
{
    private const double SkyProjectionDistance = 40.0;
    private const double TargetRadiusPixels = 44.0;
    private const float ReadingTopOffset = 64.0f;

    private readonly ICoreClientAPI api;
    private readonly AstraTerraConfig config;
    private StarCatalog? catalog;
    private PlanetCatalog? planetCatalog;
    private CometCatalog? cometCatalog;
    private PlanetRenderModel? planetModel;
    private int planetModelDaysPerYear;
    private double planetModelHoursPerDay;
    private CometRenderModel? cometModel;
    private int cometModelDaysPerYear;
    private double[]? cachedViewMatrix;
    private double[]? cachedProjectionMatrix;
    private readonly ConstellationBookClient? bookClient;
    private LoadedTexture? readingTexture;
    private string? readingText;
    private bool sneakWasDown;
    private bool sneakPressed;

    /// <param name="catalog">Null when the star catalog failed to load. Sun and moon sighting still works.</param>
    /// <param name="planetCatalog">Null when the planet catalog failed to load. Everything else still sights.</param>
    /// <param name="cometCatalog">Null when the comet catalog failed to load. Everything else still sights.</param>
    /// <param name="bookClient">Null when there is no book channel. Sighting still reads; it just cannot be written down.</param>
    public SextantReadingRenderer(
        ICoreClientAPI api,
        AstraTerraConfig config,
        StarCatalog? catalog,
        PlanetCatalog? planetCatalog = null,
        CometCatalog? cometCatalog = null,
        ConstellationBookClient? bookClient = null)
    {
        this.api = api;
        this.config = config;
        this.catalog = catalog;
        this.planetCatalog = planetCatalog;
        this.cometCatalog = cometCatalog;
        this.bookClient = bookClient;
    }

    public double RenderOrder => 0.99;

    public int RenderRange => 9999;

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplaceCatalog"/>.</summary>
    public void ReplaceCatalog(StarCatalog replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        catalog = replacement;
    }

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplacePlanetCatalog"/>.</summary>
    public void ReplacePlanetCatalog(PlanetCatalog? replacement)
    {
        planetCatalog = replacement;
        planetModel = null;
        planetModelDaysPerYear = 0;
        planetModelHoursPerDay = 0;
    }

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplaceCometCatalog"/>.</summary>
    public void ReplaceCometCatalog(CometCatalog? replacement)
    {
        cometCatalog = replacement;
        cometModel = null;
        cometModelDaysPerYear = 0;
    }

    public void OnMouseDown(MouseEvent args)
    {
        if (!IsReadingSextant() || args.Button != EnumMouseButton.Middle)
        {
            return;
        }

        var mode = SextantReadingState.CycleGridMode();
        api.ShowChatMessage($"Sextant display: {FormatGridMode(mode)}");
        args.Handled = true;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (stage == EnumRenderStage.Opaque)
        {
            CacheCameraMatrices();
            TrackSneak();
            return;
        }

        if (stage != EnumRenderStage.Ortho || !IsReadingSextant())
        {
            return;
        }

        var target = ResolveTarget(out var status);
        RenderReading(status);
        PollRecordObservation(target);
    }

    public void Dispose()
    {
        DeleteReadingTexture();
    }

    /// <summary>What the sight is on, and what the readout says about it either way.</summary>
    private SightedBody? ResolveTarget(out string status)
    {
        if (cachedViewMatrix is null || cachedProjectionMatrix is null)
        {
            status = "Sextant: no sight line";
            return null;
        }

        if (!HasOpenSkyAbovePlayer())
        {
            status = "Sextant: sky blocked";
            return null;
        }

        var target = FindTargetBody(CollectSightableBodies());
        if (target is null)
        {
            status = CanSightStars()
                ? "Sextant: align with a star, the sun, or the moon"
                : "Sextant: align with the sun or the moon";
            return null;
        }

        status = $"{target.DisplayName} — angle above horizon: {target.AltitudeDeg:+0.0;-0.0;0.0} deg";
        return target;
    }

    /// <summary>
    /// Writes the current sight into the book when the observer sneaks, the same key the telescope
    /// writes with and for the same reason: right mouse is already held to keep the instrument up.
    /// </summary>
    /// <remarks>
    /// What travels is the reading, never the identity: an altitude, an azimuth, a brightness, the
    /// day and hour, and the latitude the observer stood at. The book keeps a dated entry about a
    /// position; deciding what stood there is the observer's own work, done later by comparing
    /// entries.
    /// </remarks>
    private void PollRecordObservation(SightedBody? target)
    {
        if (!sneakPressed)
        {
            return;
        }

        sneakPressed = false;
        if (target is null || bookClient is null)
        {
            return;
        }

        if (!bookClient.CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        var calendar = api.World.Calendar;
        var position = api.World.Player.Entity.Pos;
        var latitude = LatitudeMapper.MapGameLatitude(
            position.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(
            position.X,
            api.World.BlockAccessor.MapSizeX,
            api.World.BlockAccessor.MapSizeZ);

        // How far the sky had turned goes into the entry alongside the angles. Without it the book
        // could only ever compare two sightings taken at the same moment of the same night, which is
        // to say it could not compare anything at all.
        var siderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        bookClient.SendRecordObservation(
            target.AltitudeDeg,
            target.AzimuthDeg,
            target.VisualMagnitude,
            (int)Math.Floor(calendar.TotalDays),
            calendar.HourOfDay,
            latitude,
            InstrumentResolution.BrassSextantDeg,
            siderealAngle);
    }

    private void TrackSneak()
    {
        var down = api.World.Player.Entity.Controls.Sneak;
        sneakPressed = down && !sneakWasDown;
        sneakWasDown = down;
    }

    /// <summary>
    /// The sun and moon come straight from Vintage Story, so the sextant measures the bodies the
    /// game actually draws. Both are sightable whenever they are up, which is what lets the moon be
    /// shot in daylight; only the star catalog needs a dark sky.
    /// </summary>
    private IEnumerable<SightedBody> CollectSightableBodies()
    {
        var calendar = api.World.Calendar;
        var position = api.World.Player.Entity.Pos;

        var sunVector = calendar.GetSunPosition(position.XYZ, calendar.TotalDays);
        var sun = SkyBodyModel.FromWorldDirection("Sun", sunVector.X, sunVector.Y, sunVector.Z);
        if (sun is not null && sun.AltitudeDeg > 0)
        {
            yield return sun;
        }

        var moonVector = calendar.GetMoonPosition(position.XYZ, calendar.TotalDays);
        var moon = SkyBodyModel.FromWorldDirection("Moon", moonVector.X, moonVector.Y, moonVector.Z);
        if (moon is not null && moon.AltitudeDeg > 0)
        {
            yield return moon;
        }

        // The null check is redundant with CanSightStars, but the compiler cannot see through it.
        if (catalog is null || !CanSightStars())
        {
            yield break;
        }

        var latitude = LatitudeMapper.MapGameLatitude(position.Z, calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        var visibleStars = StarRenderModel.ProjectVisibleStars(
            catalog.Stars,
            latitude,
            localSiderealAngle,
            config.StarBrightnessBias,
            visualHorizonCutoffDeg: 0.0);
        foreach (var star in visibleStars)
        {
            yield return SkyBodyModel.FromStar(star);
        }

        // Planets sight under the same dark-sky rule as stars, because that is when the sky renderer
        // draws them: the sextant should only measure what an observer can actually see.
        // A comet sights like anything else once it is up: it is a body with an altitude, and the
        // whole point of the instrument is reading that angle. Unlike a planet it keeps its name
        // without a book — the catalog authors one for it, because a comet is not a discovery a
        // player makes by watching a star wander, it is an event the sky announces.
        var comets = ResolveCometModel(Math.Max(1, calendar.DaysPerYear));
        if (comets is not null)
        {
            var cometSunVector = calendar.GetSunPosition(position.XYZ, calendar.TotalDays);
            var visibleComets = comets.ProjectVisibleComets(
                calendar.TotalDays,
                latitude,
                localSiderealAngle,
                config.StarBrightnessBias,
                new SkyDirection(cometSunVector.X, cometSunVector.Y, cometSunVector.Z),
                visualHorizonCutoffDeg: 0.0);
            foreach (var comet in visibleComets)
            {
                yield return SkyBodyModel.FromBody($"{comet.DisplayName} · comet", comet.Body);
            }
        }

        var planets = ResolvePlanetModel(Math.Max(1, calendar.DaysPerYear), Math.Max(1.0, calendar.HoursPerDay));
        if (planets is null)
        {
            yield break;
        }

        var visiblePlanets = planets.ProjectVisiblePlanets(
            calendar.TotalDays,
            latitude,
            localSiderealAngle,
            config.StarBrightnessBias,
            visualHorizonCutoffDeg: 0.0);

        // A planet reads by whatever the observer's own book calls it. Without that book it is what
        // it looks like from the ground: a star that wanders. The catalog's name is never shown —
        // it arrives only through a book somebody already wrote.
        var journal = ConstellationBookService.ReadPlanetJournalOrEmpty(
            api.World.Player.Entity.LeftHandItemSlot?.Itemstack);
        foreach (var planet in visiblePlanets)
        {
            yield return SkyBodyModel.FromBody(journal.DisplayName(planet.Id), planet.Body);
        }
    }

    /// <summary>
    /// Built on demand and kept, because a client only learns the world's <c>daysPerYear</c> when the
    /// server hands it over, and because each planet's ephemeris caches its last sample.
    /// </summary>
    /// <summary>Built on demand and kept, for the same reasons the planet model is.</summary>
    private CometRenderModel? ResolveCometModel(int daysPerYear)
    {
        if (cometCatalog is null)
        {
            return null;
        }

        if (cometModel is null || daysPerYear != cometModelDaysPerYear)
        {
            cometModel = new CometRenderModel(cometCatalog, daysPerYear);
            cometModelDaysPerYear = daysPerYear;
        }

        return cometModel;
    }

    private PlanetRenderModel? ResolvePlanetModel(int daysPerYear, double hoursPerDay)
    {
        if (planetCatalog is null)
        {
            return null;
        }

        if (planetModel is null
            || daysPerYear != planetModelDaysPerYear
            || Math.Abs(hoursPerDay - planetModelHoursPerDay) > 1e-9)
        {
            planetModel = new PlanetRenderModel(planetCatalog, daysPerYear, hoursPerDay);
            planetModelDaysPerYear = daysPerYear;
            planetModelHoursPerDay = hoursPerDay;
        }

        return planetModel;
    }

    private bool CanSightStars()
        => catalog is not null
            && (SkyStarSunMoonRenderer.ForceDaylightStars || 1.0 - api.World.Calendar.DayLightStrength > 0.02);

    private SightedBody? FindTargetBody(IEnumerable<SightedBody> bodies)
    {
        var centerX = api.Render.FrameWidth / 2f;
        var centerY = api.Render.FrameHeight / 2f;
        var radiusSquared = TargetRadiusPixels * TargetRadiusPixels;
        SightedBody? closest = null;
        var closestDistanceSquared = double.MaxValue;

        foreach (var body in bodies)
        {
            if (!TryProject(body, cachedViewMatrix!, cachedProjectionMatrix!, api.Render.FrameWidth, api.Render.FrameHeight, out var x, out var y))
            {
                continue;
            }

            var dx = x - centerX;
            var dy = y - centerY;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared <= radiusSquared && distanceSquared < closestDistanceSquared)
            {
                closest = body;
                closestDistanceSquared = distanceSquared;
            }
        }

        return closest;
    }

    private bool IsReadingSextant()
    {
        if (!IsActiveHeldSextant())
        {
            return false;
        }

        return SextantReadingState.IsReading || api.World.Player.Entity.Controls.RightMouseDown;
    }

    private bool IsActiveHeldSextant()
    {
        var stack = api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        var code = stack?.Collectible?.Code;
        return string.Equals(code?.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
            && string.Equals(code?.Path, "sextant", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatGridMode(SkyGridMode mode)
        => mode switch
        {
            SkyGridMode.Equatorial => "equatorial grid (right ascension/declination)",
            SkyGridMode.Horizontal => "azimuthal grid (altitude/azimuth)",
            SkyGridMode.Both => "both sky grids",
            _ => "angle only"
        };

    private void RenderReading(string text)
    {
        if (readingTexture is null || readingText != text)
        {
            DeleteReadingTexture();
            readingText = text;
            readingTexture = api.Gui.TextTexture.GenTextTexture(
                text,
                new CairoFont(22, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
                null);
        }

        if (readingTexture.TextureId == 0)
        {
            return;
        }

        var x = (api.Render.FrameWidth - readingTexture.Width) / 2f;
        api.Render.Render2DLoadedTexture(readingTexture, x, ReadingTopOffset, 1001f);
    }

    private void DeleteReadingTexture()
    {
        if (readingTexture is not null && readingTexture.TextureId != 0)
        {
            api.Render.GLDeleteTexture(readingTexture.TextureId);
        }

        if (readingTexture is not null)
        {
            readingTexture.TextureId = 0;
        }
    }

    private void CacheCameraMatrices()
    {
        var view = api.Render.CameraMatrixOriginf;
        var projection = api.Render.CurrentProjectionMatrix;
        if (view is null || view.Length < 16 || projection is null || projection.Length < 16)
        {
            return;
        }

        cachedViewMatrix ??= new double[16];
        cachedProjectionMatrix ??= new double[16];
        for (var i = 0; i < 16; i++)
        {
            cachedViewMatrix[i] = view[i];
            cachedProjectionMatrix[i] = projection[i];
        }
    }

    // Shared with the star pass so the lines, the grid and the sighting reticle appear exactly where
    // the stars themselves do -- under a tree or in a doorway, but not underground.
    private bool HasOpenSkyAbovePlayer() => SkyExposure.CanSeeSky(api);

    private bool TryProject(
        SightedBody body,
        double[] viewMatrix,
        double[] projectionMatrix,
        float frameWidth,
        float frameHeight,
        out float screenX,
        out float screenY)
    {
        var (worldX, worldY, worldZ) = BuildSkyProjectionPosition(body.DirectionX, body.DirectionY, body.DirectionZ);
        return ProjectWorldToScreen(
            worldX,
            worldY,
            worldZ,
            viewMatrix,
            projectionMatrix,
            frameWidth,
            frameHeight,
            out screenX,
            out screenY);
    }

    private (double X, double Y, double Z) BuildSkyProjectionPosition(double directionX, double directionY, double directionZ)
    {
        var entity = api.World.Player.Entity;
        var y = (directionY * SkyProjectionDistance)
            + entity.LocalEyePos.Y
            - ((entity.Pos.Y - api.World.SeaLevel) / 10000.0);
        return (
            directionX * SkyProjectionDistance,
            y,
            directionZ * SkyProjectionDistance);
    }

    private static bool ProjectWorldToScreen(
        double worldX,
        double worldY,
        double worldZ,
        double[] viewMatrix,
        double[] projectionMatrix,
        float frameWidth,
        float frameHeight,
        out float screenX,
        out float screenY)
    {
        screenX = 0f;
        screenY = 0f;

        var viewX = (viewMatrix[0] * worldX) + (viewMatrix[4] * worldY) + (viewMatrix[8] * worldZ) + viewMatrix[12];
        var viewY = (viewMatrix[1] * worldX) + (viewMatrix[5] * worldY) + (viewMatrix[9] * worldZ) + viewMatrix[13];
        var viewZ = (viewMatrix[2] * worldX) + (viewMatrix[6] * worldY) + (viewMatrix[10] * worldZ) + viewMatrix[14];
        var viewW = (viewMatrix[3] * worldX) + (viewMatrix[7] * worldY) + (viewMatrix[11] * worldZ) + viewMatrix[15];

        var clipX = (projectionMatrix[0] * viewX) + (projectionMatrix[4] * viewY) + (projectionMatrix[8] * viewZ) + (projectionMatrix[12] * viewW);
        var clipY = (projectionMatrix[1] * viewX) + (projectionMatrix[5] * viewY) + (projectionMatrix[9] * viewZ) + (projectionMatrix[13] * viewW);
        var clipW = (projectionMatrix[3] * viewX) + (projectionMatrix[7] * viewY) + (projectionMatrix[11] * viewZ) + (projectionMatrix[15] * viewW);

        if (clipW <= 0.00001)
        {
            return false;
        }

        var ndcX = clipX / clipW;
        var ndcY = clipY / clipW;
        if (ndcX < -1.2 || ndcX > 1.2 || ndcY < -1.2 || ndcY > 1.2)
        {
            return false;
        }

        screenX = (float)((ndcX * 0.5 + 0.5) * frameWidth);
        screenY = (float)((1.0 - ((ndcY * 0.5) + 0.5)) * frameHeight);
        return true;
    }
}
