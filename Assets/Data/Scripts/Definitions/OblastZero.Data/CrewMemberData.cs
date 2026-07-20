// Assets/Data/Scripts/Definitions/CrewMemberData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum CrewBackground
    {
        LonerScavenger,
        ExCordonSoldier,
        ExSocietyClerk,
        FieldMedic,
        Mechanic,
        KafedraDefector,
        EcologistSurvivor
    }

    [System.Serializable]
    public struct CrewBaseStats
    {
        [Range(0, 100)] public int maxHealth;
        [Range(0, 100)] public int maxSanity;
        public float carryCapacityKg;
        [Range(0f, 2f)] public float sanityRecoveryMultiplier;
        [Range(0f, 2f)] public float radiationResistanceMultiplier;
        [Range(0f, 2f)] public float combatResolutionMultiplier;
    }

    [CreateAssetMenu(menuName = "OblastZero/Crew Member", fileName = "Crew_")]
    public class CrewMemberData : GameDataObject
    {
        [Header("Identity")]
        public string firstName;
        public string lastName;
        public string patronymic;
        public CrewBackground background;
        public Sprite portrait;

        [Header("Base Stats")]
        public CrewBaseStats baseStats;

        [Header("Starting Traits")]
        public List<TraitData> startingTraits;

        [Header("Voice / Personality")]
        public VoiceLineGroup voiceLineGroup;

        [Header("Backstory")]
        [TextArea(3, 10)] public string backstoryText;
    }
}
