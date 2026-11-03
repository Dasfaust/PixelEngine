using System.Collections.Generic;
using UnityEngine;

namespace PixelEngine.Presentation
{
    /// <summary>
    /// The runtime copy of an authored material, for a component that writes uniforms every frame.
    ///
    /// A camera component that writes `_ZoomLevel`, `_SubPixelOffset`, or `_EdgeMargin` onto the
    /// material asset in its `SerializeField` writes into the file Unity serializes. Those components
    /// carry `ExecuteAlways`, so the write happens in Edit Mode too, and Unity marks the asset dirty.
    /// Recorded on 6000.6.4f1 while the Editor sat in Edit Mode with no play pass: `Middleground.mat`
    /// and `SubpixelViewport.mat` both answered `EditorUtility.IsDirty` true, and `Assets/Camera/
    /// Background/Background.mat` states `_ZoomLevel: 1.75` on disk, which is the answer a zoom-3 view
    /// of `OrthoBackground` computes and no authored scene object asks for. The next asset save writes
    /// those numbers out, which is the drift a run record has to revert.
    ///
    /// A component that needs a per-frame uniform therefore writes a copy and draws with that copy. The
    /// authored asset is read once, at the moment the copy is made, and is never written.
    ///
    /// The copy is shared per authored asset, because the writer and the drawer must agree on one
    /// object: a camera writes the uniform and the `RawImage` samples it, and a second copy would draw
    /// the values the writer did not set. `Acquire` answers the same copy for the same authored asset.
    ///
    /// The table is static, and statics are reset by a domain reload. Play-mode exit performs one, and
    /// destroys objects created during play, so an entry left behind reads as a Unity-null reference.
    /// `Acquire` checks that and replaces the entry, so a session that keeps statics alive between
    /// play passes (Enter Play Mode Options) still draws a live material.
    /// </summary>
    public static class ViewportMaterialRuntime
    {
        private static readonly Dictionary<Material, Material> copies = new Dictionary<Material, Material>();

        /// <summary>
        /// The copy this component writes and its drawer samples. A null authored material is no ask and
        /// answers null, so a component with no viewport material keeps its current behaviour. An entry
        /// whose copy Unity has destroyed is replaced, and the replacement is read from the authored
        /// asset as it stands at that moment.
        /// </summary>
        public static Material Acquire(Material authored)
        {
            if (authored == null)
            {
                return null;
            }

            if (copies.TryGetValue(authored, out var existing))
            {
                if (existing != null)
                {
                    return existing;
                }

                copies.Remove(authored);
            }

            var copy = new Material(authored);
            copy.hideFlags = HideFlags.DontSave;
            copy.name = authored.name + " (runtime)";
            copies[authored] = copy;
            return copy;
        }

        /// <summary>
        /// The copy for <paramref name="authored"/>, or null when none exists yet. A drawer that must not
        /// build one still needs to know whether it is drawing the authored asset or a copy.
        /// </summary>
        public static bool TryGet(Material authored, out Material copy)
        {
            copy = null;
            if (authored == null)
            {
                return false;
            }

            if (!copies.TryGetValue(authored, out copy))
            {
                return false;
            }

            if (copy == null)
            {
                copies.Remove(authored);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Destroy the copy for one authored asset. A component calls this when it stops drawing, so a
        /// long Editor session does not hold a copy for a camera that is no longer running.
        /// </summary>
        public static void Release(Material authored)
        {
            if (authored == null || !copies.TryGetValue(authored, out var copy))
            {
                return;
            }

            copies.Remove(authored);
            DestroyCopy(copy);
        }

        /// <summary>
        /// Destroy every copy. A test uses this to leave no state behind for the next test.
        /// </summary>
        public static void ReleaseAll()
        {
            foreach (var pair in copies)
            {
                DestroyCopy(pair.Value);
            }

            copies.Clear();
        }

        /// <summary>
        /// Destroy one copy. While the game runs this is a deferred <see cref="Object.Destroy"/>, because a
        /// material a frame is drawing with must not vanish mid-frame. Outside the running game it is
        /// immediate, which is the shape a test teardown and an Edit-Mode release need: a deferred destroy
        /// is not legal in an EditMode test run and logs an error instead.
        /// </summary>
        private static void DestroyCopy(Material copy)
        {
            if (copy == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(copy);
            }
            else
            {
                Object.DestroyImmediate(copy);
            }
        }

        /// <summary>
        /// How many copies the table holds. A test asserts this number instead of a private field.
        /// </summary>
        public static int CopyCount => copies.Count;
    }
}
