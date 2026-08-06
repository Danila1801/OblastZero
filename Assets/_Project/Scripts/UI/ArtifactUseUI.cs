// Assets/_Project/Scripts/UI/ArtifactUseUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Gameplay;

namespace OblastZero.UI
{
    /// <summary>
    /// The bunker's artifact register: the four bible artifacts the player holds, what each one does,
    /// and the targeting each one needs. Opened from the bunker HUD, spawned and destroyed by its
    /// opener, so it needs no scene wiring — the same lifecycle the run-flow screens use.
    ///
    /// <para><b>The screen never applies an effect itself.</b> Every button routes to
    /// <see cref="ArtifactSystem"/>, which owns the artifact fields on <c>RunData</c>. UI reads state
    /// and raises intents; that rule is what keeps a use working identically whether it came from this
    /// screen, a debug command, or an event outcome later.</para>
    ///
    /// <para><b>Refusals are shown as reasons, not as greyed buttons.</b> Every artifact stays clickable
    /// and a refused use prints why in the register's own voice — on cooldown, already on file, no
    /// living subject selected. A disabled button tells the player they cannot; it does not tell them
    /// the Margin Note is four days from being fileable again, which is the thing they need in order
    /// to plan around it.</para>
    /// </summary>
    public class ArtifactUseUI : MonoBehaviour
    {
        private ArtifactSystem _artifacts;
        private CrewManager _crew;
        private Action _onClose;

        private RectTransform _listColumn;
        private RectTransform _detailColumn;
        private TextMeshProUGUI _detailTitle;
        private TextMeshProUGUI _detailBody;
        private TextMeshProUGUI _status;
        private RectTransform _targetColumn;

        private string _selected;
        private string _selectedCrewInstanceId;
        private CrewStat _selectedStat = CrewStat.Health;
        private int _finalDraftValue = 50;

        private readonly List<GameObject> _dynamic = new List<GameObject>();

        /// <summary>Spawns the screen. <paramref name="onClose"/> fires once, when it closes itself.</summary>
        public static ArtifactUseUI Open(Action onClose)
        {
            var go = new GameObject("ArtifactUseUI");
            var ui = go.AddComponent<ArtifactUseUI>();
            ui._onClose = onClose;
            return ui;
        }

        private void Awake()
        {
            var gm = GameManager.Instance;
            _artifacts = gm != null ? gm.Artifacts : null;
            _crew = gm != null ? gm.Crew : null;

            BuildChrome();
            RefreshList();
            ShowDetail(null);
        }

        private void OnEnable() => EventBus.Subscribe<ArtifactUsedEvent>(OnArtifactUsed);
        private void OnDisable() => EventBus.Unsubscribe<ArtifactUsedEvent>(OnArtifactUsed);

        private void OnArtifactUsed(ArtifactUsedEvent e)
        {
            // Rebuilt rather than patched: a use can consume the artifact, change a cooldown, or move a
            // bearer, and each of those changes a different row. Rebuilding four rows is cheaper than
            // being wrong about which one moved.
            RefreshList();
            ShowDetail(_selected);
        }

        // ── Chrome ───────────────────────────────────────────────────────────

