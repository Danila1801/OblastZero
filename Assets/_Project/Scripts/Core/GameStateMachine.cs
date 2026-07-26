using System;
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
    ///
    /// Registration: states are authored as children of this transform in _Bootstrap.unity, but the
    /// scene is not the source of truth for *which* states must exist — the code is. Initialize()
    /// reconciles the two (see <see cref="EnsureStatesRegistered"/>), so a state that was never added
    /// to the scene cannot dead-end a run.
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        [Header("Initial State")]
        [SerializeField] private GameState initialState = GameState.MainMenu;

        private readonly Dictionary<GameState, BaseGameState> _states = new();
        private BaseGameState _currentState;
        private StateContext _context;

        /// <summary>Cached result of the concrete-state reflection scan. One scan per domain reload.</summary>
        private static IReadOnlyList<Type> _discoveredStateTypes;

        public GameState CurrentStateEnum => _currentState != null ? _currentState.StateEnum : GameState.None;
        public BaseGameState CurrentState => _currentState;
        public StateContext Context => _context;

        public void Initialize(StateContext context)
        {
            _context = context;

            // Reconcile scene against code BEFORE the scan below, so anything created here is picked up
            // by it in the same pass.
            EnsureStatesRegistered();

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

        /// <summary>
        /// Creates a child GameObject for every concrete <see cref="BaseGameState"/> that the scene does
        /// not already provide. Without this, a state the designer forgot to add is not a compile error or
        /// a missing-reference warning — it is a silent dead-end: TransitionTo logs "No state registered"
        /// and the run stops there, which is exactly what a wipe used to do before RunFailedState was
        /// wired into _Bootstrap by hand.
        ///
        /// The Editor tool (OblastZero → Setup → Register Missing States) does the same thing at author
        /// time and is still the preferred route, because a scene-authored state can carry serialized
        /// inspector values. This is the safety net, not a replacement: it logs a warning naming the tool
        /// whenever it has to step in.
        /// </summary>
        private void EnsureStatesRegistered()
        {
            var present = new HashSet<Type>();
            foreach (var existing in GetComponentsInChildren<BaseGameState>(includeInactive: true))
            {
                if (existing != null) present.Add(existing.GetType());
            }

            var missing = new List<string>();
            foreach (var type in DiscoverStateTypes())
            {
                if (present.Contains(type)) continue;

                var go = new GameObject(type.Name);
                go.transform.SetParent(transform, false);
                go.AddComponent(type);
                missing.Add(type.Name);
            }

            if (missing.Count > 0)
            {
                Debug.LogWarning($"[GameStateMachine] {missing.Count} state(s) missing from the scene were created at runtime: " +
                                 $"{string.Join(", ", missing)}. Run 'OblastZero → Setup → Register Missing States' on " +
                                 "_Bootstrap.unity and save it to author them properly.");
            }
        }

        /// <summary>
        /// Every concrete <see cref="BaseGameState"/> in the game assembly, found by reflection and cached
        /// for the lifetime of the domain. Reflection rather than a hand-maintained list because a list is
        /// a second place to forget a state — the failure this whole mechanism exists to prevent. Results
        /// are name-sorted so the child order under the machine is deterministic between runs.
        /// </summary>
        public static IReadOnlyList<Type> DiscoverStateTypes()
        {
            if (_discoveredStateTypes != null) return _discoveredStateTypes;

            Type[] candidates;
            try
            {
                candidates = typeof(BaseGameState).Assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                // A single unloadable type must not cost us every state. Keep what did load.
                Debug.LogWarning($"[GameStateMachine] Partial type load while discovering states: {ex.Message}");
                candidates = ex.Types;
            }

            var discovered = new List<Type>();
            foreach (var type in candidates)
            {
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition) continue;
                if (!typeof(BaseGameState).IsAssignableFrom(type)) continue;
                discovered.Add(type);
            }
            discovered.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            _discoveredStateTypes = discovered;
            return _discoveredStateTypes;
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
