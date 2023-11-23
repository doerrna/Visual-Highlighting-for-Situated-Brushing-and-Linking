using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using DxR;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using SimpleJSON;
using TMPro;
using UnityEngine;

namespace BrushingAndLinking
{
    public class FilterSliderManager : MonoBehaviour
    {
        public static FilterSliderManager Instance { get; private set; }

        public Vis MainVis;
        public Transform XMinSlider;
        public Transform XMaxSlider;
        public Transform YMinSlider;
        public Transform YMaxSlider;
        public TextMeshPro XMinTextFront;
        public TextMeshPro XMaxTextFront;
        public TextMeshPro YMinTextFront;
        public TextMeshPro YMaxTextFront;
        public TextMeshPro XMinTextBack;
        public TextMeshPro XMaxTextBack;
        public TextMeshPro YMinTextBack;
        public TextMeshPro YMaxTextBack;
        public LineRenderer XMinLineRenderer;
        public LineRenderer XMaxLineRenderer;
        public LineRenderer YMinLineRenderer;
        public LineRenderer YMaxLineRenderer;
        public LineRenderer XRangeLineRenderer;
        public LineRenderer YRangeLineRenderer;

        // The Oculus SDK RayInteractor component. Used to perform raycasts from the controller which contains this component
        public RayInteractor LeftRayInteractor;
        public RayInteractor RightRayInteractor;

        private RayInteractor rayInteractorToUse;

        public int UpdateRate = 10;

        private float xMinRange;
        private float xMaxRange;
        private float yMinRange;
        private float yMaxRange;

        private float prevXMinSliderPos;
        private float prevXMaxSliderPos;
        private float prevYMinSliderPos;
        private float prevYMaxSliderPos;

        public Collider XMinCollider;
        public Collider XMaxCollider;
        public Collider YMinCollider;
        public Collider YMaxCollider;

        private float timeBetweenUpdates;
        private float lastUpdateTime;

        private bool filterQueued = false;

        private JSONNode visSpecsInferred;

        private Vector2 xDimensionRanges;
        private Vector2 yDimensionRanges;
        private string xDimensionType;
        private string yDimensionType;
        private string xDimensionName;
        private string yDimensionName;

        private float xSliderThresholdPos;
        private float ySliderThresholdPos;

        private bool[] grabbedSliders = new bool[] { false, false, false, false };

