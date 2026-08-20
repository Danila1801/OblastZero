# MASTER CLAUDE CODE PROMPT — OblastZero Phase 5: Steam Store Page + Demo Build + CI/CD + Final Polish

**Target:** Claude Code CLI (Opus 5, max effort), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** Phases 1-4 complete. Full game loop, audio, VFX, 2 scavenge sites, meta-progression, traits, balanced content. Now we SHIP.

---

## CONTEXT: WHAT EXISTS RIGHT NOW

The game is feature-complete:
- 2 scavenge sites (Grain Depot + Census Office), 60s panic each
- Bunker phase with 1020 events, 3 factions, 4 victory endings
- Meta-progression with 10 unlocks, salvage token economy
- Crew traits with mechanical effects
- Audio (procedural SFX + ambient + stingers)
- VFX (emission escalation, pickup feedback, post-processing)
- Steam (achievements, stats, cloud save)
- Save/load (bifurcated, migration, pendingEventId)
- 4/11 GLB prop meshes (7 still primitives — acceptable for EA ship, polish later)

### What's Missing (YOUR SCOPE)
1. **No Steam store page content** — description, tags, screenshots, trailer
2. **No demo build configuration** — build settings, scene list, platform target
3. **No CI/CD pipeline update** — GitHub Actions build + test on push
4. **No options menu** — volume controls, graphics quality, key binding
5. **No localization** — keys are orphaned, UI renders raw keys not translated text
6. **No Steam rich presence** — "Playing Oblast Zero: Day 7, Grain Depot"
7. **No controller support** — keyboard/mouse only
8. **No tutorial/first-run experience** — player is dropped in with no guidance

---

## TASK 1: STEAM STORE PAGE CONTENT + STEAMCMD DEPLOY

### Problem
The game needs a Steam store page. The `steam_deploy.sh` script exists in `tools/` but may need updating. The store page needs a description, tags, screenshots, and trailer preparation.

### Implementation

**1. Store page description (write to `docs/STEAM_STORE_PAGE.md`):**

Write the full Steam store page description in Oblast voice. Follow the CLAUDE.md §9 voice rules: post-administrative, not post-apocalyptic. Soviet bureaucratic register. No pulp clichés. The Oblast does not raise its voice. The Oblast files a form.

Structure:
- **Short description** (300 chars max): "A 60-second scavenge. A 15-day survival. One filing. Oblast Zero is a roguelite of bureaucratic survival in the contaminated territories. Register. Scavenge. File. Survive. Or don't."
- **Full description** (5000 chars): expand with game loop description, features list, EA disclaimer
- **Tags**: Survival, Roguelite, Post-apocalyptic (no — "Bureaucratic Dystopia"), Atmospheric, Story Rich, Single-player
- **AI disclosure**: Pre-Generated (per CLAUDE.md §10 — dev tools exempt, but player-facing AI art/audio needs disclosure)
- **Screenshot captions**: 5-10 captions for screenshots Danil will capture

**2. Steam app ID configuration:**
- Update `steam_appid.txt` with the real app ID (currently 480 = placeholder)
- Update `Assets/_Project/Scripts/Steam/SteamConfig.cs` with real achievement/stat keys
- Update `tools/steam_deploy.sh` if needed

**3. Steam achievements catalog (write to `docs/STEAM_ACHIEVEMENTS.md`):**

Design 20 achievements following these rules:
- No spoilers in achievement names (use Oblast voice: bureaucratic, understated)
- Mix of progression (first run, first win, each ending), collection (find all artifacts), and challenge (win with neutral rep, win without using medkits, survive 30 days)
- Each achievement has: id, display name, description, hidden? (yes/no), icon description

