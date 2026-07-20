// Assets/_Project/Scripts/Gameplay/BunkerDayLoopTest.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// End-to-end verification for Step 4 + the event wiring. Builds an in-memory database, constructs the
    /// managers, connects a ManagerEventBridge, seeds a bunker with limited food and two fragile crew, then
    /// advances days and watches them starve. Subscribes to the GLOBAL EventBus to prove DayAdvancedEvent and
    /// CrewDiedEvent actually fire through the bridge — so this exercises managers → bridge → EventBus → day
    /// engine all at once.
    ///
    /// USAGE: attach to an empty GameObject, right-click the component → "Run Bunker Day-Loop Test".
    /// Deletable after you've seen it. Ships nothing.
    /// </summary>
    public class BunkerDayLoopTest : MonoBehaviour
    {
        [ContextMenu("Run Bunker Day-Loop Test")]
        public void Run()
        {
            Debug.Log("──────── BUNKER DAY-LOOP TEST ────────");

            var db = BuildInMemoryDatabase();
            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);
            var bridge = new ManagerEventBridge();
            bridge.Connect(inventory, crew);

            var run = new RunData
            {
                runId = Guid.NewGuid().ToString("N"),
                runStartedUtc = DateTime.UtcNow,
                currentDay = 0,
                bunkerRadiationPool = 40
            };
            inventory.Bind(run);
            crew.Bind(run);

            // Seed the bunker: only 3 ration units for 2 crew → starvation begins on day 2.
            inventory.AddItem(InventoryChannel.Bunker, "item_ration", 3);
            crew.AddRescued("crew_a");
            crew.AddRescued("crew_b");
            crew.CommitRescuedToBunker();

            // Listen on the GLOBAL bus to confirm the bridge is forwarding events.
            int dayEvents = 0;
            int deathEvents = 0;
            Action<DayAdvancedEvent> onDay = e => { dayEvents++; Debug.Log($"   [EventBus] DayAdvancedEvent → day {e.NewDay}"); };
            Action<CrewDiedEvent> onDeath = e => { deathEvents++; Debug.Log($"   [EventBus] CrewDiedEvent → {e.CrewDataId}"); };
            EventBus.Subscribe(onDay);
            EventBus.Subscribe(onDeath);

            var controller = new BunkerDayController(run, inventory, crew, db, new BunkerDayConfig(), saveService: null);

            try
            {
                for (int i = 0; i < 8; i++)
                {
                    DayResult r = controller.AdvanceDay();
                    Debug.Log($"   → day {r.newDay}: fed={r.crewFed} starving={r.starvingCrew} deaths={r.deathsThisDay} alive={r.aliveRemaining}");
                    if (r.aliveRemaining <= 0)
                    {
                        Debug.Log("   all crew dead — run would now end. Stopping loop.");
                        break;
                    }
                }
            }
            finally
            {
                EventBus.Unsubscribe(onDay);
                EventBus.Unsubscribe(onDeath);
                bridge.Disconnect();
            }

            Debug.Log($"──────── DONE. DayAdvancedEvents fired: {dayEvents}, CrewDiedEvents: {deathEvents}. " +
                      $"Final day {run.currentDay}, radiation pool {run.bunkerRadiationPool}. ────────");
        }

        private GameDatabase BuildInMemoryDatabase()
        {
            var ration = ScriptableObject.CreateInstance<ItemData>();
            ration.id = "item_ration";
            ration.displayName = "Ration Tin";
            ration.category = ItemCategory.Food;
            ration.weightKg = 0.4f;
            ration.durability = 100;
            ration.decayPerDay = 0f;
            ration.utilityTags = new List<UtilityTag> { UtilityTag.Eat };

            var crewA = MakeFragileCrew("crew_a", "Anton", "Sokolov");
            var crewB = MakeFragileCrew("crew_b", "Boris", "Markov");

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.items = new List<ItemData> { ration };
            db.crew = new List<CrewMemberData> { crewA, crewB };
            db.traits = new List<TraitData>();
            db.voiceGroups = new List<VoiceLineGroup>();
            db.factions = new List<FactionData>();
            db.anomalies = new List<AnomalyData>();
            db.mutants = new List<MutantData>();
            db.events = new List<ExpeditionEventData>();
            db.Initialize(force: true);
            return db;
        }

        private CrewMemberData MakeFragileCrew(string id, string first, string last)
        {
            var c = ScriptableObject.CreateInstance<CrewMemberData>();
            c.id = id;
            c.displayName = first;
            c.firstName = first;
            c.lastName = last;
            c.background = CrewBackground.LonerScavenger;
            c.baseStats = new CrewBaseStats
            {
                maxHealth = 30,   // fragile so starvation resolves quickly in the demo
                maxSanity = 50,
                carryCapacityKg = 20f,
                sanityRecoveryMultiplier = 1f,
                radiationResistanceMultiplier = 1f,
                combatResolutionMultiplier = 1f
            };
            c.startingTraits = new List<TraitData>();
            return c;
        }
    }
}
