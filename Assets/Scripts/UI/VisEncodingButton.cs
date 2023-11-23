using System.Collections;
using System.Collections.Generic;
using DxR;
using UnityEngine;

namespace BrushingAndLinking
{
    public enum VisualEncoding
    {
        x,
        y
    }

    public class VisEncodingButton : ButtonGroupChild
    {
        public VisualEncoding VisualEncoding;
        public string DimensionName;
        public string DimensionType = "quantitative";

        private Vis mainVis;

        protected override void Awake()
        {
            base.Awake();

            mainVis = FindAnyObjectByType<Vis>();
        }

        public override void Select()
        {
            base.Select();

            var json = mainVis.GetVisSpecs();

            json["encoding"][VisualEncoding.ToString()]["field"] = DimensionName;
            json["encoding"][VisualEncoding.ToString()]["type"] = DimensionType;

            mainVis.UpdateVisSpecsFromJSONNode(json);

            StudyManager.Instance.InteractionOccurred(InteractionType.DimensionChange, DimensionName);
        }
    }
}