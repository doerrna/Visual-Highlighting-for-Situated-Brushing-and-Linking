using UnityEngine;
using UnityEditor;

namespace BrushingAndLinking
{
    [CustomEditor(typeof(HighlightManager))]
    [CanEditMultipleObjects]
    public class HighlightManagerEditor : Editor
    {
        private HighlightManager highlightManagerScript;
        private string productToHighlight;

        private void OnEnable()
        {
            highlightManagerScript = (HighlightManager)target;
        }

        public override void OnInspectorGUI()
        {
            if (Application.isPlaying)
            {
                productToHighlight = EditorGUILayout.TextField("Product name to (un)highlight: ", productToHighlight);

                if (GUILayout.Button("Highlight Product"))
                {
                    highlightManagerScript.HighlightProductByName(productToHighlight);
                }
                if (GUILayout.Button("Unhighlight Product"))
                {
                    highlightManagerScript.UnhighlightProductByName(productToHighlight);
                }
            }

            DrawDefaultInspector();
        }
    }
}