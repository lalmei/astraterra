using AstraTerra.Astronomy;
using AstraTerra.Config;
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
    private readonly StarCatalog? catalog;
    private double[]? cachedViewMatrix;
    private double[]? cachedProjectionMatrix;
    private LoadedTexture? readingTexture;
    private string? readingText;

    /// <param name="catalog">Null when the star catalog failed to load. Sun and moon sighting still works.</param>
    public SextantReadingRenderer(ICoreClientAPI api, AstraTerraConfig config, StarCatalog? catalog)
    {
        this.api = api;
        this.config = config;
        this.catalog = catalog;
    }

    public double RenderOrder => 0.99;

    public int RenderRange => 9999;

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
            return;
        }

        if (stage != EnumRenderStage.Ortho || !IsReadingSextant())
        {
            return;
        }

        RenderReading(BuildReadingText());
    }

    public void Dispose()
    {
        DeleteReadingTexture();
    }

    private string BuildReadingText()
    {
        if (cachedViewMatrix is null || cachedProjectionMatrix is null)
        {
            return "Sextant: no sight line";
        }

        if (!HasOpenSkyAbovePlayer())
        {
            return "Sextant: sky blocked";
        }

        var target = FindTargetBody(CollectSightableBodies());
        if (target is null)
        {
            return CanSightStars()
                ? "Sextant: align with a star, the sun, or the moon"
                : "Sextant: align with the sun or the moon";
        }

        return $"{target.DisplayName} — angle above horizon: {target.AltitudeDeg:+0.0;-0.0;0.0} deg";
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

    private bool HasOpenSkyAbovePlayer()
    {
        var entity = api.World.Player.Entity;
        var blockPos = entity.Pos.AsBlockPos;
        var eyeY = entity.Pos.InternalY + entity.LocalEyePos.Y;
        return api.World.BlockAccessor.GetRainMapHeightAt(blockPos) <= eyeY;
    }

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
