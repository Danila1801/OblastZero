# Play-loop verification — first end-to-end Play-mode run

**Date:** 5 Aug 2026
**Unity:** 6000.4.6f1 · **Project:** `C:\Users\danil\projects\OblastZero` · **Branch:** `feat/scavenge-3d-scene`
**Scope:** Phase 2, Task 2 — take the game through a complete run in Play mode for the first time, fix everything that surfaces.

**Headline:** the loop now runs end to end with **zero console errors and zero warnings**. Six defects were found and fixed, one per commit. The most severe took the entire Phase-2 narrative layer offline — all 1020 authored events were unreachable in every run — and it was invisible to both the console and the test suite.

---

## 1. What was run

Boot from `Assets/Scenes/_Bootstrap.unity`, driven through the **real UI and EventBus intent paths** (`Button.onClick.Invoke()`, `EndDayRequestedEvent`, `EventChoiceSelectedEvent`) rather than by calling game logic directly, so the wiring under test is the wiring that ships.

| Step | Result |
|---|---|
| Boot + data layer | 711 items, 1020 events, 3 crew, 3 factions, 73 localization keys |
| MainMenu | Renders; NEW REGISTRATION / RESUME FILING / CLOSE FILE all live |
| RunSetup | 3 sites (1 available, 2 greyed with reasons), 3 crew with authored stats |
| ScavengePhase3D | 25 pickups, player, HUD, bunker trigger; additive load/unload clean |
| Pickup + carry cap | Enforced (see §4.1) |
| Bunker entrance trigger | Fires `ReachBunkerEvent`, ends the phase early |
| TransitionCutscene | Transferred 14 stacks / 28 units + 3 crew into the bunker |
| SurvivalPhase2D | 15 consecutive days, 15 events, autosave each day |
| Wipe → RunFailed | Ran out at day 16, summary screen correct |
| Return to MainMenu | Expedition save correctly deleted, meta counters updated |
| All 4 RunVictory_* | Reach state, render, unlock ending (see §4.6) |

### Environment note (not a game defect)

The Unity MCP bridge was undiscoverable: the Editor plugin was set to **HTTP Local (port 8090)** while the MCP servers run `--transport stdio`. Fixed by setting `MCPForUnity.UseHttpTransport = 0` and relaunching via the package's own `McpCiBoot.StartStdioForCi`. Worth recording in `CLAUDE.md` — it costs a session's first 20 minutes every time.

Also: **`Application.runInBackground` is false**, so Play mode freezes whenever the Editor loses focus. The 60-second emission timer never ticked (`Time.frameCount` identical across calls) until it was set true at runtime. This is correct behaviour for a shipped game and was **not** changed in the project settings — but any agent- or CI-driven Play-mode run must set it, or the real-time phase silently does nothing.

---

## 2. Bugs found and fixed

One commit per logical fix, in commit order.

### 2.1 `109f1ad` — All 1020 events were unreachable *(critical)*

`SurvivalPhase2DState` called `BunkerPhaseController.EndDay()` with no region tags. `EventEngine.PassesPrerequisites` rejects any event carrying `regionTagsAny` when the caller supplies none — and **100% of shipped events carry them**. So the selector returned `null` on every day of every run: no events, no faction consequences, no narrative. Phase 2 was a day counter with a starvation clock.

Why it survived: the empty-pool branch logged one line — `"No eligible events for the current state"` — which is exactly what a legitimately quiet day looks like. And every smoke test passes tags explicitly, so no test exercised the call signature the game actually uses.

**Fix:** added `RegionTags` as the single authority for the vocabulary (mirrored from `tools/generate_events.py`), and `SurvivalPhase2DState` now passes `RegionTags.BunkerPhaseActive` — the three interior spaces plus the two immediate approaches. The five remote expedition tags stay excluded, so location remains a real filter instead of becoming a no-op.

**Measured:** 0 of 15 days produced an event before; 15 of 15 after, with per-day pools of 150–254.

### 2.2 `c7689a6` — A total blackout logged as routine

