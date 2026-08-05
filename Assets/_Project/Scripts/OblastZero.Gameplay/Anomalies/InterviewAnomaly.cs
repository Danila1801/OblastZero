// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/InterviewAnomaly.cs
using UnityEngine;
using OblastZero.Core;
using OblastZero.UI;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// ANM-Ψ-12/IV — The Interview. Room-scale cognitive anomaly (BESTIARY.md §2). The player may walk
    /// through it safely. Sitting at the desk fades the screen, stops the emission clock, and plays a fixed
    /// six-item personnel form that stops being a personnel form around item four. Completing it pays an
    /// artifact; walking out pays nothing and costs nothing.
    ///
    /// <para><b>Why the reward is filed to the bunker rather than added to the pack.</b> The Blowout has a
    /// hard carry cap, so an artifact added to the scavenged channel by a full-handed player is silently
    /// refused — the anomaly's entire payoff would evaporate into a log line at the exact moment it is
    /// meant to land. Routing it to the bunker channel instead is both mechanically safe (that channel is
    /// uncapped) and the more literal reading of the fiction: the Interview does not hand you an object, it
    /// completes a form, and the form is waiting when you get back.</para>
    ///
    /// <para><b>The room being bigger inside is a level-geometry job, not a script.</b> The bible's
    /// "interior larger than the exterior suggests" is achieved by the scene generator sizing the admin
    /// room's interior shell past its exterior footprint; this component owns only the interaction. Putting
    /// the illusion in code would mean moving colliders at runtime under a CharacterController, which is how
    /// you get a player standing inside a wall.</para>
    /// </summary>
    public class InterviewAnomaly : AnomalyZone
    {
        [Tooltip("Where the player is placed when they sit. Optional — the zone centre is used if unset.")]
        [SerializeField] private Transform sitPosition;

        /// <summary>Bible classification: cognitive anomaly, psi series.</summary>
        public override string ClassificationCode { get { return "ANM-Ψ-12/IV"; } }

        /// <summary>No — the bible marks the Interview as Geiger-undetectable. You read the room or you don't.</summary>
        public override bool IsGeigerDetectable { get { return false; } }

        /// <summary>True once this room has been sat in. One interview per run; the form is already filed.</summary>
        public bool Completed { get; private set; }

        private ScavengePlayerController _player;
        private InterviewSequenceUI _screen;
        private bool _inSession;

        protected override void OnPlayerEnter(Collider player)
        {
            _player = player != null ? player.GetComponentInParent<ScavengePlayerController>() : null;
            if (_player == null || Completed || _inSession) return;

            _player.InteractPressedWithNoTarget += OnInteractPressed;
            EventBus.Raise(new AnomalyPromptEvent
            {
                Show = true,
                Text = "Sit for the interview"
            });

            Debug.Log($"[{ClassificationCode}] Player entered the interview room. " +
                      "The proportions are wrong and the chair is pulled out.");
        }

        protected override void OnPlayerExit(Collider player)
        {
            if (_player != null) _player.InteractPressedWithNoTarget -= OnInteractPressed;
            _player = null;

            // A session in progress survives the trigger exit: sitting teleports the player to the desk,
            // and depending on where the desk sits relative to the volume that can read as leaving. The
            // session is ended by the screen, never by geometry.
            if (!_inSession) EventBus.Raise(new AnomalyPromptEvent { Show = false, Text = string.Empty });
        }

        private void OnInteractPressed()
        {
            if (_inSession || Completed || _player == null) return;

            // Looking at a pickup takes priority: the same key grabs, and a player reaching for a document
            // on the desk must not be seated by it. ScavengePlayerController raises PickupRequested for
            // that case and this event for every press, so the two are distinguished by the look target.
            if (_player.HasLookTarget) return;

            BeginSession();
        }

        private void BeginSession()
        {
            _inSession = true;

            EventBus.Raise(new AnomalyPromptEvent { Show = false, Text = string.Empty });
            EventBus.Raise(new AnomalyTriggeredEvent
            {
                ClassificationCode = ClassificationCode,
                DisplayName = "The Interview",
                Position = transform.position
            });

            // Order matters: hold the clock before suspending input, so a frame spent building the screen
            // is not a frame charged to the player's sixty seconds.
            ScavengeClockReadout.RequestHold("interview");
            _player.InputSuspended = true;

            Vector3 seat = sitPosition != null ? sitPosition.position : transform.position;
            MovePlayerTo(seat);

            Debug.Log($"[{ClassificationCode}] Interview begins. {InterviewSequenceUI.QuestionCount} items. " +
                      "Emission clock held.");

            _screen = InterviewSequenceUI.Open(OnSessionFinished);
        }

        /// <summary>
        /// Teleports the player to the desk. The CharacterController must be disabled across the move or it
        /// resolves the displacement as a collision sweep and refuses it — a silent no-op that leaves the
        /// player standing in the doorway watching the interview happen without them.
        /// </summary>
        private void MovePlayerTo(Vector3 position)
        {
            var controller = _player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            _player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private void OnSessionFinished(InterviewSequenceUI.Outcome outcome)
        {
            _screen = null;
            _inSession = false;
            Completed = outcome != InterviewSequenceUI.Outcome.Abandoned;

            ScavengeClockReadout.ReleaseHold("interview");

            if (_player != null)
            {
                _player.InputSuspended = false;
                _player.InteractPressedWithNoTarget -= OnInteractPressed;
            }

            switch (outcome)
            {
                case InterviewSequenceUI.Outcome.Abandoned:
                    Debug.Log($"[{ClassificationCode}] Subject left before the form was complete. " +
                              "No entry made. No reward.");
                    break;

                case InterviewSequenceUI.Outcome.CompletedConsented:
                    // Consent buys the override and costs the thing that noticed it was being edited.
                    FileArtifact(ArtifactIds.StampedTongue, "consent recorded");
                    ApplyOperatorSanityCost(BalanceConstants.INTERVIEW_CONSENT_SANITY_COST);
                    break;

                case InterviewSequenceUI.Outcome.CompletedDeclined:
                    FileArtifact(ArtifactIds.NotarizedHeart, "consent withheld; correction applied anyway");
                    break;
            }
        }

        /// <summary>
        /// Puts the awarded artifact straight into the bunker channel. Returns false when the item id is
        /// not in the database, which is a content fault worth an error rather than a shrug — a missing
        /// artifact makes the anomaly look like it does nothing.
        /// </summary>
        private bool FileArtifact(string itemId, string reason)
        {
            var inventory = GameManager.Instance != null ? GameManager.Instance.Inventory : null;
            if (inventory == null)
            {
                Debug.LogError($"[{ClassificationCode}] No InventoryManager — artifact '{itemId}' lost. " +
                               "A run must be active before Phase A.");
                return false;
            }

            var added = inventory.AddItem(InventoryChannel.Bunker, itemId);
            if (added == null)
            {
                Debug.LogError($"[{ClassificationCode}] Could not file '{itemId}' ({reason}). " +
                               "Check the item exists in the database.");
                return false;
            }

            Debug.Log($"[{ClassificationCode}] Form complete — {reason}. " +
                      $"'{itemId}' filed to the bunker inventory.");

            EventBus.Raise(new AnomalyRewardEvent
            {
                ClassificationCode = ClassificationCode,
                ItemDataId = itemId,
                Reason = reason
            });
            return true;
        }

        private void ApplyOperatorSanityCost(int cost)
        {
            if (cost <= 0) return;

            var crew = GameManager.Instance != null ? GameManager.Instance.Crew : null;
            var operatorInstance = crew != null ? crew.FieldOperator() : null;
            if (operatorInstance == null)
            {
                Debug.LogWarning($"[{ClassificationCode}] No operator in the field to charge the " +
                                 $"{cost}-point sanity cost to. Consent recorded without it.");
                return;
            }

            crew.ApplySanityDelta(operatorInstance.instanceId, -cost);
            Debug.Log($"[{ClassificationCode}] Operator '{operatorInstance.instanceId}' " +
                      $"loses {cost} sanity — they watched the correction being made.");
        }
    }
}
