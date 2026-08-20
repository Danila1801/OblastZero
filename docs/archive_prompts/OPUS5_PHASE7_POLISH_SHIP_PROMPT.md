# MASTER CLAUDE CODE PROMPT — OblastZero Phase 7: Final Polish + Steam Store + CI/CD + EA Ship
**Target:** Claude Code CLI (Opus 5, max effort), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** Phases 1-6 COMPLETE. Full game loop, 2 sites (1 built, 2 pending generators), audio, VFX, meta-progression, traits, anomalies, mutants, expeditions, artifacts, localization keys (orphaned), options menu, controller support, first-run tooltips. 142 C# scripts. All gates GREEN (verify_steam_layer 39/39, verify_prop_pipeline 93/93, content_qa PASS, csharp_string_qa PASS). Scene generator byte-deterministic. **What remains: 2 scene generators, localization wiring, Steam store page, CI/CD, one full Play-mode verification run, then ship.**

---

## CONTEXT: WHAT ACTUALLY EXISTS (verified by reading files, not assumptions)

| System | State | Evidence |
|---|---|---|
| Core framework (state machine, EventBus, ServiceLocator, GameManager, bifurcated save) | ✅ Built | Verified compiles, source read |
| Data schemas (ItemData, ExpeditionEventData, FactionData, AnomalyData, MutantData, CrewMemberData, TraitData, GameDatabase) | ✅ Built | 142 C# files under Assets/_Project/Scripts |
| Content instances | ✅ 703 items + 1020 events as JSON | File counts verified 5 Aug 2026 |
| Scenes & bootstrap rig | ✅ 3 scenes in Build Settings | _Bootstrap=0, Bunker=1, Scavenge=2 |
| Phase A — 3D scavenge (Collapsed Grain Depot) | ✅ Playable, generated | tools/generate_scavenge_scene.py owns it |
| Phase B — Bunker day loop + event engine | ✅ Complete | BunkerPhaseController, EventEngine, FactionReputationManager |
| Victory conditions (4 endings) | ✅ Wired and tested | VictoryConditionEvaluator, 4 RunVictory* states |
| Save/load (bifurcated, migration) | ✅ Working | DataLayerSmokeTest 24/24 |
| Steamworks integration | ✅ Built + compiles | Facepunch.Steamworks 2.x, Win64 only |
| Audio (Phase 3) | ✅ Built | AudioManager, ProceduralSfx (22 cues, zero audio files), FootstepAudio, OblastUIAudio |
| VFX + atmosphere (Phase 3) | ✅ Built | EmissionVfxController, ScreenShake, PickupVfx, ScavengeDustField, 7 volume overrides |
| Prop pipeline (4/11 GLB meshes) | ✅ 8/25 pickups dressed | verify_prop_pipeline.py 93/93 |
| Meta-progression | ✅ Built | MetaProgressData, MetaUnlockCatalog (10 unlocks), MetaUnlockUI |
| Crew trait system | ✅ Wired | TraitEffects resolver reads from DB, modifies consumption/event success/rep/carry |
| Region taxonomy (two axes) | ✅ Complete | RegionTags (10 locales) + OblastRegions (7 districts) orthogonal |
| Balance | ✅ Victory reachable | balance_analysis.py: 73-88% win rate, spread 15.4 pts |
| Anomalies (Carbon Copy, Interview, Backlog) | ✅ Built | 6 anomaly scripts, nav grid export, defective item logic |
| Mutants (Drowned Census-Taker, The Editor) | ✅ Built | 4 mutant scripts, A* on generated nav grid |
| Expedition system | ✅ Built | ExpeditionManager, ExpeditionUI |
| Artifact system | ✅ Built | ArtifactSystem, ArtifactUseUI (4 artifacts) |
| UI screens | ✅ All exist | 14 UI scripts including OptionsUI, ControllerNavigationUI, FirstRunTooltips |
| Localization keys | ⚠️ 73 keys LOADED but UNUSED | localization_en.json exists, NO UI code calls LocalizedStrings.Get() |

---

## WHAT IS NOT DONE (YOUR ACTUAL SCOPE)

### 1. **Two Scavenge Site Generators** — Census Office + Reservoir
The bible defines 3 sites. Only Grain Depot (`Scavenge.unity`) exists. `ScavengeSiteCatalog` has entries for `site_census_office` and `site_reservoir` with `IsBuilt = false`. Building either is: write a deterministic generator (following `generate_scavenge_scene.py` pattern with all 5+ validation gates) + flip `IsBuilt = true`.

