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
        private ArtifactSystem _artifacts;
        private Gameplay.ExpeditionSystem.ExpeditionManager _expeditions;
        private ManagerEventBridge _bridge;

        public GameStateMachine StateMachine => stateMachine;
        public RunData CurrentRun => stateMachine.Context?.CurrentRun;
        public MetaProgressData MetaProgress => stateMachine.Context?.MetaProgress;

        public GameDatabase Database => gameDatabase;
        public InventoryManager Inventory => _inventory;
        public CrewManager Crew => _crew;
        public FactionReputationManager Reputation => _reputation;
        public EventEngine Events => _events;

        /// <summary>The four bible artifacts and their use effects. Never null after boot.</summary>
        public ArtifactSystem Artifacts => _artifacts;

        /// <summary>Ranged crew missions from the bunker. Never null after boot.</summary>
        public Gameplay.ExpeditionSystem.ExpeditionManager Expeditions => _expeditions;

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

            // Device-local preferences: registered before the data layer, because the data layer's first
            // act is to load a language table and the preferences are what say which language that is.
            var preferences = new PreferencesService(saveService);
            ServiceLocator.Register<PreferencesService>(preferences);
            preferences.ApplyAll();

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
            //
            // The language comes from the player's preferences when they have one, not from the default —
            // a player who chose Russian last session should not see one English main menu before the
            // options screen lets them choose again. PreferencesService has already validated the stored
            // code against the tables this build actually ships.
            string languageCode = LocalizationJsonLoader.DefaultLanguageCode;
            if (ServiceLocator.TryGet<PreferencesService>(out var preferences) && preferences != null)
                languageCode = preferences.Current.languageCode;

            LocalizationJsonLoader.LoadLanguage(languageCode);

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

            // The artifact layer is wired after the four managers rather than alongside them, because
            // it is built from two of them and then hands itself back to two of them. Two of the bible
            // artifacts change how an event resolves and one changes how radiation accumulates, so the
            // hooks go where those things actually happen -- EventEngine and CrewManager -- rather than
            // each caller remembering to ask. Both hooks are optional on the receiving side, so an
            // engine or a crew manager constructed without one behaves exactly as it did before.
            _artifacts = new ArtifactSystem(gameDatabase, _inventory, _crew);
            _events.Artifacts = _artifacts;
            _crew.RadiationMultiplierProvider = _artifacts.RadiationMultiplierFor;

            _expeditions = new Gameplay.ExpeditionSystem.ExpeditionManager(gameDatabase, _inventory, _crew);

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
            _artifacts?.Bind(run);
            _expeditions?.Bind(run);

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
        public void BeginNewRun(string scavengeSiteId, int rngSeed, string leadCrewDataId = null,
                                string secondCrewDataId = null)
        {
            // The purchased-unlock effects are resolved before RunData exists, because two of them
            // (crew stat bonuses) have to be on the run before the first crew member is created — a
            // CrewInstance takes its starting health from the max, and the max is what the unlock raises.
            var unlocks = MetaUnlockCatalog.AggregateFor(MetaProgress);

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
                crewMaxHealthBonus = unlocks.bonusMaxCrewHealth,
                crewMaxSanityBonus = unlocks.bonusMaxCrewSanity,
            };

            stateMachine.Context.CurrentRun = newRun;
            MetaProgress.totalRunsAttempted++;

            // Point the run-scoped managers at the fresh run.
            _inventory?.Bind(newRun);
            _crew?.Bind(newRun);
            _reputation?.Bind(newRun);
            _events?.Bind(newRun);
            _artifacts?.Bind(newRun);
            _expeditions?.Bind(newRun);

            // Every Blowout starts with an empty pack. The ceiling on it is the lead operator's authored
            // carry capacity, not a flat constant — without this the CARRY figure on the RunSetup roster
            // is decoration, and a player who picked Sasha for her pack gets Marina's.
            float carryCapacityKg = BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG;
            string capacitySource = "baseline (no lead)";

            if (!string.IsNullOrEmpty(leadCrewDataId))
            {
                var lead = _crew?.AddRescued(leadCrewDataId);
                if (lead == null)
                {
                    Debug.LogError($"[GameManager] Lead operator '{leadCrewDataId}' could not be added — " +
                                   "the run will reach the bunker with no crew unless someone is rescued.");
                }
                else
                {
                    Debug.Log($"[GameManager] Lead operator '{leadCrewDataId}' registered for the expedition.");

                    // CrewInstance carries only the data id, so the authored stats come from the database.
                    CrewMemberData leadData;
                    if (gameDatabase != null && gameDatabase.TryGetCrew(leadCrewDataId, out leadData) && leadData != null)
                    {
                        float authored = leadData.baseStats.carryCapacityKg;
                        float withTraits = TraitEffects.ModifiedCarryCapacity(lead, authored, gameDatabase);
                        carryCapacityKg = Mathf.Max(BalanceConstants.SCAVENGE_MIN_CARRY_WEIGHT_KG, withTraits);
                        capacitySource = $"lead '{leadCrewDataId}' ({authored:0.##} kg authored" +
                                         (Mathf.Abs(withTraits - authored) > 0.001f ? $", {withTraits:0.##} kg after traits" : string.Empty) + ")";
                    }
                    else
                    {
                        Debug.LogWarning($"[GameManager] No CrewMemberData for lead '{leadCrewDataId}'; " +
                                         "falling back to the baseline scavenge capacity.");
                    }
                }
            }

            // A second operator is a purchased unlock. Registering one without it would hand the effect out
            // free, so the check is here rather than only on the setup screen — BeginNewRun is also reachable
            // from debug launchers and from a future quick-start path.
            if (!string.IsNullOrEmpty(secondCrewDataId))
            {
                if (!unlocks.unlockSecondOperator)
                {
                    Debug.LogWarning($"[GameManager] Second operator '{secondCrewDataId}' ignored — " +
                                     "'unlock_second_operator' is not on file for this profile.");
                }
                else if (secondCrewDataId == leadCrewDataId)
                {
                    Debug.LogWarning("[GameManager] Second operator is the same person as the lead — ignored.");
                }
                else if (_crew?.AddRescued(secondCrewDataId) != null)
                {
                    Debug.Log($"[GameManager] Second operator '{secondCrewDataId}' registered. " +
                              "Two mouths on the ration draw from day one.");
                }
            }

            // The pack bonus applies on top of the lead's capacity, after the crew floor — a purchased
            // frame should be worth its 20 tokens even to the operator with the smallest authored pack.
            if (unlocks.bonusCarryWeightKg > 0f)
            {
                carryCapacityKg += unlocks.bonusCarryWeightKg;
                capacitySource += $" +{unlocks.bonusCarryWeightKg:0.##} kg unlock";
            }

            if (_inventory != null)
            {
                _inventory.ScavengeCarryCapacityKg = carryCapacityKg;
                Debug.Log($"[GameManager] Scavenge carry capacity {carryCapacityKg:0.##} kg — from {capacitySource}. " +
                          $"Baseline {BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG} kg, " +
                          $"floor {BalanceConstants.SCAVENGE_MIN_CARRY_WEIGHT_KG} kg.");
            }

            ApplyStartingUnlocks(unlocks);

            Debug.Log($"[GameManager] New run begun. id={newRun.runId} site={scavengeSiteId} seed={rngSeed} " +
                      $"lead={leadCrewDataId ?? "(none)"} second={secondCrewDataId ?? "(none)"}");

            EventBus.Raise(new RunStartedEvent { RunId = newRun.runId, SiteId = scavengeSiteId });
        }

        /// <summary>
        /// Applies the purchased-unlock effects that need the managers already bound: banked stock and
        /// opening reputation. Crew stat bonuses and the carry bonus are applied by the caller, earlier,
        /// because they must land before the first crew member is created and before the pack is sized.
        ///
        /// <para>Stock goes into the Bunker channel, not the Scavenged one. A purchased ration is issued at
        /// the bunker, not carried through the Blowout — routing it through the Scavenged channel would put
        /// it under the carry cap and turn "three free rations" into "three kilograms less loot".</para>
        /// </summary>
        private void ApplyStartingUnlocks(UnlockEffect unlocks)
        {
            if (unlocks == null) return;

            if (_inventory != null)
            {
                if (unlocks.bonusStartingRations > 0)
                    _inventory.AddItem(InventoryChannel.Bunker, MetaUnlockCatalog.StartingRationItemId,
                                       unlocks.bonusStartingRations);

                if (unlocks.bonusStartingMedical > 0)
                    _inventory.AddItem(InventoryChannel.Bunker, MetaUnlockCatalog.StartingMedicalItemId,
                                       unlocks.bonusStartingMedical);
            }

            if (_reputation != null)
            {
                _reputation.ApplyDelta(FactionId.ScaleSociety, unlocks.startingRepScaleSociety);
                _reputation.ApplyDelta(FactionId.Cordon, unlocks.startingRepCordon);
                _reputation.ApplyDelta(FactionId.Kafedra, unlocks.startingRepKafedra);
            }

            if (unlocks.bonusMaxCrewHealth != 0 || unlocks.bonusMaxCrewSanity != 0)
                Debug.Log($"[GameManager] Unlock stat bonuses active: +{unlocks.bonusMaxCrewHealth} max health, " +
                          $"+{unlocks.bonusMaxCrewSanity} max sanity for every crew member this run.");
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

            // Snapshot the outcome while RunData still exists and the meta counters are already updated.
            // The run-end states enter after this method returns, by which point CurrentRun is null —
            // LastRunSummary is the only place these numbers survive.
            //
            // Order matters: the summary computes the token award from live run state, the award is credited
            // to the profile, and only then is the profile written. Saving first — as this method used to —
            // would persist a profile that does not yet contain the tokens the run just earned, and the only
            // thing that would rescue them is some later save happening to fire before the process exits.
            LastRunSummary = RunSummary.FromRun(run, MetaProgress, reason, gameDatabase);
            MetaProgress.AwardTokens(LastRunSummary.SalvageTokensAwarded);
            LastRunSummary.SalvageTokenBalance = MetaProgress.salvageTokens;

            ServiceLocator.Get<ISaveService>().SaveProfile(MetaProgress);

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
