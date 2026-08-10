// Assets/_Project/Scripts/Core/MetaUnlockCatalog.cs
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// What a purchased unlock does to the next run. Every field is additive and defaults to zero, so a
    /// freshly-constructed effect is a no-op and unlocks compose by summation without ordering effects.
    ///
    /// <para>Only effects that some system actually consumes exist here. That is a deliberate constraint:
    /// an unlock the player pays 30 tokens for and which silently does nothing is worse than no unlock at
    /// all, and a catalogue is exactly the kind of table where a plausible-looking dead field survives
    /// review. Every field below is read in <c>GameManager.ApplyUnlockEffects</c>; if you add one, wire it
    /// in the same commit.</para>
    /// </summary>
    [System.Serializable]
    public class UnlockEffect
    {
        /// <summary>Extra units of the standard bunker ration, banked before day one.</summary>
        public int bonusStartingRations;

        /// <summary>Extra units of the standard field dressing, banked before day one.</summary>
        public int bonusStartingMedical;

        /// <summary>Added to the lead operator's Blowout carry ceiling, in kg.</summary>
        public float bonusCarryWeightKg;

        /// <summary>Added to every crew member's maximum health for this run (and to their starting health).</summary>
        public int bonusMaxCrewHealth;

        /// <summary>Added to every crew member's maximum sanity for this run (and to their starting sanity).</summary>
        public int bonusMaxCrewSanity;

        /// <summary>Reputation the run opens with, per faction.</summary>
        public int startingRepScaleSociety;
        public int startingRepCordon;
        public int startingRepKafedra;

        /// <summary>When true, the setup screen lets the player register a second operator.</summary>
        public bool unlockSecondOperator;
    }

    /// <summary>
    /// One purchasable entry. <see cref="Id"/> is the stable key written to
    /// <see cref="MetaProgressData.purchasedUnlockIds"/>; never rename one once a profile exists.
    ///
    /// <para><b>There is deliberately no <c>purchased</c> field here.</b> The catalogue is a static
    /// process-wide table; a mutable purchase flag on it would be shared by every profile in the process,
    /// would not survive a restart, and would read as purchased in a fresh profile after any run that bought
    /// it. Ownership lives in <see cref="MetaProgressData"/>, which is the thing that gets saved.</para>
    /// </summary>
    public class MetaUnlock
    {
        public string Id;

        /// <summary>
        /// Localization key for the entry's name. A key rather than a string because the catalogue is a
        /// static table initialised once per process, while the active language can change at any time from
        /// the options screen — baking English in here would freeze the Supply Office in whatever language
        /// happened to be loaded when the type was first touched.
        /// </summary>
        public string DisplayNameKey;

        /// <summary>Localization key for the entry's one-line description. Same reasoning as <see cref="DisplayNameKey"/>.</summary>
        public string DescriptionKey;

        public int TokenCost;
        public UnlockEffect Effect;
    }

    /// <summary>
    /// The provisioning catalogue: what Salvage Tokens buy between runs. Read by the Supply Office screen and
    /// by <c>GameManager.BeginNewRun</c>, which applies every purchased effect to the fresh run.
    ///
    /// <para>Pricing is anchored on the token formula in <see cref="BalanceConstants"/>: a run that survives
    /// to the day-15 endgame tenure pays roughly 30 tokens from days alone, so the cheap consumable unlocks
    /// land inside one good run, the stat unlocks inside two, and the two structural unlocks — a second
    /// operator and the second site — take several. The full catalogue is a little under ten good runs, which
    /// is the curve a roguelite wants: always something in reach, never everything.</para>
    /// </summary>
    public static class MetaUnlockCatalog
    {
        /// <summary>
        /// The unlock that opens the Flooded Census Office. Named because
        /// <see cref="ScavengeSiteCatalog"/> gates that site on it and a literal there would drift.
        /// </summary>
        public const string SecondSiteUnlockId = "unlock_second_site";

        /// <summary>Item granted by the ration unlock. An authored .asset item, so content regeneration cannot retire it.</summary>
        public const string StartingRationItemId = "item_canned_meat";

        /// <summary>Item granted by the medical unlock. Authored .asset, same reasoning as the ration id.</summary>
        public const string StartingMedicalItemId = "item_bandage";

        private static readonly List<MetaUnlock> _catalog = new List<MetaUnlock>
        {
            new MetaUnlock
            {
                Id = "unlock_extra_rations",
                DisplayNameKey = UIStringKeys.UnlockRationsName,
                DescriptionKey = UIStringKeys.UnlockRationsDesc,
                TokenCost = 10,
                Effect = new UnlockEffect { bonusStartingRations = 3 },
            },
            new MetaUnlock
            {
                Id = "unlock_medkit",
                DisplayNameKey = UIStringKeys.UnlockMedkitName,
                DescriptionKey = UIStringKeys.UnlockMedkitDesc,
                TokenCost = 15,
                Effect = new UnlockEffect { bonusStartingMedical = 1 },
            },
            new MetaUnlock
            {
                Id = "unlock_carry_boost",
                DisplayNameKey = UIStringKeys.UnlockCarryName,
                DescriptionKey = UIStringKeys.UnlockCarryDesc,
                TokenCost = 20,
                Effect = new UnlockEffect { bonusCarryWeightKg = 3f },
            },
            new MetaUnlock
            {
                Id = "unlock_crew_health",
                DisplayNameKey = UIStringKeys.UnlockHealthName,
                DescriptionKey = UIStringKeys.UnlockHealthDesc,
                TokenCost = 25,
                Effect = new UnlockEffect { bonusMaxCrewHealth = 10 },
            },
            new MetaUnlock
            {
                Id = "unlock_crew_sanity",
                DisplayNameKey = UIStringKeys.UnlockSanityName,
                DescriptionKey = UIStringKeys.UnlockSanityDesc,
                TokenCost = 25,
                Effect = new UnlockEffect { bonusMaxCrewSanity = 10 },
            },
            new MetaUnlock
            {
                Id = "unlock_rep_society",
                DisplayNameKey = UIStringKeys.UnlockRepSocietyName,
                DescriptionKey = UIStringKeys.UnlockRepSocietyDesc,
                TokenCost = 30,
                Effect = new UnlockEffect { startingRepScaleSociety = 10 },
            },
            new MetaUnlock
            {
                Id = "unlock_rep_cordon",
                DisplayNameKey = UIStringKeys.UnlockRepCordonName,
                DescriptionKey = UIStringKeys.UnlockRepCordonDesc,
                TokenCost = 30,
                Effect = new UnlockEffect { startingRepCordon = 10 },
            },
            new MetaUnlock
            {
                Id = "unlock_rep_kafedra",
                DisplayNameKey = UIStringKeys.UnlockRepKafedraName,
                DescriptionKey = UIStringKeys.UnlockRepKafedraDesc,
                TokenCost = 30,
                Effect = new UnlockEffect { startingRepKafedra = 10 },
            },
            new MetaUnlock
            {
                Id = SecondSiteUnlockId,
                DisplayNameKey = UIStringKeys.UnlockSecondSiteName,
                DescriptionKey = UIStringKeys.UnlockSecondSiteDesc,
                TokenCost = 40,
                Effect = new UnlockEffect(),
            },
            new MetaUnlock
            {
                Id = "unlock_second_operator",
                DisplayNameKey = UIStringKeys.UnlockSecondOperatorName,
                DescriptionKey = UIStringKeys.UnlockSecondOperatorDesc,
                TokenCost = 50,
                Effect = new UnlockEffect { unlockSecondOperator = true },
            },
        };

        /// <summary>Every unlock, in display order.</summary>
        public static IReadOnlyList<MetaUnlock> All => _catalog;

        /// <summary>Looks up an unlock by its stable id. Returns null when unknown.</summary>
        public static MetaUnlock Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var unlock in _catalog)
                if (unlock.Id == id) return unlock;
            return null;
        }

        /// <summary>
        /// The summed effect of everything a profile has bought. Unknown ids are skipped with a warning
        /// rather than throwing: a save written against a catalogue that has since dropped an entry must
        /// still load, or a content edit bricks every existing profile.
        /// </summary>
        public static UnlockEffect AggregateFor(MetaProgressData meta)
        {
            var total = new UnlockEffect();
            if (meta?.purchasedUnlockIds == null) return total;

            foreach (var id in meta.purchasedUnlockIds)
            {
                var unlock = Get(id);
                if (unlock == null)
                {
                    UnityEngine.Debug.LogWarning($"[MetaUnlockCatalog] Profile holds unknown unlock '{id}' — " +
                                                 "skipping it. Was a catalogue entry renamed or removed?");
                    continue;
                }

                var e = unlock.Effect;
                if (e == null) continue;

                total.bonusStartingRations += e.bonusStartingRations;
                total.bonusStartingMedical += e.bonusStartingMedical;
                total.bonusCarryWeightKg += e.bonusCarryWeightKg;
                total.bonusMaxCrewHealth += e.bonusMaxCrewHealth;
                total.bonusMaxCrewSanity += e.bonusMaxCrewSanity;
                total.startingRepScaleSociety += e.startingRepScaleSociety;
                total.startingRepCordon += e.startingRepCordon;
                total.startingRepKafedra += e.startingRepKafedra;
                total.unlockSecondOperator |= e.unlockSecondOperator;
            }

            return total;
        }

        /// <summary>Total cost of the whole catalogue — the Supply Office shows it as a completion target.</summary>
        public static int TotalCatalogueCost()
        {
            int sum = 0;
            foreach (var unlock in _catalog) sum += unlock.TokenCost;
            return sum;
        }
    }
}
