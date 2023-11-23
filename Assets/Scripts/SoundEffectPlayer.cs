using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BrushingAndLinking
{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEffectPlayer : MonoBehaviour
    {
        public static SoundEffectPlayer Instance { get; private set; }

        public AudioSource AudioSource;
        public AudioClip CorrectProductSelectedClip;
        public AudioClip IncorrectProductSelectedClip;
        public AudioClip HypothesisResonseGivenClip;
        public AudioClip TrialFinishedClip;

        private void Awake()
        {
            // Assign this object to the Instance property if it isn't already assigned, otherwise delete this object
            if (Instance != null && Instance != this) Destroy(this);
            else Instance = this;

            if (AudioSource == null) AudioSource = GetComponent<AudioSource>();
        }

        public void PlayCorrectProductSelected()
        {
            if (CorrectProductSelectedClip != null)
            {
                AudioSource.clip = CorrectProductSelectedClip;
                AudioSource.Play();
            }
        }

        public void PlayIncorrectProductSelected()
        {
            if (IncorrectProductSelectedClip != null)
            {
                AudioSource.clip = IncorrectProductSelectedClip;
                AudioSource.Play();
            }
        }

        public void PlayHypothesisResponseGiven()
        {
            if (HypothesisResonseGivenClip != null)
            {
                AudioSource.clip = HypothesisResonseGivenClip;
                AudioSource.Play();
            }
        }

        public void PlayTrialFinished()
        {
            if (TrialFinishedClip != null)
            {
                AudioSource.clip = TrialFinishedClip;
                AudioSource.Play();
            }
        }
    }
}