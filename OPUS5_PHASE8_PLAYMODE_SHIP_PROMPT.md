# MASTER CLAUDE CODE PROMPT — OblastZero Phase 8: Localization Debt + Play-Mode Verification + Ship
**Target:** Claude Code CLI (Opus 5, max effort), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** All code done. 3 scavenge sites built. All gates GREEN. 21 achievements + 15 stats wired. **What remains: 91 hardcoded strings across 5 screens, one Play-mode pass (agent-capable, proven 5 Aug), and the uncommitted tree decision.**

---

## CONTEXT: WHAT ACTUALLY EXISTS (verified by running every gate, 10 Aug 2026)

| System | State | Evidence |
|---|---|---|
| Core framework + state machine + EventBus | ✅ Complete | 142 .cs files, compiles 39/39 |
| 3 scavenge sites | ✅ All built, all in Build Settings | Scavenge (423 GO), CensusOffice (286 GO), Reservoir (199 GO) |
| Prop pipeline (4/11 GLB meshes) | ✅ 93/93 | verify_prop_pipeline.py |
| Audio (22 procedural cues, zero audio files) | ✅ 32/32 | verify_procedural_sfx.py |
| VFX + atmosphere (emission, dust, post-processing) | ✅ Complete | 7 volume overrides, all 3 scenes |
| Bunker day loop + event engine (1020 events) | ✅ Complete | EventEngine, 1020 events in JSON |
| Victory conditions (4 endings) | ✅ Wired | VictoryConditionEvaluator, all 4 RunVictory* states |
| Save/load (bifurcated, migration) | ✅ 24/24 | DataLayerSmokeTest |
| Steam (21 achievements, 15 stats) | ✅ All wired | 21/21 have Unlock() calls, 15/15 written |
| Meta-progression (10 unlocks, salvage tokens) | ✅ Complete | MetaUnlockCatalog + MetaUnlockUI |
| Crew traits (10 traits, mechanical effects) | ✅ Complete | TraitEffects resolver, 406 choices use traits |
| Anomalies (Carbon Copy, Interview, Backlog) | ✅ Built | 6 scripts, all 3 scenes have zones |
| Mutants (Census-Taker, Editor, spawn system) | ✅ Built | 4 scripts, A* on generated nav grids |
| Expeditions + Artifacts (4 artifacts) | ✅ Built | ExpeditionManager, ArtifactSystem |
| Options menu + pause + controller | ✅ Complete | OptionsUI, PauseMenuUI, ControllerNavigationUI |
| Composable player speed (water + anomaly) | ✅ Fixed | WaterVolume + BacklogAnomaly compose correctly |
| Region taxonomy (2 orthogonal axes) | ✅ Complete | RegionTags + OblastRegions |
| Balance (victory reachable) | ✅ 73-88% win rate | balance_analysis.py |
| Content QA (IP + voice) | ✅ PASS | content_qa.py, csharp_string_qa.py |
| Localization (175 call sites, 221 EN/RU keys) | ✅ Mostly done | 91 literals across 5 screens remain |
| CI/CD pipeline | ✅ Running | .github/workflows/ci.yml |
| Steam docs | ✅ Complete | STEAM_STORE_PAGE.md, STEAM_ACHIEVEMENTS.md, STEAM_STATS.md |
| Directory.Build.targets (compile fix) | ✅ Fix | Drops missing VSTU analyzers |
| SteamConfig asset | ✅ Exists | Assets/Data/Resources/SteamConfig.asset (appId 480) |
| runInBackground | ✅ Fixed | ProjectSettings/ProjectSettings.asset: 0→1 (commit abe331c) |
| SampleScene.unity | ✅ Deleted | git rm'd (commit abe331c) |
| Play-mode verification (boot→wipe) | ✅ DONE 5 Aug | PLAY_LOOP_VERIFICATION.md (253 lines, 6 bugs fixed, commits 109f1ad-573e4d1) |

### What was verified on 5 Aug (regression-check, fast):
Boot + data layer, MainMenu, RunSetup (3 sites, crew stats), Grain Depot scavenge incl. carry-weight enforcement, bunker entrance → TransitionCutscene, bunker phase 15 consecutive days with autosave, wipe → RunFailed summary, return to MainMenu, all 4 RunVictory* states, Steam stats incrementing on App ID 480. Zero console errors.

