# OblastZero

A Unity 6 URP roguelite, built in my own time. In development, not released.

Each run alternates two phases. A 60-second first-person 3D scavenge, where you
send one crew member into a site and bring back what they can carry. Then a 2D
bunker phase, where days pass, events fire, and the run ends in one of four
endings or in failure.

## What is in the code

Roughly 27,000 lines of C# across 143 scripts under `Assets/`.

- **Two phases, one state machine.** Main menu, run setup, 3D scavenge, 2D
  bunker, four victory states and a failure state, all registered under
  `GameStateMachine`.
- **The run is seeded and replayable.** Run randomness goes through `RunRng`
  (`Assets/_Project/Scripts/Core/RunRng.cs`): event selection, loot rolls and
  mutant spawns. Each draw is a pure function of `(seed, stream counter)`, both
  stored in `RunData`, so a run reproduces identically from its seed and
  continues the same stream after save and load. A seed is enough to reproduce
  a bug.
- **Three scavenge sites, fixed layouts.** Collapsed Grain Depot, Flooded
  Census Office and Abandoned Reservoir are handcrafted layouts, not procedural.
  Each scene is emitted by a Python generator in `tools/` from a coordinate
  plan, and the generators are byte-reproducible: running one again produces
  the same `.unity` file, so a regenerated scene can be diffed against the
  committed one. CI runs that check for two of the three sites.
- **Content as JSON.** 703 items and 1020 events under
  `Assets/Data/Resources`, loaded at boot, with a formula evaluator for event
  outcomes. Two localization tables (EN and RU, 221 entries each).
- **Bunker systems.** Crew with carry weight (12, 15 and 19 kg), health,
  sanity and radiation; three factions with reputation; traits; expeditions
  sent from the bunker; artifacts with uses; a meta-progression layer that
  awards salvage tokens at run end and spends them on ten unlocks, one of
  which gates the second site.
- **Scavenge systems.** Three anomalies placed in every site, a mutant spawner
  that fields two mutant types according to the site's threat profile, a
  navigation grid for the mutants, GLB prop loading through glTFast with LOD
  groups, procedural audio and VFX.
- **Steamworks.** Wired through Facepunch.Steamworks: 21 achievements and 15
  stats, driven from the game's event bus. It currently runs against Valve's
  test App ID 480 and has not been validated against a real app page.

## Tech stack

- Unity 6 (6000.4.6f1), Universal Render Pipeline
- C# 9.0
- Unity Input System
- Facepunch.Steamworks (Win64)
- glTFast for GLB props
- Newtonsoft JSON
- Python 3 for content generation, scene generation and the QA gates in `tools/`

## Run it locally

Open the folder in Unity Hub with Unity 6000.4.6f1, open
`Assets/Scenes/_Bootstrap.unity` and press Play. Steam initialises against App
ID 480 from `steam_appid.txt`; without a running Steam client the layer logs a
warning and the game continues offline.

## Checks without the Editor

```bash
python tools/verify_steam_layer.py            # compiles the C# and checks the Steam layer, 39 checks
python tools/content_qa.py                    # item and event JSON: schema, voice, IP firewall
python tools/localization_qa.py               # every key in UIStringKeys.cs present in every table
python tools/generate_census_scene.py --check # regenerated scene matches the committed one
```

`.github/workflows/ci.yml` runs these and the other `tools/` gates on every
push. The Editor-side smoke tests live next to the code they test
(`*Test.cs` under `Assets/_Project/Scripts/OblastZero.Gameplay`) and run from
the component context menu.

## Status

In development. The systems above exist and compile. Open work: content
balance (whether an authored run can actually reach an ending has not been
measured in play), prop meshes (4 of the 11 prop archetypes have one), and
playtesting.

## License

MIT, see `LICENSE`.
