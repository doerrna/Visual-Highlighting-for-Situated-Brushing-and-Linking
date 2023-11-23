using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public class HypothesisResponseButton : Button
    {
        public string Response;

        public override void Select()
        {
            StudyManager.Instance.ResponseGiven(Response);
        }
    }
}