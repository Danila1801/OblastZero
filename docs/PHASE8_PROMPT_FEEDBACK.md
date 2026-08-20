# Deep feedback on `OPUS5_PHASE8_PLAYMODE_SHIP_PROMPT.md`

**Audience:** the GLM 5.2 aggregator in the Hermes agent that generates these phase prompts.
**Written by:** the executing agent (Claude Opus 5, Claude Code session), 2026-08-10, *before* executing the
prompt — the verification pass came first this time, which is why several of these are still cheap to fix.
**Repo:** `C:\Users\danil\projects\OblastZero`, branch `feat/scavenge-3d-scene` @ `eb5a805`.
**Purpose:** improve the *next* generated prompt. The three tasks are the right three tasks. Every finding
below is about **stated premises and routing**, not about scope.

**Standalone note:** this document assumes no prior conversation. Every claim below carries the command that
produced it, so any model or human can re-run the check.

---

## 0. Headline

This is a materially better prompt than Phase 5's. The status table is *mostly true* — I ran every gate it
cites and they are all green, at the counts claimed. The scope is correctly reduced to three items. The
closing recommendation ("play-mode verification is the only thing that matters now") is the right call.

Three things are wrong, and they are wrong in descending order of cost:

1. **The prompt's single most consequential sentence is false.** It marks play-mode verification "THE #1
   BLOCKER (HUMAN REQUIRED)" and states in bold: *"This cannot be done by the agent."* A 253-line report
   **committed in this repository** documents the agent doing exactly that on 5 Aug — boot to wipe, six
   defects found and fixed, one commit each. The prompt's own prerequisite list is visibly derived from that
   report's environment notes, so the evidence was read and its conclusion inverted.

2. **Task 2's commit plan would push a branch that does not compile.** It enumerates 1,020 JSONs + 15 C#
   files + 22 materials. It never mentions the **four untracked `.cs` files** — 820 lines, referenced by 20
   other files including `GameManager.cs` — that the working tree currently depends on. Executed literally,
   with the mandated explicit-path staging, the pushed branch is missing the files that make it build.

3. **Two of the six human prerequisites are already done, and two more are agent work.** Only two genuinely
   need Danil. The prompt front-loads four blockers that aren't blockers, in front of the task it called #1.

Underneath all three is one pattern, unchanged from the Phase 5 feedback: **the prompt asserts state instead
of marking it as unverified.** Phase 5's version of this was false positives ("X exists" when it didn't).
Phase 8's is worse in kind: a false *negative* ("the agent cannot do this"), because a wrong assertion fails
loudly on first contact while a wrong negation fails silently forever — the work simply never gets attempted.

---

## 1. What the prompt got right — verified, not assumed

I ran every gate the status table cites. Reproduce with the commands in §6.

| Prompt claim | Verified result | Verdict |
|---|---|---|
| `verify_steam_layer.py` 39/39 | **39/39 ALL GREEN** | ✅ |
| `verify_prop_pipeline.py` 93/93 | **93/93 ALL GREEN** | ✅ |
| `verify_procedural_sfx.py` 32/32 | **32/32 ALL GREEN** | ✅ |
| `content_qa.py` PASS | **PASS**, self-test green | ✅ |
| `csharp_string_qa.py` PASS | **PASS** over 143 files / 2,502 literals, self-test green | ✅ |
| `localization_qa.py` PASS | **PASS**, self-test **10/10** | ✅ |
| `migrate_event_tags.py --check` PASS | **PASS** — 1,020 events, 0 needing update | ✅ |
| 91 hardcoded literals across 5 screens | **91 exactly** (30/30/17/14/1), all 5 screens confirmed at 0 `LocalizedStrings.Get` calls | ✅ |
| 221 EN + 221 RU keys | **221 / 221** (219 display + `_comment` + `_voice`) | ✅ |
| 190 key constants in `UIStringKeys.cs` | **190** | ✅ |
| 1,020 events | **1,020** `.json` files | ✅ |
| 21 achievements + 15 stats | **21** `achv*` fields on `SteamConfig.cs`; **15** `stat_*` ids in `docs/STEAM_STATS.md`, all present in C# | ✅ |
| 3 scavenge sites in Build Settings | `_Bootstrap`, `Bunker`, `Scavenge`, `CensusOffice`, `Reservoir` | ✅ |
| Balance: victory reachable | `balance_analysis.py` runs, paths healthy (e.g. Kafedra 72.5%) | ✅ |
| Event JSON diff is the tag migration | Diff is **`oblastRegionsAny` insertions + trailing-newline fix only** — no prose, weight, or choice changes | ✅ |
| CI pipeline running | `.github/workflows/ci.yml` + `unity-ci.yml` present | ✅ |

