using UnityEngine;
using UnityEditor;
using DxR;

namespace BrushingAndLinking
{
    [CustomEditor(typeof(BrushingManager))]
    [CanEditMultipleObjects]
    public class BrushingManagerEditor : Editor
    {
        private BrushingManager brushingManagerScript;
        private string productToHighlight;

        private void OnEnable()
        {
            brushingManagerScript = (BrushingManager)target;
        }

        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                // productToHighlight = EditorGUILayout.TextField("Product name to (un)highlight: ", productToHighlight);

                if (GUILayout.Button("Start Brushing"))
                {
                    brushingManagerScript.StartBrushing();
                }
                if (GUILayout.Button("Stop Brushing"))
                {
                    brushingManagerScript.StopBrushing();
                }
            }

            DrawDefaultInspector();
        }
    }
}