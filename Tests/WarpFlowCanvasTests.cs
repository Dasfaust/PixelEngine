using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// The CameraCanvas composite wiring: the rig builds a World Space canvas
    /// carrying the authored RawImage, keeps it dark until the composite
    /// camera is named, rides it at the authored distance in front of that
    /// camera, scales it so one canvas pixel is one viewport pixel, and
    /// renders it on a layer the camera culls. The warp texture is half
    /// precision so a band at the HDR gain crosses the bloom threshold of 1.
    /// The fixture drives the rig through Initialize — AddComponent runs Awake
    /// while every serialized field is still default, so the scene-placed
    /// entry point is what the test calls. The overlay composite is the
    /// regression half: it must build no canvas and leave the image where the
    /// scene placed it.
    /// </summary>
    public class WarpFlowCanvasTests
    {
        private GameObject cameraGo;
        private GameObject rigGo;
        private GameObject imageGo;
        private WarpFlow rig;
        private Material sourceMaterial;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            cameraGo = new GameObject("WarpCanvas Test Camera");
            camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
            imageGo = new GameObject("WarpCanvas Test Image");
            var image = imageGo.AddComponent<RawImage>();
            sourceMaterial = new Material(Shader.Find("PixelEngine/Warp Flow"));
            rigGo = new GameObject("WarpCanvas Test Rig");
            // AddComponent runs Awake here. Initialize returns on the null
            // guard, so the build happens on the explicit call below, after
            // the fields exist.
            rig = rigGo.AddComponent<WarpFlow>();
            SetField("outputImage", image);
            SetField("warpMaterial", sourceMaterial);
            SetField("composite", WarpFlow.WarpComposite.CameraCanvas);
            SetField("planeDistance", 400f);
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null)
            {
                rig.DisposeRuntime();
            }
            if (rigGo != null)
            {
                Object.DestroyImmediate(rigGo);
            }
            if (imageGo != null)
            {
                Object.DestroyImmediate(imageGo);
            }
            if (cameraGo != null)
            {
                Object.DestroyImmediate(cameraGo);
            }
            if (sourceMaterial != null)
            {
                Object.DestroyImmediate(sourceMaterial);
            }
        }

        [Test]
        public void CameraCanvas_Initialize_BuildsWorldSpaceCanvasCarryingTheImage()
        {
            rig.Initialize();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas, "the camera composite builds its canvas in Initialize");
            Assert.AreEqual(RenderMode.WorldSpace, canvas.renderMode,
                "Screen Space - Camera lands after the post stack in URP; the composite must ride transparent geometry");
            Assert.IsFalse(canvas.gameObject.activeSelf,
                "an unparented canvas is loose in the world — it stands dark until the camera is named");
            Assert.AreEqual(canvas.transform, imageGo.transform.parent,
                "the canvas carries the RawImage the scene authored");
            Assert.IsInstanceOf<RenderTexture>(imageGo.GetComponent<RawImage>().texture,
                "the warp texture is wired to the image");
        }

        [Test]
        public void CameraCanvas_SetFlowWithCamera_RidesTheCompositeCameraAtTheAuthoredPlane()
        {
            rig.Initialize();
            rig.SetFlow(new float3(0f, 0f, 1f), 0.5f, camera);
            rig.SyncCameraCanvas();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas);
            Assert.IsTrue(canvas.gameObject.activeSelf, "naming the camera switches the canvas on");
            Assert.AreEqual(cameraGo.transform, canvas.transform.parent,
                "parenting to the camera anchors the plane to the view");
            Assert.AreEqual(400f, canvas.transform.localPosition.z,
                "the plane sits at the authored distance in front of the camera — the camera gazes along local +Z");
        }

        [Test]
        public void CameraCanvas_PlaneDistanceIsTheAuthoredPlane()
        {
            SetField("planeDistance", 250f);
            rig.Initialize();
            rig.SetFlow(new float3(0f, 0f, 1f), 0.5f, camera);
            rig.SyncCameraCanvas();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas);
            Assert.AreEqual(250f, canvas.transform.localPosition.z,
                "the plane distance is authored, and it is the occlusion rule");
        }

        [Test]
        public void SyncCameraCanvas_MapsOneCanvasPixelToOneViewportPixel()
        {
            rig.Initialize();
            rig.SetFlow(new float3(0f, 0f, 1f), 0.5f, camera);
            rig.SyncCameraCanvas();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas);
            var rect = canvas.GetComponent<RectTransform>();
            Assert.AreEqual(800f, rect.sizeDelta.x, 0.01f);
            Assert.AreEqual(600f, rect.sizeDelta.y, 0.01f);
            // The frustum section at the plane is 2 x orthographicSize tall;
            // spread over the viewport height, one canvas pixel is one
            // viewport pixel. The test camera's parent scale is 1.
            float expected = 2f * 5f / 600f;
            Assert.AreEqual(expected, canvas.transform.localScale.x, 0.0001f);
            Assert.AreEqual(expected, canvas.transform.localScale.y, 0.0001f);
        }

        [Test]
        public void CameraCanvas_WarpTextureIsHalfPrecisionSoGainCrossesTheBloomThreshold()
        {
            rig.Initialize();

            var texture = GetField<RenderTexture>("warpTexture");
            Assert.IsNotNull(texture);
            Assert.AreEqual(RenderTextureFormat.ARGBHalf, texture.format,
                "an 8-bit format clamps the band at 1.0 and a bloom threshold of 1.0 never fires");
        }

        [Test]
        public void CameraCanvas_CanvasSitsOnALayerTheCameraCulls()
        {
            rig.Initialize();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas);
            var layer = canvas.gameObject.layer;
            Assert.AreEqual(LayerMask.NameToLayer("UI"), layer);
            Assert.That(camera.cullingMask & (1 << layer), Is.Not.EqualTo(0),
                "a World Space canvas outside the camera's culling mask draws nothing, with no log");
        }

        [Test]
        public void ViewportCanvas_Initialize_BuildsNoCanvasAndWiresTheImageInPlace()
        {
            SetField("composite", WarpFlow.WarpComposite.ViewportCanvas);

            rig.Initialize();

            Assert.IsNull(GetField<Canvas>("flowCanvas"),
                "the overlay composite draws where the scene placed the image and builds no canvas");
            Assert.IsNull(imageGo.transform.parent);
            Assert.IsNotNull(imageGo.GetComponent<RawImage>().texture);
        }

        private void SetField(string name, object value)
        {
            Field(name).SetValue(rig, value);
        }

        private T GetField<T>(string name)
        {
            return (T)Field(name).GetValue(rig);
        }

        private static FieldInfo Field(string name)
        {
            var info = typeof(WarpFlow).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(info, $"WarpFlow carries no serialized field '{name}'");
            return info;
        }
    }
}
