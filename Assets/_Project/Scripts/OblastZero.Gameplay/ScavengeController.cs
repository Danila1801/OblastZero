// Assets/_Project/Scripts/Gameplay/ScavengeController.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// In-scene coordinator for the 3D Blowout. Listens to the player's pickup requests and routes them into
    /// the run-scoped managers (resolved from GameManager): items go to InventoryManager.AddItem on the
    /// Scavenged channel, crew go to CrewManager.AddRescued. On success the world object is destroyed —
    /// instant kinematic pickup, exactly as the locked design specifies (no physics carry).
    ///
    /// Lives in the 3D scavenge scene; the managers it talks to live on the persistent GameManager, so a run
    /// must be active (GameManager.BeginNewRun) before scavenging. Manager events flow on to the EventBus via
    /// the ManagerEventBridge automatically.
    /// </summary>
    public class ScavengeController : MonoBehaviour
    {
        [Tooltip("The scene's player. Auto-found if left empty.")]
        [SerializeField] private ScavengePlayerController player;

        private InventoryManager _inventory;
        private CrewManager _crew;

        private void Awake()
        {
            if (player == null) player = FindObjectOfType<ScavengePlayerController>();
        }

        private void OnEnable()
        {
            ResolveManagers();
            if (player != null) player.PickupRequested += OnPickupRequested;
            else Debug.LogWarning("[ScavengeController] No ScavengePlayerController found in the scene.");
        }

        private void OnDisable()
        {
            if (player != null) player.PickupRequested -= OnPickupRequested;
        }

        private void ResolveManagers()
        {
            var gm = GameManager.Instance;
            _inventory = gm != null ? gm.Inventory : null;
            _crew = gm != null ? gm.Crew : null;

            if (_inventory == null || _crew == null)
                Debug.LogWarning("[ScavengeController] Inventory/Crew managers unavailable. " +
                                 "Begin a run (GameManager.BeginNewRun) before scavenging.");
        }

        private void OnPickupRequested(ScavengePickup pickup)
        {
            if (pickup == null) return;
            if (_inventory == null || _crew == null) ResolveManagers();
            if (_inventory == null || _crew == null) return;

            bool collected = false;

            switch (pickup.Kind)
            {
                case ScavengePickup.PickupKind.Item:
                    int? durability = pickup.DurabilityOverride < 0 ? (int?)null : pickup.DurabilityOverride;
                    var added = _inventory.AddItem(InventoryChannel.Scavenged, pickup.DataId,
                                                   pickup.Quantity, durability, pickup.Contamination);
                    collected = added != null;
                    break;

                case ScavengePickup.PickupKind.Crew:
                    var rescued = _crew.AddRescued(pickup.DataId);
                    collected = rescued != null;
                    break;
            }

            if (!collected) return; // invalid id — GameDatabase logged it; leave the object in the world

            Debug.Log($"[ScavengeController] Collected {pickup.Kind} '{pickup.DataId}'. Removing from world.");
            Destroy(pickup.gameObject);
        }
    }
}
