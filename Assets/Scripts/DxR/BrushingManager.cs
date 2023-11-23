using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DxR;
using System.Linq;

namespace BrushingAndLinking
{
    public enum SelectionMode
    {
        Add,        // All newly brushed marks are added to the selection
        Subtract,   // All newly brushed marks are subtracted from the selection
        Free        // Only newly brushed marks are part of the selection
    }

    public enum BrushMode
    {
        Sphere,     // A sphere of a specified radius around the BrushPoint
        Box         // A box to be drawn from the start to the end point
    }

    public class BrushingManager : MonoBehaviour
    {
        // Singleton pattern. The BrushingManager can be accessed from anywhere using BrushingManager.Instance
        public static BrushingManager Instance { get; private set; }

        #region Public variables

        [Header("Core Brushing Variables")]
        // The main visualisation that is being brushed
        public Vis MainVis;
        // The origin point of the brush
        public Transform BrushPoint;
        // The list of DxRMarks that are brushed
        public List<Mark> BrushedMarks = new List<Mark>();
        // The list of names of Products that are brushed
        public List<string> BrushedNames = new List<string>();
        // Tne number of times per second can the brushing be updated. This is limited by framerate
        public int UpdateRate = 20;

        [Header("Brushing Properties")]
        // The shape of the brush
        public BrushMode BrushMode = BrushMode.Sphere;
        // The behaviour of the selection
        public SelectionMode SelectionMode = SelectionMode.Free;
        // The colour of the brush when it is adding and subtracting
        public Color AdditiveBrushColour = new Color(1, 1, 0, 0.3f);
        public Color SubtractiveBrushColour = new Color(1, 0, 0, 0.3f);
        // Whether the brushing is active or not
        public bool IsBrushing = false;

        [Header("Brushing Radius")]
        // The radius of the brush. Only valid for sphere brushing
        public float BrushRadius = 0.02f;

        #endregion Public variables

        #region Private variables

        // The amount of time in seconds between brush updates. This is based on the UpdateRate variable
        private float timeBetweenUpdates;
        // The timestamp of the last update
        private float lastUpdateTime;

        // The local position of the BrushPoint relative to the MainVis when brushing starts
        private Vector3 localStartPos;

        // The GameObject used to show a spherical brushing region. Only used for Sphere BrushMode
        private GameObject brushSphere;
        // The GameObject used to show a rectangular brushing region. Only used for Box BrushMode
        private GameObject brushBox;

        #endregion Private variables

        private void Awake()
        {
            // Assign this object to the Instance property if it isn't already assigned, otherwise delete this object
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;

            if (MainVis == null)
            {
                MainVis = Object.FindAnyObjectByType<Vis>();
                if (MainVis == null)
                    throw new System.Exception("There is no DxR Vis object in the scene. Brushing will only work with a DxR Vis.");
            }

            if (BrushPoint == null)
            {
                BrushPoint = new GameObject("BrushPoint").transform;
                BrushPoint.SetParent(MainVis.transform.parent);
            }

            // Calculate the amount of time between brush updates based on the given UpdateRate. This is easier to calculate
            timeBetweenUpdates = 1 / (float)UpdateRate;
        }

        /// <summary>
        /// Starts the brushing with the given BrushMode and SelectionMode properties
        /// </summary>
        public void StartBrushing()
        {
            IsBrushing = true;

            // Create and/or activate the GameObjects used to guide the brushing
            Renderer brushRenderer = null;

            switch (BrushMode)
            {
                case BrushMode.Sphere:
                    {
                        if (brushSphere == null)
                        {
                            brushSphere = Instantiate(Resources.Load("BrushSphere")) as GameObject;
                            brushSphere.transform.SetParent(MainVis.transform.parent);
                        }
                        else
                            brushSphere.SetActive(true);

                        brushRenderer = brushSphere.GetComponent<Renderer>();
                        break;
                    }

                case BrushMode.Box:
                    {
                        localStartPos = MainVis.transform.InverseTransformPoint(BrushPoint.position);
                        if (brushBox == null)
                        {
                            brushBox = Instantiate(Resources.Load("BrushBox")) as GameObject;
                            brushBox.transform.SetParent(MainVis.transform.parent);
                        }
                        else
                            brushBox.SetActive(true);

                        // We need to position the box this frame so that it doesn't "jump" suddenly
                        PositionBrushBox();

                        brushRenderer = brushBox.GetComponent<Renderer>();
                        break;
                    }
            }

            // Change the colour of the brush depending on the SelectionMode
            switch (SelectionMode)
            {
                case SelectionMode.Add:
                    brushRenderer.material.color = AdditiveBrushColour;
                    break;

                case SelectionMode.Subtract:
                    brushRenderer.material.color = SubtractiveBrushColour;
                    break;
            }

            StudyManager.Instance.InteractionOccurred(InteractionType.Brushing, "Start");
        }

