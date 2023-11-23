using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    public class NextTaskButton : Button
    {
        public override void Select()
        {
            StudyManager.Instance.NextStudyStep();
        }
    }
}