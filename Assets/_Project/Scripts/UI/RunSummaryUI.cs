// Assets/_Project/Scripts/UI/RunSummaryUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.UI
{
    /// <summary>
    /// The end-of-run screen. Reads as a closed case file rather than a score screen: the Oblast does not
    /// congratulate or commiserate, it records the outcome and files it.
    ///
    /// Presentation only — it is handed a <see cref="RunSummary"/> snapshot and renders it. It does not
    /// read RunData, does not apply salvage, and does not end the run; by the time this screen appears the
    /// state has already called EndCurrentRun. Raises <see cref="AcknowledgeRequested"/> when the player
    /// closes the file.
    /// </summary>
    public class RunSummaryUI : MonoBehaviour
    {
        /// <summary>Raised by the "Return to Menu" button.</summary>
        public event Action AcknowledgeRequested;

        private RectTransform _root;
        private RectTransform _statColumn;
        private RectTransform _repColumn;
        private TextMeshProUGUI _verdict;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _closingLine;

        private void Awake() => BuildChrome();

        /// <summary>Renders one run's outcome. Safe to call once, after the component exists.</summary>
        public void Present(RunSummary summary)
        {
            if (summary == null)
            {
                Debug.LogError("[RunSummaryUI] Present called with a null summary.");
                return;
            }

            _verdict.text = summary.Headline;
            _verdict.color = summary.Survived ? OblastUI.Olive : OblastUI.Danger;
            _caption.text = summary.Subheadline;

            BuildStatRows(summary);
            BuildReputationRows(summary);

            _closingLine.text = summary.ClosingLine;

            Debug.Log($"[RunSummaryUI] Presented summary: {summary.Headline} " +
                      $"(day {summary.DaysSurvived}, {summary.CrewLost} lost, {summary.ItemsRecovered} recovered).");
        }

        // ── Rows ─────────────────────────────────────────────────────────────

        private void BuildStatRows(RunSummary summary)
        {
            float y = 0f;
            AddRow(_statColumn, "SITE", summary.SiteName, ref y);
            AddRow(_statColumn, "DAYS ON RECORD", summary.DaysSurvived.ToString(), ref y);
            AddRow(_statColumn, "PERSONNEL LOST", summary.CrewLost.ToString(), ref y);
            AddRow(_statColumn, "PERSONNEL REMAINING", summary.CrewRemaining.ToString(), ref y);
            AddRow(_statColumn, "LINE ITEMS RECOVERED", summary.ItemsRecovered.ToString(), ref y);
            AddRow(_statColumn, $"SALVAGE APPLIED ({summary.SalvageRatePercent}%)",
                   summary.ItemsSalvaged.ToString(), ref y);
        }

        private void BuildReputationRows(RunSummary summary)
        {
            float y = 0f;
            foreach (var entry in summary.Reputations)
                AddRow(_repColumn, entry.Key.ToUpperInvariant(), FormatRep(entry.Value), ref y);

            AddRule(_repColumn, ref y);
            AddRow(_repColumn, "REGISTRATIONS FILED", summary.TotalRunsAttempted.ToString(), ref y);
            AddRow(_repColumn, "RETURNED INTACT", summary.TotalRunsSurvived.ToString(), ref y);
        }

        private static void AddRow(Transform parent, string label, string value, ref float y)
        {
            const float rowHeight = 42f;
            float width = ((RectTransform)parent).sizeDelta.x;

            var row = OblastUI.Group(parent, $"Row_{label}");
            OblastUI.TopLeft(row, new Vector2(0f, -y), new Vector2(width, rowHeight));

            var key = OblastUI.Label(row, "Key", label, 21f, FontStyles.Normal,
                                     TextAlignmentOptions.Left, OblastUI.TextDim);
            OblastUI.TopLeft(key.rectTransform, new Vector2(0f, -8f), new Vector2(width - 190f, 28f));
            key.characterSpacing = 3f;

            var val = OblastUI.Label(row, "Value", value, 23f, FontStyles.Bold,
                                     TextAlignmentOptions.Right, OblastUI.TextPrimary);
            OblastUI.TopLeft(val.rectTransform, new Vector2(width - 320f, -8f), new Vector2(320f, 28f));

            var underline = OblastUI.Rect(row, "Underline", new Color(1f, 1f, 1f, 0.05f));
            OblastUI.BottomLeft(underline.rectTransform, Vector2.zero, new Vector2(width, 1f));

            y += rowHeight;
        }

        private static void AddRule(Transform parent, ref float y)
        {
            float width = ((RectTransform)parent).sizeDelta.x;
            var rule = OblastUI.Rect(parent, "Divider", OblastUI.Hairline);
            OblastUI.TopLeft(rule.rectTransform, new Vector2(0f, -(y + 12f)), new Vector2(width, 1f));
            y += 32f;
        }

        private static string FormatRep(int value)
        {
            string sign = value > 0 ? "+" : string.Empty;
            string band = value >= 50 ? "TRUSTED"
                        : value >= 15 ? "COOPERATIVE"
                        : value > -15 ? "NEUTRAL"
                        : value > -50 ? "OBSTRUCTIVE"
                        : "HOSTILE";
            return $"{sign}{value}  <color=#7A7873><size=17>{band}</size></color>";
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildChrome()
        {
            _root = OblastUI.CreateScreenCanvas(transform, "RunSummary_Canvas", 100);

            var bg = OblastUI.Rect(_root, "Background", OblastUI.Background, raycast: true);
            OblastUI.Stretch(bg.rectTransform);

            var stampBand = OblastUI.Rect(_root, "StampBand", OblastUI.Panel);
            OblastUI.StretchBand(stampBand.rectTransform, 70f, 168f, 200f);

            var bandEdge = OblastUI.Rect(stampBand.transform, "Edge", OblastUI.Hairline);
            OblastUI.StretchTop(bandEdge.rectTransform, 1f);

            _verdict = OblastUI.Label(_root, "Verdict", "CASE FILED", 78f, FontStyles.Bold,
                                      TextAlignmentOptions.Center, OblastUI.Danger);
            OblastUI.StretchBand(_verdict.rectTransform, 96f, 90f);
            _verdict.characterSpacing = 12f;

            _caption = OblastUI.Label(_root, "Caption", string.Empty, 22f, FontStyles.Normal,
                                      TextAlignmentOptions.Center, OblastUI.TextDim);
            OblastUI.StretchBand(_caption.rectTransform, 186f, 30f);
            _caption.characterSpacing = 4f;

            // ── Two record columns ───────────────────────────────────────────
            var leftHeading = OblastUI.Label(_root, "LeftHeading", "EXPEDITION RECORD", 24f, FontStyles.Bold,
                                             TextAlignmentOptions.TopLeft, OblastUI.Stamp);
            OblastUI.TopLeft(leftHeading.rectTransform, new Vector2(240f, -292f), new Vector2(640f, 30f));
            leftHeading.characterSpacing = 5f;

            var rightHeading = OblastUI.Label(_root, "RightHeading", "STANDING WITH FACTIONS", 24f, FontStyles.Bold,
                                              TextAlignmentOptions.TopLeft, OblastUI.Stamp);
            OblastUI.TopLeft(rightHeading.rectTransform, new Vector2(1040f, -292f), new Vector2(640f, 30f));
            rightHeading.characterSpacing = 5f;

            _statColumn = OblastUI.Group(_root, "StatColumn");
            OblastUI.TopLeft(_statColumn, new Vector2(240f, -340f), new Vector2(640f, 400f));

            _repColumn = OblastUI.Group(_root, "RepColumn");
            OblastUI.TopLeft(_repColumn, new Vector2(1040f, -340f), new Vector2(640f, 400f));

            // ── Footer ───────────────────────────────────────────────────────
            var footRule = OblastUI.Rule(_root, "FooterRule", 1440f, OblastUI.Hairline);
            OblastUI.BottomCenter(footRule.rectTransform, new Vector2(0f, 208f), new Vector2(1440f, 1f));

            _closingLine = OblastUI.Label(_root, "ClosingLine", string.Empty, 21f, FontStyles.Italic,
                                          TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.BottomCenter(_closingLine.rectTransform, new Vector2(0f, 152f), new Vector2(1440f, 44f));

            TextMeshProUGUI ackLabel;
            var ack = OblastUI.Button(_root, "AcknowledgeButton", "RETURN TO MAIN MENU", 24f,
                                      () => AcknowledgeRequested?.Invoke(), out ackLabel);
            OblastUI.BottomCenter(ack.GetComponent<RectTransform>(), new Vector2(0f, 62f), new Vector2(440f, 74f));
            ackLabel.characterSpacing = 5f;

            Debug.Log("[RunSummaryUI] Summary screen built.");
        }
    }
}
