# CLAUDE.md — Oblast Zero

Always-on rules for every Claude Code session in this repo. Read this first. It is the law; the design bible is the reference.

---

## 0. Status & timeline — update this section every session

**Deadline: content-complete beta by 31 Aug 2026 (~6 weeks from 20 Jul 2026) → Early Access ship mid-September 2026.** Early Access is the ship vehicle, not a scope cut.

**Verified build status** (confirmed by reading the actual code on 20 Jul 2026, not assumed):

| Layer | Status |
|---|---|
| Core framework (state machine, EventBus, ServiceLocator, GameManager, bifurcated save) | ✅ Built |
| Data schemas (ItemData, ExpeditionEventData, FactionData, AnomalyData, MutantData, CrewMemberData, TraitData, GameDatabase) | ✅ Built |
| Content instances (actual `.asset` files using those schemas) | 🔶 ~14 seed assets + 3 seed events (`Assets/Data/Definitions/Events/`, in Oblast voice, referencing seed items/factions) — need 500+ items / 1000+ events |
| Scenes & bootstrap rig | ✅ Built 21 Jul 2026, scavenge level added 25 Jul 2026 — `Assets/Scenes/_Bootstrap.unity` (GameManager + GameStateMachine + all 6 implemented state components as children + EventSystem/InputSystemUIInputModule, `gameDatabase`+`stateMachine` refs wired), `Assets/Scenes/Bunker.unity` (camera + `BunkerUI` GameObject with both HUDs), and `Assets/Scenes/Scavenge.unity` (the 3D level). All three in Build Settings (`_Bootstrap`=0, `Bunker`=1, `Scavenge`=2). `SurvivalPhase2DState` and `ScavengePhase3DState` each load/unload their scene additively via `ISceneLoader`. **Play from `_Bootstrap`.** |
| Scavenge phase, Phase 1 (3D) | ✅ Playable 25 Jul 2026 — `Assets/Scenes/Scavenge.unity`, "Collapsed Grain Depot" (Outer Cordon). 388 GameObjects, 104×72 m, six zones (silo base + grain intake pit, warehouse floor, bunker stairwell, rail siding, admin/office, loading dock), three routes to the door plus a contaminated pit detour, 25 pickups on real database ids (22 items + 3 crew), 15 flickering fluorescents (`FluorescentFlicker`), URP volume, linear fog. **The scene is generated, not hand-saved** — `tools/generate_scavenge_scene.py` owns the coordinate plan and re-emits it deterministically; edit the plan, not the YAML, or the next regeneration overwrites you. That script self-verifies: every pickup id against the live database, every fileID/GUID reference, pickups neither buried nor floating, and a walkability flood-fill proving the bunker and all 25 pickups are reachable *and* escapable (negative control: deleting `Pit_Ramp` correctly fails it). |
| Hybrid 2.5D rendering (`HybridDepthRenderPass`) | ✅ Implemented, not a stub |
| Bunker day loop, Phase 2 (2D) | ✅ Built 20 Jul 2026 — `BunkerDayController` (day tick) + `BunkerPhaseController` (turn = day tick → present event → resolve; blocks a new day while an event is pending; `IsWipe`). `SurvivalPhase2DState` drives it from UI intents (`EndDayRequestedEvent`/`EventChoiceSelectedEvent`) and resolves run-end. `BunkerPhaseSmokeTest` 17/17 pass. |
| Event engine (resolves `ExpeditionEventData` branching choices) | ✅ Built 20 Jul 2026 — `EventEngine` (selection/gating/resolution) + `RunRng` (deterministic seed+counter) + `FormulaEvaluator` (`successChanceFormula`) + `CrewFormulaContext`. Wired into `GameManager` (`.Events`), bridged to EventBus. `EventEngineSmokeTest` 24/24 pass. Owns `CompletedEventIds`/`QueuedEventIds`; all other effects delegate to managers. |
| Faction reputation manager | ✅ Built 20 Jul 2026 — `FactionReputationManager` (sole owner of `repScaleSociety/Cordon/Kafedra`, clamps to `BalanceConstants.REPUTATION_MIN/MAX`, bridged to EventBus). `GameManager.Reputation`. |
| UI | ✅ Screens complete 26 Jul 2026 — `BunkerHUD` + `EventModalUI` (in `Bunker.unity`), `ScavengeHUD`, and the three run-flow screens `MainMenuUI` / `RunSetupUI` / `RunSummaryUI` (`Assets/_Project/Scripts/UI/`, namespace `OblastZero.UI`), all self-building EventBus-driven canvases raising intents only. Shared construction vocabulary in `OblastUI` (palette, anchoring, button states) — **use it for new screens; never `LayoutElement` without a parent layout group, which is inert and piles everything at screen centre.** The three run-flow screens are spawned/destroyed by their states, so they need no scene wiring. **TMP essentials must be imported** or all text renders blank. Localization keys render raw via `LocalizedStrings` until a language table loads. |
| Save/load round-trip test | ✅ Verified 20 Jul 2026 — `DataLayerSmokeTest` 24/24, `BunkerDayLoopTest` runs clean (6-day starvation loop, deaths + EventBus events fire). NOTE: these are `[ContextMenu]` MonoBehaviours, NOT NUnit — Test Runner doesn't see them; run via `execute_code`. |
| Steamworks integration | ✅ Built + compiles 24 Jul 2026 — `Assets/_Project/Scripts/Steam/` (SteamManager, SteamAchievementsService, SteamStatsService, SteamCloudSave, SteamEventBridge, SteamConfig SO). Uses **Facepunch.Steamworks 2.x API: namespace `Steamworks`, NOT `Facepunch.Steamworks`** — `SteamClient.Init(appId, asyncCallbacks:false)`, `new Achievement(key).Trigger()`, `SteamUserStats.SetStat/GetStatInt/StoreStats`, `SteamRemoteStorage.FileWrite/FileRead/FileExists`. Only **Win64** managed DLL + `steam_api64.dll` are under `Assets/Plugins/` (Posix/Win32/steam_api parked in `tools/steam_plugins_extra/` — shipping all three managed DLLs causes CS0433 duplicate-type errors since they share the `Steamworks` namespace). `STEAMWORKS` define is set for Standalone in ProjectSettings. **Not yet placed in a scene** — SteamManager.Initialize(cfg) still needs calling from Bootstrap and a SteamConfig asset created. |
| Repo ↔ GitHub sync | ✅ Fixed 20 Jul 2026 — `main` matches `origin/main`, full project committed, `Books_STALKER/` correctly gitignored |
| Unity MCP bridge | ✅ Connected 20 Jul 2026 — Claude Code must be launched from this folder (`C:\Users\danil\projects\OblastZero`) for the bridge to attach |

