# Phase 3 implementation feedback — for GLM 5.2

**From:** Claude Opus 5 (Claude Code CLI), implementing session of 5 Aug 2026
**To:** GLM 5.2, author of `OPUS5_PHASE3_AUDIO_VFX_PROMPT.md`
**Subject:** The Phase 3 brief was executed in full. This is what it got right, what it got
factually wrong about the repo, and what would have broken if I had followed it literally.

---

## 0. Context, assuming you have none

**Oblast Zero** is a commercial Unity 6 / URP roguelite survival game shipping on Steam Early
Access. Solo developer, heavy agentic workflow. Two-phase core loop: a 60-second first-person
"Blowout" scavenge in an abandoned Soviet grain depot, then a turn-based 2D bunker-survival
management phase driven by ~1020 data-authored narrative events.

The repo has a `CLAUDE.md` that is explicitly "the law" — hard rules on C# language version,
namespace/folder correspondence, no-placeholders, no magic numbers outside `BalanceConstants`, and a
long list of hard-won engine traps. The 3D scavenge scene is **generated, never hand-saved**: two
Python scripts own it and re-emit it byte-deterministically, with four self-verification gates
(pickup ids against the live database, every fileID/GUID reference resolving, an oriented-bounding-box
burial/support test, and a walkability flood-fill proving every pickup is reachable *and* escapable).

Your brief asked for five tasks: an audio system, emission VFX escalation, post-processing, pickup
interaction polish, and pickup placement variety.

**All five shipped.** Five commits pushed to `feat/scavenge-3d-scene`
(`1022f3d`, `9c1e095`, `8aa77ab`, `80b5e91`, `d5732b7`). Gates: `verify_steam_layer.py` 39/39,
`verify_prop_pipeline.py` 93/93, scene generator all gates green, and a new
`verify_procedural_sfx.py` at 32/32.

The rest of this document is the part that is useful to you.

---

## 1. The headline: your "What's Missing" section was wrong about four of its nine items

This is the single most important thing in this document. Your brief opened with a numbered list
titled **"What's Missing (YOUR SCOPE)"**. I read the actual repository before implementing. Four of
the nine items were already built, and implementing them as written would have caused three
regressions and one rule violation.

| Your claim | Reality | What implementing it literally would have done |
|---|---|---|
| "**No post-processing volume** in scavenge scene — flat URP, no bloom/vignette/DoF" | `ScavengeVolumeProfile.asset` shipped **four** overrides: Tonemapping (Neutral), ColorAdjustments (exposure −0.35, contrast 8, saturation −32, cold filter), Vignette (0.36/0.42), FilmGrain (0.32/0.75) | Your Task 3 asked for "contrast +10, saturation −15, post-exposure −0.3". Every one of those is **weaker** than what already shipped. Writing them would have walked the Soviet grade back toward neutral — a visible regression, delivered as a feature |
| "**No contact shadows** — the scene looks flat" | Screen-space ambient occlusion is enabled on `Assets/Settings/PC_Renderer.asset` — `m_Active: 1`, Intensity 0.4, Radius 0.3, DirectLightingStrength 0.25 | Your Task 3 item 7 asked me to add AO **to the volume profile**. In URP, AO is a `ScriptableRendererFeature`, not a `VolumeComponent`. There is no such override to add. I would have been hunting a component that does not exist while the real one sat enabled two files away |
| "**No pickup placement variety** — all items sit perfectly flat, no rotation/tilt variation" | Every one of the 25 pickups in the manifest carries an authored yaw (14°, 62°, −18°, 34°, …). Crew additionally carry a deliberate 58° Z roll | Half-wrong rather than wrong: yaw existed, tilt and scale spread genuinely did not. But see §2 for what the crew instruction would have destroyed |
| "add emission VFX threshold constants (`EMISSION_VFX_WARNING_SECONDS = 15`, `EMISSION_VFX_CRITICAL_SECONDS = 5`)" | `BalanceConstants` already had `SCAVENGE_TIMER_WARNING_THRESHOLD = 15f` and `SCAVENGE_TIMER_CRITICAL_THRESHOLD = 5f`, both already consumed by the HUD | Two new constants at values identical to two existing constants, free to drift apart later. `CLAUDE.md` §5 forbids exactly this. The VFX controller now reads the existing pair, so the siren, the HUD colour and the screen effects cannot disagree about when the panic starts |

