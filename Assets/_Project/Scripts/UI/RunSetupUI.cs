// Assets/_Project/Scripts/UI/RunSetupUI.cs
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
    /// The pre-run registration screen: pick a scavenge site, pick the operator who walks in, confirm.
    /// Builds its whole canvas on Awake like the other screens; RunSetupState spawns it and listens.
    ///
    /// Presentation only. It never calls BeginNewRun — it raises <see cref="RunConfirmed"/> with the
    /// three values the state needs (site id, lead crew id, seed) and lets the state do the work.
    ///
    /// Populate it with <see cref="Populate"/> before it is useful; the state supplies the crew list from
    /// the GameDatabase and the site list from <see cref="ScavengeSiteCatalog"/>, because the UI layer is
    /// not allowed to go reading content registries on its own.
    /// </summary>
    public class RunSetupUI : MonoBehaviour
    {
        /// <summary>Raised on CONFIRM. Args: siteId, leadCrewDataId, rngSeed.</summary>
        public event Action<string, string, int> RunConfirmed;

        /// <summary>Raised on CANCEL / back.</summary>
        public event Action Cancelled;

        private RectTransform _root;
        private RectTransform _siteColumn;
        private RectTransform _crewColumn;

        private readonly List<SelectableCard> _siteCards = new List<SelectableCard>();
        private readonly List<SelectableCard> _crewCards = new List<SelectableCard>();

        private TextMeshProUGUI _seedLabel;
        private TextMeshProUGUI _validationLabel;
        private TextMeshProUGUI _confirmLabel;
        private Button _confirmButton;

        private string _selectedSiteId;
        private string _selectedCrewId;
        private int _seed;

        private void Awake() => BuildChrome();

        // ── Population ───────────────────────────────────────────────────────

        /// <summary>
        /// Fills the two columns. <paramref name="sites"/> and <paramref name="crew"/> come from the state.
        /// The first selectable site and the first crew member are pre-selected so CONFIRM is reachable
        /// immediately — a registration form that starts invalid is a form nobody finishes.
        /// </summary>
        public void Populate(IReadOnlyList<ScavengeSite> sites, IReadOnlyList<CrewMemberData> crew, int seed)
        {
            _seed = seed;
            UpdateSeedLabel();

            BuildSiteCards(sites);
            BuildCrewCards(crew);
            RefreshValidation();
        }

        /// <summary>Replaces the RNG seed (the state owns seed generation, not the UI).</summary>
        public void SetSeed(int seed)
        {
            _seed = seed;
            UpdateSeedLabel();
        }

        private void BuildSiteCards(IReadOnlyList<ScavengeSite> sites)
        {
            foreach (var card in _siteCards) if (card.Root != null) Destroy(card.Root.gameObject);
            _siteCards.Clear();
            _selectedSiteId = null;

            if (sites == null || sites.Count == 0)
            {
                OblastUI.Label(_siteColumn, "NoSites", "NO SITES CLEARED FOR ENTRY", 22f, FontStyles.Italic,
                               TextAlignmentOptions.TopLeft, OblastUI.TextFaint);
                return;
            }

            const float cardHeight = 116f;
            const float gap = 12f;
            float y = 0f;

            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                string id = site.Id;

                var card = SelectableCard.Create(_siteColumn, $"Site_{id}", new Vector2(0f, -y),
                                                 new Vector2(720f, cardHeight), site.IsAvailable,
                                                 () => SelectSite(id));

                card.Title.text = site.DisplayName.ToUpperInvariant();
                card.Detail.text = site.Summary;
                card.Status.text = site.IsAvailable ? site.RegionTag.ToUpperInvariant() : "PENDING SURVEY";
                card.Status.color = site.IsAvailable ? OblastUI.TextDim : OblastUI.Danger;

                if (!site.IsAvailable)
                {
                    card.Title.color = OblastUI.TextFaint;
                    card.Detail.color = OblastUI.TextFaint;
                    card.Detail.text = site.UnavailableReason;
                }

                _siteCards.Add(card);
                y += cardHeight + gap;

                if (site.IsAvailable && _selectedSiteId == null) SelectSite(id);
            }
        }

        private void BuildCrewCards(IReadOnlyList<CrewMemberData> crew)
        {
            foreach (var card in _crewCards) if (card.Root != null) Destroy(card.Root.gameObject);
            _crewCards.Clear();
            _selectedCrewId = null;

            if (crew == null || crew.Count == 0)
            {
                OblastUI.Label(_crewColumn, "NoCrew", "NO PERSONNEL ON THE ROSTER", 22f, FontStyles.Italic,
                               TextAlignmentOptions.TopLeft, OblastUI.TextFaint);
                return;
            }

            const float cardHeight = 148f;
            const float gap = 12f;
            float y = 0f;

            foreach (var member in crew)
            {
                if (member == null) continue;
                string id = member.id;

                var card = SelectableCard.Create(_crewColumn, $"Crew_{id}", new Vector2(0f, -y),
                                                 new Vector2(860f, cardHeight), true,
                                                 () => SelectCrew(id));

                card.Title.text = FullName(member).ToUpperInvariant();
                card.Status.text = BackgroundLabel(member.background);

                var stats = member.baseStats;
                card.Detail.text =
                    $"HEALTH <color=#C9C7C0>{stats.maxHealth}</color>   " +
                    $"SANITY <color=#C9C7C0>{stats.maxSanity}</color>   " +
                    $"CARRY <color=#C9C7C0>{stats.carryCapacityKg:0.#} kg</color>\n" +
                    $"<size=17>{Truncate(member.backstoryText, 128)}</size>";

                _crewCards.Add(card);
                y += cardHeight + gap;

                if (_selectedCrewId == null) SelectCrew(id);
            }
        }

        // ── Selection ────────────────────────────────────────────────────────

        private void SelectSite(string siteId)
        {
            _selectedSiteId = siteId;
            foreach (var card in _siteCards) card.SetSelected(card.Root.name == $"Site_{siteId}");
            RefreshValidation();
        }

        private void SelectCrew(string crewId)
        {
            _selectedCrewId = crewId;
            foreach (var card in _crewCards) card.SetSelected(card.Root.name == $"Crew_{crewId}");
            RefreshValidation();
        }

        private void RefreshValidation()
        {
            bool siteOk = !string.IsNullOrEmpty(_selectedSiteId);
            bool crewOk = !string.IsNullOrEmpty(_selectedCrewId);
            bool ready = siteOk && crewOk;

            OblastUI.SetInteractable(_confirmButton, _confirmLabel, ready);

            if (_validationLabel == null) return;
            if (ready)
            {
                _validationLabel.text = "FORM COMPLETE — SUBMIT FOR ENTRY";
                _validationLabel.color = OblastUI.TextDim;
            }
            else
            {
                _validationLabel.text = !siteOk
                    ? "INCOMPLETE: NO SITE DESIGNATED"
                    : "INCOMPLETE: NO OPERATOR ASSIGNED";
                _validationLabel.color = OblastUI.Danger;
            }
        }

        private void UpdateSeedLabel()
        {
            if (_seedLabel != null) _seedLabel.text = $"REQUISITION NO. {_seed:D10}";
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildChrome()
        {
            _root = OblastUI.CreateScreenCanvas(transform, "RunSetup_Canvas", 60);

            var bg = OblastUI.Rect(_root, "Background", OblastUI.Background, raycast: true);
            OblastUI.Stretch(bg.rectTransform);

            var header = OblastUI.Label(_root, "Header", "EXPEDITION REGISTRATION", 52f, FontStyles.Bold,
                                        TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.StretchBand(header.rectTransform, 54f, 62f);
            header.characterSpacing = 10f;

            var sub = OblastUI.Label(_root, "HeaderSub",
                                     "COMPLETE ALL FIELDS. INCOMPLETE FORMS ARE NOT PROCESSED.",
                                     20f, FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.StretchBand(sub.rectTransform, 118f, 26f);
            sub.characterSpacing = 5f;

            var headRule = OblastUI.Rule(_root, "HeaderRule", 1680f, OblastUI.Hairline);
            OblastUI.TopCenter(headRule.rectTransform, new Vector2(0f, -162f), new Vector2(1680f, 1f));

            // ── Column headings ──────────────────────────────────────────────
            var siteHeading = OblastUI.Label(_root, "SiteHeading", "1.  SCAVENGE SITE", 26f, FontStyles.Bold,
                                             TextAlignmentOptions.TopLeft, OblastUI.Stamp);
            OblastUI.TopLeft(siteHeading.rectTransform, new Vector2(120f, -196f), new Vector2(720f, 32f));
            siteHeading.characterSpacing = 6f;

            var crewHeading = OblastUI.Label(_root, "CrewHeading", "2.  ASSIGNED OPERATOR", 26f, FontStyles.Bold,
                                             TextAlignmentOptions.TopLeft, OblastUI.Stamp);
            OblastUI.TopLeft(crewHeading.rectTransform, new Vector2(900f, -196f), new Vector2(860f, 32f));
            crewHeading.characterSpacing = 6f;

            _siteColumn = OblastUI.Group(_root, "SiteColumn");
            OblastUI.TopLeft(_siteColumn, new Vector2(120f, -244f), new Vector2(720f, 600f));

            _crewColumn = OblastUI.Group(_root, "CrewColumn");
            OblastUI.TopLeft(_crewColumn, new Vector2(900f, -244f), new Vector2(860f, 600f));

            // ── Footer ───────────────────────────────────────────────────────
            var footRule = OblastUI.Rule(_root, "FooterRule", 1680f, OblastUI.Hairline);
            OblastUI.BottomCenter(footRule.rectTransform, new Vector2(0f, 150f), new Vector2(1680f, 1f));

            _seedLabel = OblastUI.Label(_root, "Seed", "REQUISITION NO. 0000000000", 20f, FontStyles.Normal,
                                        TextAlignmentOptions.Left, OblastUI.TextFaint);
            OblastUI.BottomLeft(_seedLabel.rectTransform, new Vector2(120f, 104f), new Vector2(600f, 26f));
            _seedLabel.characterSpacing = 4f;

            _validationLabel = OblastUI.Label(_root, "Validation", "INCOMPLETE: NO SITE DESIGNATED", 20f,
                                              FontStyles.Normal, TextAlignmentOptions.Left, OblastUI.Danger);
            OblastUI.BottomLeft(_validationLabel.rectTransform, new Vector2(120f, 70f), new Vector2(800f, 26f));
            _validationLabel.characterSpacing = 4f;

            TextMeshProUGUI cancelLabel;
            var cancel = OblastUI.Button(_root, "CancelButton", "WITHDRAW", 24f,
                                         () => Cancelled?.Invoke(), out cancelLabel);
            OblastUI.BottomRight(cancel.GetComponent<RectTransform>(),
                                 new Vector2(-460f, 62f), new Vector2(260f, 72f));
            cancelLabel.characterSpacing = 5f;

            _confirmButton = OblastUI.Button(_root, "ConfirmButton", "SUBMIT — ENTER THE OBLAST", 24f,
                                             OnConfirm, out _confirmLabel);
            OblastUI.BottomRight(_confirmButton.GetComponent<RectTransform>(),
                                 new Vector2(-120f, 62f), new Vector2(320f, 72f));
            _confirmLabel.characterSpacing = 3f;

            OblastUI.SetInteractable(_confirmButton, _confirmLabel, false);

            Debug.Log("[RunSetupUI] Registration screen built.");
        }

        private void OnConfirm()
        {
            if (string.IsNullOrEmpty(_selectedSiteId) || string.IsNullOrEmpty(_selectedCrewId))
            {
                RefreshValidation();
                return;
            }
            RunConfirmed?.Invoke(_selectedSiteId, _selectedCrewId, _seed);
        }

        // ── Formatting helpers ───────────────────────────────────────────────

        private static string FullName(CrewMemberData member)
        {
            if (!string.IsNullOrEmpty(member.lastName))
            {
                string first = string.IsNullOrEmpty(member.firstName) ? member.displayName : member.firstName;
                return string.IsNullOrEmpty(member.patronymic)
                    ? $"{member.lastName}, {first}"
                    : $"{member.lastName}, {first} {member.patronymic}";
            }
            return string.IsNullOrEmpty(member.displayName) ? member.id : member.displayName;
        }

        private static string BackgroundLabel(CrewBackground background)
        {
            switch (background)
            {
                case CrewBackground.LonerScavenger:   return "LONER / SCAVENGER";
                case CrewBackground.ExCordonSoldier:  return "EX-CORDON SOLDIER";
                case CrewBackground.ExSocietyClerk:   return "EX-SOCIETY CLERK";
                case CrewBackground.FieldMedic:       return "FIELD MEDIC";
                case CrewBackground.Mechanic:         return "MECHANIC";
                case CrewBackground.KafedraDefector:  return "KAFEDRA DEFECTOR";
                case CrewBackground.EcologistSurvivor:return "ECOLOGIST SURVIVOR";
                default:                              return "UNCLASSIFIED";
            }
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "No file on record.";
            string flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return flat.Length <= max ? flat : flat.Substring(0, max - 1).TrimEnd() + "…";
        }

        // ── Card widget ──────────────────────────────────────────────────────

        /// <summary>
        /// One selectable row: an accent bar that lights when chosen, a title, a right-aligned status and a
        /// detail block. Kept private to this screen — RunSummaryUI's rows are read-only and differ enough
        /// that sharing would cost more than it saves.
        /// </summary>
        private class SelectableCard
        {
            public RectTransform Root;
            public Image Background;
            public Image Accent;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Detail;
            public TextMeshProUGUI Status;
            public Button Button;
            public bool Enabled;

            public static SelectableCard Create(Transform parent, string name, Vector2 position, Vector2 size,
                                                bool enabled, Action onClick)
            {
                var card = new SelectableCard { Enabled = enabled };

                card.Background = OblastUI.Rect(parent, name, OblastUI.Panel, raycast: true);
                card.Root = card.Background.rectTransform;
                OblastUI.TopLeft(card.Root, position, size);

                card.Accent = OblastUI.Rect(card.Root, "Accent", OblastUI.Hairline);
                card.Accent.rectTransform.anchorMin = new Vector2(0f, 0f);
                card.Accent.rectTransform.anchorMax = new Vector2(0f, 1f);
                card.Accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                card.Accent.rectTransform.offsetMin = Vector2.zero;
                card.Accent.rectTransform.offsetMax = new Vector2(4f, 0f);

                card.Title = OblastUI.Label(card.Root, "Title", string.Empty, 26f, FontStyles.Bold,
                                            TextAlignmentOptions.TopLeft, OblastUI.TextPrimary);
                OblastUI.TopLeft(card.Title.rectTransform, new Vector2(24f, -16f),
                                 new Vector2(size.x - 260f, 32f));

                card.Status = OblastUI.Label(card.Root, "Status", string.Empty, 18f, FontStyles.Normal,
                                             TextAlignmentOptions.TopRight, OblastUI.TextDim);
                OblastUI.TopLeft(card.Status.rectTransform, new Vector2(size.x - 244f, -18f),
                                 new Vector2(220f, 26f));
                card.Status.characterSpacing = 3f;

                card.Detail = OblastUI.Label(card.Root, "Detail", string.Empty, 19f, FontStyles.Normal,
                                             TextAlignmentOptions.TopLeft, OblastUI.TextDim);
                OblastUI.TopLeft(card.Detail.rectTransform, new Vector2(24f, -54f),
                                 new Vector2(size.x - 48f, size.y - 66f));

                card.Button = card.Background.gameObject.AddComponent<Button>();
                card.Button.targetGraphic = card.Background;
                card.Button.transition = Selectable.Transition.ColorTint;
                card.Button.colors = new ColorBlock
                {
                    normalColor = Color.white,
                    highlightedColor = new Color(1.35f, 1.35f, 1.4f, 1f),
                    pressedColor = new Color(0.8f, 0.8f, 0.85f, 1f),
                    selectedColor = Color.white,
                    disabledColor = new Color(0.7f, 0.7f, 0.7f, 1f),
                    colorMultiplier = 1f,
                    fadeDuration = 0.08f
                };
                card.Button.interactable = enabled;
                if (enabled && onClick != null) card.Button.onClick.AddListener(() => onClick());

                card.SetSelected(false);
                return card;
            }

            public void SetSelected(bool selected)
            {
                bool lit = selected && Enabled;
                Accent.color = lit ? OblastUI.Stamp : OblastUI.Hairline;
                Background.color = lit ? OblastUI.PanelRaised : OblastUI.Panel;
                if (Enabled) Title.color = lit ? OblastUI.TextPrimary : OblastUI.TextDim;
            }
        }
    }
}
