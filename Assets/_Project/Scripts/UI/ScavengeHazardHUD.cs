// Assets/_Project/Scripts/UI/ScavengeHazardHUD.cs
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;

namespace OblastZero.UI
{
    /// <summary>
    /// The Phase A hazard readout: what is following you, what is being written down, what the room is
    /// doing to time, and what the Editor has just taken. Self-building canvas in the house style — add
    /// the component and it constructs itself.
    ///
    /// <para><b>A separate component from <see cref="ScavengeHUD"/> on purpose.</b> That class owns the
    /// clock, the pack and the interaction prompt: state that exists on every run. These read-outs are
    /// exceptional by construction — most runs show none of them — and they subscribe to a different
    /// set of events with a different lifetime. Keeping them apart means the hazard layer can be added
    /// to a scene, or left out of one, without touching the HUD every level has.</para>
    ///
    /// <para><b>Register: the tone is deliberate.</b> CLAUDE.md §9 — the Oblast does not raise its
    /// voice, it files a form. So a stalker on your heels reads "SUBJECT UNDER OBSERVATION", not a red
    /// alert, and being written down reads as a form being completed, because from the oblast's side
    /// that is all that is happening. The only element that is allowed to be loud is the glitch, and
    /// that is not the interface shouting — it is the interface being edited.</para>
    /// </summary>
    public class ScavengeHazardHUD : MonoBehaviour
    {
        [Tooltip("Seconds a completed-registration notice stays up.")]
        [SerializeField] private float registrationNoticeSeconds = 4f;

        [Tooltip("Seconds an Editor edit notice stays up.")]
        [SerializeField] private float editNoticeSeconds = 3.5f;

        [Tooltip("Seconds of scrambled text after the Editor touches the pack.")]
        [SerializeField] private float glitchSeconds = 0.9f;

        private TextMeshProUGUI _pursuit;
        private TextMeshProUGUI _registrationLabel;
        private RectTransform _registrationTrack;
        private Image _registrationFill;
        private TextMeshProUGUI _tally;
        private TextMeshProUGUI _dilation;
        private TextMeshProUGUI _prompt;
        private TextMeshProUGUI _notice;

        private float _noticeHideAt = -1f;
        private float _glitchUntil = -1f;
        private string _noticeText = string.Empty;
        private float _registrationProgress;
        private bool _registering;

        private static readonly char[] _glitchAlphabet =
            "▒▓█/\\|—_=+#*·:.".ToCharArray();

        private void Awake() => BuildUI();

        private void OnEnable()
        {
            EventBus.Subscribe<CensusTakerPursuitEvent>(OnPursuit);
            EventBus.Subscribe<RegistrationProgressEvent>(OnRegistrationProgress);
            EventBus.Subscribe<PlayerRegisteredEvent>(OnRegistered);
            EventBus.Subscribe<EditorEditEvent>(OnEditorEdit);
            EventBus.Subscribe<EditorSightingEvent>(OnEditorSighting);
            EventBus.Subscribe<BacklogStateChangedEvent>(OnBacklog);
            EventBus.Subscribe<AnomalyPromptEvent>(OnAnomalyPrompt);
            EventBus.Subscribe<AnomalyRewardEvent>(OnAnomalyReward);

            SetPursuit(false, 0f);
            SetRegistration(false, 0f);
            SetDilation(false, 1f);
            SetPrompt(false, string.Empty);
            RefreshTally();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CensusTakerPursuitEvent>(OnPursuit);
            EventBus.Unsubscribe<RegistrationProgressEvent>(OnRegistrationProgress);
            EventBus.Unsubscribe<PlayerRegisteredEvent>(OnRegistered);
            EventBus.Unsubscribe<EditorEditEvent>(OnEditorEdit);
            EventBus.Unsubscribe<EditorSightingEvent>(OnEditorSighting);
            EventBus.Unsubscribe<BacklogStateChangedEvent>(OnBacklog);
            EventBus.Unsubscribe<AnomalyPromptEvent>(OnAnomalyPrompt);
            EventBus.Unsubscribe<AnomalyRewardEvent>(OnAnomalyReward);
        }

