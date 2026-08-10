// Assets/_Project/Scripts/UI/MetaUnlockUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;

namespace OblastZero.UI
{
    /// <summary>
    /// The Supply Office: where Salvage Tokens become permanent provisioning. Reached from the title screen,
    /// returns to it.
    ///
    /// <para>Presentation only, like every other screen in this namespace. It never touches
    /// <see cref="MetaProgressData"/> — it raises <see cref="PurchaseRequested"/> with an unlock id and
    /// <see cref="BackRequested"/>, and <c>MainMenuState</c> decides what those mean and when to save. The
    /// screen is then told what changed via <see cref="Present"/>. Letting the UI call
    /// <c>MetaProgressData.Purchase</c> directly would put a second writer on the profile and leave the save
    /// point up to whichever screen happened to be open.</para>
    ///
    /// <para>Laid out as a fixed two-column grid rather than a scroll view. Ten entries at 118 px fit inside
    /// the reference height with the header and footer, so a ScrollRect would add a viewport, a content
    /// rect and a mask to solve a problem that does not exist — and every one of those is a chance to
    /// reproduce the inert-LayoutElement failure the shared vocabulary exists to prevent. Add the scroll
    /// view the day the catalogue outgrows the page.</para>
    /// </summary>
    public class MetaUnlockUI : MonoBehaviour
    {
        /// <summary>Raised when the player presses PURCHASE on an entry. Arg: the unlock id.</summary>
        public event Action<string> PurchaseRequested;

        /// <summary>Raised by the BACK button.</summary>
        public event Action BackRequested;

        private RectTransform _root;
        private RectTransform _grid;
        private TextMeshProUGUI _balanceLabel;
        private TextMeshProUGUI _noticeLabel;

        private readonly List<UnlockCard> _cards = new List<UnlockCard>();

        private void Awake() => BuildChrome();

        // ── Population ───────────────────────────────────────────────────────

        /// <summary>
        /// Draws the catalogue against a profile. Safe to call repeatedly — after every purchase, the state
        /// calls it again with the updated profile and the whole board re-reads, so affordability across all
        /// ten entries stays consistent with one balance.
        /// </summary>
        public void Present(MetaProgressData meta)
        {
            int balance = meta != null ? meta.salvageTokens : 0;
            int lifetime = meta != null ? meta.lifetimeSalvageTokens : 0;

            if (_balanceLabel != null)
            {
                _balanceLabel.text = LocalizedStrings.Get(UIStringKeys.SupplyBalance,
                                                          balance, lifetime,
                                                          MetaUnlockCatalog.TotalCatalogueCost());
            }

            BuildCards(meta, balance);
            SetNotice(string.Empty, false);
        }

        /// <summary>Shows a one-line response under the grid — a refusal, or a confirmation of a purchase.</summary>
        public void SetNotice(string message, bool isRefusal)
        {
            if (_noticeLabel == null) return;
            _noticeLabel.text = message ?? string.Empty;
            _noticeLabel.color = isRefusal ? OblastUI.Danger : OblastUI.Olive;
        }

        private void BuildCards(MetaProgressData meta, int balance)
        {
            foreach (var card in _cards)
                if (card.Root != null) Destroy(card.Root.gameObject);
            _cards.Clear();

            const float cardWidth = 820f;
            const float cardHeight = 106f;
            const float columnGap = 40f;
            const float rowGap = 12f;

            var catalogue = MetaUnlockCatalog.All;
            for (int i = 0; i < catalogue.Count; i++)
            {
                var unlock = catalogue[i];
                int column = i % 2;
                int row = i / 2;

                var position = new Vector2(column * (cardWidth + columnGap), -row * (cardHeight + rowGap));

                bool owned = meta != null && meta.IsPurchased(unlock.Id);
                bool affordable = !owned && balance >= unlock.TokenCost;

                string id = unlock.Id;
                var card = UnlockCard.Create(_grid, $"Unlock_{id}", position, new Vector2(cardWidth, cardHeight),
                                             () => PurchaseRequested?.Invoke(id));

                card.Title.text = LocalizedStrings.Get(unlock.DisplayNameKey).ToUpperInvariant();
                card.Detail.text = LocalizedStrings.Get(unlock.DescriptionKey);
                card.Apply(owned, affordable, unlock.TokenCost);

                _cards.Add(card);
            }
        }

