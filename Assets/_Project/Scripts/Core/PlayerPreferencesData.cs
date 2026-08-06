// Assets/_Project/Scripts/Core/PlayerPreferencesData.cs
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace OblastZero.Core
{
    /// <summary>
    /// Device-local player preferences: volumes, display settings, key bindings, language.
    ///
    /// <para>These live in a THIRD save channel, alongside the run channel (RunData) and the progression
    /// channel (MetaProgressData) — not inside either. The reason is what each channel means. RunData dies
    /// with the run. MetaProgressData is progression the player earned, and is the channel Steam Cloud
    /// syncs across machines. A monitor resolution, a key binding and a master volume are properties of the
    /// MACHINE, not of the player's progress: syncing them would push a 4K borderless setup onto a laptop
    /// and a rebind made on a keyboard onto a machine using a pad. Bible §6 names the bifurcation between
    /// run and progression; nothing in it says preferences belong to either.</para>
    ///
    /// <para>Serialized by Newtonsoft (never JsonUtility — it drops the Dictionary below outright), so the
    /// binding map round-trips as readable key names.</para>
    /// </summary>
    [Serializable]
    public class PlayerPreferencesData
    {
        // ─── Audio ──────────────────────────────────────────────────────────────
        // Stored 0..100 rather than 0..1 so the file reads the way the sliders do, and so a hand-edited
        // "80" cannot be mistaken for "silent, off by two orders of magnitude".

        public int volumeMaster = DefaultVolumeMaster;
        public int volumeSfx = DefaultVolumeSfx;
        public int volumeMusic = DefaultVolumeMusic;
        public int volumeAmbient = DefaultVolumeAmbient;

        public const int DefaultVolumeMaster = 80;
        public const int DefaultVolumeSfx = 80;
        public const int DefaultVolumeMusic = 70;
        public const int DefaultVolumeAmbient = 60;

        // ─── Display ────────────────────────────────────────────────────────────

        /// <summary>Index into <c>QualitySettings.names</c>. -1 means "whatever the project shipped with".</summary>
        public int qualityLevel = -1;

        public int resolutionWidth;
        public int resolutionHeight;
        public int refreshRateHz;

        /// <summary>Matches <c>UnityEngine.FullScreenMode</c>. Borderless (1) is the safe default.</summary>
        public int fullScreenMode = 1;

        public bool vSync = true;

        // ─── Language ───────────────────────────────────────────────────────────

        /// <summary>Language code of the loaded table. Validated against the shipped tables on load.</summary>
        public string languageCode = "en";

        // ─── Controls ───────────────────────────────────────────────────────────

        /// <summary>
        /// Action name → <see cref="Key"/> name. Stored as strings on both sides so the file is legible and
        /// so an enum reorder in a future Unity cannot silently repoint a binding at a different key.
        /// Missing or unparseable entries fall back to the action's default via
        /// <see cref="InputBindingTable.ParseOrDefault"/>.
        /// </summary>
        public Dictionary<string, string> keyBindings = new Dictionary<string, string>();

        // ─── Accessors ──────────────────────────────────────────────────────────

        /// <summary>The bound key for an action, or its default when nothing valid is stored.</summary>
        public Key GetBinding(OblastAction action)
        {
            string stored = null;
            keyBindings?.TryGetValue(action.ToString(), out stored);
            return InputBindingTable.ParseOrDefault(stored, action);
        }

        /// <summary>
        /// Assigns a key to an action. Returns the action that previously held the key, or null when the
        /// key was free — the options screen reports the displacement rather than silently leaving two
        /// actions on one key, which in play reads as an input bug.
        /// </summary>
        public OblastAction? SetBinding(OblastAction action, Key key)
        {
            if (keyBindings == null) keyBindings = new Dictionary<string, string>();

            OblastAction? displaced = null;
            foreach (var other in InputBindingTable.AllActions)
            {
                if (other == action) continue;
                if (GetBinding(other) != key) continue;

                displaced = other;
                // The displaced action loses the key rather than sharing it. Sharing would mean one press
                // firing two actions, which is never what a player rebinding a key intends.
                keyBindings[other.ToString()] = Key.None.ToString();
                break;
            }

            keyBindings[action.ToString()] = key.ToString();
            return displaced;
        }

        /// <summary>Restores standard-issue bindings, leaving audio/display/language alone.</summary>
        public void ResetBindingsToDefaults()
        {
            keyBindings = new Dictionary<string, string>();
            foreach (var action in InputBindingTable.AllActions)
                keyBindings[action.ToString()] = InputBindingTable.DefaultFor(action).ToString();
        }

        /// <summary>A preferences object with every field at its shipped default.</summary>
        public static PlayerPreferencesData CreateDefaults()
        {
            var prefs = new PlayerPreferencesData();
            prefs.ResetBindingsToDefaults();
            return prefs;
        }
    }
}