### 2. **Localization Wiring** — Replace all hardcoded English strings
`LocalizedStrings` loads 73 keys from `localization_en.json` but **zero** UI code calls `LocalizedStrings.Get(key)`. Every screen has hardcoded English. Need to audit all 14 UI scripts, replace every player-facing string, expand the EN table to ~200 keys, create RU table, add language dropdown to OptionsUI.

### 3. **Steam Store Page Content** — Description, 20 achievements, 13 stats, screenshots plan
Write `docs/STEAM_STORE_PAGE.md`, `docs/STEAM_ACHIEVEMENTS.md`, `docs/STEAM_STATS.md`. Wire achievements/stats in SteamAchievementsService/SteamStatsService. Update SteamConfig with real App ID.

### 4. **CI/CD Pipeline** — GitHub Actions build + test on every push
Create `.github/workflows/ci.yml` running all QA gates (content_qa, csharp_string_qa, verify_prop_pipeline, generate_scavenge_scene --check, decimate_props --check, rebalance_weights --check, migrate_event_tags --check, verify_steam_layer.py with dotnet build).

### 5. **Full Play-Mode Verification Run** — The #1 blocker
Boot → MainMenu → RunSetup → Scavenge → TransitionCutscene → Bunker → End Day ×N → Wipe → RunFailed → MainMenu. **Zero console errors.** This has NEVER been done end-to-end. The Unity Editor must be driven live (MCP transport fix in CLAUDE.md §0).

### 6. **Editor-Only: SteamConfig Asset** — Cannot be scripted
Create `SteamConfig` asset in Unity Editor (`Assets → Create → OblastZero/Steam/Config`), set real App ID, assign to `Bootstrap.steamConfig`. This requires a human in Unity.

---

## TASK 1: CENSUS OFFICE SCENE GENERATOR

### Design (from bible + BESTIARY.md)
The Census District is bureaucratic — offices, filing rooms, flooded basement, narrow corridors, water hazards. Different from Grain Depot's industrial open spaces.

### Implementation
Create `tools/generate_census_scene.py` following the **exact pattern** of `generate_scavenge_scene.py`:
- **Coordinate plan in header comment** (reviewable artifact)
- **SceneBuilder from scavenge_scene_lib.py** (reuse YAML emitters)
- **25 pickups with real database IDs** — heavy on Documents, Medical, data-intel items, 1 crew (census clerk), 1 artifact near Interview anomaly
- **Flooded basement** — ankle-deep water (transparent plane, blue tint), slows player (0.6x speed via trigger)
- **Interview anomaly room** — larger inside than outside, desk + chair
- **All 5+ validation gates** (database IDs, YAML refs, OBB burial/support, walkability flood-fill + escapability, nav grid export)
- **Nav grid export** to `Assets/Data/Resources/Nav/navgrid_census.bytes` (same format as scavenge)
- **Anomaly zones** — Carbon Copy, Interview, Backlog placed per bestiary
- **MutantSpawner** — 1 Drowned Census-Taker (Census District), 15% Editor chance
- **Materials per archetype** — distinct colors for primitives
- **Clustering + tilt + scale spread** — same polish as depot

### Files to Create
- `tools/generate_census_scene.py`
- `tools/census_scene_lib.py` (or extend scavenge_scene_lib.py)
- `Assets/Scenes/CensusOffice.unity` (generated)

### Files to Modify
- `Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs` — set `site_census_office.IsBuilt = true`, `SceneName = "CensusOffice"`
- `Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs` — loads correct scene by site ID (already supports this)
- `Assets/_Project/Scripts/UI/RunSetupUI.cs` — shows Census Office as available (no unlock needed? Check MetaUnlockCatalog)
- `ProjectSettings/EditorBuildSettings.asset` — add CensusOffice at index 3

### Verify (AUTOMATED)
- `python tools/generate_census_scene.py` runs without error, all gates pass
- Scene regenerates **byte-identical** on second run
- `python tools/verify_steam_layer.py` 39/39
- `python tools/content_qa.py` PASS

### Verify (NEEDS HUMAN IN PLAY MODE)
- Scene loads additively, player spawns, can reach all 25 pickups and bunker
- Water slows player in basement
- Interview anomaly room exists, larger inside
- Drowned Census-Taker spawns and pursues via nav grid