Example achievements:
```
ACH_FIRST_FILING      | "Initial Registration"     | Complete your first scavenge run.          | No  | Filing form with stamp
ACH_FIRST_SURVIVAL    | "Case Resolved"             | Survive to day 10.                          | No  | Calendar with circled date
ACH_STABILIZATION     | "Alignment: Stabilization"  | Achieve the Stabilization ending.           | No  | Scale Society emblem
ACH_RELIEF            | "Alignment: Relief"         | Achieve the Relief ending.                  | No  | Cordon emblem
ACH_ADAPTATION        | "Alignment: Adaptation"     | Achieve the Adaptation ending.              | No  | Kafedra emblem
ACH_INDEPENDENT       | "Alignment: Unaligned"      | Achieve the Independent ending.             | No  | Broken emblem
ACH_ALL_ENDINGS       | "Complete Archive"          | Unlock all four endings.                     | No  | Four emblems arranged
ACH_30_DAYS            | "Extended Filing"           | Survive to day 30.                           | No  | Thick file folder
ACH_ZERO_MEDICAL      | "Resourceful"                | Win a run without using any medical items.  | Yes | Empty medkit
ACH_NEUTRAL_WIN       | "Unaffiliated"               | Win with the Independent ending having      | Yes | Blank badge
                             never joined any faction.
ACH_ARTIFACT_HOARDER  | "Artifact Collector"         | Find 5 artifacts across runs.                | No  | Glowing artifact
ACH_CARRY_MASTER      | "Logistics Specialist"       | Fill your carry capacity to 95%+            | Yes | Weight scale
                             and survive the run.
ACH_SPEED_RUN         | "Express Filing"             | Win a run in under 20 in-game days.          | Yes | Stopwatch
ACH_NO_DEATHS         | "Full Roster"                | Win a run with no crew deaths.               | Yes | Full crew photo
ACH_HUNTED            | "Designation: Hunted"        | Be hunted by all three factions              | Yes | Redacted stamp
                             simultaneously.
ACH_BACKLOG_ESCAPE    | "Temporal Compliance"        | Escape the Backlog anomaly without          | Yes | Clock with missing hand
                             losing time.
ACH_INTERVIEW_SURVIVOR| "Candid Response"            | Complete the Interview anomaly              | Yes | Chair and desk
                             and return unharmed.
ACH_EDITOR_PAPER      | "Final Draft"                | Obtain the Final Draft artifact.            | Yes | Sheet of paper with text
ACH_CENSUS_TAKER      | "Wet Clerk"                  | Be registered by the Drowned                 | Yes | Fountain pen dripping
                             Census-Taker. (Achievement, not penalty.)
ACH_FULL_UNLOCK       | "Provisioning Complete"      | Purchase all meta-unlocks.                   | No  | Supply requisition form
```

