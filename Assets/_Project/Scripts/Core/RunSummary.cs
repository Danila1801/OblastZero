// Assets/_Project/Scripts/Core/RunSummary.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Data;

namespace OblastZero.Core
{
    /// <summary>
    /// A flat, already-computed snapshot of how a run ended.
    ///
    /// It exists because <see cref="GameManager.EndCurrentRun"/> clears <c>Context.CurrentRun</c> as part of
    /// closing the run, while the run-end states run AFTER that call — so by the time a summary screen is
    /// built there is no live RunData left to read. GameManager takes this snapshot at the moment of
    /// closure, when the numbers are still true, and hands it to whoever needs to display them.
    ///
    /// Lives in Core rather than UI on purpose: it is a record of game state, and Core must not depend on
    /// the UI layer. RunSummaryUI only renders it.
    /// </summary>
    public class RunSummary
    {
        public bool Survived;
        public RunEndReason Reason;
        public string Headline = "CASE FILED";
        public string Subheadline = string.Empty;
        public string ClosingLine = string.Empty;
        public string SiteName = "Unrecorded Site";

        public int DaysSurvived;
        public int CrewLost;
        public int CrewRemaining;
        public int ItemsRecovered;
        public int ItemsSalvaged;
        public int SalvageRatePercent;

        public int TotalRunsAttempted;
        public int TotalRunsSurvived;

        /// <summary>Faction display name → reputation. Ordered, so the column reads the same every run.</summary>
        public List<KeyValuePair<string, int>> Reputations = new List<KeyValuePair<string, int>>();

        /// <summary>
        /// Builds a summary from live run state. Must be called while <paramref name="run"/> is still
        /// populated. <paramref name="database"/> may be null — faction names then fall back to defaults.
        /// </summary>
        public static RunSummary FromRun(RunData run, MetaProgressData meta, RunEndReason reason,
                                         GameDatabase database)
        {
            var summary = new RunSummary { Reason = reason };
            if (run == null)
            {
                Debug.LogWarning("[RunSummary] FromRun called with a null run — returning an empty summary.");
                return summary;
            }

            bool survived = reason == RunEndReason.Extracted
                         || reason == RunEndReason.VictoryStabilization
                         || reason == RunEndReason.VictoryRelief
                         || reason == RunEndReason.VictoryAdaptation
                         || reason == RunEndReason.VictoryIndependent;

            summary.Survived = survived;
            summary.Headline = HeadlineFor(reason);
            summary.Subheadline = SubheadlineFor(reason);
            summary.ClosingLine = ClosingLineFor(reason);
            summary.SiteName = ScavengeSiteCatalog.DisplayNameOf(run.currentScavengeSiteId);

            summary.DaysSurvived = run.currentDay;
            summary.SalvageRatePercent = Mathf.RoundToInt(BalanceConstants.SALVAGE_RATE_ON_DEATH * 100f);

            int lost = 0, remaining = 0;
            foreach (var member in run.ActiveCrew)
            {
                if (member == null) continue;
                if (member.isAlive) remaining++;
                else lost++;
            }
            summary.CrewLost = lost;
            summary.CrewRemaining = remaining;

            int units = 0;
            foreach (var stack in run.BunkerInventory)
                if (stack != null) units += Mathf.Max(0, stack.quantity);
            summary.ItemsRecovered = units;
            summary.ItemsSalvaged = survived
                ? units
                : Mathf.FloorToInt(units * BalanceConstants.SALVAGE_RATE_ON_DEATH);

            summary.Reputations.Add(new KeyValuePair<string, int>(
                FactionName(database, FactionId.ScaleSociety, "Scale Society"), run.repScaleSociety));
            summary.Reputations.Add(new KeyValuePair<string, int>(
                FactionName(database, FactionId.Cordon, "Cordon"), run.repCordon));
            summary.Reputations.Add(new KeyValuePair<string, int>(
                FactionName(database, FactionId.Kafedra, "Kafedra"), run.repKafedra));

            if (meta != null)
            {
                summary.TotalRunsAttempted = meta.totalRunsAttempted;
                summary.TotalRunsSurvived = meta.totalRunsSurvived;
            }

            return summary;
        }

        private static string FactionName(GameDatabase database, FactionId id, string fallback)
        {
            if (database == null) return fallback;
            var data = database.GetFaction(id);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : fallback;
        }

        private static string HeadlineFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.AllCrewDead:            return "REGISTRATION CLOSED";
                case RunEndReason.BunkerBreach:           return "SITE DEREGISTERED";
                case RunEndReason.Quit:                   return "FILE WITHDRAWN";
                case RunEndReason.Extracted:              return "EXTRACTION LOGGED";
                case RunEndReason.VictoryStabilization:   return "CONDITION STABILISED";
                case RunEndReason.VictoryRelief:          return "RELIEF COLUMN ARRIVED";
                case RunEndReason.VictoryAdaptation:      return "ADAPTATION RECORDED";
                case RunEndReason.VictoryIndependent:     return "STATUS: INDEPENDENT";
                default:                                  return "CASE FILED";
            }
        }

        private static string SubheadlineFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.AllCrewDead:
                    return "NO SURVIVING PERSONNEL AT THIS ADDRESS. FILE CLOSED PENDING NEXT OF KIN.";
                case RunEndReason.BunkerBreach:
                    return "SHELTER INTEGRITY LOST. THE ADDRESS IS NO LONGER LISTED.";
                case RunEndReason.Quit:
                    return "APPLICANT WITHDREW BEFORE THE PERIOD CLOSED. NO ADJUSTMENT MADE.";
                default:
                    return "EXPEDITION CONCLUDED. DOCUMENTATION WILL FOLLOW.";
            }
        }

        private static string ClosingLineFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.AllCrewDead:
                    return "\"The quota for the period was met by other means.\"";
                case RunEndReason.BunkerBreach:
                    return "\"Structural deviation noted. A revised floor plan has been requisitioned.\"";
                case RunEndReason.Quit:
                    return "\"The form was returned incomplete. This is not, in itself, an irregularity.\"";
                default:
                    return "\"Retain this record. It will not be issued again.\"";
            }
        }
    }
}
