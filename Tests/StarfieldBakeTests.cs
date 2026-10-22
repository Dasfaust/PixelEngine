using NUnit.Framework;
using PixelEngine.Presentation;
using Unity.Mathematics;
using UnityEngine;

namespace PixelEngine.Tests
{
    /// <summary>
    /// EditMode tests for the starfield bake math: the bake size rule, the atlas shape, the cache
    /// key, the seed-to-parameter table, and the strip seam readout. The tests drive the pure
    /// functions with no scene and no GPU, so the screen answers belong to the play-mode pass.
    /// </summary>
    public class StarfieldBakeTests
    {
        const int CellW = 964;
        const int CellH = 544;
        const int Columns = 8;
        const int Rows = 2;

        // Layout

        [Test]
        public void CellResolution_MatchesTheBackgroundCameraResolution()
        {
            int2 cell = StarfieldBakeLayout.CellResolution(1920, 1080, 2);
            Assert.AreEqual(964, cell.x, "Half of 1920 is 960, even already, plus 4 texels of edge margin.");
            Assert.AreEqual(544, cell.y, "Half of 1080 is 540, even already, plus 4 texels of edge margin.");
        }

        [Test]
        public void CellResolution_RoundsAnOddHalfUpToEven()
        {
            int2 cell = StarfieldBakeLayout.CellResolution(1919, 1079, 2);
            Assert.AreEqual(964, cell.x, "Half of 1919 is 959, one short of even, so 960 plus the margin.");
            Assert.AreEqual(544, cell.y, "Half of 1079 is 539, one short of even, so 540 plus the margin.");
        }

        [Test]
        public void CellResolution_ReadsARatioBelowOneAsOne()
        {
            int2 cell = StarfieldBakeLayout.CellResolution(1920, 1080, 0);
            Assert.AreEqual(1924, cell.x);
            Assert.AreEqual(1084, cell.y);
        }

        [Test]
        public void CellResolution_AspectMatchesTheWindowAspect()
        {
            int2 cell = StarfieldBakeLayout.CellResolution(1920, 1080, 2);
            float cellAspect = cell.x / (float)cell.y;
            float windowAspect = 1920f / 1080f;
            Assert.Less(math.abs(cellAspect - windowAspect), 0.01f,
                "The margin is small against the cell, so the cell aspect and the window aspect agree.");
        }

        [Test]
        public void AtlasSize_IsColumnsWideAndRowsTall()
        {
            int2 atlas = StarfieldBakeLayout.AtlasSize(CellW, CellH, Columns, Rows);
            Assert.AreEqual(CellW * Columns, atlas.x, "Row 0 is the strip: one image, columns cells wide.");
            Assert.AreEqual(CellH * Rows, atlas.y, "Row 1 carries the nadir cell.");
        }

        [Test]
        public void CellPixelRect_AddressesRowZeroByPoseAndTheNadirCellByRow()
        {
            var yaw0 = StarfieldBakeLayout.CellPixelRect(0, Columns, Rows, CellW, CellH);
            Assert.AreEqual(0, yaw0.x);
            Assert.AreEqual(0, yaw0.y);

            var yaw7 = StarfieldBakeLayout.CellPixelRect(7, Columns, Rows, CellW, CellH);
            Assert.AreEqual(7 * CellW, yaw7.x);
            Assert.AreEqual(0, yaw7.y, "Every yaw cell sits in row 0.");

            var nadir = StarfieldBakeLayout.CellPixelRect(Columns, Columns, Rows, CellW, CellH);
            Assert.AreEqual(0, nadir.x, "The nadir image is row 1, column 0, the cell the rig addresses.");
            Assert.AreEqual(CellH, nadir.y);
            Assert.AreEqual(CellW, nadir.z);
            Assert.AreEqual(CellH, nadir.w);
        }

