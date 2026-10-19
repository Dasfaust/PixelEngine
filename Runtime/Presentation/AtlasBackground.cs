using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Pure layout math for the pose-addressed atlas background, split out so a test
    /// can drive it without a scene.
    ///
    /// Atlas layout: a grid of <c>columns</c> by <c>rows</c> cells in UV space, origin
    /// bottom-left. Pose 0..columns-1 addresses row 0, column = pose. The nadir pose
    /// (index <c>columns</c>) addresses row 1, column 0. Row 1 exists for the nadir
    /// image alone; its other columns carry no pose.
    ///
    /// The answered rect is the texture region a RawImage must show to display the
    /// pose at a given zoom. Zoom 1 shows the whole cell. Larger zoom shrinks the
    /// region toward the cell center, so the cell center stays on the view center.
    /// The region's pixel aspect equals the view aspect: the cell's spare axis crops,
    /// and the region never leaves the cell. The crop rule keeps a bake aspect and a
    /// window aspect from agreeing, and a window resize rescales UVs with no rebake.
    ///
    /// UV units are normalized by the texture size, so the view aspect and the UV
    /// aspect differ by the texture aspect: a rect is a square in UV units when the
    /// texture is 16:9, and it is a square in pixels when the texture is 1:1. The
    /// crop therefore divides the view aspect by the texture aspect
    /// (width over height), and the answered width and height stay UV units.
    /// </summary>
    public static class AtlasBackgroundLayout
    {
        public static Rect CellRect(int pose, int columns, int rows, float zoomFactor, float viewAspect, float textureAspect)
        {
            int nadirPose = columns;
            int cell = pose == nadirPose ? 0 : pose;
            int row = pose == nadirPose ? 1 : 0;

            float cellW = 1f / columns;
            float cellH = 1f / rows;
            float centerX = (cell + 0.5f) * cellW;
            float centerY = (row + 0.5f) * cellH;

            float zoom = math.max(zoomFactor, 1f);
            float uvAspect = math.max(viewAspect / math.max(textureAspect, 0.0001f), 0.0001f);
            float zoomW = cellW / zoom;
            float zoomH = cellH / zoom;

            // Crop the zoom rect to the view aspect in UV units, inside the zoom
            // rect. The min keeps both axes inside: width caps at the zoom width,
            // and the matching height caps at the zoom height.
            float w = math.min(zoomW, zoomH * uvAspect);
            float h = w / uvAspect;

            return new Rect(centerX - w * 0.5f, centerY - h * 0.5f, w, h);
        }
    }

    /// <summary>
    /// Pose-addressed atlas background: drives a RawImage from one cell of a baked
    /// atlas texture, chosen by camera pose. The background answer for a snapped
    /// orthographic camera lives in texture space, because a rotation in geometry
    /// lands the image at sub-pixel positions and the stars shimmer.
    ///
    /// The caller names a pose index (see <see cref="AtlasBackgroundLayout"/> for the
    /// atlas layout rule). The game layer owns the pose model; this component takes a
    /// cell index and an atlas texture, and no game concept crosses the seam.
    ///
    /// Parallax: the region shrinks with the parent camera's UV zoom at the
    /// CameraConfig's backgroundParallaxModifier rate, so the background travels
    /// slower than the world at a modifier below 1. The modifier multiplies the
    /// travel above zoom 1, so zoom 1 shows the cell whole.
    ///
    /// Texture rules (importer-side, not enforced here): the atlas needs
    /// <c>filterMode = Point</c> for crisp texels, <c>mipMaps = off</c>, and
    /// <c>wrapMode = Repeat</c> for the slide rule that the yaw strip wraps through
    /// the atlas edge. The component clears the RawImage's custom material so the
    /// default UI material samples the atlas with uvRect addressing.
    /// </summary>
    [ExecuteAlways]
    public class AtlasBackground : MonoBehaviour
    {
        [SerializeField] private SubpixelCameraBase parentCamera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private RawImage outputImage;
        [SerializeField] private Texture2D atlas;
        [Tooltip("Yaw cells in the atlas grid: the atlas width is this many cells. The nadir pose is index `columns`, in the second row.")]
        [SerializeField] private int columns = 8;
        [Tooltip("Cell rows in the atlas grid. Two rows: yaw cells in row 0, the nadir image in row 1.")]
        [SerializeField] private int rows = 2;

        private int pose;
        private Rect lastRect = Rect.zero;
        private bool missingReported;

        /// <summary>The pose index that reads the nadir cell (row 1, column 0).</summary>
        public int NadirPoseIndex => columns;

        /// <summary>The pose index the component answers right now.</summary>
        public int Pose => pose;

        public void SetAtlas(Texture2D value)
        {
            atlas = value;
            SyncOutputImage();
            Apply();
        }

        public void SetPose(int value)
        {
            pose = value;
            Apply();
        }

        private void OnEnable()
        {
            if (parentCamera != null)
            {
                parentCamera.OnCameraSnapped.AddListener(OnCameraSnapped);
            }
            SyncOutputImage();
            Apply();
        }

        private void OnDisable()
        {
            if (parentCamera != null)
            {
                parentCamera.OnCameraSnapped.RemoveListener(OnCameraSnapped);
            }
        }

        private void Start()
        {
            SyncOutputImage();
            Apply();
        }

        private void Update()
        {
            // The canvas rescales the RawImage rect on a window resize, and the view
            // aspect rides that rect. Poll the rect the way the sibling components poll
            // Screen.width/height, so a resize re-answers with no rebake.
            if (outputImage == null)
            {
                return;
            }
            Rect rect = outputImage.rectTransform.rect;
            if (rect.width != lastRect.width || rect.height != lastRect.height)
            {
                Apply();
            }
        }

        private void OnCameraSnapped(float3 referencePosition)
        {
            Apply();
        }

        private void SyncOutputImage()
        {
            if (outputImage == null)
            {
                return;
            }
            outputImage.texture = atlas;
            // The default UI material honors uvRect addressing and the texture's own
            // filter and wrap modes. A custom material answers its own uniform
            // contract, and uvRect passes through as geometry UV only.
            outputImage.material = null;
        }

        private void Apply()
        {
            if (outputImage == null || atlas == null)
            {
                if (!missingReported)
                {
                    missingReported = true;
                    Debug.LogError("AtlasBackground: outputImage or atlas is missing, so the background answers nothing.", this);
                }
                return;
            }
            missingReported = false;

            Rect view = outputImage.rectTransform.rect;
            float viewAspect = view.height > 0f ? view.width / view.height : 1f;
            float textureAspect = atlas.height > 0 ? atlas.width / (float)atlas.height : 1f;

            float zoom = 1f;
            if (parentCamera != null && cameraConfig != null)
            {
                zoom = 1f + (parentCamera.GetUVZoomLevel() - 1f) * cameraConfig.backgroundParallaxModifier;
            }

            lastRect = view;
            outputImage.uvRect = AtlasBackgroundLayout.CellRect(pose, columns, rows, zoom, viewAspect, textureAspect);
        }
    }
}
