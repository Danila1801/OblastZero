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

        private ExpeditionEventData _pending;

        public bool HasPendingEvent => _pending != null;
        public ExpeditionEventData PendingEvent => _pending;

        public BunkerPhaseController(BunkerDayController dayController, EventEngine events, CrewManager crew)
        {
            _dayController = dayController ?? throw new ArgumentNullException(nameof(dayController));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _crew = crew ?? throw new ArgumentNullException(nameof(crew));
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
