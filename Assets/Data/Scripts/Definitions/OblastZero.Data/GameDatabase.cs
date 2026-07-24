// Assets/Data/Scripts/Definitions/GameDatabase.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Services;

namespace OblastZero.Data
{
    /// <summary>
    /// The single content registry. Holds every authored data asset and indexes it by stable <c>id</c>
    /// into dictionaries built once at startup, so runtime lookups are O(1) and there are no per-frame
    /// Resources.Load calls. This is what lets Phase 2 scale to 500+ items and 1000+ events cleanly.
    ///
    /// Create one asset (Create → OblastZero/System/Game Database), then either assign the lists by hand
    /// or use the "Rebuild From Project" context-menu to auto-scan every matching asset in the project.
    /// </summary>
    [CreateAssetMenu(menuName = "OblastZero/System/Game Database", fileName = "GameDatabase")]
    public class GameDatabase : ScriptableObject
    {
        [Header("Content (assign by hand, or auto-populate via the context menu)")]
        public List<ItemData> items = new();
        public List<CrewMemberData> crew = new();
        public List<TraitData> traits = new();
        public List<VoiceLineGroup> voiceGroups = new();
        public List<FactionData> factions = new();
        public List<AnomalyData> anomalies = new();
        public List<MutantData> mutants = new();
        public List<ExpeditionEventData> events = new();

        private Dictionary<string, ItemData> _itemsById;
        private Dictionary<string, CrewMemberData> _crewById;
        private Dictionary<string, TraitData> _traitsById;
        private Dictionary<string, VoiceLineGroup> _voiceById;
        private Dictionary<string, FactionData> _factionsById;
        private Dictionary<FactionId, FactionData> _factionsByEnum;
        private Dictionary<string, AnomalyData> _anomaliesById;
        private Dictionary<string, MutantData> _mutantsById;
        private Dictionary<string, ExpeditionEventData> _eventsById;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        /// <summary>Builds all id indexes. Call once at bootstrap. Idempotent unless <paramref name="force"/>.</summary>
        public void Initialize(bool force = false)
        {
            if (_initialized && !force) return;

            _itemsById = BuildIndex(items, "ItemData");
            _crewById = BuildIndex(crew, "CrewMemberData");
            _traitsById = BuildIndex(traits, "TraitData");
            _voiceById = BuildIndex(voiceGroups, "VoiceLineGroup");
            _factionsById = BuildIndex(factions, "FactionData");
            _anomaliesById = BuildIndex(anomalies, "AnomalyData");
            _mutantsById = BuildIndex(mutants, "MutantData");

            // Load JSON events and merge with authored events.
            var jsonEvents = EventJsonLoader.LoadEventsFromResources(this);
            if (jsonEvents != null && jsonEvents.Count > 0)
            {
                events = new List<ExpeditionEventData>(events ?? new List<ExpeditionEventData>());
                events.AddRange(jsonEvents);
            }

            _eventsById = BuildIndex(events, "ExpeditionEventData");

            // Secondary index: factions are also reached by their enum id (reputation is enum-driven).
            _factionsByEnum = new Dictionary<FactionId, FactionData>(factions?.Count ?? 0);
            if (factions != null)
            {
                foreach (var faction in factions)
                {
                    if (faction == null) continue;
                    if (_factionsByEnum.ContainsKey(faction.factionId))
                    {
                        Debug.LogError($"[GameDatabase] Duplicate FactionId '{faction.factionId}' on '{faction.name}'; first one wins.");
                        continue;
                    }
                    _factionsByEnum[faction.factionId] = faction;
                }
            }

            _initialized = true;
            Debug.Log($"[GameDatabase] Initialized: {_itemsById.Count} items, {_crewById.Count} crew, " +
                      $"{_traitsById.Count} traits, {_voiceById.Count} voice groups, {_factionsById.Count} factions, " +
                      $"{_anomaliesById.Count} anomalies, {_mutantsById.Count} mutants, {_eventsById.Count} events.");
        }

