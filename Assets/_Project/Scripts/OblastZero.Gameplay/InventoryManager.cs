// Assets/_Project/Scripts/Gameplay/InventoryManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>Which inventory list a request targets on the active run.</summary>
    public enum InventoryChannel
    {
        Scavenged, // filled during the 60-second 3D Blowout
        Bunker     // persistent Phase-2 storage
    }

    /// <summary>
    /// The ONLY class permitted to write <see cref="RunData.ScavengedInventory"/> and
    /// <see cref="RunData.BunkerInventory"/>. Everything else reads through it and listens to its events.
    /// Plain C# class for testability: construct with a <see cref="GameDatabase"/>, then <see cref="Bind"/>
    /// the active run (on new run / load). Register in your ServiceLocator at bootstrap.
    ///
    /// Stacking rule: instances merge only when itemDataId + durability + contamination all match, so a
    /// half-broken axe never merges with a pristine one, while plain consumables stack naturally.
    /// </summary>
    public class InventoryManager
    {
        /// <summary>Tolerance on carry-weight comparisons so a load of exactly the cap still fits.</summary>
        private const float kWeightEpsilon = 0.0005f;

        private readonly GameDatabase _db;
        private RunData _run;
        private float _scavengeCapacityKg = BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG;

        public event Action<ItemInstance, InventoryChannel> ItemAdded;
        public event Action<ItemInstance, InventoryChannel> ItemRemoved;
        public event Action<ItemInstance, InventoryChannel> ItemChanged;
        public event Action<ItemInstance, InventoryChannel, InventoryChannel> ItemTransferred;

        /// <summary>Scavenged load moved. Args: currentKg, capacityKg.</summary>
        public event Action<float, float> ScavengeLoadChanged;

        /// <summary>A Blowout pickup was refused for weight. Args: itemDataId, itemKg, currentKg, capacityKg.</summary>
        public event Action<string, float, float, float> ScavengePickupRejected;

        public InventoryManager(GameDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>Point the manager at the run it should mutate. Call on new run and after load.</summary>
        public void Bind(RunData run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            Debug.Log($"[InventoryManager] Bound to run '{run.runId}'. " +
                      $"Scavenge carry capacity {_scavengeCapacityKg:0.##} kg.");
            RaiseScavengeLoadChanged();
        }

        // ---- Scavenge carry capacity ----

        /// <summary>
        /// Weight ceiling in kg on the Scavenged channel during the 60-second Blowout. Defaults to
        /// <see cref="BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG"/>. Settable so a run can override the
        /// baseline (loadout perks, a lead crew member's carry capacity) without this class knowing why.
        /// The Bunker channel is deliberately uncapped — shelf space is a Phase-2 problem, not a Phase-A one.
        /// </summary>
        public float ScavengeCarryCapacityKg
        {
            get { return _scavengeCapacityKg; }
            set
            {
                float clamped = Mathf.Max(0f, value);
                if (Mathf.Abs(clamped - _scavengeCapacityKg) < kWeightEpsilon) return;
                _scavengeCapacityKg = clamped;
                Debug.Log($"[InventoryManager] Scavenge carry capacity set to {_scavengeCapacityKg:0.##} kg.");
                RaiseScavengeLoadChanged();
            }
        }

        /// <summary>Current weight in kg carried on the Scavenged channel. 0 when no run is bound.</summary>
        public float ScavengeLoadKg
        {
            get { return _run == null ? 0f : WeightOf(_run.ScavengedInventory); }
        }

        /// <summary>Headroom in kg before the Blowout carry cap is hit.</summary>
        public float ScavengeRemainingKg
        {
            get { return Mathf.Max(0f, _scavengeCapacityKg - ScavengeLoadKg); }
        }

        /// <summary>
        /// Whether a prospective Blowout pickup fits under the carry cap. All-or-nothing: a stack that
        /// only partly fits is refused whole, so the world object stays grabbable and the player keeps the
        /// choice. <paramref name="incomingKg"/> is the weight the pickup would add.
        /// </summary>
        public bool WouldFitInScavenge(string itemDataId, int quantity, out float incomingKg)
        {
            incomingKg = 0f;
            if (quantity <= 0) return false;

            var data = _db.GetItem(itemDataId);
            if (data == null) return false;

            incomingKg = Mathf.Max(0f, data.weightKg) * quantity;
            return ScavengeLoadKg + incomingKg <= _scavengeCapacityKg + kWeightEpsilon;
        }

        // ---- Mutations ----

        /// <summary>
        /// Adds an item to a channel, merging into a matching stack when possible.
        /// <paramref name="durability"/> defaults to the item's max; pass a value for field-found items.
        /// </summary>
        /// <param name="defective">
        /// True when the stack is a Carbon Copy duplicate. Part of stack identity, so a defective stack
        /// never merges into a genuine one — see <see cref="ItemInstance.isDefective"/>.
        /// </param>
        public ItemInstance AddItem(InventoryChannel channel, string itemDataId, int quantity = 1,
                                    int? durability = null, float contamination = 0f,
                                    bool defective = false)
        {
            if (!Ready(nameof(AddItem))) return null;
            if (quantity <= 0)
            {
                Debug.LogWarning($"[InventoryManager] AddItem ignored: quantity {quantity} for '{itemDataId}'.");
                return null;
            }

            var data = _db.GetItem(itemDataId);
            if (data == null) return null; // GameDatabase already logged the miss

            // Blowout carry cap. Enforced here rather than at the pickup site so every route into the
            // Scavenged channel obeys it — world grabs, scripted grants, debug tools alike.
            if (channel == InventoryChannel.Scavenged)
            {
                float incomingKg = Mathf.Max(0f, data.weightKg) * quantity;
                float currentKg = WeightOf(_run.ScavengedInventory);

                if (currentKg + incomingKg > _scavengeCapacityKg + kWeightEpsilon)
                {
                    Debug.Log($"[InventoryManager] Pickup refused (too heavy): '{itemDataId}' x{quantity} " +
                              $"weighs {incomingKg:0.##} kg but only {Mathf.Max(0f, _scavengeCapacityKg - currentKg):0.##} kg " +
                              $"of {_scavengeCapacityKg:0.##} kg remains (carrying {currentKg:0.##} kg).");
                    ScavengePickupRejected?.Invoke(itemDataId, incomingKg, currentKg, _scavengeCapacityKg);
                    return null;
                }
            }

            int maxDur = Mathf.Max(0, data.durability);
            int durabilityValue = Mathf.Clamp(durability ?? maxDur, 0, maxDur);
            var list = Channel(channel);

            var stack = FindStack(list, itemDataId, durabilityValue, contamination, defective);
            if (stack != null)
            {
                stack.quantity += quantity;
                Debug.Log($"[InventoryManager] Stacked +{quantity} '{itemDataId}' in {channel} (now {stack.quantity}).");
                ItemChanged?.Invoke(stack, channel);
                if (channel == InventoryChannel.Scavenged) RaiseScavengeLoadChanged();
                return stack;
            }

            var inst = new ItemInstance
            {
                itemDataId = itemDataId,
                currentDurability = durabilityValue,
                currentContamination = contamination,
                quantity = quantity,
                isDefective = defective
            };
            list.Add(inst);
            Debug.Log($"[InventoryManager] Added new stack '{itemDataId}' x{quantity} to {channel}" +
                      (defective ? " (defective — Carbon Copy duplicate)." : "."));
            ItemAdded?.Invoke(inst, channel);
            if (channel == InventoryChannel.Scavenged) RaiseScavengeLoadChanged();
            return inst;
        }

        /// <summary>
        /// Removes exactly one unit and reports whether the unit taken was a Carbon Copy duplicate.
        /// Returns false when the channel holds none.
        ///
        /// <para><b>Which unit gets taken is weighted chance, and that is the mechanic.</b> The bible is
        /// explicit that the crew cannot tell a copy from the original — "the crate on the table looks
        /// correct, the crate on the floor also looks correct" — so a player holding one genuine med kit
        /// and three copies runs a 75% risk every time one is used. Taking the genuine stack first would
        /// make the copies a delayed inconvenience; taking the defective one first would make them an
        /// immediate tax. Weighting by quantity is the only version where the player's own decision at the
        /// anomaly — how many did I grab? — is what sets the odds.</para>
        ///
        /// <para><paramref name="selectionRoll"/> is a pre-drawn value in [0,1) from the run's RNG stream.
        /// Passed in rather than drawn here because <see cref="InventoryManager"/> holds no RNG and must
        /// not: everything that branches goes through the seed + stream counter on <c>RunData</c>, or the
        /// run stops being reproducible.</para>
        /// </summary>
        public bool RemoveOneWeighted(InventoryChannel channel, string itemDataId, float selectionRoll,
                                      out bool wasDefective)
        {
            wasDefective = false;
            if (!Ready(nameof(RemoveOneWeighted))) return false;

            var list = Channel(channel);

            int total = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].itemDataId == itemDataId) total += Mathf.Max(0, list[i].quantity);

            if (total <= 0) return false;

            // Walk the stacks accumulating quantity until the roll's share is crossed. Equivalent to
            // picking one unit uniformly at random out of every unit held, without materialising them.
            int target = Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(selectionRoll) * total), 0, total - 1);
            int cursor = 0;

            for (int i = 0; i < list.Count; i++)
            {
                var inst = list[i];
                if (inst.itemDataId != itemDataId) continue;

                int q = Mathf.Max(0, inst.quantity);
                if (target >= cursor + q) { cursor += q; continue; }

                wasDefective = inst.isDefective;
                inst.quantity -= 1;

                if (inst.quantity <= 0)
                {
                    list.RemoveAt(i);
                    ItemRemoved?.Invoke(inst, channel);
                }
                else
                {
                    ItemChanged?.Invoke(inst, channel);
                }

                if (channel == InventoryChannel.Scavenged) RaiseScavengeLoadChanged();

                Debug.Log($"[InventoryManager] Consumed 1 '{itemDataId}' from {channel} " +
                          $"({(wasDefective ? "a copy" : "genuine")}; {total - 1} left of that id).");
                return true;
            }

            return false;
        }

        /// <summary>True when the channel holds at least one defective stack of this item.</summary>
        public bool HasDefective(InventoryChannel channel, string itemDataId)
        {
            if (!Ready(nameof(HasDefective))) return false;

            var list = Channel(channel);
            for (int i = 0; i < list.Count; i++)
                if (list[i].itemDataId == itemDataId && list[i].isDefective && list[i].quantity > 0)
                    return true;
            return false;
        }

        /// <summary>Total defective units held in a channel, across every item id. Drives the bunker readout.</summary>
        public int DefectiveUnitCount(InventoryChannel channel)
        {
            if (!Ready(nameof(DefectiveUnitCount))) return 0;

            int n = 0;
            var list = Channel(channel);
            for (int i = 0; i < list.Count; i++)
                if (list[i].isDefective) n += Mathf.Max(0, list[i].quantity);
            return n;
        }

        /// <summary>Removes <paramref name="quantity"/> of an item from a channel. Returns false if short.</summary>
        public bool RemoveItem(InventoryChannel channel, string itemDataId, int quantity = 1)
        {
            if (!Ready(nameof(RemoveItem))) return false;
            if (quantity <= 0) return false;

            var list = Channel(channel);
            int remaining = quantity;

            for (int i = list.Count - 1; i >= 0 && remaining > 0; i--)
            {
                if (list[i].itemDataId != itemDataId) continue;
                var inst = list[i];
                int take = Mathf.Min(inst.quantity, remaining);
                inst.quantity -= take;
                remaining -= take;

                if (inst.quantity <= 0)
                {
                    list.RemoveAt(i);
                    ItemRemoved?.Invoke(inst, channel);
                }
                else
                {
                    ItemChanged?.Invoke(inst, channel);
                }
            }

            if (channel == InventoryChannel.Scavenged) RaiseScavengeLoadChanged();

            if (remaining > 0)
            {
                Debug.LogWarning($"[InventoryManager] RemoveItem '{itemDataId}' short by {remaining} in {channel}.");
                return false;
            }
            Debug.Log($"[InventoryManager] Removed '{itemDataId}' x{quantity} from {channel}.");
            return true;
        }

        /// <summary>
        /// The 3D→2D handoff for items: moves every scavenged stack into the bunker (merging where it can)
        /// and clears the scavenged list. Called at the transition cutscene.
        /// </summary>
        public void TransferScavengedToBunker()
        {
            if (!Ready(nameof(TransferScavengedToBunker))) return;

            var scavenged = _run.ScavengedInventory;
            if (scavenged.Count == 0)
            {
                Debug.Log("[InventoryManager] No scavenged items to transfer.");
                return;
            }

            var snapshot = new List<ItemInstance>(scavenged);
            scavenged.Clear();
            foreach (var inst in snapshot)
            {
                // isDefective must cross the phase boundary with the stack. This is the single line that
                // makes the Carbon Copy a two-phase mechanic instead of a Phase A curiosity: drop it and
                // every duplicate launders itself into a genuine item at the transition cutscene.
                var landed = AddItem(InventoryChannel.Bunker, inst.itemDataId, inst.quantity,
                                     inst.currentDurability, inst.currentContamination, inst.isDefective);
                if (landed != null)
                    ItemTransferred?.Invoke(landed, InventoryChannel.Scavenged, InventoryChannel.Bunker);
            }
            Debug.Log($"[InventoryManager] Transferred {snapshot.Count} scavenged stack(s) into the bunker.");

            // The pack is empty again — the next Blowout starts from zero.
            RaiseScavengeLoadChanged();
        }

        /// <summary>
        /// Applies one day of decay to bunker items with decayPerDay &gt; 0. Decay is taken as
        /// ceil(decayPerDay) off currentDurability per day. (If you want sub-1/day rates, we move
        /// ItemInstance.currentDurability to float — flag it and it's a 2-minute change.)
        /// </summary>
        public void ApplyDailyDecay()
        {
            if (!Ready(nameof(ApplyDailyDecay))) return;

            int decayedStacks = 0;
            foreach (var inst in _run.BunkerInventory)
            {
                var data = _db.GetItem(inst.itemDataId);
                if (data == null || data.decayPerDay <= 0f) continue;

                int loss = Mathf.CeilToInt(data.decayPerDay);
                int before = inst.currentDurability;
                inst.currentDurability = Mathf.Max(0, inst.currentDurability - loss);

                if (inst.currentDurability != before)
                {
                    decayedStacks++;
                    ItemChanged?.Invoke(inst, InventoryChannel.Bunker);
                }
            }
            if (decayedStacks > 0)
                Debug.Log($"[InventoryManager] Daily decay applied to {decayedStacks} bunker stack(s).");
        }

        // ---- Queries ----

        /// <summary>Total weight in kg of a channel, resolved via item data.</summary>
        public float GetTotalWeight(InventoryChannel channel)
        {
            if (!Ready(nameof(GetTotalWeight))) return 0f;
            return WeightOf(Channel(channel));
        }

        /// <summary>Read-only view of a channel's stacks. Never mutate the returned list directly.</summary>
        public IReadOnlyList<ItemInstance> Get(InventoryChannel channel)
            => Ready(nameof(Get)) ? Channel(channel) : Array.Empty<ItemInstance>();

        // ---- Internals ----

        private List<ItemInstance> Channel(InventoryChannel c)
            => c == InventoryChannel.Scavenged ? _run.ScavengedInventory : _run.BunkerInventory;

        /// <summary>
        /// Summed kg of a stack list. Scans the run's own list (tens of entries at most), never the
        /// content set — the GameDatabase lookup behind it is an O(1) id index.
        /// </summary>
        private float WeightOf(List<ItemInstance> list)
        {
            float total = 0f;
            foreach (var inst in list)
            {
                if (!_db.TryGetItem(inst.itemDataId, out var data) || data == null) continue;
                total += Mathf.Max(0f, data.weightKg) * inst.quantity;
            }
            return total;
        }

        /// <summary>Fires <see cref="ScavengeLoadChanged"/> with the live load. Safe before Bind.</summary>
        private void RaiseScavengeLoadChanged()
        {
            if (_run == null) return;
            ScavengeLoadChanged?.Invoke(WeightOf(_run.ScavengedInventory), _scavengeCapacityKg);
        }

        private bool Ready(string op)
        {
            if (_run != null) return true;
            Debug.LogError($"[InventoryManager] {op} called before Bind(RunData). No-op.");
            return false;
        }

        /// <summary>
        /// A matching stack, or null. <paramref name="defective"/> is part of the identity: a Carbon Copy
        /// duplicate and the genuine article are the same item id with the same durability and the same
        /// contamination, and merging them is exactly the outcome the anomaly must not produce. Keeping
        /// them apart is what leaves the player with two crates on the table and no way to tell which is
        /// which — the bible's whole scene.
        /// </summary>
        private static ItemInstance FindStack(List<ItemInstance> list, string id, int durability,
                                              float contamination, bool defective)
        {
            const float eps = 0.0001f;
            foreach (var inst in list)
            {
                if (inst.itemDataId == id
                    && inst.currentDurability == durability
                    && inst.isDefective == defective
                    && Mathf.Abs(inst.currentContamination - contamination) < eps)
                    return inst;
            }
            return null;
        }
    }
}
