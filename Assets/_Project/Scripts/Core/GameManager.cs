using UnityEngine;
using OblastZero.Data;
using OblastZero.Gameplay;
using OblastZero.Services;

namespace OblastZero.Core
{
    /// <summary>
    /// The top-level singleton. Lives forever, owns the state machine, owns the context.
    /// Placed on a root GameObject in the _Bootstrap scene with [DontDestroyOnLoad].
    ///
    /// Acts as the composition root: it loads the GameDatabase, constructs the gameplay managers
    /// (Inventory/Crew), wires them to the EventBus via ManagerEventBridge, and exposes them as properties.
    /// The ServiceLocator holds only cross-cutting infra services (save, scene); the run-scoped managers and
    /// the content database are reached through GameManager.Instance.Inventory / .Crew / .Database.
    ///
    /// Access pattern: GameManager.Instance.StateMachine.TransitionTo(GameState.MainMenu);
    /// Access pattern: GameManager.Instance.CurrentRun.currentDay++;
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] private GameStateMachine stateMachine;
        [SerializeField] private GameDatabase gameDatabase; // assign Assets/Data/GameDatabase.asset in the inspector

        private InventoryManager _inventory;
        private CrewManager _crew;
        private FactionReputationManager _reputation;
        private EventEngine _events;
        private ManagerEventBridge _bridge;

        public GameStateMachine StateMachine => stateMachine;
        public RunData CurrentRun => stateMachine.Context?.CurrentRun;
        public MetaProgressData MetaProgress => stateMachine.Context?.MetaProgress;

        public GameDatabase Database => gameDatabase;
        public InventoryManager Inventory => _inventory;
        public CrewManager Crew => _crew;
        public FactionReputationManager Reputation => _reputation;
        public EventEngine Events => _events;

        /// <summary>
        /// Outcome of the most recently ended run, captured inside <see cref="EndCurrentRun"/> while the
        /// RunData was still populated. The run-end states read this: they enter AFTER the run has been
        /// closed and cleared, so it is the only place the numbers still exist. Null before the first
        /// run ends.
        /// </summary>
        public RunSummary LastRunSummary { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[GameManager] Duplicate instance detected on {gameObject.name}. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Debug.Log("[GameManager] Awake — initializing core services.");
            InitializeServices();
        }

        private void InitializeServices()
        {
            // Register cross-cutting infra services into the locator (these implement IService).
            // Order matters — SaveService must be ready before context is built.
            var saveService = new SaveService();
            ServiceLocator.Register<ISaveService>(saveService);

            var sceneLoader = gameObject.AddComponent<SceneLoader>();
            ServiceLocator.Register<ISceneLoader>(sceneLoader);

            // Data layer: load the content registry, then build the run-scoped gameplay managers and
            // bridge their events onto the EventBus. Managers are bound to a run in BeginNewRun.
            InitializeDataLayer();

            // Build the persistent context. MetaProgress loads from disk if it exists; otherwise fresh.
            var meta = saveService.LoadProfile() ?? new MetaProgressData();
            var context = new StateContext
            {
                CurrentRun = null,           // No active run on boot
                MetaProgress = meta,
            };

            stateMachine.Initialize(context);

            Debug.Log($"[GameManager] Boot complete. MetaProgress loaded: runs attempted={meta.totalRunsAttempted}, survived={meta.totalRunsSurvived}.");
        }

        private void InitializeDataLayer()
        {
            // Language table before content: a language load swaps the whole table, so it has to happen
            // before anything registers inline display strings while deserializing content.
            LocalizationJsonLoader.LoadLanguage(LocalizationJsonLoader.DefaultLanguageCode);

            if (gameDatabase == null)
            {
                Debug.LogError("[GameManager] GameDatabase is not assigned in the inspector. " +
                               "Inventory/Crew systems will be unavailable. Assign Assets/Data/GameDatabase.asset.");
                return;
            }

            gameDatabase.Initialize();
            LogContentDiagnostics();

            _inventory = new InventoryManager(gameDatabase);
            _crew = new CrewManager(gameDatabase);
            _reputation = new FactionReputationManager();
            _events = new EventEngine(gameDatabase, _inventory, _crew, _reputation);

            _bridge = new ManagerEventBridge();
            _bridge.Connect(_inventory, _crew, _reputation, _events);

            Debug.Log("[GameManager] Data layer wired: GameDatabase + Inventory/Crew/Reputation managers + EventEngine + EventBus bridge.");
        }

        /// <summary>
        /// One line at boot saying exactly how much content actually made it into memory, plus a hard
        /// error per channel that came back short.
        ///
        /// This exists because the failure it catches is invisible: <c>GameDatabase.Initialize</c> succeeds
        /// whether the Resources JSON loaders returned 703 items or zero, and the game boots either way.
        /// The first symptom would otherwise be an event resolving against an id that is not in the index,
        /// many minutes into a run. Counts are read from AllItems/AllEvents rather than the serialized
        /// lists, so this measures the merged authored + JSON set the game will actually query.
        /// </summary>
        private void LogContentDiagnostics()
        {
            int itemCount = gameDatabase.AllItems?.Count ?? 0;
            int eventCount = gameDatabase.AllEvents?.Count ?? 0;
            int crewCount = gameDatabase.AllCrew?.Count ?? 0;
            int factionCount = gameDatabase.factions?.Count ?? 0;

            Debug.Log($"[GameManager] GameDatabase loaded: {itemCount} items, {eventCount} events, " +
                      $"{crewCount} crew, {factionCount} factions. " +
                      $"Localization: {LocalizedStrings.Count} keys ('{LocalizedStrings.ActiveLanguageCode ?? "none"}').");

            if (itemCount < BalanceConstants.CONTENT_MIN_EXPECTED_ITEMS)
                Debug.LogError($"[GameManager] CRITICAL: only {itemCount} items loaded (expected at least " +
                               $"{BalanceConstants.CONTENT_MIN_EXPECTED_ITEMS}). Check ItemJsonLoader and Assets/Data/Resources/Items/.");

            if (eventCount < BalanceConstants.CONTENT_MIN_EXPECTED_EVENTS)
                Debug.LogError($"[GameManager] CRITICAL: only {eventCount} events loaded (expected at least " +
                               $"{BalanceConstants.CONTENT_MIN_EXPECTED_EVENTS}). Check EventJsonLoader and Assets/Data/Resources/Events/.");

            if (crewCount < BalanceConstants.CONTENT_MIN_EXPECTED_CREW)
                Debug.LogError($"[GameManager] CRITICAL: {crewCount} crew loaded. RunSetup has no operator to " +
                               "register as lead, so every run reaches the bunker empty.");

            if (factionCount < BalanceConstants.CONTENT_MIN_EXPECTED_FACTIONS)
                Debug.LogError($"[GameManager] CRITICAL: {factionCount} factions loaded (expected " +
                               $"{BalanceConstants.CONTENT_MIN_EXPECTED_FACTIONS}: Scale Society, Cordon, Kafedra). " +
                               "Reputation lookups will return null.");

            if (LocalizedStrings.Count == 0)
                Debug.LogError("[GameManager] CRITICAL: no localization keys loaded. Every UI string will " +
                               "render as its raw key. Check Assets/Data/Resources/Locale/.");
        }

        /// <summary>
        /// Points every run-scoped manager at whatever run currently sits on the context. Call after
        /// restoring a run from disk — without it the managers stay bound to the previous run (or to
        /// nothing at all) and every read comes back empty.
        /// </summary>
        public void RebindManagersToCurrentRun()
        {
            var run = CurrentRun;
            if (run == null)
            {
                Debug.LogWarning("[GameManager] RebindManagersToCurrentRun called with no active run. Ignoring.");
                return;
            }

            _inventory?.Bind(run);
            _crew?.Bind(run);
            _reputation?.Bind(run);
            _events?.Bind(run);

            Debug.Log($"[GameManager] Managers rebound to run '{run.runId}' (day {run.currentDay}).");
        }

        /// <summary>
        /// Starts a new run. Creates a fresh RunData and pushes it into the context.
        /// Called by RunSetupState after the player commits their loadout.
        ///
        /// <paramref name="leadCrewDataId"/> is the operator the player registered for the expedition. They
        /// are added to the rescued list up front, so the run always reaches the bunker with at least one
        /// living crew member — without a lead, day one is an immediate wipe.
        /// </summary>
        public void BeginNewRun(string scavengeSiteId, int rngSeed, string leadCrewDataId = null)
        {
            var newRun = new RunData
            {
                runId = System.Guid.NewGuid().ToString("N"),
                runStartedUtc = System.DateTime.UtcNow,
                currentDay = 0,
                currentScavengeSiteId = scavengeSiteId,
                rngSeed = rngSeed,
                rngStreamCounter = 0,
                bunkerSealed = false,
                bunkerMorale = BalanceConstants.STARTING_BUNKER_MORALE,
                bunkerRadiationPool = BalanceConstants.STARTING_BUNKER_RADIATION_POOL,
            };

            stateMachine.Context.CurrentRun = newRun;
            MetaProgress.totalRunsAttempted++;

            // Point the run-scoped managers at the fresh run.
            _inventory?.Bind(newRun);
            _crew?.Bind(newRun);
            _reputation?.Bind(newRun);
            _events?.Bind(newRun);

            // Every Blowout starts with an empty pack against the standard ceiling.
            if (_inventory != null)
                _inventory.ScavengeCarryCapacityKg = BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG;

            if (!string.IsNullOrEmpty(leadCrewDataId))
            {
                var lead = _crew?.AddRescued(leadCrewDataId);
                if (lead == null)
                    Debug.LogError($"[GameManager] Lead operator '{leadCrewDataId}' could not be added — " +
                                   "the run will reach the bunker with no crew unless someone is rescued.");
                else
                    Debug.Log($"[GameManager] Lead operator '{leadCrewDataId}' registered for the expedition.");
            }

            Debug.Log($"[GameManager] New run begun. id={newRun.runId} site={scavengeSiteId} seed={rngSeed} " +
                      $"lead={leadCrewDataId ?? "(none)"}");

            EventBus.Raise(new RunStartedEvent { RunId = newRun.runId, SiteId = scavengeSiteId });
        }

        /// <summary>
        /// Ends the current run. Applies salvage, commits meta-progression, clears RunData.
        /// </summary>
        public void EndCurrentRun(RunEndReason reason)
        {
            var run = CurrentRun;
            if (run == null)
            {
                Debug.LogWarning("[GameManager] EndCurrentRun called with no active run. Ignoring.");
                return;
            }

            Debug.Log($"[GameManager] Ending run {run.runId} — reason={reason}, day={run.currentDay}");

            if (reason == RunEndReason.Extracted ||
                reason == RunEndReason.VictoryStabilization ||
                reason == RunEndReason.VictoryRelief ||
                reason == RunEndReason.VictoryAdaptation ||
                reason == RunEndReason.VictoryIndependent)
            {
                MetaProgress.totalRunsSurvived++;
                ApplySuccessfulExtraction(run);
            }
            else
            {
                ApplyDeathSalvage(run);
            }

            ServiceLocator.Get<ISaveService>().SaveProfile(MetaProgress);

            // Snapshot the outcome while RunData still exists and the meta counters are already updated.
            // The run-end states enter after this method returns, by which point CurrentRun is null —
            // LastRunSummary is the only place these numbers survive.
            LastRunSummary = RunSummary.FromRun(run, MetaProgress, reason, gameDatabase);

            // The run is over: its save channel should not outlive it, or the main menu would offer to
            // "resume" a run that has already been closed out.
            ServiceLocator.Get<ISaveService>().DeleteExpeditionSave();

            stateMachine.Context.CurrentRun = null;

            EventBus.Raise(new RunEndedEvent { Reason = reason });
        }

        private void ApplySuccessfulExtraction(RunData run)
        {
            // Full inventory transfers to meta. Implementation hooked up when HomeBunker
            // resource banking system is built in Step 6 (Meta-Progression).
            Debug.Log($"[GameManager] Successful extraction — full bunker inventory ({run.BunkerInventory.Count} items) committed to meta-bank.");
        }

        private void ApplyDeathSalvage(RunData run)
        {
            // SALVAGE_RATE_ON_DEATH applies here. Detailed banking logic in MetaProgression step.
            int salvageCount = Mathf.FloorToInt(run.BunkerInventory.Count * BalanceConstants.SALVAGE_RATE_ON_DEATH);
            Debug.Log($"[GameManager] Death salvage applied — {salvageCount} of {run.BunkerInventory.Count} items recovered ({BalanceConstants.SALVAGE_RATE_ON_DEATH:P0} rate).");
        }

        private void OnApplicationQuit()
        {
            Debug.Log("[GameManager] Application quitting — flushing saves.");
            if (MetaProgress != null)
            {
                ServiceLocator.Get<ISaveService>().SaveProfile(MetaProgress);
            }
            if (CurrentRun != null)
            {
                ServiceLocator.Get<ISaveService>().SaveExpedition(CurrentRun);
            }

            _bridge?.Disconnect();
        }
    }

    public enum RunEndReason
    {
        Quit,
        AllCrewDead,
        BunkerBreach,
        Extracted,
        VictoryStabilization,
        VictoryRelief,
        VictoryAdaptation,
        VictoryIndependent,
    }
}
