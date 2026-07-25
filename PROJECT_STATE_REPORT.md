# OBLAST ZERO — PROJECT STATE REPORT

**Snapshot date:** 25 July 2026
**Repo:** `C:\Users\danil\projects\OblastZero` — private GitHub `Danila1801/OblastZero`
**Reporting branch:** `feat/scavenge-3d-scene` @ `e7b03db` (one commit ahead of `main` @ `7706a76`)
**Author of this report:** Claude Opus 5 (Claude Code session, 25 Jul 2026)

---

## 0. HOW TO READ THIS DOCUMENT

This is a **standalone briefing**. It assumes you have no prior conversation context and cannot read the disk. Every claim is labelled by evidence class:

| Label | Meaning |
|---|---|
| **[VERIFIED]** | Proven by running a command or reading the file during this session. The proof is named. |
| **[BUILT, UNRUN]** | Code exists and compiles; its runtime behaviour has never been observed. |
| **[ASSUMED]** | Inherited from prior sessions' notes, not re-proven today. Treat as suspect. |
| **[GAP]** | Known missing or broken. Ranked in §9. |

The single most important sentence in this document: **the game compiles green and is structurally complete, but the full loop has never been executed in Unity's Play mode.** Everything in §9 flows from that.

If you are reviewing: skip to **§9 (gaps, ranked)** and **§11 (specific review questions)**. Do not spend effort re-praising what §5 says is built.

---

## 1. THE PRODUCT

**Oblast Zero** is a commercial roguelite survival game for PC, shipping on Steam. It is not a prototype, a portfolio piece, or a game jam entry. Solo development, credited to "Leonid," executed with heavy agentic tooling.

**The pitch:** you run a bunker in a contaminated administrative district — the Oblast. Every run is a two-phase cycle: a 60-second panic raid above ground, then an indefinite turn-based siege below it. Death is permanent, and only 33% of what you were carrying comes home with your corpse.

### The core loop — two phases

**Phase A — "The Blowout"** (3D, first-person, real-time, **60 seconds**)
You are above ground when an emission is registered as inbound. You have one minute to grab supplies, artifacts, and stranded crew, and get back through the bunker door. Pickup is **instant kinematic** — the object snaps into inventory. It is explicitly **not** a physics grab, and that decision is locked. The clock is the entire design: it converts "loot everything" into "choose what you can afford to reach."

**Phase B — "The Bunker"** (2D, turn-based, unbounded)
Ration what you brought back. Manage crew health, sanity, fatigue, and radiation. Resolve data-driven narrative events involving the three factions, anomalies, and mutants. Each turn is a day tick → an event presented → the event resolved. A new day is blocked while an event is pending.

**The handoff between them** is a first-class architectural concern, not an afterthought: Phase A fills `RunData.ScavengedInventory` / `RescuedCrew`; a transition cutscene state commits those into `BunkerInventory` / `ActiveCrew`.

### Timeline (hard)

| Milestone | Date |
|---|---|
| Content-complete beta | **31 Aug 2026** |
| Steam Early Access launch | **mid-Sept 2026** |

Early Access is the **ship vehicle, not a scope cut**. The scope below is what ships.

---

## 2. LOCKED DECISIONS — DO NOT RE-LITIGATE

A reviewer proposing changes to any row below is wasting tokens. These are settled.

| Decision | Value |
|---|---|
| Engine | **Unity 6 LTS + URP.** Never propose Godot or Unreal. |
| Renderer | Custom 2.5D hybrid: `HybridDepthRenderPass` / `HybridDepthRendererFeature` share a depth buffer between the 3D and 2D passes. |
| Language | C#, **LangVersion 9.0** (see §3 — this constraint bites) |
| Serializer | **Newtonsoft JSON**, never `JsonUtility` (which silently drops `Dictionary` and mangles `DateTime`) |
| Save architecture | **Bifurcated** — permadeath run data in a separate channel from persistent meta-progression. Atomic dual-channel JSON writes. |
| Death salvage rate | **33%**, as a named constant, never inline |
| Pickup feel | Instant kinematic snap. Not physics. |
| Factions (original IP) | **Scale Society** (bureaucratic exploitation), **Cordon** (militaristic hostility), **Kafedra** (scientific exploitation) |
| Central mystery | "The Reality Distortion Field" |
| Repo | Private GitHub, Git LFS for binaries. **The repo is the source of truth.** |

---

## 3. HARD CONSTRAINTS ON ALL GENERATED WORK

These are enforced by `CLAUDE.md` in the repo root, which is explicitly described there as **"the law."** The design bible (`DESIGN_BIBLE_Сlaude.Opus4.7.md`, ~1300 lines, 7 sections) is the reference for taxonomy, schemas, data flow, and voice.

### 3.1 IP firewall — absolute
The project uses S.T.A.L.K.E.R. novels as a **tone reference only** (fatalism, rust, dread, bureaucratic indifference, how people behave under extreme conditions). Those books live in `Books_STALKER/`, which is **gitignored and must never be published**.

**Zero** names, locations, factions, or mutants may be reused from those books or games. No Strelok, no Scar, no Sidorovich, no Pripyat, no ChNPP, no Duty, no Freedom. Everything ships as original Oblast Zero lore. This is a legal boundary, not a style preference.

