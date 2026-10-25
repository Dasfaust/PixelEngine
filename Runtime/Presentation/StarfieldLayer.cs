using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Motion math for the dynamic star layer, split out so a test drives it without a scene.
    ///
    /// The layer draws its stars from a screen-space lattice and samples no texture, so the rig's
    /// answers are the picture. Four numbers place the field, and each carries one rule:
    ///
    /// <list type="bullet">
    /// <item><description><b>Slide.</b> The yaw pose arrives in yaw cells, the same value
    /// <see cref="AtlasBackground.SetPose"/> takes. The baked strip carries one cell per yaw step, and
    /// a cell is one screen width of sky, so a yaw step moves the view across one screen width of
    /// sky. The star layer covers one screen width, so the same step carries its pattern by the
    /// pattern's whole cell count. The parallax factor scales that travel, so a factor below 1 leaves
    /// the field moving less than the baked sky over the same yaw move, which is the parallax
    /// read.</description></item>
    /// <item><description><b>Pattern scale.</b> The camera's UV zoom scales the view. The layer
    /// scales its pattern by the travel above zoom 1 at its own rate, so the field spreads at a rate
    /// a reader tunes against the baked layer. The lattice cell count follows the scale, so the
    /// pattern width holds the screen and the density follows the spread.</description></item>
    /// <item><description><b>Roll.</b> At nadir, yaw rotates the camera up around the view axis, so
    /// world content counter-rotates on screen about the focus. The layer rotates its pattern about
    /// the layer center by the same angle, in the direction the caller's sign names. The plan's art
    /// rule needs this one: a RawImage cannot rotate its sampled region, so the layer that rolls is
    /// the layer that carries crisp stars.</description></item>
    /// <item><description><b>Opacity.</b> The layer fades in with the nadir cross-fade, from its
    /// authored base alpha at no fade to full alpha at a full fade, so the top-down view shows the
    /// crisp stars on the layer that rolls.</description></item>
    /// </list>
    ///
    /// A star dot's radius arrives in lattice cells: the authored pixel size divides by the pixel
    /// width of a cell at the live scale. The dot zoom factor states whether the dot keeps its pixel
    /// size as the field spreads (0) or grows with the field (1).
    ///
    /// A star's identity — presence, position inside its cell, size, brightness, color, and twinkle
    /// phase — comes from the cell hash in the shader, so this class answers the grid and never a
    /// star.
    /// </summary>
    public static class StarfieldLayerMath
    {
        /// <summary>The slide in star cells for a yaw pose. One yaw cell carries the field by its
        /// whole width, because the baked view moves one screen width per yaw cell and the field
        /// covers one screen width. The parallax factor scales the travel, and the pose's sign
        /// carries through, so a yaw move the other way carries the field the other way.</summary>
        public static float SlideCells(float pose, float cellsWide, float parallaxFactor)
        {
            return pose * cellsWide * parallaxFactor;
        }

        /// <summary>The pattern scale for a camera UV zoom level. The travel above zoom 1 scales at
        /// the layer's own rate, so zoom 1 answers the authored field for every rate. A level below
        /// 1 answers the same, because the camera's zoom floor is 1 and a shrunk field reads as a
        /// denser sky.</summary>
        public static float PatternScale(float uvZoom, float zoomParallaxFactor)
        {
            return 1f + (math.max(uvZoom, 1f) - 1f) * zoomParallaxFactor;
        }

        /// <summary>The authored pattern's cell count: the authored width across, and the height that
        /// follows the layer aspect. The count carries no zoom, and that is the rule it exists for. The
        /// pattern repeats at its own width, so the wrap period and the pattern center hold at every
        /// zoom, and the ratio between the two axes is the layer's ratio, which is what keeps a cell
        /// square in screen pixels. A count derived from the visible count would round on its own
        /// boundaries, and the answer is a star field that stretches and steps while the camera zooms.
        /// The floor holds one whole cell on both axes.</summary>
        public static float2 PatternCells(float cellsWide, float layerAspect)
        {
            float wide = math.max(cellsWide, 1f);
            float tall = math.max(wide / math.max(layerAspect, 0.0001f), 1f);
            return new float2(wide, tall);
        }

        /// <summary>The cell count the screen shows at a pattern scale: the authored width divides by the
        /// scale, and the height follows the layer aspect. This is the pattern's count at the live zoom,
        /// so dividing the pattern's count by the scale answers the same numbers. The floor holds one
        /// whole cell on both axes, so an extreme scale answers a big star field and not an empty sky.
        /// The dot radius reads this count, because the authored pixel size divides by this cell's pixel
        /// width.</summary>
        public static float2 LatticeCount(float cellsWide, float patternScale, float layerAspect)
        {
            float wide = math.max(cellsWide / math.max(patternScale, 0.0001f), 1f);
            float tall = math.max(wide / math.max(layerAspect, 0.0001f), 1f);
            return new float2(wide, tall);
        }

        /// <summary>A star dot's radius in lattice cells: the authored pixel size divides by the
        /// pixel width of a cell at the live scale. The dot zoom factor lifts the pixel size with the
        /// scale, so 0 keeps the dot's screen size while the field spreads and 1 grows the dot with
        /// the field. The floor answers a dot with no authored pixel size: the shader divides by the
        /// radius on the dot's edge, so a zero radius is a division by zero on every star.</summary>
        public static float DotRadiusCells(float starSizePixels, float layerWidthPixels, float cellsWide,
            float patternScale, float dotZoomFactor)
        {
            float scale = math.max(patternScale, 0.0001f);
            float cellPixels = math.max(layerWidthPixels * scale / math.max(cellsWide, 1f), 0.0001f);
            float dotPixels = math.max(starSizePixels, 0f) * (1f + (scale - 1f) * dotZoomFactor);
            return math.max(dotPixels / cellPixels, 0.0001f);
        }

        /// <summary>The layer alpha for a nadir cross-fade value: the authored base alpha stands with
        /// no fade, and the answer lerps to full alpha at a full fade, so the top-down end of the
        /// cross-fade shows the crisp stars for every authored base alpha. Both inputs clamp to
        /// 0..1, because they ride Inspector fields.</summary>
        public static float LayerOpacity(float baseOpacity, float nadirFade)
        {
            float baseValue = math.clamp(baseOpacity, 0f, 1f);
            return baseValue + (1f - baseValue) * math.clamp(nadirFade, 0f, 1f);
        }

        /// <summary>The pattern roll angle for a camera yaw. The camera rolls by its yaw at nadir, so
        /// the pattern carries that angle, and the sign names the direction the stars travel against
        /// the world content.</summary>
        public static float RollDegrees(float yawDegrees, float rollSign)
        {
            return yawDegrees * (rollSign >= 0f ? 1f : -1f);
        }
    }

    /// <summary>
    /// The dynamic star layer: a procedural star field on its own UI layer, above the baked sky and
    /// below the world blit.
    ///
    /// The field samples no texture, so a slide carries no resampling and the stars shimmer at no
    /// zoom and at no slide rate. The plan built this layer for the nadir roll: at top-down the yaw
    /// keys rotate the camera up around the view axis, world content counter-rotates on screen, and
    /// the baked image cannot rotate its sampled region. Crisp stars live on the layer that rolls,
    /// because a crisp star in a screen-fixed image beside stars that roll reads as two star fields
    /// moving apart.
    ///
    /// The caller owns the pose model and names the same three values <see cref="AtlasBackground"/>
    /// takes: the yaw pose (fractional, in yaw cells), the nadir fade (0..1), and the camera yaw the
    /// roll rides. The UV zoom arrives from the parent camera's zoom level, so a zoom move re-answers
    /// on the camera's snap event with no polling.
    ///
    /// The material authors the look — the cell count across the screen, the star density, the dot's
    /// pixel size, the size spread, the brightness bands, the colors, the warm share, the twinkle rate
    /// and depth, the dot zoom factor, the grain snap, and the seed — and the rig authors the motion.
    /// The rig reads the authored numbers once at Awake, the way the warp flow rig reads its authored
    /// flow rate, so the material stays the one author.
    ///
    /// The rig writes the derived numbers the shader reads: the slide in cells, the pattern scale, the
    /// lattice cell count, the dot radius in cells, the roll angle, the layer aspect and pixel size,
    /// and the alpha. <see cref="StarfieldLayerMath"/> answers every one, so a test reads the numbers
    /// the layer draws.
    ///
    /// Writes land on a runtime copy of the material, so the authored asset keeps its bytes: the layer
    /// answers derived numbers every frame, and a write that reached the asset would carry the last
    /// frame's answer into the project.
    ///
    /// The field answers its own lattice, so the layer holds its picture at any window size: the layer
    /// pixel size and aspect ride the RawImage rect, and a resize re-answers the derived numbers with
    /// no authored change.
    /// </summary>
    [ExecuteAlways]
    public class StarfieldLayer : MonoBehaviour
    {
        [SerializeField] private SubpixelCameraBase parentCamera;
        [SerializeField] private RawImage outputImage;
        [Tooltip("The star layer material (PixelEngine/Starfield Layer). The material authors the look, and this component writes the motion.")]
        [SerializeField] private Material starfieldMaterial;
        [Tooltip("Star travel as a fraction of the baked sky's travel over the same yaw move. 1 glues the field to the baked sky, and a lower factor leaves it travelling less, which is the parallax read.")]
        [SerializeField, Range(0f, 2f)] private float parallaxFactor = 0.5f;
        [Tooltip("Pattern spread as a fraction of the view's zoom travel. 1 matches the baked layer's spread, and 0 holds the field at its authored cell count.")]
        [SerializeField, Range(0f, 2f)] private float zoomParallaxFactor = 0.5f;
        [Tooltip("The layer alpha with no nadir fade. The field reaches full alpha at a full fade for any value here, so the top-down view keeps its crisp stars.")]
        [SerializeField, Range(0f, 1f)] private float baseOpacity = 1f;
        [Tooltip("Roll direction. 1 rolls the pattern with the camera yaw, -1 against it. The look pass picks the sign that carries the stars with the world content.")]
        [SerializeField, Range(-1f, 1f)] private float rollSign = 1f;

        private Material runtimeMaterial;
        private Canvas canvas;
        private float pose;
        private float nadirFade;
        private float rollYawDegrees;
        private float authoredCellsWide = 96f;
        private float authoredStarSizePixels = 2f;
        private float authoredDotZoomFactor = 0f;
        private bool authoredReadable;
        private Vector2 lastLayerSize = Vector2.zero;
        private bool missingReported;

        /// <summary>The yaw pose the layer answers, slide fractions included.</summary>
        public float Pose => pose;

        /// <summary>The nadir fade the layer answers, 0..1.</summary>
        public float NadirFade => nadirFade;

        /// <summary>The camera yaw the roll rides, in degrees.</summary>
        public float RollYawDegrees => rollYawDegrees;

        /// <summary>The slide in star cells the layer answered on the last apply.</summary>
        public float AnsweredSlideCells { get; private set; }

        /// <summary>The pattern scale the layer answered on the last apply.</summary>
        public float AnsweredPatternScale { get; private set; } = 1f;

        /// <summary>The lattice cell count the layer answered on the last apply.</summary>
        public Vector2 AnsweredLatticeCount { get; private set; } = Vector2.zero;

        /// <summary>The pattern cell count the rig wrote: the authored width across and the height that
        /// follows the layer aspect. The number carries no zoom, so the pattern's center and its wrap
        /// period hold at every zoom.</summary>
        public Vector2 AnsweredPatternCells { get; private set; } = Vector2.zero;

        /// <summary>A star dot's radius in lattice cells on the last apply.</summary>
        public float AnsweredDotRadiusCells { get; private set; }

        /// <summary>The layer alpha on the last apply.</summary>
        public float AnsweredOpacity { get; private set; } = 1f;

        /// <summary>The pattern roll angle in degrees on the last apply.</summary>
        public float AnsweredRollDegrees { get; private set; }

        /// <summary>The layer's pixel size on the last apply.</summary>
        public Vector2 AnsweredLayerSize { get; private set; } = Vector2.zero;

        /// <summary>The cell count across the screen the material authors.</summary>
        public float CellsWide => authoredCellsWide;

        /// <summary>True when the material carries the authored numbers the rig reads.</summary>
        public bool HasAuthoredNumbers => authoredReadable;

        /// <summary>The parallax factor the layer answers with.</summary>
        public float ParallaxFactor => parallaxFactor;

        /// <summary>The pattern zoom rate the layer answers with.</summary>
        public float ZoomParallaxFactor => zoomParallaxFactor;

        /// <summary>The layer alpha authored for no nadir fade.</summary>
        public float BaseOpacity => baseOpacity;

        /// <summary>The roll direction the layer answers with.</summary>
        public float RollSign => rollSign;

        private void Awake()
        {
            ReadAuthoredNumbers();
        }

        private void OnEnable()
        {
            ReadAuthoredNumbers();
            if (parentCamera != null)
            {
                parentCamera.OnCameraSnapped.AddListener(OnCameraSnapped);
            }
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
            ReadAuthoredNumbers();
            Apply();
        }

        private void Update()
        {
            // The canvas rescales the RawImage rect on a window resize and on a canvas scale change,
            // and the layer's pixel size and aspect ride that rect. Poll the rect the way the sibling
            // components poll Screen.width/height, so a resize re-answers with no authored change.
            if (outputImage == null)
            {
                return;
            }
            Vector2 size = outputImage.rectTransform.rect.size;
            if (size.x != lastLayerSize.x || size.y != lastLayerSize.y)
            {
                Apply();
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial == null)
            {
                return;
            }
            // The component runs in edit mode too, and Unity refuses Destroy outside play mode:
            // an edit-mode teardown that calls Destroy logs the fault and leaves the copy alive.
            if (Application.isPlaying)
            {
                Destroy(runtimeMaterial);
            }
            else
            {
                DestroyImmediate(runtimeMaterial);
            }
            runtimeMaterial = null;
        }

        private void OnCameraSnapped(float3 referencePosition)
        {
            Apply();
        }

        /// <summary>
        /// Read the authored look numbers off the material. The material is the one author of the
        /// look, the way the warp flow rig reads its authored flow rate, so this component keeps no
        /// copy of a number the material carries.
        /// </summary>
        private void ReadAuthoredNumbers()
        {
            if (starfieldMaterial == null)
            {
                authoredReadable = false;
                return;
            }
            authoredReadable = starfieldMaterial.HasProperty("_StarCellsWide")
                && starfieldMaterial.HasProperty("_StarSizePx")
                && starfieldMaterial.HasProperty("_DotZoomFactor");
            if (!authoredReadable)
            {
                Debug.LogError("StarfieldLayer: the star material carries none of _StarCellsWide, _StarSizePx, " +
                    "and _DotZoomFactor, so the layer cannot compute its cell grid.", this);
                return;
            }
            authoredCellsWide = starfieldMaterial.GetFloat("_StarCellsWide");
            authoredStarSizePixels = starfieldMaterial.GetFloat("_StarSizePx");
            authoredDotZoomFactor = starfieldMaterial.GetFloat("_DotZoomFactor");
        }

        /// <summary>Set the yaw pose: the slide answers the pose's cell travel at the authored
        /// parallax factor, the same pose value <see cref="AtlasBackground.SetPose"/> takes.</summary>
        public void SetPose(float value)
        {
            pose = value;
            Apply();
        }

        /// <summary>Set the nadir fade (0..1): the layer alpha rides it, so the crisp stars reach full
        /// strength at the pose the baked nadir image holds.</summary>
        public void SetNadirFade(float value)
        {
            nadirFade = math.clamp(value, 0f, 1f);
            Apply();
        }

        /// <summary>Set the camera yaw the roll rides, in degrees. The pattern rolls by that angle at
        /// the authored sign, so a yaw press at nadir carries the stars around the screen center with
        /// the world content.</summary>
        public void SetRollYawDegrees(float value)
        {
            rollYawDegrees = value;
            Apply();
        }

        /// <summary>
        /// Set the look numbers the play-mode look pass tunes, so a compare runs on one scene with no
        /// scene edit between reads. Each setter re-answers the derived numbers on the spot.
        /// </summary>
        public void SetParallaxFactor(float value)
        {
            parallaxFactor = value;
            Apply();
        }

        /// <summary>Set the pattern zoom rate live, for the look pass that reads the two layers
        /// spreading against each other on a zoom move.</summary>
        public void SetZoomParallaxFactor(float value)
        {
            zoomParallaxFactor = value;
            Apply();
        }

        /// <summary>Set the layer alpha authored for no nadir fade live, for the look pass that reads
        /// the star field's strength against the baked sky.</summary>
        public void SetBaseOpacity(float value)
        {
            baseOpacity = math.clamp(value, 0f, 1f);
            Apply();
        }

        /// <summary>Set the roll direction live: 1 rolls the pattern with the camera yaw, -1 against
        /// it.</summary>
        public void SetRollSign(float value)
        {
            rollSign = value;
            Apply();
        }

        private void Apply()
        {
            if (outputImage == null || starfieldMaterial == null)
            {
                if (!missingReported)
                {
                    missingReported = true;
                    Debug.LogError("StarfieldLayer: outputImage or starfieldMaterial is missing, so the star layer " +
                        "answers nothing.", this);
                }
                return;
            }
            missingReported = false;

            if (!authoredReadable)
            {
                ReadAuthoredNumbers();
                if (!authoredReadable)
                {
                    return;
                }
            }

            if (runtimeMaterial == null)
            {
                runtimeMaterial = new Material(starfieldMaterial);
                outputImage.material = runtimeMaterial;
            }

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }
            float canvasScale = canvas != null ? canvas.scaleFactor : 1f;

            Rect rect = outputImage.rectTransform.rect;
            float layerWidth = math.max(rect.width * canvasScale, 1f);
            float layerHeight = math.max(rect.height * canvasScale, 1f);
            float aspect = layerWidth / layerHeight;
            lastLayerSize = rect.size;

            float uvZoom = parentCamera != null ? parentCamera.GetUVZoomLevel() : 1f;
            float patternScale = StarfieldLayerMath.PatternScale(uvZoom, zoomParallaxFactor);
            float2 lattice = StarfieldLayerMath.LatticeCount(authoredCellsWide, patternScale, aspect);
            float2 pattern = StarfieldLayerMath.PatternCells(authoredCellsWide, aspect);

            AnsweredSlideCells = StarfieldLayerMath.SlideCells(pose, authoredCellsWide, parallaxFactor);
            AnsweredPatternScale = patternScale;
            AnsweredLatticeCount = new Vector2(lattice.x, lattice.y);
            AnsweredPatternCells = new Vector2(pattern.x, pattern.y);
            AnsweredDotRadiusCells = StarfieldLayerMath.DotRadiusCells(authoredStarSizePixels, layerWidth,
                authoredCellsWide, patternScale, authoredDotZoomFactor);
            AnsweredOpacity = StarfieldLayerMath.LayerOpacity(baseOpacity, nadirFade);
            AnsweredRollDegrees = StarfieldLayerMath.RollDegrees(rollYawDegrees, rollSign);
            AnsweredLayerSize = new Vector2(layerWidth, layerHeight);

            runtimeMaterial.SetVector("_Slide", new Vector4(AnsweredSlideCells, 0f, 0f, 0f));
            runtimeMaterial.SetFloat("_ZoomScale", AnsweredPatternScale);
            runtimeMaterial.SetVector("_PatternCells", new Vector4(AnsweredPatternCells.x, AnsweredPatternCells.y, 0f, 0f));
            runtimeMaterial.SetFloat("_DotRadiusCells", AnsweredDotRadiusCells);
            runtimeMaterial.SetFloat("_RollAngle", AnsweredRollDegrees);
            runtimeMaterial.SetFloat("_LayerAspect", aspect);
            runtimeMaterial.SetVector("_LayerSize", new Vector4(layerWidth, layerHeight, 0f, 0f));
            runtimeMaterial.SetFloat("_Opacity", AnsweredOpacity);
        }
    }
}
