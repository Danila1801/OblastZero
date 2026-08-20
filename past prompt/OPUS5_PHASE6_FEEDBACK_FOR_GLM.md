# Phase 6 implementation report — for GLM 5.2

**From:** Claude Opus 5, executing `OPUS5_PHASE6_ANOMALIES_MUTANTS_PROMPT.md`
**Project:** Oblast Zero — Unity 6 / URP roguelite survival game, C# LangVersion 9.0, shipping to Steam Early Access
**Repo:** `github.com/Danila1801/OblastZero`, branch `feat/scavenge-3d-scene`
**Date:** 5 August 2026
**Commits:** `6be26e2`, `8eeba24`, `5353609`, `7b2cb2e`, `ef135c8` — all pushed

This report assumes no prior context. Everything needed to evaluate it is below.

---

## 0. What the game is, in one paragraph

Two-phase roguelite. **Phase A ("the Blowout")** is a 60-second first-person real-time scavenge in a 3D level: grab supplies and crew, reach the bunker door before a radiation emission hits. **Phase B ("the Bunker")** is turn-based 2D management: feed the crew, manage health/sanity/radiation, resolve data-driven narrative events. A run is permadeath; meta-progression persists across runs. Setting is a Soviet-administrative post-disaster oblast — the tone rule in the project's `CLAUDE.md` is "the Oblast does not raise its voice, it files a form." All content is original IP with a hard firewall against a well-known Ukrainian survival-shooter franchise used only as a tone reference.

Two documents are authoritative: `CLAUDE.md` (engineering law) and a design bible, with `BESTIARY.md` as the extracted creature/anomaly reference. The Phase 6 brief asked for five systems from that bestiary.

---

## 1. Headline

**All five task areas were addressed. Four shipped complete and verified. One shipped as its data layer only, deliberately, and I say exactly what is missing in §6.**

Every gate green:

| Gate | Result |
|---|---|
| `verify_steam_layer.py` | 39/39 (real `dotnet build`, zero `error CS`) |
| `verify_prop_pipeline.py` | 93/93 |
| `content_qa.py` | PASS — no IP/voice/schema violations |
| `csharp_string_qa.py` | PASS — no player-facing violations across 2124 string literals |
| `generate_scavenge_scene.py` | all gates, scene + nav grid regenerate **byte-identical** |

Scene went 412 → 423 GameObjects. 14 new C# files, ~4,000 lines. Three new generator gates, each with a negative control.

**But the brief's premise was substantially false, and that is the most useful thing in this report.** §2 is the important section.

---

## 2. Brief-versus-reality: what the spec asserted that was not true

I checked every type, member, file and tool the brief named *before* planning, because a prior session's notes recorded that briefs for this project carry invented names. That check paid for itself several times over.

### 2.1 The audio layer the brief's code called did not exist

The brief opened with: *"Phases 1-5 complete. Full game loop, 2 sites, audio, VFX, meta-progression, traits, localization, Steam achievements, options, CI/CD."*

Its ready-to-paste `CarbonCopyAnomaly` called `AudioManager.Play3D(AudioManager.CUE_UI_HOVER, …)` and its `BacklogAnomaly` called `AudioManager.SetTimeScale(0.5f)`.

**There was no `AudioManager` anywhere in the project.** No file matching `*Audio*.cs` under `Assets/`. Phase 3 was the audio phase and it had not shipped.

*(It landed mid-session — a parallel agent session completed and pushed the audio layer while I was working. See §7. But at the moment the brief was written and at the moment I started, it did not exist.)*

Even after it landed, `SetTimeScale` did not exist and **should not have**. See §3.1 — implementing the brief's sample code faithfully would have encoded a bug the brief's own prose warned against two paragraphs earlier.

### 2.2 There was one scavenge level, not two

The brief listed `tools/generate_census_scene.py` under **"Files to Modify"** for Tasks 1 and 2. That file has never existed. `Assets/Scenes/` contained exactly one gameplay level, `Scavenge.unity`.

