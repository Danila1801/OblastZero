// Assets/_Project/Scripts/Gameplay/FluorescentFlicker.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Drives a failing overhead fluorescent fixture: the tube holds a nominal output, dips and stutters on an
    /// irregular schedule, and occasionally drops out entirely before striking again. Purely presentational —
    /// it owns nothing but its own <see cref="Light"/> and an optional emissive fixture renderer, and raises no
    /// events, so it is safe to drop onto every fixture in the scavenge scene.
    ///
    /// The waveform is deterministic per-fixture: <see cref="Random.InitState"/> is never touched, and the
    /// noise is sampled from Perlin noise seeded off the instance's own position, so two adjacent fixtures
    /// stutter out of phase with each other without any authored offsets.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FluorescentFlicker : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("Intensity the fixture settles at when the tube is holding. Captured from the Light on Awake if left at 0.")]
        [SerializeField] private float nominalIntensity = 0f;

        [Tooltip("Fraction of nominal the tube sags to on a normal stutter. 0 = full blackout, 1 = no sag.")]
        [SerializeField, Range(0f, 1f)] private float sagFloor = 0.45f;

        [Header("Waveform")]
        [Tooltip("Speed the noise field is sampled at. Higher = busier, more electrical buzz.")]
        [SerializeField] private float noiseSpeed = 7.5f;

        [Tooltip("How much of the output the noise field is allowed to move. 0 = steady, 1 = violent.")]
        [SerializeField, Range(0f, 1f)] private float noiseDepth = 0.35f;

        [Header("Dropouts")]
        [Tooltip("Average seconds between full dropouts, where the tube loses the arc entirely.")]
        [SerializeField] private float secondsBetweenDropouts = 6.5f;

        [Tooltip("How long a full dropout lasts before the tube strikes again.")]
        [SerializeField] private float dropoutDuration = 0.22f;

        [Tooltip("Optional fixture renderer whose emissive colour tracks the tube. Leave empty for light-only fixtures.")]
        [SerializeField] private Renderer fixtureRenderer;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Light _light;
        private Color _nominalEmission;
        private float _noiseOrigin;
        private float _dropoutEndsAt;
        private float _nextDropoutAt;
        private bool _hasEmission;

        // Per-renderer override. Writing to sharedMaterial would mutate the material asset every
        // fixture shares — every tube in the level would then stutter in lockstep, and the .mat file
        // would come back dirty in the Editor. A property block keeps the change local to this renderer
        // without instantiating a material per fixture.
        private MaterialPropertyBlock _block;

        private void Awake()
        {
            _light = GetComponent<Light>();

            if (nominalIntensity <= 0f) nominalIntensity = _light.intensity;

            // Per-fixture phase offset derived from world position, so no two tubes stutter together.
            Vector3 p = transform.position;
            _noiseOrigin = Mathf.Abs(p.x * 3.17f + p.y * 7.31f + p.z * 11.73f);

            if (fixtureRenderer != null && fixtureRenderer.sharedMaterial != null &&
                fixtureRenderer.sharedMaterial.HasProperty(EmissionColorId))
            {
                _nominalEmission = fixtureRenderer.sharedMaterial.GetColor(EmissionColorId);
                _block = new MaterialPropertyBlock();
                _hasEmission = true;
            }

            ScheduleNextDropout();
        }

        private void OnDisable()
        {
            // Leave the fixture at nominal so a disabled flicker never strands a dark light.
            if (_light != null) _light.intensity = nominalIntensity;
            ApplyEmission(1f);
        }

        private void Update()
        {
            float t = Time.time;

            if (t >= _nextDropoutAt)
            {
                _dropoutEndsAt = t + dropoutDuration;
                ScheduleNextDropout();
            }

            float level = t < _dropoutEndsAt ? 0f : SampleHoldLevel(t);

            _light.intensity = nominalIntensity * level;
            ApplyEmission(level);
        }

        /// <summary>Nominal output modulated by the fixture's own noise band, floored at <see cref="sagFloor"/>.</summary>
        private float SampleHoldLevel(float t)
        {
            float noise = Mathf.PerlinNoise(_noiseOrigin, t * noiseSpeed);      // 0..1, smooth
            float modulated = 1f - noiseDepth * (1f - noise);
            return Mathf.Max(sagFloor, modulated);
        }

        private void ApplyEmission(float level)
        {
            if (!_hasEmission) return;
            _block.SetColor(EmissionColorId, _nominalEmission * level);
            fixtureRenderer.SetPropertyBlock(_block);
        }

        private void ScheduleNextDropout()
        {
            // Exponential-ish spacing so dropouts feel unscheduled rather than metronomic.
            float jitter = Random.Range(0.35f, 1.75f);
            _nextDropoutAt = Time.time + Mathf.Max(dropoutDuration * 2f, secondsBetweenDropouts * jitter);
        }
    }
}
