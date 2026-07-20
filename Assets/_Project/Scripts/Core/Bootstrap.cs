using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// Scene entry point. Place on a GameObject in _Bootstrap.unity.
    /// Triggers GameManager initialization on Awake.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private GameManager gameManagerPrefab;

        private void Awake()
        {
            Debug.Log("[Bootstrap] ──────────────────────────────────────────");
            Debug.Log("[Bootstrap] OBLAST ZERO — Bootstrap awake.");
            Debug.Log($"[Bootstrap] Unity {Application.unityVersion} | Platform {Application.platform} | Build {Application.version}");
            Debug.Log("[Bootstrap] ──────────────────────────────────────────");

            if (GameManager.Instance != null)
            {
                Debug.Log("[Bootstrap] GameManager already exists — skipping instantiation.");
                return;
            }

            if (gameManagerPrefab != null)
            {
                Instantiate(gameManagerPrefab);
                Debug.Log("[Bootstrap] GameManager prefab instantiated.");
            }
            else
            {
                Debug.LogError("[Bootstrap] No GameManager prefab assigned in Inspector. Game cannot start.");
            }
        }
    }
}
