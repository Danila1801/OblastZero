# MASTER CLAUDE CODE PROMPT — OblastZero Phase 3: Audio + VFX + Atmosphere + Polish

**Target:** Claude Code CLI (Opus 5, max effort), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** Phase 1 + Phase 2 are DONE. 86 C# scripts, 39/39 verify_steam_layer, 100/100 in-editor tests. VictoryConditionEvaluator works. RunDataMigrator handles save migration. 4/11 archetypes have GLB meshes. 7 still need primitives. The game boots, runs end-to-end, can be won, saves/loads correctly.

---

## CONTEXT: WHAT EXISTS RIGHT NOW

### Gameplay Complete
- **State machine:** `_Bootstrap → MainMenu → RunSetup → ScavengePhase3D → TransitionCutscene → SurvivalPhase2D → RunEnd states` — all wired, all reachable, all verified in Play mode
- **Scavenge:** 60-second FPS panic, 25 pickups (22 items + 3 crew), carry weight enforced (Marina 12kg/Yuri 15kg/Sasha 19kg), BunkerEntranceTrigger, EmissionTimer, ScavengeHUD
- **Bunker:** Day loop, event engine (1020 events, 15 resolved per run test), faction reputation (3 factions), crew management (HP/SAN/FAT/RAD), BunkerHUD, EventModalUI
- **Victory:** 4 endings (Stabilization/Relief/Adaptation/Independent), VictoryConditionEvaluator checks rep ≥+60 after day 15 or neutral after longer tenure
- **Steam:** Achievements, stats, cloud save — all wired via Facepunch.Steamworks 2.x
- **Prop pipeline:** 4 GLB props (crate, ammo_box, artifact, pry_bar) → decimated to 8k/3k/1k LODs, runtime loaded via glTFast, ScavengePropDresser swaps 8/25 pickups

### What's Missing (YOUR SCOPE)
1. **ZERO audio** — no music, no SFX, no ambient, no footsteps, no pickup sounds, no UI clicks
2. **No VFX** — no emission flash visual, no particle effects, no screen shake, no pickup feedback
3. **No post-processing volume** in scavenge scene — flat URP, no bloom/vignette/DoF
4. **No dust particles** — the atmosphere is sterile
5. **No hover highlight** on pickups — player can't tell what they're looking at
6. **No world-space tooltip** — player doesn't know item name/weight before picking up
7. **No pickup placement variety** — all items sit perfectly flat, no rotation/tilt variation
8. **No contact shadows** — the scene looks flat
9. **No ambient audio** — silence is wrong, should be wind + distant rumble + fluorescent buzz

---

## TASK 1: AUDIO SYSTEM — AudioManager + Music + SFX + Ambient

### Problem
The game has zero audio. A survival game without audio is dead. The atmosphere needs:
- Ambient wind + distant rumble in scavenge phase
- Fluorescent light buzz (synced to FluorescentFlicker)
- Footstep sounds (surface material aware)
- Pickup SFX (different per archetype — metallic clink for cans, heavy thud for crates, paper rustle for documents)
- UI button clicks
- Emission warning siren (escalating as timer drops)
- Bunker ambient hum (generator, ventilation)
- Day transition sound
- Event modal open/close sounds
- Victory/defeat stingers

### Implementation

Create `Assets/_Project/Scripts/OblastZero.Gameplay/AudioManager.cs` (namespace `OblastZero.Gameplay`):

