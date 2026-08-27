using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Builds the disc the way its owner scratched it: gold on the rim wherever the sun was marked.
/// </summary>
/// <remarks>
/// The band lives on the itemstack, so two discs of the same item are different objects and must be
/// drawn differently. Vintage Story hands an item one model, so the model is built here per stack
/// and kept against a name for exactly what it looks like — a disc that has not been scratched since
/// the last frame is never rebuilt, and two discs marked alike share one model.
/// <para>
/// The marks are geometry rather than a texture, for the same reason the notches are: this is a
/// scratched object, and gold sitting proud of a bronze rim is what the real artefact does.
/// </para>
/// </remarks>
public sealed class SkyDiscMeshes : IDisposable
{
    /// <summary>Where the sunset rim runs, taken from the notches already drawn on that rim.</summary>
    private const double SunsetRadius = 6.17;

    private const double SunriseRadius = 4.66;

    // A scratch is a line cut across the rim, not an object put on it. So a mark is a hairline in
    // width, long enough radially to cross the rim and run out past both its edges, and it lies flush
    // with the rim's surface — barely proud of it, and lower than the graduations it runs between.
    // Standing one up turns it into a token sitting on the disc, which is exactly what it is not.
    private const double MarkWidth = 0.14;
    private const double MarkLength = 2.4;
    private const double EdgeMarkLength = 3.4;

    /// <summary>Sunk into the rim, so the mark reads as cut through its surface rather than laid on it.</summary>
    private const double MarkFloor = 0.98;

    private const double MarkCeiling = 1.1;

    /// <summary>The two the band stops at: the same cut, carried further, never taller.</summary>
    private const double EdgeMarkCeiling = 1.13;

    private const string MarkTexture = "gold";
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
        var template = shape.GetElementByName("sunset-notch-01");
        if (template is null)
        {
            api.Logger.Warning("AstraTerra found no notch to copy on the sky disc shape; its marks will not show.");
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
    /// One gold mark, laid on the rim where the sun went down.
    /// </summary>
    /// <remarks>
    /// The notches on the shape are placed by their own corners and merely turned to sit square to
    /// the rim, so a mark is placed the same way: put its centre on the circle at the notch's angle,
    /// then turn it by that angle so it lies along the radius. Zero degrees is the far side of the
    /// disc's face, and the angle runs the way a bearing does.
    /// </remarks>
    private static ShapeElement Scratch(ShapeElement template, SkyDiscFaceMark mark, int index)
    {
        var radius = mark.Event == SolarEvent.Sunset ? SunsetRadius : SunriseRadius;
        var length = mark.IsEdge ? EdgeMarkLength : MarkLength;
        var ceiling = mark.IsEdge ? EdgeMarkCeiling : MarkCeiling;
        var radians = mark.NotchDeg * Math.PI / 180.0;
        var centreX = 8.0 + (radius * Math.Sin(radians));
        var centreZ = 8.0 + (radius * Math.Cos(radians));

        var element = template.Clone();
        element.Name = $"mark-{index:000}";
        element.From = [centreX - (MarkWidth / 2.0), MarkFloor, centreZ - (length / 2.0)];
        element.To = [centreX + (MarkWidth / 2.0), ceiling, centreZ + (length / 2.0)];
        element.RotationOrigin = [centreX, MarkFloor, centreZ];
        element.RotationY = mark.NotchDeg;
        element.Children = null;

        // Clone() copies the array but not the faces in it, and these have to carry the ornament
        // texture without the notch they were copied from taking it too. A face the template does
        // not have stays absent here, which is what the empty slots in the array mean.
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
