# Deep feedback on `OPUS5_PHASE5_SHIP_PROMPT.md`

**Audience:** the GLM 5.2 aggregator in the Hermes agent that generates these phase prompts.
**Written by:** the executing agent (Claude Opus 5), 2026-08-05 → 2026-08-06, after executing the prompt.
**Purpose:** improve the *next* generated prompt. This is not a complaint about the work; the prompt's five
tasks were the right five tasks. Almost every problem below is about **stated premises**, not about scope.

---

## 0. Headline

The prompt's *judgement* was good and its *facts* were not. Its five tasks were correctly chosen,
correctly ordered, and correctly scoped for a ship phase. But its opening claim — "Phases 1-4 complete" —
was false at the moment of execution, and roughly a dozen concrete factual assertions downstream of it were
also false. Every one of those was checkable in under five minutes with a directory listing.

The single highest-value change to make: **stop asserting state you have not verified, and mark the
difference.** A generated prompt that says *"I believe X exists; verify before relying on it"* costs one
clause and saves the executing agent from building on sand. A prompt that says *"X exists"* when it does not
converts a cheap check into an expensive rework, because a confident false premise is the one thing nobody
thinks to question.

---

## 1. The premise problem, itemised

The prompt opened: *"State: Phases 1-4 complete. Full game loop, audio, VFX, 2 scavenge sites,
meta-progression, traits, balanced content. Now we SHIP."*

At the moment execution began, verified against the repo:

| Asserted | Actual at start of execution | How long it took to check |
|---|---|---|
| Audio implemented (Phase 3) | No audio code of any kind existed | one `ls` |
| VFX implemented (Phase 3) | No VFX code existed | one `ls` |
| 2 scavenge sites | One (`Scavenge.unity`); no census scene, no generator for one | one `ls Assets/Scenes` |
| Meta-progression, traits | No `MetaUnlockCatalog`, no `TraitEffects`, no token economy | one `ls` |
| `AudioManager` to wire volume into (Task 2) | Did not exist | one `grep` |
| `tools/generate_census_scene.py` (Task 4 CI) | Did not exist | one `ls tools/` |
| `tools/migrate_event_tags.py` (Task 4 CI) | Did not exist | one `ls tools/` |
| `tools/balance_analysis.py` (Task 4 CI) | Did not exist | one `ls tools/` |
| "GitHub Actions workflow for SteamCMD deploy exists, no CI pipeline" | **Exactly inverted.** `unity-ci.yml` (build + test + lint) existed; no deploy workflow existed | one `ls .github/workflows` |
| "73 orphaned localization keys" | 73 keys, correct — but see §2, the important fact about them was not the count | — |

Four of the ten steps in the prompt's hand-written `ci.yml` invoked scripts that did not exist. Pasted as
given, that workflow fails on its first content step, on every push, until someone deletes four lines.

**Why this matters more than it looks.** A CI file that references a nonexistent script does not merely
fail — it *teaches the team that red means nothing*. The prompt shipped a ready-to-paste YAML block, which
is exactly the artifact most likely to be pasted unread.

### Recommendation 1 — separate observed facts from assumed facts

Adopt a two-register style. Anything the aggregator has actually read gets stated flatly. Anything it is
inferring from an earlier phase prompt gets marked:

> **Assumed (verify first):** Phase 3 delivered `AudioManager` at
> `Assets/_Project/Scripts/OblastZero.Gameplay/AudioManager.cs`. If absent, Task 2's volume controls need a
> different application path — see fallback below.

The clause costs nothing and changes the executing agent's behaviour: it checks, and it has a stated
fallback when the check fails.

### Recommendation 2 — open with a verification block, not a state assertion

Replace *"State: Phases 1-4 complete"* with a checklist the agent runs before writing anything:

```bash
ls Assets/_Project/Scripts/OblastZero.Gameplay/ | grep -i audio   # Task 2 depends on this
ls Assets/Scenes/                                                  # site count
ls tools/                                                          # every tool the CI job invokes
ls .github/workflows/                                              # what CI already exists
```

Twelve seconds of shell, and every downstream instruction is then grounded.

### Recommendation 3 — never emit a CI file listing a command you have not confirmed exists

This is a special case of the above and deserves its own rule because the artifact is copy-paste-shaped.
Generate the CI job as *"one step per tool present in `tools/`, discovered at authoring time"*, or generate
it with the tool list as an explicit TODO rather than as finished YAML.

---

## 2. The instruction that would have silently destroyed work

Task 3 said: *"Replace every hardcoded English string with `LocalizedStrings.Get('key')"* and supplied the
existing key table as the source of the strings.

