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
        // The render texture is this many times the screen size: 1 = native, 2 = the
        // camera renders twice the screen so zoom 1 overlooks the world at one screen
        // pixel per two game pixels. Powers of two (1, 2, 4). SubpixelCameraBase
        // .SetRenderScale changes it at runtime; see README "Core formulas".
        public int renderScale = 1;
        public float zoomTime;
        public float followSpeed;
    }
}