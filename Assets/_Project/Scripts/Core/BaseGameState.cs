using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// Abstract MonoBehaviour base for all game states. Lives on a child of the GameStateMachine.
    /// Implements IGameState as specified by DESIGN_BIBLE Section 6.3.
    ///
    /// Subclasses override OnEnter / OnExit / OnTick. The base handles logging and lifecycle gating.
    /// States are NEVER destroyed during normal flow — they're enabled/disabled by the state machine.
    /// </summary>
    public abstract class BaseGameState : MonoBehaviour, IGameState
    {
        public abstract string StateId { get; }
        public abstract GameState StateEnum { get; }

        protected StateContext Context { get; private set; }
        protected bool IsActive { get; private set; }

        public void OnEnter(StateContext context)
        {
            Context = context;
            IsActive = true;

            if (BalanceConstants.VERBOSE_STATE_LOGGING)
            {
                Debug.Log($"[State:{StateId}] OnEnter — day={context?.CurrentRun?.currentDay ?? -1}");
            }

            HandleEnter();
        }

        public void OnExit(StateContext context)
        {
            if (BalanceConstants.VERBOSE_STATE_LOGGING)
            {
                Debug.Log($"[State:{StateId}] OnExit");
            }

            HandleExit();
            IsActive = false;
            Context = null;
        }

        public void OnTick(float deltaTime)
        {
            if (!IsActive) return;
            HandleTick(deltaTime);
        }

        /// <summary>Subclass-specific enter logic. Called after Context is set.</summary>
        protected abstract void HandleEnter();

        /// <summary>Subclass-specific exit logic. Called before Context is cleared.</summary>
        protected abstract void HandleExit();

        /// <summary>Subclass-specific per-frame tick. Default: no-op.</summary>
        protected virtual void HandleTick(float deltaTime) { }

        /// <summary>
        /// Convenience helper for subclasses to request a state transition.
        /// Routes through GameManager so we have a single transition pipeline.
        /// </summary>
        protected void RequestTransition(GameState target)
        {
            if (BalanceConstants.VERBOSE_STATE_LOGGING)
            {
                Debug.Log($"[State:{StateId}] Requesting transition → {target}");
            }
            GameManager.Instance?.StateMachine.TransitionTo(target);
        }
    }
}
