// Assets/_Project/Scripts/Core/IGameState.cs
namespace OblastZero.Core
{
    /// <summary>
    /// Contract every game state (MainMenu, ScavengePhase3D, Transition, SurvivalPhase2D,
    /// RunVictory/RunFailed) must implement. The GameStateMachine drives these calls.
    /// </summary>
    public interface IGameState
    {
        /// <summary>Stable identifier used for logging and transition lookups.</summary>
        string StateId { get; }

        /// <summary>Called once when the machine enters this state. Receives the shared run/meta context.</summary>
        void OnEnter(StateContext context);

        /// <summary>Called once when the machine leaves this state. Clean up scenes, listeners, timers here.</summary>
        void OnExit(StateContext context);

        /// <summary>Called every frame (or day-substep) while this state is active.</summary>
        void OnTick(float deltaTime);
    }
}
