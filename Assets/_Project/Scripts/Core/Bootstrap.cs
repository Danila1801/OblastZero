using UnityEngine;
using OblastZero.Steam;

namespace OblastZero.Core
{
    /// <summary>
    /// Scene entry point. Place on a GameObject in _Bootstrap.unity.
    /// Brings up Steam (if available) and then triggers GameManager initialization on Awake.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class Bootstrap : MonoBehaviour
    {
        /// <summary>Resources-relative path to the SteamConfig asset (no file extension).</summary>
        private const string SteamConfigResourcePath = "SteamConfig";

        [SerializeField] private GameManager gameManagerPrefab;

        [Tooltip("Optional explicit SteamConfig. When empty, Bootstrap loads Resources/SteamConfig.")]
        [SerializeField] private SteamConfig steamConfig;

        [Tooltip("Disable to skip Steam initialization entirely (useful for isolated tests).")]
        [SerializeField] private bool initializeSteam = true;

        private void Awake()
        {
            Debug.Log("[Bootstrap] ──────────────────────────────────────────");
            Debug.Log("[Bootstrap] OBLAST ZERO — Bootstrap awake.");
            Debug.Log($"[Bootstrap] Unity {Application.unityVersion} | Platform {Application.platform} | Build {Application.version}");
            Debug.Log("[Bootstrap] ──────────────────────────────────────────");

            InitializeSteamLayer();

            if (GameManager.Instance != null)
            {
                Debug.Log("[Bootstrap] GameManager already exists — skipping instantiation.");
                return;
            }

            if (gameManagerPrefab != null)
            {
                Instantiate(gameManagerPrefab);
                Debug.Log("[Bootstrap] GameManager prefab instantiated.");
                return;
            }

            // No prefab is the normal case in _Bootstrap.unity: the GameManager is authored directly into the
            // scene there, so it boots itself. Bootstrap still has to run in that scene for the Steam layer,
            // and it runs first (execution order -2000 vs -1000) — which means GameManager.Instance is still
            // null at this point even though the component is sitting right there in the hierarchy. Checking
            // the scene rather than the singleton is what tells the two situations apart; without it, adding
            // Bootstrap to the scene reports "Game cannot start" on every boot of a game that starts fine.
            var sceneManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (sceneManager != null)
                Debug.Log($"[Bootstrap] GameManager authored in the scene ('{sceneManager.gameObject.name}') — " +
                          "it self-initializes; no prefab needed.");
            else
                Debug.LogError("[Bootstrap] No GameManager prefab assigned and none present in the scene. " +
                               "Game cannot start.");
        }

        /// <summary>
        /// Boots SteamManager + SteamEventBridge before the GameManager, so the bridge is already
        /// listening when the first RunStartedEvent fires. Safe when Steam is absent: SteamManager
        /// falls back to offline mode and every Steam call becomes a no-op.
        /// </summary>
        private void InitializeSteamLayer()
        {
            if (!initializeSteam)
            {
                Debug.Log("[Bootstrap] Steam initialization disabled via Inspector.");
                return;
            }

            if (SteamManager.Instance != null)
            {
                Debug.Log("[Bootstrap] SteamManager already exists — skipping Steam boot.");
                return;
            }

            var cfg = steamConfig != null ? steamConfig : Resources.Load<SteamConfig>(SteamConfigResourcePath);
            if (cfg == null)
            {
                Debug.LogWarning($"[Bootstrap] No SteamConfig assigned and none found at Resources/{SteamConfigResourcePath}. Skipping Steam — game runs offline.");
                return;
            }

            SteamManager.Initialize(cfg);

            if (SteamManager.Instance != null)
            {
                SteamManager.Instance.gameObject.AddComponent<SteamEventBridge>();
                Debug.Log("[Bootstrap] SteamEventBridge attached.");
            }
        }
    }
}
