using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace AstraTerra.Client.Rendering;

/// <summary>What a sky picture does past its edges.</summary>
public enum SkyTextureWrap
{
    /// <summary>The picture continues from its opposite edge, as galactic longitude does.</summary>
    Repeat,

    /// <summary>The picture stops at its last texel, as a plate cut out of the sky does.</summary>
    ClampToEdge
}

/// <summary>
/// Loads the sky pictures a telescope magnifies: smoothed when enlarged, mipmapped when shrunk.
/// </summary>
/// <remarks>
/// <see cref="IRenderAPI.GetOrLoadTexture(AssetLocation)"/> is the wrong loader for these. It
/// uploads every texture with nearest-neighbour magnification and no mipmaps, which suits the game's
/// block textures and nothing that is meant to be looked at up close. The Milky Way at the brass
/// scope's full zoom spreads one texel over some twenty screen pixels, and drawn nearest that is a
/// staircase of flat squares running along the band.
/// <para>
/// A texture loaded here is not in the game's shared cache, so whoever loads it owns it and has to
/// dispose it.
/// </para>
/// </remarks>
public static class SkyTextureLoader
{
    /// <summary><see cref="IRenderAPI.LoadTexture"/>'s clamp mode for clamp-to-edge on both axes.</summary>
    public const int ClampToEdgeClampMode = 1;

    /// <summary><see cref="IRenderAPI.LoadTexture"/>'s clamp mode for repeat on both axes.</summary>
    public const int RepeatClampMode = 2;

    private const string TexturesPrefix = "textures/";

    // Skia decodes all of these despite the loader's name, so a photograph can ship as a JPEG.
    private static readonly string[] ImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static int ToClampMode(SkyTextureWrap wrap) => wrap switch
    {
        SkyTextureWrap.Repeat => RepeatClampMode,
        SkyTextureWrap.ClampToEdge => ClampToEdgeClampMode,
        _ => throw new ArgumentOutOfRangeException(nameof(wrap), wrap, null)
    };

    /// <summary>
    /// The asset a configured texture path names, spelled the way the asset manager stores it.
    /// </summary>
    /// <remarks>
    /// Configured paths follow the shorthand <see cref="IRenderAPI.GetOrLoadTexture(AssetLocation)"/>
    /// accepts, with the <c>textures/</c> folder and the <c>.png</c> left off. Reading the asset
    /// directly means filling both back in, while keeping an extension that is already there.
    /// </remarks>
    public static string ToAssetPath(string texturePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texturePath);

        var domainSeparatorIndex = texturePath.IndexOf(':', StringComparison.Ordinal);
        var domain = domainSeparatorIndex < 0 ? null : texturePath[..domainSeparatorIndex];
        var path = domainSeparatorIndex < 0 ? texturePath : texturePath[(domainSeparatorIndex + 1)..];
        if (!path.StartsWith(TexturesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = TexturesPrefix + path;
        }

        if (!HasImageExtension(path))
        {
            path += ".png";
        }

        return domain is null ? path : domain + ":" + path;
    }

    /// <summary>
    /// Uploads a sky picture, or returns null when the asset is missing or will not upload.
    /// </summary>
    /// <remarks>Must run on the render thread: there is one OpenGL context.</remarks>
    public static LoadedTexture? TryLoad(ICoreClientAPI clientApi, string texturePath, SkyTextureWrap wrap)
    {
        ArgumentNullException.ThrowIfNull(clientApi);

        var asset = clientApi.Assets.TryGet(new AssetLocation(ToAssetPath(texturePath)));
        if (asset?.Data is not { Length: > 0 } data)
        {
            return null;
        }

        using var bitmap = clientApi.Render.BitmapCreateFromPng(data);
        var texture = new LoadedTexture(clientApi);
        clientApi.Render.LoadTexture(
            bitmap,
            ref texture,
            linearMag: true,
            clampMode: ToClampMode(wrap),
            generateMipmaps: true);
        if (texture.TextureId == 0)
        {
            texture.Dispose();
            return null;
        }

        return texture;
    }

    private static bool HasImageExtension(string path)
    {
        foreach (var extension in ImageExtensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
