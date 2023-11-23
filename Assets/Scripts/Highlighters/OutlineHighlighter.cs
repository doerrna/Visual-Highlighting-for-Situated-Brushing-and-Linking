using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public class OutlineHighlighter : Highlighter
    {
        public override HighlightTechnique Mode { get { return HighlightTechnique.Outline; } }

        private Outline outlineScript;
        private readonly Color outlineColor = new Color(1, 1, 0);
        private readonly float outlineWidth = 5.0f;

        private bool isHighlighted = false;

        private void Awake()
        {
            outlineScript = gameObject.AddComponent<Outline>();
            outlineScript.enabled = false;
            outlineScript.OutlineColor = outlineColor;
            outlineScript.OutlineWidth = outlineWidth;
            outlineScript.OutlineMode = Outline.Mode.OutlineVisible;
        }

        public override void Highlight()
        {
            if (!isHighlighted)
            {
                outlineScript.enabled = true;

                isHighlighted = true;
            }
        }

        public override void Unhighlight()
        {
            if (isHighlighted)
            {
                outlineScript.enabled = false;

                isHighlighted = false;
            }
        }

        public void OnDisable()
        {
            Unhighlight();
        }

        public void OnDestroy()
        {
            Destroy(outlineScript);
        }
    }
}