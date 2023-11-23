using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public abstract class Highlighter : MonoBehaviour
    {
        // This enum makes it easier to determine what mode of Highlighter the child is, rather than having to use reflection
        public abstract HighlightTechnique Mode { get; }

        public abstract void Highlight();

        public abstract void Unhighlight();
    }
}