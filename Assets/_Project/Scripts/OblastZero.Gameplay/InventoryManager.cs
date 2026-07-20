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
        private readonly GameDatabase _db;
        private RunData _run;

        public event Action<ItemInstance, InventoryChannel> ItemAdded;
        public event Action<ItemInstance, InventoryChannel> ItemRemoved;
        public event Action<ItemInstance, InventoryChannel> ItemChanged;
        public event Action<ItemInstance, InventoryChannel, InventoryChannel> ItemTransferred;

        public InventoryManager(GameDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        /// <summary>Point the manager at the run it should mutate. Call on new run and after load.</summary>
        public void Bind(RunData run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            Debug.Log($"[InventoryManager] Bound to run '{run.runId}'.");
        }

        // ---- Mutations ----

        /// <summary>
        /// Adds an item to a channel, merging into a matching stack when possible.
        /// <paramref name="durability"/> defaults to the item's max; pass a value for field-found items.
        /// </summary>
        public ItemInstance AddItem(InventoryChannel channel, string itemDataId, int quantity = 1,
                                    int? durability = null, float contamination = 0f)
        {
            if (!Ready(nameof(AddItem))) return null;
            if (quantity <= 0)
            {
                Debug.LogWarning($"[InventoryManager] AddItem ignored: quantity {quantity} for '{itemDataId}'.");
                return null;
            }

            var data = _db.GetItem(itemDataId);
            if (data == null) return null; // GameDatabase already logged the miss

            int maxDur = Mathf.Max(0, data.durability);
            int durabilityValue = Mathf.Clamp(durability ?? maxDur, 0, maxDur);
            var list = Channel(channel);

            var stack = FindStack(list, itemDataId, durabilityValue, contamination);
            if (stack != null)
            {
                stack.quantity += quantity;
                Debug.Log($"[InventoryManager] Stacked +{quantity} '{itemDataId}' in {channel} (now {stack.quantity}).");
                ItemChanged?.Invoke(stack, channel);
                return stack;
            }

            var inst = new ItemInstance
            {
                itemDataId = itemDataId,
                currentDurability = durabilityValue,
                currentContamination = contamination,
                quantity = quantity
            };
            list.Add(inst);
            Debug.Log($"[InventoryManager] Added new stack '{itemDataId}' x{quantity} to {channel}.");
            ItemAdded?.Invoke(inst, channel);
            return inst;
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
                var landed = AddItem(InventoryChannel.Bunker, inst.itemDataId, inst.quantity,
                                     inst.currentDurability, inst.currentContamination);
                if (landed != null)
                    ItemTransferred?.Invoke(landed, InventoryChannel.Scavenged, InventoryChannel.Bunker);
            }
            Debug.Log($"[InventoryManager] Transferred {snapshot.Count} scavenged stack(s) into the bunker.");
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
            float total = 0f;
            foreach (var inst in Channel(channel))
            {
                var data = _db.GetItem(inst.itemDataId);
                if (data != null) total += data.weightKg * inst.quantity;
            }
            return total;
        }

        /// <summary>Read-only view of a channel's stacks. Never mutate the returned list directly.</summary>
        public IReadOnlyList<ItemInstance> Get(InventoryChannel channel)
            => Ready(nameof(Get)) ? Channel(channel) : Array.Empty<ItemInstance>();

        // ---- Internals ----

        private List<ItemInstance> Channel(InventoryChannel c)
            => c == InventoryChannel.Scavenged ? _run.ScavengedInventory : _run.BunkerInventory;

        private bool Ready(string op)
        {
            if (_run != null) return true;
            Debug.LogError($"[InventoryManager] {op} called before Bind(RunData). No-op.");
            return false;
        }

        private static ItemInstance FindStack(List<ItemInstance> list, string id, int durability, float contamination)
        {
            const float eps = 0.0001f;
            foreach (var inst in list)
            {
                if (inst.itemDataId == id
                    && inst.currentDurability == durability
                    && Mathf.Abs(inst.currentContamination - contamination) < eps)
                    return inst;
            }
            return null;
        }
    }
}
