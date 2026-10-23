using NUnit.Framework;
using UnityEngine;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// Plate geometry and the intensity scale for the bow plume. The rules under test are the ones a look
    /// pass cannot see: the scale is linear in the warp intensity and clamped to the authored range, the
    /// nose of the plate sits at zero so a shader reads uv.y as distance downstream from the burning face,
    /// the plate centres on its own x, a rebuild on a standing mesh writes the new size and not the old one
    /// with a new vertex list appended, and a zero authored size answers the floor and not a degenerate mesh.
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
            Assert.AreEqual(0f, WarpPlumeMath.PlateScale(0f), 1e-6f, "no warp is no plate");
            Assert.AreEqual(0.25f, WarpPlumeMath.PlateScale(0.25f), 1e-6f);
            Assert.AreEqual(0.5f, WarpPlumeMath.PlateScale(0.5f), 1e-6f, "half warp is half the plate");
            Assert.AreEqual(1f, WarpPlumeMath.PlateScale(1f), 1e-6f);
            Assert.AreEqual(1f, WarpPlumeMath.PlateScale(3f), 1e-6f, "the intensity is authored 0..1");
            Assert.AreEqual(0f, WarpPlumeMath.PlateScale(-2f), 1e-6f);
        }

        [Test]
        public void BuildPlate_PutsTheNoseAtZeroAndTheTailAtTheAuthoredLength()
        {
            WarpPlumeMath.BuildPlate(mesh, 2f, 6f);

            Assert.AreEqual(4, mesh.vertexCount);
            var uv = new Vector2[4];
            mesh.uv.CopyTo(uv, 0);
            Assert.AreEqual(2, CountAt(uv, 0f), "two corners sit at the nose");
            Assert.AreEqual(2, CountAt(uv, 1f), "two corners sit at the tail");

            Assert.AreEqual(0f, mesh.bounds.min.y, 1e-5f, "the nose corner is the plate's origin");
            Assert.AreEqual(6f, mesh.bounds.max.y, 1e-5f, "the tail reaches the authored length");
        }

        [Test]
        public void BuildPlate_CentresTheWidthOnItsOwnOrigin()
        {
            WarpPlumeMath.BuildPlate(mesh, 3f, 1f);

            Assert.AreEqual(-1.5f, mesh.bounds.min.x, 1e-5f);
            Assert.AreEqual(1.5f, mesh.bounds.max.x, 1e-5f);
            Assert.AreEqual(0f, mesh.bounds.center.x, 1e-5f);
        }

        [Test]
        public void BuildPlate_FillsTheWholeWidthAndSixIndices()
        {
            WarpPlumeMath.BuildPlate(mesh, 4f, 2f);

            var uv = new Vector2[4];
            mesh.uv.CopyTo(uv, 0);
            Assert.AreEqual(0f, uv[0].x, 1e-6f);
            Assert.AreEqual(1f, uv[1].x, 1e-6f);
            Assert.AreEqual(0f, uv[2].x, 1e-6f);
            Assert.AreEqual(1f, uv[3].x, 1e-6f);

            Assert.AreEqual(6, mesh.triangles.Length, "one quad is two triangles");
            Assert.AreEqual(4f, mesh.bounds.size.x, 1e-5f);
            Assert.AreEqual(2f, mesh.bounds.size.y, 1e-5f);
        }

        [Test]
        public void BuildPlate_RebuildOnAStandingMeshWritesTheNewSize()
        {
            WarpPlumeMath.BuildPlate(mesh, 2f, 1f);
            Assert.AreEqual(2f, mesh.bounds.size.x, 1e-5f);

            WarpPlumeMath.BuildPlate(mesh, 8f, 5f);

            Assert.AreEqual(4, mesh.vertexCount, "a rebuild replaces the vertex list and does not append");
            Assert.AreEqual(8f, mesh.bounds.size.x, 1e-5f);
            Assert.AreEqual(5f, mesh.bounds.size.y, 1e-5f);
        }

        [Test]
        public void BuildPlate_AnAuthoredZeroSizeAnswersTheFloorAndNotADegenerateMesh()
        {
            WarpPlumeMath.BuildPlate(mesh, 0f, 0f);

            Assert.Greater(mesh.bounds.size.x, 0f, "a zero-area plate draws nothing");
            Assert.Greater(mesh.bounds.size.y, 0f);
        }

        private static int CountAt(Vector2[] uv, float y)
        {
            int count = 0;
            foreach (var v in uv)
            {
                if (Mathf.Abs(v.y - y) < 1e-5f)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
