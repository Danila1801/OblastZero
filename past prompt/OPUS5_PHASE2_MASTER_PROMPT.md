# MASTER CLAUDE CODE PROMPT — OblastZero Phase 2: Play Loop + GLB Prefabs + Visual Polish

**Target:** Claude Code CLI (Opus 5 / Sonnet 4), launched from `C:\Users\danil\projects\OblastZero`
**Branch:** `feat/scavenge-3d-scene`
**Scope:** Wire GLB meshes into the VisualArchetype system, build the runtime prefab loader, fix the scene flow for the first end-to-end play loop, and add visual polish to the scavenge scene.
**Rule:** Read CLAUDE.md first. It is the law. The design bible is the reference.
**State:** Phase 1 is DONE — all 5 code-gap tasks committed (§9.3, §9.5, §9.4, VisualArchetype, §8.3). `verify_steam_layer.py` is 39/39 green. 4 prop GLBs + 3 textures + 4 reference images are committed under `Assets/Art/`.

---

## CONTEXT: WHAT EXISTS RIGHT NOW

### Art Assets (committed to repo)
```
Assets/Art/Meshes/Props/
├── prop_crate.glb          # Wooden supply crate
├── prop_ammo_box.glb       # Flat metal ammunition box
├── prop_artifact.glb       # Glowing anomaly artifact (sphere-like)
└── prop_pry_bar.glb        # Pry bar tool

Assets/Art/Textures/
├── tex_concrete_stain.png  # Stained concrete floor texture
├── tex_concrete_wall.png   # Concrete wall texture
└── tex_rusted_metal.png     # Rusted metal surface

Assets/Art/References/       # Reference images used to generate the GLBs (keep for docs)
```

### VisualArchetype System (already built — 4c657c2)
The enum `VisualArchetype` has 12 values: `Auto, Crate, MetalCan, AmmunitionBox, Document, WeaponSidearm, WeaponLong, Tool, Artifact, Clothing, Medical, Crew`.

`VisualArchetypeMapping` has:
- `Derive(ItemData)` — classifies items by category + id substring rules
- `Resolve(ItemData)` — returns authored override or derived archetype
- `ShapeOf(archetype)` — returns `PrimitiveType` + `Vector3` local scale
- `CreateVisual(archetype, parent, material)` — creates a primitive GameObject (placeholder)

**The problem:** `CreateVisual` currently spawns Unity primitives (Cube, Cylinder, Sphere, Capsule). The GLB files exist in `Assets/Art/Meshes/Props/` but NOTHING loads them. The scavenge scene uses primitives, not real meshes.

### Scene Flow (current)
```
_Bootstrap.unity  →  (Bootstrap.cs instantiates GameManager prefab)
  → MainMenuState  →  RunSetupState  →  ScavengePhase3DState (loads Scavenge.unity additively)
    → ScavengeController  →  player walks, picks up items
  → SurvivalPhase2DState (loads Bunker.unity additively)
  → RunEnd states (RunFailed, RunVictory*)
```

`SceneLoader.cs` handles additive scene load/unload. `ScavengePhase3DState.cs` calls `_sceneLoader.LoadSceneAdditive(ScavengeSceneName)`.

### GLTFast Package
`com.unity.cloud.gltfast` v6.19.0 is already in `Packages/manifest.json`. This is the official Unity GLB importer. Use it for runtime GLB loading.

---

## TASK 1: RUNTIME GLB PREFAB LOADER — REPLACE PRIMITIVES WITH REAL MESHES

### Problem
`VisualArchetypeMapping.CreateVisual()` spawns primitives. We have 4 GLB files that should replace 4 archetypes:
- `prop_crate.glb` → `VisualArchetype.Crate`
- `prop_ammo_box.glb` → `VisualArchetype.AmmunitionBox`
- `prop_artifact.glb` → `VisualArchetype.Artifact`
- `prop_pry_bar.glb` → `VisualArchetype.Tool`

