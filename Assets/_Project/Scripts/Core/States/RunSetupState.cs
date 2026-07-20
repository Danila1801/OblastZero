using UnityEngine;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Crew/loadout/site selection. Calls GameManager.BeginNewRun() when the player commits.
    /// </summary>
    public class RunSetupState : BaseGameState
    {
        public override string StateId => "RunSetup";
        public override GameState StateEnum => GameState.RunSetup;

        protected override void HandleEnter()
        {
            Debug.Log("[RunSetupState] Entered. Player selecting crew, loadout, and scavenge site.");
        }

        protected override void HandleExit()
        {
            Debug.Log("[RunSetupState] Exited — committing run setup.");
        }
    }
}
