// Assets/_Project/Scripts/Services/LocalizationJsonLoader.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using OblastZero.Core;

namespace OblastZero.Services
{
    /// <summary>
    /// Loads a language table from Resources/Locale/localization_&lt;code&gt;.json into
    /// <see cref="LocalizedStrings"/>. Mirrors the ItemJsonLoader / EventJsonLoader pattern.
    ///
    /// The table is a flat JSON object of key → display string. Keys prefixed with an underscore are
    /// file metadata (e.g. <c>_comment</c>) and are not registered, so they can never leak into the UI.
    ///
    /// Until this runs, <see cref="LocalizedStrings.Get"/> returns the key unchanged — which is why the
    /// HUD and event modal rendered raw keys like <c>menu_main_new_run</c>. Nothing called into the
    /// localization table before; <c>GameManager.InitializeDataLayer</c> is now that caller.
    /// </summary>
    public static class LocalizationJsonLoader
    {
        /// <summary>Language shipped in the Early Access build. RU follows via localization_ru.json.</summary>
        public const string DefaultLanguageCode = "en";

        private const string LocaleResourceFolder = "Locale";
        private const string LocaleFilePrefix = "localization_";

        /// <summary>Keys beginning with this are file metadata, not display strings.</summary>
        private const char MetadataKeyPrefix = '_';

        /// <summary>
        /// Replaces the live language table with the requested one. Returns the number of display keys
        /// registered — 0 means the table is missing or empty and every key will render raw.
        /// </summary>
        public static int LoadLanguage(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
            {
                Debug.LogError("[LocalizationJsonLoader] Empty language code. Keeping the current table.");
                return LocalizedStrings.Count;
            }

            string resourcePath = LocaleResourceFolder + "/" + LocaleFilePrefix + languageCode;
            var asset = Resources.Load<TextAsset>(resourcePath);

            if (asset == null)
            {
                Debug.LogError($"[LocalizationJsonLoader] No language table at Resources/{resourcePath}.json. " +
                               "Keys will render raw. Keeping the current table.");
                return LocalizedStrings.Count;
            }

            Dictionary<string, string> entries;
            try
            {
                entries = ParseTable(asset.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalizationJsonLoader] Resources/{resourcePath}.json failed to parse: {ex.Message}. " +
                               "Keeping the current table.");
                return LocalizedStrings.Count;
            }

            // Swap, don't merge: a language switch must not leave the previous language's keys behind.
            LocalizedStrings.Clear();
            LocalizedStrings.RegisterAll(entries);
            LocalizedStrings.ActiveLanguageCode = languageCode;

            int count = LocalizedStrings.Count;
            Debug.Log($"[LocalizationJsonLoader] Loaded {count} keys for '{languageCode}' from Resources/{resourcePath}.json.");

            if (count == 0)
            {
                Debug.LogError($"[LocalizationJsonLoader] Resources/{resourcePath}.json registered no keys. " +
                               "Every localized string will render as its raw key.");
            }

            return count;
        }

        /// <summary>
        /// Flattens the language JSON to key → string. Non-string values are rejected rather than
        /// coerced: a nested object stringifies to something like <c>{"a":1}</c>, which would render
        /// in the UI as if it were copy.
        /// </summary>
        private static Dictionary<string, string> ParseTable(string jsonText)
        {
            var root = JObject.Parse(jsonText);
            var entries = new Dictionary<string, string>(root.Count);

            foreach (var property in root.Properties())
            {
                if (string.IsNullOrEmpty(property.Name)) continue;
                if (property.Name[0] == MetadataKeyPrefix) continue;

                if (property.Value == null || property.Value.Type != JTokenType.String)
                {
                    Debug.LogWarning($"[LocalizationJsonLoader] Key '{property.Name}' is " +
                                     $"{property.Value?.Type.ToString() ?? "null"}, not a string. Skipped.");
                    continue;
                }

                entries[property.Name] = property.Value.ToString();
            }

            return entries;
        }
    }
}
