// Assets/_Project/Scripts/UI/InterviewSequenceUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;

namespace OblastZero.UI
{
    /// <summary>
    /// The screen the Interview anomaly (ANM-Ψ-12/IV) plays behind a black fade while the emission clock is
    /// held. Self-building canvas in the house style: add the component and it constructs itself, exactly
    /// like the other screens, so the scavenge scene needs no wiring for it.
    ///
    /// <para><b>The questions are hardcoded, and that is the design.</b> Everything else narrative in this
    /// game is data-driven JSON, because there are a thousand bunker events and they must be mass-authored.
    /// The Interview is one fixed scripted sequence with a fork at the end — the bible describes a specific
    /// escalation from mundane to not-mundane, in a specific order, where question four only lands because
    /// question three was boring. Routing that through the weighted event pool would let it arrive shuffled,
    /// which is the one thing it cannot survive.</para>
    ///
    /// <para><b>Leaving is always available and always free.</b> The bible: "Refusing to sit or leaving
    /// during the interview = safe but forfeits reward." So the walk-out button is present on every
    /// question, never disabled, and never punished. The pressure is the clock the player knows is frozen
    /// but does not entirely trust, not a trap door.</para>
    /// </summary>
    public class InterviewSequenceUI : MonoBehaviour
    {
        /// <summary>How the player left the interview. Drives what the anomaly awards.</summary>
        public enum Outcome
        {
            /// <summary>Walked out before the last question. No reward, no penalty.</summary>
            Abandoned,

            /// <summary>Answered every question and consented to the correction of the file.</summary>
            CompletedConsented,

            /// <summary>Answered every question and declined the correction.</summary>
            CompletedDeclined
        }

        private sealed class Question
        {
            public string Heading;
            public string Body;
            public string PrimaryAnswer;
            public string SecondaryAnswer;
        }

        /// <summary>
        /// Six questions. The first three are a real personnel form; the last three are the same form
        /// having read it. Voice per CLAUDE.md §9 — the Oblast does not raise its voice, it files.
        /// </summary>
        private static readonly List<Question> _questions = new List<Question>
        {
            new Question
            {
                Heading = "ITEM 1 — IDENTIFICATION",
                Body = "State your full name, including patronymic, as it appears on your issued documents.",
                PrimaryAnswer = "State it",
                SecondaryAnswer = "Give the name on the older document"
            },
            new Question
            {
                Heading = "ITEM 2 — SERVICE NUMBER",
                Body = "Service number. If none was issued, give the number of the form on which you were " +
                       "last recorded.",
                PrimaryAnswer = "Recite the number",
                SecondaryAnswer = "Say you were never issued one"
            },
            new Question
            {
                Heading = "ITEM 3 — PRIOR EMPLOYMENT",
                Body = "List all positions held in the last six years. Omissions are treated as deviations " +
                       "and are corrected without notice.",
                PrimaryAnswer = "List six years",
                SecondaryAnswer = "List everything"
            },
            new Question
            {
                Heading = "ITEM 4 — RECONCILIATION",
                Body = "You have accounted for six years. The file in front of the interviewer accounts for " +
                       "eleven. The intervening five are already filled in, in handwriting resembling your " +
                       "own. Confirm or dispute the entry.",
                PrimaryAnswer = "Confirm the entry",
                SecondaryAnswer = "Dispute the entry"
            },
            new Question
            {
                Heading = "ITEM 5 — NEXT OF KIN",
                Body = "The box marked NEXT OF KIN is not blank. The name in it also appears on a requisition " +
                       "filed from this office in a year in which this office was sealed. State whether this " +
                       "is the same person.",
                PrimaryAnswer = "The same person",
                SecondaryAnswer = "Decline to state"
            },
            new Question
            {
                Heading = "ITEM 6 — CONSENT TO CORRECTION",
                Body = "The interviewer has completed the form. One entry remains at variance with the " +
                       "record. Correction requires the subject's consent. Consent is not required for the " +
                       "correction to be applied; it is required for the correction to be filed as routine.",
                PrimaryAnswer = "Consent to correction",
                SecondaryAnswer = "Withhold consent"
            }
        };

        /// <summary>Number of questions in the sequence. Read by the anomaly for its progress log.</summary>
        public static int QuestionCount { get { return _questions.Count; } }

        private Action<Outcome> _onFinished;
        private CanvasGroup _fade;
        private TextMeshProUGUI _heading;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _progress;
        private Button _primary;
        private TextMeshProUGUI _primaryLabel;
        private Button _secondary;
        private TextMeshProUGUI _secondaryLabel;

        private int _index;
        private bool _finished;
        private float _fadeInSeconds = BalanceConstants.INTERVIEW_FADE_SECONDS;
        private float _elapsed;

        /// <summary>
        /// Spawns the screen. <paramref name="onFinished"/> fires exactly once, whichever way it ends, and
        /// the screen destroys itself immediately afterwards — the caller must not hold the reference past
        /// the callback.
        /// </summary>
        public static InterviewSequenceUI Open(Action<Outcome> onFinished)
        {
            var go = new GameObject("InterviewSequenceUI");
            var ui = go.AddComponent<InterviewSequenceUI>();
            ui._onFinished = onFinished;
            return ui;
        }

        private void Awake()
        {
            BuildUI();
            ShowQuestion(0);
        }

        private void Update()
        {
            // The fade is driven here rather than by a coroutine so it cannot be left half-finished by a
            // scene unload mid-transition — a stuck 60%-opaque black plate over the Blowout is a far worse
            // failure than a fade that snaps.
            if (_fade == null) return;
            _elapsed += Time.unscaledDeltaTime;
            _fade.alpha = _fadeInSeconds <= 0f ? 1f : Mathf.Clamp01(_elapsed / _fadeInSeconds);
        }

