# MASTER CLAUDE CODE PROMPT — Oblast Zero Code Gaps

**Target:** Claude Code CLI (Opus 5 / Sonnet 4), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Scope:** All remaining code gaps from PROJECT_STATE_REPORT §9. Five tasks, each self-contained, each independently committable.
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.

---

## TASK 1: WIRE CREW CARRY CAPACITY INTO SCAVENGE CAP (§9.5 — UI-tells-a-lie bug)

### Problem
`CrewMemberData.baseStats.carryCapacityKg` is authored per crew member (Marina 22 / Yuri 28 / Sasha 34 kg) and displayed in `RunSetupUI.cs:151` as a roster stat, but has ZERO mechanical effect. The scavenge carry cap is a flat 15 kg for everyone, set in `GameManager.cs:172`:

```csharp
_inventory.ScavengeCarryCapacityKg = BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG;
```

This is a UI-tells-a-lie bug: a player choosing Sasha for her 34 kg gets 15 kg like everyone else. But wiring the authored values naively makes every crew member's cap ABOVE the 15 kg design point, re-opening the unreachable-cap failure (§9.2b).

### Fix Required
1. **Rescale the authored `carryCapacityKg` values** in `Assets/Data/Scripts/Definitions/OblastZero.Data/CrewMemberData.cs` and any seed `.asset` files so they straddle 15 kg instead of sitting above it. Suggested: Marina 12, Yuri 15, Sasha 19. This makes crew choice a real trade-off: Sasha carries more but costs more (higher food/water consumption), Marina is lighter but cheaper. Keep the values as authored data, not constants.

2. **Wire the lead crew's `carryCapacityKg` into `InventoryManager.ScavengeCarryCapacityKg`** in `GameManager.BeginNewRun` (around line 172). After the lead crew member is resolved (the `lead` variable at line ~177), set:

```csharp
if (lead != null && lead.Data != null)
{
    float crewCap = Mathf.Max(
        BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG * 0.5f,  // floor at 50% of base
        lead.Data.baseStats.carryCapacityKg                      // use crew member's authored value
    );
    _inventory.ScavengeCarryCapacityKg = crewCap;
    Debug.Log($"[GameManager] Scavenge carry capacity set to {crewCap:0.##} kg from lead crew '{leadCrewDataId}'.");
}
```

This replaces the flat `BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG` assignment. The floor prevents a misauthored crew member from having zero capacity.

3. **Update `RunSetupUI.cs`** — the display at line 151 already reads `stats.carryCapacityKg`, so once the authored values are rescaled, the UI will show the correct numbers. No code change needed here beyond verifying the format string renders the rescaled values cleanly.

4. **Update test fixtures** — `ScavengeLogicTest.cs:115` already sets `inventory.ScavengeCarryCapacityKg = 4f` (a test-only override), so it's fine. Check `BunkerPhaseSmokeTest.cs:115`, `DataLayerSmokeTest.cs:152`, `EventEngineSmokeTest.cs:163` — these set `carryCapacityKg` on crew data for test purposes and should continue to work since they don't go through the scavenge path.

### Files to Touch
- `Assets/_Project/Scripts/Core/GameManager.cs` (line ~172-180)
- `Assets/Data/Scripts/Definitions/OblastZero.Data/CrewMemberData.cs` (comments only — rescale happens in `.asset` files)
- Any `Crew_*.asset` files in `Assets/Data/Definitions/Crew/` (rescale the `carryCapacityKg` field)
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — add a `SCAVENGE_MIN_CARRY_WEIGHT_KG` constant for the floor (suggested: `8`, which is ~50% of 15)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `grep -rn "carryCapacityKg" Assets --include="*.cs"` shows the GameManager binding
- The depot floor (28.72 kg) still exceeds any crew member's cap (max 19 kg), so the weight decision is still live

---

## TASK 2: AUTO-REGISTER STATES WITHOUT REQUIRING THE EDITOR MENU (§9.3)

