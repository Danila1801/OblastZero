// Assets/_Project/Scripts/OblastZero.Gameplay/ExpeditionSystem/ExpeditionManager.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay.ExpeditionSystem
{
    /// <summary>
    /// Ranged missions from the bunker: assign a crew member, a region and a loadout, wait some days,
    /// and find out what comes back. Sole owner of <see cref="RunData.ExpeditionsInFlight"/>.
    ///
    /// <para><b>What this adds to the bunker.</b> The day loop is otherwise a single verb — End Day,
    /// answer the event, repeat — and every decision in it is reactive. An expedition is the one thing
    /// the player initiates: it costs a body off the roster for three to five days, in a phase where
    /// bodies eat rations and events target whoever is present, and it pays in supplies you cannot get
    /// any other way once the Blowout is over.</para>
    ///
    /// <para><b>Why returning crew queue an event rather than resolving one silently.</b> The obvious
    /// reading of "resolve the expedition through the event engine" is to call
    /// <c>SelectNextEvent</c> and <c>Resolve</c> internally. That is wrong twice: <c>SelectNextEvent</c>
    /// raises <c>EventPresented</c> and writes <c>RunData.pendingEventId</c>, so an expedition
    /// resolving on the same tick as a bunker day would fight the day's own event for the modal and
    /// for that field; and resolving a choice on the player's behalf picks their branch for them, which
    /// is the one thing the event system exists not to do. So an expedition applies its own concrete
    /// outcomes — items, fatigue, radiation, hazards — and may <i>queue</i> a region-tagged event, which
    /// the player then meets through the ordinary day loop with the choice still theirs.</para>
    ///
    /// <para><b>Hazards reuse the Phase A systems rather than reimplementing them.</b> A Backlog delays
    /// the return, an Editor rewrites part of the pack, a Census-Taker registers the crew member through
    /// the same <see cref="Mutants.RegistrationAffliction"/> the scavenge phase uses. One definition of
    /// what each hazard costs, so the bunker and the Blowout cannot drift apart on it.</para>
    /// </summary>
    public class ExpeditionManager
    {
        /// <summary>Why a dispatch was refused. Each is a distinct state the screen explains.</summary>
        public enum DispatchResult
        {
            Success,
            NoRun,
            CrewUnavailable,
            TooManyInFlight,
            UnknownRegion,
            LoadoutUnavailable
        }

        /// <summary>What one resolved expedition did. Returned to the day loop for the log and the HUD.</summary>
        public struct Report
        {
            public string expeditionId;
            public string crewInstanceId;
            public string oblastRegionId;
            public bool crewReturned;
            public bool delayed;
            public int daysDelayed;
            public int itemsRecovered;
            public string queuedEventId;
            public string summary;
        }

        private readonly GameDatabase _db;
        private readonly InventoryManager _inventory;
        private readonly CrewManager _crew;
        private RunData _run;
        private RunRng _rng;

        public ExpeditionManager(GameDatabase db, InventoryManager inventory, CrewManager crew)
        {
            _db = db;
            _inventory = inventory;
            _crew = crew;
        }

        /// <summary>Binds to a run. Called by <c>GameManager</c> alongside the other managers.</summary>
        public void Bind(RunData run)
        {
            _run = run;
            _rng = run != null ? new RunRng(run) : null;

            if (run == null) return;
            if (run.ExpeditionsInFlight == null) run.ExpeditionsInFlight = new List<ActiveExpedition>();

            Debug.Log($"[ExpeditionManager] Bound to run '{run.runId}'. " +
                      $"{run.ExpeditionsInFlight.Count} expedition(s) in flight.");
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>Expeditions currently out. Never null.</summary>
        public IReadOnlyList<ActiveExpedition> InFlight
        {
            get
            {
                return _run != null && _run.ExpeditionsInFlight != null
                    ? (IReadOnlyList<ActiveExpedition>)_run.ExpeditionsInFlight
                    : new List<ActiveExpedition>();
            }
        }

        /// <summary>True when this crew member is already out in the field.</summary>
        public bool IsDeployed(string crewInstanceId)
        {
            if (_run == null || string.IsNullOrEmpty(crewInstanceId)) return false;
            for (int i = 0; i < _run.ExpeditionsInFlight.Count; i++)
                if (_run.ExpeditionsInFlight[i].crewInstanceId == crewInstanceId) return true;
            return false;
        }

        /// <summary>Living crew in the bunker who could be sent out right now.</summary>
        public List<CrewInstance> AvailableCrew()
        {
            var available = new List<CrewInstance>();
            if (_crew == null) return available;

            var roster = _crew.ActiveCrew;
            for (int i = 0; i < roster.Count; i++)
            {
                var member = roster[i];
                if (member == null || !member.isAlive) continue;
                if (IsDeployed(member.instanceId)) continue;
                available.Add(member);
            }
            return available;
        }

        /// <summary>Day the given expedition is due back.</summary>
        public static int ReturnDayOf(ActiveExpedition expedition)
        {
            return expedition.dayStarted + expedition.duration;
        }

        /// <summary>Whether a dispatch would be accepted, and why not when it would not.</summary>
        public DispatchResult CanDispatch(string crewInstanceId, string oblastRegionId)
        {
            if (_run == null) return DispatchResult.NoRun;
            if (_run.ExpeditionsInFlight.Count >= BalanceConstants.EXPEDITION_MAX_CONCURRENT)
                return DispatchResult.TooManyInFlight;
            if (!OblastRegions.IsCanonical(oblastRegionId)) return DispatchResult.UnknownRegion;

            var member = _crew != null ? _crew.GetMember(crewInstanceId) : null;
            if (member == null || !member.isAlive || IsDeployed(crewInstanceId))
                return DispatchResult.CrewUnavailable;

            return DispatchResult.Success;
        }

        /// <summary>
        /// Days a mission to this region will take. Deterministic from the run stream, drawn at
        /// dispatch so the screen can show the estimate and the estimate is the truth.
        /// </summary>
        public int RollDuration()
        {
            if (_rng == null) return BalanceConstants.EXPEDITION_MIN_DAYS;
            return _rng.NextInt(BalanceConstants.EXPEDITION_MIN_DAYS, BalanceConstants.EXPEDITION_MAX_DAYS);
        }

        // ── Dispatch ─────────────────────────────────────────────────────────

        /// <summary>
        /// Sends a crew member out. The loadout is removed from the bunker at dispatch, not on return —
        /// kit that has left the bunker is not in the bunker, and leaving it countable would let the
        /// player equip an expedition and then feed the same tins to the crew that stayed.
        /// </summary>
        public DispatchResult Dispatch(string crewInstanceId, string oblastRegionId,
                                       IReadOnlyList<string> loadoutItemIds, out ActiveExpedition dispatched)
        {
            dispatched = null;

            var check = CanDispatch(crewInstanceId, oblastRegionId);
            if (check != DispatchResult.Success) return check;

            var taken = new List<string>();
            if (loadoutItemIds != null)
            {
                int slots = Mathf.Min(loadoutItemIds.Count, BalanceConstants.EXPEDITION_MAX_LOADOUT_ITEMS);
                for (int i = 0; i < slots; i++)
                {
                    string id = loadoutItemIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    if (_inventory.RemoveItem(InventoryChannel.Bunker, id, 1)) taken.Add(id);
                }

                // A partial loadout is honoured rather than refused. The screen only offers items the
                // bunker holds, so a miss here means something consumed them between the click and the
                // dispatch — and cancelling the whole mission over one missing tin would be the more
                // surprising behaviour.
                if (taken.Count < slots)
                    Debug.LogWarning($"[ExpeditionManager] {slots - taken.Count} loadout item(s) were no " +
                                     "longer in the bunker at dispatch. Sending with what remained.");
            }

            int duration = RollDuration();
            var expedition = new ActiveExpedition
            {
                expeditionId = $"exp_{_run.runId}_{_run.currentDay}_{crewInstanceId}",
                crewInstanceId = crewInstanceId,
                regionTag = oblastRegionId,
                dayStarted = _run.currentDay,
                duration = duration,
                loadoutItemInstanceIds = taken,
                resolvedEventIds = new List<string>()
            };

            _run.ExpeditionsInFlight.Add(expedition);

            // locationTag is what tells the rest of the game this person is not in the bunker. It is on
            // CrewInstance and CrewManager owns that, but nothing else writes this particular field and
            // the expedition is the only thing that knows the destination.
            var member = _crew.GetMember(crewInstanceId);
            if (member != null) member.locationTag = "expedition:" + oblastRegionId;

            dispatched = expedition;

            Debug.Log($"[ExpeditionManager] '{crewInstanceId}' dispatched to " +
                      $"{OblastRegions.DisplayNameOf(oblastRegionId)} on day {_run.currentDay} " +
                      $"for {duration} day(s), carrying {taken.Count} item(s). " +
                      $"Due back day {ReturnDayOf(expedition)}.");

            EventBus.Raise(new ExpeditionDispatchedEvent
            {
                ExpeditionId = expedition.expeditionId,
                CrewInstanceId = crewInstanceId,
                OblastRegionId = oblastRegionId,
                ReturnDay = ReturnDayOf(expedition)
            });

            return DispatchResult.Success;
        }

        // ── Daily tick ───────────────────────────────────────────────────────

        /// <summary>
        /// Advances every expedition one day and resolves any that are due. Called by
        /// <c>BunkerDayController</c> after the day counter moves, so <c>currentDay</c> is already the
        /// new day when returns are tested.
        /// </summary>
        public List<Report> TickDay()
        {
            var reports = new List<Report>();
            if (_run == null || _run.ExpeditionsInFlight.Count == 0) return reports;

            // Iterated backwards: resolution removes from the same list.
            for (int i = _run.ExpeditionsInFlight.Count - 1; i >= 0; i--)
            {
                var expedition = _run.ExpeditionsInFlight[i];
                if (_run.currentDay < ReturnDayOf(expedition)) continue;

                reports.Add(Resolve(expedition, i));
            }

            return reports;
        }

        private Report Resolve(ActiveExpedition expedition, int index)
        {
            var report = new Report
            {
                expeditionId = expedition.expeditionId,
                crewInstanceId = expedition.crewInstanceId,
                oblastRegionId = expedition.regionTag,
                crewReturned = true
            };

            var member = _crew.GetMember(expedition.crewInstanceId);
            int daysOut = Mathf.Max(1, _run.currentDay - expedition.dayStarted);

            // 1. Backlog. Tested first because a delay means nothing else resolves today — the
            //    expedition is still out, and everything below happens on whatever day it does return.
            if (_rng.Chance(BalanceConstants.EXPEDITION_BACKLOG_DELAY_CHANCE))
            {
                int extra = _rng.NextInt(1, 3);
                expedition.duration += extra;
                _run.ExpeditionsInFlight[index] = expedition;

                report.delayed = true;
                report.daysDelayed = extra;
                report.summary = $"Overdue. The note left at the edge is dated {extra} day(s) from now, " +
                                 "in handwriting that is theirs.";

                Debug.Log($"[ExpeditionManager] '{expedition.crewInstanceId}' delayed {extra} day(s) " +
                          $"in the {OblastRegions.DisplayNameOf(expedition.regionTag)}. " +
                          $"Now due day {ReturnDayOf(expedition)}.");
                return report;
            }

            _run.ExpeditionsInFlight.RemoveAt(index);

            // 2. Loss. The lowest-probability branch and the only one that ends a crew member, so it
            //    resolves before anything is banked — an expedition that did not come back did not
            //    bring anything back either.
            if (member != null && _rng.Chance(BalanceConstants.EXPEDITION_LOSS_CHANCE))
            {
                _crew.Kill(member);
                report.crewReturned = false;
                report.summary = "Did not return. The file remains open pending further information.";

                Debug.Log($"[ExpeditionManager] '{expedition.crewInstanceId}' did not return from the " +
                          $"{OblastRegions.DisplayNameOf(expedition.regionTag)}.");

                RaiseResolved(report);
                return report;
            }

            if (member != null) member.locationTag = "bunker";

            // 3. The haul. Loadout increases yield: kit sent out is what makes a mission productive
            //    rather than a walk, and it is the reason the loadout slots are a decision.
            int yield = BalanceConstants.EXPEDITION_BASE_ITEM_YIELD
                        + Mathf.FloorToInt(expedition.loadoutItemInstanceIds.Count
                                           * BalanceConstants.EXPEDITION_YIELD_PER_LOADOUT_ITEM);
            report.itemsRecovered = BankHaul(yield);

            // 4. The cost of being out there at all.
            if (member != null)
            {
                _crew.ApplyFatigueDelta(member.instanceId,
                                        daysOut * BalanceConstants.EXPEDITION_FATIGUE_PER_DAY);
                // Radiation goes through CrewManager, so the Notarized Heart's halving applies here
                // without this class knowing the artifact exists.
                _crew.ApplyRadiation(member.instanceId,
                                     daysOut * BalanceConstants.EXPEDITION_RADIATION_PER_DAY);
            }

            // 5. Field hazards, reusing the Phase A definitions.
            var notes = new List<string>();

            if (member != null && _rng.Chance(BalanceConstants.EXPEDITION_REGISTRATION_CHANCE))
            {
                Mutants.RegistrationAffliction.Register(Mutants.DrownedCensusTaker.ClassificationCode);
                notes.Add("Reports a wet clerk on the return leg. They were asked to confirm a spelling.");
            }

            if (_rng.Chance(BalanceConstants.EXPEDITION_EDITOR_EDIT_CHANCE))
            {
                string edited = EditReturningPack();
                if (edited != null)
                    notes.Add($"The manifest does not agree with the pack. '{edited}' is not on the list.");
            }

            // 6. A region-tagged event queued for the player to meet through the ordinary day loop,
            //    with the choice still theirs. See the class remarks for why this is queued and not
            //    resolved here.
            report.queuedEventId = QueueRegionEvent(expedition.regionTag);
            if (report.queuedEventId != null) expedition.resolvedEventIds.Add(report.queuedEventId);

            report.summary = $"Returned after {daysOut} day(s) with {report.itemsRecovered} item(s)."
                             + (notes.Count > 0 ? " " + string.Join(" ", notes) : string.Empty);

            Debug.Log($"[ExpeditionManager] '{expedition.crewInstanceId}' returned from the " +
                      $"{OblastRegions.DisplayNameOf(expedition.regionTag)}: {report.summary}");

            RaiseResolved(report);
            return report;
        }

        /// <summary>
        /// Adds <paramref name="count"/> random items to the bunker and returns how many landed.
        /// Drawn from the whole corpus rather than a region loot table because no such table exists in
        /// the content set — inventing one here would be a second, undocumented authority on what a
        /// region contains, disagreeing with the events that already describe it.
        /// </summary>
        private int BankHaul(int count)
        {
            var all = _db != null ? _db.AllItems : null;
            if (all == null || all.Count == 0 || count <= 0) return 0;

            int landed = 0;
            for (int i = 0; i < count; i++)
            {
                var data = all[_rng.NextInt(0, all.Count - 1)];
                if (data == null || string.IsNullOrEmpty(data.id)) continue;
                if (_inventory.AddItem(InventoryChannel.Bunker, data.id) != null) landed++;
            }
            return landed;
        }

        /// <summary>Redacts one returning stack, the bunker-side echo of the Editor. Returns its id.</summary>
        private string EditReturningPack()
        {
            var bunker = _inventory.Get(InventoryChannel.Bunker);
            if (bunker.Count == 0) return null;

            var stack = bunker[_rng.NextInt(0, bunker.Count - 1)];
            if (stack == null) return null;

            stack.isRedacted = true;
            return stack.itemDataId;
        }

        /// <summary>
        /// Queues an event tagged with the expedition's region, or null when nothing eligible exists.
        ///
        /// <para>Selection is a direct weighted draw over the corpus rather than a call to
        /// <c>EventEngine.SelectNextEvent</c>, which would raise <c>EventPresented</c> and claim
        /// <c>RunData.pendingEventId</c> — both of which belong to the bunker day that is happening
        /// around this. Queueing by id is the engine's own supported handoff: <c>QueuedEventIds</c>
        /// takes priority on the next selection, so the player meets it on the following day.</para>
        /// </summary>
        private string QueueRegionEvent(string oblastRegionId)
        {
            if (_db == null) return null;

            var candidates = new List<ExpeditionEventData>();
            var all = _db.AllEvents;

            for (int i = 0; i < all.Count; i++)
            {
                var evt = all[i];
                if (evt == null || evt.baseWeight <= 0f) continue;
                if (_run.CompletedEventIds.Contains(evt.id)) continue;
                if (_run.QueuedEventIds.Contains(evt.id)) continue;

                var regions = evt.prerequisites.oblastRegionsAny;
                if (regions == null || regions.Count == 0) continue;
                if (!regions.Contains(oblastRegionId)) continue;

                candidates.Add(evt);
            }

            if (candidates.Count == 0)
            {
                Debug.Log($"[ExpeditionManager] No unseen event tagged '{oblastRegionId}' to queue. " +
                          "The expedition returns without a story attached.");
                return null;
            }

            var chosen = candidates[_rng.NextInt(0, candidates.Count - 1)];
            _run.QueuedEventIds.Add(chosen.id);

            Debug.Log($"[ExpeditionManager] Queued '{chosen.id}' from the return " +
                      $"({candidates.Count} candidate(s) tagged '{oblastRegionId}').");
            return chosen.id;
        }

        private static void RaiseResolved(Report report)
        {
            EventBus.Raise(new ExpeditionResolvedEvent
            {
                ExpeditionId = report.expeditionId,
                CrewInstanceId = report.crewInstanceId,
                OblastRegionId = report.oblastRegionId,
                CrewReturned = report.crewReturned,
                WasDelayed = report.delayed,
                ItemsRecovered = report.itemsRecovered,
                OutcomeSummary = report.summary
            });
        }
    }
}