### 3.2 Content voice
Post-**administrative**, not post-apocalyptic. Soviet/post-Soviet bureaucratic register: *registered, line item, deviation, protocol, pending review, quota, requisition, standing order*. Concrete is *stained*, not *broken*. Equipment is *operational*, never *new*. `[REDACTED]` is applied unevenly.

**Forbidden pulp clichés:** "twisted metal," "eerie silence," "unnatural glow," "screams in the distance."

The governing line: **"The Oblast does not raise its voice. The Oblast files a form."**

### 3.3 Code rules
- **No placeholders. Ever.** No `// TODO`, no `// add logic here`, no stubbed method bodies. Deliver complete implementations or don't deliver.
- **All balance numbers come from `BalanceConstants`.** No magic numbers in system code.
- **Strict logic/UI separation.** UI reads state and raises intents; it never owns game logic.
- **Event-driven communication** via `EventBus`. Services resolve through `ServiceLocator`, never `FindObjectOfType`.
- **Namespace `OblastZero.<Layer>` must match the folder. File name == primary type name.**
- **All `RunData` / `MetaProgressData` mutation goes through manager classes.** Nothing else writes those fields.
- Must scale to 500+ items and 1000+ events with **no per-frame `Resources.Load`** and no linear scans on the hot path. Index by `id`.
- **LangVersion is 9.0.** File-scoped namespaces and other C# 10+ syntax fail with CS8773. This is why third-party libs ship as prebuilt DLLs rather than vendored source.

### 3.4 Shipping gates (Steam)
- **AI disclosure, two-tier (2026 rules):** dev tools (Claude Code, MCP, Copilot) are **exempt**. Player-facing AI-made content (art, audio, narrative, localization) needs an accurate **Pre-Generated** disclosure. The current data-driven event system stays in that simpler tier; adding a **live-LLM runtime feature would escalate it to Live-Generated** and require guardrails. This constrains feature choices.
- **Music:** Suno paid tiers grant a commercial license; fully AI-generated music is not US-copyrightable. Disclose, keep stems, human-in-the-loop anything load-bearing.
- **Assets:** verify commercial terms per tool. Quad-remesh AI meshes in Blender before Unity import.

---

## 4. VERIFIED TECHNICAL FACTS

**[VERIFIED]** by reading files this session:

```
Unity                 6000.4.6f1 LTS  (ProjectSettings/ProjectVersion.txt)
Render pipeline       URP
C# LangVersion        9.0
Serializer            Newtonsoft JSON (installed package)
Steam wrapper         Facepunch.Steamworks 2.x, Win64 managed DLL only
Scenes in build       _Bootstrap=0, Bunker=1, Scavenge=2
Content               691 item JSON + 8 Item_*.asset seeds = 699 resolvable item ids
                      1020 event JSON + 3 Event_*.asset seeds
                      3 Crew_*.asset, 3 Faction_*.asset, 3 Trait_*.asset
C# source files       64 under Assets/_Project/Scripts/
```

### 4.1 Folder & namespace map

```
Assets/_Project/Scripts/
  Core/          → OblastZero.Core       state machine, RunData, MetaProgressData, IGameState,
                                         StateContext, EventBus, GameEvents, ServiceLocator,
                                         GameManager, BalanceConstants, GameState enum, Bootstrap,
                                         RunRng, FormulaEvaluator, LocalizedStrings
    States/      → OblastZero.Core       11 IGameState implementations in 8 files
                                         (the 4 RunVictory_* share RunVictoryStates.cs)
  Services/      → OblastZero.Services   SaveSystem, SceneLoader, EventJsonLoader, ItemJsonLoader
  Rendering/     → OblastZero.Rendering  HybridDepthRenderPass / RendererFeature / Settings
  Gameplay/      → OblastZero.Gameplay   run-scoped managers — sole owners of RunData mutation
  UI/            → OblastZero.UI         BunkerHUD, EventModalUI, ScavengeHUD
  Steam/                                 SteamManager + 4 services + SteamConfig SO
  Editor/                                StateRegistrationTool, OblastZeroContentSeeder

Assets/Data/Scripts/Definitions/
                 → OblastZero.Data       all ScriptableObject schemas; base class GameDataObject
Assets/Data/Resources/{Items,Events,Locale}/   the generated JSON content
```

### 4.2 State machine

`GameState` enum, 14 values **[VERIFIED]** from `Core/GameState.cs`:

```
None, Boot, MainMenu, HomeBunker, RunSetup, ScavengePhase3D, TransitionCutscene,
SurvivalPhase2D, RunFailed, RunVictory_Stabilization, RunVictory_Relief,
RunVictory_Adaptation, RunVictory_Independent, Paused
```

Every state is a `MonoBehaviour` implementing `IGameState`, living as a child of the singleton machine in `_Bootstrap.unity`, **which is never unloaded**. Phase scenes load **additively on top of it** — `SurvivalPhase2DState` loads `Bunker`, `ScavengePhase3DState` loads `Scavenge`, both via `ISceneLoader` resolved from `ServiceLocator`.

Four distinct victory endings map to the three factions plus a rare neutral outcome.

### 4.3 Balance constants that matter for review

**[VERIFIED]** from `Core/BalanceConstants.cs`:

```csharp
SALVAGE_RATE_ON_DEATH               = 0.33f
ARTIFACTS_BYPASS_SALVAGE_LOSS       = true
CONSUMABLES_LOST_ON_DEATH           = true

SCAVENGE_TIMER_SECONDS              = 60f
SCAVENGE_TIMER_WARNING_THRESHOLD    = 15f    // HUD flashes red below this
SCAVENGE_TIMER_CRITICAL_THRESHOLD   = 5f     // emission rumble begins
SCAVENGE_MAX_CARRY_WEIGHT_KG        = 25     // ← NOT ENFORCED ANYWHERE. See §9.2
SCAVENGE_PICKUP_LERP_DURATION       = 0.25f

DAILY_FOOD_PER_CREW                 = 1
DAILY_WATER_PER_CREW                = 1
STARVATION_HEALTH_LOSS_PER_DAY      = 8f
DEHYDRATION_HEALTH_LOSS_PER_DAY     = 12f
STARVATION_SANITY_LOSS_PER_DAY      = 5f

CREW_{HEALTH,SANITY,FATIGUE,RADIATION}_MAX = 100
RADIATION_SICKNESS_THRESHOLD        = 60
SANITY_AFFLICTION_THRESHOLD         = 25
REPUTATION_MIN / MAX                = -100 / 100
```

---

## 5. WHERE THE BUILD ACTUALLY STANDS

| Layer | State | Evidence |
|---|---|---|
| Core framework — state machine, EventBus, ServiceLocator, GameManager, bifurcated save | ✅ Built | **[VERIFIED]** compiles; source read |
| Data schemas — ItemData, ExpeditionEventData, FactionData, AnomalyData, MutantData, CrewMemberData, TraitData, GameDatabase | ✅ Built | **[VERIFIED]** |
| Content instances | ✅ 691 items + 1020 events as JSON | **[VERIFIED]** file counts. **[GAP]** quality unaudited — see §9.5 |
| Scenes & bootstrap rig | ✅ 3 scenes, all in Build Settings | **[VERIFIED]** |
| **Phase A — 3D scavenge level** | ✅ **Shipped this session** | **[VERIFIED]** — all of §6 |
| Hybrid 2.5D rendering | ✅ Implemented, not a stub | **[ASSUMED]** — compiles, never seen rendering |
| Phase B — bunker day loop | ✅ `BunkerDayController` + `BunkerPhaseController` | **[ASSUMED]** — `BunkerPhaseSmokeTest` reported 17/17 |
| Event engine | ✅ `EventEngine` + `RunRng` + `FormulaEvaluator` + `CrewFormulaContext` | **[ASSUMED]** — `EventEngineSmokeTest` reported 24/24 |
| Faction reputation | ✅ `FactionReputationManager`, sole owner of the 3 rep scales | **[ASSUMED]** |
| UI | 🔶 `BunkerHUD`, `EventModalUI`, `ScavengeHUD` — self-building EventBus-driven canvases | **[GAP]** no main menu, run-setup, or run-summary screens exist |
| Save/load round-trip | ✅ `DataLayerSmokeTest` 24/24, `BunkerDayLoopTest` clean | **[ASSUMED]** |
| Steamworks | ✅ Built + compiles, auto-boots from `Bootstrap` | **[VERIFIED]** compiles. **[GAP]** needs a real App ID |
| Compile gate | ✅ **39/39 ALL GREEN** | **[VERIFIED]** — ran it live this session |

> ⚠️ **Critical caveat on every "[ASSUMED]" row.** The smoke tests (`DataLayerSmokeTest`, `BunkerPhaseSmokeTest`, `EventEngineSmokeTest`, `BunkerDayLoopTest`, `ScavengeLogicTest`) are **`[ContextMenu]` MonoBehaviours, not NUnit tests**. Unity's Test Runner does not see them and CI cannot execute them. Their pass counts come from prior sessions' notes. **They were not re-run today** — the Unity Editor was not drivable (§7). Do not treat those numbers as current evidence.

---

## 6. THIS SESSION'S DELIVERABLE — THE 3D SCAVENGE SCENE

### 6.1 The brief

Build the complete 3D Scavenge Scene (Phase A) as `Assets/Scenes/Scavenge.unity` at Build Settings index 2, loaded additively by `ScavengePhase3DState`. Level: "Collapsed Grain Depot," Outer Cordon, per design bible §2.2.

Explicit requirements: traversable in 60 s with **meaningful route choices, not a single corridor**; distinct silo / warehouse / office / loading-dock / stairwell areas; cover objects; 1980s Soviet grain-facility props; fully enclosed by walls; **primitives only, no external asset imports**; bake-friendly lighting; a clearly marked bunker entrance with a trigger; player spawn at the opposite end; **15–25 pickups using real item IDs from the database**, including 2–3 crew and 1–2 artifacts; overcast directional light plus interior point lights; linear gray fog; a URP volume with vignette, desaturated green-gray grading, and film grain.

A hard constraint accompanied it: **do not rewrite the existing systems code** (`ScavengePlayerController`, `ScavengePickup`, `ScavengeController`, `EmissionTimer`, `BunkerEntranceTrigger`, `ScavengeHUD`, `SceneLoader`) unless a bug actually blocks scene creation. Read the contracts and conform to them.

### 6.2 The constraint that shaped the approach