        private void BuildChrome()
        {
            var root = OblastUI.CreateScreenCanvas(transform, "Canvas", sortingOrder: 320);
            OblastUI.Stretch(root);

            var scrim = OblastUI.Rect(root, "Scrim", new Color(0.02f, 0.02f, 0.024f, 0.92f), raycast: true);
            OblastUI.Stretch(scrim.rectTransform);

            var panel = OblastUI.Rect(root, "Panel", OblastUI.Panel);
            OblastUI.Center(panel.rectTransform, Vector2.zero, new Vector2(1280f, 700f));

            var edge = OblastUI.Rect(panel.transform, "Edge", OblastUI.Hairline);
            OblastUI.StretchTop(edge.rectTransform, 1f);

            var heading = OblastUI.Label(panel.transform, "Heading", "ARTIFACT REGISTER", 30f,
                                         FontStyles.Bold, TextAlignmentOptions.Left, OblastUI.TextPrimary);
            OblastUI.TopLeft(heading.rectTransform, new Vector2(40f, -32f), new Vector2(700f, 36f));

            var subheading = OblastUI.Label(panel.transform, "Subheading",
                                            "Items held under special conditions of use. " +
                                            "Consumption is recorded.",
                                            18f, FontStyles.Italic, TextAlignmentOptions.Left,
                                            OblastUI.TextFaint);
            OblastUI.TopLeft(subheading.rectTransform, new Vector2(40f, -70f), new Vector2(760f, 24f));

            OblastUI.TopLeft(OblastUI.Rule(panel.transform, "Rule", 1200f, OblastUI.Hairline).rectTransform,
                             new Vector2(40f, -104f), new Vector2(1200f, 1f));

            _listColumn = OblastUI.Group(panel.transform, "List");
            OblastUI.TopLeft(_listColumn, new Vector2(40f, -124f), new Vector2(430f, 460f));

            _detailColumn = OblastUI.Group(panel.transform, "Detail");
            OblastUI.TopLeft(_detailColumn, new Vector2(500f, -124f), new Vector2(740f, 460f));

            _detailTitle = OblastUI.Label(_detailColumn, "Title", string.Empty, 26f, FontStyles.Bold,
                                          TextAlignmentOptions.TopLeft, OblastUI.Stamp);
            OblastUI.TopLeft(_detailTitle.rectTransform, Vector2.zero, new Vector2(740f, 32f));

            _detailBody = OblastUI.Label(_detailColumn, "Body", string.Empty, 20f, FontStyles.Normal,
                                         TextAlignmentOptions.TopLeft, OblastUI.TextPrimary);
            _detailBody.enableWordWrapping = true;
            _detailBody.lineSpacing = 10f;
            OblastUI.TopLeft(_detailBody.rectTransform, new Vector2(0f, -44f), new Vector2(740f, 150f));

            _targetColumn = OblastUI.Group(_detailColumn, "Targets");
            OblastUI.TopLeft(_targetColumn, new Vector2(0f, -200f), new Vector2(740f, 240f));

            _status = OblastUI.Label(panel.transform, "Status", string.Empty, 19f, FontStyles.Italic,
                                     TextAlignmentOptions.Left, OblastUI.Olive);
            _status.enableWordWrapping = true;
            OblastUI.BottomLeft(_status.rectTransform, new Vector2(40f, 96f), new Vector2(1000f, 48f));

            TextMeshProUGUI closeLabel;
            var close = OblastUI.Button(panel.transform, "Close", "CLOSE THE REGISTER", 20f, Close,
                                        out closeLabel);
            OblastUI.BottomLeft(close.GetComponent<RectTransform>(), new Vector2(40f, 36f),
                                new Vector2(320f, 48f));
        }

        // ── List ─────────────────────────────────────────────────────────────

        private void RefreshList()
        {
            ClearChildren(_listColumn);
            if (_artifacts == null)
            {
                OblastUI.Label(_listColumn, "None", "No register available.", 20f, FontStyles.Italic,
                               TextAlignmentOptions.TopLeft, OblastUI.TextDim);
                return;
            }

            var held = _artifacts.HeldArtifacts();
            if (held.Count == 0)
            {
                var empty = OblastUI.Label(_listColumn, "None",
                                           "Nothing on the register. Artifacts are recovered from " +
                                           "anomalies and from what the Editor leaves behind.",
                                           19f, FontStyles.Italic, TextAlignmentOptions.TopLeft,
                                           OblastUI.TextDim);
                empty.enableWordWrapping = true;
                OblastUI.TopLeft(empty.rectTransform, Vector2.zero, new Vector2(420f, 90f));
                return;
            }

            for (int i = 0; i < held.Count; i++)
            {
                string id = held[i];
                int count = _artifacts.CountOf(id);
                string label = DisplayNameOf(id) + (count > 1 ? $"   x{count}" : string.Empty);

                TextMeshProUGUI buttonLabel;
                var button = OblastUI.Button(_listColumn, "Row_" + id, label, 20f,
                                             () => { _selected = id; ShowDetail(id); }, out buttonLabel);
                buttonLabel.alignment = TextAlignmentOptions.Left;
                OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                 new Vector2(0f, -i * 62f), new Vector2(420f, 52f));
            }
        }

