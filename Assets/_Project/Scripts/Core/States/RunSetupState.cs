using UnityEngine;
using UnityEngine.UI;
using OblastZero.Data;
using System.Collections.Generic;
using System.Linq;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Run setup screen. Player selects:
    /// 1. Starting crew (from available crew archetypes in the database)
    /// 2. Scavenge site (region/location for Phase A)
    /// 3. RNG seed (or random if not specified)
    ///
    /// Then calls GameManager.BeginNewRun() and transitions to ScavengePhase3D.
    /// </summary>
    public class RunSetupState : BaseGameState
    {
        public override string StateId => "RunSetup";
        public override GameState StateEnum => GameState.RunSetup;

        private RunSetupUI _ui;

        protected override void HandleEnter()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null)
            {
                Debug.LogError("[RunSetupState] GameManager or Database unavailable.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            Debug.Log("[RunSetupState] Entered — showing run setup screen.");
            _ui = new RunSetupUI(gm.Database, Context?.MetaProgress);
            _ui.OnRunCommitted += OnRunCommitted;
            _ui.OnCancelPressed += OnCancelPressed;
            _ui.Show();
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.OnRunCommitted -= OnRunCommitted;
                _ui.OnCancelPressed -= OnCancelPressed;
                _ui.Hide();
                _ui = null;
            }

            Debug.Log("[RunSetupState] Exited.");
        }

        private void OnRunCommitted(string crewId, string siteId, int seed)
        {
            Debug.Log($"[RunSetupState] Run committed: crew={crewId}, site={siteId}, seed={seed}");
            
            var gm = GameManager.Instance;
            gm.BeginNewRun(siteId, seed);

            // Transition to Phase A (3D scavenge).
            // NOTE: Phase A is headless (DebugRunLauncher drives it). Real 3D scene comes in Step 3.
            RequestTransition(GameState.ScavengePhase3D);
        }

        private void OnCancelPressed()
        {
            Debug.Log("[RunSetupState] Setup cancelled — returning to MainMenu.");
            RequestTransition(GameState.MainMenu);
        }
    }

    /// <summary>
    /// Self-building run setup UI. Shows crew selector, site selector, seed field.
    /// </summary>
    internal class RunSetupUI
    {
        private GameDatabase _database;
        private MetaProgressData _meta;
        private Canvas _canvas;
        private GameObject _root;

        private CrewMemberData _selectedCrew;
        private string _selectedSiteId = "default_site"; // Placeholder site ID
        private int _selectedSeed;

        public event System.Action<string, string, int> OnRunCommitted;
        public event System.Action OnCancelPressed;

        public RunSetupUI(GameDatabase database, MetaProgressData meta)
        {
            _database = database;
            _meta = meta;
            _selectedSeed = Random.Range(0, int.MaxValue);
        }

        public void Show()
        {
            _root = new GameObject("RunSetupUI");
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Dark background.
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(_root.transform, false);
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            var bgLayout = bgGo.AddComponent<LayoutElement>();
            bgLayout.preferredWidth = 1920;
            bgLayout.preferredHeight = 1080;

            // Main container.
            var containerGo = new GameObject("Container");
            containerGo.transform.SetParent(_root.transform, false);
            var containerLayout = containerGo.AddComponent<LayoutElement>();
            containerLayout.preferredWidth = 1000;
            containerLayout.preferredHeight = 700;
            var containerVLayout = containerGo.AddComponent<VerticalLayoutGroup>();
            containerVLayout.spacing = 30;
            containerVLayout.padding = new RectOffset(40, 40, 40, 40);
            containerVLayout.childControlSize = new Vector2(1, 0);
            containerVLayout.childForceExpandHeight = false;

            // Title.
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(containerGo.transform, false);
            var titleText = titleGo.AddComponent<Text>();
            titleText.text = "— EXPEDITION SETUP —";
            titleText.font = Resources.Load<Font>("Arial");
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
            var titleLayout = titleGo.AddComponent<LayoutElement>();
            titleLayout.preferredHeight = 80;

            // Crew selection section.
            CreateSectionLabel(containerGo, "Select Crew");

            var crewButtonsGo = new GameObject("CrewButtons");
            crewButtonsGo.transform.SetParent(containerGo.transform, false);
            var crewHLayout = crewButtonsGo.AddComponent<HorizontalLayoutGroup>();
            crewHLayout.spacing = 15;
            crewHLayout.childControlSize = new Vector2(0, 1);
            crewHLayout.childForceExpandWidth = false;

            // Populate crew from database.
            var allCrew = _database.allCrewMembers;
            foreach (var crewData in allCrew.Take(3)) // Show first 3 crew for now.
            {
                var crewBtn = new GameObject(crewData.displayName + "Btn");
                crewBtn.transform.SetParent(crewButtonsGo.transform, false);

                var btnImage = crewBtn.AddComponent<Image>();
                btnImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

                var btn = crewBtn.AddComponent<Button>();
                btn.onClick.AddListener(() =>
                {
                    _selectedCrew = crewData;
                    Debug.Log($"[RunSetupUI] Selected crew: {crewData.displayName}");
                });

                var btnText = new GameObject("Text");
                btnText.transform.SetParent(crewBtn.transform, false);
                var textComp = btnText.AddComponent<Text>();
                textComp.text = crewData.displayName;
                textComp.font = Resources.Load<Font>("Arial");
                textComp.fontSize = 20;
                textComp.alignment = TextAnchor.MiddleCenter;
                textComp.color = Color.white;

                var btnLayout = crewBtn.AddComponent<LayoutElement>();
                btnLayout.preferredWidth = 250;
                btnLayout.preferredHeight = 80;
            }

            // Site selection (stub).
            CreateSectionLabel(containerGo, "Scavenge Site");

            var siteButtonGo = new GameObject("SiteButton");
            siteButtonGo.transform.SetParent(containerGo.transform, false);
            var siteImage = siteButtonGo.AddComponent<Image>();
            siteImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var siteBtn = siteButtonGo.AddComponent<Button>();
            siteBtn.onClick.AddListener(() =>
            {
                _selectedSiteId = "default_site";
                Debug.Log("[RunSetupUI] Default site selected.");
            });

            var siteText = new GameObject("Text");
            siteText.transform.SetParent(siteButtonGo.transform, false);
            var siteTxtComp = siteText.AddComponent<Text>();
            siteTxtComp.text = "Thermal Plant [Contaminated]";
            siteTxtComp.font = Resources.Load<Font>("Arial");
            siteTxtComp.fontSize = 24;
            siteTxtComp.alignment = TextAnchor.MiddleCenter;
            siteTxtComp.color = Color.white;

            var siteLayout = siteButtonGo.AddComponent<LayoutElement>();
            siteLayout.preferredHeight = 80;

            // Bottom buttons (Commit / Cancel).
            var bottomButtonsGo = new GameObject("BottomButtons");
            bottomButtonsGo.transform.SetParent(containerGo.transform, false);
            var bottomHLayout = bottomButtonsGo.AddComponent<HorizontalLayoutGroup>();
            bottomHLayout.spacing = 20;
            bottomHLayout.childControlSize = new Vector2(0, 1);
            bottomHLayout.childForceExpandWidth = false;

            CreateSetupButton(bottomButtonsGo, "Begin Expedition", 200, 80, () =>
            {
                if (_selectedCrew == null)
                {
                    Debug.LogWarning("[RunSetupUI] No crew selected. Choose a crew member.");
                    return;
                }

                OnRunCommitted?.Invoke(_selectedCrew.id, _selectedSiteId, _selectedSeed);
            });

            CreateSetupButton(bottomButtonsGo, "Cancel", 200, 80, () => OnCancelPressed?.Invoke());

            Debug.Log("[RunSetupUI] Setup screen displayed.");
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

        private void CreateSectionLabel(GameObject parent, string label)
        {
            var labelGo = new GameObject(label + "Label");
            labelGo.transform.SetParent(parent.transform, false);
            var labelText = labelGo.AddComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.Load<Font>("Arial");
            labelText.fontSize = 28;
            labelText.fontStyle = FontStyle.Bold;
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            var labelLayout = labelGo.AddComponent<LayoutElement>();
            labelLayout.preferredHeight = 40;
        }

        private void CreateSetupButton(GameObject parent, string label, float width, float height, System.Action onClick)
        {
            var btnGo = new GameObject(label + "Btn");
            btnGo.transform.SetParent(parent.transform, false);

            var btnImage = btnGo.AddComponent<Image>();
            btnImage.color = new Color(0.25f, 0.25f, 0.25f, 1f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtComp = txtGo.AddComponent<Text>();
            txtComp.text = label;
            txtComp.font = Resources.Load<Font>("Arial");
            txtComp.fontSize = 24;
            txtComp.fontStyle = FontStyle.Bold;
            txtComp.alignment = TextAnchor.MiddleCenter;
            txtComp.color = Color.white;

            var btnLayout = btnGo.AddComponent<LayoutElement>();
            btnLayout.preferredWidth = width;
            btnLayout.preferredHeight = height;
        }
    }
}
