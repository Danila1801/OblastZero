using UnityEngine;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Meta hub — the "Hamlet" between runs. Player allocates banked resources,
    /// recruits new crew, upgrades rooms, picks next expedition.
    /// Real implementation in Step 6 (Meta-Progression Systems).
    /// </summary>
    public class HomeBunkerState : BaseGameState
    {
        public override string StateId => "HomeBunker";
        public override GameState StateEnum => GameState.HomeBunker;

        protected override void HandleEnter()
        {
            Debug.Log("[HomeBunkerState] Entered — player at persistent home base.");
        }

        protected override void HandleExit()
        {
            Debug.Log("[HomeBunkerState] Exited.");
        }
    }
}
