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

        /// <summary>The ray path that covers the whole view from the vanishing
        /// point: the farthest screen corner distance plus the margin. The
        /// point lands off screen on the side and nadir views, and the visible
        /// screen then starts at a distance larger than the point's distance
        /// from center — a path that ends short of the farthest corner puts
        /// every visible pixel past the far fade, and the layer answers
        /// nothing. The margin is where a head dies off the edge, so the
        /// cycle wrap reads as flow past the border and not as a pop.</summary>
        public static float RayPath(float2 vanishPoint, float margin)
        {
            float far = 0f;
            for (int i = 0; i < 4; i++)
            {
                var corner = new float2(i % 2, i / 2);
                far = math.max(far, math.length(vanishPoint - corner));
            }
            return far + margin;
        }

        /// <summary>The streak count per ray that holds the authored per-UV
        /// spacing across the path: count = perUv x path, so a longer path
        /// carries proportionally more streaks and the on-screen wavelength
        /// stays 1 / perUv at every pose. The answer is a whole number: the
        /// cycle wrap drops the head by one path, which is the count in
        /// wavelength units, and the streak pattern — a frac over the
        /// coordinate — is continuous across the wrap only when that drop is
        /// whole. A fractional count leaves the fraction as a remainder, and
        /// the pattern kicks by that fraction of a wavelength at every wrap,
        /// once per cycle.</summary>
        public static float StreakCount(float streaksPerUv, float path)
        {
            return math.max(math.round(streaksPerUv * math.max(path, 0.01f)), 1f);
        }

        /// <summary>The warp texture resolution: the screen resolution divided
        /// by the authored render scale in whole pixels, floored at 1. The
        /// canvas upscales the texture point-filtered, so the warp pixels are
        /// scale screen pixels wide at scale 1, uniform across the frame.</summary>
        public static int2 WarpResolution(int width, int height, int scale)
        {
            int s = math.max(scale, 1);
            return new int2(math.max(width / s, 1), math.max(height / s, 1));
        }

        /// <summary>The flow rate the layer runs at for a given intensity: the
        /// authored full rate scaled by that intensity, so the streak speed
        /// reads the warp strength. The rule is linear: half intensity is half
        /// speed, full intensity is the authored rate, zero intensity is
        /// stopped. The floor fraction is the fraction of the full rate a
        /// stopped warp keeps, so floor 0 gives the plain linear rule and floor
        /// 0.25 runs at a quarter speed at zero intensity. Intensity is clamped
        /// to 0..1 first, so an over-range caller cannot speed the flow past
        /// the authored rate.
        ///
        /// The authored rate enters as a number and never as live state, so
        /// calling this with the same intensity any number of times answers the
        /// same rate. A caller that feeds back the value it last wrote would
        /// decay the flow to zero, and this shape is why it does not need to
        /// keep the authored value anywhere.</summary>
        public static float FlowRate(float authoredRate, float intensity, float floorFraction)
        {
            float clamped = math.clamp(intensity, 0f, 1f);
            float floor = math.clamp(floorFraction, 0f, 1f);
            return authoredRate * (floor + (1f - floor) * clamped);
        }

        /// <summary>The distance from the vanishing point to the screen center, in
        /// screen widths. <c>VanishingPoint</c> answers in unit-square UV, so 0.5,
        /// 0.5 is the center and the answer is in screen widths. Zero means the
        /// point sits at the center: the whole screen is at the point's own speed,
        /// and the screen reads at the lowest speed the pattern has.</summary>
        public static float ScreenSpread(float2 vanishPoint)
        {
            return math.length(vanishPoint - new float2(0.5f, 0.5f));
        }

        /// <summary>The number the screen-center speed divides by: the ray path, plus
        /// the screen spread scaled by the acceleration span.
        ///
        /// The shader multiplies the per-point streak speed by
        /// <c>exp(accel * distance / path)</c>, so a pixel on the screen center runs
        /// at rate times the path-to-center factor. At the authored acceleration the
        /// factor at the center is this denominator times <c>accel</c> over the
        /// acceleration span, so one denominator per pose carries the center speed.
        ///
        /// Spread 0, a path of 0.01, and acceleration 0 each answer the plain path,
        /// which is the value the rule had before the center term existed.</summary>
        public static float ScreenSpeedDenominator(float2 vanishPoint, float path, float accelExponent)
        {
            float span = math.exp(math.max(accelExponent, 0f)) - 1f;
            return math.max(path, 0.01f) + ScreenSpread(vanishPoint) * span;
        }

        /// <summary>The flow rate one pose writes: the intensity-scaled rate, slowed
        /// when that pose's screen sits farther along the acceleration ramp than the
        /// speed reference does.
        ///
        /// The streak spacing is fixed on the screen, so a pixel's speed is the
        /// pattern's sweep, and the shader ramps that speed with distance from the
        /// vanishing point. A view whose screen lies far from its point therefore
        /// reads faster than a view whose screen sits on the point: the side view
        /// sweeps its screen from 1.5 to 2.5 screen widths out, where a head-on view
        /// sweeps from 0 to 0.85, and the same authored rate reads about twice as
        /// fast at the side view's screen center.
        ///
        /// The reference names the view the authored rate is tuned at. A pose whose
        /// denominator runs larger writes rate x reference denominator / denominator,
        /// which lands its screen-center speed on the reference view's center speed
        /// and lengthens that pose's cycle. A pose with a smaller denominator keeps
        /// the authored rate, so the authored Flow Rate stays the fastest the layer
        /// runs and the shortest cycle it has.
        ///
        /// The scaling divides the rate and never the ray path: the path keeps its
        /// full length, so the far fade keeps covering the corner it was written for.</summary>
        public static float SpeedRate(float authoredRate, float intensity, float floorFraction,
            float referenceDenominator, float denominator)
        {
            float rate = FlowRate(authoredRate, intensity, floorFraction);
            float shrink = referenceDenominator / math.max(denominator, 0.01f);
            return rate * math.min(shrink, 1f);
        }

        /// <summary>The flow clock advance one frame buys: the rate times the
        /// elapsed time, with the elapsed time capped at <c>stepCapSeconds</c>.
        /// The cap is what a stalled frame, a tab-out, or a debugger pause
        /// costs: the clock jumps the capped step and the streaks skip, where
        /// an uncapped delta would send the pattern a long way in one frame.
        /// A non-positive elapsed time advances nothing, so a caller that
        /// repeats the same clock value writes the same clock.</summary>
        public static float FlowStep(float rate, float elapsedSeconds, float stepCapSeconds)
        {
            float elapsed = math.clamp(elapsedSeconds, 0f, math.max(stepCapSeconds, 0f));
            return math.max(rate, 0f) * elapsed;
        }

        /// <summary>The flow clock after one frame, kept in [0, 1). The clock is
        /// accumulated and never rebuilt from the wall clock, which is the rule
        /// that keeps the streak direction fixed.
        ///
        /// A clock built as rate x absolute time moves per frame by
        /// rate x deltaTime + time x deltaRate, so the rate's own change enters
        /// the motion, weighted by how large the time has grown. A rate that
        /// falls then moves the clock backwards and the streaks run in reverse.
        /// Accumulating rate x deltaTime keeps the per-frame advance equal to the
        /// rate, so a falling rate slows the streaks and never turns them.
        ///
        /// The wrap keeps the clock in the shader's own cycle range, so a long
        /// warp does not push a large number into a float.</summary>
        public static float AdvancePhase(float phase, float rate, float elapsedSeconds, float stepCapSeconds)
        {
            float wrapped = phase + FlowStep(rate, elapsedSeconds, stepCapSeconds);
            return wrapped - math.floor(wrapped);
        }
    }

    /// <summary>
    /// Warp streak layer: renders the streak field into a RenderTexture at
    /// screen resolution divided by the authored render scale, and blits that
    /// texture through a RawImage with point filtering. The layer sits behind
    /// the viewport blit and in front of the background, so the world geometry
    /// the camera renders streams in front of the streaks and the sky stays
    /// behind them. The camera stays free: warp is simulated travel time, and
    /// nothing in this component touches the camera pose.
    ///
    /// The caller names the travel direction in view space and an intensity;
    /// the rig answers the vanishing point, the axial sign, the ray count,
    /// the ray path, the streak count, and the warp-texture grid cell. The
    /// path and the streak count scale with the vanishing point distance:
    /// the side and nadir views land the point off screen, and the visible
    /// screen then starts at a larger distance, so a fixed path would put
    /// every visible pixel past the far fade. The streak speed reads the same
    /// geometry twice over: the pattern advances one ray path per cycle, and the
    /// shader ramps pixel speed with the distance from the vanishing point. So a
    /// pose whose screen sits far from its point reads faster at the same rate.
    /// The rate scales down by the denominator ratio past the component's speed
    /// reference: the streaks sweep the screen center at one speed at every pose,
    /// and a pose whose screen lies farther out lengthens its cycle instead of
    /// running faster. The look parameters — the full
    /// flow rate, bands, color — are authored on the material, and
    /// the per-pose values write onto a play-mode clone of it, so the
    /// authored asset never carries the runtime answer. Unity 6.6's
    /// CanvasRenderer has no MaterialPropertyBlock path, so the clone is the
    /// per-layer write surface. No game concept crosses the seam: "game
    /// north" is the caller's, not this component's.
    ///
    /// Render path: the rig blits the clone into the warp texture each late
    /// update, clears the texture first so each frame answers only its own
    /// vanishing point, and hands the texture to the RawImage. The canvas
    /// draws that RawImage with a blit material authored on
    /// PixelEngineSubpixelViewport — edge margin 0, zoom 1, alpha blend —
    /// so the layer samples through the same bilinear point sampling the
    /// viewport blit rides: UVs snap to warp-texture texel centers, a
    /// magnified texel lands crisp whole, and a minified texel blends
    /// through the smoothstep. The blit pass stores the streak color and
    /// its alpha unmultiplied — measured in play mode: a quarter-band
    /// pixel carries (6, 177, 187) at alpha 64, the authored cyan whole —
    /// so the canvas alpha blend lands an authored band alpha as authored,
    /// one multiplication. The warp pixels are the warp texture's pixels —
    /// whole, uniform, upscale-stable — so the layer shares the world's
    /// low-resolution pixel look and answers no screen-resolution grid.
    /// The grid cell the shader quantizes to is the warp texture's cell,
    /// derived here, and the material's pixel scale multiplies it.
    /// </summary>
    public class WarpFlow : MonoBehaviour
    {
        [SerializeField] private RawImage outputImage;
        [Tooltip("Authored warp material: the look source the rig clones for its per-frame writes. The RawImage's own material is the canvas blit material, not this one.")]
        [SerializeField] private Material warpMaterial;
        [Tooltip("Warp texture scale: 2 renders at half screen resolution and the canvas upscales. 1 renders at screen resolution.")]
        [SerializeField, Range(1, 4)] private int renderScale = 2;
        [Tooltip("Authored focal length: the cone strength. The vanishing point sits focal x tan(gaze-travel angle) off center.")]
        [SerializeField, Min(0.01f)] private float focal = 0.3f;
        [Tooltip("The axial floor: the smallest receding component before the point is clamped far. The side views land on this floor as parallel streaks.")]
        [SerializeField, Range(0.01f, 1f)] private float minAxial = 0.15f;
        [Tooltip("Streak rays across the view. The ray count scales with the vanishing point distance to hold this density.")]
        [SerializeField, Min(2)] private int targetRays = 16;
        [Tooltip("Streak spacing on the screen, in streaks per UV width. The streak count scales with the ray path to hold this spacing.")]
        [SerializeField, Min(1f)] private float streaksPerUv = 10f;
        [Tooltip("How far past the farthest screen corner the ray path runs. A head dies past the corner, so the cycle wrap reads as flow past the border.")]
        [SerializeField, Min(0.02f)] private float pathMargin = 0.15f;
        [Tooltip("Flow rate at zero intensity, as a fraction of the material's authored Flow Rate. 0 is the linear rule: the streaks stop with the warp. A quarter leaves a quarter speed standing, so a faint layer keeps moving.")]
        [SerializeField, Range(0f, 1f)] private float flowRateFloor = 0f;
        [Tooltip("The largest elapsed time one frame may add to the flow clock, in seconds. A stalled frame, a tab-out, or a paused debugger reports a large elapsed time; the cap makes that a skip and not a jump.")]
        [SerializeField, Min(0.001f)] private float flowStepCapSeconds = 0.1f;
        [Tooltip("The view the material's authored Flow Rate is tuned at, as a travel direction in view space: X along screen right, Y along screen up, Z against the gaze. (0, 0, -1) is travel straight ahead of a level gaze; (0, sin p, -cos p) is travel ahead with the gaze pitched down p. The rig works out that view's screen speed from it, so no path number is authored here. A pose whose screen sits farther along the acceleration ramp writes a slower rate, lands its screen-center speed on this view's center speed, and lengthens its cycle. A pose with a nearer screen keeps the authored rate, so the authored Flow Rate is the fastest the layer runs. The default is the head-on view pitched down 30 degrees.")]
        [SerializeField] private Vector3 speedReferenceViewTravel = new Vector3(0f, 0.5f, -0.866f);

        private Material runtimeMaterial;
        private RenderTexture warpTexture;
        private bool missingReported;
        private float authoredFlowRate;
        private bool flowRateReadable;
        private float authoredAccel;
        private bool accelReadable;
        private float flowPhase;
        private double lastPhaseTime = -1;

        private void Awake()
        {
            if (outputImage != null && warpMaterial != null)
            {
                runtimeMaterial = new Material(warpMaterial);
                // The authored full rate is read once, off the authored material,
                // before any write lands on the clone. The scale multiplies this
                // number every frame, so a value read back off the clone after a
                // write would compound and decay the flow toward zero.
                flowRateReadable = warpMaterial.HasProperty("_FlowRate");
                authoredFlowRate = flowRateReadable ? warpMaterial.GetFloat("_FlowRate") : 0f;
                // The acceleration exponent sets the span of the shader's speed ramp,
                // and the pose rule divides by that span, so a material that ramps
                // harder spreads the pose-to-pose rate ratio wider. A material with no
                // _Accel answers 0, and the pose rule scales by the ray path alone.
                accelReadable = warpMaterial.HasProperty("_Accel");
                authoredAccel = accelReadable ? warpMaterial.GetFloat("_Accel") : 0f;
                // The canvas draws the RawImage with the UI default
                // material, which takes the warp texture straight; the warp
                // shader rides only the blit into the texture.
                warpTexture = CreateWarpTexture();
                outputImage.texture = warpTexture;
            }
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
            if (warpTexture != null)
            {
                warpTexture.Release();
                Destroy(warpTexture);
            }
        }

        private void LateUpdate()
        {
            if (outputImage == null || runtimeMaterial == null)
            {
                return;
            }
            var wanted = WarpFlowMath.WarpResolution(Screen.width, Screen.height, renderScale);
            if (warpTexture == null || warpTexture.width != wanted.x || warpTexture.height != wanted.y)
            {
                if (warpTexture != null)
                {
                    warpTexture.Release();
                    Destroy(warpTexture);
                }
                warpTexture = CreateWarpTexture();
                outputImage.texture = warpTexture;
            }

            // Clear the color before the blit. The streak pass alpha-blends
            // onto the texture, and an alpha-blended source leaves the
            // destination's standing alpha where its own alpha is 0 — so a
            // texture that is not cleared carries every past frame's streak
            // trails, and a camera rotation smears them across the frame.
            // The cleared floor makes each frame answer only this frame's
            // vanishing point.
            var previous = RenderTexture.active;
            RenderTexture.active = warpTexture;
            GL.Clear(true, true, Color.clear);
            Graphics.Blit((Texture)null, warpTexture, runtimeMaterial);
            RenderTexture.active = previous;
        }

        private RenderTexture CreateWarpTexture()
        {
            var size = WarpFlowMath.WarpResolution(Screen.width, Screen.height, renderScale);
            // Bilinear filtering, paired with the canvas blit material's
            // bilinear point sampling: the material snaps its UV to texel
            // centers, so a magnified texel lands crisp whole, and a
            // minified texel — a UI scale below 1 — blends through the
            // smoothstep instead of aliasing. The pairing is the same one
            // the viewport blit rides.
            var texture = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.ARGB32)
            {
                name = "WarpFlow Texture",
                filterMode = FilterMode.Bilinear
            };
            texture.Create();
            return texture;
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
            float path = WarpFlowMath.RayPath(vanishPoint, pathMargin);
            float streaks = WarpFlowMath.StreakCount(streaksPerUv, path);

            // The shader quantizes to the warp texture's own cells; the
            // material's pixel scale multiplies them from here.
            float2 gridCell = warpTexture != null && warpTexture.width > 0 && warpTexture.height > 0
                ? new float2(1f / warpTexture.width, 1f / warpTexture.height)
                : new float2(0.005f, 0.005f);

            runtimeMaterial.SetVector("_VanishPoint", new Vector4(vanishPoint.x, vanishPoint.y, 0f, 0f));
            runtimeMaterial.SetFloat("_AxialSign", axialSign);
            runtimeMaterial.SetFloat("_RayCount", rays);
            runtimeMaterial.SetFloat("_RayPath", path);
            runtimeMaterial.SetFloat("_StreakDensity", streaks);
            runtimeMaterial.SetFloat("_Intensity", math.clamp(intensity, 0f, 1f));
            // The streak speed reads the intensity: the authored Flow Rate is the
            // rate at full warp, and the same number drives brightness, so the
            // layer's speed and its strength move together. _FlowRate carries the
            // rate the rig is running at, which is the number a live read checks.
            // The pose rule sits on the same number: a pose whose screen sits farther
            // along the shader's speed ramp than the reference view's does writes a
            // proportionally slower rate, so the streaks sweep the screen center at one
            // speed at every pose and the cycle lengthens instead of the speed growing.
            // The reference is the authored view, read through the same three numbers.
            float3 refTravel = speedReferenceViewTravel;
            if (math.lengthsq(refTravel) < 1e-6f)
            {
                refTravel = new float3(0f, 0f, -1f);
            }
            float2 refPoint = WarpFlowMath.VanishingPoint(refTravel, focal, minAxial);
            float referenceDenominator = WarpFlowMath.ScreenSpeedDenominator(
                refPoint, WarpFlowMath.RayPath(refPoint, pathMargin), authoredAccel);
            float denominator = WarpFlowMath.ScreenSpeedDenominator(vanishPoint, path, authoredAccel);
            float rate = WarpFlowMath.SpeedRate(authoredFlowRate, intensity, flowRateFloor, referenceDenominator, denominator);
            if (flowRateReadable)
            {
                runtimeMaterial.SetFloat("_FlowRate", rate);
            }
            // The clock accumulates, once per frame, and the shader reads it as given.
            // A caller may call this method more than once in a frame; the frame stamp
            // is what keeps a repeated call from advancing the clock twice.
            double now = Time.timeAsDouble;
            if (now != lastPhaseTime)
            {
                flowPhase = WarpFlowMath.AdvancePhase(
                    flowPhase, rate, (float)(now - (lastPhaseTime < 0d ? now : lastPhaseTime)), flowStepCapSeconds);
                lastPhaseTime = now;
            }
            runtimeMaterial.SetFloat("_FlowPhase", flowPhase);
            runtimeMaterial.SetVector("_GridCell", new Vector4(gridCell.x, gridCell.y, 0f, 0f));
        }
    }
}
