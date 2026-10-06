# PixelEngine - Major WIP, not ready for use yet!

Sub-pixel, pixel-art camera system for Unity. Renders the world to a
reduced-resolution `RenderTexture`, snaps the camera to a 1:1 pixel grid to prevent
"swimming"/shimmering pixels, and blits the result to a UI `RawImage`. Supports a
parallax background camera on the same grid.

This is a standalone engine package: it has its own assemblies and **must not reference
game code**. Game projects consume it (dependency direction is game → engine).

> **Documentation status: WIP.** Function-level API docs are incomplete and the public
> surface is still moving (renames and signature changes are expected). The source under
> `Runtime/Presentation/` is the ground truth until this README is finished.

## Assemblies
- `PixelEngine` (`Runtime/`, code under `Runtime/Presentation/`) — runtime camera code. References only
  `Unity.Mathematics` and `UnityEngine.UI`. `autoReferenced` so a game's predefined `Assembly-CSharp` can use it.
- `PixelEngine.Editor` (`Editor/`) — custom inspector. Editor-only.

## Naming convention
- Namespace is `PixelEngine.Presentation`, **not** `PixelEngine.Camera`: a namespace named `Camera` shadows
  `UnityEngine.Camera`, which this code uses heavily. Likewise a namespace named `Editor` shadows
  `UnityEditor.Editor`, so the editor class fully-qualifies its base type (`: UnityEditor.Editor`).

## What's here (overview)
All under `namespace PixelEngine.Presentation`. Game code should target **`SubpixelCameraBase`**
so it stays camera-mode agnostic; concrete subclasses implement mode-specific behavior.

- `SubpixelCameraBase` — abstract camera surface: zoom, pivot, viewport/picking primitives,
  pixel-grid snapping, and an `OnCameraSnapped` event that companion components and game code
  subscribe to instead of the camera referencing them.
- `SubpixelOrthographicCamera` — orthographic mode; snaps the pivot to the pixel grid.
- `SubpixelPerspectiveCamera` — perspective mode, Phase 0/naive: no grid snap yet (shimmer
  expected until the cubemap-probe pipeline lands).
- `CameraConfig` — ScriptableObject tuning asset (`CreateAssetMenu → PixelEngine/Camera Config`).
- `BackgroundCamera` / `OrthoBackground` — parallax background camera (ortho-only by design) and
  its material hookup.
- `SnapToPixelGrid` — snaps an arbitrary GameObject's transform to the camera's pixel grid.

Per-method documentation is pending — see the source for now.

## Shader contract
The blit/viewport material must expose these uniforms (see the companion `PixelEngineShader` package):
- `_SubPixelOffset` (`Vector4`) — sub-pixel correction offset.
- `_ZoomLevel` (`float`) — current zoom factor.

Keep the C# and the shader in sync on these names.

## Core formulas
- `orthographicSize = renderResolution.y * 0.5 / pixelsPerUnit`
- `unitsPerPixel = 1 / pixelsPerUnit`
- Target **64 pixels per unit**.

## Consuming from a game
Keep input handling and tweening in the game layer (e.g. a `CameraController` that reads the
project's input system and drives this package's public methods via DOTween or similar). The
package itself stays free of input/tween/game dependencies so it remains reusable.
