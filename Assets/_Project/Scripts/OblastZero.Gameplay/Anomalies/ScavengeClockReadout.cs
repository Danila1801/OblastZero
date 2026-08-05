// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/ScavengeClockReadout.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// A read-only window onto the emission clock, plus the one legitimate way to stop it.
    ///
    /// <para><b>Why this exists rather than a reference to the timer.</b> <c>ScavengePhase3DState</c> owns
    /// the <see cref="EmissionTimer"/> and is its only writer — that single-owner rule is what keeps the
    /// clock honest, and handing scene objects a reference to it would end that immediately. But two
    /// anomalies genuinely need the clock: the Backlog wants to <i>read</i> it (to log how much of the run
    /// a player just spent), and the Interview must <i>stop</i> it (the bible: "screen fades to black, timer
    /// pauses"). Both go through here. Reads come off the tick event the clock already broadcasts; the stop
    /// is a request the owning state grants, so the state stays the only thing that decides whether the
    /// clock advances.</para>
    ///
    /// <para><b>The hold is counted, not boolean.</b> Two overlapping holders releasing in either order must
    /// leave the clock running, and a boolean cannot express that. In practice only the Interview holds it
    /// today, but a counter costs nothing and removes an entire class of "the timer never restarted" bug.
    /// <see cref="ResetForNewPhase"/> zeroes it on phase entry so an abandoned run cannot leak a hold into
    /// the next one.</para>
    /// </summary>
    public static class ScavengeClockReadout
    {
        private static float _secondsRemaining = -1f;
        private static int _holdCount;

        /// <summary>
        /// Seconds left on the emission clock as of the last whole-second tick, or -1 outside Phase A.
        /// Coarse by construction: <see cref="EmissionTimer"/> only broadcasts on second boundaries, and
        /// nothing here needs finer than that.
        /// </summary>
        public static float SecondsRemaining { get { return _secondsRemaining; } }

        /// <summary>True while at least one holder is stopping the clock.</summary>
        public static bool IsHeld { get { return _holdCount > 0; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            // Static handlers retain nothing but this class, which lives for the domain's lifetime anyway,
            // so there is no unsubscribe to forget. Re-running after a domain reload is correct: the
            // subscription list was rebuilt too.
            _secondsRemaining = -1f;
            _holdCount = 0;
            EventBus.Subscribe<ScavengeTimerTickEvent>(OnTick);
            EventBus.Subscribe<ScavengeTimerExpiredEvent>(OnExpired);
        }

        private static void OnTick(ScavengeTimerTickEvent e) { _secondsRemaining = e.SecondsRemaining; }
        private static void OnExpired(ScavengeTimerExpiredEvent e) { _secondsRemaining = 0f; }

        /// <summary>Called by the owning phase state on entry. Clears any hold a previous run leaked.</summary>
        public static void ResetForNewPhase()
        {
            _secondsRemaining = -1f;
            if (_holdCount != 0)
            {
                Debug.LogWarning($"[ScavengeClock] {_holdCount} clock hold(s) survived the last Blowout. " +
                                 "Clearing — an unreleased hold would freeze the new run's emission timer.");
                _holdCount = 0;
            }
        }

        /// <summary>
        /// Stops the emission clock. Every call must be matched by <see cref="ReleaseHold"/>; the clock
        /// resumes when the last holder releases.
        /// </summary>
        public static void RequestHold(string reason)
        {
            _holdCount++;
            if (_holdCount == 1)
                Debug.Log($"[ScavengeClock] Emission clock held ({reason}). " +
                          $"{_secondsRemaining:0}s frozen on the face.");
        }

        /// <summary>Releases one hold. Harmless when none is outstanding.</summary>
        public static void ReleaseHold(string reason)
        {
            if (_holdCount <= 0)
            {
                Debug.LogWarning($"[ScavengeClock] ReleaseHold('{reason}') with no hold outstanding — ignored.");
                return;
            }

            _holdCount--;
            if (_holdCount == 0)
                Debug.Log($"[ScavengeClock] Emission clock resumed ({reason}). {_secondsRemaining:0}s remain.");
        }
    }
}
