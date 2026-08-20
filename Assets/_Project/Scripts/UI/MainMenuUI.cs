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
    ///
    /// Every player-facing string resolves through <see cref="LocalizedStrings"/> under a
    /// <see cref="UIStringKeys"/> constant. The screen rebuilds itself on
    /// <see cref="LocalizedStrings.LanguageChanged"/>, so switching language in the options screen is
    /// visible immediately rather than on the next scene load.
    /// </summary>
    public class MainMenuUI : MonoBehaviour
    {
        /// <summary>Raised by the "New Registration" button.</summary>
        public event Action NewRunRequested;

        /// <summary>Raised by the "Resume Filing" button. Only reachable when a save exists.</summary>
        public event Action ContinueRequested;

        /// <summary>Raised by the "Operating Parameters" button.</summary>
        public event Action OptionsRequested;

        /// <summary>Raised by the "Supply Office" button.</summary>
        public event Action SupplyOfficeRequested;

        /// <summary>Raised by the "Close File" button.</summary>
        public event Action QuitRequested;

        private TextMeshProUGUI _continueLabel;
        private Button _continueButton;
        private TextMeshProUGUI _stampLine;
        private TextMeshProUGUI _recordLine;
        private TextMeshProUGUI _tokenLine;
        private int _tokens;
        private RectTransform _root;

        private bool _hasSave;
        private int _runsAttempted;
        private int _runsSurvived;

        private void Awake() => BuildUI();

        private void OnEnable() => LocalizedStrings.LanguageChanged += Rebuild;
        private void OnDisable() => LocalizedStrings.LanguageChanged -= Rebuild;

        /// <summary>
        /// Tears the canvas down and rebuilds it in the new language, then restores the two pieces of state
        /// the screen was showing. Rebuilding beats walking the tree and re-assigning labels: the labels are
        /// created in one place, so there is exactly one list of strings to keep correct.
        /// </summary>
        private void Rebuild()
        {
            if (_root != null) Destroy(_root.gameObject);
            BuildUI();
            SetContinueAvailable(_hasSave);
            SetRecord(_runsAttempted, _runsSurvived);
            SetTokenBalance(_tokens);
        }

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
                _stampLine.text = LocalizedStrings.Get(available
                    ? UIStringKeys.MenuFileOpen
                    : UIStringKeys.MenuFileNone);
                _stampLine.color = available ? OblastUI.Stamp : OblastUI.TextFaint;
            }
        }

        /// <summary>Shows the run tally under the title. Meta-progression is the state's to read, not ours.</summary>
        public void SetRecord(int runsAttempted, int runsSurvived)
        {
            _runsAttempted = runsAttempted;
            _runsSurvived = runsSurvived;

            if (_recordLine == null) return;
            _recordLine.text = runsAttempted <= 0
                ? LocalizedStrings.Get(UIStringKeys.MenuRecordNone)
                : LocalizedStrings.Get(UIStringKeys.MenuRecord, runsAttempted, runsSurvived);
        }

        /// <summary>
        /// Shows the Salvage Token balance beside the run tally, so the Supply Office button is never a
        /// blind click. Like <see cref="SetRecord"/>, the state supplies the number — the screen does not
        /// read the profile.
        /// </summary>
        public void SetTokenBalance(int tokens)
        {
            _tokens = tokens;
            if (_tokenLine == null) return;
            _tokenLine.text = LocalizedStrings.Get(UIStringKeys.MenuTokens, tokens);
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildUI()
        {
            var root = OblastUI.CreateScreenCanvas(transform, "MainMenu_Canvas", 50);
            _root = root;

            var bg = OblastUI.Rect(root, "Background", OblastUI.Background, raycast: true);
            OblastUI.Stretch(bg.rectTransform);

            // A dim slab behind the title block, so the type sits on something rather than floating.
            var slab = OblastUI.Rect(root, "TitleSlab", OblastUI.Panel);
            OblastUI.StretchBand(slab.rectTransform, 150f, 300f, 260f);

            var slabEdge = OblastUI.Rect(slab.transform, "SlabEdge", OblastUI.Hairline);
            OblastUI.StretchTop(slabEdge.rectTransform, 1f);

            var title = OblastUI.Label(root, "Title", LocalizedStrings.Get(UIStringKeys.MenuTitle),
                                       132f, FontStyles.Bold,
                                       TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.StretchBand(title.rectTransform, 196f, 150f);
            title.characterSpacing = 18f;

            var subtitle = OblastUI.Label(root, "Subtitle",
                                          LocalizedStrings.Get(UIStringKeys.MenuSubtitle),
                                          26f, FontStyles.Normal, TextAlignmentOptions.Center,
                                          OblastUI.TextDim);
            OblastUI.StretchBand(subtitle.rectTransform, 348f, 34f);
            subtitle.characterSpacing = 9f;

            var rule = OblastUI.Rule(root, "TitleRule", 520f, OblastUI.Hairline);
            OblastUI.TopCenter(rule.rectTransform, new Vector2(0f, -400f), new Vector2(520f, 1f));

            _recordLine = OblastUI.Label(root, "Record", LocalizedStrings.Get(UIStringKeys.MenuRecordNone), 20f,
                                         FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.StretchBand(_recordLine.rectTransform, 420f, 28f);
            _recordLine.characterSpacing = 5f;

            _tokenLine = OblastUI.Label(root, "Tokens", LocalizedStrings.Get(UIStringKeys.MenuTokens, 0), 20f,
                                        FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.Stamp);
            OblastUI.StretchBand(_tokenLine.rectTransform, 448f, 28f);
            _tokenLine.characterSpacing = 5f;

            // ── Buttons ──────────────────────────────────────────────────────
            const float buttonWidth = 420f;
            const float buttonHeight = 74f;
            const float gap = 14f;
            // Five buttons at 74+14 each need 426 px. Starting at 512 put the last one 8 px off the stamp
            // line once the Supply Office was added; 470 restores the breathing room the screen had at four.
            float top = 470f;

            TextMeshProUGUI newRunLabel;
            var newRun = OblastUI.Button(root, "NewRunButton", LocalizedStrings.Get(UIStringKeys.MenuNewRun), 26f,
                                         () => NewRunRequested?.Invoke(), out newRunLabel);
            OblastUI.TopCenter(newRun.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            newRunLabel.characterSpacing = 6f;

            top += buttonHeight + gap;
            _continueButton = OblastUI.Button(root, "ContinueButton", LocalizedStrings.Get(UIStringKeys.MenuContinue), 26f,
                                              () => ContinueRequested?.Invoke(), out _continueLabel);
            OblastUI.TopCenter(_continueButton.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            _continueLabel.characterSpacing = 6f;

            top += buttonHeight + gap;
            TextMeshProUGUI supplyLabel;
            var supply = OblastUI.Button(root, "SupplyOfficeButton",
                                         LocalizedStrings.Get(UIStringKeys.MenuSupplyOffice), 26f,
                                         () => SupplyOfficeRequested?.Invoke(), out supplyLabel);
            OblastUI.TopCenter(supply.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            supplyLabel.characterSpacing = 6f;

            top += buttonHeight + gap;
            TextMeshProUGUI optionsLabel;
            var options = OblastUI.Button(root, "OptionsButton", LocalizedStrings.Get(UIStringKeys.MenuOptions), 26f,
                                          () => OptionsRequested?.Invoke(), out optionsLabel);
            OblastUI.TopCenter(options.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            optionsLabel.characterSpacing = 6f;

            top += buttonHeight + gap;
            TextMeshProUGUI quitLabel;
            var quit = OblastUI.Button(root, "QuitButton", LocalizedStrings.Get(UIStringKeys.MenuQuit), 26f,
                                       () => QuitRequested?.Invoke(), out quitLabel);
            OblastUI.TopCenter(quit.GetComponent<RectTransform>(),
                               new Vector2(0f, -top), new Vector2(buttonWidth, buttonHeight));
            quitLabel.characterSpacing = 6f;

            // The pad navigates this screen too; without an explicit first selection a gamepad-only player
            // sees no highlight at all and the menu looks unresponsive.
            ControllerNavigationUI.Attach(root.gameObject, newRun);

            // ── Footer ───────────────────────────────────────────────────────
            _stampLine = OblastUI.Label(root, "StampLine", LocalizedStrings.Get(UIStringKeys.MenuFileNone), 19f,
                                        FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.BottomCenter(_stampLine.rectTransform, new Vector2(0f, 108f), new Vector2(900f, 26f));
            _stampLine.characterSpacing = 4f;

            var footer = OblastUI.Label(root, "Footer",
                                        LocalizedStrings.Get(UIStringKeys.MenuFooter),
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
