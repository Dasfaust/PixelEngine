using Unity.Mathematics;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Pure layout math for the starfield bake: the size a bake target needs, the atlas the strip
    /// lands in, the cell a pose reads, the cache file a seed writes, and the memory the atlas
    /// costs. Split out so a test drives it with no scene and no GPU.
    ///
    /// The cell is one yaw column of the atlas, and its size is the background camera's render
    /// resolution: the screen divided by the background downscale ratio, rounded up to an even
    /// number, plus the render texture's edge margin on both edges (see
    /// <see cref="SubpixelCameraBase.EdgeMarginTexels"/>). A cell texel is then one background
    /// render texel at zoom 1, so a baked star is one background pixel, and a deeper zoom minifies
    /// the cell instead of magnifying it.
    ///
    /// The atlas carries <c>columns</c> yaw cells in row 0 and the nadir cell in row 1, column 0.
    /// The strip the bake writes is row 0 whole: one wide image whose width is the cell width times
    /// the column count, so the yaw cells are slices of one continuous picture.
    ///
    /// The atlas is uncompressed RGBA32: four bytes per texel, no mipmaps. Lossy texture
    /// compression puts ringing and block artifacts on the star dots, so a pixel-art starfield
    /// keeps its texels whole and pays the byte cost instead.
    /// </summary>
    public static class StarfieldBakeLayout
    {
        /// <summary>Bytes per atlas texel: RGBA32, uncompressed, no mipmaps.</summary>
        public const int BytesPerTexel = 4;

        /// <summary>
        /// The cell size the bake targets: the background camera's render resolution for a screen
        /// of <c>screenWidth</c> by <c>screenHeight</c> at <c>backgroundDownscaleRatio</c>. The rule
        /// mirrors <see cref="BackgroundCamera"/>: divide, round an odd result up to even, then add
        /// the edge margin twice. A ratio below 1 is read as 1.
        /// </summary>
        public static int2 CellResolution(int screenWidth, int screenHeight, int backgroundDownscaleRatio)
        {
            int ratio = math.max(backgroundDownscaleRatio, 1);
            int width = math.max(screenWidth, 1) / ratio;
            int height = math.max(screenHeight, 1) / ratio;
            if (width % 2 != 0) width++;
            if (height % 2 != 0) height++;
            return new int2(
                width + (int)SubpixelCameraBase.EdgeMarginTexels * 2,
                height + (int)SubpixelCameraBase.EdgeMarginTexels * 2);
        }

        /// <summary>
        /// The atlas size: <c>columns</c> cells wide and <c>rows</c> cells tall, so the strip width
        /// is the cell width times the column count and every cell boundary lands on a whole cell.
        /// </summary>
        public static int2 AtlasSize(int cellWidth, int cellHeight, int columns, int rows)
        {
            return new int2(
                math.max(cellWidth, 1) * math.max(columns, 1),
                math.max(cellHeight, 1) * math.max(rows, 1));
        }

        /// <summary>
        /// The pixel rect of the pose's cell inside the atlas, in <c>(x, y, width, height)</c>,
        /// origin bottom-left. Pose 0..columns-1 reads row 0, column = pose. A pose at or above
        /// <c>columns</c> is the nadir pose and reads row 1, column 0, the same cell the rig
        /// addresses (see <see cref="AtlasBackgroundLayout.CellRect"/>).
        /// </summary>
        public static int4 CellPixelRect(int pose, int columns, int rows, int cellWidth, int cellHeight)
        {
            int column = pose < columns ? pose : 0;
            int row = pose < columns ? 0 : 1;
            return new int4(column * cellWidth, row * cellHeight, cellWidth, cellHeight);
        }

        /// <summary>
        /// The cache file name for one bake: the seed and the atlas size it was baked at. A seed
        /// change or a resolution change names a different file, so a re-entry hits the bake it
        /// asked for and never reads a picture baked at another size. The file carries the atlas texels
        /// deflated, so a reader expands it in pieces and writes the texels in pieces, and no single
        /// call builds the whole atlas.
        /// </summary>
        public static string CacheFileName(int seed, int atlasWidth, int atlasHeight)
        {
            return $"starfield-{seed}-{atlasWidth}x{atlasHeight}.deflate";
        }

        /// <summary>The cache file path under a folder the caller owns.</summary>
        public static string CachePath(string folder, int seed, int atlasWidth, int atlasHeight)
        {
            return $"{folder}/{CacheFileName(seed, atlasWidth, atlasHeight)}";
        }

        /// <summary>
        /// The largest divisor of <c>total</c> at or below <c>requested</c>, and never below 1. A caller
        /// that splits a whole into equal pieces takes this number, so every piece costs the same and no
        /// piece comes out short. A total of zero answers 1.
        /// </summary>
        public static int DivisorAtOrBelow(int total, int requested)
        {
            if (total <= 0)
            {
                return 1;
            }

            for (int candidate = math.min(math.max(requested, 1), total); candidate >= 2; candidate--)
            {
                if (total % candidate == 0)
                {
                    return candidate;
                }
            }

            return 1;
        }

        /// <summary>The atlas's memory in bytes: width × height × 4, the uncompressed RGBA32 cost.</summary>
        public static long MemoryBytes(int atlasWidth, int atlasHeight)
        {
            return (long)math.max(atlasWidth, 0) * math.max(atlasHeight, 0) * BytesPerTexel;
        }

        /// <summary>True when both atlas sides sit at or under a device's maximum texture size.</summary>
        public static bool FitsTextureLimit(int atlasWidth, int atlasHeight, int maxTextureSize)
        {
            return atlasWidth <= maxTextureSize && atlasHeight <= maxTextureSize;
        }
    }
}
