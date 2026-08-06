// Assets/_Project/Scripts/UI/FirstRunTooltips.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Services;

namespace OblastZero.UI
{
    /// <summary>
    /// One-time guidance for a player's first registration: a card of two or three lines, shown over the
    /// screen that needs explaining, dismissed by a button and never seen again.
    ///
    /// <para><b>What decides "first".</b> <c>MetaProgressData.totalRunsAttempted</c>, read at the moment the
    /// card would appear. That counter is incremented by <c>GameManager.BeginNewRun</c>, which means the
    /// registration screen sees 0 and every screen after the run starts sees 1 — so the cards must be gated
    /// on <see cref="ShouldShow"/> with the threshold each surface needs, not on a shared "is first run"
    /// boolean. Getting this wrong shows the scavenge controls card to a player on their tenth run, or never
    /// shows it at all; both were possible readings of the counter and neither is obvious from its name.</para>
    ///
    /// <para><b>What it does not do.</b> It does not pause anything, gate anything, or block the button
    /// underneath — it is a card the player reads and dismisses. A tutorial that stops a sixty-second timer
    /// would teach the wrong thing about the phase it is introducing.</para>
    ///
    /// <para>Presentation only. It reads a counter and draws text; it writes nothing.</para>
    /// </summary>
    public class FirstRunTooltips : MonoBehaviour
    {
        /// <summary>Raised when the player dismisses the card.</summary>
        public event Action Dismissed;

        private RectTransform _root;

        /// <summary>
        /// True when guidance should be shown for a surface reached after at most
        /// <paramref name="maxRunsAttempted"/> registrations.
        ///
        /// Pass 0 for screens that appear BEFORE <c>BeginNewRun</c> increments the counter (the registration
        /// form), and 1 for screens that appear after it (the Blowout, the bunker). Anything else is a
        /// judgement about how long guidance should persist, and belongs at the call site.
        /// </summary>
        public static bool ShouldShow(int maxRunsAttempted)
        {
            var meta = GameManager.Instance != null ? GameManager.Instance.MetaProgress : null;
            if (meta == null) return false;
            return meta.totalRunsAttempted <= maxRunsAttempted;
        }

        /// <summary>
        /// Spawns a guidance card under <paramref name="parent"/> and returns it, or null when this player
        /// is past their first registration. The caller keeps the reference only to destroy it early.
        /// </summary>
        public static FirstRunTooltips Show(Transform parent, int maxRunsAttempted, params string[] bodyKeys)
        {
            if (!ShouldShow(maxRunsAttempted)) return null;
            if (bodyKeys == null || bodyKeys.Length == 0) return null;

            var host = new GameObject("FirstRunTooltips");
            host.transform.SetParent(parent, false);

            var card = host.AddComponent<FirstRunTooltips>();
            card.Build(bodyKeys);
            return card;
        }

        /// <summary>
        /// The scavenge controls card, whose body has to name the player's actual key bindings. Kept here
        /// rather than at the call site so the four placeholders and the four bindings cannot drift apart.
        /// </summary>
        public static FirstRunTooltips ShowScavengeControls(Transform parent)
        {
            if (!ShouldShow(1)) return null;

            var prefs = ServiceLocator.TryGet<PreferencesService>(out var service) && service != null
                ? service.Current
                : PlayerPreferencesData.CreateDefaults();

            string move = string.Concat(
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.MoveForward)),
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.MoveLeft)),
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.MoveBackward)),
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.MoveRight)));

            string body = LocalizedStrings.Get(
                UIStringKeys.FirstRunScavengeControls,
                move,
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.Sprint)),
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.Interact)),
                InputBindingTable.DisplayName(prefs.GetBinding(OblastAction.Pause)));

            var host = new GameObject("FirstRunTooltips");
            host.transform.SetParent(parent, false);

            var card = host.AddComponent<FirstRunTooltips>();
            card.BuildFromText(new List<string> { body });
            return card;
        }

        /// <summary>Removes the card immediately, e.g. when its screen is torn down.</summary>
        public void Close()
        {
            if (_root != null) Destroy(_root.gameObject);
            Destroy(gameObject);
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void Build(IReadOnlyList<string> bodyKeys)
        {
            var lines = new List<string>(bodyKeys.Count);
            foreach (var key in bodyKeys) lines.Add(LocalizedStrings.Get(key));
            BuildFromText(lines);
        }

        private void BuildFromText(IReadOnlyList<string> lines)
        {
            // Above every screen it can appear over (options is 150), because guidance the player cannot
            // see is worse than no guidance: it costs a dismissal they never made.
            _root = OblastUI.CreateScreenCanvas(transform, "FirstRun_Canvas", 180);

            // Bottom-anchored and NOT full-screen dimmed: the card explains the screen behind it, so
            // covering that screen would defeat the point.
            float height = 132f + lines.Count * 62f;

            var card = OblastUI.Rect(_root, "Card", OblastUI.Panel, raycast: true);
            OblastUI.BottomCenter(card.rectTransform, new Vector2(0f, 60f), new Vector2(1120f, height));

            var edge = OblastUI.Rect(card.transform, "Edge", OblastUI.Stamp);
            OblastUI.StretchTop(edge.rectTransform, 2f);

            var banner = OblastUI.Label(card.transform, "Banner",
                                        LocalizedStrings.Get(UIStringKeys.FirstRunBanner),
                                        22f, FontStyles.Bold, TextAlignmentOptions.TopLeft, OblastUI.Stamp);
            OblastUI.TopLeft(banner.rectTransform, new Vector2(36f, -24f), new Vector2(1048f, 28f));
            banner.characterSpacing = 5f;

            float y = 66f;
            for (int i = 0; i < lines.Count; i++)
            {
                var body = OblastUI.Label(card.transform, $"Line{i}", lines[i], 21f, FontStyles.Normal,
                                          TextAlignmentOptions.TopLeft, OblastUI.TextPrimary);
                OblastUI.TopLeft(body.rectTransform, new Vector2(36f, -y), new Vector2(1048f, 58f));
                body.enableWordWrapping = true;
                y += 62f;
            }

            TextMeshProUGUI dismissLabel;
            var dismiss = OblastUI.Button(card.transform, "DismissButton",
                                          LocalizedStrings.Get(UIStringKeys.FirstRunDismiss), 21f,
                                          OnDismiss, out dismissLabel);
            OblastUI.BottomRight(dismiss.GetComponent<RectTransform>(),
                                 new Vector2(-36f, 22f), new Vector2(240f, 54f));
            dismissLabel.characterSpacing = 4f;

            Debug.Log($"[FirstRunTooltips] Shown ({lines.Count} line(s)) — first registration guidance.");
        }

        private void OnDismiss()
        {
            Dismissed?.Invoke();
            Close();
        }

        private void Update()
        {
            // The card is the only thing a first-time player is looking at, so accept the obvious dismiss
            // gestures as well as the button: Enter, Space, or the pad's south button.
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;

            bool confirmed =
                (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                      keyboard.spaceKey.wasPressedThisFrame)) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);

            if (confirmed) OnDismiss();
        }
    }
}
