// Assets/_Project/Scripts/UI/EventModalUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.UI
{
    /// <summary>
    /// The bunker narrative dialog. Builds its own Canvas in code and is driven entirely by the EventBus:
    ///   • EventPresentedEvent → looks the event up in the GameDatabase, shows its title + narrative, and
    ///     builds one button per choice (trait-gated choices are shown disabled).
    ///   • EventResolvedEvent  → swaps the choice list for a success/failure outcome line + a Continue button.
    /// Each choice button raises <see cref="EventChoiceSelectedEvent"/> — an intent SurvivalPhase2DState turns
    /// into an <see cref="EventEngine"/> resolution. This modal owns no game logic; it renders data and raises
    /// intents. Narrative/label strings resolve through <see cref="LocalizedStrings"/> (key shown until a
    /// language table is loaded). Acting crew is null for now (bunker-wide); a crew-assignment selector plugs
    /// in here later.
    ///
    /// NOTE: text uses TextMeshPro (import essentials if labels render blank).
    /// </summary>
    public class EventModalUI : MonoBehaviour
    {
        [Header("Palette")]
        [SerializeField] private Color textColor = new Color(0.9f, 0.9f, 0.86f);
        [SerializeField] private Color dimColor = new Color(0.9f, 0.9f, 0.86f, 0.5f);
        [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color cardColor = new Color(0.09f, 0.10f, 0.09f, 0.98f);
        [SerializeField] private Color buttonColor = new Color(0.20f, 0.24f, 0.20f, 1f);
        [SerializeField] private Color buttonDisabledColor = new Color(0.14f, 0.15f, 0.14f, 1f);
        [SerializeField] private Color successColor = new Color(0.55f, 0.85f, 0.55f);
        [SerializeField] private Color failureColor = new Color(1f, 0.45f, 0.4f);

        private GameObject _overlay;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _narrative;
        private RectTransform _choices;
        private GameObject _outcomePanel;
        private TextMeshProUGUI _outcomeText;
        private Button _continueButton;
        private Sprite _white;

        private readonly List<GameObject> _choiceButtons = new();

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            EventBus.Subscribe<EventPresentedEvent>(OnEventPresented);
            EventBus.Subscribe<EventResolvedEvent>(OnEventResolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EventPresentedEvent>(OnEventPresented);
            EventBus.Unsubscribe<EventResolvedEvent>(OnEventResolved);
        }

        // ---- Event handlers ----

        private void OnEventPresented(EventPresentedEvent e)
        {
            var gm = GameManager.Instance;
            var evt = gm != null && gm.Database != null ? gm.Database.GetEvent(e.EventId) : null;
            if (evt == null)
            {
                Debug.LogWarning($"[EventModalUI] Presented event '{e.EventId}' not found in database.");
                return;
            }
            Show(evt);
        }

        private void OnEventResolved(EventResolvedEvent e) => ShowOutcome(e);

        private void OnContinueClicked() => Hide();

        // ---- Presentation ----

        private void Show(ExpeditionEventData evt)
        {
            ClearChoiceButtons();
            _outcomePanel.SetActive(false);
            _choices.gameObject.SetActive(true);

            _title.text = LocalizedStrings.Get(evt.titleKey);
            _narrative.text = LocalizedStrings.Get(evt.narrativeTextKey);

            var engine = GameManager.Instance != null ? GameManager.Instance.Events : null;
            List<int> available = engine != null ? engine.AvailableChoiceIndices(evt, null) : null;

            if (evt.choices != null)
            {
                for (int i = 0; i < evt.choices.Count; i++)
                {
                    bool enabled = available == null || available.Contains(i);
                    string label = LocalizedStrings.Get(evt.choices[i].choiceLabelKey);
                    AddChoiceButton(i, label, enabled);
                }
            }

            _overlay.SetActive(true);
        }

        private void ShowOutcome(EventResolvedEvent e)
        {
            if (!_overlay.activeSelf) return; // outcome for an event we never showed

            ClearChoiceButtons();
            _choices.gameObject.SetActive(false);
            _outcomePanel.SetActive(true);

            string verdict = e.Success
                ? $"<color=#{ToHex(successColor)}>The matter resolves in your favour.</color>"
                : $"<color=#{ToHex(failureColor)}>It does not go as intended.</color>";
            string follow = string.IsNullOrEmpty(e.FollowUpEventId)
                ? string.Empty
                : $"\n<color=#{ToHex(dimColor)}>The file remains open. Expect correspondence.</color>";
            _outcomeText.text = verdict + follow;
        }

        private void Hide()
        {
            ClearChoiceButtons();
            _overlay.SetActive(false);
        }

        private void AddChoiceButton(int index, string label, bool enabled)
        {
            var go = new GameObject($"Choice{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_choices, false);

            var img = go.GetComponent<Image>();
            img.sprite = _white;
            img.color = enabled ? buttonColor : buttonDisabledColor;

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 64f;
            le.preferredHeight = 64f;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = enabled;

            var text = CreateText("Label", go.transform, 24f, FontStyles.Normal, TextAlignmentOptions.Left,
                                  enabled ? textColor : dimColor);
            StretchFill(text.rectTransform, 20f, 6f);
            text.text = enabled ? label : $"{label}  <color=#{ToHex(dimColor)}>(unavailable)</color>";

            if (enabled)
            {
                int captured = index;
                btn.onClick.AddListener(() => EventBus.Raise(new EventChoiceSelectedEvent
                {
                    ChoiceIndex = captured,
                    ActingCrewInstanceId = null
                }));
            }

            _choiceButtons.Add(go);
        }

        private void ClearChoiceButtons()
        {
            foreach (var go in _choiceButtons)
            {
                if (go == null) continue;
                if (Application.isPlaying) Destroy(go);
                else DestroyImmediate(go); // edit-mode tooling/tests
            }
            _choiceButtons.Clear();
        }

        private static string ToHex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ---- UI construction ----

        private void BuildUI()
        {
            _white = MakeWhiteSprite();

            var canvasGO = new GameObject("EventModal_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above the bunker HUD
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGO.GetComponent<RectTransform>();

            // Full-screen dimmer that also blocks clicks to the HUD behind it.
            var overlayGO = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
            overlayGO.transform.SetParent(root, false);
            var overlayImg = overlayGO.GetComponent<Image>();
            overlayImg.sprite = _white;
            overlayImg.color = overlayColor;
            overlayImg.raycastTarget = true;
            var ort = (RectTransform)overlayGO.transform;
            ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;
            _overlay = overlayGO;

            // Center card.
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(_overlay.transform, false);
            var cardImg = card.GetComponent<Image>();
            cardImg.sprite = _white; cardImg.color = cardColor; cardImg.raycastTarget = true;
            var crt = (RectTransform)card.transform;
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 0.5f);
            crt.sizeDelta = new Vector2(1040f, 720f);
            var vlg = card.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(48, 48, 44, 44);
            vlg.spacing = 20f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

            _title = CreateText("Title", card.transform, 40f, FontStyles.Bold, TextAlignmentOptions.TopLeft, textColor);
            AddFlexibleHeight(_title.gameObject, 0f, 64f);

            _narrative = CreateText("Narrative", card.transform, 24f, FontStyles.Normal, TextAlignmentOptions.TopLeft, textColor);
            _narrative.enableWordWrapping = true;
            AddFlexibleHeight(_narrative.gameObject, 240f, 1f, flexible: 1f);

            // Choices container (vertical list).
            var choicesGO = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesGO.transform.SetParent(card.transform, false);
            var cvlg = choicesGO.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = 12f;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            _choices = (RectTransform)choicesGO.transform;
            AddFlexibleHeight(choicesGO, 0f, 1f, flexible: 0f);

            // Outcome panel (hidden until resolved) — text + Continue.
            var outcomeGO = new GameObject("Outcome", typeof(RectTransform), typeof(VerticalLayoutGroup));
            outcomeGO.transform.SetParent(card.transform, false);
            var ovlg = outcomeGO.GetComponent<VerticalLayoutGroup>();
            ovlg.spacing = 20f;
            ovlg.childControlWidth = true; ovlg.childControlHeight = true;
            ovlg.childForceExpandWidth = true; ovlg.childForceExpandHeight = false;
            AddFlexibleHeight(outcomeGO, 0f, 1f, flexible: 1f);

            _outcomeText = CreateText("OutcomeText", outcomeGO.transform, 26f, FontStyles.Normal, TextAlignmentOptions.TopLeft, textColor);
            _outcomeText.enableWordWrapping = true;
            AddFlexibleHeight(_outcomeText.gameObject, 120f, 1f, flexible: 1f);

            _continueButton = CreateButton("Continue", outcomeGO.transform, "CONTINUE", buttonColor, out _);
            AddFlexibleHeight(_continueButton.gameObject, 0f, 72f);
            _continueButton.onClick.AddListener(OnContinueClicked);
            _outcomePanel = outcomeGO;

            _overlay.SetActive(false);
        }

        // ---- Construction helpers ----

        private Button CreateButton(string name, Transform parent, string label, Color color, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _white; img.color = color;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            labelText = CreateText("Label", go.transform, 26f, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
            StretchFill(labelText.rectTransform, 0f, 0f);
            labelText.text = label;
            return btn;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style,
                                                  TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size; text.fontStyle = style; text.alignment = align;
            text.richText = true; text.raycastTarget = false; text.color = color;
            return text;
        }

        private static void AddFlexibleHeight(GameObject go, float min, float preferred, float flexible = 0f)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.minHeight = min;
            le.preferredHeight = preferred;
            le.flexibleHeight = flexible;
        }

        private static void StretchFill(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        private static Sprite MakeWhiteSprite()
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
