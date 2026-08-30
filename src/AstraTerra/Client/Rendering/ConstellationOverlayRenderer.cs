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
    private const float StarPickRadius = 26.0f;
    private const float StarTargetDotSize = 13.0f;
    private static readonly Vec4f HoverStarTint = new(0.976f, 0.886f, 0.686f, 0.78f);
    private static readonly Vec4f DragStarTint = new(0.980f, 0.702f, 0.529f, 0.78f);
    private static readonly Vec4f PreviewSegmentTint = new(0.980f, 0.702f, 0.529f, 0.56f);

    private readonly ICoreClientAPI api;
    private readonly AstraTerraConfig config;
    private StarCatalog catalog;
    private readonly ConstellationBookClient bookClient;
    private readonly SkyDiscEngraveClient discClient;
    private readonly List<RenderedStar> overlayStars = new(6000);
    private readonly Dictionary<int, RenderedStar> overlayStarsByHip = new(6000);
    private double[]? cachedViewMatrix;
    private double[]? cachedProjectionMatrix;
    private readonly List<ProjectedStar> projectedStars = new(6000);
    private LoadedTexture pixelTexture;
    private bool renderingDisabledAfterFailure;
    private int? hoveredStarHipId;
    private int? drawStartHipId;
    private int? drawHoverHipId;
    private IReadOnlyList<RenderedScreenSegment> screenSegments = [];

    public ConstellationOverlayRenderer(
        ICoreClientAPI api,
        AstraTerraConfig config,
        StarCatalog catalog,
        ConstellationBookClient bookClient,
        SkyDiscEngraveClient discClient)
    {
        this.api = api;
        this.config = config;
        this.catalog = catalog;
        this.bookClient = bookClient;
        this.discClient = discClient;
        pixelTexture = new LoadedTexture(api)
        {
            Width = 1,
            Height = 1
        };
    }

    public double RenderOrder => 0.95;

    public int RenderRange => 9999;

    /// <summary>
    /// The instrument a line is being drawn through, or none.
    /// </summary>
    /// <remarks>
    /// Two instruments draw on the sky and each has its own way of being ready: the telescope when
    /// it is scoped, and the disc when it is held up. Both are the same gesture from here — aim at a
    /// star, press, aim at another, let go — and only the surface the line is cut into differs.
    /// </remarks>
    private DrawingSurface Surface
    {
        get
        {
            if (TelescopeScopeState.IsScoped)
            {
                return DrawingSurface.Telescope;
            }

            return SkyDiscReadingState.IsReading && SkyDiscHeld.IsHolding(api.World.Player)
                ? DrawingSurface.Disc
                : DrawingSurface.None;
        }
    }

    /// <summary>See <see cref="SkyStarSunMoonRenderer.ReplaceCatalog"/>.</summary>
    public void ReplaceCatalog(StarCatalog replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        catalog = replacement;
        overlayStars.Clear();
        overlayStarsByHip.Clear();
        projectedStars.Clear();
        screenSegments = [];
    }

    public void OnMouseDown(MouseEvent args)
    {
        var surface = Surface;
        if (surface == DrawingSurface.None)
        {
            return;
        }

        // The disc has one mode: a disc is a thing you cut a figure into, and it carries no
        // inspecting or unpicking the way a notebook does.
        if (surface == DrawingSurface.Disc)
        {
            if (args.Button == EnumMouseButton.Left)
            {
                BeginConnection(args, surface);
            }

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
                BeginConnection(args, surface);
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
        var surface = Surface;
        if (surface == DrawingSurface.None)
        {
            hoveredStarHipId = null;
            drawHoverHipId = null;
            return;
        }

        if (surface == DrawingSurface.Telescope && TelescopeScopeState.Mode != ObservationMode.Draw)
        {
            hoveredStarHipId = null;
            drawHoverHipId = null;
            return;
        }

        var target = PickStar(args.X, args.Y);
        hoveredStarHipId = target?.Hip;
        if (drawStartHipId is not null)
        {
            drawHoverHipId = target?.Hip;
        }
    }

    public void OnMouseUp(MouseEvent args)
    {
        var surface = Surface;
        if (surface == DrawingSurface.None
            || args.Button != EnumMouseButton.Left
            || (surface == DrawingSurface.Telescope && TelescopeScopeState.Mode != ObservationMode.Draw))
        {
            return;
        }

        var target = PickStar(args.X, args.Y);
        drawHoverHipId = target?.Hip ?? drawHoverHipId;
        if (drawStartHipId is int start && drawHoverHipId is int end && start != end)
        {
            if (surface == DrawingSurface.Disc)
            {
                discClient.SendEngrave(start, end);
            }
            else
            {
                bookClient.SendAddEdge(start, end);
            }

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
        var surface = Surface;

        // This pass is the drawing hand, not the sky: it exists to project stars so one can be
        // aimed at, and to show the line being pulled between two of them. What a figure looks like
        // once it is drawn is the sky pass's business, so nothing here runs unless an instrument is
        // actually up.
        if ((!SkyStarSunMoonRenderer.ForceDaylightStars && darkness <= 0.02) ||
            cachedViewMatrix is null ||
            cachedProjectionMatrix is null ||
            surface == DrawingSurface.None)
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

        // The telescope draws into the book, so without one it has nothing to draw on and nothing
        // to show. The disc is its own page and asks for no book at all.
        if (surface == DrawingSurface.Telescope && !bookClient.HasLeftHandJournalBook())
        {
            ClearInteractionTargets();
            return;
        }

        // Only the book's constellations. These become the segments a telescope can point at to
        // rename or unpick, and every one of those acts on the book by id — so a disc's figure among
        // them would be a line the book is asked to edit and does not have.
        var figures = surface == DrawingSurface.Telescope
            ? bookClient.ReadCurrentJournalOrEmpty().Constellations
            : [];

        var latitude = LatitudeMapper.MapGameLatitude(position.Z, calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        // Reused buffers rather than a fresh projection, filter and dictionary each frame: this runs
        // on the render thread whenever an instrument is up, over the whole catalog.
        StarRenderModel.ProjectVisibleStars(
            catalog.Stars,
            latitude,
            localSiderealAngle,
            Math.Max(config.StarBrightnessBias, config.GuideStarHighlightStrength),
            overlayStars);

        overlayStarsByHip.Clear();
        foreach (var star in overlayStars)
        {
            overlayStarsByHip[star.Hip] = star;
        }

        ProjectStars(overlayStars, projectedStars);
        var segments = ConstellationRenderModel.BuildConstellationSegments(figures, overlayStarsByHip);
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
            if (surface != DrawingSurface.None)
            {
                DrawDrawingFeedback();
            }
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

    private void DrawDrawingFeedback()
    {
        var hovered = FindProjectedStar(hoveredStarHipId);
        var start = FindProjectedStar(drawStartHipId);
        var end = FindProjectedStar(drawHoverHipId);

        if (start is not null)
        {
            DrawStarTarget(start.Value.X, start.Value.Y, DragStarTint, StarTargetDotSize + 4.0f);
            DrawSegment(start.Value.X, start.Value.Y, api.Render.FrameWidth / 2f, api.Render.FrameHeight / 2f, PreviewSegmentTint);
        }

        if (hovered is not null)
        {
            DrawStarTarget(hovered.Value.X, hovered.Value.Y, HoverStarTint, StarTargetDotSize);
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

    private void DrawStarTarget(float x, float y, Vec4f tint, float size)
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

    /// <summary>
    /// Screen positions for every star currently drawn, so any of them can anchor a line.
    /// </summary>
    /// <remarks>
    /// Fills a reused buffer with a plain loop for the same reason
    /// <see cref="StarRenderModel.ProjectVisibleStars(IEnumerable{StarCatalogEntry}, double, double, double, List{RenderedStar}, double, double)"/>
    /// does: this runs on the render thread over the whole visible sky every frame the scope is up,
    /// and the LINQ chain it replaced allocated a closure and a list per frame.
    /// </remarks>
    private void ProjectStars(List<RenderedStar> stars, List<ProjectedStar> destination)
    {
        destination.Clear();
        if (cachedViewMatrix is null || cachedProjectionMatrix is null)
        {
            return;
        }

        var frameWidth = api.Render.FrameWidth;
        var frameHeight = api.Render.FrameHeight;
        for (var index = 0; index < stars.Count; index++)
        {
            var star = stars[index];
            if (TryProject(star.Body, cachedViewMatrix, cachedProjectionMatrix, frameWidth, frameHeight, out var x, out var y))
            {
                destination.Add(new ProjectedStar(star.Hip, x, y));
            }
        }
    }

    private void ClearInteractionTargets()
    {
        hoveredStarHipId = null;
        drawHoverHipId = null;
        projectedStars.Clear();
        screenSegments = [];
    }

    private void BeginConnection(MouseEvent args, DrawingSurface surface)
    {
        var target = PickStar(args.X, args.Y);
        if (target is null)
        {
            return;
        }

        // A disc needs no book, no ink and no quill: the figure goes into the disc in the hand, and
        // whether that disc will take one is the server's answer to give when the line is finished.
        if (surface == DrawingSurface.Telescope && !bookClient.CanMutate(out var message))
        {
            api.ShowChatMessage(message);
            return;
        }

        drawStartHipId = target.Value.Hip;
        drawHoverHipId = null;
        hoveredStarHipId = target.Value.Hip;
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

    /// <summary>
    /// The star nearest the cursor, from every star the player can currently see: a figure may be
    /// drawn between any two of them, not only the catalog's flagged guide stars.
    /// </summary>
    private ProjectedStar? PickStar(float mouseX, float mouseY)
    {
        var radiusSquared = StarPickRadius * StarPickRadius;
        ProjectedStar? closest = null;
        var closestDistanceSquared = double.MaxValue;

        foreach (var star in projectedStars)
        {
            var dx = star.X - mouseX;
            var dy = star.Y - mouseY;
            var distanceSquared = (dx * dx) + (dy * dy);
            if (distanceSquared <= radiusSquared && distanceSquared < closestDistanceSquared)
            {
                closest = star;
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

    private ProjectedStar? FindProjectedStar(int? hipId)
    {
        if (hipId is not int hip)
        {
            return null;
        }

        foreach (var star in projectedStars)
        {
            if (star.Hip == hip)
            {
                return star;
            }
        }

        return null;
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

    private static string FormatMode(ObservationMode mode)
        => $"{TelescopeObservationState.ModeLabel(mode)} - {TelescopeObservationState.ModeHint(mode)}.";

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

    private readonly record struct ProjectedStar(int Hip, float X, float Y);

    /// <summary>Which instrument, if any, a line is being drawn through.</summary>
    private enum DrawingSurface
    {
        None,
        Telescope,
        Disc,
    }

    private readonly record struct RenderedScreenSegment(int ConstellationId, int StartHip, int EndHip, float StartX, float StartY, float EndX, float EndY);
}
