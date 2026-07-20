# CLAUDE.md — Oblast Zero

Always-on rules for every Claude Code session in this repo. Read this first. It is the law; the design bible is the reference.

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

- **Unity MCP bridge will not start while there are compile errors.** Green console first, then agent work.
- MCP runs HTTP local on **port 8090** (8080 conflicts with common services). Unity must be open for live scene/console access.
- Roadmap (mental model): 0–1 Core framework → 2 Data layer → 3 3D scavenge → 4 2D bunker engine → 5 Event system → 6 Meta-progression → 7 Polish → 8 Steamworks (Facepunch.Steamworks) → 9 Content blitz → 10 Beta/ship.
