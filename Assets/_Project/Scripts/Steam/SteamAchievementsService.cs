// Assets/_Project/Scripts/Steam/SteamAchievementsService.cs
using UnityEngine;
#if STEAMWORKS
using Steamworks.Data;
#endif

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
                var achv = new Achievement(achievementKey);
                if (!achv.State)
                {
                    achv.Trigger();
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
            if (string.IsNullOrEmpty(achievementKey)) return false;
            try
            {
                return new Achievement(achievementKey).State;
            }
            catch { return false; }
#else
            return false;
#endif
        }
    }
}
