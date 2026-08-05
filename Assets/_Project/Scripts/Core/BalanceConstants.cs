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

        // ─── Phase A hazards: anomalies (bible §5) ──────────────────────────────
        // The three anomalies are the signature mechanic, and each is tuned around a different decision.
        // Carbon Copy: "is this the crate I already took?" Interview: "is the reward worth the unknown?"
        // Backlog: "is the shortcut still a shortcut?" The numbers below exist to keep those three
        // questions live; a value that makes any of them obvious has been mistuned.

        /// <summary>
        /// Copies one Carbon Copy zone will produce before it stops. The bible's own figure ("players grab
        /// 3-4 copies"). Uncapped it is an item printer — stand in the volume and mine one crate until the
        /// carry cap intervenes — and the cap is what keeps it a trick rather than an exploit. Four is also
        /// roughly what fits in the seconds a player spends before the clock pulls them off it, so the
        /// ceiling rarely announces itself.
        /// </summary>
        public const int CARBON_COPY_MAX_DUPLICATES = 4;

        /// <summary>
        /// Player speed inside a Backlog, as a fraction of normal. The bible specifies subjective time at
        /// 40×–100× slower; 0.02 is the 50× midpoint. At 4.5 m/s walk that is 9 cm/s, so crossing a 6 m
        /// corridor costs about 67 seconds against a 60-second clock — which is the point. Entering with
        /// the clock low is meant to be unsurvivable, not merely expensive.
        /// </summary>
        public const float BACKLOG_TIME_DILATION_FACTOR = 0.02f;

        /// <summary>
        /// Seconds of enforced delay between grabs inside a Backlog. The bible slows *interaction* as well
        /// as movement, and without this a player could stand at the boundary and harvest a shelf at full
        /// speed while only their feet were slowed.
        /// </summary>
        public const float BACKLOG_INTERACTION_DELAY_SECONDS = 2.5f;

        /// <summary>
        /// Pitch multiplier applied to the ambient and music beds inside a Backlog. A perfect fourth down.
        /// The emission siren is deliberately excluded (see AudioManager.SetTemporalDrag): the whole
        /// mechanic is that the clock does not slow, and pitching the siren would tell the player's ear
        /// otherwise.
        /// </summary>
        public const float BACKLOG_AUDIO_PITCH_FACTOR = 0.75f;

        /// <summary>Seconds the screen takes to fade to black when the player sits for the Interview.</summary>
        public const float INTERVIEW_FADE_SECONDS = 1.5f;

        /// <summary>
        /// Sanity charged to the operator for consenting to the correction of their file. The consent
        /// branch pays the Stamped Tongue — a one-time override of any Scale Society event — so it has to
        /// cost something the player feels for the rest of the run rather than being a free pick.
        /// </summary>
        public const int INTERVIEW_CONSENT_SANITY_COST = 12;

        // ─── Carbon Copy defects, felt in Phase B ───────────────────────────────
        // The copy's cost lands when a crew member uses it, days after the grab that seemed free. Each
        // defect is a wrong detail rather than a broken object — the bible's register — and each is sized
        // to be worse than not having the item at all, because otherwise taking the copy is still correct.

        /// <summary>Health lost when a defective med kit's syringes turn out to hold the wrong fluid.</summary>
        public const int DEFECT_MEDICAL_HEALTH_PENALTY = 15;

        /// <summary>Sanity lost by the crew member who administered it and then read the ampoule.</summary>
        public const int DEFECT_MEDICAL_SANITY_PENALTY = 8;

        /// <summary>Chance a defective ration's contents do not match its label. Half the time it is food.</summary>
        public const float DEFECT_FOOD_POISONING_CHANCE = 0.5f;

        public const int DEFECT_FOOD_HEALTH_PENALTY = 10;
        public const int DEFECT_FOOD_FATIGUE_PENALTY = 12;

        /// <summary>
        /// Standing lost with the Scale Society for filing a document whose countersignature could not have
        /// been made. They are the faction that reads signatures, so they are the faction that notices.
        /// </summary>
        public const int DEFECT_DOCUMENT_REPUTATION_PENALTY = 8;

        /// <summary>Chance a defective weapon's misaligned sights turn a success into a failure.</summary>
        public const float DEFECT_WEAPON_FAILURE_CHANCE = 0.25f;

        // ─── Phase A hazards: Geiger detection ──────────────────────────────────

        /// <summary>Metres at which a carried Geiger counter starts clicking at a detectable anomaly.</summary>
        public const float GEIGER_DETECTION_RANGE_M = 14f;

        /// <summary>Seconds between clicks at the boundary of a detectable volume.</summary>
        public const float GEIGER_CLICK_PERIOD_NEAR = 0.14f;

        /// <summary>Seconds between clicks at the edge of detection range.</summary>
        public const float GEIGER_CLICK_PERIOD_FAR = 1.1f;

        /// <summary>Gap between the two halves of the bible's characteristic double-click, in seconds.</summary>
        public const float GEIGER_DOUBLE_CLICK_GAP = 0.06f;

        public const float GEIGER_CLICK_VOLUME = 0.42f;

        /// <summary>Click pitch. Well above the UI cue it borrows, so it does not read as a menu sound.</summary>
        public const float GEIGER_CLICK_PITCH = 1.9f;

        /// <summary>Seconds between re-checks of whether the player is carrying a counter.</summary>
        public const float GEIGER_INVENTORY_POLL_SECONDS = 1.5f;

        // ─── Phase A hazards: mutants (bible §5) ────────────────────────────────

        /// <summary>Census-Taker move speed, m/s. Walking pace — slower than the player's 4.5 m/s walk,
        /// so it can always be outpaced and never outrun forever.</summary>
        public const float CENSUS_TAKER_MOVE_SPEED = 1.2f;

        /// <summary>Metres within which the Census-Taker begins following.</summary>
        public const float CENSUS_TAKER_AGGRO_RANGE_M = 12f;

        /// <summary>
        /// Seconds the player must be near-stationary, in line of sight, before registration starts.
        /// The bible's figure. Long enough that ordinary looting does not trip it, short enough that
        /// standing still to read the HUD does.
        /// </summary>
        public const float CENSUS_TAKER_STOP_THRESHOLD_SECONDS = 10f;

        /// <summary>Speed below which the player counts as stopped, m/s.</summary>
        public const float CENSUS_TAKER_STOP_SPEED_MS = 0.5f;

        /// <summary>Seconds to complete an entry once writing begins. Moving interrupts it.</summary>
        public const float CENSUS_TAKER_REGISTRATION_SECONDS = 15f;

        /// <summary>Metres the Census-Taker must be within to write. Clipboard range.</summary>
        public const float CENSUS_TAKER_WRITING_RANGE_M = 3.5f;

        /// <summary>Health lost per completed registration. Stacks; permanent for the run.</summary>
        public const int REGISTRATION_HEALTH_PENALTY = 10;

        /// <summary>Sanity lost per completed registration. Stacks; permanent for the run.</summary>
        public const int REGISTRATION_SANITY_PENALTY = 5;

        /// <summary>Seconds of continuous line of sight before the Editor redacts an item label.</summary>
        public const float EDITOR_REDACT_AFTER_SECONDS = 3f;

        /// <summary>Seconds of continuous line of sight before the Editor deletes an item.</summary>
        public const float EDITOR_DELETE_AFTER_SECONDS = 6f;

        /// <summary>Seconds of continuous line of sight before the Editor substitutes an item.</summary>
        public const float EDITOR_REPLACE_AFTER_SECONDS = 10f;

        /// <summary>
        /// Seconds the player must keep the Editor out of sight before exposure decays. Non-zero so a
        /// player cannot defeat it by flicking the camera away for a single frame each second.
        /// </summary>
        public const float EDITOR_LOOK_AWAY_GRACE_SECONDS = 1.25f;

        /// <summary>Seconds the Editor spends reading a dropped document before resuming.</summary>
        public const float EDITOR_DISTRACTION_SECONDS = 5f;

        /// <summary>Seconds out of sight after which the Editor disappears entirely.</summary>
        public const float EDITOR_DESPAWN_AFTER_SECONDS = 3f;

        /// <summary>Base chance an Editor appears at all during one Blowout, before the site's threat multiplier.</summary>
        public const float EDITOR_BASE_SPAWN_CHANCE = 0.15f;

        /// <summary>Metres from the player the Editor materialises. Visible, well outside reach.</summary>
        public const float EDITOR_SPAWN_DISTANCE_M = 18f;

        // ─── Expeditions (Phase B) ──────────────────────────────────────────────

        /// <summary>Shortest expedition, in bunker days.</summary>
        public const int EXPEDITION_MIN_DAYS = 3;

        /// <summary>Longest expedition, in bunker days.</summary>
        public const int EXPEDITION_MAX_DAYS = 5;

        /// <summary>Most expeditions that can be in flight at once. A bunker that empties itself starves.</summary>
        public const int EXPEDITION_MAX_CONCURRENT = 2;

        /// <summary>Loadout slots offered when dispatching. Small enough that the choice is a real one.</summary>
        public const int EXPEDITION_MAX_LOADOUT_ITEMS = 3;

        /// <summary>Items a routine expedition brings home, before region and loadout modifiers.</summary>
        public const int EXPEDITION_BASE_ITEM_YIELD = 2;

        /// <summary>Extra items per loadout slot filled. Sending kit out is how you get more back.</summary>
        public const float EXPEDITION_YIELD_PER_LOADOUT_ITEM = 0.75f;

        /// <summary>Fatigue accrued per day in the field. Charged in full on return.</summary>
        public const int EXPEDITION_FATIGUE_PER_DAY = 6;

        /// <summary>Radiation accrued per day in the field, before the Notarized Heart's halving.</summary>
        public const int EXPEDITION_RADIATION_PER_DAY = 3;

        /// <summary>Chance a returning expedition is delayed by a Backlog. Adds 1-3 days.</summary>
        public const float EXPEDITION_BACKLOG_DELAY_CHANCE = 0.12f;

        /// <summary>Chance the Editor rewrites part of a returning pack.</summary>
        public const float EXPEDITION_EDITOR_EDIT_CHANCE = 0.10f;

        /// <summary>Chance a Census-Taker registers a crew member in the field. Applies the same penalties.</summary>
        public const float EXPEDITION_REGISTRATION_CHANCE = 0.14f;

        /// <summary>Chance a crew member does not come back at all. Deliberately low — permadeath already bites.</summary>
        public const float EXPEDITION_LOSS_CHANCE = 0.05f;

        // ─── Artifact uses (bible artifacts table) ──────────────────────────────

        /// <summary>In-game days between Margin Note re-rolls. One per week, per the bible.</summary>
        public const int MARGIN_NOTE_COOLDOWN_DAYS = 7;

        /// <summary>Radiation multiplier for the crew member holding a Notarized Heart. Bible: -50%.</summary>
        public const float NOTARIZED_HEART_RADIATION_MULTIPLIER = 0.5f;

        /// <summary>Highest value Final Draft can rewrite a stat to. Prevents a stat above its own ceiling.</summary>
        public const int FINAL_DRAFT_MAX_STAT_VALUE = 100;

        /// <summary>Lowest value Final Draft can write. Zero health would be a rewrite into a corpse.</summary>
        public const int FINAL_DRAFT_MIN_STAT_VALUE = 1;

        // ─── Debug ──────────────────────────────────────────────────────────────
        public const bool VERBOSE_STATE_LOGGING = true;
        public const bool VERBOSE_SAVE_LOGGING = true;
        public const bool VERBOSE_EVENT_BUS_LOGGING = false;  // can be loud; off by default
    }
}