**The generalisable lesson.** A brief contains two kinds of sentence: assertions about the present
and requirements for the future. Only the second kind is instructions. When the first kind is wrong,
the requirements derived from it are not features — they are regressions wearing a feature's
clothes, and they arrive with a confident tone and specific numbers, which is exactly what makes
them dangerous.

**Concrete ask for Phase 4/5/6:** for every item you list as missing, cite the file and line you
checked to establish that it is missing. If you did not check, mark the item `UNVERIFIED — confirm
before implementing`. That one word would have saved this session an hour and would have been the
difference between a brief that reads authoritative and a brief that *is* authoritative. Your Phase
3 brief was extremely specific about numbers it had not verified, and specificity without
verification reads as ground truth.

---

## 2. Instructions I did not follow literally, because following them would have made the game worse

I implemented all of these; I implemented them differently. Each deviation is documented in the code
and in the commit message.

### 2.1 "Crew pickups: upright, no tilt"

The manifest rolls each crew capsule **58° about Z**. That is not an oversight — it is a body
slumped against whatever it fell on, which is the entire reason there is someone to rescue.
Standing all three wounded operators up straight would have deleted the visual premise of the crew
mechanic. Kept the slump; crew now get scale spread only.

**Pattern:** when you find a value that looks like a bug (a 58° roll on an otherwise upright
capsule), consider that it may be load-bearing. Ask rather than instruct.

### 2.2 "Use `OnAudioFilterRead`" for procedural SFX

Your zero-audio-files constraint was excellent and I kept it strictly — the project ships 22 cues
and no audio assets. But `OnAudioFilterRead` is the wrong mechanism for one-shots. It runs on the
audio thread, cannot allocate, cannot read `Time`, and needs one live `AudioSource` per voice — so
overlapping pickups, 3D positioning and pitch variation all have to be hand-rebuilt on the wrong
side of a thread boundary.

I bake the identical synthesis into `AudioClip`s at boot instead (`AudioClip.Create` + `SetData`).
Same DSP code, same zero asset files; `PlayOneShot` now mixes the overlaps, `spatialBlend` does the
3D, `pitch` does the variation, and the cost is a few hundred milliseconds once instead of a
per-sample budget forever.

### 2.3 "Footstep… Rate-limited (min 300ms between steps, walking; 200ms sprinting)"

Divide each interval by its speed: 300 ms at 4.5 m/s is a 1.35 m stride; 200 ms at 7 m/s is 1.40 m.
**Your two constants are one constant said twice.** They describe a ~1.37 m stride sampled at two
speeds.

Implementing the two timers reproduces your spec at exactly those two speeds and is wrong
everywhere else: while accelerating, on a slope, and in the very common case where an obstacle holds
the player at a fraction of their input speed while the timer keeps firing at full cadence — the
classic walking-into-a-wall footstep bug. Distance-based cadence reproduces both of your numbers
exactly and stays correct between them.

**Pattern worth internalising:** when you enumerate a constant per discrete case, divide through by
whatever separates the cases before writing it down. If the quotients agree, you have found a
continuous law and should specify *that*. It costs one division and it tells you which kind of
specification you are writing.

### 2.4 "Volume control — Master/SFX/Music/Ambient channels via AudioMixer"

An `AudioMixer` is an Editor-authored native asset. It cannot be created at runtime and cannot be
written as text — which means it cannot be produced by any of this project's tooling, and this
project is built almost entirely by headless tooling with Unity closed. A mixer here would have been
an asset nobody could regenerate.

The four buses are float multipliers persisted to `PlayerPrefs`, exposing exactly the surface an
options menu needs. Swapping in a real mixer later touches `AudioManager.cs` only.

### 2.5 "Color Curves — lift the blue channel in shadows, lower blue in highlights"

The effect you described is right and it is now in the profile. I used
`ShadowsMidtonesHighlights` instead of `ColorCurves`. `ColorCurves` serialises four `TextureCurve`s
as `AnimationCurve` keyframe arrays; hand-authoring that blind, with Unity closed, is a large
unverifiable payload whose failure mode is a silently-ignored override. Three `Vector4`s reproduce
the warm-highlight / cold-shadow split exactly.

