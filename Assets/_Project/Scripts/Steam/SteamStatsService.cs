// Assets/_Project/Scripts/Steam/SteamStatsService.cs
using UnityEngine;
#if STEAMWORKS
using Steamworks;
#endif

namespace OblastZero.Steam
{
    /// <summary>
    /// Push integer stats to Steam. Automatically calls SetStat + StoreStats.
    /// Without STEAMWORKS define, all methods are no-ops.
    /// </summary>
    public static class SteamStatsService
    {
        /// <summary>Set an integer stat (e.g runs_started, days_survived_total).</summary>
        public static void SetInt(string statKey, int value)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return;
            if (string.IsNullOrEmpty(statKey)) return;

            try
            {
                SteamUserStats.SetStat(statKey, value);
                SteamUserStats.StoreStats();
                Debug.Log($"[SteamStats] '{statKey}' = {value}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SteamStats] Failed to set '{statKey}': {ex.Message}");
            }
#endif
        }

        /// <summary>Increment a stat by +delta (get current + increment).</summary>
        public static void IncrementInt(string statKey, int delta = 1)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return;
            try
            {
                SetInt(statKey, GetInt(statKey) + delta);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SteamStats] Failed to increment '{statKey}': {ex.Message}");
            }
#endif
        }

        /// <summary>Get current value of an int stat (0 if not found).</summary>
        public static int GetInt(string statKey)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return 0;
            if (string.IsNullOrEmpty(statKey)) return 0;
            try
            {
                return SteamUserStats.GetStatInt(statKey);
            }
            catch { return 0; }
#else
            return 0;
#endif
        }

        /// <summary>Only-store-max pattern: set stat only if new value &gt; current.</summary>
        public static void SetIntIfHigher(string statKey, int value)
        {
            var current = GetInt(statKey);
            if (value > current) SetInt(statKey, value);
        }
    }
}