### Problem
`StateRegistrationTool` (in `Assets/_Project/Scripts/Editor/StateRegistrationTool.cs`) is a one-click `MenuItem` that must be run by hand in the Unity Editor: `OblastZero/Setup/Register Missing States`. If you forget this, `RunFailed` and the four `RunVictory_*` states log "No state registered" and a wipe dead-ends. This is a manual step that blocks the entire run loop.

### Fix Required
Add a **runtime fallback** in `GameStateMachine.Awake()` (or `OnEnable()` if `Awake` is taken) that auto-registers any missing state types if the machine has no child for them. This makes the Editor tool a convenience rather than a requirement.

In `Assets/_Project/Scripts/Core/GameStateMachine.cs`:

```csharp
private void Awake()
{
    if (Instance == null) Instance = this;
    else { Destroy(gameObject); return; }

    EnsureStatesRegistered();
    // ... rest of existing Awake logic
}

private void EnsureStatesRegistered()
{
    // Same list as StateRegistrationTool
    System.Type[] stateTypes = {
        typeof(MainMenuState),
        typeof(RunSetupState),
        typeof(RunFailedState),
        typeof(RunVictoryStabilizationState),
        typeof(RunVictoryReliefState),
        typeof(RunVictoryAdaptationState),
        typeof(RunVictoryIndependentState),
    };

    foreach (var type in stateTypes)
    {
        var existing = GetComponentInChildren(type, includeInactive: true);
        if (existing != null) continue;

        var go = new GameObject(type.Name);
        go.transform.SetParent(transform, false);
        go.AddComponent(type);
        Debug.Log($"[GameStateMachine] Auto-registered missing state {type.Name}.");
    }
}
```

**Important:** `GameStateMachine` needs `using OblastZero.Core.States;` for the state types. Check existing usings. Also ensure this runs BEFORE any state tries to register itself — the Awake order should be: GameStateMachine first (registers children), then children's Awake calls fire.

If `GameStateMachine` is a singleton that might have its Awake order raced by child states, use `[DefaultExecutionOrder(-100)]` on `GameStateMachine` or move the registration to a static initializer.

### Files to Touch
- `Assets/_Project/Scripts/Core/GameStateMachine.cs` (add `EnsureStatesRegistered()` call)
- Possibly `Assets/_Project/Scripts/Editor/StateRegistrationTool.cs` (add a comment noting the runtime fallback)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- The StateRegistrationTool still works as a manual option (idempotent, just redundant now)
- Opening _Bootstrap.unity without running the tool no longer logs "No state registered"

---

## TASK 3: CONTENT QA — IP FIREWALL + VOICE + FORMULA PARSING (§9.4)

### Problem
703 items and 1020 events were mass-generated. Risks:
- **IP-firewall violations** — names from S.T.A.L.K.E.R. (Strelok, Scar, Sidorovich, Pripyat, ChNPP, Duty, Freedom, etc.) are a **legal exposure**
- **Voice drift** — content that uses forbidden pulp clichés ("twisted metal," "eerie silence," "unnatural glow") or breaks the post-administrative register
- **Formula parse failures** — `successChanceFormula` strings that don't parse in `FormulaEvaluator` are runtime breaks

`tools/content_qa.py` exists but may need updating. The forbidden name set and cliché list are in CLAUDE.md §8 and §9.

### Required Work
1. **Run `python tools/content_qa.py`** and capture its output. Fix any issues it reports.
2. **Add an IP-firewall scan** if `content_qa.py` doesn't have one. The forbidden name set (case-insensitive, word-boundary match):
   - `Strelok`, `Scar`, `Sidorovich`, `Pripyat`, `ChNPP`, `Chernobyl`, `Duty`, `Freedom`, `Monolith`, `Clear Sky`, `Bandits`, `Military`, `Ecologists`, `Lens`, `EMR`, `Zombified`
   - Any exact match in `displayName`, `designerNotes`, event `description`, event `choiceText`, event `outcomeText` fields
   - Report: file path, field, line number, matched string
