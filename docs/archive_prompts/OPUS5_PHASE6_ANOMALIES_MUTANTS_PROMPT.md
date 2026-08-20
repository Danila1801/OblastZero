# MASTER CLAUDE CODE PROMPT — OblastZero Phase 6: Anomaly System + Mutant Encounters + Third Site + Expedition System

**Target:** Claude Code CLI (Opus 5, max effort), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** Phases 1-5 complete. Full game loop, 2 sites, audio, VFX, meta-progression, traits, localization, Steam achievements, options, CI/CD. This is the POST-EA content expansion — the content that justifies staying in Early Access for 6+ months.

---

## CONTEXT: WHAT EXISTS RIGHT NOW

Everything from Phases 1-5 is green. The game ships. Now we add the systems that make the game *deep* instead of merely *complete*:

- **Anomalies** — the bible defines 3 anomalies (Carbon Copy, Interview, Backlog). They have `AnomalyData` schemas. They have bestiary entries. But they do NOT exist in the game — not in scavenge, not in events, not anywhere.
- **Mutants** — the bible defines 2 mutants (Drowned Census-Taker, Editor). Same: schemas exist, bestiary entries exist, nothing implements them.
- **Expeditions** — `RunData.ExpeditionsInFlight` exists, `ActiveExpedition` has a schema, but no system sends crew on expeditions or resolves them.
- **Only 2 sites** — Grain Depot + Census Office. Need more for replayability.
- **Special item interactions** — bibles artifacts (Margin Note, Notarized Heart, Stamped Tongue, Final Draft) have item IDs but no USE system.

---

## TASK 1: ANOMALY SYSTEM IN SCAVENGE PHASE — ENVIRONMENTAL HAZARDS

### Problem
The scavenge phase is a flat 60-second grab. No environmental threats. The bible's 3 anomalies are the signature mechanic that separates Oblast Zero from generic survival games. They need to exist in the 3D scavenge scene.

### Design (from BESTIARY.md)

**1. The Carbon Copy (ANM-Δ-07/CC):**
- Invisible until interacted with
- Occupies a small volume (~2m³)
- Player encounters by picking up an item WITHIN the anomaly's volume
- The item picks up correctly, but a DUPLICATE appears in the same position
- If the player picks up the duplicate, ANOTHER appears
- Time pressure causes grabbing 3-4 copies
- In Phase 2 (bunker), only one copy is original — others have subtle defects

**2. The Interview (ANM-Ψ-12/IV):**
- Room-scale anomaly in interior spaces
- Room interior is LARGER than exterior suggests
- Single desk, chair, stack of forms inside
- Player can walk past safely
- Player can sit down → screen fades, timer pauses, questions appear:
  - Start mundane (name, service number)
  - Follow-up questions are NOT mundane
  - Completing = return with reward (artifact or permanent buff/debuff)
  - Refusing/leaving = safe but forfeits reward

**3. The Backlog (ANM-Χ-21/BL):**
- Volumetric time-distortion
- Subjective time runs 40x-100x slower
- Player movement and interaction speed drop to a crawl
- Timer keeps running at normal speed
- Stepping into Backlog with 30s left = forfeited run
- Visible: distorted air, hanging dust motes

### Implementation

**1. Create anomaly zone components:**

Create `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/CarbonCopyAnomaly.cs`:
```csharp
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// Invisible anomaly that duplicates items picked up within its volume.
    /// Place a trigger collider on the zone. When a ScavengePickup inside the zone
    /// is picked up, a duplicate spawns in the same position with a hidden flag
    /// (IsCopy = true). Copies are marked for Phase 2 defect logic.
    /// 
    /// Detection: Geiger counter audio "double-click" pattern plays when player enters.
    /// Currently no Geiger item check — plays for all players. Could gate on item_geiger_counter.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class CarbonCopyAnomaly : MonoBehaviour
    {
        [Tooltip("Max duplicates before the anomaly stops (prevents infinite spam).")]
        public int maxDuplicates = 4;
        
        private int _duplicatesSpawned;
        private readonly List<string> _originalPickupIds = new();

        void OnTriggerEnter(Collider other)
        {
            // Play geiger click
            AudioManager.Play3D(AudioManager.CUE_UI_HOVER, transform.position);
        }

        // Called by ScavengeController when a pickup inside this zone is collected.
        public void OnPickupCollected(ScavengePickup pickup)
        {
            if (_duplicatesSpawned >= maxDuplicates) return;
            if (_originalPickupIds.Contains(pickup.DataId)) 
            {
                _originalPickupIds.Add(pickup.DataId);
                return;
            }

            _originalPickupIds.Add(pickup.DataId);
            SpawnDuplicate(pickup);
        }

        void SpawnDuplicate(ScavengePickup original)
        {
            var dupe = Instantiate(original.gameObject, original.transform.position, original.transform.rotation);
            var pickup = dupe.GetComponent<ScavengePickup>();
            pickup.isCopy = true; // New field on ScavengePickup
            pickup.Quantity = original.Quantity;
            _duplicatesSpawned++;
        }
    }
}
```