**4. Steam stats catalog:**
Define the stats that track across runs (supplement what already exists):
```
stat_days_survived_total      | int   | cumulative
stat_longest_run_days         | int   | per-run best
stat_total_runs               | int   | cumulative
stat_total_wins               | int   | cumulative (count = all 4 endings)
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

### Files to Create
- `docs/STEAM_STORE_PAGE.md`
- `docs/STEAM_ACHIEVEMENTS.md`
- `docs/STEAM_STATS.md`

### Files to Modify
- `Assets/_Project/Scripts/Steam/SteamAchievementsService.cs` — wire all 20 achievements
- `Assets/_Project/Scripts/Steam/SteamStatsService.cs` — wire all 13 stats
- `Assets/_Project/Scripts/Steam/SteamConfig.cs` — update with real app ID + all keys
- `steam_appid.txt` — real app ID (Danil provides this)
- `tools/steam_deploy.sh` — update if SteamCMD path or config changed

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `grep -rn "ACH_" Assets --include="*.cs"` shows all 20 achievement IDs wired
- Achievement trigger logic fires on the right events (run end, ending unlock, artifact pickup, etc.)
- Stats increment on the right actions

---

## TASK 2: OPTIONS MENU — VOLUME + GRAPHICS + KEY BINDING

### Problem
The game has no options menu. No volume controls, no quality settings, no key rebinding. Players expect these. Steam reviews ding games without basic options.

### Implementation

Create `Assets/_Project/Scripts/UI/OptionsUI.cs` (namespace `OblastZero.UI`):

**1. Volume Controls:**
- Master, SFX, Music, Ambient sliders (0-100, default 80/80/70/60)
- Read/write via `AudioManager` (created in Phase 3) using `AudioMixer.SetFloat`
- Persist to `MetaProgressData.audioSettings` (add a serializable struct)

**2. Graphics Quality:**
- Dropdown: Low / Medium / High (maps to `QualitySettings.SetQualityLevel`)
- Toggle: VSync (Application.targetFrameRate / QualitySettings.vSyncCount)
- Dropdown: Resolution (available resolutions from `Screen.resolutions`)
- Dropdown: Fullscreen mode (FullScreenWindow / ExclusiveFullScreen / Windowed)
- Persist to `MetaProgressData.graphicsSettings`

**3. Key Binding (scavenge controls):**
- Display current bindings: Forward, Backward, Left, Right, Sprint, Interact, Flashlight (if implemented)
- Click a binding to rebind → next key press becomes the new binding
- "RESET TO DEFAULTS" button
- Persist to `MetaProgressData.keyBindings` (Dictionary<string, int> action→keyCode)
- The ScavengePlayerController reads from these bindings instead of hardcoded keys

**4. UI Integration:**
- Add "OPTIONS" button to `MainMenuUI`
- Add "OPTIONS" button to in-game pause (if one exists; if not, add a simple pause menu triggered by Escape)
- Options screen: tabbed UI (Audio / Graphics / Controls)
- Built using the shared `OblastUI` vocabulary (same as all other screens)
- "BACK" button returns to previous screen

### Files to Create
- `Assets/_Project/Scripts/UI/OptionsUI.cs`
- `Assets/_Project/Scripts/UI/PauseMenuUI.cs` (if no pause exists)

### Files to Modify
- `Assets/_Project/Scripts/UI/MainMenuUI.cs` — add "OPTIONS" button
- `Assets/_Project/Scripts/Core/MetaProgressData.cs` — add audioSettings, graphicsSettings, keyBindings
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — read from key bindings
- `Assets/_Project/Scripts/OblastZero.Gameplay/AudioManager.cs` — expose volume control methods

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- Options menu opens from MainMenu
- Volume sliders change audio immediately
- Quality dropdown changes render quality
- Key rebinding works and persists
- Pause menu works in scavenge phase (Escape key)
- All settings persist across sessions (saved in meta channel)

---

## TASK 3: LOCALIZATION — WIRE 73 ORPHANED KEYS TO ALL UI SCREENS

### Problem
`LocalizedStrings` has 73 keys loaded from `localization_en.json`, but NO C# UI code calls `LocalizedStrings.Get(key)`. Every screen has hardcoded English strings. The localization system is dead infrastructure. Changing language would do nothing because no one reads the table.

### Implementation

**1. Audit all UI strings:**

Search every `.cs` file in `Assets/_Project/Scripts/UI/` for hardcoded strings (button labels, titles, subtitles, tooltips). For each:
- Identify whether it should be localized (player-facing = yes, debug = no)
- Find the matching key in `localization_en.json` (or add a new key if none exists)
- Replace the hardcoded string with `LocalizedStrings.Get("key")`

**2. Expand the localization table:**

Update `Assets/Data/Resources/Locale/localization_en.json` to cover every UI string. Current: 73 keys. Need: probably 150-200 (every button, label, title, tooltip, error message).

Key naming convention:
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
  "run_setup.crew_stats.health": "Health",
  "run_setup.crew_stats.sanity": "Sanity",
  "run_setup.crew_stats.carry": "Carry Capacity",
  "run_setup.crew_traits": "Traits",
  
  "scavenge.timer": "EMISSION INBOUND",
  "scavenge.interact": "[E] Take",
  "scavenge.rescue": "[E] Rescue",
  "scavenge.weight": "LOAD {0:F1} / {1:F0} KG",
  "scavenge.over_capacity": "OVER CAPACITY — {0} NOT LOGGED",
  "scavenge.crew_rescued": "{0} RESCUED",
  
  "bunker.day": "DAY {0}",
  "bunker.end_day": "END DAY »",
  "bunker.rations": "Rations {0}",
  "bunker.rations_days": "Rations {0} (~{1}d)",
  "bunker.crew": "CREW",
  "bunker.factions": "STANDING",
  "bunker.inventory": "BUNKER STORES",
  
  "event.title_pending": "DEVATION REGISTERED",
  "event.choose": "Select Response",
  "event.success": "RESOLVED — {0}",
  "event.failure": "REJECTED — {0}",
  
  "summary.verdict_wipe": "REGISTRATION CLOSED",
  "summary.verdict_stabilization": "CONDITION STABILISED",
  "summary.verdict_relief": "RELIEF COLUMN ARRIVED",
  "summary.verdict_adaptation": "ADAPTATION RECORDED",
  "summary.verdict_independent": "STATUS: INDEPENDENT",
  "summary.days_survived": "Days Survived",
  "summary.crew_lost": "Crew Lost",
  "summary.items_recovered": "Items Recovered",
  "summary.salvage_rate": "Salvage Rate",
  "summary.faction_standing": "Faction Standing",
  "summary.return": "Return to Registry",
  
  "options.audio": "AUDIO",
  "options.graphics": "GRAPHICS",
  "options.controls": "CONTROLS",
  "options.master": "Master Volume",
  "options.sfx": "Sound Effects",
  "options.music": "Music",
  "options.ambient": "Ambient",
  "options.quality": "Quality",
  "options.resolution": "Resolution",
  "options.fullscreen": "Display Mode",
  "options.vsync": "V-Sync",
  "options.reset": "Reset to Defaults",
  "options.back": "Back",
  
  "unlock.tokens": "SALVAGE TOKENS: {0}",
  "unlock.purchased": "ACQUIRED",
  "unlock.purchase": "REQUISITION",
  "unlock.insufficient": "INSUFFICIENT",
  "unlock.back": "Back",
  
  "pause.title": "FILING SUSPENDED",
  "pause.resume": "Resume",
  "pause.options": "Options",
  "pause.quit": "Abandon Filing"
}
```