        [Test]
        public void CacheFileName_NamesTheSeedAndTheBakeSize()
        {
            string first = StarfieldBakeLayout.CacheFileName(7, 7712, 1088);
            Assert.AreEqual("starfield-7-7712x1088.png", first);

            Assert.AreNotEqual(first, StarfieldBakeLayout.CacheFileName(8, 7712, 1088),
                "A seed change names a new file, so a re-entry bakes and never reads another seed's picture.");
            Assert.AreNotEqual(first, StarfieldBakeLayout.CacheFileName(7, 968, 1088),
                "A bake-size change names a new file, so a window change cannot read a strip baked at another size.");
            Assert.AreEqual(first, StarfieldBakeLayout.CacheFileName(7, 7712, 1088),
                "One seed at one size names one file, so a re-entry hits the standing bake.");
        }

        [Test]
        public void MemoryBytes_CountsUncompressedRgba32()
        {
            long bytes = StarfieldBakeLayout.MemoryBytes(7712, 1088);
            Assert.AreEqual(7712L * 1088L * 4L, bytes, "Four bytes per texel, no mipmaps, no compression.");
        }

        [Test]
        public void FitsTextureLimit_RefusesAnAtlasWiderThanTheDeviceLimit()
        {
            Assert.IsTrue(StarfieldBakeLayout.FitsTextureLimit(7712, 1088, 16384));
            Assert.IsFalse(StarfieldBakeLayout.FitsTextureLimit(9000, 1088, 8192),
                "A strip wider than the device limit is the shape the bake must refuse and report.");
            Assert.IsFalse(StarfieldBakeLayout.FitsTextureLimit(7712, 9000, 8192),
                "A tall atlas passes the limit too, and the refusal names both sides.");
        }

        // Seed-to-parameter table

        [Test]
        public void SameSeed_GivesTheSameRecipeTwice()
        {
            var first = StarfieldBakeRecipe.CreateStrip(4242, CellH);
            var second = StarfieldBakeRecipe.CreateStrip(4242, CellH);
            AssertRecipesAreEqual(first, second);
        }

        [Test]
        public void SeedChange_MovesTheAuthoredFeatures()
        {
            var first = StarfieldBakeRecipe.CreateStrip(11, CellH);
            var second = StarfieldBakeRecipe.CreateStrip(12, CellH);
            Assert.AreNotEqual(first.NebulaAngleDegrees, second.NebulaAngleDegrees);
            Assert.AreNotEqual(first.DeepTop, second.DeepTop);
            Assert.AreNotEqual(first.NebulaCover, second.NebulaCover);
            Assert.AreNotEqual(first.StarDensity, second.StarDensity);
        }

        [Test]
        public void EverySeed_DrawsInsideTheShaderWindowAndTheSeedTable()
        {
            for (int seed = 0; seed < 400; seed++)
            {
                var strip = StarfieldBakeRecipe.CreateStrip(seed, CellH);
                Assert.IsTrue(StarfieldBakeRules.InsideShaderWindow(strip), $"seed {seed} strip leaves the shader window");
                Assert.IsTrue(StarfieldBakeRules.InsideSeedTable(strip), $"seed {seed} strip leaves the seed table");

                var nadir = StarfieldBakeRecipe.CreateNadir(seed, CellH);
                Assert.IsTrue(StarfieldBakeRules.InsideShaderWindow(nadir), $"seed {seed} nadir leaves the shader window");
                Assert.IsTrue(StarfieldBakeRules.InsideSeedTable(nadir), $"seed {seed} nadir leaves the seed table");
            }
        }

        [Test]
        public void NadirRecipe_HoldsNoCrispStarsAndRunsAtNoPosterize()
        {
            var strip = StarfieldBakeRecipe.CreateStrip(90210, CellH);
            var nadir = StarfieldBakeRecipe.CreateNadir(90210, CellH);
            Assert.AreEqual(0f, nadir.StarStrength,
                "Crisp star dots live on the layer that rolls at nadir, so the baked nadir picture is haze.");
            Assert.AreEqual(0, nadir.PaletteBands);
            Assert.LessOrEqual(nadir.GradientBands, strip.GradientBands,
                "The top-down view reads fewer gradient steps than the standing view, the softer picture.");
            Assert.GreaterOrEqual(nadir.GradientBands, 4, "The gradient keeps at least four bands, so a haze picture keeps a cast.");
        }

