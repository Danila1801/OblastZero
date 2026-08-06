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

            var reputation = gm.Reputation;

            // Reputation is in this guard because the victory evaluator throws without it, and because the
            // faction endgames are gated on reputation — a bunker phase running with a null reputation
            // manager is a run that cannot be won, which is worse than one that refuses to start.
            if (inventory == null || crew == null || database == null || engine == null || reputation == null)
            {
                Debug.LogError("[SurvivalPhase2D] Data layer unavailable (GameDatabase assigned on GameManager?). " +
                               "Cannot start the bunker phase.");
                return;
            }

            var dayController = new BunkerDayController(run, inventory, crew, database, new BunkerDayConfig(), saveService);

            // Property rather than a constructor argument so the three smoke tests can keep building
            // a day controller with no expedition layer at all — the day loop null-checks it.
            dayController.Expeditions = GameManager.Instance != null ? GameManager.Instance.Expeditions : null;
            var victory = new VictoryConditionEvaluator(run, crew, reputation);
            _phase = new BunkerPhaseController(dayController, engine, crew, victory);

            EventBus.Subscribe<EndDayRequestedEvent>(OnEndDayRequested);
            EventBus.Subscribe<EventChoiceSelectedEvent>(OnEventChoiceSelected);
            EventBus.Subscribe<ArtifactScreenRequestedEvent>(OnArtifactScreenRequested);
            EventBus.Subscribe<ExpeditionScreenRequestedEvent>(OnExpeditionScreenRequested);

            Debug.Log($"[SurvivalPhase2D] Entered. Day {run.currentDay}, crew alive {crew.AliveCount()}, " +
                      $"bunker items {run.BunkerInventory.Count}.");

            // Load the 2D bunker scene (HUD + event modal) additively on top of _Bootstrap. The day engine
            // stays scene-independent — the scene only carries presentation, which drives itself off the bus.
            _sceneLoader = ServiceLocator.TryGet<ISceneLoader>(out var loader) ? loader : null;
            if (_sceneLoader != null)
            {
                // The restore MUST wait for the load to finish. LoadSceneAdditive is a coroutine over
                // LoadSceneAsync, so the scene — and the EventModalUI that renders the prompt — does not
                // exist yet when this method returns. Firing the restore here would raise EventPresented at
                // nobody: the resumed run would show no modal while EndDay stayed blocked on the pending
                // event, which is a soft-lock, not a cosmetic miss.
                _sceneLoader.LoadSceneAdditive(BunkerSceneName, RestoreHeldEventIfAny);
            }
            else
            {
                Debug.LogWarning("[SurvivalPhase2D] No ISceneLoader registered — bunker HUD scene not loaded (logic still runs headless).");
                RestoreHeldEventIfAny(); // headless: no listener to wait for
            }
        }

        /// <summary>
        /// Re-opens the event a reloaded run was holding, if it was holding one. Runs after the bunker scene
        /// is up so the modal is subscribed when <c>EventPresented</c> fires.
        /// </summary>
        private void RestoreHeldEventIfAny()
        {
            // The load callback can outlive the state: a fast exit (quit to menu, a wipe) unloads the scene
            // and drops _phase while the coroutine is still in flight.
            if (_phase == null)
            {
                Debug.Log("[SurvivalPhase2D] Bunker scene finished loading after the phase ended — nothing to restore.");
                return;
            }

            var restored = _phase.RestorePendingEvent();
            if (restored != null)
                Debug.Log($"[SurvivalPhase2D] Resumed holding event '{restored.id}' — waiting on a choice, " +
                          "'End Day' stays blocked until it is answered.");
            else
                Debug.Log("[SurvivalPhase2D] No event held from a previous session. Waiting on 'End Day'.");
        }

        protected override void HandleExit()
        {
            EventBus.Unsubscribe<EndDayRequestedEvent>(OnEndDayRequested);
            EventBus.Unsubscribe<EventChoiceSelectedEvent>(OnEventChoiceSelected);
            EventBus.Unsubscribe<ArtifactScreenRequestedEvent>(OnArtifactScreenRequested);
            EventBus.Unsubscribe<ExpeditionScreenRequestedEvent>(OnExpeditionScreenRequested);
            CloseSideScreen();

            if (_sceneLoader != null) _sceneLoader.UnloadScene(BunkerSceneName); // SceneLoader guards if not loaded
            _sceneLoader = null;
            _phase = null;
        }

        // Turn-based: no per-frame logic. (HandleTick stays the base no-op.)

        // ---- UI intent handlers ----

        // ── Side screens (artifacts, dispatch) ───────────────────────────────
        //
        // Opened here rather than by the HUD, for the same reason the event modal is: the HUD raises
        // intents and owns no game logic, and these screens write run state through their managers.
        //
        // One at a time, tracked in a single field. Two full-screen registers open at once would both
        // be interactable, both be writing artifact and expedition state, and neither would be on top
        // in any predictable way.
        private GameObject _sideScreen;

        private void OnArtifactScreenRequested(ArtifactScreenRequestedEvent _)
        {
            if (_sideScreen != null) return;
            var screen = UI.ArtifactUseUI.Open(() => _sideScreen = null);
            _sideScreen = screen != null ? screen.gameObject : null;
        }

        private void OnExpeditionScreenRequested(ExpeditionScreenRequestedEvent _)
        {
            if (_sideScreen != null) return;
            var screen = UI.ExpeditionUI.Open(() => _sideScreen = null);
            _sideScreen = screen != null ? screen.gameObject : null;
        }

        /// <summary>
        /// Tears down an open side screen on phase exit. Without this, abandoning a run with the
        /// artifact register open leaves a full-screen canvas over the main menu — it is spawned as a
        /// root object, so nothing else destroys it.
        /// </summary>
        private void CloseSideScreen()
        {
            if (_sideScreen == null) return;
            Object.Destroy(_sideScreen);
            _sideScreen = null;
        }

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
            // Two axes, both supplied. Proximity (BunkerPhaseActive) decides what a sealed-in crew can act
            // on and fails closed — omitting it selects nothing. Geography (the registered site's region)
            // tints the pool toward the district this run is operating in and fails open, so a site whose
            // region matches no content costs flavour rather than events.
            var oblastRegions = ScavengeSiteCatalog.RegionContextFor(
                GameManager.Instance?.CurrentRun?.currentScavengeSiteId);

            BunkerTurnResult result = _phase.EndDay(RegionTags.BunkerPhaseActive, oblastRegions);

            // Death is checked before victory: a run whose last crew member starved on the winning day is a
            // wipe, and every ending's prose asserts the crew survived.
            if (result.runEnded) { EndRun(); return; }
            if (result.victory.Achieved) EndRunVictorious(result.victory);
        }

        private void OnEventChoiceSelected(EventChoiceSelectedEvent e)
        {
            if (_phase == null) return;

            _phase.ResolvePendingEvent(e.ChoiceIndex, e.ActingCrewInstanceId);

            // A lethal outcome (crewDeathChance) can wipe the last crew — end the run if so.
            if (_phase.IsWipe()) { EndRun(); return; }

            // Reputation moves here, not only on a day tick, so the choice that pushes a faction over the
            // endgame threshold wins the run immediately. Without this check the player would have to press
            // End Day once more to collect an ending they had already earned.
            VictoryVerdict victory = _phase.EvaluateVictory();
            if (victory.Achieved) EndRunVictorious(victory);
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
        /// Closes a won run and hands off to that ending's state.
        ///
        /// <para>This is the caller the four victory states were missing. They were implemented, registered in
        /// <c>_Bootstrap</c>, and reachable by the state machine — but nothing in the game ever passed a
        /// <c>Victory*</c> reason to <see cref="GameManager.EndCurrentRun"/>, so a run could only end in death
        /// or a quit and all four endings were dead code.</para>
        ///
        /// <para>Same ordering contract as <see cref="EndRun"/>: EndCurrentRun snapshots
        /// <see cref="GameManager.LastRunSummary"/> and clears the live run before the transition, and the
        /// victory states read that snapshot rather than <c>CurrentRun</c>.</para>
        /// </summary>
        private void EndRunVictorious(VictoryVerdict victory)
        {
            Debug.Log($"[SurvivalPhase2D] Run won — ending '{victory.EndingName}' ({victory.Explanation}). " +
                      $"Transitioning to {victory.State}.");

            GameManager.Instance.EndCurrentRun(victory.Reason);
            RequestTransition(victory.State);
        }

        /// <summary>
        /// Debug/programmatic entry point equivalent to pressing "End Day". Kept so tools and tests can drive
        /// the turn without going through the UI. Routes through the same intent path.
        /// </summary>
        public void AdvanceDay() => OnEndDayRequested(default);
    }
}