### What was NEVER verified (the actual Phase 8 scope):
- Census Office end to end (flooded basement, water slow, Interview anomaly, Carbon Copy, Census-Taker nav)
- Reservoir end to end (3 crossings, basin water, Backlog + water composition, 2 mutants, Editor)
- Expeditions (DISPATCH → assign crew → resolve)
- Artifacts (ARTIFACTS → use each of 4)
- Options/Pause/Gamepad/First-run tooltips
- Russian rendering across all screens + language switch
- Emission VFX thresholds at 15s/5s (new code, never driven)
- Meta-unlock UI (token spend)

---

## TASK 1: LOCALIZATION DEBT — 91 HARDCODED STRINGS ACROSS 5 SCREENS

### Problem
Five UI screens never call `LocalizedStrings.Get()` and render English in every language:

| Screen | Location | Hardcoded Literals |
|---|---|---|
| `ArtifactUseUI` | `Assets/_Project/Scripts/UI/ArtifactUseUI.cs` | 30 |
| `InterviewSequenceUI` | `Assets/_Project/Scripts/UI/InterviewSequenceUI.cs` | 30 |
| `ExpeditionUI` | `Assets/_Project/Scripts/UI/ExpeditionUI.cs` | 17 |
| `ScavengeHazardHUD` | `Assets/_Project/Scripts/UI/ScavengeHazardHUD.cs` | 14 |
| `OblastUIAudio` | `Assets/_Project/Scripts/UI/OblastUIAudio.cs` | 1 |
| **Total** | | **91** |

**IMPORTANT:** All five files are in `Assets/_Project/Scripts/UI/` — NOT in `OblastZero.Gameplay/`. The `localization_qa.py` coverage gate scans only `Assets/_Project/Scripts/UI/` non-recursively. Verify file paths before editing.

### Implementation
For each screen, replace every hardcoded English string with:
1. A new key constant in `UIStringKeys.cs` (following existing `const string` naming convention)
2. An EN entry in `Assets/Data/Resources/Locale/localization_en.json`
3. An RU entry in `Assets/Data/Resources/Locale/localization_ru.json` (Soviet-bureaucratic register — the source language of the aesthetic)

### Files to Modify
- `Assets/_Project/Scripts/Core/UIStringKeys.cs` — add ~91 new key constants
- `Assets/Data/Resources/Locale/localization_en.json` — add ~91 entries
- `Assets/Data/Resources/Locale/localization_ru.json` — add ~91 entries
- `Assets/_Project/Scripts/UI/ArtifactUseUI.cs` — replace 30 literals
- `Assets/_Project/Scripts/UI/InterviewSequenceUI.cs` — replace 30 literals
- `Assets/_Project/Scripts/UI/ExpeditionUI.cs` — replace 17 literals
- `Assets/_Project/Scripts/UI/ScavengeHazardHUD.cs` — replace 14 literals
- `Assets/_Project/Scripts/UI/OblastUIAudio.cs` — check if the 1 literal is player-facing; if not, skip

### Verify (AUTOMATED)
- `python tools/verify_steam_layer.py` exits 0 (compile)
- `python tools/localization_qa.py` — the "91 literals across 5 screens" warning should shrink to 0
- `python tools/localization_qa.py --self-test` — 10/10 still passing
- `python tools/content_qa.py` — PASS (no IP/voice violations in new strings)

### Verify (NEEDS HUMAN IN PLAY MODE)
- Russian language: all 5 screens render in Russian
- Language switching at runtime refreshes all 5 screens

---

## TASK 2: UNCOMMITTED TREE — COMMIT THE 5 AUG PARALLEL SESSION WORK

### Problem
~1,020 event JSONs + 14 .cs files + 25 .mat files are modified in the working tree from a 5 Aug session but never committed. The `migrate_event_tags.py --check` gate PASSES, so the event JSONs match the model.