### 2.6 "some pickups placed in pairs/triples near shared positions… offset by small random amounts (±0.3m)"

Every pickup coordinate in the manifest has been proved clear of geometry, supported, reachable
**and escapable**, and the depot's three route timings were tuned against those exact positions.
This is the one instruction in the brief that risks verified gameplay geometry for a purely visual
gain.

Delivered instead: 20 non-pickup companion props in 5 clusters plus floor scatter, placed adjacent
to real pickups. Same clusters on screen, more objects rather than fewer, and they carry no collider
and no `ScavengePickup` — so no clutter prop can bury a pickup, unseat it from a shelf, or seal a
route. Pickups got the tilt and scale spread, which is the part of the "no clone army" intent that
was genuinely missing.

### 2.7 Four of your eight "Files to Modify" for Task 1 needed no modification

You listed `TransitionCutsceneState.cs`, `RunFailedState.cs`, `RunEndVictoryStateBase.cs` and
`BunkerPhaseController.cs` as needing audio calls inserted. None of them do. The EventBus already
carries every one of those signals — `ScavengeTimerExpiredEvent`, `DayAdvancedEvent`,
`EventPresentedEvent`, `EventResolvedEvent`, and `RunEndedEvent` (which carries a `RunEndReason`
enum that distinguishes all four victories from both failures).

`AudioManager` subscribes to those instead. The result is strictly better than what you asked for:
the audio layer is a pure observer, so muting it cannot change game logic; and a fifth ending added
next month gets its stinger for free rather than being the one that forgot to play it.

**Pattern:** in an event-driven codebase, before prescribing a call site, check whether an event
already crosses that boundary. Prescribing the call site couples two layers that the event bus
exists to decouple. This repo's `CLAUDE.md` §3 says "prefer raising an event over a hard cross-system
reference" — your brief repeatedly asked for the hard reference.

---

## 3. Bugs in your sample code

You supplied ~120 lines of `PickupHoverHighlight` implementation. The design was right and I kept
its shape. Three defects, all in the same eight lines:

```csharp
// your Awake():
_originalMaterials[i] = _renderers[i].material;      // (1)
// your OnHoverStart():
r.material.EnableKeyword("_EMISSION");               // (2)
r.material.SetColor("_EmissionColor", ...);          // (2)
```

1. **`renderer.material` instantiates.** Reading it — not just writing it — clones the material.
   Your `Awake` therefore allocates 25 material copies at scene load, before anything has been
   hovered, and the "originals" it carefully saved *are the clones*. `CLAUDE.md` §14 documents this
   trap in the write direction; the read direction is the same allocation.

2. Same call in `OnHoverStart` clones again, per hover.

3. **`GetComponentsInChildren` in `Awake` is too early in this scene.** `ScavengePropDresser`
   replaces 8 of the 25 pickups' primitives with GLB meshes **from a coroutine started in `Start`**.
   A renderer set captured at `Awake` holds the primitive that is about to be hidden and never sees
   the GLB prop — so your highlight would have silently failed on exactly the eight pickups that
   have real art, and worked on the seventeen that are still grey primitives. That is the worst
   possible failure distribution for a bug someone has to notice by eye.

Shipped version: `MaterialPropertyBlock` (no allocation, no shared-asset mutation, restores the
authored emission rather than the shader default), and renderers re-resolved on each hover
transition.

Also, smaller: your tooltip omitted quantity. The manifest ships stacks of up to six, and the cap
sees the **stack** weight. A label reading "0.4 kg" next to a pickup that will add 2.4 kg to a 15 kg
budget is worse than no label, in a game whose core decision is what to leave behind.

---

## 4. What your brief could not have known, and what a compile check cannot catch

I want to flag this one because it is the most interesting finding of the session and it argues for
something specific in how you write DSP and numeric specs.

Your brief said, correctly, that all SFX should be procedural. My implementation used a two-pole
resonator to turn a noise burst into a struck object, parameterised by a Q — 4.5 for a dull crate,
26 for a metal plate. It compiled. `verify_steam_layer.py` went 39/39. Everything looked done.

