using UnityEngine;
using UnityEngine.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Title/main menu state. Displays "New Run" and "Continue" buttons.
    /// On New Run → transitions to RunSetup.
    /// On Continue → loads the last save (hooked up in Step 6 when meta-progression is full).
    /// </summary>
    public class MainMenuState : BaseGameState
    {
        public override string StateId => "MainMenu";
        public override GameState StateEnum => GameState.MainMenu;

        private MainMenuUI _ui;

        protected override void HandleEnter()
        {
            Debug.Log("[MainMenuState] Entered — showing title screen.");
            _ui = new MainMenuUI();
            _ui.OnNewRunPressed += OnNewRunPressed;
            _ui.OnContinuePressed += OnContinuePressed;
            _ui.OnQuitPressed += OnQuitPressed;
            _ui.Show();
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.OnNewRunPressed -= OnNewRunPressed;
                _ui.OnContinuePressed -= OnContinuePressed;
                _ui.OnQuitPressed -= OnQuitPressed;
                _ui.Hide();
                _ui = null;
            }

            Debug.Log("[MainMenuState] Exited.");
        }

        private void OnNewRunPressed()
        {
            Debug.Log("[MainMenuState] New Run selected.");
            RequestTransition(GameState.RunSetup);
        }

        private void OnContinuePressed()
        {
            Debug.Log("[MainMenuState] Continue selected (meta-progression load hooked in Step 6).");
            // TODO Step 6: load meta-progression and jump to HomeBunker (not yet implemented).
            // For now, treat as "New Run".
            RequestTransition(GameState.RunSetup);
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

    /// <summary>
    /// Self-building main menu UI. Displays title + New Run / Continue / Quit buttons.
    /// </summary>
    internal class MainMenuUI
    {
        private Canvas _canvas;
        private GameObject _root;

        public event System.Action OnNewRunPressed;
        public event System.Action OnContinuePressed;
        public event System.Action OnQuitPressed;

        public void Show()
        {
            _root = new GameObject("MainMenuUI");
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 50;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Background.
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_root.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.08f, 1f); // Deep dark.
            var bgLayout = bgGo.AddComponent<LayoutElement>();
            bgLayout.preferredWidth = 1920;
            bgLayout.preferredHeight = 1080;

            // Title panel (top).
            var titlePanelGo = new GameObject("TitlePanel");
            titlePanelGo.transform.SetParent(_root.transform, false);
            var titlePanelLayout = titlePanelGo.AddComponent<LayoutElement>();
            titlePanelLayout.preferredWidth = 1920;
            titlePanelLayout.preferredHeight = 400;

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(titlePanelGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "OBLAST ZERO";
            titleText.font = Resources.Load<Font>("Arial");
            titleText.fontSize = 80;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.7f, 0.7f, 0.7f, 1f);

            var subtitleGo = new GameObject("Subtitle");
            subtitleGo.transform.SetParent(titlePanelGo.transform, false);
            var subtitleText = subtitleGo.AddComponent<Text>();
            subtitleText.text = "An Expedition into Bureaucratic Dread";
            subtitleText.font = Resources.Load<Font>("Arial");
            subtitleText.fontSize = 32;
            subtitleText.alignment = TextAnchor.MiddleCenter;
            subtitleText.color = new Color(0.55f, 0.55f, 0.55f, 1f);

            // Button panel (center).
            var buttonPanelGo = new GameObject("ButtonPanel");
            buttonPanelGo.transform.SetParent(_root.transform, false);
            var buttonPanel = buttonPanelGo.AddComponent<VerticalLayoutGroup>();
            buttonPanel.spacing = 20;
            buttonPanel.childControlWidth = true;
            buttonPanel.childControlHeight = false;
            buttonPanel.childForceExpandHeight = false;
            buttonPanel.padding = new RectOffset(0, 0, 0, 0);

            var buttonPanelLayout = buttonPanelGo.AddComponent<LayoutElement>();
            buttonPanelLayout.preferredWidth = 400;
            buttonPanelLayout.preferredHeight = 300;

            // New Run button.
            CreateMenuButton(buttonPanelGo, "New Run", 100, () => OnNewRunPressed?.Invoke());

            // Continue button.
            CreateMenuButton(buttonPanelGo, "Continue", 100, () => OnContinuePressed?.Invoke());

            // Quit button.
            CreateMenuButton(buttonPanelGo, "Quit", 100, () => OnQuitPressed?.Invoke());

            Debug.Log("[MainMenuUI] Menu displayed.");
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

        private void CreateMenuButton(GameObject parent, string label, float height, System.Action onClick)
        {
            var buttonGo = new GameObject(label + "Button");
            buttonGo.transform.SetParent(parent.transform, false);

            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var button = buttonGo.AddComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());

            var buttonLayout = buttonGo.AddComponent<LayoutElement>();
            buttonLayout.preferredHeight = height;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(buttonGo.transform, false);
            var textComp = textGo.AddComponent<Text>();
            textComp.text = label;
            textComp.font = Resources.Load<Font>("Arial");
            textComp.fontSize = 32;
            textComp.fontStyle = FontStyle.Bold;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.color = Color.white;
        }
    }
}
