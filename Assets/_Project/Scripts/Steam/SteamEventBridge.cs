// Assets/_Project/Scripts/Steam/SteamEventBridge.cs
// Subscribes to game EventBus events and translates them into Steam stat/achievement updates.
// Add this component alongside SteamManager (same GameObject is fine).
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Steam
{
    /// <summary>
    /// Bridges game events onto Steam stats and achievements. Attach to the same [SteamManager] GameObject.
    /// Every Steam call is a no-op without the STEAMWORKS define, so this is safe to leave enabled always.
    ///
    /// <para><b>Where the per-run state lives.</b> Six of the twenty achievements are conditions across a
    /// whole run ("won without a crew death", "won without using a medical item"). Those cannot be answered
    /// from any single event, so this component accumulates them between <c>RunStartedEvent</c> and
    /// <c>RunEndedEvent</c> and evaluates them once, at the end. The accumulators are deliberately NOT in
    /// RunData: they are not game state, nothing in the game reads them, and putting them there would make
    /// every save file carry Steam bookkeeping — and would make them survive a load, which is exactly wrong
    /// for a "never did X this run" claim on a run resumed from disk.</para>
    ///
    /// <para><b>What a resumed run does.</b> <c>RunStartedEvent</c> does not fire on a resume, so the
    /// accumulators keep their conservative starting values and the no-death / no-medical achievements stay
    /// unavailable for that run rather than being awarded on incomplete evidence. Understating is the right
    /// failure direction: a missed unlock annoys, an unearned one devalues the whole list.</para>
    ///
    /// <para><b>Cross-run counters</b> (artifacts found, catalogue completion) read the profile rather than
    /// this component's fields, because they are meta-progression by definition.</para>
    /// </summary>
    public class SteamEventBridge : MonoBehaviour
    {
        // Faction string ids as produced by ManagerEventBridge (FactionId.ToString()).
        private const string FactionScaleSociety = "ScaleSociety";
        private const string FactionCordon = "Cordon";
        private const string FactionKafedra = "Kafedra";

        /// <summary>Fraction of carry capacity that counts as a full pack for the logistics achievement.</summary>
        private const float FullPackFraction = 0.95f;

        /// <summary>Artifacts recovered across all runs before the collector award fires.</summary>
        private const int ArtifactCollectorThreshold = 5;

        /// <summary>Day on or before which a victory counts as an express filing.</summary>
        private const int SpeedRunMaxDays = 20;

        private SteamConfig cfg;

        // ── Per-run accumulators ─────────────────────────────────────────────
        // Reset on RunStartedEvent, read on RunEndedEvent.

        /// <summary>Highest day reached this run.</summary>
        private int _currentRunDay;

        /// <summary>False once a crew member has died this run.</summary>
        private bool _rosterIntact;

        /// <summary>False once a medical item has left the bunker stores this run.</summary>
        private bool _medicalUntouched;

        /// <summary>True once a run has actually been observed from its start.</summary>
        private bool _runObservedFromStart;

        /// <summary>Highest fraction of carry capacity reached during the Blowout.</summary>
        private float _peakLoadFraction;

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
            EventBus.Subscribe<ItemPickedUpEvent>(OnItemPickedUp);
            EventBus.Subscribe<CrewRescuedEvent>(OnCrewRescued);
            EventBus.Subscribe<ScavengeLoadChangedEvent>(OnScavengeLoadChanged);
            EventBus.Subscribe<BunkerItemConsumedEvent>(OnBunkerItemConsumed);

            Debug.Log("[SteamEventBridge] Subscribed to game events.");
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<RunStartedEvent>(OnRunStarted);
            EventBus.Unsubscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<FactionReputationChangedEvent>(OnRepChanged);
            EventBus.Unsubscribe<CrewDiedEvent>(OnCrewDied);
            EventBus.Unsubscribe<ItemPickedUpEvent>(OnItemPickedUp);
            EventBus.Unsubscribe<CrewRescuedEvent>(OnCrewRescued);
            EventBus.Unsubscribe<ScavengeLoadChangedEvent>(OnScavengeLoadChanged);
            EventBus.Unsubscribe<BunkerItemConsumedEvent>(OnBunkerItemConsumed);
        }

        // ── Run lifecycle ────────────────────────────────────────────────────

        private void OnRunStarted(RunStartedEvent e)
        {
            _currentRunDay = 0;
            _rosterIntact = true;
            _medicalUntouched = true;
            _runObservedFromStart = true;
            _peakLoadFraction = 0f;

            SteamAchievementsService.Unlock(cfg.achvFirstRun);
            SteamStatsService.IncrementInt(cfg.statRunsStarted);
        }

        private void OnRunEnded(RunEndedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statRunsEnded);
            SteamStatsService.SetIntIfHigher(cfg.statLongestRunDays, _currentRunDay);

            bool victory = IsVictory(e.Reason);

            if (victory)
            {
                SteamAchievementsService.Unlock(cfg.achvFirstVictory);
                SteamStatsService.IncrementInt(cfg.statTotalWins);
                UnlockEndingFor(e.Reason);
                AwardRunConstraintAchievements();
                UnlockAllEndingsIfComplete();
            }
            else if (e.Reason == RunEndReason.AllCrewDead || e.Reason == RunEndReason.BunkerBreach)
            {
                SteamAchievementsService.Unlock(cfg.achvFirstWipe);
            }

            // Profile-scoped awards are evaluated at run end because that is when the profile is written.
            EvaluateProfileAchievements();

            // A finished run must not leave its accumulators readable by the next one. Clearing the
            // observed flag is what makes a resumed run — which never raises RunStartedEvent — fall back to
            // the conservative "cannot claim a constraint achievement" state.
            _runObservedFromStart = false;
            _peakLoadFraction = 0f;
        }

        private static bool IsVictory(RunEndReason reason)
            => reason == RunEndReason.VictoryStabilization ||
               reason == RunEndReason.VictoryRelief ||
               reason == RunEndReason.VictoryAdaptation ||
               reason == RunEndReason.VictoryIndependent;

        private void UnlockEndingFor(RunEndReason reason)
        {
            switch (reason)
            {
                case RunEndReason.VictoryStabilization:
                    SteamAchievementsService.Unlock(cfg.achvEndingStabilization);
                    SteamStatsService.IncrementInt(cfg.statWinsStabilization);
                    break;
                case RunEndReason.VictoryRelief:
                    SteamAchievementsService.Unlock(cfg.achvEndingRelief);
                    SteamStatsService.IncrementInt(cfg.statWinsRelief);
                    break;
                case RunEndReason.VictoryAdaptation:
                    SteamAchievementsService.Unlock(cfg.achvEndingAdaptation);
                    SteamStatsService.IncrementInt(cfg.statWinsAdaptation);
                    break;
                case RunEndReason.VictoryIndependent:
                    SteamAchievementsService.Unlock(cfg.achvEndingIndependent);
                    SteamStatsService.IncrementInt(cfg.statWinsIndependent);
                    break;
            }
        }

        /// <summary>
        /// The four "won a run while never doing X" awards. Only evaluated when the run was observed from
        /// its own start — see the class remarks on resumed runs.
        /// </summary>
        private void AwardRunConstraintAchievements()
        {
            if (!_runObservedFromStart)
            {
                Debug.Log("[SteamEventBridge] Run was resumed rather than observed from its start — " +
                          "constraint achievements are not evaluated for it.");
                return;
            }

            if (_rosterIntact) SteamAchievementsService.Unlock(cfg.achvNoDeaths);
            if (_medicalUntouched) SteamAchievementsService.Unlock(cfg.achvNoMedical);
            if (_currentRunDay > 0 && _currentRunDay <= SpeedRunMaxDays)
                SteamAchievementsService.Unlock(cfg.achvSpeedRun);
            if (_peakLoadFraction >= FullPackFraction)
                SteamAchievementsService.Unlock(cfg.achvCarryMaster);
        }

        /// <summary>
        /// Awards that depend on the profile rather than on one run: artifacts recovered across all runs,
        /// and owning the whole supply-office catalogue.
        /// </summary>
        private void EvaluateProfileAchievements()
        {
            var meta = GameManager.Instance != null ? GameManager.Instance.MetaProgress : null;
            if (meta == null) return;

            SteamStatsService.SetIntIfHigher(cfg.statSalvageTokensEarned, meta.lifetimeSalvageTokens);

            if (meta.purchasedUnlockIds != null &&
                meta.purchasedUnlockIds.Count >= MetaUnlockCatalog.All.Count)
            {
                SteamAchievementsService.Unlock(cfg.achvFullUnlock);
            }
        }

        // ── Day / stat events ────────────────────────────────────────────────

        private void OnDayAdvanced(DayAdvancedEvent e)
        {
            _currentRunDay = e.NewDay;

            SteamStatsService.IncrementInt(cfg.statDaysSurvivedTotal);
            SteamStatsService.SetIntIfHigher(cfg.statLongestRunDays, _currentRunDay);

            if (_currentRunDay >= 10) SteamAchievementsService.Unlock(cfg.achvSurvive10Days);
            if (_currentRunDay >= 30) SteamAchievementsService.Unlock(cfg.achvSurvive30Days);
            if (_currentRunDay >= 60) SteamAchievementsService.Unlock(cfg.achvSurvive60Days);
        }

        private void OnRepChanged(FactionReputationChangedEvent e)
        {
            if (e.NewRep >= BalanceConstants.ENDGAME_REPUTATION_THRESHOLD)
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

            // Hunted by all three at once. Read from the live run rather than tracked incrementally: the
            // condition is about a simultaneous state, and three independent counters would each have to
            // agree about when they stopped being true.
            EvaluateHuntedByAll();
        }

        private void EvaluateHuntedByAll()
        {
            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            if (run == null) return;

            int threshold = BalanceConstants.HUNTED_REPUTATION_THRESHOLD;
            if (run.repScaleSociety <= threshold &&
                run.repCordon <= threshold &&
                run.repKafedra <= threshold)
            {
                SteamAchievementsService.Unlock(cfg.achvHuntedByAll);
            }
        }

        private void OnCrewDied(CrewDiedEvent e)
        {
            _rosterIntact = false;
            SteamStatsService.IncrementInt(cfg.statCrewDeathsTotal);
        }

        private void OnCrewRescued(CrewRescuedEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statCrewRescuedTotal);
        }

        private void OnItemPickedUp(ItemPickedUpEvent e)
        {
            SteamStatsService.IncrementInt(cfg.statItemsScavengedTotal);

            if (!IsArtifact(e.ItemDataId)) return;

            SteamStatsService.IncrementInt(cfg.statArtifactsFoundTotal);
            if (SteamStatsService.GetInt(cfg.statArtifactsFoundTotal) >= ArtifactCollectorThreshold)
                SteamAchievementsService.Unlock(cfg.achvArtifactCollector);
        }

        /// <summary>
        /// Tracks the fullest the pack ever got. Sampled from the load event rather than read once at the
        /// bunker door, because the player may drop below capacity on the last pickup and the achievement
        /// is about having hauled a full pack, not about the exact weight at the threshold.
        /// </summary>
        private void OnScavengeLoadChanged(ScavengeLoadChangedEvent e)
        {
            if (e.CapacityKg <= 0f) return;
            float fraction = e.CurrentKg / e.CapacityKg;
            if (fraction > _peakLoadFraction) _peakLoadFraction = fraction;
        }

        private void OnBunkerItemConsumed(BunkerItemConsumedEvent e)
        {
            if (e.Category == ItemCategory.Medical) _medicalUntouched = false;
        }

        private static bool IsArtifact(string itemDataId)
        {
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            if (db == null || string.IsNullOrEmpty(itemDataId)) return false;

            var data = db.GetItem(itemDataId);
            return data != null && data.category == ItemCategory.Artifact;
        }

        /// <summary>
        /// Grants the completionist award once all four ending achievements are unlocked on Steam.
        ///
        /// Steam itself is the source of truth rather than <c>MetaProgressData.unlockedEndings</c>, so the
        /// award survives a deleted local profile — a player who reinstalls has not un-seen three endings.
        /// </summary>
        private void UnlockAllEndingsIfComplete()
        {
            if (SteamAchievementsService.IsUnlocked(cfg.achvAllEndings)) return;

            bool complete =
                SteamAchievementsService.IsUnlocked(cfg.achvEndingStabilization) &&
                SteamAchievementsService.IsUnlocked(cfg.achvEndingRelief) &&
                SteamAchievementsService.IsUnlocked(cfg.achvEndingAdaptation) &&
                SteamAchievementsService.IsUnlocked(cfg.achvEndingIndependent);

            if (complete) SteamAchievementsService.Unlock(cfg.achvAllEndings);
        }
    }
}