**The Unity Editor could not be driven.** The MCP bridge reported "No Unity Editor instances found" on every call. Root cause diagnosed via `netstat` and `~/AppData/Local/UnityMCP/Logs/unity_mcp_server.log`: Unity's transport was set to **HTTP Local (127.0.0.1:8090)**, where Unity dials out to its own Python HTTP server and never opens the direct TCP bridge port (6400-ish) that a stdio-launched MCP server discovers by scanning. Unity's own window showed a green "Session Active," which made a broken setup look healthy. `CLAUDE.md` §11 blamed compile errors for this symptom — that note was wrong and has been corrected this session; the compile was green the whole time.

So the scene was authored as **text**: hand-written Unity scene YAML, from outside the Editor.

### 6.3 The approach — generate, don't hand-write

Two new Python files, 1808 lines total, do the authoring:

- **`tools/scavenge_scene_lib.py`** (701 lines) — Unity YAML emitters and nothing else. No level knowledge. Holds a `SceneBuilder` class (`obj`, `component`, `mesh_renderer`, `box_collider`, `capsule_collider`, `mono`, `light`, `emit`), the harvested GUID table, a 22-entry material table, and the scene-settings block (fog, ambient, sun reference).
- **`tools/generate_scavenge_scene.py`** (1107 lines) — the level itself: an ASCII plan in a header comment, the coordinate tables, the pickup manifest, and four self-verification passes.

**Why a generator rather than a one-off file:** the level is primitive geometry driven by a coordinate plan, so **the plan becomes the reviewable artifact**. A layout change is a readable diff in a coordinate table instead of churn across 38,000 lines of YAML.

**[VERIFIED] the output is byte-deterministic.** Re-running the generator this session reproduced md5 `0082c966d3a0fa85af2ffef7c7250769` exactly and left the git tree clean. GUIDs derive from `md5('OblastZero::' + name)`; nothing samples time or randomness.

> **Consequence, and it is load-bearing: never hand-edit `Assets/Scenes/Scavenge.unity`.** The next regeneration overwrites it silently. Layout changes go in the coordinate plan. This is recorded in `CLAUDE.md` §0 and §14.

### 6.4 Facts that had to be harvested, not guessed

Recorded in `CLAUDE.md` §14 so the next session does not rediscover them:

- **Script GUIDs come from the real `.meta` files.** A guessed GUID produces a silently unassigned component — no error, no warning, a dead scene.
- **Built-in primitive mesh fileIDs** (guid `0000000000000000e000000000000000`): Cube `10202`, Cylinder `10206`, Sphere `10207`, Capsule `10208`, Plane `10209`, Quad `10210`.
- **URP Lit shader guid** `933532a4fcc9baf4fa0491de14d08ed7`. A partial `m_SavedProperties` is valid — Unity fills shader defaults. Emissive materials need `m_ValidKeywords: [_EMISSION]` **and** `m_LightmapFlags: 2`.
- **Unity's Euler order is ZXY intrinsic.** Get it wrong and every rotated prop lands somewhere plausible but wrong.
- **Primitive mesh extents are not uniformly unit.** A Cylinder/Capsule mesh is **2 units tall**, so a `1,1,1` BoxCollider on a cylinder is half-height. This required a per-primitive local-extent table.

### 6.5 The level

`Assets/Scenes/Scavenge.unity` — **388 GameObjects, 38,275 lines, 1,025,864 bytes**, sealed **104 × 72 m** site, primitives only.

**Six zones** per bible §2.2:

1. **Silo base** with a sunken grain intake pit
2. **Warehouse floor**
3. **Bunker stairwell** — the exit
4. **Rail siding** — player spawn, at the opposite corner from the exit
5. **Admin / office**
6. **Loading dock**

**Route structure — this is the design answer to "not a single corridor":**

| Route | Cost |
|---|---|
| Direct yard diagonal | **~18 s** of the 60 s clock |
| Warehouse route | longer, more cover, more pickups |
| Rail-spine / dock route | longest, richest |
| Contaminated grain pit detour | holds **both artifacts**; requires descending and climbing back out |

The direct route consuming ~18 s leaves roughly **40 s of detour budget** — the number that makes the phase a decision rather than a sprint. **[BUILT, UNRUN]:** these are geometric path lengths converted at the controller's configured speed, not measured play.

**Lighting and atmosphere:** an overcast directional light plus interior point lights; **15 failing fluorescent fixtures** driven by a new `FluorescentFlicker` component; a URP volume (desaturated green-gray grading, vignette, film grain, tonemapping); **linear gray fog** (`m_FogMode: 1`, start 14 m, end 98 m); flat ambient with no skybox; an ambient `AudioSource`.

**Props:** rusted shelving, broken machinery, crates, ammunition boxes, respirator hooks, desk radios, a map board, notice boards, hazard-striped signage, fallen beams, grain spills, an oil press.

**Bunker entrance:** `BunkerEntranceTrigger` on an `isTrigger` BoxCollider spanning the stair mouth, marked with a red light, hazard stripes, and signage.

### 6.6 The pickup manifest

**25 pickups: 22 items + 3 crew.** Every `dataId` was resolved against the live database before writing — **[VERIFIED]** by the generator's own output: *"699 item ids, 3 crew ids, all 25 pickup ids resolve."*

Distribution: food and water, medical, weapons and ammunition, utility/tools, documents, **3 crew** (`crew_sasha`, `crew_marina`, `crew_yuri`, all `Kind=Crew`), and **2 artifacts** (`item_artifact_ember`, `item_artifact_ballast`) placed in the contaminated pit — the highest-value objects behind the highest-cost detour.