**IMPORTANT — enumerate ALL three git status categories:**
- **Modified:** 1,020 event JSONs (region tag migration), 14 .cs files (UI/Core/Gameplay), 25 .mat (harmless churn)
- **Untracked source files:** Already committed in `abe331c` (MetaUnlockCatalog.cs, MetaUnlockUI.cs, TraitEffects.cs, OblastRegions.cs)
- **Deleted docs:** PLAY_LOOP_VERIFICATION.md, PROJECT_STATE_REPORT.md, PLAYMODE_CHECKLIST.md — these were restored from HEAD; OPUS5_*.md prompt files were moved to `docs/archive_prompts/`

### Task
1. Run `python tools/migrate_event_tags.py --check` — confirm PASSES (1,020 events, 0 needing update)
2. Run `git status --short` — review all changes
3. Stage **explicit paths only** (NEVER `git add -A`):
   - `git add Assets/Data/Resources/Events/*.json`
   - `git add <each modified .cs file by explicit path>`
   - `git add Assets/Art/Materials/Scavenge/*.mat`
4. Commit with message: `chore: commit region-tag migration (1020 events) + UI/Core changes from 5 Aug session`
5. Push
6. Run `python tools/verify_steam_layer.py` — 39/39
7. Run `python tools/content_qa.py` — PASS
8. `git status` should now be clean

**Decision items (not automatic):**
- The 7 deleted OPUS5_*.md prompt files: these were moved to `docs/archive_prompts/`. If you completed the move, commit the deletions. If not, restore them or complete the move.
- PLAY_LOOP_VERIFICATION.md: **DO NOT DELETE** — it is the evidence that play-mode verification is agent-capable.

---

## TASK 3: FULL PLAY-MODE VERIFICATION — AGENT-CAPABLE, NEEDS UNITY RUNNING

### Capability Boundary (verified)
**The agent CAN drive Play mode.** This was proven on 5 Aug 2026 — see `PLAY_LOOP_VERIFICATION.md` (committed at HEAD). The agent drove the full loop boot→wipe through the real UI and EventBus paths, found and fixed 6 defects, one commit each.

### What genuinely needs Danil (2 steps only):
1. **Launch Unity Editor** from `C:\Users\danil\projects\OblastZero`
2. **Flip MCP transport:** In *MCP For Unity* window → Transport → **stdio** (not HTTP Local). Persistent form: set `MCPForUnity.UseHttpTransport = 0` and relaunch via `McpCiBoot.StartStdioForCi` — this stops costing 20 minutes every session.

### What is ALREADY DONE (stop requiring it):
- ✅ SteamConfig asset exists at `Assets/Data/Resources/SteamConfig.asset` (appId 480)
- ✅ Bootstrap.cs:104 auto-loads it via `Resources.Load<SteamConfig>("SteamConfig")` — no Inspector assignment needed
- ✅ `runInBackground: 1` in ProjectSettings (fixed in commit `abe331c`)
- ✅ SampleScene.unity deleted (commit `abe331c`)

### After Unity connects — AGENT WORK:
Drive the full loop through the MCP bridge. Use the real UI paths (`Button.onClick.Invoke()`, `EventBus` intents), not direct game-logic calls.

### Phase 8 Verification Checklist (delta against 5 Aug baseline):

**REGRESSION (already verified 5 Aug, fast re-check):**
- [ ] Boot `_Bootstrap.unity` → MainMenu renders
- [ ] NEW REGISTRATION, RESUME FILING, OPTIONS, CLOSE FILE live
- [ ] RunSetup shows 3 sites + crew stats + traits
- [ ] Grain Depot: 25 pickups, carry weight enforced, bunker trigger → TransitionCutscene
- [ ] Bunker: 15 days, EventModal renders, events resolve, autosave each day
- [ ] Wipe → RunFailedState summary → return to MainMenu
- [ ] All 4 RunVictory* states reachable
- [ ] Steam stats increment (App ID 480)
- [ ] Zero console errors