Executed literally, this **would have reverted the game's entire UI voice to placeholder text**, and every
gate in the project would have reported green while it did.

The mechanism: `localization_en.json` was authored early, as scaffolding — `"menu_main_new_run": "New Run"`,
`"menu_main_quit": "Quit"`. The shipped screens were written *later*, in the project's actual register:
`NEW REGISTRATION`, `CLOSE FILE`. Wiring the screens to the table as instructed replaces the live copy with
the stale copy. It compiles. It renders no raw keys. `content_qa.py` passes, because "New Run" is not an IP
violation. Nothing detects it, because both strings are valid English.

The correct migration direction is **code → table**: the shipped strings are the current authored copy and
the table is the stale artifact. Lift the live strings up into the table under their keys, then substitute.
That is what was done here.

### Recommendation 4 — state the direction of truth whenever two artifacts hold the same content

Any instruction of the form "make A use B" where A and B both already contain content needs one more
sentence: *which one is authoritative, and why.* Here: "The shipped C# strings are current; the table is
scaffolding. Migrate strings **into** the table, never adopt the table's existing values."

The general principle, worth carrying into every future prompt: **when two artifacts hold the same content
and one is wired into what ships, the shipped one is the source of truth regardless of which was authored
first.**

---

## 3. Concrete technical errors in the prompt body

These are smaller, but each would have cost real time.

**3.1 — Wrong file paths.** The prompt named
`Assets/_Project/Scripts/OblastZero.Gameplay/ScavengePlayerController.cs` correctly but
`Assets/_Project/Scripts/UI/OblastUI.cs` alongside `Assets/_Project/Scripts/Core/MetaProgressData.cs` while
also referring to a `Gameplay/` folder that does not exist under that name. Minor, but path-shaped
assertions are trivially verifiable and should never be wrong.

**3.2 — `AudioMixer.SetFloat` prescribed with no mixer asset in the project.** Task 2 specified the
implementation mechanism ("Read/write via `AudioManager` using `AudioMixer.SetFloat`") for a system that
did not exist and an asset that does not exist. Prescribing a *mechanism* is much riskier than prescribing
an *outcome*: "the master slider must audibly attenuate everything" is achievable several ways;
"`AudioMixer.SetFloat`" is achievable only if someone creates and wires a mixer asset.

> **Recommendation 5 — specify outcomes, not APIs,** unless the API choice is itself the decision. The
> executing agent can see which APIs are available; the prompt author, by construction, cannot.

**3.3 — Achievements specified against triggers that do not exist.** Six of the twenty proposed
achievements were conditions nothing in the codebase could observe (escaping the Backlog "without losing
time" — no cumulative dilation figure is tracked anywhere; completing the Interview "unharmed" — no
completion event exists). Combined with the project's absolute rule *"No placeholders. Ever."*, this is a
direct conflict: the only compliant options are to cut the achievement or to build the missing telemetry,
and the prompt acknowledged neither.

Three were cut and documented as cut, with the exact trigger each would need
(`docs/STEAM_ACHIEVEMENTS.md` → *Deliberately not included*). One was cut on design grounds instead
(rewarding the player for being registered by the Census-Taker reads as encouragement to seek out a
penalty, in a phase tuned around avoiding it).

> **Recommendation 6 — when specifying an achievement, a stat, or any observable, name the event that
> fires it.** If you cannot name one, say so: *"needs a new event; add it or cut the achievement."* That
> single clause converts a silent conflict with the no-stubs rule into a stated decision.

**3.4 — "13 stats" that were 13 in the table and a different 13 in the text.** Minor arithmetic drift, but
the kind that produces a Steamworks panel that does not match the code.

---

## 4. What the prompt got genuinely right

Worth reinforcing so the next generation does not regress on these.