**3. Wire all UI screens to use `LocalizedStrings.Get`:**

For each UI script (`MainMenuUI`, `RunSetupUI`, `BunkerHUD`, `EventModalUI`, `ScavengeHUD`, `RunSummaryUI`, `OptionsUI`, `MetaUnlockUI`, `PauseMenuUI`):
- Replace every hardcoded English string with `LocalizedStrings.Get("key")`
- For format strings, use `string.Format(LocalizedStrings.Get("key"), arg0, arg1)`
- For strings that aren't in the table, add them to `localization_en.json`
- Debug.Log strings stay hardcoded (not player-facing)

**4. Create a Russian localization table (for EA ship):**

Create `Assets/Data/Resources/Locale/localization_ru.json` with all keys translated to Russian. Use the Oblast voice: Russian bureaucratic register ( Russian register is natural — it's the source language of the aesthetic).

**5. Language selection in Options:**

Add a language dropdown to the Options → Audio or a new "Language" tab:
- English / Русский (Russian)
- Changing language immediately refreshes all UI (call a `LocalizationChangedEvent` on EventBus, all UI screens subscribe and rebuild)

### Files to Create
- `Assets/Data/Resources/Locale/localization_ru.json`

### Files to Modify
- `Assets/Data/Resources/Locale/localization_en.json` — expand from 73 to ~200 keys
- ALL UI scripts in `Assets/_Project/Scripts/UI/` — replace hardcoded strings
- `Assets/_Project/Scripts/Core/LocalizedStrings.cs` — add `Get(key, args)` overload for format strings, add `LanguageChanged` event
- `Assets/_Project/Scripts/UI/OptionsUI.cs` — add language dropdown

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `grep -rn "LocalizedStrings.Get" Assets --include="*.cs"` shows all UI screens using localization
- Running with English: all strings render correctly
- Running with Russian: all strings render in Russian
- Switching language at runtime refreshes all visible UI
- No raw key strings visible (e.g., "menu.title" should never appear on screen)

---

## TASK 4: CI/CD PIPELINE — GITHUB ACTIONS BUILD + TEST ON PUSH

### Problem
There's a GitHub Actions workflow for SteamCMD deploy, but no CI pipeline that builds and tests on every push. Every bug found in Phase 1 and 2 was found manually. A CI gate would catch compile errors, content QA failures, and verify gate regressions automatically.

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
      with:
        lfs: true
    
    - name: Set up Python
      uses: actions/setup-python@v5
      with:
        python-version: '3.11'
    
    - name: Install Python dependencies
      run: |
        pip install pyyaml pillow numpy
    
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
    
    - name: Prop decimation check (no drift)
      run: python tools/decimate_props.py --check
    
    - name: Weight balance check (idempotent)
      run: python tools/rebalance_weights.py --check
    
    - name: Event tag migration check (idempotent)
      run: python tools/migrate_event_tags.py --check
    
    - name: Balance analysis (report, not a gate)
      run: python tools/balance_analysis.py
    
    # Steam layer verification requires dotnet — install it
    - name: Set up .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Steam layer + compile verification (39 checks + dotnet build)
      run: python tools/verify_steam_layer.py
  
  build:
    needs: verify
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
    - uses: actions/checkout@v4
      with:
        lfs: true
    
    # Unity build — requires Unity license, skip for now but document the manual step
    - name: Build Notice
      run: |
        echo "::notice::Unity build must be done locally (requires Unity license + Editor)."
        echo "::notice::Run: Build > Standalone Windows 64-bit from Unity Editor."
```

### Add a pre-commit hook (optional, local):

Create `tools/install_hooks.sh`:
```bash
#!/bin/bash
# Install a pre-commit hook that runs the content QA gates
cp tools/pre-commit .git/hooks/pre-commit
chmod +x .git/hooks/pre-commit
```

Create `tools/pre-commit`:
```bash
#!/bin/bash
echo "[pre-commit] Running OblastZero QA gates..."
python tools/content_qa.py --self-test 2>&1 | tail -1
python tools/csharp_string_qa.py --self-test 2>&1 | tail -1
# Fast checks only — skip dotnet build (too slow for pre-commit)
```

### Files to Create
- `.github/workflows/ci.yml`
- `tools/install_hooks.sh`
- `tools/pre-commit`

### Verify
- Push triggers CI
- All gates pass in CI (the same gates that pass locally)
- CI fails fast on any gate failure (no silent green on broken content)

---

## TASK 5: FIRST-RUN EXPERIENCE + CONTROLLER SUPPORT

### Problem
1. A new player is dropped into MainMenu with no guidance. What's a "Scavenge Site"? What's "Carry Capacity"? What are "Traits"?
2. Only keyboard+mouse is supported. No gamepad. Steam surveys show ~30% of players use controllers.

### Implementation

**1. First-run tooltip overlay:**

Create `Assets/_Project/Scripts/UI/FirstRunTooltips.cs` (namespace `OblastZero.UI`):

A simple system that tracks `MetaProgressData.totalRuns`. If it's 0:
- Show a "FIRST REGISTRATION" banner on MainMenu
- Add contextual tooltips on the RunSetup screen:
  - "Select a scavenge site. Each has different risks and rewards."
  - "Choose your crew. Their traits affect events and survival."
  - "Carry capacity limits how much you can haul. Choose wisely."
- On entering the scavenge phase for the first time, show a 5-second "CONTROLS" overlay:
  - "WASD: Move | MOUSE: Look | SHIFT: Sprint | E: Pick up | ESC: Pause"
- On entering the bunker for the first time, show:
  - "END DAY advances time. Each day, your crew consumes rations."
  - "Events require decisions. Choose carefully — consequences are permanent."
- These only show on the first run. Second run onward: nothing.

**2. Gamepad support via Unity Input System:**

The project uses the Input System (`InputSystem_Actions.inputactions` exists). Add gamepad mapping:

Update `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs`:
- Replace `Keyboard.current` / `Mouse.current` polling with `InputAction` references
- Map: Left Stick = Move, Right Stick = Look, A = Interact, B = Sprint, Start = Pause
- Mouse look stays as default; right stick adds controller look

Create `Assets/_Project/Scripts/UI/ControllerNavigationUI.cs`:
- Handles D-pad / left stick navigation of UI buttons
- Button select → A to confirm
- Works on MainMenu, RunSetup, Options, Bunker buttons, EventModal choices
- Auto-selects the first button in each screen

### Files to Create
- `Assets/_Project/Scripts/UI/FirstRunTooltips.cs`
- `Assets/_Project/Scripts/UI/ControllerNavigationUI.cs`

### Files to Modify
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — InputAction-based controls
- `Assets/_Project/Scripts/UI/OblastUI.cs` — add controller navigation to button creation
- `Assets/_Project/Scripts/Core/MetaProgressData.cs` — `totalRuns` (may already exist)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- First run shows tooltips. Second run doesn't.
- Gamepad: can navigate menus, select items, play scavenge, interact with bunker
- Mouse still works (no regression)

---

## EXECUTION ORDER AND COMMIT STRATEGY

1. **Task 3** (localization) — reduces rework on other tasks (UI strings ready before adding new screens)
   - Commit: `feat: wire 73+ orphaned localization keys to all UI screens + Russian translation`

2. **Task 2** (options menu) — needed for Task 5 (language selection)
   - Commit: `feat: options menu — volume + graphics + key binding + language selection`

3. **Task 1** (Steam store page + achievements) — biggest retail-facing piece
   - Commit: `feat: 20 Steam achievements + 13 stats + store page content + AI disclosure`

4. **Task 5** (first-run + controller) — UX polish
   - Commit: `feat: first-run tooltips + gamepad support for menus and scavenge`

5. **Task 4** (CI/CD) — last, once all gates are stable
   - Commit: `ci: GitHub Actions CI pipeline — all QA gates on every push`

### After Each Task
- Run `python tools/verify_steam_layer.py` — must be 39/39 green
- Run `python tools/content_qa.py` — must be zero violations
- Run `python tools/csharp_string_qa.py` — must be zero violations
- `git status` — stage explicit paths only, NEVER `git add -A`
- `git commit` with the message above
- Push: `git push origin feat/scavenge-3d-scene`

### Hard Rules (from CLAUDE.md)
- **LangVersion 9.0**
- **No `// TODO`, no stubs**
- **Balance numbers in `BalanceConstants`**
- **Namespace matches folder**
- **File name == primary type name**
- **Newtonsoft JSON** — never `JsonUtility`
- **IP firewall** — no S.T.A.L.K.E.R. names. Use `content_qa.py` and `csharp_string_qa.py` after all changes.
- **AI disclosure** (CLAUDE.md §10): Pre-Generated for player-facing AI art/audio/narrative. Dev tools exempt.

### After All Five Tasks
The game will have:
- Full localization (English + Russian) across ALL UI screens ✅
- Options menu (volume, graphics, controls, language) ✅
- 20 Steam achievements + 13 stats, all wired ✅
- Store page content ready for SteamPartner ✅
- First-run experience with contextual tooltips ✅
- Gamepad support for all menus and scavenge ✅
- CI/CD pipeline catching regressions on every push ✅

**The game is ready for Early Access release.**