```csharp
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Central audio controller. Routes all game SFX and music through named cues.
    /// Uses a pool of AudioSources for overlapping SFX, plus dedicated sources for
    /// ambient loops and music. No external audio files required at build time —
    /// generates procedural SFX via OnAudioFilterRead where no clip is assigned.
    /// 
    /// Singleton via GameManager. Do NOT use FindObjectOfType.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        // ─── Cue identifiers (use these, not string literals) ───────────
        public const string CUE_PICKUP_CRATE = "pickup_crate";
        public const string CUE_PICKUP_METAL = "pickup_metal";
        public const string CUE_PICKUP_PAPER = "pickup_paper";
        public const string CUE_PICKUP_WEAPON = "pickup_weapon";
        public const string CUE_PICKUP_ARTIFACT = "pickup_artifact";
        public const string CUE_PICKUP_CREW = "pickup_crew";
        public const string CUE_PICKUP_REJECTED = "pickup_rejected";
        public const string CUE_FOOTSTEP_CONCRETE = "footstep_concrete";
        public const string CUE_FOOTSTEP_METAL = "footstep_metal";
        public const string CUE_UI_CLICK = "ui_click";
        public const string CUE_UI_HOVER = "ui_hover";
        public const string CUE_EMISSION_WARN = "emission_warn";
        public const string CUE_EMISSION_CRITICAL = "emission_critical";
        public const string CUE_EMISSION_HIT = "emission_hit";
        public const string CUE_DAY_ADVANCE = "day_advance";
        public const string CUE_EVENT_OPEN = "event_open";
        public const string CUE_EVENT_CLOSE = "event_close";
        public const string CUE_VICTORY = "victory_stinger";
        public const string CUE_DEFEAT = "defeat_stinger";
        public const string CUE_AMBIENT_SCAVENGE = "ambient_scavenge";
        public const string CUE_AMBIENT_BUNKER = "ambient_bunker";

        // ... full implementation
    }
}
```

### Requirements

1. **AudioManager singleton** — lives on the _Bootstrap scene (never unloaded), survives scene transitions via `DontDestroyOnLoad`
2. **Cue-based API** — `AudioManager.Play(CUE_PICKUP_CRATE)` / `AudioManager.Play3D(CUE_FOOTSTEP_CONCRETE, position)`
3. **Procedural SFX fallback** — if no AudioClip is assigned to a cue, generate the sound procedurally using `OnAudioFilterRead`:
   - Pickup: short noise burst + resonant filter (different Q/freq per archetype)
   - Footstep: filtered noise burst, 50-150ms, lowpass swept from 2kHz to 200Hz
   - UI click: 20ms sine blip at 800Hz
   - Emission warning: rising siren (sine sweep 400→1200Hz, 2s cycle, repeats)
   - Day advance: low rumble + bell tone
   - Victory stinger: ascending major arpeggio (C-E-G-C, sine + harmonics, 3s)
   - Defeat stinger: descending minor (A-F-D, distorted, 4s)
4. **Ambient loops** — two dedicated looping AudioSource components:
   - Scavenge ambient: low-frequency rumble (filtered noise, 40-80Hz emphasis) + occasional creaks
   - Bunker ambient: generator hum (60Hz fundamental + harmonics) + ventilation hiss
5. **Music** — simple procedural ambient music for the bunker phase (slow evolving drone, root note shifts with bunker radiation level)
6. **Footstep system** — integrate with ScavengePlayerController. Raycast down, check PhysicMaterial or surface tag, play appropriate cue. Rate-limited (min 300ms between steps, walking; 200ms sprinting)
7. **Volume control** — Master/SFX/Music/Ambient channels via AudioMixer. Expose to options menu later.
8. **EventBus integration** — subscribe to `ItemPickedUpEvent`, `CrewRescuedEvent`, `ScavengePickupRejectedEvent`, `DayAdvancedEvent`, `EventPresentedEvent`, `EventResolvedEvent`, `ScavengeTimerTickEvent` (for emission warning at 15s and 5s)

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/AudioManager.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/ProceduralSfx.cs` (the OnAudioFilterRead generators)
- `Assets/_Project/Scripts/OblastZero.Gameplay/FootstepAudio.cs` (attached to player, raycasts surface)

### Files to Modify
- `Assets/_Project/Scripts/Core/Bootstrap.cs` — add AudioManager to the bootstrap flow
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — integrate footstep audio
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs` — fire pickup SFX on item pickup
- `Assets/_Project/Scripts/UI/OblastUI.cs` — add UI click/hover sounds to button creation
- `Assets/_Project/Scripts/Core/States/TransitionCutsceneState.cs` — play emission hit + day advance
- `Assets/_Project/Scripts/Core/States/RunFailedState.cs` — play defeat stinger
- `Assets/_Project/Scripts/Core/States/RunEndVictoryStateBase.cs` — play victory stinger
- `Assets/_Project/Scripts/OblastZero.Gameplay/BunkerPhaseController.cs` — play day advance + event open/close

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- No audio clip files are needed — all procedural
- On entering Scavenge scene, ambient loop starts
- Walking produces footstep sounds at appropriate intervals
- Picking up an item produces a sound matching its archetype
- Timer at 15s starts warning siren, at 5s goes critical
- Entering bunker plays day advance sound
- Victory/defeat plays stinger
- `grep -rn "AudioManager" Assets --include="*.cs"` shows the system wired everywhere

