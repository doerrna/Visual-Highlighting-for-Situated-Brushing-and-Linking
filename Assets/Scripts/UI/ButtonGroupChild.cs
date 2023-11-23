using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

namespace BrushingAndLinking
{
    public class ButtonGroupChild : Button
    {
        public ButtonGroup ButtonGroupParent;
        public Renderer ButtonRenderer;
        public Color SelectedColour = Color.yellow;

        private Color defaultColour;

        protected virtual void Awake()
        {
            base.Awake();

            if (ButtonRenderer == null)
                ButtonRenderer = GetComponentInChildren<Renderer>();

            if (ButtonGroupParent == null)
                ButtonGroupParent = GetComponentInParent<ButtonGroup>();

            defaultColour = ButtonRenderer.material.color;
        }

        public override void Select()
        {
            ButtonGroupParent.ButtonSelected(this);
            ButtonRenderer.material.color = SelectedColour;
        }

        public virtual void Deselect()
        {
            ButtonRenderer.material.color = defaultColour;
        }
    }
}