// Assets/_Project/Scripts/Gameplay/DebugRunLauncher.cs
using UnityEngine;
using UnityEngine.InputSystem;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Optional dev shortcut, not part of the shipping flow. The real entry path is
    /// MainMenu → RunSetup → BeginNewRun → ScavengePhase3D, which loads the Scavenge level additively;
    /// this exists only so a fixed seed can be re-entered repeatedly while tuning the 60-second level,
    /// skipping the menus. It is deliberately absent from every scene — add it to a _Bootstrap child by
    /// hand when you want it, and tick <c>enableShortcut</c>.
    ///
    /// Compiled out of release builds entirely, so it can never hijack the flow in a shipped product.
    /// </summary>
    public class DebugRunLauncher : MonoBehaviour
    {
        [Tooltip("Off by default. The shortcut does nothing until this is ticked, so an accidentally " +
                 "placed launcher cannot pre-empt the real MainMenu → RunSetup flow.")]
        [SerializeField] private bool enableShortcut = false;

        [SerializeField] private string scavengeSiteId = "site_test";
        [SerializeField] private int rngSeed = 12345;
        [SerializeField] private Key launchKey = Key.F5;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            if (!enableShortcut) return;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[launchKey].wasPressedThisFrame) return;

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[DebugRunLauncher] No GameManager in the scene. Add the bootstrap first.");
                return;
            }

            gm.BeginNewRun(scavengeSiteId, rngSeed);
            gm.StateMachine.TransitionTo(GameState.ScavengePhase3D);
            Debug.Log($"[DebugRunLauncher] Launched run '{scavengeSiteId}' → ScavengePhase3D. Grab supplies, reach the bunker.");
        }
#endif
    }
}
