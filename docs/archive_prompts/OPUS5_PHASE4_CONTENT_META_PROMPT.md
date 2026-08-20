# MASTER CLAUDE CODE PROMPT — OblastZero Phase 4: Content Depth + Balance + Meta-Progression + Second Scavenge Site

**Target:** Claude Code CLI (Opus 5, max effort), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** Phases 1-3 complete. 86+ C# scripts, 39/39 + 100/100 + 93/93 + 17/17 + 19/19 all GREEN. Audio/VFX/Polish done. Game boots, runs, can be won, looks good, sounds good. Now we need DEPTH.

---

## CONTEXT: WHAT EXISTS RIGHT NOW

| System | State |
|--------|-------|
| Core framework (state machine, EventBus, ServiceLocator, GameManager) | ✅ Complete |
| Data schemas (ItemData, ExpeditionEventData, FactionData, AnomalyData, MutantData, CrewMemberData, TraitData) | ✅ Complete |
| Content: 703 items, 1020 events | ✅ Loaded from JSON, verified |
| Scavenge phase (3D, 60s panic, 1 site) | ✅ Playable |
| Bunker phase (2D, day loop, events, factions) | ✅ Playable |
| Victory conditions (4 endings) | ✅ Wired and tested |
| Save/load (bifurcated, migration) | ✅ Working |
| Steam (achievements, stats, cloud) | ✅ Wired |
| Prop pipeline (4/11 GLB meshes) | ✅ 8/25 pickups dressed |
| Audio + VFX + Polish | ✅ Done in Phase 3 |
| UI (MainMenu, RunSetup, BunkerHUD, EventModal, ScavengeHUD, RunSummary) | ✅ All self-building |
| Meta-progression | ❌ NOT IMPLEMENTED |
| Second scavenge site | ❌ NOT IMPLEMENTED |
| Anomaly system (in scavenge) | ❌ NOT IMPLEMENTED |
| Mutant encounters (in scavenge) | ❌ NOT IMPLEMENTED |
| Trait system for crew | ❌ Schema exists but not wired |
| Event region tag migration | ❌ Events use wrong tags |
| Content balance for victory reachability | ❌ Unverified |

---

## TASK 1: META-PROGRESSION SYSTEM — PERSISTENT UNLOCKS ACROSS RUNS

### Problem
`MetaProgressData` exists as a schema and is saved/loaded, but it does NOTHING. The player has zero reason to run again. No unlocks, no progression, no meta-currency. The roguelite loop is broken: the "lite" part is missing.

### Design (from CLAUDE.md §6)
- `MetaProgressData` = persistent cross-run progression (loaded once, survives death)
- Saved in a separate channel from `RunData` (bifurcated save — already implemented)
- Death salvage rate: 33% (const in `BalanceConstants` — already exists)

### Implementation

**1. Meta-currency: "Salvage Tokens"**
- Earned at run end: `(days survived × 2) + (items recovered × 0.1) + (faction rep / 10 per faction) + victory bonus (50 tokens for any win)`
- On wipe: items are salvaged at 33% (existing constant) — these converted items become tokens
- On victory: ALL bunker inventory converts to tokens at 100%
- Tokens persist in `MetaProgressData`

**2. Persistent unlocks (purchase with tokens between runs, on the MainMenu):**

