using UnityEngine;

namespace PixelEngine.Presentation
{
    [ExecuteAlways]
    public class SnapToPixelGrid : MonoBehaviour
    {
        private SubpixelCameraBase _camera;

        public void LateUpdate()
        {
            if (_camera == null && Camera.main != null)
            {
                _camera = Camera.main.GetComponent<SubpixelCameraBase>();
                return;
            }

            if (_camera != null && transform.hasChanged)
            {
                transform.position = _camera.WorldPositionToPixelGrid(transform.position);
            }
        }
    }
}