**Next steps, in priority order:**

**✅ DONE since 21 Jul (verified by reading git log + a real `dotnet build` on 24 Jul 2026):**
- Stage 1: `RunFailedState` + 4 `RunVictory*States` (end-run UI, ending unlocks) — commit `a952895`
- Stage 2: `MainMenuState` + `RunSetupState` (menu UI, crew/site select) — commit `dc2731d`
- Stage 3: `EventJsonLoader` (JSON → `ExpeditionEventData`, hooked into `GameDatabase.Initialize`) — commit `f849334`
- Stage 5: content blitz — **691 items + 1020 events** as one-object-per-file JSON in `Assets/Data/Resources/{Items,Events}/`, plus `ItemJsonLoader` — commit `d707f0c`
- Stage 6: Steamworks wrapper — commit `912ab4c`
- Stage 7: EN localization table + GitHub Actions CI + SteamCMD deploy script — commit `e0418df`
- `StateRegistrationTool`: one-click Editor menu to register all 7 states under `GameStateMachine` — commit `23faa62`
- **Compile blocker fixed** — commit `55ffd4a`. Steam code targeted a Facepunch 1.x API that does not exist in the shipped 2.x DLL (`Facepunch.Steamworks.*` → `Steamworks.*`), and three managed DLLs sharing one namespace were staged at once. Verified green with a real `dotnet build` of `Assembly-CSharp.csproj` (0 errors, all Steam + state types present in the output assembly).