Create `Assets/_Project/Scripts/Core/MetaUnlocks.cs` (namespace `OblastZero.Core`):

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// Persistent unlocks purchased with Salvage Tokens between runs.
    /// Each unlock modifies the NEXT run's starting state.
    /// </summary>
    [System.Serializable]
    public class MetaUnlock
    {
        public string id;
        public string displayName;
        public string description;
        public int tokenCost;
        public bool purchased;
        // The effect is interpreted by GameManager.BeginNewRun + BunkerPhaseController
        public UnlockEffect effect;
    }

    [System.Serializable]
    public class UnlockEffect
    {
        // One or more of these may be set per unlock
        public int bonusStartingRations;
        public int bonusStartingMedical;
        public float bonusCarryWeightKg;
        public int bonusStartingCrewHealth;
        public int bonusStartingCrewSanity;
        public float startingReputationScaleSociety;
        public float startingReputationCordon;
        public float startingReputationKafedra;
        public int extraScavengeSite; // unlocks a new site at index
        public int startingEnemyIntelLevel; // reveals more info on HUD
        public bool unlockSecondCrewPick; // pick 2 crew instead of 1
    }

    public static class MetaUnlockCatalog
    {
        // The catalog — read by the unlock UI and by BeginNewRun
        public static readonly MetaUnlock[] Catalog = new MetaUnlock[]
        {
            new MetaUnlock
            {
                id = "unlock_extra_rations",
                displayName = "Field Supply Cache",
                description = "Begin each run with 3 additional rations.",
                tokenCost = 10,
                effect = new UnlockEffect { bonusStartingRations = 3 },
            },
            new MetaUnlock
            {
                id = "unlock_medkit",
                displayName = "Medical Resupply",
                description = "Begin each run with 1 field medkit.",
                tokenCost = 15,
                effect = new UnlockEffect { bonusStartingMedical = 1 },
            },
            new MetaUnlock
            {
                id = "unlock_carry_boost",
                displayName = "Reinforced Packs",
                description = "+3 kg carry capacity for all crew.",
                tokenCost = 20,
                effect = new UnlockEffect { bonusCarryWeightKg = 3f },
            },
            new MetaUnlock
            {
                id = "unlock_crew_health",
                displayName = "Conditioning Protocol",
                description = "All crew start with +10 health.",
                tokenCost = 25,
                effect = new UnlockEffect { bonusStartingCrewHealth = 10 },
            },
            new MetaUnlock
            {
                id = "unlock_crew_sanity",
                displayName = "Psychological Screening",
                description = "All crew start with +10 sanity.",
                tokenCost = 25,
                effect = new UnlockEffect { bonusStartingCrewSanity = 10 },
            },
            new MetaUnlock
            {
                id = "unlock_rep_society",
                displayName = "Bureau Liaison Credential",
                description = "Start runs at +10 reputation with Scale Society.",
                tokenCost = 30,
                effect = new UnlockEffect { startingReputationScaleSociety = 10 },
            },
            new MetaUnlock
            {
                id = "unlock_rep_cordon",
                displayName = "Cordon Field Manual",
                description = "Start runs at +10 reputation with Cordon.",
                tokenCost = 30,
                effect = new UnlockEffect { startingReputationCordon = 10 },
            },
            new MetaUnlock
            {
                id = "unlock_rep_kafedra",
                displayName = "Kafedra Research Note",
                description = "Start runs at +10 reputation with Kafedra.",
                tokenCost = 30,
                effect = new UnlockEffect { startingReputationKafedra = 10 },
            },
            new MetaUnlock
            {
                id = "unlock_second_crew",
                displayName = "Squad Authorization",
                description = "Deploy with 2 crew members instead of 1.",
                tokenCost = 50,
                effect = new UnlockEffect { unlockSecondCrewPick = true },
            },
            new MetaUnlock
            {
                id = "unlock_second_site",
                displayName = "Reconnaissance Report: Flooded Census Office",
                description = "Unlocks a second scavenge site: the Flooded Census Office (Census District).",
                tokenCost = 40,
                effect = new UnlockEffect { extraScavengeSite = 1 },
            },
        };
    }
}
```

**3. Wire into MetaProgressData:**

Update `Assets/_Project/Scripts/Core/MetaProgressData.cs`:
- Add `int salvageTokens` field
- Add `List<string> purchasedUnlockIds` field (persisted)
- Add `SalvageTokens` property (get/set with dirty flag)
- Add `IsPurchased(string unlockId)` / `Purchase(string unlockId)` methods
- `Purchase` checks token balance, deducts, adds to purchased list, returns true/false
- Apply `UnlockEffect` values in `GameManager.BeginNewRun` after creating the RunData

**4. Unlock UI in MainMenu:**

Add a "PROVISIONING" or "SUPPLY OFFICE" button to `MainMenuUI.cs` that opens a new `MetaUnlockUI.cs` (namespace `OblastZero.UI`, self-building canvas):
- Shows current token balance
- Lists all unlocks in `MetaUnlockCatalog.Catalog`
- Purchased items are marked and greyed out
- Affordable items show "PURCHASE" button
- Locked items (too expensive) show cost in red
- Purchasing calls `MetaProgressData.Purchase(id)` and saves immediately
- "BACK" button returns to MainMenu

**5. Apply unlocks in BeginNewRun:**

Update `Assets/_Project/Scripts/Core/GameManager.cs` in `BeginNewRun`:
```csharp
var meta = MetaProgressData.Current;
if (meta != null)
{
    foreach (var unlockId in meta.purchasedUnlockIds)
    {
        var unlock = System.Array.Find(MetaUnlockCatalog.Catalog, u => u.id == unlockId);
        if (unlock == null) continue;
        ApplyUnlockEffect(unlock.effect, run, inventory, crew, rep);
    }
}
```

### Files to Create
- `Assets/_Project/Scripts/Core/MetaUnlocks.cs`
- `Assets/_Project/Scripts/UI/MetaUnlockUI.cs`

### Files to Modify
- `Assets/_Project/Scripts/Core/MetaProgressData.cs` — add token + unlock tracking
- `Assets/_Project/Scripts/Core/GameManager.cs` — ApplyUnlockEffects, salvage token calculation at run end
- `Assets/_Project/Scripts/Core/States/RunFailedState.cs` — award tokens on wipe (salvage)
- `Assets/_Project/Scripts/Core/States/RunEndVictoryStateBase.cs` — award tokens on victory
- `Assets/_Project/Scripts/UI/MainMenuUI.cs` — add "Supply Office" button
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — add salvage token formula constants

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- After a run, MetaProgressData has salvage tokens
- MainMenu shows Supply Office button
- Purchasing an unlock deducts tokens and persists
- Next run applies the unlock effect (bonus rations, carry weight, etc.)
- Save/load preserves purchased unlocks + token balance

---

## TASK 2: SECOND SCAVENGE SITE — "FLOODED CENSUS OFFICE"

### Problem
The game has only 1 scavenge site: "Collapsed Grain Depot" (Outer Cordon). For a roguelite, 1 site is not enough. The player needs variety. The design bible mentions "The Census District" as a distinct region — use it.

### Design (from BESTIARY.md and bible §2)
The Census District is different from the Grain Belt:
- **Grain Depot:** warehouse, silos, loading dock — industrial, large open spaces, grain spills
- **Census Office:** bureaucratic building — offices, filing rooms, flooded basement, narrow corridors, water hazards

### Implementation

**1. Create scene generator for Census Office:**

Create `tools/generate_census_scene.py` (based on `generate_scavenge_scene.py` patterns):
- A different layout: 2-floor office building with a flooded basement
- Floor 1: reception area, filing room (rows of shelves with documents), interview room (anomaly reference), stairwell down
- Basement: flooded corridors (ankle-deep water — use a transparent plane with slight blue tint), storage room, boiler room
- Narrower corridors (2m wide vs 4m in the depot)
- Offices are smaller rooms (4×4m) with desks (cubicles — cube primitives with scale 1.2×0.05×0.6 as desk top, plus 0.8m legs)
- Filing shelves along walls (tall thin bookshelves — cube 0.4×2.0×3.0)
- The bunker entrance is in the basement — a heavy door at the end of the flooded corridor

**2. Pickups for Census Office (25 pickups):**
- Heavy on Documents (region = census_district), Medical, and data-intel items
- Fewer weapons, more medical + utility items
- 1 crew member rescue (different crew — a census clerk who survived)
- 1 artifact (spawns near the Interview anomaly location)
- Water in the basement obscures some pickups (they're on the floor in shallow water)

**3. Update ScavengeSiteCatalog:**
```csharp
public static readonly ScavengeSite[] Sites = new ScavengeSite[]
{
    new ScavengeSite {
        id = "site_grain_depot",
        displayName = "Collapsed Grain Depot",
        region = "outer_cordon",
        description = "Soviet grain processing facility on the defunct rail line. Large open spaces, rusted shelving, grain silos.",
        requiresUnlock = null, // always available
    },
    new ScavengeSite {
        id = "site_census_office",
        displayName = "Flooded Census Office",
        region = "census_district",
        description = "Regional census bureau, partially submerged. Filing rooms, interview chambers, flooded basement archive.",
        requiresUnlock = "unlock_second_site", // meta-unlock from Task 1
    },
};
```

**4. Scene loading:**
- `ScavengePhase3DState` must load the scene based on the selected site ID
- Check `RunData.currentScavengeSiteId` and load the appropriate scene additively
- Add `site_census_office` to Build Settings (index 3)

**5. Environmental hazards:**
- The flooded basement slows the player (CharacterController speed multiplier 0.6x when in water — detect via trigger collider covering the water plane area)
- The Interview room (from BESTIARY.md §2 anomaly) is a special room: if the player enters and sits at the desk, the screen fades and an event fires (but this is a LATER task — for now, just build the room)

### Files to Create
- `tools/generate_census_scene.py` — scene generator (follows the same deterministic, validated pattern)
- `tools/census_scene_lib.py` — YAML emitters shared between scenes (or extend `scavenge_scene_lib.py`)
- `Assets/Scenes/CensusOffice.unity` — generated by the script

### Files to Modify
- `Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs` — add second site
- `Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs` — load scene based on selected site
- `Assets/_Project/Scripts/UI/RunSetupUI.cs` — show 2 sites (greyed if locked)
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — add census-specific constants (water speed multiplier, etc.)
- `ProjectSettings/EditorBuildSettings.asset` — add CensusOffice scene at index 3

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_census_scene.py` generates the scene
- RunSetup shows 2 sites: Grain Depot (always) + Census Office (if unlocked)
- Selecting Census Office loads the correct scene with different layout
- Water slows the player in the basement
- 25 pickups use real item IDs from the database
- Scene passes the same walkability/burial/support validations as the depot