The other 8 archetypes still need primitives (no GLBs yet): `MetalCan, Document, WeaponSidearm, WeaponLong, Clothing, Medical, Crew`. The system must gracefully fall back to primitives when no GLB exists.

### Fix Required

1. **Create `PropMeshLoader.cs`** in `Assets/_Project/Scripts/Core/` (namespace `OblastZero.Core`):

```csharp
using System.Collections.Generic;
using UnityEngine;
using GLTFast;
using GLTFast.Loading;

namespace OblastZero.Core
{
    /// <summary>
    /// Loads GLB meshes from Assets/Art/Meshes/Props/ and caches them by VisualArchetype.
    /// Falls back to VisualArchetypeMapping.CreateVisual (primitives) when no GLB exists.
    ///
    /// GLBs are loaded via GLTFast at runtime. The loader is async — callers must yield.
    /// Loaded GameObjects are cached as prefabs (inactive clones) and instantiated on demand.
    /// </summary>
    public class PropMeshLoader : MonoBehaviour
    {
        private static PropMeshLoader _instance;
        public static PropMeshLoader Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[PropMeshLoader]");
                    _instance = go.AddComponent<PropMeshLoader>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        // Map archetype → GLB filename in Assets/Art/Meshes/Props/
        private static readonly Dictionary<VisualArchetype, string> GlbMap = new()
        {
            { VisualArchetype.Crate, "prop_crate.glb" },
            { VisualArchetype.AmmunitionBox, "prop_ammo_box.glb" },
            { VisualArchetype.Artifact, "prop_artifact.glb" },
            { VisualArchetype.Tool, "prop_pry_bar.glb" },
        };

        private readonly Dictionary<VisualArchetype, GameObject> _cache = new();
        private readonly HashSet<VisualArchetype> _loading = new();

        // ... see full implementation below
    }
}
```

2. **Load GLBs via GLTFast** at runtime. The import path is `Assets/Art/Meshes/Props/<name>.glb`. In Unity, this is accessed via `Resources.Load` if the GLBs are in a `Resources/` folder, OR via Addressables, OR via direct file path. 

**Simplest approach for this project:** Move the 4 GLB files from `Assets/Art/Meshes/Props/` to `Assets/Art/Meshes/Props/Resources/` so they load via `Resources.Load<TextAsset>("Props/prop_crate")`. GLTFast can load from a `TextAsset` (the raw GLB bytes). Create the `Resources` subfolder.

Actually — **GLTFast has a `GltfAsset` component** that references a GLB via the Inspector. But since we're authoring scenes without the Editor (CLAUDE.md §14), we need runtime loading.

**Use `GLTFast.GltfImporter.LoadGltf` with a `DownloadProvider` that reads from Resources.** Or simpler: use `Resources.Load<TextAsset>` to get the GLB bytes and feed them to GLTFast's `GltfImporter.LoadGltfFromBytes`.

Here's the actual implementation pattern:

```csharp
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GLTFast;
using OblastZero.Data;

namespace OblastZero.Core
{
    public class PropMeshLoader : MonoBehaviour
    {
        private static PropMeshLoader _instance;
        public static PropMeshLoader Instance { get { /* singleton lazy init */ } }

        private static readonly Dictionary<VisualArchetype, string> GlbMap = new()
        {
            { VisualArchetype.Crate, " prop_crate" },
            { VisualArchetype.AmmunitionBox, "prop_ammo_box" },
            { VisualArchetype.Artifact, "prop_artifact" },
            { VisualArchetype.Tool, "prop_pry_bar" },
        };

        private readonly Dictionary<VisualArchetype, GameObject> _cache = new();

        /// <summary>
        /// Returns a cloned visual GameObject for the archetype.
        /// If a GLB is cached, instantiates it. Otherwise falls back to primitives.
        /// </summary>
        public GameObject CreateVisual(VisualArchetype archetype, Transform parent, Material material)
        {
            if (_cache.TryGetValue(archetype, out var prefab) && prefab != null)
            {
                var go = Instantiate(prefab, parent, false);
                go.name = "Visual_" + archetype;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                // Apply the archetype's authored scale from VisualArchetypeMapping
                var shape = VisualArchetypeMapping.ShapeOf(archetype);
                go.transform.localScale = shape.LocalScale;
                if (material != null)
                {
                    var renderers = go.GetComponentsInChildren<MeshRenderer>();
                    foreach (var r in renderers) r.sharedMaterial = material;
                }
                // Strip colliders so they don't block the player
                var colliders = go.GetComponentsInChildren<Collider>();
                foreach (var c in colliders) Destroy(c);
                return go;
            }

            // Fallback: primitive (existing behavior)
            return VisualArchetypeMapping.CreateVisual(archetype, parent, material);
        }

        /// <summary>
        /// Preload all GLBs. Call this during scene load (coroutine).
        /// </summary>
        public IEnumerator PreloadAll()
        {
            foreach (var kvp in GlbMap)
            {
                if (_cache.ContainsKey(kvp.Key)) continue;
                yield return LoadGlb(kvp.Key, kvp.Value);
            }
        }

        private IEnumerator LoadGlb(VisualArchetype archetype, string resourceName)
        {
            var asset = Resources.Load<TextAsset>("Props/" + resourceName);
            if (asset == null)
            {
                Debug.LogWarning($"[PropMeshLoader] GLB not found in Resources: Props/{resourceName}");
                yield break;
            }

            var importer = new GLTFast.GltfImport();
            bool done = false;
            bool success = false;

            // GLTFast async load from bytes
            var task = importer.LoadGltfFromBytes(System.Text.Encoding.UTF8.GetString(asset.bytes));
            // If LoadGltfFromBytes doesn't exist in v6.19, use the GltfAsset approach instead

            while (!task.IsCompleted) yield return null;

            if (task.IsCompletedSuccessfully)
            {
                // Wait for instantiation
                var instantiator = new GameObjectInstantiator();
                var instTask = importer.InstantiateMainScene(instantiator);
                while (!instTask.IsCompleted) yield return null;

                if (instTask.IsCompletedSuccessfully)
                {
                    var go = instantiator.Scene;
                    go.SetActive(false);
                    _cache[archetype] = go;
                    Debug.Log($"[PropMeshLoader] Cached GLB for {archetype}: {resourceName}");
                }
            }

            importer.Dispose();
        }
    }
}
```

**IMPORTANT:** The GLTFast v6.19 API may differ from the pseudocode above. Check the actual GLTFast API by reading the package source: `Library/PackageCache/com.unity.cloud.gltfast@6.19.0/`. The key types are `GltfImport`, `GltfAsset`, and the loading methods. If the byte-loading API is not available, move GLBs to `StreamingAssets/Props/` and load via `file:///` URI instead.

**Alternative simpler approach if GLTFast runtime loading is complex:** Create prefabs in the Unity Editor by dragging the GLBs in and assigning them to a `PropMeshLoader` Inspector field. But since we author without the Editor (CLAUDE.md §14), prefer the runtime approach.

