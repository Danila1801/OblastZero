// Assets/_Project/Scripts/Steam/SteamConfig.cs
using UnityEngine;

namespace OblastZero.Steam
{
    /// <summary>
    /// Single source of truth for Steam App ID, achievement keys, and stat keys.
    /// Create one asset via Assets → Create → OblastZero/Steam/Config and assign to SteamManager.
    ///
    /// <para><b>The keys here must match the Steamworks Admin panel character for character.</b> A key that
    /// does not exist in the panel fails silently — <c>Achievement.Trigger()</c> returns without unlocking
    /// and without throwing — so a typo here ships as an achievement nobody can ever earn. The catalogue in
    /// <c>docs/STEAM_ACHIEVEMENTS.md</c> is the copy that goes into the panel; these fields and that document
    /// are the two halves of one contract.</para>
    ///
    /// <para><b>App ID.</b> 480 is Spacewar, Valve's public test app. It is the correct value for local
    /// testing and the wrong value to ship: with 480 the overlay initialises, stats round-trip, and nothing
    /// reaches the real app's leaderboards. Replace it with the real App ID before the first depot upload —
    /// and update <c>steam_appid.txt</c> at the repo root to match, since that file is what the local
    /// process reads when it launches outside the Steam client.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "OblastZero/Steam/Config", fileName = "SteamConfig")]
    public class SteamConfig : ScriptableObject
    {
        [Header("App")]
        [Tooltip("Steam App ID. 480 (Spacewar) is the test value — replace before shipping, and keep " +
                 "steam_appid.txt at the repo root in step with it.")]
        public uint appId = 480;

        // ─── Progression ────────────────────────────────────────────────────────
        // Visible achievements: a player who has not bought the game can read these on the store page and
        // learn only that the game has runs, days and endings.

        [Header("Achievements — progression (visible)")]
        [Tooltip("Complete a first registration (any run start).")]
        public string achvFirstRun = "ACH_FIRST_FILING";

        [Tooltip("First run that ended in a wipe.")]
        public string achvFirstWipe = "ACH_FIRST_CLOSURE";

        [Tooltip("Survive to day 10.")]
        public string achvSurvive10Days = "ACH_CASE_RESOLVED";

        [Tooltip("Survive to day 30.")]
        public string achvSurvive30Days = "ACH_EXTENDED_FILING";

        [Tooltip("Survive to day 60.")]
        public string achvSurvive60Days = "ACH_PERMANENT_RECORD";

        [Tooltip("Win a run by any of the four endings.")]
        public string achvFirstVictory = "ACH_FIRST_RESOLUTION";

        // ─── Endings ────────────────────────────────────────────────────────────
        // One per victory branch, plus the completionist. Named by their in-game alignment rather than by
        // what happens, so the list does not spoil the endings on the store page.

        [Header("Achievements — endings (visible)")]
        public string achvEndingStabilization = "ACH_ALIGNMENT_STABILIZATION";
        public string achvEndingRelief = "ACH_ALIGNMENT_RELIEF";
        public string achvEndingAdaptation = "ACH_ALIGNMENT_ADAPTATION";
        public string achvEndingIndependent = "ACH_ALIGNMENT_UNALIGNED";
        public string achvAllEndings = "ACH_COMPLETE_ARCHIVE";

        // ─── Faction standing ───────────────────────────────────────────────────

        [Header("Achievements — standing (visible)")]
        [Tooltip("Reach the endgame reputation threshold with the Scale Society.")]
        public string achvMaxRepScaleSociety = "ACH_STANDING_SOCIETY";

        [Tooltip("Reach the endgame reputation threshold with the Cordon.")]
        public string achvMaxRepCordon = "ACH_STANDING_CORDON";

        [Tooltip("Reach the endgame reputation threshold with the Kafedra.")]
        public string achvMaxRepKafedra = "ACH_STANDING_KAFEDRA";

        [Tooltip("Hold hunted status with all three factions at once.")]
        public string achvHuntedByAll = "ACH_DESIGNATION_HUNTED";

        // ─── Challenge (hidden) ─────────────────────────────────────────────────
        // Hidden in the panel: each one describes a constraint, and reading the constraint is most of the
        // solution. They are the achievements a second playthrough is for.

        [Header("Achievements — challenge (hidden)")]
        [Tooltip("Win a run in which no crew member died.")]
        public string achvNoDeaths = "ACH_FULL_ROSTER";

        [Tooltip("Win a run without consuming a single medical item.")]
        public string achvNoMedical = "ACH_RESOURCEFUL";

        [Tooltip("Win a run on or before day 20.")]
        public string achvSpeedRun = "ACH_EXPRESS_FILING";

        [Tooltip("Reach the bunker door carrying at least 95% of capacity.")]
        public string achvCarryMaster = "ACH_LOGISTICS_SPECIALIST";

        [Tooltip("Recover five artifacts across all runs on this profile.")]
        public string achvArtifactCollector = "ACH_ARTIFACT_COLLECTOR";

        [Tooltip("Own every entry in the supply office catalogue.")]
        public string achvFullUnlock = "ACH_PROVISIONING_COMPLETE";

        // ─── Stats ──────────────────────────────────────────────────────────────
        // Every stat here is INT. Steam supports float stats, but an int survives a partial write
        // unambiguously and reads correctly in the panel's own charts, and nothing tracked here is
        // fractional.

        [Header("Stats — cumulative (keys must match Steamworks Admin panel)")]
        public string statRunsStarted = "stat_total_runs";
        public string statRunsEnded = "stat_runs_ended";
        public string statTotalWins = "stat_total_wins";
        public string statDaysSurvivedTotal = "stat_days_survived_total";
        public string statCrewDeathsTotal = "stat_deaths_total";
        public string statItemsScavengedTotal = "stat_items_scavenged_total";
        public string statCrewRescuedTotal = "stat_crew_rescued_total";
        public string statArtifactsFoundTotal = "stat_artifacts_found_total";
        public string statSalvageTokensEarned = "stat_salvage_tokens_earned";

        [Header("Stats — per-ending tallies")]
        public string statWinsStabilization = "stat_wins_by_stabilization";
        public string statWinsRelief = "stat_wins_by_relief";
        public string statWinsAdaptation = "stat_wins_by_adaptation";
        public string statWinsIndependent = "stat_wins_by_independent";

        [Header("Stats — records")]
        public string statLongestRunDays = "stat_longest_run_days";
        public string statHighestRep = "stat_highest_rep";
    }
}
