using AstraTerra.Astronomy;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Draws the bodies near enough to show a disc: the planet a moon world hangs beneath, and the
/// sibling moons crossing that sky.
/// </summary>
/// <remarks>
/// <para>
/// A pass of its own rather than part of the star pass, because these bodies are not stars in any
/// respect that matters here. They are up in daylight, they are wide enough to need geometry rather
/// than a sprite, and they must be drawn over the star field rather than among it. It runs just
/// after the sun and moon and before terrain, so the ground still covers whatever has set.
/// </para>
/// <para>
/// Nothing is drawn until something authors a catalog -- a world with an ordinary sky has no near
/// bodies, and this pass then costs one comparison per frame.
/// </para>
/// </remarks>
public sealed class NearBodyRenderer : IRenderer
{
    /// <summary>After the sun, moon and star field, before opaque terrain occludes what has set.</summary>
    public const double NearBodyRenderOrder = 0.31;

    /// <summary>Just inside the star sphere: these bodies are between the observer and the stars.</summary>
    private const float SkyDistance = 39.5f;

    /// <summary>How far a body may drift before its mesh is rebuilt: a fifth of an arcminute.</summary>
    private const double PositionTolerance = 0.00006;

    /// <summary>How far the sun may move before the shading is redone: a tenth of a degree.</summary>
    private const double SunTolerance = 0.0017;

    private readonly ICoreClientAPI api;
    private readonly Dictionary<string, BodyPass> passes = new(StringComparer.Ordinal);
    private readonly float[] modelMatrix = IdentityModelMatrix();
    private NearBodyCatalog catalog = NearBodyCatalog.Empty;
    private NearBodyCatalog? pending;
    private bool renderingDisabledAfterFailure;

    public NearBodyRenderer(ICoreClientAPI api)
    {
        this.api = api;
    }

    public double RenderOrder => NearBodyRenderOrder;

    public int RenderRange => 9999;

    /// <summary>
    /// Whether the game's own moon should stand down. Read by the patch that suppresses it, which
    /// cannot see this instance.
    /// </summary>
    public static bool HidesVanillaMoon { get; private set; }

    /// <summary>Hands over the bodies to draw. Takes effect on the next frame.</summary>
    public void Apply(NearBodyCatalog? replacement)
    {
        pending = replacement ?? NearBodyCatalog.Empty;
        renderingDisabledAfterFailure = false;
    }

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (renderingDisabledAfterFailure || stage != EnumRenderStage.Opaque)
        {
            return;
        }

        if (pending is { } replacement)
        {
            pending = null;
            catalog = replacement;
            HidesVanillaMoon = replacement.HidesVanillaMoon;
            RetireUnusedPasses(replacement);
        }

        if (catalog.Bodies.Count == 0)
        {
            return;
        }

