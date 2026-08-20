using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;
using OblastZero.Data;

namespace OblastZero.Services
{
    /// <summary>
    /// Loads expedition events from JSON files and deserializes them into ExpeditionEventData objects.
    /// JSON schema diverges from SO schema: JSON uses `narrativeText` (localization key), `itemId` (string refs),
    /// `reputationFactionSecondary` (optional secondary faction), etc. This loader maps them to the SO model.
    ///
    /// Usage: called by GameDatabase.Initialize() during boot.
    /// </summary>
    public static class EventJsonLoader
    {
        private const string EventResourcePath = "Events"; // Assets/Data/Resources/Events/

        /// <summary>
        /// Load all .json files from Resources/Events/ and deserialize into ExpeditionEventData instances.
        /// Returns a runtime-only list; GameDatabase indexes it alongside the authored `events` list
        /// rather than appending to it, so the serialized asset stays authored-only.
        /// </summary>
        public static List<ExpeditionEventData> LoadEventsFromResources(GameDatabase database)
        {
            var events = new List<ExpeditionEventData>();

            // Load all TextAssets from the Resources/Events folder.
            var jsonAssets = Resources.LoadAll<TextAsset>(EventResourcePath);

            foreach (var jsonAsset in jsonAssets)
            {
                try
                {
                    var eventData = DeserializeEventFromJson(jsonAsset.text, jsonAsset.name, database);
                    if (eventData != null)
                    {
                        events.Add(eventData);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[EventJsonLoader] Failed to deserialize event '{jsonAsset.name}': {ex.Message}");
                }
            }

            Debug.Log($"[EventJsonLoader] Loaded {events.Count} events from JSON.");
            return events;
        }

        /// <summary>
        /// Deserialize a single JSON string into an ExpeditionEventData object.
        /// Maps JSON schema to SO schema:
        /// - `narrativeText` → narrativeTextKey (localization key)
        /// - `title` → titleKey
        /// - `itemId` → look up ItemData from database by id
        /// - `reputationFactionSecondary` → not stored in SO; logged only
        /// - `successChance` → encoded as formula if `successChanceFormula` is present
        /// </summary>
        private static ExpeditionEventData DeserializeEventFromJson(string jsonText, string sourceName, GameDatabase database)
        {
            var jobj = JObject.Parse(jsonText);

            var eventData = ScriptableObject.CreateInstance<ExpeditionEventData>();
            eventData.name = sourceName;

            // Identity
            eventData.id = jobj["id"]?.ToString() ?? System.Guid.NewGuid().ToString("N");
            eventData.displayName = jobj["title"]?.ToString() ?? "Unnamed Event";
            eventData.titleKey = jobj["title"]?.ToString() ?? "event.title.unknown";
            eventData.narrativeTextKey = jobj["narrativeText"]?.ToString() ?? "event.narrative.unknown";

            // Prerequisite gating
            var prereqJobj = jobj["prerequisites"];
            if (prereqJobj != null)
            {
                eventData.prerequisites = new EventPrerequisite
                {
                    minDay = prereqJobj["minDay"]?.Value<int>() ?? 0,
                    maxDay = prereqJobj["maxDay"]?.Value<int>() ?? 999,
                    minFactionRep = prereqJobj["minFactionRep"]?.Value<int>() ?? -100,
                    maxFactionRep = prereqJobj["maxFactionRep"]?.Value<int>() ?? 100,
                };

                // Parse faction context if present.
                var factionStr = prereqJobj["factionContext"]?.ToString();
                if (!string.IsNullOrEmpty(factionStr) && System.Enum.TryParse<FactionId>(factionStr, out var faction))
                {
                    eventData.prerequisites.factionContext = faction;
                }

                // Proximity locales (fail-closed gate — see EventPrerequisite.regionTagsAny).
                eventData.prerequisites.regionTagsAny = StringList(prereqJobj["regionTagsAny"] as JArray);

                // Canonical oblast regions (fail-open gate — see EventPrerequisite.oblastRegionsAny).
                // Absent on legacy content, which reads as an empty list and therefore matches anywhere.
                eventData.prerequisites.oblastRegionsAny = StringList(prereqJobj["oblastRegionsAny"] as JArray);

                // Event-level trait gating. Distinct from the per-choice traits parsed below: these decide
                // whether the event is offered at all, where those decide which branches are pickable.
                eventData.prerequisites.requiredCrewTraitIds = StringList(prereqJobj["requiredCrewTraitIds"] as JArray);

                // requiredItemsAny stays empty from JSON: the field is List<ItemData> (direct SO references),
                // and the JSON corpus addresses items by id. Resolving ids here would need the item index,
                // which is still loading when events deserialize. Content expresses item gating through
                // OutcomeDelta.itemsLost / lootGained instead, both of which do resolve by id.
                eventData.prerequisites.requiredItemsAny = new List<ItemData>();
            }

            // Base weight for event selection.
            eventData.baseWeight = jobj["baseWeight"]?.Value<float>() ?? 1f;

            // Parse choices (outcomes).
            eventData.choices = new List<EventChoice>();
            var choicesArray = jobj["choices"] as JArray;
            if (choicesArray != null)
            {
                foreach (var choiceJobj in choicesArray)
                {
                    var choice = new EventChoice
                    {
                        choiceLabelKey = choiceJobj["choiceLabel"]?.ToString() ?? "event.choice.unknown",
                        successChance = choiceJobj["successChance"]?.Value<float>() ?? 0.5f,
                        successChanceFormula = choiceJobj["successChanceFormula"]?.ToString() ?? string.Empty,
                        requiredTraitsAny = choiceJobj["requiredTraitsAny"] as JArray != null
                            ? ((JArray)choiceJobj["requiredTraitsAny"]).ToObject<List<string>>()
                            : new List<string>(),
                        blockedByTraits = choiceJobj["blockedByTraits"] as JArray != null
                            ? ((JArray)choiceJobj["blockedByTraits"]).ToObject<List<string>>()
                            : new List<string>(),
                    };

                    // Success outcome.
                    var successJobj = choiceJobj["successOutcome"];
                    if (successJobj != null)
                    {
                        choice.successOutcome = DeserializeOutcomeDelta((JObject)successJobj, database);
                    }

                    // Failure outcome (optional).
                    var failureJobj = choiceJobj["failureOutcome"];
                    if (failureJobj != null)
                    {
                        choice.failureOutcome = DeserializeOutcomeDelta((JObject)failureJobj, database);
                    }

                    eventData.choices.Add(choice);
                }
            }

            eventData.sourceJsonPath = sourceName;

            return eventData;
        }

        /// <summary>
        /// Deserialize a JSON outcome object into an OutcomeDelta struct.
        /// Maps `itemId` (string) → looks up ItemData from database.
        /// Maps `reputationFactionSecondary` if present (currently logged only, not stored).
        /// </summary>
        private static OutcomeDelta DeserializeOutcomeDelta(JObject jobj, GameDatabase database)
        {
            var delta = new OutcomeDelta
            {
                sanityDelta = jobj["sanityDelta"]?.Value<int>() ?? 0,
                fatigueDelta = jobj["fatigueDelta"]?.Value<int>() ?? 0,
                radiationDelta = jobj["radiationDelta"]?.Value<int>() ?? 0,
                healthDelta = jobj["healthDelta"]?.Value<int>() ?? 0,
                crewDeathChance = jobj["crewDeathChance"]?.Value<float>() ?? 0f,
                followUpEventId = jobj["followUpEventId"]?.ToString() ?? string.Empty,
            };

            // Primary reputation change.
            var repFactionStr = jobj["reputationFaction"]?.ToString();
            if (!string.IsNullOrEmpty(repFactionStr) && System.Enum.TryParse<FactionId>(repFactionStr, out var faction))
            {
                delta.reputationFaction = faction;
            }
            delta.reputationDelta = jobj["reputationDelta"]?.Value<int>() ?? 0;

            // Secondary faction (if present) — currently logged only.
            var secondaryFactionStr = jobj["reputationFactionSecondary"]?.ToString();
            if (!string.IsNullOrEmpty(secondaryFactionStr))
            {
                Debug.Log($"[EventJsonLoader] Note: secondary faction '{secondaryFactionStr}' in outcome. Not stored in SO schema yet.");
            }

            // Loot gained (by itemId).
            delta.lootGained = new List<WeightedItem>();
            var lootArray = jobj["lootGained"] as JArray;
            if (lootArray != null)
            {
                foreach (var lootJobj in lootArray)
                {
                    var itemId = lootJobj["itemId"]?.ToString();
                    var weight = lootJobj["weight"]?.Value<float>() ?? 1f;
                    var quantity = lootJobj["quantity"]?.Value<int>() ?? 1;

                    if (!string.IsNullOrEmpty(itemId) && database.TryGetItem(itemId, out var itemData))
                    {
                        delta.lootGained.Add(new WeightedItem
                        {
                            item = itemData,
                            dropChance = weight,
                            minQty = quantity,
                            maxQty = quantity,
                        });
                    }
                }
            }

            // Items lost (by itemId).
            delta.itemsLost = new List<ItemData>();
            var lostArray = jobj["itemsLost"] as JArray;
            if (lostArray != null)
            {
                foreach (var lostItemId in lostArray)
                {
                    var itemId = lostItemId.ToString();
                    if (!string.IsNullOrEmpty(itemId) && database.TryGetItem(itemId, out var itemData))
                    {
                        delta.itemsLost.Add(itemData);
                    }
                }
            }

            return delta;
        }

        /// <summary>
        /// A JSON array of strings as a List, never null. Absent and empty arrays both yield an empty list,
        /// because every consumer of these fields treats "no entries" as "no constraint" and a null would
        /// instead throw at the first foreach.
        /// </summary>
        private static List<string> StringList(JArray array)
        {
            var result = new List<string>();
            if (array == null) return result;

            foreach (var token in array)
            {
                var value = token?.ToString();
                if (!string.IsNullOrEmpty(value)) result.Add(value);
            }
            return result;
        }
    }
}