---

## TASK 2: VFX — EMISSION FLASH + SCREEN SHAKE + PICKUP FEEDBACK

### Problem
The emission timer ticks down silently and visually does nothing except change the HUD text color. A 60-second panic countdown without visual escalation is not panic. The player needs to FEEL the emission coming.

### Implementation

Create `Assets/_Project/Scripts/OblastZero.Gameplay/EmissionVfxController.cs` (namespace `OblastZero.Gameplay`):

```csharp
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Drives the visual escalation of the Emission as the timer counts down.
    /// Subscribes to ScavengeTimerTickEvent. Three phases:
    ///   1. Normal (60-15s): no effect
    ///   2. Warning (15-5s): screen edge desaturation, subtle vignette pulsing,
    ///      occasional flash frames, camera micro-shake
    ///   3. Critical (5-0s): heavy screen distortion, redshift, violent screen shake,
    ///      rapid flash frames, FOV punch
    /// </summary>
    public class EmissionVfxController : MonoBehaviour
    {
        // ... full implementation
    }
}
```

### Requirements

1. **Screen-edge desaturation** — as timer drops below 15s, apply a URP volume override that desaturates the screen edges (radial mask, center=full color, edges=grey). Intensify from 0 at 15s to 0.7 at 5s.

2. **Vignette pulse** — below 15s, pulse the vignette intensity at 1Hz (15s) → 4Hz (5s). Use `Mathf.Sin(Time.time * frequency)` scaled by remaining time.

3. **Screen shake** — below 10s, add a micro-shake to the camera (random offset ±0.02m at 10s → ±0.08m at 0s). Use `Camera.main.transform.localPosition` offset, accumulate, then restore. Do NOT interfere with mouse look — apply as a post-look offset on a parent transform or a child camera.

4. **Flash frames** — below 15s, randomly (increasing probability as timer drops) trigger a single-frame white flash overlay on the HUD canvas. At 15s: 2% chance per frame. At 5s: 15% per frame. At 0s: guaranteed. Use a fullscreen UIPanel with a white Image, alpha 0 normally, spike to 0.6 for 1 frame, fade over 200ms.

5. **Redshift** — below 5s, apply a ColorAdjustments override shifting hue toward red (post-exposure +0.2, color filter warm red). Intensify toward 0s.

6. **FOV punch** — at 0s (emission hit), punch the camera FOV from 60 → 75 over 0.3s, then hold. This is the "empression hits" moment.

7. **Pickup feedback VFX** — when an item is picked up:
   - Brief scale pulse on the pickup object (1.0 → 1.2 → 0.0 over 200ms before destruction)
   - A particle burst (8-12 particles, upward spread, colored to match archetype: medical=white, artifact=cyan, weapon=orange, food=green, tool=yellow)
   - A localized light flash (point light, intensity 0→3→0 over 150ms, color match)

8. **Carry weight overflow feedback** — when a pickup is refused due to weight:
   - Red flash on the HUD load bar (red→normal over 300ms)
   - Slight camera shake (±0.02m, one frame)
   - The refused item does a brief shake animation (rotate ±10° Y, 200ms)

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/EmissionVfxController.cs`
- `Assets/_Project/Scripts/OblastZero.Gameplay/PickupVfx.cs` (scale pulse + particle burst + light flash on pickup)
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScreenShake.cs` (reusable camera shake utility)

