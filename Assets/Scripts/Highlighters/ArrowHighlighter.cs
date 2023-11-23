using System.Collections;
using System.Collections.Generic;
using Gruenefeld.OutOfView.Core;
using UnityEngine;
using System.Linq;

namespace BrushingAndLinking
{
    public class ArrowHighlighter : Highlighter
    {
        public override HighlightTechnique Mode { get { return HighlightTechnique.Arrow; } }

        private static Technique techniqueScript;

        private Outline outlineScript;
        private readonly Color outlineColor = new Color(1, 1, 0);
        private readonly float outlineWidth = 5.0f;

        private bool isHighlighted = false;

        private void Awake()
        {
            if (techniqueScript == null)
                techniqueScript = Object.FindAnyObjectByType<Technique>();
        }

        public override void Highlight()
        {
            if (!isHighlighted && !techniqueScript.targets.Contains(gameObject))
            {
                techniqueScript.targets = techniqueScript.targets.Append(gameObject).ToArray();

                isHighlighted = true;
            }
        }

        public override void Unhighlight()
        {
            if (isHighlighted && techniqueScript.targets.Contains(gameObject))
            {
                var targetsList = techniqueScript.targets.ToList();
                targetsList.Remove(gameObject);
                techniqueScript.targets = targetsList.ToArray();

                isHighlighted = false;
            }
        }

        public void OnDisable()
        {
            Unhighlight();
        }

        public void OnDestroy()
        {
            Unhighlight();
        }

        public static void VisMarksChanged()
        {
            if (techniqueScript != null)
                techniqueScript.targets = new GameObject[0];
        }
    }
}