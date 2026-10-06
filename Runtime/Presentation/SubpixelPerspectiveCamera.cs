using Unity.Mathematics;
using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Phase 0 perspective sub-pixel camera: renders through the same low-resolution
    /// RenderTexture + RawImage blit chain as the orthographic camera, but performs no
    /// grid snap — screen-space snapping provably cannot stabilize pixels under perspective
    /// projection. Expect shimmer/pixel creep until the cubemap-probe pipeline replaces
    /// SnapToPixelGrid with probe-origin snapping (see Docs/perspective-camera-spike.md).
    /// FOV and clip planes are configured on the backing UnityEngine.Camera component.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class SubpixelPerspectiveCamera : SubpixelCameraBase
    {
        protected override void OnResolutionUpdated()
        {
            if (cameraConfig == null) return;
            unitsPerPixel = 1.0f / cameraConfig.pixelsPerUnit;
        }

        protected override void SnapToPixelGrid()
        {
            // No snap in naive perspective mode: the pivot is applied as-is so pan/recenter
            // keep working through the shared rig. Probe-origin snapping (Phase 1 of
            // Docs/perspective-camera-spike.md) will quantize this later.
            if (transform.parent != null)
            {
                transform.parent.position = referencePosition;
            }

            _SubPixelOffset = float4.zero;
            ApplyViewportMaterial();
            OnCameraSnapped?.Invoke(referencePosition);
        }

        public override float3 WorldPositionToPixelGrid(float3 worldPosition)
        {
            // Axis-aligned voxel snap: the perspective-mode equivalent of the orthographic
            // screen-plane snap. Deliberately world-axis aligned, not view aligned, so hit
            // positions stay stable as the camera rotates.
            return new float3(
                math.floor(worldPosition.x / unitsPerPixel) * unitsPerPixel,
                math.floor(worldPosition.y / unitsPerPixel) * unitsPerPixel,
                math.floor(worldPosition.z / unitsPerPixel) * unitsPerPixel
            );
        }
    }
}