**Simplest working approach:** Use `Resources.Load<GameObject>` if Unity has already imported the GLB as a prefab (it does this with glTFast's import settings). Check if `Resources.Load<GameObject>("Props/prop_crate")` returns a prefab — if so, the loading is trivial (no GLTFast runtime code needed, Unity handles it at import time).

### Files to Touch
- `Assets/_Project/Scripts/Core/PropMeshLoader.cs` (new — runtime GLB loader + cache)
- `Assets/Art/Meshes/Props/Resources/` (new folder — move GLBs here so Resources.Load works)
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs` (use PropMeshLoader to spawn visuals)
- `Assets/Data/Scripts/Definitions/OblastZero.Data/VisualArchetype.cs` (add a `HasGlb(archetype)` helper or just let PropMeshLoader own the map)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- On entering Scavenge scene, console logs `[PropMeshLoader] Cached GLB for Crate: prop_crate` etc.
- The 4 pickups with GLB archetypes (Crate, AmmunitionBox, Artifact, Tool) render as real meshes, not primitives
- Other archetypes (MetalCan, Document, etc.) still render as primitives (fallback)

---

## TASK 2: SCENE FLOW — FIRST END-TO-END PLAY LOOP

### Problem
The game has never been played end-to-end. The state flow is:
```
_Bootstrap → MainMenu → RunSetup → ScavengePhase3D → SurvivalPhase2D → [RunEnd states]
```

But several wiring points are unverified:
1. Does `_Bootstrap.unity` → `MainMenuState` actually transition on boot?
2. Does `MainMenuState` → `RunSetupState` work when the player clicks "Start Run"?
3. Does `RunSetupState` → `ScavengePhase3DState` correctly load `Scavenge.unity` additively?
4. After the player finishes scavenging, does `ScavengePhase3DState` → `SurvivalPhase2DState` fire?
5. After survival, do the run-end states (`RunFailed`, `RunVictory*`) actually display?

### Fix Required

1. **Audit the state machine transitions** — read every state in `Assets/_Project/Scripts/Core/States/` and verify:
   - Each state has a correct `Enter()` that sets up the scene/UI
   - Each state has a transition call to the next state (e.g., `_stateMachine.TransitionTo<RunSetupState>()`)
   - No state is missing its `OnEnter` / `OnExit` logic

2. **Fix the `MainMenuState`** — it currently logs `[MainMenuState] OnEnter` but may not have a working "Start Run" button → `RunSetupState` transition. The `MainMenuUI.cs` builds its own canvas at runtime. Verify:
   - The "Start Run" button calls `GameStateMachine.Instance.TransitionTo<RunSetupState>()`
   - Or it calls a method on `GameManager` that triggers the transition

3. **Fix `RunSetupState`** — verify that confirming crew + site selection calls `GameManager.BeginNewRun()` and then transitions to `ScavengePhase3DState`. The `RunSetupUI.cs` should:
   - Show the crew roster (Marina, Yuri, Sasha) with their stats
   - Show the scavenge site catalog (only "Collapsed Grain Depot" is available)
   - On "Confirm", call `GameManager.Instance.BeginNewRun(crewId, siteId)` then transition to scavenge

4. **Fix `ScavengePhase3DState`** — verify that:
   - It loads `Scavenge.unity` additively via `SceneLoader`
   - It finds or spawns a `ScavengePlayerController` and `ScavengeController`
   - It has a way to detect "scavenging complete" (timer, exit trigger, or button) and transition to `SurvivalPhase2DState`
   - On exit, it unloads `Scavenge.unity`

5. **Fix `SurvivalPhase2DState`** — verify that:
   - It loads `Bunker.unity` additively
   - The bunker phase has a minimal loop (consume resources, pass days, trigger events)
   - After N days or a terminal condition, it transitions to a run-end state

6. **Add a `ScavengeExitTrigger`** — the player needs a way to finish scavenging. Add a simple trigger zone at the depot entrance. When the player enters it, fire an event that `ScavengePhase3DState` listens to and transitions to `SurvivalPhase2DState`. This can be a simple `GameObject` with a trigger collider and a monobehaviour that calls `GameStateMachine.Instance.TransitionTo<SurvivalPhase2DState>()`.

### Files to Touch
- `Assets/_Project/Scripts/Core/States/MainMenuState.cs`
- `Assets/_Project/Scripts/Core/States/RunSetupState.cs`
- `Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs`
- `Assets/_Project/Scripts/Core/States/SurvivalPhase2DState.cs`
- `Assets/_Project/Scripts/UI/MainMenuUI.cs` (verify button → transition)
- `Assets/_Project/Scripts/UI/RunSetupUI.cs` (verify confirm → BeginNewRun → transition)
- Possibly new: `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeExitTrigger.cs`

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `grep -rn "TransitionTo" Assets/_Project/Scripts/Core/States --include="*.cs"` shows a complete chain
- Every state references the next state correctly
- No state is a dead end (every path leads to a run-end state or back to menu)

---

## TASK 3: VISUAL POLISH — LIGHTING, FOG, POST-PROCESSING FOR THE SCAVENGE SCENE

### Problem
The scavenge scene (`Scavenge.unity`) has flat URP lighting and no post-processing. The scene already has:
- 15 `FluorescentFlicker` fixtures (ceiling tube lights that flicker)
- A URP volume with fog
- Concrete floor + wall materials
- The 25 pickups on shelving

But it looks flat. The atmosphere should feel like a dim, abandoned Soviet-era grain depot — cold fluorescent light, dust in the air, shadows pooling in corners.

### Fix Required

1. **Enhance the URP Volume** in `Scavenge.unity` via the scene generator (`tools/generate_scavenge_scene.py`):
   - Add a Bloom override (threshold 0.9, intensity 0.5) — makes the fluorescent tubes glow
   - Add a Color Adjustments override (contrast +10, saturation -15, post-exposure -0.3) — desaturated, slightly dark
   - Add a Vignette override (intensity 0.4, smoothness 0.5) — darkens corners
   - Add a Depth of Field override (focus distance 5m, focal length 35, aperture f/2.8) — background shelves blur slightly
   - Keep the existing fog (exponential, density 0.08, color #3a3a42)

2. **Add a cold ambient color** — set the scene's ambient light to a cold blue-grey (#50545c) so shadows have a cold tone. The fluorescent fixtures emit warm-white (#fff4e0) so the contrast between warm light pools and cold shadow is the visual signature.

3. **Add contact shadows** — if URP supports it in the project's render pipeline asset, enable contact shadows on the directional light (soft, distance 0.5).

4. **Update `tools/generate_scavenge_scene.py`** to write these overrides into the scene YAML. The scene generator is the source of truth — any changes made directly in Unity would be overwritten on the next regeneration.

5. **Add a subtle dust particle system** — 200 particles, slow downward drift, lit by the fluorescent fixtures, lifetime 10-15s. This is the atom of atmosphere that sells "abandoned." Place it near the center of the depot.

### Files to Touch
- `tools/generate_scavenge_scene.py` (add URP volume overrides, ambient, dust particles)
- `Assets/Scenes/Scavenge.unity` (regenerate by running the script)
- Possibly `Assets/_Project/Scripts/OblastZero.Gameplay/DustParticles.cs` if the particle system needs a script rather than pure scene data

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_scavenge_scene.py` runs without error and regenerates the scene
- The scene has 5+ URP volume overrides (bloom, color adjust, vignette, DoF, fog)
- Console shows no errors on scene load

---

## TASK 4: PICKUP INTERACTION POLISH — HOVER HIGHLIGHT + TOOLTIP

### Problem
The player picks up items by walking near them and the `ScavengePlayerController` fires `PickupRequested`. But there's no visual feedback when the player is near a pickup or looking at one. The player can't tell what an item is before picking it up.

### Fix Required

1. **Add a hover highlight** — when the player's interaction raycast hits a `ScavengePickup`, add an outline or emissive boost to the pickup's renderer. When the raycast leaves, remove it. Use a simple approach:
   - On hover: set the renderer's material to a highlighted variant (e.g., emission color 0.2 white) or swap to an outline material
   - On un-hover: restore the original material
   
   Add this to `ScavengePlayerController.cs` or a new `PickupHoverHighlight.cs` component.

2. **Add a world-space tooltip** — when hovering a pickup, show a small floating text above it:
   - Line 1: item display name (from `ItemData.displayName` or derived from id)
   - Line 2: weight in kg (e.g., "1.2 kg")
   - Line 3: archetype name (e.g., "CRATE", "AMMO BOX", "ARTIFACT")
   
   Use a simple `TextMesh` or Unity UI `TextMeshProUGUI` anchored to the pickup, facing the camera. Show on hover, hide on un-hover. Do NOT use TextMeshPro if it's not in the project — check `Packages/manifest.json` first. If TMP is not available, use a `TextMesh` (legacy but always available).

3. **Add a pickup range indicator** — a subtle circle on the ground under the player showing the interaction radius. This can be a simple transparent quad with a ring texture, rotated to lie flat, parented to the player.

### Files to Touch
- `Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` (add hover detection + highlight)
- New: `Assets/_Project/Scripts/OblastZero.Gameplay/PickupHoverHighlight.cs` (decoupled highlight component)
- New: `Assets/_Project/Scripts/OblastZero.Gameplay/PickupTooltip.cs` (world-space tooltip)
- `tools/generate_scavenge_scene.py` (add tooltip components to pickup prefabs in the scene)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- When the player approaches a pickup, the pickup gets highlighted and a tooltip appears
- Walking away removes the highlight and tooltip
- The tooltip shows the correct item name, weight, and archetype

---

## TASK 5: SCENE REGENERATION — PICKUP ARCHETYPE VARIETY

### Problem
The scavenge scene has 25 pickups (22 items + 3 crew). All item pickups currently render as their archetype shape (some cubes, some cylinders), but the pickups don't have varied rotations or realistic placement. Some items rest at angles, some lie flat, some lean against walls. The scene looks too regular.

### Fix Required

1. **Update `tools/generate_scavenge_scene.py`** to add per-pickup placement variety:
   - Random rotation Y (0-360°) for items on shelves
   - Slight random tilt (±5° on X and Z) for items that would be knocked over
   - Crew pickups stand upright (no tilt, no random Y if they face a direction)
   
2. **Add prop clustering** — some items should sit in groups (e.g., 3 crates stacked, 2 ammo boxes side by side). The generator currently places each pickup independently. Add a `cluster_group` concept:
   - Some pickups get grouped in pairs or triples near a shared shelf position
   - One pickup in the group is the "anchor" (shelf position), others offset by small random amounts

3. **Scale variation** — items of the same archetype should have ±10% scale variation to avoid the "identical clone" look.

4. **Update the material assignment** — each archetype already has a material (`M_Pickup_Crate`, `M_Pickup_Ammo`, etc.). Verify the scene generator assigns the correct material per archetype, and that GLB-loaded props also get the correct material override (if the GLB brings its own materials, they should be kept; the material override is only for primitive fallbacks).

### Files to Touch
- `tools/generate_scavenge_scene.py` (placement variety, clustering, scale variation, material assignment)

### Verify
- `python tools/verify_steam_layer.py` passes 39/39
- `python tools/generate_scavenge_scene.py` regenerates the scene without error
- The 25 pickups have varied rotations and slight tilts
- Same-archetype items have slightly different scales
- No two items are at identical transforms

---

## EXECUTION ORDER AND COMMIT STRATEGY

Each task is independently committable. Suggested order:

1. **Task 1** (GLB prefab loader) — the biggest visual upgrade, replaces primitives with real meshes
   - Commit: `feat: runtime GLB loader — replace primitives with real prop meshes for 4 archetypes`

2. **Task 2** (scene flow + play loop) — the first end-to-end play
   - Commit: `feat: wire state machine transitions for end-to-end play loop + scavenge exit trigger`

3. **Task 5** (scene regeneration variety) — quick scene polish
   - Commit: `polish: pickup placement variety + scale variation + prop clustering in scavenge scene`

4. **Task 3** (lighting + post-processing) — visual atmosphere
   - Commit: `polish: URP post-processing volume + ambient + dust particles for scavenge scene atmosphere`

5. **Task 4** (hover highlight + tooltip) — interaction feel
   - Commit: `feat: pickup hover highlight + world-space tooltip with item info`

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
- **GLTFast v6.19.0** is already in the project — use it, don't add alternative GLB importers
- **Primitives cứu cylinder/capsule Y scale** — see CLAUDE.md §14: Cylinder and Capsule primitive meshes are TWO units tall, so Y scale is half the desired height

### After All Five Tasks
The game will have:
- Real 3D meshes for 4 archetypes (not primitives) ✅
- Complete state machine flow from boot to run-end ✅
- Visual atmosphere (bloom, fog, vignette, dust) ✅
- Hover highlight + item tooltips ✅
- Varied, natural-looking pickup placement ✅
- The first screenshot/playtest will look like a real game, not a debug scene

The remaining work after this phase:
- Generate more GLB props via Gemini Pro (Imagen) for the remaining 8 archetypes
- Audio (ambient hum, footsteps, pickup sounds)
- Event system UI (the 2D bunker phase)
- Steam store page
- Beta testing

---

## FILES THAT EXIST AND YOU SHOULD NOT RE-CREATE

```
Assets/_Project/Scripts/Core/GameStateMachine.cs           ← state machine (complete)
Assets/_Project/Scripts/Core/GameManager.cs               ← run lifecycle (complete)
Assets/_Project/Scripts/Core/Bootstrap.cs                 ← entry point (complete)
Assets/_Project/Scripts/Core/RunSummary.cs                ← run snapshot (complete)
Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs       ← site list (complete)
Assets/_Project/Scripts/Core/States/MainMenuState.cs       ← exists, verify transitions
Assets/_Project/Scripts/Core/States/RunSetupState.cs       ← exists, verify transitions
Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs ← exists, verify scene load
Assets/_Project/Scripts/Core/States/SurvivalPhase2DState.cs ← exists, verify scene load
Assets/_Project/Scripts/Core/States/RunEndVictoryStateBase.cs ← base class for victory states
Assets/_Project/Scripts/Core/States/RunVictoryStabilizationState.cs ← 4 victory variants
Assets/_Project/Scripts/Core/States/RunVictoryReliefState.cs
Assets/_Project/Scripts/Core/States/RunVictoryAdaptationState.cs
Assets/_Project/Scripts/Core/States/RunVictoryIndependentState.cs
Assets/_Project/Scripts/Core/States/RunFailedState.cs       ← failure state
Assets/_Project/Scripts/OblastZero.Gameplay/ScavengeController.cs ← pickup routing
Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs ← player movement + interaction
Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePickup.cs ← pickup component
Assets/_Project/Scripts/Services/SceneLoader.cs           ← additive scene load/unload
Assets/_Project/Scripts/UI/MainMenuUI.cs                    ← runtime canvas
Assets/_Project/Scripts/UI/RunSetupUI.cs                   ← runtime canvas
Assets/_Project/Scripts/UI/RunSummaryUI.cs                  ← runtime canvas
Assets/Data/Scripts/Definitions/OblastZero.Data/VisualArchetype.cs ← archetype enum + mapping + CreateVisual
Assets/Data/Scripts/Definitions/OblastZero.Data/ItemData.cs ← has visualArchetype field
Assets/Art/Meshes/Props/prop_crate.glb                     ← 4 GLBs to load
Assets/Art/Meshes/Props/prop_ammo_box.glb
Assets/Art/Meshes/Props/prop_artifact.glb
Assets/Art/Meshes/Props/prop_pry_bar.glb
Assets/Art/Textures/tex_concrete_stain.png                 ← 3 textures
Assets/Art/Textures/tex_concrete_wall.png
Assets/Art/Textures/tex_rusted_metal.png
tools/generate_scavenge_scene.py                            ← scene generator (source of truth)
tools/verify_steam_layer.py                                 ← run after each task
Packages/manifest.json                                      ← has GLTFast 6.19.0
```

Go. Ship it. Make it look like a real game.