---

## TASK 3: CREW TRAIT SYSTEM — TRAITS THAT AFFECT EVENTS AND STATS

### Problem
`TraitData` schema exists, `CrewMemberData.traitIds` exists, `RunData` ships trait lists, but NOTHING reads traits. They're cosmetic data with zero mechanical effect. The event engine doesn't check traits. The crew manager doesn't apply trait modifiers. Imagine playing a roguelite where perks are listed but do nothing.

### Design (from bible §4 — crew traits)

The bible defines traits that affect events, stats, and interactions. Each trait is a persistent modifier.

### Implementation

**1. Create `Assets/_Project/Scripts/OblastZero.Gameplay/TraitSystem.cs`:**

```csharp
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Applies persistent crew trait effects during the bunker phase.
    /// Traits modify daily consumption, event success chances, and special abilities.
    /// </summary>
    public static class TraitSystem
    {
        // Trait IDs — must match the authored TraitData.id values
        public const string TRAIT_STEADY = "trait_steady";       // -20% sanity loss rate
        public const string TRAIT_PARANOID = "trait_paranoid";    // +15% event detection, +10% sanity loss
        public const string TRAIT_SUPERSTITIOUS = "trait_superstitious"; // +20% anomaly resistance, -10% Kafedra rep gain
        public const string TRAIT_RESILIENT = "trait_resilient";  // +15% health regen, -10% radiation gain
        public const string TRAIT_OBSERVANT = "trait_observant";  // reveals extra event info, detects traps
        public const string TRAIT_DEFERRED = "trait_deferred";    // +10% reputation gains
        public const string TRAIT_FIELD_HARDENED = "trait_field_hardened"; // +20% combat success
        public const string TRAIT_BUREAUCRAT = "trait_bureaucrat"; // +20% Scale Society rep gain
        public const string TRAIT_SCAVENGER = "trait_scavenger";  // +3kg carry capacity
        public const string TRAIT_MEDIC = "trait_medic";         // medical events +20% success, injuries heal faster

        /// <summary>
        /// Applied once per day in BunkerDayController, before consumption.
        /// Modifies the consumption amounts for this crew member.
        /// </summary>
        public static void ModifyDailyConsumption(CrewInstance crew, ref float hungerRate, ref float thirstRate, ref float fatigueRate, ref float radiationRate)
        {
            if (crew == null || crew.Data == null) return;
            var traits = crew.Data.traitIds;
            if (traits == null) return;

            foreach (var traitId in traits)
            {
                switch (traitId)
                {
                    case TRAIT_RESILIENT:
                        radiationRate *= 0.85f; // -15% radiation gain
                        break;
                    case TRAIT_FIELD_HARDENED:
                        fatigueRate *= 0.90f; // -10% fatigue
                        break;
                    case TRAIT_PARANOID:
                        fatigueRate *= 1.10f; // +10% fatigue (worrying)
                        break;
                }
            }
        }

        /// <summary>
        /// Applied during event resolution. Modifies the success chance for this crew member.
        /// </summary>
        public static float ModifyEventSuccessChance(CrewInstance crew, float baseChance)
        {
            if (crew == null || crew.Data == null) return baseChance;
            float chance = baseChance;
            var traits = crew.Data.traitIds;
            if (traits == null) return chance;

            foreach (var traitId in traits)
            {
                switch (traitId)
                {
                    case TRAIT_FIELD_HARDENED:
                        chance += 0.20f; break;
                    case TRAIT_MEDIC:
                        // Only +20% on medical events — caller can check event type
                        // For now, apply globally as a small bonus
                        chance += 0.05f; break;
                    case TRAIT_PARANOID:
                        chance += 0.10f; break;
                }
            }
            return Mathf.Clamp01(chance);
        }

        /// <summary>
        /// Applied when reputation changes. Scales the delta for this crew member's influence.
        /// </summary>
        public static float ModifyReputationGain(CrewInstance crew, FactionId faction, float delta)
        {
            if (crew == null || crew.Data == null) return delta;
            var traits = crew.Data.traitIds;
            if (traits == null) return delta;

            foreach (var traitId in traits)
            {
                switch (traitId)
                {
                    case TRAIT_DEFERRED:
                        delta *= 1.10f; break;
                    case TRAIT_BUREAUCRAT when faction == FactionId.ScaleSociety:
                        delta *= 1.20f; break;
                    case TRAIT_SUPERSTITIOUS when faction == FactionId.Kafedra:
                        delta *= 0.90f; break;
                }
            }
            return delta;
        }

        /// <summary>
        /// Applied to carry capacity at run start.
        /// </summary>
        public static float ModifyCarryCapacity(CrewInstance crew, float baseCapacity)
        {
            if (crew == null || crew.Data == null) return baseCapacity;
            var traits = crew.Data.traitIds;
            if (traits == null) return baseCapacity;

            float capacity = baseCapacity;
            foreach (var traitId in traits)
            {
                switch (traitId)
                {
                    case TRAIT_SCAVENGER:
                        capacity += 3f; break;
                }
            }
            return capacity;
        }
    }
}
```