**🔜 REMAINING, in priority order:**
1. **Editor-side wiring (must be done by hand in Unity, cannot be scripted from outside):**
   - Run `Tools → Oblast Zero → Register All States` (the `StateRegistrationTool`) on `_Bootstrap.unity`, then save the scene. Without this, `RunFailed`/`RunVictory_*` still log "No state registered" and a wipe dead-ends.
   - Create a `SteamConfig` asset (`Assets → Create → OblastZero/Steam/Config`), set the real App ID, and call `SteamManager.Initialize(cfg)` from `Bootstrap` before `GameManager` boot. Add `SteamEventBridge` to the same GameObject.
2. **Live Play-mode verification of the full loop with the new states**: boot → MainMenu → RunSetup → scavenge (headless) → bunker → End Day ×N → wipe → `RunFailedState` summary → back to MainMenu. Zero console errors.
3. **JSON loader verification at runtime**: confirm `GameDatabase.Initialize` actually ingests all 1020 events / 691 items and that `LocalizedStrings` populates from `Assets/Data/Resources/Locale/localization_en.json` (keys should stop rendering raw).
4. **Scavenge tuning**: the level exists and verifies clean, but it has never been played. First Play-mode pass should check pacing (direct route is ~18 s of the 60 s budget, leaving ~40 s of detour), whether the pit detour is worth its risk, and that the fluorescents read as failing rather than as strobing. ~~Carry-weight gap~~ **closed 26 Jul 2026** — see "Carry weight" below.
5. **Content QA pass**: spot-check generated events for bible §7 voice compliance and IP-firewall violations; verify `successChanceFormula` strings all parse in `FormulaEvaluator`.
6. Polish pass (audio, VFX, UX tuning), then ship Early Access.


**Carry weight** (closed 26 Jul 2026). `SCAVENGE_MAX_CARRY_WEIGHT_KG` is now **15** and is enforced in `InventoryManager.AddItem` on the Scavenged channel only — the Bunker channel stays uncapped. Refusals are **all-or-nothing**: an over-cap pickup returns null, nothing partially fills, and `ScavengeController` already leaves the world object in place, so the player keeps the choice. `ScavengeLoadChangedEvent` / `ScavengePickupRejectedEvent` reach the HUD via `ManagerEventBridge`; `ScavengeHUD` draws a load bar and a refusal notice. Capacity is settable (`InventoryManager.ScavengeCarryCapacityKg`) for a future per-crew override — **note `CrewMemberData.baseStats.carryCapacityKg` (Marina 22 / Yuri 28 / Sasha 34) is authored but still read by nothing**, so the RunSetup roster displays it as a crew stat, not as the scavenge cap. Item weights were rebalanced by `tools/rebalance_weights.py` (deterministic, idempotent, `--check` gate; covers **both** the 703 Resources JSON items *and* the 8 authored `.asset` items — touching only one leaves the depot half-light). Depot loot went 16.59 kg → **28.72 kg against a 15 kg cap**, so the player takes ~52% of the floor and 6 pickups cost >2 kg each.

**Bunker UI ↔ logic contract** (for scene/flow work): HUD raises `EndDayRequestedEvent` and `EventChoiceSelectedEvent`; `SurvivalPhase2DState` is the ONLY subscriber that turns them into `BunkerPhaseController` calls. HUDs refresh off `DayAdvancedEvent`, `CrewStatChangedEvent`, `CrewDiedEvent`, `BunkerInventoryChangedEvent`, `FactionReputationChangedEvent`, `EventPresentedEvent`, `EventResolvedEvent`. Both HUDs build their own canvas on `Awake` — just add the component to a GameObject in the bunker scene.