**NEW — NEVER VERIFIED (the real Phase 8 surface):**
- [ ] **Census Office:** loads, flooded basement, water slow (0.6x), Interview anomaly room, Carbon Copy duplicates, Drowned Census-Taker follows via nav grid, all 25 pickups reachable, vault door exit
- [ ] **Reservoir:** loads, 3 crossings (catwalk/tunnel/basin), basin water slow (0.5x), Backlog in tunnel composes with water (0.5 × 0.02 = 0.01), 2 Census-Takers, Editor appears (15%), control room exit
- [ ] **Expeditions:** DISPATCH button → ExpeditionUI → assign crew → resolve after N days → crew returns with items
- [ ] **Artifacts:** ARTIFACTS button → ArtifactUseUI → use Margin Note, Notarized Heart, Stamped Tongue, Final Draft
- [ ] **Options menu:** volume sliders, graphics quality, controls, language dropdown — all work + persist
- [ ] **Pause menu:** Escape in scavenge → pause works
- [ ] **Gamepad:** navigate menus, play scavenge, interact with bunker
- [ ] **First-run tooltips:** show on first run, not on second
- [ ] **Russian language:** all UI renders in Russian (including 5 newly localized screens)
- [ ] **Runtime language switch:** changing language refreshes all visible UI
- [ ] **Emission VFX:** 15s warning (vignette/desat), 5s critical (redshift/shake/flashes)
- [ ] **Meta-unlock UI:** Supply Office → 10 unlocks, token spend, persist
- [ ] **Reputation movement:** all 3 factions visible on HUD after event choices
- [ ] Zero console errors on ALL of the above

### Bug found during Play mode?
One commit per logical fix, following the 5 Aug pattern. Commit message: `fix: <what was wrong> (found in Phase 8 play-mode verification)`. Run `python tools/verify_steam_layer.py` after each fix.

---

## EXECUTION ORDER

1. **Task 1** (localization debt) — pure scope work, no Play mode needed
   - Commit: `feat: localize 5 remaining screens — ArtifactUseUI, InterviewSequenceUI, ExpeditionUI, ScavengeHazardHUD, OblastUIAudio`

2. **Task 2** (uncommitted tree) — commit the 5 Aug parallel session work
   - Commit: `chore: commit region-tag migration (1020 events) + UI/Core changes from 5 Aug session`

3. **Task 3** (Play-mode) — after Danil launches Unity + flips MCP transport
   - Fixes committed per bug found
   - Final verification: zero console errors on full loop

### After Each Task
- `python tools/verify_steam_layer.py` 39/39
- `python tools/content_qa.py` PASS
- `python tools/csharp_string_qa.py` PASS
- `python tools/localization_qa.py` — debt count should only decrease
- Stage explicit paths only, NEVER `git add -A`
- Before staging: `git status --short` to check for concurrent session activity
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
- **Deterministic scene generation** — byte-deterministic, self-validating
- **IP firewall** — no S.T.A.L.K.E.R. names. The "Zone" is "the Oblast." Check `content_qa.py` and `csharp_string_qa.py` after changes.
- **Event-driven** — prefer raising an event over hard cross-system reference
- **Oblast voice** — post-administrative, not post-apocalyptic. "The Oblast does not raise its voice. The Oblast files a form."
- **Concurrent sessions** — stage explicit paths only, NEVER `git add -A`. Before staging: `git status --short`. Enumerate untracked source files deliberately — explicit-path staging automatically excludes them.
- **Verify negations as hard as assertions** — "cannot be done by the agent" needs MORE verification than "X exists", not less. A wrong assertion fails loudly; a wrong negation fails silently forever.

---

## AFTER ALL TASKS

The game will have:
- **All 5 remaining screens localized (EN + RU)** ✅
- **Uncommitted work reviewed and committed** ✅
- **All 3 scavenge sites verified in Play mode** ✅
- **Expeditions, artifacts, options, gamepad, first-run, Russian — all verified** ✅
- **Zero console errors on full loop** ✅

**The game is ready for Early Access release.**

---

## THE REAL RECOMMENDATION

> *"Three complete levels, four endings, an event engine with 1,020 events, 21 achievements and a Russian translation are all sitting behind a loop that has never once been run from boot to wipe — except it has, on 5 Aug, and the agent did it. Every gate in this project verifies structure. None of them can tell you the game is fun, and at this point the only unverified surface is the part that was built after that run."*

**Play-mode verification is the only thing that matters now — and it's agent work, not a human blocker.** Launch Unity, flip the MCP transport, and the agent can drive everything else.