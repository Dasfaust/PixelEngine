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

        // The pose rule on the rate. The shader advances the streak pattern by one ray
        // path per cycle, and the cycle is one over the rate, so the screen sweep is
        // path x rate. The path grows with the vanishing point's distance off screen,
        // because it must reach the farthest screen corner, so a side view runs a path
        // about 2.7 times the head-on path. These tests state that the rate carries
        // that growth and the sweep does not.
        [Test]
        public void SpeedRate_MatchesTheIntensityRate_WhenThePathEqualsTheReference()
        {
            float path = 0.989f;
            for (int i = 0; i <= 5; i++)
            {
                float intensity = i * 0.2f;
                Assert.AreEqual(
                    WarpFlowMath.FlowRate(AuthoredRate, intensity, 0.25f),
                    WarpFlowMath.SpeedRate(AuthoredRate, intensity, 0.25f, path, path),
                    1e-6f, $"intensity {intensity} at the reference path");
            }
        }

        [Test]
        public void SpeedRate_ScalesByThePathRatio_WhenThePathRunsLonger()
        {
            const float reference = 0.989f;
            const float sidePath = 2.700f;
            float expected = AuthoredRate * reference / sidePath;
            Assert.AreEqual(expected, WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, reference, sidePath), 1e-6f,
                "full intensity pays the ratio");
            Assert.AreEqual(expected * 0.5f, WarpFlowMath.SpeedRate(AuthoredRate, 0.5f, 0f, reference, sidePath), 1e-6f,
                "the intensity scale rides the same number");
            Assert.AreEqual(0.25f * AuthoredRate * reference / sidePath,
                WarpFlowMath.SpeedRate(AuthoredRate, 0f, 0.25f, reference, sidePath), 1e-6f,
                "the floor rides the same number");
            Assert.AreEqual(0f, WarpFlowMath.SpeedRate(AuthoredRate, 0f, 0f, reference, sidePath), 1e-6f,
                "a stopped warp stays stopped at any pose");
        }

        [Test]
        public void SpeedRate_NeverRunsAPoseFasterThanTheAuthoredRate()
        {
            const float reference = 0.989f;
            foreach (float path in new[] { 0.5f, 0.857f, 0.988f })
            {
                Assert.AreEqual(AuthoredRate, WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, reference, path), 1e-6f,
                    $"path {path} shorter than the reference keeps the authored rate");
            }
            Assert.LessOrEqual(WarpFlowMath.SpeedRate(AuthoredRate, 2f, 1f, reference, 2.7f), AuthoredRate, "over-range inputs stay under the authored rate");
        }

        [Test]
        public void SpeedRate_HoldsOneScreenSweepAcrossThePoseTable()
        {
            // The poses the layer runs at, read through the rig's own math.
            float margin = 0.15f;
            var poses = new[]
            {
                new float3(0f, 0f, -1f),                     // gaze along travel, level
                new float3(0f, 0.5f, -0.8660254f),           // gaze along travel, pitch 30
                new float3(-0.7071068f, 0.3535534f, -0.6123724f), // yaw 45, pitch 30
                new float3(-1f, 0f, 0f),                     // side view
                new float3(1f, 0f, 0f),                      // mirrored side view
                new float3(0f, 1f, 0f)                       // nadir
            };
            var paths = new float[poses.Length];
            for (int i = 0; i < poses.Length; i++)
            {
                paths[i] = WarpFlowMath.RayPath(VP(poses[i]), margin);
            }
            float reference = WarpFlowMath.RayPath(VP(new float3(0f, 0.5f, -0.8660254f)), margin);

            for (int i = 0; i < poses.Length; i++)
            {
                float rate = WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, reference, paths[i]);
                float sweep = paths[i] * rate;
                float cycle = 1f / rate;
                if (paths[i] >= reference)
                {
                    Assert.AreEqual(reference * AuthoredRate, sweep, 1e-4f, $"pose {poses[i]} sweeps the reference distance per second");
                    Assert.AreEqual(paths[i] / (reference * AuthoredRate), cycle, 1e-2f, $"pose {poses[i]} spends its extra path on the cycle");
                }
                else
                {
                    Assert.AreEqual(AuthoredRate, rate, 1e-6f, $"pose {poses[i]} keeps the authored rate");
                    Assert.Less(sweep, reference * AuthoredRate + 1e-4f, $"pose {poses[i]} sweeps less, and never more");
                }
            }
        }

        [Test]
        public void SpeedRate_ScalesTheRateAndKeepsThePathThatTheFarFadeNeeds()
        {
            // The ray path does two jobs: it reaches the farthest screen corner so the
            // far fade covers the view, and it sets the cycle length. The rule scales
            // the rate and leaves the path whole, so the coverage rule keeps standing.
            float2 vp = new float2(-1.5f, 0.5f);   // side view
            float path = WarpFlowMath.RayPath(vp, 0.15f);
            float farFadeRate = 3f;
            foreach (int i in new[] { 0, 1, 2, 3 })
            {
                var corner = new float2(i % 2, i / 2);
                float fade = math.saturate((path - math.length(vp - corner)) * farFadeRate);
                Assert.Greater(fade, 0f, $"corner {corner} stays inside the far fade at the scaled rate");
            }
            Assert.Greater(WarpFlowMath.SpeedRate(AuthoredRate, 1f, 0f, 0.989f, path), 0f, "the pose still runs");
            Assert.AreEqual(path, WarpFlowMath.RayPath(vp, 0.15f), 0f, "the path the fade reads is the path the rate divides by");
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
