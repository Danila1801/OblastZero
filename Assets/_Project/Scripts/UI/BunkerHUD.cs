// Assets/_Project/Scripts/UI/BunkerHUD.cs
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.UI
{
    /// <summary>
    /// The 2D bunker HUD. Builds its whole Canvas in code (drop the component into the bunker scene and it
    /// appears — no manual wiring) and drives itself entirely from the EventBus:
    ///   • DayAdvancedEvent            → the day counter + full refresh
    ///   • CrewStatChangedEvent / CrewDiedEvent → the crew roster (health / sanity / fatigue / radiation)
    ///   • BunkerInventoryChangedEvent → the ration count
    ///   • FactionReputationChangedEvent → the three faction standings
    ///   • EventPresentedEvent / EventResolvedEvent → disables "End Day" while an event awaits a choice
    /// The "End Day" button raises <see cref="EndDayRequestedEvent"/> — an intent. This HUD is presentation
    /// only: it reads run state + the GameDatabase and raises intents, and never mutates anything.
    ///
    /// NOTE: text uses TextMeshPro. If labels render blank, import via Window → TextMeshPro → Import TMP
    /// Essential Resources.
    /// </summary>
    public class BunkerHUD : MonoBehaviour
    {
        [Header("Palette")]
        [SerializeField] private Color textColor = new Color(0.86f, 0.86f, 0.82f);
        [SerializeField] private Color dimColor = new Color(0.86f, 0.86f, 0.82f, 0.55f);
        [SerializeField] private Color warnColor = new Color(1f, 0.7f, 0.2f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.3f, 0.25f);
        [SerializeField] private Color panelColor = new Color(0.06f, 0.07f, 0.06f, 0.82f);
        [SerializeField] private Color buttonColor = new Color(0.18f, 0.22f, 0.18f, 0.95f);

        private TextMeshProUGUI _dayLabel;
        private TextMeshProUGUI _crewBody;
        private TextMeshProUGUI _suppliesBody;
        private TextMeshProUGUI _repBody;
        private Button _endDayButton;
        private TextMeshProUGUI _endDayLabel;
        private Button _artifactsButton;
        private TextMeshProUGUI _artifactsLabel;
        private Button _dispatchButton;
        private TextMeshProUGUI _dispatchLabel;
        private TextMeshProUGUI _crewHeader;
        private Sprite _white;

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<CrewStatChangedEvent>(OnCrewStatChanged);
            EventBus.Subscribe<CrewDiedEvent>(OnCrewDied);
            EventBus.Subscribe<BunkerInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Subscribe<FactionReputationChangedEvent>(OnRepChanged);
            EventBus.Subscribe<EventPresentedEvent>(OnEventPresented);
            EventBus.Subscribe<EventResolvedEvent>(OnEventResolved);
            LocalizedStrings.LanguageChanged += OnLanguageChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<CrewStatChangedEvent>(OnCrewStatChanged);
            EventBus.Unsubscribe<CrewDiedEvent>(OnCrewDied);
            EventBus.Unsubscribe<BunkerInventoryChangedEvent>(OnInventoryChanged);
            EventBus.Unsubscribe<FactionReputationChangedEvent>(OnRepChanged);
            EventBus.Unsubscribe<EventPresentedEvent>(OnEventPresented);
            EventBus.Unsubscribe<EventResolvedEvent>(OnEventResolved);
            LocalizedStrings.LanguageChanged -= OnLanguageChanged;
        }

        // ---- Event handlers ----

        private void OnDayAdvanced(DayAdvancedEvent e) => RefreshAll();
        private void OnCrewStatChanged(CrewStatChangedEvent e) => RefreshCrew();
        private void OnCrewDied(CrewDiedEvent e) => RefreshCrew();
        private void OnInventoryChanged(BunkerInventoryChangedEvent e) => RefreshSupplies();
        private void OnRepChanged(FactionReputationChangedEvent e) => RefreshReputation();
        private void OnEventPresented(EventPresentedEvent e) => SetEndDayInteractable(false);
        private void OnEventResolved(EventResolvedEvent e) => SetEndDayInteractable(true);

        private void OnEndDayClicked() => EventBus.Raise(new EndDayRequestedEvent());

        /// <summary>
        /// Re-renders in the new language. The dynamic panels come back through RefreshAll; the two
        /// fixed labels are re-read here, because nothing else ever writes them after construction and
        /// they would otherwise sit in the previous language for the rest of the run.
        /// </summary>
        private void OnLanguageChanged()
        {
            if (_crewHeader != null) _crewHeader.text = LocalizedStrings.Get(UIStringKeys.BunkerCrewHeader);
            if (_endDayLabel != null) _endDayLabel.text = LocalizedStrings.Get(UIStringKeys.BunkerEndDay);
            RefreshAll();
        }

        // ---- View updates ----

        private void RefreshAll()
        {
            RefreshDay();
            RefreshCrew();
            RefreshSupplies();
            RefreshReputation();
        }

        private void RefreshDay()
        {
            if (_dayLabel == null) return;
            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            _dayLabel.text = run != null
                ? LocalizedStrings.Get(UIStringKeys.BunkerDay, run.currentDay)
                : LocalizedStrings.Get(UIStringKeys.BunkerDayUnknown);
        }

        private void RefreshCrew()
        {
            if (_crewBody == null) return;
            var gm = GameManager.Instance;
            var run = gm != null ? gm.CurrentRun : null;
            var db = gm != null ? gm.Database : null;

            if (run == null || run.ActiveCrew.Count == 0)
            {
                _crewBody.text = $"<i>{LocalizedStrings.Get(UIStringKeys.BunkerCrewNone)}</i>";
                return;
            }

            var sb = new StringBuilder();
            foreach (var c in run.ActiveCrew)
            {
                string name = ResolveCrewName(db, c.crewDataId);
                if (!c.isAlive)
                {
                    sb.AppendLine($"<color=#{ToHex(dangerColor)}>{LocalizedStrings.Get(UIStringKeys.BunkerCrewDeceased, name)}</color>");
                    continue;
                }
                sb.AppendLine(name);
                sb.AppendLine(
                    $"  <color=#{ToHex(StatColor(c.currentHealth, 30))}>{LocalizedStrings.Get(UIStringKeys.BunkerStatHealth)} {c.currentHealth}</color>   " +
                    $"<color=#{ToHex(StatColor(c.currentSanity, 25))}>{LocalizedStrings.Get(UIStringKeys.BunkerStatSanity)} {c.currentSanity}</color>   " +
                    $"<color=#{ToHex(StatColorInverted(c.currentFatigue, 70))}>{LocalizedStrings.Get(UIStringKeys.BunkerStatFatigue)} {c.currentFatigue}</color>   " +
                    $"<color=#{ToHex(StatColorInverted(c.currentRadiation, 60))}>{LocalizedStrings.Get(UIStringKeys.BunkerStatRadiation)} {c.currentRadiation}</color>");
            }
            _crewBody.text = sb.ToString().TrimEnd();
        }

        private void RefreshSupplies()
        {
            if (_suppliesBody == null) return;
            var gm = GameManager.Instance;
            var run = gm != null ? gm.CurrentRun : null;
            var db = gm != null ? gm.Database : null;

            if (run == null || db == null)
            {
                _suppliesBody.text = $"<i>{LocalizedStrings.Get(UIStringKeys.BunkerPlaceholder)}</i>";
                return;
            }

            int food = 0;
            int totalStacks = 0;
            foreach (var inst in run.BunkerInventory)
            {
                totalStacks++;
                var data = db.GetItem(inst.itemDataId);
                if (data != null && data.category == ItemCategory.Food) food += inst.quantity;
            }

            int alive = 0;
            foreach (var c in run.ActiveCrew) if (c.isAlive) alive++;
            int daysOfFood = alive > 0 ? food / alive : food;

            var foodColor = food <= alive ? dangerColor : (food <= alive * 3 ? warnColor : textColor);
            _suppliesBody.text =
                $"<color=#{ToHex(foodColor)}>{LocalizedStrings.Get(UIStringKeys.BunkerRations, food)}</color>  <color=#{ToHex(dimColor)}>{LocalizedStrings.Get(UIStringKeys.BunkerRationsDays, daysOfFood)}</color>\n" +
                $"<color=#{ToHex(dimColor)}>{LocalizedStrings.Get(UIStringKeys.BunkerStores, totalStacks, run.bunkerRadiationPool)}</color>";
        }

        private void RefreshReputation()
        {
            if (_repBody == null) return;
            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            if (run == null) { _repBody.text = $"<i>{LocalizedStrings.Get(UIStringKeys.BunkerPlaceholder)}</i>"; return; }

            _repBody.text =
                $"{LocalizedStrings.Get(UIStringKeys.FactionShortScaleSociety)}  <color=#{ToHex(RepColor(run.repScaleSociety))}>{Signed(run.repScaleSociety)}</color>\n" +
                $"{LocalizedStrings.Get(UIStringKeys.FactionShortCordon)}  <color=#{ToHex(RepColor(run.repCordon))}>{Signed(run.repCordon)}</color>\n" +
                $"{LocalizedStrings.Get(UIStringKeys.FactionShortKafedra)}  <color=#{ToHex(RepColor(run.repKafedra))}>{Signed(run.repKafedra)}</color>";
        }

        private void SetEndDayInteractable(bool interactable)
        {
            if (_endDayButton == null) return;
            _endDayButton.interactable = interactable;
            if (_endDayLabel != null) _endDayLabel.color = interactable ? textColor : dimColor;
        }

        // ---- Colour helpers ----

        private Color StatColor(int value, int dangerBelow)
            => value <= dangerBelow ? dangerColor : (value <= dangerBelow * 2 ? warnColor : textColor);

        private Color StatColorInverted(int value, int dangerAbove)
            => value >= dangerAbove ? dangerColor : (value >= dangerAbove * 0.6f ? warnColor : textColor);

        private Color RepColor(int rep)
            => rep <= -25 ? dangerColor : (rep >= 25 ? new Color(0.55f, 0.85f, 0.55f) : textColor);

        private static string ResolveCrewName(GameDatabase db, string id)
        {
            if (db == null) return id;
            var data = db.GetCrew(id);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : id;
        }

        private static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
        private static string ToHex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        // ---- UI construction ----

        private void BuildUI()
        {
            _white = MakeWhiteSprite();

            var canvasGO = new GameObject("BunkerHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGO.GetComponent<RectTransform>();

            // Day counter — top center.
            _dayLabel = CreateText("Day", root, 56f, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
            SetTop(_dayLabel.rectTransform, new Vector2(0f, -36f), new Vector2(500f, 80f));

            // Crew roster — left panel.
            var crewPanel = CreatePanel("CrewPanel", root, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                        new Vector2(40f, -120f), new Vector2(520f, 520f));
            _crewHeader = CreateText("CrewHeader", crewPanel, 24f, FontStyles.Bold, TextAlignmentOptions.TopLeft, dimColor);
            StretchTop(_crewHeader.rectTransform, 18f, 20f, 34f);
            _crewHeader.text = LocalizedStrings.Get(UIStringKeys.BunkerCrewHeader);
            _crewBody = CreateText("CrewBody", crewPanel, 22f, FontStyles.Normal, TextAlignmentOptions.TopLeft, textColor);
            StretchBelow(_crewBody.rectTransform, 18f, 62f, 18f);
            _crewBody.text = $"<i>{LocalizedStrings.Get(UIStringKeys.BunkerCrewNone)}</i>";

            // Supplies — top-left small panel.
            var supPanel = CreatePanel("SuppliesPanel", root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                                       new Vector2(40f, -40f), new Vector2(360f, 92f));
            _suppliesBody = CreateText("SuppliesBody", supPanel, 22f, FontStyles.Normal, TextAlignmentOptions.Left, textColor);
            StretchFill(_suppliesBody.rectTransform, 16f);
            _suppliesBody.text = $"<i>{LocalizedStrings.Get(UIStringKeys.BunkerPlaceholder)}</i>";

            // Reputation — top-right panel.
            var repPanel = CreatePanel("RepPanel", root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                                       new Vector2(-40f, -40f), new Vector2(360f, 120f));
            _repBody = CreateText("RepBody", repPanel, 22f, FontStyles.Normal, TextAlignmentOptions.Left, textColor);
            StretchFill(_repBody.rectTransform, 16f);
            _repBody.text = $"<i>{LocalizedStrings.Get(UIStringKeys.BunkerPlaceholder)}</i>";

            // End Day — bottom right button.
            // "»" not "▸": the shipped LiberationSans SDF atlas is 250 glyphs and carries no Geometric Shapes
            // block, so U+25B8 fell through to the tofu box on every frame of every run. Latin-1 punctuation
            // is present, so anything from that range renders. Verify new glyphs against the font atlas before
            // using them — TMP only warns once per text object and the box reads as a font bug, not a typo.
            _endDayButton = CreateButton("EndDayButton", root, LocalizedStrings.Get(UIStringKeys.BunkerEndDay), out _endDayLabel);
            var brt = (RectTransform)_endDayButton.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-40f, 40f);
            brt.sizeDelta = new Vector2(280f, 76f);
            _endDayButton.onClick.AddListener(OnEndDayClicked);

            // Artifacts and Dispatch — the two bunker screens that are not the day loop. They sit left
            // of End Day and are deliberately smaller: the day tick is the primary verb, and these are
            // things the player does between days rather than instead of them.
            //
            // Both raise an intent and nothing more. The HUD does not own these screens and does not
            // know what they do; SurvivalPhase2DState opens them, exactly as it already does for the
            // event modal, so the UI-raises-intents rule holds for the new screens too.
            _artifactsButton = CreateButton("ArtifactsButton", root, "ARTIFACTS", out _artifactsLabel);
            var art = (RectTransform)_artifactsButton.transform;
            art.anchorMin = art.anchorMax = new Vector2(1f, 0f);
            art.pivot = new Vector2(1f, 0f);
            art.anchoredPosition = new Vector2(-336f, 40f);
            art.sizeDelta = new Vector2(200f, 76f);
            _artifactsButton.onClick.AddListener(OnArtifactsClicked);

            _dispatchButton = CreateButton("DispatchButton", root, "DISPATCH", out _dispatchLabel);
            var dis = (RectTransform)_dispatchButton.transform;
            dis.anchorMin = dis.anchorMax = new Vector2(1f, 0f);
            dis.pivot = new Vector2(1f, 0f);
            dis.anchoredPosition = new Vector2(-548f, 40f);
            dis.sizeDelta = new Vector2(200f, 76f);
            _dispatchButton.onClick.AddListener(OnDispatchClicked);
        }

        private void OnArtifactsClicked() => EventBus.Raise(new ArtifactScreenRequestedEvent());
        private void OnDispatchClicked() => EventBus.Raise(new ExpeditionScreenRequestedEvent());

        // ---- Construction helpers ----

        private RectTransform CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                          Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _white;
            img.color = panelColor;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        private Button CreateButton(string name, Transform parent, string label, out TextMeshProUGUI labelText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _white;
            img.color = buttonColor;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            labelText = CreateText("Label", go.transform, 26f, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
            StretchFill(labelText.rectTransform, 0f);
            labelText.text = label;
            return btn;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style,
                                                  TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.richText = true;
            text.raycastTarget = false;
            text.color = color;
            return text;
        }

        private static void SetTop(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
        }

        private static void StretchFill(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }

        private static void StretchTop(RectTransform rt, float sidePad, float topPad, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sidePad, -(topPad + height));
            rt.offsetMax = new Vector2(-sidePad, -topPad);
        }

        private static void StretchBelow(RectTransform rt, float sidePad, float topPad, float bottomPad)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(sidePad, bottomPad);
            rt.offsetMax = new Vector2(-sidePad, -topPad);
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
