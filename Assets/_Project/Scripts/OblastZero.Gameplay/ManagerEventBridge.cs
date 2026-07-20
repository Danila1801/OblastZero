// Assets/_Project/Scripts/Gameplay/ManagerEventBridge.cs
using OblastZero.Core;
using OblastZero.Data;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Translates the gameplay managers' decoupled C# events into the global, struct-based
    /// <see cref="EventBus"/> so UI and other systems subscribe in one place without ever referencing the
    /// managers directly. The managers stay pure and unit-testable; this adapter is the single seam where
    /// local events become global events.
    ///
    /// Lifecycle: construct once at bootstrap after the managers exist, call <see cref="Connect"/>; call
    /// <see cref="Disconnect"/> on run-end / shutdown so subscriptions don't leak across runs.
    /// </summary>
    public class ManagerEventBridge
    {
        private InventoryManager _inventory;
        private CrewManager _crew;
        private bool _connected;

        public void Connect(InventoryManager inventory, CrewManager crew)
        {
            if (_connected) Disconnect();

            _inventory = inventory;
            _crew = crew;

            _inventory.ItemAdded += OnItemAdded;
            _inventory.ItemRemoved += OnBunkerInventoryTouched;
            _inventory.ItemChanged += OnBunkerInventoryTouched;
            _inventory.ItemTransferred += OnItemTransferred;

            _crew.CrewAdded += OnCrewAdded;
            _crew.CrewStatsChanged += OnCrewStatsChanged;
            _crew.CrewDied += OnCrewDied;

            _connected = true;
            Debug.Log("[ManagerEventBridge] Connected manager events to EventBus.");
        }

        public void Disconnect()
        {
            if (!_connected) return;

            _inventory.ItemAdded -= OnItemAdded;
            _inventory.ItemRemoved -= OnBunkerInventoryTouched;
            _inventory.ItemChanged -= OnBunkerInventoryTouched;
            _inventory.ItemTransferred -= OnItemTransferred;

            _crew.CrewAdded -= OnCrewAdded;
            _crew.CrewStatsChanged -= OnCrewStatsChanged;
            _crew.CrewDied -= OnCrewDied;

            _connected = false;
            Debug.Log("[ManagerEventBridge] Disconnected manager events from EventBus.");
        }

        // ---- Inventory ----

        private void OnItemAdded(ItemInstance inst, InventoryChannel channel)
        {
            if (channel == InventoryChannel.Scavenged)
                EventBus.Raise(new ItemPickedUpEvent { ItemDataId = inst.itemDataId });
            else
                EventBus.Raise(new BunkerInventoryChangedEvent { ItemDataId = inst.itemDataId });
        }

        private void OnBunkerInventoryTouched(ItemInstance inst, InventoryChannel channel)
        {
            if (channel == InventoryChannel.Bunker)
                EventBus.Raise(new BunkerInventoryChangedEvent { ItemDataId = inst.itemDataId });
        }

        private void OnItemTransferred(ItemInstance inst, InventoryChannel from, InventoryChannel to)
        {
            if (to == InventoryChannel.Bunker)
                EventBus.Raise(new BunkerInventoryChangedEvent { ItemDataId = inst.itemDataId });
        }

        // ---- Crew ----

        private void OnCrewAdded(CrewInstance c)
            => EventBus.Raise(new CrewRescuedEvent { CrewDataId = c.crewDataId });

        private void OnCrewStatsChanged(CrewInstance c, CrewStat stat, int oldValue, int newValue)
            => EventBus.Raise(new CrewStatChangedEvent
            {
                CrewInstanceId = c.instanceId,
                StatName = stat.ToString(),
                OldValue = oldValue,
                NewValue = newValue
            });

        private void OnCrewDied(CrewInstance c)
            => EventBus.Raise(new CrewDiedEvent { CrewInstanceId = c.instanceId, CrewDataId = c.crewDataId });
    }
}
