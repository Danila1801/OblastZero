// Assets/_Project/Scripts/Gameplay/BunkerPhaseController.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>Outcome of one bunker turn: the day tick, whether the run ended, and any event now awaiting a choice.</summary>
    public struct BunkerTurnResult
    {
        public DayResult day;
        public bool runEnded;                    // all crew dead after the day tick
        public ExpeditionEventData presentedEvent; // null when nothing was eligible this day

        /// <summary>
        /// Whether this day advance won the run, and by which ending. Checked before event selection, so a
        /// won run never also presents an event the player would have to dismiss before seeing the outcome.
        /// </summary>
        public VictoryVerdict victory;
    }

    /// <summary>
    /// Orchestrates one bunker turn on top of the two lower-level systems: a <see cref="BunkerDayController"/>
    /// day tick (decay / rations / crew ticks) followed by an <see cref="EventEngine"/> event presentation.
    /// Turn-based flow: <see cref="EndDay"/> advances the day and — if anyone is still alive — presents the
    /// next event (held as <see cref="PendingEvent"/>); the player then picks a branch via
    /// <see cref="ResolvePendingEvent"/>. A new day cannot start while an event is still pending.
    ///
    /// Pure C#: it owns no RunData fields itself (the day controller and engine own theirs) and stays off the
    /// EventBus — <see cref="BunkerDayController"/> and <see cref="EventEngine"/> already raise the global
    /// events the UI listens to. SurvivalPhase2DState drives this from UI intents.
    /// </summary>
    public class BunkerPhaseController
    {
        private readonly BunkerDayController _dayController;
        private readonly EventEngine _events;
        private readonly CrewManager _crew;
        private readonly VictoryConditionEvaluator _victory; // optional; null disables win checks

        private ExpeditionEventData _pending;

        public bool HasPendingEvent => _pending != null;
        public ExpeditionEventData PendingEvent => _pending;

        /// <param name="victory">
        /// Win-condition evaluator. Optional so a test that only exercises the day/event loop does not have
        /// to build one; when null every victory check reports "not configured" and the run can only end in
        /// death or a quit — which is exactly the pre-Phase-2 behaviour, so leaving it out is a silent
        /// downgrade in production. <c>SurvivalPhase2DState</c> always supplies one.
        /// </param>
        public BunkerPhaseController(BunkerDayController dayController, EventEngine events, CrewManager crew,
                                     VictoryConditionEvaluator victory = null)
        {
            _dayController = dayController ?? throw new ArgumentNullException(nameof(dayController));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _crew = crew ?? throw new ArgumentNullException(nameof(crew));
            _victory = victory;
        }

        /// <summary>
        /// Runs the win-condition check against current run state. Safe to call at any point; the caller
        /// decides what to do with a positive verdict.
        ///
        /// <para>Exposed separately from <see cref="EndDay"/> because reputation moves during event
        /// resolution, not only on a day tick — a choice that pushes a faction over the endgame threshold
        /// should win the run then and there rather than on the next press of End Day.</para>
        /// </summary>
        public VictoryVerdict EvaluateVictory()
        {
            if (_victory == null) return VictoryVerdict.None("no victory evaluator configured");
            return _victory.Evaluate();
        }

        /// <summary>
        /// Re-opens the event a reloaded run was holding, if any, and returns it. Call once on entering the
        /// bunker phase, before the player can press End Day.
        ///
        /// <para>Without this, a run resumed at an open event came back with no prompt and
        /// <see cref="EndDay"/> free to advance — so the day the player had already reached an event on
        /// silently rolled forward, and the event itself was redrawn from an RNG stream that had already moved
        /// past it. Restoring puts the same prompt back and re-arms the "cannot end a day with an event
        /// pending" guard.</para>
        /// </summary>
        public ExpeditionEventData RestorePendingEvent()
        {
            var restored = _events.RestorePendingEvent();
            if (restored == null) return null;

            _pending = restored;
            Debug.Log($"[BunkerPhase] Resumed holding event '{restored.id}' — End Day stays blocked until it is resolved.");
            return restored;
        }

        /// <summary>
        /// Advances one bunker day, then presents the next eligible event (if any). Refuses while an event is
        /// still pending — resolve it first. Region tags scope which location-flavoured events can fire.
        /// </summary>
        public BunkerTurnResult EndDay(IReadOnlyCollection<string> regionTags = null)
        {
            if (_pending != null)
            {
                Debug.LogWarning("[BunkerPhase] EndDay refused — an event is still awaiting a choice.");
                return new BunkerTurnResult { runEnded = false, presentedEvent = _pending };
            }

            DayResult day = _dayController.AdvanceDay();

            if (day.aliveRemaining <= 0)
            {
                Debug.Log("[BunkerPhase] All crew dead after the day tick — no event; run ends.");
                return new BunkerTurnResult { day = day, runEnded = true, presentedEvent = null };
            }

            // Victory is checked here, between the day tick and event selection, for two reasons: the day
            // counter the faction endgames gate on has just moved, and a run that has been won must not also
            // present an event — the player would be answering a prompt for a run that is already over.
            VictoryVerdict victory = EvaluateVictory();
            if (victory.Achieved)
            {
                Debug.Log($"[BunkerPhase] Day {day.newDay} won the run — ending '{victory.EndingName}' " +
                          $"({victory.Explanation}). No event presented.");
                return new BunkerTurnResult { day = day, runEnded = false, presentedEvent = null, victory = victory };
            }

            var evt = _events.SelectNextEvent(regionTags);
            _pending = evt;

            if (evt != null) Debug.Log($"[BunkerPhase] Day {day.newDay} presented event '{evt.id}'.");
            else Debug.Log($"[BunkerPhase] Day {day.newDay} — no event this day.");

            return new BunkerTurnResult { day = day, runEnded = false, presentedEvent = evt };
        }

        /// <summary>
        /// Resolves the pending event's chosen branch and clears it. Returns the full effect report. If the
        /// choice was rejected (e.g. trait-gated) the event stays pending so the UI can re-prompt.
        /// </summary>
        public EventResolution ResolvePendingEvent(int choiceIndex, string actingCrewInstanceId = null)
        {
            if (_pending == null)
            {
                Debug.LogWarning("[BunkerPhase] ResolvePendingEvent called with no event pending.");
                return EventResolution.Invalid(null, choiceIndex);
            }

            var res = _events.Resolve(_pending, choiceIndex, actingCrewInstanceId);
            if (res.valid) _pending = null;
            return res;
        }

        /// <summary>True if every crew member is dead — the caller (state) uses this to end the run after a resolution.</summary>
        public bool IsWipe() => _crew.AliveCount() <= 0;
    }
}