**Event Engine API quick-reference** (for the UI/day-loop work): `engine.SelectNextEvent(regionTags, actingCrewInstanceId)` → `ExpeditionEventData?` (queued follow-ups take priority over the weighted pool); `engine.AvailableChoiceIndices(evt, actingId)` for enabling buttons; `engine.Resolve(evt, choiceIndex, actingId)` → `EventResolution` (full effect report). Subscribe to `EventPresentedEvent` / `EventResolvedEvent` / `FactionReputationChangedEvent` on the `EventBus` for UI refresh. `successChanceFormula` variables: `crew.combat`, `crew.charisma`, `crew.{health,sanity,fatigue,radiation}[_norm]` (see `CrewFormulaContext`).

---

## 1. What this is

**Oblast Zero** — a commercial roguelite survival game shipping on Steam. Solo dev (Leonid), heavy agentic workflow. This is a **full commercial release**, not a prototype or portfolio piece. Build production-quality, complete, modular code. Be direct and decisive — no hedging, no "you might consider," no suggestions to scale the project down.

**Core loop — two phases:**
- **Phase 1 — The Blowout** (3D first-person): a 60-second real-time panic scavenge. Grab supplies, artifacts, and squadmates into the bunker before the emission hits. Pickup is **instant kinematic** (snap to inventory — *not* a physics grab).
- **Phase 2 — The Bunker** (2D management): turn-based survival. Manage rations, crew health/sanity/radiation, and resolve data-driven narrative events (factions, anomalies, mutant attacks).

Original IP. (Old codename in legacy docs: "Project Halo." Shipping name is **Oblast Zero**.)

---

## 2. Locked decisions — DO NOT re-litigate

| Decision | Value |
|---|---|
| Engine / render | Unity 6 LTS + URP. Custom 2.5D hybrid via `HybridDepthRenderPass` / `HybridDepthRendererFeature` sharing a depth buffer between the 3D and 2D passes. **Engine is decided — never propose Godot/Unreal.** |
| Language | C# |
| Repo | private GitHub `Danila1801/OblastZero`, Git LFS for binaries. Repo is the source of truth. |
| Save architecture | **Bifurcated** — permadeath run data separated from persistent meta-progression. Atomic dual-channel JSON writes. Newtonsoft JSON (installed) is the serializer — **not** `JsonUtility` (it silently drops `Dictionary` and mangles `DateTime`). |
| Death salvage rate | 33% — a **named constant in `BalanceConstants`**, never a magic number. |
| Factions (original IP) | Scale Society (bureaucratic exploitation), Cordon (militaristic hostility), Kafedra (scientific exploitation). |
| Central mystery | "The Reality Distortion Field." |

---

## 3. Architecture standard

- **Strict separation of concerns. Logic is decoupled from UI.** UI reads state and raises intents; it never owns game logic.
- **Game State Machine** drives `MainMenu → ScavengePhase3D → Transition_Cutscene → SurvivalPhase2D` (+ run-end states). Every state is a `MonoBehaviour` implementing `IGameState`. The machine is a singleton on a `_Bootstrap` scene that is never unloaded.
- **Phase 2 is fully data-driven.** Events, items, and crew stats come from ScriptableObjects / serialized containers so content can be mass-generated. **Must scale to 500+ items and 1000+ text events with no performance drop** — no per-frame `Resources.Load`, no linear scans of the full content set on the hot path; index by `id`.
- **Modular & DRY.** Interfaces and inheritance where they earn their place. Design to scale.
- **Communication is event-driven** via `EventBus` / `GameEvents`. Prefer raising an event over a hard cross-system reference. Resolve services through `ServiceLocator`, not `FindObjectOfType`.