**2. Wire into BunkerDayController:**
- In the daily tick, before applying consumption, call `TraitSystem.ModifyDailyConsumption(crew, ref hunger, ref thirst, ref fatigue, ref radiation)`
- The modified rates are what get applied

**3. Wire into EventEngine:**
- In `EventEngine.Resolve`, after computing `baseChance` from `successChanceFormula` (or the numeric `successChance`), call `TraitSystem.ModifyEventSuccessChance(actingCrew, chance)`
- This must happen BEFORE the RNG roll

**4. Wire into FactionReputationManager:**
- In the method that applies reputation changes, call `TraitSystem.ModifyReputationGain(actingCrew, faction, delta)` on the delta before applying

**5. Wire into GameManager.BeginNewRun:**
- After resolving the lead crew's carry capacity, call `TraitSystem.ModifyCarryCapacity(lead, crewCap)`

**6. Create TraitData .asset files:**
- Create `Assets/Data/Definitions/Traits/Trait_*.asset` for each of the 10 traits
- Use the `[CreateAssetMenu]` menu from the existing schema
- Fields: id, displayName, description (for the UI tooltip in RunSetup)

**7. Add trait display to RunSetupUI:**
- Under each crew member's stat block, show their traits as labeled badges
- "TRAITS: Steady, Resilient" etc.

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/TraitSystem.cs`
- `Assets/Data/Definitions/Traits/Trait_Steady.asset`
- `Assets/Data/Definitions/Traits/Trait_Paranoid.asset`
- `Assets/Data/Definitions/Traits/Trait_Superstitious.asset`
- `Assets/Data/Definitions/Traits/Trait_Resilient.asset`
- `Assets/Data/Definitions/Traits/Trait_Observant.asset`
- `Assets/Data/Definitions/Traits/Trait_Deferred.asset`
- `Assets/Data/Definitions/Traits/Trait_FieldHardened.asset`
- `Assets/Data/Definitions/Traits/Trait_Bureaucrat.asset`
- `Assets/Data/Definitions/Traits/Trait_Scavenger.asset`
- `Assets/Data/Definitions/Traits/Trait_Medic.asset`

### Files to Modify
- `Assets/_Project/Scripts/OblastZero.Gameplay/BunkerDayController.cs` — ModifyDailyConsumption
- `Assets/_Project/Scripts/OblastZero.Gameplay/EventEngine.cs` — ModifyEventSuccessChance
- `Assets/_Project/Scripts/OblastZero.Gameplay/FactionReputationManager.cs` — ModifyReputationGain
- `Assets/_Project/Scripts/Core/GameManager.cs` — ModifyCarryCapacity
- `Assets/_Project/Scripts/UI/RunSetupUI.cs` — trait badges display

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `grep -rn "TraitSystem" Assets --include="*.cs"` shows the system wired into 4 systems
- Running with a crew member with TRAIT_RESILIENT shows reduced radiation accumulation
- Running with TRAIT_FIELD_HARDENED shows increased event success chances (log the modified chance)
- RunSetup crew display shows trait names

---

## TASK 4: EVENT REGION TAG MIGRATION — USE BIBLE REGIONS, NOT GENERIC TAGS

### Problem
The 1020 events use generic region tags like `abandoned_school`, `old_factory`, `warehouse_floor` — tags invented during content generation that don't match the design bible's 7 canonical regions. This means the region-based event filtering (which controls which events appear in which phase/context) is using a vocabulary that doesn't match the game's world design.

### Bible Regions (from BESTIARY.md §Region Tags)

| Bible Region | Suggested tag |
|---|---|
| The Outer Cordon | `outer_cordon` |
| The Census District | `census_district` |
| The Reservoir | `reservoir` |
| The Grain Belt | `grain_belt` |
| The Bureau Quarter | `bureau_quarter` |
| The Inner Ring | `inner_ring` |
| The Threshold | `threshold` |

### Implementation

Create `tools/migrate_event_tags.py`:

```python
"""
Migrates event JSON region tags from generated-generic to bible-canonical.
Tag mapping is derived from the bestiary + design bible, not invented.

Before running: back up Assets/Data/Resources/Events/
After running: run `python tools/content_qa.py` to verify no IP violations
were introduced (the migration tool only changes tags, but verify anyway).

The migration is idempotent: running twice produces the same result.
Events already using canonical tags are unchanged.
"""
```

**Mapping table:**

```python
TAG_MIGRATION = {
    # Generic generated tags → bible canonical
    "abandoned_school": "census_district",      # census = records/bureaucracy/education
    "old_factory": "grain_belt",                 # factory = industrial/agricultural
    "warehouse_floor": "grain_belt",
    "grain_depot": "grain_belt",
    "silo": "grain_belt",
    "rail_siding": "grain_belt",
    "loading_dock": "grain_belt",
    "office": "bureau_quarter",
    "admin_office": "bureau_quarter",
    "bunker_interior": "outer_cordon",           # the bunker is in the outer cordon
    "bunker_approach": "outer_cordon",
    "reservoir_approach": "reservoir",
    "water_crossing": "reservoir",
    "inner_city": "inner_ring",
    "downtown": "bureau_quarter",
    "threshold_crossing": "threshold",
    # Tags that are already canonical — pass through
    "outer_cordon": "outer_cordon",
    "census_district": "census_district",
    "reservoir": "reservoir",
    "grain_belt": "grain_belt",
    "bureau_quarter": "bureau_quarter",
    "inner_ring": "inner_ring",
    "threshold": "threshold",
}
```

**RegionTags.cs update:**
Update `Assets/_Project/Scripts/Core/RegionTags.cs` to mirror the bible's 7 regions. Keep `BunkerPhaseActive` as a composite (a set of tags that apply during the bunker phase):
```csharp
public static class RegionTags
{
    // Bible canonical regions
    public const string OuterCordon = "outer_cordon";
    public const string CensusDistrict = "census_district";
    public const string Reservoir = "reservoir";
    public const string GrainBelt = "grain_belt";
    public const string BureauQuarter = "bureau_quarter";
    public const string InnerRing = "inner_ring";
    public const string Threshold = "threshold";

