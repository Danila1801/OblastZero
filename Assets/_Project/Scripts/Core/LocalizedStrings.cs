// Assets/_Project/Scripts/Core/LocalizedStrings.cs
using System;
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// Minimal localization registry. Content (events, items, crew) stores stable string KEYS
    /// (<c>titleKey</c>, <c>narrativeTextKey</c>, <c>choiceLabelKey</c>) rather than display text, so the same
    /// data can render in any language. UI resolves a key through <see cref="Get"/>; until a language table is
    /// loaded, <see cref="Get"/> returns the key itself so screens still show something meaningful.
    ///
    /// This is the single seam the future localization loader (and the JSON event loader, which can carry
    /// inline strings) populates via <see cref="Register"/> / <see cref="RegisterAll"/>. It is deliberately a
    /// plain static table — no per-frame lookups on the hot path; keys resolve in O(1).
    /// </summary>
    public static class LocalizedStrings
    {
        private static readonly Dictionary<string, string> _table = new();

        /// <summary>
        /// Number of keys currently registered. Zero means every <see cref="Get"/> call returns its key
        /// verbatim — the exact symptom of a language table that never loaded, so boot diagnostics assert
        /// on this rather than assuming the loader ran.
        /// </summary>
        public static int Count => _table.Count;

        /// <summary>
        /// Language code of the table currently loaded, or null before any load. Set by the loader in
        /// OblastZero.Services; read by diagnostics and by any future language-switch UI.
        /// </summary>
        public static string ActiveLanguageCode { get; set; }

        /// <summary>Registers (or overwrites) a single key → display-string mapping.</summary>
        public static void Register(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _table[key] = value ?? string.Empty;
        }

        /// <summary>Bulk-registers a table of key → display-string mappings (e.g. a loaded language file).</summary>
        public static void RegisterAll(IDictionary<string, string> entries)
        {
            if (entries == null) return;
            foreach (var kvp in entries) Register(kvp.Key, kvp.Value);
        }

        /// <summary>
        /// Raised after the active table has been swapped for a different language. Screens subscribe and
        /// rebuild their labels; nothing else in the game reads it. Static rather than an EventBus struct
        /// because the localization table is itself static and has no run scope — a screen that exists
        /// across a language change is the only thing that cares.
        /// </summary>
        public static event Action LanguageChanged;

        /// <summary>Resolves a key to its display string, or returns the key unchanged if none is registered.</summary>
        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            return _table.TryGetValue(key, out var value) ? value : key;
        }

        /// <summary>
        /// Resolves a key and formats it with <paramref name="args"/>.
        ///
        /// Formatting is guarded: a translator who drops a <c>{1}</c> or writes an unescaped brace produces a
        /// <see cref="FormatException"/> deep inside a HUD refresh, which in play reads as a frozen screen
        /// rather than as a bad string. On a format failure this logs the offending key and returns the raw
        /// template, so the screen keeps drawing and the console names the file to fix.
        /// <c>tools/localization_qa.py</c> is the gate that stops such a table reaching a build at all;
        /// this is the runtime backstop for the case where it does anyway.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            string template = Get(key);
            if (args == null || args.Length == 0) return template;

            try
            {
                return string.Format(template, args);
            }
            catch (FormatException ex)
            {
                UnityEngine.Debug.LogError(
                    $"[LocalizedStrings] Key '{key}' in language '{ActiveLanguageCode ?? "none"}' has a bad " +
                    $"format template (\"{template}\") for {args.Length} argument(s): {ex.Message}");
                return template;
            }
        }

        public static bool Has(string key) => !string.IsNullOrEmpty(key) && _table.ContainsKey(key);

        /// <summary>Empties the table and forgets the active language. The loader calls this before a swap.</summary>
        public static void Clear()
        {
            _table.Clear();
            ActiveLanguageCode = null;
        }

        /// <summary>
        /// Announces that a new table is live. The loader calls this once, after registering the new keys —
        /// never between <see cref="Clear"/> and <see cref="RegisterAll"/>, or subscribers would rebuild
        /// against an empty table and render every label as its raw key.
        /// </summary>
        public static void NotifyLanguageChanged() => LanguageChanged?.Invoke();
    }
}
