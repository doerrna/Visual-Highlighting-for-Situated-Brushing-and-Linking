using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace BrushingAndLinking
{
    public abstract class Button : MonoBehaviour
    {
        private RayInteractable rayInteractable;
        private Collider collider;
        private ColliderSurface colliderSurface;
        protected virtual void Awake()
        {
            rayInteractable = GetComponent<RayInteractable>();
            collider = GetComponentInChildren<Collider>();
            colliderSurface = GetComponentInChildren<ColliderSurface>();

            colliderSurface.InjectCollider(collider);
            rayInteractable.InjectSurface(colliderSurface);

            // This event is called whenever the user targets this object and clicks
            rayInteractable.WhenSelectingInteractorViewAdded += OnButtonSelected;
        }

        protected void OnButtonSelected(IInteractorView view)
        {
            Select();
        }

        public abstract void Select();
    }
}