    // Composite: tags active during bunker phase (interior + approaches)
    public static readonly string[] BunkerPhaseActive = {
        OuterCordon, CensusDistrict, GrainBelt,
    };

    // Composite: all bible regions
    public static readonly string[] All = {
        OuterCordon, CensusDistrict, Reservoir, GrainBelt,
        BureauQuarter, InnerRing, Threshold,
    };
}
```

### Files to Create
- `tools/migrate_event_tags.py`

### Files to Modify
- `Assets/_Project/Scripts/Core/RegionTags.cs` — rewrite to bible canonical
- `Assets/Data/Resources/Events/*.json` — migrated by the script (1020 files)

### Verify
- `python tools/migrate_event_tags.py --dry-run` shows the migration plan
- `python tools/migrate_event_tags.py` performs it
- `python tools/content_qa.py` passes with zero violations
- The event pool size per day changes (events now filtered by biblical regions, which may be broader or narrower)
- `BunkerEventReachabilityTest` still 9/9

---

## TASK 5: BALANCE PASS — MAKE VICTORY REACHABLE IN PLAY

### Problem
The `VictoryConditionEvaluator` requires a faction at +60 reputation by day 15 (or neutral unhunted by a longer tenure). The code is correct. But nobody has verified that the AUTHORED CONTENT can actually push a faction to +60 by day 15. If events only give +2-3 rep per choice, and there are ~15 days, that's 30-45 rep max — not enough. The game is unwinnable in practice even though the win code is correct.

### Implementation

**1. Create a balance analysis tool:**

Create `tools/balance_analysis.py`:

```python
"""
Analyzes whether the authored content can produce a victory scenario.
For each faction, measures:
- Maximum achievable reputation by day 15 (best case)
- Average reputation gain per event
- Events that give reputation for each faction
- Whether the faction endgame threshold (+60) is reachable

This is NOT a test — it's a diagnostic that prints a report.
The balance is a design decision. This tool gives you the data to make it.
"""
```

The tool should:
- Parse all 1020 event JSONs
- Extract every choice's effects (reputation changes, faction targets)
- Compute: per-faction, how many events give positive reputation, average delta, max delta
- Simulate a best-case run: 15 days, 1 event per day, pick the choice that maximizes one faction
- Report whether +60 is achievable for each faction
- Report the "independent" path: can you survive 25+ days at neutral rep?

**2. Run the analysis and adjust content:**

After running the balance tool, you will likely find one of two problems:
- **A) Rep gains are too small** → increase reputation deltas in events (multiply all rep deltas by a factor, or add high-rep events)
- **B) Rep gains are too easy** → clamp down (reduce some deltas)

Add a `rebalance_reputation.py` tool that can:
- Scale all reputation deltas by a factor (per faction)
- Add minimum/maximum clamps per choice
- Be idempotent (`--check` mode)

**3. Adjust BalanceConstants:**

Review and adjust:
- `ENDGAME_REPUTATION_THRESHOLD` — is +60 right? Maybe +50 is more achievable
- `ENDGAME_MIN_TENURE_DAYS` — is day 15 right? Maybe 12 is better
- `INDEPENDENT_MIN_TENURE_DAYS` — is it too long?
- `HUNTED_REPUTATION_THRESHOLD` — is -70 too punishing?

Adjust these based on the analysis. Document the reasoning in `BalanceConstants` comments.

### Files to Create
- `tools/balance_analysis.py`
- `tools/rebalance_reputation.py`

### Files to Modify
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — adjust thresholds
- `Assets/Data/Resources/Events/*.json` — if rep deltas need to change (via rebalance_reputation.py)

### Verify
- `python tools/balance_analysis.py` prints a clear report
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/content_qa.py` passes (no content violations from rep rebalancing)
- `VictoryAndResumeTest` still passes (test explicitly sets rep, so it verifies the eval, not the content)
- Report: best-case rep per faction by day 15, and whether each ending is reachable

---

## EXECUTION ORDER AND COMMIT STRATEGY

1. **Task 4** (region tag migration) — foundational, affects event pools for everything else
   - Commit: `fix: migrate event region tags to bible-canonical regions + update RegionTags.cs`

2. **Task 3** (trait system) — adds mechanical depth
   - Commit: `feat: crew trait system — traits modify consumption, event success, rep gain, carry capacity`

3. **Task 1** (meta-progression) — the roguelite loop
   - Commit: `feat: meta-progression — salvage tokens, persistent unlocks, supply office UI`

4. **Task 5** (balance) — make victory reachable
   - Commit: `balance: reputation rebalancing + threshold tuning for victory reachability`

5. **Task 2** (second site) — content variety
   - Commit: `feat: second scavenge site — Flooded Census Office (census_district region)`

### After Each Task
- Run `python tools/verify_steam_layer.py` — must be 39/39 green
- Run `python tools/content_qa.py` — must be zero violations
- `git status` — stage explicit paths only, NEVER `git add -A`
- `git commit` with the message above
- Push: `git push origin feat/scavenge-3d-scene`

### Hard Rules (from CLAUDE.md)
- **LangVersion 9.0** — no file-scoped namespaces, no C# 10+ syntax
- **No `// TODO`, no stubs** — complete implementations only
- **Balance numbers in `BalanceConstants`** — no magic numbers in system code
- **Namespace matches folder** — `OblastZero.<Layer>`
- **File name == primary type name**
- **Newtonsoft JSON** — never `JsonUtility`
- **All `RunData` mutation through managers** — nothing else writes those fields
- **Deterministic scene generation** — if you create a new scene generator, it must be byte-deterministic and self-validating (same pattern as `generate_scavenge_scene.py`)
- **IP firewall** — no S.T.A.L.K.E.R. names, locations, factions. The "Zone" is "the Oblast." Check `content_qa.py` and `csharp_string_qa.py` after changes.

### After All Five Tasks
The game will have:
- Meta-progression: salvage tokens + 10 persistent unlocks + Supply Office UI ✅
- 2 scavenge sites: Grain Depot + Flooded Census Office ✅
- Crew traits that mechanically affect gameplay ✅
- Bible-canonical region tags across all 1020 events ✅
- Balance-tuned content where victory is provably reachable ✅

The game will be a *real* roguelite, not just a roguelike with a single path.
