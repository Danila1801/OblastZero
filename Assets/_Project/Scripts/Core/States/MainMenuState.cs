using UnityEngine;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Entry state. Shows the title screen. Waits for player to start a new run or continue.
    /// Real menu UI hookup happens in Step 7 (Polish).
    /// </summary>
    public class MainMenuState : BaseGameState
    {
        public override string StateId => "MainMenu";
        public override GameState StateEnum => GameState.MainMenu;

        protected override void HandleEnter()
        {
            Debug.Log("[MainMenuState] Entered. Awaiting player input.");
            // TODO Step 7: load MainMenu scene additively, present UI.
        }

        protected override void HandleExit()
        {
            Debug.Log("[MainMenuState] Exited.");
            // TODO Step 7: unload MainMenu scene.
        }
    }
}
