using UnityEngine;
using OblastZero.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Title/main menu state. Spawns <see cref="MainMenuUI"/>, listens to its intents, and turns them into
    /// transitions. The screen builds its own canvas, so there is nothing to wire in a scene.
    ///
    /// "Continue" is only offered when an expedition save exists on disk; picking it restores that run and
    /// drops the player back into the bunker rather than starting a fresh registration.
    /// </summary>
    public class MainMenuState : BaseGameState
    {
        public override string StateId => "MainMenu";
        public override GameState StateEnum => GameState.MainMenu;

        private MainMenuUI _ui;

        protected override void HandleEnter()
        {
            Debug.Log("[MainMenuState] Entered — showing title screen.");

            var host = new GameObject("MainMenuUI");
            host.transform.SetParent(transform, false);
            _ui = host.AddComponent<MainMenuUI>();

            _ui.NewRunRequested += OnNewRunPressed;
            _ui.ContinueRequested += OnContinuePressed;
            _ui.QuitRequested += OnQuitPressed;

            _ui.SetContinueAvailable(HasResumableRun());

            var meta = Context?.MetaProgress;
            if (meta != null) _ui.SetRecord(meta.totalRunsAttempted, meta.totalRunsSurvived);
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.NewRunRequested -= OnNewRunPressed;
                _ui.ContinueRequested -= OnContinuePressed;
                _ui.QuitRequested -= OnQuitPressed;
                Destroy(_ui.gameObject);
                _ui = null;
            }

            Debug.Log("[MainMenuState] Exited.");
        }

        /// <summary>True when a saved expedition is on disk and can be resumed.</summary>
        private static bool HasResumableRun()
        {
            if (!ServiceLocator.TryGet<ISaveService>(out var save) || save == null)
            {
                Debug.LogWarning("[MainMenuState] No save service registered — Continue disabled.");
                return false;
            }
            return save.HasExpeditionSave();
        }

        private void OnNewRunPressed()
        {
            Debug.Log("[MainMenuState] New Run selected.");
            RequestTransition(GameState.RunSetup);
        }

        private void OnContinuePressed()
        {
            if (!ServiceLocator.TryGet<ISaveService>(out var save) || save == null)
            {
                Debug.LogError("[MainMenuState] Continue pressed with no save service. Staying on the menu.");
                return;
            }

            var run = save.LoadExpedition();
            if (run == null)
            {
                Debug.LogError("[MainMenuState] Continue pressed but the expedition save failed to load. " +
                               "Disabling Continue and staying on the menu.");
                _ui?.SetContinueAvailable(false);
                return;
            }

            // Restore the run and re-point every run-scoped manager at it before anything reads it.
            Context.CurrentRun = run;
            GameManager.Instance?.RebindManagersToCurrentRun();

            Debug.Log($"[MainMenuState] Resumed run '{run.runId}' at day {run.currentDay} — entering the bunker.");
            RequestTransition(GameState.SurvivalPhase2D);
        }

        private void OnQuitPressed()
        {
            Debug.Log("[MainMenuState] Quit selected.");
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
