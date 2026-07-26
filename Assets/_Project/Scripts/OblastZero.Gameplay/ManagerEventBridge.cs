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
        private FactionReputationManager _reputation;
        private EventEngine _events;
        private bool _connected;

        /// <summary>
        /// Wires the manager C# events onto the global EventBus. <paramref name="reputation"/> and
        /// <paramref name="events"/> are optional so lightweight test harnesses can bridge just inventory/crew.
        /// </summary>
        public void Connect(InventoryManager inventory, CrewManager crew,
                            FactionReputationManager reputation = null, EventEngine events = null)
        {
            if (_connected) Disconnect();

            _inventory = inventory;
            _crew = crew;
            _reputation = reputation;
            _events = events;

            _inventory.ItemAdded += OnItemAdded;
            _inventory.ItemRemoved += OnBunkerInventoryTouched;
            _inventory.ItemChanged += OnBunkerInventoryTouched;
            _inventory.ItemTransferred += OnItemTransferred;
            _inventory.ScavengeLoadChanged += OnScavengeLoadChanged;
            _inventory.ScavengePickupRejected += OnScavengePickupRejected;

            _crew.CrewAdded += OnCrewAdded;
            _crew.CrewStatsChanged += OnCrewStatsChanged;
            _crew.CrewDied += OnCrewDied;

            if (_reputation != null) _reputation.ReputationChanged += OnReputationChanged;
            if (_events != null)
            {
                _events.EventPresented += OnEventPresented;
                _events.EventResolved += OnEventResolved;
            }

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
            _inventory.ScavengeLoadChanged -= OnScavengeLoadChanged;
            _inventory.ScavengePickupRejected -= OnScavengePickupRejected;

            _crew.CrewAdded -= OnCrewAdded;
            _crew.CrewStatsChanged -= OnCrewStatsChanged;
            _crew.CrewDied -= OnCrewDied;

            if (_reputation != null) _reputation.ReputationChanged -= OnReputationChanged;
            if (_events != null)
            {
                _events.EventPresented -= OnEventPresented;
                _events.EventResolved -= OnEventResolved;
            }

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

        private void OnScavengeLoadChanged(float currentKg, float capacityKg)
            => EventBus.Raise(new ScavengeLoadChangedEvent { CurrentKg = currentKg, CapacityKg = capacityKg });

        private void OnScavengePickupRejected(string itemDataId, float itemKg, float currentKg, float capacityKg)
            => EventBus.Raise(new ScavengePickupRejectedEvent
            {
                ItemDataId = itemDataId,
                ItemWeightKg = itemKg,
                CurrentKg = currentKg,
                CapacityKg = capacityKg
            });

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

        // ---- Faction reputation ----

        private void OnReputationChanged(FactionId faction, int oldRep, int newRep)
            => EventBus.Raise(new FactionReputationChangedEvent
            {
                FactionId = faction.ToString(),
                OldRep = oldRep,
                NewRep = newRep
            });

        // ---- Event engine ----

        private void OnEventPresented(ExpeditionEventData evt)
            => EventBus.Raise(new EventPresentedEvent { EventId = evt != null ? evt.id : null });

        private void OnEventResolved(EventResolution res)
            => EventBus.Raise(new EventResolvedEvent
            {
                EventId = res.eventId,
                ChoiceIndex = res.choiceIndex,
                Success = res.success,
                ActingCrewInstanceId = res.actingCrewInstanceId,
                FollowUpEventId = res.followUpQueued
            });
    }
}