Consequences the brief did not account for:
- Task 2's Drowned Census-Taker is specified by the bible to appear only in the Census District and the Reservoir. Neither had a level. The mutant had nowhere to walk.
- Task 3's "third scavenge site" was actually the *second*.

### 2.3 A live shipping bug the brief walked straight past

`ScavengeSiteCatalog.cs` declared:

```csharp
Id = "site_census_office",
IsBuilt = true,
SceneName = "CensusOffice",
```

with no such scene in the project or in Build Settings. This is not a harmless placeholder. `ScavengePhase3DState.ResolveSceneName()` falls back to the depot only when `SceneName` is **empty**:

```csharp
if (site == null || string.IsNullOrEmpty(site.SceneName)) return FallbackSceneName;
```

A non-empty name for a missing scene passes that guard. The player would select the site, the additive load would find nothing, and they would get sixty seconds of empty room with no error anywhere. Fixed in `8eeba24`.

### 2.4 Three API signatures in the brief's code would not compile

| Brief's code | Reality |
|---|---|
| `pickup.Quantity = original.Quantity;` | `public int Quantity => Mathf.Max(1, quantity)` — read-only computed property |
| `player.MoveSpeed *= f; player.SprintSpeed *= f;` | `private float walkSpeed` / `sprintSpeed` — private serialized fields, different names |
| `item_geiger_counter` | The shipped id is `item_kafedra_geiger_counter`. Coding against the brief's name yields a detector that never detects: `GameDatabase` logs the miss, returns null, and the anomaly concludes the player has no counter. |

### 2.5 `AnomalyData` / `MutantData` have schemas but zero instances

The brief: *"They have `AnomalyData` schemas. They have bestiary entries."* Both true. But `Assets/Data/Anomalies/` and `Assets/Data/Mutants/` are **empty directories**. `BESTIARY.md`'s own TODO section says exactly this. The brief presented the absence of *behaviour* as the only gap; the absence of *content instances* is a separate one, still open.

### 2.6 "must pass 39/39" is only accidentally right

The brief repeated *"`python tools/verify_steam_layer.py` passes 39/39"* as an acceptance criterion for every task. The script's counter is dynamic (`checks += 1`) and it **aborts early on failure** — my first failing run printed `26/29`. So 39 is what a full green run happens to total today, not a fixed contract. It rises whenever a check is added. Stating it as a number invites a future session to "fix" a legitimate 41/41 back down to 39.

### What GLM should take from §2

The pattern is consistent: **the brief's prose is broadly right about intent and unreliable about facts.** Its sample code is the least reliable part, precisely because it looks the most authoritative. Concretely, for the next brief:

1. **Do not write sample code that calls APIs you have not verified exist.** A named method in a code block is read as a contract. Prose like "play a spatial audio cue when the player enters" costs you nothing and cannot be wrong about a signature.
2. **Separate "the schema exists" from "instances exist" from "behaviour exists."** These were conflated for anomalies, mutants and sites, and each conflation hid real work.
3. **State acceptance criteria as properties, not as numbers.** "`verify_steam_layer.py` exits 0" is durable; "39/39" is a snapshot.
4. **A file listed under "Files to Modify" is an assertion that the file exists.** Two of them did not.

---

## 3. Design decisions where I deliberately departed from the brief

Dan's instruction was that I decide these myself and report them. Five are worth review.

### 3.1 The Backlog's audio drag deliberately exempts the alarm — the brief's API would not have

**The brief's code:** `AudioManager.SetTimeScale(0.5f); // pitch everything down`.

**The brief's own prose, in the same task:** *"Timer keeps running at normal speed. Stepping into Backlog with 30s left = forfeited run."*

The Backlog is a time-distortion anomaly: the player crawls at 2% speed while the emission countdown runs normally. That indifference **is** the trap. Pitching down "everything" includes the emission siren — which tells the player's ear that the deadline slowed too. That is the exact false conclusion that kills them, delivered by the sound design.

Shipped as `AudioManager.SetTemporalDrag(float)`: drags the ambient and music beds only, siren untouched.

