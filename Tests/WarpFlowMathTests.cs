using NUnit.Framework;
using Unity.Mathematics;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// Flow-field math for the warp streak layer. The fixture states the table
    /// rules the plan authors: gaze along travel vanishes at the view center,
    /// gaze against travel converges there, the side views land mirrored far
    /// off screen as parallel flow, the converge cone mirrors the diverge cone
    /// across 90 degrees, the ray count holds the authored density as the
    /// point moves, and a perpendicular gaze answers the tie deterministically.
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
    }
}
