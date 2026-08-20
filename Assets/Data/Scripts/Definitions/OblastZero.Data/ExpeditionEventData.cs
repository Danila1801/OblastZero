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

        /// <summary>
        /// Proximity locales this event can fire at (bunker_interior, perimeter, old_factory, …). Matched
        /// against the caller's active locales and <b>fails closed</b>: an event that names locales is
        /// rejected outright when the caller supplies none. See <c>OblastZero.Core.RegionTags</c>.
        /// </summary>
        public List<string> regionTagsAny;

        /// <summary>
        /// Canonical oblast regions this event belongs to (outer_cordon, census_district, …). A second,
        /// orthogonal axis to <see cref="regionTagsAny"/>: that one says how far from the bunker door, this
        /// one says which district of the oblast. See <c>OblastZero.Core.OblastRegions</c>.
        ///
        /// <para><b>Empty means "anywhere", and this gate fails open</b> — an event that names regions still
        /// passes when the caller supplies none. That asymmetry with <see cref="regionTagsAny"/> is
        /// deliberate: it makes this axis able to narrow a pool but never to empty one, so region authoring
        /// cannot reproduce the content blackout that a fail-closed locale gate can.</para>
        /// </summary>
        public List<string> oblastRegionsAny;
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
