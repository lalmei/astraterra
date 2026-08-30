using AstraTerra.Astronomy;
using AstraTerra.Config;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Draws the moon from photographs of the real one, in place of the game's own disc.
/// </summary>
/// <remarks>
/// <para>
/// Only the picture changes. Where the moon is, what phase it is in, how much light it sheds and how
/// that phase advances are all still Vintage Story's — this reads them and draws what they describe,
/// so a mod or a command that moves the moon moves this too. What it adds is a moon worth pointing a
/// telescope at: eight faces of the real surface, at the half degree the moon actually subtends, so
/// magnification shows craters rather than a larger smooth disc.
/// </para>
/// <para>
/// A pass of its own rather than part of the star pass, because the moon is up in daylight and the
/// star pass is not. It draws before opaque terrain, so a moon that has set is occluded by the
/// ground.
/// </para>
/// </remarks>
public sealed class MoonDiscRenderer : IRenderer
{
    /// <summary>Just after the sun and moon, and before the near bodies that may hide it.</summary>
    public const double MoonDiscRenderOrder = 0.305;

    /// <summary>Just inside the star sphere: the moon is between the observer and the stars.</summary>
    private const float SkyDistance = 39.6f;

    /// <summary>
    /// How far the moon may move before its geometry is rebuilt. Well under what the eye can catch at
    /// the naked eye, and under what a telescope can either: magnification multiplies any step.
    /// </summary>
    private const double PositionToleranceRadians = 0.0002;

    private readonly ICoreClientAPI api;
    private readonly AstraTerraConfig config;
    private readonly float[] modelMatrix = IdentityModelMatrix();
    private readonly Dictionary<string, int> textureIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> failedTexturePaths = new(StringComparer.Ordinal);
    private MeshRef? mesh;
    private SkyDirection lastDirection;
    private SkyDirection lastSun;
    private string? lastFaceId;
    private bool renderingDisabledAfterFailure;

    public MoonDiscRenderer(ICoreClientAPI api, AstraTerraConfig config)
    {
        this.api = api;
        this.config = config;
    }

    public double RenderOrder => MoonDiscRenderOrder;

    public int RenderRange => 9999;

    /// <summary>
    /// Whether Vintage Story's own moon should stand down, which it should exactly while this one is
    /// being drawn in its place. Read by the patch that suppresses it, which cannot see this instance.
    /// </summary>
    public static bool HidesVanillaMoon { get; private set; }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (renderingDisabledAfterFailure || stage != EnumRenderStage.Opaque)
        {
            return;
        }

