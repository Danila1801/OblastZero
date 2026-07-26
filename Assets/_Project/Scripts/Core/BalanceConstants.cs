namespace OblastZero.Core
{
    /// <summary>
    /// Centralized balance and tuning constants. ALL gameplay numbers route through here.
    /// When designers want to retune, they edit ONE file. No magic numbers in system code.
    ///
    /// Convention: group by system. Use SCREAMING_SNAKE_CASE for true constants,
    /// PascalCase for runtime-tweakable static values (exposed via debug console later).
    /// </summary>
    public static class BalanceConstants
    {
        // ─── Run Economy ────────────────────────────────────────────────────────
        /// <summary>
        /// Fraction of unbanked expedition inventory that survives back to the Home Bunker
        /// when a run ends in death (vs. successful extraction). Range [0.0, 1.0].
        /// Design bible reference: Section 4 (Expedition Mechanics) — Darkest Dungeon corpse-recovery model.
        /// </summary>
        public const float SALVAGE_RATE_ON_DEATH = 0.33f;

        /// <summary>If true, artifact-class items always survive death regardless of salvage rate.</summary>
        public const bool ARTIFACTS_BYPASS_SALVAGE_LOSS = true;

        /// <summary>If true, consumables (food, water, meds) are wiped on death regardless of salvage rate.</summary>
        public const bool CONSUMABLES_LOST_ON_DEATH = true;

        // ─── Scavenge Phase (3D, Phase A) ───────────────────────────────────────
        public const float SCAVENGE_TIMER_SECONDS = 60f;
        public const float SCAVENGE_TIMER_WARNING_THRESHOLD = 15f;  // UI flashes red below this
        public const float SCAVENGE_TIMER_CRITICAL_THRESHOLD = 5f;  // Emission rumble begins
        /// <summary>
        /// Weight ceiling on what the player can haul out of the Blowout, in kg. Enforced by
        /// InventoryManager on the Scavenged channel — a pickup that would breach it is refused whole and
        /// the world object stays put. Tuned against the Collapsed Grain Depot, which holds roughly twice
        /// this in loot, so "what do I leave behind" is a live decision every run rather than a formality.
        /// </summary>
        public const int   SCAVENGE_MAX_CARRY_WEIGHT_KG = 15;
        public const float SCAVENGE_PICKUP_LERP_DURATION = 0.25f;   // SmoothDamp time for instant-pickup visual

        // ─── Bunker Phase (2D, Phase B) ─────────────────────────────────────────
        public const int   BUNKER_DAY_LENGTH_SECONDS = 0;           // 0 = turn-based, no real-time
        public const int   STARTING_BUNKER_MORALE = 60;
        public const int   STARTING_BUNKER_RADIATION_POOL = 0;

        // Per-day consumption per crew member
        public const int   DAILY_FOOD_PER_CREW = 1;
        public const int   DAILY_WATER_PER_CREW = 1;
        public const float STARVATION_HEALTH_LOSS_PER_DAY = 8f;
        public const float DEHYDRATION_HEALTH_LOSS_PER_DAY = 12f;
        public const float STARVATION_SANITY_LOSS_PER_DAY = 5f;

        // ─── Crew Stat Caps ─────────────────────────────────────────────────────
        public const int CREW_HEALTH_MAX = 100;
        public const int CREW_SANITY_MAX = 100;
        public const int CREW_FATIGUE_MAX = 100;
        public const int CREW_RADIATION_MAX = 100;

        public const int RADIATION_SICKNESS_THRESHOLD = 60;
        public const int SANITY_AFFLICTION_THRESHOLD = 25;

        // ─── Faction Reputation ─────────────────────────────────────────────────
        /// <summary>Lower bound for any tracked faction's reputation (matches the [-100,100] SO Range attributes).</summary>
        public const int REPUTATION_MIN = -100;
        /// <summary>Upper bound for any tracked faction's reputation.</summary>
        public const int REPUTATION_MAX = 100;

        // ─── Save System ────────────────────────────────────────────────────────
        public const string SAVE_FOLDER_NAME = "Saves";
        public const string PROFILE_SAVE_FILE = "profile.json";
        public const string EXPEDITION_SAVE_FILE = "expedition.json";
        public const string SAVE_BACKUP_SUFFIX = ".bak";
        public const int    MAX_SAVE_SLOTS = 3;

        // ─── Debug ──────────────────────────────────────────────────────────────
        public const bool VERBOSE_STATE_LOGGING = true;
        public const bool VERBOSE_SAVE_LOGGING = true;
        public const bool VERBOSE_EVENT_BUS_LOGGING = false;  // can be loud; off by default
    }
}