        private void Update()
        {
            if (IsBrushing)
            {
                // Rate limiter. If not enough time has elapsed between the last update, then we skip brushing this frame
                if ((Time.time - lastUpdateTime) <= timeBetweenUpdates)
                    return;

                // Brushing each frame depends on the BrushMode
                switch (BrushMode)
                {
                    case BrushMode.Sphere:
                        PositionBrushSphere();
                        GetAndHighlightBrushedMarks();
                        break;

                    case BrushMode.Box:
                        PositionBrushBox();
                        // Note: Box brushing only makes a selection when the brushing ends
                        break;
                }

                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Depending on the BrushMode and SelectionMode, this function finds all Marks that are to be brushed and highlights them accordingly.
        /// </summary>
        private void GetAndHighlightBrushedMarks()
        {
            IEnumerable<Mark> newBrushedMarks = null;

            // Select the DxRMarks. This varies based on the BrushMode
            switch (BrushMode)
            {
                case BrushMode.Sphere:
                    {
                        // Get all of the GameObjects that are around the BrushPoint, and filter it so it only has DxRMarks
                        newBrushedMarks = Physics.OverlapSphere(BrushPoint.position, BrushRadius)
                            .Where(go => go.tag == "DxRMark")
                            .Select(go => go.GetComponent<Mark>())
                            .Where(mark => !mark.IsFiltered);
                        break;
                    }

                case BrushMode.Box:
                    {
                        // Get all of the GameObjects that are inside of the BrushBox, and filter it so it only has DxRMarks
                        newBrushedMarks = Physics.OverlapBox(brushBox.transform.position, new Vector3(brushBox.transform.localScale.x / 2f, brushBox.transform.localScale.y / 2f, 0.05f), brushBox.transform.rotation)
                            .Where(col => col.tag == "DxRMark")
                            .Select(col => col.GetComponent<Mark>())
                            .Where(mark => !mark.IsFiltered);
                        break;
                    }
            }

            // Based on the SelectionMode, highlight/unhighlight the DxRMarks as necessary
            switch (SelectionMode)
            {
                // NOTE: There is an issue where if a DxRMark gets filtered during a additive selection, the Product doesn't get unhighlighted
                //       This issue technically shouldn't arise if the user can't brush while filtering
                case SelectionMode.Add:
                    {
                        // For every newly brushed mark, highlight it if it was not already highlighted from before
                        foreach (Mark mark in newBrushedMarks)
                        {
                            if (!BrushedMarks.Contains(mark))
                            {
                                mark.Highlight();
                                BrushedMarks.Add(mark);
                            }
                        }
                        break;
                    }

                case SelectionMode.Subtract:
                    {
                        // For every newly brushed mark, unhighlight it if it was already highlighted from before
                        foreach (Mark mark in newBrushedMarks)
                        {
                            if (BrushedMarks.Contains(mark))
                            {
                                mark.Unhighlight();
                                BrushedMarks.Remove(mark);
                            }
                        }
                        break;
                    }

                case SelectionMode.Free:
                    {
                        // For every previously brushed mark, unhighlight it if it is not part of the newly brushed marks
                        foreach (Mark mark in BrushedMarks)
                        {
                            if (!newBrushedMarks.Contains(mark))
                            {
                                mark.Unhighlight();
                            }
                        }

                        // For every newly brushed mark, highlight it if it was not already highlighted from before
                        foreach (Mark mark in newBrushedMarks)
                        {
                            if (!BrushedMarks.Contains(mark))
                            {
                                mark.Highlight();
                            }
                        }

                        BrushedMarks = newBrushedMarks.ToList();
                        break;
                    }
            }

            // From the list of names of the Products that were brushed. This is the list we send to the HighlightManager
            BrushedNames = BrushedMarks.Select(mark => mark.ProductName).ToList();
            HighlightManager.Instance.HighlightProductsByList(BrushedNames);
        }

        /// <summary>
        /// Positions the BrushSphere. Only used for Sphere BrushMode
        /// </summary>
        private void PositionBrushSphere()
        {
            brushSphere.transform.position = BrushPoint.transform.position;
        }

        /// <summary>
        /// Positions the BrushBox. Only used for Box BrushMode.
        /// </summary>
        private void PositionBrushBox()
        {
            Vector3 p1 = localStartPos;
            Vector3 p2 = MainVis.transform.InverseTransformPoint(BrushPoint.position);

            float left = Mathf.Min(p1.x, p2.x);
            float right = Mathf.Max(p1.x, p2.x);
            float top = Mathf.Max(p1.y, p2.y);
            float bottom = Mathf.Min(p1.y, p2.y);

            Vector3 size = new Vector3(right - left, top - bottom, 0.01f);
            Vector3 centre = new Vector3((left + right) / 2f, (top + bottom) / 2f, 0);
            centre = MainVis.transform.TransformPoint(centre);

            brushBox.transform.position = centre;
            brushBox.transform.rotation = MainVis.transform.rotation;
            brushBox.transform.localScale = size;
        }

        public void StopBrushing()
        {
            switch (BrushMode)
            {
                case BrushMode.Sphere:
                    brushSphere.SetActive(false);
                    break;

                case BrushMode.Box:
                    GetAndHighlightBrushedMarks();
                    brushBox.SetActive(false);
                    break;
            }

            IsBrushing = false;

            StudyManager.Instance.InteractionOccurred(InteractionType.Brushing, "End");
        }

        public void MarkFiltered(Mark mark)
        {
            BrushedMarks.Remove(mark);
        }

        public void RemoveAllBrushing()
        {
            foreach (Mark mark in BrushedMarks)
            {
                mark.Unhighlight();
            }
            BrushedMarks.Clear();
        }
    }
}