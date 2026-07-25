# OPUS 5 PROMPT — OblastZero 3D Scavenge Scene Build
# Copy-paste this entire file into Claude Code CLI with Opus 5 set to max effort.
# Launch Claude Code from C:\Users\danil\projects\OblastZero

---

You are a senior Unity 6 game developer with 40 years of experience, working on **Oblast Zero** — a commercial roguelite survival game shipping on Steam Early Access in September 2026. This is a real product, not a prototype. Write production-quality, complete, functional code.

## YOUR TASK

Build the complete 3D Scavenge Scene (Phase A — "The Blowout") for Oblast Zero. This is the first-person 60-second real-time panic scavenge phase where the player grabs supplies, rescues crew members, and reaches the bunker before the Emission hits.

All the SYSTEMS code already exists and compiles green. You are building the actual playable SCENE + level geometry + pickup placement + scene wiring.

## WHAT ALREADY EXISTS (DO NOT REWRITE)

The following scripts are already built, tested, and compile. Read them in the repo to understand the contracts, then use them. Do not modify them unless you find a bug that blocks scene creation:

**Core state:**
- `Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs` — owns the EmissionTimer, listens for ReachBunkerEvent, transitions to TransitionCutscene
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — all balance values, including SCAVENGE_TIMER_SECONDS, SCAVENGE_MAX_CARRY_WEIGHT_KG, etc.
- `Assets/_Project/Scripts/Core/GameEvents.cs` — all EventBus event structs (ScavengeTimerTickEvent, ReachBunkerEvent, ItemPickedUpEvent, CrewRescuedEvent, ScavengeTargetChangedEvent)
- `Assets/_Project/Scripts/Core/RunData.cs` — the run state; ScavengedInventory + RescuedCrew are filled here
- `Assets/_Project/Scripts/Core/GameManager.cs` — singleton, owns Inventory + Crew managers
- `Assets/_Project/Scripts/Core/Bootstrap.cs` — boots Steam then GameManager

**Gameplay (scavenge systems):**
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — CharacterController-based FPS movement, mouse look, E-key interaction, look-raycast for pickups. REQUIRES: CharacterController component, a child camera. Input System (Keyboard.current, Mouse.current polling).
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePickup.cs` — attach to world objects. Fields: Kind (Item/Crew), DataId (must exist in GameDatabase), Quantity, DurabilityOverride, Contamination.
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs` — routes pickups to InventoryManager.AddItem(ScavengedChannel) and CrewManager.AddRescued. Destroys world object on pickup.
- `Assets/_Project/Scripts/OblastZero.Gameplay/EmissionTimer.cs` — pure C# countdown, raises events per second + at expiry.
- `Assets/_Project/Scripts/OblastZero.Gameplay/BunkerEntranceTrigger.cs` — trigger collider at bunker door, raises ReachBunkerEvent when player enters.
- `Assets/_Project/Scripts/OblastZero.Gameplay/DebugRunLauncher.cs` — TEMPORARY dev tool. Press F5 to jump into scavenge. REPLACE with real flow from MainMenu → RunSetup → ScavengePhase3D.

**UI:**
- `Assets/_Project/Scripts/UI/ScavengeHUD.cs` — self-building canvas (no manual UI wiring needed). Shows: countdown timer (color shifts white→orange→red), interaction prompt, grabbed items list, emission flash. Just drop the component on a GameObject in the scene.

**Services:**
- `Assets/_Project/Scripts/Services/SceneLoader.cs` — additive scene load/unload via ISceneLoader

**Data:**
- 691 items in Assets/Data/Resources/Items/ (JSON, one per file) — schemas include id, displayName, category, weightKg, durability, utilityTags[]
- 1020 events in Assets/Data/Resources/Events/ (JSON) — for Phase B, NOT your concern
- ItemData SOs use id field like "item_12_gauge_carbine", "item_canned_meat", "item_geiger_counter", etc.
- CrewMemberData SOs use id field — check Assets/Data/ for crew member definitions