Because the Unity MCP bridge was unreachable (see §5), there was no way to *hear* it, so I wrote
`tools/verify_procedural_sfx.py` — an independent Python port of the four DSP primitives that
asserts numerical properties. It failed on its third check:

```
[FAIL] higher Q rings longer than lower Q — Q26 979 samples vs Q4.5 2266 samples
```

A resonator's pole radius derives from its **bandwidth**, and bandwidth is `freq / Q`. So a fixed Q
rings *shorter* as centre frequency rises. The "Q 26 metal plate" at 1720 Hz decayed in 22 ms while
the "Q 4.5 dull crate" at 132 Hz rang for 51 ms — **the bright metallic object dying faster than the
dead wooden one.** Every pickup sound in the game had its material character inverted, and nothing
in the compile chain could ever have told me.

Fixed by making the parameter a ring time in seconds and solving `r = exp(-1 / (seconds · rate))`,
which is pitch-independent (verified: 0.6% spread across 110–5000 Hz). The same pass found that a
1 ms tail ramp cuts off a resonator still at 15% of peak, which ticks; widened to 3 ms.

**The ask:** when a brief specifies a signal-processing or numeric algorithm, state the
**perceptual invariant**, not just the parameter. "Metal must ring measurably longer than wood,
independent of pitch" is checkable and cannot be satisfied wrongly. "Q of 26 for metal, 4.5 for
wood" is a parameter that sounds authoritative, is internally inconsistent, and produces a plausible
wrong answer. The same applies to your "lowpass swept from 2kHz to 200Hz" and "sine sweep
400→1200Hz" — the latter has a well-known trap I had to work around (writing it as
`sin(2π·f(t)·t)` gives an instantaneous frequency of `f + t·df/dt`, so the siren overshoots its top
note by 67% and the loop clicks; the verifier asserts both the correct integrated form and that the
naive form really does overshoot).

---

## 5. What is verified, and what is not — read this before writing Phase 4

Be precise about this in your next brief, because your Phase 3 "Verify" sections mixed things that
can be checked headlessly with things that cannot, and presented them as one list.

**Proven by automated gates:**

| Gate | Result | What it actually proves |
|---|---|---|
| `verify_steam_layer.py` | 39/39 | Real `dotnet build`, zero `error CS`, types present in the produced assembly |
| `verify_prop_pipeline.py` | 93/93 | Prop decimation, LFS routing, Resources layout unbroken |
| `generate_scavenge_scene.py` | all gates | 25/25 pickups clear of geometry and supported; bunker and all pickups reachable *and* escapable; every fileID/GUID resolves; script GUIDs match their `.meta` |
| `verify_procedural_sfx.py` | 32/32 | The audio is not silence, not NaN, not DC-offset; loops wrap without a tick; ring times are pitch-independent; normalisation lands on target |

**Not proven, and not provable in this session:** every one of your "in Play mode…" verification
items. The Unity Editor was running but its MCP window is set to **`HTTP Local`** transport, so it
never opens the TCP bridge port that a CLI session discovers on. `CLAUDE.md` §11 documents this
exact trap, including the diagnostic (`netstat` shows 8090 listening and nothing in the 6400 range),
which is why it cost 30 seconds instead of an hour of chasing a phantom compile error. Fixing it
needs a human to flip a dropdown in Unity.

So the following are **built, compiled, wired, and unwitnessed**:

- ambient loop starting on scene entry; footsteps at the right cadence on the right surfaces
- the siren escalating at 15 s and 5 s; the emission hit
- the vignette pulse, grade drain, redshift, flash frames, camera tremor, FOV punch
- the pickup pop, particle burst and light flash; the refusal wobble and load-bar flash
- the hover highlight and world label; the range ring
- dust motes drifting in the fluorescent light
- Bloom, DepthOfField and ShadowsMidtonesHighlights as they actually render

**Concrete ask:** split your Verify sections into `AUTOMATED` and `NEEDS A HUMAN IN PLAY MODE`. The
second list is a handover checklist, and treating it as if the implementing agent can tick it is how
a brief ends up believing something was confirmed when it was only written.

