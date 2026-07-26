using UnityEngine;
using OblastZero.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Displayed when the run ends in failure (all crew dead, bunker breach, or the player withdrew).
    /// Spawns <see cref="RunSummaryUI"/> and shows the closed case file.
    ///
    /// EndCurrentRun runs BEFORE the transition into this state (SurvivalPhase2DState calls it), so by the
    /// time this state enters, Context.CurrentRun is already null. The numbers come from
    /// <see cref="GameManager.LastRunSummary"/>, which GameManager captures at the moment of closure —
    /// this state displays the outcome and waits for an acknowledgement, nothing more.
    /// </summary>
    public class RunFailedState : BaseGameState
    {
        public override string StateId => "RunFailed";
        public override GameState StateEnum => GameState.RunFailed;

        private RunSummaryUI _ui;

        protected override void HandleEnter()
        {
            var summary = GameManager.Instance != null ? GameManager.Instance.LastRunSummary : null;

            if (summary == null)
            {
                Debug.LogError("[RunFailedState] Entered with no run summary — GameManager.EndCurrentRun " +
                               "should have run before this transition. Returning to MainMenu.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            Debug.Log($"[RunFailedState] Run ended ({summary.Reason}). Days: {summary.DaysSurvived}, " +
                      $"crew lost: {summary.CrewLost}, remaining: {summary.CrewRemaining}, " +
                      $"items recovered: {summary.ItemsRecovered}, salvaged: {summary.ItemsSalvaged}.");

            var host = new GameObject("RunSummaryUI");
            host.transform.SetParent(transform, false);
            _ui = host.AddComponent<RunSummaryUI>();
            _ui.AcknowledgeRequested += OnAcknowledged;
            _ui.Present(summary);
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.AcknowledgeRequested -= OnAcknowledged;
                Destroy(_ui.gameObject);
                _ui = null;
            }

            Debug.Log("[RunFailedState] Exited — returning to MainMenu.");
        }

        private void OnAcknowledged()
        {
            RequestTransition(GameState.MainMenu);
        }
    }
}
