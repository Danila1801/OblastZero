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
    ///
    /// Lifetime note: this is a ScriptableObject asset, so a single instance is shared between the Editor
    /// and Play mode. Entering Play mode runs a domain reload, which round-trips the live instance through
    /// Unity's serializer — that preserves plain fields but silently drops every <see cref="Dictionary{K,V}"/>.
    /// The init state is therefore derived from <see cref="IndexesBuilt"/> rather than trusted from a bool,
    /// so a dropped index always rebuilds instead of being reported as "already initialized".
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

        // JSON-sourced content lives in runtime-only lists, never in the serialized ones above.
        // Appending it to `items`/`events` grew the asset by 703 items / 1020 events on every rebuild,
        // and any Editor tool that saved the asset afterwards persisted throwaway CreateInstance objects
        // as broken references. Kept separate, the indexes merge both sources and the asset stays authored-only.
        [System.NonSerialized] private List<ItemData> _jsonItems;
        [System.NonSerialized] private List<ExpeditionEventData> _jsonEvents;
        [System.NonSerialized] private List<ItemData> _allItems;
        [System.NonSerialized] private List<ExpeditionEventData> _allEvents;

        // Must not survive a domain reload — the dictionaries above cannot, and a stale `true` here
        // is what makes Initialize() early-return over a set of null indexes.
        [System.NonSerialized] private bool _initialized;
        [System.NonSerialized] private bool _initializing;

        public bool IsInitialized => _initialized && IndexesBuilt;

        /// <summary>
        /// True only when every index actually exists. This is the real test for "usable"; the
        /// <c>_initialized</c> flag alone cannot be trusted across a play-mode domain reload.
        /// </summary>
        private bool IndexesBuilt =>
            _itemsById != null && _crewById != null && _traitsById != null && _voiceById != null &&
            _factionsById != null && _factionsByEnum != null && _anomaliesById != null &&
            _mutantsById != null && _eventsById != null;

        /// <summary>Builds all id indexes. Call once at bootstrap. Idempotent unless <paramref name="force"/>.</summary>
        public void Initialize(bool force = false)
        {
            if (_initialized && IndexesBuilt && !force) return;
            if (_initializing)
            {
                Debug.LogWarning("[GameDatabase] Initialize() called re-entrantly. Skipping.");
                return;
            }
            _initializing = true;

            try
            {
                // Load JSON items first — the event loader resolves item ids through TryGetItem,
                // so the item index has to exist before events are deserialized.
                _jsonItems = ItemJsonLoader.LoadItemsFromResources() ?? new List<ItemData>();

                _itemsById = BuildIndex("ItemData", items, _jsonItems);

                _crewById = BuildIndex("CrewMemberData", crew);
                _traitsById = BuildIndex("TraitData", traits);
                _voiceById = BuildIndex("VoiceLineGroup", voiceGroups);
                _factionsById = BuildIndex("FactionData", factions);
                _anomaliesById = BuildIndex("AnomalyData", anomalies);
                _mutantsById = BuildIndex("MutantData", mutants);

                _jsonEvents = EventJsonLoader.LoadEventsFromResources(this) ?? new List<ExpeditionEventData>();

                _eventsById = BuildIndex("ExpeditionEventData", events, _jsonEvents);

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

                _allItems = Combine(items, _jsonItems);
                _allEvents = Combine(events, _jsonEvents);

                _initialized = true;
                Debug.Log($"[GameDatabase] Initialized: {_itemsById.Count} items, {_crewById.Count} crew, " +
                          $"{_traitsById.Count} traits, {_voiceById.Count} voice groups, {_factionsById.Count} factions, " +
                          $"{_anomaliesById.Count} anomalies, {_mutantsById.Count} mutants, {_eventsById.Count} events.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[GameDatabase] Initialize() threw: {ex}");
                throw;
            }
            finally
            {
                _initializing = false;
            }
        }

        private Dictionary<string, T> BuildIndex<T>(string label, params List<T>[] sources) where T : GameDataObject
        {
            int capacity = 0;
            foreach (var source in sources) capacity += source?.Count ?? 0;

            var dict = new Dictionary<string, T>(capacity);
            foreach (var source in sources)
            {
                if (source == null) continue;
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
            }
            return dict;
        }

        private static List<T> Combine<T>(List<T> authored, List<T> fromJson)
        {
            var combined = new List<T>((authored?.Count ?? 0) + (fromJson?.Count ?? 0));
            if (authored != null) combined.AddRange(authored);
            if (fromJson != null) combined.AddRange(fromJson);
            return combined;
        }

        private void EnsureInit()
        {
            if (_initialized && IndexesBuilt) return;
            if (_initializing)
            {
                // Re-entrant call during Initialize() — e.g. EventJsonLoader looking up an item
                // via TryGetItem while Initialize is still mid-flight. The indexes built so far are
                // safe to read; the ones not yet built are null, which every Lookup already guards.
                return;
            }
            Initialize();
        }

        // ---- Lookups ----

        public ItemData GetItem(string id) { EnsureInit(); return Lookup(_itemsById, id, "ItemData"); }
        public bool TryGetItem(string id, out ItemData data) { EnsureInit(); data = null; return _itemsById != null && _itemsById.TryGetValue(id ?? string.Empty, out data); }

        public CrewMemberData GetCrew(string id) { EnsureInit(); return Lookup(_crewById, id, "CrewMemberData"); }
        public bool TryGetCrew(string id, out CrewMemberData data) { EnsureInit(); data = null; return _crewById != null && _crewById.TryGetValue(id ?? string.Empty, out data); }

        public TraitData GetTrait(string id) { EnsureInit(); return Lookup(_traitsById, id, "TraitData"); }
        public VoiceLineGroup GetVoiceGroup(string id) { EnsureInit(); return Lookup(_voiceById, id, "VoiceLineGroup"); }

        public FactionData GetFaction(string id) { EnsureInit(); return Lookup(_factionsById, id, "FactionData"); }
        public FactionData GetFaction(FactionId factionId)
        {
            EnsureInit();
            if (_factionsByEnum == null)
            {
                Debug.LogError($"[GameDatabase] Faction index missing after Initialize(); cannot look up '{factionId}'.");
                return null;
            }
            if (_factionsByEnum.TryGetValue(factionId, out var value)) return value;
            Debug.LogWarning($"[GameDatabase] No FactionData found for FactionId '{factionId}'.");
            return null;
        }

        public AnomalyData GetAnomaly(string id) { EnsureInit(); return Lookup(_anomaliesById, id, "AnomalyData"); }
        public MutantData GetMutant(string id) { EnsureInit(); return Lookup(_mutantsById, id, "MutantData"); }
        public ExpeditionEventData GetEvent(string id) { EnsureInit(); return Lookup(_eventsById, id, "ExpeditionEventData"); }

        // Authored + JSON content. These are the lists callers iterate (EventEngine walks AllEvents to
        // build its weighted pool), so they must include the JSON content the indexes were built from.
        public IReadOnlyList<ItemData> AllItems { get { EnsureInit(); return _allItems ?? (IReadOnlyList<ItemData>)items; } }
        public IReadOnlyList<CrewMemberData> AllCrew { get { EnsureInit(); return crew; } }
        public IReadOnlyList<ExpeditionEventData> AllEvents { get { EnsureInit(); return _allEvents ?? (IReadOnlyList<ExpeditionEventData>)events; } }
        public List<CrewMemberData> allCrewMembers => crew; // Convenience for UI.

        private T Lookup<T>(Dictionary<string, T> dict, string id, string label) where T : GameDataObject
        {
            if (dict == null)
            {
                Debug.LogError($"[GameDatabase] {label} index missing after Initialize(); lookup for '{id}' failed.");
                return null;
            }
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
