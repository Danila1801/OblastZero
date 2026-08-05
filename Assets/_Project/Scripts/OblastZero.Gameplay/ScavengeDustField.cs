// Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeDustField.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Airborne dust for the Collapsed Grain Depot: the thing that makes a fluorescent tube read as a
    /// light source rather than a bright quad. Builds its own <see cref="ParticleSystem"/> at runtime
    /// from the fields below and needs nothing authored in the scene except this component and a
    /// material reference.
    ///
    /// <para><b>Why runtime and not scene YAML.</b> Scavenge.unity is generated, never hand-saved
    /// (CLAUDE.md §14), and a ParticleSystem serializes as roughly four hundred lines across a dozen
    /// module blocks whose field names change between Unity versions. A single wrong key in that
    /// payload yields a system that loads without error and emits nothing — the same silent-failure
    /// class as a stale script GUID. Configuring the modules through the typed C# API instead means
    /// the compiler checks every field, the generator emits one component, and the scene stays
    /// byte-deterministic.</para>
    ///
    /// <para><b>Why it follows the camera.</b> The depot floor is 104 x 72 m. Three hundred motes
    /// spread over that volume is roughly one mote per twenty-five cubic metres — invisible. The
    /// emitter box therefore tracks the view position while the particles simulate in
    /// <see cref="ParticleSystemSimulationSpace.World"/>, so motes are always resident where the
    /// player is looking but hang in the air as the player runs past them instead of travelling
    /// along. This is the standard ambient-particle rig; a fixed emitter at the scene origin would
    /// only ever dress the warehouse.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ScavengeDustField : MonoBehaviour
    {
        [Header("Appearance")]
        [Tooltip("Unlit transparent particle material. Generated as M_Dust by tools/generate_scavenge_scene.py.")]
        [SerializeField] private Material dustMaterial;

        [Tooltip("Mote tint. Pale grey with low alpha — dust catches light, it does not emit it.")]
        [SerializeField] private Color tint = new Color(0.78f, 0.78f, 0.74f, 0.30f);

        [SerializeField] private float sizeMin = 0.02f;
        [SerializeField] private float sizeMax = 0.05f;

        [Header("Motion")]
        [Tooltip("Seconds a mote survives. Long, because dust does not hurry.")]
        [SerializeField] private float lifetimeMin = 10f;
        [SerializeField] private float lifetimeMax = 15f;

        [Tooltip("Downward drift, m/s. Negative Y — motes settle, they do not fall.")]
        [SerializeField] private float driftMin = 0.10f;
        [SerializeField] private float driftMax = 0.30f;

        [Tooltip("Lateral air movement, m/s, applied symmetrically on X and Z.")]
        [SerializeField] private float lateralDrift = 0.06f;

        [Header("Volume")]
        [Tooltip("Emitter box dimensions in metres. Centred on the follow target.")]
        [SerializeField] private Vector3 boxSize = new Vector3(30f, 8f, 30f);

        [SerializeField] private float emissionRatePerSecond = 15f;
        [SerializeField] private int maxParticles = 300;

        [Header("Follow")]
        [Tooltip("Transform the emitter box centres on. Auto-binds to the main camera when empty.")]
        [SerializeField] private Transform followTarget;

        [Tooltip("Metres the target must move before the emitter box re-centres. Stops the box " +
                 "jittering with mouse look while keeping motes resident around the player.")]
        [SerializeField] private float followStepMetres = 4f;

        private ParticleSystem _system;
        private Vector3 _anchor;
        private bool _anchored;

        private void Awake()
        {
            _system = GetComponent<ParticleSystem>();
            if (_system == null) _system = gameObject.AddComponent<ParticleSystem>();
            Configure();
        }

        private void OnEnable()
        {
            // Re-anchor on enable so a scene reload does not start the field wherever it was left.
            _anchored = false;
            ResolveFollowTarget();
            SnapToTarget();
        }

        private void LateUpdate()
        {
            // LateUpdate, not Update: the camera has already been moved by ScavengePlayerController
            // this frame, so the emitter follows the position the player actually sees from.
            if (followTarget == null)
            {
                ResolveFollowTarget();
                if (followTarget == null) return;
            }

            Vector3 target = followTarget.position;
            if (!_anchored || (target - _anchor).sqrMagnitude >= followStepMetres * followStepMetres)
                SnapTo(target);
        }

        private void ResolveFollowTarget()
        {
            if (followTarget != null) return;
            var cam = Camera.main;
            if (cam != null) followTarget = cam.transform;
        }

        private void SnapToTarget()
        {
            if (followTarget != null) SnapTo(followTarget.position);
        }

        private void SnapTo(Vector3 position)
        {
            _anchor = position;
            _anchored = true;
            transform.position = position;
        }

        /// <summary>
        /// Writes every module this field needs. Modules Unity enables by default and this rig does
        /// not want (collision, trails, sub-emitters) are left alone: they default to disabled, and
        /// touching them only widens the surface where a Unity version bump can change behaviour.
        /// </summary>
        private void Configure()
        {
            var main = _system.main;
            main.loop = true;
            main.prewarm = true;              // the room is already dusty on frame one, not in 15 seconds
            main.playOnAwake = true;
            main.duration = Mathf.Max(lifetimeMax, 1f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = 0f;             // drift comes from velocityOverLifetime, not the shape normal
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = tint;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startRotation3D = false;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = Mathf.Max(0f, emissionRatePerSecond);

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;
            shape.rotation = Vector3.zero;
            shape.position = Vector3.zero;
            shape.randomDirectionAmount = 0f;

            // Downward settle plus a little lateral air. World space so the drift direction is
            // absolute — the emitter's own rotation must not steer the dust.
            var velocity = _system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-lateralDrift, lateralDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(-Mathf.Abs(driftMax), -Mathf.Abs(driftMin));
            velocity.z = new ParticleSystem.MinMaxCurve(-lateralDrift, lateralDrift);

            // Fade in and out at the ends of the life so motes never pop into or out of existence.
            var colorOverLifetime = _system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(BuildFadeGradient());

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortMode = ParticleSystemSortMode.Distance;
                if (dustMaterial != null) renderer.sharedMaterial = dustMaterial;
                else Debug.LogWarning("[ScavengeDustField] No dust material assigned — motes will " +
                                      "render with Unity's default particle material.");
            }

            Debug.Log($"[ScavengeDustField] Dust field up: {maxParticles} motes max, " +
                      $"{emissionRatePerSecond:0.#}/s over a {boxSize.x:0}x{boxSize.y:0}x{boxSize.z:0} m box.");
        }

        /// <summary>Alpha ramp: 0 → full by 15% of life, holding until 75%, back to 0 at the end.</summary>
        private static Gradient BuildFadeGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.75f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }
    }
}