---

## TASK 2: RESERVOIR SCENE GENERATOR

### Design (from bible + BESTIARY.md)
Abandoned municipal water reservoir — massive concrete basin, pump house, catwalk over water, flooded tunnels, dry control room (bunker entrance). Highest threat: 2 Drowned Census-Takers, Backlog anomaly in shortcut tunnel.

### Implementation
Create `tools/generate_reservoir_scene.py` — **same deterministic pattern**:
- **Layout:** pump house (interior), catwalk, flooded tunnels, control room
- **Water hazard:** deep water = 0.5x speed, shallow = splash FX
- **25 pickups** — medical (wet/contaminated), documents (waterlogged), weapons (submerged), 1 crew (drowned clerk survivor)
- **Backlog anomaly** covers flooded tunnel shortcut
- **2 Drowned Census-Takers** (highest threat)
- **Nav grid export** to `navgrid_reservoir.bytes`
- **All validation gates + negative controls**

### Files to Create
- `tools/generate_reservoir_scene.py`
- `Assets/Scenes/Reservoir.unity` (generated)

### Files to Modify
- `ScavengeSiteCatalog.cs` — `site_reservoir.IsBuilt = true`, `SceneName = "Reservoir"`
- `EditorBuildSettings.asset` — add Reservoir at index 4
- `RunSetupUI.cs` — shows 3 sites (Reservoir may need meta-unlock)

### Verify (AUTOMATED)
- `python tools/generate_reservoir_scene.py` — all gates pass, byte-identical regen
- `verify_steam_layer.py` 39/39
- `content_qa.py` PASS

### Verify (NEEDS HUMAN IN PLAY MODE)
- Scene loads, water slowdown works, 2 Census-Takers patrol
- Backlog anomaly in tunnel is lethal trap
- Catwalk navigation works on nav grid

---

## TASK 3: LOCALIZATION WIRING — ALL UI SCREENS

### Problem
`localization_en.json` has 73 keys. `LocalizedStrings.Get(key)` is **never called** in any UI script. All 14 UI screens use hardcoded English.

### Implementation

**1. Audit every UI script** (`Assets/_Project/Scripts/UI/*.cs`):
Find every player-facing string (button labels, titles, subtitles, tooltips, HUD text). For each:
- Check if key exists in `localization_en.json`
- If not, add it (expand to ~200 keys)
- Replace hardcoded string with `LocalizedStrings.Get("key")` or `string.Format(LocalizedStrings.Get("key"), args)`

**2. Expand `localization_en.json`** to cover all UI strings:
```json
{
  "menu.title": "OBLAST ZERO",
  "menu.subtitle": "Registered for demographic adjustment.",
  "menu.new_run": "New Run",
  "menu.continue": "Resume Filing",
  "menu.options": "Options",
  "menu.quit": "Close File",
  "menu.supply_office": "Supply Office",
  "run_setup.title": "FILING REQUISITION",
  "run_setup.site": "Scavenge Site",
  "run_setup.crew": "Crew Roster",
  "run_setup.confirm": "Submit Filing",
  "scavenge.timer": "EMISSION INBOUND",
  "scavenge.interact": "[E] Take",
  "scavenge.rescue": "[E] Rescue",
  "scavenge.weight": "LOAD {0:F1} / {1:F0} KG",
  "bunker.day": "DAY {0}",
  "bunker.end_day": "END DAY »",
  "event.choose": "Select Response",
  "summary.verdict_wipe": "REGISTRATION CLOSED",
  "options.audio": "AUDIO",
  "options.graphics": "GRAPHICS",
  "options.controls": "CONTROLS",
  "options.master": "Master Volume",
  "options.language": "Language",
  ...
}
```

**3. Create `localization_ru.json`** — Russian bureaucratic register (source language of the aesthetic). Every key translated.

**4. Wire language switching in `OptionsUI.cs`:**
- Language dropdown (English / Русский)
- On change: `LocalizedStrings.SetLanguage(code)`, fire `LocalizationChangedEvent` on EventBus
- All UI screens subscribe and rebuild text

**5. Update `LocalizedStrings.cs`:**
- Add `Get(key, params object[] args)` overload for format strings
- Add `LanguageChanged` event

