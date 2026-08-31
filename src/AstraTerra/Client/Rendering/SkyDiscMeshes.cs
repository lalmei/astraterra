using AstraTerra.Astronomy;
using AstraTerra.Constellations;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>
/// Builds the disc the way its owner worked it: solar scratches round the rim and its one figure
/// engraved across the face.
/// </summary>
/// <remarks>
/// The band and figure live on the itemstack, so two discs of the same item are different objects
/// and must be drawn differently. Vintage Story hands an item one model, so the model is built here
/// per stack and kept against a name for exactly what it looks like — a disc that has not been
/// worked since the last frame is never rebuilt, and two discs marked alike share one model.
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

    /// <summary>The narrow cut that joins two punched stars in the figure.</summary>
    private const double FigureLineWidth = 0.14;

    /// <summary>The square punch at every star the figure is hung on.</summary>
    private const double FigureStarSize = 0.24;

    /// <summary>
    /// The shadow and exposed material in a fresh cut, not an ornament. This is a separate texture
    /// from the rim: on clay, <c>bronze-dark</c> is still the clay of the rim itself and disappears
    /// against the face, while <c>engraving</c> stays dark both before and after firing.
    /// </summary>
    private const string MarkTexture = "engraving";
    private const string FigureTexture = "engraving";
    private const string ShapePath = "astraterra:shapes/item/sky-disc.json";

    private readonly ICoreClientAPI api;
    private readonly Dictionary<string, MultiTextureMeshRef> meshes = [];
    private IReadOnlyDictionary<int, StarCatalogEntry> catalogByHip;

    /// <summary>Chunk meshes are built on worker threads, and the shape they are built from is shared.</summary>
    private readonly Lock gate = new();
    private Shape? baseShape;

    public SkyDiscMeshes(ICoreClientAPI api, StarCatalog? catalog)
    {
        this.api = api;
        catalogByHip = Index(catalog);
    }

    /// <summary>
    /// The client's own cache, which the item reaches for while it is being drawn. Client-side and
    /// holder-agnostic: any disc in view is drawn from here, including one lying in somebody's hand
    /// across the room.
    /// </summary>
    public static SkyDiscMeshes? Active { get; private set; }

    public static void Install(ICoreClientAPI api, StarCatalog? catalog)
    {
        Reset();
        Active = new SkyDiscMeshes(api, catalog);
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
    public MultiTextureMeshRef? Get(Item item, SolarBand? band, SkyDiscFigure? figure)
    {
        var face = SkyDiscFace.Read(band);
        var key = CacheKey(item, face, figure);
        if (meshes.TryGetValue(key, out var cached) && !cached.Disposed)
        {
            return cached;
        }

        var sketch = SkyDiscFigureSketch.Project(figure, catalogByHip);
        if (face.Count == 0 && sketch.Lines.Count == 0)
        {
            // An untouched disc is the shape as authored, which the game already has a model for.
            return null;
        }

        if (!IsMainThread)
        {
            return null;
        }

        var built = Build(item, face, sketch);
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
    public static string CacheKey(Item item, SolarBand? band, SkyDiscFigure? figure)
        => CacheKey(item, SkyDiscFace.Read(band), figure);

    /// <summary>
    /// The disc's geometry, for a caller that draws it into a chunk rather than into a hand.
    /// </summary>
    /// <remarks>
    /// Tesselating is safe on the worker threads that build chunk meshes — it is only uploading that
    /// belongs to the main one — so a disc lying in the world is built where it is asked for, into
    /// whichever atlas the block is drawn from. Nothing back means an unscratched disc, which the
    /// caller already has a model for.
    /// </remarks>
    public MeshData? BuildDisplayMesh(
        Item item,
        SolarBand? band,
        SkyDiscFigure? figure,
        ITextureAtlasAPI targetAtlas)
    {
        var face = SkyDiscFace.Read(band);
        var sketch = SkyDiscFigureSketch.Project(figure, catalogByHip);
        if (face.Count == 0 && sketch.Lines.Count == 0)
        {
            return null;
        }

        lock (gate)
        {
            var shape = MarkedShape(item, face, sketch);
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
        ClearMeshes();
        baseShape = null;
    }

    /// <summary>
    /// Reprojects figures against a world-authored replacement catalog and retires the old models.
    /// </summary>
    public void ReplaceCatalog(StarCatalog replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        ClearMeshes();
        catalogByHip = Index(replacement);
    }

    private void ClearMeshes()
    {
        foreach (var mesh in meshes.Values)
        {
            if (!mesh.Disposed)
            {
                mesh.Dispose();
            }
        }

        meshes.Clear();
    }

    private static IReadOnlyDictionary<int, StarCatalogEntry> Index(StarCatalog? starCatalog)
        => starCatalog?.Stars.ToDictionary(star => star.Hip)
            ?? new Dictionary<int, StarCatalogEntry>();

    private static string CacheKey(Item item, IReadOnlyList<SkyDiscFaceMark> face, SkyDiscFigure? figure)
        => $"{item.Code}|band:{SkyDiscFace.Key(face)}|figure:{SkyDiscFigureSketch.Key(figure)}";

    /// <summary>The authored disc with this disc's own marks added to it.</summary>
    private Shape? MarkedShape(
        Item item,
        IReadOnlyList<SkyDiscFaceMark> face,
        SkyDiscFigureSketch sketch)
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

        shape.Elements =
        [
            .. shape.Elements,
            .. face.Select((mark, index) => Scratch(template, mark, index)),
            .. sketch.Lines.Select((line, index) => FigureLine(template, line, index)),
            .. sketch.Stars.Select((star, index) => FigureStar(template, star, index)),
        ];
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

    private MultiTextureMeshRef? Build(
        Item item,
        IReadOnlyList<SkyDiscFaceMark> face,
        SkyDiscFigureSketch sketch)
    {
        var shape = MarkedShape(item, face, sketch);
        if (shape is null)
        {
            return null;
        }

        api.Tesselator.TesselateShape(item, shape, out MeshData? mesh);
        api.Logger.Notification(
            "AstraTerra sky disc model built: item={0}; marks={1}; figureLines={2}; elements={3}; mesh={4}",
            item.Code,
            face.Count,
            sketch.Lines.Count,
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
        element.FacesResolved = Faces(template, MarkTexture);

        return element;
    }

    /// <summary>A narrow rectangular cut between two projected stars.</summary>
    private static ShapeElement FigureLine(ShapeElement template, SkyDiscSketchLine line, int index)
    {
        var startX = 8.0 + line.StartX;
        var startZ = 8.0 - line.StartY;
        var endX = 8.0 + line.EndX;
        var endZ = 8.0 - line.EndY;
        var deltaX = endX - startX;
        var deltaZ = endZ - startZ;
        var length = Math.Sqrt((deltaX * deltaX) + (deltaZ * deltaZ));
        var centreX = (startX + endX) / 2.0;
        var centreZ = (startZ + endZ) / 2.0;
        var floor = BodyTop - MarkDepth;

        var element = template.Clone();
        element.Name = $"figure-line-{index:000}";
        element.From = [centreX - (FigureLineWidth / 2.0), floor, centreZ - (length / 2.0)];
        element.To = [centreX + (FigureLineWidth / 2.0), BodyTop + MarkProud, centreZ + (length / 2.0)];
        element.RotationOrigin = [centreX, floor, centreZ];
        element.RotationY = Math.Atan2(deltaX, deltaZ) * 180.0 / Math.PI;
        element.Children = null;
        element.FacesResolved = Faces(template, FigureTexture);
        return element;
    }

    /// <summary>A small diamond punched at a figure's star.</summary>
    private static ShapeElement FigureStar(ShapeElement template, SkyDiscSketchStar star, int index)
    {
        var centreX = 8.0 + star.X;
        var centreZ = 8.0 - star.Y;
        var half = FigureStarSize / 2.0;
        var floor = BodyTop - MarkDepth;

        var element = template.Clone();
        element.Name = $"figure-star-{index:000}-{star.Hip}";
        element.From = [centreX - half, floor, centreZ - half];
        element.To = [centreX + half, BodyTop + MarkProud, centreZ + half];
        element.RotationOrigin = [centreX, floor, centreZ];
        element.RotationY = 45.0;
        element.Children = null;
        element.FacesResolved = Faces(template, FigureTexture);
        return element;
    }

    /// <summary>The template's six faces, copied so this new element can carry its own cut texture.</summary>
    private static ShapeElementFace[] Faces(ShapeElement template, string texture)
    {
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
                Texture = texture,
                Uv = existing.Uv is null ? null : (float[])existing.Uv.Clone(),
                Rotation = existing.Rotation,
                Enabled = existing.Enabled,
            };
        }

        return faces;
    }
}
