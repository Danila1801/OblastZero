// Assets/_Project/Scripts/Gameplay/BunkerDayController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>Summary of what one day advance did. Returned to the state so it can decide on run-end / UI updates.</summary>
    public struct DayResult
    {
        public int newDay;
        public int crewFed;
        public int starvingCrew;
        public int deathsThisDay;
        public int aliveRemaining;
    }

    /// <summary>
    /// The bunker turn engine (Step 4). Pure C# — depends only on RunData, the managers, the database, and
    /// the EventBus, so it is fully testable without the scene or state machine. One <see cref="AdvanceDay"/>
    /// call resolves a single in-game day: item decay, ration consumption, then per-crew daily ticks
    /// (fatigue, radiation bleed from the bunker pool, sanity dread, radiation-sickness and starvation damage,
    /// and passive regen for fed/rested crew). Raises <see cref="DayAdvancedEvent"/> and autosaves the run.
    /// All crew mutation flows through CrewManager; all item mutation through InventoryManager.
    /// </summary>
    public class BunkerDayController
    {
        private readonly RunData _run;
        private readonly InventoryManager _inventory;
        private readonly CrewManager _crew;
        private readonly GameDatabase _db;
        private readonly BunkerDayConfig _config;
        private readonly ISaveService _saveService; // optional; autosave skipped if null

        public BunkerDayController(RunData run, InventoryManager inventory, CrewManager crew, GameDatabase db,
                                   BunkerDayConfig config = null, ISaveService saveService = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _crew = crew ?? throw new ArgumentNullException(nameof(crew));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _config = config ?? new BunkerDayConfig();
            _saveService = saveService;
        }

        /// <summary>
        /// Ranged missions in the field, ticked at the top of each day. Optional: a day controller
        /// built without one runs exactly as it did before expeditions existed, which is what lets the
        /// smoke tests construct it bare.
        /// </summary>
        public ExpeditionSystem.ExpeditionManager Expeditions { get; set; }

        public DayResult AdvanceDay()
        {
            int day = ++_run.currentDay;
            Debug.Log($"[BunkerDay] ──────── Advancing to day {day} ────────");

            // 0. Expeditions in the field. Resolved first, before stores decay and before the crew is
            //    fed, because a returning party changes both inputs: they bring stock into the larder
            //    the decay pass is about to walk, and they rejoin the roster the ration count is about
            //    to be computed from. Resolving them after would feed a bunker one head short and spoil
            //    food that arrived this morning.
            if (Expeditions != null)
            {
                var returned = Expeditions.TickDay();
                for (int i = 0; i < returned.Count; i++)
                    Debug.Log($"[BunkerDay] Expedition: {returned[i].crewInstanceId} — {returned[i].summary}");
            }

            // 1. Items spoil / wear.
            _inventory.ApplyDailyDecay();

            // 2. Feed the crew. Consume food units, then distribute them in roster order.
            var aliveCrew = AliveCrewSnapshot();
            int rationsNeeded = aliveCrew.Count * _config.rationsPerCrewPerDay;
            int rationsAvailable = ConsumeFood(rationsNeeded);
            int rationsBudget = rationsAvailable;

            // 3. Per-crew daily ticks. Health is resolved last so death is the final step for a member.
            int fedCount = 0;
            int deaths = 0;

            foreach (var member in aliveCrew)
            {
                string id = member.instanceId;

                bool fed = rationsBudget >= _config.rationsPerCrewPerDay;
                if (fed)
                {
                    rationsBudget -= _config.rationsPerCrewPerDay;
                    fedCount++;
                }

                // Fatigue accrues.
                if (_config.fatiguePerDay != 0)
                    _crew.ApplyFatigueDelta(id, _config.fatiguePerDay);

                // Radiation bleeds out of the bunker pool into each occupant.
                if (_run.bunkerRadiationPool > 0 && _config.radiationPoolBleedFactor > 0f)
                {
                    int radGain = Mathf.RoundToInt(_run.bunkerRadiationPool * _config.radiationPoolBleedFactor);
                    if (radGain > 0) _crew.ApplyRadiation(id, radGain);
                }

                bool radiationSick = member.currentRadiation >= _config.radiationSicknessThreshold;

                // Sanity: baseline dread, worsened by sickness and hunger.
                int sanityLoss = _config.sanityDrainPerDay;
                if (radiationSick) sanityLoss += _config.sanityDrainFromSickness;
                if (!fed) sanityLoss += _config.sanityDrainFromStarvation;
                if (sanityLoss != 0) _crew.ApplySanityDelta(id, -sanityLoss);

                // Health: sickness + starvation do damage; an otherwise-healthy fed/rested member recovers.
                int healthDelta = 0;
                if (radiationSick) healthDelta -= _config.radiationHealthDamage;
                if (!fed) healthDelta -= _config.starvationHealthDamage;
                if (healthDelta == 0 && fed && member.currentFatigue <= _config.restedFatigueCeiling)
                    healthDelta += _config.passiveHealthRegen;

                if (healthDelta != 0)
                {
                    bool wasAlive = member.isAlive;
                    _crew.ApplyHealthDelta(id, healthDelta); // may trigger death (fires CrewDied)
                    if (wasAlive && !member.isAlive) deaths++;
                }
            }

            int aliveRemaining = _crew.AliveCount();
            int starving = aliveCrew.Count - fedCount;

            // 4. Announce the day to the rest of the game.
            EventBus.Raise(new DayAdvancedEvent { NewDay = day });

            // 5. Autosave the run (bible: each day advance autosaves).
            if (_saveService != null)
            {
                _saveService.SaveExpedition(_run);
                Debug.Log($"[BunkerDay] Autosaved run '{_run.runId}' at day {day}.");
            }

            Debug.Log($"[BunkerDay] Day {day} complete. fed={fedCount} starving={starving} deaths={deaths} alive={aliveRemaining}.");

            return new DayResult
            {
                newDay = day,
                crewFed = fedCount,
                starvingCrew = starving,
                deathsThisDay = deaths,
                aliveRemaining = aliveRemaining
            };
        }

        // ---- Internals ----

        private List<CrewInstance> AliveCrewSnapshot()
        {
            var list = new List<CrewInstance>();
            foreach (var c in _crew.ActiveCrew)
                if (c.isAlive) list.Add(c);
            return list;
        }

        /// <summary>
        /// Removes up to <paramref name="unitsNeeded"/> food units from the bunker (any items in the
        /// Food category, by quantity) and returns how many were actually consumed.
        /// </summary>
        private int ConsumeFood(int unitsNeeded)
        {
            if (unitsNeeded <= 0) return 0;

            // Aggregate available food by id first (so removing across duplicate stacks isn't double-counted).
            var foodById = new Dictionary<string, int>();
            foreach (var inst in _inventory.Get(InventoryChannel.Bunker))
            {
                var data = _db.GetItem(inst.itemDataId);
                if (data == null || data.category != ItemCategory.Food) continue;
                foodById.TryGetValue(inst.itemDataId, out int running);
                foodById[inst.itemDataId] = running + inst.quantity;
            }

            int consumed = 0;
            foreach (var kvp in foodById)
            {
                if (consumed >= unitsNeeded) break;
                int take = Mathf.Min(kvp.Value, unitsNeeded - consumed);
                if (take > 0 && _inventory.RemoveItem(InventoryChannel.Bunker, kvp.Key, take))
                    consumed += take;
            }

            if (consumed > 0)
                Debug.Log($"[BunkerDay] Consumed {consumed}/{unitsNeeded} ration unit(s).");
            else if (unitsNeeded > 0)
                Debug.Log($"[BunkerDay] No food available — {unitsNeeded} ration unit(s) short.");

            return consumed;
        }
    }
}
