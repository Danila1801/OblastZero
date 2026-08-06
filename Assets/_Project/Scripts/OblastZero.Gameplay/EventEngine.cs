// Assets/_Project/Scripts/Gameplay/EventEngine.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Full report of one resolved event choice — what was rolled and every effect that landed. Returned by
    /// <see cref="EventEngine.Resolve(ExpeditionEventData,int,string)"/> and pushed on
    /// <see cref="EventEngine.EventResolved"/>. <see cref="valid"/> is false when resolution was rejected
    /// (bad index, trait-gated choice, missing event) and no effects were applied.
    /// </summary>
    public struct EventResolution
    {
        public bool valid;
        public string eventId;
        public int choiceIndex;
        public bool success;
        public string actingCrewInstanceId;
        public float chanceUsed;   // the success probability actually used (post-formula, clamped)
        public float rollValue;    // the RNG draw compared against chanceUsed
        public FactionId reputationFaction;
        public int reputationDeltaApplied; // post-clamp actual change
        public bool crewDied;
        public string diedCrewInstanceId;
        public string followUpQueued;
        public List<string> lootAddedItemIds;
        public List<string> itemsLostItemIds;

        /// <summary>
        /// True when a Carbon Copy duplicate (ANM-Δ-07/CC) was the unit consumed by this resolution. The
        /// UI reads this to explain an outcome that otherwise looks like the dice betraying the player.
        /// </summary>
        public bool defectiveItemUsed;

        /// <summary>True when a Margin Note bought this resolution a second draw.</summary>
        public bool marginNoteRerolled;

        /// <summary>True when a Stamped Tongue turned this resolution's failure into a success.</summary>
        public bool stampedTongueOverrode;

        /// <summary>What the defect did, in the Oblast's own register. Empty when none was used.</summary>
        public string defectSummary;

        public static EventResolution Invalid(string eventId, int choiceIndex) => new EventResolution
        {
            valid = false,
            eventId = eventId,
            choiceIndex = choiceIndex,
            lootAddedItemIds = new List<string>(),
            itemsLostItemIds = new List<string>()
        };
    }

    /// <summary>
    /// The Phase-2 narrative core. Loads <see cref="ExpeditionEventData"/> from the <see cref="GameDatabase"/>,
    /// selects which event to present given the run's state (day, faction reputation, crew traits, held items,
    /// active region), gates each branch's availability by traits, then resolves the player's chosen branch:
    /// rolls success (static <c>successChance</c> or a <c>successChanceFormula</c> evaluated against the acting
    /// crew member), and applies the resulting <see cref="OutcomeDelta"/>.
    ///
    /// This is the single owner of <see cref="RunData.CompletedEventIds"/> and
    /// <see cref="RunData.QueuedEventIds"/>. Every OTHER effect is delegated to the owning manager:
    /// crew stats/death → <see cref="CrewManager"/>, loot/loss → <see cref="InventoryManager"/>, reputation →
    /// <see cref="FactionReputationManager"/>. All randomness flows through <see cref="RunRng"/> so resolution
    /// is seed-reproducible and survives save/load.
    ///
    /// Plain C# class, EventBus-free by design — <see cref="ManagerEventBridge"/> translates
    /// <see cref="EventPresented"/> / <see cref="EventResolved"/> onto the global bus.
    ///
    /// Crew targeting: stat deltas / death target the acting crew member when one is supplied; when it is null
    /// (a bunker-wide event with no single actor) stat deltas apply to every alive crew member and a death
    /// roll picks one alive member at random. Loot, item loss, reputation and follow-ups are always global.
    /// </summary>
    public class EventEngine
    {
        private readonly GameDatabase _db;
        private readonly InventoryManager _inventory;
        private readonly CrewManager _crew;
        private readonly FactionReputationManager _rep;

        private RunData _run;
        private RunRng _rng;

        public event Action<ExpeditionEventData> EventPresented;
        public event Action<EventResolution> EventResolved;

        /// <summary>
        /// The artifact layer, or null when none is wired. Two of the four bible artifacts modify a
        /// resolution — the Margin Note buys a second draw, the Stamped Tongue overrides a Scale
        /// Society failure — so the engine has to ask before it commits an outcome.
        ///
        /// <para>A settable property rather than a constructor argument, because
        /// <c>ArtifactSystem</c> is constructed from the inventory and crew managers this engine also
        /// holds, and a mutual constructor dependency has no valid ordering. Null is fully supported:
        /// every artifact hook is guarded, so an engine built without one resolves exactly as it did
        /// before artifacts existed. That is what keeps the smoke tests constructing it bare.</para>
        /// </summary>
        public ArtifactSystem Artifacts { get; set; }

        public EventEngine(GameDatabase db, InventoryManager inventory, CrewManager crew, FactionReputationManager rep)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _crew = crew ?? throw new ArgumentNullException(nameof(crew));
            _rep = rep ?? throw new ArgumentNullException(nameof(rep));
        }

        /// <summary>Point the engine at the active run. Call on new run and after load.</summary>
        public void Bind(RunData run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _rng = new RunRng(run);
            Debug.Log($"[EventEngine] Bound to run '{run.runId}'.");
        }

        // ─── Selection ────────────────────────────────────────────────────────────

        /// <summary>
        /// Picks the next event to present, or null if nothing is eligible. Scripted follow-ups queued by
        /// earlier outcomes take priority (in queue order); otherwise a weighted-random draw over every event
        /// whose prerequisites pass. Fires <see cref="EventPresented"/> for the returned event.
        /// </summary>
        public ExpeditionEventData SelectNextEvent(IReadOnlyCollection<string> regionTags = null,
                                                   string actingCrewInstanceId = null,
                                                   IReadOnlyCollection<string> oblastRegions = null)
        {
            if (!Ready(nameof(SelectNextEvent))) return null;

            var regionSet = regionTags != null && regionTags.Count > 0 ? new HashSet<string>(regionTags) : null;
            var oblastSet = oblastRegions != null && oblastRegions.Count > 0 ? new HashSet<string>(oblastRegions) : null;

            // 1. Scripted follow-ups fire first, regardless of weight or prerequisites (they were earned).
            var followUp = PeekEligibleFollowUp();
            if (followUp != null)
            {
                Debug.Log($"[EventEngine] Presenting queued follow-up '{followUp.id}'.");
                MarkPending(followUp.id);
                EventPresented?.Invoke(followUp);
                return followUp;
            }

            // 2. Weighted random over all eligible events.
            var pool = new List<ExpeditionEventData>();
            float totalWeight = 0f;
            foreach (var evt in _db.AllEvents)
            {
                if (evt == null || evt.baseWeight <= 0f) continue;
                if (!PassesPrerequisites(evt, regionSet, oblastSet, actingCrewInstanceId)) continue;
                pool.Add(evt);
                totalWeight += evt.baseWeight;
            }

            if (pool.Count == 0 || totalWeight <= 0f)
            {
                ReportEmptyPool(regionSet);
                MarkPending(null);
                return null;
            }

            float roll = (float)(_rng.NextDouble() * totalWeight);
            float cursor = 0f;
            ExpeditionEventData chosen = pool[pool.Count - 1]; // guard against float drift on the last bucket
            foreach (var evt in pool)
            {
                cursor += evt.baseWeight;
                if (roll < cursor) { chosen = evt; break; }
            }

            Debug.Log($"[EventEngine] Presenting '{chosen.id}' (pool {pool.Count}, weight {chosen.baseWeight:0.##}/{totalWeight:0.##}).");
            MarkPending(chosen.id);
            EventPresented?.Invoke(chosen);
            return chosen;
        }

        // ─── Pending-event persistence ──────────────────────────────────────────────

        /// <summary>
        /// Records which event is awaiting a choice on the run itself, so the autosave that fires on the day
        /// tick carries it. Null clears it.
        ///
        /// <para>The engine is the owner: it is already the single writer of
        /// <see cref="RunData.CompletedEventIds"/> and <see cref="RunData.QueuedEventIds"/>, and a pending
        /// event is the same class of state. Putting it on <c>BunkerPhaseController</c> instead would put a
        /// second writer on RunData's event fields, against the standing rule that each field has one owner.</para>
        /// </summary>
        private void MarkPending(string eventId)
        {
            if (_run == null) return;
            _run.pendingEventId = eventId;
        }

        /// <summary>
        /// Re-presents the event a reloaded run was holding open, or null when it was holding none.
        ///
        /// <para>Deliberately draws no randomness. That is the entire fix: <see cref="SelectNextEvent"/>
        /// advances the RNG stream, so re-selecting on resume handed the player a *different* event and made
        /// quit-and-reload a free re-roll on any prompt they did not like. Restoring by id reproduces the
        /// event they were actually looking at and leaves <see cref="RunData.rngStreamCounter"/> alone.</para>
        ///
        /// <para>Fires <see cref="EventPresented"/> so the modal re-opens through the same path a fresh
        /// presentation uses; the UI cannot tell the two apart, which is what it should not have to.</para>
        /// </summary>
        public ExpeditionEventData RestorePendingEvent()
        {
            if (!Ready(nameof(RestorePendingEvent))) return null;

            string id = _run.pendingEventId;
            if (string.IsNullOrEmpty(id)) return null;

            // An event already resolved cannot be pending. Reachable if a save was written between the
            // resolution and the clear, or if content was renumbered under an existing save.
            if (_run.CompletedEventIds.Contains(id))
            {
                Debug.LogWarning($"[EventEngine] Pending event '{id}' is already in CompletedEventIds — " +
                                 "clearing it rather than re-presenting a resolved event.");
                MarkPending(null);
                return null;
            }

            var evt = _db.GetEvent(id);
            if (evt == null)
            {
                Debug.LogWarning($"[EventEngine] Pending event '{id}' is not in the database (content changed " +
                                 "since the save?). Clearing it; the next day advance draws normally.");
                MarkPending(null);
                return null;
            }

            Debug.Log($"[EventEngine] Restored pending event '{id}' from the save — no RNG draw, " +
                      $"stream still at {_run.rngStreamCounter}.");
            EventPresented?.Invoke(evt);
            return evt;
        }

        /// <summary>
        /// Explains an empty selection pool instead of stating it.
        ///
        /// "No eligible events" is a legitimate result on a quiet day, and for a long time it was logged as one
        /// routine line. That made it indistinguishable from the pathological case: a caller that passes no
        /// region tags rejects every event carrying <c>regionTagsAny</c>, and since all shipped events carry
        /// them, the whole narrative layer went dark for entire runs while the console said something that
        /// read like normal operation. A blackout across a corpus this large is never quiet news, so it is
        /// reported as an error naming the cause, while a genuinely narrow day stays a Log line.
        /// </summary>
        private void ReportEmptyPool(HashSet<string> regionSet)
        {
            int corpus = _db.AllEvents?.Count ?? 0;

            if (regionSet == null && corpus > 0)
            {
                int tagged = 0;
                foreach (var evt in _db.AllEvents)
                {
                    if (evt == null) continue;
                    var tags = evt.prerequisites.regionTagsAny;
                    if (tags != null && tags.Count > 0) tagged++;
                }

                if (tagged > 0)
                {
                    Debug.LogError($"[EventEngine] No events eligible and NO region tags were supplied, while " +
                                   $"{tagged} of {corpus} events require one. Every tagged event is rejected on a " +
                                   "null tag set, so this selects nothing every time it is called. The caller must " +
                                   "pass its active tags (bunker days use RegionTags.BunkerPhaseActive).");
                    return;
                }
            }

            Debug.Log($"[EventEngine] No eligible events for the current state " +
                      $"(corpus {corpus}, day {_run.currentDay}, " +
                      $"tags {(regionSet == null ? "none" : string.Join("/", regionSet))}).");
        }

        /// <summary>First queued follow-up that still exists and hasn't been completed (non-destructive).</summary>
        private ExpeditionEventData PeekEligibleFollowUp()
        {
            for (int i = 0; i < _run.QueuedEventIds.Count; i++)
            {
                string id = _run.QueuedEventIds[i];
                if (string.IsNullOrEmpty(id) || _run.CompletedEventIds.Contains(id)) continue;
                var evt = _db.GetEvent(id);
                if (evt != null) return evt;
            }
            return null;
        }

        // ─── Choice availability ────────────────────────────────────────────────────

        /// <summary>Indices of the choices the acting crew (or the whole roster) may currently pick.</summary>
        public List<int> AvailableChoiceIndices(ExpeditionEventData evt, string actingCrewInstanceId)
        {
            var result = new List<int>();
            if (evt?.choices == null) return result;
            for (int i = 0; i < evt.choices.Count; i++)
                if (IsChoiceAvailable(evt.choices[i], actingCrewInstanceId)) result.Add(i);
            return result;
        }

        /// <summary>A choice is available when a required trait (if any) is present and no blocking trait is.</summary>
        public bool IsChoiceAvailable(EventChoice choice, string actingCrewInstanceId)
        {
            var traits = CandidateTraitSet(actingCrewInstanceId);

            if (choice.requiredTraitsAny != null && choice.requiredTraitsAny.Count > 0)
            {
                bool any = false;
                foreach (var t in choice.requiredTraitsAny)
                    if (!string.IsNullOrEmpty(t) && traits.Contains(t)) { any = true; break; }
                if (!any) return false;
            }

            if (choice.blockedByTraits != null)
            {
                foreach (var t in choice.blockedByTraits)
                    if (!string.IsNullOrEmpty(t) && traits.Contains(t)) return false;
            }

            return true;
        }

        // ─── Resolution ─────────────────────────────────────────────────────────────

        /// <summary>Resolves a choice by event id (looked up in the database).</summary>
        public EventResolution Resolve(string eventId, int choiceIndex, string actingCrewInstanceId = null)
        {
            if (!Ready(nameof(Resolve))) return EventResolution.Invalid(eventId, choiceIndex);

            var evt = _db.GetEvent(eventId);
            if (evt == null)
            {
                Debug.LogWarning($"[EventEngine] Resolve: no event '{eventId}'.");
                return EventResolution.Invalid(eventId, choiceIndex);
            }
            return Resolve(evt, choiceIndex, actingCrewInstanceId);
        }

        /// <summary>Rolls the outcome for a choice and applies every effect. The core resolution entry point.</summary>
        public EventResolution Resolve(ExpeditionEventData evt, int choiceIndex, string actingCrewInstanceId = null)
        {
            if (!Ready(nameof(Resolve)) || evt == null) return EventResolution.Invalid(evt?.id, choiceIndex);

            if (evt.choices == null || choiceIndex < 0 || choiceIndex >= evt.choices.Count)
            {
                Debug.LogWarning($"[EventEngine] Resolve: choice index {choiceIndex} out of range for '{evt.id}'.");
                return EventResolution.Invalid(evt.id, choiceIndex);
            }

            var choice = evt.choices[choiceIndex];
            if (!IsChoiceAvailable(choice, actingCrewInstanceId))
            {
                Debug.LogWarning($"[EventEngine] Resolve: choice {choiceIndex} of '{evt.id}' is trait-gated and unavailable.");
                return EventResolution.Invalid(evt.id, choiceIndex);
            }

            // Success roll — always draw so the roll is reportable and the stream advances deterministically.
            float chance = ResolveSuccessChance(choice, actingCrewInstanceId);
            float roll = _rng.NextFloat();

            // Margin Note (item_margin_note): draw twice and keep the better reading. The second draw
            // is taken unconditionally once the artifact is spent, rather than only when the first
            // failed, so the RNG stream advances by the same amount either way and a seeded run stays
            // reproducible regardless of which outcome the first draw happened to give.
            bool overrideUsed = false;
            bool rerolled = Artifacts != null && Artifacts.ConsumeMarginNoteReroll();
            if (rerolled)
            {
                float second = _rng.NextFloat();
                Debug.Log($"[EventEngine] Margin Note: first roll {roll:0.###}, second {second:0.###}; " +
                          $"keeping {Mathf.Min(roll, second):0.###}.");
                roll = Mathf.Min(roll, second);   // lower roll = more likely under chance = better
            }

            bool success = roll < chance;

            // Stamped Tongue (item_stamped_tongue): a filed override decides a Scale Society matter in
            // the player's favour. Checked after the roll so the override is only spent when it changes
            // something — a matter that succeeded on its own does not consume the artifact.
            if (!success && Artifacts != null && InvolvesScaleSociety(choice) &&
                Artifacts.ConsumeStampedTongueOverride())
            {
                success = true;
                overrideUsed = true;
            }

            OutcomeDelta outcome = success ? choice.successOutcome : choice.failureOutcome;

            Debug.Log($"[EventEngine] Resolving '{evt.id}' choice {choiceIndex}: chance={chance:0.###} roll={roll:0.###} => {(success ? "SUCCESS" : "FAILURE")}.");

            var res = EventResolution.Invalid(evt.id, choiceIndex);
            res.valid = true;
            res.success = success;
            res.actingCrewInstanceId = actingCrewInstanceId;
            res.chanceUsed = chance;
            res.rollValue = roll;
            res.marginNoteRerolled = rerolled;
            res.stampedTongueOverrode = overrideUsed;

            ApplyOutcome(outcome, actingCrewInstanceId, ref res);

            // Book-keeping: this event is now spent; drop any queued copy so a follow-up won't re-fire, and
            // clear the pending marker so a save taken from here forward does not re-present it.
            if (!_run.CompletedEventIds.Contains(evt.id)) _run.CompletedEventIds.Add(evt.id);
            _run.QueuedEventIds.Remove(evt.id);
            if (_run.pendingEventId == evt.id) MarkPending(null);

            EventResolved?.Invoke(res);
            return res;
        }

        /// <summary>
        /// True when either branch of this choice moves Scale Society standing — the test for whether a
        /// Stamped Tongue override applies.
        ///
        /// <para>Both branches are checked, not just the failure branch, because the question is
        /// whether the <i>matter</i> is a Society matter, and an event that rewards standing on success
        /// and does nothing on failure is still one. Testing only the branch that is about to be
        /// applied would make the override fire on some Society events and not others, for a reason no
        /// player could ever infer.</para>
        /// </summary>
        private static bool InvolvesScaleSociety(EventChoice choice)
        {
            return choice.successOutcome.reputationFaction == FactionId.ScaleSociety
                || choice.failureOutcome.reputationFaction == FactionId.ScaleSociety;
        }

        private float ResolveSuccessChance(EventChoice choice, string actingCrewInstanceId)
        {
            float fallback = Mathf.Clamp01(choice.successChance);
            if (string.IsNullOrWhiteSpace(choice.successChanceFormula)) return fallback;

            var ctx = BuildCrewContext(actingCrewInstanceId);
            if (ctx == null)
            {
                Debug.Log($"[EventEngine] Formula '{choice.successChanceFormula}' needs a crew actor but none was given; using static chance {fallback:0.###}.");
                return fallback;
            }

            try
            {
                double v = FormulaEvaluator.Evaluate(choice.successChanceFormula, ctx.TryResolve);
                return Mathf.Clamp01((float)v);
            }
            catch (FormulaException ex)
            {
                Debug.LogWarning($"[EventEngine] Formula error ({ex.Message}); falling back to static chance {fallback:0.###}.");
                return fallback;
            }
        }

        private CrewFormulaContext BuildCrewContext(string actingCrewInstanceId)
        {
            if (string.IsNullOrEmpty(actingCrewInstanceId)) return null;
            var inst = _crew.GetMember(actingCrewInstanceId);
            if (inst == null) return null;
            var data = _db.GetCrew(inst.crewDataId);
            return new CrewFormulaContext(inst, data, _db);
        }

        // ─── Effect application ─────────────────────────────────────────────────────

        private void ApplyOutcome(OutcomeDelta outcome, string actingCrewInstanceId, ref EventResolution res)
        {
            // 1. Crew stat deltas. Radiation first, health last so a member's death is the final step for them.
            var targets = ResolveStatTargets(actingCrewInstanceId);
            foreach (var id in targets)
            {
                if (outcome.radiationDelta != 0) _crew.ApplyRadiation(id, outcome.radiationDelta);
                if (outcome.fatigueDelta != 0) _crew.ApplyFatigueDelta(id, outcome.fatigueDelta);
                if (outcome.sanityDelta != 0) _crew.ApplySanityDelta(id, outcome.sanityDelta);
                if (outcome.healthDelta != 0) _crew.ApplyHealthDelta(id, outcome.healthDelta);
            }

            // 2. Explicit lethal roll, independent of any health damage above.
            if (outcome.crewDeathChance > 0f)
            {
                string victim = PickDeathVictim(actingCrewInstanceId);
                if (victim != null && _rng.Chance(outcome.crewDeathChance))
                {
                    var m = _crew.GetMember(victim);
                    if (m != null && m.isAlive)
                    {
                        _crew.Kill(m);
                        res.crewDied = true;
                        res.diedCrewInstanceId = victim;
                    }
                }
            }

            // 3. Loot gained (weighted drops into the bunker). An entry with dropChance <= 0 is treated as
            //    guaranteed — listing an item in lootGained means you intend it to be grantable.
            if (outcome.lootGained != null)
            {
                foreach (var w in outcome.lootGained)
                {
                    if (w.item == null || string.IsNullOrEmpty(w.item.id)) continue;
                    if (!_rng.Chance(w.dropChance <= 0f ? 1f : w.dropChance)) continue;

                    int minQty = Mathf.Max(1, w.minQty);
                    int maxQty = Mathf.Max(minQty, w.maxQty);
                    int qty = _rng.NextInt(minQty, maxQty);

                    var added = _inventory.AddItem(InventoryChannel.Bunker, w.item.id, qty);
                    if (added != null) res.lootAddedItemIds.Add(w.item.id);
                }
            }

            // 4. Items lost (one unit of each listed item).
            //
            //    Consumption goes through RemoveOneWeighted rather than RemoveItem so a Carbon Copy
            //    duplicate can be the unit that gets used. That is the entire second half of ANM-Δ-07/CC:
            //    the free crates the player grabbed under the clock come due here, on whichever crate the
            //    crew happened to reach for, with the odds set by how many copies they took. Two draws per
            //    item — one to pick the unit, one for the defect's own branch — both off the run stream,
            //    so the whole resolution stays reproducible from the seed.
            if (outcome.itemsLost != null)
            {
                foreach (var item in outcome.itemsLost)
                {
                    if (item == null || string.IsNullOrEmpty(item.id)) continue;

                    bool wasDefective;
                    float selectionRoll = _rng.NextFloat();
                    if (!_inventory.RemoveOneWeighted(InventoryChannel.Bunker, item.id, selectionRoll,
                                                      out wasDefective))
                        continue;

                    res.itemsLostItemIds.Add(item.id);
                    if (!wasDefective) continue;

                    var defect = Anomalies.DefectiveItemEffects.Apply(
                        _db.GetItem(item.id), _crew, _rep, actingCrewInstanceId, _rng.NextFloat());

                    if (!defect.Applied) continue;

                    res.defectiveItemUsed = true;
                    res.defectSummary = defect.Summary;
                    Debug.Log($"[EventEngine] Carbon Copy defect during '{res.eventId}': {defect.Summary}");

                    // A defect can pull a success down into a failure, but never the reverse. The player's
                    // choice was sound; the object they made it with was not. Note that the outcome's other
                    // effects have already been applied from the success branch — reversing them would mean
                    // re-running resolution and drawing a second time from the RNG stream, which would
                    // desynchronise the seed. Flagging the resolution is the honest report: the attempt
                    // succeeded on paper and the equipment made it fail in practice.
                    if (defect.ForcesFailure && res.success) res.success = false;
                }
            }

            // 5. Reputation.
            if (outcome.reputationFaction != FactionId.None && outcome.reputationDelta != 0)
            {
                int before = _rep.Get(outcome.reputationFaction);
                _rep.ApplyDelta(outcome.reputationFaction, outcome.reputationDelta);
                res.reputationFaction = outcome.reputationFaction;
                res.reputationDeltaApplied = _rep.Get(outcome.reputationFaction) - before;
            }

            // 6. Follow-up event queued for a later day.
            if (!string.IsNullOrEmpty(outcome.followUpEventId)
                && !_run.QueuedEventIds.Contains(outcome.followUpEventId)
                && !_run.CompletedEventIds.Contains(outcome.followUpEventId))
            {
                _run.QueuedEventIds.Add(outcome.followUpEventId);
                res.followUpQueued = outcome.followUpEventId;
                Debug.Log($"[EventEngine] Queued follow-up '{outcome.followUpEventId}'.");
            }
        }

        private List<string> ResolveStatTargets(string actingCrewInstanceId)
        {
            var list = new List<string>();
            if (!string.IsNullOrEmpty(actingCrewInstanceId))
            {
                var m = _crew.GetMember(actingCrewInstanceId);
                if (m != null && m.isAlive) list.Add(actingCrewInstanceId);
                return list;
            }
            foreach (var m in _crew.ActiveCrew)
                if (m != null && m.isAlive) list.Add(m.instanceId);
            return list;
        }

        private string PickDeathVictim(string actingCrewInstanceId)
        {
            if (!string.IsNullOrEmpty(actingCrewInstanceId))
            {
                var m = _crew.GetMember(actingCrewInstanceId);
                return (m != null && m.isAlive) ? actingCrewInstanceId : null;
            }

            var alive = new List<string>();
            foreach (var m in _crew.ActiveCrew)
                if (m != null && m.isAlive) alive.Add(m.instanceId);
            if (alive.Count == 0) return null;
            return alive[_rng.NextInt(0, alive.Count - 1)];
        }

        // ─── Prerequisites ──────────────────────────────────────────────────────────

        private bool PassesPrerequisites(ExpeditionEventData evt, HashSet<string> regionTags,
                                         HashSet<string> oblastRegions, string actingCrewInstanceId)
        {
            if (_run.CompletedEventIds.Contains(evt.id)) return false;

            var p = evt.prerequisites;

            // Day window (minDay <= 0 => no lower bound; maxDay <= 0 => no upper bound).
            if (p.minDay > 0 && _run.currentDay < p.minDay) return false;
            if (p.maxDay > 0 && _run.currentDay > p.maxDay) return false;

            // Faction-context reputation band.
            if (p.factionContext != FactionId.None)
            {
                int rep = _rep.Get(p.factionContext);
                if (rep < p.minFactionRep || rep > p.maxFactionRep) return false;
            }

            // Required crew traits — every listed trait must be present across the candidate crew set.
            if (p.requiredCrewTraitIds != null && p.requiredCrewTraitIds.Count > 0)
            {
                var traits = CandidateTraitSet(actingCrewInstanceId);
                foreach (var t in p.requiredCrewTraitIds)
                    if (!string.IsNullOrEmpty(t) && !traits.Contains(t)) return false;
            }

            // Required items — at least one of the listed items held in the bunker.
            if (p.requiredItemsAny != null && p.requiredItemsAny.Count > 0)
                if (!AnyItemInBunker(p.requiredItemsAny)) return false;

            // Proximity locales — if the event constrains locale, at least one must be active.
            // FAIL-CLOSED: a caller that supplies no locales matches no locale-tagged event. Every shipped
            // event carries locales, so a caller that forgets them selects nothing, every time, silently.
            // ReportEmptyPool exists to name that case when it happens.
            if (p.regionTagsAny != null && p.regionTagsAny.Count > 0)
            {
                if (regionTags == null) return false;
                bool overlap = false;
                foreach (var tag in p.regionTagsAny)
                    if (!string.IsNullOrEmpty(tag) && regionTags.Contains(tag)) { overlap = true; break; }
                if (!overlap) return false;
            }

            // Canonical oblast regions — the geographic axis, orthogonal to the locales above.
            // FAIL-OPEN, and the asymmetry is the point: a caller that supplies no regions matches
            // everything, so this gate can narrow a pool but never empty one. One fail-closed content gate
            // is all this pipeline can safely carry; see OblastRegions for the measured cost of a second.
            if (oblastRegions != null && p.oblastRegionsAny != null && p.oblastRegionsAny.Count > 0)
            {
                bool overlap = false;
                foreach (var region in p.oblastRegionsAny)
                    if (!string.IsNullOrEmpty(region) && oblastRegions.Contains(region)) { overlap = true; break; }
                if (!overlap) return false;
            }

            return true;
        }

        private bool AnyItemInBunker(List<ItemData> items)
        {
            var have = new HashSet<string>();
            foreach (var inst in _inventory.Get(InventoryChannel.Bunker))
                if (inst.quantity > 0) have.Add(inst.itemDataId);

            foreach (var item in items)
                if (item != null && have.Contains(item.id)) return true;
            return false;
        }

        // ─── Shared helpers ─────────────────────────────────────────────────────────

        private HashSet<string> CandidateTraitSet(string actingCrewInstanceId)
        {
            var set = new HashSet<string>();
            if (_run == null) return set;

            if (!string.IsNullOrEmpty(actingCrewInstanceId))
            {
                var m = _crew.GetMember(actingCrewInstanceId);
                if (m?.traitIds != null)
                    foreach (var t in m.traitIds) if (!string.IsNullOrEmpty(t)) set.Add(t);
                return set;
            }

            foreach (var m in _crew.ActiveCrew)
            {
                if (m == null || !m.isAlive || m.traitIds == null) continue;
                foreach (var t in m.traitIds) if (!string.IsNullOrEmpty(t)) set.Add(t);
            }
            return set;
        }

        private bool Ready(string op)
        {
            if (_run != null && _rng != null) return true;
            Debug.LogError($"[EventEngine] {op} called before Bind(RunData). No-op.");
            return false;
        }
    }
}
