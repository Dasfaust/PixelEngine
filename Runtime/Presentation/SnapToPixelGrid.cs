using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Keeps an object stable on the camera's pixel grid.
    ///
    /// The snap quantizes from an anchor position, not from the transform's current
    /// position. Quantizing the current position feeds the object its own rounded
    /// output: each frame the floor pushes it by up to one pixel, the push is not
    /// invertible, and a camera rotation that ends where it started leaves the object
    /// at a drifted position. An anchor keeps the snap stateless, so a rotation that
    /// returns its axes returns the object's offset too. This is the same pattern the
    /// camera uses with its reference position.
    ///
    /// A moving object moves through SetPosition, which moves the anchor. A direct
    /// write to transform.position is overwritten by the next snap.
    /// </summary>
    [ExecuteAlways]
    public class SnapToPixelGrid : MonoBehaviour
    {
        private SubpixelCameraBase _camera;
        private Vector3 _anchor;

        private void OnEnable()
        {
            _anchor = transform.position;
        }

        public void LateUpdate()
        {
            if (_camera == null && Camera.main != null)
            {
                _camera = Camera.main.GetComponent<SubpixelCameraBase>();
                return;
            }

            if (_camera != null && transform.hasChanged)
            {
                transform.position = _camera.WorldPositionToPixelGrid(_anchor);
            }
        }

        /// <summary>
        /// Moves the object and its anchor together. The snap keeps quantizing from the
        /// new anchor, so movement survives rotation.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            _anchor = position;
            transform.position = _camera != null ? _camera.WorldPositionToPixelGrid(position) : position;
        }
    }
}
