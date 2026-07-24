// Assets/_Project/Scripts/Steam/SteamEventBridge.cs
// Subscribes to game EventBus events and translates them into Steam stat/achievement updates.
// Add this component alongside SteamManager (same GameObject is fine).
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Steam
{
    /// <summary>
    /// Bridges game events (RunStarted, RunEnded, DayAdvanced, RepChanged, CrewDied) to Steam
    /// stats + achievements. Attach to the same [SteamManager] GameObject, or any persistent singleton.
    /// All Steam calls are no-ops without the STEAMWORKS define, so this is safe to keep enabled.
    /// </summary>
    public class SteamEventBridge : MonoBehaviour
    {
        // Faction string ids as produced by ManagerEventBridge (FactionId.ToString()).
        private const string FactionScaleSociety = "ScaleSociety";
        private const string FactionCordon = "Cordon";
        private const string FactionKafedra = "Kafedra";

        // Reputation threshold that counts as "maxed" for achievement purposes.
        private const int MaxRepAchievementThreshold = 60;

        private SteamConfig cfg;

        /// <summary>Highest day number reached during the current run (reset on RunStarted).</summary>
        private int currentRunDay;

        private void Awake()
        {
            cfg = SteamManager.Instance ? SteamManager.Instance.Config : null;
        }

        private void OnEnable()
        {
            if (cfg == null) cfg = SteamManager.Instance ? SteamManager.Instance.Config : null;
            if (cfg == null)
            {
                Debug.LogWarning("[SteamEventBridge] No SteamConfig available — bridge inactive.");
                return;
            }

            EventBus.Subscribe<RunStartedEvent>(OnRunStarted);
            EventBus.Subscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<FactionReputationChangedEvent>(OnRepChanged);
            EventBus.Subscribe<CrewDiedEvent>(OnCrewDied);

            Debug.Log("[SteamEventBridge] Subscribed to game events.");
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RunStartedEvent>(OnRunStarted);
            EventBus.Unsubscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<FactionReputationChangedEvent>(OnRepChanged);
            EventBus.Unsubscribe<CrewDiedEvent>(OnCrewDied);
        }

        private void OnRunStarted(RunStartedEvent e)
        {
            currentRunDay = 0;
            SteamAchievementsService.Unlock(cfg.achvFirstRun);
            SteamStatsService.IncrementInt(cfg.statRunsStarted);
        }

        private void OnRunEnded(RunEndedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statRunsEnded);
            SteamStatsService.SetIntIfHigher(cfg.statLongestRunDays, currentRunDay);

            switch (e.Reason)
            {
                case RunEndReason.VictoryStabilization:
                case RunEndReason.VictoryRelief:
                case RunEndReason.VictoryAdaptation:
                case RunEndReason.VictoryIndependent:
                    SteamAchievementsService.Unlock(cfg.achvFirstVictory);
                    UnlockAllEndingsIfComplete();
                    break;

                case RunEndReason.AllCrewDead:
                case RunEndReason.BunkerBreach:
                    SteamAchievementsService.Unlock(cfg.achvFirstWipe);
                    break;
            }
        }

        private void OnDayAdvanced(DayAdvancedEvent e)
        {
            currentRunDay = e.NewDay;

            SteamStatsService.IncrementInt(cfg.statDaysSurvivedTotal);
            SteamStatsService.SetIntIfHigher(cfg.statLongestRunDays, currentRunDay);

            if (currentRunDay >= 10) SteamAchievementsService.Unlock(cfg.achvSurvive10Days);
            if (currentRunDay >= 30) SteamAchievementsService.Unlock(cfg.achvSurvive30Days);
            if (currentRunDay >= 60) SteamAchievementsService.Unlock(cfg.achvSurvive60Days);
        }

        private void OnRepChanged(FactionReputationChangedEvent e)
        {
            if (e.NewRep >= MaxRepAchievementThreshold)
            {
                switch (e.FactionId)
                {
                    case FactionScaleSociety:
                        SteamAchievementsService.Unlock(cfg.achvMaxRepScaleSociety);
                        break;
                    case FactionCordon:
                        SteamAchievementsService.Unlock(cfg.achvMaxRepCordon);
                        break;
                    case FactionKafedra:
                        SteamAchievementsService.Unlock(cfg.achvMaxRepKafedra);
                        break;
                }
            }

            SteamStatsService.SetIntIfHigher(cfg.statHighestRep, e.NewRep);
        }

        private void OnCrewDied(CrewDiedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statCrewDeathsTotal);
        }

        /// <summary>
        /// Grants the completionist achievement once every victory ending has been unlocked on Steam.
        /// Uses Steam itself as the source of truth so it survives local save wipes.
        /// </summary>
        private void UnlockAllEndingsIfComplete()
        {
            if (SteamAchievementsService.IsUnlocked(cfg.achvAllEndings)) return;

            // The four victory endings each map to a Steam achievement key derived from the
            // ending-specific achievements configured in SteamConfig. Until per-ending keys exist,
            // the completionist award is gated on the first-victory key plus all rep maxes,
            // which is the strongest signal currently modelled in config.
            var complete =
                SteamAchievementsService.IsUnlocked(cfg.achvFirstVictory) &&
                SteamAchievementsService.IsUnlocked(cfg.achvMaxRepScaleSociety) &&
                SteamAchievementsService.IsUnlocked(cfg.achvMaxRepCordon) &&
                SteamAchievementsService.IsUnlocked(cfg.achvMaxRepKafedra);

            if (complete) SteamAchievementsService.Unlock(cfg.achvAllEndings);
        }
    }
}