        // ── Detail ───────────────────────────────────────────────────────────

        private void ShowDetail(string artifactId)
        {
            ClearChildren(_targetColumn);
            _selected = artifactId;

            if (string.IsNullOrEmpty(artifactId))
            {
                _detailTitle.text = string.Empty;
                _detailBody.text = "Select an entry.";
                return;
            }

            _detailTitle.text = DisplayNameOf(artifactId).ToUpperInvariant();
            _detailBody.text = DescriptionOf(artifactId);

            switch (artifactId)
            {
                case ArtifactIds.MarginNote:
                    BuildSimpleUse("FILE THE NOTE", () => _artifacts.UseMarginNote(), artifactId);
                    break;

                case ArtifactIds.StampedTongue:
                    BuildSimpleUse("LODGE THE OVERRIDE", () => _artifacts.UseStampedTongue(), artifactId);
                    break;

                case ArtifactIds.NotarizedHeart:
                    BuildCrewPicker("ASSIGN THE HEART",
                                    () => _artifacts.UseNotarizedHeart(_selectedCrewInstanceId),
                                    artifactId, withStatEditor: false);
                    break;

                case ArtifactIds.FinalDraft:
                    BuildCrewPicker("APPLY THE DRAFT",
                                    () => _artifacts.UseFinalDraft(_selectedCrewInstanceId,
                                                                   _selectedStat, _finalDraftValue),
                                    artifactId, withStatEditor: true);
                    break;
            }
        }

        private void BuildSimpleUse(string verb, Func<ArtifactSystem.UseResult> use, string artifactId)
        {
            TextMeshProUGUI label;
            var button = OblastUI.Button(_targetColumn, "Use", verb, 21f,
                                         () => Apply(use(), artifactId), out label);
            OblastUI.TopLeft(button.GetComponent<RectTransform>(), Vector2.zero, new Vector2(360f, 52f));
            ShowPrecondition(artifactId);
        }

        private void BuildCrewPicker(string verb, Func<ArtifactSystem.UseResult> use, string artifactId,
                                     bool withStatEditor)
        {
            var roster = _crew != null ? _crew.AllAlive() : new List<CrewInstance>();
            if (roster.Count == 0)
            {
                OblastUI.Label(_targetColumn, "NoCrew", "No living subject on strength.", 19f,
                               FontStyles.Italic, TextAlignmentOptions.TopLeft, OblastUI.Danger);
                return;
            }

            if (string.IsNullOrEmpty(_selectedCrewInstanceId) ||
                _crew.GetMember(_selectedCrewInstanceId) == null)
                _selectedCrewInstanceId = roster[0].instanceId;

            var heading = OblastUI.Label(_targetColumn, "SubjectHeading", "SUBJECT", 17f, FontStyles.Bold,
                                         TextAlignmentOptions.TopLeft, OblastUI.TextFaint);
            OblastUI.TopLeft(heading.rectTransform, Vector2.zero, new Vector2(300f, 20f));

            for (int i = 0; i < roster.Count && i < 4; i++)
            {
                var member = roster[i];
                bool chosen = member.instanceId == _selectedCrewInstanceId;

                TextMeshProUGUI label;
                var button = OblastUI.Button(_targetColumn, "Crew_" + member.instanceId,
                                             (chosen ? "> " : "  ") + CrewLabel(member), 18f,
                                             () =>
                                             {
                                                 _selectedCrewInstanceId = member.instanceId;
                                                 ShowDetail(_selected);
                                             }, out label);
                label.alignment = TextAlignmentOptions.Left;
                label.color = chosen ? OblastUI.Stamp : OblastUI.TextPrimary;
                OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                 new Vector2(0f, -26f - i * 44f), new Vector2(420f, 38f));
            }