Several items carry a `durabilityOverride` (a worn pistol at 55, a carbine at 45, a torch at 70), and the artifacts and the geiger counter carry non-zero `contamination` (35.0–38.8) — the bible's mechanic for loot that costs you something to bring home.

Pickup colliders are **triggers**, so the crosshair raycast hits them while the player capsule walks through them rather than bumping into them.

### 6.7 Code changes — deliberately minimal

The brief said not to rewrite systems code. Four files touched, one file added.

**Added — `Assets/_Project/Scripts/OblastZero.Gameplay/FluorescentFlicker.cs`** (124 lines). Failing overhead tubes; per-fixture phase derived from world position so they do not blink in unison. The load-bearing detail, from the source comment:

```csharp
// Per-renderer override. Writing to sharedMaterial would mutate the material asset every
// fixture shares — every tube in the level would then stutter in lockstep, and the .mat file
// would come back dirty in the Editor. A property block keeps the change local to this renderer
// without instantiating a material per fixture.
private MaterialPropertyBlock _block;
```

**Modified — `Core/States/ScavengePhase3DState.cs`.** Added additive load/unload through `ISceneLoader`, mirroring `SurvivalPhase2DState`. Removed a serialized `emissionSeconds = 60f` that duplicated `BalanceConstants.SCAVENGE_TIMER_SECONDS` — a §3.3 violation that was already in the file. The timer is owned by the state, not the scene, so it keeps running while the level streams in; the player loses that time, which is the honest behaviour for a real-time phase. Unload happens *before* the cutscene commits the haul, which is safe because `RunData` already holds everything picked up.

**Modified — `OblastZero.Gameplay/DebugRunLauncher.cs`.** This was the old entry path into Phase A. Rather than deleting the file (the brief said not to), it is now opt-in via `enableShortcut = false` and its `Update` is wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. **[VERIFIED]** it appears in no scene.

**Modified — `Core/States/RunSetupState.cs`.** Deleted a stale comment claiming Phase A was headless and that the real scene would come later. It is here now.

**Modified — `ProjectSettings/EditorBuildSettings.asset`.** Registered `Scavenge.unity` at index 2.

**Player rig:** root tagged `Player` with `CharacterController` + `ScavengePlayerController`; child camera tagged `MainCamera` with the `AudioListener`. The camera is wired into `cameraPivot` **explicitly by fileID** rather than relying on `Camera.main`, because `_Bootstrap` stays loaded underneath and `Camera.main` would be ambiguous. `ScavengeController`, `ScavengeHUD`, and `BunkerEntranceTrigger` are all present in the scene.

### 6.8 Verification — and what it caught

Four gates run inside the generator, and **`main()` refuses to write if any fails**:

1. **Database ID resolution** — every pickup `dataId` against 699 live item ids and 3 crew ids, read from the actual JSON and `.asset` files.
2. **YAML reference integrity** — every local fileID resolves, every GUID is recognised, every component's `m_GameObject` back-reference is valid.
3. **Placement** — **OBB** burial and support tests. A world-AABB test was tried first and produced a false positive on a rotated collapsed silo, so the test transforms each point into the solid's local frame instead.
4. **Walkability flood-fill** — a height field using the real `CharacterController` metrics (height 1.8, radius 0.35, step offset 0.32), with 1-cell erosion for capsule radius. Traversal is **asymmetric**: climbs are capped by step offset, drops are free. It floods from spawn to prove reachability, then floods **from every pickup** to prove the bunker is still reachable afterwards.

That fourth gate's second half is the one worth noting: it proves every pickup is **escapable**, not merely reachable. Without it a pit is a run-ending trap that reads as fine on inspection.

**What the gates found during authoring — none of this was theoretical:**

- **6 pickups buried inside solid geometry**, including a crew member inside the oil press body and both artifacts inside pit geometry. All relocated.
- **2 routes completely sealed, and 2 pickups unreachable.** Root cause: **box colliders on debris meshes.** A grain spill (box 9 × 1.1 × 7, top surface 0.85 m) walled off the entire western silo approach, and a 16 m fallen beam sealed the west corridor — both unclimbable against a 0.32 m step offset. Fixed by making all 3 grain spills and all 3 fallen beams non-colliding decoration.
- **Props attached to walls that do not exist** — a map board, a notice board, and four respirator hooks were floating in mid-air at a Z where there is no wall. Found by manual review, not by a gate.
- **Shelving in the wrong zone** — labelled `Shelving_Silo` but placed in the admin zone.
- **One pickup with the wrong material** — `item_bandage` had `M_Pickup_Document`.
- **My own arithmetic was wrong once.** I hand-calculated a ration as buried and the validator disagreed; the validator was right (I had used a scale value as a full height rather than a half-extent). This is precisely why the gate was worth building.
- **A real bug in my own new code:** the first `FluorescentFlicker` wrote `sharedMaterial`, which would have made all 15 fixtures pulse in lockstep and left the shared `.mat` dirty in the Editor. Fixed with `MaterialPropertyBlock`.

**Negative control.** Deleting `Pit_Ramp` and re-running correctly produces *"cannot reach the bunker after taking Pickup_item_artifact_ember — dead end."* A gate never observed failing is decoration; this one fails when it should.

**Independent post-checks on the emitted file:** parses under PyYAML as 1754 documents; no duplicate fileIDs; `m_Children`/`m_Father` mutually consistent; `SceneRoots` matches the 6 real roots; all 388 component back-references valid; all quaternions unit-length.

