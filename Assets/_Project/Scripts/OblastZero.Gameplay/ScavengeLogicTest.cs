// Assets/_Project/Scripts/Gameplay/ScavengeLogicTest.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Verifies the non-visual logic of the 3D Blowout without needing a built scene: the pickup routing
    /// (what ScavengeController calls when you grab something) and the EmissionTimer's tick/expiry events,
    /// all flowing through the managers and the EventBus bridge. Set up the FP scene separately for feel.
    ///
    /// USAGE: attach to an empty GameObject, right-click the component → "Run Scavenge Logic Test".
    /// </summary>
    public class ScavengeLogicTest : MonoBehaviour
    {
        private int _checks;
        private int _passed;

        [ContextMenu("Run Scavenge Logic Test")]
        public void Run()
        {
            _checks = 0;
            _passed = 0;
            Debug.Log("──────── SCAVENGE LOGIC TEST ────────");

            var db = BuildInMemoryDatabase();
            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);
            var bridge = new ManagerEventBridge();
            bridge.Connect(inventory, crew);

            var run = new RunData { runId = Guid.NewGuid().ToString("N"), runStartedUtc = DateTime.UtcNow, currentDay = 0 };
            inventory.Bind(run);
            crew.Bind(run);

            // Watch the global bus to confirm pickups surface as events through the bridge.
            int pickedEvents = 0, rescuedEvents = 0;
            Action<ItemPickedUpEvent> onPick = _ => pickedEvents++;
            Action<CrewRescuedEvent> onRescue = _ => rescuedEvents++;
            EventBus.Subscribe(onPick);
            EventBus.Subscribe(onRescue);

            // Simulate the grabs ScavengeController performs on pickup.
            inventory.AddItem(InventoryChannel.Scavenged, "item_canned_meat", 2);
            inventory.AddItem(InventoryChannel.Scavenged, "item_axe", 1, durability: 55);
            crew.AddRescued("crew_marina");

            Check("two scavenged stacks", run.ScavengedInventory.Count == 2);
            Check("canned meat quantity 2", StackQty(run.ScavengedInventory, "item_canned_meat") == 2);
            Check("axe durability 55 preserved", StackDurability(run.ScavengedInventory, "item_axe") == 55);
            Check("one crew rescued (incoming)", run.RescuedCrew.Count == 1 && run.ActiveCrew.Count == 0);
            Check("ItemPickedUpEvent fired twice via bridge", pickedEvents == 2);
            Check("CrewRescuedEvent fired once via bridge", rescuedEvents == 1);

            // Emission timer: tick a 3s timer past zero, expect exactly one expiry + per-second ticks.
            int ticks = 0, expiries = 0;
            Action<ScavengeTimerTickEvent> onTick = _ => ticks++;
            Action<ScavengeTimerExpiredEvent> onExpire = _ => expiries++;
            EventBus.Subscribe(onTick);
            EventBus.Subscribe(onExpire);

            var timer = new EmissionTimer(3f);
            for (int i = 0; i < 40; i++) timer.Tick(0.1f); // 4s of ticking over a 3s clock

            Check("timer reports expired", timer.IsExpired);
            Check("expiry event fired exactly once", expiries == 1);
            Check("per-second tick events fired", ticks >= 2);

            EventBus.Unsubscribe(onPick);
            EventBus.Unsubscribe(onRescue);
            EventBus.Unsubscribe(onTick);
            EventBus.Unsubscribe(onExpire);
            bridge.Disconnect();

            bool allPass = _passed == _checks;
            string verdict = allPass ? "ALL PASS" : $"{_checks - _passed} FAILED";
            if (allPass) Debug.Log($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
            else Debug.LogError($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
        }

        private GameDatabase BuildInMemoryDatabase()
        {
            var meat = ScriptableObject.CreateInstance<ItemData>();
            meat.id = "item_canned_meat";
            meat.displayName = "Canned Meat";
            meat.category = ItemCategory.Food;
            meat.weightKg = 0.4f;
            meat.durability = 100;
            meat.utilityTags = new List<UtilityTag> { UtilityTag.Eat };

            var axe = ScriptableObject.CreateInstance<ItemData>();
            axe.id = "item_axe";
            axe.displayName = "Fire Axe";
            axe.category = ItemCategory.Tool;
            axe.weightKg = 3.2f;
            axe.durability = 100;
            axe.utilityTags = new List<UtilityTag> { UtilityTag.Repair, UtilityTag.Fight };

            var marina = ScriptableObject.CreateInstance<CrewMemberData>();
            marina.id = "crew_marina";
            marina.displayName = "Marina";
            marina.lastName = "Volkova";
            marina.background = CrewBackground.FieldMedic;
            marina.baseStats = new CrewBaseStats
            {
                maxHealth = 100, maxSanity = 100, carryCapacityKg = 22f,
                sanityRecoveryMultiplier = 1f, radiationResistanceMultiplier = 1f, combatResolutionMultiplier = 1f
            };
            marina.startingTraits = new List<TraitData>();

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.items = new List<ItemData> { meat, axe };
            db.crew = new List<CrewMemberData> { marina };
            db.traits = new List<TraitData>();
            db.voiceGroups = new List<VoiceLineGroup>();
            db.factions = new List<FactionData>();
            db.anomalies = new List<AnomalyData>();
            db.mutants = new List<MutantData>();
            db.events = new List<ExpeditionEventData>();
            db.Initialize(force: true);
            return db;
        }

        private void Check(string label, bool condition)
        {
            _checks++;
            if (condition) { _passed++; Debug.Log($"   PASS  {label}"); }
            else Debug.LogError($"   FAIL  {label}");
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
