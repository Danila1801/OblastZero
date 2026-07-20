// Assets/_Project/Scripts/Core/States/SurvivalPhase2DState.cs
using UnityEngine;
using OblastZero.Gameplay;

namespace OblastZero.Core
{
    /// <summary>
    /// The long bunker state (Step 4). Pulls the run-scoped managers and content database from GameManager,
    /// owns a <see cref="BunkerDayController"/>, and exposes <see cref="AdvanceDay"/> for the bunker UI's
    /// "End Day" action. Turn-based: nothing advances per-frame; the day moves on player command. After each
    /// day it checks for an all-crew-dead failure and ends the run, routing to RunFailed.
    /// </summary>
    public class SurvivalPhase2DState : BaseGameState
    {
        public override string StateId => "SurvivalPhase2D";
        public override GameState StateEnum => GameState.SurvivalPhase2D;

        private BunkerDayController _dayController;

        protected override void HandleEnter()
        {
            var run = Context?.CurrentRun;
            if (run == null)
            {
                Debug.LogError("[SurvivalPhase2D] Entered with no active run. Cannot start the bunker phase.");
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[SurvivalPhase2D] No GameManager instance available.");
                return;
            }

            var inventory = gm.Inventory;
            var crew = gm.Crew;
            var database = gm.Database;
            var saveService = ServiceLocator.Get<ISaveService>();

            if (inventory == null || crew == null || database == null)
            {
                Debug.LogError("[SurvivalPhase2D] Data layer unavailable (GameDatabase assigned on GameManager?). " +
                               "Cannot start the bunker phase.");
                return;
            }

            _dayController = new BunkerDayController(run, inventory, crew, database, new BunkerDayConfig(), saveService);

            Debug.Log($"[SurvivalPhase2D] Entered. Day {run.currentDay}, crew alive {crew.AliveCount()}, " +
                      $"bunker items {run.BunkerInventory.Count}.");

            // The additive bunker-scene load goes through ISceneLoader here once its API is wired
            // (kept out of this logic wrapper so the day engine stays scene-independent).
        }

        protected override void HandleExit()
        {
            _dayController = null;
        }

        // Turn-based: no per-frame logic. (HandleTick stays the base no-op.)

        /// <summary>Advances one bunker day and resolves run-end conditions. Called by the bunker UI.</summary>
        public void AdvanceDay()
        {
            if (_dayController == null)
            {
                Debug.LogWarning("[SurvivalPhase2D] AdvanceDay called before the day controller was ready.");
                return;
            }

            DayResult result = _dayController.AdvanceDay();

            if (result.aliveRemaining <= 0)
            {
                Debug.Log("[SurvivalPhase2D] All crew dead — ending run.");
                GameManager.Instance.EndCurrentRun(RunEndReason.AllCrewDead);
                RequestTransition(GameState.RunFailed);
            }
        }
    }
}