**Unity itself accepted the assets** despite the dead bridge: it auto-imported them, created folder `.meta` files, compiled shader variants for the 22 new materials, and `Logs/AssetImportWorker0.log` showed a clean domain reload.

**Compile gate: `python tools/verify_steam_layer.py` → 39/39 ALL GREEN**, re-run live during this session. `FluorescentFlicker`, `ScavengePhase3DState`, `DebugRunLauncher`, and `ScavengePickup` were confirmed present in the produced assembly, so they compiled in rather than merely parsing.

### 6.9 What was committed

```
branch  feat/scavenge-3d-scene
commit  e7b03db
msg     feat: 3D Scavenge scene (Phase A — Collapsed Grain Depot)
        — playable 60-second blowout level
stat    61 files changed, 43291 insertions(+), 13 deletions(-)
```

Contents: the scene + `.meta`, 22 URP Lit materials + metas, `ScavengeVolumeProfile.asset` + meta, the two generator scripts, `FluorescentFlicker.cs` + meta, the four modified C# / settings files, and the `CLAUDE.md` §0 update.

---

## 7. THE BLOCKER: NOTHING HAS BEEN RUN

**This is the honest headline and it should drive the review.**

Because the Unity bridge was unreachable (§6.2), the following were **never executed**:

- The scene has never been opened in the Editor.
- The trigger has never fired.
- The HUD has never drawn a countdown.
- No pickup has ever been picked up.
- The full flow `_Bootstrap → MainMenu → RunSetup → Scavenge → Bunker → wipe → RunFailed` has never run.
- **No smoke test was re-run today.**

Everything in §6 is static analysis plus Unity's asset importer accepting the files. That is a genuinely strong form of evidence for *structural* correctness — references resolve, geometry is navigable, ids exist, it compiles into the assembly — and **no** evidence at all about *behaviour*.

**The fix is one setting, and it is Dan's to make:** in Unity's *MCP For Unity* window, switch **Transport** off `HTTP Local`. Discovery runs per call, so no restart is needed on the agent side. After that the Editor can be driven live, the scene opened, Play mode entered, and the console read.

---

## 8. THE PLAN — WHAT REMAINS TO SHIP

Ordered. Items 1–3 are ship blockers; nothing after them matters if they fail.

### 1. Editor-side wiring (**must be done by hand in Unity — cannot be scripted from outside**)
- Run `Tools → Oblast Zero → Register All States` (`StateRegistrationTool`) on `_Bootstrap.unity`, then **save the scene**. Without this, `RunFailed` and the four `RunVictory_*` states log "No state registered" and **a wipe dead-ends** — the run cannot end.
- Create a `SteamConfig` asset with the real App ID. `Bootstrap` already auto-loads `Resources/SteamConfig` and initialises offline-safe, so this is data entry, not code.

### 2. Live Play-mode verification of the full loop
Boot → MainMenu → RunSetup → Scavenge (the new level) → TransitionCutscene → Bunker → End Day ×N → wipe → `RunFailedState` summary → back to MainMenu. **Zero console errors.** Re-run all five smoke tests via `execute_code` while the Editor is live, since CI cannot see them.

### 3. Runtime data-loader verification
Confirm `GameDatabase.Initialize` actually ingests all **1020 events and 691 items** at runtime, and that `LocalizedStrings` populates from `Assets/Data/Resources/Locale/localization_en.json` — right now localization keys render raw, which is the symptom of the table not loading.

### 4. Scavenge tuning + close the carry-weight gap
Play the level. Check pacing against the ~18 s / ~40 s budget, whether the pit detour earns its risk, and whether the fluorescents read as *failing* rather than as *strobing*. Then enforce `SCAVENGE_MAX_CARRY_WEIGHT_KG` (see §9.2).

### 5. Missing UI screens
Main menu, run setup, and run summary do not exist. The states exist; their screens do not.

### 6. Content QA
Spot-check the 1020 generated events for §3.2 voice compliance and §3.1 IP-firewall violations, and verify every `successChanceFormula` string parses in `FormulaEvaluator`. A tool for this (`tools/content_qa.py`) appeared mid-session from a concurrent session and is currently **untracked**.

### 7. Polish, then ship
Audio, VFX, UX tuning, Steam store page with the correct AI disclosure tier.

---

## 9. GAPS AND RISKS, RANKED

### 9.1 — CRITICAL: the game has never been played end-to-end
Six weeks from a content-complete beta, with a state machine, two phase scenes, an event engine, and 1711 content objects that have **never executed together**. Integration bugs are not merely likely, they are near-certain, and they are currently invisible. Every other item on this list is smaller than this one.

**Compounding factor:** the only tests that exist are `[ContextMenu]` MonoBehaviours. **CI cannot run them.** There is no automated behavioural regression net at all — the 39-check gate proves compilation, not conduct.

### 9.2 — HIGH: `SCAVENGE_MAX_CARRY_WEIGHT_KG` is enforced nowhere
`InventoryManager.AddItem` has **no weight gate**, and `GetTotalWeight()` exists but **nothing calls it during the scavenge**. The constant (25 kg) is declared and ignored.