        [Test]
        public void NadirAndStripShareTheirSkyFromOneSeed()
        {
            var strip = StarfieldBakeRecipe.CreateStrip(777, CellH);
            var nadir = StarfieldBakeRecipe.CreateNadir(777, CellH);
            Assert.AreEqual(strip.NebulaAngleDegrees, nadir.NebulaAngleDegrees);
            Assert.AreEqual(strip.NebulaEdge, nadir.NebulaEdge);
            Assert.AreEqual(strip.LaneDepth, nadir.LaneDepth);
            Assert.Greater(nadir.NebulaWidthPixels, strip.NebulaWidthPixels,
                "The nadir wash is wider than the strip band, the shape of a haze picture.");
        }

        [Test]
        public void BandWidths_ScaleWithTheCellHeight()
        {
            var small = StarfieldBakeRecipe.CreateStrip(1234, 544);
            var doubleSize = StarfieldBakeRecipe.CreateStrip(1234, 1088);
            Assert.AreEqual(small.NebulaWidthPixels * 2f, doubleSize.NebulaWidthPixels, 0.5f);
            Assert.AreEqual(small.NebulaOffsetPixels * 2f, doubleSize.NebulaOffsetPixels, 0.5f);
            Assert.AreEqual(small.NebulaAngleDegrees, doubleSize.NebulaAngleDegrees,
                "The band angle carries no pixel unit, so it does not move with the size.");
        }

        [Test]
        public void SeedOutsideTheAuthoredRange_IsClampedToTheRange()
        {
            Assert.AreEqual(StarfieldBakeRules.SeedMin, StarfieldBakeRules.ClampSeed(-5));
            Assert.AreEqual(StarfieldBakeRules.SeedMax, StarfieldBakeRules.ClampSeed(5_000_000));
            Assert.AreEqual(1234, StarfieldBakeRules.ClampSeed(1234));
            Assert.AreEqual(StarfieldBakeRules.SeedMax, StarfieldBakeRecipe.CreateStrip(5_000_000, CellH).Seed,
                "A bake recipe carries the clamped seed, so the shader's seed uniform never loses precision.");
        }

        [Test]
        public void NebulaColors_RunEdgeToCoreInLightness()
        {
            for (int seed = 0; seed < 60; seed++)
            {
                var recipe = StarfieldBakeRecipe.CreateStrip(seed, CellH);
                Assert.Less(recipe.NebulaEdge.grayscale, recipe.NebulaMid.grayscale, $"seed {seed}");
                Assert.Less(recipe.NebulaMid.grayscale, recipe.NebulaCore.grayscale, $"seed {seed}");
            }
        }

        // Strip seam readout

        [Test]
        public void CyclicStrip_SeamReadsWhatItsInteriorReads()
        {
            int width = 64, height = 8;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // A pattern that wraps at the strip width: the phase at x = 0 and at x = width
                    // names the same point, so the edge columns are neighbours.
                    // No clamping inside the pattern: a clamped row reports a zero interior
                    // difference, and the ratio measures the clamp instead of the seam.
                    float tone = 96f + 40f * Mathf.Sin(2f * Mathf.PI * x / width) + 8f * y;
                    byte lit = (byte)Mathf.RoundToInt(tone);
                    pixels[y * width + x] = new Color32(lit, lit, lit, 255);
                }
            }

