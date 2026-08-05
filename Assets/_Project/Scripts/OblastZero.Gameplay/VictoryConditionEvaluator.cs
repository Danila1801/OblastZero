// Assets/_Project/Scripts/Gameplay/VictoryConditionEvaluator.cs
using System;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The outcome of one victory check: whether a run has been won, which ending it earned, and the state
    /// to transition into. <see cref="Explanation"/> is a diagnostic line, not player-facing prose — the
    /// endings own their own text (see <c>RunEndVictoryStateBase</c>).
    /// </summary>
    public struct VictoryVerdict
    {
        public bool Achieved;
        public RunEndReason Reason;
        public GameState State;
        public string EndingName;
        public string Explanation;

        /// <summary>The "no ending yet" verdict. Carries the reason the run is still open.</summary>
        public static VictoryVerdict None(string why) => new VictoryVerdict
        {
            Achieved = false,
            Reason = RunEndReason.Quit,
            State = GameState.SurvivalPhase2D,
            EndingName = null,
            Explanation = why,
        };
    }

    /// <summary>
    /// Decides whether the current run has been won, and by which of the four endings.
    ///
    /// <para><b>Why this class exists.</b> All four victory states were implemented, registered in
    /// <c>_Bootstrap</c>, and wired to <see cref="GameManager.LastRunSummary"/> — and every one of them was
    /// dead code, because nothing anywhere called
    /// <c>EndCurrentRun(RunEndReason.Victory*)</c>. A run could only ever end in death or a quit. This is the
    /// missing caller.</para>
    ///
    /// <para><b>The rules come from the design bible, not from taste.</b> Bible §3 fixes the reputation
    /// thresholds ("Going above +60 with any faction unlocks that faction's endgame branch") and the
    /// alignment pivot at day fifteen; §2 fixes which faction owns which ending — Scale Society →
    /// Stabilization, Cordon → Relief, Kafedra → Adaptation — and §6.3 marks Independent as the "rare
    /// neutral-ending branch". The numbers live in <see cref="BalanceConstants"/>; this class only applies
    /// them.</para>
    ///
    /// <para><b>What is deliberately NOT gated on.</b> <c>RunData.bunkerSealed</c> and
    /// <c>RunData.bunkerMorale</c> read like natural victory inputs, and the Stabilization prose even says
    /// "the bunker is sealed" — but no system in the game writes either field: both are set once in
    /// <see cref="GameManager.BeginNewRun"/> and never touched again. Requiring them would produce an ending
    /// that can never fire, which is the exact defect this class was written to remove. They become
    /// candidates the day a system actually moves them.</para>
    ///
    /// <para>Pure C#: it reads <see cref="RunData"/> and the two managers, mutates nothing, and touches
    /// neither the EventBus nor the scene, so the whole win-condition matrix is unit-testable headless.</para>
    /// </summary>
    public class VictoryConditionEvaluator
    {
        private readonly RunData _run;
        private readonly CrewManager _crew;
        private readonly FactionReputationManager _rep;

        public VictoryConditionEvaluator(RunData run, CrewManager crew, FactionReputationManager rep)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _crew = crew ?? throw new ArgumentNullException(nameof(crew));
            _rep = rep ?? throw new ArgumentNullException(nameof(rep));
        }

        /// <summary>
        /// Evaluates every ending against the run's current state and returns the first that qualifies.
        ///
        /// <para>Order is precedence: a faction endgame outranks the neutral ending (they are mutually
        /// exclusive by their own conditions, but the ordering makes that explicit rather than incidental),
        /// and among factions the highest reputation wins.</para>
        /// </summary>
        public VictoryVerdict Evaluate()
        {
            // A victory is a run the crew survived. Every ending's prose says so out loud ("Your crew has
            // survived", "Your crew walks toward the horizon"), and the wipe path owns the alternative.
            // Callers check for a wipe first; this is the guard that makes the ordering non-load-bearing.
            int alive = _crew.AliveCount();
            if (alive <= 0)
                return VictoryVerdict.None("no surviving crew — a wipe is not an ending");

            int day = _run.currentDay;

            var factionVerdict = EvaluateFactionEndgame(day, alive);
            if (factionVerdict.Achieved) return factionVerdict;

            var neutralVerdict = EvaluateIndependent(day, alive);
            if (neutralVerdict.Achieved) return neutralVerdict;

            return VictoryVerdict.None(
                $"day {day}, crew {alive}, rep S{_rep.Get(FactionId.ScaleSociety)}/" +
                $"C{_rep.Get(FactionId.Cordon)}/K{_rep.Get(FactionId.Kafedra)} — no ending qualifies");
        }

        // ─── Faction endgames (bible §2: one ending per canonical faction) ──────────

        /// <summary>
        /// The three faction endings share one shape: tenure past the alignment pivot, plus reputation at or
        /// above the endgame threshold with the faction that is furthest ahead.
        ///
        /// <para>Ties and multiple qualifying factions are resolved by taking the highest reputation, then by
        /// declared order. Bible §3 says the player "can be in good standing with at most one faction at a
        /// time after day fifteen", but nothing in the code enforces that, so two factions above the
        /// threshold is reachable state and needs a deterministic answer rather than whichever branch happens
        /// to be checked first.</para>
        /// </summary>
        private VictoryVerdict EvaluateFactionEndgame(int day, int aliveCrew)
        {
            if (day < BalanceConstants.ENDGAME_MIN_TENURE_DAYS)
                return VictoryVerdict.None($"day {day} is short of the day-" +
                                           $"{BalanceConstants.ENDGAME_MIN_TENURE_DAYS} alignment pivot");

            // Declared order is the tiebreak, so it is written once, here.
            FactionId leader = FactionId.None;
            int leaderRep = int.MinValue;
            int qualifying = 0;

            foreach (var faction in EndgameFactions)
            {
                int rep = _rep.Get(faction);
                if (rep < BalanceConstants.ENDGAME_REPUTATION_THRESHOLD) continue;

                qualifying++;
                if (rep > leaderRep)
                {
                    leaderRep = rep;
                    leader = faction;
                }
            }

            if (leader == FactionId.None)
                return VictoryVerdict.None($"no faction at or above +{BalanceConstants.ENDGAME_REPUTATION_THRESHOLD}");

            if (qualifying > 1)
            {
                Debug.LogWarning($"[VictoryConditions] {qualifying} factions are at or above " +
                                 $"+{BalanceConstants.ENDGAME_REPUTATION_THRESHOLD} on day {day}. Bible §3 expects at " +
                                 $"most one after day {BalanceConstants.ENDGAME_MIN_TENURE_DAYS}. Awarding the highest " +
                                 $"({leader} at {leaderRep}).");
            }

            switch (leader)
            {
                case FactionId.ScaleSociety:
                    return new VictoryVerdict
                    {
                        Achieved = true,
                        Reason = RunEndReason.VictoryStabilization,
                        State = GameState.RunVictory_Stabilization,
                        EndingName = "Stabilization",
                        Explanation = $"Scale Society rep {leaderRep} on day {day} with {aliveCrew} crew alive",
                    };

                case FactionId.Cordon:
                    return new VictoryVerdict
                    {
                        Achieved = true,
                        Reason = RunEndReason.VictoryRelief,
                        State = GameState.RunVictory_Relief,
                        EndingName = "Relief",
                        Explanation = $"Cordon rep {leaderRep} on day {day} with {aliveCrew} crew alive",
                    };

                case FactionId.Kafedra:
                    return new VictoryVerdict
                    {
                        Achieved = true,
                        Reason = RunEndReason.VictoryAdaptation,
                        State = GameState.RunVictory_Adaptation,
                        EndingName = "Adaptation",
                        Explanation = $"Kafedra rep {leaderRep} on day {day} with {aliveCrew} crew alive",
                    };

                default:
                    // Unreachable while EndgameFactions holds only the three tracked factions; kept so a
                    // fourth faction added there fails loudly instead of silently never winning.
                    Debug.LogError($"[VictoryConditions] Faction '{leader}' cleared the endgame threshold but " +
                                   "owns no ending. Add its case here or drop it from EndgameFactions.");
                    return VictoryVerdict.None($"faction '{leader}' has no ending mapped");
            }
        }

        // ─── Independent (bible §6.3: the rare neutral branch) ─────────────────────

        /// <summary>
        /// The neutral ending. Earned by refusing every faction and outlasting the need for them: a longer
        /// tenure than the faction endings, nobody at the endgame threshold, and — the part that makes it a
        /// choice rather than a default — nobody driven down to hunted status either. Drifting to −70 with
        /// all three is not independence, it is being cornered, and it has its own failure paths.
        /// </summary>
        private VictoryVerdict EvaluateIndependent(int day, int aliveCrew)
        {
            if (day < BalanceConstants.INDEPENDENT_MIN_TENURE_DAYS)
                return VictoryVerdict.None($"day {day} is short of the day-" +
                                           $"{BalanceConstants.INDEPENDENT_MIN_TENURE_DAYS} independent tenure");

            foreach (var faction in EndgameFactions)
            {
                int rep = _rep.Get(faction);

                if (rep >= BalanceConstants.ENDGAME_REPUTATION_THRESHOLD)
                    return VictoryVerdict.None($"{faction} rep {rep} is an alignment, not independence");

                if (rep <= BalanceConstants.HUNTED_REPUTATION_THRESHOLD)
                    return VictoryVerdict.None($"{faction} rep {rep} is hunted status, not independence");
            }

            return new VictoryVerdict
            {
                Achieved = true,
                Reason = RunEndReason.VictoryIndependent,
                State = GameState.RunVictory_Independent,
                EndingName = "Independent",
                Explanation = $"day {day} unaligned and unhunted with {aliveCrew} crew alive " +
                              $"(S{_rep.Get(FactionId.ScaleSociety)}/C{_rep.Get(FactionId.Cordon)}/" +
                              $"K{_rep.Get(FactionId.Kafedra)})",
            };
        }

        /// <summary>
        /// The factions that own an ending, in tiebreak order. Mirrors
        /// <see cref="FactionReputationManager.IsTracked"/> — the untracked factions always read 0, so
        /// including them would let a run "win" on a faction it never interacted with.
        /// </summary>
        private static readonly FactionId[] EndgameFactions =
        {
            FactionId.ScaleSociety,
            FactionId.Cordon,
            FactionId.Kafedra,
        };
    }
}
