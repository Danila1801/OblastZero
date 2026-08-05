// Assets/_Project/Scripts/Gameplay/ScavengePlayerController.cs
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using OblastZero.Core;
using OblastZero.Services;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// First-person controller for the 3D Blowout. CharacterController-based movement with mouse look, a
    /// continuous center-screen look-raycast (drives the HUD prompt via <see cref="ScavengeTargetChangedEvent"/>),
    /// and an interact key that grabs whatever the crosshair is currently on. Driven by the new Input System
    /// (device polling — no action asset required).
    ///
    /// Also owns the two things that hang off that same raycast: the hover highlight on whatever the
    /// crosshair is currently on (<see cref="PickupHoverHighlight"/>), and the ground ring showing how
    /// far the player's reach extends. Both are presentation, but both are driven by state only this
    /// class knows, so they live here rather than re-running the raycast somewhere else.
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

        [Tooltip("Gamepad right-stick look speed, in degrees per second at full deflection. Applied " +
                 "with Time.deltaTime; mouse delta is not, because it is already a per-frame quantity.")]
        [SerializeField] private float stickLookDegreesPerSecond = 220f;
        [SerializeField] private float pitchClampDegrees = 85f;

        [Header("Interaction")]
        [Tooltip("Crosshair reach in metres. Mirrors BalanceConstants.SCAVENGE_INTERACTION_RANGE.")]
        [SerializeField] private float interactRange = BalanceConstants.SCAVENGE_INTERACTION_RANGE;
        [SerializeField] private LayerMask interactMask = ~0;

        [Header("Range Ring")]
        [Tooltip("Draw a ground ring at the interaction radius under the player.")]
        [SerializeField] private bool showInteractionRing = true;

        [Tooltip("Transparent unlit material for the ring. Generated as M_RangeRing by " +
                 "tools/generate_scavenge_scene.py; the ring is skipped when this is empty.")]
        [SerializeField] private Material interactionRingMaterial;

        /// <summary>Raised when the player presses interact while looking at a pickup in range.</summary>
        public event Action<ScavengePickup> PickupRequested;

        /// <summary>
        /// Raised when interact is pressed with nothing grabbable under the crosshair. Subscribed by world
        /// interactions that are not pickups — today the Interview anomaly's chair.
        ///
        /// <para>Firing only on the empty-crosshair case rather than on every press is what keeps a player
        /// reaching for a document on the desk from being seated by the same keypress. A pickup always
        /// wins; a listener can never steal a grab.</para>
        /// </summary>
        public event Action InteractPressedWithNoTarget;

        /// <summary>Crosshair reach in metres, as the raycast actually uses it.</summary>
        public float InteractRange => interactRange;

        /// <summary>True while the crosshair is on a grabbable pickup in range.</summary>
        public bool HasLookTarget => _lookTarget != null;

        /// <summary>
        /// Scales walk and sprint speed. 1 is normal; the Backlog anomaly (ANM-Χ-21/BL) drops it to 0.02.
        ///
        /// <para>A multiplier rather than public base speeds. Exposing <c>walkSpeed</c>/<c>sprintSpeed</c>
        /// for a hazard to overwrite means every hazard has to save and restore two values, and any pair of
        /// them that overlaps restores the other's saved copy — the player walks out of one anomaly at
        /// another's speed, permanently, with nothing logged. One multiplier has one owner at a time and
        /// resets to a known constant.</para>
        /// </summary>
        public float SpeedMultiplier
        {
            get { return _speedMultiplier; }
            set { _speedMultiplier = Mathf.Clamp(value, 0.001f, 4f); }
        }

        /// <summary>
        /// Minimum seconds between grabs. 0 outside a Backlog. The bible slows interaction as well as
        /// movement; without this a player could stand at the boundary with their feet slowed and strip a
        /// shelf at full speed.
        /// </summary>
        public float InteractionDelaySeconds { get; set; }

        /// <summary>
        /// The player's horizontal speed this frame, m/s. Read by the Drowned Census-Taker (MTN-Β-04/DC),
        /// whose entire threat is a stop-timer: measured from the CharacterController's realised motion
        /// rather than from input, so a player held against a wall counts as stopped — which is exactly
        /// what the mutant should notice, and what reading the input axis would miss.
        /// </summary>
        public float HorizontalSpeed { get; private set; }

        /// <summary>
        /// Raised when the player presses the pause action. The owning phase state decides what a
        /// pause means — this controller does not open menus and never touches Time.timeScale.
        /// </summary>
        public event Action PauseRequested;

        /// <summary>
        /// While true the controller ignores movement, look and interact. The phase state sets it for
        /// the duration of the pause overlay. It is a flag rather than <c>enabled = false</c> because
        /// disabling the component would run OnDisable, which unlocks the cursor and clears the look
        /// target — both of which must survive a pause so resuming puts the player back where they were.
        /// </summary>
        public bool InputSuspended { get; set; }

        private CharacterController _controller;
        private PreferencesService _preferences;
        private float _pitch;
        private float _verticalVelocity;
        private ScavengePickup _lookTarget;
        private PickupHoverHighlight _hovered;
        private float _speedMultiplier = 1f;
        private float _nextInteractAllowedAt;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraPivot == null && Camera.main != null) cameraPivot = Camera.main.transform;

            // Bindings are resolved through the service on every read rather than cached as keys, so a
            // rebind made in the pause overlay takes effect the moment the overlay closes.
            if (!ServiceLocator.TryGet<PreferencesService>(out _preferences) || _preferences == null)
            {
                Debug.LogWarning("[ScavengePlayerController] No PreferencesService registered — using " +
                                 "standard-issue key bindings.");
            }

            // Footsteps are an implementation detail of moving, not something to wire in the scene: the
            // component reads this object's CharacterController and has no designer-facing state the
            // controller does not already own.
            if (GetComponent<FootstepAudio>() == null) gameObject.AddComponent<FootstepAudio>();

            if (showInteractionRing) BuildInteractionRing();
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
            // And drop the highlight, or the pickup keeps its emissive boost into the next phase.
            ClearHover();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            // Pause is read even while suspended: it is how a player closes the overlay with the same key
            // or button that opened it.
            if (PausePressed(keyboard, gamepad)) PauseRequested?.Invoke();
            if (InputSuspended) return;

            // A pad-only player has neither keyboard nor mouse. Neither device may be assumed present.
            if (keyboard == null && gamepad == null) return;

            HandleLook(Mouse.current, gamepad);
            HandleMovement(keyboard, gamepad);
            UpdateLookTarget();

            if (InteractPressed(keyboard, gamepad)) TryInteract();
        }

        // ── Input reading ────────────────────────────────────────────────────
        // Devices are polled directly rather than through an .inputactions asset. That is the pattern this
        // controller already used, and it is what lets the rebinding screen work against a plain Key enum
        // instead of runtime rebinding overrides on an action map — much the simpler of the two for seven
        // fixed actions.

        /// <summary>The key currently bound to an action, or its shipped default without preferences.</summary>
        private Key BoundKey(OblastAction action)
            => _preferences != null
                ? _preferences.Current.GetBinding(action)
                : InputBindingTable.DefaultFor(action);

        /// <summary>True while the key bound to <paramref name="action"/> is held.</summary>
        private bool Held(Keyboard keyboard, OblastAction action)
        {
            if (keyboard == null) return false;
            Key key = BoundKey(action);
            return key != Key.None && keyboard[key].isPressed;
        }

        /// <summary>True on the frame the key bound to <paramref name="action"/> goes down.</summary>
        private bool Pressed(Keyboard keyboard, OblastAction action)
        {
            if (keyboard == null) return false;
            Key key = BoundKey(action);
            return key != Key.None && keyboard[key].wasPressedThisFrame;
        }

        private bool InteractPressed(Keyboard keyboard, Gamepad gamepad)
            => Pressed(keyboard, OblastAction.Interact) ||
               (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

        private bool PausePressed(Keyboard keyboard, Gamepad gamepad)
            => Pressed(keyboard, OblastAction.Pause) ||
               (gamepad != null && gamepad.startButton.wasPressedThisFrame);

        private void HandleLook(Mouse mouse, Gamepad gamepad)
        {
            if (cameraPivot == null) return;

            Vector2 delta = Vector2.zero;

            // Mouse delta is already a per-frame pixel count, so it must NOT be scaled by deltaTime; stick
            // input is a position in [-1,1] and must be, or look speed becomes frame-rate dependent. Getting
            // this backwards is the classic pad-look bug: fine at 60 fps, spinning at 144.
            if (mouse != null) delta += mouse.delta.ReadValue() * lookSensitivity;
            if (gamepad != null)
                delta += gamepad.rightStick.ReadValue() * (stickLookDegreesPerSecond * Time.deltaTime);

            if (delta == Vector2.zero) return;

            transform.Rotate(Vector3.up, delta.x);
            _pitch = Mathf.Clamp(_pitch - delta.y, -pitchClampDegrees, pitchClampDegrees);
            cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        private void HandleMovement(Keyboard keyboard, Gamepad gamepad)
        {
            float x = (Held(keyboard, OblastAction.MoveRight) ? 1f : 0f) -
                      (Held(keyboard, OblastAction.MoveLeft) ? 1f : 0f);
            float z = (Held(keyboard, OblastAction.MoveForward) ? 1f : 0f) -
                      (Held(keyboard, OblastAction.MoveBackward) ? 1f : 0f);

            bool sprinting = Held(keyboard, OblastAction.Sprint);

            if (gamepad != null)
            {
                // The stick ADDS to the keyboard rather than replacing it, so a player using both is not
                // fighting their own input. ClampMagnitude below keeps the sum at or under full speed.
                Vector2 stick = gamepad.leftStick.ReadValue();
                x += stick.x;
                z += stick.y;
                if (gamepad.buttonEast.isPressed) sprinting = true;
            }

            Vector3 horizontal = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f);

            // The multiplier scales horizontal speed only. Gravity is not subjective: a Backlog that also
            // slowed the fall would leave the player hanging in mid-air on the way down a stairwell, which
            // reads as a physics bug rather than as time distortion.
            float speed = (sprinting ? sprintSpeed : walkSpeed) * _speedMultiplier;

            if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = horizontal * speed + Vector3.up * _verticalVelocity;

            Vector3 before = transform.position;
            _controller.Move(motion * Time.deltaTime);

            // Realised speed, not intended speed. A player pushing into a wall has full input and zero
            // motion, and the Census-Taker must read that as standing still.
            Vector3 delta = transform.position - before;
            delta.y = 0f;
            HorizontalSpeed = Time.deltaTime > 0f ? delta.magnitude / Time.deltaTime : 0f;
        }

        /// <summary>
        /// Per-frame raycast from the crosshair. Raises an event when the target changes so the HUD
        /// updates, and moves the hover highlight with it. The early return on an unchanged target is
        /// what keeps this to one highlight transition per change instead of one per frame.
        /// </summary>
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

            SetHover(target != null ? target.GetComponent<PickupHoverHighlight>() : null);
        }

        private void SetHover(PickupHoverHighlight next)
        {
            if (ReferenceEquals(next, _hovered)) return;
            if (_hovered != null) _hovered.OnHoverEnd();
            _hovered = next;
            if (_hovered != null) _hovered.OnHoverStart();
        }

        private void ClearHover()
        {
            if (_hovered == null) return;
            _hovered.OnHoverEnd();
            _hovered = null;
        }

        /// <summary>
        /// A flat ring on the ground at the interaction radius. The mesh is generated rather than
        /// authored so the radius always matches <see cref="interactRange"/> — a prefab quad scaled to
        /// fit would silently lie the moment the reach is retuned.
        ///
        /// It hangs off the player root and is deliberately not parented to the camera, so it does not
        /// pitch with mouse look. Y sits just above the floor plane to stay out of z-fighting.
        /// </summary>
        private void BuildInteractionRing()
        {
            if (interactionRingMaterial == null)
            {
                Debug.LogWarning("[ScavengePlayerController] No interaction ring material assigned — " +
                                 "skipping the range ring.");
                return;
            }

            var go = new GameObject("Interaction_Range_Ring");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.transform.localRotation = Quaternion.identity;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = interactionRingMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.AddComponent<MeshFilter>().sharedMesh =
                BuildRingMesh(interactRange - 0.06f, interactRange, 64);
        }

        /// <summary>An annulus in the XZ plane, wound so it faces +Y.</summary>
        private static Mesh BuildRingMesh(float innerRadius, float outerRadius, int segments)
        {
            segments = Mathf.Max(8, segments);
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = ((i + 1) % segments) * 2;
                int d = ((i + 1) % segments) * 2 + 1;

                triangles[i * 6] = a;
                triangles[i * 6 + 1] = c;
                triangles[i * 6 + 2] = b;
                triangles[i * 6 + 3] = b;
                triangles[i * 6 + 4] = c;
                triangles[i * 6 + 5] = d;
            }

            var mesh = new Mesh { name = "InteractionRangeRing" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private void TryInteract()
        {
            // Inside a Backlog every interaction takes seconds, not a keypress. The gate is here rather
            // than in the anomaly so it covers both branches below with one rule — a player who cannot
            // grab a crate also cannot sit down for an interview at normal speed.
            if (Time.time < _nextInteractAllowedAt) return;
            if (InteractionDelaySeconds > 0f) _nextInteractAllowedAt = Time.time + InteractionDelaySeconds;

            if (_lookTarget != null)
            {
                PickupRequested?.Invoke(_lookTarget);
                return;
            }

            // No pickup under the crosshair — offer the press to whatever else is listening. Ordering is
            // deliberate: a pickup always wins, so a listener can never steal a grab.
            InteractPressedWithNoTarget?.Invoke();
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
