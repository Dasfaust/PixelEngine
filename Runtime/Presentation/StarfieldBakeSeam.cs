using Unity.Mathematics;
using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// Readouts a baked strip answers on its own pixels, so a bake is measured and not looked at.
    /// The strip is cyclic: its last texel column sits beside its first, because every lattice in
    /// the field wraps at the strip width. The continuity readout compares the seam pair, the two
    /// edge columns, against interior adjacent column pairs. A strip whose seam reads what its
    /// interior reads joins the ring with no visible join, and a strip that does not carries the
    /// join the plan refuses.
    ///
    /// The re-bake readout counts texels that differ between two bakes of one seed. The bake pass
    /// carries no time term, so the count is zero on the machine that baked both.
    ///
    /// A texel pair's difference is the mean of the four channel differences, so one number names
    /// one pair and a dark pair and a lit pair never compare on different scales.
    /// </summary>
    public static class StarfieldBakeSeam
    {
        const ulong Prime = 1099511628211UL;

        /// <summary>Interior column pairs are sampled every this many columns, so the readout
        /// measures the strip's own spacing and not every texel in it.</summary>
        public const int InteriorColumnStride = 4;

        /// <summary>The seam readout for one strip: the mean per-pair difference across the ring
        /// seam, and the mean and the largest pair difference across the interior adjacent pairs the
        /// stride samples. The seam pair is an interior pair that happens to sit at the strip's
        /// edge, so the pair that answers the largest interior difference bounds it: a seam past
        /// that maximum is a join, and a seam inside it is continuous.</summary>
        public static void Analyze(Color32[] pixels, int width, int height,
            out float seamDifference, out float interiorDifference, out float interiorMaximum)
        {
            float seamSum = 0f;
            float interiorSum = 0f;
            float interiorMax = 0f;
            int interiorPairs = 0;

            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                seamSum += PairDifference(pixels[row + width - 1], pixels[row]);

                for (int x = 0; x + 1 < width; x += InteriorColumnStride)
                {
                    float pair = PairDifference(pixels[row + x], pixels[row + x + 1]);
                    interiorSum += pair;
                    interiorPairs++;
                    if (pair > interiorMax)
                    {
                        interiorMax = pair;
                    }
                }
            }

            seamDifference = height > 0 ? seamSum / height : 0f;
            interiorDifference = interiorPairs > 0 ? interiorSum / interiorPairs : 0f;
            interiorMaximum = interiorMax;
        }

        /// <summary>The texels that differ between two bakes, counted per channel. Two bakes of one
        /// seed at one size answer zero, and the count names every texel that moved.</summary>
        public static int CountDifferences(Color32[] first, Color32[] second)
        {
            int count = 0;
            int shared = first.Length < second.Length ? first.Length : second.Length;
            for (int i = 0; i < shared; i++)
            {
                if (first[i].r != second[i].r || first[i].g != second[i].g
                    || first[i].b != second[i].b || first[i].a != second[i].a)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// The atlas hash: one 64-bit FNV-1a over every texel's four channels. Two bakes of one seed
        /// answer the same hash, and a bake and the cache file it wrote answer the same hash, so a
        /// determinism record is two numbers and not two pictures. The hash is a read and not an
        /// identity: it never enters a content hash, and the background is client-side cosmetic work.
        /// </summary>
        public static ulong Hash(Color32[] pixels)
        {
            ulong hash = HashSeed;
            for (int i = 0; i < pixels.Length; i++)
            {
                hash = (hash ^ pixels[i].r) * Prime;
                hash = (hash ^ pixels[i].g) * Prime;
                hash = (hash ^ pixels[i].b) * Prime;
                hash = (hash ^ pixels[i].a) * Prime;
            }

            return hash;
        }

        /// <summary>The FNV-1a offset basis: the value a hash run starts from.</summary>
        public const ulong HashSeed = 14695981039346656037UL;

        /// <summary>
        /// The atlas hash over one run of texel bytes: RGBA bytes in texel order, the layout a
        /// <c>Color32</c> array holds in memory. A run folds into the standing hash value, so a caller
        /// that hashes a big atlas in pieces answers the same number as a caller that hashes it whole,
        /// and the piece order is the texel order. A caller that streams a file feeds this call once
        /// per piece and keeps the returned value for the next piece.
        /// </summary>
        public static ulong HashBytes(ulong hash, byte[] bytes, int start, int count)
        {
            int last = math.min(start + count, bytes.Length);
            for (int i = math.max(start, 0); i < last; i++)
            {
                hash = (hash ^ bytes[i]) * Prime;
            }

            return hash;
        }

        /// <summary>
        /// The atlas hash over a run of bytes the caller never materializes: the empty part of the
        /// atlas. Folding a zero byte leaves the hash at <c>hash * Prime</c>, so a run of zeros folds
        /// to <c>hash * Prime^count</c>, and that power is a whole number of multiplications. The
        /// answer equals the answer of the same number of zero bytes passed to <see cref="HashBytes"/>.
        /// </summary>
        public static ulong HashZeros(ulong hash, long count)
        {
            ulong factor = 1UL;
            ulong baseValue = Prime;
            long left = math.max(count, 0L);
            while (left > 0L)
            {
                if ((left & 1L) == 1L)
                {
                    factor *= baseValue;
                }

                baseValue *= baseValue;
                left >>= 1;
            }

            return hash * factor;
        }

        /// <summary>The mean channel difference between two texels, in 0..255.</summary>
        public static float PairDifference(Color32 a, Color32 b)
        {
            int delta = System.Math.Abs(a.r - b.r) + System.Math.Abs(a.g - b.g)
                + System.Math.Abs(a.b - b.b) + System.Math.Abs(a.a - b.a);
            return delta / 4f;
        }
    }
}
