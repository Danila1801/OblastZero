// Assets/_Project/Scripts/UI/OblastUI.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OblastZero.UI
{
    /// <summary>
    /// Construction vocabulary shared by every code-built screen (MainMenuUI, RunSetupUI, RunSummaryUI).
    /// Screens build their own Canvas at runtime rather than shipping prefabs, so there is nothing to wire
    /// by hand in a scene and nothing to re-wire when a layout changes — but they should not each reinvent
    /// anchoring, palette and button states, which is what this file exists to prevent.
    ///
    /// Everything here is presentation only. Nothing in OblastZero.UI reads or writes game state; screens
    /// raise intents and their owning state turns those into calls.
    ///
    /// NOTE: text uses TextMeshPro. If labels render blank or as boxes, import the one-time TMP resources
    /// via Window → TextMeshPro → Import TMP Essential Resources.
    /// </summary>
    public static class OblastUI
    {
        // ── Palette ──────────────────────────────────────────────────────────
        // Institutional, not cinematic: unbleached paper on a room nobody has repainted. The single warm
        // colour is a rubber stamp, and it is used sparingly enough to still mean something.

        public static readonly Color Background = new Color(0.043f, 0.043f, 0.047f, 1f);
        public static readonly Color Panel = new Color(0.075f, 0.075f, 0.082f, 1f);
        public static readonly Color PanelRaised = new Color(0.110f, 0.110f, 0.118f, 1f);
        public static readonly Color Hairline = new Color(1f, 1f, 1f, 0.10f);

        public static readonly Color TextPrimary = new Color(0.788f, 0.780f, 0.753f, 1f);
        public static readonly Color TextDim = new Color(0.478f, 0.471f, 0.451f, 1f);
        public static readonly Color TextFaint = new Color(0.318f, 0.314f, 0.302f, 1f);

        public static readonly Color Stamp = new Color(0.706f, 0.333f, 0.184f, 1f);
        public static readonly Color Danger = new Color(0.639f, 0.227f, 0.173f, 1f);
        public static readonly Color Olive = new Color(0.431f, 0.486f, 0.353f, 1f);

        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static Sprite _whiteSprite;

        /// <summary>A 1×1 white sprite every Image tints. Built once, reused for the process lifetime.</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var tex = new Texture2D(4, 4);
                    var pixels = new Color[16];
                    for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
                    tex.SetPixels(pixels);
                    tex.Apply();
                    _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 100f);
                    _whiteSprite.name = "OblastUI_White";
                }
                return _whiteSprite;
            }
        }

        // ── Root construction ────────────────────────────────────────────────

        /// <summary>
        /// Builds a full-screen overlay canvas under <paramref name="parent"/>, complete with a scaler and a
        /// raycaster so buttons are clickable. Returns the canvas's RectTransform to parent content onto.
        /// </summary>
        public static RectTransform CreateScreenCanvas(Transform parent, string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            return go.GetComponent<RectTransform>();
        }

        /// <summary>An empty RectTransform child, for grouping.</summary>
        public static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // ── Widgets ──────────────────────────────────────────────────────────

        public static Image Rect(Transform parent, string name, Color color, bool raycast = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = WhiteSprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, string text, float size,
                                            FontStyles style, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = align;
            label.color = color;
            label.richText = true;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// A flat institutional button: filled rectangle, hairline top edge, centred label. Colour-tint
        /// transitions come from Unity's own Selectable states, so hover/press need no extra scripts.
        /// The returned Button's <c>onClick</c> is already wired to <paramref name="onClick"/>.
        /// </summary>
        public static Button Button(Transform parent, string name, string text, float fontSize,
                                    System.Action onClick, out TextMeshProUGUI label)
        {
            var image = Rect(parent, name, PanelRaised, raycast: true);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1.5f, 1.5f, 1.55f, 1f),
                pressedColor = new Color(0.7f, 0.7f, 0.75f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            // Click before the callback: several of these callbacks tear their own screen down, and a
            // listener registered after would be destroyed along with it before it ever fired.
            button.onClick.AddListener(OblastUIAudio.PlayClick);
            if (onClick != null) button.onClick.AddListener(() => onClick());

            // Hover/click sound for every button in the game, added here rather than screen by screen:
            // this factory is the single construction site for buttons (MainMenuUI, RunSetupUI,
            // RunSummaryUI, EventModalUI all route through it), so wiring audio once means a screen
            // added later cannot forget it.
            OblastUIAudio.AttachHover(button.gameObject);

            // Hairline along the top edge — reads as a stamped form field rather than a game button.
            var edge = Rect(image.transform, "Edge", Hairline);
            StretchTop(edge.rectTransform, 1f);

            label = Label(image.transform, "Label", text, fontSize, FontStyles.Bold,
                          TextAlignmentOptions.Center, TextPrimary);
            Stretch(label.rectTransform, 18f, 0f);

            return button;
        }

        /// <summary>A 1px horizontal rule — the form-ruling that holds these screens together.</summary>
        public static Image Rule(Transform parent, string name, float width, Color color)
        {
            var rule = Rect(parent, name, color);
            rule.rectTransform.sizeDelta = new Vector2(width, 1f);
            return rule;
        }

        /// <summary>Applies the standard disabled look to a button plus its label.</summary>
        public static void SetInteractable(Button button, TextMeshProUGUI label, bool interactable)
        {
            if (button != null) button.interactable = interactable;
            if (label != null) label.color = interactable ? TextPrimary : TextFaint;
        }

        // ── Anchoring ────────────────────────────────────────────────────────
        // Explicit anchors, never LayoutElement-without-a-LayoutGroup: a LayoutElement whose parent has no
        // layout group is inert, which silently piles every widget at the screen centre.

        public static void Stretch(RectTransform rt, float padX = 0f, float padY = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }

        public static void StretchTop(RectTransform rt, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(0f, -height);
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Full-width band pinned a given distance below the top of its parent.</summary>
        public static void StretchBand(RectTransform rt, float topOffset, float height, float sideInset = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(sideInset, -(topOffset + height));
            rt.offsetMax = new Vector2(-sideInset, -topOffset);
        }

        public static void Center(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void TopCenter(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void TopLeft(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void BottomLeft(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void BottomRight(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static void BottomCenter(RectTransform rt, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }
    }
}
