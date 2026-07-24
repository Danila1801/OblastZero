// Assets/_Project/Scripts/Gameplay/BunkerPhaseSmokeTest.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// End-to-end verification for the bunker turn loop (Step 4→5 wiring). Builds an in-memory database with a
    /// branching event that queues a lethal follow-up, then drives a <see cref="BunkerPhaseController"/> exactly
    /// as SurvivalPhase2DState does: EndDay presents an event, EndDay is refused while one is pending, resolving
    /// applies effects and queues the follow-up, the follow-up fires next day with queue priority, and a lethal
    /// outcome wipes the crew so IsWipe/runEnded report the failure.
    ///
    /// USAGE: attach to an empty GameObject, right-click the component → "Run Bunker Phase Test". Ships nothing.
    /// </summary>
    public class BunkerPhaseSmokeTest : MonoBehaviour
    {
        private int _checks;
        private int _passed;

        [ContextMenu("Run Bunker Phase Test")]
        public void Run()
        {
            _checks = 0;
            _passed = 0;
            Debug.Log("──────── BUNKER PHASE TEST ────────");

            var db = BuildInMemoryDatabase();
            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);
            var rep = new FactionReputationManager();
            var engine = new EventEngine(db, inventory, crew, rep);

            var run = new RunData
            {
                runId = System.Guid.NewGuid().ToString("N"),
                runStartedUtc = System.DateTime.UtcNow,
                currentDay = 0,
                rngSeed = 7
            };
            inventory.Bind(run);
            crew.Bind(run);
            rep.Bind(run);
            engine.Bind(run);

            // Plenty of food so day ticks never starve anyone before the scripted lethal event lands.
            inventory.AddItem(InventoryChannel.Bunker, "item_ration", 20);
            crew.AddRescued("crew_solo");
            crew.CommitRescuedToBunker();

            var dayController = new BunkerDayController(run, inventory, crew, db, new BunkerDayConfig(), saveService: null);
            var phase = new BunkerPhaseController(dayController, engine, crew);

            // ---- Day 1: end the day → an event is presented and held pending ----
            var t1 = phase.EndDay();
            Check("day advanced to 1", t1.day.newDay == 1);
            Check("run did not end", !t1.runEnded);
            Check("an event was presented", t1.presentedEvent != null && t1.presentedEvent.id == "evt_a");
            Check("controller holds the pending event", phase.HasPendingEvent && phase.PendingEvent.id == "evt_a");

            // ---- EndDay is refused while an event is pending ----
            int dayBefore = run.currentDay;
            var blocked = phase.EndDay();
            Check("EndDay refused while an event is pending (day unchanged)", run.currentDay == dayBefore);
            Check("still pending after the refused EndDay", phase.HasPendingEvent);
            Check("refused EndDay reports the same pending event", blocked.presentedEvent != null && blocked.presentedEvent.id == "evt_a");

            // ---- Resolve the event → effects apply, follow-up queues, pending clears ----
            var res = phase.ResolvePendingEvent(0);
            Check("resolution valid", res.valid);
            Check("reputation applied (Cordon = +8)", rep.Get(FactionId.Cordon) == 8);
            Check("pending cleared after resolution", !phase.HasPendingEvent);
            Check("lethal follow-up queued", run.QueuedEventIds.Contains("evt_lethal"));

            // ---- Day 2: the queued follow-up fires with priority ----
            var t2 = phase.EndDay();
            Check("day advanced to 2", t2.day.newDay == 2);
            Check("queued follow-up presented with priority", t2.presentedEvent != null && t2.presentedEvent.id == "evt_lethal");

            // ---- Resolve the lethal event → crew wipes ----
            var lethal = phase.ResolvePendingEvent(0);
            Check("lethal resolution valid", lethal.valid);
            Check("crew died from the lethal outcome", lethal.crewDied);
            Check("IsWipe reports the wipe", phase.IsWipe());

            // ---- A further EndDay with no survivors reports run end ----
            var t3 = phase.EndDay();
            Check("EndDay with no survivors ends the run", t3.runEnded);

            bool allPass = _passed == _checks;
            string verdict = allPass ? "ALL PASS" : $"{_checks - _passed} FAILED";
            if (allPass) Debug.Log($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
            else Debug.LogError($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
        }

        private GameDatabase BuildInMemoryDatabase()
        {
            var ration = ScriptableObject.CreateInstance<ItemData>();
            ration.id = "item_ration";
            ration.displayName = "Ration Tin";
            ration.category = ItemCategory.Food;
            ration.weightKg = 0.4f;
            ration.durability = 100;
            ration.utilityTags = new List<UtilityTag> { UtilityTag.Eat };

            var solo = ScriptableObject.CreateInstance<CrewMemberData>();
            solo.id = "crew_solo";
            solo.displayName = "Nadia";
            solo.lastName = "Orlova";
            solo.background = CrewBackground.LonerScavenger;
            solo.baseStats = new CrewBaseStats
            {
                maxHealth = 100, maxSanity = 100, carryCapacityKg = 20f,
                sanityRecoveryMultiplier = 1f, radiationResistanceMultiplier = 1f, combatResolutionMultiplier = 1f
            };
            solo.startingTraits = new List<TraitData>();

            var evtA = ScriptableObject.CreateInstance<ExpeditionEventData>();
            evtA.id = "evt_a";
            evtA.titleKey = "evt.a.title";
            evtA.baseWeight = 1.0f;
            evtA.prerequisites = new EventPrerequisite { minDay = 1, maxDay = 0, factionContext = FactionId.None };
            evtA.choices = new List<EventChoice>
            {
                new EventChoice
                {
                    choiceLabelKey = "evt.a.choice.0",
                    successChance = 1.0f,
                    successOutcome = new OutcomeDelta
                    {
                        reputationFaction = FactionId.Cordon,
                        reputationDelta = 8,
                        followUpEventId = "evt_lethal"
                    }
                }
            };

            var evtLethal = ScriptableObject.CreateInstance<ExpeditionEventData>();
            evtLethal.id = "evt_lethal";
            evtLethal.titleKey = "evt.lethal.title";
            evtLethal.baseWeight = 0f; // only ever fires via the queue
            evtLethal.prerequisites = new EventPrerequisite { minDay = 1, maxDay = 0, factionContext = FactionId.None };
            evtLethal.choices = new List<EventChoice>
            {
                new EventChoice
                {
                    choiceLabelKey = "evt.lethal.choice.0",
                    successChance = 1.0f,
                    successOutcome = new OutcomeDelta { crewDeathChance = 1.0f }
                }
            };

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.items = new List<ItemData> { ration };
            db.crew = new List<CrewMemberData> { solo };
            db.traits = new List<TraitData>();
            db.voiceGroups = new List<VoiceLineGroup>();
            db.factions = new List<FactionData>();
            db.anomalies = new List<AnomalyData>();
            db.mutants = new List<MutantData>();
            db.events = new List<ExpeditionEventData> { evtA, evtLethal };
            db.Initialize(force: true);
            return db;
        }

        private void Check(string label, bool condition)
        {
            _checks++;
            if (condition) { _passed++; Debug.Log($"   PASS  {label}"); }
            else Debug.LogError($"   FAIL  {label}");
        }
    }
}
