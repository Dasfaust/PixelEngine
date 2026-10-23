using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// The bow cap geometry and the plume's intensity scale.
    ///
    /// The rules under test are the ones a look pass cannot state: the arc reaches the authored radius, a
    /// sweep wider than a half circle wraps past the sides and behind them, the tail row sits downstream from
    /// the cap row by the authored length, the lift raises the whole cap, the normals point out of the arc's
    /// axis (which is what makes the front the hot band), the segment count writes the vertex list it claims,
    /// a rebuild on a standing mesh replaces the mesh, and a zero authored size answers the floor and not a
    /// degenerate mesh.
    /// </summary>
    public class WarpPlumeMathTests
    {
        private Mesh mesh;

        [SetUp]
        public void SetUp()
        {
            mesh = new Mesh();
        }

        [TearDown]
        public void TearDown()
        {
            if (mesh != null)
            {
                Object.DestroyImmediate(mesh);
                mesh = null;
            }
        }

        [Test]
        public void PlateScale_IsLinearInIntensityAndClampedToTheAuthoredRange()
        {
            Assert.AreEqual(0f, WarpPlumeMath.PlateScale(0f), 1e-6f, "no warp is no sheath");
            Assert.AreEqual(0.25f, WarpPlumeMath.PlateScale(0.25f), 1e-6f);
            Assert.AreEqual(0.5f, WarpPlumeMath.PlateScale(0.5f), 1e-6f, "half warp is half the sheath");
            Assert.AreEqual(1f, WarpPlumeMath.PlateScale(1f), 1e-6f);
            Assert.AreEqual(1f, WarpPlumeMath.PlateScale(3f), 1e-6f, "the intensity is authored 0..1");
            Assert.AreEqual(0f, WarpPlumeMath.PlateScale(-2f), 1e-6f);
        }

        [Test]
        public void BuildCap_AHalfCircleArcReachesTheAuthoredRadiusAndFrontsAtTheCenter()
        {
            WarpPlumeMath.BuildCap(mesh, 4f, 180f, 1f, 0f, 20);

            Assert.AreEqual(-4f, mesh.bounds.min.x, 1e-4f, "the arc ends reach the authored radius");
            Assert.AreEqual(4f, mesh.bounds.max.x, 1e-4f);
            Assert.AreEqual(4f, mesh.bounds.max.z, 1e-4f, "the front of the cap is one radius ahead");
        }

        [Test]
        public void BuildCap_ASweepWiderThanAHalfCircleWrapsBehindTheSides()
        {
            WarpPlumeMath.BuildCap(mesh, 4f, 240f, 1f, 0f, 20);

            // The sweep ends sit at 120 degrees from the front, where the cos is -0.5: two units behind the
            // axis, and the tail row runs one unit further downstream from there.
            Assert.AreEqual(-3f, mesh.bounds.min.z, 1e-3f, "the wrap ends reach behind the axis");
            Assert.Less(mesh.bounds.min.z, 0f);
        }

        [Test]
        public void BuildCap_TheTailRowSitsDownstreamFromTheCapRowByTheAuthoredLength()
        {
            WarpPlumeMath.BuildCap(mesh, 3f, 120f, 5f, 0f, 8);

            var verts = mesh.vertices;
            var uv = mesh.uv;
            Assert.AreEqual(verts.Length, uv.Length);
            for (int i = 0; i < verts.Length; i += 2)
            {
                Assert.AreEqual(0f, uv[i].y, 1e-5f, "the first vertex of a pair is the cap row");
                Assert.AreEqual(1f, uv[i + 1].y, 1e-5f, "the second vertex of a pair is the tail row");
                Assert.AreEqual(verts[i].x, verts[i + 1].x, 1e-5f, "the tail runs straight downstream");
                Assert.AreEqual(verts[i].y, verts[i + 1].y, 1e-5f);
                Assert.AreEqual(verts[i].z - 5f, verts[i + 1].z, 1e-4f, "the tail is one authored length downstream");
            }
        }

        [Test]
        public void BuildCap_TheLiftRaisesTheWholeCap()
        {
            WarpPlumeMath.BuildCap(mesh, 3f, 180f, 2f, 4f, 12);

            Assert.AreEqual(4f, mesh.bounds.min.y, 1e-4f);
            Assert.AreEqual(4f, mesh.bounds.max.y, 1e-4f, "a flat ribbon at the authored height has no height of its own");
        }

        [Test]
        public void BuildCap_TheNormalsPointOutOfTheArcAxis()
        {
            WarpPlumeMath.BuildCap(mesh, 2f, 180f, 1f, 0f, 4);

            var verts = mesh.vertices;
            var normals = mesh.normals;
            int front = FrontIndex(verts);
            Assert.AreEqual(0f, normals[front].x, 1e-4f, "the front of the cap points along the mesh's +Z");
            Assert.AreEqual(0f, normals[front].y, 1e-4f);
            Assert.AreEqual(1f, normals[front].z, 1e-4f);

            int wrapEnd = verts.Length - 2;
            Assert.AreEqual(1f, normals[wrapEnd].x, 1e-3f, "a sweep end at 90 degrees points out on +X");
            Assert.AreEqual(0f, normals[wrapEnd].z, 1e-3f);
        }

        [Test]
        public void BuildCap_TheSegmentCountWritesTheVertexListItClaims()
        {
            WarpPlumeMath.BuildCap(mesh, 2f, 200f, 1f, 0f, 20);

            Assert.AreEqual(42, mesh.vertexCount, "one row pair per arc step");
            Assert.AreEqual(120, mesh.triangles.Length, "one quad per arc step");
        }

        [Test]
        public void BuildCap_ARebuildOnAStandingMeshReplacesTheMesh()
        {
            WarpPlumeMath.BuildCap(mesh, 4f, 180f, 1f, 0f, 8);
            Assert.AreEqual(18, mesh.vertexCount);

            WarpPlumeMath.BuildCap(mesh, 2f, 300f, 3f, 0f, 20);

            Assert.AreEqual(42, mesh.vertexCount, "a rebuild replaces the vertex list and does not append");
            Assert.Greater(mesh.triangles.Length, 0);
            // A 300-degree sweep spans 150 degrees each side of the front, so it crosses the 90-degree
            // points and reaches the new radius on both sides.
            Assert.AreEqual(2f, mesh.bounds.max.x, 1e-4f, "the new arc answers the new radius");
            Assert.AreEqual(-2f, mesh.bounds.min.x, 1e-4f);
            // The wrap ends of a 300-degree sweep sit at 150 degrees from the front, where the cos is
            // -0.866, and the tail row runs the authored length downstream from there.
            Assert.AreEqual(2f * -0.8660254f - 3f, mesh.bounds.min.z, 1e-3f,
                "the wrap ends plus the authored tail set the downstream reach");
        }

        [Test]
        public void BuildCap_AuthoredZeroSizeAnswersTheFloorAndNotADegenerateMesh()
        {
            WarpPlumeMath.BuildCap(mesh, 0f, 0f, 0f, 0f, 0);

            Assert.Greater(mesh.bounds.size.x, 0f, "a zero-area cap draws nothing");
            Assert.Greater(mesh.bounds.size.z, 0f);
            Assert.AreEqual(4, mesh.vertexCount, "the segment floor is one arc step");
        }

        [Test]
        public void BuildCap_AuthoredSweepIsClampedToTheCircle()
        {
            WarpPlumeMath.BuildCap(mesh, 3f, 540f, 1f, 0f, 24);

            // A 360-degree sweep closes the arc on itself: the front and the ends coincide, so the cap
            // spans the diameter on x and the radius plus the tail on z.
            Assert.AreEqual(-3f, mesh.bounds.min.x, 1e-3f);
            Assert.AreEqual(3f, mesh.bounds.max.x, 1e-3f);
            Assert.AreEqual(3f, mesh.bounds.max.z, 1e-3f);
            Assert.AreEqual(-4f, mesh.bounds.min.z, 1e-3f, "the tail runs one authored length downstream from the far end of the circle");
        }

        private static int FrontIndex(IList<Vector3> verts)
        {
            int best = 0;
            for (int i = 0; i < verts.Count; i += 2)
            {
                if (verts[i].z > verts[best].z)
                {
                    best = i;
                }
            }
            return best;
        }
    }
}
