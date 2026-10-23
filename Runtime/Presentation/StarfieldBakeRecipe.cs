using System;
using UnityEngine;
using Unity.Mathematics;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// One authored parameter set for a starfield bake pass, and the seed-to-parameter table that
    /// derives it. A seed int drives every authored number through <see cref="SeedStream"/>, so the
    /// same seed names the same parameters on the machine that derives them, and no authored field
    /// sits outside the range the bake shader declares.
    ///
    /// Two recipes come from one seed. The strip recipe bakes the yaw ring: deep-space gradient, one
    /// nebula band with its wash and dust lane, and three star layers. The nadir recipe bakes the
    /// top-down cell from the same seed with <see cref="StarfieldBakeRules.NadirStarStrength"/> star
    /// strength: crisp star dots live only on the layer that rolls with a nadir yaw press, so the
    /// baked top-down picture is soft haze.
    ///
    /// Band widths and the band offset are authored as fractions of the cell height, so a bake at
    /// another resolution keeps the same picture shape. The factory turns each fraction into field
    /// pixels, the unit the shader reads.
    ///
    /// <see cref="WriteToMaterial"/> writes one pass's numbers onto a material, together with
    /// <c>_FieldDims</c>: the bake target size, which is also the period every lattice inside the
    /// field wraps at.
    ///
    /// The struct is <c>[Serializable]</c> so a MonoBehaviour can carry one as an Inspector field. The
    /// bake driver does: an authored override holds the numbers a person tuned, and Unity writes the
    /// fields into the scene only when the type carries the attribute. Without it a
    /// <c>[SerializeField]</c> field of this type is ignored, the Inspector shows nothing, and the
    /// override reads the struct's default values in play mode.
    /// </summary>
    [Serializable]
    public struct StarfieldBakeRecipe
    {
        public int Seed;

        /// <summary>The cell height in texels the pixel-unit fields were authored against: the band
        /// offset and the band width are fractions of this number.</summary>
        public int CellHeight;

        public Color DeepTop;
        public Color DeepBottom;
        public int GradientBands;

        public float NebulaAngleDegrees;
        public float NebulaOffsetPixels;
        public float NebulaWidthPixels;
        public float NebulaScale;
        public float NebulaWarpPixels;
        public float NebulaFloor;
        public float NebulaContrast;
        public int NebulaBands;
        public float NebulaCover;
        public float GlowWidthBands;
        public float GlowCover;
        public Color NebulaEdge;
        public Color NebulaMid;
        public Color NebulaCore;
        public float LaneScale;
        public float LaneDepth;

        public float StarStrength;
        public float StarDensity;
        public float StarSizePixels;
        public float StarSizeSpread;
        public int StarBands;
        public Color StarCool;
        public Color StarWarm;

        public float Exposure;
        public int PaletteBands;

        static readonly int FieldDimsId = Shader.PropertyToID("_FieldDims");
        static readonly int SeedId = Shader.PropertyToID("_Seed");
        static readonly int DeepTopId = Shader.PropertyToID("_DeepTop");
        static readonly int DeepBottomId = Shader.PropertyToID("_DeepBottom");
        static readonly int GradientBandsId = Shader.PropertyToID("_GradientBands");
        static readonly int NebulaAngleId = Shader.PropertyToID("_NebulaAngle");
        static readonly int NebulaOffsetId = Shader.PropertyToID("_NebulaOffset");
        static readonly int NebulaWidthId = Shader.PropertyToID("_NebulaWidth");
        static readonly int NebulaScaleId = Shader.PropertyToID("_NebulaScale");
        static readonly int NebulaWarpId = Shader.PropertyToID("_NebulaWarp");
        static readonly int NebulaFloorId = Shader.PropertyToID("_NebulaFloor");
        static readonly int NebulaContrastId = Shader.PropertyToID("_NebulaContrast");
        static readonly int NebulaBandsId = Shader.PropertyToID("_NebulaBands");
        static readonly int NebulaCoverId = Shader.PropertyToID("_NebulaCover");
        static readonly int GlowWidthId = Shader.PropertyToID("_GlowWidth");
        static readonly int GlowCoverId = Shader.PropertyToID("_GlowCover");
        static readonly int NebulaEdgeId = Shader.PropertyToID("_NebulaEdge");
        static readonly int NebulaMidId = Shader.PropertyToID("_NebulaMid");
        static readonly int NebulaCoreId = Shader.PropertyToID("_NebulaCore");
        static readonly int LaneScaleId = Shader.PropertyToID("_LaneScale");
        static readonly int LaneDepthId = Shader.PropertyToID("_LaneDepth");
        static readonly int StarStrengthId = Shader.PropertyToID("_StarStrength");
        static readonly int StarDensityId = Shader.PropertyToID("_StarDensity");
        static readonly int StarSizeId = Shader.PropertyToID("_StarSize");
        static readonly int StarSizeSpreadId = Shader.PropertyToID("_StarSizeSpread");
        static readonly int StarBandsId = Shader.PropertyToID("_StarBands");
        static readonly int StarCoolId = Shader.PropertyToID("_StarCool");
        static readonly int StarWarmId = Shader.PropertyToID("_StarWarm");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        static readonly int PaletteBandsId = Shader.PropertyToID("_PaletteBands");

        /// <summary>The yaw-strip recipe for a seed: stars lit, one nebula band, its wash, and its
        /// dust lane. <c>cellHeight</c> is the cell's height in texels, the unit the band widths
        /// convert to.</summary>
        public static StarfieldBakeRecipe CreateStrip(int seed, int cellHeight)
        {
            return CreateStrip(seed, cellHeight, StarfieldBakeColorRanges.FromRules());
        }

        /// <summary>The yaw-strip recipe for a seed, drawing its colors inside an authored family. The
        /// layout numbers — the nebula's angle, offset, width, scale and warp, the band counts, the star
        /// lattice, the exposure — read the same ranges the rules table names, so an authored color family
        /// moves the sky and leaves the sky's shape where the seed put it. The draw order is unchanged, so
        /// one seed keeps the same layout under any family.</summary>
        public static StarfieldBakeRecipe CreateStrip(int seed, int cellHeight,
            in StarfieldBakeColorRanges ranges)
        {
            var stream = new SeedStream(StarfieldBakeRules.ClampSeed(seed));
            var recipe = CreateBase(seed, cellHeight, ref stream, ranges);
            recipe.StarStrength = 1f;
            return recipe;
        }

        /// <summary>The nadir-cell recipe for the same seed: the same sky, no lit stars, and a
        /// softer, wider haze. Reads the seed a second time in the same draw order, so one seed
        /// gives one strip and one nadir picture that share their sky.</summary>
        public static StarfieldBakeRecipe CreateNadir(int seed, int cellHeight)
        {
            return CreateNadir(seed, cellHeight, StarfieldBakeColorRanges.FromRules());
        }

        /// <summary>The nadir-cell recipe for a seed, drawing its colors inside an authored family. Reads
        /// the seed in the same draw order as <see cref="CreateStrip(int,int,StarfieldBakeColorRanges)"/>,
        /// so the strip and the nadir view share one sky under the same family.</summary>
        public static StarfieldBakeRecipe CreateNadir(int seed, int cellHeight,
            in StarfieldBakeColorRanges ranges)
        {
            var stream = new SeedStream(StarfieldBakeRules.ClampSeed(seed));
            var recipe = CreateBase(seed, cellHeight, ref stream, ranges);

            recipe.StarStrength = StarfieldBakeRules.NadirStarStrength;
            recipe.NebulaWidthPixels *= StarfieldBakeRules.NadirWidthScale;
            recipe.GlowWidthBands *= StarfieldBakeRules.NadirGlowWidthScale;
            recipe.GlowCover *= StarfieldBakeRules.NadirGlowCoverScale;
            recipe.NebulaCover *= StarfieldBakeRules.NadirCoverScale;
            recipe.NebulaContrast *= StarfieldBakeRules.NadirContrastScale;
            recipe.NebulaWarpPixels *= StarfieldBakeRules.NadirWarpScale;
            recipe.NebulaScale *= StarfieldBakeRules.NadirScaleScale;
            recipe.GradientBands = math.max(StarfieldBakeRules.GradientBandsMin,
                recipe.GradientBands / StarfieldBakeRules.NadirGradientDivisor);
            recipe.DeepTop = StarfieldBakeRules.Scaled(recipe.DeepTop, StarfieldBakeRules.NadirDeepScale);
            recipe.DeepBottom = StarfieldBakeRules.Scaled(recipe.DeepBottom, StarfieldBakeRules.NadirDeepScale);
            recipe.Exposure *= StarfieldBakeRules.NadirExposureScale;
            recipe.PaletteBands = 0;
            return recipe;
        }

        /// <summary>Draw order for one seed. Both recipes read this order, so the strip and the
        /// nadir cell share their sky, and the recipe a seed names never depends on which factory
        /// ran first.</summary>
        static StarfieldBakeRecipe CreateBase(int seed, int cellHeight, ref SeedStream stream,
            in StarfieldBakeColorRanges ranges)
        {
            int height = math.max(cellHeight, 1);
            var recipe = new StarfieldBakeRecipe
            {
                Seed = StarfieldBakeRules.ClampSeed(seed),
                CellHeight = height
            };

            float baseHue = stream.Range(ranges.DeepHueMin, ranges.DeepHueMax);
            float saturation = stream.Range(ranges.DeepSaturationMin, ranges.DeepSaturationMax);
            recipe.DeepTop = Color.HSVToRGB(baseHue, saturation,
                stream.Range(ranges.DeepTopValueMin, ranges.DeepTopValueMax));
            recipe.DeepBottom = Color.HSVToRGB(baseHue, saturation * StarfieldBakeRules.DeepBottomSaturationScale,
                stream.Range(ranges.DeepBottomValueMin, ranges.DeepBottomValueMax));
            recipe.GradientBands = stream.IntRange(StarfieldBakeRules.GradientBandsMin, StarfieldBakeRules.GradientBandsMax);

            recipe.NebulaAngleDegrees = stream.Range(StarfieldBakeRules.NebulaAngleMin, StarfieldBakeRules.NebulaAngleMax);
            recipe.NebulaOffsetPixels = stream.Range(StarfieldBakeRules.NebulaOffsetFractionMin,
                StarfieldBakeRules.NebulaOffsetFractionMax) * height;
            recipe.NebulaWidthPixels = stream.Range(StarfieldBakeRules.NebulaWidthFractionMin,
                StarfieldBakeRules.NebulaWidthFractionMax) * height;
            recipe.NebulaScale = stream.Range(StarfieldBakeRules.NebulaScaleMin, StarfieldBakeRules.NebulaScaleMax);
            recipe.NebulaWarpPixels = stream.Range(StarfieldBakeRules.NebulaWarpMin, StarfieldBakeRules.NebulaWarpMax);
            recipe.NebulaFloor = stream.Range(StarfieldBakeRules.NebulaFloorMin, StarfieldBakeRules.NebulaFloorMax);
            recipe.NebulaContrast = stream.Range(StarfieldBakeRules.NebulaContrastMin, StarfieldBakeRules.NebulaContrastMax);
            recipe.NebulaBands = stream.IntRange(StarfieldBakeRules.NebulaBandsMin, StarfieldBakeRules.NebulaBandsMax);
            recipe.NebulaCover = stream.Range(StarfieldBakeRules.NebulaCoverMin, StarfieldBakeRules.NebulaCoverMax);
            recipe.GlowWidthBands = stream.Range(StarfieldBakeRules.GlowWidthMin, StarfieldBakeRules.GlowWidthMax);
            recipe.GlowCover = stream.Range(StarfieldBakeRules.GlowCoverMin, StarfieldBakeRules.GlowCoverMax);

            float nebulaHue = stream.Range(ranges.NebulaHueOffsetMin, ranges.NebulaHueOffsetMax) + baseHue;
            nebulaHue = nebulaHue - math.floor(nebulaHue);
            float nebulaSaturation = stream.Range(ranges.NebulaSaturationMin, ranges.NebulaSaturationMax);
            float edgeValue = stream.Range(ranges.NebulaEdgeValueMin, ranges.NebulaEdgeValueMax);
            float midValue = edgeValue + stream.Range(ranges.NebulaStepMin, ranges.NebulaStepMax);
            float coreValue = midValue + stream.Range(ranges.NebulaStepMin, ranges.NebulaStepMax);
            recipe.NebulaEdge = Color.HSVToRGB(nebulaHue, nebulaSaturation, edgeValue);
            recipe.NebulaMid = Color.HSVToRGB(nebulaHue, nebulaSaturation, midValue);
            recipe.NebulaCore = Color.HSVToRGB(nebulaHue, nebulaSaturation, coreValue);

            recipe.LaneScale = stream.Range(StarfieldBakeRules.LaneScaleMin, StarfieldBakeRules.LaneScaleMax);
            recipe.LaneDepth = stream.Range(StarfieldBakeRules.LaneDepthMin, StarfieldBakeRules.LaneDepthMax);

            recipe.StarDensity = stream.Range(StarfieldBakeRules.StarDensityMin, StarfieldBakeRules.StarDensityMax);
            recipe.StarSizePixels = stream.Range(StarfieldBakeRules.StarSizeMin, StarfieldBakeRules.StarSizeMax);
            recipe.StarSizeSpread = stream.Range(StarfieldBakeRules.StarSizeSpreadMin, StarfieldBakeRules.StarSizeSpreadMax);
            recipe.StarBands = stream.IntRange(StarfieldBakeRules.StarBandsMin, StarfieldBakeRules.StarBandsMax);
            recipe.StarCool = Color.HSVToRGB(
                stream.Range(ranges.StarCoolHueMin, ranges.StarCoolHueMax),
                stream.Range(ranges.StarCoolSaturationMin, ranges.StarCoolSaturationMax), 1f);
            recipe.StarWarm = Color.HSVToRGB(
                stream.Range(ranges.StarWarmHueMin, ranges.StarWarmHueMax),
                stream.Range(ranges.StarWarmSaturationMin, ranges.StarWarmSaturationMax), 1f);

            recipe.Exposure = stream.Range(StarfieldBakeRules.ExposureMin, StarfieldBakeRules.ExposureMax);
            recipe.PaletteBands = stream.Range(0f, 1f) < StarfieldBakeRules.PaletteChance
                ? stream.IntRange(StarfieldBakeRules.PaletteBandsMin, StarfieldBakeRules.PaletteBandsMax)
                : 0;
            return recipe;
        }

        /// <summary>Write this pass's numbers onto a bake material, with the target size the
        /// lattices wrap at.</summary>
        public void WriteToMaterial(Material material, int2 fieldDims)
        {
            material.SetVector(FieldDimsId, new Vector4(fieldDims.x, fieldDims.y, 0f, 0f));
            material.SetFloat(SeedId, Seed);
            material.SetColor(DeepTopId, DeepTop);
            material.SetColor(DeepBottomId, DeepBottom);
            material.SetFloat(GradientBandsId, GradientBands);
            material.SetFloat(NebulaAngleId, NebulaAngleDegrees);
            material.SetFloat(NebulaOffsetId, NebulaOffsetPixels);
            material.SetFloat(NebulaWidthId, NebulaWidthPixels);
            material.SetFloat(NebulaScaleId, NebulaScale);
            material.SetFloat(NebulaWarpId, NebulaWarpPixels);
            material.SetFloat(NebulaFloorId, NebulaFloor);
            material.SetFloat(NebulaContrastId, NebulaContrast);
            material.SetFloat(NebulaBandsId, NebulaBands);
            material.SetFloat(NebulaCoverId, NebulaCover);
            material.SetFloat(GlowWidthId, GlowWidthBands);
            material.SetFloat(GlowCoverId, GlowCover);
            material.SetColor(NebulaEdgeId, NebulaEdge);
            material.SetColor(NebulaMidId, NebulaMid);
            material.SetColor(NebulaCoreId, NebulaCore);
            material.SetFloat(LaneScaleId, LaneScale);
            material.SetFloat(LaneDepthId, LaneDepth);
            material.SetFloat(StarStrengthId, StarStrength);
            material.SetFloat(StarDensityId, StarDensity);
            material.SetFloat(StarSizeId, StarSizePixels);
            material.SetFloat(StarSizeSpreadId, StarSizeSpread);
            material.SetFloat(StarBandsId, StarBands);
            material.SetColor(StarCoolId, StarCool);
            material.SetColor(StarWarmId, StarWarm);
            material.SetFloat(ExposureId, Exposure);
            material.SetFloat(PaletteBandsId, PaletteBands);
        }
    }

    /// <summary>
    /// <summary>
    /// The twelve color draws a seed makes, each as a min and a max the draw runs inside. A caller
    /// authors these to state a color family — a teal sky, a red nebula, warm stars — and every seed
    /// keeps drawing its own picture inside that family. The seed's draw order is fixed, so the layout
    /// numbers a seed names stay the same when only the color ranges move.
    ///
    /// <see cref="FromRules"/> answers the ranges the rules table ships, so a caller that names nothing
    /// draws exactly what the table draws today. Hue, saturation, and value all run 0 to 1, the same
    /// order <c>Color.HSVToRGB</c> reads. The nebula range is an offset added to the deep-space base
    /// hue, so a nebula stays in the neighborhood of its sky the way the rules table keeps it.
    /// </summary>
    [Serializable]
    public struct StarfieldBakeColorRanges
    {
        public float DeepHueMin, DeepHueMax;
        public float DeepSaturationMin, DeepSaturationMax;
        public float DeepTopValueMin, DeepTopValueMax;
        public float DeepBottomValueMin, DeepBottomValueMax;

        public float NebulaHueOffsetMin, NebulaHueOffsetMax;
        public float NebulaSaturationMin, NebulaSaturationMax;
        public float NebulaEdgeValueMin, NebulaEdgeValueMax;
        public float NebulaStepMin, NebulaStepMax;

        public float StarCoolHueMin, StarCoolHueMax;
        public float StarCoolSaturationMin, StarCoolSaturationMax;
        public float StarWarmHueMin, StarWarmHueMax;
        public float StarWarmSaturationMin, StarWarmSaturationMax;

        /// <summary>The ranges the rules table ships. A caller that passes these answers the bake the
        /// rules table produces, field for field.</summary>
        public static StarfieldBakeColorRanges FromRules()
        {
            return new StarfieldBakeColorRanges
            {
                DeepHueMin = StarfieldBakeRules.DeepHueMin,
                DeepHueMax = StarfieldBakeRules.DeepHueMax,
                DeepSaturationMin = StarfieldBakeRules.DeepSaturationMin,
                DeepSaturationMax = StarfieldBakeRules.DeepSaturationMax,
                DeepTopValueMin = StarfieldBakeRules.DeepTopValueMin,
                DeepTopValueMax = StarfieldBakeRules.DeepTopValueMax,
                DeepBottomValueMin = StarfieldBakeRules.DeepBottomValueMin,
                DeepBottomValueMax = StarfieldBakeRules.DeepBottomValueMax,

                NebulaHueOffsetMin = StarfieldBakeRules.NebulaHueMin,
                NebulaHueOffsetMax = StarfieldBakeRules.NebulaHueMax,
                NebulaSaturationMin = StarfieldBakeRules.NebulaSaturationMin,
                NebulaSaturationMax = StarfieldBakeRules.NebulaSaturationMax,
                NebulaEdgeValueMin = StarfieldBakeRules.NebulaEdgeValueMin,
                NebulaEdgeValueMax = StarfieldBakeRules.NebulaEdgeValueMax,
                NebulaStepMin = StarfieldBakeRules.NebulaStepMin,
                NebulaStepMax = StarfieldBakeRules.NebulaStepMax,

                StarCoolHueMin = StarfieldBakeRules.StarCoolHueMin,
                StarCoolHueMax = StarfieldBakeRules.StarCoolHueMax,
                StarCoolSaturationMin = StarfieldBakeRules.StarCoolSaturationMin,
                StarCoolSaturationMax = StarfieldBakeRules.StarCoolSaturationMax,
                StarWarmHueMin = StarfieldBakeRules.StarWarmHueMin,
                StarWarmHueMax = StarfieldBakeRules.StarWarmHueMax,
                StarWarmSaturationMin = StarfieldBakeRules.StarWarmSaturationMin,
                StarWarmSaturationMax = StarfieldBakeRules.StarWarmSaturationMax
            };
        }
    }

    /// The authored ranges the seed-to-parameter table draws inside, and the constants the nadir
    /// variant applies. Every range here names a range the bake shader declares for the same
    /// property, so a derived value never lands on a number the shader does not declare.
    ///
    /// The seed range is stated on purpose. The seed reaches the shader as a float, and a float
    /// carries whole-number precision to 2^24, so the table caps the seed below that number. A seed
    /// above the cap is clamped, and a clamp is the report a caller reads.
    /// </summary>
    public static class StarfieldBakeRules
    {
        public const int SeedMin = 0;
        public const int SeedMax = 999_999;

        public const float DeepHueMin = 0.55f, DeepHueMax = 0.95f;
        public const float DeepSaturationMin = 0.55f, DeepSaturationMax = 0.85f;
        public const float DeepTopValueMin = 0.03f, DeepTopValueMax = 0.07f;
        public const float DeepBottomValueMin = 0.08f, DeepBottomValueMax = 0.18f;
        public const float DeepBottomSaturationScale = 0.85f;

        public const int GradientBandsMin = 8, GradientBandsMax = 18;

        public const float NebulaAngleMin = 0f, NebulaAngleMax = 180f;
        public const float NebulaOffsetFractionMin = -0.18f, NebulaOffsetFractionMax = 0.18f;
        public const float NebulaWidthFractionMin = 0.14f, NebulaWidthFractionMax = 0.42f;
        public const float NebulaScaleMin = 0.045f, NebulaScaleMax = 0.09f;
        public const float NebulaWarpMin = 4f, NebulaWarpMax = 14f;
        public const float NebulaFloorMin = 0.20f, NebulaFloorMax = 0.38f;
        public const float NebulaContrastMin = 1.8f, NebulaContrastMax = 3.4f;
        public const int NebulaBandsMin = 4, NebulaBandsMax = 8;
        public const float NebulaCoverMin = 0.55f, NebulaCoverMax = 0.90f;
        public const float GlowWidthMin = 2.0f, GlowWidthMax = 4.5f;
        public const float GlowCoverMin = 0.25f, GlowCoverMax = 0.55f;

        public const float NebulaHueMin = -0.06f, NebulaHueMax = 0.06f;
        public const float NebulaSaturationMin = 0.35f, NebulaSaturationMax = 0.70f;
        public const float NebulaEdgeValueMin = 0.20f, NebulaEdgeValueMax = 0.32f;
        public const float NebulaStepMin = 0.10f, NebulaStepMax = 0.18f;
        public const float NebulaValueCeiling = 0.72f;

        public const float LaneScaleMin = 0.035f, LaneScaleMax = 0.07f;
        public const float LaneDepthMin = 0.35f, LaneDepthMax = 0.75f;

        public const float StarDensityMin = 0.55f, StarDensityMax = 0.90f;
        public const float StarSizeMin = 0.50f, StarSizeMax = 0.80f;
        public const float StarSizeSpreadMin = 1.6f, StarSizeSpreadMax = 3.0f;
        public const int StarBandsMin = 4, StarBandsMax = 7;
        public const float StarCoolHueMin = 0.52f, StarCoolHueMax = 0.60f;
        public const float StarCoolSaturationMin = 0.35f, StarCoolSaturationMax = 0.60f;
        public const float StarWarmHueMin = 0.03f, StarWarmHueMax = 0.12f;
        public const float StarWarmSaturationMin = 0.25f, StarWarmSaturationMax = 0.50f;

        public const float ExposureMin = 0.90f, ExposureMax = 1.15f;
        public const float PaletteChance = 0.34f;
        public const int PaletteBandsMin = 12, PaletteBandsMax = 24;

        public const float NadirStarStrength = 0f;
        public const float NadirWidthScale = 1.7f;
        public const float NadirGlowWidthScale = 1.6f;
        public const float NadirGlowCoverScale = 0.9f;
        public const float NadirCoverScale = 0.8f;
        public const float NadirContrastScale = 0.45f;
        public const float NadirWarpScale = 1.3f;
        public const float NadirScaleScale = 0.6f;
        public const int NadirGradientDivisor = 2;
        public const float NadirDeepScale = 0.7f;
        public const float NadirExposureScale = 0.85f;

        /// <summary>Fold a seed into the authored range, so a caller cannot hand a value the float
        /// precision of the shader's seed uniform cannot carry.</summary>
        public static int ClampSeed(int seed)
        {
            if (seed < SeedMin)
            {
                return SeedMin;
            }

            if (seed > SeedMax)
            {
                return SeedMax;
            }

            return seed;
        }

        /// <summary>True when every derived number sits inside the window the bake shader declares
        /// for its property. A number outside that window is a parameter the shader's own Range
        /// declaration does not name.</summary>
        public static bool InsideShaderWindow(in StarfieldBakeRecipe recipe)
        {
            return InRange(recipe.GradientBands, StarfieldBakeShaderRanges.GradientBandsMin, StarfieldBakeShaderRanges.GradientBandsMax)
                && InRange(recipe.NebulaAngleDegrees, StarfieldBakeShaderRanges.NebulaAngleMin, StarfieldBakeShaderRanges.NebulaAngleMax)
                && InRange(recipe.NebulaFloor, StarfieldBakeShaderRanges.NebulaFloorMin, StarfieldBakeShaderRanges.NebulaFloorMax)
                && InRange(recipe.NebulaContrast, StarfieldBakeShaderRanges.NebulaContrastMin, StarfieldBakeShaderRanges.NebulaContrastMax)
                && InRange(recipe.NebulaBands, StarfieldBakeShaderRanges.NebulaBandsMin, StarfieldBakeShaderRanges.NebulaBandsMax)
                && InRange(recipe.NebulaCover, StarfieldBakeShaderRanges.NebulaCoverMin, StarfieldBakeShaderRanges.NebulaCoverMax)
                && InRange(recipe.GlowCover, StarfieldBakeShaderRanges.GlowCoverMin, StarfieldBakeShaderRanges.GlowCoverMax)
                && InRange(recipe.LaneDepth, StarfieldBakeShaderRanges.LaneDepthMin, StarfieldBakeShaderRanges.LaneDepthMax)
                && InRange(recipe.StarStrength, StarfieldBakeShaderRanges.StarStrengthMin, StarfieldBakeShaderRanges.StarStrengthMax)
                && InRange(recipe.StarDensity, StarfieldBakeShaderRanges.StarDensityMin, StarfieldBakeShaderRanges.StarDensityMax)
                && InRange(recipe.StarSizePixels, StarfieldBakeShaderRanges.StarSizeMin, StarfieldBakeShaderRanges.StarSizeMax)
                && InRange(recipe.StarSizeSpread, StarfieldBakeShaderRanges.StarSizeSpreadMin, StarfieldBakeShaderRanges.StarSizeSpreadMax)
                && InRange(recipe.StarBands, StarfieldBakeShaderRanges.StarBandsMin, StarfieldBakeShaderRanges.StarBandsMax)
                && InRange(recipe.Exposure, StarfieldBakeShaderRanges.ExposureMin, StarfieldBakeShaderRanges.ExposureMax)
                && InRange(recipe.PaletteBands, StarfieldBakeShaderRanges.PaletteBandsMin, StarfieldBakeShaderRanges.PaletteBandsMax);
        }

        /// <summary>True when every derived number sits inside the seed table's authored range for
        /// its kind. The nadir recipe is the strip recipe with the nadir scales applied, so its
        /// bounds are the authored bounds multiplied by the same scales. The nebula band colors run
        /// edge to mid to core in lightness, and the core stays under the authored ceiling: a band
        /// whose core is darker than its mid reads as a hole and not as a bright center.</summary>
        public static bool InsideSeedTable(in StarfieldBakeRecipe recipe)
        {
            return SeedTableFault(recipe).Length == 0;
        }

        /// <summary>The first seed-table rule a recipe breaks, as the field name and the number it
        /// reads, or an empty string when every rule holds. The report names the field, so a seed
        /// that draws a bad number says which field to retune. The nadir recipe is the strip recipe
        /// with the nadir scales applied, so its bounds are the authored bounds multiplied by the
        /// same scales. The nebula band colors run edge to mid to core in lightness, and the core
        /// stays under the authored ceiling: a band whose core is darker than its mid reads as a
        /// hole and not as a bright center.</summary>
        public static string SeedTableFault(in StarfieldBakeRecipe recipe)
        {
            bool nadir = recipe.StarStrength == NadirStarStrength;
            float widthScale = nadir ? NadirWidthScale : 1f;
            float glowWidthScale = nadir ? NadirGlowWidthScale : 1f;
            float glowCoverScale = nadir ? NadirGlowCoverScale : 1f;
            float coverScale = nadir ? NadirCoverScale : 1f;
            float contrastScale = nadir ? NadirContrastScale : 1f;
            float warpScale = nadir ? NadirWarpScale : 1f;
            float scaleScale = nadir ? NadirScaleScale : 1f;
            float exposureScale = nadir ? NadirExposureScale : 1f;
            int gradientMin = nadir ? math.min(GradientBandsMin, GradientBandsMin / NadirGradientDivisor) : GradientBandsMin;

            // Grayscale is the lightness read a color answers, and the three band colors share a
            // hue and a saturation, so their grayscale order is their authored value order.
            if (!InRange(recipe.Seed, SeedMin, SeedMax)) { return "seed " + recipe.Seed; }
            if (!InRange(recipe.GradientBands, gradientMin, GradientBandsMax)) { return "gradient bands " + recipe.GradientBands; }
            if (!InRange(recipe.NebulaAngleDegrees, NebulaAngleMin, NebulaAngleMax)) { return "nebula angle " + recipe.NebulaAngleDegrees; }
            if (!InRange(recipe.NebulaScale, NebulaScaleMin * scaleScale, NebulaScaleMax * scaleScale)) { return "nebula scale " + recipe.NebulaScale; }
            if (!InRange(recipe.NebulaWarpPixels, NebulaWarpMin * warpScale, NebulaWarpMax * warpScale)) { return "nebula warp " + recipe.NebulaWarpPixels; }
            if (!InRange(recipe.NebulaFloor, NebulaFloorMin, NebulaFloorMax)) { return "nebula floor " + recipe.NebulaFloor; }
            if (!InRange(recipe.NebulaContrast, NebulaContrastMin * contrastScale, NebulaContrastMax * contrastScale)) { return "nebula contrast " + recipe.NebulaContrast; }
            if (!InRange(recipe.NebulaBands, NebulaBandsMin, NebulaBandsMax)) { return "nebula bands " + recipe.NebulaBands; }
            if (!InRange(recipe.NebulaCover, NebulaCoverMin * coverScale, NebulaCoverMax * coverScale)) { return "nebula cover " + recipe.NebulaCover; }
            if (!InRange(recipe.GlowWidthBands, GlowWidthMin * glowWidthScale, GlowWidthMax * glowWidthScale)) { return "glow width " + recipe.GlowWidthBands; }
            if (!InRange(recipe.GlowCover, GlowCoverMin * glowCoverScale, GlowCoverMax * glowCoverScale)) { return "glow cover " + recipe.GlowCover; }
            if (!InRange(recipe.NebulaWidthPixels, NebulaWidthFractionMin * recipe.CellHeight * widthScale, NebulaWidthFractionMax * recipe.CellHeight * widthScale)) { return "nebula width " + recipe.NebulaWidthPixels; }
            if (!InRange(recipe.NebulaOffsetPixels, NebulaOffsetFractionMin * recipe.CellHeight, NebulaOffsetFractionMax * recipe.CellHeight)) { return "nebula offset " + recipe.NebulaOffsetPixels; }
            if (!InRange(recipe.LaneScale, LaneScaleMin, LaneScaleMax)) { return "lane scale " + recipe.LaneScale; }
            if (!InRange(recipe.LaneDepth, LaneDepthMin, LaneDepthMax)) { return "lane depth " + recipe.LaneDepth; }
            if (!InRange(recipe.StarStrength, NadirStarStrength, 1f)) { return "star strength " + recipe.StarStrength; }
            if (!InRange(recipe.StarDensity, StarDensityMin, StarDensityMax)) { return "star density " + recipe.StarDensity; }
            if (!InRange(recipe.StarSizePixels, StarSizeMin, StarSizeMax)) { return "star size " + recipe.StarSizePixels; }
            if (!InRange(recipe.StarSizeSpread, StarSizeSpreadMin, StarSizeSpreadMax)) { return "star size spread " + recipe.StarSizeSpread; }
            if (!InRange(recipe.StarBands, StarBandsMin, StarBandsMax)) { return "star bands " + recipe.StarBands; }
            if (!InRange(recipe.Exposure, ExposureMin * exposureScale, ExposureMax * exposureScale)) { return "exposure " + recipe.Exposure; }
            if (nadir && recipe.PaletteBands != 0) { return "nadir posterize " + recipe.PaletteBands; }
            if (!nadir && !InRange(recipe.PaletteBands, 0, PaletteBandsMax)) { return "posterize bands " + recipe.PaletteBands; }
            if (!(recipe.NebulaEdge.grayscale < recipe.NebulaMid.grayscale)) { return "nebula edge to mid"; }
            if (!(recipe.NebulaMid.grayscale < recipe.NebulaCore.grayscale)) { return "nebula mid to core"; }
            if (recipe.NebulaCore.grayscale > NebulaValueCeiling) { return "nebula core lightness " + recipe.NebulaCore.grayscale.ToString("F3"); }
            return string.Empty;
        }

        /// <summary>Darken a color by a factor on its channels. The seed table builds every color
        /// from a hue, a saturation, and a value, so a channel-scaled color is the same hue and
        /// saturation at a lower value.</summary>
        public static Color Scaled(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, 1f);
        }

        static bool InRange(float value, float min, float max)
        {
            return value >= min - 0.0001f && value <= max + 0.0001f;
        }
    }

    /// <summary>
    /// The windows the bake shader declares for the numeric properties a recipe writes. The seed
    /// table draws inside its own authored ranges, and every one of them sits inside the window
    /// named here, so a derived parameter never lands on a number the shader does not declare.
    /// A property the shader declares as a plain float carries no window and is not listed.
    /// </summary>
    public static class StarfieldBakeShaderRanges
    {
        public const float GradientBandsMin = 2f, GradientBandsMax = 32f;
        public const float NebulaAngleMin = 0f, NebulaAngleMax = 180f;
        public const float NebulaFloorMin = 0f, NebulaFloorMax = 0.9f;
        public const float NebulaContrastMin = 0.5f, NebulaContrastMax = 6f;
        public const float NebulaBandsMin = 2f, NebulaBandsMax = 32f;
        public const float NebulaCoverMin = 0f, NebulaCoverMax = 1f;
        public const float GlowCoverMin = 0f, GlowCoverMax = 1f;
        public const float LaneDepthMin = 0f, LaneDepthMax = 1f;
        public const float StarStrengthMin = 0f, StarStrengthMax = 1f;
        public const float StarDensityMin = 0f, StarDensityMax = 1f;
        public const float StarSizeMin = 0.1f, StarSizeMax = 3f;
        public const float StarSizeSpreadMin = 1f, StarSizeSpreadMax = 4f;
        public const float StarBandsMin = 2f, StarBandsMax = 16f;
        public const float ExposureMin = 0f, ExposureMax = 4f;
        public const float PaletteBandsMin = 0f, PaletteBandsMax = 64f;
    }

    /// <summary>
    /// The seed stream: a SplitMix64 generator over the seed, so one seed replays one draw order.
    /// Each call advances the stream, and the stream carries no Unity state, so a derived recipe
    /// depends on nothing but its seed.
    /// </summary>
    struct SeedStream
    {
        ulong state;

        public SeedStream(int seed)
        {
            state = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
        }

        public ulong Next()
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        /// <summary>A value in [min, max], drawn from 24 bits so the result sits inside a float
        /// exactly and replays identically.</summary>
        public float Range(float min, float max)
        {
            float unit = (Next() >> 40) / 16777216f;
            return min + (max - min) * unit;
        }

        /// <summary>A whole number in [min, max], inclusive.</summary>
        public int IntRange(int min, int max)
        {
            ulong span = (ulong)(max - min + 1);
            return min + (int)(Next() % span);
        }
    }
}
