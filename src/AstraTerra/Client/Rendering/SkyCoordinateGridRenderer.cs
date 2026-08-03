using AstraTerra.Astronomy;
using AstraTerra.Config;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed class SkyCoordinateGridRenderer : IRenderer
{
    private const float SkyDistance = 39.5f;
    private const double GridRebuildThresholdDeg = 0.1;
    private const int HorizontalColor = unchecked((int)0xa64fc3f7);
    private const int HorizontalReferenceColor = unchecked((int)0xe65ee7ff);
    private const int EquatorialColor = unchecked((int)0xa6f38ba8);
    private const int EquatorialReferenceColor = unchecked((int)0xe6ff9fba);

    private readonly ICoreClientAPI api;
    private readonly AstraTerraConfig config;
    private MeshRef? mesh;
    private SkyGridMode cachedMode = (SkyGridMode)(-1);
    private double cachedLatitude = double.NaN;
    private double cachedLocalSiderealAngle = double.NaN;
    private bool renderingDisabledAfterFailure;

    public SkyCoordinateGridRenderer(ICoreClientAPI api, AstraTerraConfig config)
    {
        this.api = api;
        this.config = config;
    }

    public double RenderOrder => 0.96;

    public int RenderRange => 9999;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (renderingDisabledAfterFailure || stage != EnumRenderStage.Opaque)
        {
            return;
        }

        try
        {
            RenderGrid();
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            DisposeMesh();
            api.Logger.Warning(
                "AstraTerra disabled sky coordinate grid rendering after an unexpected error: {0}",
                exception);
        }
    }

    public void Dispose()
    {
        DisposeMesh();
    }

    private void RenderGrid()
    {
        var mode = SkyGridDisplayPolicy.Resolve(
            config.GetSkyGridMode(),
            IsReadingSextant(),
            SextantReadingState.GridMode);
        if (mode == SkyGridMode.None)
        {
            DisposeMesh();
            cachedMode = mode;
            return;
        }

        if (!HasOpenSkyAbovePlayer())
        {
            return;
        }

        var calendar = api.World.Calendar;
        var playerPosition = api.World.Player.Entity.Pos;
        var latitude = LatitudeMapper.MapGameLatitude(
            playerPosition.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(
            playerPosition.X,
            api.World.BlockAccessor.MapSizeX,
            api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        EnsureMesh(mode, latitude, localSiderealAngle);
        if (mesh is null)
        {
            return;
        }

        var render = api.Render;
        var shader = render.GetEngineShader(EnumShaderProgram.Wireframe);
        var entity = api.World.Player.Entity;
        var verticalOrigin = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - api.World.SeaLevel) / 10000f;

        render.GlToggleBlend(true, EnumBlendMode.Standard);
        render.GlDisableCullFace();
        render.GLDisableDepthTest();
        render.GLDepthMask(false);
        try
        {
            shader.Use();
            shader.UniformMatrix("projectionMatrix", render.CurrentProjectionMatrix);
            shader.UniformMatrix("modelViewMatrix", render.CameraMatrixOriginf);
            shader.Uniform("colorIn", ColorUtil.WhiteArgbVec);
            shader.Uniform("origin", 0f, verticalOrigin, 0f);
            shader.Uniform("extraGlow", 1f);
            render.RenderMesh(mesh);
        }
        finally
        {
            shader.Stop();
            render.GLDepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
            render.GlEnableCullFace();
            render.GLEnableDepthTest();
        }
    }

    private void EnsureMesh(
        SkyGridMode mode,
        double latitudeDeg,
        double localSiderealDeg)
    {
        var equatorialGridMoves = SkyGridModeParser.ShowsEquatorial(mode);
        if (mesh is not null &&
            mode == cachedMode &&
            (!equatorialGridMoves ||
             (Math.Abs(latitudeDeg - cachedLatitude) < GridRebuildThresholdDeg &&
              AngularDifferenceDeg(localSiderealDeg, cachedLocalSiderealAngle) < GridRebuildThresholdDeg)))
        {
            return;
        }

        var segments = SkyCoordinateGridModel.Build(mode, latitudeDeg, localSiderealDeg);
        var meshData = BuildMeshData(segments);
        DisposeMesh();
        mesh = meshData.VerticesCount == 0 ? null : api.Render.UploadMesh(meshData);
        cachedMode = mode;
        cachedLatitude = latitudeDeg;
        cachedLocalSiderealAngle = localSiderealDeg;
    }

    private static MeshData BuildMeshData(IReadOnlyList<SkyGridSegment> segments)
    {
        var meshData = new MeshData(
            segments.Count * 2,
            segments.Count * 2,
            withNormals: false,
            withUv: false,
            withRgba: true,
            withFlags: true)
        {
            mode = EnumDrawMode.Lines
        };

        foreach (var segment in segments)
        {
            var color = ResolveColor(segment);
            var vertexIndex = meshData.VerticesCount;
            meshData.AddVertexSkipTex(
                (float)segment.Start.X * SkyDistance,
                (float)segment.Start.Y * SkyDistance,
                (float)segment.Start.Z * SkyDistance,
                color);
            meshData.AddVertexSkipTex(
                (float)segment.End.X * SkyDistance,
                (float)segment.End.Y * SkyDistance,
                (float)segment.End.Z * SkyDistance,
                color);
            meshData.AddIndex(vertexIndex);
            meshData.AddIndex(vertexIndex + 1);
        }

        return meshData;
    }

    private static int ResolveColor(SkyGridSegment segment)
        => segment.System switch
        {
            SkyCoordinateGridSystem.Horizontal when segment.IsReferenceLine => HorizontalReferenceColor,
            SkyCoordinateGridSystem.Horizontal => HorizontalColor,
            SkyCoordinateGridSystem.Equatorial when segment.IsReferenceLine => EquatorialReferenceColor,
            _ => EquatorialColor
        };

    private bool HasOpenSkyAbovePlayer()
    {
        var entity = api.World.Player.Entity;
        var blockPos = entity.Pos.AsBlockPos;
        var eyeY = entity.Pos.InternalY + entity.LocalEyePos.Y;
        return api.World.BlockAccessor.GetRainMapHeightAt(blockPos) <= eyeY;
    }

    private bool IsReadingSextant()
    {
        var stack = api.World.Player.InventoryManager.ActiveHotbarSlot?.Itemstack;
        var code = stack?.Collectible?.Code;
        var isActiveHeldSextant = string.Equals(code?.Domain, "astraterra", StringComparison.OrdinalIgnoreCase)
            && string.Equals(code?.Path, "sextant", StringComparison.OrdinalIgnoreCase);

        return isActiveHeldSextant
            && (SextantReadingState.IsReading || api.World.Player.Entity.Controls.RightMouseDown);
    }

    private void DisposeMesh()
    {
        mesh?.Dispose();
        mesh = null;
    }

    private static double AngularDifferenceDeg(double first, double second)
    {
        var difference = Math.Abs(CelestialMath.NormalizeDegrees(first) - CelestialMath.NormalizeDegrees(second));
        return Math.Min(difference, 360.0 - difference);
    }
}
