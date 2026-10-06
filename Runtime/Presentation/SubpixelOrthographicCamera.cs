using Unity.Mathematics;
using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Orthographic sub-pixel camera: snaps the pivot to the pixel grid on the camera's
    /// screen plane and passes the sub-pixel remainder to the viewport material, which
    /// keeps every pixel locked regardless of camera movement or rotation.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class SubpixelOrthographicCamera : SubpixelCameraBase
    {
        private void UpdateOrthographicSize()
        {
            if (cameraConfig == null) return;
            
            cam.orthographicSize = renderResolution.y * 0.5f / cameraConfig.pixelsPerUnit;
            unitsPerPixel = 1.0f / cameraConfig.pixelsPerUnit;
        }

        protected override void OnResolutionUpdated()
        {
            UpdateOrthographicSize();
        }

        public override float3 WorldPositionToPixelGrid(float3 worldPosition)
        {
            // We want to snap the world position projected onto the camera's
            // local X and Y axes (i.e., the screen plane axes in world space).
            // This works regardless of rotation
            float3 pos = worldPosition;
            float3 right = transform.right;
            float3 up    = transform.up;
            
            float projX = math.dot(pos, right);
            float projY = math.dot(pos, up);
            
            float snappedX = math.floor(projX / unitsPerPixel) * unitsPerPixel;
            float snappedY = math.floor(projY / unitsPerPixel) * unitsPerPixel;
            
            float remX = projX - snappedX;
            float remY = projY - snappedY;
            
            return pos - right * remX - up * remY;
        }

        protected override void SnapToPixelGrid()
        {
            if (cameraConfig == null) return;
            
            // We want to snap the camera position projected onto the camera's
            // local X and Y axes (i.e., the screen plane axes in world space).
            // This works regardless of rotation
            float3 pos = referencePosition;
            float3 right = transform.right;
            float3 up    = transform.up;

            // Project position onto screen-plane axes
            float projX = math.dot(pos, right);
            float projY = math.dot(pos, up);

            // Snap to nearest pixel
            float snappedX = math.floor(projX / unitsPerPixel) * unitsPerPixel;
            float snappedY = math.floor(projY / unitsPerPixel) * unitsPerPixel;

            // Sub-pixel remainder (in world units)
            float remX = projX - snappedX;
            float remY = projY - snappedY;

            // Move camera by the negative remainder along those axes to snap it
            transform.parent.position = pos - right * remX - up * remY;

            // Convert remainder to UV offset for the blit correction
            // renderHeight = pixelsPerUnit * orthographicSize * 2 (by your formula)
            // so we just express the offset as a fraction of the render target size
            float renderWidth  = renderResolution.x;
            float renderHeight = renderResolution.y;

            // remX/remY are in world units; convert to pixels then to UV
            _SubPixelOffset.x = (remX * cameraConfig.pixelsPerUnit) / renderWidth;
            _SubPixelOffset.y = (remY * cameraConfig.pixelsPerUnit) / renderHeight;

            ApplyViewportMaterial();
            
            OnCameraSnapped?.Invoke(referencePosition);
        }
    }
}
