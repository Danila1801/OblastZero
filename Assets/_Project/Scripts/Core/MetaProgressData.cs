// Assets/_Project/Scripts/Core/MetaProgressData.cs
using System;
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// Persistent progression that survives permadeath. Lives in its own save channel, separate
    /// from RunData (the bifurcated save design). Written by the run-end states (RunVictory_* /
    /// RunFailed) and loaded once by MainMenuState.
    ///
    /// <para>This is the only mutable meta state in the game, so it is also the only place purchase
    /// ownership may live. <see cref="MetaUnlockCatalog"/> is a static table shared by the whole process and
    /// deliberately carries no purchase flag — see its remarks.</para>
    /// </summary>
    [Serializable]
    public class MetaProgressData
    {
        public int totalRunsAttempted;
        public int totalRunsSurvived;
        public List<string> unlockedScavengeSites = new();
        public List<string> unlockedStartingKits = new();
        public List<string> unlockedCrewArchetypes = new();
        public List<string> discoveredAnomalyIds = new();
        public List<string> discoveredMutantIds = new();
        public List<string> recoveredDocumentIds = new();
        public List<string> unlockedEndings = new();
        public Dictionary<string, int> steamStats = new();

        // ─── Meta-currency ──────────────────────────────────────────────────────

        /// <summary>
        /// Salvage Tokens: the meta-currency. Awarded at run end by
        /// <see cref="RunSummary.SalvageTokensAwarded"/> and spent in the Supply Office.
        ///
        /// <para>Public field rather than a property because the whole class is a serialization DTO —
        /// Newtonsoft round-trips fields, and a profile written before this field existed deserializes it to
        /// zero, which is exactly right for a new currency.</para>
        /// </summary>
        public int salvageTokens;

        /// <summary>Lifetime tokens earned, never spent down. Shown in the Supply Office as a career figure.</summary>
        public int lifetimeSalvageTokens;

        /// <summary>Ids from <see cref="MetaUnlockCatalog"/> this profile has bought.</summary>
        public List<string> purchasedUnlockIds = new();

        // ─── Unlock ownership ───────────────────────────────────────────────────

        /// <summary>True when this profile owns the named unlock. Null/unknown ids read as false.</summary>
        public bool IsPurchased(string unlockId)
        {
            if (string.IsNullOrEmpty(unlockId) || purchasedUnlockIds == null) return false;
            return purchasedUnlockIds.Contains(unlockId);
        }

        /// <summary>Whether the current balance covers an unlock that is not already owned.</summary>
        public bool CanAfford(MetaUnlock unlock)
        {
            if (unlock == null) return false;
            if (IsPurchased(unlock.Id)) return false;
            return salvageTokens >= unlock.TokenCost;
        }

        /// <summary>
        /// Buys an unlock: deducts the cost and records ownership. Returns false — changing nothing — when
        /// the unlock is unknown, already owned, or unaffordable.
        ///
        /// <para>Deduct-and-record is one operation on purpose. Splitting it would allow a caller to record
        /// ownership without paying, and the Supply Office is the kind of screen where a double-click during
        /// a save write is a realistic way to reach that path.</para>
        /// </summary>
        public bool Purchase(string unlockId)
        {
            var unlock = MetaUnlockCatalog.Get(unlockId);
            if (unlock == null)
            {
                UnityEngine.Debug.LogWarning($"[MetaProgress] Purchase refused — no unlock '{unlockId}' in the catalogue.");
                return false;
            }

            if (IsPurchased(unlock.Id))
            {
                UnityEngine.Debug.Log($"[MetaProgress] '{unlock.Id}' is already on file — purchase ignored.");
                return false;
            }

            if (salvageTokens < unlock.TokenCost)
            {
                UnityEngine.Debug.Log($"[MetaProgress] Purchase refused — '{unlock.Id}' costs {unlock.TokenCost}, " +
                                      $"balance is {salvageTokens}.");
                return false;
            }

            salvageTokens -= unlock.TokenCost;
            if (purchasedUnlockIds == null) purchasedUnlockIds = new List<string>();
            purchasedUnlockIds.Add(unlock.Id);

            UnityEngine.Debug.Log($"[MetaProgress] Purchased '{unlock.Id}' for {unlock.TokenCost} token(s). " +
                                  $"Balance now {salvageTokens}.");
            return true;
        }

        /// <summary>
        /// Credits tokens earned by a finished run. Negative or zero awards are ignored rather than clamped
        /// silently, so a formula regression shows up in the log instead of quietly zeroing a career total.
        /// </summary>
        public void AwardTokens(int amount)
        {
            if (amount <= 0)
            {
                UnityEngine.Debug.Log($"[MetaProgress] Run awarded no tokens ({amount}).");
                return;
            }

            salvageTokens += amount;
            lifetimeSalvageTokens += amount;
            UnityEngine.Debug.Log($"[MetaProgress] Awarded {amount} salvage token(s). " +
                                  $"Balance {salvageTokens}, lifetime {lifetimeSalvageTokens}.");
        }
    }
}
