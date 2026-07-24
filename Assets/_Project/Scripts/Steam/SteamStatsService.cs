// Assets/_Project/Scripts/Steam/SteamStatsService.cs
using UnityEngine;

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
                var stat = Facepunch.Steamworks.SteamUserStats.FindStat(statKey);
                if (stat == null)
                {
                    Debug.LogWarning($"[SteamStats] Stat '{statKey}' not found. Check Steamworks Admin.");
                    return;
                }
                stat.IntValue = value;
                Facepunch.Steamworks.SteamUserStats.StoreStats();
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
                var current = GetInt(statKey);
                SetInt(statKey, current + delta);
            }
            catch { }
#endif
        }

        /// <summary>Get current value of an int stat (0 if not found).</summary>
        public static int GetInt(string statKey)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return 0;
            try
            {
                var stat = Facepunch.Steamworks.SteamUserStats.FindStat(statKey);
                return stat?.IntValue ?? 0;
            }
            catch { return 0; }
#else
            return 0;
#endif
        }

        /// <summary>Only-store-max pattern: set stat only if new value > current.</summary>
        public static void SetIntIfHigher(string statKey, int value)
        {
            var current = GetInt(statKey);
            if (value > current) SetInt(statKey, value);
        }
    }
}
