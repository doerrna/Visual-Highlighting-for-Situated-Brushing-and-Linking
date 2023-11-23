using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public class SizeHighlighter : Highlighter
    {
        public override HighlightTechnique Mode { get { return HighlightTechnique.Size; } }

        public override void Highlight()
        {
            transform.localScale = Vector3.one * 1.5f;
        }

        public override void Unhighlight()
        {
            transform.localScale = Vector3.one;
        }

        public void OnDisable()
        {
            Unhighlight();
        }
    }
}