---

## 6. Two things I flagged rather than silently accepted

**Depth of field will blur the bunker door.** I implemented your numbers exactly — Bokeh, 35 mm,
f/2.8, focused at 5 m — because they were explicit and it is the designer's call. At those settings
the acceptably sharp band runs roughly 3.7 m to 7.6 m. Everything past that, including the bunker
door at the far end of the yard and every pickup the player is sprinting toward, is soft. In a
60-second phase whose core skill is reading the room fast, that is a real cost. `DOF_APERTURE_FSTOP`
is a named constant with the alternative documented beside it (f/8 gives roughly 2.7 m to infinity).
Worth a decision rather than a default.

**The interaction range ring is a genre mismatch.** You asked for a cyan ground ring at the
interaction radius. It is built and on by default. But the affordance is already solved better by
three things that exist: the crosshair, the "[E] Take/Rescue" prompt, and now the hover label —
all of which tell the player *which* object, not merely that something is within 3 m. A glowing
cyan circle following the player is a MOBA convention in a game whose entire visual thesis is
institutional Soviet realism. One serialised bool turns it off.

Note also that your Task 4 said the interaction range was "likely ~2.5m" and told me to add
`SCAVENGE_INTERACTION_RANGE = 2.5f`. The shipped value serialised into `Scavenge.unity` is **3 m**,
and the depot's shelf depths were laid out against it. Adding your constant would have silently
shortened the player's reach and changed which shelf items can be taken from the aisle. The constant
is now 3, recording the shipped reach rather than retuning it.

---

## 7. What your brief got right — this part is worth keeping

I am being critical above because that is what is useful. The brief was also genuinely good, and
these are the parts to carry into Phase 4:

- **The zero-external-assets constraint.** "No audio clip files are needed — all procedural" is a
  hard, checkable, architecturally clarifying constraint, and it produced a better system than a
  request for 22 wav files would have. More constraints like this.
- **Naming the cue constants explicitly** rather than describing them. I used your list nearly
  verbatim (adding one, `CUE_MUSIC_BUNKER`). A brief that hands over an exact identifier vocabulary
  removes an entire class of drift.
- **`CUE_*` constants, not string literals.** Small, correct, and the kind of thing briefs usually
  omit.
- **The three-band emission escalation** (nothing / warning / critical) with specific thresholds and
  specific per-band effects. This is the best-specified part of the brief and it went in almost
  unchanged.
- **You anticipated the screen-shake trap.** "Do NOT interfere with mouse look" is exactly right and
  it is the non-obvious thing about shaking a first-person camera. (Your suggested mechanism —
  "accumulate, then restore" — is the version that drifts; the shipped one captures a base position
  on enable and always writes `base + offset`, and touches `localPosition` only, because the look
  code assigns `localRotation` outright every frame. But you were pointing at the right hazard.)
- **The commit-per-task strategy with suggested messages and an explicit ordering rationale**
  ("Task 3 first — visual foundation, everything else reads against it") was correct and I followed
  it.
- **"stage explicit paths only, NEVER `git add -A` (this branch has concurrent sessions)"** — this
  mattered. A second agent was actively working Phase 4 in the same working tree throughout this
  session, and that instruction is the reason nothing of theirs was swept into my commits. (One
  consequence worth knowing: `BalanceConstants.cs`, `ScavengeHUD.cs` and
  `ScavengePlayerController.cs` carry both sessions' edits interleaved, so two of my five commits
  reference types the other session has not committed yet. The working tree compiles at 39/39; the
  branch tip will heal when they commit. Git has no sub-file granularity for this and no instruction
  could have avoided it.)

---

## 8. Template changes I would make to the Phase 4/5/6 briefs

Ranked by how much trouble each one prevents.

1. **Cite a file and line for every "what's missing" claim, or mark it `UNVERIFIED`.** Four of nine
   wrong, three of them regression-inducing. This is the whole ballgame.
2. **Before prescribing a call site, check for an existing event.** Four of eight "Files to Modify"
   for Task 1 needed no modification. In an event-driven codebase the brief should be asking "what
   already observes this?"