3. **Add a cliché scan** for forbidden pulp: "twisted metal", "eerie silence", "unnatural glow", "screams in the distance", "abandoned", "desolate", "lurking", "ominous"
4. **Add a formula parse check** — load every event JSON, extract `successChanceFormula` strings, and verify they parse in a Python reimplementation of `FormulaEvaluator` (or at minimum: balanced parens, valid operands, no null bytes). Flag any that fail.
5. **Fix any violations found** in the JSON files directly (string replacements preserving JSON structure).

### Files to Touch
- `tools/content_qa.py` (enhance)
- Any `Assets/Data/Resources/Items/*.json` or `Assets/Data/Resources/Events/*.json` files that have violations

### Verify
- `python tools/content_qa.py` exits 0 with no violations
- Item/event counts unchanged (703 items, 1020 events — fixing text doesn't add/remove files)

---

## TASK 4: visualArchetype SYSTEM — MAP ITEM IDS TO VISUAL REPRESENTATIONS

### Problem
The scavenge scene has 25 pickups on real database IDs (22 items + 3 crew) but all are rendered as primitive cubes/planes with flat URP materials. The game has 703 items in JSON with no visual representation system. The project needs a `visualArchetype` concept: a mapping from item category/type to a visual prefab so that items render with appropriate meshes.

Current state: there is NO `visualArchetype` field anywhere in the codebase (confirmed by grep). The scene generator (`tools/generate_scavenge_scene.py`) assigns primitive meshes (Cube, Cylinder, Sphere) to pickups based on their `kind` (Item vs Crew) but has no category-aware visual mapping.

### Required Work
1. **Design the `VisualArchetype` enum or string** in `Assets/_Project/Scripts/Core/` or `Assets/Data/Scripts/Definitions/`:

```csharp
namespace OblastZero.Data
{
    /// <summary>
    /// Visual category for rendering items in the 3D scavenge scene.
    /// Maps to prefabs/meshes loaded by the scene generator.
    /// </summary>
    public enum VisualArchetype
    {
        Crate,        // wooden supply crate (food, water, generic supplies)
        MetalCan,     // small cylindrical can (canned food, medical supplies)
        AmmunitionBox,// metal ammo box
        Document,     // folder/binder (documents, dossiers, intel)
        WeaponSidearm,// pistol-sized weapon
        WeaponLong,   // rifle/long gun
        Tool,         // pry bar, wrench, toolkit
        Artifact,     // glowing anomaly artifact (emissive)
        Clothing,     // coats, masks, gear
        Medical,      // medkit, bandage box
        Crew,         // crew member (standing figure)
        Default       // fallback crate
    }
}
```

2. **Add a `visualArchetype` field to `ItemData`** in the schema. This should be a string that maps to the enum, authored in the item JSON files or derived from `category` / `itemType` if the field is absent.

3. **Write a derivation function** that maps `ItemData.category` (or `itemType`) to a `VisualArchetype`:

```csharp
public static VisualArchetype DeriveArchetype(ItemData data)
{
    if (data == null) return VisualArchetype.Default;
    
    string cat = data.category?.ToLowerInvariant() ?? "";
    string id = data.id?.ToLowerInvariant() ?? "";
    
    if (id.Contains("weapon") || id.Contains("pistol") || id.Contains("rifle")) 
        return id.Contains("pistol") || id.Contains("sidearm") ? VisualArchetype.WeaponSidearm : VisualArchetype.WeaponLong;
    if (cat.Contains("ammo") || id.Contains("ammo")) return VisualArchetype.AmmunitionBox;
    if (cat.Contains("document") || id.Contains("dossier") || id.Contains("intel")) return VisualArchetype.Document;
    if (cat.Contains("artifact") || id.Contains("artifact")) return VisualArchetype.Artifact;
    if (cat.Contains("tool") || id.Contains("pry") || id.Contains("wrench")) return VisualArchetype.Tool;
    if (cat.Contains("medical") || id.Contains("medkit") || id.Contains("bandage")) return VisualArchetype.Medical;
    if (cat.Contains("clothing") || id.Contains("coat") || id.Contains("mask")) return VisualArchetype.Clothing;
    if (cat.Contains("food") || id.Contains("canned") || id.Contains("ration")) return VisualArchetype.MetalCan;
    if (cat.Contains("water") || id.Contains("flask") || id.Contains("drink")) return VisualArchetype.MetalCan;
    
    return VisualArchetype.Crate; // default for generic supplies
}
```

4. **Update `tools/generate_scavenge_scene.py`** to use `VisualArchetype` when assigning meshes to pickups. Instead of all items getting a Cube, map:
   - `Crate` → Cube (existing, fine)
   - `MetalCan` → Cylinder (short)
   - `AmmunitionBox` → Cube (smaller scale)
   - `Document` → Plane (flat, thin)
   - `Artifact` → Sphere with emissive material
   - `WeaponSidearm` / `WeaponLong` → elongated Box
   - `Tool` → elongated Box
   - `Crew` → Capsule

5. **Create a `VisualArchetypeMapping` static class** in C# that the runtime `ScavengeController` can use to spawn the correct prefab/mesh when a pickup is rendered. This is the bridge between the data-driven item system and the visual representation.

### Files to Touch
- `Assets/_Project/Scripts/Core/VisualArchetype.cs` (new file — enum + DeriveArchetype + mapping)
- `Assets/Data/Scripts/Definitions/OblastZero.Data/ItemData.cs` (add `visualArchetype` field if schema allows)
- `tools/generate_scavenge_scene.py` (update pickup mesh assignment)
- Possibly `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs` (runtime archetype → prefab lookup)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_scavenge_scene.py` regenerates the scene with archetype-aware meshes
- The 25 pickups in the scene have varied shapes (not all cubes)
- `grep -rn "VisualArchetype" Assets --include="*.cs"` shows the new system

---

## TASK 5: RUNTIME DATA-LOADER VERIFICATION HOOK (§8.3)

### Problem
`GameDatabase.Initialize` is supposed to ingest all 1020 events and 703 items at runtime, but this has never been verified. `LocalizedStrings` should populate from `Assets/Data/Resources/Locale/localization_en.json` but localization keys currently render raw, which is the symptom of the table not loading.

### Required Work
1. **Add a bootstrap diagnostic** — in `Bootstrap.cs` (or `GameManager.Initialize`), after `GameDatabase.Initialize()` runs, log a summary:

```csharp
Debug.Log($"[Bootstrap] GameDatabase loaded: {_db.ItemCount} items, {_db.EventCount} events, {_db.CrewCount} crew, {_db.FactionCount} factions.");
if (_db.ItemCount < 600) Debug.LogError($"[Bootstrap] CRITICAL: only {_db.ItemCount} items loaded (expected 700+). JSON loader may be broken.");
if (_db.EventCount < 900) Debug.LogError($"[Bootstrap] CRITICAL: only {_db.EventCount} events loaded (expected 1000+). JSON loader may be broken.");
```

2. **Verify `LocalizedStrings` initialization** — check that `LocalizedStrings` is actually loading `Assets/Data/Resources/Locale/localization_en.json`. Add a diagnostic log:

```csharp
Debug.Log($"[LocalizedStrings] Loaded {localizedKeys.Count} keys from {localeFilePath}");
if (localizedKeys.Count == 0) Debug.LogError("[LocalizedStrings] No localization keys loaded. Keys will render raw.");
```

3. **If `LocalizedStrings` has no initialization call**, wire one. Check `Bootstrap.cs` — if it doesn't call `LocalizedStrings.Initialize()` or equivalent, add it after the database loads.

4. **Fix `ItemJsonLoader` / `EventJsonLoader` if counts are wrong** — if the loaders are present but not being called by `GameDatabase.Initialize`, that's the bug. The loaders exist per CLAUDE.md (`ItemJsonLoader` was added in `624e586`, `EventJsonLoader` in commit `f849334`). Verify they're actually invoked in the init chain.

### Files to Touch
- `Assets/_Project/Scripts/Core/Bootstrap.cs` (diagnostics)
- `Assets/_Project/Scripts/Core/GameManager.cs` (if `Initialize` is here)
- `Assets/_Project/Scripts/Core/LocalizedStrings.cs` (diagnostics + possibly initialization fix)
- `Assets/_Project/Scripts/Services/ItemJsonLoader.cs` or wherever it lives (if broken)
- `Assets/_Project/Scripts/Services/EventJsonLoader.cs` (if broken)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- On entering Play mode, the console shows: `[Bootstrap] GameDatabase loaded: 703 items, 1020 events, ...`
- Localization keys no longer render as raw strings (e.g., `menu.start` renders as "Start Run" not "menu.start")

---

## EXECUTION ORDER AND COMMIT STRATEGY

Each task is independently committable. Suggested order:

1. **Task 2** (auto-register states) — smallest, unblocks the entire play loop
   - Commit: `feat: auto-register game states at runtime (fixes §9.3)`
   
2. **Task 5** (data-loader diagnostics) — fast, gives visibility into everything else
   - Commit: `feat: bootstrap diagnostics for database + localization loading (§8.3)`

3. **Task 1** (crew carry capacity) — medium, fixes the UI-tells-a-lie bug
   - Commit: `fix: wire crew carry capacity into scavenge cap + rescale authored values (§9.5)`

4. **Task 3** (content QA) — potentially long, run `content_qa.py` and fix issues
   - Commit: `fix: content QA pass — IP firewall violations + voice + formula parsing (§9.4)`
   - May need multiple commits if violations are spread across many files

5. **Task 4** (visualArchetype) — largest, most architectural
   - Commit: `feat: VisualArchetype system — map item categories to 3D visual representations`

### After Each Task
- Run `python tools/verify_steam_layer.py` — must be 39/39 green
- `git status` — stage explicit paths only, NEVER `git add -A` (this branch has concurrent sessions)
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

### After All Five Tasks
The code side will be in shape for the first real Play-mode test. The remaining work is:
- Higgsfield 3D meshes for props (replacing primitives) — happening in parallel via Hermes
- Higgsfield texture images for materials
- Audio, VFX, Steam store page

---

## FILES THAT EXIST AND YOU SHOULD NOT RE-CREATE

```
Assets/_Project/Scripts/Core/GameStateMachine.cs        ← add EnsureStatesRegistered()
Assets/_Project/Scripts/Core/GameManager.cs            ← line ~172, fix carry capacity binding
Assets/_Project/Scripts/Core/BalanceConstants.cs       ← add SCAVENGE_MIN_CARRY_WEIGHT_KG
Assets/_Project/Scripts/Core/Bootstrap.cs              ← add diagnostics
Assets/_Project/Scripts/Core/LocalizedStrings.cs        ← add diagnostics, fix init if broken
Assets/_Project/Scripts/OblastZero.Gameplay/InventoryManager.cs ← already has ScavengeCarryCapacityKg
Assets/_Project/Scripts/UI/RunSetupUI.cs              ← line 151, no change needed
Assets/_Project/Scripts/Editor/StateRegistrationTool.cs ← existing manual tool, keep
Assets/Data/Scripts/Definitions/OblastZero.Data/CrewMemberData.cs ← schema, has carryCapacityKg
Assets/Data/Scripts/Definitions/OblastZero.Data/ItemData.cs ← add visualArchetype if schema allows
Assets/Data/Resources/Items/*.json                     ← 703 item JSON files
Assets/Data/Resources/Events/*.json                    ← 1020 event JSON files
Assets/Data/Resources/Locale/localization_en.json       ← localization table
tools/content_qa.py                                    ← enhance
tools/generate_scavenge_scene.py                       ← update pickup mesh assignment
tools/verify_steam_layer.py                            ← run after each task, don't modify
```

Go. Ship it.