Create `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/InterviewAnomaly.cs`:
```csharp
/// <summary>
/// Room-scale anomaly. When the player enters the interview room and sits at the desk,
/// the screen fades to black, the emission timer pauses, and a scripted question sequence
/// plays. Completing yields a reward (artifact). Leaving yields nothing.
/// 
/// The room is visually larger inside than outside — achieved by scaling the interior
/// colliders and adding a portal-like transition (fade through a doorway).
/// </summary>
[RequireComponent(typeof(Collider))]
public class InterviewAnomaly : MonoBehaviour
{
    public Transform sitPosition; // where the player teleports to when sitting
    public float fadeDuration = 1.5f;
    
    private bool _activated;

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || _activated) return;
        // Show prompt: "Press E to sit" 
        // When E pressed: fade screen, pause timer, start question sequence
    }
    
    // Question sequence fires scripted "interview" events through the EventEngine
    // but in real-time, not the bunker phase. The questions are hardcoded here
    // as they're a fixed scripted experience, not data-driven.
    
    // On completion: spawn artifact (item_notarized_heart or item_stamped_tongue)
    // On refuse: fade back, resume timer, no reward
}
```

Create `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/BacklogAnomaly.cs`:
```csharp
/// <summary>
/// Volumetric time distortion. Player movement speed drops to 1-2% normal.
/// Timer keeps normal speed. Visually: dust motes hang motionless in the air,
/// slight chromatic aberration, audio pitch-shifted down.
/// 
/// The zone is a trigger collider covering a corridor or room section.
/// Entering applies the slow. Exiting returns to normal.
/// This is a TRAP — entering with low timer = forfeit.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BacklogAnomaly : MonoBehaviour
{
    public float timeDilationFactor = 0.02f; // 2% speed
    public ParticleSystem hangingMotes; // pre-placed particles that only play while zone is active
    
    private ScavengePlayerController _player;
    private float _originalMoveSpeed;
    private float _originalSprintSpeed;
    
    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<ScavengePlayerController>();
        if (player == null) return;
        _player = player;
        _originalMoveSpeed = player.MoveSpeed;
        _originalSprintSpeed = player.SprintSpeed;
        player.MoveSpeed *= timeDilationFactor;
        player.SprintSpeed *= timeDilationFactor;
        // Audio pitch shift down
        AudioManager.SetTimeScale(0.5f); // pitch everything down
        if (hangingMotes) hangingMotes.Play();
        // Chromatic aberration via volume override
    }
    
    void OnTriggerExit(Collider other)
    {
        if (_player == null) return;
        _player.MoveSpeed = _originalMoveSpeed;
        _player.SprintSpeed = _originalSprintSpeed;
        AudioManager.SetTimeScale(1f);
        if (hangingMotes) hangingMotes.Stop();
    }
}
```

**2. Add `isCopy` field to ScavengePickup:**
```csharp
[Tooltip("True if this is a Carbon Copy duplicate. Copies have subtle defects in Phase 2.")]
public bool isCopy = false;
```

**3. Wire copy defects into Phase 2:**
In the 3D→2D handoff (`TransitionCutsceneState` or `SurvivalPhase2DState`):
- When committing scavenged items to the bunker, mark copied items with a `defected` flag
- In event resolution, if a crew member uses a defected item, it has wrong outcomes:
  - Defected medical: wrong fluid injection → negative effect instead of healing
  - Defected food: wrong label → 50% chance food poisoning
  - Defected documents: wrong signature → reputation penalty with Scale Society
  - Defected weapon: misaligned sights → 25% chance to fail combat events