        private void Update()
        {
            if (_noticeHideAt > 0f && Time.time >= _noticeHideAt)
            {
                _noticeHideAt = -1f;
                _notice.gameObject.SetActive(false);
            }

            // The glitch is redrawn per frame rather than animated, because a scramble that resolves
            // smoothly reads as a transition effect. This has to read as text being overwritten.
            if (_glitchUntil > 0f && _notice.gameObject.activeSelf)
            {
                if (Time.time < _glitchUntil) _notice.text = Scramble(_noticeText);
                else { _glitchUntil = -1f; _notice.text = _noticeText; }
            }

            if (_registering) _registrationFill.rectTransform.sizeDelta =
                new Vector2(_registrationTrack.sizeDelta.x * Mathf.Clamp01(_registrationProgress),
                            _registrationFill.rectTransform.sizeDelta.y);
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnPursuit(CensusTakerPursuitEvent e) => SetPursuit(e.Pursuing, e.DistanceMetres);

        private void OnRegistrationProgress(RegistrationProgressEvent e)
        {
            _registrationProgress = e.Progress01;
            SetRegistration(!e.Interrupted && e.Progress01 < 1f, e.Progress01);
        }

        private void OnRegistered(PlayerRegisteredEvent e)
        {
            SetRegistration(false, 0f);
            RefreshTally();
            ShowNotice($"ENTRY COMPLETED. SUBJECT REGISTERED (x{e.TotalRegistrations}).", glitch: false);
        }

        private void OnEditorEdit(EditorEditEvent e)
        {
            string line;
            switch (e.Stage)
            {
                case "redacted": line = "MANIFEST AMENDED. ONE LINE ITEM REDACTED."; break;
                case "deleted": line = "MANIFEST AMENDED. ONE LINE ITEM STRUCK."; break;
                case "replaced": line = "MANIFEST AMENDED. ONE LINE ITEM SUBSTITUTED."; break;
                default: line = "MANIFEST AMENDED."; break;
            }
            ShowNotice(line, glitch: true);
        }

        private void OnEditorSighting(EditorSightingEvent e)
        {
            // No text for a sighting. The bible's Editor is not announced — the player is supposed to
            // work out on their own that looking at it is what costs them. A warning label would do
            // that work for them and delete the discovery.
            if (e.InSight && e.ExposureSeconds <= 0.1f) _glitchUntil = Time.time + 0.25f;
        }

        private void OnBacklog(BacklogStateChangedEvent e) => SetDilation(e.Inside, e.DilationFactor);

        private void OnAnomalyPrompt(AnomalyPromptEvent e) => SetPrompt(e.Show, e.Text);

        private void OnAnomalyReward(AnomalyRewardEvent e)
            => ShowNotice("FILED TO STORES: " + e.ItemDataId.ToUpperInvariant(), glitch: false);

        // ── Presentation ─────────────────────────────────────────────────────

        private void SetPursuit(bool pursuing, float distance)
        {
            _pursuit.gameObject.SetActive(pursuing);
            if (pursuing) _pursuit.text = $"SUBJECT UNDER OBSERVATION  ·  {distance:0} m";
        }

        private void SetRegistration(bool active, float progress01)
        {
            _registering = active;
            _registrationLabel.gameObject.SetActive(active);
            _registrationTrack.gameObject.SetActive(active);
            if (!active) return;

            _registrationLabel.text = "ENTRY IN PROGRESS  ·  MOVE";
            _registrationFill.rectTransform.sizeDelta =
                new Vector2(_registrationTrack.sizeDelta.x * Mathf.Clamp01(progress01),
                            _registrationFill.rectTransform.sizeDelta.y);
        }

        private void RefreshTally()
        {
            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            int count = run != null ? run.registrationCount : 0;

            _tally.gameObject.SetActive(count > 0);
            if (count > 0) _tally.text = $"REGISTERED x{count}";
        }

        private void SetDilation(bool inside, float factor)
        {
            _dilation.gameObject.SetActive(inside);
            if (!inside) return;

            // Stating the clock's indifference outright. The player can read the countdown and see it
            // is not slowing, but under time pressure they will not, and the trap is only fair if the
            // interface says so plainly at least once.
            _dilation.text = $"LOCAL RATE {factor:P0} OF STANDARD  ·  EMISSION SCHEDULE UNCHANGED";
        }

        private void SetPrompt(bool show, string text)
        {
            _prompt.gameObject.SetActive(show);
            if (show) _prompt.text = text.ToUpperInvariant() + "   [" + InteractKeyCap() + "]";
        }

        private void ShowNotice(string text, bool glitch)
        {
            _noticeText = text;
            _notice.text = text;
            _notice.gameObject.SetActive(true);
            _noticeHideAt = Time.time + (glitch ? editNoticeSeconds : registrationNoticeSeconds);
            if (glitch) _glitchUntil = Time.time + glitchSeconds;
        }

        /// <summary>
        /// Replaces roughly half the non-space characters with block and rule glyphs. Deliberately
        /// partial: fully scrambled text reads as a rendering fault, whereas half-legible text reads
        /// as a document being amended while you look at it, which is the actual thing happening.
        /// </summary>
        private static string Scramble(string source)
        {
            if (string.IsNullOrEmpty(source)) return source;

            var builder = new StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == ' ') { builder.Append(c); continue; }

                // UnityEngine.Random is correct here and nowhere else in this feature: this is a
                // per-frame cosmetic, not a game outcome, so it must not consume the run's RNG stream
                // and desynchronise a seeded replay.
                builder.Append(Random.value < 0.5f
                    ? _glitchAlphabet[Random.Range(0, _glitchAlphabet.Length)]
                    : c);
            }
            return builder.ToString();
        }