**This table is the good news and it should be said plainly: the prompt's factual accuracy is up sharply
from Phase 5.** The problems below are concentrated in the two places the prompt *didn't* check — git's
index, and its own prior report.

---

## 2. Finding 1 (load-bearing): "This cannot be done by the agent" is false, and it's the wrong sentence to get wrong

### What the prompt says

> ### 3. **Full Play-Mode Verification — THE #1 BLOCKER (HUMAN REQUIRED)**
> **This cannot be done by the agent.** The Unity Editor must be driven live.

and, quoting Phase 7:

> *"...a loop that has never once been run from boot to wipe."*

### What is in the repository

`PLAY_LOOP_VERIFICATION.md`, 253 lines, committed at `HEAD`, dated 5 Aug 2026:

> **Headline:** the loop now runs end to end with **zero console errors and zero warnings**. Six defects were
> found and fixed, one per commit.

Its §1 table records: boot + data layer, MainMenu, RunSetup, ScavengePhase3D, pickup + carry cap, bunker
trigger, TransitionCutscene, SurvivalPhase2D **15 consecutive days**, wipe → RunFailed at day 16, return to
MainMenu, **all four `RunVictory_*` states**. Driven through the real UI and EventBus paths
(`Button.onClick.Invoke()`, `EndDayRequestedEvent`, `EventChoiceSelectedEvent`) rather than by calling game
logic directly. Six commits are named in it, and all six are in this branch's history: `109f1ad` (region
tags so Phase-2 events fire at all), `c7689a6` (report an empty event pool as an error), `d21b383` (END DAY
tofu box), `cbe0f61` (Bootstrap missing from `_Bootstrap.unity`, so Steam never initialised), `31cba47` (all
four victory states could not display), `573e4d1` (IP firewall violation in shipping prose).

The most severe of those six — `EventEngine` returning `null` for every day of every run, taking all 1,020
events offline — is exactly the class of defect no structural gate can see, which is the argument the prompt
itself makes for why play mode matters. **The agent found it, in play mode, by driving the editor.**

### Why this specific error is expensive

Verifying a positive claim is cheap to skip because it fails loudly: call the API, get an error, fix the
prompt. Verifying a *negation* is expensive to skip because it never fails at all. "Human required" routes
the work out of the agent's queue and into Danil's. Nothing downstream reports that it wasn't attempted. The
project stalls with every gate green — which is precisely the failure mode the prompt's own closing
paragraph warns about.

And the check was two commands: `git log --oneline` + `ls *.md` would have surfaced the report; `git show
HEAD:PLAY_LOOP_VERIFICATION.md` reads it.

### Corroborating detail: the prompt read the report and inverted it

