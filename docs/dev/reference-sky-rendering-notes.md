# Reference Sky Rendering Notes

Historical research notes. They record what was learned from local reference sky implementation
packages while AstraTerra's rendering baseline was being settled, and they are kept because the
decisions in [Architecture](architecture.md#rendering-baseline) refer back to them.

The packages themselves were inspected from a local download directory and are not in this
repository, so nothing here is reproducible from a checkout. Treat it as the reasoning behind the
baseline rather than as a description of the current code — [Celestial Model](celestial-model.md)
and [Architecture](architecture.md) are what the renderers actually do now.

## Rendering Takeaways

- The reference implementation renders active sky objects primarily as 3D billboards injected into Vintage Story's sun/moon pass.
- It reuses the vanilla sun/moon quad mesh when available, disables depth test/culling, enables blending, and renders textured quads with `StandardShader`.
- Useful default placement distances were close to the camera by sky-rendering standards: near layer `20`, planetary layer `28`, stellar layer `40`.
- Orthographic rendering is better kept for debug markers, projected overlays, constellation lines, and labels.
- Readability comes from stylized brightness curves and multipliers, not physically faithful magnitude luminance.

## Star Brightness

The reference constellation-star path clamps faint stars to a visible floor:

```csharp
var dimming = Math.Clamp((magnitude - 0.4f) / 1f, 0f, 1f);
return 1f - dimming * 0.8f;
```

Stars fainter than about magnitude `1.4` still render at roughly `20%` brightness before other multipliers. AstraTerra follows the same product direction: magnitude matters, but the sky must remain readable in game.

## AstraTerra Baseline

AstraTerra now:

- keeps Earth-accurate catalog positions and horizon classification in `StarRenderModel`,
- uses a stylized brightness curve,
- preserves horizon fading,
- renders star billboards in the sun/moon 3D pass,
- uses orthographic renderers for constellation overlays, telescope scope UI, and sextant text.

## Atlas Notes From Reference Implementation

The reference atlas system is item-driven. Item stack data records discovered stars or constellations, and renderers use that metadata for highlight bloom and labels. The chart emerges from recorded target metadata rather than from a pre-rendered map texture.

Useful idea for AstraTerra: a future constellation catalog item can start as structured saved entries plus render-time highlighting and labels. It does not need a complex chart texture to become useful.
