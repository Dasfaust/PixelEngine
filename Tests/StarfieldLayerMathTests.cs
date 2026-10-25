using NUnit.Framework;
using Unity.Mathematics;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// The dynamic star layer's motion math. The layer draws its stars from a screen-space
    /// lattice, so the rig's answers are the picture: the slide states where the field sits, the
    /// scale states how far it spreads, the grid states the lattice the hash reads, the radius
    /// states how big a dot is, and the alpha states how visible the field is.
    ///
    /// Each test names the rule it asserts and states the fault a reader would see when the rule
    /// breaks, so a failure reads as the visual fault and not as a number out of place.
    /// </summary>
    public class StarfieldLayerMathTests
    {
        private const float CellsWide = 96f;
        private const int YawCells = 8;
        private const float Aspect = 16f / 9f;
        private const float LayerWidthPixels = 960f;
        private const float RadiusFloor = 0.0001f;

        // --- Slide ---

        [Test]
        public void AZeroPoseAnswersNoSlide()
        {
            Assert.AreEqual(0f, StarfieldLayerMath.SlideCells(0f, CellsWide, 1f), 1e-5f);
        }

        [Test]
        public void OneYawCellCarriesTheFieldByItsWholeWidth()
        {
            // The baked strip carries one cell per yaw step, and a cell is one screen width of sky.
            // The star layer covers one screen width, so the yaw step that moves the baked view by a
            // cell moves this field by its whole pattern width: the two layers travel the same sky.
            Assert.AreEqual(CellsWide, StarfieldLayerMath.SlideCells(1f, CellsWide, 1f), 1e-5f);
        }

        [Test]
        public void AWholeRevolutionAtWholeParallaxReturnsTheFieldToItsStart()
        {
            // The strip wraps after the authored yaw cell count. A full turn must land the star
            // field on itself, so a reader sees no jump where the strip wraps: the answered slide is
            // a whole number of pattern widths.
            float slide = StarfieldLayerMath.SlideCells(YawCells, CellsWide, 1f);
            Assert.AreEqual(YawCells, slide / CellsWide, 1e-5f);
            Assert.AreEqual(0f, slide % CellsWide, 1e-5f);
        }

        [Test]
        public void EveryYawStepMovesTheSameDistanceAcrossThePatternEdge()
        {
            // The lattice wraps on its own width, so the step that crosses the pattern edge moves
            // the same distance as its neighbours. A slide that clamped at the pattern width would
            // stall the field for one step, and the sky would stop under a continuous yaw press.
            float before = StarfieldLayerMath.SlideCells(CellsWide - 1f, CellsWide, 1f);
            float crossing = StarfieldLayerMath.SlideCells(CellsWide, CellsWide, 1f);
            float after = StarfieldLayerMath.SlideCells(CellsWide + 1f, CellsWide, 1f);
            Assert.AreEqual(CellsWide, crossing - before, 1e-4f);
            Assert.AreEqual(CellsWide, after - crossing, 1e-4f);
        }

        [Test]
        public void AParallaxFactorBelowOneTravelsLessOverTheSameGround()
        {
            // The parallax read is the star field moving a shorter distance than the baked sky over
            // the same yaw move, and the authored factor states that share.
            Assert.AreEqual(CellsWide * 0.5f, StarfieldLayerMath.SlideCells(1f, CellsWide, 0.5f), 1e-5f);
            Assert.AreEqual(0f, StarfieldLayerMath.SlideCells(1f, CellsWide, 0f), 1e-5f);
        }

        [Test]
        public void TheSlideCarriesTheSignOfThePose()
        {
            // A yaw move the other way carries the sky the other way. A slide that took the absolute
            // value would run the field the same direction for both yaw keys.
            Assert.AreEqual(-CellsWide, StarfieldLayerMath.SlideCells(-1f, CellsWide, 1f), 1e-5f);
        }

        [Test]
        public void AFractionalPoseSlidesTheFieldByAFractionOfItsWidth()
        {
            // The slide is continuous, the rule the shipped sampling mode answers for the baked
            // layer. A star field that stepped in whole pattern widths would stutter against a baked
            // sky that slides smoothly.
            Assert.AreEqual(CellsWide * 0.25f, StarfieldLayerMath.SlideCells(0.25f, CellsWide, 1f), 1e-5f);
        }

        // --- Pattern scale ---

        [Test]
        public void TheRestingZoomAnswersScaleOneForEveryRate()
        {
            // The camera's resting answer is zoom 1, and at zoom 1 the layer shows its authored
            // field. A rate that moved the answer at rest would change the star density the moment
            // the layer appeared.
            Assert.AreEqual(1f, StarfieldLayerMath.PatternScale(1f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.PatternScale(1f, 1f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.PatternScale(1f, 2f), 1e-5f);
        }

        [Test]
        public void TheScaleCarriesTheTravelAboveRestAtTheAuthoredRate()
        {
            // The field spreads as the view zooms in, at the layer's own rate, which is the number
            // the look pass tunes against the baked layer's rate.
            Assert.AreEqual(1.5f, StarfieldLayerMath.PatternScale(2f, 0.5f), 1e-5f);
            Assert.AreEqual(2f, StarfieldLayerMath.PatternScale(2f, 1f), 1e-5f);
            Assert.AreEqual(3f, StarfieldLayerMath.PatternScale(2f, 2f), 1e-5f);
        }

        [Test]
        public void AZoomLevelBelowRestAnswersNoScale()
        {
            // The camera's zoom floor is 1. A caller naming a level below 1 must not shrink the
            // field, because a shrunk field packs the stars together and reads as a different sky.
            Assert.AreEqual(1f, StarfieldLayerMath.PatternScale(0.4f, 1f), 1e-5f);
        }

        // --- Cell grid ---

        [Test]
        public void TheGridHoldsTheAuthoredCellCountAtScaleOne()
        {
            float2 grid = StarfieldLayerMath.LatticeCount(CellsWide, 1f, Aspect);
            Assert.AreEqual(CellsWide, grid.x, 1e-5f);
            Assert.AreEqual(CellsWide / Aspect, grid.y, 1e-5f);
        }

        [Test]
        public void AScaleAboveOneAnswersFewerCellsAcrossTheScreen()
        {
            // The pattern scales about the center, so the cell count crossing the screen divides by
            // the scale. A grid that ignored the scale would keep the field the same width on
            // screen, and the zoom move would read as nothing happening.
            float2 half = StarfieldLayerMath.LatticeCount(CellsWide, 2f, Aspect);
            Assert.AreEqual(CellsWide / 2f, half.x, 1e-5f);
            Assert.AreEqual(CellsWide / 2f / Aspect, half.y, 1e-5f);
        }

        [Test]
        public void TheGridStaysAtLeastOneCellWide()
        {
            // A deep scale on a thin authored field answers fewer than one cell across the screen,
            // and a lattice with no whole cell answers no stars. The floor keeps one cell, so an
            // extreme scale reads a big star field and not an empty sky.
            float2 grid = StarfieldLayerMath.LatticeCount(8f, 64f, Aspect);
            Assert.GreaterOrEqual(grid.x, 1f);
            Assert.GreaterOrEqual(grid.y, 1f);
        }

        // --- Star dot radius ---

        [Test]
        public void TheDotRadiusDividesTheAuthoredPixelSizeByTheCellWidth()
        {
            // A dot authored at 2 pixels in a 96-cell field on a 960-pixel layer covers a fifth of
            // a cell. The authored pixel size is the look number, so the conversion runs through the
            // layer's own width.
            Assert.AreEqual(2f / (LayerWidthPixels / CellsWide),
                StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels, CellsWide, 1f, 0f), 1e-5f);
        }

        [Test]
        public void TheDotKeepsItsPixelSizeWhenTheLayerWidens()
        {
            // A wider layer answers more cells of the same authored picture, so a dot keeps its
            // authored pixels and its fraction of a cell halves. A rule that read the layer's width
            // as a fixed constant would grow the stars on a bigger window.
            float narrow = StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels, CellsWide, 1f, 0f);
            float wide = StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels * 2f, CellsWide, 1f, 0f);
            Assert.AreEqual(narrow / 2f, wide, 1e-5f);
        }

        [Test]
        public void AZeroDotZoomFactorKeepsThePixelSizeAsTheFieldSpreads()
        {
            // The dot zoom factor at 0 keeps the authored pixel size, so at scale 2 the dot's
            // fraction of a cell halves: the field spreads and the dots stay the same size on the
            // screen.
            float rest = StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels, CellsWide, 1f, 0f);
            float spread = StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels, CellsWide, 2f, 0f);
            Assert.AreEqual(rest / 2f, spread, 1e-5f);
        }

        [Test]
        public void AFullDotZoomFactorGrowsTheDotWithTheField()
        {
            // The dot zoom factor at 1 grows the dot's pixel size with the scale, so its fraction of
            // a cell holds: the field magnifies and the stars magnify with it.
            float rest = StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels, CellsWide, 1f, 1f);
            float spread = StarfieldLayerMath.DotRadiusCells(2f, LayerWidthPixels, CellsWide, 4f, 1f);
            Assert.AreEqual(rest, spread, 1e-5f);
        }

        [Test]
        public void AStarWithNoAuthoredSizeAnswersTheRadiusFloor()
        {
            // A dot authored at 0 pixels answers the floor radius and not zero. The shader divides by
            // the radius on the dot's own edge, so a zero answer is a division by zero on every
            // star's edge test.
            Assert.AreEqual(RadiusFloor,
                StarfieldLayerMath.DotRadiusCells(0f, LayerWidthPixels, CellsWide, 1f, 0f), 1e-6f);
        }

        // --- Layer alpha ---

        [Test]
        public void TheNadirFadeCarriesTheFieldToFullAlpha()
        {
            // At nadir the baked image holds the screen and this is the layer that rolls, so the
            // field must reach full alpha where the fade reaches 1. A field that stayed at its base
            // alpha under the fade would leave the top-down view without its crisp stars.
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(0.35f, 1f), 1e-5f);
        }

        [Test]
        public void NoFadeAnswersTheAuthoredBaseAlpha()
        {
            // Away from nadir the field reads against the baked sky, and the authored base alpha is
            // the number the look pass sets for that read.
            Assert.AreEqual(0.35f, StarfieldLayerMath.LayerOpacity(0.35f, 0f), 1e-5f);
        }

        [Test]
        public void TheAlphaStepsWithTheFadeAndStopsAtOne()
        {
            // The rise is a lerp, so a half fade answers a half step toward full, and the answer
            // never runs past 1.
            Assert.AreEqual(0.675f, StarfieldLayerMath.LayerOpacity(0.35f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(1f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, StarfieldLayerMath.LayerOpacity(0f, 0.5f), 1e-5f);
        }

        [Test]
        public void AFullFadeAnswersFullAlphaForEveryBaseAlpha()
        {
            // The nadir end of the cross-fade is one answer for every authored base alpha, so the
            // look pass can set the base alpha without changing what the top-down view shows.
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(0f, 1f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(0.62f, 1f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(1f, 1f), 1e-5f);
        }

        [Test]
        public void ABaseAlphaOutsideTheRangeIsClamped()
        {
            // The base alpha rides an Inspector field, so a value outside 0..1 can arrive. A base
            // above 1 must not push the answer past full, and a negative base must not dim the field
            // as the fade rises.
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(1.4f, 0f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(1.4f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, StarfieldLayerMath.LayerOpacity(-0.5f, 0f), 1e-5f);
        }

        // --- Roll ---

        [Test]
        public void TheRollAngleCarriesTheYawStepAngle()
        {
            // The camera rolls by its yaw at nadir, and world content counter-rotates by the same
            // angle. The field carries the angle the caller names, so a 45 degree step spins the
            // stars 45 degrees and the stars keep up with the world.
            Assert.AreEqual(45f, StarfieldLayerMath.RollDegrees(45f, 1f), 1e-5f);
        }

        [Test]
        public void TheRollSignFlipsTheDirectionAndKeepsTheAngle()
        {
            // The sign is the look pass's choice of direction. It flips the spin and leaves the angle
            // alone, so a reader who changes the sign reads the same sky turning the other way.
            Assert.AreEqual(-45f, StarfieldLayerMath.RollDegrees(45f, -1f), 1e-5f);
            Assert.AreEqual(45f, math.abs(StarfieldLayerMath.RollDegrees(45f, -1f)), 1e-5f);
            Assert.AreEqual(0f, StarfieldLayerMath.RollDegrees(0f, -1f), 1e-5f);
        }
    }
}