**This is the single most useful pattern in this report.** The prose and the sample code disagreed, the prose was right, and the sample code was more specific and therefore more likely to be transcribed. When a spec names an API that must be created anyway, derive it from the prose.

Related, caught while implementing it: `SetMusicTranspose` already wrote `_musicSource.pitch` directly. A naive `SetTemporalDrag` writing the same field would mean leaving a Backlog resets pitch to a hardcoded 1 and silently discards a run-state transpose, permanently. Both are now ratios composed at a single write site.

### 3.2 No NavMesh. The generator exports its own navigation grid.

**The brief mandated** `NavMeshAgent`, and its Hard Rules said: *"Census Office and Reservoir scenes must have NavMesh baked (or the Census-Taker won't move). If the generator can't emit NavMesh data, document the manual bake step."*

I rejected both halves. Reasoning:

1. **A generator cannot emit NavMesh data.** It is a binary asset produced by an interactive Editor bake. The brief's own fallback — "document the manual bake step" — puts a human in the middle of a pipeline whose defining property (`CLAUDE.md` §14) is that it is headless and byte-deterministic.
2. **More importantly, it would create a second answer to the same question.** `generate_scavenge_scene.py` already runs a walkability flood-fill using the real `CharacterController` metrics (height 1.8, radius 0.35, step offset 0.32) against the real geometry, with a negative control proving it detects a sealed route. That flood-fill is what decides whether the level *ships*. A separately-baked NavMesh could quietly disagree with it — and the failure mode is a mutant walking somewhere the shipping gate says is impassable.

**Shipped:** the flood-fill's output is exported as `Assets/Data/Resources/Nav/navgrid_scavenge.bytes` — 209×145 cells at 0.5 m, heights in centimetres as `int16`, `-32768` for impassable. 60,638 bytes, byte-deterministic. `ScavengeNavGrid` parses it and A*s over it, four-connected to match the flood-fill exactly (eight-connectivity would cut corners the flood-fill treats as blocked — that is how an agent clips a doorframe).

Only cells **reachable from the player spawn** are marked passable. Walkable floor behind a sealed wall is walkable and useless; an agent pathing into it could never reach the player and would stand there looking broken.

The exported bytes are independently re-parsed and BFS'd by the generator to prove connectivity — testing the object rather than the blob would leave every encoding bug (endianness, stride, row-major order) to be discovered as a Census-Taker walking through a wall.

**Cost:** one manual step removed from the pipeline forever; the stalker and the shipping gate agree by construction. **The `.bytes` extension is load-bearing** — a `.bin` under `Resources` is not imported as a `TextAsset` and `Resources.Load<TextAsset>` returns null with no error. That trap is documented in this project's `docs/PROP_PIPELINE.md` and cost a prior session real time.

### 3.3 The Margin Note re-rolls forward, not backward

**The brief and the bible:** *"Re-roll one expedition event outcome per in-game week."* The natural reading is "reverse the last result and roll again."

That is not implementable honestly. An applied outcome has already killed crew members, clamped reputation against its bounds, consumed items that later effects stacked into, and advanced the RNG stream. Several of those are lossy. "Undo" would mean reconstructing an *approximation* of a prior state and presenting it as the real one — a save system that quietly lies, in a game whose entire save architecture is built around seed reproducibility.

**Shipped:** using a Margin Note arms the next resolution to draw twice and keep the better reading. Mechanically a re-roll of an outcome, applied prospectively. It also reads better in the game's register: a note in the margin is filed *before* a decision is reviewed.

Detail worth noting: the second draw is taken **unconditionally** once the artifact is spent, not only when the first draw failed. Otherwise the RNG stream advances by a different amount depending on the first result, and a seeded run stops being reproducible — the hardest class of bug to notice, because everything still works.

### 3.4 The Stamped Tongue is filed in advance, not offered mid-event

**The brief:** *"appears during Scale Society events as a 'USE OVERRIDE' button."*

**Shipped:** lodged in advance from the artifact register; the next Scale Society matter resolves in the player's favour. Three reasons: it keeps `EventModalUI` free of artifact-specific wiring; it fits the bureaucratic fiction better (an override is *on file* before the matter comes up); and it makes the artifact a planning decision rather than a reflex.

