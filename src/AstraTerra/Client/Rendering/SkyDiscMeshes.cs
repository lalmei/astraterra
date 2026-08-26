using AstraTerra.Observation;
using Vintagestory.API.Client;
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

    private const double MarkWidth = 0.16;
    private const double MarkLength = 0.72;
    private const double EdgeMarkLength = 1.15;
    private const double MarkFloor = 1.06;
    private const double MarkCeiling = 1.32;
    private const double EdgeMarkCeiling = 1.44;

    /// <summary>
    /// The shape's ornament slot, which each disc fills with what it is made of: gold inlay on
    /// metal, a burnt impression on clay. No hash on the code — the shape loader strips it off face
    /// textures when it resolves them, and "##gold" maps to nothing.
    /// </summary>
    private const string MarkTexture = "gold";
    private const string ShapePath = "astraterra:shapes/item/sky-disc.json";

    private readonly ICoreClientAPI api;
    private readonly Dictionary<string, MultiTextureMeshRef> meshes = [];
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

    /// <summary>The model for one disc, built on first sight of that pattern of marks.</summary>
    public MultiTextureMeshRef? Get(Item item, SolarBand? band)
    {
        var face = SkyDiscFace.Read(band);
        if (face.Count == 0)
        {
            // An unscratched disc is the shape as authored, which the game already has a model for.
            return null;
        }

        var key = $"{item.Code}|{SkyDiscFace.Key(face)}";
        if (meshes.TryGetValue(key, out var cached) && !cached.Disposed)
        {
            return cached;
        }

        var built = Build(item, face);
        if (built is null)
        {
            return null;
        }

        meshes[key] = built;
        return built;
    }

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

    private MultiTextureMeshRef? Build(Item item, IReadOnlyList<SkyDiscFaceMark> face)
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

        MeshData? mesh = null;
        api.Tesselator.TesselateShape(item, shape, out mesh);
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

        // Clone() copies the array but not the faces in it, and these have to be gold without the
        // notch they were copied from turning gold as well.
        element.FacesResolved = [.. (template.FacesResolved ?? []).Select(existing =>
            existing is null
                ? null
                : new ShapeElementFace
                {
                    Texture = MarkTexture,
                    Uv = existing.Uv is null ? null : (float[])existing.Uv.Clone(),
                    Rotation = existing.Rotation,
                    Enabled = existing.Enabled,
                })];

        return element;
    }
}
