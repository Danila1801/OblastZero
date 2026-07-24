// Assets/_Project/Scripts/Steam/SteamEventBridge.cs
// Subscribes to game EventBus events and translates them into Steam stat/achievement updates.
// Add this component alongside SteamManager (same GameObject is fine).
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Steam
{
    /// <summary>
    /// Bridges game events (RunEnded, DayAdvanced, RepChanged, CrewDied) to Steam stats + achievements.
    /// Attach to the same [SteamManager] GameObject, or any persistent singleton.
    /// </summary>
    public class SteamEventBridge : MonoBehaviour
    {
        private SteamConfig cfg;
        private int longestRunCache;

        private void Awake()
        {
            cfg = SteamManager.Instance ? SteamManager.Instance.Config : null;
        }

        private void OnEnable()
        {
            if (cfg == null) cfg = SteamManager.Instance?.Config;
            if (cfg == null) return;

            longestRunCache = SteamStatsService.GetInt(cfg.statLongestRunDays);

            EventBus.Subscribe<RunStartedEvent>(OnRunStarted);
            EventBus.Subscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<FactionReputationChangedEvent>(OnRepChanged);
            EventBus.Subscribe<CrewDiedEvent>(OnCrewDied);
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
            SteamAchievementsService.Unlock(cfg.achvFirstRun);
            SteamStatsService.IncrementInt(cfg.statRunsStarted);
        }

        private void OnRunEnded(RunEndedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statRunsEnded);

            // Victory achievements (depends on how RunEndReason enum is shaped)
            var reason = e.Reason.ToString();
            if (reason.Contains("Victory") || reason.Contains("Win"))
            {
                SteamAchievementsService.Unlock(cfg.achvFirstVictory);
            }
            else if (reason.Contains("Fail") || reason.Contains("Wipe"))
            {
                SteamAchievementsService.Unlock(cfg.achvFirstWipe);
            }
        }

        private void OnDayAdvanced(DayAdvancedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statDaysSurvivedTotal);

            // Update longest-run stat
            // DayAdvancedEvent may expose DayNumber; if not, we infer by incrementing
            // For now: compare against cached longest and update
            // (Caller is responsible for knowing the current run's day count — see GameManager.RunData)
            // We'll do a simple approach: track local counter per run via RunStartedEvent reset
        }

        private void OnRepChanged(FactionReputationChangedEvent e)
        {
            if (cfg == null) return;
            var newRep = e.NewReputation;

            // Max-rep achievements
            if (newRep >= 60)
            {
                switch (e.Faction)
                {
                    case FactionId.ScaleSociety:
                        SteamAchievementsService.Unlock(cfg.achvMaxRepScaleSociety);
                        break;
                    case FactionId.Cordon:
                        SteamAchievementsService.Unlock(cfg.achvMaxRepCordon);
                        break;
                    case FactionId.Kafedra:
                        SteamAchievementsService.Unlock(cfg.achvMaxRepKafedra);
                        break;
                }
            }

            // Highest-rep stat
            SteamStatsService.SetIntIfHigher(cfg.statHighestRep, newRep);
        }

        private void OnCrewDied(CrewDiedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statCrewDeathsTotal);
        }
    }
}
