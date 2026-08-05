// Assets/_Project/Scripts/OblastZero.Gameplay/FootstepAudio.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Footsteps for the Blowout. Attached at runtime by <see cref="ScavengePlayerController"/>, reads
    /// the <see cref="CharacterController"/> directly, and needs no wiring in the scene.
    ///
    /// <para><b>Cadence is distance-based, not timed.</b> The brief specified two fixed intervals — 300 ms
    /// walking, 200 ms sprinting. Those are the same thing expressed less generally: at the shipped
    /// speeds (4.5 m/s walk, 7 m/s sprint) they work out to a 1.35 m and a 1.40 m stride. Stepping every
    /// <see cref="strideMetres"/> of ground actually covered reproduces both cadences and stays correct
    /// for every speed in between — including the ramp while the player is still accelerating, and the
    /// case where a wall is holding them at half speed while the input says sprint. A timer would keep
    /// stepping at full cadence there, which is the classic walking-into-a-wall footstep bug.</para>
    ///
    /// <para><b>Surface.</b> There are no PhysicMaterials in the generated scene (every collider writes
    /// <c>m_Material: {fileID: 0}</c>), so the surface is read off the renderer's shared material name,
    /// which the generator does control — M_Steel_* and the dock plate read as metal, everything else as
    /// concrete. <c>sharedMaterial</c> and not <c>material</c>: the latter would instantiate a copy of
    /// the depot's floor material on every step.</para>
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootstepAudio : MonoBehaviour
    {
        /// <summary>Material-name prefix that means "this surface rings". Everything else is concrete.</summary>
        private const string MetalMaterialPrefix = "M_Steel";

        [Tooltip("Metres of ground covered per step. 1.37 m reproduces the design cadence at both " +
                 "the walk and the sprint speed.")]
        [SerializeField] private float strideMetres = 1.37f;

        [Tooltip("How far below the capsule's base to look for a surface.")]
        [SerializeField] private float probeDepth = 1.2f;

        [Tooltip("Random pitch spread per step, so a sprint does not sound like a metronome.")]
        [SerializeField, Range(0f, 0.5f)] private float pitchJitter = 0.12f;

        [Tooltip("Volume at walking pace. Sprinting scales up toward 1.")]
        [SerializeField, Range(0f, 1f)] private float walkVolume = 0.55f;

        private CharacterController _controller;
        private float _distanceSinceStep;
        private string _cachedSurfaceCue = AudioManager.CUE_FOOTSTEP_CONCRETE;
        private int _surfaceProbeCountdown;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            // Half a stride of credit, so the very first step lands a beat after the player starts
            // moving rather than on the frame the phase begins.
            _distanceSinceStep = strideMetres * 0.5f;
        }

        private void Update()
        {
            if (_controller == null || !_controller.isGrounded) return;

            // Horizontal only: falling must not accumulate stride, and neither must the constant
            // downward velocity a CharacterController carries while grounded.
            Vector3 velocity = _controller.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            if (speed < 0.2f) return;

            _distanceSinceStep += speed * Time.deltaTime;
            if (_distanceSinceStep < strideMetres) return;

            _distanceSinceStep -= strideMetres;
            Step(speed);
        }

        private void Step(float speed)
        {
            // Re-probe the surface every fourth step. The depot's zones are tens of metres across, so
            // per-step raycasts would spend a whole run confirming the same answer.
            if (_surfaceProbeCountdown <= 0)
            {
                _cachedSurfaceCue = ProbeSurfaceCue();
                _surfaceProbeCountdown = 4;
            }
            _surfaceProbeCountdown--;

            // Sprinting is heavier as well as faster.
            float t = Mathf.InverseLerp(2f, 7f, speed);
            float volume = Mathf.Lerp(walkVolume, 1f, t);

            // UnityEngine.Random on purpose, not RunRng: this is presentation, and drawing from the
            // run's seeded stream would make a save's outcome depend on how many steps the player took.
            float pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

            AudioManager.Play3D(_cachedSurfaceCue, FootPosition(), volume, pitch);
        }

        private Vector3 FootPosition()
        {
            return transform.position + _controller.center - Vector3.up * (_controller.height * 0.5f);
        }

        private string ProbeSurfaceCue()
        {
            Vector3 origin = transform.position + _controller.center;
            var ray = new Ray(origin, Vector3.down);

            if (!Physics.Raycast(ray, out RaycastHit hit, _controller.height * 0.5f + probeDepth,
                                 ~0, QueryTriggerInteraction.Ignore))
                return AudioManager.CUE_FOOTSTEP_CONCRETE;

            var renderer = hit.collider.GetComponent<Renderer>();
            var material = renderer != null ? renderer.sharedMaterial : null;
            if (material != null && material.name.StartsWith(MetalMaterialPrefix,
                                                             System.StringComparison.Ordinal))
                return AudioManager.CUE_FOOTSTEP_METAL;

            return AudioManager.CUE_FOOTSTEP_CONCRETE;
        }
    }
}