        private void Awake()
        {
            // Assign this object to the Instance property if it isn't already assigned, otherwise delete this object
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;

            xMinRange = XMinSlider.localPosition.x;
            xMaxRange = XMaxSlider.localPosition.x;
            yMinRange = YMinSlider.localPosition.x;
            yMaxRange = YMaxSlider.localPosition.x;

            prevXMinSliderPos = xMinRange;
            prevXMaxSliderPos = xMaxRange;
            prevYMinSliderPos = yMinRange;
            prevYMaxSliderPos = yMaxRange;

            XMinLineRenderer.enabled = false;
            XMaxLineRenderer.enabled = false;
            YMinLineRenderer.enabled = false;
            YMaxLineRenderer.enabled = false;

            XMinCollider = XMinSlider.GetComponent<Collider>();
            XMaxCollider = XMaxSlider.GetComponent<Collider>();
            YMinCollider = YMinSlider.GetComponent<Collider>();
            YMaxCollider = YMaxSlider.GetComponent<Collider>();

            timeBetweenUpdates = 1 / (float)UpdateRate;

            XMinSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewAdded += (IInteractorView view) => { grabbedSliders[0] = true; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "Start"); };
            XMaxSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewAdded += (IInteractorView view) => { grabbedSliders[1] = true; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "Start"); };
            YMinSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewAdded += (IInteractorView view) => { grabbedSliders[2] = true; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "Start"); };
            YMaxSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewAdded += (IInteractorView view) => { grabbedSliders[3] = true; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "Start"); };

            XMinSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewRemoved += (IInteractorView view) => { grabbedSliders[0] = false; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "End"); };
            XMaxSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewRemoved += (IInteractorView view) => { grabbedSliders[1] = false; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "End"); };
            YMinSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewRemoved += (IInteractorView view) => { grabbedSliders[2] = false; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "End"); };
            YMaxSlider.GetComponent<RayInteractable>().WhenSelectingInteractorViewRemoved += (IInteractorView view) => { grabbedSliders[3] = false; StudyManager.Instance.InteractionOccurred(InteractionType.Filter, "End"); };

            rayInteractorToUse = RightRayInteractor;
        }

        private void Start()
        {
            visSpecsInferred = MainVis.GetVisSpecsInferred();
            MainVis.VisUpdatedInferred.AddListener(MainVisUpdated);

            UpdateDimensionRangesAndTypes();
            AdjustSliderText();
        }

        public void SetHandedness(Handedness handedness)
        {
            if (handedness == Handedness.Left)
            {
                rayInteractorToUse = LeftRayInteractor;
            }
            else
            {
                rayInteractorToUse = RightRayInteractor;
            }
        }

        public void TaskStarted()
        {
            AdjustSliderRangeGuidesAndThresholds();
        }

        private void MainVisUpdated(Vis vis, JSONNode visSpecsInferred)
        {
            this.visSpecsInferred = visSpecsInferred;
            UpdateDimensionRangesAndTypes();
            AdjustSliderText();
            AdjustSliderRangeGuidesAndThresholds();

            // This needs to be called slightly later in order to give the Marks time to update their positions
            Invoke("FilterOutOfRange", 0.02f);
        }

        private void LateUpdate()
        {
            // If the sliders are being grabbed, chances are they are being moved
            if (CheckSlidersGrabbed())
            {
                UpdateSliderPosition();

                filterQueued = true;
                ClampAndSnapSliders();
            }

            // Rate limiter. If not enough time has elapsed between the last update, then we skip filtering this frame
            if ((Time.time - lastUpdateTime) <= timeBetweenUpdates)
            {
                return;
            }

            lastUpdateTime = Time.time;

            // When the min or max slider have moved
            if (filterQueued)
            {
                ClampAndSnapSliders();
                FilterOutOfRange();
                AdjustSliderText();
                AdjustLineRenderers();
                CheckSlidersOverlappingAtEnds();

                filterQueued = false;
            }
        }
        private bool CheckSlidersGrabbed()
        {
            return grabbedSliders.Any(b => b);
        }

        /// <summary>
        /// Because the Oculus SDK ranged interaction sucks, I add a new one here that is more similar to MRTK
        /// </summary>
        private void UpdateSliderPosition()
        {
            // Update the slider that is being grabbed
            int idx = System.Array.IndexOf(grabbedSliders, true);
            Transform sliderToUpdate = null;

            if (idx == 0) sliderToUpdate = XMinSlider;
            else if (idx == 1) sliderToUpdate = XMaxSlider;
            else if (idx == 2) sliderToUpdate = YMinSlider;
            else sliderToUpdate = YMaxSlider;

            // Get the position of the raycast of the right hand with any object
            if (Physics.Raycast(rayInteractorToUse.Origin, rayInteractorToUse.Forward, out RaycastHit hitInfo, 2f))
            {
                sliderToUpdate.transform.position = hitInfo.point;
            }
        }

        private void ClampAndSnapSliders()
        {
            // CLAMP X SLIDERS
            float minXPos = XMinSlider.localPosition.x;
            float maxXPos = XMaxSlider.localPosition.x;

            // Clamp sliders
            // If min slider wasn't moved, it is the anchor for the max slider
            if (ApproximatelyEqualEpsilon(prevXMinSliderPos, XMinSlider.localPosition.x))
            {
                maxXPos = Mathf.Clamp(maxXPos, minXPos, xMaxRange);
            }
            // If max slider wasn't moved, it is the anchor for the min slider
            else if (ApproximatelyEqualEpsilon(prevXMaxSliderPos, XMaxSlider.localPosition.x))
            {
                minXPos = Mathf.Clamp(minXPos, xMinRange, maxXPos);
            }
            // If both were moved, minimum slider takes priority
            else
            {
                minXPos = Mathf.Clamp(minXPos, xMinRange, maxXPos);
                maxXPos = Mathf.Clamp(maxXPos, minXPos, xMaxRange);
            }

            // Now that we have clamped successfully, snap the sliders to the threshold if it is nearby it, AND not at the ends
            if (xDimensionType == "quantitative" && xSliderThresholdPos != -1)
            {
                if (!ApproximatelyEqualEpsilon(minXPos, xMinRange) && (Mathf.Abs(minXPos - xSliderThresholdPos) < 0.01f))
                {
                    minXPos = xSliderThresholdPos;
                }
                if (!ApproximatelyEqualEpsilon(maxXPos, xMaxRange) && (Mathf.Abs(maxXPos - xSliderThresholdPos) < 0.01f))
                {
                    maxXPos = xSliderThresholdPos;
                }
            }

            XMinSlider.localPosition = new Vector3(minXPos, 0, 0);
            XMaxSlider.localPosition = new Vector3(maxXPos, 0, 0);
            XMinSlider.localRotation = Quaternion.identity;
            XMaxSlider.localRotation = Quaternion.identity;

            prevXMinSliderPos = minXPos;
            prevXMaxSliderPos = maxXPos;

            // CLAMP Y SLIDERS
            float minYPos = YMinSlider.localPosition.x;
            float maxYPos = YMaxSlider.localPosition.x;

            // Clamp sliders
            // If min slider wasn't moved, it is the anchor for the max slider
            if (ApproximatelyEqualEpsilon(prevYMinSliderPos, YMinSlider.localPosition.x))
            {
                maxYPos = Mathf.Clamp(maxYPos, minYPos, yMaxRange);
            }
            // If max slider wasn't moved, it is the anchor for the min slider
            else if (ApproximatelyEqualEpsilon(prevYMaxSliderPos, YMaxSlider.localPosition.x))
            {
                minYPos = Mathf.Clamp(minYPos, yMinRange, maxYPos);
            }
            // If both were moved, minimum slider takes priority
            else
            {
                minYPos = Mathf.Clamp(minYPos, yMinRange, maxYPos);
                maxYPos = Mathf.Clamp(maxYPos, minYPos, yMaxRange);
            }

            // Now that we have clamped successfully, snap the sliders to the threshold if it is nearby it, AND not at the ends
            if (yDimensionType == "quantitative" && ySliderThresholdPos != -1)
            {
                if (!ApproximatelyEqualEpsilon(minYPos, yMinRange) && (Mathf.Abs(minYPos - ySliderThresholdPos) < 0.01f))
                {
                    minYPos = ySliderThresholdPos;
                }
                if (!ApproximatelyEqualEpsilon(maxYPos, yMaxRange) && (Mathf.Abs(maxYPos - ySliderThresholdPos) < 0.01f))
                {
                    maxYPos = ySliderThresholdPos;
                }
            }

            YMinSlider.localPosition = new Vector3(minYPos, 0, 0);
            YMaxSlider.localPosition = new Vector3(maxYPos, 0, 0);
            YMinSlider.localRotation = Quaternion.identity;
            YMaxSlider.localRotation = Quaternion.identity;

            prevYMinSliderPos = minYPos;
            prevYMaxSliderPos = maxYPos;
        }

        private int FindClosestValueInList(float val, List<float> list)
        {
            int max = list.Count;
            int min = 0;
            int index = max / 2;

            while (max - min > 1)
            {
                if (val < list[index])
                    max = index;
                else if (val > list[index])
                    min = index;
                else
                    return index;

                index = (max - min) / 2 + min;
            }

            if (max != list.Count &&
                    Mathf.Abs(list[max] - val) < Mathf.Abs(list[min] - val))
            {
                return max;
            }

            return min;
        }

        private void FilterOutOfRange()
        {
            List<GameObject> markGameObjects = MainVis.markInstances;

            // Make box selection using the slider positions
            float left = MainVis.transform.InverseTransformPoint(XMinSlider.position).x;
            float right = MainVis.transform.InverseTransformPoint(XMaxSlider.position).x;
            float top = MainVis.transform.InverseTransformPoint(YMaxSlider.position).y;
            float bottom = MainVis.transform.InverseTransformPoint(YMinSlider.position).y;

            Vector3 halfExtents = new Vector3((right - left) / 2f, (top - bottom) / 2f, 0.05f);
            Vector3 centre = new Vector3((left + right) / 2f, (top + bottom) / 2f, 0);
            centre = MainVis.transform.TransformPoint(centre);

            // Get the marks that are not to be filtered
            var unfilteredMarks = Physics.OverlapBox(centre, halfExtents, MainVis.transform.rotation)
                .Where(col => col.tag == "DxRMark")
                .Select(col => col.gameObject);

            // For each mark, filter/unfilter as necessary
            foreach (GameObject markGo in markGameObjects)
            {
                if (!unfilteredMarks.Contains(markGo))
                {
                    markGo.GetComponent<Mark>().Filter();
                }
                else
                {
                    markGo.GetComponent<Mark>().Unfilter();
                }
            }
        }

        // If the sliders are overlapping exactly on each other, it makes fixing it problematic
        // This function disables one of the colliders so that it can be moved properly again
        private void CheckSlidersOverlappingAtEnds()
        {
            // If both sliders are at the minimum position, disable the collider of the minimum slider
            if (ApproximatelyEqualEpsilon(XMinSlider.localPosition.x, xMinRange) && ApproximatelyEqualEpsilon(XMaxSlider.localPosition.x, xMinRange))
            {
                XMinCollider.enabled = false;
            }
            // If both sliders are at the maximum position, disable the collider of the maximum slider
            else if (ApproximatelyEqualEpsilon(XMinSlider.localPosition.x, xMaxRange) && ApproximatelyEqualEpsilon(XMaxSlider.localPosition.x, xMaxRange))
            {
                XMaxCollider.enabled = false;
            }
            else
            {
                if (!XMinCollider.enabled) XMinCollider.enabled = true;
                if (!XMaxCollider.enabled) XMaxCollider.enabled = true;
            }

            // If both sliders are at the minimum position, disable the collider of the minimum slider
            if (ApproximatelyEqualEpsilon(YMinSlider.localPosition.x, yMinRange) && ApproximatelyEqualEpsilon(YMaxSlider.localPosition.x, yMinRange))
            {
                YMinCollider.enabled = false;
            }
            // If both sliders are at the maximum position, disable the collider of the maximum slider
            else if (ApproximatelyEqualEpsilon(YMinSlider.localPosition.x, yMaxRange) && ApproximatelyEqualEpsilon(YMaxSlider.localPosition.x, yMaxRange))
            {
                YMaxCollider.enabled = false;
            }
            else
            {
                if (!YMinCollider.enabled) YMinCollider.enabled = true;
                if (!YMaxCollider.enabled) YMaxCollider.enabled = true;
            }
        }

        private void AdjustLineRenderers()
        {
            XMinLineRenderer.enabled = (XMinSlider.localPosition.x >= (xMinRange + 0.00001f));
            XMaxLineRenderer.enabled = (XMaxSlider.localPosition.x <= (xMaxRange - 0.00001f));
            YMinLineRenderer.enabled = (YMinSlider.localPosition.x >= (yMinRange + 0.00001f));
            YMaxLineRenderer.enabled = (YMaxSlider.localPosition.x <= (yMaxRange - 0.00001f));
        }

        private void UpdateDimensionRangesAndTypes()
        {
            JSONArray xDomain = visSpecsInferred["encoding"]["x"]["scale"]["domain"].AsArray;
            xDimensionRanges = new Vector2(xDomain[0].AsFloat, xDomain[1].AsFloat);

            JSONArray yDomain = visSpecsInferred["encoding"]["y"]["scale"]["domain"].AsArray;
            yDimensionRanges = new Vector2(yDomain[0].AsFloat, yDomain[1].AsFloat);

            xDimensionType = visSpecsInferred["encoding"]["x"]["type"];
            yDimensionType = visSpecsInferred["encoding"]["y"]["type"];

            xDimensionName = visSpecsInferred["encoding"]["x"]["field"];
            yDimensionName = visSpecsInferred["encoding"]["y"]["field"];
        }

        private void AdjustSliderText()
        {
            if (xDimensionType == "quantitative")
            {
                // Normalise within ranges
                XMinTextFront.text = Normalise(XMinSlider.localPosition.x, xMinRange, xMaxRange, xDimensionRanges.x, xDimensionRanges.y).ToString("F1");
                XMaxTextFront.text = Normalise(XMaxSlider.localPosition.x, xMinRange, xMaxRange, xDimensionRanges.x, xDimensionRanges.y).ToString("F1");

                XMinTextBack.text = string.Format("<mark=#000000FF padding=\"10, 10, 0, 0\">{0}</mark>", XMinTextFront.text);
                XMaxTextBack.text = string.Format("<mark=#000000FF padding=\"10, 10, 0, 0\">{0}</mark>", XMaxTextFront.text);
            }
            else
            {
                XMinTextFront.text = "";
                XMaxTextFront.text = "";
                XMinTextBack.text = "";
                XMaxTextBack.text = "";
            }

            if (yDimensionType == "quantitative")
            {
                YMinTextFront.text = Normalise(YMinSlider.localPosition.x, yMinRange, yMaxRange, yDimensionRanges.x, yDimensionRanges.y).ToString("F1");
                YMaxTextFront.text = Normalise(YMaxSlider.localPosition.x, yMinRange, yMaxRange, yDimensionRanges.x, yDimensionRanges.y).ToString("F1");

                YMinTextBack.text = string.Format("<mark=#000000FF padding=\"10, 10, 0, 0\">{0}</mark>", YMinTextFront.text);
                YMaxTextBack.text = string.Format("<mark=#000000FF padding=\"10, 10, 0, 0\">{0}</mark>", YMaxTextFront.text);
            }
            else
            {
                YMinTextFront.text = "";
                YMaxTextFront.text = "";
                YMinTextBack.text = "";
                YMaxTextBack.text = "";
            }

            if (ApproximatelyEqualEpsilon(XMinSlider.localPosition.x, xMinRange))
            {
                XMinTextFront.text = "";
                XMinTextBack.text = "";
            }
            if (ApproximatelyEqualEpsilon(XMaxSlider.localPosition.x, xMaxRange))
            {
                XMaxTextFront.text = "";
                XMaxTextBack.text = "";
            }
            if (ApproximatelyEqualEpsilon(YMinSlider.localPosition.x, yMinRange))
            {
                YMinTextFront.text = "";
                YMinTextBack.text = "";
            }
            if (ApproximatelyEqualEpsilon(YMaxSlider.localPosition.x, yMaxRange))
            {
                YMaxTextFront.text = "";
                YMaxTextBack.text = "";
            }
        }

        private float Normalise(float val, float valmin, float valmax, float min, float max)
        {
            return (((val - valmin) / (valmax - valmin)) * (max - min)) + min;
        }

        private void AdjustSliderRangeGuidesAndThresholds()
        {
            if (!StudyManager.Instance.TrialActive || (StudyManager.Instance.TrialActive && StudyManager.Instance.CurrentTrial.Task == TaskType.Tutorial))
            {
                XRangeLineRenderer.enabled = false;
                YRangeLineRenderer.enabled = false;

                Axis axis = MainVis.GetAxis("x");
                var labels = axis.GetComponentsInChildren<Transform>().Where(t => t.gameObject.name == "TickLabel").Select(t => t.GetComponent<TextMesh>());
                foreach (var label in labels)
                {
                    label.color = Color.white;
                    label.fontStyle = FontStyle.Normal;
                }

                axis = MainVis.GetAxis("y");
                labels = axis.GetComponentsInChildren<Transform>().Where(t => t.gameObject.name == "TickLabel").Select(t => t.GetComponent<TextMesh>());
                foreach (var label in labels)
                {
                    label.color = Color.white;
                    label.fontStyle = FontStyle.Normal;
                }

                return;
            }

            // X DIMENSION
            string threshold = "";
            string direction = "";
            // Check if this slider's dimensions is one of the needed dimensions in the study
            if (StudyManager.Instance.CurrentTrial.DimensionName1 == xDimensionName)
            {
                threshold = StudyManager.Instance.CurrentTrial.DimensionThreshold1;
                direction = StudyManager.Instance.CurrentTrial.DimensionDirection1;
            }
            else if (StudyManager.Instance.CurrentTrial.DimensionName2 == xDimensionName)
            {
                threshold = StudyManager.Instance.CurrentTrial.DimensionThreshold2;
                direction = StudyManager.Instance.CurrentTrial.DimensionDirection2;
            }

            // Direction will be empty either when the slider's dimension doesn't match, or when the dimension is categorical
            if (direction != "")
            {
                // Convert threshold from the data domain to the visualisation domain
                xSliderThresholdPos = Normalise(float.Parse(threshold), xDimensionRanges.x, xDimensionRanges.y, xMinRange, xMaxRange);

                // Figure out the other end of the range guide
                float otherPos = -1;
                if (direction == "less" || direction == "lowest")
                {
                    otherPos = xMinRange;
                }
                else if (direction == "more" || direction == "highest")
                {
                    otherPos = xMaxRange;
                }

                // If the start and end points are overlapping, spread them out a little bit so that the line renderer is actually visible
                float startPos = xSliderThresholdPos;
                if (startPos == otherPos)
                {
                    if (direction == "lowest")
                        startPos += 0.005f;
                    else if (direction == "highest")
                        startPos -= 0.005f;
                }

                // Assign to line renderer
                XRangeLineRenderer.enabled = true;
                XRangeLineRenderer.SetPositions(new Vector3[] { new Vector3(startPos, 0.0095f, 0), new Vector3(otherPos, 0.0095f, 0) });
            }
            // For categorical dimensions...
            else
            {
                // Set threshold to -1 to indicate it is not set
                xSliderThresholdPos = -1;
                // Hide line renderer
                XRangeLineRenderer.enabled = false;

                // Get the labels and change the required value label's colour to red
                Axis axis = MainVis.GetAxis("x");
                var labels = axis.GetComponentsInChildren<Transform>().Where(t => t.gameObject.name == "TickLabel").Select(t => t.GetComponent<TextMesh>());
                foreach (var label in labels)
                {
                    if (label.text == threshold)
                    {
                        label.color = new Color(0.545f, 0, 0);
                        label.fontStyle = FontStyle.Bold;
                    }
                    else
                    {
                        label.color = Color.white;
                        label.fontStyle = FontStyle.Normal;
                    }
                }
            }


            // Y DIMENSION
            threshold = "";
            direction = "";
            // Check if this slider's dimensions is one of the needed dimensions in the study
            if (StudyManager.Instance.CurrentTrial.DimensionName1 == yDimensionName)
            {
                threshold = StudyManager.Instance.CurrentTrial.DimensionThreshold1;
                direction = StudyManager.Instance.CurrentTrial.DimensionDirection1;
            }
            else if (StudyManager.Instance.CurrentTrial.DimensionName2 == yDimensionName)
            {
                threshold = StudyManager.Instance.CurrentTrial.DimensionThreshold2;
                direction = StudyManager.Instance.CurrentTrial.DimensionDirection2;
            }

            // Direction will be empty either when the slider's dimension doesn't match, or when the dimension is categorical
            if (direction != "")
            {
                // Convert threshold from the data domain to the visualisation domain
                ySliderThresholdPos = Normalise(float.Parse(threshold), yDimensionRanges.x, yDimensionRanges.y, yMinRange, yMaxRange);

                // Figure out the other end of the range guide
                float otherPos = -1;
                if (direction == "less" || direction == "lowest")
                {
                    otherPos = yMinRange;
                }
                else if (direction == "more" || direction == "highest")
                {
                    otherPos = yMaxRange;
                }

                // If the start and end points are overlapping, spread them out a little bit so that the line renderer is actually visible
                float startPos = ySliderThresholdPos;
                if (startPos == otherPos)
                {
                    if (direction == "lowest")
                        startPos += 0.005f;
                    else if (direction == "highest")
                        startPos -= 0.005f;
                }

                // Assign to line renderer
                YRangeLineRenderer.enabled = true;
                YRangeLineRenderer.SetPositions(new Vector3[] { new Vector3(startPos, -0.0095f, 0), new Vector3(otherPos, -0.0095f, 0) });
            }
            // For categorical dimensions...
            else
            {
                // Set threshold to -1 to indicate it is not set
                ySliderThresholdPos = -1;
                // Hide line renderer
                YRangeLineRenderer.enabled = false;

                // Get the labels and change the required value label's colour to red
                Axis axis = MainVis.GetAxis("y");
                var labels = axis.GetComponentsInChildren<Transform>().Where(t => t.gameObject.name == "TickLabel").Select(t => t.GetComponent<TextMesh>());
                foreach (var label in labels)
                {
                    if (label.text == threshold)
                    {
                        label.color = new Color(0.545f, 0, 0);
                        label.fontStyle = FontStyle.Bold;
                    }
                    else
                    {
                        label.color = Color.white;
                        label.fontStyle = FontStyle.Normal;
                    }
                }
            }
        }

        private bool ApproximatelyEqualEpsilon(float a, float b, float epsilon = 0.00001f)
        {
            const float floatNormal = (1 << 23) * float.Epsilon;
            float absA = Mathf.Abs(a);
            float absB = Mathf.Abs(b);
            float diff = Mathf.Abs(a - b);

            if (a == b)
            {
                // Shortcut, handles infinities
                return true;
            }

            if (a == 0.0f || b == 0.0f || diff < floatNormal)
            {
                // a or b is zero, or both are extremely close to it.
                // relative error is less meaningful here
                return diff < (epsilon * floatNormal);
            }

            // use relative error
            return diff / Mathf.Min((absA + absB), float.MaxValue) < epsilon;
        }

        public void ResetFiltering()
        {
            XMinSlider.localPosition = new Vector3(xMinRange, 0, 0);
            XMaxSlider.localPosition = new Vector3(xMaxRange, 0, 0);
            XMinSlider.localRotation = Quaternion.identity;
            XMaxSlider.localRotation = Quaternion.identity;

            YMinSlider.localPosition = new Vector3(yMinRange, 0, 0);
            YMaxSlider.localPosition = new Vector3(yMaxRange, 0, 0);
            YMinSlider.localRotation = Quaternion.identity;
            YMaxSlider.localRotation = Quaternion.identity;

            prevXMinSliderPos = xMinRange;
            prevXMaxSliderPos = xMaxRange;
            prevYMinSliderPos = yMinRange;
            prevYMaxSliderPos = yMaxRange;

            foreach (GameObject markGo in MainVis.markInstances)
            {
                Mark mark = markGo.GetComponent<Mark>();
                if (mark.IsFiltered)
                {
                    mark.Unfilter();
                }
            }

            AdjustSliderText();
            AdjustLineRenderers();
        }
    }
}