        private Dictionary<string, T> BuildIndex<T>(List<T> source, string label) where T : GameDataObject
        {
            var dict = new Dictionary<string, T>(source?.Count ?? 0);
            if (source == null) return dict;
            foreach (var entry in source)
            {
                if (entry == null)
                {
                    Debug.LogWarning($"[GameDatabase] Null {label} entry skipped.");
                    continue;
                }
                if (string.IsNullOrEmpty(entry.id))
                {
                    Debug.LogError($"[GameDatabase] {label} asset '{entry.name}' has an empty id; skipped.");
                    continue;
                }
                if (dict.ContainsKey(entry.id))
                {
                    Debug.LogError($"[GameDatabase] Duplicate {label} id '{entry.id}' on '{entry.name}'; first one wins.");
                    continue;
                }
                dict[entry.id] = entry;
            }
            return dict;
        }

        private void EnsureInit()
        {
            if (!_initialized) Initialize();
        }

        // ---- Lookups ----

        public ItemData GetItem(string id) { EnsureInit(); return Lookup(_itemsById, id, "ItemData"); }
        public bool TryGetItem(string id, out ItemData data) { EnsureInit(); return _itemsById.TryGetValue(id ?? string.Empty, out data); }

        public CrewMemberData GetCrew(string id) { EnsureInit(); return Lookup(_crewById, id, "CrewMemberData"); }
        public bool TryGetCrew(string id, out CrewMemberData data) { EnsureInit(); return _crewById.TryGetValue(id ?? string.Empty, out data); }

        public TraitData GetTrait(string id) { EnsureInit(); return Lookup(_traitsById, id, "TraitData"); }
        public VoiceLineGroup GetVoiceGroup(string id) { EnsureInit(); return Lookup(_voiceById, id, "VoiceLineGroup"); }

        public FactionData GetFaction(string id) { EnsureInit(); return Lookup(_factionsById, id, "FactionData"); }
        public FactionData GetFaction(FactionId factionId)
        {
            EnsureInit();
            if (_factionsByEnum.TryGetValue(factionId, out var value)) return value;
            Debug.LogWarning($"[GameDatabase] No FactionData found for FactionId '{factionId}'.");
            return null;
        }

        public AnomalyData GetAnomaly(string id) { EnsureInit(); return Lookup(_anomaliesById, id, "AnomalyData"); }
        public MutantData GetMutant(string id) { EnsureInit(); return Lookup(_mutantsById, id, "MutantData"); }
        public ExpeditionEventData GetEvent(string id) { EnsureInit(); return Lookup(_eventsById, id, "ExpeditionEventData"); }

        public IReadOnlyList<ItemData> AllItems => items;
        public IReadOnlyList<CrewMemberData> AllCrew => crew;
        public IReadOnlyList<ExpeditionEventData> AllEvents => events;
        public List<CrewMemberData> allCrewMembers => crew; // Convenience for UI.

        private T Lookup<T>(Dictionary<string, T> dict, string id, string label) where T : GameDataObject
        {
            if (!string.IsNullOrEmpty(id) && dict.TryGetValue(id, out var value)) return value;
            Debug.LogWarning($"[GameDatabase] No {label} found for id '{id}'.");
            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild From Project")]
        private void RebuildFromProject()
        {
            items = ScanAll<ItemData>();
            crew = ScanAll<CrewMemberData>();
            traits = ScanAll<TraitData>();
            voiceGroups = ScanAll<VoiceLineGroup>();
            factions = ScanAll<FactionData>();
            anomalies = ScanAll<AnomalyData>();
            mutants = ScanAll<MutantData>();
            events = ScanAll<ExpeditionEventData>();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            Initialize(force: true);
            Debug.Log("[GameDatabase] Rebuilt content lists from project assets.");
        }

        private static List<T> ScanAll<T>() where T : Object
        {
            var result = new List<T>();
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            foreach (var guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            return result;
        }
#endif
    }
}
