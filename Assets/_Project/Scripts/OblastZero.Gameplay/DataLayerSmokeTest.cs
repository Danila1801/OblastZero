// Assets/_Project/Scripts/Gameplay/DataLayerSmokeTest.cs
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Vertical-slice verification for the whole data layer. Builds an in-memory database, runs a
    /// scavenge → transition → bunker-day scenario through the managers, then serializes RunData with
    /// Newtonsoft and deserializes it back, asserting the state survived intact. Also proves Newtonsoft
    /// round-trips the MetaProgress Dictionary (the reason JsonUtility was rejected).
    ///
    /// USAGE: attach to any empty GameObject, then right-click the component header in the Inspector and
    /// pick "Run Data Layer Round-Trip Test". Read the result in the Console. Delete this file (or move it
    /// to an Editor test assembly) once you've seen it pass — it ships nothing.
    /// </summary>
    public class DataLayerSmokeTest : MonoBehaviour
    {
        private int _checks;
        private int _passed;

        [ContextMenu("Run Data Layer Round-Trip Test")]
        public void Run()
        {
            _checks = 0;
            _passed = 0;
            Debug.Log("──────── DATA LAYER ROUND-TRIP TEST ────────");

            var db = BuildInMemoryDatabase();
            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);

            var run = NewRun();
            inventory.Bind(run);
            crew.Bind(run);

            // ---- Phase A: the 60-second scavenge ----
            inventory.AddItem(InventoryChannel.Scavenged, "item_canned_meat", 3);         // consumable
            inventory.AddItem(InventoryChannel.Scavenged, "item_canned_meat", 2);         // merges -> qty 5
            inventory.AddItem(InventoryChannel.Scavenged, "item_axe", 1, durability: 40); // durable, partial
            crew.AddRescued("crew_marina");

            Check("scavenged holds 2 distinct stacks", run.ScavengedInventory.Count == 2);
            Check("canned meat merged to qty 5", StackQty(run.ScavengedInventory, "item_canned_meat") == 5);
            Check("crew rescued but not yet in bunker", run.RescuedCrew.Count == 1 && run.ActiveCrew.Count == 0);

            // ---- Transition: the 3D -> 2D handoff ----
            inventory.TransferScavengedToBunker();
            crew.CommitRescuedToBunker();

            Check("scavenged cleared after transfer", run.ScavengedInventory.Count == 0);
            Check("bunker received 2 stacks", run.BunkerInventory.Count == 2);
            Check("crew now active in bunker", run.ActiveCrew.Count == 1 && run.RescuedCrew.Count == 0);

            float weight = inventory.GetTotalWeight(InventoryChannel.Bunker);
            Check("bunker weight computed from item data (>0)", weight > 0f);
            Debug.Log($"   bunker weight = {weight:0.00} kg");

            // ---- Phase B: a day passes, hazards land ----
            string marinaId = run.ActiveCrew[0].instanceId;
            crew.ApplyRadiation(marinaId, 30);
            crew.ApplySanityDelta(marinaId, -25);
            crew.ApplyHealthDelta(marinaId, -10);
            inventory.ApplyDailyDecay(); // canned meat decays (decayPerDay = 2 -> -2 durability)

            int meatDurabilityAfterDecay = StackDurability(run.BunkerInventory, "item_canned_meat");
            Check("radiation registered on crew", run.ActiveCrew[0].currentRadiation > 0);
            Check("crew survived the day", run.ActiveCrew[0].isAlive);
            Check("canned meat decayed below max", meatDurabilityAfterDecay < 100);

            // ---- SAVE: serialize RunData via Newtonsoft ----
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Include
            };
            string runJson = JsonConvert.SerializeObject(run, settings);
            Debug.Log($"   serialized RunData = {runJson.Length} chars");

            // ---- LOAD: deserialize into a fresh object ----
            var loaded = JsonConvert.DeserializeObject<RunData>(runJson, settings);

            Check("loaded run is not null", loaded != null);
            Check("runId preserved", loaded.runId == run.runId);
            Check("runStartedUtc preserved (DateTime survives JSON)", loaded.runStartedUtc == run.runStartedUtc);
            Check("bunker stack count preserved", loaded.BunkerInventory.Count == run.BunkerInventory.Count);
            Check("canned meat quantity preserved", StackQty(loaded.BunkerInventory, "item_canned_meat") == 5);
            Check("axe durability preserved (40)", StackDurability(loaded.BunkerInventory, "item_axe") == 40);
            Check("meat decay value persisted", StackDurability(loaded.BunkerInventory, "item_canned_meat") == meatDurabilityAfterDecay);
            Check("active crew count preserved", loaded.ActiveCrew.Count == 1);
            Check("crew health preserved", loaded.ActiveCrew[0].currentHealth == run.ActiveCrew[0].currentHealth);
            Check("crew sanity preserved", loaded.ActiveCrew[0].currentSanity == run.ActiveCrew[0].currentSanity);
            Check("crew radiation preserved", loaded.ActiveCrew[0].currentRadiation == run.ActiveCrew[0].currentRadiation);
            Check("crew traitIds list preserved", loaded.ActiveCrew[0].traitIds.Count == run.ActiveCrew[0].traitIds.Count);

            // ---- MetaProgress: prove Newtonsoft handles Dictionary (JsonUtility would drop it) ----
            var meta = new MetaProgressData { totalRunsAttempted = 7 };
            meta.steamStats["mutants_killed"] = 12;
            meta.steamStats["days_survived"] = 41;
            meta.unlockedScavengeSites.Add("site_reservoir");

            string metaJson = JsonConvert.SerializeObject(meta, settings);
            var metaLoaded = JsonConvert.DeserializeObject<MetaProgressData>(metaJson, settings);

            Check("meta Dictionary survived round-trip", metaLoaded.steamStats.Count == 2 && metaLoaded.steamStats["mutants_killed"] == 12);
            Check("meta list survived round-trip", metaLoaded.unlockedScavengeSites.Count == 1);

            // ---- Verdict ----
            bool allPass = _passed == _checks;
            string verdict = allPass ? "ALL PASS" : $"{_checks - _passed} FAILED";
            if (allPass)
                Debug.Log($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
            else
                Debug.LogError($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
        }

        // ---- Scenario fixtures ----

        private GameDatabase BuildInMemoryDatabase()
        {
            var meat = ScriptableObject.CreateInstance<ItemData>();
            meat.id = "item_canned_meat";
            meat.displayName = "Canned Meat";
            meat.category = ItemCategory.Food;
            meat.weightKg = 0.4f;
            meat.durability = 100;
            meat.decayPerDay = 2f;
            meat.utilityTags = new List<UtilityTag> { UtilityTag.Eat, UtilityTag.Trade };

            var axe = ScriptableObject.CreateInstance<ItemData>();
            axe.id = "item_axe";
            axe.displayName = "Fire Axe";
            axe.category = ItemCategory.Tool;
            axe.weightKg = 3.2f;
            axe.durability = 100;
            axe.decayPerDay = 0f;
            axe.utilityTags = new List<UtilityTag> { UtilityTag.Repair, UtilityTag.Fight };

            var marina = ScriptableObject.CreateInstance<CrewMemberData>();
            marina.id = "crew_marina";
            marina.displayName = "Marina";
            marina.lastName = "Volkova";
            marina.background = CrewBackground.FieldMedic;
            marina.baseStats = new CrewBaseStats
            {
                maxHealth = 100,
                maxSanity = 100,
                carryCapacityKg = 25f,
                sanityRecoveryMultiplier = 1f,
                radiationResistanceMultiplier = 1f,
                combatResolutionMultiplier = 1f
            };
            marina.startingTraits = new List<TraitData>();

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.items = new List<ItemData> { meat, axe };
            db.crew = new List<CrewMemberData> { marina };
            db.traits = new List<TraitData>();
            db.voiceGroups = new List<VoiceLineGroup>();
            db.Initialize(force: true);
            return db;
        }

        private RunData NewRun() => new RunData
        {
            runId = System.Guid.NewGuid().ToString("N"),
            runStartedUtc = System.DateTime.UtcNow,
            currentDay = 1,
            rngSeed = 12345
        };

        // ---- Assert + small read helpers ----

        private void Check(string label, bool condition)
        {
            _checks++;
            if (condition)
            {
                _passed++;
                Debug.Log($"   PASS  {label}");
            }
            else
            {
                Debug.LogError($"   FAIL  {label}");
            }
        }

        private static int StackQty(List<ItemInstance> list, string id)
        {
            int q = 0;
            foreach (var i in list) if (i.itemDataId == id) q += i.quantity;
            return q;
        }

        private static int StackDurability(List<ItemInstance> list, string id)
        {
            foreach (var i in list) if (i.itemDataId == id) return i.currentDurability;
            return -1;
        }
    }
}
