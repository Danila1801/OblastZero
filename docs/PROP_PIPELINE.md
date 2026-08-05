# Prop pipeline — GLB meshes into the scavenge scene

How an authored `.glb` becomes a mesh the player sees on a pickup, and what to run when you change one.

This replaces the Addressables setup the Phase 2 brief originally called for. **Addressables is not
installed and is not used** — see [Why not Addressables](#why-not-addressables) for the reasoning and
the migration path if that changes.

---

## 1. The short version

```bash
# after adding or replacing anything in Assets/Art/Meshes/Props/
python tools/decimate_props.py          # 40 MB source -> ~0.8 MB shippable, with LODs
python tools/generate_prop_registry.py  # refresh the archetype -> mesh registry asset
python tools/verify_prop_pipeline.py    # 93 checks; must be ALL GREEN
```

Then, in Play mode, attach `PropPipelineSmokeTest` to an empty GameObject and run its context menu
item. Nothing else needs wiring: the scene already carries the dresser, and the loader builds itself.

---

## 2. Why the source meshes cannot ship

The four props under `Assets/Art/Meshes/Props/` are raw AI-generator output:

| Prop | Source size | Source triangles |
|---|---:|---:|
| `prop_ammo_box.glb` | 41.3 MB | 1,420,814 |
| `prop_artifact.glb` | 43.3 MB | 1,487,070 |
| `prop_crate.glb` | 43.0 MB | 1,472,911 |
| `prop_pry_bar.glb` | 38.7 MB | 1,339,696 |

A supply crate rendered at 1.47 M triangles is roughly a thousand times its budget. `Scavenge.unity`
places 25 pickups; at source density that is ~35 M triangles and 165 MB of mesh streamed into a
60-second panic sequence. CLAUDE.md §10 already calls for quad-remeshing AI meshes before import —
`tools/decimate_props.py` is the in-repo, reproducible substitute for that manual Blender step.

After decimation the same set is **3.37 MB total, a 47× reduction**, at 8k/3k/1k triangles per LOD.

Two related facts, both fixed here:

- The source `.glb` files carry a `DefaultImporter` meta, so Unity never imported them as models.
  That is expected, not a mistake: glTFast registers its importer with `overrideExts` rather than
  claiming `.glb` outright, so `AssetDatabase.LoadAssetAtPath<GameObject>` on them returns null.
- `.gitattributes` listed `fbx`/`obj`/`blend` for LFS but not `glb`, so all 165 MB went into git as
  raw blobs. `*.glb` and `*.gltf` are now routed to LFS, which fixes it going **forward only** —
  the existing blobs are already in history. Cleaning that needs `git lfs migrate import
  --include="*.glb"`, which rewrites history and is a deliberate, destructive decision to make
  separately.

---

## 3. What the decimator produces

One `.bytes` file per prop under `Assets/Art/Resources/Props/`, each containing three LOD meshes as
sibling nodes sharing one material and one texture set:

```
Assets/Art/Resources/Props/
├── prop_crate.bytes          # nodes: prop_crate_LOD0 / _LOD1 / _LOD2
├── prop_ammo_box.bytes
├── prop_artifact.bytes
├── prop_pry_bar.bytes
└── prop_manifest.json        # per-prop report: source size, triangle counts, texture sizes
```

**`.bytes`, not `.glb`.** Unity only exposes a file to `Resources.Load<TextAsset>` if its extension
maps to the text-script importer. A `.glb` dropped in a Resources folder imports as DefaultImporter
and loads as null — the same trap the source files are already in.

**Meshes are normalised** — centred on their bounding-box centre and uniformly scaled so the longest
axis is exactly 1.0. That makes a prop's authored size irrelevant at runtime: the loader applies the
`VisualArchetype` footprint and the prop's bottom lands where the scene generator put the primitive's.

**Output is byte-deterministic.** Vertex clustering uses a sorted grid, JSON keys are emitted sorted,
and nothing samples time or randomness. `--check` regenerates in memory and fails on any drift, which
is what makes it a real gate. The one external variable is Pillow's JPEG encoder; a Pillow major
version bump can shift texture bytes, and `--check` names the prop that moved.

### Decimation algorithm

Rossignac–Borrel vertex clustering, binary-searching grid resolution to hit each triangle budget.
Chosen over quadric-error decimation because it is O(n), trivially vectorised, and deterministic
without needing a stable tie-break on edge-collapse order. At a 99.4% reduction on a 34 cm object the
two are indistinguishable.

`python tools/decimate_props.py --self-test` runs 46 checks including two negative controls: a 1-cell
grid must collapse the mesh to nothing, and a corrupted GLB header must be rejected rather than
returning junk.

---

## 4. Runtime path

```
ScavengePropDresser  (in Scavenge.unity, under === SYSTEMS ===)
  └─ GLBPropLoader.PreloadAllRoutine()
       └─ Resources.Load<TextAsset>("Props/prop_crate")
            └─ GltfImport.Load(bytes)  →  InstantiateMainSceneAsync  →  cached template
  └─ for each ScavengePickup:
       resolve VisualArchetype from GameDatabase
       └─ GLBPropLoader.CreateVisual(archetype, pickup.transform, sceneMaterial)
            ├─ cached  → clone template, fit to footprint, build LODGroup
            └─ no mesh → VisualArchetypeMapping.CreateVisual (primitive silhouette)
```

| File | Role |
|---|---|
| `GLBPropLoader.cs` | Loads, caches, instantiates, reference-counts, releases |
| `PropResourceKeys.cs` | Archetype → resource key table, plus readable aliases |
| `PropArchetypeRegistry.cs` | ScriptableObject: per-archetype fit mode, offsets, LOD settings |
| `PropLODManager.cs` | Builds LODGroups; converts switch distances to screen heights |
| `PropInstanceTag.cs` | Marks an instance with its template; distinguishes mesh from fallback |
| `ScavengePropDresser.cs` | Swaps baked primitives for meshes on scene start |
| `PropPipelineSmokeTest.cs` | Play-mode verification (context menu) |

### Fit

The scene generator scales each pickup root to its archetype's local scale and puts a primitive on
it. The dresser hides that primitive and parents a mesh underneath, so the mesh inherits a
**non-uniform** parent scale. `Uniform` fit divides that back out, so a pry bar under a
0.52 × 0.12 × 0.12 root comes out 0.52 m long with its own proportions intact rather than squashed
into a ribbon. `Stretch` fills the footprint per-axis instead, matching what the primitive did — use
it only for props authored as a unit cube.

Note `FootprintOf` is **not** just `ShapeOf(archetype).LocalScale`: Unity's Cylinder and Capsule
primitive meshes are two units tall (CLAUDE.md §14), so a MetalCan authored at 0.13 Y scale is a
0.26 m can. Getting that wrong fits every cylindrical prop at half height.

### LOD

`LODGroup` selects on screen-relative height, not distance, so the design intent ("swap at 10 m") is
converted rather than hardcoded:

```
screenHeight = worldSize / (2 · distance · tan(fov / 2))
```

Defaults: LOD0 under 10 m, LOD1 under 25 m, LOD2 under 50 m, culled beyond. Because the conversion
uses the prop's real world size and the live camera FOV, those distances hold across prop sizes and
FOV changes. `QualitySettings.lodBias` still scales the effective distance — that is what the bias is
for.

### Lifetime

Each template owns a `GltfImport`, which owns the meshes and textures it created. Disposing one turns
live instances into missing-mesh renderers, so instances are reference-counted:

- `ReleaseProp(instance)` — destroy one instance, decrement
- `ReleaseUnused()` — dispose templates with no live instances (the safe reclaim point)
- `ReleaseAll()` — tear everything down; call on scavenge exit

---

## 5. Adding a new prop

1. Drop `prop_<name>.glb` into `Assets/Art/Meshes/Props/`.
2. Add the archetype → key pair to `DefaultKeys` in `PropResourceKeys.cs`, and the matching entry to
   `AUTHORED_MESHES` in `tools/generate_prop_registry.py`. **Both** — `verify_prop_pipeline.py` parses
   the C# table and fails if the two disagree or if a shipped file is unclaimed.
3. Run the three commands from §1.

Seven of eleven archetypes have no mesh yet (`MetalCan`, `Document`, `WeaponSidearm`, `WeaponLong`,
`Clothing`, `Medical`, `Crew`) and keep their primitive silhouettes. That is a designed fallback, not
a gap: an unauthored pickup must still be visible and grabbable.

---

## 6. Verification

| Command | Covers |
|---|---|
| `python tools/decimate_props.py --self-test` | 46 checks — decimation invariants, determinism, 2 negative controls |
| `python tools/decimate_props.py --check` | On-disk outputs match a fresh decimation |
| `python tools/generate_prop_registry.py --check` | Registry asset has not drifted |
| `python tools/verify_prop_pipeline.py` | 93 checks — LOD structure, budgets, normalisation, texture sizes, C#↔file agreement, registry wiring, scene wiring, LFS hygiene |
| `python tools/verify_prop_pipeline.py --self-test` | 4 negative controls for the checks themselves |
| `python tools/verify_steam_layer.py` | 39 checks including a real `dotnet build` with zero `error CS` |
| `PropPipelineSmokeTest` (Play mode) | Real GLTFast load, fit accuracy, LOD wiring, fallback, release |

The scene generator gained a gate too: `assert_script_guids()` verifies every project script GUID in
`SCRIPT_GUIDS` still matches its `.cs.meta`. A wrong script GUID yields a component that is present
but never runs — the most expensive failure mode in headless scene authoring, and previously nothing
watched for it.

---

## 7. Why not Addressables

| | Addressables | Resources + glTFast (chosen) |
|---|---|---|
| Installed | No | glTFast 6.19.0 already in `manifest.json` |
| Group config | `AddressableAssetSettings` + group assets, Editor-only in practice | None |
| Headless authoring | Breaks CLAUDE.md §14's working mode | Works fully headless |
| Build step | Requires an Addressables build before anything loads | None |
| Verifiable in CI | Not without the Editor | Yes — `verify_prop_pipeline.py` |

Content scale also does not justify it: 3.37 MB of props, loaded once per scavenge and released on
exit, is not a streaming problem. Addressables earns its complexity on content sets that cannot fit
in memory or that ship as downloadable packs.

**If that changes**, the migration is contained. `GLBPropLoader.LoadTemplateAsync` is the only method
that touches `Resources.Load`; everything else — fitting, LOD, reference counting, release, the
registry, the dresser — is storage-agnostic. Swapping in
`Addressables.LoadAssetAsync<TextAsset>(key)` there, and pointing `PropResourceKeys` at addressable
keys, is the whole change. Keep the `.bytes` extension either way.

---

## 8. Known gaps

- **`ItemJsonLoader` does not read `visualArchetype`.** The 703 JSON items always derive their
  archetype from category and id; the authored override only applies to the 8 `.asset` items. Correct
  for every current item, but an authored override in JSON is silently ignored.
- **Source props remain under `Assets/`.** They import as DefaultImporter, are referenced by nothing,
  and so do not reach a build — but they do cost 165 MB in every clone. Moving them to a non-`Assets`
  source folder would fix that; it has not been done because they are the only copies.
- **Normal maps are re-encoded as JPEG.** The sources are already JPEG-compressed, so no new
  generation of loss is introduced, but a future PNG/KTX2 source should not be routed through the
  JPEG path.
