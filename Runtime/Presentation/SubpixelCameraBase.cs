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
        /// The decoupling seam: companion components and game code subscribe to this instead
        /// of the camera referencing them. Invoked whenever the camera's stable reference
        /// position is updated (orthographic: after the pivot snap; perspective: every frame,
        /// un-snapped until probe-origin snapping lands — see Docs/perspective-camera-spike.md).
        /// </summary>
        public UnityEvent<float3> OnCameraSnapped;

        public void Start()
        {
            cam = GetComponent<Camera>();
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

        // --- Orientation lock ---------------------------------------------------
        // README rule: "The render camera's rotation is also locked to 30-90 degrees on
        // pitch, and 45 degree increments on yaw." The package enforces it. A parented
        // camera, an animation clip, or a game-side tween can write any rotation for one
        // frame; the lock below quantizes it back inside LateUpdate, the same per-frame
        // slot as the pivot snap, so the screen never renders an unlocked angle.
        // Roll is locked to 0 with them: the pitch/yaw rule describes an upright camera,
        // and a rolled camera breaks the screen-plane axis projection the pivot snap uses.

        private const float DefaultMinPitch = 30f;
        private const float DefaultMaxPitch = 90f;
        private const float DefaultYawStep = 45f;

        public float GetMinPitchDegrees()
        {
            return cameraConfig != null ? cameraConfig.minPitchDegrees : DefaultMinPitch;
        }

        public float GetMaxPitchDegrees()
        {
            return cameraConfig != null ? cameraConfig.maxPitchDegrees : DefaultMaxPitch;
        }

        public float GetYawStepDegrees()
        {
            return cameraConfig != null ? cameraConfig.yawStepDegrees : DefaultYawStep;
        }

        /// <summary>Pitch inside the authored range. Values outside it land on the nearer end.</summary>
        public float ClampPitch(float pitchDegrees)
        {
            return math.clamp(pitchDegrees, GetMinPitchDegrees(), GetMaxPitchDegrees());
        }

        /// <summary>Yaw quantized onto the authored step grid.</summary>
        public float SnapYaw(float yawDegrees)
        {
            float step = GetYawStepDegrees();
            if (step <= 0f)
            {
                return yawDegrees;
            }
            return math.round(yawDegrees / step) * step;
        }

        /// <summary>The camera's live pitch in degrees, normalized to (-180, 180].</summary>
        public float GetPitch()
        {
            return NormalizeAngle(transform.eulerAngles.x);
        }

        /// <summary>The camera's live yaw in degrees, normalized to (-180, 180].</summary>
        public float GetYaw()
        {
            return NormalizeAngle(transform.eulerAngles.y);
        }

        /// <summary>Set pitch to a clamped value; yaw and roll keep their locked form.</summary>
        public void SetPitch(float pitchDegrees)
        {
            SetOrientation(ClampPitch(pitchDegrees), GetYaw());
        }

        /// <summary>Set yaw to a snapped value; pitch and roll keep their locked form.</summary>
        public void SetYaw(float yawDegrees)
        {
            SetOrientation(GetPitch(), SnapYaw(yawDegrees));
        }

        /// <summary>Turn by a whole count of yaw steps (Q/E input calls this).</summary>
        public void StepYaw(int steps)
        {
            SetYaw(GetYaw() + steps * GetYawStepDegrees());
        }

        private static float NormalizeAngle(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees <= -180f) degrees += 360f;
            return degrees;
        }

        private void SetOrientation(float pitch, float yaw)
        {
            yaw = SnapYaw(NormalizeAngle(yaw));
            transform.rotation = Quaternion.Euler(ClampPitch(pitch), NormalizeAngle(yaw), 0f);
        }

        /// <summary>
        /// Quantize the live rotation back inside the lock. Runs every frame before the
        /// pivot snap, so the pivot snap projects onto the locked axes, never onto an
        /// angle the lock is about to discard.
        /// </summary>
        protected void ApplyOrientationLock()
        {
            float pitch = NormalizeAngle(transform.eulerAngles.x);
            float yaw = NormalizeAngle(transform.eulerAngles.y);
            float lockedPitch = ClampPitch(pitch);
            float lockedYaw = SnapYaw(yaw);
            if (math.abs(pitch - lockedPitch) > 0.01f ||
                math.abs(yaw - lockedYaw) > 0.01f)
            {
                transform.rotation = Quaternion.Euler(lockedPitch, NormalizeAngle(lockedYaw), 0f);
            }
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
            
            var zoomPercentage = 1.0f / _ZoomLevel;
            uvRect.width = zoomPercentage;
            uvRect.height = zoomPercentage;
            uvRect.x = (1.0f - zoomPercentage) * 0.5f;
            uvRect.y = (1.0f - zoomPercentage) * 0.5f;
            uvRect.x += _SubPixelOffset.x;
            uvRect.y += _SubPixelOffset.y;
            
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
            renderResolution.x = screenSize.x / divisor.x;
            renderResolution.y = screenSize.y / divisor.y;

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
        /// </summary>
        protected void ApplyViewportMaterial()
        {
            if (viewportMaterial != null)
            {
                viewportMaterial.SetVector("_SubPixelOffset", _SubPixelOffset);
                viewportMaterial.SetFloat("_ZoomLevel", _ZoomLevel);
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
            ApplyOrientationLock();
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
