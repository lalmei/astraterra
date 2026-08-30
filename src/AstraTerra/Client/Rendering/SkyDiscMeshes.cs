using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Builds the disc the way its owner scratched it: a mark cut into it wherever the sun was seen.
/// </summary>
/// <remarks>
/// The band lives on the itemstack, so two discs of the same item are different objects and must be
/// drawn differently. Vintage Story hands an item one model, so the model is built here per stack
/// and kept against a name for exactly what it looks like — a disc that has not been scratched since
/// the last frame is never rebuilt, and two discs marked alike share one model.
/// <para>
/// A mark is a scratch, so it is drawn as the tarnish of a line cut through the surface rather than
/// as anything standing on it. It is still geometry, because a disc's marks differ per stack and a
/// texture on the shape is the same for every disc — but the geometry is a decal: a hairline the
/// width of a scratch, flush with the surface it is cut into. Inlaying gold into those cuts is the
/// disc owner's own later work, and nothing here presumes it has been done.
/// </para>
/// </remarks>
public sealed class SkyDiscMeshes : IDisposable
{
    /// <summary>
    /// Where every mark begins: at the rim, tucked a little under it so no hairline of bare face
    /// shows between the two.
    /// </summary>
    /// <remarks>
    /// Both bands start here. Which band a mark belongs to is its bearing — sunsets west, sunrises
    /// east — and a scratch made at the rim is a scratch made at the rim whichever it was. Setting
    /// the two off at different radii only made the same gesture look like two different ones.
    /// </remarks>
    private const double MarkInnerRadius = 4.75;

    // A scratch is a line cut across the surface, not an object put on it. So a mark is a hairline in
    // width, running outward from the rim across the open face — far enough to be read, and stopping
    // short of the disc's edge at every bearing. The two the band stops at are the same cut carried
    // further: longer, never taller, and still inside the disc.
    private const double MarkWidth = 0.14;
    private const double MarkLength = 1.15;
    private const double EdgeMarkLength = 1.65;

    /// <summary>The top of the disc's own face, which every mark is cut into.</summary>
    private const double BodyTop = 0.8;

    /// <summary>Sunk below the surface, so a mark reads as cut through it rather than laid on it.</summary>
    private const double MarkDepth = 0.08;

    /// <summary>
    /// How far a mark's own top stands above the surface it is cut into: a hair, and no more. It
    /// cannot be nothing — two faces at one height flicker against each other — but at this height
    /// the eye reads a line in the surface rather than a ridge on it.
    /// </summary>
    private const double MarkProud = 0.01;

    /// <summary>
    /// The tarnish of a fresh cut, not an ornament. Gold is what a disc's owner puts into a mark
    /// afterwards, so a mark is not born gold.
    /// </summary>
    private const string MarkTexture = "bronze-dark";
    private const string ShapePath = "astraterra:shapes/item/sky-disc.json";

    private readonly ICoreClientAPI api;
    private readonly Dictionary<string, MultiTextureMeshRef> meshes = [];

    /// <summary>Chunk meshes are built on worker threads, and the shape they are built from is shared.</summary>
    private readonly Lock gate = new();
    private Shape? baseShape;

    public SkyDiscMeshes(ICoreClientAPI api)
    {
        this.api = api;
    }

    /// <summary>
    /// The client's own cache, which the item reaches for while it is being drawn. Client-side and
    /// holder-agnostic: any disc in view is drawn from here, including one lying in somebody's hand
    /// across the room.
    /// </summary>
    public static SkyDiscMeshes? Active { get; private set; }

    public static void Install(ICoreClientAPI api)
    {
        Reset();
        Active = new SkyDiscMeshes(api);
    }

    public static void Reset()
    {
        Active?.Dispose();
        Active = null;
    }

    /// <summary>
    /// The model for one disc, or nothing if it is not built and this is no place to build it.
    /// </summary>
    /// <remarks>
    /// Uploading a model is an OpenGL call and OpenGL belongs to the main thread. A disc drawn where
    /// it lies goes through <see cref="BuildDisplayMesh"/> instead, which only tesselates.
    /// </remarks>
    public MultiTextureMeshRef? Get(Item item, SolarBand? band)
    {
        var face = SkyDiscFace.Read(band);
        if (face.Count == 0)
        {
            // An unscratched disc is the shape as authored, which the game already has a model for.
            return null;
        }

        var key = CacheKey(item, face);
        if (meshes.TryGetValue(key, out var cached) && !cached.Disposed)
        {
            return cached;
        }

        if (!IsMainThread)
        {
            return null;
        }

        var built = Build(item, face);
        if (built is null)
        {
            return null;
        }

        meshes[key] = built;
        return built;
    }

    /// <summary>
    /// A name for exactly what one disc looks like, which is what the game caches its model against.
    /// </summary>
    public static string CacheKey(Item item, SolarBand? band)
        => CacheKey(item, SkyDiscFace.Read(band));