**4. Place anomalies in the scavenge scene generator:**
- Carbon Copy: near a cluster of pickups in the warehouse area
- Interview: in the admin/office room (a room with a desk)
- Backlog: covering a shortcut corridor (tempting but dangerous)
- Each site gets ONE anomaly, positioned differently for variety

**5. Geiger counter item interaction:**
- If the player has `item_geiger_counter` in their scavenged inventory:
- Audible "double-click" when entering a Carbon Copy zone
- Audible "click pattern" when near any anomaly (warning)
- The geiger is not required but helps identify invisible anomalies

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/CarbonCopyAnomaly.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/InterviewAnomaly.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/BacklogAnomaly.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/AnomalyAudioCue.cs` (shared geiger click)

### Files to Modify
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePickup.cs` — add `isCopy` field
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs` — check for Carbon Copy zone on pickup, call `OnPickupCollected`
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — expose MoveSpeed/SprintSpeed for Backlog
- `Assets/_Project/Scripts/Core/States/TransitionCutsceneState.cs` — mark defected items
- `Assets/_Project/Scripts/OblastZero.Gameplay/EventEngine.cs` — defected item logic during event resolution
- `tools/generate_scavenge_scene.py` — place anomaly zones
- `tools/generate_census_scene.py` — place anomaly zones at different positions
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — anomaly constants

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- Carbon Copy: picking up an item inside the zone spawns a duplicate. Max 4 duplicates.
- Interview: sitting at the desk pauses the timer and starts the question sequence. Completing gives an artifact.
- Backlog: entering slows the player to 2% speed. Timer keeps normal speed. Exiting returns normal speed.
- Defected items cause wrong outcomes in Phase 2 event resolution
- Geiger counter plays click sounds near anomalies

---

## TASK 2: MUTANT ENCOUNTERS IN SCAVENGE PHASE

### Problem
The scavenge phase has no threats. No enemies. The player just grabs and runs. The bible's 2 mutants are the core encounter system. Without them, the 60-second panic has no teeth — it's just a timer.

### Design (from BESTIARY.md)

**1. The Drowned Census-Taker (MTN-Β-04/DC):**
- Slow-moving humanoid, formerly a Scale Society clerk
- Does NOT attack directly
- Follows the player, takes notes on a clipboard
- If player stops moving >10 seconds within line of sight:
  - Census-Taker catches up, begins WRITING the player's name (~15 seconds)
  - On completion: permanent stat penalty for the remainder of the run ("registered")
  - Multiple registrations stack
- Counter-tactic: keep moving. Don't stop.
- Appears in Census District (Census Office site) and Reservoir

**2. The Editor (MTN-Ψ-09/ED):**
- Appears and disappears — not a persistent entity
- When Editor enters line of sight:
  - Player's HUD glitches
  - Inventory items progressively redacted, then deleted, then replaced
  - Cannot be killed by conventional means
  - Can be distracted by throwing documents (it stops to read)
- Effect proportional to how long Editor was on screen
- Counter-tactic: look away. Cover the screen with a wall.
- Appears rarely, mid-to-late-campaign areas

### Implementation

**1. Drowned Census-Taker:**

Create `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/DrownedCensusTaker.cs`:

```csharp
using UnityEngine;
using UnityEngine.AI;
using OblastZero.Core;