Two subtleties that a naive implementation gets wrong:
- It tests **both branches** of a choice for Scale Society involvement, not just the branch about to be applied. The question is whether the *matter* is a Society matter. Testing only the applied branch fires the override on some Society events and not others, for a reason no player could ever infer.
- It is consumed only when it actually **changes** something — checked after the roll, so a matter that succeeded on its own leaves the artifact on the register.

### 3.5 Expeditions queue an event; they do not resolve one

**The brief:** *"The expedition resolution uses the EventEngine to select events tagged with the chosen region."*

Implemented literally — calling `SelectNextEvent` then `Resolve` internally — this is wrong twice:

1. `SelectNextEvent` raises `EventPresented` **and** writes `RunData.pendingEventId`. An expedition resolving on the same tick as a bunker day would fight that day's own event for the modal and for that single field.
2. `Resolve(evt, choiceIndex, …)` requires picking a branch. Choosing on the player's behalf is the one thing the event system exists not to do.

**Shipped:** an expedition applies its own concrete outcomes (items, fatigue, radiation, hazards) and adds a region-tagged event id to `RunData.QueuedEventIds` — the engine's own supported handoff, which takes priority on the next selection. The player meets it through the ordinary day loop with the choice still theirs.

This is also what makes an in-flight piece of work from another session pay off: `oblastRegionsAny` had just been added to every event as a second gating axis. The expedition's destination matches against it, so **where you sent someone determines what story comes back with them.**

---

## 4. Bugs found and fixed that were not in scope

Three, all of the "no error anywhere" kind:

1. **`site_census_office` loaded nothing** (§2.3). A shipping bug reachable from the run-setup screen.
2. **Audio pitch double-writer** (§3.1). Would have destroyed a run-state music transpose permanently on leaving a Backlog.
3. **The scene generator's balance mirror was a comment, not a gate.** `generate_scavenge_scene.py` mirrored several `BalanceConstants` values in Python and serialized them into the scene, with a comment saying they must match. Nothing checked. A drifted mirror produces two internally-consistent halves that disagree — the range ring draws one radius while the raycast honours another, and it presents as "the reach feels wrong" months later. Now `assert_balance_mirror()`, with a negative control.

---

## 5. Engineering notes worth reusing

**Every new gate got a negative control**, per `CLAUDE.md` §14 ("a gate never observed failing is decoration"). This caught a real problem: my first attempt at a negative control for the spawn-point check *did not fire*, because the coordinate I picked as "obviously inside a silo" was actually open floor. Two further controls (a point inside a wall, a point off the map) both fired, with different diagnoses. **Without running the control I would have shipped a gate believing it worked.**

**Sub-file merge conflicts have no git-level solution.** Three agent sessions shared this working tree (§7). Several files carry interleaved work from more than one. I staged explicit paths only and never `git add -A`, but git has no sub-file granularity — a file containing two sessions' edits commits both. Worth designing briefs around: partition tasks by *file ownership*, not just by feature.

**A read-only computed property is a better error than a public field.** `ScavengePickup.Quantity` being `=> Mathf.Max(1, quantity)` is what surfaced the brief's assignment as a compile error at minute five rather than as a runtime surprise. I extended the pattern: `IsCopy` is exposed read-only with a one-way `MarkAsCarbonCopy()` method rather than a settable property, because clearing that flag downstream would launder every Carbon Copy duplicate into a genuine item and silently delete the mechanic.

---

## 6. What is NOT done — stated plainly

**Task 3 shipped as its data layer only.** What exists:

- `ScavengeSite` gained `CensusTakerCount`, `EditorSpawnChance` and `ThreatSummary`, so threat is a property of the **site** rather than re-decided by each scene generator. Per the bible, the depot fields no Census-Takers (wrong region), the Census Office one, the Reservoir two.
- `site_reservoir` is in the catalogue with its threat profile and its in-fiction unavailable reason.
- The `site_census_office` lie is fixed; both unbuilt sites are honestly `IsBuilt = false`.
- `MutantSpawner` reads all of it, so **building either level is a generator plus flipping one flag.**

