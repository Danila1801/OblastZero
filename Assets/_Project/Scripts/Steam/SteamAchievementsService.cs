// Assets/_Project/Scripts/Steam/SteamAchievementsService.cs
using UnityEngine;

namespace OblastZero.Steam
{
    /// <summary>
    /// Unlock Steam achievements by key. Batch-friendly.
    /// Without STEAMWORKS define, all methods are no-ops.
    /// </summary>
    public static class SteamAchievementsService
    {
        /// <summary>Unlock a single achievement by Steam API key.</summary>
        public static void Unlock(string achievementKey)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return;
            if (string.IsNullOrEmpty(achievementKey)) return;

            try
            {
                var achv = Facepunch.Steamworks.SteamUserStats.FindAchievement(achievementKey);
                if (achv == null)
                {
                    Debug.LogWarning($"[SteamAchievements] Achievement '{achievementKey}' not found. Check Steamworks Admin.");
                    return;
                }
                if (!achv.State) // only unlock if not already
                {
                    achv.Trigger(false); // false = don't show popup? true = show
                    Debug.Log($"[SteamAchievements] Unlocked '{achievementKey}'.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SteamAchievements] Failed to unlock '{achievementKey}': {ex.Message}");
            }
#endif
        }

        /// <summary>Unlock multiple achievements at once.</summary>
        public static void UnlockMany(params string[] keys)
        {
            if (keys == null) return;
            foreach (var k in keys) Unlock(k);
        }

        /// <summary>Check whether an achievement is already unlocked.</summary>
        public static bool IsUnlocked(string achievementKey)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return false;
            try
            {
                var achv = Facepunch.Steamworks.SteamUserStats.FindAchievement(achievementKey);
                return achv?.State ?? false;
            }
            catch { return false; }
#else
            return false;
#endif
        }
    }
}