namespace OblastZero.Gameplay.Mutants
{
    /// <summary>
    /// Slow-stalking mutant that follows the player and registers them if they stop
    /// moving for 10 seconds in line of sight. Registration applies a permanent stat
    /// penalty (the "Compromised" affliction) for the rest of the run.
    /// 
    /// Uses NavMeshAgent for slow pursuit. Movement speed = walking pace.
    /// Does not attack. Only threat is the registration mechanic.
    /// 
    /// Spawns from a spawn point, despawns when player reaches bunker exit.
    /// Only spawns in Census District and Reservoir sites.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DrownedCensusTaker : MonoBehaviour
    {
        public float moveSpeed = 1.2f; // walking pace
        public float aggroRange = 12f;
        public float registrationTime = 15f; // seconds to complete registration
        public float stopThreshold = 0.5f; // velocity below which player is "stopped"
        public float stopTimerThreshold = 10f; // how long player must be stopped

        private NavMeshAgent _agent;
        private Transform _player;
        private float _playerVelocity;
        private float _stopTimer;
        private float _registrationTimer;
        private bool _hasLineOfSight;
        private bool _isRegistering;
        private bool _spawned;

        // ... full implementation with NavMeshAgent pursuit, raycast line-of-sight,
        // stop-timer accumulation, registration progress, HUD glitch effect,
        // and the permanent stat penalty application
    }
}
```

**2. The Editor:**

Create `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/TheEditor.cs`:

```csharp
/// <summary>
/// Psychic hazard mutant that appears in the player's line of sight and
/// progressively redacts, deletes, and replaces inventory items.
/// 
/// Not a physical entity — it's a visual apparition + game effect.
/// When the Editor is on screen (raycast from camera hits the Editor):
/// - After 3 seconds: random inventory item label is redacted ([REDACTED])
/// - After 6 seconds: random inventory item is deleted
/// - After 10 seconds: random inventory item is replaced with a different random item
/// - Effect continues until the player looks away (raycast no longer hits)
/// 
/// Can be distracted: if the player drops a document-class item, the Editor
/// pauses for 5 seconds to "read" it.
/// 
/// Spawns rarely (15% chance per scavenge run, weighted by site threat level).
/// </summary>
public class TheEditor : MonoBehaviour
{
    // The Editor appears at a random position within the player's view, 
    // far enough to be visible but not in grab range.
    // Lifecycle: appear → exist until looked-away-from for 3s → disappear
    // Material: humanoid silhouette, paper sheet over face, glitchy outline
}
```

**3. Mutant spawn system:**

Create `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/MutantSpawner.cs`:
- Attached to a system GameObject in the scavenge scene
- Reads the scavenge site's `mutantThreat` level from `ScavengeSiteCatalog`
- Census Office: spawns 1 Drowned Census-Taker, 15% chance for Editor
- Grain Depot: 10% chance for Editor (no Census-Taker — wrong region)
- Third site (Reservoir, from Task 3): spawns 2 Drowned Census-Takers

**4. Registration penalty:**
- Applied to `RunData.ActiveCrew` members: -10 health, -5 sanity (permanent)
- Stackable — multiple registrations compound
- The HUD shows "REGISTERED ×N" when the player is registered

**5. HUD integration:**
- When a Census-Taker is pursuing: a subtle "FOLLOWED" indicator on the HUD
- When registration is in progress: a progress bar + "BEING REGISTERED" warning
- When the Editor is on screen: HUD glitch effect (text scrambles briefly)
- These should be UNSETTLING, not alarming — the Oblast doesn't raise its voice

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/DrownedCensusTaker.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/TheEditor.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/MutantSpawner.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/RegistrationAffliction.cs`

### Files to Modify
- `Assets/_Project/Scripts/UI/ScavengeHUD.cs` — followed/registration indicators, Editor glitch
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — velocity tracking for stop detection
- `Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs` — add threat level per site
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePickup.cs` — add method for dropping items (for Editor distraction)
- `tools/generate_scavenge_scene.py` — add MutantSpawner to scene
- `tools/generate_census_scene.py` — add MutantSpawner + NavMesh baking

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- Census-Taker: spawns in Census Office, follows player via NavMesh, registration timer starts when player stops for 10s
- Editor: appears rarely, HUD glitches when on screen, inventory items affected over time
- Registration penalty persists into Phase 2 (crew stats reduced)
- Dropping a document pauses the Editor for 5 seconds
- NavMesh is baked in the scenes (the Census Office generator must emit NavMesh data or the agent won't move)

---

## TASK 3: THIRD SCAVENGE SITE — "ABANDONED RESERVOIR" (RESERVOIR REGION)

### Problem
2 sites are enough to ship, but the Reservoir region from the bible is the most atmospheric — water crossings, the Drowned Census-Taker's hunting ground, flooded structures. This is the high-threat, high-reward site.

### Implementation

Create `tools/generate_reservoir_scene.py`:
- Abandoned municipal water reservoir — a massive concrete basin with water
- Layout: pump house (interior), catwalk over the water basin, flooded tunnels, a dry control room
- The bunker entrance is in the control room (only accessible through the flooded tunnels)
- Water hazard: deep water sections slow movement (0.5x speed), shallow water splashes
- 2 Drowned Census-Takers spawn here (highest threat level)
- The Backlog anomaly covers a flooded tunnel shortcut (tempting but deadly)
- Pickups emphasize: medical supplies (wet, contaminated), documents (waterlogged), weapons (submerged), crew (drowned clerk who survived)

### Files to Create
- `tools/generate_reservoir_scene.py`
- `Assets/Scenes/Reservoir.unity` — generated

### Files to Modify
- `Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs` — add third site
- `Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs` — load correct scene based on site ID
- `Assets/_Project/Scripts/UI/RunSetupUI.cs` — show 3 sites
- `Assets/_Project/Scripts/Core/MetaUnlocks.cs` — add unlock for the third site
- `ProjectSettings/EditorBuildSettings.asset` — add Reservoir at index 4

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_reservoir_scene.py` generates the scene
- 3 sites appear in RunSetup (2 greyed until unlocked)
- Reservoir has 2 Census-Takers, water slowdown, Backlog anomaly in the shortcut
- Walkability/burial/support validations pass