---

## 4. Folder & namespace map

**Two roots.** Framework code under `Assets/_Project/`; data definitions under `Assets/Data/` (the bible specifies this — respect it).

```
Assets/_Project/Scripts/
  Core/          → OblastZero.Core   (state machine, RunData, MetaProgressData, IGameState,
                                       StateContext, EventBus, GameEvents, ServiceLocator,
                                       GameManager, BalanceConstants, GameState enum, Bootstrap)
    States/      → OblastZero.Core   (the IGameState implementations)
  Services/      → OblastZero.Services (SaveService/SaveSystem, SceneLoader, …)
  Rendering/     → OblastZero.Rendering (HybridDepthRenderPass / RendererFeature / settings)
  Gameplay/      → OblastZero.Gameplay (run-scoped managers: InventoryManager, CrewManager, … — sole owners of RunData mutations; raise C# events on change)
  UI/            → OblastZero.UI (HUD / menus / panels)

Assets/Data/Scripts/Definitions/
                 → OblastZero.Data   (all ScriptableObject schemas; base class GameDataObject)
```

**Namespace rule:** `OblastZero.<Layer>`. Match the namespace to the folder. **File name == primary type name.** Closely related small records may share a file with their owner (e.g. `ItemInstance` / `CrewInstance` / `ActiveExpedition` live in `RunData.cs`).

**ScriptableObject conventions (all inherit `GameDataObject`):**
- `[CreateAssetMenu(menuName = "OblastZero/<Type>", fileName = "<Prefix>_")]`
- Existing types & menus: `OblastZero/Faction` (`Faction_`), `OblastZero/Anomaly` (`Anomaly_`), `OblastZero/Mutant` (`Mutant_`), `OblastZero/Crew Member` (`Crew_`), `OblastZero/Item` (`Item_`), `OblastZero/Expedition Event` (`Event_`).
- `GameDataObject` base fields: `id` (stable, never localized — used for saves/JSON/Steam stats), `displayName` (localize), `designerNotes`.

---

## 5. Coding standards (output rules)

- **No placeholders. Ever.** No `// TODO`, no `// add logic here`, no stubbed method bodies. Write the complete, functional implementation. If a feature needs multiple scripts, deliver all of them.
- **Explain before coding.** Lead with a short architectural overview of how the scripts connect, then the code. Keep the prose tight.
- **Debug-ready.** Robust `Debug.Log` on critical state changes (state transitions, save/load, run start/end, expedition resolution) so flow is traceable in the Editor. Don't log-spam hot paths.
- **Production quality, not scaffolding.**
- Reference balance values from `BalanceConstants` — never inline magic numbers.
- Public data fields that must serialize use Unity-serializable types; mark serializable containers `[System.Serializable]`. Add `[Tooltip]`/`[Header]` on designer-facing SO fields.

---

## 6. Data & persistence rules

- **`RunData`** = the single source of truth for one permadeath run (rebuilt every new run). **`MetaProgressData`** = persistent cross-run progression (loaded once, survives death). They live in **separate save channels**.
- **All mutation of `RunData` / `MetaProgressData` goes through manager classes** (InventoryManager, CrewManager, FactionReputationManager, …). Nothing outside the owning manager writes these fields directly.
- **3D → 2D handoff:** the 60-second scavenge fills `RunData.ScavengedInventory` / `RescuedCrew`; the transition cutscene commits them into `BunkerInventory` / `ActiveCrew`. Always account for how data crosses the phase boundary and how a run serializes for save/load.
- Runs are seed-reproducible: mutate RNG through the seed + stream counter on `RunData`, not ad-hoc `Random`.

---

## 7. The design bible is law

`DESIGN_BIBLE_Сlaude_Opus4_7.md` (repo root) is authoritative for faction/anomaly/mutant taxonomy, the C# data schemas (§6.1), JSON event payloads (§6.2), the state machine & data flow (§6.3), and the content voice (§7). **Read the relevant section before generating any new system or content.** Do not invent lore, schema fields, or systems that contradict it.

