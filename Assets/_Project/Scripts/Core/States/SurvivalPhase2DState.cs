// Assets/_Project/Scripts/Core/States/SurvivalPhase2DState.cs
using UnityEngine;
using OblastZero.Gameplay;

namespace OblastZero.Core
{
    /// <summary>
    /// The long bunker state (Steps 4–5). Pulls the run-scoped managers, content database, and EventEngine
    /// from GameManager and owns a <see cref="BunkerPhaseController"/> — the turn engine that advances a day
    /// then presents the next narrative event. Turn-based: nothing advances per-frame.
    ///
    /// This state is the single seam between the bunker UI and the game logic. The HUD raises intents on the
    /// EventBus (<see cref="EndDayRequestedEvent"/>, <see cref="EventChoiceSelectedEvent"/>); this state is the
    /// only subscriber, translating them into controller calls and resolving run-end. The UI never calls the
    /// controller directly.
    /// </summary>
    public class SurvivalPhase2DState : BaseGameState
    {
        public override string StateId => "SurvivalPhase2D";
        public override GameState StateEnum => GameState.SurvivalPhase2D;

        /// <summary>Additive 2D scene holding the bunker HUD + event modal (registered in Build Settings).</summary>
        private const string BunkerSceneName = "Bunker";

        private BunkerPhaseController _phase;
        private ISceneLoader _sceneLoader;

        protected override void HandleEnter()
        {
            var run = Context?.CurrentRun;
            if (run == null)
            {
                Debug.LogError("[SurvivalPhase2D] Entered with no active run. Cannot start the bunker phase.");
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[SurvivalPhase2D] No GameManager instance available.");
                return;
            }

            var inventory = gm.Inventory;
            var crew = gm.Crew;
            var database = gm.Database;
            var engine = gm.Events;
            var saveService = ServiceLocator.Get<ISaveService>();

            if (inventory == null || crew == null || database == null || engine == null)
            {
                Debug.LogError("[SurvivalPhase2D] Data layer unavailable (GameDatabase assigned on GameManager?). " +
                               "Cannot start the bunker phase.");
                return;
            }

            var dayController = new BunkerDayController(run, inventory, crew, database, new BunkerDayConfig(), saveService);
            _phase = new BunkerPhaseController(dayController, engine, crew);

            EventBus.Subscribe<EndDayRequestedEvent>(OnEndDayRequested);
            EventBus.Subscribe<EventChoiceSelectedEvent>(OnEventChoiceSelected);

            // Load the 2D bunker scene (HUD + event modal) additively on top of _Bootstrap. The day engine
            // stays scene-independent — the scene only carries presentation, which drives itself off the bus.
            _sceneLoader = ServiceLocator.TryGet<ISceneLoader>(out var loader) ? loader : null;
            if (_sceneLoader != null)
                _sceneLoader.LoadSceneAdditive(BunkerSceneName);
            else
                Debug.LogWarning("[SurvivalPhase2D] No ISceneLoader registered — bunker HUD scene not loaded (logic still runs headless).");

            Debug.Log($"[SurvivalPhase2D] Entered. Day {run.currentDay}, crew alive {crew.AliveCount()}, " +
                      $"bunker items {run.BunkerInventory.Count}. Waiting on 'End Day'.");
        }

        protected override void HandleExit()
        {
            EventBus.Unsubscribe<EndDayRequestedEvent>(OnEndDayRequested);
            EventBus.Unsubscribe<EventChoiceSelectedEvent>(OnEventChoiceSelected);

            if (_sceneLoader != null) _sceneLoader.UnloadScene(BunkerSceneName); // SceneLoader guards if not loaded
            _sceneLoader = null;
            _phase = null;
        }

        // Turn-based: no per-frame logic. (HandleTick stays the base no-op.)

        // ---- UI intent handlers ----

        private void OnEndDayRequested(EndDayRequestedEvent _)
        {
            if (_phase == null)
            {
                Debug.LogWarning("[SurvivalPhase2D] End Day requested before the phase controller was ready.");
                return;
            }

            // The tags MUST be passed. EventEngine rejects any event carrying regionTagsAny when the caller
            // supplies none, and every shipped event carries them — calling EndDay() bare selects nothing,
            // every day, for the whole run, and logs only a routine "no event this day" line while doing it.
            BunkerTurnResult result = _phase.EndDay(RegionTags.BunkerPhaseActive);
            if (result.runEnded) EndRun();
        }

        private void OnEventChoiceSelected(EventChoiceSelectedEvent e)
        {
            if (_phase == null) return;

            _phase.ResolvePendingEvent(e.ChoiceIndex, e.ActingCrewInstanceId);

            // A lethal outcome (crewDeathChance) can wipe the last crew — end the run if so.
            if (_phase.IsWipe()) EndRun();
        }

        private void EndRun()
        {
            Debug.Log("[SurvivalPhase2D] All crew dead — ending run.");

            // EndCurrentRun captures GameManager.LastRunSummary before it clears the run, so RunFailedState
            // still has the numbers to display even though CurrentRun is null by the time it enters.
            GameManager.Instance.EndCurrentRun(RunEndReason.AllCrewDead);
            RequestTransition(GameState.RunFailed);
        }

        /// <summary>
        /// Debug/programmatic entry point equivalent to pressing "End Day". Kept so tools and tests can drive
        /// the turn without going through the UI. Routes through the same intent path.
        /// </summary>
        public void AdvanceDay() => OnEndDayRequested(default);
    }
}
