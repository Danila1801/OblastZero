// Assets/_Project/Scripts/Gameplay/VictoryAndResumeTest.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Guards the two Phase-2 run-lifecycle fixes: that a run can be WON, and that a run held open at an
    /// event survives a save/load without re-rolling it.
    ///
    /// <para><b>Why each half exists.</b> All four victory states were built, registered in <c>_Bootstrap</c>,
    /// and reachable by the state machine — and every one was dead code, because nothing called
    /// <c>EndCurrentRun</c> with a <c>Victory*</c> reason. A run could only end in death or a quit. Nothing in
    /// the existing suites noticed, because they assert that the day loop and event engine behave, never that
    /// the run can terminate any way other than a wipe. The second half covers the reverse-facing defect:
    /// <c>pendingEventId</c> was not serialized, so quitting at an open prompt lost it and the reload drew a
    /// different event from an already-advanced RNG stream — a free re-roll on any outcome the player
    /// disliked.</para>
    ///
    /// <para>Both halves run headless against a synthetic database. That is the right call here, unlike
    /// <see cref="BunkerEventReachabilityTest"/> which must use the shipped corpus: these are state-machine
    /// and persistence invariants, not content-reachability ones, so a fixture keeps the win matrix
    /// exhaustive and deterministic instead of dependent on which events happen to be authored.</para>
    ///
    /// USAGE: attach to an empty GameObject, right-click the component → "Run Victory And Resume Test".
    /// Needs nothing assigned and touches no save files. Ships nothing.
    /// </summary>
    public class VictoryAndResumeTest : MonoBehaviour
    {
        private int _checks;
        private int _passed;

        [ContextMenu("Run Victory And Resume Test")]
        public void Run()
        {
            _checks = 0;
            _passed = 0;
            Debug.Log("──────── VICTORY + RESUME TEST ────────");

            TestFactionEndgames();
            TestEndgameGating();
            TestIndependentEnding();
            TestWipeBeatsVictory();
            TestPendingEventSurvivesRoundTrip();
            TestMigrationOfLegacySave();

            bool green = _passed == _checks;
            string line = $"RESULT: {_passed}/{_checks} checks passed";
            if (green) Debug.Log($"──────── {line} — ALL GREEN ────────");
            else Debug.LogError($"──────── {line} — FAILURES ABOVE ────────");
        }

        // ─── Victory conditions ─────────────────────────────────────────────────────

        /// <summary>Each faction past +60 after the tenure pivot yields its own ending, and only its own.</summary>
        private void TestFactionEndgames()
        {
            int day = BalanceConstants.ENDGAME_MIN_TENURE_DAYS;
            int rep = BalanceConstants.ENDGAME_REPUTATION_THRESHOLD;

            var society = EvaluateWith(day, society: rep, cordon: 0, kafedra: 0);
            Check("Scale Society at +60 on the pivot day wins Stabilization",
                  society.Achieved && society.Reason == RunEndReason.VictoryStabilization
                                   && society.State == GameState.RunVictory_Stabilization);

            var cordon = EvaluateWith(day, society: 0, cordon: rep, kafedra: 0);
            Check("Cordon at +60 wins Relief",
                  cordon.Achieved && cordon.Reason == RunEndReason.VictoryRelief
                                  && cordon.State == GameState.RunVictory_Relief);

            var kafedra = EvaluateWith(day, society: 0, cordon: 0, kafedra: rep);
            Check("Kafedra at +60 wins Adaptation",
                  kafedra.Achieved && kafedra.Reason == RunEndReason.VictoryAdaptation
                                   && kafedra.State == GameState.RunVictory_Adaptation);

            // Two factions over the line is reachable state (nothing enforces bible §3's "at most one"),
            // so the tiebreak must be deterministic rather than whichever branch is checked first.
            var tie = EvaluateWith(day, society: rep + 5, cordon: rep + 20, kafedra: 0);
            Check("two qualifying factions award the higher reputation (Cordon 80 over Society 65)",
                  tie.Achieved && tie.Reason == RunEndReason.VictoryRelief);
        }

        /// <summary>The gates: tenure, threshold, and the fact that a live run reports no ending.</summary>
        private void TestEndgameGating()
        {
            int rep = BalanceConstants.ENDGAME_REPUTATION_THRESHOLD;

            var early = EvaluateWith(BalanceConstants.ENDGAME_MIN_TENURE_DAYS - 1,
                                     society: rep + 40, cordon: 0, kafedra: 0);
            Check("maxed reputation one day short of the pivot does NOT win",
                  !early.Achieved);

            var under = EvaluateWith(BalanceConstants.ENDGAME_MIN_TENURE_DAYS,
                                     society: rep - 1, cordon: 0, kafedra: 0);
            Check("one point under the endgame threshold does NOT win",
                  !under.Achieved);

            var day1 = EvaluateWith(1, society: 0, cordon: 0, kafedra: 0);
            Check("a fresh run reports no ending",
                  !day1.Achieved && !string.IsNullOrEmpty(day1.Explanation));
        }

        /// <summary>The neutral branch: unaligned AND unhunted, at the longer tenure.</summary>
        private void TestIndependentEnding()
        {
            int neutralDay = BalanceConstants.INDEPENDENT_MIN_TENURE_DAYS;

            var neutral = EvaluateWith(neutralDay, society: 10, cordon: -10, kafedra: 0);
            Check("unaligned and unhunted at the independent tenure wins Independent",
                  neutral.Achieved && neutral.Reason == RunEndReason.VictoryIndependent
                                   && neutral.State == GameState.RunVictory_Independent);

            var early = EvaluateWith(neutralDay - 1, society: 0, cordon: 0, kafedra: 0);
            Check("neutral one day short of the independent tenure does NOT win",
                  !early.Achieved);

            var hunted = EvaluateWith(neutralDay, society: 0, cordon: 0,
                                      kafedra: BalanceConstants.HUNTED_REPUTATION_THRESHOLD);
            Check("hunted by a faction is not independence",
                  !hunted.Achieved);

            // An aligned run at the longer tenure must resolve as that faction's ending, not the neutral one.
            var aligned = EvaluateWith(neutralDay,
                                       society: BalanceConstants.ENDGAME_REPUTATION_THRESHOLD, cordon: 0, kafedra: 0);
            Check("an aligned run past the independent tenure still wins its faction ending",
                  aligned.Achieved && aligned.Reason == RunEndReason.VictoryStabilization);
        }

        /// <summary>A run that qualifies on paper but has no living crew is a wipe, not an ending.</summary>
        private void TestWipeBeatsVictory()
        {
            var db = BuildDatabase();
            var run = BuildRun(BalanceConstants.ENDGAME_MIN_TENURE_DAYS,
                               BalanceConstants.ENDGAME_REPUTATION_THRESHOLD, 0, 0);
            var crew = new CrewManager(db);
            crew.Bind(run);
            var rep = new FactionReputationManager();
            rep.Bind(run);

            // No crew added at all — the same state a wipe leaves behind.
            var verdict = new VictoryConditionEvaluator(run, crew, rep).Evaluate();
            Check("a qualifying run with no surviving crew does NOT win",
                  !verdict.Achieved);
        }

        // ─── Pending-event persistence ──────────────────────────────────────────────

        /// <summary>
        /// The core of the re-roll fix: present an event, round-trip the run through JSON the way the
        /// autosave does, restore, and confirm the SAME event comes back without the RNG stream moving.
        /// </summary>
        private void TestPendingEventSurvivesRoundTrip()
        {
            var db = BuildDatabase();
            var run = BuildRun(day: 3, society: 0, cordon: 0, kafedra: 0);

            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);
            var rep = new FactionReputationManager();
            var engine = new EventEngine(db, inventory, crew, rep);
            BindAll(run, inventory, crew, rep, engine);
            AddOneCrewMember(db, crew);

            var presented = engine.SelectNextEvent(new[] { RegionTags.BunkerInterior });
            Check("the fixture presents an event at all", presented != null);
            if (presented == null) return;

            Check("presenting an event records it on the run",
                  run.pendingEventId == presented.id);

            int streamAfterPresent = run.rngStreamCounter;

            // Round-trip through the same serializer the save service uses. This is what the field is for:
            // an in-memory carry would have passed before the fix too.
            var reloaded = RoundTripThroughJson(run);
            Check("pendingEventId survives serialization",
                  reloaded != null && reloaded.pendingEventId == presented.id);
            if (reloaded == null) return;

            var inventory2 = new InventoryManager(db);
            var crew2 = new CrewManager(db);
            var rep2 = new FactionReputationManager();
            var engine2 = new EventEngine(db, inventory2, crew2, rep2);
            BindAll(reloaded, inventory2, crew2, rep2, engine2);

            var restored = engine2.RestorePendingEvent();
            Check("the reloaded run restores the SAME event, not a re-roll",
                  restored != null && restored.id == presented.id);
            Check("restoring draws no randomness (RNG stream unchanged)",
                  reloaded.rngStreamCounter == streamAfterPresent);

            // Resolving must clear the marker, or the next load re-presents a spent event forever.
            var resolution = engine2.Resolve(restored, 0);
            Check("resolving the restored event succeeds", resolution.valid);
            Check("resolving clears pendingEventId",
                  string.IsNullOrEmpty(reloaded.pendingEventId));

            // A pending id that names an already-resolved event must be dropped, not re-presented.
            reloaded.pendingEventId = restored.id;
            Check("a pending id already in CompletedEventIds is cleared, not re-presented",
                  engine2.RestorePendingEvent() == null && string.IsNullOrEmpty(reloaded.pendingEventId));

            // A pending id no longer in the database (content churn) must fail soft.
            reloaded.pendingEventId = "event_that_no_longer_exists";
            Check("a pending id missing from the database is cleared, not fatal",
                  engine2.RestorePendingEvent() == null && string.IsNullOrEmpty(reloaded.pendingEventId));
        }

        /// <summary>A save from before the field existed must load, be stamped, and report no pending event.</summary>
        private void TestMigrationOfLegacySave()
        {
            // Hand-built legacy payload: no saveFormatVersion, no pendingEventId, and an explicit null list
            // — the shape that overwrites a field initializer and makes every unguarded iteration throw.
            const string legacyJson =
                "{\"runId\":\"legacy\",\"currentDay\":7,\"currentScavengeSiteId\":\"site_grain_depot\"," +
                "\"BunkerInventory\":null,\"repCordon\":25,\"rngSeed\":1234,\"rngStreamCounter\":9}";

            var raw = Newtonsoft.Json.JsonConvert.DeserializeObject<RunData>(legacyJson);
            Check("a legacy save deserializes at all", raw != null);
            if (raw == null) return;

            Check("a legacy save reports version 0 before migration",
                  raw.saveFormatVersion == RunDataMigrator.LegacyUnversioned);

            var migrated = RunDataMigrator.Migrate(raw);
            Check("migration stamps the current version",
                  migrated.saveFormatVersion == RunDataMigrator.CurrentVersion);
            Check("migration leaves no pending event on a legacy save",
                  string.IsNullOrEmpty(migrated.pendingEventId));
            Check("migration repairs an explicitly-null collection",
                  migrated.BunkerInventory != null);
            Check("migration preserves run state it does not own (day, rep, RNG)",
                  migrated.currentDay == 7 && migrated.repCordon == 25 && migrated.rngStreamCounter == 9);

            // Idempotence: re-running must not double-apply or renumber.
            var again = RunDataMigrator.Migrate(migrated);
            Check("migration is idempotent",
                  again.saveFormatVersion == RunDataMigrator.CurrentVersion && again.currentDay == 7);

            Check("Migrate(null) returns null rather than throwing",
                  RunDataMigrator.Migrate(null) == null);
        }

        // ─── Fixtures ───────────────────────────────────────────────────────────────

        private VictoryVerdict EvaluateWith(int day, int society, int cordon, int kafedra)
        {
            var db = BuildDatabase();
            var run = BuildRun(day, society, cordon, kafedra);

            var crew = new CrewManager(db);
            crew.Bind(run);
            var rep = new FactionReputationManager();
            rep.Bind(run);

            AddOneCrewMember(db, crew);

            return new VictoryConditionEvaluator(run, crew, rep).Evaluate();
        }

        private static RunData BuildRun(int day, int society, int cordon, int kafedra) => new RunData
        {
            runId = "victorytest",
            currentDay = day,
            currentScavengeSiteId = "site_grain_depot",
            repScaleSociety = society,
            repCordon = cordon,
            repKafedra = kafedra,
            rngSeed = 20260805,
            rngStreamCounter = 0,
            bunkerMorale = BalanceConstants.STARTING_BUNKER_MORALE,
        };

        private static void BindAll(RunData run, InventoryManager inventory, CrewManager crew,
                                    FactionReputationManager rep, EventEngine engine)
        {
            inventory.Bind(run);
            crew.Bind(run);
            rep.Bind(run);
            engine.Bind(run);
        }

        /// <summary>
        /// Puts one living member on the roster. Every ending asserts the crew survived, so a fixture without
        /// one can only ever produce the wipe verdict.
        /// </summary>
        private static void AddOneCrewMember(GameDatabase db, CrewManager crew)
        {
            string id = null;
            var roster = db.AllCrew;
            if (roster != null)
                foreach (var member in roster)
                    if (member != null && !string.IsNullOrEmpty(member.id)) { id = member.id; break; }

            if (id == null) return;
            crew.AddRescued(id);
            crew.CommitRescuedToBunker();
        }

        /// <summary>
        /// Serializes and deserializes through Newtonsoft with the same intent as SaveService: the point is to
        /// prove the field crosses a real save boundary, not an in-memory copy.
        /// </summary>
        private static RunData RoundTripThroughJson(RunData run)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(run);
            return RunDataMigrator.Migrate(Newtonsoft.Json.JsonConvert.DeserializeObject<RunData>(json));
        }

        /// <summary>
        /// Minimal in-memory database: one crew member, one food item, one bunker-interior event with a single
        /// always-available choice. Built in code so the test carries no asset dependencies.
        /// </summary>
        private static GameDatabase BuildDatabase()
        {
            var db = ScriptableObject.CreateInstance<GameDatabase>();

            var crewMember = ScriptableObject.CreateInstance<CrewMemberData>();
            crewMember.id = "crew_test_operator";
            crewMember.displayName = "Operator";
            crewMember.baseStats.carryCapacityKg = 20f;
            db.crew = new List<CrewMemberData> { crewMember };

            var ration = ScriptableObject.CreateInstance<ItemData>();
            ration.id = "item_test_ration";
            ration.displayName = "Ration Tin";
            ration.category = ItemCategory.Food;
            db.items = new List<ItemData> { ration };

            var evt = ScriptableObject.CreateInstance<ExpeditionEventData>();
            evt.id = "event_test_pending";
            evt.displayName = "Registered Deviation";
            evt.baseWeight = 1f;
            evt.prerequisites = new EventPrerequisite
            {
                regionTagsAny = new List<string> { RegionTags.BunkerInterior },
            };
            evt.choices = new List<EventChoice>
            {
                new EventChoice { successChance = 1f },
            };
            db.events = new List<ExpeditionEventData> { evt };

            db.Initialize();
            return db;
        }

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
    }
}