Made the above diagnosable rather than silent. `ReportEmptyPool` now distinguishes "nothing matched today" from "nothing can ever match": when the pool is empty and no tags were supplied while events require them, it reports how many of the corpus are tagged and names the fix, as an **error**. A genuinely narrow day stays a `Log`, now carrying corpus size, day, and active tags.

### 2.3 `d21b383` — `END DAY` button showed a tofu box

The label used `U+25B8` (▸). The shipped `LiberationSans SDF` atlas is 250 glyphs with no Geometric Shapes block, so TMP substituted `U+25A1` — the primary bunker action read `END DAY  □` on every frame of every run. Replaced with `»`, **verified present by querying the font's `characterTable`** rather than assumed.

A sweep of every string in `Assets/_Project/Scripts` confirmed this was the only out-of-atlas glyph reaching a TMP text; the remaining ones (`→`, `─`, `§`) are in comments and `Debug.Log` output, which the console renders with its own font.

### 2.4 `cbe0f61` — Steamworks never initialized

`_Bootstrap.unity` carried `GameManager`, `GameStateMachine`, all 11 states and the `EventSystem` — but **no `Bootstrap` component**. `Bootstrap` is the only caller of `SteamManager.Initialize`, so Steam never came up, `SteamEventBridge` was never attached, and no achievement or stat could fire. The game booted fine, which is why nobody noticed.

Adding the component alone would have logged `"No GameManager prefab assigned in Inspector. Game cannot start."` every boot: `Bootstrap` runs at execution order `-2000` against `GameManager`'s `-1000`, so `GameManager.Instance` is still null when `Bootstrap` checks it even though the component is authored in the scene. `Bootstrap` now checks the **scene** for a `GameManager` rather than the singleton, which separates "nothing will boot this" from "the scene boots itself".

Verified with the App ID 480 placeholder — Steam initializes against a real account, and `stat_days_survived_total` / `stat_longest_run_days` increment per bunker day.

### 2.5 `31cba47` — All four victory states were dead on arrival

Two independent faults, either one fatal:

1. `RunEndVictoryStateBase` read `Context.CurrentRun`, but `GameManager.EndCurrentRun` clears the live run as part of closing it and runs **before** the transition into any run-end state. Every victory state found null, logged `"Missing run or meta context"`, and bounced straight back to MainMenu. `RunFailedState` documents this and reads `LastRunSummary`; the victory path did not.
2. Even on entry, nothing could draw. The hand-rolled `VictoryRunUI` hung `LayoutElement`s off a root with **no layout group** — inert, the exact failure mode `CLAUDE.md` warns about — and sourced every font from `Resources.Load<Font>("Arial")`, which no longer resolves in Unity 6. A legacy `Text` with a null font renders nothing.

**Fix:** both replaced by the path the failure case already uses — read `LastRunSummary`, present through `RunSummaryUI` (self-building TMP canvas on the shared `OblastUI` vocabulary). `RunSummary.HeadlineFor` already distinguishes all four victory reasons, so each ending supplies only its caption and closing prose. `VictoryRunUI` deleted. Also flushes the profile after recording the ending, since `EndCurrentRun` saves `MetaProgress` on its way here and an unlock added afterwards lived only in memory.

### 2.6 `573e4d1` — IP firewall: "the Zone" in shipping prose

Two victory narratives used the franchise's name for its setting: *"You are no longer leaving the Zone. The Zone is becoming you."* and *"The Zone does not permit departures."* Plus a `Psi Syndicate` doc comment. `CLAUDE.md` §8 is absolute, and the setting has its own term used everywhere else.

`content_qa.py` never had a chance: it gates content JSON, and this was prose hardcoded in C#. `"Zone"` was also absent from its term lists entirely. Added to `CONTEXTUAL_IP_TERMS` (tier 2, not tier 1 — *"the contaminated zone"* is ordinary English, so it must fail only on named-entity use).

Regression-checked: `--self-test` still 10/10 catches and 4/4 clean; a full scan of 1020 events + 703 items still passes with zero violations, so the new term adds no false positives.

