// Assets/_Project/Scripts/Steam/SteamCloudSave.cs
// Optional cloud backup: mirrors SaveSystem JSON to Steam Remote Storage.
using System.Text;
using UnityEngine;

namespace OblastZero.Steam
{
    public static class SteamCloudSave
    {
        /// <summary>Upload a save JSON blob to Steam Cloud under the given filename.</summary>
        public static bool WriteToCloud(string filename, string json)
        {
#if STEAMWORKS
            if (!SteamManager.Instance || !SteamManager.Instance.IsAvailable) return false;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                Facepunch.Steamworks.SteamRemoteStorage.FileWrite(filename, bytes);
                Debug.Log($"[SteamCloud] Wrote {bytes.Length} bytes to '{filename}'.");
                return true;
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
            try
            {
                if (!Facepunch.Steamworks.SteamRemoteStorage.FileExists(filename))
                {
                    Debug.Log($"[SteamCloud] File '{filename}' does not exist in cloud.");
                    return null;
                }
                var bytes = Facepunch.Steamworks.SteamRemoteStorage.FileRead(filename);
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
            return Facepunch.Steamworks.SteamRemoteStorage.FileExists(filename);
#else
            return false;
#endif
        }
    }
}