            float y = -26f - Mathf.Min(roster.Count, 4) * 44f - 12f;

            if (withStatEditor)
            {
                var statHeading = OblastUI.Label(_targetColumn, "StatHeading",
                                                 $"ENTRY: {_selectedStat.ToString().ToUpperInvariant()}" +
                                                 $"   VALUE: {_finalDraftValue}",
                                                 17f, FontStyles.Bold, TextAlignmentOptions.TopLeft,
                                                 OblastUI.TextFaint);
                OblastUI.TopLeft(statHeading.rectTransform, new Vector2(0f, y), new Vector2(420f, 20f));
                y -= 28f;

                var stats = new[] { CrewStat.Health, CrewStat.Sanity, CrewStat.Fatigue, CrewStat.Radiation };
                for (int i = 0; i < stats.Length; i++)
                {
                    var stat = stats[i];
                    TextMeshProUGUI label;
                    var button = OblastUI.Button(_targetColumn, "Stat_" + stat,
                                                 stat.ToString().ToUpperInvariant(), 16f,
                                                 () => { _selectedStat = stat; ShowDetail(_selected); },
                                                 out label);
                    label.color = stat == _selectedStat ? OblastUI.Stamp : OblastUI.TextPrimary;
                    OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                     new Vector2(i * 108f, y), new Vector2(100f, 36f));
                }
                y -= 46f;

