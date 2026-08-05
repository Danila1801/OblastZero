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

        // ─── Meta-progression: Salvage Tokens ───────────────────────────────────
        // The between-run currency. Awarded by RunSummary.SalvageTokensAwarded, spent in the Supply Office
        // against MetaUnlockCatalog. The whole catalogue costs 275 tokens; the numbers below are set so that
        // reaching the day-15 endgame tenure pays roughly 30 from days alone, putting the cheap unlocks
        // inside one good run and the catalogue inside something under ten. Always something in reach.

        /// <summary>Tokens per day survived. The backbone of the award — tenure is the thing runs are scored on.</summary>
        public const float TOKENS_PER_DAY_SURVIVED = 2f;

        /// <summary>
        /// Tokens per recovered item. Deliberately a fraction: a haul should reward, not dominate. The count
        /// this multiplies is already salvage-adjusted (33% on a wipe, 100% on a win), so the death penalty
        /// flows through here rather than being applied a second time.
        /// </summary>
        public const float TOKENS_PER_ITEM_RECOVERED = 0.5f;

        /// <summary>
        /// Tokens per point of faction reputation, summed across all three factions. Negative standing
        /// subtracts, so a run that burned every bridge is paid less than one that stayed quiet — but the
        /// total award floors at zero, so it can never go negative.
        /// </summary>
        public const float TOKENS_PER_REPUTATION_POINT = 0.1f;

        /// <summary>Flat bonus for a run that reached any of the four endings.</summary>
        public const int TOKENS_VICTORY_BONUS = 50;

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
        /// <summary>
        /// Hard floor under the per-crew carry override, in kg. The live cap is the lead operator's
        /// authored <c>carryCapacityKg</c> (GameManager.BeginNewRun); this stops a misauthored or
        /// zeroed crew member from producing a pack that refuses every pickup, which in play reads as
        /// a broken pickup system rather than as a crew stat. Roughly half the baseline.
        /// </summary>
        public const int   SCAVENGE_MIN_CARRY_WEIGHT_KG = 8;
        public const float SCAVENGE_PICKUP_LERP_DURATION = 0.25f;   // SmoothDamp time for instant-pickup visual
        /// <summary>
        /// How far the crosshair reaches to grab a pickup, in metres. This is the shipped value that
        /// Scavenge.unity already serializes onto ScavengePlayerController and that the depot's shelf
        /// depths were laid out against — it is recorded here so the hover-highlight and the range ring
        /// read the same number the raycast does, not so the reach can be quietly retuned. Changing it
        /// changes which shelf items can be taken from the aisle.
        /// </summary>
        public const float SCAVENGE_INTERACTION_RANGE = 3f;

        // ─── Emission VFX (3D, Phase A) ─────────────────────────────────────────────────
        // The escalation thresholds are SCAVENGE_TIMER_WARNING_THRESHOLD and
        // SCAVENGE_TIMER_CRITICAL_THRESHOLD above; EmissionVfxController reads those directly rather
        // than declaring its own copies, so the siren, the HUD colour and the screen effects can never
        // disagree about when the panic starts.

        /// <summary>Seconds remaining at which camera shake begins. Later than the visual warning.</summary>
        public const float EMISSION_VFX_SHAKE_SECONDS = 10f;

        /// <summary>Camera shake amplitude in metres at the moment shake begins.</summary>
        public const float EMISSION_VFX_SHAKE_MIN_METRES = 0.02f;

        /// <summary>Camera shake amplitude in metres at zero seconds remaining.</summary>
        public const float EMISSION_VFX_SHAKE_MAX_METRES = 0.08f;

        /// <summary>Per-frame probability of a white flash frame at the warning threshold.</summary>
        public const float EMISSION_VFX_FLASH_CHANCE_AT_WARNING = 0.02f;

        /// <summary>Per-frame probability of a white flash frame at the critical threshold.</summary>
        public const float EMISSION_VFX_FLASH_CHANCE_AT_CRITICAL = 0.15f;

        /// <summary>Field of view the camera punches to when the emission lands, in degrees.</summary>
        public const float EMISSION_VFX_FOV_PUNCH_DEGREES = 75f;

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

        /// <summary>
        /// Reputation at or above which a faction's endgame branch opens. Bible §3 (reputation thresholds):
        /// "Going above +60 with any faction unlocks that faction's endgame branch."
        /// </summary>
        public const int ENDGAME_REPUTATION_THRESHOLD = 60;

        /// <summary>
        /// Reputation at or below which a faction treats the player as hunted. Bible §3: "Going below −60
        /// with any faction unlocks 'hunted' status." Read by the neutral ending, which requires that the
        /// player has antagonised nobody, not merely that they joined nobody.
        /// </summary>
        public const int HUNTED_REPUTATION_THRESHOLD = -60;

        // ─── Victory Conditions ─────────────────────────────────────────────────
        /// <summary>
        /// Days that must be survived before any faction endgame can fire. Bible §3 puts the alignment
        /// pivot at day fifteen ("in good standing with at most one faction at a time after day fifteen"),
        /// so a run cannot be won on reputation alone before the tenure exists to justify it.
        /// </summary>
        public const int ENDGAME_MIN_TENURE_DAYS = 15;

        /// <summary>
        /// Days that must be survived for the neutral ending. Longer than the faction tenure on purpose:
        /// bible §6.3 calls Independent a "rare neutral-ending branch", and refusing every faction means
        /// refusing their supply lines, so the price of independence is time.
        /// </summary>
        public const int INDEPENDENT_MIN_TENURE_DAYS = 25;

        // ─── Save System ────────────────────────────────────────────────────────
        public const string SAVE_FOLDER_NAME = "Saves";
        public const string PROFILE_SAVE_FILE = "profile.json";
        public const string EXPEDITION_SAVE_FILE = "expedition.json";
        /// <summary>
        /// Third channel: device-local player preferences (volume, display, key bindings, language).
        /// Separate from the profile channel because preferences describe the MACHINE, not the player's
        /// progression — putting them in the profile would push one machine's resolution and rebinds onto
        /// every other machine through Steam Cloud. See <c>PlayerPreferencesData</c>.
        /// </summary>
        public const string PREFERENCES_SAVE_FILE = "preferences.json";
        public const string SAVE_BACKUP_SUFFIX = ".bak";
        public const int    MAX_SAVE_SLOTS = 3;

        // ─── Content integrity tripwires ────────────────────────────────────────
        // Floors, not targets. The shipped set is ~711 items and ~1023 events (authored .asset content
        // plus the Resources JSON blitz); these sit well below that so ordinary content churn stays
        // quiet, while a JSON loader that silently returned nothing trips them immediately. A boot that
        // reports 8 items instead of 711 is otherwise indistinguishable from a working one until an
        // event tries to resolve an id that is not in the index.
        public const int CONTENT_MIN_EXPECTED_ITEMS = 600;
        public const int CONTENT_MIN_EXPECTED_EVENTS = 900;
        /// <summary>Below this the roster is unplayable — RunSetup has nobody to register as lead.</summary>
        public const int CONTENT_MIN_EXPECTED_CREW = 1;
        /// <summary>Scale Society, Cordon, Kafedra. Reputation is enum-driven and expects all three.</summary>
        public const int CONTENT_MIN_EXPECTED_FACTIONS = 3;

        // ─── Debug ──────────────────────────────────────────────────────────────
        public const bool VERBOSE_STATE_LOGGING = true;
        public const bool VERBOSE_SAVE_LOGGING = true;
        public const bool VERBOSE_EVENT_BUS_LOGGING = false;  // can be loud; off by default
    }
}
