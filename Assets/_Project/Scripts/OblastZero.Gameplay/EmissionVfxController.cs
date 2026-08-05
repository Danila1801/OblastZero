// Assets/_Project/Scripts/OblastZero.Gameplay/EmissionVfxController.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Drives the visual escalation of the Emission across the 60-second Blowout. Three bands, all read
    /// off the same thresholds the HUD colours and the siren use
    /// (<see cref="BalanceConstants.SCAVENGE_TIMER_WARNING_THRESHOLD"/> /
    /// <see cref="BalanceConstants.SCAVENGE_TIMER_CRITICAL_THRESHOLD"/>):
    ///
    /// <list type="number">
    ///   <item>Normal (60–15 s): nothing. The depot is just a depot.</item>
    ///   <item>Warning (15–5 s): the grade desaturates, the vignette closes in and pulses, occasional
    ///         single-frame flashes, and from 10 s a camera tremor.</item>
    ///   <item>Critical (5–0 s): the pulse quickens, the image shifts red and hot, the tremor grows, and
    ///         the flashes become frequent.</item>
    /// </list>
    ///
    /// <para><b>Interpolation is per-frame; the clock is per-second.</b>
    /// <see cref="EmissionTimer"/> raises <see cref="ScavengeTimerTickEvent"/> only when the whole-second
    /// readout changes, because that is all the HUD number needs. Driving a 4 Hz vignette pulse or a
    /// per-frame flash roll off that event would give one update per second — a visible staircase. So the
    /// event is used only to latch the remaining time, and this component runs its own clock down from
    /// that latch every frame. That also means the effects keep escalating smoothly between ticks and
    /// degrade to "frozen at the last known second" rather than "stopped" if the timer ever stalls.</para>
    ///
    /// <para><b>Volume overrides are runtime-only.</b> The escalation writes to a private
    /// <see cref="VolumeProfile"/> created in code on a local <see cref="Volume"/> with a higher priority
    /// than the scene's. It never touches ScavengeVolumeProfile.asset — that asset is generated
    /// (CLAUDE.md §14) and a runtime write would both dirty the file in the Editor and persist a
    /// half-finished panic state into the next run.</para>
    /// </summary>
    public class EmissionVfxController : MonoBehaviour
    {
        [Header("Vignette pulse")]
        [Tooltip("Vignette intensity added at the warning threshold, on top of the scene's own 0.36.")]
        [SerializeField] private float warningVignetteBoost = 0.14f;

        [Tooltip("Vignette intensity added at zero seconds.")]
        [SerializeField] private float criticalVignetteBoost = 0.34f;

        [Tooltip("Pulse rate in Hz at the warning threshold.")]
        [SerializeField] private float warningPulseHz = 1f;

        [Tooltip("Pulse rate in Hz at the critical threshold and below.")]
        [SerializeField] private float criticalPulseHz = 4f;

        [Header("Grade")]
        [Tooltip("Extra saturation removed at zero seconds. Negative — the colour drains out.")]
        [SerializeField] private float criticalSaturationDrop = -45f;

        [Tooltip("Post-exposure added at zero seconds. The image blows out as the wave arrives.")]
        [SerializeField] private float criticalPostExposure = 0.2f;

        [Tooltip("Colour filter the grade drives toward inside the critical window.")]
        [SerializeField] private Color criticalColorFilter = new Color(1f, 0.62f, 0.55f, 1f);

        [Header("Flash overlay")]
        [Tooltip("Alpha the fullscreen white overlay spikes to on a flash frame.")]
        [SerializeField, Range(0f, 1f)] private float flashPeakAlpha = 0.6f;

        [Tooltip("Seconds a flash takes to fade back to nothing.")]
        [SerializeField] private float flashFadeSeconds = 0.2f;

        [Header("Camera")]
        [Tooltip("Seconds the FOV punch takes when the emission lands.")]
        [SerializeField] private float fovPunchSeconds = 0.3f;

        private Volume _volume;
        private VolumeProfile _profile;
        private Vignette _vignette;
        private ColorAdjustments _grade;

        private Camera _camera;
        private ScreenShake _shake;
        private float _baseFov;

        private EmissionFlashOverlay _overlay;

        private float _remaining = Mathf.Infinity;
        private bool _hasTick;
        private bool _hit;
        private float _hitElapsed;

        private void OnEnable()
        {
            BuildVolume();
            BindCamera();
            _overlay = EmissionFlashOverlay.Create(transform, flashPeakAlpha, flashFadeSeconds);

            _remaining = BalanceConstants.SCAVENGE_TIMER_SECONDS;
            _hasTick = false;
            _hit = false;
            _hitElapsed = 0f;

            EventBus.Subscribe<ScavengeTimerTickEvent>(OnTick);
            EventBus.Subscribe<ScavengeTimerExpiredEvent>(OnExpired);

            ApplyNormal();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScavengeTimerTickEvent>(OnTick);
            EventBus.Unsubscribe<ScavengeTimerExpiredEvent>(OnExpired);

            RestoreCamera();

            if (_volume != null) Destroy(_volume.gameObject);
            // A profile built with CreateInstance is not owned by the asset database; without this it
            // leaks one VolumeProfile per entry into the phase.
            if (_profile != null) Destroy(_profile);
            _volume = null;
            _profile = null;
            _vignette = null;
            _grade = null;
        }

        private void OnTick(ScavengeTimerTickEvent e)
        {
            _remaining = e.SecondsRemaining;
            _hasTick = true;
        }

        private void OnExpired(ScavengeTimerExpiredEvent e)
        {
            _remaining = 0f;
            _hit = true;
            _hitElapsed = 0f;
            if (_overlay != null) _overlay.Flash();
            if (_shake != null) _shake.Kick(BalanceConstants.EMISSION_VFX_SHAKE_MAX_METRES * 2f);
        }

        private void Update()
        {
            // Run the local clock between ticks. Clamped at 0 so a late tick cannot resurrect the phase.
            if (_hasTick && !_hit) _remaining = Mathf.Max(0f, _remaining - Time.deltaTime);

            if (_hit)
            {
                _hitElapsed += Time.deltaTime;
                DriveFovPunch();
            }

            float warning = BalanceConstants.SCAVENGE_TIMER_WARNING_THRESHOLD;
            float critical = BalanceConstants.SCAVENGE_TIMER_CRITICAL_THRESHOLD;

            if (_remaining > warning && !_hit)
            {
                ApplyNormal();
                return;
            }

            // 0 at the warning threshold, 1 at zero seconds. Everything below scales off this.
            float escalation = warning <= 0f ? 1f : Mathf.Clamp01(1f - _remaining / warning);

            // 0 until the critical threshold, then 0→1 across the last few seconds.
            float panic = critical <= 0f ? 0f : Mathf.Clamp01(1f - _remaining / critical);

            DriveVignette(escalation, panic);
            DriveGrade(escalation, panic);
            DriveShake();
            DriveFlashRoll(escalation, panic);
        }

        // ─── Effect drivers ──────────────────────────────────────────────────────────────

        private void DriveVignette(float escalation, float panic)
        {
            if (_vignette == null) return;

            float hz = Mathf.Lerp(warningPulseHz, criticalPulseHz, panic);
            // Half-rectified sine: the vignette closes and relaxes, it never opens past the baseline.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * hz * Mathf.PI * 2f);
            float boost = Mathf.Lerp(warningVignetteBoost, criticalVignetteBoost, escalation);

            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = Mathf.Clamp01(SceneVignetteIntensity + boost * pulse);

            _vignette.smoothness.overrideState = true;
            // Sharper edge as it closes, so the desaturated rim reads as a hard boundary.
            _vignette.smoothness.value = Mathf.Lerp(SceneVignetteSmoothness, 0.28f, escalation);
        }

        /// <summary>
        /// The screen-edge desaturation the brief asked for, expressed through the grade rather than a
        /// custom radial-mask shader. URP has no per-pixel-radius saturation override, and adding a
        /// fullscreen pass for it would mean a new ScriptableRendererFeature and a shader — real work
        /// with a real cost on a project that has no shader pipeline yet. The vignette above already
        /// darkens and hardens the rim on the same curve, so pulling global saturation down against it
        /// lands in the same place perceptually: colour survives longest where the image is brightest,
        /// which is the centre.
        /// </summary>
        private void DriveGrade(float escalation, float panic)
        {
            if (_grade == null) return;

            _grade.saturation.overrideState = true;
            _grade.saturation.value = SceneSaturation + criticalSaturationDrop * escalation;

            _grade.postExposure.overrideState = true;
            _grade.postExposure.value = ScenePostExposure + criticalPostExposure * panic;

            _grade.colorFilter.overrideState = true;
            _grade.colorFilter.value = Color.Lerp(SceneColorFilter, criticalColorFilter, panic);
        }

        private void DriveShake()
        {
            if (_shake == null) return;

            float start = BalanceConstants.EMISSION_VFX_SHAKE_SECONDS;
            if (_remaining > start)
            {
                _shake.SetSustained(0f);
                return;
            }

            float k = start <= 0f ? 1f : Mathf.Clamp01(1f - _remaining / start);
            _shake.SetSustained(Mathf.Lerp(BalanceConstants.EMISSION_VFX_SHAKE_MIN_METRES,
                                           BalanceConstants.EMISSION_VFX_SHAKE_MAX_METRES, k));
        }

        private void DriveFlashRoll(float escalation, float panic)
        {
            if (_overlay == null || _hit) return;

            float chance = panic > 0f
                ? Mathf.Lerp(BalanceConstants.EMISSION_VFX_FLASH_CHANCE_AT_CRITICAL, 1f, panic)
                : Mathf.Lerp(BalanceConstants.EMISSION_VFX_FLASH_CHANCE_AT_WARNING,
                             BalanceConstants.EMISSION_VFX_FLASH_CHANCE_AT_CRITICAL, escalation);

            // Framerate-normalised to 60 Hz. A raw per-frame roll would flash twice as often at 120 fps
            // as at 60, which turns a tuned effect into a hardware-dependent one.
            float perFrame = 1f - Mathf.Pow(1f - Mathf.Clamp01(chance), Time.deltaTime * 60f);
            if (Random.value < perFrame) _overlay.Flash();
        }

        private void DriveFovPunch()
        {
            if (_camera == null || fovPunchSeconds <= 0f) return;
            float k = Mathf.Clamp01(_hitElapsed / fovPunchSeconds);
            _camera.fieldOfView = Mathf.Lerp(_baseFov, BalanceConstants.EMISSION_VFX_FOV_PUNCH_DEGREES, k);
        }

        private void ApplyNormal()
        {
            if (_vignette != null)
            {
                _vignette.intensity.overrideState = false;
                _vignette.smoothness.overrideState = false;
            }
            if (_grade != null)
            {
                _grade.saturation.overrideState = false;
                _grade.postExposure.overrideState = false;
                _grade.colorFilter.overrideState = false;
            }
            if (_shake != null) _shake.SetSustained(0f);
        }

        // ─── Setup ───────────────────────────────────────────────────────────────────────

        // The scene profile's authored values, mirrored so the escalation is expressed as a delta on top
        // of the shipped grade instead of replacing it. These are the numbers in
        // tools/scavenge_scene_lib.py's volume_profile_yaml(); a local Volume cannot read the global
        // one's values, only outrank them, so the baseline has to be restated to be added to.
        private const float SceneVignetteIntensity = 0.36f;
        private const float SceneVignetteSmoothness = 0.42f;
        private const float SceneSaturation = -32f;
        private const float ScenePostExposure = -0.35f;
        private static readonly Color SceneColorFilter = new Color(0.85f, 0.9f, 0.82f, 1f);

        private void BuildVolume()
        {
            if (_volume != null) return;

            var go = new GameObject("Emission_VFX_Volume");
            go.transform.SetParent(transform, false);

            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "EmissionVfxRuntimeProfile";

            _vignette = _profile.Add<Vignette>(false);
            _grade = _profile.Add<ColorAdjustments>(false);

            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;      // outranks the scene volume, which ships at the default 0
            _volume.profile = _profile;
        }

        private void BindCamera()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogWarning("[EmissionVfx] No main camera — shake and FOV punch are disabled.");
                return;
            }

            _baseFov = _camera.fieldOfView;

            // The shake lives on the camera, not here: it writes localPosition, and only the camera's
            // own localPosition is free of the look code's per-frame assignment.
            _shake = _camera.GetComponent<ScreenShake>();
            if (_shake == null) _shake = _camera.gameObject.AddComponent<ScreenShake>();
        }

        private void RestoreCamera()
        {
            if (_shake != null) _shake.SetSustained(0f);
            if (_camera != null) _camera.fieldOfView = _baseFov;
            _camera = null;
            _shake = null;
        }
    }
}
