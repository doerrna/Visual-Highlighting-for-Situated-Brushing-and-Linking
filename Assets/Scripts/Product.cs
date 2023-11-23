using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace BrushingAndLinking
{
    /// <summary>
    /// The Product class is assigned to all products that the user sees and interacts with.
    ///
    /// This class manages how the highlighting behaves.
    /// </summary>
    [RequireComponent(typeof(RayInteractable))]
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(ColliderSurface))]
    public class Product : MonoBehaviour
    {
        // The Highlight technique that this Product is using
        private HighlightTechnique highlightTechnique = HighlightTechnique.None;
        // Whether this Product is Highlighted or not
        private bool isHighlighted = false;
        // The Highlighter that actually handles the highlighting
        private Highlighter highlighter;

        private RayInteractable rayInteractable;
        private Collider collider;
        private ColliderSurface colliderSurface;

        private void Awake()
        {
            rayInteractable = GetComponent<RayInteractable>();
            collider = GetComponent<Collider>();
            colliderSurface = GetComponent<ColliderSurface>();

            colliderSurface.InjectCollider(collider);
            rayInteractable.InjectSurface(colliderSurface);

            rayInteractable.WhenSelectingInteractorViewAdded += ProductSelected;

            // Rename the gameobject name if it has _Pack at the end of it
            string[] split = gameObject.name.Split('_');
            if (split[^1].ToLower().Contains("pack"))
            {
                gameObject.name = string.Join("_", split.Take(split.Length - 1));
            }
        }

        /// <summary>
        /// Sets the Highlighting mode of this Product
        /// </summary>
        /// <param name="technique"></param>
        public void SetHighlightTechnique(HighlightTechnique technique)
        {
            // Only proceed if the technique has changed
            if (highlightTechnique == technique)
                return;

            // Destroy the previous highlight technique
            if (highlighter != null)
            {
                Destroy(highlighter);
            }

            switch (technique)
            {
                case HighlightTechnique.Outline:
                {
                        highlighter = gameObject.AddComponent<OutlineHighlighter>();
                        break;
                }

                case HighlightTechnique.Color:
                {
                        highlighter = gameObject.AddComponent<ColorHighlighter>();
                        break;
                }

                case HighlightTechnique.Arrow:
                {
                        highlighter = gameObject.AddComponent<ArrowHighlighter>();
                        break;
                }

                case HighlightTechnique.Link:
                {
                        highlighter = gameObject.AddComponent<LinkHighlighter>();
                        break;
                }

                case HighlightTechnique.Size:
                {
                        highlighter = gameObject.AddComponent<SizeHighlighter>();
                        break;
                }
            }

            highlightTechnique = technique;
        }

        /// <summary>
        /// Toggles the Highlight state of this Product on or off
        /// </summary>
        public void ToggleHighlight()
        {
            SetHighlightState(!isHighlighted);
        }

        /// <summary>
        /// Sets the Highlight state of this Product based on the given value
        /// </summary>
        /// <param name="value">If true, turns on the Highlight</param>
        public void SetHighlightState(bool value)
        {
            // If the Highlight is being turned on, make sure that there is the corresponding Highlighter script on this Product
            if (value)
            {
                // If there is not already a Highlighter script, we add it
                if (highlighter == null)
                {
                    SetHighlightTechnique(highlightTechnique);
                }

                // Only call the highlight function if it is not yet highlighted
                if (!isHighlighted)
                    highlighter.Highlight();
            }
            // If the Highlight is being turned off
            else
            {
                // We don't need to remove the Highlighter script. Simply just unhighlight it
                if (highlighter != null)
                {
                    // Only call the unhighlight function if it is already highlighted
                    if (isHighlighted)
                    {
                        highlighter.Unhighlight();
                    }
                }
            }

            isHighlighted = value;
        }

        public void SetProductVisibility(bool visibility)
        {
            gameObject.SetActive(visibility);
        }

        private void ProductSelected(IInteractorView view)
        {
            StudyManager.Instance.ProductSelected(this);
        }
    }
}