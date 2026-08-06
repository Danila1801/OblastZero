// Assets/_Project/Scripts/UI/OptionsUI.cs
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
    /// The settings screen: four tabs (audio, display, controls, language) built from the shared
    /// <see cref="OblastUI"/> vocabulary, like every other screen in this project.
    ///
    /// <para>Presentation plus one dependency. Unlike the HUDs, this screen holds a
    /// <see cref="PreferencesService"/> reference and calls it directly rather than raising intents. That is
    /// deliberate and is the one place it is correct: preferences are not game state. Nothing in RunData or
    /// MetaProgressData moves when a slider moves, no state machine transition is implied, and routing a
    /// volume slider through the EventBus and back would add a hop for no separation — the rule in CLAUDE.md
    /// §3 exists to keep UI out of GAME LOGIC, and a master volume is not game logic.</para>
    ///
    /// <para>Every value applies the instant it is changed and persists immediately, so there is no APPLY
    /// button and no way to lose a change by leaving the screen. A player who breaks their display settings
    /// can still reach RESTORE STANDARD ISSUE with the keyboard, because the tab bar and the reset button
    /// are the first two things in the navigation chain.</para>
    ///
    /// <para>Spawned and destroyed by whatever opened it (the main menu state, or the pause menu), which is
    /// also what decides where BACK goes — this screen just raises <see cref="CloseRequested"/>.</para>
    /// </summary>
    public class OptionsUI : MonoBehaviour
    {
        /// <summary>Raised by BACK, or by the pause/cancel key.</summary>
        public event Action CloseRequested;

        private enum Tab
        {
            Audio,
            Display,
            Controls,
            Language
        }

        private const float RowHeight = 62f;
        private const float PanelLeft = 360f;
        private const float PanelTop = 268f;
        private const float PanelWidth = 1200f;
        private const float LabelWidth = 430f;

        private PreferencesService _prefs;

        private RectTransform _root;
        private RectTransform _panel;
        private TextMeshProUGUI _noteLabel;
        private Tab _tab = Tab.Audio;

        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<TextMeshProUGUI> _tabLabels = new List<TextMeshProUGUI>();
        private readonly List<Resolution> _resolutions = new List<Resolution>();

        private ControllerNavigationUI _navigation;

        /// <summary>The action currently awaiting a key press, or null when not rebinding.</summary>
        private OblastAction? _awaitingRebind;
        private TextMeshProUGUI _awaitingLabel;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            if (!ServiceLocator.TryGet<PreferencesService>(out _prefs) || _prefs == null)
            {
                Debug.LogError("[OptionsUI] No PreferencesService registered. The screen will build, but " +
                               "nothing it shows can be changed. Register the service in GameManager boot.");
            }

            _resolutions.AddRange(PreferencesService.AvailableResolutions());
            BuildChrome();
            BuildTabContent();
        }

        private void OnEnable() => LocalizedStrings.LanguageChanged += Rebuild;
        private void OnDisable() => LocalizedStrings.LanguageChanged -= Rebuild;

        private void Update()
        {
            if (_awaitingRebind.HasValue) PollForRebindKey();
        }

        private void Rebuild()
        {
            if (_root != null) Destroy(_root.gameObject);
            _tabButtons.Clear();
            _tabLabels.Clear();
            BuildChrome();
            BuildTabContent();
        }

        // ── Rebinding ────────────────────────────────────────────────────────

        /// <summary>
        /// Captures the next key press for the pending action.
        ///
        /// Polls <c>Keyboard.current.allKeys</c> rather than subscribing to
        /// <c>InputSystem.onAnyButtonPress</c> because the latter also fires for gamepad buttons and mouse
        /// clicks — including the click that started the rebind, which would instantly bind the action to
        /// the left mouse button before the player had touched the keyboard.
        /// </summary>
        private void PollForRebindKey()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelRebind();
                return;
            }

            foreach (var control in keyboard.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                if (!InputBindingTable.IsBindable(control.keyCode)) continue;

                CommitRebind(control.keyCode);
                return;
            }
        }

        private void BeginRebind(OblastAction action, TextMeshProUGUI keyCapLabel)
        {
            _awaitingRebind = action;
            _awaitingLabel = keyCapLabel;
            if (_awaitingLabel != null)
            {
                _awaitingLabel.text = LocalizedStrings.Get(UIStringKeys.OptionsRebindPrompt);
                _awaitingLabel.color = OblastUI.Stamp;
            }
        }

        private void CommitRebind(Key key)
        {
            if (!_awaitingRebind.HasValue) return;

            var action = _awaitingRebind.Value;
            OblastAction? displaced = _prefs != null ? _prefs.Rebind(action, key) : null;

            _awaitingRebind = null;
            _awaitingLabel = null;

            if (displaced.HasValue && _noteLabel != null)
            {
                _noteLabel.text = LocalizedStrings.Get(UIStringKeys.OptionsRebindConflict,
                    LocalizedStrings.Get(InputBindingTable.LabelKeyFor(displaced.Value)));
                _noteLabel.color = OblastUI.Stamp;
            }

            BuildTabContent();
        }

        private void CancelRebind()
        {
            _awaitingRebind = null;
            _awaitingLabel = null;
            BuildTabContent();
        }

        // ── Chrome ───────────────────────────────────────────────────────────

        private void BuildChrome()
        {
            _root = OblastUI.CreateScreenCanvas(transform, "Options_Canvas", 150);

            var bg = OblastUI.Rect(_root, "Background", OblastUI.Background, raycast: true);
            OblastUI.Stretch(bg.rectTransform);

            var band = OblastUI.Rect(_root, "TitleBand", OblastUI.Panel);
            OblastUI.StretchBand(band.rectTransform, 60f, 132f, 300f);
            var bandEdge = OblastUI.Rect(band.transform, "Edge", OblastUI.Hairline);
            OblastUI.StretchTop(bandEdge.rectTransform, 1f);

            var title = OblastUI.Label(_root, "Title", LocalizedStrings.Get(UIStringKeys.OptionsTitle),
                                       54f, FontStyles.Bold, TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.StretchBand(title.rectTransform, 78f, 64f);
            title.characterSpacing = 10f;

            var subtitle = OblastUI.Label(_root, "Subtitle", LocalizedStrings.Get(UIStringKeys.OptionsSubtitle),
                                          19f, FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.StretchBand(subtitle.rectTransform, 146f, 26f);
            subtitle.characterSpacing = 4f;

            BuildTabBar();

            _panel = OblastUI.Group(_root, "TabPanel");
            OblastUI.TopLeft(_panel, new Vector2(PanelLeft, -PanelTop), new Vector2(PanelWidth, 560f));

            var footRule = OblastUI.Rule(_root, "FooterRule", 1440f, OblastUI.Hairline);
            OblastUI.BottomCenter(footRule.rectTransform, new Vector2(0f, 168f), new Vector2(1440f, 1f));

            _noteLabel = OblastUI.Label(_root, "Note", string.Empty, 19f, FontStyles.Normal,
                                        TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.BottomCenter(_noteLabel.rectTransform, new Vector2(0f, 124f), new Vector2(1440f, 30f));
            _noteLabel.characterSpacing = 3f;

            TextMeshProUGUI resetLabel;
            var reset = OblastUI.Button(_root, "ResetButton", LocalizedStrings.Get(UIStringKeys.OptionsReset), 22f,
                                        OnResetPressed, out resetLabel);
            OblastUI.BottomLeft(reset.GetComponent<RectTransform>(), new Vector2(360f, 48f), new Vector2(400f, 66f));
            resetLabel.characterSpacing = 4f;

            TextMeshProUGUI backLabel;
            var back = OblastUI.Button(_root, "BackButton", LocalizedStrings.Get(UIStringKeys.OptionsBack), 22f,
                                       () => CloseRequested?.Invoke(), out backLabel);
            OblastUI.BottomRight(back.GetComponent<RectTransform>(), new Vector2(-360f, 48f), new Vector2(280f, 66f));
            backLabel.characterSpacing = 4f;

            _navigation = ControllerNavigationUI.Attach(_root.gameObject,
                                                        _tabButtons.Count > 0 ? _tabButtons[0] : back);

            Debug.Log("[OptionsUI] Options screen built.");
        }

        private void BuildTabBar()
        {
            var tabs = new[] { Tab.Audio, Tab.Display, Tab.Controls, Tab.Language };
            var keys = new[]
            {
                UIStringKeys.OptionsTabAudio,
                UIStringKeys.OptionsTabDisplay,
                UIStringKeys.OptionsTabControls,
                UIStringKeys.OptionsTabLanguage
            };

            const float tabWidth = 280f;
            const float tabHeight = 58f;
            const float gap = 8f;
            float totalWidth = tabs.Length * tabWidth + (tabs.Length - 1) * gap;
            float x = -totalWidth * 0.5f + tabWidth * 0.5f;

            for (int i = 0; i < tabs.Length; i++)
            {
                var tab = tabs[i];
                TextMeshProUGUI label;
                var button = OblastUI.Button(_root, $"Tab_{tab}", LocalizedStrings.Get(keys[i]), 22f,
                                             () => SelectTab(tab), out label);
                OblastUI.TopCenter(button.GetComponent<RectTransform>(),
                                   new Vector2(x, -196f), new Vector2(tabWidth, tabHeight));
                label.characterSpacing = 5f;

                _tabButtons.Add(button);
                _tabLabels.Add(label);
                x += tabWidth + gap;
            }

            HighlightActiveTab();
        }

        private void SelectTab(Tab tab)
        {
            if (_tab == tab) return;
            CancelRebindQuietly();
            _tab = tab;
            HighlightActiveTab();
            BuildTabContent();
        }

        private void HighlightActiveTab()
        {
            var tabs = new[] { Tab.Audio, Tab.Display, Tab.Controls, Tab.Language };
            for (int i = 0; i < _tabButtons.Count && i < tabs.Length; i++)
            {
                bool active = tabs[i] == _tab;
                var image = _tabButtons[i].targetGraphic as Image;
                if (image != null) image.color = active ? OblastUI.PanelRaised : OblastUI.Panel;
                _tabLabels[i].color = active ? OblastUI.Stamp : OblastUI.TextDim;
            }
        }

        private void CancelRebindQuietly()
        {
            _awaitingRebind = null;
            _awaitingLabel = null;
        }

        // ── Tab content ──────────────────────────────────────────────────────

        private void BuildTabContent()
        {
            if (_panel == null) return;

            for (int i = _panel.childCount - 1; i >= 0; i--)
            {
                var child = _panel.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            switch (_tab)
            {
                case Tab.Audio:    BuildAudioTab(); break;
                case Tab.Display:  BuildDisplayTab(); break;
                case Tab.Controls: BuildControlsTab(); break;
                case Tab.Language: BuildLanguageTab(); break;
            }

            // The panel's controls were just replaced, so the navigation graph points at destroyed objects.
            if (_navigation != null) _navigation.RefreshOrder();
        }

        private void BuildAudioTab()
        {
            var p = _prefs != null ? _prefs.Current : PlayerPreferencesData.CreateDefaults();
            float y = 0f;

            AddSliderRow(UIStringKeys.OptionsVolumeMaster, p.volumeMaster, ref y,
                         v => _prefs?.SetMasterVolume(v));
            AddSliderRow(UIStringKeys.OptionsVolumeSfx, p.volumeSfx, ref y,
                         v => _prefs?.SetChannelVolume(PreferencesService.AudioChannel.Sfx, v));
            AddSliderRow(UIStringKeys.OptionsVolumeMusic, p.volumeMusic, ref y,
                         v => _prefs?.SetChannelVolume(PreferencesService.AudioChannel.Music, v));
            AddSliderRow(UIStringKeys.OptionsVolumeAmbient, p.volumeAmbient, ref y,
                         v => _prefs?.SetChannelVolume(PreferencesService.AudioChannel.Ambient, v));

            AddFootnote(UIStringKeys.OptionsVolumeNote, ref y);
        }

        private void BuildDisplayTab()
        {
            var p = _prefs != null ? _prefs.Current : PlayerPreferencesData.CreateDefaults();
            float y = 0f;

            // Quality levels come from the project's own quality settings, not a hardcoded Low/Medium/High:
            // a build with four tiers would silently lose one, and a build with two would offer a level
            // that does not exist.
            var qualityNames = QualitySettings.names;
            var qualityOptions = new List<string>(qualityNames);
            int qualityIndex = p.qualityLevel >= 0 && p.qualityLevel < qualityNames.Length
                ? p.qualityLevel
                : QualitySettings.GetQualityLevel();

            AddCycleRow(UIStringKeys.OptionsQuality, qualityOptions, qualityIndex, ref y,
                        index => _prefs?.SetQualityLevel(index));

            var resolutionOptions = new List<string>(_resolutions.Count);
            foreach (var res in _resolutions) resolutionOptions.Add($"{res.width} × {res.height}");
            int resolutionIndex = CurrentResolutionIndex(p);

            if (resolutionOptions.Count > 0)
            {
                AddCycleRow(UIStringKeys.OptionsResolution, resolutionOptions, resolutionIndex, ref y,
                            index =>
                            {
                                var res = _resolutions[index];
                                _prefs?.SetResolution(res.width, res.height,
                                                      Mathf.RoundToInt((float)res.refreshRateRatio.value));
                            });
            }

            var modes = new List<string>
            {
                LocalizedStrings.Get(UIStringKeys.OptionsDisplayModeExclusive),
                LocalizedStrings.Get(UIStringKeys.OptionsDisplayModeBorderless),
                LocalizedStrings.Get(UIStringKeys.OptionsDisplayModeWindowed)
            };
            var modeValues = new[]
            {
                FullScreenMode.ExclusiveFullScreen,
                FullScreenMode.FullScreenWindow,
                FullScreenMode.Windowed
            };
            int modeIndex = Array.IndexOf(modeValues, (FullScreenMode)p.fullScreenMode);
            if (modeIndex < 0) modeIndex = 1;

            AddCycleRow(UIStringKeys.OptionsDisplayMode, modes, modeIndex, ref y,
                        index => _prefs?.SetFullScreenMode(modeValues[index]));

            var onOff = new List<string>
            {
                LocalizedStrings.Get(UIStringKeys.OptionsOff),
                LocalizedStrings.Get(UIStringKeys.OptionsOn)
            };
            AddCycleRow(UIStringKeys.OptionsVsync, onOff, p.vSync ? 1 : 0, ref y,
                        index => _prefs?.SetVSync(index == 1));
        }

        private void BuildControlsTab()
        {
            var p = _prefs != null ? _prefs.Current : PlayerPreferencesData.CreateDefaults();
            float y = 0f;

            foreach (var action in InputBindingTable.AllActions)
            {
                Key bound = p.GetBinding(action);
                string cap = bound == Key.None
                    ? LocalizedStrings.Get(UIStringKeys.OptionsUnbound)
                    : InputBindingTable.DisplayName(bound);

                AddBindingRow(action, cap, ref y);
            }

            AddFootnote(UIStringKeys.OptionsControlsNote, ref y);
        }

        private void BuildLanguageTab()
        {
            float y = 0f;

            var codes = LocalizationJsonLoader.SupportedLanguageCodes;
            foreach (var code in codes)
            {
                string labelKey = code == "ru" ? UIStringKeys.OptionsLanguageRussian
                                               : UIStringKeys.OptionsLanguageEnglish;
                bool active = string.Equals(code, LocalizedStrings.ActiveLanguageCode,
                                            StringComparison.OrdinalIgnoreCase);
                string captured = code;

                AddActionRow(LocalizedStrings.Get(labelKey),
                             active ? LocalizedStrings.Get(UIStringKeys.OptionsOn)
                                    : LocalizedStrings.Get(UIStringKeys.OptionsOff),
                             active, ref y,
                             () => _prefs?.ApplyLanguage(captured));
            }

            AddFootnote(UIStringKeys.OptionsLanguageNote, ref y);
        }

        private int CurrentResolutionIndex(PlayerPreferencesData p)
        {
            int width = p.resolutionWidth > 0 ? p.resolutionWidth : Screen.width;
            int height = p.resolutionHeight > 0 ? p.resolutionHeight : Screen.height;

            for (int i = 0; i < _resolutions.Count; i++)
                if (_resolutions[i].width == width && _resolutions[i].height == height) return i;

            return 0;
        }

        private void OnResetPressed()
        {
            CancelRebindQuietly();
            _prefs?.ResetAllToDefaults();
            // ResetAllToDefaults may swap the language, which rebuilds the whole screen through
            // LanguageChanged. When it does not (already English), rebuild the panel ourselves so the
            // restored values are on screen either way.
            BuildTabContent();
        }

        // ── Row widgets ──────────────────────────────────────────────────────

        /// <summary>A labelled row shell: returns the RectTransform that the control sits in.</summary>
        private RectTransform AddRow(string labelText, ref float y)
        {
            var row = OblastUI.Group(_panel, $"Row_{labelText}");
            OblastUI.TopLeft(row, new Vector2(0f, -y), new Vector2(PanelWidth, RowHeight));

            var label = OblastUI.Label(row, "Label", labelText, 24f, FontStyles.Normal,
                                       TextAlignmentOptions.Left, OblastUI.TextDim);
            OblastUI.TopLeft(label.rectTransform, new Vector2(0f, -14f), new Vector2(LabelWidth, 32f));

            var underline = OblastUI.Rect(row, "Underline", new Color(1f, 1f, 1f, 0.05f));
            OblastUI.BottomLeft(underline.rectTransform, Vector2.zero, new Vector2(PanelWidth, 1f));

            y += RowHeight;
            return row;
        }

        /// <summary>
        /// A 0..100 slider with a live percentage readout. Uses Unity's own Slider so pad navigation and
        /// arrow keys adjust it for free — a custom drag widget would need its own controller support.
        /// </summary>
        private void AddSliderRow(string labelKey, int value, ref float y, Action<int> onChanged)
        {
            var row = AddRow(LocalizedStrings.Get(labelKey), ref y);

            var readout = OblastUI.Label(row, "Value", LocalizedStrings.Get(UIStringKeys.OptionsPercent, value),
                                         23f, FontStyles.Bold, TextAlignmentOptions.Right, OblastUI.TextPrimary);
            OblastUI.TopLeft(readout.rectTransform, new Vector2(PanelWidth - 130f, -14f), new Vector2(130f, 32f));

            var sliderGO = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(row, false);
            var sliderRect = (RectTransform)sliderGO.transform;
            OblastUI.TopLeft(sliderRect, new Vector2(LabelWidth + 30f, -22f), new Vector2(560f, 18f));

            var track = OblastUI.Rect(sliderRect, "Track", new Color(1f, 1f, 1f, 0.12f), raycast: true);
            OblastUI.Stretch(track.rectTransform, 0f, 5f);

            var fillArea = OblastUI.Group(sliderRect, "FillArea");
            OblastUI.Stretch(fillArea, 0f, 5f);
            var fill = OblastUI.Rect(fillArea, "Fill", OblastUI.Stamp);

            var handle = OblastUI.Rect(sliderRect, "Handle", OblastUI.TextPrimary, raycast: true);
            handle.rectTransform.sizeDelta = new Vector2(10f, 26f);

            var slider = sliderGO.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(Mathf.Clamp(value, 0, 100));

            slider.onValueChanged.AddListener(v =>
            {
                int rounded = Mathf.RoundToInt(v);
                readout.text = LocalizedStrings.Get(UIStringKeys.OptionsPercent, rounded);
                onChanged?.Invoke(rounded);
            });
        }

        /// <summary>
        /// A row whose value cycles on click: "‹ value ›". A cycling button beats a TMP_Dropdown here
        /// because a dropdown opens a runtime-built list that has to be re-parented above every other
        /// canvas to be visible, and needs its own controller navigation once open. Cycling is one
        /// Selectable, reachable by pad and keyboard with no extra machinery.
        /// </summary>
        private void AddCycleRow(string labelKey, List<string> options, int index, ref float y,
                                 Action<int> onChanged)
        {
            if (options == null || options.Count == 0) return;

            int current = Mathf.Clamp(index, 0, options.Count - 1);
            var row = AddRow(LocalizedStrings.Get(labelKey), ref y);

            TextMeshProUGUI valueLabel = null;

            var button = OblastUI.Button(row, "Value", options[current], 22f, null, out valueLabel);
            OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                             new Vector2(LabelWidth + 30f, -10f), new Vector2(560f, 44f));

            // « and », not ‹ and ›. The shipped LiberationSans SDF atlas carries Latin-1 punctuation and
            // nothing from General Punctuation — the same trap that made the END DAY arrow render as a tofu
            // box. Verify any new glyph against the atlas before using it; TMP warns once and the box reads
            // as a font bug rather than as a missing character.
            var chevronLeft = OblastUI.Label(button.transform, "Left", "«", 24f, FontStyles.Bold,
                                             TextAlignmentOptions.Left, OblastUI.TextFaint);
            OblastUI.Stretch(chevronLeft.rectTransform, 16f, 0f);

            var chevronRight = OblastUI.Label(button.transform, "Right", "»", 24f, FontStyles.Bold,
                                              TextAlignmentOptions.Right, OblastUI.TextFaint);
            OblastUI.Stretch(chevronRight.rectTransform, 16f, 0f);

            int captured = current;
            button.onClick.AddListener(() =>
            {
                captured = (captured + 1) % options.Count;
                valueLabel.text = options[captured];
                onChanged?.Invoke(captured);
            });
        }

        /// <summary>A row whose value is a button that runs an action (language selection).</summary>
        private void AddActionRow(string labelText, string valueText, bool active, ref float y, Action onPressed)
        {
            var row = AddRow(labelText, ref y);

            TextMeshProUGUI valueLabel;
            var button = OblastUI.Button(row, "Value", valueText, 22f, onPressed, out valueLabel);
            OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                             new Vector2(LabelWidth + 30f, -10f), new Vector2(560f, 44f));
            valueLabel.color = active ? OblastUI.Stamp : OblastUI.TextDim;
            button.interactable = !active;
        }

        /// <summary>A controls row: action label on the left, key cap on the right, click to rebind.</summary>
        private void AddBindingRow(OblastAction action, string keyCap, ref float y)
        {
            var row = AddRow(LocalizedStrings.Get(InputBindingTable.LabelKeyFor(action)), ref y);

            TextMeshProUGUI capLabel;
            var button = OblastUI.Button(row, "KeyCap", keyCap, 22f, null, out capLabel);
            OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                             new Vector2(LabelWidth + 30f, -10f), new Vector2(300f, 44f));
            capLabel.characterSpacing = 3f;

            var captured = action;
            button.onClick.AddListener(() => BeginRebind(captured, capLabel));
        }

        private void AddFootnote(string labelKey, ref float y)
        {
            var note = OblastUI.Label(_panel, "Footnote", LocalizedStrings.Get(labelKey), 18f,
                                      FontStyles.Italic, TextAlignmentOptions.TopLeft, OblastUI.TextFaint);
            OblastUI.TopLeft(note.rectTransform, new Vector2(0f, -(y + 18f)), new Vector2(PanelWidth, 60f));
            note.characterSpacing = 2f;
            y += 78f;
        }
    }
}
