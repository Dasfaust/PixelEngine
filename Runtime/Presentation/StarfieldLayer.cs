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

        /// <summary>The camera's pan travel in screen pixels. A world unit is `unitsPerPixel` pixels
        /// wide on the render, and the UV zoom magnifies that, so the travel in pixels is the world
        /// travel divided by the units per pixel and multiplied by the zoom. The pan projects onto the
        /// camera's own screen axes, which is what carries the pitch and the yaw: the camera's right and
        /// up are the screen's right and up, so a world pan lands on the screen the player sees, at any
        /// pitch and any yaw, with no authored angle. The answer is one pair, x across the screen and
        /// y up it.</summary>
        public static float2 ScreenTravelPixels(float3 pan, float3 cameraRight, float3 cameraUp,
            float unitsPerPixel, float uvZoom)
        {
            float perPixel = math.max(unitsPerPixel, 0.0001f);
            float zoom = math.max(uvZoom, 1f);
            return new float2(math.dot(pan, cameraRight), math.dot(pan, cameraUp)) / perPixel * zoom;
        }

        /// <summary>The slide in star cells for a travel across the screen in pixels. A cell is the
        /// layer's pixel width times the pattern scale, divided by the pattern's cell count, so the
        /// travel divides by that cell width to answer cells. The parallax factor scales the travel, so
        /// 1 carries the sky with the world content and 0 pins it. The camera's pan and the sky's travel
        /// run opposite on screen, and the slide's sign carries that: a bigger slide reads as the field
        /// travelling the other way, so a pan the camera makes carries the sky the way the world content
        /// goes.</summary>
        public static float2 PanSlideCells(float2 screenTravelPixels, float layerWidthPixels,
            float patternCellsWide, float patternScale, float panParallaxFactor)
        {
            float cellPixels = math.max(layerWidthPixels, 1f) * math.max(patternScale, 0.0001f)
                / math.max(patternCellsWide, 1f);
            return screenTravelPixels / math.max(cellPixels, 0.0001f) * panParallaxFactor;
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

        /// <summary>The layer alpha while the camera pitches between notch levels. The pitch move is
        /// the one moment the sky is in motion, and the stars are the crisp thing on a screen that is
        /// turning, so the layer dips through the move and returns when the pitch lands: the transition
        /// amount is 0 at a landing and 1 mid-move. The dip depth is authored, so a layer can stay fully
        /// lit through a pitch move at 0 and vanish at 1. The answer never goes below zero, and at a
        /// landing it answers the cross-fade alpha unchanged.</summary>
        public static float LayerOpacity(float baseOpacity, float nadirFade, float transitionAmount,
            float transitionDip)
        {
            float full = LayerOpacity(baseOpacity, nadirFade);
            float amount = math.clamp(transitionAmount, 0f, 1f);
            float dip = math.clamp(transitionDip, 0f, 1f);
            return full * (1f - dip * amount);
        }

        /// <summary>The pattern roll angle for a camera yaw. The camera rolls by its yaw at nadir, so
        /// the pattern carries that angle, and the sign names the direction the stars travel against
        /// the world content.</summary>
        public static float RollDegrees(float yawDegrees, float rollSign)
        {
            return yawDegrees * (rollSign >= 0f ? 1f : -1f);
        }

        /// <summary>The roll angle at a live nadir fade. A yaw step is a slide at the isometric look and a
        /// roll at top-down, and the nadir cross-fade is the number that says which look the camera is in,
        /// so the roll rides it: 0 fade answers no roll, a full fade answers the yaw whole, and the
        /// cross-fade carries the roll in between. A layer that rolls at every pitch reads as a star
        /// viewport turning under a camera that is translating its content.</summary>
        public static float RollDegrees(float yawDegrees, float rollSign, float nadirFade)
        {
            return RollDegrees(yawDegrees, rollSign) * math.clamp(nadirFade, 0f, 1f);
        }

        /// <summary>The roll angle at a live nadir fade, with the roll led ahead of the fade. The lead
        /// multiplies the fade the roll reads, so a lead of 2 answers a full roll at half the fade: the
        /// sky turns while the layer is still dim, and the field comes back into view already turned.
        /// A lead of 1 rides the fade, which is the shape where the last part of the turn lands on a
        /// field the player can already see.</summary>
        public static float RollDegrees(float yawDegrees, float rollSign, float nadirFade, float rollLead)
        {
            float lead = math.max(rollLead, 0f);
            return RollDegrees(yawDegrees, rollSign, math.clamp(nadirFade * lead, 0f, 1f));
        }

        /// <summary>The pitch travel's slide in pattern cells. The sky answers a pitch step the way it
        /// answers a yaw step: 45 degrees of camera rotation carries the field one pattern width at a
        /// factor of 1, so the 60-degree tilt from the isometric notch to top-down carries it 1.333
        /// widths, and a pitch at or below the base notch answers no slide. The sign moves the field down
        /// the screen, which is the direction the world content travels as the camera tips to top-down,
        /// and the slide lands in the pattern's own frame, so the nadir roll turns its direction to the
        /// camera's angle.</summary>
        public static float PitchSlideCells(float pitchDegrees, float basePitchDegrees, float cellsWide,
            float pitchParallaxFactor)
        {
            float turn = math.max(pitchDegrees - basePitchDegrees, 0f);
            return turn * (1f / 45f) * cellsWide * pitchParallaxFactor;
        }

        /// <summary>The camera's tilt in degrees, read from the view direction. A camera pointing
        /// straight down reads 90 and a level camera reads 0, and the number carries every yaw, so no
        /// authored angle and no euler wrap is involved.</summary>
        public static float PitchDegrees(float3 viewForward)
        {
            return math.degrees(math.asin(math.clamp(-math.dot(viewForward, math.normalize(new float3(0f, 1f, 0f))),
                -1f, 1f)));
        }

        /// <summary>The screen direction the stars flow during a warp, from the travel vector the warp
        /// layer reads. The travel arrives in view space, where x is screen right and y is screen up, so
        /// the direction needs no camera axes and no authored angle: it is the travel's own screen
        /// components, taken against the travel, because the stars run opposite the streaks. A travel
        /// with no screen component answers no direction, which is the level look where a warp carries
        /// the sky along the view axis and nothing across the screen.</summary>
        public static float2 WarpFlowDirection(float3 viewTravel)
        {
            float2 screen = new float2(viewTravel.x, viewTravel.y);
            float length = math.length(screen);
            if (length < 0.0001f)
            {
                return float2.zero;
            }
            return -screen / length;
        }

        /// <summary>The flow speed in pattern cells per second: one pattern width per second at a full
        /// intensity and a speed of 1, so the speed knob multiplies the warp intensity and nothing else
        /// moves. Intensity is clamped first, so an over-range caller cannot speed the sky past the
        /// authored rate, and a stopped warp carries no star flow at all.</summary>
        public static float WarpFlowRateCells(float patternCellsWide, float intensity, float flowSpeed)
        {
            return patternCellsWide * math.clamp(intensity, 0f, 1f) * math.max(flowSpeed, 0f);
        }

        /// <summary>A cell distance folded into one pattern period. The warp flow accumulates every
        /// frame, and a long warp would grow the number without bound; the pattern repeats at its own
        /// width, so folding at the period answers the same screen at every fold.</summary>
        public static float WrapCells(float cells, float patternCells)
        {
            float period = math.max(patternCells, 1f);
            return cells - math.floor(cells / period) * period;
        }

        /// <summary>The fade weight the layer's visibility reads. The lag pushes the visibility ramp
        /// back along the cross-fade, so the field stays dim through the near side of the tilt and
        /// brightens as the camera closes on top-down. The roll reads the fade ahead of this number,
        /// which is what lets the sky turn before the stars come back into view.</summary>
        public static float FadeWeight(float nadirFade, float fadeLag)
        {
            float fade = math.clamp(nadirFade, 0f, 1f);
            float lag = math.clamp(fadeLag, 0f, 1f);
            return math.pow(fade, 1f + 3f * lag);
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
        [Tooltip("Sky travel as a fraction of the world content's travel over the same camera pan. 1 carries the sky with the world, and 0 pins it. The pan projects onto the camera's screen axes, so the pitch and the yaw need no authored angle.")]
        [SerializeField, Range(0f, 4f)] private float panParallaxFactor = 0.5f;
        [Tooltip("How far the layer dims while the camera pitches between notch levels. 1 vanishes mid-move and returns at the landing, and 0 keeps the layer lit through the turn.")]
        [SerializeField, Range(0f, 1f)] private float pitchTransitionDip = 1f;
        [Tooltip("The layer alpha with no nadir fade. The field reaches full alpha at a full fade for any value here, so the top-down view keeps its crisp stars.")]
        [SerializeField, Range(0f, 1f)] private float baseOpacity = 1f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("The tilt the pitch slide starts from, matching the camera's isometric notch. The slide " +
                 "measures pitch above this number, so the isometric landing carries no pitch term.")]
        private float pitchBaseDegrees = 30f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("The star flow's speed multiplier over the warp intensity. One pattern width per second " +
                 "at a full intensity and a speed of 1, so 2 doubles the flow and 0 stops it. The stars " +
                 "run opposite the warp streaks at every view angle, and the direction comes from the " +
                 "warp layer's own travel vector.")]
        private float warpFlowSpeed = 1f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("How far the pitch tilt slides the sky, in the same currency as the yaw slide: 1 carries " +
                 "45 degrees of tilt one pattern width, so the tilt from the isometric notch to top-down " +
                 "carries 1.333 widths. 0 leaves the sky standing while the camera tips.")]
        private float pitchSlideParallax = 1f;
        [Tooltip("Roll direction. 1 rolls the pattern with the camera yaw, -1 against it. The look pass picks the sign that carries the stars with the world content.")]
        [SerializeField, Range(-1f, 1f)] private float rollSign = 1f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("How far ahead of the nadir cross-fade the roll runs. A lead of 2 turns the sky to the " +
                 "authored yaw at half the fade, so the stars come back into view already turned. A lead " +
                 "of 1 rides the fade, and the last part of the turn lands on a field already visible.")]
        private float rollLead = 2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How far back along the nadir cross-fade the layer's visibility sits. The lag keeps the " +
                 "field dim through the near side of the tilt, so the roll runs while the stars are faint.")]
        private float fadeLag = 0.6f;

        private Material runtimeMaterial;
        private Canvas canvas;
        private float pose;
        private float nadirFade;
        private float rollYawDegrees;
        private float pitchTransition;
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

        /// <summary>The travel the layer answered for the camera's pan, in pattern cells, across and
        /// up the screen. The yaw slide answers separately as <see cref="AnsweredSlideCells"/>, and the
        /// layer writes the sum to the material.</summary>
        public Vector2 AnsweredPanSlideCells { get; private set; }

        /// <summary>The pitch tilt's share of the slide, in pattern cells.</summary>
        public float AnsweredPitchSlideCells { get; private set; }

        /// <summary>The camera tilt the rig read, in degrees.</summary>
        public float AnsweredPitchDegrees { get; private set; }

        /// <summary>The warp flow's share of the slide, in pattern cells.</summary>
        public Vector2 AnsweredWarpFlowCells { get; private set; }

        /// <summary>The warp flow's speed, in pattern cells per second.</summary>
        public float AnsweredWarpFlowRateCells { get; private set; }

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

        /// <summary>How far ahead of the cross-fade the roll runs.</summary>
        public float RollLead => rollLead;

        /// <summary>How far back along the cross-fade the layer's visibility sits.</summary>
        public float FadeLag => fadeLag;

        /// <summary>The tilt the pitch slide starts from.</summary>
        public float PitchBaseDegrees => pitchBaseDegrees;

        /// <summary>How far the pitch tilt slides the sky.</summary>
        public float PitchSlideParallax => pitchSlideParallax;

        /// <summary>The star flow's speed multiplier over the warp intensity.</summary>
        public float WarpFlowSpeed => warpFlowSpeed;

        private Vector3 warpTravel;
        private float warpIntensity;
        private float2 warpFlowCells;

        /// <summary>The warp layer's travel vector and intensity, read every frame the warp layer reads
        /// them. The travel is in view space, where x is screen right and y is screen up, so the star
        /// flow needs no camera axes and no authored angle to run opposite the streaks.</summary>
        public void SetWarpFlow(Vector3 viewTravel, float intensity)
        {
            warpTravel = viewTravel;
            warpIntensity = intensity;
        }

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

            // The authored asset goes back on the Graphic, so the scene reference a save writes is the
            // authored asset and not the play-mode clone.
            if (outputImage != null && starfieldMaterial != null)
            {
                outputImage.material = starfieldMaterial;
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

        /// <summary>Set the pitch transition amount (0 at a landing, 1 mid-move between the notch
        /// levels). The layer dims through the move by the authored dip and returns at the landing, so
        /// the crisp stars are off the screen while the sky is turning.</summary>
        public void SetPitchTransition(float value)
        {
            pitchTransition = math.clamp(value, 0f, 1f);
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
                // The clone carries the rig-written numbers, and it is never a saved object: a clone
                // assigned to a scene Graphic is serialized into the scene as a scene-local material, and
                // the scene then stops referencing the authored asset. HideFlags.DontSave keeps the clone
                // out of any saved file, and the Graphic takes it only while the game runs, because a
                // save in edit mode serializes whatever reference the Graphic holds at that moment.
                runtimeMaterial = new Material(starfieldMaterial)
                {
                    hideFlags = UnityEngine.HideFlags.DontSave
                };
                if (UnityEngine.Application.isPlaying)
                {
                    outputImage.material = runtimeMaterial;
                }
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

            // The camera's pan scrolls the field across and up the screen. The pivot position is the
            // pan, so the layer reads the camera rig's own number and needs no caller for it. The travel
            // projects onto the camera's own screen axes, so the pitch and the yaw are carried by the
            // camera's transform and the rule needs no authored angle, at nadir and at the isometric
            // landing alike.
            Camera view = parentCamera != null ? parentCamera.GetCamera() : null;
            float unitsPerPixel = parentCamera != null ? parentCamera.GetUnitsPerPixel() : 1f;
            float3 viewRight = view != null ? view.transform.right : Vector3.right;
            float3 viewUp = view != null ? view.transform.up : Vector3.up;
            float3 panWorld = parentCamera != null ? parentCamera.GetPivotPosition() : float3.zero;
            float2 screenTravel = StarfieldLayerMath.ScreenTravelPixels(panWorld, viewRight, viewUp,
                unitsPerPixel, uvZoom);
            float2 panSlide = StarfieldLayerMath.PanSlideCells(screenTravel, layerWidth, authoredCellsWide,
                patternScale, panParallaxFactor);
            AnsweredPanSlideCells = new Vector2(panSlide.x, panSlide.y);

            // The pitch tilt slides the sky, and the slide is the same channel the yaw and the pan write.
            // A pitch step and a yaw step are the same rotation of the camera, so the sky answers them
            // with the same travel, and the direction comes from the camera's own axes: the tilt lands
            // the field down the screen at the camera's angle, at every yaw notch.
            float pitchDegrees = view != null ? StarfieldLayerMath.PitchDegrees(view.transform.forward)
                : pitchBaseDegrees;
            float pitchSlide = StarfieldLayerMath.PitchSlideCells(pitchDegrees, pitchBaseDegrees,
                authoredCellsWide, pitchSlideParallax);
            AnsweredPitchDegrees = pitchDegrees;
            AnsweredPitchSlideCells = pitchSlide;

            AnsweredDotRadiusCells = StarfieldLayerMath.DotRadiusCells(authoredStarSizePixels, layerWidth,
                authoredCellsWide, patternScale, authoredDotZoomFactor);
            AnsweredOpacity = StarfieldLayerMath.LayerOpacity(baseOpacity,
                StarfieldLayerMath.FadeWeight(nadirFade, fadeLag), pitchTransition, pitchTransitionDip);
            AnsweredRollDegrees = StarfieldLayerMath.RollDegrees(rollYawDegrees, rollSign, nadirFade,
                rollLead);
            AnsweredLayerSize = new Vector2(layerWidth, layerHeight);

            // The warp flow is a rate, not a position, so it accumulates. The warp layer reads its
            // travel every frame, and the star layer folds the accumulated cells into the pattern
            // period so a long warp keeps a bounded number and the same screen.
            float2 flowDirection = StarfieldLayerMath.WarpFlowDirection(warpTravel);
            float flowRate = StarfieldLayerMath.WarpFlowRateCells(authoredCellsWide, warpIntensity,
                warpFlowSpeed);
            float seconds = UnityEngine.Application.isPlaying ? UnityEngine.Time.deltaTime : 0f;
            warpFlowCells += flowDirection * flowRate * seconds;
            warpFlowCells = new float2(
                StarfieldLayerMath.WrapCells(warpFlowCells.x, pattern.x),
                StarfieldLayerMath.WrapCells(warpFlowCells.y, pattern.y));
            AnsweredWarpFlowCells = new Vector2(warpFlowCells.x, warpFlowCells.y);
            AnsweredWarpFlowRateCells = flowRate;

            runtimeMaterial.SetVector("_Slide", new Vector4(AnsweredSlideCells + panSlide.x + warpFlowCells.x,
                panSlide.y + pitchSlide + warpFlowCells.y, 0f, 0f));
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
