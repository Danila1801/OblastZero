// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/CarbonCopyAnomaly.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// ANM-Δ-07/CC — The Carbon Copy. Invisible duplicative anomaly occupying a small volume
    /// (BESTIARY.md §1). A pickup taken from inside the volume succeeds normally, and a duplicate of it
    /// appears in the same place. Taking the duplicate produces another, up to
    /// <see cref="BalanceConstants.CARBON_COPY_MAX_DUPLICATES"/>.
    ///
    /// <para><b>The mechanic is a trap that pays off a phase later.</b> Every copy after the first grab is
    /// flagged <see cref="ScavengePickup.IsCopy"/>, and that flag rides into the bunker as
    /// <c>ItemInstance.isDefective</c>. Defective stacks are deliberately kept <i>separate</i> from genuine
    /// ones by <c>InventoryManager</c> rather than merged, because the bible's whole point is that the
    /// player cannot tell which crate on the table is the real one — merging them would either destroy the
    /// information or, worse, silently contaminate the genuine stack. The cost lands in Phase B, where
    /// <c>EventEngine</c> reads the flag during resolution: the syringe injects the wrong fluid, the label
    /// is in the wrong Cyrillic, the signature belongs to someone who could not have signed it.</para>
    ///
    /// <para><b>Why the count is capped.</b> Uncapped, the anomaly is a free item printer — stand in it and
    /// mine one crate until the carry cap stops you. Four is the bible's own figure ("players grab 3-4
    /// copies"), and it is also roughly what fits in the seconds a player will spend before the clock pulls
    /// them away, so the cap almost never announces itself in play.</para>
    /// </summary>
    public class CarbonCopyAnomaly : AnomalyZone
    {
        [Tooltip("Copies this zone will produce in one run before it stops. " +
                 "Mirrors BalanceConstants.CARBON_COPY_MAX_DUPLICATES.")]
        [SerializeField] private int maxDuplicates = BalanceConstants.CARBON_COPY_MAX_DUPLICATES;

        private int _duplicatesSpawned;

        /// <summary>Bible classification: duplicative anomaly, delta series.</summary>
        public override string ClassificationCode { get { return "ANM-Δ-07/CC"; } }

        /// <summary>Yes — the bible gives this anomaly a characteristic non-radioactive double-click.</summary>
        public override bool IsGeigerDetectable { get { return true; } }

        /// <summary>How many duplicates this zone has produced. Read by the HUD and by tests.</summary>
        public int DuplicatesSpawned { get { return _duplicatesSpawned; } }

        /// <summary>True once the zone has produced its quota and will no longer copy.</summary>
        public bool Exhausted { get { return _duplicatesSpawned >= Mathf.Max(0, maxDuplicates); } }

        /// <summary>
        /// Called by <see cref="ScavengeController"/> the moment a pickup inside this volume has been
        /// accepted by the managers, and <i>before</i> the world object is retired. Returns the duplicate,
        /// or null when the zone is spent.
        ///
        /// <para>Ordering is load-bearing. The duplicate is cloned from the original's GameObject, so it has
        /// to happen while that object still exists — <c>PickupVfx.Play</c> takes ownership of the destroy
        /// immediately afterwards. Cloning after the fact would mean rebuilding the visual from the
        /// archetype tables, which would produce a copy that looks subtly unlike the thing it copies and
        /// hand the player the tell the anomaly is supposed to withhold.</para>
        /// </summary>
        public ScavengePickup OnPickupCollected(ScavengePickup original)
        {
            if (original == null) return null;
            if (original.Kind != ScavengePickup.PickupKind.Item)
            {
                // Crew are people. The bible's anomaly duplicates objects found in drawers, and a duplicated
                // squadmate is a different (and much larger) design than the one specified.
                return null;
            }

            if (Exhausted)
            {
                Debug.Log($"[{ClassificationCode}] Spent — {_duplicatesSpawned} copies produced, " +
                          $"cap is {maxDuplicates}. No further duplication.");
                return null;
            }

            var duplicate = SpawnDuplicate(original);
            if (duplicate == null) return null;

            _duplicatesSpawned++;
            Debug.Log($"[{ClassificationCode}] Duplicated '{original.DataId}' " +
                      $"({_duplicatesSpawned}/{maxDuplicates}). The copy is flagged defective.");

            EventBus.Raise(new AnomalyTriggeredEvent
            {
                ClassificationCode = ClassificationCode,
                DisplayName = "The Carbon Copy",
                Position = original.transform.position
            });

            return duplicate;
        }

        private ScavengePickup SpawnDuplicate(ScavengePickup original)
        {
            var go = Instantiate(original.gameObject, original.transform.position,
                                 original.transform.rotation, original.transform.parent);
            go.name = original.gameObject.name + "_Copy" + (_duplicatesSpawned + 1);

            var pickup = go.GetComponent<ScavengePickup>();
            if (pickup == null)
            {
                // Cloning a pickup produced something that is not one. That is a scene-authoring fault, not
                // a runtime condition to absorb quietly — destroy the orphan so it cannot sit in the level
                // looking grabbable.
                Debug.LogError($"[{ClassificationCode}] Duplicate of '{original.name}' has no " +
                               "ScavengePickup component. Destroying the orphan.");
                Destroy(go);
                return null;
            }

            pickup.MarkAsCarbonCopy();
            return pickup;
        }
    }
}
