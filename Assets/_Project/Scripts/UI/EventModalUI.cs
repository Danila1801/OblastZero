// Assets/_Project/Scripts/UI/EventModalUI.cs
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
    /// The outcome can be acknowledged by the Continue button, by clicking anywhere on the dimmer, or with
    /// Enter/Space/Escape, so the day never stalls on hitting one specific rectangle.
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

        /// <summary>
        /// Passed to <see cref="AddFlexibleHeight"/> for a min or preferred that should NOT be forced.
        /// Unity's <c>LayoutUtility</c> skips negative values, so the owning LayoutGroup's own measurement
        /// is used instead. Writing a real number here on a self-sizing container is what collapsed this
        /// modal: a LayoutElement outranks a LayoutGroup and silently wins.
        /// </summary>
        private const float UNSET = -1f;

        /// <summary>Fixed height of the Continue button, used as both its min and its preferred.</summary>
        private const float CONTINUE_HEIGHT = 72f;

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

        /// <summary>
        /// Dismisses the modal only once an outcome is on screen. While the choices are still up the same
        /// click must do nothing: the dimmer is a deliberate blocker there, and letting it close the modal
        /// would silently skip the player's decision.
        /// </summary>
        private void DismissOutcomeIfShowing()
        {
            if (_overlay == null || !_overlay.activeSelf) return;
            if (_outcomePanel == null || !_outcomePanel.activeSelf) return;
            OblastUIAudio.PlayClick();
            Hide();
        }

        /// <summary>
        /// Enter, Space and Escape also acknowledge an outcome. Polls the Input System device directly
        /// because <c>activeInputHandler</c> is 1 in ProjectSettings (new Input System only), where the
        /// legacy <c>UnityEngine.Input</c> API throws instead of returning false.
        /// </summary>
        private void Update()
        {
            if (_outcomePanel == null || !_outcomePanel.activeSelf) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame
                || keyboard.numpadEnterKey.wasPressedThisFrame
                || keyboard.spaceKey.wasPressedThisFrame
                || keyboard.escapeKey.wasPressedThisFrame)
            {
                DismissOutcomeIfShowing();
            }
        }

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
                ? $"<color=#{ToHex(successColor)}>{LocalizedStrings.Get(UIStringKeys.EventSuccess)}</color>"
                : $"<color=#{ToHex(failureColor)}>{LocalizedStrings.Get(UIStringKeys.EventFailure)}</color>";
            string follow = string.IsNullOrEmpty(e.FollowUpEventId)
                ? string.Empty
                : $"\n<color=#{ToHex(dimColor)}>{LocalizedStrings.Get(UIStringKeys.EventFollowUp)}</color>";
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
            if (enabled) OblastUIAudio.AttachHover(go);

            var text = CreateText("Label", go.transform, 24f, FontStyles.Normal, TextAlignmentOptions.Left,
                                  enabled ? textColor : dimColor);
            StretchFill(text.rectTransform, 20f, 6f);
            text.text = enabled
                ? label
                : $"{label}  <color=#{ToHex(dimColor)}>" +
                  $"{LocalizedStrings.Get(UIStringKeys.EventChoiceUnavailable)}</color>";

            if (enabled)
            {
                int captured = index;
                btn.onClick.AddListener(OblastUIAudio.PlayClick);
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

            // Clicking the dimmer dismisses the outcome, so acknowledging a result never depends on hitting
            // one specific rectangle. Transition is None because the target graphic here is the full-screen
            // dimmer, and a colour tint on it would flash the whole screen on hover.
            var overlayBtn = overlayGO.AddComponent<Button>();
            overlayBtn.targetGraphic = overlayImg;
            overlayBtn.transition = Selectable.Transition.None;
            overlayBtn.onClick.AddListener(DismissOutcomeIfShowing);

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
            // Narrative is the ONLY flexible row, so every spare pixel in the fixed-height card lands here
            // and the rows below always get their natural size. Preferred is left unset (-1) so TMP's own
            // measured text height is used; the 120 floor keeps a short line from collapsing the block.
            AddFlexibleHeight(_narrative.gameObject, 120f, UNSET, flexible: 1f);

            // Choices container (vertical list).
            var choicesGO = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            choicesGO.transform.SetParent(card.transform, false);
            var cvlg = choicesGO.GetComponent<VerticalLayoutGroup>();
            cvlg.spacing = 12f;
            cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            _choices = (RectTransform)choicesGO.transform;
            // Min and preferred stay UNSET so this container reports the size its own VerticalLayoutGroup
            // computes from the choice buttons. A LayoutElement outranks a LayoutGroup, so a hardcoded
            // preferredHeight here would starve the list: it used to say 1px, and the 64px buttons then
            // spilled below the card with a fourth choice rendering off-screen entirely.
            AddFlexibleHeight(choicesGO, UNSET, UNSET, flexible: 0f);

            // Outcome panel (hidden until resolved) — text + Continue.
            var outcomeGO = new GameObject("Outcome", typeof(RectTransform), typeof(VerticalLayoutGroup));
            outcomeGO.transform.SetParent(card.transform, false);
            var ovlg = outcomeGO.GetComponent<VerticalLayoutGroup>();
            ovlg.spacing = 20f;
            ovlg.childControlWidth = true; ovlg.childControlHeight = true;
            ovlg.childForceExpandWidth = true; ovlg.childForceExpandHeight = false;
            AddFlexibleHeight(outcomeGO, UNSET, UNSET, flexible: 0f);

            _outcomeText = CreateText("OutcomeText", outcomeGO.transform, 26f, FontStyles.Normal, TextAlignmentOptions.TopLeft, textColor);
            _outcomeText.enableWordWrapping = true;
            AddFlexibleHeight(_outcomeText.gameObject, 90f, UNSET, flexible: 0f);

            _continueButton = CreateButton("Continue", outcomeGO.transform,
                                           LocalizedStrings.Get(UIStringKeys.EventContinue), buttonColor, out _);
            // minHeight EQUALS preferredHeight so this button can never be lerped down. It used to carry
            // min 0 / preferred 72 inside a container whose preferred was pinned to 1px, which resolved it
            // to Lerp(0, 72, 0.0625) = 4.5px. TMP overflows its rect, so the word CONTINUE still rendered at
            // full size over a 4.5px hitbox: the player saw a normal button, clicked it, and nothing
            // happened until the cursor happened to land in the band. That reads as a long wait, not a
            // missed click, which is exactly how it was reported.
            AddFlexibleHeight(_continueButton.gameObject, CONTINUE_HEIGHT, CONTINUE_HEIGHT);
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
            // This modal builds its own buttons rather than using OblastUI.Button, so the audio that factory
            // wires up has to be attached here too. Without it a near-miss click produced no feedback at all.
            btn.onClick.AddListener(OblastUIAudio.PlayClick);
            OblastUIAudio.AttachHover(go);
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
