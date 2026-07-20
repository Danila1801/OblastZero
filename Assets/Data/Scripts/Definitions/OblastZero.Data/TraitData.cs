// Assets/Data/Scripts/Definitions/TraitData.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Data
{
    /// <summary>
    /// Stat adjustments a trait applies to a crew member. All values use ADDITIVE-TO-ONE semantics:
    /// a value of 0 means "no change", +0.25 means +25%, -0.25 means -25%. A freshly-created struct is
    /// therefore neutral by default, which avoids the "blank struct zeroes the stat" footgun.
    /// </summary>
    [System.Serializable]
    public struct CrewStatModifiers
    {
        [Tooltip("Flat add to max health (can be negative).")]
        public int maxHealthDelta;

        [Tooltip("Flat add to max sanity (can be negative).")]
        public int maxSanityDelta;

        [Tooltip("Bonus to sanity recovery. 0 = no change, +0.25 = +25%.")]
        [Range(-1f, 1f)] public float sanityRecoveryBonus;

        [Tooltip("Bonus to radiation resistance. 0 = no change, +0.25 = +25% more resistant.")]
        [Range(-1f, 1f)] public float radiationResistanceBonus;

        [Tooltip("Bonus to combat resolution. 0 = no change, +0.25 = +25%.")]
        [Range(-1f, 1f)] public float combatResolutionBonus;

        [Tooltip("Bonus to carry capacity. 0 = no change, +0.25 = +25%.")]
        [Range(-1f, 1f)] public float carryCapacityBonus;
    }

    /// <summary>
    /// A virtue or affliction attached to a crew member (by id, on the crew instance). Carries display
    /// text, a virtue/affliction flag for UI and event matching, and the stat modifiers it applies.
    /// </summary>
    [CreateAssetMenu(menuName = "OblastZero/Trait", fileName = "Trait_")]
    public class TraitData : GameDataObject
    {
        [Header("Description")]
        [TextArea(2, 5)] public string description;

        [Header("Nature")]
        [Tooltip("False = virtue (beneficial). True = affliction (detrimental). Drives UI tinting and event matching.")]
        public bool isAffliction;

        [Header("Stat Modifiers")]
        public CrewStatModifiers modifiers;

        [Header("Event Hooks")]
        [Tooltip("Free-form tags the event engine matches against (e.g. 'claustrophobic', 'steady_hands').")]
        public List<string> behaviorTags = new();
    }
}