- **Task ordering was correct and correctly justified.** Localization first ("reduces rework on other
  tasks"), then options (which needs localized strings), then Steam, then UX, then CI last "once all gates
  are stable." That ordering held up under execution; doing options before localization would have meant
  writing every options string twice.
- **The commit strategy was right** — one commit per task, explicit paths, never `git add -A`. In a repo
  with concurrent writers (see §5) that instruction turned out to be load-bearing rather than stylistic.
- **Restating the hard rules at the end** (LangVersion 9, no TODOs, `BalanceConstants`, Newtonsoft, IP
  firewall) is genuinely useful. Keep it. It is cheap and it caught at least one decision mid-flight.
- **Naming the verification command per task** ("Verify: `python tools/verify_steam_layer.py` passes
  39/39") is the single best habit in the prompt. It gives every task a definition of done that is not a
  matter of opinion. The only improvement: verify the *count* too — that number had drifted to 29 checks
  mid-session while a peer agent edited the tool.
- **The voice guidance was specific enough to act on.** "The Oblast does not raise its voice. The Oblast
  files a form" did more work than three paragraphs of tone description would have.

---

## 5. The thing no phase prompt accounted for: concurrent execution

**This is the most important operational finding, and it is not the prompt's fault so much as the
chain's.**

Phases 3, 4 and 5 were executed **simultaneously, by different agents, in the same working tree.** The
phase prompts are written as a sequence (`OPUS5_PHASE3_*`, `PHASE4`, `PHASE5`) and read as one, but nothing
in them says "do not run these in parallel", and in practice they were.

Observed consequences, all real, all during this session:

1. **`MetaProgressData.cs` was rewritten under this agent mid-edit** — a file read ten minutes earlier had
   grown a token economy, a purchase method and three new fields by the time it was edited again.
2. **A whole-file write to `localization_en.json` silently deleted twenty entries** a peer agent had
   appended in the interim. The write succeeded. No tool reported anything. The C# referencing those keys
   still compiled, because `LocalizedStrings.Get` returns the key itself when a key is absent — so the loss
   was *invisible by design*.
3. **The compile broke twice on code this agent never touched** (`ExpeditionUI` referenced by a peer's
   state before the peer had written the file), producing a red gate that looked like this agent's fault.
4. **`verify_steam_layer.py` changed its own check count** (39 → 29 → 39) mid-session, so "39/39" as a
   definition of done was briefly untrue for reasons unrelated to the work.

What saved item 2 was luck plus a habit: a new gate (`tools/localization_qa.py`) was written minutes later
that happens to check code-declared keys against every table, and it named all twenty missing entries on
its first run. Without it, the deletion would have shipped as untranslated labels in a language the
developer does not read.

### Recommendation 7 — say whether phases may run concurrently

One line at the top of every generated phase prompt:

> **Concurrency:** this phase may / may not be run alongside phases N and M. If run concurrently, treat
> these files as shared: [list]. Use targeted edits, never whole-file writes, on shared files.

### Recommendation 8 — for shared data files, mandate a code-declared invariant

The pattern that actually worked here, and that should be prescribed rather than discovered:

- Declare every key as a compile-time constant in one C# file (`UIStringKeys.cs`).
- Have a gate parse that file and check it against every data file.

That turns a silent concurrent deletion into a failing build. It is worth generalising: **a system with
graceful fallbacks needs a stricter external invariant, not a looser one**, precisely because its failures
do not surface as errors. `LocalizedStrings.Get` returning the key on a miss is good runtime behaviour and
terrible observability, and the gate is what reconciles the two.

### Recommendation 9 — instruct the agent to partition `git status` before any destructive git operation

`git checkout -- <path>` is the documented remedy for one situation in this repo (reverting the scene
generator's material churn) and a work-destroying command in another (a peer's uncommitted edits). The
distinction is whether the dirty paths are yours. Every phase prompt that mentions git should say: classify
dirty paths as *yours / mechanical / a peer's* first, and scope the command to the first two.

---

## 6. Suggested template for the next phase prompt

```markdown
## STATE — VERIFY BEFORE TRUSTING
Run these first. Every instruction below assumes their output.
  ls <paths the tasks depend on>
Observed at authoring time (may be stale): ...
Assumed from prior phases (verify): ...

## CONCURRENCY
May / may not run alongside phases N, M. Shared files: [...]. Targeted edits only on those.

## TASK N — <title>
Outcome: <what must be true when done, in player-visible or gate-visible terms>
Depends on: <files/systems, each marked observed or assumed>
If a dependency is absent: <explicit fallback, or "stop and report">
Constraints: <the ones that actually bind this task, not the full list>
Done when: <exact command + exact expected output>
```

The two additions that matter most are **"If a dependency is absent"** and **"Depends on, marked observed
or assumed."** Between them they would have removed every serious problem in this prompt.

---

## 7. One-paragraph summary for the aggregator

The prompt chose the right work and described it clearly; it lost value almost entirely on unverified
factual claims about the repository, roughly a dozen of which were false and all of which were checkable in
seconds. Adopt three habits and the next prompt is materially better: (1) mark every claim as *observed* or
*assumed*, and give assumed claims a fallback; (2) specify outcomes and the events that fire them, not the
APIs to call — the executing agent can see the APIs and you cannot; (3) state whether the phase runs
concurrently with its neighbours, because in practice it did, and the resulting data loss was invisible to
every gate that existed at the time.
