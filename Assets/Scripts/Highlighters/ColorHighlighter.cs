using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public class ColorHighlighter : Highlighter
    {
        public override HighlightTechnique Mode { get { return HighlightTechnique.Color; } }

        private Color highlightColour = Color.yellow;
        private Dictionary<Renderer, Material[]> originalMaterials;
        private Dictionary<Renderer, Material[]> highlightMaterials;

        private bool isHighlighted = false;

        private void Awake()
        {
            originalMaterials = new Dictionary<Renderer, Material[]>();
            highlightMaterials = new Dictionary<Renderer, Material[]>();

            // Get the original materials from the renderer
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                originalMaterials[renderer] = renderer.materials;
            }

            // Create an equivalent array of materials with an unlit shader
            Material newMat = new Material(Resources.Load("ColourUnlitMat") as Material);
            newMat.color = highlightColour;
            foreach (var renderer in renderers)
            {
                var newMats= new Material[originalMaterials[renderer].Length];
                for (int i = 0; i < newMats.Length; i++)
                {
                    newMats[i] = newMat;
                }
                highlightMaterials[renderer] = newMats;
            }
        }

        public override void Highlight()
        {
            if (!isHighlighted)
            {
                // Reassign the original materials with our highlight materials
                foreach (var kvp in highlightMaterials)
                {
                    kvp.Key.materials = kvp.Value;
                }

                isHighlighted = true;
            }
        }

        public override void Unhighlight()
        {
            if (isHighlighted)
            {
                // Reassign the original materials with our highlight materials
                foreach (var kvp in originalMaterials)
                {
                    kvp.Key.materials = kvp.Value;
                }

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
    }

    // public class ColorHighlighter : Highlighter
    // {
    //     public override HighlightTechnique Mode { get { return HighlightTechnique.Color; } }

    //     private Dictionary<Material, Tuple<Color, Color>> materialColourDict = new Dictionary<Material, Tuple<Color, Color>>();
    //     private readonly Color highlightColour = Color.yellow;

    //     private bool isHighlighted = false;

    //     private void Awake()
    //     {
    //         var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);

    //         foreach (MeshRenderer meshRenderer in meshRenderers)
    //         {
    //             foreach (Material material in meshRenderer.materials)
    //             {
    //                 Color color1 = material.color;
    //                 Color color2 = material.HasColor("_Color1") ? material.GetColor("_Color1") : Color.white;

    //                 materialColourDict.Add(material, new Tuple<Color, Color>(color1, color2));
    //             }
    //         }
    //     }

    //     public override void Highlight()
    //     {
    //         if (!isHighlighted)
    //         {
    //             foreach (Material material in materialColourDict.Keys)
    //             {
    //                 material.color = highlightColour;
    //                 if (material.HasColor("_Color1"))
    //                     material.SetColor("_Color1", highlightColour);
    //             }

    //             isHighlighted = true;
    //         }
    //     }

    //     public override void Unhighlight()
    //     {
    //         if (isHighlighted)
    //         {
    //             foreach (Material material in materialColourDict.Keys)
    //             {
    //                 material.color = materialColourDict[material].Item1;
    //                 if (material.HasColor("_Color1"))
    //                     material.SetColor("_Color1", materialColourDict[material].Item2);
    //             }

    //             isHighlighted = false;
    //         }
    //     }

    //     private void OnDisable()
    //     {
    //         Unhighlight();
    //     }

    //     private void OnDestroy()
    //     {
    //         Unhighlight();
    //     }
    // }
}