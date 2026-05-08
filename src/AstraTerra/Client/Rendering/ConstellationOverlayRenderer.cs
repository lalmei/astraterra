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
    private const double OcclusionRayDistance = 192.0;
    private const float LineDotSpacing = 5.0f;
    private const float LineDotSize = 4.5f;
    private const float EndpointDotSize = 8.0f;
    private const float GuidePickRadius = 26.0f;
    private const float GuideTargetDotSize = 13.0f;
    private static readonly Vec4f SegmentTint = new(0.55f, 0.85f, 1.0f, 0.82f);
    private static readonly Vec4f EndpointTint = new(0.85f, 0.95f, 1.0f, 0.92f);
    private static readonly Vec4f HoverGuideTint = new(0.95f, 1.0f, 0.75f, 0.95f);
    private static readonly Vec4f DragGuideTint = new(1.0f, 0.86f, 0.48f, 0.95f);
    private static readonly Vec4f PreviewSegmentTint = new(1.0f, 0.86f, 0.48f, 0.86f);

    private readonly ICoreClientAPI api;
    private readonly AstraTerraConfig config;
    private readonly StarCatalog catalog;
    private readonly ConstellationJournal journal;
    private readonly Action? onJournalChanged;
    private double[]? cachedViewMatrix;
    private double[]? cachedProjectionMatrix;
    private IReadOnlyList<RenderedStar> guideOverlayStars = [];
    private IReadOnlyList<ProjectedGuideStar> projectedGuideStars = [];
    private readonly Dictionary<int, bool> occludedGuideStars = new();
    private LoadedTexture pixelTexture;
    private int? hoveredGuideHipId;
    private int? drawStartHipId;
    private int? drawHoverHipId;
    private IReadOnlyList<RenderedScreenSegment> screenSegments = [];

    public ConstellationOverlayRenderer(ICoreClientAPI api, AstraTerraConfig config, StarCatalog catalog, ConstellationJournal journal, Action? onJournalChanged = null)
    {
        this.api = api;
        this.config = config;
        this.catalog = catalog;
        this.journal = journal;
        this.onJournalChanged = onJournalChanged;
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
            journal.AddEdgeAndMerge(start, end);
            onJournalChanged?.Invoke();
            args.Handled = true;
        }

        drawStartHipId = null;
        drawHoverHipId = null;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
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
        if ((!SkyStarSunMoonRenderer.ForceDaylightStars && darkness <= 0.02) || cachedViewMatrix is null || cachedProjectionMatrix is null)
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

        var latitude = LatitudeMapper.MapGameLatitude(position.Z, calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(position.X, api.World.BlockAccessor.MapSizeX, api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);
        guideOverlayStars = BuildGuideOverlay(catalog.Stars, latitude, localSiderealAngle, config);

        var visibleStars = StarRenderModel.ProjectVisibleStars(catalog.Stars, latitude, localSiderealAngle, Math.Max(config.StarBrightnessBias, config.GuideStarHighlightStrength));
        occludedGuideStars.Clear();
        projectedGuideStars = ProjectGuideStars(visibleStars.Where(star => star.IsGuideStar));
        var visibleStarMap = visibleStars.ToDictionary(star => star.Hip);
        var segments = ConstellationOverlayModel.BuildConstellationSegments(journal.Constellations, visibleStarMap);
        var nextScreenSegments = new List<RenderedScreenSegment>();

        EnsurePixelTexture();
        if (pixelTexture.TextureId == 0)
        {
            ClearInteractionTargets();
            return;
        }

        api.Render.GlToggleBlend(true, EnumBlendMode.Glow);
        try
        {
            foreach (var segment in segments)
            {
                if (!TryProject(segment.Start, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var startX, out var startY) ||
                    !TryProject(segment.End, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var endX, out var endY))
                {
                    continue;
                }

                nextScreenSegments.Add(new RenderedScreenSegment(segment.ConstellationId, segment.StartHip, segment.EndHip, startX, startY, endX, endY));
                DrawSegment(segment.Start, segment.End, startX, startY, endX, endY);
            }

            screenSegments = nextScreenSegments;
            DrawTelescopeDrawingFeedback();
        }
        finally
        {
            api.Render.GlToggleBlend(false, EnumBlendMode.Glow);
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

    private void DrawSegment(RenderedStar start, RenderedStar end, float startX, float startY, float endX, float endY)
    {
        DrawSegment(
            startX,
            startY,
            endX,
            endY,
            SegmentTint,
            EndpointTint,
            (t) =>
            {
                var x = Lerp(start.DirectionX, end.DirectionX, t);
                var y = Lerp(start.DirectionY, end.DirectionY, t);
                var z = Lerp(start.DirectionZ, end.DirectionZ, t);
                return !IsSkyDirectionOccluded(x, y, z);
            });
    }

    private void DrawSegment(float startX, float startY, float endX, float endY, Vec4f? tintOverride = null)
    {
        var tint = tintOverride ?? SegmentTint;
        DrawSegment(startX, startY, endX, endY, tint, tintOverride ?? EndpointTint, _ => true);
    }

    private void DrawSegment(
        float startX,
        float startY,
        float endX,
        float endY,
        Vec4f tint,
        Vec4f endpointTint,
        System.Func<float, bool> shouldDrawAt)
    {
        var dx = endX - startX;
        var dy = endY - startY;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));
        var steps = Math.Max(1, (int)MathF.Ceiling(length / LineDotSpacing));

        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            if (!shouldDrawAt(t))
            {
                continue;
            }

            var x = startX + (dx * t);
            var y = startY + (dy * t);
            api.Render.Render2DTexture(pixelTexture.TextureId, x - (LineDotSize / 2f), y - (LineDotSize / 2f), LineDotSize, LineDotSize, 760f, tint);
        }

        if (shouldDrawAt(0f))
        {
            api.Render.Render2DTexture(pixelTexture.TextureId, startX - (EndpointDotSize / 2f), startY - (EndpointDotSize / 2f), EndpointDotSize, EndpointDotSize, 761f, endpointTint);
        }

        if (shouldDrawAt(1f))
        {
            api.Render.Render2DTexture(pixelTexture.TextureId, endX - (EndpointDotSize / 2f), endY - (EndpointDotSize / 2f), EndpointDotSize, EndpointDotSize, 761f, endpointTint);
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

    private bool HasOpenSkyAbovePlayer()
    {
        var entity = api.World.Player.Entity;
        var blockPos = entity.Pos.AsBlockPos;
        var eyeY = entity.Pos.InternalY + entity.LocalEyePos.Y;
        return api.World.BlockAccessor.GetRainMapHeightAt(blockPos) <= eyeY;
    }

    private IReadOnlyList<ProjectedGuideStar> ProjectGuideStars(IEnumerable<RenderedStar> stars)
    {
        if (cachedViewMatrix is null || cachedProjectionMatrix is null)
        {
            return [];
        }

        return stars.Select(star =>
            {
                var projected = TryProject(star, cachedViewMatrix, cachedProjectionMatrix, api.Render.FrameWidth, api.Render.FrameHeight, out var x, out var y);
                return projected && !IsGuideStarOccluded(star) ? new ProjectedGuideStar(star.Hip, x, y) : (ProjectedGuideStar?)null;
            })
            .Where(star => star is not null)
            .Select(star => star!.Value)
            .ToList();
    }

    private void ClearInteractionTargets()
    {
        hoveredGuideHipId = null;
        drawHoverHipId = null;
        projectedGuideStars = [];
        screenSegments = [];
    }

    private void BeginConnection(MouseEvent args)
    {
        var guide = PickGuideStar(args.X, args.Y);
        if (guide is null)
        {
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
        var record = segment is null
            ? null
            : journal.Constellations.FirstOrDefault(constellation => constellation.Id == segment.Value.ConstellationId);
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

        journal.RemoveEdgeAndSplit(segment.Value.ConstellationId, segment.Value.StartHip, segment.Value.EndHip);
        onJournalChanged?.Invoke();
        args.Handled = true;
    }

    private void RenameConstellation(int constellationId, string? name)
    {
        var record = journal.Constellations.FirstOrDefault(constellation => constellation.Id == constellationId);
        if (record is null)
        {
            return;
        }

        journal.Replace(record with { Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim() });
        onJournalChanged?.Invoke();
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
        RenderedStar star,
        double[] viewMatrix,
        double[] projectionMatrix,
        float frameWidth,
        float frameHeight,
        out float screenX,
        out float screenY)
    {
        var (worldX, worldY, worldZ) = BuildSkyProjectionPosition(star.DirectionX, star.DirectionY, star.DirectionZ);
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

    private bool IsGuideStarOccluded(RenderedStar star)
    {
        if (!occludedGuideStars.TryGetValue(star.Hip, out var occluded))
        {
            occluded = IsSkyDirectionOccluded(star.DirectionX, star.DirectionY, star.DirectionZ);
            occludedGuideStars[star.Hip] = occluded;
        }

        return occluded;
    }

    private bool IsSkyDirectionOccluded(double directionX, double directionY, double directionZ)
    {
        var length = Math.Sqrt((directionX * directionX) + (directionY * directionY) + (directionZ * directionZ));
        if (length <= 0.000001)
        {
            return true;
        }

        var entity = api.World.Player.Entity;
        var eye = new Vec3d(entity.Pos.X, entity.Pos.InternalY + entity.LocalEyePos.Y, entity.Pos.Z);
        var scale = OcclusionRayDistance / length;
        var target = new Vec3d(
            eye.X + (directionX * scale),
            eye.Y + (directionY * scale),
            eye.Z + (directionZ * scale));
#pragma warning disable CS8600
        BlockSelection blockSelection = null;
        EntitySelection entitySelection = null;
        api.World.RayTraceForSelection(
            eye,
            target,
            ref blockSelection,
            ref entitySelection,
            (pos, block) => true,
            _ => false);
#pragma warning restore CS8600
        return blockSelection is not null;
    }

    private static double Lerp(double start, double end, float t)
    {
        return start + ((end - start) * t);
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

    private readonly record struct RenderedScreenSegment(int ConstellationId, int StartHip, int EndHip, float StartX, float StartY, float EndX, float EndY);
}
