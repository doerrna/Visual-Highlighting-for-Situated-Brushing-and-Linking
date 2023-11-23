using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;

namespace BrushingAndLinking
{
    public class Tablet : MonoBehaviour
    {
        [Header("Main Variables")]
        public Handedness Handedness = Handedness.Right;

        [Header("Left Hand Variables")]
        public Transform LeftHandAnchor;
        public Vector3 LeftHandTranslationOffset;
        public Vector3 LeftHandRotationOffset;

        [Header("Right Hand Variables")]
        public Transform RightHandAnchor;
        public Vector3 RightHandTranslationOffset;
        public Vector3 RightHandRotationOffset;

        [Header("Tablet Children Variables")]
        public GameObject ContentParent;
        public GameObject PreTutorialControlsParent;
        public GameObject MidTutorialControlsParent;
        public GameObject PreTrialControlsParent;
        public GameObject HypothesisResponseControlsParent;
        public GameObject PostTrialControlsParent;
        public TextMeshPro TaskText;
        public TextMeshPro InfoText;

        public GameObject AlcoholButton1;
        public GameObject AlcoholButton2;

        private bool visibility = false;

        private OneEuroFilter<Vector3> positionFilter;
        private OneEuroFilter<Quaternion> rotationFilter;
        private float filterFrequency = 120f;

        private void Start()
        {
            positionFilter = new OneEuroFilter<Vector3>(filterFrequency, 0.9f, 10f);
            rotationFilter = new OneEuroFilter<Quaternion>(filterFrequency, 0.9f, 10f);
        }

        public void LateUpdate()
        {
            if (visibility)
            {
                if (Handedness == Handedness.Right && LeftHandAnchor.position != Vector3.zero)
                {
                    transform.SetParent(LeftHandAnchor);
                    transform.localPosition = LeftHandTranslationOffset;
                    transform.localEulerAngles = LeftHandRotationOffset;
                    transform.parent = null;
                }
                else if (Handedness == Handedness.Left && RightHandAnchor.position != Vector3.zero)
                {
                    transform.SetParent(RightHandAnchor);
                    transform.localPosition = RightHandTranslationOffset;
                    transform.localEulerAngles = RightHandRotationOffset;
                    transform.parent = null;
                }

                // Filter new position and rotation values
                Vector3 unfilteredPosition = transform.position;
                Quaternion unfilteredRotation = transform.rotation;

                Vector3 filteredPosition = positionFilter.Filter(unfilteredPosition);
                Quaternion filteredRotation = rotationFilter.Filter(unfilteredRotation);

                transform.position = filteredPosition;
                transform.rotation = filteredRotation;
            }
        }

        public void SetHandedness(Handedness handedness)
        {
            Handedness = handedness;
        }

        public void SetOverallVisibility(bool visibility)
        {
            this.visibility = visibility;

            if (!visibility)
            {
                transform.position = new Vector3(0, -10, 0);
            }
        }

        public void SetContentVisibility(bool visibility)
        {
            if (visibility)
            {
                ContentParent.transform.localPosition = Vector3.zero;
                ContentParent.transform.localRotation = Quaternion.identity;
                InfoText.gameObject.SetActive(false);
            }
            else
            {
                ContentParent.transform.position = new Vector3(0, -20, 0);
                InfoText.gameObject.SetActive(true);
            }
        }

        public void SetControlsVisibility(TabletControls tabletControls, bool visibility)
        {
            switch (tabletControls)
            {
                case TabletControls.PreTutorial:
                    PreTutorialControlsParent.SetActive(visibility);
                    return;

                case TabletControls.MidTutorial:
                    MidTutorialControlsParent.SetActive(visibility);
                    return;

                case TabletControls.PreTrial:
                    PreTrialControlsParent.SetActive(visibility);
                    return;

                case TabletControls.HypothesisResponse:
                    HypothesisResponseControlsParent.SetActive(visibility);
                    return;

                case TabletControls.PostTrial:
                    PostTrialControlsParent.SetActive(visibility);
                    return;

                case TabletControls.All:
                    PostTrialControlsParent.SetActive(visibility);
                    PreTrialControlsParent.SetActive(visibility);
                    HypothesisResponseControlsParent.SetActive(visibility);
                    return;
            }
        }

        public void SetTaskText(string text)
        {
            TaskText.text = text;
        }

        public void SetAlcoholButtonVisibility(bool visibility)
        {
            AlcoholButton1.SetActive(visibility);
            AlcoholButton2.SetActive(visibility);
        }
    }

    public enum TabletControls
    {
        PreTutorial,
        MidTutorial,
        PreTrial,
        HypothesisResponse,
        PostTrial,
        All
    }
}