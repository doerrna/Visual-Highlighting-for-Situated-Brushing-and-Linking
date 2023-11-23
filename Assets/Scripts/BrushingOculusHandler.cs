using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.Input;

namespace BrushingAndLinking
{
    public class BrushingOculusHandler : MonoBehaviour
    {
        public static BrushingOculusHandler Instance { get; private set; }

        #region Public variables

        // The Oculus SDK RayInteractor component. Used to perform raycasts from the controller which contains this component
        public RayInteractor LeftRayInteractor;
        public RayInteractor RightRayInteractor;
        public Handedness Handedness = Handedness.Right;

        #endregion Public variables

        #region Private variables

        // The actual ray interactor that we will be raycasting from.
        private RayInteractor rayInteractorToUse;
        // Is this handler currently brushing? Note that this is separate from the BrushingManager boolean
        private bool isBrushing = false;
        // The button used to perform additive brushing (https://developer.oculus.com/documentation/unity/unity-ovrinput/)
        private OVRInput.Axis1D BrushAddButton;
        // The button used to perform subtractive brushing
        private OVRInput.Axis1D BrushSubtractButton;
        // The extent that the buttons need to be pressed for the brushing to be enabled. For trigger-like buttons this is within the range 0..1
        private readonly float ButtonPressThreshold = 0.35f;
        // The LayerMask of for the "Brushable" layer. Used to limit the raycast to only that of the visualisation tablet's backplate
        private int brushableLayerMask;

        private bool isBrushingLocked = false;

        #endregion

        private void Awake()
        {
            // Assign this object to the Instance property if it isn't already assigned, otherwise delete this object
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;

            brushableLayerMask = LayerMask.GetMask("Brushable");
        }

        public void SetHandedness(Handedness handedness)
        {
            if (handedness == Handedness.Left)
            {
                LeftRayInteractor.enabled = true;
                RightRayInteractor.enabled = false;
                rayInteractorToUse = LeftRayInteractor;
                BrushAddButton = OVRInput.Axis1D.PrimaryIndexTrigger;
                BrushSubtractButton = OVRInput.Axis1D.PrimaryHandTrigger;
            }
            else
            {
                LeftRayInteractor.enabled = false;
                RightRayInteractor.enabled = true;
                rayInteractorToUse = RightRayInteractor;
                BrushAddButton = OVRInput.Axis1D.SecondaryIndexTrigger;
                BrushSubtractButton = OVRInput.Axis1D.SecondaryHandTrigger;
            }

            Handedness = handedness;
        }

        private void Update()
        {
            // If the button to do either brush mode was pressed but it doesn't actually hit anything, we lock out brushing
            if (!isBrushing && !isBrushingLocked)
            {
                if (OVRInput.Get(BrushAddButton, OVRInput.Controller.Active) > ButtonPressThreshold || OVRInput.Get(BrushSubtractButton, OVRInput.Controller.Active) > ButtonPressThreshold)
                {
                    if (!RaycastTargetingBrushable())
                    {
                        isBrushingLocked = true;
                    }
                }
            }
            if (isBrushingLocked)
            {
                if (OVRInput.Get(BrushAddButton, OVRInput.Controller.Active) <= ButtonPressThreshold && OVRInput.Get(BrushSubtractButton, OVRInput.Controller.Active) <= ButtonPressThreshold)
                {
                    isBrushingLocked = false;
                }
                return;
            }

            // If the button that activates the trigger for the additive brush mode is pushed past the specified threshold
            if (OVRInput.Get(BrushAddButton, OVRInput.Controller.Active) > ButtonPressThreshold)
            {
                // We were already brushing, but doing it subtractively, stop that brushing. i.e. additive takes priority over subtractive
                if (isBrushing && BrushingManager.Instance.SelectionMode == SelectionMode.Subtract)
                {
                    BrushingManager.Instance.StopBrushing();
                    isBrushing = false;
                }

                // Update the position of the BrushPoint via raycast
                if (UpdateBrushPoint())
                {
                    // If we are no longer brushing, we now start brushing. Set up the required states
                    if (!isBrushing)
                    {
                        // Make sure the selection mode is correct
                        BrushingManager.Instance.SelectionMode = SelectionMode.Add;

                        BrushingManager.Instance.StartBrushing();
                        isBrushing = true;
                    }
                }


            }
            // If the trigger not being pressed, and brushing was just occurring last frame, we stop brushing
            else if (isBrushing && BrushingManager.Instance.SelectionMode == SelectionMode.Add)
            {
                BrushingManager.Instance.StopBrushing();
                isBrushing = false;
            }

            // If either there is NO additive brushing active (remember that additive takes priority), or if there's a subtractive brushing active
            if (!(isBrushing && BrushingManager.Instance.SelectionMode != SelectionMode.Add) || (isBrushing && BrushingManager.Instance.SelectionMode == SelectionMode.Subtract))
            {
                // If the button that activates the trigger for the subtractive brush mode is pushed past the specified threshold
                if (OVRInput.Get(BrushSubtractButton, OVRInput.Controller.Active) > ButtonPressThreshold)
                {
                    // Update the position of the BrushPoint via raycast
                    if (UpdateBrushPoint())
                    {
                        // If we are not brushing, we now start brushing. Set up the required states
                        if (!isBrushing)
                        {
                            // Make sure the selection mode is correct
                            BrushingManager.Instance.SelectionMode = SelectionMode.Subtract;

                            BrushingManager.Instance.StartBrushing();
                            isBrushing = true;
                        }
                    }
                }
                // If the trigger is not being pressed, and brushing was just occurring last frame, we stop brushing
                else if (isBrushing && BrushingManager.Instance.SelectionMode == SelectionMode.Subtract)
                {
                    BrushingManager.Instance.StopBrushing();
                    isBrushing = false;
                }
            }

        }

        /// <summary>
        /// Updates the position of the BrushPoint transform on BrushingManager based on a raycast from the provided RayInteractor component.
        ///
        /// This only raycasts from one single controller, and can only raycast against GameObjects of the "Brushable" layer.
        ///
        /// Returns true if a "Brushable" GameObject was hit, and false if it wasn't
        /// </summary>
        private bool UpdateBrushPoint()
        {
            if (Physics.Raycast(rayInteractorToUse.Origin, rayInteractorToUse.Forward, out RaycastHit hitInfo, 2f))
            {
                if (hitInfo.collider.tag == "Brushable")
                {
                    BrushingManager.Instance.BrushPoint.transform.position = hitInfo.point;
                    return true;
                }
            }

            return false;
        }

        private bool RaycastTargetingBrushable()
        {
            if (Physics.Raycast(rayInteractorToUse.Origin, rayInteractorToUse.Forward, out RaycastHit hitInfo, 2f))
            {
                if (hitInfo.collider.tag == "Brushable")
                {
                    return true;
                }
            }

            return false;
        }
    }
}