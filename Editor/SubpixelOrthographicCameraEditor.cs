using PixelEngine.Presentation;
using UnityEditor;
using UnityEngine;

namespace PixelEngine.Editor
{
    [CustomEditor(typeof(SubpixelOrthographicCamera))]
    public class SubpixelOrthographicCameraEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            SubpixelOrthographicCamera script = (SubpixelOrthographicCamera)target;
            if (GUILayout.Button("Update Resolution"))
            {
                script.Start();
                script.UpdateResolution();
            }
        }
    }
}