---

## TASK 4: EXPEDITION SYSTEM — SEND CREW ON RANGED MISSIONS FROM THE BUNKER

### Problem
`RunData.ExpeditionsInFlight` exists. `ActiveExpedition` has a schema. But nothing sends crew on expeditions. The bunker phase is just: click End Day, resolve event, repeat. No active decision about risk vs reward. Expeditions add the "what do I risk my crew for?" decision layer.

### Design

While in the bunker phase, the player can assign idle crew members to expeditions:
- Choose a crew member (they must NOT be on another expedition)
- Choose a region tag (which area to explore: census_district, reservoir, grain_belt, etc.)
- Choose a loadout (items from bunker inventory to send with them: weapons, medical, tools)
- The expedition takes N days (3-5, scaled by distance)
- On return: the crew member brings back items + may trigger events
- Risks: crew can come back late (Backlog), injured, insane, not at all, or changed (Editor)
- The expedition resolution uses the EventEngine to select events tagged with the chosen region

### Implementation

Create `Assets/_Project/Scripts/OblastZero.Gameplay/ExpeditionSystem/ExpeditionManager.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay.ExpeditionSystem
{
    /// <summary>
    /// Manages ranged expeditions from the bunker:
    /// - Assign crew + loadout + target region
    /// - Countdown days for return
    /// - On return: resolve events, bring back items, apply effects
    /// 
    /// Does NOT run while a crew member is on another expedition.
    /// Killed crew on expedition = added to CrewDiedEvent.
    /// Late returns (Backlog) = crew returns days later than scheduled.
    /// </summary>
    public class ExpeditionManager
    {
        // ... full implementation
    }
}
```

**UI:**