        try
        {
            HidesVanillaMoon = Draw();
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            HidesVanillaMoon = false;
            api.Logger.Error("AstraTerra stopped drawing the moon after a render failure: {0}", exception);
        }
    }

    public void Dispose()
    {
        mesh?.Dispose();
        mesh = null;
        HidesVanillaMoon = false;
    }

    /// <summary>Draws the moon, and reports whether the game's own should therefore stay hidden.</summary>
    private bool Draw()
    {
        // A world that is itself a moon has no moon of its own, and the near-body pass has already
        // taken Vintage Story's off the sky. Drawing one here would put it back.
        if (NearBodyRenderer.HidesVanillaMoon)
        {
            return false;
        }

        if (!StarfieldModeParser.ShowsAstraTerra(config.GetStarfieldMode()))
        {
            return false;
        }

        var entity = api.World.Player?.Entity;
        if (entity is null)
        {
            return false;
        }

        var calendar = api.World.Calendar;
        var moonPosition = calendar.GetMoonPosition(entity.Pos.XYZ, calendar.TotalDays).Clone().Normalize();
        var direction = new SkyDirection(moonPosition.X, moonPosition.Y, moonPosition.Z);
        var face = MoonDiscModel.SelectFace(calendar.MoonPhaseExact);
        var textureId = ResolveTexture(face);
        if (textureId == 0)
        {
            // With no picture there is nothing to put in the game's moon's place, so it keeps the sky.
            return false;
        }

        // Below the horizon it is still this pass's moon -- the game's must stay hidden, or it would
        // reappear at moonset -- there is simply nothing to draw.
        var altitudeSine = direction.Y + Math.Sin(MoonDiscModel.AngularDiameterDeg * 0.5 * Math.PI / 180.0);
        if (altitudeSine < Math.Sin(MoonDiscModel.HorizonCutoffDeg * Math.PI / 180.0))
        {
            return true;
        }

        var sunPosition = calendar.GetSunPosition(entity.Pos.XYZ, calendar.TotalDays).Clone().Normalize();
        var sun = new SkyDirection(sunPosition.X, sunPosition.Y, sunPosition.Z);
        EnsureMesh(direction, sun, face);
        if (mesh is null)
        {
            return false;
        }

        // The sky sphere is centred on the player's eye, the same origin every other sky pass draws in.
        modelMatrix[13] = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - api.World.SeaLevel) / 10000f;

        var render = api.Render;
        var shader = render.StandardShader;
        var daylight = (float)Math.Clamp(calendar.DayLightStrength, 0.0, 1.0);

        render.GlDisableCullFace();
        render.GLDisableDepthTest();
        render.GLDepthMask(false);
        try
        {
            shader.Use();
            shader.Uniform("skyShaded", 0);
            shader.RgbaAmbientIn = ColorUtil.WhiteRgbVec;
            shader.RgbaFogIn = render.FogColor;
            shader.ExtraGlow = 0;
            shader.FogMinIn = render.FogMin;
            shader.FogDensityIn = render.FogDensity;
            shader.DontWarpVertices = 0;
            shader.AddRenderFlags = 0;
            shader.ExtraZOffset = 0f;
            shader.NormalShaded = 0;
            shader.OverlayOpacity = 0f;
            shader.ExtraGodray = 0f;
            shader.AlphaTest = 0.01f;
            shader.ViewMatrix = render.CameraMatrixOriginf;
            shader.ProjectionMatrix = render.CurrentProjectionMatrix;
            shader.RgbaLightIn = ColorUtil.WhiteArgbVec;
            shader.Tex2D = textureId;
            ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrix);

            // The night side of the picture is solid black, which is what the moon is: after dark it
            // is a bite out of the star field, and the disc is drawn as the opaque thing it is. By day
            // that same black would be a hole in a blue sky, so the picture is added to the sky
            // instead, where black contributes nothing and only the lit crescent shows -- which is
            // also how a daytime moon looks. Between the two the pass crossfades.
            if (daylight < 1f)
            {
                render.GlToggleBlend(true, EnumBlendMode.Standard);
                shader.RgbaTint = new Vec4f(1f, 1f, 1f, 1f - daylight);
                render.RenderMesh(mesh);
            }

            if (daylight > 0f)
            {
                render.GlToggleBlend(true, EnumBlendMode.Glow);
                shader.RgbaTint = new Vec4f(1f, 1f, 1f, daylight);
                render.RenderMesh(mesh);
            }
        }
        finally
        {
            shader.Stop();
            render.GLDepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
            render.GlEnableCullFace();
            render.GLEnableDepthTest();
        }

        return true;
    }

    /// <summary>
    /// Rebuilds the moon's quad when it has actually moved, turned with the sun, or changed face.
    /// </summary>
    private void EnsureMesh(SkyDirection direction, SkyDirection sun, MoonPhaseFace face)
    {
        if (mesh is not null
            && lastFaceId == face.Id
            && Separation(direction, lastDirection) < PositionToleranceRadians
            && Separation(sun, lastSun) < PositionToleranceRadians)
        {
            return;
        }

        var meshData = DeepSkyQuadMeshBuilder.Build(
            MoonDiscModel.BuildQuad(direction, sun, face),
            SkyDistance,
            subdivisions: 2);
        if (mesh is null)
        {
            mesh = api.Render.UploadMesh(meshData);
        }
        else
        {
            api.Render.UpdateMesh(mesh, meshData);
        }

        lastDirection = direction;
        lastSun = sun;
        lastFaceId = face.Id;
    }

    private int ResolveTexture(MoonPhaseFace face)
    {
        if (textureIds.TryGetValue(face.TexturePath, out var textureId))
        {
            return textureId;
        }

        if (failedTexturePaths.Contains(face.TexturePath))
        {
            return 0;
        }

        try
        {
            textureId = api.Render.GetOrLoadTexture(new AssetLocation(face.TexturePath + ".png"));
            if (textureId != 0)
            {
                textureIds[face.TexturePath] = textureId;
                return textureId;
            }
        }
        catch (Exception exception)
        {
            api.Logger.Error("AstraTerra could not load the moon texture {0}: {1}", face.TexturePath, exception);
        }

        // Said once per face: with no picture the game's own moon keeps the sky, which is a working
        // sky rather than an empty one.
        failedTexturePaths.Add(face.TexturePath);
        api.Logger.Warning(
            "AstraTerra is leaving Vintage Story's moon in place: the {0} texture could not be loaded ({1}).",
            face.DisplayName,
            face.TexturePath);
        return 0;
    }

    private static double Separation(SkyDirection left, SkyDirection right)
    {
        var dot = (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
        return Math.Acos(Math.Clamp(dot, -1.0, 1.0));
    }

    private static float[] IdentityModelMatrix()
        =>
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f
        ];
}