### Files to Modify
- `Assets/Data/Resources/Locale/localization_en.json` — expand 73 → ~200 keys
- `Assets/Data/Resources/Locale/localization_ru.json` — create
- All 14 UI scripts in `Assets/_Project/Scripts/UI/` — replace hardcoded strings
- `Assets/_Project/Scripts/Core/LocalizedStrings.cs` — add Get(args), LanguageChanged event
- `Assets/_Project/Scripts/UI/OptionsUI.cs` — add language dropdown

### Verify (AUTOMATED)
- `python tools/verify_steam_layer.py` 39/39
- `grep -rn "LocalizedStrings.Get" Assets --include="*.cs"` shows all 14 UI screens using it
- `python tools/localization_qa.py` PASS (if exists, else create)

### Verify (NEEDS HUMAN IN PLAY MODE)
- English: all strings render correctly
- Russian: all strings render in Russian
- Switching language at runtime refreshes all visible UI
- No raw keys visible (e.g., "menu.title" never appears on screen)

---

## TASK 4: STEAM STORE PAGE + ACHIEVEMENTS + STATS

### Implementation

**1. Write `docs/STEAM_STORE_PAGE.md`** (Oblast voice — post-administrative, not post-apocalyptic):
- Short description (300 chars): "A 60-second scavenge. A 15-day survival. One filing. Oblast Zero is a roguelite of bureaucratic survival in the contaminated territories. Register. Scavenge. File. Survive. Or don't."
- Full description (5000 chars): game loop, features, EA disclaimer
- Tags: Survival, Roguelite, Bureaucratic Dystopia, Atmospheric, Story Rich, Single-player
- AI disclosure: Pre-Generated (per CLAUDE.md §10)
- Screenshot captions: 5-10 for Danil to capture

**2. Write `docs/STEAM_ACHIEVEMENTS.md`** — 20 achievements (Oblast voice, no spoilers):
| ID | Display Name | Description | Hidden | Icon |
|---|---|---|---|---|
| ACH_FIRST_FILING | Initial Registration | Complete your first scavenge run | No | Filing form with stamp |
| ACH_FIRST_SURVIVAL | Case Resolved | Survive to day 10 | No | Calendar with circled date |
| ACH_STABILIZATION | Alignment: Stabilization | Achieve Stabilization ending | No | Scale Society emblem |
| ACH_RELIEF | Alignment: Relief | Achieve Relief ending | No | Cordon emblem |
| ACH_ADAPTATION | Alignment: Adaptation | Achieve Adaptation ending | No | Kafedra emblem |
| ACH_INDEPENDENT | Alignment: Unaligned | Achieve Independent ending | No | Broken emblem |
| ACH_ALL_ENDINGS | Complete Archive | Unlock all four endings | No | Four emblems arranged |
| ACH_30_DAYS | Extended Filing | Survive to day 30 | No | Thick file folder |
| ACH_ZERO_MEDICAL | Resourceful | Win without using medical items | Yes | Empty medkit |
| ACH_NEUTRAL_WIN | Unaffiliated | Win Independent never joining a faction | Yes | Blank badge |
| ACH_ARTIFACT_HOARDER | Artifact Collector | Find 5 artifacts across runs | No | Glowing artifact |
| ACH_CARRY_MASTER | Logistics Specialist | Fill carry to 95%+ and survive | Yes | Weight scale |
| ACH_SPEED_RUN | Express Filing | Win in under 20 in-game days | Yes | Stopwatch |
| ACH_NO_DEATHS | Full Roster | Win with no crew deaths | Yes | Full crew photo |
| ACH_HUNTED | Designation: Hunted | Be hunted by all three factions simultaneously | Yes | Redacted stamp |
| ACH_BACKLOG_ESCAPE | Temporal Compliance | Escape Backlog anomaly without losing time | Yes | Clock with missing hand |
| ACH_INTERVIEW_SURVIVOR | Candid Response | Complete Interview anomaly and return unharmed | Yes | Chair and desk |
| ACH_EDITOR_PAPER | Final Draft | Obtain the Final Draft artifact | Yes | Sheet of paper with text |
| ACH_CENSUS_TAKER | Wet Clerk | Be registered by the Drowned Census-Taker | Yes | Fountain pen dripping |
| ACH_FULL_UNLOCK | Provisioning Complete | Purchase all meta-unlocks | No | Supply requisition form |

