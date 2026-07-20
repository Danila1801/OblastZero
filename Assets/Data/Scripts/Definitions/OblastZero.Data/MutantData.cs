// Assets/Data/Scripts/Definitions/MutantData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum MutantBehaviorType
    {
        AmbushPredator,
        SlowStalker,    // Drowned Census-Taker
        PsychicHazard,  // Editor
        SwarmHarasser,
        BurrowingAttacker,
        SpecialEncounter
    }

    [System.Serializable]
    public struct HealthProfile
    {
        public int maxHealth;
        public int armorPiercingThreshold;
        public bool immuneToConventionalFirearms;
        public bool requiresArtifactToKill;
    }

    [CreateAssetMenu(menuName = "OblastZero/Mutant", fileName = "Mutant_")]
    public class MutantData : GameDataObject
    {
        [Header("Classification")]
        public string classificationCode; // "MTN-Β-04/DC"
        public string fieldName;

        [Header("Behavior")]
        public MutantBehaviorType behavior;
        public HealthProfile health;
        public float moveSpeed;
        public float sightRangeMeters;
        public float aggroRangeMeters;

        [Header("Hazards")]
        public DamageProfile contactDamage;
        public int fearFactor; // sanity drain on visual encounter, 0–100

        [Header("Loot")]
        public List<WeightedItem> lootTable;

        [Header("Phase B")]
        [TextArea(2, 5)] public string expeditionEncounterTextKey;
        public ExpeditionEventData expeditionEvent;
    }
}
