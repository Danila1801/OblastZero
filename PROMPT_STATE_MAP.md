# OBLASTZERO — PROMPT STATE MAP
**As of 10 Aug 2026 — Post-Phase-8-Feedback actual repo state**

---

## THE KEY INSIGHT
**Phases 1-7 are ALL COMPLETE. The project is feature-complete.** What remains:
1. 91 hardcoded strings across 5 UI screens (localization debt — pure scope work)
2. ~1,020 uncommitted event JSONs + 14 C# files from 5 Aug parallel session (commit decision)
3. **Play-mode verification for all systems built after 5 Aug** — agent-capable, proven 5 Aug (see PLAY_LOOP_VERIFICATION.md), needs only Unity running + MCP transport on stdio

---

## CRITICAL: Play-Mode Verification IS Agent-Capable

`PLAY_LOOP_VERIFICATION.md` (committed at HEAD, 253 lines) documents the agent driving Play mode boot→wipe on 5 Aug 2026. Six defects found and fixed, one commit each (`109f1ad` through `573e4d1`). The agent used `Button.onClick.Invoke()`, `EndDayRequestedEvent`, `EventChoiceSelectedEvent` — the real UI/EventBus paths.

**Only 2 human steps required:**
1. Launch Unity Editor from `C:\Users\danil\projects\OblastZero`
2. In MCP For Unity window → Transport → **stdio** (not HTTP Local)

**Already done (stop requiring):**
- ✅ SteamConfig asset exists (appId 480)
- ✅ Bootstrap auto-loads it (`Resources.Load<SteamConfig>("SteamConfig")`)
- ✅ `runInBackground: 1` in ProjectSettings (commit `abe331c`)
- ✅ SampleScene.unity deleted (commit `abe331c`)

---

## WHAT WAS NEVER VERIFIED (The Real Phase 8 Scope)

| System | Built When | Play-Mode Verified? |
|---|---|---|
| Grain Depot scavenge | 25 Jul | ✅ 5 Aug |
| Census Office scavenge | 9 Aug | ❌ NEVER RENDERED |
| Reservoir scavenge | 9 Aug | ❌ NEVER RENDERED |
| Anomalies (Carbon Copy, Interview, Backlog) | 5 Aug | ❌ ONLY GATE-VERIFIED |
| Mutants (Census-Taker, Editor) | 5 Aug | ❌ ONLY GATE-VERIFIED |
| Expeditions | 5 Aug | ❌ NEVER DRIVEN |
| Artifacts (4 use effects) | 5 Aug | ❌ NEVER DRIVEN |
| Options/Pause/Gamepad | 5 Aug | ❌ NEVER DRIVEN |
| First-run tooltips | 5 Aug | ❌ NEVER DRIVEN |
| Russian rendering + language switch | 5 Aug | ❌ NEVER DRIVEN |
| Emission VFX thresholds (15s/5s) | 5 Aug | ❌ GATE-VERIFIED ONLY |
| Meta-unlock UI (token spend) | 5 Aug | ❌ NEVER DRIVEN |
| Composable speed (water + Backlog) | 9 Aug | ❌ NEVER TESTED IN PLAY |

---

## GATES STATUS (All Green, 10 Aug 2026)

```
verify_steam_layer.py          39/39   ALL GREEN (Directory.Build.targets fix)
verify_prop_pipeline.py        93/93   ALL GREEN
content_qa.py                  PASS    schema, IP, voice
csharp_string_qa.py            PASS    2502 literals, 143 files
localization_qa.py             PASS    190 keys, 221 EN, 221 RU
                                        WARNING: 91 literals in 5 screens (allowlist gate)
                                        Gate scope: Assets/_Project/Scripts/UI/ non-recursive ONLY
generate_scavenge_scene.py     ALL GATES + byte-identical
generate_census_scene.py       ALL GATES + byte-identical
generate_reservoir_scene.py    ALL GATES + byte-identical
verify_procedural_sfx.py       32/32
rebalance_weights.py           --check  PASS (mirror gate added)
migrate_event_tags.py           --check  PASS (1020 events, 0 needing update)
decimate_props.py              --check  PASS
balance_analysis.py            Report OK (73-88% win rate)
```

---

## THE 5 SCREENS WITH LOCALIZATION DEBT

| Screen | Location (VERIFIED) | Hardcoded Literals |
|---|---|---|
| `ArtifactUseUI` | `Assets/_Project/Scripts/UI/ArtifactUseUI.cs` | 30 |
| `InterviewSequenceUI` | `Assets/_Project/Scripts/UI/InterviewSequenceUI.cs` | 30 |
| `ExpeditionUI` | `Assets/_Project/Scripts/UI/ExpeditionUI.cs` | 17 |
| `ScavengeHazardHUD` | `Assets/_Project/Scripts/UI/ScavengeHazardHUD.cs` | 14 |
| `OblastUIAudio` | `Assets/_Project/Scripts/UI/OblastUIAudio.cs` | 1 |
| **Total** | All in `UI/` — NOT in `OblastZero.Gameplay/` | **91** |

---

## ACTIVE PROMPT

`OPUS5_PHASE8_PLAYMODE_SHIP_PROMPT.md` — corrected per Phase 8 feedback (docs/PHASE8_PROMPT_FEEDBACK.md)

## ARCHIVED PROMPTS

All old prompts in `docs/archive_prompts/`:
- OPUS5_PHASE3_AUDIO_VFX_PROMPT.md — DONE
- OPUS5_PHASE4_CONTENT_META_PROMPT.md — DONE
- OPUS5_PHASE5_SHIP_PROMPT.md — DONE
- OPUS5_PHASE6_ANOMALIES_MUTANTS_PROMPT.md — DONE
- OPUS5_PHASE7_POLISH_SHIP_PROMPT.md — DONE