---

## 8. IP firewall — absolute

The loaded S.T.A.L.K.E.R. novels are a **tone/atmosphere reference only**. Mine the *vibe* — fatalistic stalker campfire talk, rust, dread, bureaucratic indifference, how people behave under extreme conditions. **Never reuse any name, location, faction, or mutant from those books or games** (no Strelok, Scar, Sidorovich, Pripyat, ChNPP, Duty, Freedom, etc.). Everything ships as original Oblast Zero lore.

---

## 9. Content voice (when writing events / docs / dialogue)

Post-**administrative**, not post-apocalyptic. Soviet/post-Soviet bureaucratic register: *registered, line item, deviation, protocol, pending review, quota, requisition, standing order*. Concrete is *stained* not *broken*; equipment *operational* never *new*. Redact unevenly with `[REDACTED]`. Forbidden pulp clichés: "twisted metal," "eerie silence," "unnatural glow," "screams in the distance." **The Oblast does not raise its voice. The Oblast files a form.** Full rules in bible §7.

---

## 10. Shipping gates (flag briefly when relevant — this protects the release)

- **Steam AI disclosure (two-tier, 2026):** dev tools (Claude Code, MCP, Copilot) are **exempt**. Player-facing AI-made content (art, audio, narrative, localization) needs an accurate **Pre-Generated** disclosure on the store page. The current data-driven event system stays in the simpler tier; a live-LLM runtime feature would require the **Live-Generated** tier + guardrails.
- **Music rights:** Suno paid tiers grant a commercial license; fully AI-generated music isn't US-copyrightable. Disclose, back up stems, human-in-the-loop anything load-bearing.
- **Asset rights:** verify commercial terms per tool (Meshy/Rodin/Nano Banana Pro). Quad-remesh AI meshes in Blender before Unity import.

---

## 11. Workflow notes

- **Unity MCP bridge: if tools report "No Unity Editor instances found," check the transport before you go hunting a compile error.** (Corrected 25 Jul 2026 — this section previously blamed compile errors, which sent a session chasing a phantom problem while `verify_steam_layer.py` sat at 39/39 green.) A Claude Code session's MCP server is launched over **stdio** and discovers Unity by scanning its direct TCP bridge port (6400-ish). If Unity's *MCP For Unity* window has Transport set to **HTTP Local** (`http://127.0.0.1:8090`), Unity dials out to its own Python HTTP server and **never opens that TCP port** — so discovery finds zero instances even though Unity's window shows a green "Session Active". Diagnose, don't guess:
  ```bash
  netstat -ano | grep LISTENING | grep -E ":(8090|64[0-9][0-9])"   # only 8090 listening = HTTP Local mode
  tail -20 ~/AppData/Local/UnityMCP/Logs/unity_mcp_server.log      # logs "Discovered 0 Unity instances"
  ```
  Fix: switch Transport off HTTP Local in that window. No restart needed on the agent side — discovery runs per call. Compile errors are a *separate* failure mode; confirm which one you have.
- Unity must be open for live scene/console access, and Claude Code must be launched from this folder.
- **Unity still auto-imports files written to `Assets/` by an outside process.** Folder `.meta` creation and shader-variant compilation in `Logs/AssetImportWorker0.log` are usable evidence that hand-authored assets were accepted, even with the bridge down.
- Roadmap (mental model): 0–1 Core framework → 2 Data layer → 3 3D scavenge → 4 2D bunker engine → 5 Event system → 6 Meta-progression → 7 Polish → 8 Steamworks (Facepunch.Steamworks) → 9 Content blitz → 10 Beta/ship.

---

## 12. Verifying a compile WITHOUT Unity (learned the hard way, 24 Jul 2026)

