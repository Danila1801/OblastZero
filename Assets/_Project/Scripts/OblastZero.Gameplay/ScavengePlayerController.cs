// Assets/_Project/Scripts/Gameplay/ScavengePlayerController.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// First-person controller for the 3D Blowout. CharacterController-based movement with mouse look, a
    /// continuous center-screen look-raycast (drives the HUD prompt via <see cref="ScavengeTargetChangedEvent"/>),
    /// and an interact key that grabs whatever the crosshair is currently on. Driven by the new Input System
    /// (device polling — no action asset required).
    ///
    /// Requires Active Input Handling = "Input System Package (New)" or "Both" (Project Settings → Player).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class ScavengePlayerController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 4.5f;
        [SerializeField] private float sprintSpeed = 7.0f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Look")]
        [Tooltip("The camera transform (a child of this object). Auto-bound to Camera.main if left empty.")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField] private float pitchClampDegrees = 85f;

        [Header("Interaction")]
        [SerializeField] private float interactRange = 3f;
        [SerializeField] private LayerMask interactMask = ~0;

        /// <summary>Raised when the player presses interact while looking at a pickup in range.</summary>
        public event Action<ScavengePickup> PickupRequested;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;
        private ScavengePickup _lookTarget;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;
        }

        private void OnEnable() => SetCursorLocked(true);

        private void OnDisable()
        {
            SetCursorLocked(false);
            // Clear any lingering HUD prompt as we leave the phase.
            if (_lookTarget != null)
            {
                _lookTarget = null;
                EventBus.Raise(new ScavengeTargetChangedEvent { HasTarget = false, Verb = string.Empty });
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return; // no keyboard bound (e.g., old input backend) — fail safe

            HandleLook(Mouse.current);
            HandleMovement(keyboard);
            UpdateLookTarget();

            if (keyboard.eKey.wasPressedThisFrame) TryInteract();
            if (keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);
        }

        private void HandleLook(Mouse mouse)
        {
            if (mouse == null || cameraPivot == null) return;

            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(Vector3.up, delta.x);

            _pitch = Mathf.Clamp(_pitch - delta.y, -pitchClampDegrees, pitchClampDegrees);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement(Keyboard keyboard)
        {
            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);

            Vector3 horizontal = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);
            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : walkSpeed;

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontal * speed + Vector3.up * _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);
        }

        /// <summary>Per-frame raycast from the crosshair; raises an event when the target changes so the HUD updates.</summary>
        private void UpdateLookTarget()
        {
            ScavengePickup target = RaycastPickup();
            if (ReferenceEquals(target, _lookTarget)) return;

            _lookTarget = target;
            EventBus.Raise(new ScavengeTargetChangedEvent
            {
                HasTarget = target != null,
                Verb = target != null ? target.InteractionVerb : string.Empty
            });
        }

        private void TryInteract()
        {
            if (_lookTarget != null) PickupRequested?.Invoke(_lookTarget);
        }

        private ScavengePickup RaycastPickup()
        {
            if (cameraPivot == null) return null;

            var ray = new Ray(cameraPivot.position, cameraPivot.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask, QueryTriggerInteraction.Collide))
                return null;

            return hit.collider.GetComponentInParent<ScavengePickup>();
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