        try
        {
            Draw();
        }
        catch (Exception exception)
        {
            renderingDisabledAfterFailure = true;
            api.Logger.Error("AstraTerra stopped drawing near bodies after a render failure: {0}", exception);
        }
    }

    public void Dispose()
    {
        foreach (var pass in passes.Values)
        {
            pass.Dispose();
        }

        passes.Clear();
        HidesVanillaMoon = false;
    }

    private void Draw()
    {
        var calendar = api.World.Calendar;
        var entity = api.World.Player?.Entity;
        if (entity is null)
        {
            return;
        }

        var position = entity.Pos;
        var latitude = LatitudeMapper.MapGameLatitude(
            position.Z,
            calendar.OnGetLatitude is null ? null : z => calendar.OnGetLatitude(z));
        var longitude = LatitudeMapper.MapWorldLongitude(
            position.X,
            api.World.BlockAccessor.MapSizeX,
            api.World.BlockAccessor.MapSizeZ);
        var localSiderealAngle = CelestialMath.GetVanillaAlignedLocalSiderealAngle(
            calendar.TotalDays,
            Math.Max(1, calendar.DaysPerYear),
            Math.Max(1.0, calendar.HoursPerDay),
            longitude);

        var sunPosition = calendar.GetSunPosition(position.XYZ, calendar.TotalDays).Clone().Normalize();
        var placed = NearBodyRenderModel.Place(
            catalog,
            calendar.TotalDays,
            latitude,
            localSiderealAngle,
            new SkyDirection(sunPosition.X, sunPosition.Y, sunPosition.Z));
        if (placed.Count == 0)
        {
            return;
        }

        // The sky sphere is centred on the player's eye, the same origin the star pass draws in.
        modelMatrix[13] = (float)entity.LocalEyePos.Y
            - ((float)entity.Pos.Y - api.World.SeaLevel) / 10000f;

        var render = api.Render;
        var shader = render.StandardShader;

        render.GlToggleBlend(true, EnumBlendMode.Standard);
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
            shader.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            shader.RgbaLightIn = ColorUtil.WhiteArgbVec;
            ((IShaderProgram)shader).UniformMatrix("modelMatrix", modelMatrix);

            var daylight = Math.Clamp(calendar.DayLightStrength, 0.0, 1.0);
            foreach (var body in placed)
            {
                PassFor(body.Body).Draw(shader, body, SkyDistance, daylight);
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
    }

    private BodyPass PassFor(NearBodyEntry body)
    {
        if (passes.TryGetValue(body.Id, out var pass) && pass.Matches(body))
        {
            return pass;
        }

        pass?.Dispose();
        pass = new BodyPass(api, body);
        passes[body.Id] = pass;
        return pass;
    }

    private void RetireUnusedPasses(NearBodyCatalog replacement)
    {
        var live = replacement.Bodies.Select(static body => body.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in passes.Keys.Where(id => !live.Contains(id)).ToList())
        {
            passes[id].Dispose();
            passes.Remove(id);
        }
    }

    private static float[] IdentityModelMatrix()
        =>
        [
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            0f, 0f, 1f, 0f,
            0f, 0f, 0f, 1f
        ];

    /// <summary>One body's texture and mesh, kept for as long as that body is in the catalog.</summary>
    private sealed class BodyPass
    {
        private readonly ICoreClientAPI api;
        private readonly NearBodyFace face;
        private LoadedTexture texture;
        private MeshRef? mesh;
        private bool uploaded;
        private SkyDirection lastDirection;
        private SkyDirection lastSun;
        private double lastDaylight = -1.0;

        public BodyPass(ICoreClientAPI api, NearBodyEntry body)
        {
            this.api = api;
            face = body.Face;
            texture = new LoadedTexture(api) { Width = face.Size, Height = face.Size };
        }

        public bool Matches(NearBodyEntry body) => ReferenceEquals(body.Face, face);

        public void Draw(IStandardShaderProgram shader, PlacedNearBody placed, float radius, double daylight)
        {
            EnsureTexture();
            if (texture.TextureId == 0)
            {
                return;
            }

            // A face is tens of thousands of vertices, and nothing about it changes quickly: the sky
            // turns, the sun moves, and both do it slowly enough that rebuilding on every frame is
            // work nobody sees. Rebuild when the geometry has actually moved.
            if (mesh is null || HasMoved(placed, daylight))
            {
                var meshData = NearBodyMeshBuilder.Build([placed], radius, capacity: 1, daylight);
                if (mesh is null)
                {
                    mesh = api.Render.UploadMesh(meshData);
                }
                else
                {
                    api.Render.UpdateMesh(mesh, meshData);
                }

                lastDirection = placed.Direction;
                lastSun = placed.SunDirection;
                lastDaylight = daylight;
            }

            shader.Tex2D = texture.TextureId;
            api.Render.RenderMesh(mesh);
        }

        public void Dispose()
        {
            mesh?.Dispose();
            mesh = null;
            texture.Dispose();
        }

        /// <summary>
        /// Whether anything has changed enough to be worth rebuilding tens of thousands of
        /// vertices for.
        /// </summary>
        /// <remarks>
        /// Where the body is and where its light comes from are held to very different standards.
        /// A body's own position is the thing a player watches move, and it is baked into the
        /// vertices, so the tolerance on it has to be far below what the eye can catch -- through a
        /// telescope especially, where magnification multiplies any step. A tenth of a degree was
        /// enough to make a moon crossing the sky visibly stutter. The sun only shades those
        /// vertices, and a tenth of a degree of it changes nothing anyone can see.
        ///
        /// The saving is kept where it was worth having: a tidally locked world's parent planet
        /// does not move at all, and it is the body with the vertices.
        /// </remarks>
        private bool HasMoved(PlacedNearBody placed, double daylight)
            => Math.Abs(daylight - lastDaylight) > 0.01
               || Separation(placed.Direction, lastDirection) > PositionTolerance
               || Separation(placed.SunDirection, lastSun) > SunTolerance;

        private static double Separation(SkyDirection left, SkyDirection right)
        {
            var dot = (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
            return Math.Acos(Math.Clamp(dot, -1.0, 1.0));
        }

        private void EnsureTexture()
        {
            if (uploaded)
            {
                return;
            }

            uploaded = true;
            texture.Width = face.Size;
            texture.Height = face.Size;
            api.Render.LoadOrUpdateTextureFromRgba(face.RgbaPixels, true, 0, ref texture);
        }
    }
}

/// <summary>
/// Stands Vintage Story's moon down while near bodies replace it.
/// </summary>
/// <remarks>
/// The moon's scale is a field on the renderer rather than something the draw call takes, so this
/// zeroes it before the frame and puts it back the moment the catalog stops asking. Nothing else
/// about the moon changes: it still lights the world and still drives the phase the calendar
/// reports, so the day and the night keep the length and the brightness they had.
/// </remarks>
[HarmonyPatch(typeof(SystemRenderSunMoon), nameof(SystemRenderSunMoon.OnRenderFrame3D))]
public static class VanillaMoonSuppressionPatch
{
    private static float vanillaMoonScale;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    public static void Prefix(SystemRenderSunMoon __instance)
    {
        if (vanillaMoonScale <= 0f && __instance.moonScale > 0f)
        {
            vanillaMoonScale = __instance.moonScale;
        }

        __instance.moonScale = NearBodyRenderer.HidesVanillaMoon ? 0f : vanillaMoonScale;
    }
}