---

## 3. Not a bug — checked and cleared

Recorded because each looked like a defect first.

- **Summary rows appeared mis-paired.** An artefact of `FindObjectsByType` returning unordered results. Walking the hierarchy showed every `Row_*` correctly paired.
- **Crew roster read empty after the cutscene.** The query was aimed at `RescuedCrew`; `CommitRescuedToBunker` had legitimately moved them to `ActiveCrew`.
- **Duplicate `item_axe` errors during tests.** Test scaffolding registering a synthetic item over a shipped id. Boot itself is clean.
- **Re-collecting the already-rescued lead is refused.** `"'crew_marina' is already on strength — rescue ignored"`, and the world object is correctly left in place. Minor wart: that pickup can then never be collected and gives no player feedback.

---

## 4. Specific checks

### 4.1 Crew carry capacity actually limits inventory — ✅

Authored values confirmed on the roster screen and at runtime: **Marina 12 kg · Yuri 15 kg · Sasha 19 kg**. `BeginNewRun` resolves the lead's `carryCapacityKg` and the HUD shows it (`LOAD 0.0 / 12 KG`).

Marina run, heaviest-first against 27.78 kg of depot loot:

| | |
|---|---|
| Accepted / refused | 5 / 17 |
| Final load | 11.99 kg of 12 |
| Cap ever exceeded | **No** (max observed 11.99) |
| Refused props left in world | 17 of 17 |

All-or-nothing refusal confirmed: `item_pistol_ammo` (0.06 kg) was refused with 0.01 kg headroom — nothing partially fills. Refusal notice renders (`OVER CAPACITY — Pry Bar (2 kg) NOT LOGGED`) and self-hides after 1.6 s unscaled. Yuri's run independently resolved to 15 kg.

The cap has real teeth: taking tools on the Marina run reached the bunker with **0 rations**, and the crew starved out. That is the weight decision working as designed.

### 4.2 GLB props instead of primitives — ⚠️ **works, partial coverage by design**

**First pass: dependency not met.** At the start of verification the scene contained only primitives — `Cube ×15`, `Sphere ×2`, `Cylinder ×5`, `Capsule ×3` plus a 301-mesh static batch — and no code in the project referenced `.glb`/`glTF` at all. Task 1 (the GLB loader) had not run.

**Re-tested after Task 1 landed** (`9e9f277`, `06e6ca3`, committed by the concurrent session mid-verification). Props are **not** baked into the scene YAML — `ScavengePropDresser` swaps them in at runtime from `Assets/Art/Resources/Props/*.bytes` — so the check has to be made in Play mode, not by reading the scene:

```
[GLBPropLoader] Loaded Props/prop_crate     (0.73x0.59x0.99 normalised, 3 LOD levels)
[GLBPropLoader] Loaded Props/prop_ammo_box  (0.68x0.73x1.00 normalised, 3 LOD levels)
[GLBPropLoader] Loaded Props/prop_artifact  (0.71x1.00x0.83 normalised, 3 LOD levels)
[GLBPropLoader] Loaded Props/prop_pry_bar   (0.69x0.42x1.00 normalised, 3 LOD levels)
[ScavengePropDresser] Dressed 8/25 pickups with authored meshes.
```

Confirmed by mesh census at runtime: 8 `PropInstanceTag` instances, each with 3 real LOD meshes — `prop_pry_bar_LOD0/1/2 ×4`, `prop_ammo_box_LOD0/1/2 ×2`, `prop_artifact_LOD0/1/2 ×2`.

**8 of 25 pickups are GLB-dressed; the other 17 remain primitive because only 4 of the 11 visual archetypes have authored meshes yet.** The dresser's own census names the gap: `Crew ×3, Document ×2, Medical ×4, MetalCan ×5, WeaponLong ×2, WeaponSidearm ×1` are all still primitive. The remaining `Cube`/`Cylinder`/`Sphere`/`Capsule` counts are level geometry (walls, floors, pit ramp), which is correct — those were never meant to be props.

