// Assets/_Project/Scripts/UI/PauseMenuUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;

namespace OblastZero.UI
{
    /// <summary>
    /// The in-run pause overlay: RESUME, OPERATING PARAMETERS, ABANDON FILING.
    ///
    /// <para><b>It does not stop time.</b> The Blowout is a sixty-second real-time panic and the emission
    /// timer is the whole tension of the phase; a pause menu that froze it would turn "should I risk the
    /// pit?" into "let me stop and think about the pit", which is a different game. So the overlay releases
    /// the cursor, blocks the player controller's input and says outright, in the subtitle, that the clock
    /// is still running. That honesty is cheaper than a hidden rule the player discovers by dying.</para>
    ///
    /// <para>Presentation only: it raises three events and the phase state decides what they mean. The
    /// state is also what suppresses the player controller while this is open, since the controller is a
    /// gameplay object and this is not allowed to reach into it.</para>
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        /// <summary>Raised by RESUME, and by pressing the pause key again while open.</summary>
        public event Action ResumeRequested;

        /// <summary>Raised by OPERATING PARAMETERS.</summary>
        public event Action OptionsRequested;

        /// <summary>Raised by ABANDON FILING — the state decides whether that ends the run or returns to menu.</summary>
        public event Action AbandonRequested;

        private RectTransform _root;
        private GameObject _overlay;

        /// <summary>True while the overlay is on screen.</summary>
        public bool IsOpen => _overlay != null && _overlay.activeSelf;

        private void Awake() => BuildUI();

        private void OnEnable() => LocalizedStrings.LanguageChanged += Rebuild;
        private void OnDisable() => LocalizedStrings.LanguageChanged -= Rebuild;

        private void Rebuild()
        {
            bool wasOpen = IsOpen;
            if (_root != null) Destroy(_root.gameObject);
            BuildUI();
            if (wasOpen) Open();
        }

        /// <summary>Shows the overlay and frees the cursor so the buttons are clickable.</summary>
        public void Open()
        {
            if (_overlay == null) return;
            _overlay.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>Hides the overlay. The caller re-locks the cursor if its phase wants it locked.</summary>
        public void Close()
        {
            if (_overlay == null) return;
            _overlay.SetActive(false);
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Above the scavenge HUD (100) and the bunker HUD (90), below the options screen (150) so the
            // options screen opened from here draws over it rather than under it.
            _root = OblastUI.CreateScreenCanvas(transform, "PauseMenu_Canvas", 140);

            var overlay = OblastUI.Rect(_root, "Overlay", new Color(0f, 0f, 0f, 0.78f), raycast: true);
            OblastUI.Stretch(overlay.rectTransform);
            _overlay = overlay.gameObject;

            var card = OblastUI.Rect(overlay.transform, "Card", OblastUI.Panel, raycast: true);
            OblastUI.Center(card.rectTransform, Vector2.zero, new Vector2(680f, 460f));

            var cardEdge = OblastUI.Rect(card.transform, "Edge", OblastUI.Hairline);
            OblastUI.StretchTop(cardEdge.rectTransform, 1f);

            var title = OblastUI.Label(card.transform, "Title", LocalizedStrings.Get(UIStringKeys.PauseTitle),
                                       44f, FontStyles.Bold, TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.TopCenter(title.rectTransform, new Vector2(0f, -44f), new Vector2(620f, 56f));
            title.characterSpacing = 8f;

            var subtitle = OblastUI.Label(card.transform, "Subtitle",
                                          LocalizedStrings.Get(UIStringKeys.PauseSubtitle),
                                          18f, FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.TopCenter(subtitle.rectTransform, new Vector2(0f, -106f), new Vector2(580f, 52f));
            subtitle.characterSpacing = 2f;

            var rule = OblastUI.Rule(card.transform, "Rule", 560f, OblastUI.Hairline);
            OblastUI.TopCenter(rule.rectTransform, new Vector2(0f, -170f), new Vector2(560f, 1f));

            const float buttonWidth = 480f;
            const float buttonHeight = 68f;
            const float gap = 12f;
            float top = 200f;

            TextMeshProUGUI resumeLabel;
            var resume = OblastUI.Button(card.transform, "ResumeButton",
                                         LocalizedStrings.Get(UIStringKeys.PauseResume), 24f,
                                         () => ResumeRequested?.Invoke(), out resumeLabel);
            OblastUI.TopCenter(resume.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            resumeLabel.characterSpacing = 5f;

            top += buttonHeight + gap;
            TextMeshProUGUI optionsLabel;
            var options = OblastUI.Button(card.transform, "OptionsButton",
                                          LocalizedStrings.Get(UIStringKeys.PauseOptions), 24f,
                                          () => OptionsRequested?.Invoke(), out optionsLabel);
            OblastUI.TopCenter(options.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            optionsLabel.characterSpacing = 5f;

            top += buttonHeight + gap;
            TextMeshProUGUI abandonLabel;
            var abandon = OblastUI.Button(card.transform, "AbandonButton",
                                          LocalizedStrings.Get(UIStringKeys.PauseAbandon), 24f,
                                          () => AbandonRequested?.Invoke(), out abandonLabel);
            OblastUI.TopCenter(abandon.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            abandonLabel.characterSpacing = 5f;
            abandonLabel.color = OblastUI.Danger;

            ControllerNavigationUI.Attach(_root.gameObject, resume);

            _overlay.SetActive(false);

            Debug.Log("[PauseMenuUI] Pause overlay built.");
        }
    }
}