**What is missing: `tools/generate_reservoir_scene.py` and `Assets/Scenes/Reservoir.unity`.** Also `generate_census_scene.py` and `CensusOffice.unity`, which the brief assumed already existed.

Why I stopped there: the existing depot generator is 1,900 lines with five validation gates (pickup ids, archetype mirror, script GUIDs, OBB burial/support, walkability flood-fill). A Reservoir adds water-depth movement modifiers, a catwalk over open water, and flooded tunnels — each needing its own gate, since none of the existing five reason about water. Producing that plus a second generator inside the remaining budget would have meant shipping scene generators I could not validate to the standard the other four tasks met. **Given a choice between five tasks at four-fifths quality and four at full quality plus an honest statement, I took the second.** Dan's call whether that was right.

**Also still open, and not in the brief's scope:**
- `AnomalyData` / `MutantData` `.asset` instances (§2.5). The behaviour exists; the data objects the bestiary UI would read from do not.
- **Nothing here has been seen in Play mode.** Unity was not driveable this session. Every system compiles, every gate passes, the scene loads its components — but the Census-Taker has never actually walked, the Interview screen has never actually faded in, and the Backlog haze has never been looked at. That is the single highest-value next action and it is one Play session.

---

## 7. Process note: three concurrent agent sessions on one working tree

Worth reporting because it shaped the work and will recur.

When I started, **two other Claude Code sessions were actively writing to the same working tree.** I detected it by stat-ing the exact "Files to Modify" list the brief provided: nine of seventeen had been written within six minutes, and two more were written *during* the check — one five seconds before my sampling command ran. Nine of those nine were files Phase 6 needed to modify.

I stopped and surfaced it rather than proceeding. One session then completed (shipping the audio layer this brief had assumed already existed); the other two hit credit limits mid-work, leaving ~1,000 modified files uncommitted, including a region-tag reconciliation across all 1,020 event JSONs.

Two things follow for future briefs:

1. **The "Files to Modify" list is a free contention check.** One `stat` over it converts an invisible race into a visible fact, and two samples separated in time distinguish "a batch finished" from "a session is writing right now."
2. **Sessions that die mid-work leave the tree in a state that compiles but is unattributable.** The audio layer arriving mid-session meant the brief's *false* premise became true partway through — which was lucky, and is not a plan.

---

## 8. Suggestions for the next brief

Ranked by expected value.

**1. Make Play-mode verification its own task, first.** Four systems' worth of behaviour is now unwitnessed. Everything else is speculation on top of that. This is worth more than any new feature.

**2. Write the two missing scene generators as one task, not as a side effect of two others.** The Census Office and the Reservoir are each a real level-design job — the depot took a dedicated session and has 1,900 lines of generator plus five gates. Budget them as such. The threat profiles and the nav-grid export already exist, so the *systems* work is done; what remains is geometry and water-specific validation gates.

**3. Author the `AnomalyData` / `MutantData` instances.** Five `.asset` files. Cheap, and they unblock a bestiary/codex screen, which is exactly the kind of thing an Early Access community engages with.

**4. Consider a codex screen as the next content feature.** The classification codes, hazard types, counter-tactics and expedition-log excerpts are all already written in `BESTIARY.md` and now all have working referents in code. This is one of the highest content-value-per-line features available, and the register suits it — a codex written as an internal hazard-reporting file rather than as a monster manual.

**5. Bunker-side visibility for the Carbon Copy.** Duplicates currently arrive as separate stacks with no UI difference — correct, since the player is not supposed to be able to tell. But there is currently no *moment* where the game tells them a defect fired: `EventResolution.defectiveItemUsed` and `defectSummary` are populated and nothing reads them. Wiring that into the event modal turns an invisible penalty into the bible's actual scene ("the crate on the table looks correct; the crate on the floor also looks correct"). Small job, large payoff.

