// Assets/_Project/Scripts/Steam/SteamCloudSave.cs
// Optional cloud backup: mirrors SaveSystem JSON to Steam Remote Storage.
using System.Text;
using UnityEngine;
#if STEAMWORKS
using Steamworks;
#endif

namespace OblastZero.Steam
{
    public static class SteamCloudSave
    {
        /// <summary>Upload a save JSON blob to Steam Cloud under the given filename.</summary>
        public static bool WriteToCloud(string filename, string json)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return false;
            if (string.IsNullOrEmpty(filename) || json == null) return false;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                var ok = SteamRemoteStorage.FileWrite(filename, bytes);
                if (ok) Debug.Log($"[SteamCloud] Wrote {bytes.Length} bytes to '{filename}'.");
                else Debug.LogWarning($"[SteamCloud] FileWrite rejected '{filename}' (quota or cloud disabled).");
                return ok;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SteamCloud] Write failed '{filename}': {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }

        /// <summary>Download a save JSON blob from Steam Cloud. Null if not found.</summary>
        public static string ReadFromCloud(string filename)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return null;
            if (string.IsNullOrEmpty(filename)) return null;
            try
            {
                if (!SteamRemoteStorage.FileExists(filename))
                {
                    Debug.Log($"[SteamCloud] File '{filename}' does not exist in cloud.");
                    return null;
                }
                var bytes = SteamRemoteStorage.FileRead(filename);
                if (bytes == null || bytes.Length == 0) return null;
                return Encoding.UTF8.GetString(bytes);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SteamCloud] Read failed '{filename}': {ex.Message}");
                return null;
            }
#else
            return null;
#endif
        }

        public static bool CloudFileExists(string filename)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return false;
            if (string.IsNullOrEmpty(filename)) return false;
            try { return SteamRemoteStorage.FileExists(filename); }
            catch { return false; }
#else
            return false;
#endif
        }
    }
}
