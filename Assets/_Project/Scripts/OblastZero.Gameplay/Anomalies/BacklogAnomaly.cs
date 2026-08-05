// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/BacklogAnomaly.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// ANM-Χ-21/BL — The Backlog. Volumetric temporal anomaly (BESTIARY.md §3). Subjective time inside runs
    /// 40×–100× slower than outside: the player's movement and interaction crawl while the emission clock
    /// keeps its normal pace.
    ///
    /// <para><b>This is the only anomaly that is a pure trap, and it is deliberately visible.</b> The bible
    /// is explicit that skilled players should identify and avoid it — hanging dust motes, distorted air. So
    /// the danger is not that it is hidden; it is that it sits across a shortcut, and a player with fifteen
    /// seconds left and a clear line to the door has to decide whether the shortcut is really a shortcut.
    /// Stepping in at thirty seconds forfeits the run. That decision only exists if the zone is legible, so
    /// it ships with its particles and its own audio treatment rather than as an invisible gotcha.</para>
    ///
    /// <para><b>Why the siren is exempt from the pitch drag.</b> Everything ambient pitches down as you cross
    /// the boundary — that is the sensory read on "time is thick here". The emission siren does not, because
    /// the entire mechanic is that the clock is <i>indifferent</i> to the anomaly. Dragging the siren down
    /// with everything else would tell the player's ear that the deadline slowed too, which is precisely the
    /// false conclusion that kills them. See <c>AudioManager.SetTemporalDrag</c>.</para>
    ///
    /// <para><b>Restoration is absolute, not incremental.</b> Exit sets the multiplier back to 1 rather than
    /// dividing out what was applied. Anomaly volumes do not overlap — the base class documents that as a
    /// level bug and the scene generator's placement gate enforces it — so there is no nesting to unwind,
    /// and an absolute reset cannot accumulate float drift across repeated crossings the way a
    /// multiply/divide pair does.</para>
    /// </summary>
    public class BacklogAnomaly : AnomalyZone
    {
        [Tooltip("Player speed inside the zone, as a fraction of normal. " +
                 "Mirrors BalanceConstants.BACKLOG_TIME_DILATION_FACTOR.")]
        [SerializeField, Range(0.005f, 1f)]
        private float timeDilationFactor = BalanceConstants.BACKLOG_TIME_DILATION_FACTOR;

        [Tooltip("The hanging-mote haze that marks this volume. Auto-found among the children. Runs " +
                 "continuously — see BacklogMotes for why it must never be gated on entry.")]
        [SerializeField] private BacklogMotes hangingMotes;

        /// <summary>Bible classification: temporal anomaly, chi series.</summary>
        public override string ClassificationCode { get { return "ANM-Χ-21/BL"; } }

        /// <summary>No — the bible gives the Geiger to the Carbon Copy alone. This one you see, or you don't.</summary>
        public override bool IsGeigerDetectable { get { return false; } }

        /// <summary>The speed fraction applied inside. Read by the HUD warning and by tests.</summary>
        public float TimeDilationFactor { get { return Mathf.Clamp(timeDilationFactor, 0.005f, 1f); } }

        private ScavengePlayerController _player;

        protected override void Awake()
        {
            base.Awake();

            // Auto-find keeps the generator from having to emit a component reference, which would be a
            // scene fileID it has no other reason to know. A child carrying the effect is enough.
            if (hangingMotes == null) hangingMotes = GetComponentInChildren<BacklogMotes>(true);

            // The haze is sized from the trigger box rather than authored separately, so the thing the
            // player can see and the thing that actually catches them are the same volume by construction.
            // Two numbers in the level plan would eventually disagree, and the failure is invisible: a
            // player avoiding the visible edge would still be caught by a trigger that reached further.
            if (hangingMotes != null && Volume != null)
                hangingMotes.SetVolume(Volume.bounds.size);
            else if (hangingMotes == null)
                Debug.LogWarning($"[{ClassificationCode}] No BacklogMotes child — this zone is invisible. " +
                                 "The bible requires the Backlog to be identifiable before it is entered.");
        }

        protected override void OnPlayerEnter(Collider player)
        {
            _player = player != null ? player.GetComponentInParent<ScavengePlayerController>() : null;
            if (_player == null) return;

            _player.SpeedMultiplier = TimeDilationFactor;
            _player.InteractionDelaySeconds = BalanceConstants.BACKLOG_INTERACTION_DELAY_SECONDS;

            AudioManager.SetTemporalDrag(BalanceConstants.BACKLOG_AUDIO_PITCH_FACTOR);

            float secondsLeft = EstimateSecondsLeft();
            Debug.Log($"[{ClassificationCode}] Player entered the Backlog. Speed → " +
                      $"{TimeDilationFactor:P1} of normal; the clock is unaffected" +
                      (secondsLeft >= 0f ? $" ({secondsLeft:0}s remain)." : "."));

            EventBus.Raise(new AnomalyTriggeredEvent
            {
                ClassificationCode = ClassificationCode,
                DisplayName = "The Backlog",
                Position = transform.position
            });

            EventBus.Raise(new BacklogStateChangedEvent { Inside = true, DilationFactor = TimeDilationFactor });
        }

        protected override void OnPlayerExit(Collider player)
        {
            if (_player != null)
            {
                _player.SpeedMultiplier = 1f;
                _player.InteractionDelaySeconds = 0f;
                _player = null;
            }

            AudioManager.SetTemporalDrag(1f);

            Debug.Log($"[{ClassificationCode}] Player left the Backlog. Speed restored.");
            EventBus.Raise(new BacklogStateChangedEvent { Inside = false, DilationFactor = 1f });
        }

        /// <summary>
        /// Seconds left on the emission clock, or -1 when it cannot be read. Log-only: the zone never gates
        /// on it, because refusing to let a player make a fatal decision is not the same game.
        /// </summary>
        private static float EstimateSecondsLeft()
        {
            return ScavengeClockReadout.SecondsRemaining;
        }
    }
}
