// Assets/_Project/Scripts/UI/MainMenuUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;

namespace OblastZero.UI
{
    /// <summary>
    /// The title screen. Builds its whole canvas on Awake, exactly like BunkerHUD and ScavengeHUD, so
    /// MainMenuState only has to spawn a GameObject with this component on it.
    ///
    /// Presentation only. It raises <see cref="NewRunRequested"/> / <see cref="ContinueRequested"/> /
    /// <see cref="QuitRequested"/> and MainMenuState decides what those mean — this class never touches
    /// the state machine, never begins a run, and never loads a save.
    ///
    /// "Continue" is disabled unless an expedition save actually exists on disk, so a fresh install
    /// cannot offer a button that would dead-end.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        /// <summary>Raised by the "New Registration" button.</summary>
        public event Action NewRunRequested;

        /// <summary>Raised by the "Resume Filing" button. Only reachable when a save exists.</summary>
        public event Action ContinueRequested;

        /// <summary>Raised by the "Close File" button.</summary>
        public event Action QuitRequested;

        private TextMeshProUGUI _continueLabel;
        private Button _continueButton;
        private TextMeshProUGUI _stampLine;
        private TextMeshProUGUI _recordLine;

        private bool _hasSave;

        private void Awake() => BuildUI();

        /// <summary>
        /// Tells the screen whether a resumable expedition exists. Called by the state after it asks the
        /// save service — the UI does not go looking for save files itself.
        /// </summary>
        public void SetContinueAvailable(bool available)
        {
            _hasSave = available;
            OblastUI.SetInteractable(_continueButton, _continueLabel, available);
            if (_stampLine != null)
            {
                _stampLine.text = available
                    ? "FILE OPEN — EXPEDITION PENDING REVIEW"
                    : "NO FILE ON RECORD FOR THIS APPLICANT";
                _stampLine.color = available ? OblastUI.Stamp : OblastUI.TextFaint;
            }
        }

        /// <summary>Shows the run tally under the title. Meta-progression is the state's to read, not ours.</summary>
        public void SetRecord(int runsAttempted, int runsSurvived)
        {
            if (_recordLine == null) return;
            _recordLine.text = runsAttempted <= 0
                ? "REGISTRATIONS FILED: NONE"
                : $"REGISTRATIONS FILED: {runsAttempted}     RETURNED: {runsSurvived}";
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildUI()
        {
            var root = OblastUI.CreateScreenCanvas(transform, "MainMenu_Canvas", 50);

            var bg = OblastUI.Rect(root, "Background", OblastUI.Background, raycast: true);
            OblastUI.Stretch(bg.rectTransform);

            // A dim slab behind the title block, so the type sits on something rather than floating.
            var slab = OblastUI.Rect(root, "TitleSlab", OblastUI.Panel);
            OblastUI.StretchBand(slab.rectTransform, 150f, 300f, 260f);

            var slabEdge = OblastUI.Rect(slab.transform, "SlabEdge", OblastUI.Hairline);
            OblastUI.StretchTop(slabEdge.rectTransform, 1f);

            var title = OblastUI.Label(root, "Title", "OBLAST ZERO", 132f, FontStyles.Bold,
                                       TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.StretchBand(title.rectTransform, 196f, 150f);
            title.characterSpacing = 18f;

            var subtitle = OblastUI.Label(root, "Subtitle",
                                          "REGISTERED FOR DEMOGRAPHIC ADJUSTMENT",
                                          26f, FontStyles.Normal, TextAlignmentOptions.Center,
                                          OblastUI.TextDim);
            OblastUI.StretchBand(subtitle.rectTransform, 348f, 34f);
            subtitle.characterSpacing = 9f;

            var rule = OblastUI.Rule(root, "TitleRule", 520f, OblastUI.Hairline);
            OblastUI.TopCenter(rule.rectTransform, new Vector2(0f, -400f), new Vector2(520f, 1f));

            _recordLine = OblastUI.Label(root, "Record", "REGISTRATIONS FILED: NONE", 20f,
                                         FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.StretchBand(_recordLine.rectTransform, 420f, 28f);
            _recordLine.characterSpacing = 5f;

            // ── Buttons ──────────────────────────────────────────────────────
            const float buttonWidth = 420f;
            const float buttonHeight = 74f;
            const float gap = 14f;
            float top = 512f;

            TextMeshProUGUI newRunLabel;
            var newRun = OblastUI.Button(root, "NewRunButton", "NEW REGISTRATION", 26f,
                                         () => NewRunRequested?.Invoke(), out newRunLabel);
            OblastUI.TopCenter(newRun.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            newRunLabel.characterSpacing = 6f;

            top += buttonHeight + gap;
            _continueButton = OblastUI.Button(root, "ContinueButton", "RESUME FILING", 26f,
                                              () => ContinueRequested?.Invoke(), out _continueLabel);
            OblastUI.TopCenter(_continueButton.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            _continueLabel.characterSpacing = 6f;

            top += buttonHeight + gap;
            TextMeshProUGUI quitLabel;
            var quit = OblastUI.Button(root, "QuitButton", "CLOSE FILE", 26f,
                                       () => QuitRequested?.Invoke(), out quitLabel);
            OblastUI.TopCenter(quit.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            quitLabel.characterSpacing = 6f;

            // ── Footer ───────────────────────────────────────────────────────
            _stampLine = OblastUI.Label(root, "StampLine", "NO FILE ON RECORD FOR THIS APPLICANT", 19f,
                                        FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.BottomCenter(_stampLine.rectTransform, new Vector2(0f, 108f), new Vector2(900f, 26f));
            _stampLine.characterSpacing = 4f;

            var footer = OblastUI.Label(root, "Footer",
                                        "OBLAST ADMINISTRATIVE DISTRICT ZERO  ·  FORM 4-B  ·  RETAIN FOR YOUR RECORDS",
                                        16f, FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.BottomCenter(footer.rectTransform, new Vector2(0f, 54f), new Vector2(1400f, 24f));
            footer.characterSpacing = 3f;

            // Default state until the owning state reports what is on disk.
            OblastUI.SetInteractable(_continueButton, _continueLabel, false);

            Debug.Log("[MainMenuUI] Title screen built.");
        }

        private void Update()
        {
            // The one piece of motion on the screen: the record line breathes very slightly, like a tube
            // that has not been replaced. Nothing else moves — the Oblast does not raise its voice.
            if (_stampLine == null || !_hasSave) return;
            float pulse = 0.72f + 0.28f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 1.4f));
            var c = OblastUI.Stamp;
            _stampLine.color = new Color(c.r, c.g, c.b, pulse);
        }
    }
}
