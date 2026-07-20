// Assets/_Project/Scripts/Core/RunData.cs
using System;
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// The single canonical source of truth for one permadeath run. Serialized to JSON on every
    /// day advance (autosave) and on every state transition. Mirrored to Steam Cloud on cloud builds.
    /// Nothing outside Assets/_Project/Scripts/Core/ writes to this directly — all mutation goes
    /// through manager classes (InventoryManager, CrewManager, FactionReputationManager, ...).
    /// </summary>
    [Serializable]
    public class RunData
    {
        public string runId;
        public DateTime runStartedUtc;
        public int currentDay;
        public string currentScavengeSiteId;

        // Phase A handoff (filled during the 60-second 3D scavenge)
        public List<ItemInstance> ScavengedInventory = new();
        public List<CrewInstance> RescuedCrew = new();

        // Phase B persistent state (committed at the transition cutscene, owned by the 2D bunker)
        public List<ItemInstance> BunkerInventory = new();
        public List<CrewInstance> ActiveCrew = new();
        public List<ActiveExpedition> ExpeditionsInFlight = new();
        public List<string> CompletedEventIds = new();
        public List<string> QueuedEventIds = new();

        // Faction reputation
        public int repScaleSociety;
        public int repCordon;
        public int repKafedra;

        // Environmental state
        public int bunkerRadiationPool;
        public int bunkerMorale;
        public bool bunkerSealed;

        // RNG (seed + stream counter so a run is fully reproducible from its seed)
        public int rngSeed;
        public int rngStreamCounter;
    }

    /// <summary>A concrete stack/instance of an item, pointing back at its ScriptableObject data by id.</summary>
    [Serializable]
    public class ItemInstance
    {
        public string itemDataId;
        public int currentDurability;
        public float currentContamination;
        public int quantity;
    }

    /// <summary>A concrete crew member instance. instanceId persists if the same member is recruited again.</summary>
    [Serializable]
    public class CrewInstance
    {
        public string crewDataId;
        public string instanceId; // unique per crew member, persists across runs if recruited again
        public int currentHealth;
        public int currentSanity;
        public int currentFatigue;
        public int currentRadiation;
        public List<string> traitIds = new();
        public bool isAlive;
        public string locationTag; // "bunker", "expedition:reservoir", "missing", "dead_recoverable"
    }

    /// <summary>An expedition currently out in the field, resolved over several days.</summary>
    [Serializable]
    public class ActiveExpedition
    {
        public string expeditionId;
        public string crewInstanceId;
        public string regionTag;
        public int dayStarted;
        public int duration;
        public List<string> loadoutItemInstanceIds;
        public List<string> resolvedEventIds = new();
    }
}