        // ── Construction ─────────────────────────────────────────────────────

        private void BuildChrome()
        {
            _root = OblastUI.CreateScreenCanvas(transform, "SupplyOffice_Canvas", 55);

            var bg = OblastUI.Rect(_root, "Background", OblastUI.Background, raycast: true);
            OblastUI.Stretch(bg.rectTransform);

            var header = OblastUI.Label(_root, "Header", LocalizedStrings.Get(UIStringKeys.SupplyHeader),
                                        52f, FontStyles.Bold,
                                        TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.StretchBand(header.rectTransform, 46f, 62f);
            header.characterSpacing = 10f;

            var sub = OblastUI.Label(_root, "HeaderSub",
                                     LocalizedStrings.Get(UIStringKeys.SupplySubheader),
                                     20f, FontStyles.Normal, TextAlignmentOptions.Center, OblastUI.TextFaint);
            OblastUI.StretchBand(sub.rectTransform, 110f, 26f);
            sub.characterSpacing = 5f;

            _balanceLabel = OblastUI.Label(_root, "Balance",
                                           LocalizedStrings.Get(UIStringKeys.SupplyBalance, 0, 0, 0),
                                           26f, FontStyles.Bold,
                                           TextAlignmentOptions.Center, OblastUI.TextPrimary);
            OblastUI.StretchBand(_balanceLabel.rectTransform, 148f, 32f);
            _balanceLabel.characterSpacing = 5f;

            var headRule = OblastUI.Rule(_root, "HeaderRule", 1720f, OblastUI.Hairline);
            OblastUI.TopCenter(headRule.rectTransform, new Vector2(0f, -196f), new Vector2(1720f, 1f));

            _grid = OblastUI.Group(_root, "UnlockGrid");
            OblastUI.TopLeft(_grid, new Vector2(120f, -220f), new Vector2(1680f, 620f));

            _noticeLabel = OblastUI.Label(_root, "Notice", string.Empty, 21f, FontStyles.Normal,
                                          TextAlignmentOptions.Left, OblastUI.Olive);
            OblastUI.BottomLeft(_noticeLabel.rectTransform, new Vector2(120f, 104f), new Vector2(1200f, 28f));
            _noticeLabel.characterSpacing = 4f;

            var footRule = OblastUI.Rule(_root, "FooterRule", 1720f, OblastUI.Hairline);
            OblastUI.BottomCenter(footRule.rectTransform, new Vector2(0f, 150f), new Vector2(1720f, 1f));

            var footer = OblastUI.Label(_root, "Footer", LocalizedStrings.Get(UIStringKeys.SupplyFooter),
                                        16f, FontStyles.Normal, TextAlignmentOptions.Left, OblastUI.TextFaint);
            OblastUI.BottomLeft(footer.rectTransform, new Vector2(120f, 62f), new Vector2(1200f, 24f));
            footer.characterSpacing = 3f;

            TextMeshProUGUI backLabel;
            var back = OblastUI.Button(_root, "BackButton", LocalizedStrings.Get(UIStringKeys.SupplyReturn), 24f,
                                       () => BackRequested?.Invoke(), out backLabel);
            OblastUI.BottomRight(back.GetComponent<RectTransform>(),
                                 new Vector2(-120f, 62f), new Vector2(280f, 72f));
            backLabel.characterSpacing = 5f;

            Debug.Log("[MetaUnlockUI] Supply office built.");
        }

        // ── Card widget ──────────────────────────────────────────────────────

        /// <summary>
        /// One catalogue row: title, description, and a right-hand cost block that doubles as the purchase
        /// button. Three visual states — owned, affordable, unaffordable — because "greyed out" alone cannot
        /// distinguish "you already have this" from "you cannot pay for this", and those want opposite
        /// reactions from the player.
        /// </summary>
        private class UnlockCard
        {
            public RectTransform Root;
            public Image Background;
            public Image Accent;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Detail;
            public TextMeshProUGUI CostLabel;
            public Button PurchaseButton;

            public static UnlockCard Create(Transform parent, string name, Vector2 position, Vector2 size,
                                            Action onPurchase)
            {
                var card = new UnlockCard();

                card.Background = OblastUI.Rect(parent, name, OblastUI.Panel);
                card.Root = card.Background.rectTransform;
                OblastUI.TopLeft(card.Root, position, size);

                card.Accent = OblastUI.Rect(card.Root, "Accent", OblastUI.Hairline);
                card.Accent.rectTransform.anchorMin = new Vector2(0f, 0f);
                card.Accent.rectTransform.anchorMax = new Vector2(0f, 1f);
                card.Accent.rectTransform.pivot = new Vector2(0f, 0.5f);
                card.Accent.rectTransform.offsetMin = Vector2.zero;
                card.Accent.rectTransform.offsetMax = new Vector2(4f, 0f);

                card.Title = OblastUI.Label(card.Root, "Title", string.Empty, 24f, FontStyles.Bold,
                                            TextAlignmentOptions.TopLeft, OblastUI.TextPrimary);
                OblastUI.TopLeft(card.Title.rectTransform, new Vector2(22f, -14f),
                                 new Vector2(size.x - 230f, 30f));

                card.Detail = OblastUI.Label(card.Root, "Detail", string.Empty, 18f, FontStyles.Normal,
                                             TextAlignmentOptions.TopLeft, OblastUI.TextDim);
                OblastUI.TopLeft(card.Detail.rectTransform, new Vector2(22f, -48f),
                                 new Vector2(size.x - 230f, 48f));

                TextMeshProUGUI costLabel;
                card.PurchaseButton = OblastUI.Button(card.Root, "Purchase", string.Empty, 20f,
                                                      onPurchase, out costLabel);
                card.CostLabel = costLabel;
                OblastUI.TopLeft(card.PurchaseButton.GetComponent<RectTransform>(),
                                 new Vector2(size.x - 192f, -22f), new Vector2(170f, 62f));
                card.CostLabel.characterSpacing = 3f;

                return card;
            }

            /// <summary>Applies the three-state look. Owned entries are not clickable; unaffordable ones are not either.</summary>
            public void Apply(bool owned, bool affordable, int cost)
            {
                if (owned)
                {
                    Accent.color = OblastUI.Olive;
                    Background.color = OblastUI.Panel;
                    Title.color = OblastUI.TextDim;
                    Detail.color = OblastUI.TextFaint;
                    CostLabel.text = LocalizedStrings.Get(UIStringKeys.SupplyOnFile);
                    CostLabel.color = OblastUI.Olive;
                    PurchaseButton.interactable = false;
                    return;
                }

                if (affordable)
                {
                    Accent.color = OblastUI.Stamp;
                    Background.color = OblastUI.PanelRaised;
                    Title.color = OblastUI.TextPrimary;
                    Detail.color = OblastUI.TextDim;
                    CostLabel.text = LocalizedStrings.Get(UIStringKeys.SupplyApprove, cost);
                    CostLabel.color = OblastUI.TextPrimary;
                    PurchaseButton.interactable = true;
                    return;
                }

                Accent.color = OblastUI.Hairline;
                Background.color = OblastUI.Panel;
                Title.color = OblastUI.TextFaint;
                Detail.color = OblastUI.TextFaint;
                CostLabel.text = LocalizedStrings.Get(UIStringKeys.SupplyCost, cost);
                CostLabel.color = OblastUI.Danger;
                PurchaseButton.interactable = false;
            }
        }
    }
}
