// Assets/Data/Scripts/Definitions/ItemData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum ItemCategory
    {
        Food,
        Water,
        Medical,
        Weapon,
        Ammunition,
        Tool,
        Document,
        Artifact,
        Crafting,
        Special
    }

    public enum UtilityTag
    {
        Eat,
        Drink,
        Heal,
        Repair,
        Fight,
        Trade,
        Read,
        Decontaminate,
        Defend,
        Ritual
    }

    [CreateAssetMenu(menuName = "OblastZero/Item", fileName = "Item_")]
    public class ItemData : GameDataObject
    {
        [Header("Basic")]
        public ItemCategory category;
        public Sprite icon;
        public GameObject worldPrefab; // for Phase A pickup

        [Tooltip("Optional override for how this item renders in the 3D scavenge. Leave on Auto — the " +
                 "usual case — and VisualArchetypeMapping derives the shape from category and id. Set it " +
                 "only when an item's category misrepresents its silhouette. Not read by ItemJsonLoader " +
                 "yet, so today this is an authored-asset override.")]
        public VisualArchetype visualArchetype = VisualArchetype.Auto;

        [Header("Physical")]
        public float weightKg;
        [Range(0, 100)] public int durability;
        public float decayPerDay;

        [Header("Multi-Utility (60 Seconds! DNA)")]
        [Tooltip("Tags describing every use this item supports. An axe might have [Repair, Fight, Ritual].")]
        public List<UtilityTag> utilityTags;

        [Header("Hazard")]
        public bool radiationContaminated;
        public float radiationContaminationLevel;

        [Header("Trade Values")]
        public int baseTradeValueScale;
        public int baseTradeValueCordon;
        public int baseTradeValueKafedra;
    }
}
