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

        /// <summary>
        /// Salvage Tokens this run earned, already computed. <see cref="GameManager.EndCurrentRun"/> credits
        /// this to the profile; the summary screen displays it.
        /// </summary>
        public int SalvageTokensAwarded;

        /// <summary>Token balance after the award, for the summary line. Filled by GameManager after crediting.</summary>
        public int SalvageTokenBalance;

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

            summary.SalvageTokensAwarded = ComputeSalvageTokens(summary, run);

            return summary;
        }

        /// <summary>
        /// The token award for a finished run: tenure, plus the haul that actually came home, plus standing,
        /// plus a flat bonus for reaching an ending.
        ///
        /// <para>The haul term reads <see cref="ItemsSalvaged"/>, not <see cref="ItemsRecovered"/>. Those two
        /// already differ by <see cref="BalanceConstants.SALVAGE_RATE_ON_DEATH"/> — 33% of the shelf on a
        /// wipe, all of it on a win — so the death penalty arrives once, through the count. Multiplying the
        /// recovered count and then applying the salvage rate again would charge a dying run for its losses
        /// twice, which is the kind of double-count that reads as "meta progression feels stingy" long before
        /// anyone finds it in the formula.</para>
        ///
        /// <para>Reputation sums across all three factions and may be negative — a run that antagonised
        /// everyone is worth less than a quiet one. The total floors at zero: an award is a payment, and a
        /// payment is never a debt.</para>
        /// </summary>
        private static int ComputeSalvageTokens(RunSummary summary, RunData run)
        {
            float tokens = summary.DaysSurvived * BalanceConstants.TOKENS_PER_DAY_SURVIVED;
            tokens += summary.ItemsSalvaged * BalanceConstants.TOKENS_PER_ITEM_RECOVERED;

            int repTotal = run.repScaleSociety + run.repCordon + run.repKafedra;
            tokens += repTotal * BalanceConstants.TOKENS_PER_REPUTATION_POINT;

            if (summary.Survived) tokens += BalanceConstants.TOKENS_VICTORY_BONUS;

            int award = Mathf.Max(0, Mathf.FloorToInt(tokens));

            Debug.Log($"[RunSummary] Salvage token award {award} = " +
                      $"{summary.DaysSurvived}d x{BalanceConstants.TOKENS_PER_DAY_SURVIVED} + " +
                      $"{summary.ItemsSalvaged} salvaged x{BalanceConstants.TOKENS_PER_ITEM_RECOVERED} + " +
                      $"{repTotal} rep x{BalanceConstants.TOKENS_PER_REPUTATION_POINT}" +
                      (summary.Survived ? $" + {BalanceConstants.TOKENS_VICTORY_BONUS} victory" : string.Empty) + ".");

            return award;
        }

        private static string FactionName(GameDatabase database, FactionId id, string fallback)
        {
            if (database == null) return fallback;
            var data = database.GetFaction(id);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : fallback;
        }

        // The three verdict lines are resolved through the localization table at snapshot time rather than
        // stored as English. A summary is built once, inside EndCurrentRun, and then read by whichever
        // run-end state follows — so the language in force when the run ended is the language on the screen,
        // which is the correct behaviour for a record that is supposed to read as a filed document.

        private static string HeadlineFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.AllCrewDead:            return LocalizedStrings.Get(UIStringKeys.VerdictAllCrewDead);
                case RunEndReason.BunkerBreach:           return LocalizedStrings.Get(UIStringKeys.VerdictBunkerBreach);
                case RunEndReason.Quit:                   return LocalizedStrings.Get(UIStringKeys.VerdictQuit);
                case RunEndReason.Extracted:              return LocalizedStrings.Get(UIStringKeys.VerdictExtracted);
                case RunEndReason.VictoryStabilization:   return LocalizedStrings.Get(UIStringKeys.VerdictStabilization);
                case RunEndReason.VictoryRelief:          return LocalizedStrings.Get(UIStringKeys.VerdictRelief);
                case RunEndReason.VictoryAdaptation:      return LocalizedStrings.Get(UIStringKeys.VerdictAdaptation);
                case RunEndReason.VictoryIndependent:     return LocalizedStrings.Get(UIStringKeys.VerdictIndependent);
                default:                                  return LocalizedStrings.Get(UIStringKeys.VerdictDefault);
            }
        }

        private static string SubheadlineFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.AllCrewDead:  return LocalizedStrings.Get(UIStringKeys.VerdictSubAllCrewDead);
                case RunEndReason.BunkerBreach: return LocalizedStrings.Get(UIStringKeys.VerdictSubBunkerBreach);
                case RunEndReason.Quit:         return LocalizedStrings.Get(UIStringKeys.VerdictSubQuit);
                default:                        return LocalizedStrings.Get(UIStringKeys.VerdictSubDefault);
            }
        }

        private static string ClosingLineFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.AllCrewDead:  return LocalizedStrings.Get(UIStringKeys.VerdictCloseAllCrewDead);
                case RunEndReason.BunkerBreach: return LocalizedStrings.Get(UIStringKeys.VerdictCloseBunkerBreach);
                case RunEndReason.Quit:         return LocalizedStrings.Get(UIStringKeys.VerdictCloseQuit);
                default:                        return LocalizedStrings.Get(UIStringKeys.VerdictCloseDefault);
            }
        }
    }
}