**3. Write `docs/STEAM_STATS.md`** — 13 stats:
```
stat_days_survived_total      | int   | cumulative
stat_longest_run_days         | int   | per-run best
stat_total_runs               | int   | cumulative
stat_total_wins               | int   | cumulative (all 4 endings)
stat_wins_by_stabilization    | int   | cumulative
stat_wins_by_relief           | int   | cumulative
stat_wins_by_adaptation       | int   | cumulative
stat_wins_by_independent      | int   | cumulative
stat_items_scavenged_total    | int   | cumulative
stat_crew_rescued_total       | int   | cumulative
stat_deaths_total             | int   | cumulative
stat_artifacts_found_total   | int   | cumulative
stat_salvage_tokens_earned    | int   | cumulative
```

**4. Wire in Steam services:**
- `SteamAchievementsService.cs` — trigger all 20 on correct events
- `SteamStatsService.cs` — increment all 13 on correct actions
- `SteamConfig.cs` — update with real App ID + all keys

### Files to Create
- `docs/STEAM_STORE_PAGE.md`
- `docs/STEAM_ACHIEVEMENTS.md`
- `docs/STEAM_STATS.md`

### Files to Modify
- `Assets/_Project/Scripts/Steam/SteamAchievementsService.cs`
- `Assets/_Project/Scripts/Steam/SteamStatsService.cs`
- `Assets/_Project/Scripts/Steam/SteamConfig.cs`
- `steam_appid.txt` — real App ID (Danil provides)

### Verify (AUTOMATED)
- `python tools/verify_steam_layer.py` 39/39
- `grep -rn "ACH_" Assets --include="*.cs"` shows all 20 wired
- Achievement triggers fire on correct events

---

## TASK 5: CI/CD PIPELINE — GITHUB ACTIONS

### Implementation
Create `.github/workflows/ci.yml`:

```yaml
name: OblastZero CI

on:
  push:
    branches: [ main, feat/* ]
  pull_request:
    branches: [ main ]

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
      with: { lfs: true }

    - name: Set up Python
      uses: actions/setup-python@v5
      with: { python-version: '3.11' }

    - name: Install Python dependencies
      run: pip install pyyaml pillow numpy

    - name: Content QA (IP firewall + voice + schema)
      run: python tools/content_qa.py --self-test && python tools/content_qa.py

    - name: C# string QA (player-facing string firewall)
      run: python tools/csharp_string_qa.py --self-test && python tools/csharp_string_qa.py

    - name: Prop pipeline verification
      run: python tools/verify_prop_pipeline.py --self-test && python tools/verify_prop_pipeline.py

    - name: Scene generation check (deterministic — no drift)
      run: |
        python tools/generate_scavenge_scene.py --check
        python tools/generate_census_scene.py --check
        python tools/generate_reservoir_scene.py --check

    - name: Prop decimation check (no drift)
      run: python tools/decimate_props.py --check

    - name: Weight balance check (idempotent)
      run: python tools/rebalance_weights.py --check

    - name: Event tag migration check (idempotent)
      run: python tools/migrate_event_tags.py --check

    - name: Balance analysis (report, not a gate)
      run: python tools/balance_analysis.py

    - name: Set up .NET
      uses: actions/setup-dotnet@v4
      with: { dotnet-version: '8.0.x' }

    - name: Steam layer + compile verification
      run: python tools/verify_steam_layer.py
```

Also create `tools/pre-commit` hook (fast gates only):
```bash
#!/bin/bash
echo "[pre-commit] Running OblastZero QA gates..."
python tools/content_qa.py --self-test 2>&1 | tail -1
python tools/csharp_string_qa.py --self-test 2>&1 | tail-1
```

### Files to Create
- `.github/workflows/ci.yml`
- `tools/pre-commit`
- `tools/install_hooks.sh`

### Verify
- Push triggers CI
- All gates pass in CI
- CI fails fast on any gate failure

---

## TASK 6: FULL PLAY-MODE VERIFICATION (HUMAN REQUIRED)

**This cannot be done by the agent.** The Unity Editor must be driven live.

### Prerequisites (Danil must do):
1. In Unity's *MCP For Unity* window: switch **Transport** off `HTTP Local` → use stdio transport
2. Launch Unity Editor from `C:\Users\danil\projects\OblastZero`
3. Create `SteamConfig` asset: `Assets → Create → OblastZero/Steam/Config`, set real App ID
4. Assign `SteamConfig` to `Bootstrap.steamConfig` in `_Bootstrap.unity`
5. Set `Application.runInBackground = true` at runtime (for agent-driven Play mode)

