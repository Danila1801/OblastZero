// Assets/_Project/Scripts/Steam/SteamManager.cs
// Compile-gated: without STEAMWORKS define, this class is an inert no-op singleton.
using UnityEngine;
#if STEAMWORKS
using Steamworks;
#endif

namespace OblastZero.Steam
{
    /// <summary>
    /// Steam initialization + callback pump. Singleton, persistent across scenes.
    /// Call SteamManager.Initialize(config) from Bootstrap before GameManager boot.
    /// Without the STEAMWORKS scripting define, all methods are no-ops so the game still builds.
    /// </summary>
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }

        [SerializeField] private SteamConfig config;
        public SteamConfig Config => config;

        public bool IsInitialized { get; private set; }
        public bool IsAvailable { get; private set; }

        public static void Initialize(SteamConfig cfg)
        {
            if (Instance != null && Instance.IsInitialized)
            {
                Debug.LogWarning("[SteamManager] Already initialized.");
                return;
            }

            var go = new GameObject("[SteamManager]");
            DontDestroyOnLoad(go);
            var mgr = go.AddComponent<SteamManager>();
            mgr.config = cfg;
            Instance = mgr;

#if STEAMWORKS
            mgr.BootSteam(cfg);
#else
            Debug.Log("[SteamManager] STEAMWORKS not defined. Running in offline mode.");
#endif
        }

        public static void Shutdown()
        {
#if STEAMWORKS
            if (Instance != null && Instance.IsInitialized)
            {
                SteamClient.Shutdown();
                Instance.IsInitialized = false;
                Instance.IsAvailable = false;
                Debug.Log("[SteamManager] Steam client shut down.");
            }
#endif
        }

#if STEAMWORKS
        private void BootSteam(SteamConfig cfg)
        {
            try
            {
                // App ID resolves from steam_appid.txt in the Editor / build folder,
                // or from the launching Steam client at runtime.
                SteamClient.Init(cfg.appId, asyncCallbacks: false);

                IsInitialized = true;
                IsAvailable = SteamClient.IsValid;

                if (IsAvailable)
                {
                    Debug.Log($"[SteamManager] Initialized for '{SteamClient.Name}' ({SteamClient.SteamId}). App {cfg.appId}.");

                    // Kick initial stat/achievement fetch so reads are warm.
                    SteamUserStats.RequestCurrentStats();
                }
                else
                {
                    Debug.LogWarning("[SteamManager] Steam client invalid (no steam_appid.txt or Steam not running).");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SteamManager] Steam init failed: {ex.Message}");
                IsInitialized = false;
                IsAvailable = false;
            }
        }

        private void Update()
        {
            if (IsAvailable)
            {
                // Pump Steam callbacks each frame (achievements unlock, stat updates).
                SteamClient.RunCallbacks();
            }
        }

        private void OnApplicationQuit()
        {
            Shutdown();
        }
#endif
    }
}