            StarfieldBakeSeam.Analyze(pixels, width, height, out float seam, out float interior, out float interiorMax);
            Assert.Greater(interior, 0f, "The interior pairs move, so the comparison is not a zero-over-zero ratio.");
            Assert.LessOrEqual(seam, interiorMax,
                "The seam pair moves no more than the busiest interior pair, so the strip joins the ring clean.");
        }

        [Test]
        public void HardSeamedStrip_SeamReadsPastItsInterior()
        {
            int width = 64, height = 8;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color32((byte)(x * 3), (byte)(x * 3), (byte)(x * 3), 255);
                }

                // The last column of every row is painted far from the first column: the shape a
                // strip carries when its ends do not match.
                pixels[y * width + width - 1] = new Color32(250, 250, 250, 255);
            }

            StarfieldBakeSeam.Analyze(pixels, width, height, out float seam, out float interior, out float interiorMax);
            Assert.Greater(seam, interiorMax * 2f,
                "The seam pair moves past every interior pair, so the readout names the join.");
        }

        [Test]
        public void AtlasHash_RepeatsForOnePicture_AndMovesOnOneTexel()
        {
            var first = BuildPattern(64);
            var second = BuildPattern(64);
            Assert.AreEqual(StarfieldBakeSeam.Hash(first), StarfieldBakeSeam.Hash(second),
                "One picture hashes to one number, so a determinism record is two numbers.");

            second[1234] = new Color32(255, 0, 0, 255);
            Assert.AreNotEqual(StarfieldBakeSeam.Hash(first), StarfieldBakeSeam.Hash(second),
                "One moved texel moves the hash, so the record cannot pass a picture that changed.");

            var swapped = BuildPattern(64);
            var keep = swapped[10];
            swapped[10] = swapped[40];
            swapped[40] = keep;
            Assert.AreNotEqual(StarfieldBakeSeam.Hash(first), StarfieldBakeSeam.Hash(swapped),
                "The hash reads texels in order, so a picture with its texels moved is a different picture.");
        }

        static Color32[] BuildPattern(int side)
        {
            var pixels = new Color32[side * side];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32((byte)(i % 256), (byte)(i / 256), (byte)((i * 7) % 256), 255);
            }

            return pixels;
        }

        [Test]
        public void CountDifferences_CountsTheTexelsThatMoved()
        {
            var first = new Color32[16];
            var second = new Color32[16];
            for (int i = 0; i < 16; i++)
            {
                first[i] = new Color32(10, 20, 30, 255);
                second[i] = new Color32(10, 20, 30, 255);
            }

            Assert.AreEqual(0, StarfieldBakeSeam.CountDifferences(first, second));

            second[7] = new Color32(10, 21, 30, 255);
            Assert.AreEqual(1, StarfieldBakeSeam.CountDifferences(first, second));
        }

        static void AssertRecipesAreEqual(StarfieldBakeRecipe first, StarfieldBakeRecipe second)
        {
            Assert.AreEqual(first.Seed, second.Seed);
            Assert.AreEqual(first.DeepTop, second.DeepTop);
            Assert.AreEqual(first.DeepBottom, second.DeepBottom);
            Assert.AreEqual(first.GradientBands, second.GradientBands);
            Assert.AreEqual(first.NebulaAngleDegrees, second.NebulaAngleDegrees);
            Assert.AreEqual(first.NebulaOffsetPixels, second.NebulaOffsetPixels);
            Assert.AreEqual(first.NebulaWidthPixels, second.NebulaWidthPixels);
            Assert.AreEqual(first.NebulaScale, second.NebulaScale);
            Assert.AreEqual(first.NebulaWarpPixels, second.NebulaWarpPixels);
            Assert.AreEqual(first.NebulaFloor, second.NebulaFloor);
            Assert.AreEqual(first.NebulaContrast, second.NebulaContrast);
            Assert.AreEqual(first.NebulaBands, second.NebulaBands);
            Assert.AreEqual(first.NebulaCover, second.NebulaCover);
            Assert.AreEqual(first.GlowWidthBands, second.GlowWidthBands);
            Assert.AreEqual(first.GlowCover, second.GlowCover);
            Assert.AreEqual(first.NebulaEdge, second.NebulaEdge);
            Assert.AreEqual(first.NebulaMid, second.NebulaMid);
            Assert.AreEqual(first.NebulaCore, second.NebulaCore);
            Assert.AreEqual(first.LaneScale, second.LaneScale);
            Assert.AreEqual(first.LaneDepth, second.LaneDepth);
            Assert.AreEqual(first.StarStrength, second.StarStrength);
            Assert.AreEqual(first.StarDensity, second.StarDensity);
            Assert.AreEqual(first.StarSizePixels, second.StarSizePixels);
            Assert.AreEqual(first.StarSizeSpread, second.StarSizeSpread);
            Assert.AreEqual(first.StarBands, second.StarBands);
            Assert.AreEqual(first.StarCool, second.StarCool);
            Assert.AreEqual(first.StarWarm, second.StarWarm);
            Assert.AreEqual(first.Exposure, second.Exposure);
            Assert.AreEqual(first.PaletteBands, second.PaletteBands);
        }
    }
}
