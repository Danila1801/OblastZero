// Assets/_Project/Scripts/Gameplay/BunkerEventReachabilityTest.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Guards the bunker day loop's ability to present events AT ALL, against the real shipped content.
    ///
    /// This exists because a total content blackout shipped undetected: SurvivalPhase2DState called
    /// BunkerPhaseController.EndDay() with no region tags, EventEngine rejects any event carrying
    /// regionTagsAny when the caller supplies none, and every shipped event carries them — so the whole
    /// Phase-2 narrative layer selected nothing, every day, for every run.
    ///
    /// The existing smoke tests could not catch it, and still cannot: they build small synthetic databases
    /// whose events have no regionTagsAny, so a bare EndDay() passes there and fails in the game. The only
    /// test that can catch this class of bug is one that runs the selector against the authored corpus. That
    /// is what this does — it asserts reachability, not behaviour, and deliberately overlaps no other suite.
    ///
    /// USAGE: attach to an empty GameObject, right-click the component → "Run Bunker Event Reachability Test".
    /// Needs the real database: assign it, or run in Play mode where GameManager supplies it. Ships nothing.
    ///
    /// EXPECTED CONSOLE ERROR: one check deliberately calls SelectNextEvent(null) to pin the fail-closed
    /// behaviour, which trips EventEngine's blackout diagnostic by design. A single
    /// "[EventEngine] No events eligible and NO region tags were supplied..." error during this test is the
    /// diagnostic working, not a failure — read the RESULT line, not the error count.
    /// </summary>
    public class BunkerEventReachabilityTest : MonoBehaviour
    {
        [Tooltip("The real content database. Left empty, the test borrows GameManager.Instance.Database.")]
        [SerializeField] private GameDatabase database;

        /// <summary>Floor on how much of the corpus a bunker day must be able to reach, as a fraction.</summary>
        private const float MinBunkerReachableFraction = 0.5f;

        private int _checks;
        private int _passed;

        [ContextMenu("Run Bunker Event Reachability Test")]
        public void Run()
        {
            _checks = 0;
            _passed = 0;
            Debug.Log("──────── BUNKER EVENT REACHABILITY TEST ────────");

            var db = database != null ? database
                   : (GameManager.Instance != null ? GameManager.Instance.Database : null);
            if (db == null)
            {
                Debug.LogError("[BunkerEventReachabilityTest] No GameDatabase. Assign one, or run in Play mode " +
                               "so GameManager.Instance.Database is available. Test skipped.");
                return;
            }

            db.Initialize();
            var all = db.AllEvents;
            if (all == null || all.Count == 0)
            {
                Debug.LogError("[BunkerEventReachabilityTest] The database reports no events — nothing to verify.");
                return;
            }

            // ---- The corpus is entirely region-gated, which is what makes a null tag set fatal ----
            int tagged = 0;
            var vocabulary = new HashSet<string>();
            foreach (var evt in all)
            {
                if (evt == null) continue;
                var tags = evt.prerequisites.regionTagsAny;
                if (tags == null || tags.Count == 0) continue;
                tagged++;
                foreach (var tag in tags)
                    if (!string.IsNullOrEmpty(tag)) vocabulary.Add(tag);
            }
            Check($"every event is region-gated ({tagged}/{all.Count})", tagged == all.Count);

            // ---- RegionTags must know every tag the content actually uses ----
            var known = new HashSet<string>(RegionTags.All);
            var unknown = new List<string>();
            foreach (var tag in vocabulary)
                if (!known.Contains(tag)) unknown.Add(tag);
            Check("RegionTags.All covers the authored vocabulary" +
                  (unknown.Count > 0 ? $" (unknown: {string.Join(", ", unknown)})" : ""),
                  unknown.Count == 0);

            // ---- The bunker's active set must reach a real share of the corpus ----
            var active = new HashSet<string>(RegionTags.BunkerPhaseActive);
            int reachable = 0;
            foreach (var evt in all)
            {
                if (evt == null) continue;
                var tags = evt.prerequisites.regionTagsAny;
                if (tags == null) continue;
                foreach (var tag in tags)
                    if (tag != null && active.Contains(tag)) { reachable++; break; }
            }
            float fraction = (float)reachable / all.Count;
            Check($"bunker tags reach >= {MinBunkerReachableFraction:P0} of the corpus ({reachable}/{all.Count}, {fraction:P0})",
                  fraction >= MinBunkerReachableFraction);

            // ---- The selector itself, against the real corpus ----
            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);
            var rep = new FactionReputationManager();
            var engine = new EventEngine(db, inventory, crew, rep);

            var run = new RunData
            {
                runId = System.Guid.NewGuid().ToString("N"),
                runStartedUtc = System.DateTime.UtcNow,
                currentDay = 1,
                rngSeed = 4242
            };
            inventory.Bind(run);
            crew.Bind(run);
            rep.Bind(run);
            engine.Bind(run);

            var lead = FirstCrewId(db);
            if (!string.IsNullOrEmpty(lead))
            {
                crew.AddRescued(lead);
                crew.CommitRescuedToBunker();
            }
            Check("a crew member is on strength for the day tick", crew.AliveCount() > 0);

            // The regression this whole file exists for: with the bunker's tags, a day yields an event.
            var withTags = engine.SelectNextEvent(RegionTags.BunkerPhaseActive);
            Check("SelectNextEvent(BunkerPhaseActive) returns an event", withTags != null);

            // And the fail-closed behaviour that made the omission fatal, pinned so a future change to it
            // is a deliberate decision rather than a silent one.
            var withoutTags = engine.SelectNextEvent(null);
            Check("SelectNextEvent(null) selects nothing from a fully region-gated corpus", withoutTags == null);

            // ---- The full turn, driven the way SurvivalPhase2DState drives it ----
            inventory.AddItem(InventoryChannel.Bunker, FirstFoodId(db) ?? "item_canned_meat", 30);
            var dayController = new BunkerDayController(run, inventory, crew, db, new BunkerDayConfig(), saveService: null);
            var phase = new BunkerPhaseController(dayController, engine, crew);

            var turn = phase.EndDay(RegionTags.BunkerPhaseActive);
            Check("EndDay(BunkerPhaseActive) advanced the day", turn.day.newDay > 1);
            Check("EndDay(BunkerPhaseActive) presented an event", turn.presentedEvent != null);
            Check("controller holds the presented event", phase.HasPendingEvent);

            Debug.Log($"──────── RESULT: {_passed}/{_checks} — {(_passed == _checks ? "ALL PASS" : "FAILURES PRESENT")} ────────");
        }

        /// <summary>First authored crew id, so the test does not hardcode roster content.</summary>
        private static string FirstCrewId(GameDatabase db)
        {
            var crew = db.AllCrew;
            if (crew == null) return null;
            foreach (var member in crew)
                if (member != null && !string.IsNullOrEmpty(member.id)) return member.id;
            return null;
        }

        /// <summary>
        /// First item in the Food category — the same test BunkerDayController.ConsumeFood applies — so the
        /// day tick can feed the crew without this test hardcoding an item id that content churn may retire.
        /// </summary>
        private static string FirstFoodId(GameDatabase db)
        {
            var items = db.AllItems;
            if (items == null) return null;
            foreach (var item in items)
                if (item != null && item.category == ItemCategory.Food && !string.IsNullOrEmpty(item.id))
                    return item.id;
            return null;
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
