// Assets/_Project/Scripts/Gameplay/ScavengePickup.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Attach to any world object the player can grab during the 3D scavenge. Holds the data id and
    /// condition the pickup represents; the player's interaction raycast finds this component and the
    /// ScavengeController routes it into the run. Instant kinematic — on collection the object is removed
    /// from the world and the data is written straight into RunData.ScavengedInventory / RescuedCrew.
    /// </summary>
    public class ScavengePickup : MonoBehaviour
    {
        public enum PickupKind { Item, Crew }

        [SerializeField] private PickupKind kind = PickupKind.Item;

        [Tooltip("ItemData id (for Item) or CrewMemberData id (for Crew). Must exist in the GameDatabase.")]
        [SerializeField] private string dataId;

        [Tooltip("How many units this pickup grants (items only).")]
        [SerializeField] private int quantity = 1;

        [Tooltip("Item durability when picked up. -1 = use the item's max durability.")]
        [SerializeField] private int durabilityOverride = -1;

        [Tooltip("Radiation contamination on this instance (items only).")]
        [SerializeField, Range(0f, 100f)] private float contamination = 0f;

        public PickupKind Kind => kind;
        public string DataId => dataId;
        public int Quantity => Mathf.Max(1, quantity);
        public int DurabilityOverride => durabilityOverride;
        public float Contamination => contamination;

        /// <summary>Verb shown in the HUD interaction prompt ("Take" for items, "Rescue" for crew).</summary>
        public string InteractionVerb => kind == PickupKind.Crew ? "Rescue" : "Take";
    }
}
