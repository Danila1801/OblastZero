// Assets/_Project/Scripts/Gameplay/DebugRunLauncher.cs
using UnityEngine;
using UnityEngine.InputSystem;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// TEMPORARY dev tool. Lets you jump straight into the 3D Blowout without the MainMenu → RunSetup flow:
    /// press the launch key in Play mode to begin a run and enter ScavengePhase3D. Delete once the real
    /// entry flow exists. Put it on any GameObject in the scene that also has the GameManager available.
    /// </summary>
    public class DebugRunLauncher : MonoBehaviour
    {
        [SerializeField] private string scavengeSiteId = "site_test";
        [SerializeField] private int rngSeed = 12345;
        [SerializeField] private Key launchKey = Key.F5;

        private void Update()
        {
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
    }
}