**Scenes:**
- `Assets/Scenes/_Bootstrap.unity` — GameManager + GameStateMachine + all state components. Build Settings index 0. This is never unloaded.
- `Assets/Scenes/Bunker.unity` — Phase 2 UI scene. Build Settings index 1. Loaded additively by SurvivalPhase2DState.
- NO scavenge scene exists yet. That's what you're building.

## BUILD SETTINGS
New scene should be `Assets/Scenes/Scavenge.unity`, Build Settings index 2 (after Bunker). It will be loaded additively by ScavengePhase3DState during the run.

## SCENE REQUIREMENTS

### 1. Level Layout — "Collapsed Grain Depot" (Outer Cordon region)

This is the first/primary scavenge site. The design bible (DESIGN_BIBLE_Сlaude.Opus4.7.md §2.2) describes the Grain Belt as "collapsing agricultural processing plants — flour mills, oil presses, fertilizer warehouses — strung along a single defunct rail line."

Build a playable interior+exterior space that:
- Is traversable in 60 seconds with meaningful route choices (NOT a single corridor)
- Has multiple rooms/areas with different loot concentrations
- Has a clear path to the bunker entrance (marked, visible, with a BunkerEntranceTrigger)
- Uses primitive shapes (cubes, planes) with appropriate scales — no external asset imports needed
- Has architectural logic: grain silo area, warehouse floor, office/admin room, loading dock, bunker stairwell
- Has cover objects (crates, shelves, barriers) that serve as visual occlusion
- Has realistic prop placement for a 1980s Soviet grain processing facility: rusted shelving, broken machinery, stacked crates, ammunition boxes, canned food piles, gas masks on hooks, radios on desks
- Is enclosed by walls/fences so the player can't wander off
- Has consistent lighting (bake-friendly: overhead fluorescent fixtures in interior, overcast skylight in exterior areas)

### 2. Bunker Entrance
- At one end of the level, place a bunker entrance (stairwell going down or heavy door)
- On the entrance, place a GameObject with BunkerEntranceTrigger + a BoxCollider (isTrigger=true) sized to the doorway
- Tag the player object as "Player" (ScavengePlayerController checks this)
- Place a clear visual marker (red light, hazard stripes on floor, signage)

### 3. Player Spawn
- Place the player spawn point at the opposite end from the bunker entrance
- The player needs: ScavengePlayerController, CharacterController, a child Camera with AudioListener
- Tag the player root "Player"
- Make sure the camera is tagged "MainCamera" (Camera.main must find it)

### 4. ScavengeController
- Place a ScavengeController on a GameObject in the scene
- It auto-finds the player via FindObjectOfType

### 5. ScavengeHUD
- Place a ScavengeHUD component on a GameObject in the scene
- It builds its own canvas in code, no manual UI wiring needed
- Needs TextMeshPro resources imported (Window → TextMeshPro → Import TMP Essential Resources — assume this is done)

### 6. Pickup Placement — Use REAL item IDs from the database

Place 15-25 pickups around the level. Each pickup is a GameObject with:
- A primitive mesh renderer (cube/capsule) as visual proxy
- A Collider (for the raycast to hit — the ScavengePlayerController raycasts with QueryTriggerInteraction.Collide)
- A ScavengePickup component with the correct DataId

RESEARCH the actual item IDs first. Run:
```
ls Assets/Data/Resources/Items/*.json | head -30
```
Open a few files to see the id field. Use REAL ids from the database. 

Mix of pickups:
- Food/water items (canned meat, water bottles, rations)
- Medical supplies (med kit, radiation pills, bandages)  
- Weapons (carbine, pistol, ammo)
- Utility items (geiger counter, gas mask, radio, axe)
- 2-3 crew members (CrewMemberData ids — check Assets/Data/ for crew member definitions)
- 1-2 artifacts (rare, high-value)

