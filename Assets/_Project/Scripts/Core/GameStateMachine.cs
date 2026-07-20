using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// Central state machine. Lives on the GameManager singleton.
    /// Holds references to every BaseGameState MonoBehaviour child and routes transitions.
    ///
    /// Transition rules:
    ///   - One state is "current" at any time (excluding overlay states like Paused).
    ///   - Transitions go through TransitionTo() — never call OnEnter/OnExit directly.
    ///   - The machine emits events via EventBus on every transition for UI/audio to react.
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        [Header("Initial State")]
        [SerializeField] private GameState initialState = GameState.MainMenu;

        private readonly Dictionary<GameState, BaseGameState> _states = new();
        private BaseGameState _currentState;
        private StateContext _context;

        public GameState CurrentStateEnum => _currentState != null ? _currentState.StateEnum : GameState.None;
        public BaseGameState CurrentState => _currentState;
        public StateContext Context => _context;

        public void Initialize(StateContext context)
        {
            _context = context;

            // Auto-register every BaseGameState found as a child of this transform.
            var foundStates = GetComponentsInChildren<BaseGameState>(includeInactive: true);
            foreach (var state in foundStates)
            {
                if (_states.ContainsKey(state.StateEnum))
                {
                    Debug.LogError($"[GameStateMachine] Duplicate state registered for {state.StateEnum}. " +
                                   $"Existing: {_states[state.StateEnum].name}, new: {state.name}. Ignoring new.");
                    continue;
                }
                _states.Add(state.StateEnum, state);
                state.gameObject.SetActive(false);
            }

            Debug.Log($"[GameStateMachine] Initialized with {_states.Count} states registered.");

            TransitionTo(initialState);
        }

        public void TransitionTo(GameState target)
        {
            if (!_states.TryGetValue(target, out var nextState))
            {
                Debug.LogError($"[GameStateMachine] No state registered for {target}. Transition aborted.");
                return;
            }

            if (_currentState != null && _currentState.StateEnum == target)
            {
                Debug.LogWarning($"[GameStateMachine] Already in state {target}. Ignoring transition.");
                return;
            }

            var previousState = _currentState?.StateEnum ?? GameState.None;

            if (_currentState != null)
            {
                _currentState.OnExit(_context);
                _currentState.gameObject.SetActive(false);
            }

            _currentState = nextState;
            _currentState.gameObject.SetActive(true);
            _currentState.OnEnter(_context);

            EventBus.Raise(new GameStateChangedEvent
            {
                Previous = previousState,
                Current = target,
            });
        }

        private void Update()
        {
            if (_currentState != null)
            {
                _currentState.OnTick(Time.deltaTime);
            }
        }
    }
}
