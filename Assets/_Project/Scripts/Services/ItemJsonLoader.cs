// Assets/_Project/Scripts/Services/ItemJsonLoader.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using OblastZero.Data;

namespace OblastZero.Services
{
    /// <summary>
    /// Loads item definitions from JSON files in Resources/Items/ and deserializes them
    /// into ItemData ScriptableObject instances at runtime. Mirrors the EventJsonLoader pattern.
    ///
    /// JSON schema fields: id, displayName, category, weightKg, durability, decayPerDay,
    /// utilityTags[], radiationContaminated, radiationContaminationLevel,
    /// baseTradeValueScale, baseTradeValueCordon, baseTradeValueKafedra.
    ///
    /// Usage: called by GameDatabase.Initialize() during boot, AFTER authored SO items are loaded.
    /// </summary>
    public static class ItemJsonLoader
    {
        private const string ItemResourcePath = "Items";

        /// <summary>
        /// Load all .json files from Resources/Items/ and deserialize into ItemData instances.
        /// Returns a list ready to be merged with GameDatabase.items.
        /// </summary>
        public static List<ItemData> LoadItemsFromResources()
        {
            var items = new List<ItemData>();
            var jsonAssets = Resources.LoadAll<TextAsset>(ItemResourcePath);

            foreach (var jsonAsset in jsonAssets)
            {
                try
                {
                    var itemData = DeserializeItemFromJson(jsonAsset.text, jsonAsset.name);
                    if (itemData != null)
                    {
                        items.Add(itemData);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ItemJsonLoader] Failed to deserialize item '{jsonAsset.name}': {ex.Message}");
                }
            }

            Debug.Log($"[ItemJsonLoader] Loaded {items.Count} items from JSON.");
            return items;
        }

        private static ItemData DeserializeItemFromJson(string jsonText, string sourceName)
        {
            var jobj = JObject.Parse(jsonText);

            var item = ScriptableObject.CreateInstance<ItemData>();
            item.name = sourceName;

            // Identity
            item.id = jobj["id"]?.ToString() ?? System.Guid.NewGuid().ToString("N");
            item.displayName = jobj["displayName"]?.ToString() ?? "Unnamed Item";

            // Category
            var catStr = jobj["category"]?.ToString();
            if (!string.IsNullOrEmpty(catStr) && Enum.TryParse<ItemCategory>(catStr, out var category))
            {
                item.category = category;
            }

            // Physical
            item.weightKg = jobj["weightKg"]?.Value<float>() ?? 0.1f;
            item.durability = jobj["durability"]?.Value<int>() ?? 100;
            item.decayPerDay = jobj["decayPerDay"]?.Value<float>() ?? 0f;

            // Utility tags
            item.utilityTags = new List<UtilityTag>();
            var tagsArray = jobj["utilityTags"] as JArray;
            if (tagsArray != null)
            {
                foreach (var tagToken in tagsArray)
                {
                    var tagStr = tagToken.ToString();
                    if (Enum.TryParse<UtilityTag>(tagStr, out var tag))
                    {
                        item.utilityTags.Add(tag);
                    }
                }
            }

            // Hazard
            item.radiationContaminated = jobj["radiationContaminated"]?.Value<bool>() ?? false;
            item.radiationContaminationLevel = jobj["radiationContaminationLevel"]?.Value<float>() ?? 0f;

            // Trade values
            item.baseTradeValueScale = jobj["baseTradeValueScale"]?.Value<int>() ?? 0;
            item.baseTradeValueCordon = jobj["baseTradeValueCordon"]?.Value<int>() ?? 0;
            item.baseTradeValueKafedra = jobj["baseTradeValueKafedra"]?.Value<int>() ?? 0;

            // icon and worldPrefab stay null (set in-editor for Phase A pickup prefabs)

            return item;
        }
    }
}
