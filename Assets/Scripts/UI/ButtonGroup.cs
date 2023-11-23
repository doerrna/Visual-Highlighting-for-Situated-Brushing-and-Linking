using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public class ButtonGroup : MonoBehaviour
    {
        public List<ButtonGroupChild> Buttons = new List<ButtonGroupChild>();
        public int DefaultButton;

        private List<Outline> outlines = new List<Outline>();

        private void Start()
        {
            Buttons[DefaultButton].Select();
        }

        public void ButtonSelected(ButtonGroupChild selectedButton)
        {
            foreach (ButtonGroupChild button in Buttons)
            {
                if (button != selectedButton)
                {
                    button.Deselect();
                }
            }
        }

        public void HighlightButtonByDimensionName(string dimName)
        {
            for (int i = 0; i < Buttons.Count; i++)
            {
                VisEncodingButton visButton = Buttons[i] as VisEncodingButton;
                if (visButton.DimensionName == dimName)
                {
                    HighlightButton(i);
                    return;
                }
            }
        }

        public void HighlightButton(int id)
        {
            Outline outline = Buttons[id].gameObject.AddComponent<Outline>();
            outline.OutlineWidth = 10f;
            outline.OutlineColor = new Color(0.545f, 0, 0);
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outlines.Add(outline);
        }

        public void UnhighlightButtons()
        {
            for (int i = 0; i < outlines.Count; i++)
            {
                Destroy(outlines[i]);
            }
            outlines.Clear();
        }
    }
}