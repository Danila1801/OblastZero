using UnityEngine;
using UnityEngine.UI;
using OblastZero.Gameplay;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Displayed when the run ends in failure (all crew dead, bunker breach, or player quit).
    /// Shows a run summary screen: days survived, crew lost, salvage applied.
    /// EndCurrentRun() is called BEFORE the transition (from SurvivalPhase2DState), so this
    /// state only displays the outcome and waits for the player to acknowledge.
    /// </summary>
    public class RunFailedState : BaseGameState
    {
        public override string StateId => "RunFailed";
        public override GameState StateEnum => GameState.RunFailed;

        private FailedRunUI _ui;

        protected override void HandleEnter()
        {
            var run = Context?.CurrentRun;
            if (run == null)
            {
                Debug.LogError("[RunFailedState] Entered with no CurrentRun (already cleared). " +
                               "This should not happen — EndCurrentRun should be called before transition.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            var meta = Context?.MetaProgress;
            if (meta == null)
            {
                Debug.LogError("[RunFailedState] MetaProgress unavailable.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            Debug.Log($"[RunFailedState] Run ended. Days survived: {run.currentDay}, " +
                      $"crew alive: {run.ActiveCrew.FindAll(c => c.isAlive).Count}, " +
                      $"bunker items salvaged: {Mathf.FloorToInt(run.BunkerInventory.Count * BalanceConstants.SALVAGE_RATE_ON_DEATH)}");

            // Build and show the failed-run summary UI.
            _ui = new FailedRunUI(run, meta);
            _ui.OnContinuePressed += OnContinuePressed;
            _ui.Show();
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.OnContinuePressed -= OnContinuePressed;
                _ui.Hide();
                _ui = null;
            }

            Debug.Log("[RunFailedState] Exited — returning to MainMenu.");
        }

        private void OnContinuePressed()
        {
            RequestTransition(GameState.MainMenu);
        }
    }

    /// <summary>
    /// Self-building UI panel for run failure. Mirrors the pattern of BunkerHUD / EventModalUI:
    /// constructs its own Canvas on demand, subscribes to no events (only internal state).
    /// </summary>
    internal class FailedRunUI
    {
        private RunData _run;
        private MetaProgressData _meta;
        private Canvas _canvas;
        private GameObject _root;

        public event System.Action OnContinuePressed;

        public FailedRunUI(RunData run, MetaProgressData meta)
        {
            _run = run;
            _meta = meta;
        }

        public void Show()
        {
            // Create a root canvas if none exists.
            _root = new GameObject("FailedRunUI");
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // Above all other UI.

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Background panel.
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_root.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.95f); // Dark overlay.
            var bgLayout = bgGo.AddComponent<LayoutElement>();
            bgLayout.preferredWidth = 1920;
            bgLayout.preferredHeight = 1080;

            // Content panel (centered).
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(_root.transform, false);
            var contentLayout = contentGo.AddComponent<LayoutElement>();
            contentLayout.preferredWidth = 800;
            contentLayout.preferredHeight = 600;
            contentGo.AddComponent<VerticalLayoutGroup>();

            // Title.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(contentGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "— EXPEDITION FAILED —";
            titleText.font = Resources.Load<Font>("Arial");
            titleText.fontSize = 48;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.9f, 0.3f, 0.3f, 1f); // Red.

            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 100;

            // Summary stats.
            var summaryGo = new GameObject("Summary");
            summaryGo.transform.SetParent(contentGo.transform, false);
            var summaryText = summaryGo.AddComponent<Text>();

            int daysAlive = _run.currentDay;
            int crewDeaths = _run.ActiveCrew.FindAll(c => !c.isAlive).Count;
            int salvageCount = Mathf.FloorToInt(_run.BunkerInventory.Count * BalanceConstants.SALVAGE_RATE_ON_DEATH);

            summaryText.text = $"Days Survived: {daysAlive}\n" +
                               $"Crew Lost: {crewDeaths}\n" +
                               $"Items Salvaged: {salvageCount}\n" +
                               $"\n" +
                               $"Total Runs: {_meta.totalRunsAttempted}\n" +
                               $"Successful Extractions: {_meta.totalRunsSurvived}";
            summaryText.font = Resources.Load<Font>("Arial");
            summaryText.fontSize = 32;
            summaryText.alignment = TextAnchor.UpperCenter;
            summaryText.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Light gray.

            var summaryLayout = summaryGo.AddComponent<LayoutElement>();
            summaryLayout.preferredHeight = 300;

            // Continue button.
            var buttonGo = new GameObject("ContinueButton");
            buttonGo.transform.SetParent(contentGo.transform, false);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray button.
            var button = buttonGo.AddComponent<Button>();

            var buttonText = new GameObject("Text");
            buttonText.transform.SetParent(buttonGo.transform, false);
            var buttonTextComp = buttonText.AddComponent<Text>();
            buttonTextComp.text = "Return to Menu";
            buttonTextComp.font = Resources.Load<Font>("Arial");
            buttonTextComp.fontSize = 28;
            buttonTextComp.fontStyle = FontStyle.Bold;
            buttonTextComp.alignment = TextAnchor.MiddleCenter;
            buttonTextComp.color = Color.white;

            button.onClick.AddListener(() => OnContinuePressed?.Invoke());

            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.preferredHeight = 80;

            Debug.Log("[FailedRunUI] UI displayed.");
        }

        public void Hide()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
                _canvas = null;
            }
        }
    }
}