### Verification Checklist (run in Play mode, zero console errors):
- [ ] Boot `_Bootstrap.unity` → MainMenu renders
- [ ] NEW REGISTRATION → RunSetup shows 3 sites (1 built, 2 greyed if locked)
- [ ] Select Grain Depot → Scavenge loads, 25 pickups, HUD, timer
- [ ] Pick up items, carry weight enforced, HUD load bar updates
- [ ] Reach bunker entrance → TransitionCutscene plays
- [ ] Bunker loads → BunkerHUD renders day 1, crew, rations, factions
- [ ] END DAY → EventModal renders, choices work, consequences apply
- [ ] 15 consecutive days → autosave each day
- [ ] Wipe at day 16 → RunFailedState renders correct summary
- [ ] Return to MainMenu → RESUME FILING works (save/load round-trip)
- [ ] All 4 RunVictory* states reachable (trigger manually if needed)
- [ ] Options menu: volume, graphics, controls, language all work + persist
- [ ] First-run tooltips show on run 1, not on run 2
- [ ] Gamepad: navigate menus, play scavenge, interact with bunker
- [ ] Russian language: all UI renders in Russian
- [ ] Steam: stats increment, achievements unlock (with App ID 480 placeholder)
- [ ] Zero console errors, zero warnings

---

## EXECUTION ORDER AND COMMIT STRATEGY

Each task independently committable. Suggested order:

1. **Task 1** (Census Office generator) — content variety, enables mutant/anomaly testing
   - Commit: `feat: Census Office scavenge scene generator (census_district region, flooded basement, Interview anomaly)`

2. **Task 2** (Reservoir generator) — third site, highest threat
   - Commit: `feat: Reservoir scavenge scene generator (reservoir region, water hazards, 2 Census-Takers)`

3. **Task 3** (Localization) — reduces rework, needed for EA ship
   - Commit: `feat: full localization wiring — EN/RU across all 14 UI screens, language switching`

4. **Task 4** (Steam store page) — retail-facing, can parallelize with Task 5
   - Commit: `feat: Steam store page + 20 achievements + 13 stats + AI disclosure`

5. **Task 5** (CI/CD) — last, once all gates stable
   - Commit: `ci: GitHub Actions pipeline — all QA gates on every push`

### After Each Task
- `python tools/verify_steam_layer.py` — must be 39/39 green
- `python tools/content_qa.py` — must be zero violations
- `python tools/csharp_string_qa.py` — must be zero violations
- `git status` — **stage explicit paths only, NEVER `git add -A`** (concurrent sessions on this branch)
- `git commit` with message above
- `git push origin feat/scavenge-3d-scene`

---

## HARD RULES (from CLAUDE.md)

- **LangVersion 9.0** — no file-scoped namespaces, no C# 10+ syntax
- **No `// TODO`, no stubs** — complete implementations only
- **Balance numbers in `BalanceConstants`** — no magic numbers in system code
- **Namespace matches folder** — `OblastZero.<Layer>`
- **File name == primary type name**
- **Newtonsoft JSON** — never `JsonUtility`
- **All `RunData` mutation through managers** — nothing else writes those fields
- **Deterministic scene generation** — byte-deterministic, self-validating (5+ gates + negative controls)
- **IP firewall** — no S.T.A.L.K.E.R. names, locations, factions. The "Zone" is "the Oblast." Check `content_qa.py` and `csharp_string_qa.py` after changes.
- **AI disclosure** (CLAUDE.md §10): Pre-Generated for player-facing AI art/audio/narrative. Dev tools exempt.
- **Event-driven** — prefer raising an event over hard cross-system reference
- **Sample code in briefs** — if provided, must be type-checked against engine API. State perceptual invariants for DSP/numeric work, not just parameters.

---

## AFTER ALL TASKS

The game will have:
- 3 scavenge sites with distinct atmospheres, anomalies, mutants ✅
- Full localization (English + Russian) across ALL UI screens ✅
- Options menu (volume, graphics, controls, language) ✅
- 20 Steam achievements + 13 stats, all wired ✅
- Store page content ready for SteamPartner ✅
- First-run experience with contextual tooltips ✅
- Gamepad support for all menus and scavenge ✅
- CI/CD pipeline catching regressions on every push ✅
- **Full Play-mode verification complete** ✅

**The game is ready for Early Access release.**