### Files to Modify
- `Assets/_Project/Scripts/UI/ScavengeHUD.cs` — add the flash overlay Image
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs` — trigger PickupVfx on pickup
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — attach ScreenShake
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — add emission VFX threshold constants (EMISSION_VFX_WARNING_SECONDS = 15, EMISSION_VFX_CRITICAL_SECONDS = 5)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- In Play mode at 15s, visual escalation begins (subtle)
- At 5s, the screen is clearly distressed (shake, redshift, flashes)
- Picking up an item shows a particle burst + light flash
- Refusing an item shows red flash on load bar + item shake

---

## TASK 3: POST-PROCESSING VOLUME FOR SCAVENGE SCENE

### Problem
The scavenge scene has flat URP lighting with only fog. It needs post-processing to read as "abandoned Soviet grain depot at dusk" — bloom on the fluorescents, vignette, depth of field, dust particles, and a cold color grade.

### Implementation

Update `tools/generate_scavenge_scene.py` to add URP volume overrides to the `ScavengeVolumeProfile.asset`:

### Volume Overrides to Add

1. **Bloom** — threshold 0.9, intensity 0.5, tint cold white (#e8e8f0). Makes fluorescent tubes glow.

2. **Color Adjustments** — contrast +10, saturation -15, post-exposure -0.3. Desaturated, slightly dark. The Soviet look: everything is grey, everything is tired.

3. **Color Curves** — lift the blue channel in shadows (cold shadows), lower blue in highlights (warm highlights from fluorescents). This is the warm-light/cold-shadow contrast that defines the scene's mood.

4. **Vignette** — intensity 0.4, smoothness 0.5, center (0.5, 0.5), color #1a1a22. Darkens corners naturally.

5. **Depth of Field** — focus distance 5m, focal length 35mm, aperture f/2.8, max blur size small. Background shelves blur slightly — adds depth perception and frames the player's attention.

6. **Film Grain** — intensity 0.15, type Medium, response 0.5. Subtle film grain adds texture and hides banding in the fog.

7. **Ambient Occlusion** (if URP supports it in this project) — intensity 0.5, radius 0.5, type Multi. Contact shadows in the corners of shelves and behind crates.

### Ambient Color
- Set scene ambient to cold blue-grey (#50545c) — shadows read cold
- Fluorescent fixtures stay warm-white (#fff4e0)
- The warm-cold contrast is the visual signature

### Dust Particle System
Add a `ParticleSystem` component to a GameObject at the scene center:
- 200 particles max
- Start lifetime: 10-15 seconds (random between two constants)
- Start speed: 0.1-0.3 downward (slow drift)
- Start size: 0.02-0.05 (tiny motes)
- Start color: pale grey with low alpha (0.3)
- Shape: Box, dimensions 30×8×30 (covers the depot interior)
- Emission rate: 15/s
- Renderer: billboard quad, material = `M_Dust` (unlit, additive, soft particle)
- Simulation space: World (so dust doesn't follow the parent)
- Max particles: 300 (some overlap for continuous coverage)

### Files to Modify
- `tools/generate_scavenge_scene.py` — add all volume overrides + dust particle system to the scene YAML
- `Assets/Settings/ScavengeVolumeProfile.asset` — will be regenerated by the script
- `Assets/Art/Materials/Scavenge/M_Dust.mat` — new, additive unlit material for dust

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_scavenge_scene.py` runs without error and regenerates scene
- The scene has 6+ volume overrides
- Dust particles visible in Play mode (slow drift, lit by fluorescents)
- IMPORTANT: If you run the generator only to validate, restore materials afterwards:
  ```bash
  git checkout -- Assets/Art/Materials/Scavenge Assets/Settings/ScavengeVolumeProfile.asset
  ```

---

## TASK 4: PICKUP INTERACTION POLISH — HOVER HIGHLIGHT + WORLD TOOLTIP

### Problem
The player walks near a pickup and has no idea what it is. No highlight, no name, no weight. The player just hits E and hopes. This is unacceptable for a game where carry weight decisions are the core mechanic.

### Implementation

Create `Assets/_Project/Scripts/OblastZero.Gameplay/PickupHoverHighlight.cs`:

```csharp
using UnityEngine;
using OblastZero.Data;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Attaches to each ScavengePickup. Highlights when the player's interaction
    /// raycast hits it, shows a world-space tooltip with item name + weight + archetype.
    /// 
    /// The tooltip is a dynamically created TextMesh (NOT TextMeshPro — we can't
    /// assume TMP is imported in all environments; TextMesh uses the default font
    /// and always renders). It billboard-faces the camera each frame.
    /// </summary>
    [RequireComponent(typeof(ScavengePickup))]
    public class PickupHoverHighlight : MonoBehaviour
    {
        private ScavengePickup _pickup;
        private Renderer[] _renderers;
        private Material[] _originalMaterials;
        private TextMesh _tooltip;
        private bool _isHovered;

        void Awake()
        {
            _pickup = GetComponent<ScavengePickup>();
            _renderers = GetComponentsInChildren<Renderer>();
            _originalMaterials = new Material[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
                _originalMaterials[i] = _renderers[i].material;
            CreateTooltip();
        }

        void CreateTooltip()
        {
            var tooltipObj = new GameObject("Tooltip");
            tooltipObj.transform.SetParent(transform, false);
            tooltipObj.transform.localPosition = Vector3.up * 1.0f; // 1m above the pickup
            _tooltip = tooltipObj.AddComponent<TextMesh>();
            _tooltip.fontSize = 24;
            _tooltip.characterSize = 0.04f;
            _tooltip.anchor = TextAnchor.LowerCenter;
            _tooltip.alignment = TextAlignment.Center;
            _tooltip.color = new Color(0.9f, 0.9f, 0.95f, 0.95f);
            _tooltip.text = BuildTooltipText();
            _tooltip.gameObject.SetActive(false);
        }

        string BuildTooltipText()
        {
            // Resolve item data from GameDatabase
            if (_pickup.Kind == ScavengePickup.PickupKind.Item)
            {
                var item = GameManager.Instance?.Database?.GetItem(_pickup.DataId);
                if (item != null)
                {
                    var archetype = VisualArchetypeMapping.Resolve(item);
                    float weight = item.weightKg;
                    return $"{item.displayName}\n{weight:F1} kg\n[{archetype.ToString().ToUpper()}]";
                }
            }
            else if (_pickup.Kind == ScavengePickup.PickupKind.Crew)
            {
                var crew = GameManager.Instance?.Database?.GetCrew(_pickup.DataId);
                if (crew != null) return $"{crew.displayName}\nCREW\n[RESCUE]";
            }
            return _pickup.DataId;
        }

        public void OnHoverStart()
        {
            if (_isHovered) return;
            _isHovered = true;
            _tooltip.gameObject.SetActive(true);
            // Emissive highlight: boost emission on all renderers
            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.material.EnableKeyword("_EMISSION");
                r.material.SetColor("_EmissionColor", new Color(0.3f, 0.3f, 0.4f, 1f));
            }
        }

        public void OnHoverEnd()
        {
            if (!_isHovered) return;
            _isHovered = false;
            _tooltip.gameObject.SetActive(false);
            // Restore original materials
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].material = _originalMaterials[i];
            }
        }

        void Update()
        {
            // Billboard the tooltip toward the camera
            if (_isHovered && _tooltip != null && Camera.main != null)
            {
                _tooltip.transform.rotation = Quaternion.LookRotation(
                    _tooltip.transform.position - Camera.main.transform.position);
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _originalMaterials != null && i < _originalMaterials.Length)
                    _renderers[i].material = _originalMaterials[i];
            }
        }
    }
}
```

### Integration with ScavengePlayerController

The ScavengePlayerController already does a raycast for interaction. Extend it:
- When the raycast hits a `ScavengePickup`, call `GetComponent<PickupHoverHighlight>().OnHoverStart()`
- When the raycast no longer hits that pickup, call `OnHoverEnd()` on the previous one
- Track the currently hovered pickup to avoid calling OnHoverStart every frame

### Pickup Range Indicator
Add a ground ring under the player showing the interaction radius:
- Simple transparent cylinder, Y=0.01 above ground, radius = interaction range (check BalanceConstants for the value, likely ~2.5m)
- Material: additive transparent, color cyan with 0.1 alpha
- Parented to the player root, follows the player
- Only visible in scavenge phase

### Files to Create
- `Assets/_Project/Scripts/OblastZero.Gameplay/PickupHoverHighlight.cs`