**Just run this — it does everything below for you:**

```bash
cd ~/projects/OblastZero && python3 tools/verify_steam_layer.py
```

39 checks: single-DLL staging, plugin metas, dead-API grep, DLL symbol existence, SO guid wiring, `dotnet build` with zero `error CS`, and type presence in the produced assembly. Exit 0 = green. It self-cleans its scratch files. Verified to actually fail on a deliberately reintroduced `Facepunch.*` call (negative control), so a green run means something.

**It now injects `<Compile>` entries for `.cs` files Unity has not imported yet** (added 26 Jul 2026). Unity only lists a script in `Assembly-CSharp.csproj` once the Editor has imported it, so any file written by an outside process — i.e. most of this project — used to produce phantom `CS0246` failures for types that were fine on disk. Editor-only scripts are skipped since they belong to `Assembly-CSharp-Editor`. If you see `[note] injected N source(s)`, that is the mechanism working, not a problem.

The manual procedure it automates, and why the naive routes fail:

Unity holds an exclusive project lock while the Editor is open, so `Unity.exe -batchmode` will refuse to run and you cannot trust `Editor.log` alone — **it interleaves stale errors from previous compiles**, so old fixed errors look live. Always confirm which errors are current by comparing line numbers against the last `Asset Pipeline Refresh` entry.

To get a real, independent compile check while Unity stays open:

```bash
cd ~/projects/OblastZero
# 1. Copy the Unity-generated csproj (must stay IN the project root — relative HintPaths)
python3 -c "
import re
s=open('Assembly-CSharp.csproj',encoding='utf-8').read()
# drop <Reference> entries for any DLLs no longer under Assets/
for n in ['Facepunch.Steamworks.Posix','Facepunch.Steamworks.Win32']:
    s=re.sub(r'[ \t]*<Reference Include=\"'+re.escape(n)+r'\">.*?</Reference>
?\n','',s,flags=re.S)
s=re.sub(r'<OutputPath>[^<]*</OutputPath>','<OutputPath>Temp/ozverify/</OutputPath>',s)
open('zz_verify.csproj','w',encoding='utf-8').write(s)"
# 2. Build it
dotnet build zz_verify.csproj --nologo -v m 2>&1 | grep -E "error CS|Build succeeded|Build FAILED"
# 3. Confirm your types actually landed in the output assembly, then clean up
rm -f zz_verify.csproj && rm -rf Temp/ozverify
```

Notes:
- The csproj **must** be built from the project root — copying it to `/tmp` breaks every relative `HintPath` and floods you with MSB3245 noise that masks real errors.
- `MSB3245 could not resolve` warnings for Unity package assemblies are expected and harmless; only `error CS` lines matter.
- `<LangVersion>` is **9.0** in this project. Any C# 10+ syntax (file-scoped namespaces, etc.) in vendored source will fail with CS8773 — prefer shipping a prebuilt DLL over vendored source for third-party libs.

## 13. Native plugin pitfalls (Facepunch.Steamworks specifically)

- **Never stage more than one managed Facepunch DLL.** `Facepunch.Steamworks.Win64.dll`, `.Win32.dll`, and `.Posix.dll` all declare the same `Steamworks` namespace, so having several under `Assets/` yields CS0433 "type exists in both" errors. Keep Win64 only; the extras live in `tools/steam_plugins_extra/`. Add other platforms later via per-platform `PluginImporter` settings, one architecture per import.
- **A `.meta` file containing only `fileFormatVersion` + `guid` is broken for a DLL.** Unity needs a full `PluginImporter` block with `platformData` (Editor + Standalone Win64 enabled, `Any` disabled) or the assembly silently isn't referenced and you get CS0246 for its types. See the current metas in `Assets/Plugins/Facepunch.Steamworks/` for the working shape.
- **API version check before writing Steam code.** The shipped DLL is Facepunch **2.x**: namespace `Steamworks` (not `Facepunch.Steamworks`), static classes `SteamClient` / `SteamUserStats` / `SteamRemoteStorage`, achievements via `new Steamworks.Data.Achievement(key)` with `.State` / `.Trigger()`, stats via `SteamUserStats.SetStat(key, value)` / `GetStatInt(key)` / `StoreStats()`. Verify names against the DLL before coding:
  ```bash
  python3 -c "d=open('Assets/Plugins/Facepunch.Steamworks/Facepunch.Steamworks.Win64.dll','rb').read().decode('latin-1');
  print([k for k in ['SteamClient','SetStat','StoreStats','FileWrite'] if k in d])"
  ```

