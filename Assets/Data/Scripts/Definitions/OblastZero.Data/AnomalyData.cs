// Assets/Data/Scripts/Definitions/AnomalyData.cs
using UnityEngine;
using System.Collections.Generic;

namespace OblastZero.Data
{
    public enum AnomalyHazardType
    {
        Cognitive,     // Interview, Editor-adjacent — mind-state hazards
        Temporal,      // Backlog, time-pooling
        Duplicative,   // Carbon Copy — produces erroneous duplicates
        Gravitational, // STALKER-style spatial hazards
        Thermal,
        Electrical,
        Chemical,
        Psionic
    }

    [System.Serializable]
    public struct DamageProfile
    {
        public float healthPerSecond;
        public float radiationPerSecond;
        public float sanityPerExposure;
        public bool causesPermanentTrait;
        public string permanentTraitId;
    }

    [System.Serializable]
    public struct WeightedItem
    {
        public ItemData item;
        [Range(0f, 1f)] public float dropChance;
        public int minQty;
        public int maxQty;
    }

    [CreateAssetMenu(menuName = "OblastZero/Anomaly", fileName = "Anomaly_")]
    public class AnomalyData : GameDataObject
    {
        [Header("Classification")]
        public string classificationCode; // "ANM-Δ-07/CC"
        public string fieldName;           // "The carbon", "the desk drawer"

        [Header("Hazard Profile")]
        public AnomalyHazardType primaryHazard;
        public DamageProfile damageProfile;
        public float effectiveRadiusMeters;
        public bool visibleToNakedEye;
        public bool detectableByGeiger;

        [Header("Drops / Artifacts")]
        public List<WeightedItem> artifactDropTable;

        [Header("Phase B (Expedition Log)")]
        [TextArea(2, 5)] public string expeditionEncounterTextKey; // localization key
        public ExpeditionEventData expeditionEvent;                // optional dedicated event trigger
    }
}
