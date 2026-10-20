using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Flow-field math for the warp streak layer, split out so a test drives it
    /// without a scene.
    ///
    /// The camera translates along a fixed travel direction while its view
    /// rotates. The streaks answer one vanishing point: the screen projection of
    /// the travel direction under an authored focal length. The point carries
    /// every pose case in one field:
    /// - Gaze along travel: the point sits at the view center; rays fan from it
    ///   and streaks diverge.
    /// - Gaze rotated by yaw: the point slides off center by the tangent of the
    ///   gaze-travel angle; rays fan into a cone sweeping from side to side.
    /// - Gaze perpendicular to travel: the axial component goes to zero, the
    ///   clamp sends the point far off screen, and the rays flatten into
    ///   parallel streaks moving across the view.
    /// - Gaze against travel: the axial sign flips; streaks converge into the
    ///   point.
    ///
    /// The projection is authored perspective, not measured perspective: the
    /// orthographic game camera has no parallax under translation, so the
    /// convergence is a look, tuned by the focal length. The flow direction per
    /// pose follows the physical projection; the cone shape and the
    /// converge/diverge signs are the authored half.
    ///
    /// The view direction arrives in camera view space: x is screen right, y is
    /// screen up, z is the negative gaze direction (Unity view convention), so
    /// <c>-viewDir.z</c> is the axial component, positive when the travel
    /// recedes from the viewer.
    /// </summary>
    public static class WarpFlowMath
    {
        /// <summary>The screen-space vanishing point of the travel direction,
        /// in UV units of the streak layer. The axial floor is signed, so the
        /// point lands on the travel side of center while the view recedes
        /// (diverge) and crosses to the opposite side once the gaze turns
        /// against travel past the floor (converge): the cone slides off
        /// center, sits on the travel side at the far side, and returns to
        /// center as the gaze turns against travel. Converging into that
        /// point is the flow a receding-to-approaching rotation asks
        /// physically. Inside the floor band the answer is the band edge, so
        /// a perpendicular gaze lands the point on the travel side by rule
        /// and not by the sign of a float residue.</summary>
        public static float2 VanishingPoint(float3 viewDir, float focal, float minAxial)
        {
            float axial = -viewDir.z;
            float band = math.max(minAxial, 0.0001f);
            float denominator = axial > band ? axial : (axial < -band ? math.min(axial, -band) : band);
            return new float2(0.5f, 0.5f) + focal * new float2(viewDir.x, viewDir.y) / denominator;
        }

        /// <summary>+1 when the travel recedes from the viewer (streaks diverge
        /// from the vanishing point), -1 when it approaches past the floor band
        /// (streaks converge into it). Inside the band the answer is +1: a side
        /// view streams away from the travel-side point, which is the lateral
        /// flow, and the answer must not ride the sign of a float residue — a
        /// live camera matrix lands the axial term near zero on noise.</summary>
        public static float AxialSign(float3 viewDir, float minAxial)
        {
            return -viewDir.z > -math.max(minAxial, 0.0001f) ? 1f : -1f;
        }

        /// <summary>The ray count that answers the target ray density across
        /// the view. Angles compress when the vanishing point sits far off
        /// screen, so the count scales with the point distance; a near point
        /// keeps the target density over the full fan.</summary>
        public static float RayCount(float targetRays, float2 vanishPoint)
        {
            float dist = math.length(vanishPoint - new float2(0.5f, 0.5f));
            return targetRays * 2f * math.PI * math.max(dist, 1f / (2f * math.PI));
        }
    }

    /// <summary>
    /// Warp streak layer: drives a RawImage with the PixelEngine/Warp Flow
    /// shader. The layer sits behind the viewport blit and in front of the
    /// background, so the world geometry the camera renders streams in front
    /// of the streaks and the sky stays behind them. The camera stays free:
    /// warp is simulated travel time, and nothing in this component touches
    /// the camera pose.
    ///
    /// The caller names the travel direction in view space and an intensity;
    /// the rig answers the vanishing point, the axial sign, the ray count,
    /// and the render-texture grid cell. The look parameters — streak
    /// density, flow rate, bands, color — are authored on the material, and
    /// the per-pose values write onto a play-mode clone of it, so the
    /// authored asset never carries the runtime answer. Unity 6.6's
    /// CanvasRenderer has no MaterialPropertyBlock path, so the clone is the
    /// per-layer write surface. No game concept crosses the seam: "game
    /// north" is the caller's, not this component's.
    ///
    /// Grid: the streaks quantize to the viewport render-texture cells, the
    /// same on-screen pixel size as the world, so the effect rides the pixel
    /// grid it shares. The cell comes from the parent camera's target
    /// texture; a camera with no texture yet answers a placeholder cell and
    /// re-answers on the next push.
    /// </summary>
    public class WarpFlow : MonoBehaviour
    {
        [SerializeField] private RawImage outputImage;
        [SerializeField] private SubpixelCameraBase parentCamera;
        [Tooltip("Authored focal length: the cone strength. The vanishing point sits focal x tan(gaze-travel angle) off center.")]
        [SerializeField, Min(0.01f)] private float focal = 0.3f;
        [Tooltip("The axial floor: the smallest receding component before the point is clamped far. The side views land on this floor as parallel streaks.")]
        [SerializeField, Range(0.01f, 1f)] private float minAxial = 0.15f;
        [Tooltip("Streak rays across the view. The ray count scales with the vanishing point distance to hold this density.")]
        [SerializeField, Min(2)] private int targetRays = 16;

        private Material runtimeMaterial;
        private bool missingReported;

        private void Awake()
        {
            if (outputImage != null && outputImage.material != null)
            {
                runtimeMaterial = new Material(outputImage.material);
                outputImage.material = runtimeMaterial;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
        }

        public void SetFlow(float3 viewTravelDir, float intensity)
        {
            if (outputImage == null || runtimeMaterial == null)
            {
                if (!missingReported)
                {
                    missingReported = true;
                    Debug.LogError("WarpFlow: outputImage has no material to drive, so the warp layer answers nothing.", this);
                }
                return;
            }
            missingReported = false;

            float2 vanishPoint = WarpFlowMath.VanishingPoint(viewTravelDir, focal, minAxial);
            float axialSign = WarpFlowMath.AxialSign(viewTravelDir, minAxial);
            float rays = WarpFlowMath.RayCount(targetRays, vanishPoint);

            var texture = parentCamera != null && parentCamera.GetCamera() != null
                ? parentCamera.GetCamera().targetTexture
                : null;
            float2 gridCell = texture != null && texture.width > 0 && texture.height > 0
                ? new float2(1f / texture.width, 1f / texture.height)
                : new float2(0.005f, 0.005f);

            runtimeMaterial.SetVector("_VanishPoint", new Vector4(vanishPoint.x, vanishPoint.y, 0f, 0f));
            runtimeMaterial.SetFloat("_AxialSign", axialSign);
            runtimeMaterial.SetFloat("_RayCount", rays);
            runtimeMaterial.SetFloat("_Intensity", math.clamp(intensity, 0f, 1f));
            runtimeMaterial.SetVector("_GridCell", new Vector4(gridCell.x, gridCell.y, 0f, 0f));
        }
    }
}
