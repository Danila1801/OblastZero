// Assets/Data/Scripts/Definitions/FactionData.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Data
{
    public enum FactionId
    {
        None = 0,
        ScaleSociety = 10,
        Cordon = 20,
        Kafedra = 30,
        Loners = 40,
        Bandits = 50
    }

    [System.Serializable]
    public struct FactionRelation
    {
        public FactionId other;
        [Range(-100, 100)] public int defaultStanding;
    }

    [System.Serializable]
    public struct ReputationThreshold
    {
        public string thresholdName; // "Hunted", "Hostile", "Neutral", "Allied", "Endgame"
        public int minReputation;
        public int maxReputation;
    }

    [CreateAssetMenu(menuName = "OblastZero/Faction", fileName = "Faction_")]
    public class FactionData : GameDataObject
    {
        [Header("Identity")]
        public FactionId factionId;
        public Color factionColor;
        public Sprite factionEmblem;

        [Header("Ideology Tags")]
        [Tooltip("Free-form tags used by the event engine to match faction-flavored events.")]
        public List<string> ideologyTags; // e.g. "bureaucratic", "demographic", "actuarial"

        [Header("Inter-Faction Relations")]
        public List<FactionRelation> baseRelations;

        [Header("Reputation Bands")]
        public List<ReputationThreshold> thresholds;

        [Header("Voice / Flavor")]
        public VoiceLineGroup radioChatter;
        public VoiceLineGroup combatBarks;

        [Header("Signature Equipment")]
        public List<ItemData> signatureEquipment;

        [Header("Endgame")]
        public string endgameBranchId; // referenced by the event engine to gate the faction-specific ending
    }
}