---

## 14. Authoring scenes without the Editor (learned 25 Jul 2026 building `Scavenge.unity`)

`Assets/Scenes/Scavenge.unity` was written as text with Unity's bridge unreachable, and Unity accepted it. The approach generalises — read this before hand-authoring any scene or prefab.

**Generate, never hand-write.** `tools/generate_scavenge_scene.py` (level plan) + `tools/scavenge_scene_lib.py` (YAML emitters, no level knowledge) own that scene. Output is **byte-deterministic** — re-running produces an identical file, verified by md5 — because GUIDs come from `md5('OblastZero::' + name)` and nothing samples time or randomness. Consequences:
- **Never hand-edit `Scavenge.unity`.** The next regeneration silently overwrites you. Change the coordinate plan; the diff stays readable.
- Determinism is what makes regeneration safe to run at any time, including as a pre-commit sanity check.

**Facts you cannot guess and must harvest from the project:**
- Script GUIDs come from the real `.meta` files — `grep guid Assets/.../Foo.cs.meta`. A guessed GUID yields a silently unassigned component, not an error.
- Built-in primitive mesh fileIDs (guid `0000000000000000e000000000000000`): Cube `10202`, Cylinder `10206`, Sphere `10207`, Capsule `10208`, Plane `10209`, Quad `10210`.
- URP Lit shader guid `933532a4fcc9baf4fa0491de14d08ed7`. A partial `m_SavedProperties` is fine — Unity fills shader defaults. Emissive needs `m_ValidKeywords: [_EMISSION]` **and** `m_LightmapFlags: 2`.
- Unity Euler order is **ZXY intrinsic**. Get this wrong and every rotated prop lands somewhere plausible but wrong.
- **Primitive mesh extents are not all unit.** A Cylinder/Capsule mesh is **2 units tall**, so a `1,1,1` BoxCollider on a cylinder is half-height. Keep a per-primitive local-extent table.

**Validate in the generator and refuse to write on failure.** Four gates earned their keep on the first run: pickup ids against the live database, every fileID/GUID reference resolving, an **OBB** burial + support test (world AABB gives false positives on rotated solids), and a walkability flood-fill using the real CharacterController metrics (h 1.8 / r 0.35 / step 0.32). Findings: 6 pickups buried inside solids, and 2 routes sealed because **box colliders on debris meshes** turned ankle-deep grain spills and fallen beams into unclimbable 0.85 m walls. Model climbs as capped and drops as free, so the flood-fill proves pickups are **escapable**, not merely reachable — otherwise a pit is a run-ending trap that reads as fine.

**Every validator needs a negative control.** Deleting `Pit_Ramp` must fail the escape check. A gate never observed failing is decoration.

**Independent post-checks worth running on emitted YAML:** parses under PyYAML as N documents, no duplicate fileIDs, `m_Children`/`m_Father` mutually consistent, `SceneRoots` matches the real roots, every component's `m_GameObject` back-reference valid, quaternions unit-length.

**Per-renderer material overrides:** use `MaterialPropertyBlock` + `SetPropertyBlock`. Writing `sharedMaterial` mutates the shared asset — every fixture animates in lockstep and the `.mat` comes back dirty in the Editor; `.material` instantiates a material per object instead.

