using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BrushingAndLinking
{
    public class BrushModeButton : ButtonGroupChild
    {
        public BrushMode BrushMode;

        public override void Select()
        {
            base.Select();

            BrushingManager.Instance.BrushMode = BrushMode;

            StudyManager.Instance.InteractionOccurred(InteractionType.DimensionChange, BrushMode.ToString());
        }
    }
}