### Files to Modify
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` — hover tracking
- `tools/generate_scavenge_scene.py` — add PickupHoverHighlight to every pickup GameObject
- `Assets/_Project/Scripts/Core/BalanceConstants.cs` — if no interaction range constant exists, add `SCAVENGE_INTERACTION_RANGE = 2.5f`

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- When the player looks at a pickup, it highlights (emissive boost) and a tooltip appears above it
- Tooltip shows: item display name, weight in kg, archetype name
- Walking away removes highlight + tooltip
- Crew pickups show "CREW / [RESCUE]" instead of weight
- The interaction ring is visible on the ground under the player

---

## TASK 5: PICKUP PLACEMENT VARIETY + SCENE POLISH

### Problem
All 25 pickups sit perfectly flat at their positions with zero rotation. The scene looks like a debug grid, not an abandoned depot. Items should rest at natural angles, some grouped, some knocked over.

### Implementation

Update `tools/generate_scavenge_scene.py` to add:

1. **Per-pickup rotation variety:**
   - Random Y rotation (0-360°) for items on shelves
   - Slight random tilt (±5° X and Z) for ~30% of items (simulating being knocked over)
   - Crew pickups: upright, no tilt, but face a random direction (random Y only)

2. **Scale variation:**
   - Same-archetype items get ±10% scale variation on all axes
   - This avoids the "identical clone army" look

3. **Prop clustering:**
   - Add cluster groups: some pickups placed in pairs/triples near shared positions
   - One pickup is the "anchor" (shelf position), others offset by small random amounts (±0.3m)
   - 4-5 clusters across the scene: 3 crates stacked, 2 ammo boxes side by side, 2 medical kits near each other, etc.

4. **Material per-archetype validation:**
   - Each archetype gets its own material color:
     - Crate: warm brown (#8B6F47)
     - MetalCan: dull green-grey (#6B7268)
     - AmmunitionBox: dark olive (#4A4D3A)
     - Document: manila (#C4A968)
     - WeaponSidearm: gunmetal (#3A3A40)
     - WeaponLong: wood + metal (#5A4A35)
     - Tool: rusty (#8A5A30)
     - Artifact: emissive cyan (#40E0D0)
     - Clothing: olive (#5A5A3A)
     - Medical: faded red (#8A4040)
     - Crew: capsule grey (#A0A0A5)
   - GLB-loaded props keep their authored materials; only primitives get these colors

5. **Shelving detail:**
   - Add rust stain decals (simple colored quads with transparent material) on shelf surfaces near items
   - Add a few fallen items on the floor (not pickups, just visual props — primitive shapes with random rotation, no collider, no ScavengePickup component)

### Files to Modify
- `tools/generate_scavenge_scene.py` — all of the above

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_scavenge_scene.py` regenerates the scene without error
- The 25 pickups have varied rotations and slight tilts
- Same-archetype items have slightly different scales
- At least 4 clusters of 2+ items visible in the scene
- No two items at identical transforms
- Floor props (non-pickup visual items) present

---

## EXECUTION ORDER AND COMMIT STRATEGY

Each task is independently committable. Suggested order:

1. **Task 3** (post-processing volume) — visual foundation, everything else reads against it
   - Commit: `polish: URP post-processing volume + dust particles for scavenge atmosphere`

2. **Task 1** (audio) — the biggest "feels alive" upgrade
   - Commit: `feat: AudioManager + procedural SFX + ambient loops + footstep system`

3. **Task 2** (VFX) — emission escalation, makes the panic phase feel like panic
   - Commit: `feat: emission VFX escalation + pickup feedback + screen shake system`

4. **Task 4** (hover highlight + tooltip) — interaction feel
   - Commit: `feat: pickup hover highlight + world-space tooltip + interaction range indicator`

5. **Task 5** (placement variety) — scene polish
   - Commit: `polish: pickup placement variety + prop clustering + scale variation`

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
- **No external audio files needed** — all SFX procedural via OnAudioFilterRead
- **No TextMeshPro dependency for tooltip** — use TextMesh (always available, no import step)

### After All Five Tasks
The game will have:
- Full audio (ambient, SFX, UI, stingers) — all procedural, zero asset files needed ✅
- Emission VFX escalation (desaturation, shake, flash, redshift, FOV punch) ✅
- Post-processing atmosphere (bloom, vignette, DoF, film grain, AO, color grading) ✅
- Hover highlight + world-space tooltip — player knows what they're grabbing ✅
- Natural-looking pickup placement with clusters and variety ✅
- Dust particles floating in the fluorescent light ✅

The game will FEEL like a real shipped product, not a debug scene.
