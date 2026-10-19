using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Pure layout math for the pose-addressed atlas background, split out so a test
    /// can drive it without a scene.
    ///
    /// Atlas layout: a grid of <c>columns</c> by <c>rows</c> cells in UV space, origin
    /// bottom-left. Pose 0..columns-1 addresses row 0, column = pose, and a fractional
    /// pose slides the region by pose × cell width. Repeat wrap carries a slide through
    /// the atlas edge, so the answered x is unbounded: x and x+1 name the same pixels.
    /// The nadir pose (<c>columns</c> and above) addresses row 1, column 0. Row 1
    /// exists for the nadir image alone; its other columns carry no pose.
    ///
    /// The answered rect is the texture region a RawImage must show to display the
    /// pose at a given zoom. Zoom 1 shows the whole cell. Larger zoom shrinks the
    /// region toward the cell center, so the cell center stays on the view center.
    /// The region's pixel aspect equals the view aspect: the cell's spare axis crops,
    /// and the region stays inside its row band. The crop rule keeps a bake aspect and
    /// a window aspect from agreeing, and a window resize rescales UVs with no rebake.
    ///
    /// UV units are normalized by the texture size, so the view aspect and the UV
    /// aspect differ by the texture aspect: a rect is a square in UV units when the
    /// texture is 16:9, and it is a square in pixels when the texture is 1:1. The
    /// crop therefore divides the view aspect by the texture aspect
    /// (width over height), and the answered width and height stay UV units.
    /// </summary>
    public static class AtlasBackgroundLayout
    {
        public static Rect CellRect(float pose, int columns, int rows, float zoomFactor, float viewAspect, float textureAspect)
        {
            bool nadir = pose >= columns;
            float cellW = 1f / columns;
            float cellH = 1f / rows;
            float centerX = nadir ? 0.5f * cellW : (pose + 0.5f) * cellW;
            float centerY = nadir ? 1.5f * cellH : 0.5f * cellH;

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
    /// Pose-addressed atlas background: drives a RawImage from a baked atlas texture,
    /// answered by camera pose. The background answer for a snapped orthographic
    /// camera lives in texture space, because a rotation in geometry lands the image
    /// at sub-pixel positions and the stars shimmer.
    ///
    /// The caller names a pose (see <see cref="AtlasBackgroundLayout"/> for the atlas
    /// layout rule). Pose is a float: a fractional pose slides the answered region
    /// between cells, and Repeat wrap carries the slide through the atlas edge, so a
    /// rotation that names live yaw gives a slide that ends exactly when the
    /// rotation does. The game layer owns the pose model; this component takes a pose
    /// value, a grid shape, and an atlas texture, and no game concept crosses the seam.
    ///
    /// Nadir fade: an optional second RawImage answers the nadir cell at a
    /// caller-named alpha, so a pitch move cross-fades the yaw view into the top-down
    /// view with no rotation and no slide. The base layer keeps answering the yaw pose
    /// under it, and the caller owns when that pose freezes.
    ///
    /// Parallax: the region shrinks with the parent camera's UV zoom at the
    /// CameraConfig's backgroundParallaxModifier rate, so the background travels
    /// slower than the world at a modifier below 1. The modifier multiplies the
    /// travel above zoom 1, so zoom 1 shows the cell whole.
    ///
    /// Texture rules (importer-side, not enforced here): the atlas needs
    /// <c>filterMode = Point</c> for crisp texels, <c>mipMaps = off</c>, and
    /// <c>wrapMode = Repeat</c> for the slide rule that the yaw strip wraps through
    /// the atlas edge. The component clears each RawImage's custom material so the
    /// default UI material samples the atlas with uvRect addressing.
    /// </summary>
    [ExecuteAlways]
    public class AtlasBackground : MonoBehaviour
    {
        [SerializeField] private SubpixelCameraBase parentCamera;
        [SerializeField] private CameraConfig cameraConfig;
        [SerializeField] private RawImage outputImage;
        [Tooltip("Optional second RawImage that answers the nadir cell at the SetNadirFade alpha. Leave empty to answer the nadir pose on the base layer.")]
        [SerializeField] private RawImage nadirImage;
        [SerializeField] private Texture2D atlas;
        [Tooltip("Yaw cells in the atlas grid: the atlas width is this many cells. The nadir pose is index `columns`, in the second row.")]
        [SerializeField] private int columns = 8;
        [Tooltip("Cell rows in the atlas grid. Two rows: yaw cells in row 0, the nadir image in row 1.")]
        [SerializeField] private int rows = 2;

        private float pose;
        private float nadirFade;
        private Rect lastRect = Rect.zero;
        private bool missingReported;

        /// <summary>The pose value that reads the nadir cell (row 1, column 0).</summary>
        public int NadirPoseIndex => columns;

        /// <summary>The pose the component answers right now, slide fractions included.</summary>
        public float Pose => pose;

        /// <summary>The alpha the nadir layer answers with, 0..1.</summary>
        public float NadirFade => nadirFade;

        /// <summary>True when a nadir fade layer is assigned, so the caller cross-fades
        /// instead of naming the nadir pose.</summary>
        public bool HasNadirLayer => nadirImage != null;

        public void SetAtlas(Texture2D value)
        {
            atlas = value;
            SyncImages();
            Apply();
        }

        public void SetPose(float value)
        {
            pose = value;
            Apply();
        }

        public void SetNadirFade(float value)
        {
            nadirFade = math.clamp(value, 0f, 1f);
            Apply();
        }

        private void OnEnable()
        {
            if (parentCamera != null)
            {
                parentCamera.OnCameraSnapped.AddListener(OnCameraSnapped);
            }
            SyncImages();
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
            SyncImages();
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

        private void SyncImages()
        {
            // The default UI material honors uvRect addressing and the texture's own
            // filter and wrap modes. A custom material answers its own uniform
            // contract, and uvRect passes through as geometry UV only.
            if (outputImage != null)
            {
                outputImage.texture = atlas;
                outputImage.material = null;
            }
            if (nadirImage != null)
            {
                nadirImage.texture = atlas;
                nadirImage.material = null;
            }
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

            if (nadirImage != null)
            {
                Rect nadirView = nadirImage.rectTransform.rect;
                float nadirAspect = nadirView.height > 0f ? nadirView.width / nadirView.height : 1f;
                nadirImage.uvRect = AtlasBackgroundLayout.CellRect(columns, columns, rows, zoom, nadirAspect, textureAspect);
                nadirImage.color = new Color(1f, 1f, 1f, nadirFade);
            }
        }
    }
}
