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
            var target = ResolveViewportMaterial();
            if (target != null)
            {
                // The sub-pixel offset is not pushed here: this layer samples the whole background
                // texture, and the parent camera's snap correction belongs to the viewport window.
                target.SetFloat("_ZoomLevel", 1.0f + (_camera.GetUVZoomLevel() * 0.25f));
            }
        }

        /// <summary>
        /// The material this layer writes: a runtime copy of the authored viewport material (see
        /// <see cref="ViewportMaterialRuntime"/>). The authored asset is never written, so a play pass cannot
        /// leave a zoom answer in the file. Edit Mode answers null, so an edit-time pass writes nothing.
        /// </summary>
        private Material ResolveViewportMaterial()
        {
            if (viewportMaterial == null || !Application.isPlaying)
            {
                return null;
            }

            return ViewportMaterialRuntime.Acquire(viewportMaterial);
        }

        private void OnDestroy()
        {
            ViewportMaterialRuntime.Release(viewportMaterial);
        }
    }
}