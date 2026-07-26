using UnityEngine;
using UnityEngine.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Base class for all victory end states. Handles the common pattern:
    /// - Show ending-specific narrative text + outcome
    /// - Unlock the ending in MetaProgress.unlockedEndings
    /// - Wait for player to acknowledge and return to MainMenu
    ///
    /// Each concrete ending lives in its own file. Unity binds a MonoBehaviour to the script asset
    /// whose file name matches the class name, so several MonoBehaviours sharing one file cannot be
    /// referenced from a scene at all.
    /// </summary>
    public abstract class RunEndVictoryStateBase : BaseGameState
    {
        protected abstract string EndingName { get; }
        protected abstract string EndingTitle { get; }
        protected abstract string EndingNarrative { get; }

        private VictoryRunUI _ui;

        protected override void HandleEnter()
        {
            var run = Context?.CurrentRun;
            var meta = Context?.MetaProgress;

            if (run == null || meta == null)
            {
                Debug.LogError($"[{StateId}] Missing run or meta context. Returning to MainMenu.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            // Unlock this ending in persistent progression.
            if (!meta.unlockedEndings.Contains(EndingName))
            {
                meta.unlockedEndings.Add(EndingName);
                Debug.Log($"[{StateId}] Ending '{EndingName}' unlocked.");
            }

            Debug.Log($"[{StateId}] Victory. Days survived: {run.currentDay}, ending: {EndingName}");

            // Show the victory UI.
            _ui = new VictoryRunUI(EndingTitle, EndingNarrative, run, meta);
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

            Debug.Log($"[{StateId}] Exited.");
        }

        private void OnContinuePressed()
        {
            RequestTransition(GameState.MainMenu);
        }
    }

    /// <summary>
    /// Self-building UI for victory screens. Mirrors FailedRunUI pattern.
    /// Owned solely by <see cref="RunEndVictoryStateBase"/>, so it shares that file.
    /// </summary>
    internal class VictoryRunUI
    {
        private string _title;
        private string _narrative;
        private RunData _run;
        private MetaProgressData _meta;
        private Canvas _canvas;
        private GameObject _root;

        public event System.Action OnContinuePressed;

        public VictoryRunUI(string title, string narrative, RunData run, MetaProgressData meta)
        {
            _title = title;
            _narrative = narrative;
            _run = run;
            _meta = meta;
        }

        public void Show()
        {
            _root = new GameObject("VictoryRunUI");
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Dark overlay background.
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_root.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.95f);
            var bgLayout = bgGo.AddComponent<LayoutElement>();
            bgLayout.preferredWidth = 1920;
            bgLayout.preferredHeight = 1080;

            // Content panel.
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(_root.transform, false);
            var contentLayout = contentGo.AddComponent<LayoutElement>();
            contentLayout.preferredWidth = 900;
            contentLayout.preferredHeight = 650;
            contentGo.AddComponent<VerticalLayoutGroup>();

            // Title.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(contentGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = _title;
            titleText.font = Resources.Load<Font>("Arial");
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.3f, 0.9f, 0.3f, 1f); // Green for victory.

            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 100;

            // Narrative.
            var narrativeGo = new GameObject("Narrative");
            narrativeGo.transform.SetParent(contentGo.transform, false);
            var narrativeText = narrativeGo.AddComponent<Text>();
            narrativeText.text = _narrative;
            narrativeText.font = Resources.Load<Font>("Arial");
            narrativeText.fontSize = 28;
            narrativeText.alignment = TextAnchor.UpperCenter;
            narrativeText.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            narrativeText.horizontalOverflow = HorizontalWrapMode.Wrap;
            narrativeText.verticalOverflow = VerticalWrapMode.Truncate;

            var narrativeLayout = narrativeGo.AddComponent<LayoutElement>();
            narrativeLayout.preferredHeight = 350;

            // Stats.
            var statsGo = new GameObject("Stats");
            statsGo.transform.SetParent(contentGo.transform, false);
            var statsText = statsGo.AddComponent<Text>();
            statsText.text = $"Days Survived: {_run.currentDay} | Total Runs: {_meta.totalRunsAttempted}";
            statsText.font = Resources.Load<Font>("Arial");
            statsText.fontSize = 22;
            statsText.alignment = TextAnchor.MiddleCenter;
            statsText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            var statsLayout = statsGo.AddComponent<LayoutElement>();
            statsLayout.preferredHeight = 50;

            // Continue button.
            var buttonGo = new GameObject("ContinueButton");
            buttonGo.transform.SetParent(contentGo.transform, false);
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);
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

            Debug.Log("[VictoryRunUI] Victory screen displayed.");
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
