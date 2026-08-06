namespace OblastZero.Core
{
    // ─── Game flow ────────────────────────────────────────────────────────────
    public struct GameStateChangedEvent
    {
        public GameState Previous;
        public GameState Current;
    }

    public struct RunStartedEvent
    {
        public string RunId;
        public string SiteId;
    }

    public struct RunEndedEvent
    {
        public RunEndReason Reason;
    }

    // ─── Scene management ─────────────────────────────────────────────────────
    public struct SceneLoadProgressEvent
    {
        public string SceneName;
        public float Progress;
    }

    public struct SceneLoadedEvent
    {
        public string SceneName;
    }

    public struct SceneUnloadedEvent
    {
        public string SceneName;
    }

    // ─── Save system ──────────────────────────────────────────────────────────
    public struct SaveCompletedEvent
    {
        public string FilePath;
        public bool IsProfile;
    }

    public struct SaveFailedEvent
    {
        public string FilePath;
        public string ErrorMessage;
    }

    // ─── Scavenge phase (Phase A) ─────────────────────────────────────────────
    public struct ScavengeTimerTickEvent
    {
        public float SecondsRemaining;
    }

    public struct ScavengeTimerExpiredEvent { }

    public struct ItemPickedUpEvent
    {
        public string ItemDataId;
    }

    public struct CrewRescuedEvent
    {
        public string CrewDataId;
    }

    // Raised whenever the weight carried on the Scavenged channel moves, so the Blowout HUD can show
    // the load bar. Also fires once on run bind, so the HUD starts at a truthful 0 / capacity.
    public struct ScavengeLoadChangedEvent
    {
        public float CurrentKg;
        public float CapacityKg;
    }

    // Raised when a Blowout pickup is refused because it would breach the carry cap. The world object
    // stays where it is — the player has to drop something, or leave it.
    public struct ScavengePickupRejectedEvent
    {
        public string ItemDataId;
        public float ItemWeightKg;
        public float CurrentKg;
        public float CapacityKg;
    }

    // Raised by the bunker-entrance trigger when the player reaches safety before the emission hits.
    public struct ReachBunkerEvent { }

    // Raised by the player controller as the look-target under the crosshair changes, so the HUD can
    // show/hide the contextual interaction prompt. Verb is e.g. "Take" or "Rescue".
    public struct ScavengeTargetChangedEvent
    {
        public bool HasTarget;
        public string Verb;
    }

    // ─── Inventory (bunker) ───────────────────────────────────────────────────
    // Raised by ManagerEventBridge whenever the bunker inventory changes (add / remove /
    // durability / decay / transfer-in). Coarse "something changed, refresh" signal for bunker UI.
    public struct BunkerInventoryChangedEvent
    {
        public string ItemDataId;
    }

    /// <summary>
    /// A bunker item was consumed or removed from stores, carrying the category the coarse
    /// <see cref="BunkerInventoryChangedEvent"/> does not. Raised alongside that event, not instead of it:
    /// the HUD wants "something changed, refresh" and does not care what; anything reasoning about WHAT was
    /// used — the no-medical-items achievement, a future consumption log — needs the category, and resolving
    /// it from the id at every subscriber would mean each of them holding a GameDatabase reference.
    /// </summary>
    public struct BunkerItemConsumedEvent
    {
        public string ItemDataId;
        public OblastZero.Data.ItemCategory Category;
        public int Quantity;
    }

    // ─── Bunker phase (Phase B) ───────────────────────────────────────────────
    public struct DayAdvancedEvent
    {
        public int NewDay;
    }

    public struct CrewStatChangedEvent
    {
        public string CrewInstanceId;
        public string StatName;
        public int OldValue;
        public int NewValue;
    }

    // Raised when a crew member dies (health hit 0 or a lethal outcome).
    public struct CrewDiedEvent
    {
        public string CrewInstanceId;
        public string CrewDataId;
    }

    public struct FactionReputationChangedEvent
    {
        public string FactionId;
        public int OldRep;
        public int NewRep;
    }

    // ─── Event engine (Phase B narrative) ─────────────────────────────────────
    // Raised by ManagerEventBridge when the EventEngine presents an event to the player.
    public struct EventPresentedEvent
    {
        public string EventId;
    }

    // Raised by ManagerEventBridge when the EventEngine finishes applying a resolved choice.
    public struct EventResolvedEvent
    {
        public string EventId;
        public int ChoiceIndex;
        public bool Success;
        public string ActingCrewInstanceId;
        public string FollowUpEventId;
    }

    // ─── Bunker UI intents (UI → logic) ───────────────────────────────────────
    // The bunker HUD raises these; SurvivalPhase2DState is the sole subscriber that turns them into
    // BunkerPhaseController calls. UI never touches game logic directly — it only raises intents.

    // "End Day" pressed. Advances the bunker turn (and may present an event).
    public struct EndDayRequestedEvent { }

    // A choice button pressed on the presented event. ActingCrewInstanceId may be null (bunker-wide).
    public struct EventChoiceSelectedEvent
    {
        public int ChoiceIndex;
        public string ActingCrewInstanceId;
    }

    // ─── Phase A hazards: anomalies (bible §5 / BESTIARY.md) ──────────────────
    // Raised by the zones in OblastZero.Gameplay.Anomalies. The HUD is the only subscriber that matters;
    // everything else about an anomaly is applied by the zone itself, because the effects (speed, clock,
    // inventory) each have exactly one owner already and routing them through the bus would add a second.

    /// <summary>An anomaly did the thing it does. Purely informational — the effect is already applied.</summary>
    public struct AnomalyTriggeredEvent
    {
        public string ClassificationCode;  // e.g. "ANM-Δ-07/CC" — stable, never localized
        public string DisplayName;
        public UnityEngine.Vector3 Position;
    }

    /// <summary>Show or clear a world-interaction prompt that is not a pickup ("Sit for the interview").</summary>
    public struct AnomalyPromptEvent
    {
        public bool Show;
        public string Text;
    }

    /// <summary>An anomaly paid out. The item is already in the inventory when this fires.</summary>
    public struct AnomalyRewardEvent
    {
        public string ClassificationCode;
        public string ItemDataId;
        public string Reason;
    }

    /// <summary>
    /// The player crossed a Backlog boundary. The HUD uses this for the time-dilation readout; the speed
    /// change itself is applied directly by the zone.
    /// </summary>
    public struct BacklogStateChangedEvent
    {
        public bool Inside;
        public float DilationFactor;   // 1 = normal, 0.02 = the bible's crawl
    }

    // ─── Phase A hazards: mutants (bible §5 / BESTIARY.md) ────────────────────

    /// <summary>
    /// A Drowned Census-Taker's pursuit state changed. Drives the HUD's "FOLLOWED" indicator, which is
    /// deliberately understated — the Oblast does not raise its voice.
    /// </summary>
    public struct CensusTakerPursuitEvent
    {
        public bool Pursuing;
        public float DistanceMetres;
    }

    /// <summary>
    /// Registration progress, 0..1. Fires while a Census-Taker is writing the player's name; a value of 1
    /// means the entry was completed and <see cref="PlayerRegisteredEvent"/> follows.
    /// </summary>
    public struct RegistrationProgressEvent
    {
        public float Progress01;
        public bool Interrupted;
    }

    /// <summary>The player was entered in the register. Stat penalties are already applied and stack.</summary>
    public struct PlayerRegisteredEvent
    {
        public int TotalRegistrations;
        public int HealthPenaltyApplied;
        public int SanityPenaltyApplied;
    }

    /// <summary>
    /// The Editor is (or is no longer) in the player's line of sight. <see cref="ExposureSeconds"/> is the
    /// running total for the current sighting, which is what the redaction ladder escalates on.
    /// </summary>
    public struct EditorSightingEvent
    {
        public bool InSight;
        public float ExposureSeconds;
    }

    /// <summary>The Editor altered the pack. Raised once per edit so the HUD can glitch in response.</summary>
    public struct EditorEditEvent
    {
        public string Stage;          // "redacted" | "deleted" | "replaced" — stable keys, never localized
        public string ItemDataId;
        public string ReplacementItemDataId;
    }

    // ─── Expeditions (Phase B) ────────────────────────────────────────────────

    /// <summary>A crew member was sent out. Raised by ExpeditionManager after the dispatch is recorded.</summary>
    public struct ExpeditionDispatchedEvent
    {
        public string ExpeditionId;
        public string CrewInstanceId;
        public string OblastRegionId;
        public int ReturnDay;
    }

    /// <summary>An expedition resolved — returned, returned late, or did not return.</summary>
    public struct ExpeditionResolvedEvent
    {
        public string ExpeditionId;
        public string CrewInstanceId;
        public string OblastRegionId;
        public bool CrewReturned;
        public bool WasDelayed;
        public int ItemsRecovered;
        public string OutcomeSummary;
    }

    /// <summary>
    /// "ARTIFACTS" pressed on the bunker HUD. An intent, like EndDayRequestedEvent — the HUD does not
    /// own the screen and does not know what it does; SurvivalPhase2DState opens it.
    /// </summary>
    public struct ArtifactScreenRequestedEvent { }

    /// <summary>"DISPATCH" pressed on the bunker HUD. Opens the expedition screen.</summary>
    public struct ExpeditionScreenRequestedEvent { }

    /// <summary>An artifact was consumed or spent. Raised by ArtifactSystem after the effect lands.</summary>
    public struct ArtifactUsedEvent
    {
        public string ItemDataId;
        public string TargetCrewInstanceId;
        public string EffectSummary;
        public bool Consumed;
    }
}
