using UnityEngine;

namespace PixelEngine.Presentation
{
    public class OrthoBackground : MonoBehaviour
    {
        [SerializeField]
        private SubpixelCameraBase _camera;
        [SerializeField]
        private Material viewportMaterial;

        public void LateUpdate()
        {
            if (viewportMaterial != null)
            {
                //viewportMaterial.SetVector("_SubPixelOffset", _camera.GetSubpixelOffset());
                viewportMaterial.SetFloat("_ZoomLevel", 1.0f + (_camera.GetUVZoomLevel() * 0.25f));
            }
        }
    }
}