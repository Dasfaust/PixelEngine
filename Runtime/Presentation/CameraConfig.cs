using UnityEngine;

namespace PixelEngine.Presentation
{
    [CreateAssetMenu(menuName = "PixelEngine/Camera Config")]
    public class CameraConfig : ScriptableObject
    {
        public float pixelsPerUnit;
        public float backgroundPixelsPerUnit;
        public float backgroundParallaxModifier;
        public int downscaleRatio;
        public int backgroundDownscaleRatio;
        public float zoomTime;
        public float followSpeed;

        /// <summary>
        /// Render camera orientation lock, authored per config: pitch stays inside
        /// [minPitchDegrees, maxPitchDegrees], yaw stays on multiples of yawStepDegrees.
        /// The SubpixelCameraBase enforces both every frame.
        /// </summary>
        public float minPitchDegrees = 30f;
        public float maxPitchDegrees = 90f;
        public float yawStepDegrees = 45f;
    }
}