**6. Partition tasks by file ownership when running sessions in parallel.** Phase 6's five tasks all touched `BalanceConstants.cs`, `EventEngine.cs` and `GameManager.cs`. That is fine for one session and unmergeable for three.

**7. Drop numeric acceptance criteria in favour of properties.** "`verify_steam_layer.py` exits 0" rather than "39/39".

---

## 9. Verification transcript

```
verify_steam_layer.py      39/39   ALL GREEN   (dotnet build, 0 error CS)
verify_prop_pipeline.py    93/93   ALL GREEN
content_qa.py              PASS    schema-valid, no IP / voice violations
csharp_string_qa.py        PASS    2124 literals, 131 files, no player-facing violations

generate_scavenge_scene.py
  database check       711 item ids, 3 crew ids, all 25 pickup ids resolve
  archetype check      tables match C# (30 rules, 12 shapes)
  script guid check    17 project scripts match their .meta
  balance mirror       7 constants match BalanceConstants.cs        [NEW]
  anomaly check        3 zones, none overlapping                    [NEW]
  placement check      all 25 pickups clear of geometry, supported
  reachability check   bunker + all 25 pickups reachable from spawn
  nav grid             209 x 145 @ 0.50 m, 23902 passable, 60638 B  [NEW]
  nav grid check       header round-trips; spawn and bunker passable;
                       bunker reachable by search over the exported bytes
  scene                423 GameObjects, byte-identical on regeneration
```

Negative controls run and observed to fire:

| Gate | Control | Fired |
|---|---|---|
| `assert_balance_mirror` | Geiger range 14 → 13 in the Python mirror only | yes, named the constant and both values |
| `verify_anomaly_zones` | Backlog moved onto the Carbon Copy volume | yes, named both zones |
| nav-grid spawn points | point inside the Z=+5 wall | yes — "not on a passable cell, walkable ground nearby" |
| nav-grid spawn points | point off the map footprint | yes — "inside geometry or cut off from spawn" |
| nav-grid spawn points | point at (-40, 25) intended as "inside a silo" | **no — the control was wrong, that spot is open floor** |

That last row is included deliberately: it is the reason the rule exists.

---

## 10. Files

**Created (14):**
```
Assets/_Project/Scripts/Core/ArtifactIds.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/AnomalyZone.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/CarbonCopyAnomaly.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/InterviewAnomaly.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/BacklogAnomaly.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/BacklogMotes.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/AnomalyAudioCue.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/ScavengeClockReadout.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/DefectiveItemEffects.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/ScavengeNavGrid.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/DrownedCensusTaker.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/TheEditor.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/MutantSpawner.cs
Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/RegistrationAffliction.cs
Assets/_Project/Scripts/OblastZero.Gameplay/ArtifactSystem.cs
Assets/_Project/Scripts/OblastZero.Gameplay/ExpeditionSystem/ExpeditionManager.cs
Assets/_Project/Scripts/UI/InterviewSequenceUI.cs
Assets/_Project/Scripts/UI/ScavengeHazardHUD.cs
Assets/_Project/Scripts/UI/ArtifactUseUI.cs
Assets/_Project/Scripts/UI/ExpeditionUI.cs
Assets/Data/Resources/Nav/navgrid_scavenge.bytes          (generated)
```

**Modified:** `RunData.cs`, `GameEvents.cs`, `BalanceConstants.cs`, `GameManager.cs`, `ScavengeSiteCatalog.cs`, `ScavengePhase3DState.cs`, `SurvivalPhase2DState.cs`, `AudioManager.cs`, `CrewManager.cs`, `EventEngine.cs`, `InventoryManager.cs`, `ScavengeController.cs`, `ScavengePickup.cs`, `ScavengePlayerController.cs`, `BunkerDayController.cs`, `BunkerHUD.cs`, `generate_scavenge_scene.py`, `scavenge_scene_lib.py`, `Scavenge.unity`, `CLAUDE.md`.

**Deliberately not created:** `tools/generate_reservoir_scene.py`, `Assets/Scenes/Reservoir.unity`, `tools/generate_census_scene.py`, `Assets/Scenes/CensusOffice.unity`. See §6.
