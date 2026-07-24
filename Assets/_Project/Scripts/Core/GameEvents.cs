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
}
