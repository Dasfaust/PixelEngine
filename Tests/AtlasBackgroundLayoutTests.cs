using NUnit.Framework;
using PixelEngine.Presentation;
using UnityEngine;

namespace PixelEngine.Tests
{
    /// <summary>
    /// Layout math for the pose-addressed atlas background. The atlas fixture is a
    /// 2048 x 288 grid: 8 yaw columns by 2 rows, origin bottom-left, so one cell
    /// measures 256 x 144 pixels and the cell aspect is 16:9, the bake aspect. A 16:9
    /// view at zoom 1 must answer the whole cell. Pose is a float: fractional poses
    /// slide the region, and Repeat wrap makes x unbounded (x and x+1 name the same
    /// pixels).
    /// </summary>
    public class AtlasBackgroundLayoutTests
    {
        private const int Columns = 8;
        private const int Rows = 2;
        private const float TextureAspect = 2048f / 288f;
        private const float CellW = 1f / 8f;
        private const float CellH = 1f / 2f;
        private const float CellPixelAspect = 16f / 9f;

        private static Rect CellRect(float pose, float zoom = 1f, float viewAspect = CellPixelAspect)
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
        public void NadirBoundary_HalfStepsStayYawCell()
        {
            Rect justBelow = CellRect(7.999f);
            Assert.AreEqual(0f, justBelow.yMin, 1e-6f, "7.999 keeps the yaw row");
            Rect atNadir = CellRect(8f);
            Assert.AreEqual(CellH, atNadir.yMin, 1e-6f, "8.0 reads the nadir row");
        }

        [Test]
        public void FractionalPose_SlidesByCellWidth()
        {
            Rect half = CellRect(0.5f);
            Rect whole = CellRect(1f);
            Rect zero = CellRect(0f);
            // A half-step pose sits exactly between the cell-0 center and the
            // cell-1 center: the slide moves the region by pose × cell width.
            Assert.AreEqual(zero.center.x + 0.5f * CellW, half.center.x, 1e-6f, "half-step center");
            Assert.AreEqual(zero.center.x + CellW, whole.center.x, 1e-6f, "full-step center");
            Assert.AreEqual(zero.height, half.height, 1e-6f, "slide does not resize");
        }

        [Test]
        public void NegativePose_WrapsToTheSamePixels()
        {
            // Repeat wrap: x and x+1 name the same pixels, so a pose below 0 and
            // the pose one full strip cycle above it must answer rects one texture
            // width apart.
            Rect below = CellRect(-0.5f);
            Rect cyclic = CellRect(7.5f);
            Assert.AreEqual(1f, cyclic.center.x - below.center.x, 1e-6f, "one strip cycle apart");
            Assert.Less(below.center.x, 0.01f, "the answered x runs below 0 and the wrap carries it");
        }

        [Test]
        public void ContinuousPoses_StayInsideTheRowBand_AndTheCellSize()
        {
            foreach (float pose in new[] { -0.25f, 0.5f, 3.75f, 7.999f })
            {
                foreach (float zoom in new[] { 1f, 2f, 4f })
                {
                    foreach (float aspect in new[] { 1f, CellPixelAspect, 3f })
                    {
                        Rect r = CellRect(pose, zoom, aspect);
                        Assert.GreaterOrEqual(r.yMin, -1e-6f, $"yMin pose {pose}");
                        Assert.LessOrEqual(r.yMax, CellH + 1e-6f, $"yMax pose {pose}");
                        Assert.LessOrEqual(r.width, CellW + 1e-6f, $"width pose {pose} zoom {zoom}");
                        Assert.LessOrEqual(r.height, CellH + 1e-6f, $"height pose {pose} zoom {zoom}");
                    }
                }
            }
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
        public void IntegerRegions_StayInsideTheirCell()
        {
            foreach (int pose in new[] { 0, 4, 7, Columns })
            {
                foreach (float zoom in new[] { 1f, 2f, 4f, 8f })
                {
                    foreach (float aspect in new[] { 1f, CellPixelAspect, 3f })
                    {
                        Rect r = CellRect(pose, zoom, aspect);
                        float cellX = pose == Columns ? 0f : pose * CellW;
                        float rowY = pose == Columns ? CellH : 0f;
                        Assert.GreaterOrEqual(r.xMin, cellX - 1e-6f, $"xMin pose {pose} zoom {zoom}");
                        Assert.LessOrEqual(r.xMax, cellX + CellW + 1e-6f, $"xMax pose {pose} zoom {zoom}");
                        Assert.GreaterOrEqual(r.yMin, rowY - 1e-6f, $"yMin pose {pose} zoom {zoom}");
                        Assert.LessOrEqual(r.yMax, rowY + CellH + 1e-6f, $"yMax pose {pose} zoom {zoom}");
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

        [Test]
        public void QuantizedPose_SitsOnTheStepGrid()
        {
            // 5 steps cover a cell: every answered pose is a multiple of 1/5 cell.
            foreach (float pose in new[] { 0.31f, 0.58f, 7.44f, -1.2f, 12.9f })
            {
                float q = AtlasBackgroundLayout.QuantizedPose(pose, 5);
                float whole = q * 5f;
                Assert.AreEqual(Mathf.RoundToInt(whole), whole, 1e-5f, $"pose {pose} off the grid");
            }
        }

        [Test]
        public void QuantizedPose_LeavesIntegerPosesWhole()
        {
            foreach (int pose in new[] { 0, 3, 7, 8, 12, -1 })
            {
                Assert.AreEqual(pose, AtlasBackgroundLayout.QuantizedPose(pose, 5), 1e-6f, $"pose {pose}");
            }
        }

        [Test]
        public void QuantizedPose_RoundsToTheNearestStep()
        {
            Assert.AreEqual(0.4f, AtlasBackgroundLayout.QuantizedPose(0.31f, 5), 1e-6f, "up to 2/5");
            Assert.AreEqual(0.4f, AtlasBackgroundLayout.QuantizedPose(0.49f, 5), 1e-6f, "nearest is 2/5");
            Assert.AreEqual(-1f, AtlasBackgroundLayout.QuantizedPose(-1.05f, 5), 1e-6f, "negative rounds on the grid");
        }

        [Test]
        public void QuantizedPose_OneStepPerCellAtOneStepPerCell()
        {
            // N = 1 answers whole cells only: a slide in flight answers the nearest cell.
            Assert.AreEqual(1f, AtlasBackgroundLayout.QuantizedPose(0.6f, 1), 1e-6f, "past the halfway mark");
            Assert.AreEqual(0f, AtlasBackgroundLayout.QuantizedPose(0.4f, 1), 1e-6f, "before it");
        }

        [Test]
        public void QuantizedPose_StepsLandOnTheWrapTargetWhole()
        {
            // The 7-to-0 wrap rides the unbounded axis: target 8. The step grid
            // reaches 8 exactly, so the answered cell reads as column 0.
            float q = AtlasBackgroundLayout.QuantizedPose(7.96f, 5);
            Assert.AreEqual(8f, q, 1e-6f, "wrap target");
            Rect r = CellRect(q);
            Assert.AreEqual(0f, r.xMin, 1e-6f, "answered column after the wrap");
        }
    }
}