`prop_crate` loads successfully but produces **zero instances**: no pickup in the depot's 25 maps to the `Crate` archetype. Not a fault, just no consumer in this scene.

Two notes for whoever owns Task 1:

- **A real LOD warning fires:** `SetLODs: Attempting to set LOD where the screen relative size is greater then or equal to a higher detail LOD level.` The LOD screen-relative thresholds are misordered. Cosmetic today, but it will misbehave at distance.
- The 7 undressed archetypes need reference images and GLBs before the scene reads as authored rather than half-authored.

Nothing outside this session's own files was staged at any point; the concurrent session's work was left entirely to them.

### 4.3 EventModal displays, choices work, consequences apply — ✅

Modal renders title, narrative and all three choices as real prose; `EndDayButton` is correctly disabled while an event is pending. Resolution is fully wired:

```
[EventEngine] Resolving 'evt_bunker_0143' choice 0: chance=0.7 roll=0.964 => FAILURE.
[CrewManager] Volkova fatigue +15 -> 35/100.  sanity -5 -> 77/100.  health -5 -> 69/90.
```

Across 15 resolved events, reputation moved to Scale Society **−50 (HOSTILE)**, Cordon **−25 (OBSTRUCTIVE)**, Kafedra **−25 (OBSTRUCTIVE)** — confirming faction effects, weighted selection, trait gating (`AvailableChoiceIndices`), and follow-up queueing all apply through the UI path.

### 4.4 BunkerHUD updates resources, crew, day counter — ✅

Day counter, per-crew HP/SAN/FAT/RAD, ration count with days-of-supply and severity colour (amber `Rations 7 (~2d)`, red at 0), stores count, bunker radiation, and all three faction standings — all refresh off their EventBus events. Verified changing live across 15 days.

### 4.5 Save/load across the loop (bifurcated) — ✅

Built a run with distinctive state, advanced 3 days (autosave fires per day), dropped the in-memory run entirely, then pressed the real **RESUME FILING** button to force a genuine disk round-trip:

| Field | Result |
|---|---|
| `runId`, `currentDay`, `rngSeed` | ✅ |
| `ActiveCrew` count + per-member HP/SAN/FAT | ✅ |
| `BunkerInventory` stacks, durability, decay | ✅ |
| All three faction reputations | ✅ |
| `CompletedEventIds`, `bunkerMorale`, `bunkerRadiationPool` | ✅ |
| Managers rebound (weight, alive count, reputation) | ✅ |
| `rngStreamCounter` | 4 vs 5 — **explained below, not corruption** |

The two channels are properly separated: `profile.json` (meta) persists across runs, the expedition channel is deleted on run end so the menu cannot offer to resume a closed run.

**On `rngStreamCounter`:** the autosave fires inside `AdvanceDay` **before** event selection, and selection consumes RNG. So the save legitimately holds the counter as of the day tick. The real consequence is a design gap, not a bug: `RunData` has no field for a pending event, so **an event on screen when the player quits is discarded on reload and re-rolled**. Recommended fix if this matters — add a `pendingEventId` to `RunData` alongside `QueuedEventIds`. Left alone here because it changes the save format and is a design call.

### 4.6 RunFailed and all four RunVictory_* states — ✅ *(after 2.5)*

`RunFailed` renders correctly from `LastRunSummary`: verdict `REGISTRATION CLOSED`, caption, closing line, 6 stat rows (site, days 16, lost 3, remaining 0, recovered 21, salvage 6 at 33%), 3 faction rows with standings, and meta totals.

All four victory states now reach their state, render 11 stat/rep rows, and unlock:

| State | Verdict | Unlocked |
|---|---|---|
| `RunVictory_Stabilization` | CONDITION STABILISED | ✅ |
| `RunVictory_Relief` | RELIEF COLUMN ARRIVED | ✅ |
| `RunVictory_Adaptation` | ADAPTATION RECORDED | ✅ |
| `RunVictory_Independent` | STATUS: INDEPENDENT | ✅ |

`unlockedEndings = Stabilization, Relief, Adaptation, Independent`, `totalRunsSurvived` incremented per victory.

