using NUnit.Framework;
using PixelEngine.Presentation;
using UnityEditor;
using UnityEngine;

namespace PixelEngine.Tests
{
    /// <summary>
    /// The runtime copy a per-frame uniform writer needs. A camera that writes `_ZoomLevel` onto the
    /// material asset it was assigned writes into the serialized file, and a component marked
    /// `ExecuteAlways` does that in Edit Mode too. The seam answers a copy instead, and the copy is shared
    /// per authored asset so the writer and the drawer agree on one object.
    ///
    /// The fixture is a material this project ships, because that is the case the seam serves. Measured on
    /// 6000.6.4f1: a copy of an authored material carries the authored `_ZoomLevel`, `_EdgeMargin`,
    /// `renderQueue`, and keyword list, while a copy of a material built by script from a shader carries
    /// none of the values the script wrote. A test built on a script material therefore asserts a case no
    /// component here ever hits. The fixture is read and never written.
    /// </summary>
    public class ViewportMaterialRuntimeTests
    {
        private const string AuthoredMaterialPath = "Assets/Camera/SubpixelViewport.mat";

        private Material authored;
        private float authoredZoom;
        private float authoredMargin;

        [SetUp]
        public void SetUp()
        {
            ViewportMaterialRuntime.ReleaseAll();
            authored = AssetDatabase.LoadAssetAtPath<Material>(AuthoredMaterialPath);
            Assert.That(authored, Is.Not.Null, $"No material at {AuthoredMaterialPath}, so no fixture exists.");
            Assert.That(authored.HasProperty("_ZoomLevel"), Is.True,
                "The fixture must carry the uniform contract this seam writes.");
            authoredZoom = authored.GetFloat("_ZoomLevel");
            authoredMargin = authored.GetFloat("_EdgeMargin");
        }

        [TearDown]
        public void TearDown()
        {
            ViewportMaterialRuntime.ReleaseAll();
        }

        [Test]
        public void Acquire_AnswersACopyAndNotTheMaterialItWasGiven()
        {
            var copy = ViewportMaterialRuntime.Acquire(authored);

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy, Is.Not.SameAs(authored));
            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(1));
        }

        [Test]
        public void AcquireTwice_AnswersTheSameCopySoTheWriterAndTheDrawerShareOneObject()
        {
            var first = ViewportMaterialRuntime.Acquire(authored);
            var second = ViewportMaterialRuntime.Acquire(authored);

            Assert.That(second, Is.SameAs(first));
            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(1));
        }

        [Test]
        public void Acquire_CopiesTheUniformContractTheAuthoredMaterialStates()
        {
            var copy = ViewportMaterialRuntime.Acquire(authored);

            Assert.That(copy.GetFloat("_ZoomLevel"), Is.EqualTo(authoredZoom));
            Assert.That(copy.GetFloat("_EdgeMargin"), Is.EqualTo(authoredMargin));
            Assert.That(copy.shader, Is.SameAs(authored.shader));
            Assert.That(copy.renderQueue, Is.EqualTo(authored.renderQueue));
        }

        [Test]
        public void Acquire_CopiesTheKeywordListSoACopyDrawsTheSameVariant()
        {
            var copy = ViewportMaterialRuntime.Acquire(authored);

            Assert.That(copy.shaderKeywords, Is.EqualTo(authored.shaderKeywords));
        }

        [Test]
        public void WritingTheCopyLeavesTheAuthoredMaterialAtItsOwnValue()
        {
            var copy = ViewportMaterialRuntime.Acquire(authored);

            copy.SetFloat("_ZoomLevel", authoredZoom + 6.25f);
            copy.SetFloat("_EdgeMargin", authoredMargin + 7f);

            Assert.That(authored.GetFloat("_ZoomLevel"), Is.EqualTo(authoredZoom),
                "A write reached the authored material instead of its copy, which is the drift this seam exists to stop.");
            Assert.That(authored.GetFloat("_EdgeMargin"), Is.EqualTo(authoredMargin));
            Assert.That(copy.GetFloat("_ZoomLevel"), Is.EqualTo(authoredZoom + 6.25f));
        }

        [Test]
        public void Acquire_OnNoAuthoredMaterialAnswersNullAndBuildsNoCopy()
        {
            Assert.That(ViewportMaterialRuntime.Acquire(null), Is.Null);
            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(0));
        }

        [Test]
        public void TryGet_AnswersNoCopyBeforeOneIsAcquired()
        {
            Assert.That(ViewportMaterialRuntime.TryGet(authored, out var missing), Is.False);
            Assert.That(missing, Is.Null);

            var copy = ViewportMaterialRuntime.Acquire(authored);

            Assert.That(ViewportMaterialRuntime.TryGet(authored, out var found), Is.True);
            Assert.That(found, Is.SameAs(copy));
        }

        [Test]
        public void Release_DropsTheEntryAndTheNextAcquireBuildsAFreshCopy()
        {
            var copy = ViewportMaterialRuntime.Acquire(authored);
            copy.SetFloat("_ZoomLevel", authoredZoom + 3f);

            ViewportMaterialRuntime.Release(authored);
            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(0));

            var fresh = ViewportMaterialRuntime.Acquire(authored);
            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(1));
            Assert.That(fresh.GetFloat("_ZoomLevel"), Is.EqualTo(authoredZoom),
                "A new copy must read the authored state and not the value the released copy was written to.");
        }

        [Test]
        public void ReleaseAll_EmptiesTheTable()
        {
            var other = AssetDatabase.LoadAssetAtPath<Material>("Assets/Camera/Middleground.mat");
            Assert.That(other, Is.Not.Null, "Middleground.mat is the second fixture and it is missing.");

            ViewportMaterialRuntime.Acquire(authored);
            ViewportMaterialRuntime.Acquire(other);
            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(2));

            ViewportMaterialRuntime.ReleaseAll();

            Assert.That(ViewportMaterialRuntime.CopyCount, Is.EqualTo(0));
        }
    }
}
