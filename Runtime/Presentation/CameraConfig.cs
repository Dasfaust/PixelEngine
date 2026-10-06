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
    }
}