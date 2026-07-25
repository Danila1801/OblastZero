# Oblast Zero

A commercial roguelite survival game shipping on Steam Early Access. Built in Unity 6 LTS with a hybrid phase structure: a 60-second 3D first-person scavenge followed by 2D turn-based bunker management.

## Status

**~75% complete** (July 2026). Core framework, state machine, event engine, 691 items, 1020 events, Steamworks integration, localization, and CI/CD are all built and compiling green. The 3D scavenge scene is the primary remaining milestone.

| System | Status |
|---|---|
| Core framework (state machine, EventBus, GameManager) | ✅ Built |
| 8 game states (MainMenu → RunSetup → Scavenge → Bunker → RunFailed → Victory) | ✅ Built |
| Event engine (1020 events, deterministic RNG, formula evaluator) | ✅ Built, 24/24 tests |
| Bunker day loop (Phase 2 management) | ✅ Built, 17/17 tests |
| Data content (691 items + 1020 events as JSON) | ✅ Generated, QA clean |
| Steamworks (Facepunch 2.x — achievements, stats, cloud save) | ✅ Built, compiles green |
| EN localization + GitHub Actions CI + SteamCMD deploy | ✅ Built |
| 3D Scavenge scene (Phase A — playable level) | 🔶 In progress |
| Play-mode verification + polish |🔜 Pending |

## Tech stack

- **Unity 6** (6000.4.6f1)
- **Universal Render Pipeline** (custom 2.5D hybrid depth pass)
- **Unity Input System** (new)
- **Facepunch.Steamworks 2.x** (Win64)
- **Newtonsoft JSON** (serialization)

## Run it locally

Open in Unity Hub with Unity 6000.4.6f1, then open `Assets/Scenes/_Bootstrap.unity` and press Play. Bootstrap initializes Steam (App ID 480 / Spacewar for testing — no Steamworks registration needed during development).

## Compile check (without Unity Editor)

```bash
cd ~/projects/OblastZero && python3 tools/verify_steam_layer.py
# 39/39 checks = green
```
