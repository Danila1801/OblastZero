// Assets/_Project/Scripts/Gameplay/EventEngineSmokeTest.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// End-to-end verification for the Event Engine (Step 5). Builds an in-memory database with a branching
    /// event, a follow-up, a second weighted event, and a crew member with a trait, then drives the engine
    /// through selection → choice gating → formula evaluation (+ fallback) → resolution, asserting every
    /// effect path lands: reputation via FactionReputationManager, loot/loss via InventoryManager, follow-up
    /// queueing, completed-event book-keeping, and deterministic seed-reproducible selection.
    ///
    /// USAGE: attach to an empty GameObject, right-click the component header → "Run Event Engine Test".
    /// Read the result in the Console. Deletable after you've seen it pass — it ships nothing.
    /// </summary>
    public class EventEngineSmokeTest : MonoBehaviour
    {
        private int _checks;
        private int _passed;

        [ContextMenu("Run Event Engine Test")]
        public void Run()
        {
            _checks = 0;
            _passed = 0;
            Debug.Log("──────── EVENT ENGINE TEST ────────");

            var db = BuildInMemoryDatabase();
            var inventory = new InventoryManager(db);
            var crew = new CrewManager(db);
            var rep = new FactionReputationManager();
            var engine = new EventEngine(db, inventory, crew, rep);

            var run = new RunData
            {
                runId = System.Guid.NewGuid().ToString("N"),
                runStartedUtc = System.DateTime.UtcNow,
                currentDay = 5,
                rngSeed = 999
            };
            inventory.Bind(run);
            crew.Bind(run);
            rep.Bind(run);
            engine.Bind(run);

            var marina = crew.AddRescued("crew_marina");
            crew.CommitRescuedToBunker();
            string marinaId = marina.instanceId;

            // ---- RunRng is deterministic from (seed, counter) ----
            {
                var a = new RunRng(new RunData { rngSeed = 42, rngStreamCounter = 0 });
                var b = new RunRng(new RunData { rngSeed = 42, rngStreamCounter = 0 });
                bool same = true;
                for (int i = 0; i < 8; i++) if (a.NextUInt() != b.NextUInt()) same = false;
                Check("RunRng reproduces the same stream from the same seed+counter", same);
            }

            // ---- Selection is deterministic and honours prerequisites ----
            int c0 = run.rngStreamCounter;
            var firstPick = engine.SelectNextEvent();
            run.rngStreamCounter = c0; // rewind the stream
            var secondPick = engine.SelectNextEvent();
            Check("weighted selection returns an eligible event", firstPick != null);
            Check("selection is deterministic for the same stream position",
                  firstPick != null && secondPick != null && firstPick.id == secondPick.id);
            Check("zero-weight follow-up is never randomly selected", firstPick != null && firstPick.id != "evt_follow");

            var primary = db.GetEvent("evt_primary");

            // ---- Choice gating by trait ----
            Check("comply choice (no trait gate) is available", engine.IsChoiceAvailable(primary.choices[0], marinaId));
            Check("ambush choice (requires missing trait) is unavailable", !engine.IsChoiceAvailable(primary.choices[2], marinaId));
            var available = engine.AvailableChoiceIndices(primary, marinaId);
            Check("gated choice is excluded from available indices", !available.Contains(2) && available.Contains(0));

            // ---- Resolve the 'comply' branch (guaranteed success) and verify every effect ----
            var comply = engine.Resolve(primary, 0, marinaId);
            Check("comply resolution is valid", comply.valid);
            Check("comply succeeded (chance 1.0)", comply.success);
            Check("reputation applied via manager (ScaleSociety = +10)", rep.Get(FactionId.ScaleSociety) == 10);
            Check("reputation delta reported on resolution", comply.reputationDeltaApplied == 10);
            Check("loot granted into bunker (tushonka present)", BunkerQty(inventory, "item_food_tushonka") >= 2);
            Check("loot ids reported on resolution", comply.lootAddedItemIds.Contains("item_food_tushonka"));
            Check("follow-up queued", run.QueuedEventIds.Contains("evt_follow"));
            Check("follow-up reported on resolution", comply.followUpQueued == "evt_follow");
            Check("primary event marked completed", run.CompletedEventIds.Contains("evt_primary"));
            Check("marina took the sanity hit (-5)", crew.GetMember(marinaId).currentSanity == 95);

            // ---- Formula path: chance computed from crew stats ----
            var formulaRes = engine.Resolve(primary, 1, marinaId);
            // marina combat = combatResolutionMultiplier(1) * 50 = 50 -> 0.2 + 0.5*(50/100) = 0.45
            Check("successChanceFormula evaluated against crew stats (~0.45)", Approx(formulaRes.chanceUsed, 0.45f, 0.001f));

            // ---- Formula fallback: a bad formula falls back to the static chance, never crashes ----
            var fallbackRes = engine.Resolve(primary, 3, marinaId);
            Check("malformed formula falls back to static successChance (0.9)", Approx(fallbackRes.chanceUsed, 0.9f, 0.001f));

            // ---- Resolving a trait-gated choice is rejected ----
            var rejected = engine.Resolve(primary, 2, marinaId);
            Check("resolving an unavailable (gated) choice is rejected", !rejected.valid);

            // ---- Completed events are filtered out of future selection ----
            run.rngStreamCounter = 0;
            var afterPrimary = engine.SelectNextEvent();
            Check("a completed primary is not re-selected", afterPrimary == null || afterPrimary.id != "evt_primary");

            // ---- Queued follow-up fires next regardless of its zero weight ----
            // (evt_secondary may also be eligible; force the follow-up by exhausting secondary from the pool
            //  is unnecessary — the queue path takes strict priority over the weighted pool.)
            var queued = engine.SelectNextEvent();
            Check("queued follow-up is presented with priority over the weighted pool",
                  queued != null && queued.id == "evt_follow");

            var followRes = engine.Resolve(queued, 0, marinaId);
            Check("follow-up resolves and stacks reputation (ScaleSociety = 15)",
                  followRes.valid && rep.Get(FactionId.ScaleSociety) == 15);
            Check("resolved follow-up is removed from the queue", !run.QueuedEventIds.Contains("evt_follow"));

            // ---- Verdict ----
            bool allPass = _passed == _checks;
            string verdict = allPass ? "ALL PASS" : $"{_checks - _passed} FAILED";
            if (allPass) Debug.Log($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
            else Debug.LogError($"──────── RESULT: {_passed}/{_checks} — {verdict} ────────");
        }

        // ---- Fixtures ----

        private GameDatabase BuildInMemoryDatabase()
        {
            var tushonka = ScriptableObject.CreateInstance<ItemData>();
            tushonka.id = "item_food_tushonka";
            tushonka.displayName = "Tushonka Tin";
            tushonka.category = ItemCategory.Food;
            tushonka.weightKg = 0.4f;
            tushonka.durability = 100;
            tushonka.utilityTags = new List<UtilityTag> { UtilityTag.Eat };

            var pistol = ScriptableObject.CreateInstance<ItemData>();
            pistol.id = "item_pistol_pm";
            pistol.displayName = "Makarov PM";
            pistol.category = ItemCategory.Weapon;
            pistol.weightKg = 0.7f;
            pistol.durability = 100;
            pistol.utilityTags = new List<UtilityTag> { UtilityTag.Fight };

            var steady = ScriptableObject.CreateInstance<TraitData>();
            steady.id = "trait_steady";
            steady.displayName = "Steady";

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
            marina.startingTraits = new List<TraitData> { steady };

            var primary = ScriptableObject.CreateInstance<ExpeditionEventData>();
            primary.id = "evt_primary";
            primary.titleKey = "evt.primary.title";
            primary.baseWeight = 1.0f;
            primary.prerequisites = new EventPrerequisite { minDay = 1, maxDay = 0, factionContext = FactionId.None };
            primary.choices = new List<EventChoice>
            {
                // 0 — comply: guaranteed, grants rep + loot + follow-up
                new EventChoice
                {
                    choiceLabelKey = "comply",
                    successChance = 1.0f,
                    successOutcome = new OutcomeDelta
                    {
                        sanityDelta = -5,
                        reputationFaction = FactionId.ScaleSociety,
                        reputationDelta = 10,
                        lootGained = new List<WeightedItem>
                        {
                            new WeightedItem { item = tushonka, dropChance = 1.0f, minQty = 2, maxQty = 3 }
                        },
                        followUpEventId = "evt_follow"
                    }
                },
                // 1 — formula-driven success chance
                new EventChoice
                {
                    choiceLabelKey = "negotiate",
                    successChance = 0.7f,
                    successChanceFormula = "0.2 + 0.5 * (crew.combat / 100)",
                    successOutcome = new OutcomeDelta { fatigueDelta = 5 },
                    failureOutcome = new OutcomeDelta { sanityDelta = -10 }
                },
                // 2 — trait-gated (crew lacks trait_ex_cordon)
                new EventChoice
                {
                    choiceLabelKey = "ambush",
                    requiredTraitsAny = new List<string> { "trait_ex_cordon" },
                    successChance = 0.5f,
                    successOutcome = new OutcomeDelta { lootGained = new List<WeightedItem> { new WeightedItem { item = pistol, dropChance = 1f, minQty = 1, maxQty = 1 } } }
                },
                // 3 — malformed formula, must fall back to static chance
                new EventChoice
                {
                    choiceLabelKey = "bluff",
                    successChance = 0.9f,
                    successChanceFormula = "crew.bogus_variable / 2",
                    successOutcome = new OutcomeDelta { }
                }
            };

            var secondary = ScriptableObject.CreateInstance<ExpeditionEventData>();
            secondary.id = "evt_secondary";
            secondary.titleKey = "evt.secondary.title";
            secondary.baseWeight = 1.0f;
            secondary.prerequisites = new EventPrerequisite { minDay = 1, maxDay = 0, factionContext = FactionId.None };
            secondary.choices = new List<EventChoice>
            {
                new EventChoice { choiceLabelKey = "ignore", successChance = 1.0f, successOutcome = new OutcomeDelta { sanityDelta = -1 } }
            };

            var follow = ScriptableObject.CreateInstance<ExpeditionEventData>();
            follow.id = "evt_follow";
            follow.titleKey = "evt.follow.title";
            follow.baseWeight = 0f; // only ever fires via the queue
            follow.prerequisites = new EventPrerequisite { minDay = 1, maxDay = 0, factionContext = FactionId.None };
            follow.choices = new List<EventChoice>
            {
                new EventChoice
                {
                    choiceLabelKey = "acknowledge",
                    successChance = 1.0f,
                    successOutcome = new OutcomeDelta { reputationFaction = FactionId.ScaleSociety, reputationDelta = 5 }
                }
            };

            var db = ScriptableObject.CreateInstance<GameDatabase>();
            db.items = new List<ItemData> { tushonka, pistol };
            db.crew = new List<CrewMemberData> { marina };
            db.traits = new List<TraitData> { steady };
            db.voiceGroups = new List<VoiceLineGroup>();
            db.factions = new List<FactionData>();
            db.anomalies = new List<AnomalyData>();
            db.mutants = new List<MutantData>();
            db.events = new List<ExpeditionEventData> { primary, secondary, follow };
            db.Initialize(force: true);
            return db;
        }

        // ---- Assert + helpers ----

        private void Check(string label, bool condition)
        {
            _checks++;
            if (condition) { _passed++; Debug.Log($"   PASS  {label}"); }
            else Debug.LogError($"   FAIL  {label}");
        }

        private static bool Approx(float a, float b, float eps) => Mathf.Abs(a - b) <= eps;

        private static int BunkerQty(InventoryManager inv, string id)
        {
            int q = 0;
            foreach (var i in inv.Get(InventoryChannel.Bunker)) if (i.itemDataId == id) q += i.quantity;
            return q;
        }
    }
}
