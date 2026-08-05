// Assets/_Project/Scripts/OblastZero.Gameplay/PickupRefusalShake.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The refused-pickup wobble: ±10° about Y over 200 ms, decaying, ending exactly where it started.
    /// Added on demand by <see cref="PickupVfx.PlayRefusal"/>.
    ///
    /// <para>Returning cleanly to the rest rotation is the whole trick. An over-cap pickup is <b>not</b>
    /// removed from the world — carry-weight refusals are all-or-nothing and the object stays put so the
    /// player keeps the choice — so this animation runs on an object the player will very likely poke
    /// again. Leaving a few degrees of residue behind each time would visibly walk a refused crate
    /// around its shelf over a run, and re-capturing the "rest" pose mid-wobble is how that happens.
    /// Hence the idle check in <see cref="Restart"/>.</para>
    /// </summary>
    public class PickupRefusalShake : MonoBehaviour
    {
        private const float DurationSeconds = 0.20f;
        private const float AmplitudeDegrees = 10f;
        private const float Cycles = 2f;

        private Quaternion _baseRotation;
        private float _elapsed = -1f;

        /// <summary>Starts, or restarts, the wobble from the object's true resting rotation.</summary>
        public void Restart()
        {
            if (_elapsed < 0f) _baseRotation = transform.localRotation;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (_elapsed < 0f) return;

            _elapsed += Time.deltaTime;
            if (_elapsed >= DurationSeconds)
            {
                transform.localRotation = _baseRotation;
                _elapsed = -1f;
                return;
            }

            float k = _elapsed / DurationSeconds;
            float angle = Mathf.Sin(k * Mathf.PI * 2f * Cycles) * AmplitudeDegrees * (1f - k);
            transform.localRotation = _baseRotation * Quaternion.Euler(0f, angle, 0f);
        }
    }
}