⚠️ **Each had to be triggered manually — nothing in the game can reach them.** No code path calls `EndCurrentRun(RunEndReason.Victory*)` or transitions to a `RunVictory_*` state. `RunEndReason` and `RunSummary.HeadlineFor` handle all four, but **no win condition is implemented**: today every run can only end in failure. Deciding what constitutes victory (day threshold? reputation? a scripted event chain?) is a design decision and is left for you — see §6.

### 4.7 Steamworks bootstrap — ✅ *(after 2.4)*

```
[SteamManager] Initialized for '<user>' (<steamid>). App 480.
[SteamEventBridge] Subscribed to game events.
[Bootstrap] GameManager authored in the scene ('GameManager') — it self-initializes; no prefab needed.
```

No errors with the App ID 480 placeholder. Stats flow during play. `SteamConfig.asset` is present in `Resources` with all achievement/stat keys populated.

---

## 5. Tests

All pre-existing suites green, run in Play mode against the real database (they are `[ContextMenu]` MonoBehaviours, not NUnit — Test Runner does not see them):

| Suite | Result |
|---|---|
| `DataLayerSmokeTest` | **24/24** |
| `EventEngineSmokeTest` | **24/24** |
| `BunkerPhaseSmokeTest` | **17/17** |
| `ScavengeLogicTest` | **26/26** |
| `BunkerDayLoopTest` | runs clean (no assertion count) |
| **Pre-existing total** | **91/91 — no failures** |
| `BunkerEventReachabilityTest` (new, below) | **9/9** |
| **Grand total** | **100/100 — no failures** |

All six were also run together in a single pass after the final recompile, with zero `FAIL` lines.

`python tools/content_qa.py` → PASS (0 IP / §7 / schema / formula violations over 1020 events + 703 items).
`python tools/content_qa.py --self-test` → PASS (10/10 catches, 4/4 clean).

### New: `BunkerEventReachabilityTest`

§2.1 was invisible to the existing suites *by construction* — they build small synthetic databases whose events carry no `regionTagsAny`, so a bare `EndDay()` passes there and fails in the game. The only test that can catch that class of bug runs the selector against the **authored corpus**, which is what this adds:

- every shipped event is region-gated — **1020/1020**, which is what makes a null tag set fatal
- `RegionTags.All` covers the authored vocabulary — catches drift between `generate_events.py` and the C# mirror
- `RegionTags.BunkerPhaseActive` reaches ≥ 50% of the corpus — **actual 858/1020 (84%)**
- `SelectNextEvent(BunkerPhaseActive)` returns an event ← **the regression guard**
- `SelectNextEvent(null)` returns nothing — pins the fail-closed behaviour so changing it is deliberate
- a full `EndDay(BunkerPhaseActive)` turn advances the day and presents an event

**Result: 9/9 — ALL PASS.** It also exercises the §2.2 diagnostic, which fired verbatim.

> One check calls `SelectNextEvent(null)` on purpose, so the run deliberately logs a single
> `[EventEngine] No events eligible and NO region tags were supplied...` error. That is the diagnostic
> working. Read the `RESULT` line, not the error count.

---

## 6. Open items for you

1. **No win condition exists.** All four victory states work but are unreachable (§4.6). This is the largest remaining gap in the loop — every run currently ends in failure. Needs a design decision.
2. **GLB props: 7 of 11 archetypes still unauthored**, and the LOD thresholds are misordered (§4.2). Task 1's owner.
3. **Pending events are not saved** — quitting with an event on screen re-rolls it (§4.5). Needs a `pendingEventId` on `RunData` if that matters.
4. **No gate reads user-facing strings in C#.** `content_qa.py` covers content JSON only, which is how "the Zone" shipped in two victory screens (§2.6). Extending it needs a decision on how to extract prose from source.
5. **Record the MCP transport trap in `CLAUDE.md`** (§1) — it recurs every session.
6. **The already-rescued lead's pickup** stays in the world uncollectable with no feedback (§3).
