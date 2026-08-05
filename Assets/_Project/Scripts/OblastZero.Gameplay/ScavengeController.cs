// Assets/_Project/Scripts/Gameplay/ScavengeController.cs
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// In-scene coordinator for the 3D Blowout. Listens to the player's pickup requests and routes them into
    /// the run-scoped managers (resolved from GameManager): items go to InventoryManager.AddItem on the
    /// Scavenged channel, crew go to CrewManager.AddRescued. On success the world object is retired —
    /// instant kinematic pickup, exactly as the locked design specifies (no physics carry).
    ///
    /// Lives in the 3D scavenge scene; the managers it talks to live on the persistent GameManager, so a run
    /// must be active (GameManager.BeginNewRun) before scavenging. Manager events flow on to the EventBus via
    /// the ManagerEventBridge automatically.
    ///
    /// <para><b>Feedback.</b> This is also where a successful grab gets its sound and its pop, because this
    /// is the only place that knows all three things they need: the resolved <see cref="ItemData"/>, hence
    /// the archetype and therefore which of the six pickup sounds and which burst colour; the world position,
    /// so the sound is spatial; and whether the managers actually accepted it. Refusals are not handled here —
    /// they arrive as <c>ScavengePickupRejectedEvent</c>, which <see cref="AudioManager"/> already observes —
    /// except for the world-object wobble, which needs the object reference only this class holds.</para>
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
            var archetype = VisualArchetype.Crate;

            switch (pickup.Kind)
            {
                case ScavengePickup.PickupKind.Item:
                    int? durability = pickup.DurabilityOverride < 0 ? (int?)null : pickup.DurabilityOverride;
                    var added = _inventory.AddItem(InventoryChannel.Scavenged, pickup.DataId,
                                                   pickup.Quantity, durability, pickup.Contamination);
                    collected = added != null;
                    archetype = ResolveArchetype(pickup.DataId);
                    break;

                case ScavengePickup.PickupKind.Crew:
                    var rescued = _crew.AddRescued(pickup.DataId);
                    collected = rescued != null;
                    archetype = VisualArchetype.Crew;
                    break;
            }

            if (!collected)
            {
                // Two different failures land here and they deserve different feedback. An over-cap
                // refusal is a legitimate game state the player has to feel, so the object wobbles and
                // the HUD says why (via ScavengePickupRejectedEvent, raised by InventoryManager). An
                // unresolvable data id is a content bug: GameDatabase has already logged it, and adding
                // a wobble there would dress a broken pickup up as a working one.
                if (pickup.Kind == ScavengePickup.PickupKind.Item && IsKnownItem(pickup.DataId))
                    PickupVfx.PlayRefusal(pickup.gameObject);
                return;
            }

            Debug.Log($"[ScavengeController] Collected {pickup.Kind} '{pickup.DataId}'. Removing from world.");

            // Sound is spatial and archetype-specific: the player needs to be able to tell a can from a
            // crate without looking at the HUD list, because they are already looking at the clock.
            //
            // Items only. A crew rescue also raises CrewRescuedEvent, which AudioManager observes, and
            // that path covers a rescue resolved by a bunker event as well — playing it from both places
            // would double the sound here and leave it missing there.
            if (pickup.Kind == ScavengePickup.PickupKind.Item)
                AudioManager.Play3D(CueFor(archetype), pickup.transform.position);

            // PickupVfx takes ownership of the destroy. The item is already committed to RunData at this
            // point, so a scene teardown mid-animation cannot cost the player the pickup.
            PickupVfx.Play(pickup.gameObject, archetype);
        }

        private VisualArchetype ResolveArchetype(string itemDataId)
        {
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            var data = db != null ? db.GetItem(itemDataId) : null;
            return data != null ? VisualArchetypeMapping.Resolve(data) : VisualArchetype.Crate;
        }

        private bool IsKnownItem(string itemDataId)
        {
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            if (db == null) return false;
            ItemData ignored;
            return db.TryGetItem(itemDataId, out ignored);
        }

        /// <summary>
        /// Maps a silhouette class onto one of the six pickup sounds. Deliberately many-to-one: eleven
        /// distinct grab sounds would be eleven things to learn in sixty seconds, whereas the shipped
        /// six — thud, clink, rustle, clack, shimmer, and the crew tone — separate along the lines the
        /// player is actually deciding on.
        /// </summary>
        private static string CueFor(VisualArchetype archetype)
        {
            switch (archetype)
            {
                case VisualArchetype.MetalCan:
                case VisualArchetype.AmmunitionBox:
                case VisualArchetype.Medical:
                    return AudioManager.CUE_PICKUP_METAL;

                case VisualArchetype.Document:
                case VisualArchetype.Clothing:
                    return AudioManager.CUE_PICKUP_PAPER;

                case VisualArchetype.WeaponSidearm:
                case VisualArchetype.WeaponLong:
                case VisualArchetype.Tool:
                    return AudioManager.CUE_PICKUP_WEAPON;

                case VisualArchetype.Artifact:
                    return AudioManager.CUE_PICKUP_ARTIFACT;

                case VisualArchetype.Crew:
                    return AudioManager.CUE_PICKUP_CREW;

                default:
                    return AudioManager.CUE_PICKUP_CRATE;
            }
        }
    }
}
