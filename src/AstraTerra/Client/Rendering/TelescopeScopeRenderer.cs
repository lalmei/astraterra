using AstraTerra.Client.Observation;
using AstraTerra.Observation;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace AstraTerra.Client.Rendering;

public sealed class TelescopeScopeRenderer : IRenderer
{
    private const string BrassScopeTitle = "Brass Telescope scoped view";
    private const string PrecisionScopeTitle = "Precision Telescope scoped view";
    private const string BrassScopeTexturePath = "textures/gui/telescope-scope.png";
    private const string PrecisionScopeTexturePath = "textures/gui/telescope-scope-precision.png";
    private const int TransparentRgba = 0x00000000;
    private const float ScopeClearRadiusFactor = 0.90f;
    private const float TitleTopMargin = 18.0f;

    private readonly ICoreClientAPI api;
    private int brassTextureId;
    private int precisionTextureId;
    private LoadedTexture maskTexture;
    private LoadedTexture? titleTexture;
    private int maskFrameWidth;
    private int maskFrameHeight;
    private float maskScopeX;
    private float maskScopeY;
    private float maskScopeSize;
    private ObservationMode? titleMode;
    private bool? titleUsesPrecisionScope;

    public TelescopeScopeRenderer(ICoreClientAPI api)
    {
        this.api = api;
        maskTexture = new LoadedTexture(api)
        {
            Width = 1,
            Height = 1
        };
    }

    public double RenderOrder => 0.98;

    public int RenderRange => 9999;

    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        if (!TelescopeScopeState.IsScoped)
        {
            return;
        }

        var usesPrecisionScope = TelescopeScopeState.MaxZoomStep > TelescopeObservationState.MaxZoomStep;
        var textureId = GetScopeTextureId(usesPrecisionScope);

        if (textureId == 0)
        {
            return;
        }

        var frameWidth = api.Render.FrameWidth;
        var frameHeight = api.Render.FrameHeight;
        var size = (float)Math.Min(frameWidth, frameHeight) * 0.86f;
        var x = (frameWidth - size) / 2f;
        var y = (frameHeight - size) / 2f;

        EnsureMaskTexture(frameWidth, frameHeight, x, y, size);
        if (maskTexture.TextureId != 0)
        {
            api.Render.Render2DTexture(maskTexture.TextureId, 0, 0, frameWidth, frameHeight, 998f, new Vec4f(1f, 1f, 1f, 1f));
        }

        var mode = TelescopeScopeState.Mode;
        api.Render.Render2DTexture(textureId, x, y, size, size, 999f, new Vec4f(1f, 1f, 1f, 1f));
        RenderTitle(frameWidth, mode, usesPrecisionScope);
    }

    public void Dispose()
    {
        DeleteTexture(maskTexture);
        DeleteTexture(titleTexture);
    }

    private void EnsureMaskTexture(int frameWidth, int frameHeight, float scopeX, float scopeY, float scopeSize)
    {
        if (maskTexture.TextureId != 0 &&
            maskFrameWidth == frameWidth &&
            maskFrameHeight == frameHeight &&
            Math.Abs(maskScopeX - scopeX) < 0.5f &&
            Math.Abs(maskScopeY - scopeY) < 0.5f &&
            Math.Abs(maskScopeSize - scopeSize) < 0.5f)
        {
            return;
        }

        var pixels = BuildScopeMask(frameWidth, frameHeight, scopeX + (scopeSize / 2f), scopeY + (scopeSize / 2f), (scopeSize / 2f) * ScopeClearRadiusFactor);
        maskTexture.Width = frameWidth;
        maskTexture.Height = frameHeight;
        api.Render.LoadOrUpdateTextureFromRgba(pixels, true, 0, ref maskTexture);
        maskFrameWidth = frameWidth;
        maskFrameHeight = frameHeight;
        maskScopeX = scopeX;
        maskScopeY = scopeY;
        maskScopeSize = scopeSize;
    }

    private int GetScopeTextureId(bool usesPrecisionScope)
    {
        if (usesPrecisionScope)
        {
            precisionTextureId = precisionTextureId == 0
                ? api.Render.GetOrLoadTexture(new AssetLocation("astraterra", PrecisionScopeTexturePath))
                : precisionTextureId;
            return precisionTextureId;
        }

        brassTextureId = brassTextureId == 0
            ? api.Render.GetOrLoadTexture(new AssetLocation("astraterra", BrassScopeTexturePath))
            : brassTextureId;
        return brassTextureId;
    }

    private void RenderTitle(int frameWidth, ObservationMode mode, bool usesPrecisionScope)
    {
        if (titleTexture is null || titleMode != mode || titleUsesPrecisionScope != usesPrecisionScope)
        {
            DeleteTitleTexture();
            var scopeTitle = usesPrecisionScope ? PrecisionScopeTitle : BrassScopeTitle;
            titleTexture = api.Gui.TextTexture.GenTextTexture(
                $"{scopeTitle} - {FormatMode(mode)}",
                new CairoFont(18, GuiStyle.StandardFontName, ColorUtil.WhiteArgbDouble, ColorUtil.BlackArgbDouble),
                null);
            titleMode = mode;
            titleUsesPrecisionScope = usesPrecisionScope;
        }

        if (titleTexture.TextureId == 0)
        {
            return;
        }

        var titleX = (frameWidth - titleTexture.Width) / 2f;
        api.Render.Render2DLoadedTexture(titleTexture, titleX, TitleTopMargin, 1000f);
    }

    private void DeleteTitleTexture()
    {
        DeleteTexture(titleTexture);
    }

    private void DeleteTexture(LoadedTexture? texture)
    {
        if (texture is null)
        {
            return;
        }

        if (texture.TextureId != 0)
        {
            try
            {
                api.Render.GLDeleteTexture(texture.TextureId);
            }
            catch (Exception exception)
            {
                api.Logger.Warning("AstraTerra could not delete telescope texture during cleanup: {0}", exception.Message);
            }
        }

        texture.TextureId = 0;
    }

    private static string FormatMode(ObservationMode mode) => mode switch
    {
        ObservationMode.RemoveSegment => "Remove Segment",
        ObservationMode.Draw => "Create Constellation",
        ObservationMode.Inspect => "Inspect Constellation",
        _ => "Observe"
    };

    private static int[] BuildScopeMask(int width, int height, float centerX, float centerY, float radius)
    {
        var pixels = new int[width * height];
        var feather = Math.Max(2.0f, radius * 0.01f);
        var outerRadius = radius + feather;
        var innerRadius = radius - feather;
        var outerRadiusSquared = outerRadius * outerRadius;
        var innerRadiusSquared = innerRadius * innerRadius;

        for (var y = 0; y < height; y++)
        {
            var dy = y + 0.5f - centerY;
            for (var x = 0; x < width; x++)
            {
                var dx = x + 0.5f - centerX;
                var distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= innerRadiusSquared)
                {
                    pixels[(y * width) + x] = TransparentRgba;
                    continue;
                }

                var alpha = 255;
                if (distanceSquared < outerRadiusSquared)
                {
                    var distance = MathF.Sqrt(distanceSquared);
                    alpha = (int)Math.Clamp(((distance - innerRadius) / (outerRadius - innerRadius)) * 255.0f, 0.0f, 255.0f);
                }

                pixels[(y * width) + x] = ColorUtil.ColorFromRgba(0, 0, 0, alpha);
            }
        }

        return pixels;
    }
}
