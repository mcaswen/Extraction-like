# URP Toon Outline System

This package is built for Unity 2022.3 and URP 14. The main outline path is an inverted hull pass rendered globally by `ToonGlobalOutlineFeature`; the optional Sobel post effect is only a global consistency layer.

## Mesh Smooth Normals

Use `Tools/Rendering/Toon Outlines/Bake Smooth Normals To Tangent` for the cheapest path. The shader reads `tangent.xyz` as an object-space smoothed normal when `Smooth Normal Source` is `0`.

Use `Tools/Rendering/Toon Outlines/Bake Smooth Normals To UV2` when a material needs its original tangents for normal maps. In this mode set `Smooth Normal Source` to `1`. UV2 stores the smoothed normal encoded from signed `-1..1` to `0..1`.

The baker creates new mesh assets under `Assets/Art/Generated/ToonSmoothNormalMeshes`, so imported source meshes stay untouched.

## Global Inverted Hull Outline

Create a material with `TA/Toon/URP Toon Lit Outline`.

- `Outline Thickness Pixels` is screen-space thickness in pixels.
- `Near Thickness Scale` and `Far Thickness Scale` control how thick the hull is near and far from the camera.
- `Distance Thickness Start` and `Distance Thickness End` define the camera-distance range used for that interpolation.
- `Distance Thickness Curve` bends the interpolation. Values above `1` keep the near thickness for longer; values below `1` move toward the far thickness faster.
- `Smooth Normal Blend` mixes the imported mesh normal and baked smoothed normal.
- `Outline Intensity` multiplies outline RGB.
- `Shadow Threshold` controls the Toon lighting ramp.

The outline pass uses `Cull Front` and expands along the baked smoothed normal. The pass uses `LightMode = ToonOutline`, so URP does not draw it automatically as a normal forward pass; `ToonGlobalOutlineFeature` draws it globally. The final offset is applied in clip space, so the base width is stable on screen, then the distance scale is applied deliberately. Set `Far Thickness Scale` to `0` for a fade-out style, below `1` for thinner distant outlines, or above `1` for thicker distant silhouettes.

Add `ToonGlobalOutlineFeature` to the active URP Renderer Data. Use its `Layer Mask` and `Render Queue` to decide which renderers contribute to the global hull outline. `Global Thickness Scale` is a true global shader value set by the renderer feature, so it multiplies every material's outline thickness at render time without editing each character material.

## Multi-Region Colors

The most stable production path is `Outline Region Mask RGBA`.

- R: hair outline color
- G: cloth outline color
- B: skin outline color
- A: accent outline color

Enable `Use Outline Mask Map` to read this texture.

`Use Lightmap Alpha Region` is also supported for pipelines that already pack character regions into lightmap alpha. It interprets alpha as compact IDs:

- `0.20..0.45`: hair
- `0.45..0.70`: cloth
- `0.70..0.95`: skin
- `0.95..1.00`: accent

If the shader variant has no `LIGHTMAP_ON`, it automatically falls back to the mask map path.

## Face Control

Assign a `Face Mask R Suppress` texture where the face area is white in the red channel.

- `Face Outline Suppress` removes hull alpha on the face region.
- `Face Front View Suppress` reduces front-facing face outlines while keeping side silhouettes.
- `Face View Falloff` controls how quickly the side silhouette comes back.

For Genshin/Wuthering Waves style characters, keep the face material separate when possible and use low `Face Outline Suppress` on hair/neck materials.

## Optional Sobel Renderer Feature

Add `ToonSobelOutlineFeature` to the active URP Renderer Data only if you need a global depth/normal edge layer.

The feature requests depth and normals with `ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal`, then runs `Hidden/TA/Toon/Sobel Outline` before post-processing.

- `Depth Threshold` catches object intersections and outer silhouettes.
- `Normal Threshold` catches normal discontinuities.
- `Depth Weight` and `Normal Weight` balance both sources.
- `Thickness` is a small 1 to 4 pixel sampling radius.

On mobile, keep `Thickness` at `1`, avoid enabling the Sobel pass on every camera, and rely on per-object hull outlines as the primary style.