Create `Assets/_Project/Scripts/UI/ExpeditionUI.cs`:
- Opens from the BunkerHUD as a new screen (add "DISPATCH" button)
- Shows available crew (not on expedition, alive)
- Shows selectable target regions (grid of buttons with region names + threat levels)
- Shows loadout slots (drag items from bunker inventory, or click to assign)
- Shows estimated return time
- "CONFIRM DISPATCH" button establishes the expedition
- Active expeditions shown on BunkerHUD with countdown + crew status

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/ExpeditionSystem/ExpeditionManager.cs`
- `Assets/_Project/Scripts/UI/ExpeditionUI.cs`

### Files to Modify
- `Assets/_Project/Scripts/OblastZero.Gameplay/BunkerDayController.cs` — tick active expeditions per day
- `Assets/_Project/Scripts/OblastZero.Gameplay/BunkerPhaseController.cs` — wire expedition resolution
- `Assets/_Project/Scripts/UI/BunkerHUD.cs` — add "DISPATCH" button + active expedition display
- `Assets/_Project/Scripts/Core/GameManager.cs` — add ExpeditionManager
- `Assets/_Project/Scripts/Core/RunData.cs` — ensure ExpeditionsInFlight is properly serialized
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — expedition constants (min/max days, reward scaling)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- Can assign a crew member to an expedition from BunkerHUD
- Expedition resolves after N days
- Returning crew brings items
- Risk events fire (Backlog delay, Editor item swap, Census-Taker registration)
- Expedition in-flight survives save/load

---

## TASK 5: SPECIAL ITEM INTERACTION — ARTIFACT USES IN BUNKER

### Problem
The bible defines 4 artifacts with specific use effects, but the bunker phase has no "use item" system. You can see items in your bunker inventory but can't interact with them beyond consuming rations.

### Artifact Uses (from BESTIARY.md)

| Artifact | Effect |
|----------|--------|
| **Margin Note** (`item_margin_note`) | Re-roll one expedition event outcome per in-game week |
| **Notarized Heart** (`item_notarized_heart`) | -50% personal radiation accumulation for one crew member |
| **Stamped Tongue** (`item_stamped_tongue`) | One-time "official override" — auto-succeed any one Scale Society event |
| **Final Draft** (`item_final_draft`) | Permanently rewrite one stat of one crew member (consumed) |

### Implementation

Create `Assets/_Project/Scripts/UI/ArtifactUseUI.cs`:
- A new screen opened from the BunkerHUD "ARTIFACTS" tab
- Lists all artifacts in the bunker inventory
- Selecting an artifact shows its use effect and a "USE" prompt
- Margin Note: select a recent event outcome to re-roll
- Notarized Heart: select a crew member to apply radiation reduction
- Stamped Tongue: appears during Scale Society events as a "USE OVERRIDE" button
- Final Draft: select a crew member + select a stat + enter new value

### Files to Create
- `Assets/_Project/Scripts/UI/ArtifactUseUI.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/ArtifactSystem.cs`

### Files to Modify
- `Assets/_Project/Scripts/UI/BunkerHUD.cs` — add "ARTIFACTS" button
- `Assets/_Project/Scripts/OblastZero.Gameplay/EventEngine.cs` — handle Margin Note re-roll + Stamped Tongue override
- `Assets/_Project/Scripts/OblastZero.Gameplay/CrewManager.cs` — handle Notarized Heart + Final Draft
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — artifact use constants (cooldowns, max uses)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- Each artifact can be used from the BunkerHUD
- Margin Note re-rolls a past event outcome
- Notarized Heart reduces radiation for the selected crew member
- Stamped Tongue auto-succeeds a Scale Society event
- Final Draft rewrites a crew stat and is consumed
- Artifacts in the bunker inventory are clickable → use screen

---

## EXECUTION ORDER AND COMMIT STRATEGY

1. **Task 1** (anomaly system) — the signature mechanic
   - Commit: `feat: 3 scavenge-phase anomalies — Carbon Copy, Interview, Backlog`

2. **Task 2** (mutant encounters) — makes the panic phase dangerous
   - Commit: `feat: mutant encounters — Drowned Census-Taker + The Editor + spawn system`

3. **Task 3** (third site: Reservoir) — content variety
   - Commit: `feat: third scavenge site — Abandoned Reservoir (reservoir region, 2 mutants)`

4. **Task 5** (artifact uses) — special item depth
   - Commit: `feat: artifact use system — Margin Note, Notarized Heart, Stamped Tongue, Final Draft`

5. **Task 4** (expedition system) — the largest system, adds the deepest layer
   - Commit: `feat: expedition system — ranged crew missions from the bunker`

### After Each Task
- Run `python tools/verify_steam_layer.py` — must be 39/39 green
- Run `python tools/content_qa.py` — must be zero violations
- Run `python tools/csharp_string_qa.py` — must be zero violations
- `git status` — stage explicit paths only, NEVER `git add -A`
- `git commit` with the message above
- Push: `git push origin feat/scavenge-3d-scene`

### Hard Rules
- **LangVersion 9.0** — no C# 10+ syntax
- **No `// TODO`, no stubs** — complete implementations only
- **Balance numbers in `BalanceConstants`**
- **Namespace matches folder**
- **IP firewall** — no S.T.A.L.K.E.R. names. Use `content_qa.py` and `csharp_string_qa.py` after changes.
- **Deterministic scene generation** — new generators must be byte-deterministic and self-validating
- **NavMesh** — Census Office and Reservoir scenes must have NavMesh baked (or the Census-Taker won't move). If the generator can't emit NavMesh data, document the manual bake step.

### After All Five Tasks
The game has:
- 3 scavenge sites with distinct atmospheres, anomalies, and mutants ✅
- Environmental hazards (Carbon Copy, Interview, Backlog) that create meaningful decisions ✅
- Mutant encounters that make the scavenge phase DANGEROUS (not just timed) ✅
- Expedition system that adds active risk/reward decisions to the bunker phase ✅
- Artifact use system that gives special items MEANING ✅

**This is the content that justifies 6+ months in Early Access and builds a player community.**
