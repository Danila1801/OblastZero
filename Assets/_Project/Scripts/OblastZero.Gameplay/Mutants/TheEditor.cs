// Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/TheEditor.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay.Mutants
{
    /// <summary>
    /// MTN-Ψ-09/ED — The Editor. A psychic hazard rather than an enemy (BESTIARY.md §5). It does not
    /// pursue and cannot be killed. While it is in the player's line of sight it works through the
    /// pack: first redacting a label, then deleting an item, then substituting one for something else.
    /// The counter-tactic is to stop looking at it.
    ///
    /// <para><b>The escalation ladder is cumulative exposure, not elapsed time.</b> Looking away banks
    /// the progress rather than clearing it, and only a sustained break (
    /// <see cref="BalanceConstants.EDITOR_LOOK_AWAY_GRACE_SECONDS"/>) starts it decaying. Without that
    /// grace the mechanic is defeated by flicking the camera off it for one frame a second, which is
    /// both trivial and unreadable — the player would never learn why it sometimes stops.</para>
    ///
    /// <para><b>Redaction comes before deletion for a reason.</b> The bible's horror is that the pack
    /// "will differ from what the player remembers", which only works if the player had something to
    /// remember. A redacted label is visible and survivable; it is the warning that teaches what the
    /// next ten seconds cost. Deleting first would just look like a bug.</para>
    ///
    /// <para><b>It will not appear while the emission clock is held.</b> The Interview
    /// (ANM-Ψ-12/IV) suspends player input behind a black fade, and an Editor that materialised during
    /// it would edit an inventory the player cannot see, react to, or look away from — a penalty with
    /// no counter-play, which is the one thing this mutant is designed not to be.</para>
    /// </summary>
    public class TheEditor : MonoBehaviour
    {
        /// <summary>Bible classification: psychic-hazard mutant, psi series.</summary>
        public const string ClassificationCode = "MTN-Ψ-09/ED";

        [Tooltip("Layers that block line of sight. Looking at it through a wall does not count.")]
        [SerializeField] private LayerMask sightBlockers = ~0;

        /// <summary>Cumulative seconds the player has spent looking at it during this appearance.</summary>
        public float ExposureSeconds { get; private set; }

        /// <summary>True while the player's camera can actually see it.</summary>
        public bool InSight { get; private set; }

        private ScavengePlayerController _player;
        private Camera _camera;
        private RunRng _rng;

        private float _outOfSightFor;
        private float _distractedUntil;
        private bool _redacted;
        private bool _deleted;
        private bool _replaced;

        /// <summary>
        /// Called by <see cref="MutantSpawner"/>. The RNG is the run's stream, passed in rather than
        /// sampled from <c>UnityEngine.Random</c>, so which item the Editor eats is reproducible from
        /// the run seed like every other branch in the game.
        /// </summary>
        public void Initialize(ScavengePlayerController player, Camera viewCamera, RunRng rng)
        {
            _player = player;
            _camera = viewCamera != null ? viewCamera : Camera.main;
            _rng = rng;

            if (_player != null) _player.InteractPressedWithNoTarget += OnInteractPressed;

            Debug.Log($"[{ClassificationCode}] Present at {transform.position}. " +
                      "It is not looking at anything in particular.");
        }

        private void OnDestroy()
        {
            if (_player != null) _player.InteractPressedWithNoTarget -= OnInteractPressed;
        }

        private void Update()
        {
            if (_player == null || _camera == null) return;

            float dt = Time.deltaTime;

            if (Time.time < _distractedUntil)
            {
                // Reading. It is not editing anything and it is not counting.
                InSight = false;
                return;
            }

            bool visible = IsVisibleToCamera();
            if (visible != InSight)
            {
                InSight = visible;
                EventBus.Raise(new EditorSightingEvent { InSight = visible, ExposureSeconds = ExposureSeconds });
            }

            if (visible)
            {
                _outOfSightFor = 0f;
                ExposureSeconds += dt;
                Escalate();
                return;
            }

            _outOfSightFor += dt;
            if (_outOfSightFor < BalanceConstants.EDITOR_LOOK_AWAY_GRACE_SECONDS) return;

            // Exposure decays at the same rate it accrued, so a look-away costs the Editor exactly
            // what it gained. Faster decay would make glancing free; slower would make one mistake
            // unrecoverable inside a sixty-second phase.
            ExposureSeconds = Mathf.Max(0f, ExposureSeconds - dt);

            if (_outOfSightFor >= BalanceConstants.EDITOR_DESPAWN_AFTER_SECONDS && ExposureSeconds <= 0f)
                Vanish();
        }

        private void Escalate()
        {
            if (!_redacted && ExposureSeconds >= BalanceConstants.EDITOR_REDACT_AFTER_SECONDS)
            {
                _redacted = true;
                RedactOne();
            }

            if (!_deleted && ExposureSeconds >= BalanceConstants.EDITOR_DELETE_AFTER_SECONDS)
            {
                _deleted = true;
                DeleteOne();
            }

            if (!_replaced && ExposureSeconds >= BalanceConstants.EDITOR_REPLACE_AFTER_SECONDS)
            {
                _replaced = true;
                ReplaceOne();
            }
        }

        // ── The three edits ──────────────────────────────────────────────────

        private void RedactOne()
        {
            var stack = PickCarriedStack();
            if (stack == null) return;

            stack.isRedacted = true;
            Report("redacted", stack.itemDataId, null);
        }

        private void DeleteOne()
        {
            var inventory = Inventory();
            var stack = PickCarriedStack();
            if (inventory == null || stack == null) return;

            string id = stack.itemDataId;
            bool ignored;
            if (inventory.RemoveOneWeighted(InventoryChannel.Scavenged, id, _rng.NextFloat(), out ignored))
                Report("deleted", id, null);
        }

        private void ReplaceOne()
        {
            var inventory = Inventory();
            var stack = PickCarriedStack();
            if (inventory == null || stack == null) return;

            string id = stack.itemDataId;
            string substitute = PickSubstituteFor(id);
            if (substitute == null) return;

            bool ignored;
            if (!inventory.RemoveOneWeighted(InventoryChannel.Scavenged, id, _rng.NextFloat(), out ignored))
                return;

            // The substitute is granted regardless of the carry cap. It is not a pickup — the player
            // did not choose to take it, and refusing it on weight would turn the Editor's signature
            // effect into a silent no-op for exactly the players carrying enough to notice.
            var landed = inventory.AddItem(InventoryChannel.Scavenged, substitute);
            if (landed != null) landed.isRedacted = true;

            Report("replaced", id, substitute);
        }

        /// <summary>
        /// A random stack from the pack, weighted by nothing — a uniform pick over stacks rather than
        /// over units. The Editor is not reaching into a bag; it is correcting a list, and a list has
        /// one line per stack.
        /// </summary>
        private ItemInstance PickCarriedStack()
        {
            var inventory = Inventory();
            if (inventory == null) return null;

            var carried = inventory.Get(InventoryChannel.Scavenged);
            if (carried.Count == 0)
            {
                Debug.Log($"[{ClassificationCode}] Nothing in the pack to correct.");
                return null;
            }

            return carried[_rng.NextInt(0, carried.Count - 1)];
        }

        /// <summary>A different item id from the database, or null when one cannot be found.</summary>
        private string PickSubstituteFor(string originalId)
        {
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            if (db == null) return null;

            var all = db.AllItems;
            if (all == null || all.Count == 0) return null;

            // Bounded retry rather than building a filtered list of ~700 ids to draw one from.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var candidate = all[_rng.NextInt(0, all.Count - 1)];
                if (candidate == null || string.IsNullOrEmpty(candidate.id)) continue;
                if (candidate.id == originalId) continue;
                return candidate.id;
            }
            return null;
        }

        private void Report(string stage, string itemId, string replacementId)
        {
            Debug.Log($"[{ClassificationCode}] {stage}: '{itemId}'" +
                      (replacementId != null ? $" -> '{replacementId}'" : "") +
                      $" after {ExposureSeconds:0.#}s of eye contact.");

            EventBus.Raise(new EditorEditEvent
            {
                Stage = stage,
                ItemDataId = itemId,
                ReplacementItemDataId = replacementId
            });
        }

        // ── Distraction ──────────────────────────────────────────────────────

        /// <summary>
        /// The bible's counter: "can be distracted briefly by throwing certain documents (it stops to
        /// read)." Pressing interact with nothing under the crosshair, while carrying a document and
        /// with the Editor in view, spends one document to buy
        /// <see cref="BalanceConstants.EDITOR_DISTRACTION_SECONDS"/> of safety.
        /// </summary>
        private void OnInteractPressed()
        {
            if (!InSight || Time.time < _distractedUntil) return;

            var inventory = Inventory();
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            if (inventory == null || db == null) return;

            string document = FirstCarriedDocument(inventory, db);
            if (document == null) return;

            bool ignored;
            if (!inventory.RemoveOneWeighted(InventoryChannel.Scavenged, document, _rng.NextFloat(), out ignored))
                return;

            _distractedUntil = Time.time + BalanceConstants.EDITOR_DISTRACTION_SECONDS;
            AudioManager.Play3D(AudioManager.CUE_PICKUP_PAPER, transform.position, 0.8f, 0.85f);

            Debug.Log($"[{ClassificationCode}] '{document}' thrown. It stops to read for " +
                      $"{BalanceConstants.EDITOR_DISTRACTION_SECONDS:0}s.");
        }

        private static string FirstCarriedDocument(InventoryManager inventory, GameDatabase db)
        {
            var carried = inventory.Get(InventoryChannel.Scavenged);
            for (int i = 0; i < carried.Count; i++)
            {
                var data = db.GetItem(carried[i].itemDataId);
                if (data != null && data.category == ItemCategory.Document) return carried[i].itemDataId;
            }
            return null;
        }

        // ── Visibility ───────────────────────────────────────────────────────

        /// <summary>
        /// True when the Editor is inside the camera frustum and nothing solid is between it and the
        /// camera. Both halves are needed: the frustum test alone reports it visible through a wall,
        /// and the raycast alone reports it visible when it is directly behind the player.
        /// </summary>
        private bool IsVisibleToCamera()
        {
            Vector3 chest = transform.position + Vector3.up * 1.1f;
            Vector3 viewport = _camera.WorldToViewportPoint(chest);

            if (viewport.z <= 0f) return false;
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f) return false;

            Vector3 from = _camera.transform.position;
            Vector3 delta = chest - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) return true;

            return !Physics.Raycast(from, delta / distance, distance, sightBlockers,
                                    QueryTriggerInteraction.Ignore);
        }

        private void Vanish()
        {
            Debug.Log($"[{ClassificationCode}] Gone. The pack is whatever it is now.");
            EventBus.Raise(new EditorSightingEvent { InSight = false, ExposureSeconds = 0f });
            Destroy(gameObject);
        }

        private static InventoryManager Inventory()
        {
            return GameManager.Instance != null ? GameManager.Instance.Inventory : null;
        }
    }
}