Prerequisites 1 and 5 — "switch Transport off HTTP Local → stdio" and "set `Application.runInBackground =
true` at runtime" — are **both documented in that same report**, in a section titled *"Environment note (not
a game defect)"*:

> The Unity MCP bridge was undiscoverable: the Editor plugin was set to **HTTP Local (port 8090)** while the
> MCP servers run `--transport stdio`. [...] Also: **`Application.runInBackground` is false**, so Play mode
> freezes whenever the Editor loses focus.

So the report was in context. Its environment notes were carried forward into the prerequisites. Its
headline finding — that an agent drove this loop end to end — was not.

### What is actually true about the capability boundary

Measured this session: `mcp__unityMCP__read_console` returns
`"No Unity Editor instances found. Please ensure Unity is running with MCP for Unity bridge."` So:

| Step | Who |
|---|---|
| Launch Unity Editor | **Danil** — cannot be done headless from the agent |
| Flip the MCP transport off `HTTP Local` in the Unity GUI | **Danil** (one-time; can be persisted, see §4) |
| Everything after the bridge connects — boot, drive UI, read console, assert state, fix, commit | **Agent** — demonstrated 5 Aug |

That three-row table is what the prompt should have contained. It is a *smaller* ask of Danil than the
current six-item prerequisite list, and it does not misroute the project's top-priority task.

---

## 3. Finding 2: Task 2's commit plan pushes a branch that does not compile

### What the prompt says

> **Files to check/commit:**
> ```
> 1,020 files: Assets/Data/Resources/Events/*.json (region tag migration)
> 15 files:    Assets/_Project/Scripts/**/*.cs (UI + Core + Gameplay changes)
> 22 files:    Assets/Art/Materials/Scavenge/*.mat (harmless churn)
> ```
> 4. If the changes are sound: **stage explicit paths only** (NEVER `git add -A` — concurrent sessions)

### What `git status --short` actually shows

1,109 entries: 1,060 modified, 7 **deleted**, 42 **untracked**. The prompt accounts for the modified ones
and nothing else. Four of the untracked entries are source files that have **never been committed**:

| Untracked source file | Lines | Referenced by |
|---|---|---|
| `Assets/_Project/Scripts/Core/MetaUnlockCatalog.cs` | 236 | 8 files, incl. `GameManager.cs`, `MainMenuState.cs`, `ScavengeSiteCatalog.cs` |
| `Assets/_Project/Scripts/UI/MetaUnlockUI.cs` | 260 | 2 files, incl. `MainMenuState.cs` |
| `Assets/_Project/Scripts/OblastZero.Gameplay/TraitEffects.cs` | 176 | 4 files, incl. `GameManager.cs`, `CrewManager.cs`, `RunSetupUI.cs` |
| `Assets/_Project/Scripts/Core/OblastRegions.cs` | 148 | 6 files, incl. `EventEngine.cs`, `ExpeditionManager.cs`, `ExpeditionUI.cs` |

Verify:

```bash
git ls-files --error-unmatch Assets/_Project/Scripts/Core/MetaUnlockCatalog.cs
```

These are not incidental. They implement four systems the prompt's own status table marks **✅ Complete**:
meta-progression, the meta-unlock UI, crew traits, and the region taxonomy. `OblastRegions.cs` is also the
type the 1,020-file event migration in the same task writes tags *for* — so Task 2 as written commits the
data and leaves its schema untracked.

### Why the passing gate did not catch this

`verify_steam_layer.py` runs a real `dotnet build` and reports 39/39 with zero error CS lines. It is not
wrong. It compiles **the working tree** — the filesystem, which has all four files. Git tracks the **index**,
which has none of them. When a file exists in one and not the other, every local gate is green while the
published artifact is broken, and the discrepancy is invisible precisely to the author, whose working tree is
the complete one. It surfaces on someone else's clone, or on CI, after the session has ended.

This generalises: **a compile gate answers "does this machine's disk build?", not "does the repository
build?"** Any status claim consumed by someone other than its author has to be checked against the committed
tree.

Note the interaction with the (correct, and important) `NEVER git add -A` rule: explicit-path staging is
exactly the mechanism that omits *new* files, because new files never appear on a list of changes. The rule
needs a companion — enumerate untracked source paths deliberately — or it quietly guarantees this bug.

### Also unaccounted for: 7 tracked deletions

```
 D OPUS5_MASTER_CODE_PROMPT.md      D OPUS5_PHASE2_MASTER_PROMPT.md
 D OPUS5_SCAVENGE_PROMPT.md         D OPUS5_UI_WEIGHT_PROMPT.md
 D PLAYMODE_CHECKLIST.md            D PLAY_LOOP_VERIFICATION.md
 D PROJECT_STATE_REPORT.md
```

These are staged-for-deletion in the working tree and the prompt does not mention them. Committing Task 2 with
`git status` "clean" as the acceptance criterion means accepting these deletions — including
**`PLAY_LOOP_VERIFICATION.md`, the very report that refutes Task 3's premise**, and `PROJECT_STATE_REPORT.md`
(606 lines). There is an untracked `docs/archive_prompts/` directory that looks like the intended destination,
which suggests a move that was never completed. Either way it is a decision, and the prompt should have
surfaced it rather than letting it ride along inside "commit the uncommitted tree."

### Minor count drift in the same section

| Prompt | Actual |
|---|---|
| "15 C# files with ~700 lines" | **14** `.cs` files, 648 insertions / 114 deletions. The 15th non-JSON, non-material modified file is `Assets/Art/Resources/Props/prop_manifest.json` |
| "22 material files" | **25** `.mat` |

Small, but they are the numbers an executing agent uses to confirm it staged the right set.

---

## 4. Finding 3: four of six "prerequisites (Danil must do in Unity)" are not Danil's, and two are already done

| # | Prompt's prerequisite | Reality |
|---|---|---|
| 1 | Switch MCP transport off `HTTP Local` → stdio | **Real, and genuinely human** (Unity GUI). Note the 5 Aug report records a persistent form: set `MCPForUnity.UseHttpTransport = 0` and relaunch via `McpCiBoot.StartStdioForCi`. Worth stating so it stops costing 20 minutes a session |
| 2 | Launch Unity Editor | **Real, and genuinely human** |
| 3 | Create `SteamConfig` asset, set real App ID | **Already done.** `Assets/Data/Resources/SteamConfig.asset` exists with `appId: 480`, and `verify_steam_layer.py` check 5 asserts the asset's `m_Script` guid matches the script and that it sits under a `Resources/` folder. Also **self-contradictory**: this line demands a "real App ID" while the run-end checklist later says *"with App ID 480 placeholder."* 480 (Spacewar) is correct until a Steamworks app exists |
| 4 | Assign `SteamConfig` to `Bootstrap.steamConfig` in `_Bootstrap.unity` | **Unnecessary — no-op.** `Bootstrap.cs:104` reads `steamConfig != null ? steamConfig : Resources.Load<SteamConfig>("SteamConfig")`, and the field's own tooltip says *"Optional explicit SteamConfig. When empty, Bootstrap loads Resources/SteamConfig."* The asset is already in a `Resources/` folder |
| 5 | Set `Application.runInBackground = true` **at runtime** | **Real need, wrong mechanism, wrong owner.** `ProjectSettings/ProjectSettings.asset:85` reads `runInBackground: 0`. Setting it "at runtime" does not survive a domain reload, which is when it matters most. The durable fix is the project setting — a one-line change to a tracked file, committable, and **agent work** |
| 6 | Delete `Assets/Scenes/SampleScene.unity` | **Real** (the file exists, dated Jan 2025, absent from Build Settings) — but it is a tracked file, so this is `git rm` + commit: **agent work**, not a Unity GUI task |

Net: the human prerequisite list is **two items long**, not six. Four items of ceremony sat in front of the
task the prompt itself called the #1 blocker.

The reason this matters beyond tidiness: a six-item manual checklist reads as a heavy ask, and heavy asks
get deferred. Shrinking it to "launch Unity, flip the transport" is the difference between the #1 blocker
happening this week and not.

---

## 5. Finding 4: two smaller premise errors

### 5.1 Wrong path for `OblastUIAudio.cs`

> - `Assets/_Project/Scripts/OblastZero.Gameplay/OblastUIAudio.cs` — check if the 1 literal is player-facing

The file is at **`Assets/_Project/Scripts/UI/OblastUIAudio.cs`**. Nothing exists at the stated path. This is
the same failure mode as Phase 5's fabricated file paths and Observation 1 in the shared log ("briefing
feature names contradicted the codebase's own enum") — a plausible path inferred from a plausible namespace,
never `ls`-ed.

It is self-refuting inside the prompt, too: `localization_qa.py` is cited two paragraphs earlier as the
authority on these five screens, and that tool only scans `Assets/_Project/Scripts/UI/`. If the file were
where the prompt says, the tool could not have counted it.

### 5.2 The localization coverage gate is narrower than the prompt describes

> `localization_qa.py` has a coverage gate that catches new unlocalized screens

Scoped precisely: `tools/localization_qa.py` does `glob.glob(os.path.join(UI_DIR, "*.cs"))` where
`UI_DIR = Assets/_Project/Scripts/UI`. **Non-recursive, one directory.** A new unlocalized screen in a
subfolder, or under `OblastZero.Gameplay/` — which is where this very prompt believed one of the five screens
lived — is invisible to it, and the gate's silence about it is indistinguishable from a clean result.

There are real candidates outside the scan root today (`EmissionVfxController.cs`, `ScavengePlayerController.cs`,
`ArtifactSystem.cs`, `VictoryConditionEvaluator.cs` all carry capitalised prose literals and never call
`LocalizedStrings`), though whether any reach a TMP text needs a per-file read before claiming it as debt.

Its `UNLOCALIZED_DEBT` table is also keyed on **bare filenames**, so a screen moved out of `UI/` silently
drops out of the count with no number changing.

None of this makes the gate bad — its header comments are unusually honest about what key-parity checks
structurally cannot see, and it is the reason the 91-literal debt is known at all. The issue is the prompt
generalising a directory-scoped gate into a codebase-wide guarantee. **Report a gate's coverage alongside its
verdict**; "PASS" over an unstated scope can't be falsified.

---

## 6. What Task 3's checklist should have been

The current checklist is written as if nothing has ever been verified. Roughly half of it was verified on
5 Aug and is regression-checking; the other half is genuinely new and is where the risk is. Splitting it
tells the executing agent where to spend its attention.

**Already verified 5 Aug (re-run as regression, fast):** boot + MainMenu, RunSetup with 3 sites and crew
stats, Grain Depot scavenge incl. carry-weight enforcement and refusal notice, bunker entrance →
TransitionCutscene, bunker phase 15 consecutive days with autosave, wipe → RunFailed summary, return to
MainMenu, all 4 `RunVictory_*` states, Steam stats incrementing on App ID 480.

**Never verified — the actual Phase 8 scope:**

- **Census Office** end to end — flooded basement, 0.6× water slow, Interview anomaly room, Carbon Copy
  duplication, Drowned Census-Taker nav, all 25 pickups reachable, vault door exit
- **Reservoir** end to end — three crossings, 0.5× basin slow, Backlog anomaly composing with water (this is
  the composable-speed system's only real test), 2 mutants, The Editor's 15% appearance, control room exit
- **Expeditions** — DISPATCH → assign crew → resolve after N days
- **Artifacts** — ARTIFACTS → use each of the 4
- **Options / Pause / gamepad navigation / first-run tooltips** — never driven
- **Russian rendering across all screens**, including the 5 newly localized ones, and runtime language switch
- **Emission VFX thresholds** at 15s / 5s
- **Meta-unlock UI** — 10 unlocks, salvage token spend
- **Reputation movement visible on all 3 factions** after event choices

That is a genuinely large surface, and framing it as the delta makes it look like what it is: a real day of
work, not a re-run of a thing that already happened.

---

## 7. The pattern, and the one change to make

Phase 5's feedback asked for one change: *stop asserting state you have not verified, and mark the
difference.* Phase 8 shows real movement — the status table is accurate, and the gate counts are right,
because those were checkable by running a command and the prompt's author (or its source report) ran them.

The residue is everything **not** covered by running a gate:

- `git status` — never consulted, hence Findings 2 and the 7 deletions
- prior reports in the same repo — read for environment notes, not for conclusions, hence Finding 1
- a single `ls` on an asserted path — hence Finding 4.1
- a gate's own scope constants — hence Finding 4.2

Concretely, for Phase 9, three habits:

1. **Extract the negations, not just the assertions.** Anything phrased "cannot", "requires a human",
   "blocked on", "only X can" routes work away from the agent, so it deserves *more* verification than a
   positive claim, not less. Search the repo for prior art doing that thing, and probe the tool path
   directly, before writing "HUMAN REQUIRED".
2. **Run `git status --short` before writing any section about repository state**, and account for all three
   categories — modified, **deleted**, **untracked**. A commit plan built only from modified files is a
   commit plan that omits every new file in the change.
3. **Distinguish `[verified: <command>]` from `[believed]` per claim.** The Phase 5 feedback asked for this
   and it would have caught every finding above. It costs one clause. A prompt that says *"I believe
   `OblastUIAudio.cs` is under Gameplay/ — verify"* costs the executing agent one `ls`. A prompt that asserts
   it costs a wrong-folder search and a loss of trust in the other 40 assertions on the page.

And one structural suggestion: the closing quote — *"None of them can tell you the game is fun"* — is the
best sentence in the prompt and it is correct. It is also the reason Finding 1 hurts. The prompt correctly
identified that play-mode verification is the only thing that matters, then routed it to the one participant
who has to be asleep-cycle-scheduled, on the strength of a claim its own repository disproves.

---

## 8. Reproduction — every check in this document

```bash
cd ~/projects/OblastZero

# Gates (all green as of 2026-08-10, eb5a805)
python tools/verify_steam_layer.py            # 39/39
python tools/verify_prop_pipeline.py          # 93/93
python tools/verify_procedural_sfx.py         # 32/32
python tools/content_qa.py                    # PASS   (--self-test also passes)
python tools/csharp_string_qa.py              # PASS   (--self-test also passes)
python tools/localization_qa.py               # PASS, 91 literals / 5 screens (--self-test 10/10)
python tools/migrate_event_tags.py --check    # PASS, 1020 events, 0 needing update

# Finding 1 — the play-mode report that already exists
git show HEAD:PLAY_LOOP_VERIFICATION.md | head -60

# Finding 2 — untracked source the commit plan omits
git status --short | grep '^??' | grep '\.cs$'
git status --short | grep '^ D'

# Finding 3 — prerequisites already satisfied
cat Assets/Data/Resources/SteamConfig.asset | head -20        # appId: 480, exists
grep -n 'steamConfig' Assets/_Project/Scripts/Core/Bootstrap.cs
grep -n 'runInBackground' ProjectSettings/ProjectSettings.asset

# Finding 4 — the wrong path, and the gate's real scope
ls Assets/_Project/Scripts/UI/OblastUIAudio.cs
grep -n 'UI_DIR\|glob.glob' tools/localization_qa.py
```

---

## 9. Verdict

**Keep:** the three-task scope, the execution order, the explicit-path staging rule, the after-each-task gate
list, the hard-rules section, and the closing recommendation. Those are all correct and the prompt is
materially stronger than Phase 5's.

**Fix before the next one:** consult `git status` and the repo's own prior reports with the same rigour
already applied to the gates, and verify claims about what *cannot* be done at least as hard as claims about
what exists.

**Immediate consequence for Phase 8 execution:** Task 2's file list must gain the four untracked `.cs` files
(plus their `.meta` sidecars) and an explicit decision on the seven deleted docs, or the resulting push is
broken for every other clone. Task 3's prerequisite list can be cut from six items to two, and its checklist
rewritten as a delta against the 5 Aug baseline.
