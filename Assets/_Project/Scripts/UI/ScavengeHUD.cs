// Assets/_Project/Scripts/UI/ScavengeHUD.cs
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.UI
{
    /// <summary>
    /// The 3D Blowout HUD. Builds its entire Canvas in code (drop the component into the scavenge scene and
    /// it appears — no manual UI wiring) and drives itself entirely from the EventBus:
    ///   • ScavengeTimerTickEvent  → the big emission countdown (colours + pulses as it runs low)
    ///   • ScavengeTargetChangedEvent → the contextual "[E] Take/Rescue" prompt
    ///   • ItemPickedUpEvent / CrewRescuedEvent → the live GRABBED list (rebuilt from RunData truth)
    ///   • ScavengeTimerExpiredEvent → the "THE EMISSION HITS" flash
    /// Presentation only — it reads run state and the GameDatabase, never mutates anything.
    ///
    /// NOTE: text uses TextMeshPro. If labels render as blank/boxes, import the one-time TMP resources via
    /// Window → TextMeshPro → Import TMP Essential Resources.
    /// </summary>
    public class ScavengeHUD : MonoBehaviour
    {
        [Header("Countdown Colours")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warnColor = new Color(1f, 0.7f, 0.2f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.25f, 0.2f);

        [Header("Countdown Thresholds (seconds)")]
        [SerializeField] private float warnThreshold = 15f;
        [SerializeField] private float dangerThreshold = 5f;

        [Header("Display")]
        [Tooltip("Cosmetic only — what the countdown shows for the first second before the timer's first tick.")]
        [SerializeField] private float initialSecondsDisplay = 60f;

        [Header("Carry Load")]
        [Tooltip("Fraction of capacity at which the load readout turns amber.")]
        [SerializeField, Range(0f, 1f)] private float loadWarnFraction = 0.75f;

        [Tooltip("Seconds the 'TOO HEAVY' refusal notice stays on screen.")]
        [SerializeField] private float refusalNoticeSeconds = 1.6f;

        private TextMeshProUGUI _countdown;
        private TextMeshProUGUI _prompt;
        private TextMeshProUGUI _grabbedBody;
        private TextMeshProUGUI _loadLabel;
        private Image _loadBarFill;
        private RectTransform _loadBarTrack;
        private TextMeshProUGUI _refusal;
        private GameObject _emissionLabel;
        private Sprite _whiteSprite;

        private float _cachedRemaining = Mathf.Infinity;
        private bool _expired;
        private float _refusalHideAt = -1f;
        private float _loadKg;
        private float _capacityKg = BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG;

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            EventBus.Subscribe<ScavengeTimerTickEvent>(OnTimerTick);
            EventBus.Subscribe<ScavengeTimerExpiredEvent>(OnTimerExpired);
            EventBus.Subscribe<ItemPickedUpEvent>(OnPickup);
            EventBus.Subscribe<CrewRescuedEvent>(OnRescue);
            EventBus.Subscribe<ScavengeTargetChangedEvent>(OnTargetChanged);
            EventBus.Subscribe<ScavengeLoadChangedEvent>(OnLoadChanged);
            EventBus.Subscribe<ScavengePickupRejectedEvent>(OnPickupRejected);

            RefreshGrabbedList();
            SetPrompt(false, string.Empty);
            PullLiveLoad();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScavengeTimerTickEvent>(OnTimerTick);
            EventBus.Unsubscribe<ScavengeTimerExpiredEvent>(OnTimerExpired);
            EventBus.Unsubscribe<ItemPickedUpEvent>(OnPickup);
            EventBus.Unsubscribe<CrewRescuedEvent>(OnRescue);
            EventBus.Unsubscribe<ScavengeTargetChangedEvent>(OnTargetChanged);
            EventBus.Unsubscribe<ScavengeLoadChangedEvent>(OnLoadChanged);
            EventBus.Unsubscribe<ScavengePickupRejectedEvent>(OnPickupRejected);
        }

        private void Update()
        {
            // Smooth pulse on the countdown while in the danger window.
            if (_countdown == null) return;
            bool danger = !_expired && _cachedRemaining <= dangerThreshold;
            float scale = danger ? 1f + 0.10f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f)) : 1f;
            _countdown.rectTransform.localScale = Vector3.one * scale;

            // Retire the refusal notice on its own clock — unscaled, so a pause never strands it.
            if (_refusalHideAt > 0f && Time.unscaledTime >= _refusalHideAt)
            {
                _refusalHideAt = -1f;
                if (_refusal != null) _refusal.gameObject.SetActive(false);
            }
        }

        // ---- Event handlers ----

        private void OnTimerTick(ScavengeTimerTickEvent e) => SetCountdown(e.SecondsRemaining);

        private void OnTimerExpired(ScavengeTimerExpiredEvent e)
        {
            _expired = true;
            SetCountdown(0f);
            if (_emissionLabel != null) _emissionLabel.SetActive(true);
        }

        private void OnPickup(ItemPickedUpEvent e) => RefreshGrabbedList();
        private void OnRescue(CrewRescuedEvent e) => RefreshGrabbedList();
        private void OnTargetChanged(ScavengeTargetChangedEvent e) => SetPrompt(e.HasTarget, e.Verb);

        private void OnLoadChanged(ScavengeLoadChangedEvent e) => SetLoad(e.CurrentKg, e.CapacityKg);

        private void OnPickupRejected(ScavengePickupRejectedEvent e)
        {
            // The pickup stayed on the floor. Say why in one line, in the Oblast's register.
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;
            string name = ResolveItemName(db, e.ItemDataId);
            ShowRefusal($"OVER CAPACITY — {name} ({e.ItemWeightKg:0.#} kg) NOT LOGGED");
            SetLoad(e.CurrentKg, e.CapacityKg);
        }

        // ---- View updates ----

        private void SetCountdown(float seconds)
        {
            _cachedRemaining = seconds;
            _countdown.text = Mathf.CeilToInt(Mathf.Max(0f, seconds)).ToString();

            if (seconds <= dangerThreshold) _countdown.color = dangerColor;
            else if (seconds <= warnThreshold) _countdown.color = warnColor;
            else _countdown.color = normalColor;
        }

        private void SetPrompt(bool show, string verb)
        {
            if (_prompt == null) return;
            _prompt.gameObject.SetActive(show);
            if (show) _prompt.text = $"[E] {(string.IsNullOrEmpty(verb) ? "Take" : verb)}";
        }

        /// <summary>Reads the live load straight off the manager, so the HUD is correct on first frame.</summary>
        private void PullLiveLoad()
        {
            var gm = GameManager.Instance;
            var inventory = gm != null ? gm.Inventory : null;
            if (inventory == null)
            {
                SetLoad(_loadKg, _capacityKg);
                return;
            }
            SetLoad(inventory.ScavengeLoadKg, inventory.ScavengeCarryCapacityKg);
        }

        private void SetLoad(float currentKg, float capacityKg)
        {
            _loadKg = Mathf.Max(0f, currentKg);
            _capacityKg = Mathf.Max(0.01f, capacityKg);

            float fraction = Mathf.Clamp01(_loadKg / _capacityKg);
            Color tint = fraction >= 1f ? dangerColor
                       : fraction >= loadWarnFraction ? warnColor
                       : normalColor;

            if (_loadLabel != null)
            {
                _loadLabel.text = $"{_loadKg:0.0} / {_capacityKg:0.#} KG";
                _loadLabel.color = tint;
            }

            if (_loadBarFill != null && _loadBarTrack != null)
            {
                var rect = _loadBarFill.rectTransform;
                rect.sizeDelta = new Vector2(_loadBarTrack.sizeDelta.x * fraction, rect.sizeDelta.y);
                _loadBarFill.color = tint;
            }
        }

        private void ShowRefusal(string message)
        {
            if (_refusal == null) return;
            _refusal.text = message;
            _refusal.gameObject.SetActive(true);
            _refusalHideAt = Time.unscaledTime + refusalNoticeSeconds;
        }

        private void RefreshGrabbedList()
        {
            if (_grabbedBody == null) return;

            var gm = GameManager.Instance;
            var run = gm != null ? gm.CurrentRun : null;
            var db = gm != null ? gm.Database : null;

            if (run == null)
            {
                _grabbedBody.text = "<i>nothing yet</i>";
                return;
            }

            var sb = new StringBuilder();

            // Items — aggregate by id, preserving first-seen order.
            var counts = new Dictionary<string, int>();
            var order = new List<string>();
            foreach (var inst in run.ScavengedInventory)
            {
                if (!counts.ContainsKey(inst.itemDataId)) order.Add(inst.itemDataId);
                counts.TryGetValue(inst.itemDataId, out int running);
                counts[inst.itemDataId] = running + inst.quantity;
            }
            foreach (var id in order)
            {
                string name = ResolveItemName(db, id);
                int qty = counts[id];
                sb.AppendLine(qty > 1 ? $"{name}  ×{qty}" : name);
            }

            // Crew.
            foreach (var c in run.RescuedCrew)
            {
                string name = ResolveCrewName(db, c.crewDataId);
                sb.AppendLine($"{name}  <color=#8FD3FF>(crew)</color>");
            }

            string text = sb.ToString().TrimEnd();
            _grabbedBody.text = string.IsNullOrEmpty(text) ? "<i>nothing yet</i>" : text;
        }

        private static string ResolveItemName(GameDatabase db, string id)
        {
            if (db == null) return id;
            var data = db.GetItem(id);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : id;
        }

        private static string ResolveCrewName(GameDatabase db, string id)
        {
            if (db == null) return id;
            var data = db.GetCrew(id);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : id;
        }

        // ---- UI construction ----

        private void BuildUI()
        {
            _whiteSprite = MakeWhiteSprite();

            var canvasGO = new GameObject("ScavengeHUD_Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasGO.GetComponent<RectTransform>();

            // Crosshair — a small white "+" at screen center.
            var cross = NewRect("Crosshair", root);
            SetCenter(cross, Vector2.zero, new Vector2(24f, 24f));
            var hBar = CreateImage(cross, "H", new Color(1f, 1f, 1f, 0.85f));
            SetCenter(hBar.rectTransform, Vector2.zero, new Vector2(16f, 2f));
            var vBar = CreateImage(cross, "V", new Color(1f, 1f, 1f, 0.85f));
            SetCenter(vBar.rectTransform, Vector2.zero, new Vector2(2f, 16f));

            // Countdown — big, top-center.
            _countdown = CreateText("Countdown", root, 92f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetTop(_countdown.rectTransform, new Vector2(0f, -80f), new Vector2(420f, 130f));
            _countdown.color = normalColor;
            _countdown.text = Mathf.CeilToInt(initialSecondsDisplay).ToString();

            var subLabel = CreateText("CountdownLabel", root, 22f, FontStyles.Normal, TextAlignmentOptions.Center);
            SetTop(subLabel.rectTransform, new Vector2(0f, -168f), new Vector2(600f, 30f));
            subLabel.color = new Color(1f, 1f, 1f, 0.55f);
            subLabel.text = "SECONDS TO REACH THE BUNKER";

            // Interaction prompt — just below the crosshair, hidden until a target is in view.
            _prompt = CreateText("Prompt", root, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetCenter(_prompt.rectTransform, new Vector2(0f, -60f), new Vector2(420f, 44f));
            _prompt.color = normalColor;
            _prompt.gameObject.SetActive(false);

            // Grabbed list — bottom-left.
            var header = CreateText("GrabbedHeader", root, 24f, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            SetBottomLeft(header.rectTransform, new Vector2(40f, 312f), new Vector2(440f, 30f));
            header.color = new Color(1f, 1f, 1f, 0.85f);
            header.text = "GRABBED";

            _grabbedBody = CreateText("GrabbedBody", root, 22f, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
            SetBottomLeft(_grabbedBody.rectTransform, new Vector2(40f, 96f), new Vector2(460f, 214f));
            _grabbedBody.color = new Color(1f, 1f, 1f, 0.8f);
            _grabbedBody.text = "<i>nothing yet</i>";

            // Carry load — under the grabbed list. The cap is the whole decision in Phase A, so it gets
            // a bar the player can read at a glance while sprinting, not just a number.
            var loadHeader = CreateText("LoadHeader", root, 20f, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            SetBottomLeft(loadHeader.rectTransform, new Vector2(40f, 64f), new Vector2(200f, 26f));
            loadHeader.color = new Color(1f, 1f, 1f, 0.85f);
            loadHeader.text = "LOAD";

            _loadLabel = CreateText("LoadValue", root, 20f, FontStyles.Bold, TextAlignmentOptions.BottomRight);
            SetBottomLeft(_loadLabel.rectTransform, new Vector2(240f, 64f), new Vector2(260f, 26f));
            _loadLabel.color = normalColor;
            _loadLabel.text = $"0.0 / {(float)BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG:0.#} KG";

            var track = CreateImage(root, "LoadBarTrack", new Color(1f, 1f, 1f, 0.18f));
            SetBottomLeft(track.rectTransform, new Vector2(40f, 44f), new Vector2(460f, 10f));
            _loadBarTrack = track.rectTransform;

            _loadBarFill = CreateImage(track.transform, "LoadBarFill", normalColor);
            var fillRect = _loadBarFill.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0f, 10f);

            // Refusal notice — sits just above the crosshair prompt so it lands in the player's eyeline.
            _refusal = CreateText("Refusal", root, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetCenter(_refusal.rectTransform, new Vector2(0f, -110f), new Vector2(900f, 40f));
            _refusal.color = dangerColor;
            _refusal.text = string.Empty;
            _refusal.gameObject.SetActive(false);

            // Emission flash — center, hidden until the timer expires.
            var emission = CreateText("Emission", root, 64f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetCenter(emission.rectTransform, new Vector2(0f, 130f), new Vector2(1000f, 100f));
            emission.color = dangerColor;
            emission.text = "THE EMISSION HITS";
            emission.gameObject.SetActive(false);
            _emissionLabel = emission.gameObject;
        }

        // ---- Construction helpers ----

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = _whiteSprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.richText = true;
            text.raycastTarget = false;
            return text;
        }

        private static void SetCenter(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void SetTop(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        private static void SetBottomLeft(RectTransform rt, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
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
