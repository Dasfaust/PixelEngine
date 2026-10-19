using NUnit.Framework;
using PixelEngine.Presentation;
using UnityEngine;

namespace PixelEngine.Tests
{
    /// <summary>
    /// Layout math for the pose-addressed atlas background. The atlas fixture is a
    /// 2048 x 288 grid: 8 yaw columns by 2 rows, origin bottom-left, so one cell
    /// measures 256 x 144 pixels and the cell aspect is 16:9, the bake aspect. A 16:9
    /// view at zoom 1 must answer the whole cell.
    /// </summary>
    public class AtlasBackgroundLayoutTests
    {
        private const int Columns = 8;
        private const int Rows = 2;
        private const float TextureAspect = 2048f / 288f;
        private const float CellW = 1f / 8f;
        private const float CellH = 1f / 2f;
        private const float CellPixelAspect = 16f / 9f;

        private static Rect CellRect(int pose, float zoom = 1f, float viewAspect = CellPixelAspect)
        {
            return AtlasBackgroundLayout.CellRect(pose, Columns, Rows, zoom, viewAspect, TextureAspect);
        }

        [Test]
        public void YawPoses_AnswerRowZeroColumns_InOrder()
        {
            for (int pose = 0; pose < Columns; pose++)
            {
                Rect r = CellRect(pose);
                Assert.AreEqual(0f, r.yMin, 1e-6f, $"pose {pose} row");
                Assert.AreEqual(pose * CellW, r.xMin, 1e-6f, $"pose {pose} column");
            }
        }

        [Test]
        public void NadirPose_AnswersRowOneColumnZero()
        {
            Rect r = CellRect(Columns);
            Assert.AreEqual(CellH, r.yMin, 1e-6f, "nadir row start");
            Assert.AreEqual(0f, r.xMin, 1e-6f, "nadir column");
            Assert.AreEqual(CellW, r.width, 1e-6f, "nadir width");
            Assert.AreEqual(CellH, r.height, 1e-6f, "nadir height");
        }

        [Test]
        public void ZoomOne_CellAspectView_AnswersTheWholeCell()
        {
            Rect r = CellRect(3);
            Assert.AreEqual(CellW, r.width, 1e-6f, "width");
            Assert.AreEqual(CellH, r.height, 1e-6f, "height");
            Assert.AreEqual(3.5f * CellW, r.center.x, 1e-6f, "center x");
            Assert.AreEqual(0.5f * CellH, r.center.y, 1e-6f, "center y");
        }

        [Test]
        public void ZoomTwo_HalvesTheRegion_AndAnchorsAtCellCenter()
        {
            Rect r = CellRect(3, zoom: 2f);
            Assert.AreEqual(CellW / 2f, r.width, 1e-6f, "width");
            Assert.AreEqual(CellH / 2f, r.height, 1e-6f, "height");
            Assert.AreEqual(3.5f * CellW, r.center.x, 1e-6f, "center x");
            Assert.AreEqual(0.5f * CellH, r.center.y, 1e-6f, "center y");
        }

        [Test]
        public void WideView_CropsHeight_KeepsWidthAtZoomOne()
        {
            Rect r = CellRect(0, zoom: 1f, viewAspect: 3f);
            Assert.AreEqual(CellW, r.width, 1e-6f, "width");
            Assert.Greater(CellH, r.height, "height crops below the cell");
            Assert.AreEqual(CellH * 0.5f, r.center.y, 1e-6f, "center y");
            Assert.AreEqual(3f, (r.width / r.height) * TextureAspect, 1e-3f, "pixel aspect");
        }

        [Test]
        public void NarrowView_CropsWidth_KeepsHeightAtZoomOne()
        {
            Rect r = CellRect(0, zoom: 1f, viewAspect: 1f);
            Assert.AreEqual(CellH, r.height, 1e-6f, "height");
            Assert.Greater(CellW, r.width, "width crops below the cell");
            Assert.AreEqual(CellW * 0.5f, r.center.x, 1e-6f, "center x");
            Assert.AreEqual(1f, (r.width / r.height) * TextureAspect, 1e-3f, "pixel aspect");
        }

        [Test]
        public void RegionPixelAspect_EqualsViewAspect()
        {
            foreach (float aspect in new[] { 1f, 4f / 3f, CellPixelAspect, 21f / 9f })
            {
                foreach (float zoom in new[] { 1f, 2f, 4f })
                {
                    Rect r = CellRect(5, zoom, aspect);
                    Assert.AreEqual(aspect, (r.width / r.height) * TextureAspect, 1e-3f, $"aspect at zoom {zoom}");
                }
            }
        }

        [Test]
        public void Region_StaysInsideTheCell()
        {
            foreach (int pose in new[] { 0, 4, 7, Columns })
            {
                foreach (float zoom in new[] { 1f, 2f, 4f, 8f })
                {
                    foreach (float aspect in new[] { 1f, CellPixelAspect, 3f })
                    {
                        Rect r = CellRect(pose, zoom, aspect);
                        Assert.GreaterOrEqual(r.xMin, -1e-6f, $"xMin pose {pose} zoom {zoom}");
                        Assert.GreaterOrEqual(r.yMin, -1e-6f, $"yMin pose {pose} zoom {zoom}");
                        Assert.LessOrEqual(r.xMax, 1f + 1e-6f, $"xMax pose {pose} zoom {zoom}");
                        Assert.LessOrEqual(r.yMax, 1f + 1e-6f, $"yMax pose {pose} zoom {zoom}");
                    }
                }
            }
        }

        [Test]
        public void ZoomBelowOne_ClampsToTheWholeCell()
        {
            Rect r = CellRect(2, zoom: 0.5f);
            Assert.AreEqual(CellW, r.width, 1e-6f, "width");
            Assert.AreEqual(CellH, r.height, 1e-6f, "height");
        }
    }
}
