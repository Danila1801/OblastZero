// Assets/_Project/Scripts/Services/PreferencesService.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Services
{
    /// <summary>
    /// Owns the live <see cref="PlayerPreferencesData"/> and is the only thing that pushes it into the
    /// engine. Registered on the ServiceLocator at boot; the options screen mutates through it and never
    /// touches <c>QualitySettings</c>, <c>Screen</c> or <c>AudioListener</c> itself.
    ///
    /// <para><b>Audio.</b> Master volume applies immediately and completely, through
    /// <c>AudioListener.volume</c> — that attenuates every source in the scene and needs no mixer asset, so
    /// the master slider is honest the moment it moves. The three channel volumes (effects, music, ambience)
    /// are stored, persisted and broadcast on <see cref="ChannelVolumesChanged"/> with a normalised 0..1
    /// value; a source or mixer that wants to obey them subscribes and applies. This is the seam an
    /// AudioMixer-based system plugs into later: it reads <see cref="ChannelVolume"/> on start and follows
    /// the event thereafter. Until such a system exists, the channel sliders move a value nothing is yet
    /// listening to — which the options screen says in plain words rather than pretending otherwise.</para>
    ///
    /// <para><b>Display.</b> Quality, resolution, display mode and vsync apply immediately and completely.
    /// </para>
    ///
    /// <para><b>Language.</b> Delegated to <see cref="LocalizationJsonLoader"/>, which swaps the table and
    /// raises <c>LocalizedStrings.LanguageChanged</c> so live screens rebuild.</para>
    /// </summary>
    public class PreferencesService : IService
    {
        /// <summary>Audio channels the game recognises. Master is applied directly, not broadcast.</summary>
        public enum AudioChannel
        {
            Sfx,
            Music,
            Ambient
        }

        /// <summary>
        /// Raised whenever a channel volume changes, with the channel and its 0..1 linear value. Also
        /// raised once per channel by <see cref="ApplyAll"/> at boot, so a subscriber that came up before
        /// the preferences did still gets the correct value without polling.
        /// </summary>
        public event Action<AudioChannel, float> ChannelVolumesChanged;

        private readonly ISaveService _save;
        private PlayerPreferencesData _prefs;

        /// <summary>The live preferences. Mutate through this service's methods so changes are applied.</summary>
        public PlayerPreferencesData Current => _prefs;

        public PreferencesService(ISaveService save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _prefs = _save.LoadPreferences();

            // A preferences file written before a language table was removed — or hand-edited — must not
            // strand the player in a language the build cannot render. Fall back rather than trust it.
            if (!LocalizationJsonLoader.IsSupported(_prefs.languageCode))
            {
                Debug.LogWarning($"[PreferencesService] Language '{_prefs.languageCode}' is not shipped in this " +
                                 $"build. Falling back to '{LocalizationJsonLoader.DefaultLanguageCode}'.");
                _prefs.languageCode = LocalizationJsonLoader.DefaultLanguageCode;
            }
        }

        // ─── Application ────────────────────────────────────────────────────────

        /// <summary>
        /// Pushes every stored preference into the engine. Called once at boot, and again after a reset.
        /// Language is applied by the caller at the point in boot where a table swap is safe — see
        /// <see cref="ApplyLanguage"/>.
        /// </summary>
        public void ApplyAll()
        {
            ApplyMasterVolume();
            BroadcastChannelVolumes();
            ApplyQuality();
            ApplyDisplay();
            ApplyVSync();

            Debug.Log($"[PreferencesService] Applied preferences — master {_prefs.volumeMaster}, " +
                      $"quality {_prefs.qualityLevel}, {_prefs.resolutionWidth}x{_prefs.resolutionHeight}, " +
                      $"mode {(FullScreenMode)_prefs.fullScreenMode}, vsync {_prefs.vSync}, " +
                      $"lang {_prefs.languageCode}.");
        }

        /// <summary>Normalised 0..1 volume for a channel, for a source that starts after the last broadcast.</summary>
        public float ChannelVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Sfx:     return _prefs.volumeSfx / 100f;
                case AudioChannel.Music:   return _prefs.volumeMusic / 100f;
                case AudioChannel.Ambient: return _prefs.volumeAmbient / 100f;
                default:                   return 1f;
            }
        }

        private void ApplyMasterVolume()
        {
            AudioListener.volume = Mathf.Clamp01(_prefs.volumeMaster / 100f);
        }

        private void BroadcastChannelVolumes()
        {
            ChannelVolumesChanged?.Invoke(AudioChannel.Sfx, ChannelVolume(AudioChannel.Sfx));
            ChannelVolumesChanged?.Invoke(AudioChannel.Music, ChannelVolume(AudioChannel.Music));
            ChannelVolumesChanged?.Invoke(AudioChannel.Ambient, ChannelVolume(AudioChannel.Ambient));
        }

        private void ApplyQuality()
        {
            if (_prefs.qualityLevel < 0) return;              // -1 = keep whatever the project shipped with
            int count = QualitySettings.names.Length;
            if (count == 0) return;

            int level = Mathf.Clamp(_prefs.qualityLevel, 0, count - 1);
            // applyExpensiveChanges:false — the expensive part is texture/anisotropic reload, which we do
            // not want happening mid-Blowout on a 60-second clock.
            QualitySettings.SetQualityLevel(level, applyExpensiveChanges: false);
        }

        private void ApplyDisplay()
        {
            var mode = (FullScreenMode)_prefs.fullScreenMode;

            // A zeroed resolution means "never chosen" — first launch, or a file written before the field
            // existed. Take the display's own resolution rather than resizing the window to nothing.
            int width = _prefs.resolutionWidth > 0 ? _prefs.resolutionWidth : Screen.currentResolution.width;
            int height = _prefs.resolutionHeight > 0 ? _prefs.resolutionHeight : Screen.currentResolution.height;

            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning("[PreferencesService] No usable resolution available; leaving the display alone.");
                return;
            }

            Screen.SetResolution(width, height, mode);
        }

        private void ApplyVSync()
        {
            QualitySettings.vSyncCount = _prefs.vSync ? 1 : 0;
        }

        // ─── Mutation (each applies, then persists) ─────────────────────────────

        public void SetMasterVolume(int value)
        {
            _prefs.volumeMaster = Mathf.Clamp(value, 0, 100);
            ApplyMasterVolume();
            Save();
        }

        public void SetChannelVolume(AudioChannel channel, int value)
        {
            int clamped = Mathf.Clamp(value, 0, 100);
            switch (channel)
            {
                case AudioChannel.Sfx:     _prefs.volumeSfx = clamped; break;
                case AudioChannel.Music:   _prefs.volumeMusic = clamped; break;
                case AudioChannel.Ambient: _prefs.volumeAmbient = clamped; break;
            }

            ChannelVolumesChanged?.Invoke(channel, ChannelVolume(channel));
            Save();
        }

        public void SetQualityLevel(int level)
        {
            _prefs.qualityLevel = level;
            ApplyQuality();
            Save();
        }

        public void SetResolution(int width, int height, int refreshRateHz)
        {
            _prefs.resolutionWidth = width;
            _prefs.resolutionHeight = height;
            _prefs.refreshRateHz = refreshRateHz;
            ApplyDisplay();
            Save();
        }

        public void SetFullScreenMode(FullScreenMode mode)
        {
            _prefs.fullScreenMode = (int)mode;
            ApplyDisplay();
            Save();
        }

        public void SetVSync(bool enabled)
        {
            _prefs.vSync = enabled;
            ApplyVSync();
            Save();
        }

        /// <summary>
        /// Assigns a key to an action and persists it. Returns the action that lost the key, if any, so the
        /// options screen can say so — a silent displacement reads as an input bug the next time the player
        /// presses the old key and nothing happens.
        /// </summary>
        public OblastAction? Rebind(OblastAction action, UnityEngine.InputSystem.Key key)
        {
            var displaced = _prefs.SetBinding(action, key);
            Save();
            return displaced;
        }

        public void ResetBindings()
        {
            _prefs.ResetBindingsToDefaults();
            Save();
        }

        /// <summary>
        /// Swaps the language table and persists the choice. Refuses codes with no shipped table rather
        /// than clearing the table and rendering every screen as raw keys.
        /// </summary>
        public void ApplyLanguage(string languageCode)
        {
            if (!LocalizationJsonLoader.IsSupported(languageCode))
            {
                Debug.LogError($"[PreferencesService] No shipped language table for '{languageCode}'. " +
                               "Keeping the current language.");
                return;
            }

            if (string.Equals(languageCode, LocalizedStrings.ActiveLanguageCode, StringComparison.OrdinalIgnoreCase))
                return;

            LocalizationJsonLoader.LoadLanguage(languageCode);
            _prefs.languageCode = languageCode;
            Save();
        }

        /// <summary>
        /// Restores every shipped default and applies them. Bindings, audio, display and language all go
        /// back at once — a partial reset is the kind of thing a player has to press twice to understand.
        /// </summary>
        public void ResetAllToDefaults()
        {
            _prefs = PlayerPreferencesData.CreateDefaults();
            ApplyAll();
            ApplyLanguage(_prefs.languageCode);
            Save();
            Debug.Log("[PreferencesService] All preferences restored to standard issue.");
        }

        private void Save() => _save.SavePreferences(_prefs);

        /// <summary>
        /// The distinct resolutions this display supports, widest first, deduplicated by size.
        /// Unity reports one entry per refresh rate; a dropdown listing "1920x1080" six times is a bug
        /// report, so the highest refresh rate per size wins.
        /// </summary>
        public static List<Resolution> AvailableResolutions()
        {
            var best = new Dictionary<long, Resolution>();

            foreach (var res in Screen.resolutions)
            {
                long key = ((long)res.width << 32) | (uint)res.height;
                if (!best.TryGetValue(key, out var existing) ||
                    res.refreshRateRatio.value > existing.refreshRateRatio.value)
                {
                    best[key] = res;
                }
            }

            var list = new List<Resolution>(best.Values);
            list.Sort((a, b) =>
            {
                int byWidth = b.width.CompareTo(a.width);
                return byWidth != 0 ? byWidth : b.height.CompareTo(a.height);
            });
            return list;
        }
    }
}
