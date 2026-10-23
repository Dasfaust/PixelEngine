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
        /// <summary>The transform scale a plate answers at a given warp intensity. Linear, clamped to 0..1,
        /// because the intensity is authored in 0..1 and the plate is authored at full warp.</summary>
        public static float PlateScale(float intensity)
        {
            return Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Writes a plate of the authored width and tail length into the mesh, in object space, with the nose
        /// at y = 0 and the plate centred on x = 0. The normals face +Z of the plate's own space, and the
        /// plume shader ignores them: the plate is camera-facing, so the shader builds the world position and
        /// the mesh normal carries no meaning.
        /// </summary>
        public static void BuildPlate(Mesh mesh, float width, float tailLength)
        {
            float half = Mathf.Max(width, 0.01f) * 0.5f;
            float tail = Mathf.Max(tailLength, 0.01f);

            var vertices = new Vector3[4]
            {
                new Vector3(-half, 0f, 0f),
                new Vector3(half, 0f, 0f),
                new Vector3(-half, tail, 0f),
                new Vector3(half, tail, 0f)
            };
            var uv = new Vector2[4]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            var normals = new Vector3[4]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };
            var triangles = new int[6] { 0, 2, 3, 0, 3, 1 };

            mesh.Clear();
            mesh.name = "Warp Plume Plate";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
        }
    }
}
