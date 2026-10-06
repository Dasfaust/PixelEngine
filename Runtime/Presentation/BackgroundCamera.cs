using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace PixelEngine.Presentation
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class BackgroundCamera : MonoBehaviour
    {
        // Ortho-only by design (see Docs/perspective-camera-spike.md, background decision).
        [SerializeField]
        private SubpixelCameraBase parentCamera;
        [SerializeField]
        private CameraConfig cameraConfig;
        [SerializeField]
        private RenderTexture renderTexture;
        [SerializeField]
        private RawImage outputImage;
        [SerializeField]
        private Material viewportMaterial;
        private Camera cam;
        private int2 screenSize = int2.zero;
        private int2 renderResolution = int2.zero;
        private float unitsPerPixel;
        private float4 _SubPixelOffset = float4.zero;
        
        public void Start()
        {
            cam = GetComponent<Camera>();
            parentCamera.OnCameraSnapped.AddListener(SnapToPixelGrid);
        }
        
        private void UpdateOrthographicSize()
        {
            if (cameraConfig == null) return;
            
            cam.orthographicSize = renderResolution.y * 0.5f / cameraConfig.backgroundPixelsPerUnit;
            unitsPerPixel = 1.0f / cameraConfig.backgroundPixelsPerUnit;
        }

        public void UpdateResolution()
        {
            if (cameraConfig == null) return;
            if (renderTexture == null) return;

            screenSize.x = Screen.width;
            screenSize.y = Screen.height;
            renderResolution.x = screenSize.x / cameraConfig.backgroundDownscaleRatio;
            renderResolution.y = screenSize.y / cameraConfig.backgroundDownscaleRatio;
            
            if (renderResolution.x % 2 != 0) renderResolution.x++;
            if (renderResolution.y % 2 != 0) renderResolution.y++;

            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture.width = renderResolution.x;
                renderTexture.height = renderResolution.y;
                renderTexture.Create();
                cam.targetTexture = renderTexture;
            }
            
            UpdateOrthographicSize();
        }
        
        private void SnapToPixelGrid(float3 referencePosition)
        {
            if (cameraConfig == null) return;

            transform.rotation = parentCamera.transform.rotation;
            
            // We want to snap the camera position projected onto the camera's
            // local X and Y axes (i.e., the screen plane axes in world space).
            // This works regardless of rotation
            Vector3 pos = referencePosition;
            Vector3 right = transform.right;
            Vector3 up    = transform.up;

            // Project position onto screen-plane axes
            float projX = Vector3.Dot(pos, right);
            float projY = Vector3.Dot(pos, up);

            // Snap to nearest pixel
            float snappedX = Mathf.Floor(projX / unitsPerPixel) * unitsPerPixel;
            float snappedY = Mathf.Floor(projY / unitsPerPixel) * unitsPerPixel;

            // Sub-pixel remainder (in world units)
            float remX = projX - snappedX;
            float remY = projY - snappedY;

            // Move camera by the negative remainder along those axes to snap it
            transform.position = pos - right * remX - up * remY;

            // Convert remainder to UV offset for the blit correction
            // renderHeight = pixelsPerUnit * orthographicSize * 2 (by your formula)
            // so we just express the offset as a fraction of the render target size
            float renderWidth  = renderResolution.x;
            float renderHeight = renderResolution.y;

            // remX/remY are in world units; convert to pixels then to UV
            _SubPixelOffset.x = (remX * cameraConfig.backgroundPixelsPerUnit) / renderWidth;
            _SubPixelOffset.y = (remY * cameraConfig.backgroundPixelsPerUnit) / renderHeight;

            if (viewportMaterial != null)
            {
                viewportMaterial.SetVector("_SubPixelOffset", _SubPixelOffset);
                viewportMaterial.SetFloat("_ZoomLevel", 1.0f + (parentCamera.GetUVZoomLevel() * cameraConfig.backgroundParallaxModifier));
            }
        }
        
        public void Update()
        {
            if (screenSize.x != Screen.width || screenSize.y != Screen.height)
            {
                UpdateResolution();
            }
        }
    }
}