The player can currently carry the entire depot. That deletes the central decision of Phase A — *what do I leave behind* — and with it the reason the 60-second clock and the three-route layout exist. **The level I just built is balanced around a constraint the code does not apply.** Found in passing; not fixed, because `InventoryManager` was outside "build the scene" and has tests around it. Recorded as `CLAUDE.md` §0 remaining item 4.

One gate in `AddItem` plus one HUD readout is the small half of the fix. The other half is §9.3, and without it the gate does nothing.

### 9.3 — HIGH: the 25 kg cap is numerically unreachable, so enforcing it changes nothing

**[VERIFIED] this session** by summing the manifest in §6.6 against the `weightKg` field in the live item database:

```
taking EVERY item in the level (22 stacks)  =  16.59 kg  =  66% of the 25 kg cap
heaviest single item in the manifest        =   2.00 kg  (item_pry_bar)
```

So a player who strips the depot bare still walks out **8.4 kg under the limit**. Implementing §9.2 perfectly would produce a gate that never fires.

The manifest is not the problem — **the content is**. Across all **691 items**:

```
max weight        3.15 kg   (item_kafedra_issue_drill_bit)
mean              0.64 kg
median            0.47 kg
items over 5 kg   0
items over 3 kg   1
items over 2 kg   17
heaviest category Tool (mean 1.14, max 3.15) and Weapon (mean 1.17, max 2.99)
```

Reaching 25 kg requires **~8 copies of the single heaviest item in the game**, or ~39 average items. No plausible 60-second haul gets close. Note also `item_12_gauge_carbine` at **0.73 kg** — a real 12-gauge is roughly 3 kg — so the generated weights skew light for exactly the bulky objects that should force a choice.

Three levers, and this is a design call (see §11 Q3): recalibrate `weightKg` across 691 items; lower the cap to roughly **8–12 kg** to match the content as generated; or drop weight as a constraint and let the 60-second clock be the only currency. **The cheapest is lowering the cap**; the most faithful to "what do I leave behind" is probably a heavy-item pass so that a carbine or a fuel can actually costs something.

### 9.4 — HIGH: run-end dead-ends until the states are registered by hand
Until `StateRegistrationTool` is run and `_Bootstrap.unity` saved, a wipe cannot resolve. This is a save-the-scene-in-the-Editor action that no external process can perform.

### 9.5 — MEDIUM: three UI screens missing
Main menu, run setup, run summary. The states drive nothing visible. An Early Access build without a main menu is not shippable.

### 9.6 — MEDIUM: 1711 content objects, quality unaudited
691 items and 1020 events were mass-generated. Two specific risks: **IP-firewall violations** (§3.1 — a legal exposure, not a polish item) and **voice drift** (§3.2). Plus a mechanical risk: any `successChanceFormula` string that fails to parse in `FormulaEvaluator` is a runtime break in an event nobody has seen yet.

### 9.7 — MEDIUM: concurrent sessions share this repo
More than one Claude Code session works here simultaneously. During this session, `main` advanced three commits (`f0e00bb`, `57dfdc5`, `7706a76`) and `tools/content_qa.py` appeared at 23:15. My work is isolated on a branch with only my own paths staged, and the other session's file was left untracked and untouched. **Practical rule for anyone working here: re-check `git status` and `git log` before staging, stage explicit paths, and never `git add -A`.**

### 9.8 — LOW: Steam App ID is a placeholder
Blocks a real store build, but is trivial once the App ID exists.

### 9.9 — LOW: `Assets/Scenes/SampleScene.unity` is leftover Unity template cruft
Dated Jan 2025. Not in Build Settings. Should be deleted.

---

## 10. WHAT CHANGED IN THIS SESSION, IN FULL

**Added:**
- `Assets/Scenes/Scavenge.unity` (+ `.meta`) — 388 GameObjects, the Phase A level
- `tools/generate_scavenge_scene.py` — 1107 lines, the level plan and its four verification gates
- `tools/scavenge_scene_lib.py` — 701 lines, Unity YAML emitters
- `Assets/_Project/Scripts/OblastZero.Gameplay/FluorescentFlicker.cs` — 124 lines
- 22 URP Lit materials in `Assets/Art/Materials/Scavenge/`
- `Assets/Settings/ScavengeVolumeProfile.asset` — vignette, color grading, film grain, tonemapping

**Modified:**
- `Core/States/ScavengePhase3DState.cs` — additive scene load/unload via `ISceneLoader`; reads `BalanceConstants` instead of a serialized duplicate
- `OblastZero.Gameplay/DebugRunLauncher.cs` — opt-in, compiled out of release builds
- `Core/States/RunSetupState.cs` — stale "Phase A is headless" comment removed
- `ProjectSettings/EditorBuildSettings.asset` — `Scavenge` at index 2
- `.gitignore` — `tools/__pycache__/`
- `CLAUDE.md` — §0 status rows for scenes and Phase A; remaining-priority item 4 rewritten to name the carry-weight gap; **§11 corrected** (the bridge/compile-error claim was wrong and cost a session real time); **new §14** documenting how to author Unity scenes without the Editor

**Found but deliberately not changed:** the `SCAVENGE_MAX_CARRY_WEIGHT_KG` gap (§9.2) — out of scope for "build the scene," and `InventoryManager` has tests around it. Documented instead of quietly patched.

---

## 11. REVIEW QUESTIONS — WHERE OUTSIDE JUDGEMENT IS ACTUALLY WORTH SPENDING

A status dump invites a status-dump response. These are the open questions.

