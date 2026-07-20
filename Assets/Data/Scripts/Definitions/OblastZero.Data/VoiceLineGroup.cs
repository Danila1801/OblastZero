// Assets/Data/Scripts/Definitions/VoiceLineGroup.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Data
{
    /// <summary>A single voiced line: an audio clip plus an optional localization key for its subtitle.</summary>
    [System.Serializable]
    public struct VoiceLine
    {
        public AudioClip clip;

        [Tooltip("Localization key for the subtitle. Optional.")]
        public string subtitleKey;
    }

    /// <summary>
    /// A named collection of voice lines (e.g. a faction's radio chatter, a crew member's bark set).
    /// Referenced by FactionData, CrewMemberData, and MutantData via direct asset reference.
    /// </summary>
    [CreateAssetMenu(menuName = "OblastZero/Voice Line Group", fileName = "Voice_")]
    public class VoiceLineGroup : GameDataObject
    {
        [Header("Lines")]
        public List<VoiceLine> lines = new();

        public bool HasLines => lines != null && lines.Count > 0;

        /// <summary>Returns a random line, or default if the group is empty.</summary>
        public VoiceLine GetRandom()
        {
            if (!HasLines) return default;
            return lines[Random.Range(0, lines.Count)];
        }
    }
}
