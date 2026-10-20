using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using PixelEngine.Presentation;

namespace PixelEngine.Tests
{
    /// <summary>
    /// The CameraCanvas composite wiring: the rig builds a Screen Space -
    /// Camera canvas carrying the authored RawImage, at the authored plane
    /// distance, on the layer the camera culls, and the warp texture is half
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

        [SetUp]
        public void SetUp()
        {
            cameraGo = new GameObject("WarpCanvas Test Camera");
            cameraGo.AddComponent<Camera>();
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
        public void CameraCanvas_Initialize_BuildsScreenSpaceCameraCanvasCarryingTheImage()
        {
            rig.Initialize();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas, "the camera composite builds its canvas in Initialize");
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode);
            Assert.IsNull(canvas.transform.parent,
                "a canvas nested under the scene overlay canvas inherits the overlay's mode — the composite canvas is a root object");
            Assert.AreEqual(canvas.transform, imageGo.transform.parent,
                "the canvas carries the RawImage the scene authored");
            Assert.IsInstanceOf<RenderTexture>(imageGo.GetComponent<RawImage>().texture,
                "the warp texture is wired to the image");
        }

        [Test]
        public void CameraCanvas_Initialize_PlacesThePlaneAtTheAuthoredDistance()
        {
            SetField("planeDistance", 250f);
            rig.Initialize();

            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas);
            Assert.AreEqual(250f, canvas.planeDistance,
                "the plane distance is authored, and it is the occlusion rule");
        }

        [Test]
        public void CameraCanvas_SetFlowWithCamera_NamesTheCompositeCamera()
        {
            rig.Initialize();
            var canvas = GetField<Canvas>("flowCanvas");
            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.ScreenSpaceCamera, canvas.renderMode,
                "the mode stands when a world camera is named with it — the fallback to Overlay is the fault");

            rig.SetFlow(new float3(0f, 0f, 1f), 0.5f, cameraGo.GetComponent<Camera>());

            Assert.AreEqual(cameraGo.GetComponent<Camera>(), canvas.worldCamera,
                "the canvas draws through the camera the caller names");
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
            var mask = cameraGo.GetComponent<Camera>().cullingMask;
            Assert.That(mask & (1 << layer), Is.Not.EqualTo(0),
                "a canvas outside the camera's culling mask draws nothing, with no log");
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
