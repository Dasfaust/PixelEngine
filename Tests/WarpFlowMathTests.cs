using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// Flow-field math for the warp streak layer. The fixture states the table
    /// rules the plan authors: gaze along travel vanishes at the view center,
    /// gaze against travel converges there, the side views land mirrored far
    /// off screen as parallel flow, the converge cone mirrors the diverge cone
    /// across 90 degrees, the ray count holds the authored density as the
    /// point moves, the ray path covers every screen corner plus the margin
    /// as the point moves off screen, the streak count holds the on-screen
    /// spacing across paths and stays whole so the cycle wrap is seamless,
    /// and a perpendicular gaze answers the tie
    /// deterministically.
    /// </summary>
    public class WarpFlowMathTests
    {
        private const float Focal = 0.3f;
        private const float MinAxial = 0.15f;

        private static float2 VP(float3 viewDir)
        {
            return WarpFlowMath.VanishingPoint(viewDir, Focal, MinAxial);
        }

        [Test]
        public void GazeAlongTravel_VanishesAtTheViewCenter_Diverging()
        {
            float3 viewDir = new float3(0f, 0f, -1f);
            float2 vp = VP(viewDir);
            Assert.AreEqual(0.5f, vp.x, 1e-5f, "x");
            Assert.AreEqual(0.5f, vp.y, 1e-5f, "y");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(viewDir, MinAxial), 1e-6f, "sign");
        }

        [Test]
        public void GazeAgainstTravel_ConvergesIntoTheCenter()
        {
            float3 viewDir = new float3(0f, 0f, 1f);
            float2 vp = VP(viewDir);
            Assert.AreEqual(0.5f, vp.x, 1e-5f, "x");
            Assert.AreEqual(0.5f, vp.y, 1e-5f, "y");
            Assert.AreEqual(-1f, WarpFlowMath.AxialSign(viewDir, MinAxial), 1e-6f, "sign");
        }

        [Test]
        public void SideViews_LandMirroredFarOffScreen_ParallelFlow()
        {
            // Travel at the view axis, screen left (x -1) and screen right (x +1),
            // with no axial component: the signed floor answers +MinAxial, the
            // point lands on the travel side, and the two answers mirror.
            float2 east = VP(new float3(-1f, 0f, 0f));
            float2 west = VP(new float3(1f, 0f, 0f));
            Assert.AreEqual(0.5f - Focal / MinAxial, east.x, 1e-5f, "east x far left");
            Assert.AreEqual(0.5f + Focal / MinAxial, west.x, 1e-5f, "west x far right");
            Assert.AreEqual(east.x - 0.5f, -(west.x - 0.5f), 1e-5f, "mirror");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(-1f, 0f, 0f), MinAxial), 1e-6f, "east streams away from its far point");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(1f, 0f, 0f), MinAxial), 1e-6f, "west streams away from its far point");
        }

        [Test]
        public void ConeOffsets_GrowWithTheGazeTravelAngle_AndClampAtTheFloor()
        {
            float centerOffset = 0f;
            float yaw45 = VP(new float3(-0.7071f, 0f, -0.7071f)).x - 0.5f;
            Assert.AreEqual(-Focal, yaw45, 1e-4f, "yaw 45 offset = focal x tan(45)");

            float yaw60 = VP(new float3(-0.866f, 0f, -0.5f)).x - 0.5f;
            Assert.AreEqual(-Focal * 1.732f, yaw60, 1e-3f, "yaw 60 offset = focal x tan(60)");

            Assert.Greater(math.abs(yaw60), math.abs(yaw45), "the cone slides outward");
            Assert.Greater(math.abs(yaw45), math.abs(centerOffset), "the cone starts at the center");

            // The floor caps the slide at Focal / MinAxial.
            Assert.LessOrEqual(math.abs(yaw60), Focal / MinAxial, "inside the floor");
        }

        [Test]
        public void ConvergeCone_MirrorsTheDivergeCone_AcrossNinety()
        {
            // A gaze past perpendicular converges into the point on the opposite
            // side: yaw 135 mirrors the yaw 45 answer and flips the sign, and
            // the point crosses the far side as the gaze passes 90 degrees.
            float2 diverge = VP(new float3(-0.7071f, 0f, -0.7071f));
            float2 converge = VP(new float3(-0.7071f, 0f, 0.7071f));
            Assert.AreEqual(0.5f - (diverge.x - 0.5f), converge.x, 1e-5f, "135 mirrors 45 about center");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(-0.7071f, 0f, -0.7071f), MinAxial), 1e-6f, "45 recedes");
            Assert.AreEqual(-1f, WarpFlowMath.AxialSign(new float3(-0.7071f, 0f, 0.7071f), MinAxial), 1e-6f, "135 approaches");

            // Inside the floor band the answer is the band edge: 89 and 91
            // both land the point far left on the travel side with +1, and the
            // crossing to a converging answer happens past the band edge, at
            // the gaze angle where the axial term passes -MinAxial.
            float2 yaw89 = VP(new float3(-0.9994f, 0f, -0.0175f));
            float2 yaw91 = VP(new float3(-0.9994f, 0f, 0.0175f));
            Assert.AreEqual(yaw89.x, yaw91.x, 1e-5f, "89 and 91 share the band edge answer");
            Assert.Less(yaw91.x, 0.5f, "the band answer sits on the travel side");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(-0.9994f, 0f, -0.0175f), MinAxial), 1e-6f, "89 streams away from its point");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(-0.9994f, 0f, 0.0175f), MinAxial), 1e-6f, "91 streams away from its point");

            // Past the band edge the point flips to the opposite side and the
            // sign answers -1: the flow direction stays continuous across the
            // crossing, away-from-point on the near side and into-point past it.
            float2 pastEdge = VP(new float3(-0.9816f, 0f, 0.191f));
            Assert.Greater(pastEdge.x, 0.5f, "past the band edge the point sits right of center");
            Assert.AreEqual(-1f, WarpFlowMath.AxialSign(new float3(-0.9816f, 0f, 0.191f), MinAxial), 1e-6f, "past the band edge the flow converges");
        }

        [Test]
        public void PerpendicularTie_AnswersAwayFromThePoint_OnFloatNoise()
        {
            // A live camera matrix lands the axial term on a float residue at
            // a perpendicular gaze. The band answers +1 (streams away from the
            // travel-side point, the lateral flow) on either side of zero, and
            // an approaching view past the band answers -1.
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(0f, 1f, 1e-7f), MinAxial), 1e-6f, "residue above zero");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(0f, 1f, -1e-7f), MinAxial), 1e-6f, "residue below zero");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(0f, 1f, 1e-3f), MinAxial), 1e-6f, "inside the band the flow still streams away");
            Assert.AreEqual(-1f, WarpFlowMath.AxialSign(new float3(0f, 1f, 0.2f), MinAxial), 1e-6f, "past the band the flow converges");
        }

        [Test]
        public void Nadir_VanishesFarUp_StreamingDown()
        {
            // Top-down: travel is the screen up direction, axial zero sits on the
            // signed floor, the point lands far above, and the flow streams away
            // from it, which is the content sliding down the view.
            float2 vp = VP(new float3(0f, 1f, 0f));
            Assert.AreEqual(0.5f, vp.x, 1e-5f, "x");
            Assert.Greater(vp.y, 2f, "far above the view");
            Assert.AreEqual(1f, WarpFlowMath.AxialSign(new float3(0f, 1f, 0f), MinAxial), 1e-6f, "streams away from the far point");
        }

        [Test]
        public void RayCount_HoldsDensityAtTheCenter_AndGrowsWithPointDistance()
        {
            float target = 16f;
            Assert.AreEqual(target, WarpFlowMath.RayCount(target, new float2(0.5f, 0.5f)), 1e-4f, "center density");

            float far = WarpFlowMath.RayCount(target, new float2(1.5f, 0.5f));
            Assert.Greater(far, target, "a far point needs more rays for the same density");
            Assert.AreEqual(target * 2f * math.PI * 1f, far, 1e-3f, "count scales with the point distance");
        }

        [Test]
        public void RayPath_CoversEveryScreenCorner_PlusMargin()
        {
            // The path is the farthest corner distance plus the margin, so a
            // head dies past the corner at every pose. The side view lands the
            // point far off screen and the far corner dominates: a fixed path
            // shorter than that corner distance puts every visible pixel past
            // the far fade, and the layer answers nothing — the fault the rule
            // exists to refuse.
            float margin = 0.15f;
            foreach (float2 vp in new[]
            {
                new float2(0.5f, 0.5f),
                new float2(-1.5f, 0.5f),   // yaw 90 side view
                new float2(0.5f, 2.5f),    // nadir, point far above
                new float2(-0.9142f, -0.9142f), // nadir yaw combo, point far below-left
                new float2(3f, 0f)         // off-axis: the far corner is not diagonal
            })
            {
                float path = WarpFlowMath.RayPath(vp, margin);
                foreach (int i in new[] { 0, 1, 2, 3 })
                {
                    var corner = new float2(i % 2, i / 2);
                    Assert.LessOrEqual(math.length(vp - corner), path - margin + 1e-5f,
                        $"corner {corner} for {vp} must sit inside the path");
                }
                Assert.AreEqual(margin, path - FarthestCornerDistance(vp), 1e-4f, $"margin for {vp}");
            }
        }

        private static float FarthestCornerDistance(float2 vp)
        {
            float far = 0f;
            foreach (int i in new[] { 0, 1, 2, 3 })
                far = math.max(far, math.length(vp - new float2(i % 2, i / 2)));
            return far;
        }

        [Test]
        public void RayPath_SideViewKeepsEveryVisiblePixelInsideTheFarFade()
        {
            // The reported fault, stated as a rule: at the side view the point
            // sits far off screen, and every screen corner must answer a
            // positive far fade so the streaks stay visible.
            float2 vp = new float2(-1.5f, 0.5f);
            float path = WarpFlowMath.RayPath(vp, 0.15f);
            float farFadeRate = 3f;
            foreach (int i in new[] { 0, 1, 2, 3 })
            {
                var corner = new float2(i % 2, i / 2);
                float fade = math.saturate((path - math.length(vp - corner)) * farFadeRate);
                Assert.Greater(fade, 0f, $"corner {corner} answers a visible fade, not zero");
            }
        }

        private static float Frac(float x)
        {
            return x - Mathf.Floor(x);
        }

        [Test]
        public void WarpResolution_DividesTheScreenByTheScale_InWholePixels()
        {
            var half = WarpFlowMath.WarpResolution(2704, 1436, 2);
            Assert.AreEqual(1352, half.x, "half width");
            Assert.AreEqual(718, half.y, "half height");

            var full = WarpFlowMath.WarpResolution(2704, 1436, 1);
            Assert.AreEqual(2704, full.x, "scale 1 keeps the screen");
            Assert.AreEqual(1436, full.y, "scale 1 keeps the screen");

            var quarter = WarpFlowMath.WarpResolution(2704, 1436, 4);
            Assert.AreEqual(676, quarter.x, "quarter width");
            Assert.AreEqual(359, quarter.y, "quarter height");

            var tiny = WarpFlowMath.WarpResolution(1, 1, 4);
            Assert.AreEqual(1, tiny.x, "floors at one pixel");
            Assert.AreEqual(1, tiny.y, "floors at one pixel");
        }

        [Test]
        public void StreakCount_CycleWrapDropIsWhole_TheSeamlessCondition()
        {
            // The cycle wrap drops the head by one path, and one path is the
            // count in wavelength units, so the streak coordinate drops by the
            // count. The pattern reads a frac over the coordinate, so the wrap
            // is seamless only when that drop is whole: frac(u - drop) must
            // answer u at every point of the streak. The reported fault - flow
            // for a few seconds, a kick backwards, repeating each cycle - is a
            // fractional count leaving the fraction as the remainder at each
            // wrap. This test states the condition the rig must answer at the
            // pose-table points.
            float margin = 0.15f;
            foreach (float2 vp in new[]
            {
                new float2(0.5f, 0.6732f),      // yaw 0
                new float2(-1.5f, 0.5f),        // yaw 90 side view
                new float2(-0.9142f, -0.9142f)  // nadir yaw combo
            })
            {
                float path = WarpFlowMath.RayPath(vp, margin);
                float density = WarpFlowMath.StreakCount(10f, path);
                Assert.AreEqual(0f, density - Mathf.Round(density), 0f, $"the wrap drop for {vp} is whole");
                foreach (float u in new[] { 0f, 0.13f, 0.5f, 0.87f })
                {
                    Assert.AreEqual(u, Frac(u - density), 1e-3f,
                        $"frac across the wrap drop for {vp} at u={u}");
                }
            }
        }

        [Test]
        public void StreakCount_HoldsOnScreenSpacing_AcrossPaths()        {
            // The on-screen wavelength is path / count = 1 / perUv at every
            // pose, so a longer path carries proportionally more streaks and
            // the spacing a reader sees never moves with the pose. The count
            // is whole, so the spacing carries the rounding residue: at most
            // half a wavelength in the count's terms.
            float perUv = 10f;
            foreach (float path in new[] { 0.9f, 0.95f, 1f, 2.7f })
            {
                float count = WarpFlowMath.StreakCount(perUv, path);
                Assert.AreEqual(count, Mathf.Round(count), 0f, $"count for path {path} is whole");
                Assert.GreaterOrEqual(count, 1f, $"count for path {path} carries a streak");
                Assert.GreaterOrEqual(0.9f / path, path / count - 1f / perUv, $"spacing keeps pace at path {path}");
                Assert.GreaterOrEqual(1.1f / path, 1f / perUv - path / count, $"spacing keeps pace at path {path}");
            }

            float nearCount = WarpFlowMath.StreakCount(perUv, 0.9f);
            float farCount = WarpFlowMath.StreakCount(perUv, 2.7f);
            Assert.AreEqual(0.9f / 2.7f, nearCount / farCount, 0.1f, "count scales with the path");

            Assert.GreaterOrEqual(WarpFlowMath.StreakCount(perUv, 0.01f), 1f, "a tiny path still carries a streak");
        }

        [Test]
        public void FocalZero_AnswersTheCenterInEveryPose()
        {
            foreach (float3 viewDir in new[]
            {
                new float3(0f, 0f, -1f),
                new float3(-0.7071f, 0f, -0.7071f),
                new float3(-1f, 0f, 0f),
                new float3(0f, 0f, 1f)
            })
            {
                float2 vp = WarpFlowMath.VanishingPoint(viewDir, 0f, MinAxial);
                Assert.AreEqual(0.5f, vp.x, 1e-5f, $"x for {viewDir}");
                Assert.AreEqual(0.5f, vp.y, 1e-5f, $"y for {viewDir}");
            }
        }

        // Flow rate and intensity: the streak speed reads the warp strength. The authored
        // Flow Rate is the rate at full intensity, and the rule is a linear scale.
        private const float AuthoredRate = 0.3f;

        [Test]
        public void FlowRate_HalfIntensity_AnswersHalfTheAuthoredRate()
        {
            Assert.AreEqual(0.15f, WarpFlowMath.FlowRate(AuthoredRate, 0.5f, 0f), 1e-6f, "half");
        }

        [Test]
        public void FlowRate_FullIntensity_AnswersTheAuthoredRate()
        {
            Assert.AreEqual(AuthoredRate, WarpFlowMath.FlowRate(AuthoredRate, 1f, 0f), 1e-6f, "full");
        }

        [Test]
        public void FlowRate_ZeroIntensity_StopsTheFlow_UnlessAFloorIsAuthored()
        {
            Assert.AreEqual(0f, WarpFlowMath.FlowRate(AuthoredRate, 0f, 0f), 1e-6f, "floor 0");
            Assert.AreEqual(0.075f, WarpFlowMath.FlowRate(AuthoredRate, 0f, 0.25f), 1e-6f, "floor 0.25");
        }

        [Test]
        public void FlowRate_IsLinearAcrossTheRange()
        {
            // One step of intensity buys one equal step of rate, so a curve that ramps
            // intensity reads as a ramp of speed and not as a curve of speed.
            float previous = WarpFlowMath.FlowRate(AuthoredRate, 0f, 0f);
            float step = WarpFlowMath.FlowRate(AuthoredRate, 0.2f, 0f) - previous;
            for (int i = 1; i <= 5; i++)
            {
                float rate = WarpFlowMath.FlowRate(AuthoredRate, i * 0.2f, 0f);
                Assert.AreEqual(step, rate - previous, 1e-6f, $"step {i}");
                previous = rate;
            }
            Assert.AreEqual(AuthoredRate, previous, 1e-6f, "lands on the authored rate");
        }

        [Test]
        public void FlowRate_OutOfRangeIntensity_ClampsToTheAuthoredRate()
        {
            Assert.AreEqual(AuthoredRate, WarpFlowMath.FlowRate(AuthoredRate, 2f, 0f), 1e-6f, "above 1");
            Assert.AreEqual(0f, WarpFlowMath.FlowRate(AuthoredRate, -1f, 0f), 1e-6f, "below 0");
        }

        [Test]
        public void FlowRate_FloorFractions_AreClampedAndLandBetween()
        {
            Assert.AreEqual(AuthoredRate, WarpFlowMath.FlowRate(AuthoredRate, 0f, 2f), 1e-6f, "floor above 1");
            Assert.AreEqual(0f, WarpFlowMath.FlowRate(AuthoredRate, 0f, -0.5f), 1e-6f, "floor below 0");
            for (int i = 0; i <= 10; i++)
            {
                float intensity = i * 0.1f;
                float rate = WarpFlowMath.FlowRate(AuthoredRate, intensity, 0.4f);
                Assert.GreaterOrEqual(rate, 0.12f, $"floor at intensity {intensity}");
                Assert.LessOrEqual(rate, AuthoredRate, $"cap at intensity {intensity}");
            }
        }

        [Test]
        public void FlowRate_RepeatedCalls_AnswerTheSameRate_AboutTheAuthoredValue()
        {
            // The authored rate is the input every time, so the scale cannot feed itself.
            // A caller that wrote the scaled value back as its authored value would walk
            // the flow to zero, and this is the line that says the rule does not ask the
            // caller to store anything.
            float first = WarpFlowMath.FlowRate(AuthoredRate, 0.5f, 0f);
            float second = WarpFlowMath.FlowRate(AuthoredRate, 0.5f, 0f);
            float third = WarpFlowMath.FlowRate(first, 0.5f, 0f);
            float fourth = WarpFlowMath.FlowRate(third, 0.5f, 0f);
            Assert.AreEqual(first, second, 1e-6f, "pure call");
            Assert.AreEqual(0.075f, third, 1e-6f, "a fed-back value scales again, which is the compounding shape to avoid");
            Assert.AreEqual(0.0375f, fourth, 1e-6f, "the fed-back chain walks the flow toward zero");
        }

        // The pose rule on the rate. The shader ramps the streak speed with the
        // distance from the vanishing point, so a view whose screen sits far from its
        // point reads faster at the same rate: the side view sweeps from 1.5 to 2.5
        // screen widths out, where a head-on view sweeps from 0 to 0.85. These tests
        // state that the rate carries that geometry and the screen-center speed does
        // not.
        [Test]
        public void ScreenSpread_ReportsTheDistanceFromTheScreenCenter_InScreenWidths()
        {
            Assert.AreEqual(0f, WarpFlowMath.ScreenSpread(new float2(0.5f, 0.5f)), 1e-6f, "the screen center sits at zero spread");
            Assert.AreEqual(0.5f, WarpFlowMath.ScreenSpread(new float2(1.0f, 0.5f)), 1e-6f, "the edge midpoint sits half a screen width out");
            Assert.AreEqual(0.7071068f, WarpFlowMath.ScreenSpread(new float2(1f, 1f)), 1e-6f, "the corner sits root-two halves out");
            Assert.AreEqual(2.0f, WarpFlowMath.ScreenSpread(new float2(-1.5f, 0.5f)), 1e-6f, "the side view's point sits two screen widths off center");
        }

        [Test]
        public void ScreenSpeedDenominator_AddsTheSpreadOverTheRampSpan_ToThePath()
        {
            // The side view: path 2.70, the point two screen widths off center, the
            // authored acceleration 1, so the span is e - 1 = 1.71828.
            Assert.AreEqual(2.700f + 2.0f * (math.E - 1.0),
                WarpFlowMath.ScreenSpeedDenominator(new float2(-1.5f, 0.5f), 2.700f, 1f), 1e-5f,
                "the spread term rides the ramp span");
            Assert.AreEqual(0.989f + 0.173f * (math.E - 1.0),
                WarpFlowMath.ScreenSpeedDenominator(new float2(0.5f, 0.673f), 0.989f, 1f), 1e-5f,
                "the head-on view carries its short spread");
        }

        [Test]
        public void ScreenSpeedDenominator_FallsToThePath_WhenTheScreenSitsOnThePointOrTheRampIsFlat()
        {
            Assert.AreEqual(1.25f, WarpFlowMath.ScreenSpeedDenominator(new float2(0.5f, 0.5f), 1.25f, 1f), 1e-6f,
                "a point at the screen center adds no spread term");
            Assert.AreEqual(2.700f, WarpFlowMath.ScreenSpeedDenominator(new float2(-1.5f, 0.5f), 2.700f, 0f), 1e-6f,
                "a flat ramp leaves the path alone");
            Assert.AreEqual(0.01f, WarpFlowMath.ScreenSpeedDenominator(new float2(0.5f, 0.5f), 0f, 4f), 1e-6f,
                "a zero path at the screen center floors at the floor the rate divides by");
        }

        [Test]
        public void SpeedRate_MatchesTheIntensityRate_WhenThePoseSitsAtTheReferenceView()
        {
            const float reference = 1.2863f;
            for (int i = 0; i <= 5; i++)
            {
                float intensity = i * 0.2f;
                Assert.AreEqual(
                    WarpFlowMath.FlowRate(AuthoredRate, intensity, 0.25f),
                    WarpFlowMath.SpeedRate(AuthoredRate, intensity, 0.25f, reference, reference),
                    1e-6f, $"intensity {intensity} at the reference view");
            }
        }

        [Test]
        public void SpeedRate_ScalesByTheDenominatorRatio_WhenThePoseSitsFartherOut()
        {
            const float reference = 1.2863f;
            const float sideDenominator = 6.1366f;
            float expected = AuthoredRate * reference / sideDenominator;
            Assert.AreEqual(expected, WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, reference, sideDenominator), 1e-6f,
                "full intensity pays the ratio");
            Assert.AreEqual(expected * 0.5f, WarpFlowMath.SpeedRate(AuthoredRate, 0.5f, 0f, reference, sideDenominator), 1e-6f,
                "the intensity scale rides the same number");
            Assert.AreEqual(0.25f * AuthoredRate * reference / sideDenominator,
                WarpFlowMath.SpeedRate(AuthoredRate, 0f, 0.25f, reference, sideDenominator), 1e-6f,
                "the floor rides the same number");
            Assert.AreEqual(0f, WarpFlowMath.SpeedRate(AuthoredRate, 0f, 0f, reference, sideDenominator), 1e-6f,
                "a stopped warp stays stopped at any pose");
        }

        [Test]
        public void SpeedRate_NeverRunsAPoseFasterThanTheAuthoredRate()
        {
            const float reference = 1.2863f;
            foreach (float denominator in new[] { 0.5f, 0.857f, 1.2f })
            {
                Assert.AreEqual(AuthoredRate, WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, reference, denominator), 1e-6f,
                    $"denominator {denominator} under the reference keeps the authored rate");
            }
            Assert.LessOrEqual(WarpFlowMath.SpeedRate(AuthoredRate, 2f, 1f, reference, 6.1366f), AuthoredRate,
                "over-range inputs stay under the authored rate");
        }

        [Test]
        public void SpeedRate_HoldsOneScreenCenterSpeedAcrossThePoseTable()
        {
            // The center pixel's speed is the rate times the derivative of the pattern
            // advance at the center: rate x accel x denominator / (e^accel - 1). The
            // rule divides the rate by the denominator, so that product is one number
            // for every pose the rule scales, and the cycle takes up the slack.
            const float accel = 1f;
            const float span = math.E - 1.0f;
            float margin = 0.15f;
            var poses = new[]
            {
                new float3(0f, 0f, -1f),                     // gaze along travel, level
                new float3(0f, 0.5f, -0.8660254f),           // gaze along travel, pitch 30 (the reference view)
                new float3(-0.7071068f, 0.3535534f, -0.6123724f), // yaw 45, pitch 30
                new float3(-1f, 0f, 0f),                     // side view
                new float3(1f, 0f, 0f),                      // mirrored side view
                new float3(0f, 1f, 0f)                       // nadir
            };
            float2 refPoint = VP(new float3(0f, 0.5f, -0.8660254f));
            float referenceDenominator = WarpFlowMath.ScreenSpeedDenominator(refPoint, WarpFlowMath.RayPath(refPoint, margin), accel);
            float centerSpeed = AuthoredRate * accel * referenceDenominator / span;

            for (int i = 0; i < poses.Length; i++)
            {
                float2 vp = VP(poses[i]);
                float path = WarpFlowMath.RayPath(vp, margin);
                float denominator = WarpFlowMath.ScreenSpeedDenominator(vp, path, accel);
                float rate = WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, referenceDenominator, denominator);
                float speed = rate * accel * denominator / span;
                if (denominator >= referenceDenominator)
                {
                    Assert.AreEqual(centerSpeed, speed, 1e-4f, $"pose {poses[i]} runs the reference center speed");
                    Assert.AreEqual(denominator / (AuthoredRate * referenceDenominator), 1f / rate, 1e-2f,
                        $"pose {poses[i]} spends its extra denominator on the cycle length");
                    Assert.AreEqual(span / (AuthoredRate * accel * referenceDenominator), 1f / speed, 1e-2f,
                        $"pose {poses[i]} spends it on the screen cycle too");
                }
                else
                {
                    Assert.AreEqual(AuthoredRate, rate, 1e-6f, $"pose {poses[i]} keeps the authored rate");
                    Assert.Less(speed, centerSpeed + 1e-4f, $"pose {poses[i]} reads slower at the center, and never faster");
                }
            }
        }

        [Test]
        public void SpeedRate_ScalesTheRateAndKeepsThePathThatTheFarFadeNeeds()
        {
            // The ray path does two jobs: it reaches the farthest screen corner so the
            // far fade covers the view, and it carries the cycle length. The rule scales
            // the rate and leaves the path whole, so the coverage rule keeps standing.
            float2 vp = new float2(-1.5f, 0.5f);   // side view
            float path = WarpFlowMath.RayPath(vp, 0.15f);
            float denominator = WarpFlowMath.ScreenSpeedDenominator(vp, path, 1f);
            float farFadeRate = 3f;
            foreach (int i in new[] { 0, 1, 2, 3 })
            {
                var corner = new float2(i % 2, i / 2);
                float fade = math.saturate((path - math.length(vp - corner)) * farFadeRate);
                Assert.Greater(fade, 0f, $"corner {corner} stays inside the far fade at the scaled rate");
            }
            Assert.Greater(WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, 1.2863f, denominator), 0f, "the pose still runs");
            Assert.AreEqual(path, WarpFlowMath.RayPath(vp, 0.15f), 0f, "the path the fade reads is the path the rate divides by");
        }

        // The zoom mapping. The layer scales the field about the screen center by the
        // viewport's UV zoom level, and moves no offset, so a zoomed view reads the
        // authored field larger. These tests state the scale, the center anchor, the
        // round trip, and the floor the mapping divides by.
        [Test]
        public void ZoomLevel_LeavesTheAuthoredLevel_StandingOverTheFloor()
        {
            foreach (float level in new[] { 1f, 2f, 4f, 8.5f })
            {
                Assert.AreEqual(level, WarpFlowMath.ZoomLevel(level, 0.01f), 1e-6f, $"level {level} stands");
            }
        }

        [Test]
        public void ZoomLevel_FloorsTheLevel_BecauseTheMappingDividesByIt()
        {
            Assert.AreEqual(0.01f, WarpFlowMath.ZoomLevel(0f, 0.01f), 1e-6f, "a collapsed level lands on the floor");
            Assert.AreEqual(0.01f, WarpFlowMath.ZoomLevel(-3f, 0.01f), 1e-6f, "a negative level lands on the floor");
            Assert.AreEqual(0.5f, WarpFlowMath.ZoomLevel(0.2f, 0.5f), 1e-6f, "the authored floor wins over a lower level");
            Assert.AreEqual(0.01f, WarpFlowMath.ZoomLevel(0f, 0f), 1e-6f, "the mapping keeps its own floor under any authored floor");
        }

        [Test]
        public void FieldUv_IsTheIdentity_AtZoomOne_AndHoldsTheCenterAtEveryZoom()
        {
            foreach (var uv in new[] { new float2(0f, 0f), new float2(0.5f, 0.5f), new float2(1f, 0f), new float2(0.313f, 0.777f) })
            {
                float2 atOne = WarpFlowMath.FieldUv(uv, 1f);
                Assert.AreEqual(uv.x, atOne.x, 1e-6f, $"uv {uv} x at zoom 1");
                Assert.AreEqual(uv.y, atOne.y, 1e-6f, $"uv {uv} y at zoom 1");
            }
            foreach (float zoom in new[] { 1f, 2f, 4f, 8f, 0.01f })
            {
                float2 center = WarpFlowMath.FieldUv(new float2(0.5f, 0.5f), zoom);
                Assert.AreEqual(0.5f, center.x, 1e-6f, $"the center x maps to the center at zoom {zoom}: the mapping scales, it moves no offset");
                Assert.AreEqual(0.5f, center.y, 1e-6f, $"the center y maps to the center at zoom {zoom}");
            }
        }

        [Test]
        public void FieldUv_ScalesTheOffsetByTheZoomLevel_AndTheScreenMappingUndoesIt()
        {
            var uv = new float2(0.75f, 0.25f);   // a quarter screen right, a quarter down
            foreach (float zoom in new[] { 1f, 2f, 4f, 8f })
            {
                float2 field = WarpFlowMath.FieldUv(uv, zoom);
                Assert.AreEqual(0.25f / zoom, field.x - 0.5f, 1e-6f, $"the right offset divides by the zoom at {zoom}");
                Assert.AreEqual(-0.25f / zoom, field.y - 0.5f, 1e-6f, $"the down offset divides by the zoom at {zoom}");
                float2 back = WarpFlowMath.ScreenUv(field, zoom);
                Assert.AreEqual(uv.x, back.x, 1e-6f, $"the pair round-trips the x at zoom {zoom}");
                Assert.AreEqual(uv.y, back.y, 1e-6f, $"the pair round-trips the y at zoom {zoom}");
            }
        }

        [Test]
        public void ScreenUv_RunsTheVanishingPointOutward_WhenTheViewZoomsIn()
        {
            // The head-on view at pitch 30: the point sits 0.173 above center. A
            // narrower window over the same field puts it at twice the offset at zoom
            // 2, and off the top of the screen at zoom 4.
            var point = new float2(0.5f, 0.673f);
            float2 zoom2 = WarpFlowMath.ScreenUv(point, 2f);
            Assert.AreEqual(0.5f, zoom2.x, 1e-3f, "zoom 2 moves the point sideways not at all");
            Assert.AreEqual(0.846f, zoom2.y, 1e-3f, "zoom 2 doubles the offset");
            float2 zoom4 = WarpFlowMath.ScreenUv(point, 4f);
            Assert.AreEqual(0.5f, zoom4.x, 1e-3f, "zoom 4 moves the point sideways not at all");
            Assert.AreEqual(1.192f, zoom4.y, 1e-3f, "zoom 4 puts the point off the top edge");
            Assert.Greater(zoom4.y, 1f, "the point sits past the top edge of the visible screen at zoom 4");
            Assert.Less(WarpFlowMath.ScreenUv(point, 0.5f).y, 0.673f, "a zoom-out view pulls the point back toward the center");
        }

        [Test]
        public void FieldUv_AnswersFiniteNumbers_WhenTheLevelCollapsesToTheFloor()
        {
            foreach (float zoom in new[] { 0f, -1f, 0.01f })
            {
                float2 field = WarpFlowMath.FieldUv(new float2(1f, 1f), zoom);
                Assert.True(math.isfinite(field.x) && math.isfinite(field.y), $"finite field uv at zoom {zoom}");
                Assert.Greater(field.x, 0f, $"the corner maps outward and not to zero at zoom {zoom}");
            }
        }

        // The flow clock. The shader reads an accumulated clock, so the streak
        // direction cannot depend on how the rate is changing.
        [Test]
        public void FlowStep_RateTimesElapsed_WithTheElapsedCapped()
        {
            Assert.AreEqual(0.008f, WarpFlowMath.FlowStep(0.8f, 0.01f, 0.1f), 1e-6f, "one frame at full rate");
            Assert.AreEqual(0.04f, WarpFlowMath.FlowStep(0.4f, 0.1f, 0.1f), 1e-6f, "half rate");
            Assert.AreEqual(0.08f, WarpFlowMath.FlowStep(0.8f, 3f, 0.1f), 1e-6f, "a stalled frame pays the cap");
            Assert.AreEqual(0f, WarpFlowMath.FlowStep(0.8f, -1f, 0.1f), 1e-6f, "a repeated clock advances nothing");
            Assert.AreEqual(0f, WarpFlowMath.FlowStep(-1f, 0.01f, 0.1f), 1e-6f, "a negative rate advances nothing");
        }

        [Test]
        public void AdvancePhase_AccumulatesTheRateAndStaysInUnitRange()
        {
            float phase = 0f;
            for (int i = 0; i < 200; i++)
            {
                phase = WarpFlowMath.AdvancePhase(phase, 0.8f, 0.01f, 0.1f);
            }
            // 200 frames of 0.01 s at rate 0.8 is 1.6 cycles, so the clock lands on 0.6.
            Assert.AreEqual(0.6f, phase, 1e-4f, "accumulated cycles");
            Assert.GreaterOrEqual(phase, 0f, "lower bound");
            Assert.Less(phase, 1f, "upper bound");
        }

        [Test]
        public void AdvancePhase_SteppingAndOneBigStep_AgreeModOneCycle()
        {
            float stepped = 0f;
            for (int i = 0; i < 37; i++)
            {
                stepped = WarpFlowMath.AdvancePhase(stepped, 0.5f, 1f / 60f, 0.1f);
            }
            // The cap must sit above the whole span, or the big step pays its cap and the
            // two paths measure different amounts of time.
            float oneBig = WarpFlowMath.AdvancePhase(0f, 0.5f, 37f / 60f, 1f);
            Assert.AreEqual(37f / 60f * 0.5f, oneBig, 1e-4f, "the big step measured the span");
            Assert.AreEqual(oneBig, stepped, 1e-4f, "the clock is the integral of the rate");
        }

        [Test]
        public void AdvancePhase_FallingRate_NeverTurnsTheClockBack()
        {
            // The shape a warp exit produces: the rate drops frame after frame while the
            // wall clock keeps growing. A clock built from rate x wall time moves back
            // here, and the streaks run in reverse. The accumulated clock only ever moves
            // forward, at the rate of the frame.
            float phase = 0.4f;
            float previous = phase;
            float[] rates = { 0.8f, 0.6f, 0.4f, 0.2f, 0.1f, 0f };
            foreach (float rate in rates)
            {
                for (int i = 0; i < 10; i++)
                {
                    phase = WarpFlowMath.AdvancePhase(phase, rate, 1f / 60f, 0.1f);
                    float forward = phase - previous;
                    if (forward < -0.5f) { forward += 1f; }   // the wrap reads as forward motion
                    Assert.GreaterOrEqual(forward, -1e-6f, $"falling rate {rate} frame {i} turned the clock");
                    previous = phase;
                }
            }
        }

        [Test]
        public void AdvancePhase_ZeroRate_HoldsTheClockInPlace()
        {
            Assert.AreEqual(0.35f, WarpFlowMath.AdvancePhase(0.35f, 0f, 1f / 60f, 0.1f), 1e-6f, "stopped flow");
            Assert.AreEqual(0.35f, WarpFlowMath.AdvancePhase(0.35f, 0.8f, 0f, 0.1f), 1e-6f, "no elapsed time");
        }

        [Test]
        public void AdvancePhase_RepeatedFrameStamp_AdvancesNothing()
        {
            // The rig stamps the frame, so a caller that calls SetFlow twice in one frame
            // hands the same elapsed time and the clock stands where it was.
            float first = WarpFlowMath.AdvancePhase(0.2f, 0.8f, 0f, 0.1f);
            float second = WarpFlowMath.AdvancePhase(first, 0.8f, 0f, 0.1f);
            Assert.AreEqual(0.2f, first, 1e-6f, "same stamp");
            Assert.AreEqual(first, second, 1e-6f, "repeat call");
        }
    }
}
