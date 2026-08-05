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
        /// <summary>
        /// Save-format revision this run was written at. Stamped by <c>SaveService.SaveExpedition</c> and read
        /// by <c>RunDataMigrator</c> on load. A save written before the field existed deserializes to 0,
        /// which is exactly how the migrator recognises a legacy file — do not give this a nonzero
        /// initializer.
        /// </summary>
        public int saveFormatVersion;

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

        /// <summary>
        /// The event currently awaiting the player's choice, or null when none is. Owned by
        /// <c>EventEngine</c> alongside the two lists above.
        ///
        /// <para>Serialized because the bunker turn holds an event open across an arbitrary amount of wall
        /// time: the day advance presents it, the player answers whenever they answer, and the autosave that
        /// fires on the day tick lands in between. Without this field a run quit at an open event came back
        /// with the prompt gone and the RNG stream already advanced past it, so the reload silently drew a
        /// different event — a re-roll the player could farm by quitting on any outcome they disliked.
        /// Restoring by id costs no RNG draw, which is the whole point.</para>
        /// </summary>
        public string pendingEventId;

        // Faction reputation
        public int repScaleSociety;
        public int repCordon;
        public int repKafedra;

        // Environmental state
        public int bunkerRadiationPool;
        public int bunkerMorale;
        public bool bunkerSealed;

        // ─── Meta-unlock stat bonuses (run-scoped) ──────────────────────────────
        // Stamped once by GameManager.BeginNewRun from the purchased-unlock aggregate, then read by
        // CrewManager whenever it needs a maximum. They live on the run rather than being re-derived from
        // the profile because a run must stay internally consistent: a player who buys +10 max health
        // mid-run (the Supply Office is only reachable from the menu, but a save can outlive a purchase)
        // must not find their existing crew's ceiling moving under them. A save written before these fields
        // existed deserializes both to 0, which is the correct "no unlocks" reading.

        /// <summary>Added to every crew member's maximum health for this run.</summary>
        public int crewMaxHealthBonus;

        /// <summary>Added to every crew member's maximum sanity for this run.</summary>
        public int crewMaxSanityBonus;

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

        /// <summary>
        /// True when this stack came out of a Carbon Copy anomaly (ANM-Δ-07/CC) and is one of the
        /// duplicates rather than the original. The bible's defects are subtle and specific — wrong
        /// Cyrillic on the label, syringes that inject the wrong fluid, a signature belonging to someone
        /// who could not have signed it — so the flag does nothing visible in the bunker list and only
        /// surfaces when a crew member actually uses the thing, during event resolution.
        ///
        /// <para><b>Defective stacks never merge with genuine ones.</b> <c>InventoryManager.FindStack</c>
        /// treats this field as part of a stack's identity. Merging them would either lose the information
        /// outright or, worse, mark the player's real med kit defective because a copy of it landed in the
        /// same slot.</para>
        ///
        /// <para>A save written before this field existed deserializes to false, which is the correct
        /// reading for every item collected before the anomaly layer shipped.</para>
        /// </summary>
        public bool isDefective;
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