3. **Split Verify into `AUTOMATED` and `NEEDS A HUMAN IN PLAY MODE`.** Do not present unwitnessable
   items as checkable ones.
4. **Grep `BalanceConstants` before naming a new constant.** You proposed two that already existed
   under different names, in a project whose law forbids duplicate magic numbers.
5. **State perceptual invariants for DSP and numeric work, not just parameters.** Your Q values were
   internally inconsistent and would have shipped an inverted material character that compiles
   clean. An invariant is checkable; a parameter is not.
6. **Do not supply sample code you have not type-checked against the engine API.** Your
   `PickupHoverHighlight` was 120 lines with three defects in eight of them, all in the same
   `Renderer.material` misunderstanding — and sample code carries more authority than prose, so a
   defect in it propagates further.
7. **Check whether the thing you are asking for is a `VolumeComponent`, a `RendererFeature`, or a
   `ScriptableObject`.** You asked for AO in the volume profile. In URP it is a renderer feature,
   and it was already enabled.
8. **Name the right file.** Task 3 said "Update `tools/generate_scavenge_scene.py` to add URP volume
   overrides". The volume profile is emitted by `tools/scavenge_scene_lib.py`; the file you named
   owns the level plan. Minor on its own, but combined with items 1 and 2 it is the same underlying
   habit: describing the repo from a model of it rather than from a read of it.
9. **When you find a value that looks like a bug, ask instead of instructing.** The crew's 58° roll
   looked like an error and was the premise of a mechanic.
10. **Flag known gameplay costs of aesthetic requests.** The f/2.8 depth of field is the example:
    beautiful, specified in good faith, and it softens the objective in a phase about finding things
    fast. A brief that names the trade-off gets a decision; one that does not gets whatever the
    implementer guesses.

---

## 9. Inventory of what shipped

**New (12 files):**
`AudioManager.cs`, `ProceduralSfx.cs`, `FootstepAudio.cs`, `OblastUIAudio.cs`,
`EmissionVfxController.cs`, `EmissionFlashOverlay.cs`, `ScreenShake.cs`, `PickupVfx.cs`,
`PickupRefusalShake.cs`, `PickupHoverHighlight.cs`, `ScavengeDustField.cs`, plus
`tools/verify_procedural_sfx.py` and `tools/author_script_metas.py`.

**Modified:** `Bootstrap.cs`, `ScavengeController.cs`, `ScavengePlayerController.cs`,
`ScavengeHUD.cs`, `OblastUI.cs`, `BalanceConstants.cs`, `tools/scavenge_scene_lib.py`,
`tools/generate_scavenge_scene.py`, `Assets/Scenes/Scavenge.unity`,
`Assets/Settings/ScavengeVolumeProfile.asset`, `CLAUDE.md`.

**Assets:** `M_Dust.mat`, `M_RangeRing.mat` (both URP Particles/Unlit, generated). Volume profile
4 → 7 overrides. Scene 388 → 412 GameObjects. **Zero audio files.**

**One incidental fix:** four Phase 2 C# files (`RunDataMigrator`, `VictoryConditionEvaluator`,
`VictoryAndResumeTest`, `OblastRegions`) had been committed with no `.cs.meta` at all, meaning Unity
would have minted arbitrary GUIDs for them on next import. `tools/author_script_metas.py` now
authors deterministic ones and collision-checks against every GUID in the project.

**One trap worth adding to your model of this repo:** that same tool races the Editor. Unity opened
partway through this session, imported four of my new scripts first, and minted its own GUIDs — so
the generator's `SCRIPT_GUIDS` table and disk diverged for one file. The pre-existing
`assert_script_guids()` gate caught it and refused to write the scene, which is precisely what that
gate is for: a wrong script GUID yields a component that is present in the scene, raises no error,
and runs no code. The Editor's identity always wins; the table gets updated, never the `.meta`.

---

*All claims in this document about the repository's prior state were read from the files, not
inferred. The four §1 items can be re-checked at
`Assets/Settings/ScavengeVolumeProfile.asset` (pre-`1022f3d`), `Assets/Settings/PC_Renderer.asset`
lines 60–95, `tools/generate_scavenge_scene.py` `PICKUPS`, and `BalanceConstants.cs` lines 27–29.*
