using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Shared base for sub-pixel pixel-art cameras (orthographic and perspective).
    /// Owns the low-resolution RenderTexture + RawImage blit plumbing, zoom state, pivot
    /// position, and the viewport material uniform contract (_SubPixelOffset / _ZoomLevel).
    /// Mode-specific behavior (projection setup, grid snapping) is implemented by the
    /// subclasses. Game code and companion components should target this type so they stay
    /// camera-mode agnostic.
    /// </summary>
    [ExecuteAlways]
    public abstract class SubpixelCameraBase : MonoBehaviour
    {
        [SerializeField] protected CameraConfig cameraConfig;
        [SerializeField] protected RenderTexture renderTexture;
        [SerializeField] protected RawImage outputImage;
        [SerializeField] protected Material viewportMaterial;
        protected Camera cam;
        protected float unitsPerPixel;
        protected int2 screenSize = int2.zero;
        protected int2 renderResolution = int2.zero;
        protected float _ZoomLevel = 1f;
        protected float4 _SubPixelOffset = float4.zero;
        protected float3 referencePosition = float3.zero;

        /// <summary>
        /// The edge margin the render texture carries on every edge, in texels.
        /// The render texture is the screen-ratio window plus 2x this many texels,
        /// and the viewport material samples the inner window (the shader's
        /// _EdgeMargin must equal this number; ApplyViewportMaterial pushes it).
        /// The subpixel offset slides the window inside the margin, so it never
        /// samples past a texture edge at any zoom — at 1:1 the window fills the
        /// screen-ratio rect and the margin absorbs the whole offset.
        /// Two texels cover the maximum offset (one texel) with bilinear headroom.
        /// The picking seam mirrors this number.
        /// </summary>
        public const float EdgeMarginTexels = 2f;

        // Runtime render scale, seeded from the config at Start and changed by
        // SetRenderScale. The texture is scale x (screen / divisor) plus the edge
        // margin, so scale 2 doubles the world the camera covers at the same zoom
        // levels: one screen pixel per two game pixels at zoom 1. The window math,
        // the snap grid, the subpixel offset conversion, the margin, and the
        // picking seam all read the texture size, so they stay valid at any scale.
        protected int renderScale = 1;

        /// <summary>
        /// The decoupling seam: companion components and game code subscribe to this instead
        /// of the camera referencing them. Invoked whenever the camera's stable reference
        /// position is updated (orthographic: after the pivot snap; perspective: every frame,
        /// un-snapped until probe-origin snapping lands — see Docs/perspective-camera-spike.md).
        /// </summary>
        public UnityEvent<float3> OnCameraSnapped;

        public void Start()
        {
            cam = GetComponent<Camera>();
            renderScale = NormalizeRenderScale(cameraConfig != null ? cameraConfig.renderScale : 1);
        }

        /// <summary>
        /// 1, 2, 4; anything else reads as 1.
        /// </summary>
        private static int NormalizeRenderScale(int scale)
        {
            if (scale <= 1) return 1;
            if (scale <= 2) return 2;
            return 4;
        }

        /// <summary>
        /// The current render scale: the render texture is this many times the
        /// screen size, so zoom 1 shows 1/screen scale game pixels per screen pixel.
        /// </summary>
        public int GetRenderScale()
        {
            return renderScale;
        }

        /// <summary>
        /// Change the render scale at runtime (the overview camera mode calls this):
        /// 1 = native, 2 = one screen pixel per two game pixels at zoom 1, 4 = a
        /// quarter. The render texture, the camera coverage (the orthographic size
        /// formula reads the grown texture), the subpixel offset conversion, the
        /// edge margin, and the picking seam all follow in one UpdateResolution,
        /// so no other seam needs to know the mode exists. The texture
        /// reallocation is a one-time cost on the switch.
        /// </summary>
        public void SetRenderScale(int scale)
        {
            int normalized = NormalizeRenderScale(scale);
            if (normalized == renderScale)
            {
                return;
            }

            renderScale = normalized;
            UpdateResolution();
        }

        public float GetUVZoomLevel()
        {
            return _ZoomLevel;
        }

        public float4 GetSubpixelOffset()
        {
            return _SubPixelOffset;
        }

        public float ClampUVZoomLevel(float level)
        {
            return math.clamp(level, 1, cameraConfig.downscaleRatio);
        }

        public void SetUVZoomLevel(float level)
        {
            if (cameraConfig != null)
            {
                _ZoomLevel = ClampUVZoomLevel(level);
            }
            else
            {
                _ZoomLevel = math.max(level, 1);
            }
        }

        public CameraConfig GetCameraConfig()
        {
            return cameraConfig;
        }

        public float GetUnitsPerPixel()
        {
            return unitsPerPixel;
        }

        public Camera GetCamera()
        {
            return cam;
        }

        public RawImage GetOutputImage()
        {
            return outputImage;
        }

        public float3 GetPivotPosition()
        {
            return referencePosition;
        }

        public void SetPivotPosition(float3 position)
        {
            referencePosition = position;
        }

        public float2 GetAdjustedViewportPosition(float2 position)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                outputImage.rectTransform,
                position,
                null,
                out localPoint
            );
            
            Rect rect = outputImage.rectTransform.rect;
            float2 rawUV = new float2(
                (localPoint.x - rect.x) / rect.width,
                (localPoint.y - rect.y) / rect.height
            );
            
            Rect uvRect = outputImage.uvRect;
            
            // Mirror of the viewport shader window: window size = (1 - 2*margin/texels) / Z,
            // centered, plus the subpixel offset. The shader reads
            // adjustedUv = (uv - 0.5)/Z * windowSize + 0.5 + offset, so a screen
            // position maps through this rect onto exactly the texels the shader samples.
            var zoomPercentage = 1.0f / _ZoomLevel;
            var windowSizeX = 1.0f - 2.0f * EdgeMarginTexels / renderResolution.x;
            var windowSizeY = 1.0f - 2.0f * EdgeMarginTexels / renderResolution.y;
            uvRect.width = windowSizeX * zoomPercentage;
            uvRect.height = windowSizeY * zoomPercentage;
            uvRect.x = 0.5f - uvRect.width * 0.5f + _SubPixelOffset.x;
            uvRect.y = 0.5f - uvRect.height * 0.5f + _SubPixelOffset.y;
            
            float2 correctedUV = new float2(
                uvRect.x + rawUV.x * uvRect.width,
                uvRect.y + rawUV.y * uvRect.height
            );

            return correctedUV;
        }

        public float2 WorldToViewportPosition(float3 worldPosition)
        {
            return GetAdjustedViewportPosition(((float3)cam.WorldToViewportPoint(worldPosition)).xy);
        }

        /// <summary>
        /// Camera-mode-agnostic picking primitive: a screen position (in pixels) to a world
        /// ray, corrected for viewport zoom and sub-pixel offset. Valid for both projections.
        /// Compose with WorldPositionToPixelGrid (or game-side grid logic) for a stable hit point.
        /// </summary>
        public Ray GetAdjustedViewportRay(float2 screenPosition)
        {
            float2 uv = GetAdjustedViewportPosition(screenPosition);
            return cam.ViewportPointToRay(new Vector3(uv.x, uv.y, 0f));
        }

        /// <summary>
        /// Snap a world position to this camera mode's stable pixel grid.
        /// Orthographic: snaps onto the camera's screen plane. Perspective: axis-aligned
        /// voxel grid at unitsPerPixel. Game code should call this (or the base type) only,
        /// never a concrete camera subclass.
        /// </summary>
        public abstract float3 WorldPositionToPixelGrid(float3 worldPosition);

        public void UpdateResolution()
        {
            if (cameraConfig == null) return;
            if (renderTexture == null) return;

            screenSize.x = Screen.width;
            screenSize.y = Screen.height;
            int2 divisor = GetResolutionDivisor();
            renderResolution.x = screenSize.x / divisor.x * renderScale;
            renderResolution.y = screenSize.y / divisor.y * renderScale;

            if (renderResolution.x % 2 != 0) renderResolution.x++;
            if (renderResolution.y % 2 != 0) renderResolution.y++;

            // The margin: the render texture carries EdgeMarginTexels per edge
            // beyond the screen-ratio window, so the viewport window samples real
            // texels at every zoom. The camera formulas (orthographic size) read
            // this grown size, so the camera renders exactly the grown region and
            // the window stays the clean ratio inside it.
            renderResolution.x += (int)EdgeMarginTexels * 2;
            renderResolution.y += (int)EdgeMarginTexels * 2;

            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture.width = renderResolution.x;
                renderTexture.height = renderResolution.y;
                renderTexture.Create();
                cam.targetTexture = renderTexture;
            }

            OnResolutionUpdated();
        }

        /// <summary>
        /// Render-resolution divisor relative to the screen (1,1 renders at full screen size).
        /// </summary>
        protected virtual int2 GetResolutionDivisor()
        {
            return new int2(1, 1);
        }

        /// <summary>
        /// Mode-specific setup after the render resolution changes (e.g. orthographic size).
        /// </summary>
        protected virtual void OnResolutionUpdated()
        {
        }

        /// <summary>
        /// Push the sub-pixel uniform contract to the viewport material.
        /// Keep in sync with PixelEngineShader (see package README "Shader contract").
        /// The margin the shader window needs is pushed here, so the material can
        /// never disagree with the render texture the camera built.
        /// </summary>
        protected void ApplyViewportMaterial()
        {
            if (viewportMaterial != null)
            {
                viewportMaterial.SetVector("_SubPixelOffset", _SubPixelOffset);
                viewportMaterial.SetFloat("_ZoomLevel", _ZoomLevel);
                viewportMaterial.SetFloat("_EdgeMargin", EdgeMarginTexels);
            }
        }

        public void Update()
        {
            if (screenSize.x != Screen.width || screenSize.y != Screen.height)
            {
                UpdateResolution();
            }
        }

        public void LateUpdate()
        {
            SnapToPixelGrid();
        }

        /// <summary>
        /// Mode-specific stability pass, run every frame after all transforms have updated.
        /// </summary>
        protected virtual void SnapToPixelGrid()
        {
        }
    }
}
