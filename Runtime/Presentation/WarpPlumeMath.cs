using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// The bow plume's plate geometry and its intensity scale.
    ///
    /// A plate is a quad in the XY plane of its own space: x runs across the width, y runs from the nose at
    /// zero to the tail at the authored length. The uv follows the same rule, so a shader reads uv.y as
    /// "how far downstream from the face that burns" and needs no extra channel.
    ///
    /// The scale rule is linear in the warp intensity: an authored plate at full warp is the authored size,
    /// and half warp is half the size. The authored size lives in the mesh, so the transform carries only the
    /// intensity, and a reader of the transform sees the warp number and nothing else.
    /// </summary>
    public static class WarpPlumeMath
    {
        /// <summary>
        /// Writes the bow cap: an arc across the front of the platform, in the platform's own plane, with the
        /// plasma running downstream from it. The arc's centre sits at the mesh origin, the front of the cap
        /// points along +Z of the mesh space, and the sweep is authored wider than a half circle, so the ends
        /// wrap past the sides of the platform and taper behind them. The tail runs along -Z of the mesh
        /// space, downstream from the cap, which is where a sheath spends itself.
        ///
        /// The normals point outward from the arc's axis, so the surface that faces the travel is the front of
        /// the cap and the wrapped ends face sideways. The shader reads that normal as the brightness, which is
        /// what makes the front the hot band and the wraps the dim ends.
        ///
        /// The mesh is authored at full warp. The transform carries only the intensity, so a reader of the
        /// transform sees the warp number and nothing else.
        /// </summary>
        public static void BuildCap(Mesh mesh, float radius, float sweepDegrees, float tailLength,
            float lift, int segments)
        {
            int segs = Mathf.Max(segments, 1);
            float r = Mathf.Max(radius, 0.01f);
            float sweep = Mathf.Clamp(sweepDegrees, 1f, 360f);
            float tail = Mathf.Max(tailLength, 0.01f);
            int rows = segs + 1;

            var vertices = new Vector3[rows * 2];
            var normals = new Vector3[rows * 2];
            var uv = new Vector2[rows * 2];
            var triangles = new int[segs * 6];

            for (int i = 0; i < rows; i++)
            {
                float u = (float)i / segs;
                float angle = (u - 0.5f) * sweep * Mathf.Deg2Rad;
                float sin = Mathf.Sin(angle);
                float cos = Mathf.Cos(angle);
                var normal = new Vector3(sin, 0f, cos);
                var cap = new Vector3(sin * r, lift, cos * r);
                var tailPoint = cap + new Vector3(0f, 0f, -tail);

                int capIndex = i * 2;
                vertices[capIndex] = cap;
                normals[capIndex] = normal;
                uv[capIndex] = new Vector2(u, 0f);

                vertices[capIndex + 1] = tailPoint;
                normals[capIndex + 1] = normal;
                uv[capIndex + 1] = new Vector2(u, 1f);
            }

            for (int i = 0; i < segs; i++)
            {
                int a = i * 2;
                triangles[i * 6 + 0] = a;
                triangles[i * 6 + 1] = a + 2;
                triangles[i * 6 + 2] = a + 3;
                triangles[i * 6 + 3] = a;
                triangles[i * 6 + 4] = a + 3;
                triangles[i * 6 + 5] = a + 1;
            }

            mesh.Clear();
            mesh.name = "Warp Plume Cap";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }

        /// <summary>The transform scale a plate answers at a given warp intensity. Linear, clamped to 0..1,
        /// because the intensity is authored in 0..1 and the plate is authored at full warp.</summary>
        public static float PlateScale(float intensity)
        {
            return Mathf.Clamp01(intensity);
        }

    }
}
