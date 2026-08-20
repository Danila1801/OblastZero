# OPUS 5 PROMPT — UI Screens + Carry Weight Fix
# Copy-paste into Claude Code CLI with Opus 5, max effort.
# Launch from C:\Users\danil\projects\OblastZero

---

You are a senior Unity 6 game developer with 40 years of experience. You are working on **Oblast Zero**, a commercial roguelite survival game shipping on Steam Early Access in September 2026. Read CLAUDE.md first — it is the law. Read DESIGN_BIBLE_Сlaude.Opus4.7.md for world/voice reference.

## CONTEXT

The game has a full state machine, event engine, content (1020 events, 691 items), Steamworks, and a 3D scavenge scene. Compile is GREEN (39/39 checks via `python tools/verify_steam_layer.py`). The game boots and the BunkerHUD renders in Play mode.

## YOUR TASKS — TWO PARTS

### PART 1: Three Missing UI Screens

The game has 3 states that drive NO visible UI. Build self-building EventBus-driven canvases (same pattern as BunkerHUD, EventModalUI, ScavengeHUD — they build their own canvas on Awake, no manual UI wiring needed).

#### 1A: MainMenuUI
- State: `MainMenuState` (already exists in `Assets/_Project/Scripts/Core/States/MainMenuState.cs`)
- Read the state to understand what intents it expects (button → event → state transition)
- Screen shows:
  - Title: "OBLAST ZERO" (large, centered, Oblast voice — bureaucratic, cold)
  - Subtitle: something in the design bible's voice (e.g. "Registered for demographic adjustment." or "Standing order: survive.")
  - Button: "New Run" → transitions to RunSetup
  - Button: "Continue" (if save exists — check via SaveSystem) → loads save, transitions to SurvivalPhase2D
  - Button: "Quit" → Application.Quit (editor: `UnityEditor.EditorApplication.isPlaying = false`)
- Style: dark background, minimal, Soviet bureaucratic aesthetic. White/gray text on near-black. Mono-spaced or institutional font feel.
- Build canvas in code (like BunkerHUD does), use TextMeshPro.

#### 1B: RunSetupUI
- State: `RunSetupState` (already exists)
- Screen shows:
  - "SCAVENGE SITE" — select from available sites (check RunData/GameDatabase for site definitions; if none exist yet, create 3 placeholder site options: "Collapsed Grain Depot", "Flooded Census Office", "Abandoned Rail Terminal")
  - "CREW ROSTER" — show available crew members (from Assets/Data/Definitions/Crew/ — Marina, Sasha, Yuri). Player selects which crew to bring.
  - "CONFIRM" button → calls GameManager.BeginNewRun(siteId, seed) and transitions to ScavengePhase3D
  - Display each crew member's name, background, and base stats (health/sanity/carry capacity)
- Use the existing crew data. Read `Assets/Data/Definitions/Crew/Crew_*.asset` files and `CrewMemberData.cs` schema.

#### 1C: RunSummaryUI (Run Failed screen)
- State: `RunFailedState` (already exists)
- Screen shows:
  - "RUN TERMINATED" or better in Oblast voice: "REGISTRATION CLOSED" / "CASE FILED"
  - Run stats: days survived, crew lost, items recovered, faction reputations
  - "Return to Main Menu" button → transitions to MainMenu
- Read RunFailedState.cs to see what data it exposes. The run summary should pull from RunData (currentDay, ActiveCrew count, BunkerInventory count, rep values).

### PART 2: Carry Weight Enforcement

**Bug found by Opus 5:** `SCAVENGE_MAX_CARRY_WEIGHT_KG = 25` in BalanceConstants is NOT enforced anywhere. `InventoryManager.AddItem` has no weight check, and `GetTotalWeight()` exists but is never called during scavenge. The player can carry the entire depot.

**Additional finding:** All 691 items are too light. Max weight is 3.15kg, mean is 0.64kg. A 12-gauge carbine weighs 0.73kg (should be ~3kg). Even enforcing the 25kg cap would change nothing because the entire level's loot is only 16.59kg.

**Fix BOTH:**

1. **Enforce the weight cap in InventoryManager.AddItem** — when adding to the Scavenged channel, check total weight. If over cap, return null (pickup fails) and log why. The ScavengeHUD should show current weight / max weight.

2. **Rebalance item weights** — write a Python script (`tools/rebalance_weights.py`) that:
   - Reads all 691 item JSONs
   - Recalculates weightKg based on category and item type:
     - Weapons: 1.5–4.5kg (carbines ~3kg, pistols ~1kg, ammo boxes ~2kg)
     - Medical: 0.3–1.5kg (med kits ~1.2kg, pills ~0.1kg, bandages ~0.2kg)
     - Food: 0.2–1.0kg (canned meat ~0.5kg, water bottles ~1kg, rations ~0.4kg)
     - Tools: 1.0–5.0kg (pry bars ~2kg, axes ~3kg, geiger counters ~0.8kg)
     - Documents: 0.05–0.2kg
     - Artifacts: 0.1–0.5kg (bureaucratic artifacts — paper-based, light)
     - Water: 0.5–1.5kg
     - Crafting: 0.2–2.0kg
     - Special: 0.5–2.0kg
   - Ensure ~5-8 items in the scavenge level exceed 2kg so the cap actually matters
   - Total scavenge level loot should be ~30-40kg (well over the 25kg cap, forcing choices)
   - Writes updated JSON files with corrected weightKg values

3. **Lower SCAVENGE_MAX_CARRY_WEIGHT_KG** — consider changing to 15kg. At 15kg with the rebalanced weights, the player faces real choices: a carbine (3kg) + ammo (2kg) + med kit (1.2kg) + 3 cans of food (1.5kg) + water (1kg) + pry bar (2kg) = 10.7kg. They have ~4kg left for crew rescue or artifacts. That's a real decision.

## ARCHITECTURE RULES
- No placeholders. Complete functional implementations.
- No TODO comments, no stubbed methods.
- UI reads state + raises intents. Never owns game logic.
- EventBus-based communication.
- BalanceConstants for all numbers.
- LangVersion 9.0. No file-scoped namespaces.
- Namespace: OblastZero.UI for UI screens.
- All UI builds its own canvas in code (like BunkerHUD/ScavengeHUD pattern).

## VERIFY
1. `python tools/verify_steam_layer.py` → 39/39 GREEN
2. All 3 new UI scripts compile
3. Weight rebalance script runs and produces sane values
4. `python tools/rebalance_weights.py` could be idempotent (safe to re-run)
5. InventoryManager.AddItem now checks weight on Scavenged channel

## COMMIT
```
feat: 3 UI screens (MainMenu, RunSetup, RunSummary) + carry weight enforcement + item weight rebalance
```