1. **Six weeks, one unplayed integration.** Given §9.1, is "wire the Editor, then play the loop" the correct next move, or should a subset be cut *now* to shrink the untested surface before it is ever run? Which of §8's items 4–6 would you cut first if the loop turns out to be badly broken?

2. **The carry-weight gap (§9.2) inverts a design premise.** Is the right fix a hard gate in `AddItem` (refuse the pickup, HUD says why), a soft one (accept it, apply a movement-speed penalty — which interacts with the 60-second clock and the ~40 s detour budget), or a swap-prompt? The hard gate is simplest; the soft gate is arguably the better game. Which serves "what do I leave behind" better under a 60-second clock, and what does each do to the pit-detour risk calculus?

3. **The weight economy is incoherent, and the fix is a design call (§9.3).** I did the arithmetic: the entire level is **16.59 kg against a 25 kg cap**, and the heaviest of all 691 items is **3.15 kg**. Which lever? (a) A heavy-item content pass — most faithful to the design intent, but it touches 691 files and risks disturbing trade values that events may depend on. (b) Drop the cap to ~8–12 kg — one constant, ships today, but then "heavy" means *four tools* rather than anything a player would intuit as heavy. (c) Abandon weight and let the clock be the only currency — honest, and arguably fine for a 60-second phase, but deletes a locked-ish design pillar. **My inclination is (b) now and (a) after launch**; argue me out of it if the reasoning is wrong.

4. **The test net.** The only tests are Editor-only `[ContextMenu]` MonoBehaviours that CI cannot run. Is converting them to NUnit worth the days it costs six weeks out, or is a manual play-checklist the correct trade for an Early Access launch?

5. **Is ~18 s direct / ~40 s detour the right ratio** for a 60-second panic phase, and do three routes plus one high-risk detour give enough replay variety for a roguelite where this phase runs every single run? Bear in mind these numbers are geometric, not measured.

6. **Content risk (§9.5).** 1711 generated objects, unaudited, with an absolute IP firewall. Is a sampling audit defensible for launch, or does the legal exposure demand a full mechanical scan for the forbidden name set before the store page goes up?

7. **Anything in §6 where structural verification is masquerading as behavioural verification.** I have tried to label these honestly, but I built the thing — a reviewer with no stake in it is better positioned to spot where "all references resolve" got quietly upgraded to "it works."

---

## 12. APPENDIX

### 12.1 Commands

Compile gate — 39 checks, runs while Unity holds the project lock:
```bash
cd ~/projects/OblastZero && python tools/verify_steam_layer.py
```

Regenerate the scavenge level (deterministic; self-verifies; refuses to write on failure):
```bash
cd ~/projects/OblastZero && python tools/generate_scavenge_scene.py
```

Diagnose the Unity bridge before blaming a compile error:
```bash
netstat -ano | grep LISTENING | grep -E ":(8090|64[0-9][0-9])"
```

### 12.2 Key file paths

```
CLAUDE.md                                     the law — §0 status, §12 compile gate,
                                              §13 plugin pitfalls, §14 scene authoring
DESIGN_BIBLE_Сlaude.Opus4.7.md                the reference — 7 sections, ~1300 lines
PLAYMODE_CHECKLIST.md                         manual verification checklist
OPUS5_SCAVENGE_PROMPT.md                      the brief this session executed
Assets/Scenes/{_Bootstrap,Bunker,Scavenge}.unity
Assets/_Project/Scripts/Core/BalanceConstants.cs      all tuning numbers
Assets/_Project/Scripts/Core/States/                  11 IGameState implementations, 8 files
Assets/Data/Resources/{Items,Events,Locale}/          generated JSON content
tools/{generate_scavenge_scene,scavenge_scene_lib}.py the level and its emitters
tools/verify_steam_layer.py                           the 39-check gate
Books_STALKER/                                        GITIGNORED. Tone reference only.
                                                      Never publish. Never mine names.
```

### 12.3 Contracts worth knowing before touching the scavenge scene

`ScavengePickup` serialized fields: `kind` (`PickupKind.Item` | `PickupKind.Crew`), `dataId`, `quantity`, `durabilityOverride` (−1 = none), `contamination`. Exposes `InteractionVerb` → `"Rescue"` for crew, `"Take"` for items.

`ScavengeController` lives in the scene, listens for the player's pickup requests, and routes them into the run-scoped managers on the persistent `GameManager`: items to `InventoryManager.AddItem` on the Scavenged channel, crew to `CrewManager.AddRescued`. On success the world object is destroyed — instant kinematic pickup. **A run must be active (`GameManager.BeginNewRun`) before scavenging works.**

`ScavengePhase3DState` owns the clock and the two end conditions (timer expiry, or `ReachBunkerEvent` from the in-scene trigger). Both transition to `TransitionCutscene`, which commits the haul.

Bunker UI ↔ logic contract: HUD raises `EndDayRequestedEvent` and `EventChoiceSelectedEvent`; `SurvivalPhase2DState` is the **only** subscriber that turns them into `BunkerPhaseController` calls. All three HUDs build their own canvas on `Awake` — adding the component to a GameObject is the whole installation.

---

*End of report. Generated 25 Jul 2026 by Claude Opus 5 from a live read of the repository at `feat/scavenge-3d-scene` @ `e7b03db`, with the 39-check compile gate and the scene generator both re-run to confirm the numbers quoted above.*