        private void BuildUI()
        {
            // Above the scavenge HUD: the interview replaces the world, it does not overlay it.
            var root = OblastUI.CreateScreenCanvas(transform, "Canvas", sortingOrder: 400);
            OblastUI.Stretch(root);

            _fade = root.gameObject.AddComponent<CanvasGroup>();
            _fade.alpha = 0f;

            var plate = OblastUI.Rect(root, "Plate", new Color(0.02f, 0.02f, 0.024f, 1f), raycast: true);
            OblastUI.Stretch(plate.rectTransform);

            var panel = OblastUI.Rect(root, "Panel", OblastUI.Panel);
            OblastUI.Center(panel.rectTransform, new Vector2(0f, 40f), new Vector2(1040f, 520f));

            var edge = OblastUI.Rect(panel.transform, "Edge", OblastUI.Hairline);
            OblastUI.StretchTop(edge.rectTransform, 1f);

            var stamp = OblastUI.Label(panel.transform, "FormCode", "FORM 12-IV / SUBJECT INTERVIEW",
                                       20f, FontStyles.Bold, TextAlignmentOptions.Left, OblastUI.Stamp);
            OblastUI.TopLeft(stamp.rectTransform, new Vector2(36f, -28f), new Vector2(600f, 26f));

            _progress = OblastUI.Label(panel.transform, "Progress", string.Empty, 18f, FontStyles.Normal,
                                       TextAlignmentOptions.Right, OblastUI.TextFaint);
            _progress.rectTransform.anchorMin = new Vector2(1f, 1f);
            _progress.rectTransform.anchorMax = new Vector2(1f, 1f);
            _progress.rectTransform.pivot = new Vector2(1f, 1f);
            _progress.rectTransform.anchoredPosition = new Vector2(-36f, -28f);
            _progress.rectTransform.sizeDelta = new Vector2(300f, 26f);

            _heading = OblastUI.Label(panel.transform, "Heading", string.Empty, 30f, FontStyles.Bold,
                                      TextAlignmentOptions.TopLeft, OblastUI.TextPrimary);
            OblastUI.TopLeft(_heading.rectTransform, new Vector2(36f, -78f), new Vector2(960f, 40f));

            var rule = OblastUI.Rule(panel.transform, "Rule", 968f, OblastUI.Hairline);
            OblastUI.TopLeft(rule.rectTransform, new Vector2(36f, -128f), new Vector2(968f, 1f));

            _body = OblastUI.Label(panel.transform, "Body", string.Empty, 24f, FontStyles.Normal,
                                   TextAlignmentOptions.TopLeft, OblastUI.TextPrimary);
            _body.enableWordWrapping = true;
            _body.lineSpacing = 12f;
            OblastUI.TopLeft(_body.rectTransform, new Vector2(36f, -152f), new Vector2(968f, 190f));

            _primary = OblastUI.Button(panel.transform, "Primary", string.Empty, 22f,
                                       () => Answer(primary: true), out _primaryLabel);
            OblastUI.BottomLeft(_primary.GetComponent<RectTransform>(),
                                new Vector2(36f, 108f), new Vector2(472f, 56f));

            _secondary = OblastUI.Button(panel.transform, "Secondary", string.Empty, 22f,
                                         () => Answer(primary: false), out _secondaryLabel);
            OblastUI.BottomLeft(_secondary.GetComponent<RectTransform>(),
                                new Vector2(532f, 108f), new Vector2(472f, 56f));

            TextMeshProUGUI leaveLabel;
            var leave = OblastUI.Button(panel.transform, "Leave", "Stand up and leave the room", 20f,
                                        () => Finish(Outcome.Abandoned), out leaveLabel);
            leaveLabel.color = OblastUI.TextDim;
            OblastUI.BottomLeft(leave.GetComponent<RectTransform>(),
                                new Vector2(36f, 36f), new Vector2(968f, 48f));

            var footer = OblastUI.Label(panel.transform, "Footer",
                                        "The clock outside this room is not running. " +
                                        "This has been confirmed by the interviewer.",
                                        17f, FontStyles.Italic, TextAlignmentOptions.Center,
                                        OblastUI.TextFaint);
            OblastUI.BottomLeft(footer.rectTransform, new Vector2(36f, 4f), new Vector2(968f, 24f));
        }

        private void ShowQuestion(int index)
        {
            _index = index;
            var q = _questions[index];

            _heading.text = q.Heading;
            _body.text = q.Body;
            _progress.text = $"ITEM {index + 1} OF {_questions.Count}";
            _primaryLabel.text = q.PrimaryAnswer;
            _secondaryLabel.text = q.SecondaryAnswer;
        }

        private void Answer(bool primary)
        {
            if (_finished) return;

            bool lastQuestion = _index >= _questions.Count - 1;
            if (!lastQuestion)
            {
                // Which of the two mundane answers was given does not branch the sequence. The bible's
                // Interview is not a puzzle with a correct set of responses; it is a form that fills itself
                // in regardless. Only the final consent changes the outcome.
                ShowQuestion(_index + 1);
                return;
            }

            Finish(primary ? Outcome.CompletedConsented : Outcome.CompletedDeclined);
        }

        private void Finish(Outcome outcome)
        {
            if (_finished) return;
            _finished = true;

            var callback = _onFinished;
            _onFinished = null;

            Destroy(gameObject);
            if (callback != null) callback(outcome);
        }
    }
}
