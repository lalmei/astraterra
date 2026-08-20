using AstraTerra.Astronomy;
using AstraTerra.Client.Hud;
using AstraTerra.Client.Observation;
using AstraTerra.Config;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed class ConstellationOverlayRenderer : IRenderer
{
    private const double SkyProjectionDistance = 40.0;
    private const float LineDotSpacing = 6.0f;
    private const float LineDotSize = 3.2f;
    private const float GuidePickRadius = 26.0f;
    private const float GuideTargetDotSize = 13.0f;
    private static readonly Vec4f HoverGuideTint = new(0.976f, 0.886f, 0.686f, 0.78f);
    private static readonly Vec4f DragGuideTint = new(0.980f, 0.702f, 0.529f, 0.78f);
    private static readonly Vec4f PreviewSegmentTint = new(0.980f, 0.702f, 0.529f, 0.56f);

    /// <summary>
    /// How far from the middle of the eyepiece a planet may sit and still count as the one being
    /// looked at. Generous, because the telescope is aimed by turning your head.
    /// </summary>
    private const float PlanetPickRadius = 90.0f;

    private readonly ICoreClientAPI api;
    private readonly AstraTerraConfig config;
    private readonly StarCatalog catalog;
    private readonly ConstellationBookClient bookClient;
    private readonly PlanetCatalog? planetCatalog;
    private PlanetRenderModel? planetModel;
    private int planetModelDaysPerYear;
    private double planetModelHoursPerDay;
    private bool sneakWasDown;
    private double[]? cachedViewMatrix;
    private double[]? cachedProjectionMatrix;
    private IReadOnlyList<ProjectedPlanet> projectedPlanets = [];
    private IReadOnlyList<ProjectedGuideStar> projectedGuideStars = [];
    private LoadedTexture pixelTexture;
    private bool renderingDisabledAfterFailure;
    private int? hoveredGuideHipId;
    private int? drawStartHipId;
    private int? drawHoverHipId;
    private IReadOnlyList<RenderedScreenSegment> screenSegments = [];

    public ConstellationOverlayRenderer(
        ICoreClientAPI api,
        AstraTerraConfig config,
        StarCatalog catalog,
        ConstellationBookClient bookClient,
        PlanetCatalog? planetCatalog = null)
    {
        this.api = api;
        this.config = config;
        this.catalog = catalog;
        this.bookClient = bookClient;
        this.planetCatalog = planetCatalog;
        pixelTexture = new LoadedTexture(api)
        {
            Width = 1,
            Height = 1
        };
    }

    public double RenderOrder => 0.95;

    public int RenderRange => 9999;

    public void OnMouseDown(MouseEvent args)
    {
        if (!TelescopeScopeState.IsScoped)
        {
            return;
        }

        if (args.Button == EnumMouseButton.Middle)
        {
            TelescopeScopeState.CycleMode();
            drawStartHipId = null;
            drawHoverHipId = null;
            api.ShowChatMessage($"Telescope mode: {FormatMode(TelescopeScopeState.Mode)}");
            args.Handled = true;
            return;
        }

        if (args.Button != EnumMouseButton.Left)
        {
            return;
        }

        switch (TelescopeScopeState.Mode)
        {
            case ObservationMode.Draw:
                BeginConnection(args);
                break;
            case ObservationMode.Inspect:
                OpenNameDialog(args);
                break;
            case ObservationMode.RemoveSegment:
                RemoveSegment(args);
                break;
        }
    }

    public void OnMouseMove(MouseEvent args)
    {
        if (!TelescopeScopeState.IsScoped)
        {
            hoveredGuideHipId = null;
            drawHoverHipId = null;
            return;
        }

        if (TelescopeScopeState.Mode != ObservationMode.Draw)
        {
            hoveredGuideHipId = null;
            drawHoverHipId = null;
            return;
        }

        var guide = PickGuideStar(args.X, args.Y);
        hoveredGuideHipId = guide?.Hip;
        if (drawStartHipId is not null)
        {
            drawHoverHipId = guide?.Hip;
        }
    }

    public void OnMouseUp(MouseEvent args)
    {
        if (!TelescopeScopeState.IsScoped || args.Button != EnumMouseButton.Left || TelescopeScopeState.Mode != ObservationMode.Draw)
        {
            return;
        }

        var guide = PickGuideStar(args.X, args.Y);
        drawHoverHipId = guide?.Hip ?? drawHoverHipId;
        if (drawStartHipId is int start && drawHoverHipId is int end && start != end)
        {
            bookClient.SendAddEdge(start, end);
            args.Handled = true;
        }

        drawStartHipId = null;
        drawHoverHipId = null;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (renderingDisabledAfterFailure)
        {
            return;
        }

        try
        {
            OnRenderFrameCore(stage);
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            ClearInteractionTargets();
            api.Logger.Warning("AstraTerra disabled constellation overlay rendering after an unexpected error: {0}", exception);
        }
    }

    private void OnRenderFrameCore(EnumRenderStage stage)
    {
        if (stage == EnumRenderStage.Opaque)
        {
            CacheCameraMatrices();
            return;
        }

        if (stage != EnumRenderStage.Ortho)
        {
            return;
        }

        var calendar = api.World.Calendar;
        var darkness = 1.0 - calendar.DayLightStrength;
        if ((!SkyStarSunMoonRenderer.ForceDaylightStars && darkness <= 0.02) ||
            cachedViewMatrix is null ||
            cachedProjectionMatrix is null ||
            !TelescopeScopeState.IsScoped)
        {
            ClearInteractionTargets();
            return;
        }

        var position = api.World.Player.Entity.Pos;
        if (!HasOpenSkyAbovePlayer())
        {
            ClearInteractionTargets();
            return;
        }

        if (!bookClient.HasLeftHandJournalBook())
        {
            ClearInteractionTargets();
            return;
        }

        var journal = bookClient.ReadCurrentJournalOrEmpty();

        var latitude = LatitudeMapper.MapGameLatitude(position.Z, calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        var visibleStars = StarRenderModel.ProjectVisibleStars(catalog.Stars, latitude, localSiderealAngle, Math.Max(config.StarBrightnessBias, config.GuideStarHighlightStrength));
        projectedGuideStars = ProjectGuideStars(visibleStars.Where(star => star.IsGuideStar));
        projectedPlanets = ProjectPlanets(calendar.TotalDays, latitude, localSiderealAngle);
        PollIdentifyPlanet();
        var visibleStarMap = visibleStars.ToDictionary(star => star.Hip);
        var segments = ConstellationRenderModel.BuildConstellationSegments(journal.Constellations, visibleStarMap);
        var nextScreenSegments = new List<RenderedScreenSegment>();

        foreach (var segment in segments)
        {
            if (!TryProject(segment.Start.Body, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var startX, out var startY) ||
                !TryProject(segment.End.Body, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var endX, out var endY))
            {
                continue;
            }

            nextScreenSegments.Add(new RenderedScreenSegment(segment.ConstellationId, segment.StartHip, segment.EndHip, startX, startY, endX, endY));
        }

        screenSegments = nextScreenSegments;

        EnsurePixelTexture();
        if (pixelTexture.TextureId == 0)
        {
            return;
        }

        api.Render.GlToggleBlend(true, EnumBlendMode.Standard);
        try
        {
            DrawTelescopeDrawingFeedback();
        }
        finally
        {
            api.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }
    }

    public void Dispose()
    {
        if (pixelTexture.TextureId != 0)
        {
            api.Render.GLDeleteTexture(pixelTexture.TextureId);
            pixelTexture.TextureId = 0;
        }
    }

    public static IReadOnlyList<RenderedStar> BuildGuideOverlay(
        IEnumerable<StarCatalogEntry> stars,
        double latitudeDeg,
        double localSiderealDeg,
        AstraTerraConfig config)
    {
        var brightnessBias = Math.Max(config.StarBrightnessBias, config.GuideStarHighlightStrength);
        return StarRenderModel.ProjectVisibleStars(stars.Where(star => star.IsGuideStar), latitudeDeg, localSiderealDeg, brightnessBias);
    }

    private void DrawTelescopeDrawingFeedback()
    {
        var hovered = FindProjectedGuide(hoveredGuideHipId);
        var start = FindProjectedGuide(drawStartHipId);
        var end = FindProjectedGuide(drawHoverHipId);

        if (start is not null)
        {
            DrawGuideTarget(start.Value.X, start.Value.Y, DragGuideTint, GuideTargetDotSize + 4.0f);
            DrawSegment(start.Value.X, start.Value.Y, api.Render.FrameWidth / 2f, api.Render.FrameHeight / 2f, PreviewSegmentTint);
        }

        if (hovered is not null)
        {
            DrawGuideTarget(hovered.Value.X, hovered.Value.Y, HoverGuideTint, GuideTargetDotSize);
        }

        if (start is not null && end is not null && start.Value.Hip != end.Value.Hip)
        {
            DrawSegment(start.Value.X, start.Value.Y, end.Value.X, end.Value.Y, PreviewSegmentTint);
        }
    }

    private void DrawSegment(float startX, float startY, float endX, float endY, Vec4f tint)
    {
        var dx = endX - startX;
        var dy = endY - startY;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));
        var steps = Math.Max(1, (int)MathF.Ceiling(length / LineDotSpacing));

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            var x = startX + (dx * t);
            var y = startY + (dy * t);
            api.Render.Render2DTexture(pixelTexture.TextureId, x - (LineDotSize / 2f), y - (LineDotSize / 2f), LineDotSize, LineDotSize, 760f, tint);
        }
    }

    private void DrawGuideTarget(float x, float y, Vec4f tint, float size)
    {
        api.Render.Render2DTexture(pixelTexture.TextureId, x - (size / 2f), y - (size / 2f), size, size, 762f, tint);
    }

    private void EnsurePixelTexture()
    {
        if (pixelTexture.TextureId != 0)
        {
            return;
        }

        api.Render.LoadOrUpdateTextureFromRgba([unchecked((int)0xffffffff)], true, 0, ref pixelTexture);
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

    private IReadOnlyList<ProjectedGuideStar> ProjectGuideStars(IEnumerable<RenderedStar> stars)
    {
        if (cachedViewMatrix is null || cachedProjectionMatrix is null)
        {
            return [];
        }

        return stars.Select(star =>
            {
                var projected = TryProject(star.Body, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var x, out var y);
                return projected ? new ProjectedGuideStar(star.Hip, x, y) : (ProjectedGuideStar?)null;
            })
            .Where(star => star is not null)
            .Select(star => star!.Value)
            .ToList();
    }

    private IReadOnlyList<ProjectedPlanet> ProjectPlanets(double totalDays, double latitudeDeg, double localSiderealDeg)
    {
        var planets = ResolvePlanetModel();
        if (planets is null || cachedViewMatrix is null || cachedProjectionMatrix is null)
        {
            return [];
        }

        return planets
            .ProjectVisiblePlanets(totalDays, latitudeDeg, localSiderealDeg, config.StarBrightnessBias, visualHorizonCutoffDeg: 0.0)
            .Select(planet =>
            {
                var projected = TryProject(planet.Body, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var x, out var y);
                return projected ? new ProjectedPlanet(planet.Id, x, y) : (ProjectedPlanet?)null;
            })
            .Where(planet => planet is not null)
            .Select(planet => planet!.Value)
            .ToList();
    }

    private PlanetRenderModel? ResolvePlanetModel()
    {
        if (planetCatalog is null)
        {
            return null;
        }

        var daysPerYear = Math.Max(1, api.World.Calendar.DaysPerYear);
        var hoursPerDay = Math.Max(1.0, api.World.Calendar.HoursPerDay);
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

    /// <summary>
    /// Writes down whatever the telescope is pointed at, when the observer sneaks while observing.
    /// </summary>
    /// <remarks>
    /// A key rather than a click: right click is already held down to keep the scope raised, and both
    /// mouse buttons are spoken for in the drawing modes. The target is whatever sits nearest the
    /// middle of the eyepiece, because a telescope is aimed by turning your head rather than by
    /// moving a cursor.
    /// <para>
    /// Fires on the press rather than while held, so leaning on sneak does not rewrite the book every
    /// frame.
    /// </para>
    /// </remarks>
    private void PollIdentifyPlanet()
    {
        var sneakDown = api.World.Player.Entity.Controls.Sneak;
        var pressed = sneakDown && !sneakWasDown;
        sneakWasDown = sneakDown;

        if (!pressed || TelescopeScopeState.Mode != ObservationMode.Observe)
        {
            return;
        }

        var planet = PickPlanetNearCentre();
        if (planet is null)
        {
            return;
        }

        if (!bookClient.CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        bookClient.SendIdentifyPlanet(planet.Value.PlanetId);
    }

    private ProjectedPlanet? PickPlanetNearCentre()
    {
        var centreX = api.Render.FrameWidth / 2f;
        var centreY = api.Render.FrameHeight / 2f;
        var radiusSquared = PlanetPickRadius * PlanetPickRadius;
        ProjectedPlanet? closest = null;
        var closestDistanceSquared = double.MaxValue;

        foreach (var planet in projectedPlanets)
        {
            var dx = planet.X - centreX;
            var dy = planet.Y - centreY;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared > radiusSquared || distanceSquared >= closestDistanceSquared)
            {
                continue;
            }

            closest = planet;
            closestDistanceSquared = distanceSquared;
        }

        return closest;
    }

    private void ClearInteractionTargets()
    {
        hoveredGuideHipId = null;
        drawHoverHipId = null;
        projectedGuideStars = [];
        projectedPlanets = [];
        screenSegments = [];
    }

    private void BeginConnection(MouseEvent args)
    {
        var guide = PickGuideStar(args.X, args.Y);
        if (guide is null)
        {
            return;
        }

        if (!bookClient.CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        drawStartHipId = guide.Value.Hip;
        drawHoverHipId = null;
        hoveredGuideHipId = guide.Value.Hip;
        args.Handled = true;
    }

    private void OpenNameDialog(MouseEvent args)
    {
        var segment = PickSegment(args.X, args.Y);
        var journal = bookClient.ReadCurrentJournal();
        var record = segment is null
            ? null
            : journal?.Constellations.FirstOrDefault(constellation => constellation.Id == segment.Value.ConstellationId);
        if (record is null)
        {
            return;
        }

        new ConstellationNameDialog(api, record, RenameConstellation).TryOpen();
        args.Handled = true;
    }

    private void RemoveSegment(MouseEvent args)
    {
        var segment = PickSegment(args.X, args.Y);
        if (segment is null)
        {
            return;
        }

        if (!bookClient.CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        bookClient.SendRemoveEdge(segment.Value.ConstellationId, segment.Value.StartHip, segment.Value.EndHip);
        args.Handled = true;
    }

    private void RenameConstellation(int constellationId, string? name)
    {
        if (!bookClient.CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        bookClient.SendRename(constellationId, name);
    }

    private ProjectedGuideStar? PickGuideStar(float mouseX, float mouseY)
    {
        var radiusSquared = GuidePickRadius * GuidePickRadius;
        ProjectedGuideStar? closest = null;
        var closestDistanceSquared = double.MaxValue;

        foreach (var guide in projectedGuideStars)
        {
            var dx = guide.X - mouseX;
            var dy = guide.Y - mouseY;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared <= radiusSquared && distanceSquared < closestDistanceSquared)
            {
                closest = guide;
                closestDistanceSquared = distanceSquared;
            }
        }

        return closest;
    }

    private RenderedScreenSegment? PickSegment(float mouseX, float mouseY)
    {
        const float segmentPickRadius = 18.0f;
        var radiusSquared = segmentPickRadius * segmentPickRadius;
        RenderedScreenSegment? closest = null;
        var closestDistanceSquared = double.MaxValue;

        foreach (var segment in screenSegments)
        {
            var distanceSquared = DistanceToSegmentSquared(mouseX, mouseY, segment.StartX, segment.StartY, segment.EndX, segment.EndY);
            if (distanceSquared <= radiusSquared && distanceSquared < closestDistanceSquared)
            {
                closest = segment;
                closestDistanceSquared = distanceSquared;
            }
        }

        return closest;
    }

    private ProjectedGuideStar? FindProjectedGuide(int? hipId)
    {
        return hipId is null ? null : projectedGuideStars.FirstOrDefault(guide => guide.Hip == hipId.Value);
    }

    private bool TryProject(
        RenderedBody body,
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

    private static double DistanceToSegmentSquared(float x, float y, float startX, float startY, float endX, float endY)
    {
        var dx = endX - startX;
        var dy = endY - startY;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= 0.000001f)
        {
            var pointDx = x - startX;
            var pointDy = y - startY;
            return (pointDx * pointDx) + (pointDy * pointDy);
        }

        var t = Math.Clamp((((x - startX) * dx) + ((y - startY) * dy)) / lengthSquared, 0.0f, 1.0f);
        var closestX = startX + (dx * t);
        var closestY = startY + (dy * t);
        var closestDx = x - closestX;
        var closestDy = y - closestY;
        return (closestDx * closestDx) + (closestDy * closestDy);
    }

    private static string FormatMode(ObservationMode mode) => mode switch
    {
        ObservationMode.RemoveSegment => "Remove Segment",
        _ => "Observe"
    };

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

    private readonly record struct ProjectedGuideStar(int Hip, float X, float Y);

    private readonly record struct ProjectedPlanet(string PlanetId, float X, float Y);

    private readonly record struct RenderedScreenSegment(int ConstellationId, int StartHip, int EndHip, float StartX, float StartY, float EndX, float EndY);
}
