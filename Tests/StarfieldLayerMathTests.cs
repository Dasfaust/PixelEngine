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

        [Test]
        public void ThePatternCellsCarryTheAuthoredWidthAndTheLayerAspect()
        {
            // The pattern's own cell count is the authored width across, and the down count follows
            // the layer aspect. The layer's ratio is the answer, because a cell that is square on
            // screen has to answer the layer's width over its height.
            float2 pattern = StarfieldLayerMath.PatternCells(CellsWide, Aspect);
            Assert.AreEqual(CellsWide, pattern.x, 1e-5f);
            Assert.AreEqual(CellsWide / Aspect, pattern.y, 1e-5f);
            Assert.AreEqual(Aspect, pattern.x / pattern.y, 1e-4f);
        }

        [Test]
        public void ThePatternCellsCarryNoZoom()
        {
            // The pattern's count names the pattern, and the zoom names what the screen shows of it.
            // A pattern count that carried the zoom would move the pattern's center and its wrap
            // period every time the camera zoomed, and the field would step under the zoom move.
            float2 square = StarfieldLayerMath.PatternCells(CellsWide, 1f);
            float2 wide = StarfieldLayerMath.PatternCells(CellsWide, 4f);
            Assert.AreEqual(CellsWide, square.x, 1e-5f);
            Assert.AreEqual(CellsWide, wide.x, 1e-5f);
            Assert.AreEqual(CellsWide, square.y, 1e-5f);
            Assert.AreEqual(CellsWide / 4f, wide.y, 1e-5f);
        }

        [Test]
        public void TheVisibleCountIsThePatternCountDividedByTheScale()
        {
            // The two counts the rig answers are one rule read two ways: the pattern's count at the
            // live scale is the count the screen shows. A pair that disagreed would put the center
            // offset and the magnification on different numbers, and the zoom would anchor off center.
            const float scale = 2.5f;
            float2 pattern = StarfieldLayerMath.PatternCells(CellsWide, Aspect);
            float2 visible = StarfieldLayerMath.LatticeCount(CellsWide, scale, Aspect);
            Assert.AreEqual(pattern.x / scale, visible.x, 1e-5f);
            Assert.AreEqual(pattern.y / scale, visible.y, 1e-5f);
        }

        [Test]
        public void ThePatternCellsFloorAtOneCell()
        {
            // A layer so wide that the authored field cannot fill it answers no whole cell down, and
            // a pattern with no whole cell has no center and no wrap period. The floor keeps one cell
            // on both axes.
            float2 pattern = StarfieldLayerMath.PatternCells(1f, 1000f);
            Assert.GreaterOrEqual(pattern.x, 1f);
            Assert.GreaterOrEqual(pattern.y, 1f);
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

        // --- Camera pan travel ---

        [Test]
        public void APanAlongTheCameraRightAxisAnswersThatScreenAxis()
        {
            // The pan projects onto the camera's own screen axes, so the authored rule needs no angle.
            // A world pan straight along the camera's right axis lands entirely on the screen's across
            // axis and nothing on its up axis.
            float3 pan = new float3(10f, 0f, 0f);
            float2 travel = StarfieldLayerMath.ScreenTravelPixels(
                pan, new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), 1f / 64f, 1f);
            Assert.AreEqual(640f, travel.x, 1e-3f);
            Assert.AreEqual(0f, travel.y, 1e-3f);
        }

        [Test]
        public void AWorldPanProjectsItsCameraUpComponentOnly()
        {
            // At a pitched camera the screen's up axis is tilted off the world's vertical, so a pan
            // along the world Z axis reads the component that lies along that axis. The tilt is carried
            // by the axis the caller passes, and not by an authored angle.
            float3 up30 = new float3(0f, 0.5f, 0.8660254f);
            float2 tilted = StarfieldLayerMath.ScreenTravelPixels(
                new float3(0f, 0f, 10f), new float3(1f, 0f, 0f), up30, 1f / 64f, 1f);
            Assert.AreEqual(0f, tilted.x, 1e-3f);
            Assert.AreEqual(554.256f, tilted.y, 1e-2f);

            // At top-down the screen's up axis lies along the world Z axis, so the same pan reads whole.
            float2 topDown = StarfieldLayerMath.ScreenTravelPixels(
                new float3(0f, 0f, 10f), new float3(1f, 0f, 0f), new float3(0f, 0f, 1f), 1f / 64f, 1f);
            Assert.AreEqual(640f, topDown.y, 1e-3f);
        }

        [Test]
        public void TheUVZoomMagnifiesThePanTravel()
        {
            // A zoomed view carries the same world pan over more screen pixels, so the travel scales
            // with the zoom, and the star field keeps its place against the world content.
            float2 atOne = StarfieldLayerMath.ScreenTravelPixels(
                new float3(10f, 0f, 0f), new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), 1f / 64f, 1f);
            float2 atFour = StarfieldLayerMath.ScreenTravelPixels(
                new float3(10f, 0f, 0f), new float3(1f, 0f, 0f), new float3(0f, 1f, 0f), 1f / 64f, 4f);
            Assert.AreEqual(640f, atOne.x, 1e-3f);
            Assert.AreEqual(2560f, atFour.x, 1e-2f);
        }

        [Test]
        public void PanTravelConvertsToCellsByTheLiveCellWidth()
        {
            // 640 pixels of travel on a 1920-pixel layer holding 96 pattern cells at scale 1 crosses a
            // third of the pattern, and the authored factor scales that.
            float2 cells = StarfieldLayerMath.PanSlideCells(
                new float2(640f, 320f), 1920f, 96f, 1f, 0.5f);
            Assert.AreEqual(16f, cells.x, 1e-4f);
            Assert.AreEqual(8f, cells.y, 1e-4f);
        }

        [Test]
        public void ADeeperZoomCutsThePanTravelPerWorldUnit()
        {
            // The same pixel travel lands on fewer pattern cells when the cells are bigger on screen,
            // so a deep zoom answers the same world pan as the same sky distance and not as a faster
            // sky. The pattern scale carries that.
            float2 scaleOne = StarfieldLayerMath.PanSlideCells(new float2(640f, 0f), 1920f, 96f, 1f, 1f);
            float2 scaleTwo = StarfieldLayerMath.PanSlideCells(new float2(640f, 0f), 1920f, 96f, 2f, 1f);
            Assert.AreEqual(32f, scaleOne.x, 1e-4f);
            Assert.AreEqual(16f, scaleTwo.x, 1e-4f);
        }

        [Test]
        public void APanParallaxFactorOfZeroPinsTheSky()
        {
            float2 pinned = StarfieldLayerMath.PanSlideCells(new float2(640f, 640f), 1920f, 96f, 1f, 0f);
            Assert.AreEqual(0f, pinned.x, 1e-6f);
            Assert.AreEqual(0f, pinned.y, 1e-6f);
        }

        // --- Pitch transition dip ---

        [Test]
        public void TheTransitionDipLeavesTheLayerWholeAtALanding()
        {
            // The dip is a mid-move rule. At a landing the transition amount reads 0 and the layer
            // answers the cross-fade alpha unchanged, so the star field stands whole at both notches.
            Assert.AreEqual(StarfieldLayerMath.LayerOpacity(0.6f, 0f),
                StarfieldLayerMath.LayerOpacity(0.6f, 0f, 0f, 1f), 1e-5f);
            Assert.AreEqual(StarfieldLayerMath.LayerOpacity(0.6f, 1f),
                StarfieldLayerMath.LayerOpacity(0.6f, 1f, 0f, 1f), 1e-5f);
        }

        [Test]
        public void TheTransitionDipVanishesAtAFullMoveWithAFullDip()
        {
            Assert.AreEqual(0f, StarfieldLayerMath.LayerOpacity(1f, 1f, 1f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, StarfieldLayerMath.LayerOpacity(1f, 1f, 1f, 0.5f), 1e-5f);
        }

        [Test]
        public void TheTransitionDipShrinksTheAlphaBetweenTheLandings()
        {
            float full = StarfieldLayerMath.LayerOpacity(0.8f, 0f);
            float mid = StarfieldLayerMath.LayerOpacity(0.8f, 0f, 0.5f, 1f);
            Assert.Greater(full, mid);
            Assert.AreEqual(0.4f, mid, 1e-5f);
        }

        [Test]
        public void TheTransitionDipStaysAtLeastAWholeDipFromTheAmount()
        {
            // A transition amount above 1 and a dip above 1 are Inspector inputs, and an answer below
            // zero is an alpha the canvas cannot carry. The clamps keep the answer on screen.
            Assert.AreEqual(0f, StarfieldLayerMath.LayerOpacity(1f, 0f, 3f, 4f), 1e-5f);
            Assert.GreaterOrEqual(StarfieldLayerMath.LayerOpacity(0.2f, 0.5f, 2f, 2f), 0f);
        }

        [Test]
        public void ANadirFadeOfZeroCarriesNoRoll()
        {
            // The yaw step is a slide at the isometric look. A roll applied there reads as a star
            // viewport turning under a camera whose world content is translating, so the fade gate
            // answers no roll where the baked layer stands.
            Assert.AreEqual(0f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0f), 1e-6f);
            Assert.AreEqual(0f, StarfieldLayerMath.RollDegrees(315f, -1f, 0f), 1e-6f);
        }

        [Test]
        public void AFullNadirFadeCarriesTheRollWhole()
        {
            Assert.AreEqual(-45f, StarfieldLayerMath.RollDegrees(-45f, 1f, 1f), 1e-5f);
            Assert.AreEqual(45f, StarfieldLayerMath.RollDegrees(-45f, -1f, 1f), 1e-5f);
        }

        [Test]
        public void TheRollRidesTheNadirCrossFade()
        {
            // The cross-fade is the transition between the two looks, and a roll that jumped from 0 to
            // the whole yaw at one tick would read as a snap mid-tilt.
            Assert.AreEqual(-22.5f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0.5f), 1e-5f);
            Assert.AreEqual(-45f, StarfieldLayerMath.RollDegrees(-45f, 1f, 3f), 1e-5f);
            Assert.AreEqual(0f, StarfieldLayerMath.RollDegrees(-45f, 1f, -2f), 1e-6f);
        }

        // --- Roll lead and the visibility lag ---

        [Test]
        public void ARollLeadOfOneRidesTheCrossFade()
        {
            // The lead is the authored shape, and the plain ride is its baseline: a lead of 1 answers
            // the gated roll unchanged, so a reader can set the lead to 1 and get the ride.
            Assert.AreEqual(StarfieldLayerMath.RollDegrees(-45f, 1f, 0.5f),
                StarfieldLayerMath.RollDegrees(-45f, 1f, 0.5f, 1f), 1e-5f);
            Assert.AreEqual(StarfieldLayerMath.RollDegrees(-45f, 1f, 0.25f),
                StarfieldLayerMath.RollDegrees(-45f, 1f, 0.25f, 1f), 1e-5f);
        }

        [Test]
        public void ARollLeadOfTwoTurnsTheSkyAtHalfTheFade()
        {
            // The look rule this carries: the sky is turned by the time the fade reaches its middle, so
            // the field brightens onto a sky that has already moved and not onto one that is moving.
            Assert.AreEqual(-45f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0.5f, 2f), 1e-5f);
            Assert.AreEqual(-22.5f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0.25f, 2f), 1e-5f);
        }

        [Test]
        public void TheRollLeadStopsAtAFullRollAndAtNoFade()
        {
            // The lead multiplies the fade, and the gate clamps it, so a deep lead cannot run the roll
            // past the yaw, and no fade cannot roll at all.
            Assert.AreEqual(-45f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0.9f, 4f), 1e-5f);
            Assert.AreEqual(0f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0f, 4f), 1e-6f);
            Assert.AreEqual(-45f, StarfieldLayerMath.RollDegrees(-45f, 1f, 1f, 4f), 1e-5f);
            // A lead of 0 multiplies the fade the roll reads by nothing, so the roll answers no roll at
            // any fade. The Inspector range starts at 1; this is the clamp boundary below it.
            Assert.AreEqual(0f, StarfieldLayerMath.RollDegrees(-45f, 1f, 0.5f, 0f), 1e-6f);
        }

        [Test]
        public void TheVisibilityLagKeepsTheFieldDimOnTheNearSideOfTheTilt()
        {
            // At the cross-fade's middle a lagged visibility reads well below half, so the roll runs
            // while the stars are faint. The endpoints are the landings: no fade answers no visibility,
            // a full fade answers full visibility, and the lag moves the shape between them and nowhere
            // at the ends.
            Assert.AreEqual(0f, StarfieldLayerMath.FadeWeight(0f, 0.6f), 1e-6f);
            Assert.AreEqual(1f, StarfieldLayerMath.FadeWeight(1f, 0.6f), 1e-5f);
            Assert.AreEqual(1f, StarfieldLayerMath.FadeWeight(1f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, StarfieldLayerMath.FadeWeight(0.5f, 0f), 1e-5f);
            Assert.Greater(0.5f, StarfieldLayerMath.FadeWeight(0.5f, 0.6f));
            Assert.Greater(StarfieldLayerMath.FadeWeight(0.5f, 0.6f),
                StarfieldLayerMath.FadeWeight(0.5f, 1f));
            Assert.AreEqual(0.14356f, StarfieldLayerMath.FadeWeight(0.5f, 0.6f), 1e-4f);
        }

        [Test]
        public void TheVisibilityLagRunsTheFadeBehindTheRoll()
        {
            // The pair is the look rule in one line: at half the cross-fade the roll is whole and the
            // visibility is a sixth of its own span, so the turn completes before the field reads.
            float roll = math.abs(StarfieldLayerMath.RollDegrees(-45f, 1f, 0.5f, 2f));
            float visibility = StarfieldLayerMath.FadeWeight(0.5f, 0.6f);
            Assert.AreEqual(45f, roll, 1e-5f);
            Assert.Greater(0.2f, visibility);
        }

        [Test]
        public void TheLaggedVisibilityFeedsTheLayerAlpha()
        {
            // The alpha the canvas carries is the lagged visibility, so the mid-fade read is the
            // dimmed one and not the cross-fade's own middle.
            // The layer's authored base opacity is what gives the cross-fade work to do: at a base of 1
            // the layer stands at full alpha at every fade, and a lag shapes a ramp that is flat. Below
            // full base, the lagged visibility lands the alpha lower on the near side of the tilt.
            float baseAlpha = 0.35f;
            float lagged = StarfieldLayerMath.LayerOpacity(baseAlpha, StarfieldLayerMath.FadeWeight(0.5f, 0.6f));
            float plain = StarfieldLayerMath.LayerOpacity(baseAlpha, 0.5f);
            Assert.Greater(plain, lagged);
            Assert.AreEqual(0.675f, plain, 1e-5f);
            Assert.AreEqual(0.44331f, lagged, 1e-4f);
            Assert.AreEqual(1f, StarfieldLayerMath.LayerOpacity(baseAlpha, StarfieldLayerMath.FadeWeight(1f, 0.6f)),
                1e-5f);
            Assert.AreEqual(baseAlpha,
                StarfieldLayerMath.LayerOpacity(baseAlpha, StarfieldLayerMath.FadeWeight(0f, 0.6f)), 1e-5f);
        }

        // --- The pitch tilt slides the sky ---

        [Test]
        public void ATiltAtTheBaseNotchCarriesNoPitchSlide()
        {
            // The isometric landing is where the slide starts, so the camera sitting at its base notch
            // writes no pitch term and the isometric view keeps its yaw and pan travel whole.
            Assert.AreEqual(0f, StarfieldLayerMath.PitchSlideCells(30f, 30f, 96f, 1f), 1e-6f);
            Assert.AreEqual(0f, StarfieldLayerMath.PitchSlideCells(0f, 30f, 96f, 1f), 1e-6f);
            Assert.AreEqual(0f, StarfieldLayerMath.PitchSlideCells(29.999f, 30f, 96f, 4f), 1e-5f);
        }

        [Test]
        public void TheTiltFromTheIsometricNotchToTopDownCarriesOneAndAThirdPatternWidths()
        {
            // The shipped pair of notches is 30 and 90 degrees: 60 degrees of tilt, which at the yaw
            // currency is 1.333 pattern widths, the same travel a 60-degree yaw step asks of the sky.
            Assert.AreEqual(128f, StarfieldLayerMath.PitchSlideCells(90f, 30f, 96f, 1f), 1e-4f);
        }

        [Test]
        public void ThePitchSlideAndTheYawSlideShareOneCurrency()
        {
            // The rule that makes one number cover both axes: a 45-degree tilt and a 45-degree yaw step
            // answer the same travel, one pattern width at a factor of 1, and half a turn answers half
            // the travel on both.
            float yawTravel = StarfieldLayerMath.SlideCells(1f, 96f, 1f);
            float pitchTravel = StarfieldLayerMath.PitchSlideCells(75f, 30f, 96f, 1f);
            Assert.AreEqual(yawTravel, pitchTravel, 1e-4f);
            Assert.AreEqual(96f, pitchTravel, 1e-4f);
            Assert.AreEqual(48f, StarfieldLayerMath.PitchSlideCells(52.5f, 30f, 96f, 1f), 1e-4f);
        }

        [Test]
        public void ThePitchSlideParallaxScalesTheTiltTravel()
        {
            Assert.AreEqual(64f, StarfieldLayerMath.PitchSlideCells(90f, 30f, 96f, 0.5f), 1e-4f);
            Assert.AreEqual(0f, StarfieldLayerMath.PitchSlideCells(90f, 30f, 96f, 0f), 1e-6f);
            Assert.AreEqual(256f, StarfieldLayerMath.PitchSlideCells(90f, 30f, 96f, 2f), 1e-4f);
        }

        [Test]
        public void ThePitchReadIsTheTiltOfTheViewDirectionAtAnyYaw()
        {
            // The tilt is read from where the camera looks, so the yaw cannot change it: straight down
            // reads 90 at any yaw, and the isometric tilt reads 30 at any yaw. A level camera reads 0,
            // and the read never wraps, which is what an euler angle would do at a yaw past 180.
            float down = StarfieldLayerMath.PitchDegrees(new float3(0f, -1f, 0f));
            Assert.AreEqual(90f, down, 1e-4f);
            for (float yaw = 0f; yaw < 360f; yaw += 45f)
            {
                float rad = math.radians(yaw);
                float3 iso = math.normalize(new float3(math.sin(rad) * math.cos(math.radians(30f)),
                    -math.sin(math.radians(30f)), math.cos(rad) * math.cos(math.radians(30f))));
                Assert.AreEqual(30f, StarfieldLayerMath.PitchDegrees(iso), 1e-4f);
                float3 nadir = math.normalize(new float3(math.sin(rad) * math.cos(math.radians(90f)),
                    -math.sin(math.radians(90f)), math.cos(rad) * math.cos(math.radians(90f))));
                Assert.AreEqual(90f, StarfieldLayerMath.PitchDegrees(nadir), 1e-3f);
            }
            Assert.AreEqual(0f, StarfieldLayerMath.PitchDegrees(new float3(0f, 0f, 1f)), 1e-5f);
            Assert.AreEqual(-20f, StarfieldLayerMath.PitchDegrees(math.normalize(new float3(0f, 0.342f, 0.94f))),
                1e-2f);
        }

        [Test]
        public void ThePitchSlideLandsWithTheRollAtTopDown()
        {
            // The slide lands in the pattern's frame, and the roll is whole at the top-down landing, so
            // the travel the tilt wrote reads turned by the camera's angle at the landing. The pair is
            // the shape the look asked for: the field moves down the screen at the camera's angle.
            float slide = StarfieldLayerMath.PitchSlideCells(90f, 30f, 96f, 1f);
            float roll = StarfieldLayerMath.RollDegrees(-45f, 1f, 1f, 2f);
            Assert.AreEqual(128f, slide, 1e-4f);
            Assert.AreEqual(-45f, roll, 1e-4f);
            float rad = math.radians(roll);
            float2 turned = new float2(slide * math.sin(rad), slide * math.cos(rad));
            Assert.Greater(math.length(turned), 127f);
            Assert.AreEqual(0f, math.abs(turned.x) - math.abs(turned.y), 1e-3f);
        }

        // --- The warp flow runs the stars against the streaks ---

        [Test]
        public void TheWarpFlowDirectionRunsAlongTheTravelOnScreen()
        {
            // The travel vector arrives in view space, where x is screen right and y is screen up, so
            // the direction is the travel's own screen components. The forward component is the view
            // axis and carries no screen direction, so it drops out and the result is a unit vector:
            // the stars run along the travel at any travel angle, and the look settled the sign.
            float3 forwardDown = new float3(0f, 0.5f, -0.866f);
            float2 along = StarfieldLayerMath.WarpFlowDirection(forwardDown);
            Assert.AreEqual(0f, along.x, 1e-3f);
            Assert.AreEqual(1f, along.y, 1e-3f);
            Assert.AreEqual(1f, math.length(along), 1e-4f);

            float3 yawed = new float3(0.5f, 0.5f, -0.707f);
            float2 angled = StarfieldLayerMath.WarpFlowDirection(yawed);
            Assert.AreEqual(0.7071f, angled.x, 1e-3f);
            Assert.AreEqual(0.7071f, angled.y, 1e-3f);
            Assert.AreEqual(1f, math.length(angled), 1e-4f);
        }

        [Test]
        public void ATravelWithNoScreenComponentCarriesNoStarFlow()
        {
            // A level camera travelling along its own view axis has nothing to slide: the whole travel
            // is the view axis, so no direction exists and the flow answers none.
            Assert.AreEqual(0f, math.length(StarfieldLayerMath.WarpFlowDirection(new float3(0f, 0f, -1f))),
                1e-5f);
            Assert.AreEqual(0f, math.length(StarfieldLayerMath.WarpFlowDirection(float3.zero)), 1e-6f);
        }

        [Test]
        public void TheWarpFlowSpeedMultipliesTheIntensityOverOnePatternWidthPerSecond()
        {
            // The knob the look asked for is a multiplier on the warp intensity, and the unit is the
            // pattern's own width: a full intensity at a speed of 1 carries one pattern width per
            // second, half intensity is half speed, and a stopped warp carries nothing.
            Assert.AreEqual(96f, StarfieldLayerMath.WarpFlowRateCells(96f, 1f, 1f), 1e-4f);
            Assert.AreEqual(48f, StarfieldLayerMath.WarpFlowRateCells(96f, 0.5f, 1f), 1e-4f);
            Assert.AreEqual(192f, StarfieldLayerMath.WarpFlowRateCells(96f, 1f, 2f), 1e-4f);
            Assert.AreEqual(0f, StarfieldLayerMath.WarpFlowRateCells(96f, 1f, 0f), 1e-6f);
            Assert.AreEqual(0f, StarfieldLayerMath.WarpFlowRateCells(96f, 0f, 4f), 1e-6f);
        }

        [Test]
        public void TheWarpFlowRateClampsTheIntensityAndTheSpeed()
        {
            // The streak rig's own rule, carried here: an over-range intensity cannot speed the sky past
            // the authored rate, and a negative speed cannot run the stars the other way.
            Assert.AreEqual(96f, StarfieldLayerMath.WarpFlowRateCells(96f, 1.5f, 1f), 1e-4f);
            Assert.AreEqual(0f, StarfieldLayerMath.WarpFlowRateCells(96f, -1f, 1f), 1e-6f);
            Assert.AreEqual(0f, StarfieldLayerMath.WarpFlowRateCells(96f, 1f, -2f), 1e-6f);
            Assert.AreEqual(48f, StarfieldLayerMath.WarpFlowRateCells(96f, 0.5f, 1f), 1e-4f);
        }

        [Test]
        public void TheWarpFlowFoldsAtThePatternPeriod()
        {
            // The flow accumulates every frame, so a long warp needs the number folded, and the pattern
            // repeats at its own width, which is where the fold answers the same screen.
            Assert.AreEqual(0f, StarfieldLayerMath.WrapCells(96f, 96f), 1e-5f);
            Assert.AreEqual(48f, StarfieldLayerMath.WrapCells(144f, 96f), 1e-4f);
            Assert.AreEqual(48f, StarfieldLayerMath.WrapCells(48f, 96f), 1e-5f);
            Assert.AreEqual(48f, StarfieldLayerMath.WrapCells(-48f, 96f), 1e-4f);
            Assert.AreEqual(95.9999f, StarfieldLayerMath.WrapCells(95.9999f, 96f), 1e-3f);
        }

        [Test]
        public void TheWarpFlowRunsAtEveryViewAngleTheCameraUses()
        {
            // All eight yaw notches at the isometric tilt and at top-down carry a unit direction, so the
            // flow runs at every angle the camera parks at and the sign is against the travel at each.
            for (float yaw = 0f; yaw < 360f; yaw += 45f)
            {
                float rad = math.radians(yaw);
                foreach (float pitchDeg in new float[] { 30f, 90f })
                {
                    float pitch = math.radians(pitchDeg);
                    float3 forwardView = math.normalize(new float3(
                        0f,
                        math.sin(pitch) * math.cos(rad) - math.cos(pitch) * math.sin(rad) * 0f,
                        0f));
                    float3 travel = math.normalize(new float3(
                        -math.sin(rad) * math.cos(pitch),
                        math.sin(pitch),
                        -math.cos(rad) * math.cos(pitch)));
                    float2 direction = StarfieldLayerMath.WarpFlowDirection(travel);
                    if (math.length(new float2(travel.x, travel.y)) > 0.0001f)
                    {
                        Assert.AreEqual(1f, math.length(direction), 1e-3f, "yaw " + yaw + " pitch " + pitchDeg);
                        Assert.Greater(direction.y, 0f, "yaw " + yaw + " pitch " + pitchDeg);
                    }
                    Assert.AreEqual(0f, math.length(forwardView), 1f);
                }
            }
        }
    }
}
