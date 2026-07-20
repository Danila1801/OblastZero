// Assets/Data/Scripts/Definitions/ExpeditionEventData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    [System.Serializable]
    public struct EventPrerequisite
    {
        public int minDay;
        public int maxDay;
        public FactionId factionContext;
        [Range(-100, 100)] public int minFactionRep;
        [Range(-100, 100)] public int maxFactionRep;
        public List<string> requiredCrewTraitIds;
        public List<ItemData> requiredItemsAny;
        public List<string> regionTagsAny;
    }

    [System.Serializable]
    public struct OutcomeDelta
    {
        public int sanityDelta;
        public int fatigueDelta;
        public int radiationDelta;
        public int healthDelta;
        public List<WeightedItem> lootGained;
        public List<ItemData> itemsLost;
        public FactionId reputationFaction;
        public int reputationDelta;
        public string followUpEventId;
        [Range(0f, 1f)] public float crewDeathChance;
    }

    [System.Serializable]
    public struct EventChoice
    {
        public string choiceLabelKey; // localization key
        public List<string> requiredTraitsAny;
        public List<string> blockedByTraits;
        public OutcomeDelta successOutcome;
        public OutcomeDelta failureOutcome;
        [Range(0f, 1f)] public float successChance;
        public string successChanceFormula; // optional: formula evaluated against crew stats at runtime
    }

    [CreateAssetMenu(menuName = "OblastZero/Expedition Event", fileName = "Event_")]
    public class ExpeditionEventData : GameDataObject
    {
        [Header("Narrative")]
        public string titleKey;
        [TextArea(4, 10)] public string narrativeTextKey;

        [Header("Trigger Conditions")]
        public EventPrerequisite prerequisites;
        [Range(0f, 1f)] public float baseWeight;

        [Header("Branches")]
        public List<EventChoice> choices;

        [Header("Source")]
        public string sourceJsonPath; // if loaded from JSON at runtime
    }
}
