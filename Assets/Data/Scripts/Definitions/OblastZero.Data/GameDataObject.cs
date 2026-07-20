// Assets/Data/Scripts/Definitions/GameDataObject.cs
using UnityEngine;

namespace OblastZero.Data
{
    /// <summary>
    /// Base type for every designer-authored data asset (items, crew, factions, anomalies, mutants,
    /// events, traits, voice groups). Unifies the identity fields so systems like GameDatabase can
    /// index any content by a single stable <see cref="id"/>.
    /// </summary>
    public abstract class GameDataObject : ScriptableObject
    {
        [Header("Core Identity")]
        [Tooltip("Stable string identifier. Used for save-game references, JSON cross-refs, and Steam stats. Never localize.")]
        public string id;

        [Tooltip("Display name shown to the player. Localize this.")]
        public string displayName;

        [TextArea(3, 6)]
        [Tooltip("Internal designer notes. Not shown to player. Use freely for lore context.")]
        public string designerNotes;
    }
}