    /// <summary>
    /// The disc's geometry, for a caller that draws it into a chunk rather than into a hand.
    /// </summary>
    /// <remarks>
    /// Tesselating is safe on the worker threads that build chunk meshes — it is only uploading that
    /// belongs to the main one — so a disc lying in the world is built where it is asked for, into
    /// whichever atlas the block is drawn from. Nothing back means an unscratched disc, which the
    /// caller already has a model for.
    /// </remarks>
    public MeshData? BuildDisplayMesh(Item item, SolarBand? band, ITextureAtlasAPI targetAtlas)
    {
        var face = SkyDiscFace.Read(band);
        if (face.Count == 0)
        {
            return null;
        }

        lock (gate)
        {
            var shape = MarkedShape(item, face);
            if (shape is null)
            {
                return null;
            }

            var textures = TextureLocations(item, shape);
            var source = new ContainedTextureSource(
                api,
                targetAtlas,
                textures,
                $"Sky disc {item.Code}");

            api.Tesselator.TesselateShape("astraterra:sky-disc", shape, out MeshData? mesh, source, null);
            return mesh;
        }
    }

    private static bool IsMainThread
        => Environment.CurrentManagedThreadId == RuntimeEnv.MainThreadId;

    public void Dispose()
    {
        foreach (var mesh in meshes.Values)
        {
            if (!mesh.Disposed)
            {
                mesh.Dispose();
            }
        }

        meshes.Clear();
        baseShape = null;
    }

    private static string CacheKey(Item item, IReadOnlyList<SkyDiscFaceMark> face)
        => $"{item.Code}|{SkyDiscFace.Key(face)}";

    /// <summary>The authored disc with this disc's own marks added to it.</summary>
    private Shape? MarkedShape(Item item, IReadOnlyList<SkyDiscFaceMark> face)
    {
        baseShape ??= Shape.TryGet(api, ShapePath);
        if (baseShape is null)
        {
            api.Logger.Warning("AstraTerra could not load the sky disc shape; its marks will not show.");
            return null;
        }

        var shape = baseShape.Clone();
        var template = shape.GetElementByName("sunrise-rim-segment-01");
        if (template is null)
        {
            api.Logger.Warning("AstraTerra found no rim segment to copy on the sky disc shape; its marks will not show.");
            return null;
        }

        shape.Elements = [.. shape.Elements, .. face.Select((mark, index) => Scratch(template, mark, index))];
        return shape;
    }

    /// <summary>
    /// Where this disc's textures live, which is a question the item answers rather than the shape:
    /// the shape is authored in bronze, and a copper or a clay disc overrides every code on it.
    /// </summary>
    private static Dictionary<string, AssetLocation> TextureLocations(Item item, Shape shape)
    {
        var textures = new Dictionary<string, AssetLocation>(shape.Textures);
        foreach (var (code, texture) in item.Textures)
        {
            textures[code] = texture.Baked?.BakedName ?? texture.Base;
        }

        return textures;
    }

    private MultiTextureMeshRef? Build(Item item, IReadOnlyList<SkyDiscFaceMark> face)
    {
        var shape = MarkedShape(item, face);
        if (shape is null)
        {
            return null;
        }

        api.Tesselator.TesselateShape(item, shape, out MeshData? mesh);
        api.Logger.Notification(
            "AstraTerra sky disc model built: item={0}; marks={1}; elements={2}; mesh={3}",
            item.Code,
            face.Count,
            shape.Elements.Length,
            mesh is null ? "none" : mesh.VerticesCount.ToString());

        return mesh is null ? null : api.Render.UploadMultiTextureMesh(mesh);
    }

    /// <summary>
    /// One mark, cut into the disc where the sun was seen.
    /// </summary>
    /// <remarks>
    /// The rim segments on the shape are placed by their own corners and merely turned to sit square
    /// to the rim, so a mark is placed the same way: put its centre on the circle at its own angle,
    /// then turn it by that angle so it lies along the radius. Zero degrees is the far side of the
    /// disc's face, and the angle runs the way a bearing does. The centre wanted is therefore half a
    /// mark's length out from where the mark starts.
    /// </remarks>
    private static ShapeElement Scratch(ShapeElement template, SkyDiscFaceMark mark, int index)
    {
        var length = mark.IsEdge ? EdgeMarkLength : MarkLength;
        var radius = MarkInnerRadius + (length / 2.0);
        var floor = BodyTop - MarkDepth;
        var ceiling = BodyTop + MarkProud;
        var radians = mark.NotchDeg * Math.PI / 180.0;
        var centreX = 8.0 + (radius * Math.Sin(radians));
        var centreZ = 8.0 + (radius * Math.Cos(radians));

        var element = template.Clone();
        element.Name = $"mark-{index:000}";
        element.From = [centreX - (MarkWidth / 2.0), floor, centreZ - (length / 2.0)];
        element.To = [centreX + (MarkWidth / 2.0), ceiling, centreZ + (length / 2.0)];
        element.RotationOrigin = [centreX, floor, centreZ];
        element.RotationY = mark.NotchDeg;
        element.Children = null;

        // Clone() copies the array but not the faces in it, and these have to carry the scratch
        // texture without the rim segment they were copied from taking it too. A face the template
        // does not have stays absent here, which is what the empty slots in the array mean.
        var templateFaces = template.FacesResolved ?? [];
        var faces = new ShapeElementFace[templateFaces.Length];
        for (var face = 0; face < faces.Length; face++)
        {
            if (templateFaces[face] is not { } existing)
            {
                continue;
            }

            faces[face] = new ShapeElementFace
            {
                Texture = MarkTexture,
                Uv = existing.Uv is null ? null : (float[])existing.Uv.Clone(),
                Rotation = existing.Rotation,
                Enabled = existing.Enabled,
            };
        }

        element.FacesResolved = faces;

        return element;
    }
}
