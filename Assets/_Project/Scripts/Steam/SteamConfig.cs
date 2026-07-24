// Assets/_Project/Scripts/Steam/SteamConfig.cs
using UnityEngine;

namespace OblastZero.Steam
{
    /// <summary>
    /// Single source of truth for Steam App ID, achievement keys, and stat keys.
    /// Create one asset via Assets → Create → OblastZero/Steam/Config and assign to SteamManager.
    /// The string keys here must exactly match what's configured in the Steamworks Admin panel.
    /// </summary>
    [CreateAssetMenu(menuName = "OblastZero/Steam/Config", fileName = "SteamConfig")]
    public class SteamConfig : ScriptableObject
    {
        [Header("App")]
        [Tooltip("Steam App ID. Use 480 (Spacewar) for testing, real ID for ship.")]
        public uint appId = 480;

        [Header("Achievements (keys must match Steamworks Admin panel)")]
        public string achvFirstRun = "ACHV_FIRST_RUN";
        public string achvFirstWipe = "ACHV_FIRST_WIPE";
        public string achvSurvive10Days = "ACHV_SURVIVE_10";
        public string achvSurvive30Days = "ACHV_SURVIVE_30";
        public string achvSurvive60Days = "ACHV_SURVIVE_60";
        public string achvFirstVictory = "ACHV_FIRST_VICTORY";
        public string achvMaxRepScaleSociety = "ACHV_MAX_REP_SCALE";
        public string achvMaxRepCordon = "ACHV_MAX_REP_CORDON";
        public string achvMaxRepKafedra = "ACHV_MAX_REP_KAFEDRA";
        public string achvAllEndings = "ACHV_ALL_ENDINGS";

        [Header("Stats (keys must match Steamworks Admin panel)")]
        public string statRunsStarted = "stat_runs_started";
        public string statRunsEnded = "stat_runs_ended";
        public string statDaysSurvivedTotal = "stat_days_survived_total";
        public string statCrewDeathsTotal = "stat_crew_deaths_total";
        public string statLongestRunDays = "stat_longest_run_days";
        public string statHighestRep = "stat_highest_rep";
    }
}