For crew pickups, use ScavengePickup with Kind=Crew and the crew member's DataId.

### 7. Lighting
- Place a directional light for the exterior (overcast, gray, low intensity — Soviet winter feel)
- Place point lights for interior (flickering fluorescent — attach a flicker script if you write one)
- No real-time shadows on everything (performance). Use baked or no shadows on minor props.

### 8. Atmospheric/Environmental
- Fog (linear, gray, moderate density — the "тяжесть" / heaviness from the bible)
- Post-processing: URP Volume with vignette, color grading (desaturated, slightly green-gray tint), film grain
- Ambient audio source (wind, distant rumble — you can leave a placeholder AudioSource with a comment for the audio clip)

### 9. Scene Script Integration
ScavengePhase3DState currently DOES NOT load the scene additively. You need to:
- Update ScavengePhase3DState.HandleEnter() to load "Scavenge" scene additively via the ISceneLoader
- On HandleExit(), unload the Scavenge scene
- OR if SceneLoader's API doesn't support additive load/unload cleanly, add the additive load logic directly in the state (SceneManager.LoadSceneAsync with Additive mode)

Check `Assets/_Project/Scripts/Services/SceneLoader.cs` for the API. Adapt to it.

### 10. DebugRunLauncher Replacement
- Comment out or remove the DebugRunLauncher flow (but don't delete the file — just make it optional)
- The real flow is: MainMenu → RunSetup (select site, select crew) → BeginNewRun → TransitionTo(ScavengePhase3D) → loads Scavenge scene → EmissionTimer starts → player has 60 seconds → reach bunker or timer expires → TransitionCutscene

Make sure the full flow works from the main menu. If MainMenuState/RunSetupState have issues loading the scavenge phase, fix them.

### 11. Post-Compile Verification
After making all changes, run the compile verification script:
```bash
cd ~/projects/OblastZero && python3 tools/verify_steam_layer.py
```
It must show 39/39 GREEN. If it fails, fix the compile errors before finishing.

### 12. Commit
Stage and commit your changes with a clear message:
```
feat: 3D Scavenge scene (Phase A — Collapsed Grain Depot) — playable 60-second blowout level
```

## ARCHITECTURE RULES (FROM CLAUDE.md)

- No placeholders. Ever. Write complete functional implementations.
- No TODO comments, no stubbed method bodies.
- Reference balance values from BalanceConstants — never inline magic numbers.
- Strict separation: logic decoupled from UI. UI reads state + raises intents.
- Namespace rule: OblastZero.<Layer>. Match namespace to folder. File name == primary type name.
- EventBus-based communication. Prefer events over hard cross-system references.
- Production quality, not scaffolding.
- LangVersion is 9.0. No file-scoped namespaces, no C# 10+ syntax in your new code.

## IP FIREWALL (ABSOLUTE)
- No S.T.A.L.K.E.R. names, locations, factions, or mutants. Zero.
- Everything ships as original Oblast Zero lore.
- Voice: post-administrative, not post-apocalyptic. "The Oblast does not raise its voice. The Oblast files a form."
- Forbidden phrases: "twisted metal", "eerie silence", "unnatural glow", "screams in the distance"
- Use Soviet/post-Soviet bureaucratic register: registered, line item, deviation, protocol, pending review, quota, requisition

## WHEN YOU'RE DONE, VERIFY:
1. `python3 tools/verify_steam_layer.py` passes (39/39 GREEN)
2. The Scavenge scene file exists at Assets/Scenes/Scavenge.unity
3. ScavengePhase3DState loads/unloads the scene additively
4. All pickups reference real item IDs from the database
5. The BunkerEntranceTrigger fires when the player reaches the door
6. The ScavengeHUD shows the countdown, prompt, and grabbed list
7. The full flow works: _Bootstrap → MainMenu → RunSetup → Scavenge → Bunker → End Day → Run → Wipe → RunFailed

Then commit and report what you built.
