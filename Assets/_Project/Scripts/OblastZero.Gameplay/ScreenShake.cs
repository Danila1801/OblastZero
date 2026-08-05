// Assets/_Project/Scripts/OblastZero.Gameplay/ScreenShake.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Reusable camera shake. Attach to the camera transform; call <see cref="SetSustained"/> for a
    /// continuous tremor and <see cref="Kick"/> for a one-off jolt.
    ///
    /// <para><b>Why it writes localPosition and never localRotation.</b>
    /// <see cref="ScavengePlayerController"/> owns look: it yaws the player root with
    /// <c>transform.Rotate</c> and writes the camera's <c>localRotation</c> outright every frame from
    /// the accumulated pitch. Anything that shakes rotation is therefore either overwritten on the next
    /// frame (no shake) or fights the assignment (the view snaps). Position is untouched by look, so
    /// offsetting it is the one channel that composes cleanly — and translation-only shake also cannot
    /// throw the crosshair off the thing the player is aiming at, which matters in a 60-second grab
    /// where the shake peaks exactly when accuracy matters most.</para>
    ///
    /// <para>The base position is captured on enable and restored on disable, so a torn-down phase never
    /// leaves the camera 8 cm off its rig.</para>
    /// </summary>
    public class ScreenShake : MonoBehaviour
    {
        [Tooltip("Shake frequency in Hz. Low enough to read as structural, not as a broken camera.")]
        [SerializeField] private float frequency = 18f;

        [Tooltip("Seconds a Kick() takes to decay to nothing.")]
        [SerializeField] private float kickDecaySeconds = 0.22f;

        private Vector3 _basePosition;
        private bool _captured;

        private float _sustainedAmplitude;
        private float _kickAmplitude;
        private float _kickRemaining;

        // Independent phase offsets per axis, so the motion is not a diagonal line.
        private float _seedX;
        private float _seedY;
        private float _seedZ;

        private void OnEnable()
        {
            _basePosition = transform.localPosition;
            _captured = true;

            // Fixed offsets, not Random: the shake has to be identical between two runs of the same
            // build for a recorded comparison to mean anything, and three irrational-ish constants
            // decorrelate the axes just as well as noise would.
            _seedX = 0.137f;
            _seedY = 4.712f;
            _seedZ = 9.283f;
        }

        private void OnDisable()
        {
            if (_captured) transform.localPosition = _basePosition;
            _sustainedAmplitude = 0f;
            _kickAmplitude = 0f;
            _kickRemaining = 0f;
        }

        /// <summary>Continuous tremor amplitude in metres. Set to 0 to stop.</summary>
        public void SetSustained(float metres)
        {
            _sustainedAmplitude = Mathf.Max(0f, metres);
        }

        /// <summary>A single jolt of <paramref name="metres"/> that decays over <see cref="kickDecaySeconds"/>.</summary>
        public void Kick(float metres)
        {
            _kickAmplitude = Mathf.Max(_kickAmplitude, Mathf.Max(0f, metres));
            _kickRemaining = kickDecaySeconds;
        }

        private void LateUpdate()
        {
            if (!_captured) return;

            if (_kickRemaining > 0f)
            {
                _kickRemaining -= Time.deltaTime;
                if (_kickRemaining <= 0f) _kickAmplitude = 0f;
            }

            float kick = kickDecaySeconds <= 0f
                ? 0f
                : _kickAmplitude * Mathf.Clamp01(_kickRemaining / kickDecaySeconds);
            float amplitude = _sustainedAmplitude + kick;

            if (amplitude <= 0.00001f)
            {
                transform.localPosition = _basePosition;
                return;
            }

            // PerlinNoise rather than Random per frame: sampled along a line it is continuous, so the
            // camera drifts through the offset instead of teleporting to a new one every frame — which
            // reads as a tremor rather than as dropped frames.
            float t = Time.unscaledTime * frequency;
            var offset = new Vector3(
                Mathf.PerlinNoise(t, _seedX) - 0.5f,
                Mathf.PerlinNoise(t, _seedY) - 0.5f,
                Mathf.PerlinNoise(t, _seedZ) - 0.5f);

            // PerlinNoise's usable range is roughly [0.2, 0.8], so the centred value spans about ±0.3.
            // Scaling by 2 brings a full-amplitude request to roughly ±amplitude.
            transform.localPosition = _basePosition + offset * (amplitude * 2f);
        }
    }
}