        private static string InteractKeyCap()
        {
            var preferences = ServiceLocator.TryGet<Services.PreferencesService>(out var svc) ? svc : null;
            var key = preferences != null
                ? preferences.Current.GetBinding(OblastAction.Interact)
                : InputBindingTable.DefaultFor(OblastAction.Interact);
            return key.ToString().ToUpperInvariant();
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildUI()
        {
            // Below the interview screen (400) and above the base HUD, so a hazard read-out never
            // covers the countdown and never survives on top of a full-screen sequence.
            var root = OblastUI.CreateScreenCanvas(transform, "Canvas", sortingOrder: 120);
            OblastUI.Stretch(root);

            _pursuit = OblastUI.Label(root, "Pursuit", string.Empty, 22f, FontStyles.Bold,
                                      TextAlignmentOptions.Center, OblastUI.Stamp);
            OblastUI.TopCenter(_pursuit.rectTransform, new Vector2(0f, -110f), new Vector2(900f, 30f));

            _registrationLabel = OblastUI.Label(root, "RegistrationLabel", string.Empty, 24f,
                                                FontStyles.Bold, TextAlignmentOptions.Center,
                                                OblastUI.Danger);
            OblastUI.TopCenter(_registrationLabel.rectTransform, new Vector2(0f, -150f),
                               new Vector2(900f, 32f));

            var track = OblastUI.Rect(root, "RegistrationTrack", new Color(1f, 1f, 1f, 0.10f));
            _registrationTrack = track.rectTransform;
            OblastUI.TopCenter(_registrationTrack, new Vector2(0f, -190f), new Vector2(420f, 6f));

            _registrationFill = OblastUI.Rect(track.transform, "Fill", OblastUI.Danger);
            _registrationFill.rectTransform.anchorMin = new Vector2(0f, 0f);
            _registrationFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            _registrationFill.rectTransform.pivot = new Vector2(0f, 0.5f);
            _registrationFill.rectTransform.anchoredPosition = Vector2.zero;
            _registrationFill.rectTransform.sizeDelta = new Vector2(0f, 0f);

            _tally = OblastUI.Label(root, "Tally", string.Empty, 19f, FontStyles.Bold,
                                    TextAlignmentOptions.Right, OblastUI.Danger);
            _tally.rectTransform.anchorMin = new Vector2(1f, 1f);
            _tally.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tally.rectTransform.pivot = new Vector2(1f, 1f);
            _tally.rectTransform.anchoredPosition = new Vector2(-40f, -150f);
            _tally.rectTransform.sizeDelta = new Vector2(320f, 24f);

            _dilation = OblastUI.Label(root, "Dilation", string.Empty, 20f, FontStyles.Bold,
                                       TextAlignmentOptions.Center, OblastUI.Olive);
            OblastUI.BottomCenter(_dilation.rectTransform, new Vector2(0f, 170f), new Vector2(1100f, 28f));

            _prompt = OblastUI.Label(root, "AnomalyPrompt", string.Empty, 24f, FontStyles.Bold,
                                     TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.BottomCenter(_prompt.rectTransform, new Vector2(0f, 220f), new Vector2(900f, 32f));

            _notice = OblastUI.Label(root, "Notice", string.Empty, 21f, FontStyles.Bold,
                                     TextAlignmentOptions.Center, OblastUI.Stamp);
            OblastUI.BottomCenter(_notice.rectTransform, new Vector2(0f, 130f), new Vector2(1100f, 28f));
            _notice.gameObject.SetActive(false);
        }
    }
}