                // Stepped rather than a slider or a text field: the value is clamped to a narrow band
                // anyway, a slider needs drag handling this screen has no other use for, and a text
                // field needs validation and an on-screen keyboard on pad.
                int[] steps = { -25, -5, 5, 25 };
                for (int i = 0; i < steps.Length; i++)
                {
                    int delta = steps[i];
                    TextMeshProUGUI label;
                    var button = OblastUI.Button(_targetColumn, "Step_" + delta,
                                                 (delta > 0 ? "+" : string.Empty) + delta, 16f,
                                                 () =>
                                                 {
                                                     _finalDraftValue = Mathf.Clamp(
                                                         _finalDraftValue + delta,
                                                         BalanceConstants.FINAL_DRAFT_MIN_STAT_VALUE,
                                                         BalanceConstants.FINAL_DRAFT_MAX_STAT_VALUE);
                                                     ShowDetail(_selected);
                                                 }, out label);
                    OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                     new Vector2(i * 90f, y), new Vector2(82f, 36f));
                }
                y -= 50f;
            }

            TextMeshProUGUI useLabel;
            var useButton = OblastUI.Button(_targetColumn, "Use", verb, 21f,
                                            () => Apply(use(), artifactId), out useLabel);
            OblastUI.TopLeft(useButton.GetComponent<RectTransform>(), new Vector2(0f, y),
                             new Vector2(360f, 50f));

            ShowPrecondition(artifactId);
        }

        private void ShowPrecondition(string artifactId)
        {
            var check = _artifacts.CanUse(artifactId, _selectedCrewInstanceId);
            _status.text = check == ArtifactSystem.UseResult.Success
                ? string.Empty
                : Explain(check, artifactId);
            _status.color = check == ArtifactSystem.UseResult.Success ? OblastUI.Olive : OblastUI.TextDim;
        }

        private void Apply(ArtifactSystem.UseResult result, string artifactId)
        {
            if (result == ArtifactSystem.UseResult.Success)
            {
                // The ArtifactUsedEvent handler rebuilds the screen and ArtifactSystem has already
                // written the summary to the log, so nothing more is needed here.
                return;
            }

            _status.text = Explain(result, artifactId);
            _status.color = OblastUI.Danger;
        }

        private string Explain(ArtifactSystem.UseResult result, string artifactId)
        {
            switch (result)
            {
                case ArtifactSystem.UseResult.NoRun:
                    return "No expedition on record.";
                case ArtifactSystem.UseResult.NotHeld:
                    return "Not on the register.";
                case ArtifactSystem.UseResult.OnCooldown:
                    return $"A note was filed within the last {BalanceConstants.MARGIN_NOTE_COOLDOWN_DAYS} " +
                           $"days. {_artifacts.MarginNoteDaysRemaining()} day(s) until the next may be filed.";
                case ArtifactSystem.UseResult.AlreadyArmed:
                    return artifactId == ArtifactIds.StampedTongue
                        ? "An override is already on file. It has not yet been exercised."
                        : "A note is already filed against the next matter.";
                case ArtifactSystem.UseResult.InvalidTarget:
                    return "Select a living subject.";
                default:
                    return "Refused. No reason recorded.";
            }
        }

        // ── Copy ─────────────────────────────────────────────────────────────

        private static string DisplayNameOf(string artifactId)
        {
            switch (artifactId)
            {
                case ArtifactIds.MarginNote: return "Margin Note";
                case ArtifactIds.NotarizedHeart: return "Notarized Heart";
                case ArtifactIds.StampedTongue: return "Stamped Tongue";
                case ArtifactIds.FinalDraft: return "Final Draft";
                default: return artifactId;
            }
        }

        private string DescriptionOf(string artifactId)
        {
            switch (artifactId)
            {
                case ArtifactIds.MarginNote:
                    int wait = _artifacts != null ? _artifacts.MarginNoteDaysRemaining() : 0;
                    return "Recovered from an undisturbed Carbon Copy. Filed against the next matter, " +
                           "which is then reviewed twice and settled on the better reading.\n\n" +
                           $"One per {BalanceConstants.MARGIN_NOTE_COOLDOWN_DAYS} days. Not consumed." +
                           (wait > 0 ? $"\nNext filing available in {wait} day(s)." : "\nAvailable now.");

                case ArtifactIds.NotarizedHeart:
                    return "Recovered from the Interview. Worn by one member of the crew, who thereafter " +
                           "accumulates personal contamination at " +
                           $"{BalanceConstants.NOTARIZED_HEART_RADIATION_MULTIPLIER:P0} of the standard " +
                           "rate.\n\nMay be reassigned at any time. Not consumed.";

                case ArtifactIds.StampedTongue:
                    return "Recovered from the Interview. An override lodged in advance: the next matter " +
                           "before the Scale Society is settled in your favour regardless of its merits." +
                           "\n\nSpent when it is exercised, not when it is lodged — an override that " +
                           "never comes up is still on the register tomorrow.";

                case ArtifactIds.FinalDraft:
                    return "The sheet that was the Editor's face. One entry on one personnel record is " +
                           "rewritten to a value of your choosing, within establishment limits " +
                           $"({BalanceConstants.FINAL_DRAFT_MIN_STAT_VALUE}" +
                           $"-{BalanceConstants.FINAL_DRAFT_MAX_STAT_VALUE}).\n\nConsumed on use.";

                default:
                    return string.Empty;
            }
        }

        private static string CrewLabel(CrewInstance member)
        {
            return $"{member.instanceId}   HP {member.currentHealth}  SAN {member.currentSanity}  " +
                   $"RAD {member.currentRadiation}";
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
                Destroy(parent.GetChild(i).gameObject);
        }

        private void Close()
        {
            var callback = _onClose;
            _onClose = null;
            Destroy(gameObject);
            if (callback != null) callback();
        }
    }
}
