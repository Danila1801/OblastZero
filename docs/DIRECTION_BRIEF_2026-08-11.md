# Oblast Zero: Direction Brief

Date: 11 August 2026
Author: Claude Code session, after reading the actual codebase and researching the reference games
Audience: Leonid, and any other model or person picking this up cold

This document is self contained. It assumes no prior context. Everything in the "findings"
sections was verified by reading real files, not assumed. Every claim that is a guess is
labelled a guess.

**Formatting rule applied throughout this document and proposed for all game text: no em dashes.**

---

## 0. The short version

You are right about almost everything, and one of your complaints turned out to be a literal
bug that I found and can point at, line by line.

You are wrong about one thing, and it matters commercially, so I say it plainly here and argue
it properly in section 4: going full Chernobyl and full stalker is legally fine but is the one
move that makes this game harder to sell, not easier. There is a version of your idea that
keeps the real setting, the vodka, the Ukraine, the stalkers, and still keeps the one thing
this project has that no other Zone game has. I recommend that version.

The deadline in the project file says content complete beta by 31 August 2026. That is twenty
days from today, not six weeks. The plan below is sized against that reality and says which
parts do not fit.

---

## 1. What I found in the code, verified

### 1.1 The "wait a long time to press continue" bug is not a wait. The button is 4.5 pixels tall.

This is the headline finding. I searched every C# file for coroutines, timers, fades, and
typewriter effects on the bunker path. There are none. The entire chain from clicking a choice
to the Continue button being live runs synchronously in a single frame. There is no delay at all.

What actually happens is a layout bug in
[EventModalUI.cs](Assets/_Project/Scripts/UI/EventModalUI.cs).

The modal card is 1040 by 720 with a VerticalLayoutGroup. Every section is given a
`LayoutElement`, and in Unity a `LayoutElement` outranks a `LayoutGroup` when reporting
preferred size. The outcome panel is told its preferred height is 1 pixel:

```csharp
// EventModalUI.cs:246
AddFlexibleHeight(outcomeGO, 0f, 1f, flexible: 1f);   // min 0, preferred 1
// EventModalUI.cs:254
AddFlexibleHeight(_continueButton.gameObject, 0f, 72f); // min 0, preferred 72
```

Working the layout maths through: the outcome panel resolves to 144.5 pixels, its children
need 212, so Unity lerps at 0.0625 between min and preferred. The outcome text has a min of
120 so it survives. The Continue button has a min of 0, so it resolves to
`Lerp(0, 72, 0.0625)` which is **4.5 pixels**.

TextMeshPro overflows its rect by default, so the word CONTINUE renders at full size and looks
completely normal. The clickable area is a 4.5 pixel band at the bottom of the card. You click
at the word, nothing happens. You click again, nothing happens. Eventually your cursor lands in
the band and it works. That is exactly what "you need to wait a lot to press continue" feels
like from the player side.

The modal also puts a full screen raycast blocker over everything at
`canvas.sortingOrder = 200`, so while you are hunting for the 4.5 pixel band, nothing else on
screen responds either.

The same class of bug is on the choice list at `EventModalUI.cs:237`. The choices container
resolves to 1 pixel and the 64 pixel choice buttons spill out below the card. With four choices,
**the fourth choice button is rendered off the bottom of the screen entirely.**

Also found: `EventModalUI` builds its own buttons instead of using `OblastUI.Button`, so the
Continue button has no click sound and no hover sound. A near miss produces zero feedback.

And: `OnContinueClicked()` is one line, `=> Hide()`. It raises nothing, resolves nothing,
unlocks nothing. The turn already finished when you clicked the choice, and End Day was already
re enabled at `BunkerHUD.cs:83`. The Continue button exists only to dismiss the click blocker
that the modal itself put there. It is a pure extra click for no reason.

**A bunker day currently costs three clicks, one of which is meaningless and is the broken one.**

### 1.2 There is genuinely no story. The numbers are worse than "no story."

I inventoried every piece of narrative content in the project.

| Thing | Count |
|---|---|
| Event JSON files | 1020 |
| **Unique narrativeText strings among them** | **326** |
| **Unique choiceLabel strings among them** | **75** |
| Unique titles | 238 |
| Events that name Marina, Yuri, or Sasha | **0** |
| Events that chain to a follow up event | **0** |
| Distinct outcome texts, across all 1020 events | **4** |
| Character portraits in the project | **0** |
| Pieces of character art of any kind | **0** |

Every choice label appears about 41 times on average. Every outcome in the game resolves to one
of four strings, of which the two common ones are "The matter resolves in your favour." and
"It does not go as intended."

The corpus is visibly template generated and the generator never handled a versus an, so the
game ships lines like "A actuary arrives carrying an adding machine", "A acoustic anomaly
pulses near the access road", and "A electrical anomaly". Those are player facing.

The three characters exist as stat blocks with about 120 characters of backstory each. That
backstory is displayed in exactly one place, [RunSetupUI.cs:190](Assets/_Project/Scripts/UI/RunSetupUI.cs#L190),
in 17 point grey text, **truncated to 128 characters**, on a card you see once before a run and
never again. Marina's entire characterisation as experienced by the player is:

> A field medic who stayed after the others left. She keeps a list of everyone she could not save.

`CrewMemberData.cs` already has a `public Sprite portrait;` field. It is null on all three
characters and is never read by any code anywhere in the project.

There is no intro, no prologue, no premise. `TransitionCutsceneState` is named a cutscene but is
a two second timer with no visuals and no text. The player is never told who they are, why they
are in a bunker, what an emission is, or what winning means.

**One exception, and it is important.** `InterviewSequenceUI.cs` is a six question hand written
set piece for an anomaly, and it is by a wide margin the best writing in the project:

> **ITEM 4, RECONCILIATION.** You have accounted for six years. The file in front of the
> interviewer accounts for eleven. The intervening five are already filled in, in handwriting
> resembling your own. Confirm or dispute the entry.

That is the proof that hand written beats generated in this project. One person wrote that in
an afternoon and it is worth more than the other 1020 events combined.

### 1.3 The 3D level is dark for six reasons multiplied together, and has zero textures

Your "it's pretty dark" is not one setting. I measured the stack.

The project is in Linear colour space. Ambient light is authored at sRGB 0.29, which becomes
**0.068 in linear**. Multiplied by the floor albedo (0.26 sRGB, 0.054 linear) an unlit patch of
concrete lands at about **5 percent grey on screen**. Then:

- There is **no skybox** (`m_SkyboxMaterial: {fileID: 0}`), so environment reflections resolve to black.
- There is **no baked lighting** (`m_LightingDataAsset: {fileID: 0}`). All 346 renderers are realtime lit.
- The 15 ceiling fixtures are intensity 2.4 at range 12 to 15, mounted 6.5 metres up. At floor
  level that delivers roughly **0.07**. They cast **no shadows**. `FluorescentFlicker` then drops
  each one to **zero for 0.22 seconds** at random intervals.
- Post processing takes another 32 percent globally: `postExposure: -0.35` plus a colour filter
  plus `contrast: 8` crushing the low end.
- Vignette at intensity 0.36 with a near black colour takes roughly another third at the edges.
- SSAO at intensity 0.4 darkens every crease on top of that.

**And 33.4 percent of the map is under a roof, where the one directional light contributes
nothing at all.** That is the third of the level where "it's pretty dark" is most literally true.

On textures, your instinct was right but the reason is worse than you think:

**There are zero textures in the scavenge scene. All 25 materials are flat colours.** Verified
mechanically: the count of `_BaseMap` entries with a texture other than `fileID: 0` across every
material is 0. The material schema in `tools/scavenge_scene_lib.py:163` is a four tuple of
(base colour, smoothness, metallic, emission). **There is no texture field in the schema at all.**

Three texture PNGs do exist in `Assets/Art/Textures/`, about 6.7 MB, dated 26 July. A repo wide
grep finds **no references to them anywhere**. They were generated and never wired up.

And here is the trap: if you just assign those three PNGs, it will look **worse**, and that is
exactly the "textures not well placed" problem you would then be diagnosing. The generator scales
Unity primitives, whose UVs are 0 to 1 per face, and it writes `m_Scale: {x: 1, y: 1}`
unconditionally. So:

- `Ground_Main` is a single cube scaled to 75 by 72 metres carrying **one** texture tile.
- `Wall_North` is 106 metres long carrying one tile.
- `Rail_Left` is 100 metres long by 0.18 wide, one tile, stretched at 555 to 1.
- `Respirator_Hook` is 0.06 metres and also gets one full tile.

That is a **1250x texel density spread with no correction**. Fixing it needs either triplanar
shading or per object tiling derived from world scale. Neither exists.

Geometry, counted from the scene file: 423 GameObjects, 346 mesh renderers, and every single mesh
is a Unity built in primitive. 295 cubes (85 percent), 39 cylinders, 9 spheres, 3 capsules. Zero
imported meshes in the environment. The four GLB props load onto pickups only.

The entire furniture inventory of a 104 by 72 metre industrial site is: 3 desks, 3 chairs, 5
filing cabinets, 6 shelving racks, 4 respirators, 2 wall boards, 1 door. Zero windows.

The open yard is 2080 square metres, 27.8 percent of the map, and contains **two barrels**. About
36 percent of the level is bare concrete with fewer than six objects on it.

You said "the map is not done fully." It is about one third done.

### 1.4 The em dashes, counted

| Location | Em dashes | Files affected |
|---|---|---|
| Event JSON | **38** | 24 |
| Item JSON | 0 | 0 |
| Localization tables (en and ru) | **35** | 2 |
| C# comments | 510 | (not player facing) |
| C# in `Debug.Log` | 108 | (not player facing) |
| C# in `[Tooltip]` | 16 | (editor only) |
| **C# genuinely player facing** | **21** | 17 lines |
| **Total across Assets/** | **752** | |

The good news is that the player facing surface is small and concentrated. All 38 in the event
JSON come from exactly **two generator templates**:

> A specimen collector arrives. They have your bunker's file — it is thick.
> A grey sedan idles at the access road. Two clerks. The senior — registration 8799 — presents a folder.

Fix two templates, all 38 are gone.

The 21 in C# are mostly the four victory captions (`"— THE THRESHOLD CROSSED —"`) and the six
Interview item headers (`"ITEM 1 — IDENTIFICATION"`).

Two cases need a replacement rather than a deletion: `bunker_placeholder` is the string `"—"`
and `bunker_day_unknown` is `"DAY —"`. Those use the dash as a placeholder glyph. Replace with
`...` or a blank.

Existing gates: `csharp_string_qa.py` has an em dash check at line 403, but it is **opt in,
C# only, info severity, and under counts** (it returns at most one hit per string). Its own help
text says em dashes are intentional. Nothing scans the JSON or the locale tables at all. So there
is currently no gate that would stop them coming back.

### 1.5 The language really is too hard, and I can prove it

I ran a Flesch Kincaid implementation over the whole event corpus.

| Corpus | Words | Words per sentence | Grade level | Reading Ease |
|---|---|---|---|---|
| All narrative text | 13,415 | 6.77 | 6.92 | 57.5 |
| All choice labels | 14,193 | 4.40 | **8.17** | **44.3** |

That grade level looks acceptable, but it is being held down artificially by very short
sentences. The real signal is the syllable load: **17.6 percent of narrative words and 23.3
percent of choice label words are three syllables or more.** Reading Ease 44.3 is classified
"difficult." The problem is word choice, not sentence length, which is exactly what you said.

Hardest words by difficulty times exposure: `countersignature` (65 uses), `decontamination` (77),
`jurisdictional` (42), `demographic` (72), `microsieverts` (46), `actuarial` (32), `procedural`
(56), `anomalous` (48), `personnel` (119), `registration` (98), `perimeter` (178).

Also found: `actuarys` appears 4 times. That is a misspelling shipping in the game.

**The silver lining is enormous.** Because there are only 75 unique choice labels and 326 unique
narrative strings, rewriting the language of the entire game is a job of roughly **400 short
strings**, not 1020 events. That is one focused day of writing, not a month.

---

## 2. Where I agree with you, plainly

Yes, this is a spreadsheet with a mood, not a game with people in it.

Yes, the correct reference is 60 Seconds!, and the specific thing 60 Seconds! has that you do
not is that Ted, Dolores, Mary Jane and Timmy are **characters who are present in every line of
text.** They have faces. They complain. They have opinions about each other. The journal is
written in a voice. Robot Gentleman describes the whole game as a
["post apocalyptic dark comedy"](https://robotgentleman.com/presskit/60SecondsReatomized.htm)
and the Reatomized remaster's headline feature was literally a
"NEW RELATIONSHIP SYSTEM featuring more stories and crazy interactions between the McDoodle
family members." They shipped a remaster whose main selling point was **more character
interaction**. That is the market telling you what the value is.

Your game has three people with names and 120 characters of backstory each, who are never
mentioned again, ever, in any of the 1020 events, and who have no faces.

Yes, "coding is done" was never a valid stopping point. The engineering is genuinely in good
shape. What is missing is everything the player actually experiences.

---

## 3. Where I push back, and why it matters for money

You said: go full Pripyat, full Ukraine, full stalker, full vodka, all correlations to Stalker.

The legal answer and the commercial answer are different, so here they are separately.

### 3.1 Legally, you can have almost all of it

**Real place names are not protectable.** Pripyat, Chernobyl, the Exclusion Zone, the Duga radar,
the Red Forest are real geography and history. Commercial games use them freely. Chernobylite
and Chernobylite 2 by The Farm 51 are built on the real site with real scanned locations. Call of
Duty 4 shipped a Pripyat level. This is settled by practice.

**The core concepts you want are older than S.T.A.L.K.E.R. and belong to nobody in particular.**
The Zone, anomalies, artifacts, and the word "stalker" for a person who scavenges in the Zone all
come from the Strugatsky brothers' *Roadside Picnic*, published 1972, and Tarkovsky's 1979 film.
GSC built on that source, they did not invent it. Copyright protects expression, not ideas. The
concept of a bounded zone containing physics defying anomalies and valuable artifacts, entered
illegally by scavengers, is an idea.

Note that *Roadside Picnic* itself is **not** public domain. Boris Strugatsky died in 2012, so
under EU life plus seventy the text is protected into the 2080s. That constrains you from copying
its **text, plot, or named characters**, not from using a zone with anomalies in it.

**What is actually GSC's, and must not be touched:**

- The name S.T.A.L.K.E.R., the punctuated form, and the logo. GSC holds registered marks
  including [S.T.A.L.K.E.R.: Call of Pripyat](https://trademarks.justia.com/778/51/s-t-a-l-k-e-r-call-of-pripyat-77851592.html)
  for video games. Title constructions of the form "Shadow of X" or "Call of X" applied to
  Chernobyl or Pripyat are the exact thing that gets a letter.
- Faction names: Duty, Freedom, Monolith, Clear Sky, Ecologists as a proper noun faction.
- Mutant names: Bloodsucker, Snork, Controller, Pseudogiant, Pseudodog, Burer, Chimera as
  proper nouns.
- Anomaly names: Springboard, Whirligig, Burner, Electro, Vortex as proper nouns.
- Character names: Strelok, Sidorovich, Scar, Degtyarev, Marked One.
- Specific artifact names from the games.

There is precedent for GSC being active about the name. In 2014 GSC pushed on West Games' project
named "Stalker Apocalypse" and the dispute was covered widely
([Gamer/Law](https://www.gamerlaw.co.uk/2014/the-latest-games-trademark-controversy-s-t-a-l-k-e-r-and-stalker/),
[Game Developer](https://www.gamedeveloper.com/business/the-latest-games-trademark-controversy-s-t-a-l-k-e-r-vs-stalker)).
The word "stalker" as a lowercase common noun for the profession is much safer than the title,
but if you put it on the store page as a title word, you are asking for attention you do not need.

**Verdict: you can set the game in the real Chernobyl Exclusion Zone, in Ukraine, call your
characters stalkers in lowercase, have vodka, have anomalies, have artifacts, have dosimeters,
have Soviet signage. The existing CLAUDE.md IP firewall is stricter than the law requires, and
that is a deliberate choice you are allowed to relax. What you must not do is use GSC's proper
nouns or a title that trades on theirs.**

### 3.2 Commercially, going full Chernobyl is the risky move, not the safe one

Here is the argument, and then you decide.

The Chernobyl setting is not an empty field in 2026. S.T.A.L.K.E.R. 2 shipped. Chernobylite 2
launched in Early Access in March 2025 and
[topped Steam's survival horror charts](https://store.steampowered.com/app/2075100/Chernobylite_2_Exclusion_Zone/),
though it sits at a rough 59 percent user rating. The original Chernobylite is at 82 percent and
has been discounted to about 6 dollars. There is a long tail of Chernobyl liquidator sims and
Zone shooters. That audience exists and is large, but it is also **already served, price
anchored low, and very quick to call something a knockoff.**

If Oblast Zero becomes "a Chernobyl survival game with anomalies and artifacts", it lands in a
category where it is the smallest, latest entrant, competing against a 6 dollar Chernobylite and
against S.T.A.L.K.E.R. 2 itself, on exactly the terms those games are strongest on: 3D
atmosphere, scale, and gunplay. Your 3D level is 295 stretched cubes with no textures. That fight
is unwinnable and does not need to be fought.

**What this project has that none of those games have is the bureaucratic joke.** "The Oblast does
not raise its voice. The Oblast files a form." A shelter management game where the horror is
administrative, where the anomaly conducts a job interview, where your death notice is a
registration closure, is a thing that does not exist on Steam. The Interview sequence in your own
codebase is the proof of concept and it is genuinely good.

So my recommendation is not "don't do Chernobyl." It is:

> **Set it in the real Exclusion Zone. Use the real place names. Use the vodka, the dosimeters,
> the Soviet signage, the stalkers. Keep the bureaucratic voice as the thing that makes it
> yours.** The setting gets you discovered by the Zone audience. The voice is why they tell
> their friends about it.

That gives you everything you asked for except the one part that would flatten your only
advantage.

### 3.3 The AI art warning, with numbers, because this is a real risk to your revenue

You said "we will create characters, don't worry." If that means AI generated 2D art, you need
these numbers before you commit.

Valve rewrote the AI disclosure rules on 16 January 2026 to focus on content
[consumed by players](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/)
rather than dev tools. Claude Code writing your C# needs no disclosure. **AI generated character
portraits do.** They appear on your store page under "AI Generated Content Disclosure."

The measured effect on Steam:

- A data analyst found the AI stigma can reduce a game's review count by about
  [53 percent](https://www.pcgamer.com/software/ai/data-analyst-finds-ai-stigma-on-steam-can-reduce-the-number-of-reviews-a-game-gets-by-around-53-percent-and-the-reviews-it-does-get-are-more-negative/),
  and the reviews that do arrive are more negative.
- Games with disclosed AI content see roughly a 25 percent higher negative to positive ratio,
  with reviews citing "soulless" and "lazy."
- Review bombing over suspected AI art hit multiple indie games in 2025, including
  [cases where the accusation was false](https://www.pcgamer.com/games/rpg/rpg-dev-pushes-back-against-steam-review-ai-accusations-we-poured-years-of-our-lives-into-this-game-and-only-worked-with-real-human-artists-on-everything/).

Character portraits are the single most scrutinised asset class for this. Faces are what people
look at, and AI faces are what people recognise.

**My recommendation on art: you need eight to twelve portrait images total, not hundreds.** At
that volume, commissioning a human illustrator is genuinely affordable, probably 400 to 1200
euro for a consistent set in one style. That is the highest return per euro anywhere in this
project. If the budget is truly zero, the fallback that avoids the stigma is a **non
photographic, non painterly style you build yourself**: high contrast two colour stencil
portraits, printed personnel file photos with halftone dots and a stamp over them, or silhouettes
on ID card forms. That reads as art direction rather than as generated art, and it fits the
bureaucratic theme exactly. It is also something a solo dev can execute.

---

## 4. What 60 Seconds! actually does, translated into changes for your game

| 60 Seconds! | Oblast Zero today | The change |
|---|---|---|
| Four family members, drawn, always on screen | Three names in a stat block, no faces | Portraits, always visible in the bunker |
| Journal written in Ted's voice, first person, funny | 1020 events in impersonal third person, nobody speaks | Every event attributed to a speaker |
| Characters have opinions about each other | No relationship data at all | Relationship or trust values between the three |
| Dark comedy, so the bleakness is bearable for hours | Unbroken bureaucratic gloom | Keep the register, add dry humour |
| Expeditions send a specific person, who might not come back | Expedition system exists but is impersonal | Named consequences, per character |
| A run is short, so death is cheap and you replay | A run is up to 45 days of three clicks each | Cut the click count, tighten the day |
| One shelter you learn intimately | Three sites, none played yet | Ship one great site, not three unplayed ones |

The single highest leverage change on that list: **give every event a speaker.** Right now the
schema has no speaker field, so nobody in this world says anything as a person. Adding one
string field and rendering a name plus a portrait next to the text changes the entire feel of
1020 events without rewriting 1020 events.

---

## 5. The story proposal

You asked for: three characters, each in a different situation, who come together as a team,
hide in the bunker from an anomaly, then do tasks in the bunker that affect their lives, with
Detroit style branching endings.

Here is that, sized for a solo dev with twenty days.

### 5.1 The premise, in plain words

It is 1986. Something went wrong at the plant, and then something went wrong with the going
wrong. The evacuation buses came and left. The paperwork did not stop. Somewhere in the
administration a district was created called Oblast Zero to handle the deviation, and that
district has never been closed, because closing it requires a form that nobody is authorised to
sign.

Three people are still inside the perimeter. None of them chose to be. Every so often the sky
goes wrong, and the only survivable answer is to be underground when it does.

That is the whole premise. It fits on one screen. It is currently nowhere in the game.

### 5.2 The three, and why they are each in a different situation

Keep the existing names and stats. Give them a reason to exist.

**Marina Volkova, field medic.** Situation: she is at the clinic, and the clinic is officially
closed, which means her patients officially do not exist. She stayed because the evacuation list
had names on it that were not on the bus. Her drive is the list. Her flaw is that she cannot
triage, which is fatal in a resource game. Her arc question: **when do you stop counting?**

**Yuri Lebedev, ex soldier.** Situation: he is at a checkpoint that is still being manned
against an order from 1981 that was never rescinded. His unit rotated out and nobody replaced
him and nobody told him to leave. His drive is the order. His flaw is that he obeys structures
that have stopped existing. Arc question: **who is actually giving the orders now?**

**Sasha Morozov, scavenger.** Situation: he is not supposed to be here at all. He came in for
the goods and got caught inside when the perimeter tightened. He knows every culvert and
stairwell. His drive is out. His flaw is that he will trade anything, including people. Arc
question: **is there an outside left to get out to?**

Three people, three relationships to authority: the one who serves people, the one who serves
the system, the one who serves himself. That is the thematic spine and it gives every event a
reason to hit each of them differently.

### 5.3 The opening, and the convergence

Do not build three separate playable prologues. That is a Detroit budget and you have twenty days.

Instead: **the first scavenge run is the convergence.** Three short text screens before the run,
one per character, thirty words each, saying where they were and what made them run. Then the 60
second scavenge, which they are already in together. Then the bunker door closes and the emission
hits. The player learns who they are from the events afterwards, not from a prologue.

This is the cheap and correct version. Convergence is satisfying when the meeting is caused by
the same external pressure hitting three different lives, and here the emission does that for
free. It is arbitrary when the characters just happen to be in the same place.

Cost: about 120 words plus three portraits. Value: the player finally knows what is happening.

### 5.4 The bunker arcs, and how to build them for cheap

Do not attempt a Detroit style node graph. Detroit took five and a half years, 180 staff, over
[2000 pages of branching script](https://www.mithrie.com/blogs/comprehensive-guide-detroit-become-human/),
and Quantic Dream had to build custom tooling just to debug the branches. Their flowchart shows
85 endings which collapses to roughly 40 real ones, each protagonist having four to six major
outcomes.

The technique that gets you 80 percent of that feeling at 2 percent of the cost is
**quality based narrative**, also called storylets, described precisely by Emily Short in
[Beyond Branching](https://emshort.blog/2016/04/12/beyond-branching-quality-based-and-salience-based-narrative-structures/)
and used by Fallen London, Sunless Sea, Cultist Simulator, Wildermyth, and King of Dragon Pass.

A storylet is a piece of content with **prerequisites** that decide when it can appear and
**effects** on world state when it plays. You do not author a tree. You author a pool, and the
tree emerges from the state.

**You already have this engine and you are not using it.** `EventEngine.cs` already gates on day
range, faction reputation, crew traits, items held, region, and completed event ids. It already
supports `followUpEventId` queuing with dedup and non repeat. `EventPrerequisite` is already a
working storylet condition system. **Zero events use the follow up field.** The machinery is
built and idle.

What is missing is three small schema additions:

1. **`speakerId`** on the event, so somebody says it.
2. **A flag store on RunData**, a simple `List<string> storyFlags`, plus `requiredFlagsAny` and
   `blockedByFlags` on the prerequisite, and `setsFlags` on the outcome. This is the whole
   quality based narrative mechanism. About 60 lines of code.
3. **`act`** on the prerequisite, gated by day count, so the pool escalates: days 1 to 10 is
   survival, 11 to 25 is the factions noticing you, 26 plus is the ending pressure.

With flags, an arc is authored as five storylets that gate on each other. "Marina finds the
list" sets `marina_list_found`. Three days later "The list has a name you recognise" requires
that flag. And so on. Linear authoring cost, combinatorial felt experience.

**Budget: 45 hand written storylets total, 15 per character, five per act.** At roughly 120 words
each including choices and outcomes that is about 5400 words. That is two to three focused days
of writing. Those 45 sit in the same pool as the 1020 procedural ones with a much higher weight,
so the player hits a real story beat every few days and filler in between. That is exactly the
mix that games like Wildermyth and Fallen London ship.

### 5.5 Endings

You have four endings today, all triggered by the same rule (survive to day 15, get one faction
above 60) and selected purely by which reputation number is highest. That is not four endings,
it is one ending with four skins. None of them mentions any character by name. Dead crew are
never named in any ending.

Minimum viable improvement, in cost order:

1. **Name the dead.** Every ending lists who did not make it and one line about each. This is
   about 12 lines of writing and is the single biggest emotional return in this entire document.
2. **Per character epilogues.** Each survivor gets a two sentence epilogue selected by their
   flags. Three characters times three or four flag states is nine to twelve short paragraphs.
   With four faction endings that multiplies into a felt sense of dozens of outcomes for the
   cost of about 1000 words.
3. **One extra ending that is not faction based.** All three alive, no faction above 40, day 30
   plus. The "we stayed and it was ours" ending. This rewards the player who refuses to pick a
   side, which is currently punished.

That is a real Detroit style outcome grid at about 1500 words of writing, because the branching
is computed from flags rather than authored as a tree.

---

## 6. Language and em dash policy

Concrete rules to apply to all player facing text:

1. **No em dashes and no en dashes.** Use a comma, a full stop, or brackets. Two placeholders
   (`bunker_placeholder`, `bunker_day_unknown`) need a substitute glyph, not a deletion.
2. **No word longer than three syllables unless a 12 year old would know it.** Specifically
   retire: countersignature, jurisdictional, actuarial, demographic, procedural, decontamination
   in prose. Keep the bureaucratic feel with short institutional words instead: form, stamp,
   file, order, quota, ration, permit, log, notice, transfer.
3. **Keep sentences under 15 words.** They already average 6.8, which is good. Do not lose that.
4. **Every event has a speaker.** Somebody in this world says the line.
5. **Fix the a versus an bug.** "A actuary" and "A electrical" currently ship.
6. **Fix `actuarys`.**

Scope of the rewrite, and this is the good news: **75 unique choice labels, 326 unique narrative
strings, 238 titles, 4 outcome strings.** Rewriting the language of the entire game is about 400
short strings. Not 1020 events.

**New gates needed**, because nothing currently prevents regression:
- A dash gate over JSON, locale tables, and player facing C#, failing the build, not info only.
  The existing check at `csharp_string_qa.py:403` is opt in, C# only, and under counts.
- A syllable and reading level gate with a threshold, so the vocabulary cannot creep back.
- Note: `content_qa.py:184` currently lists the em dash as a valid sentence terminator in its
  named entity heuristic. Removing dashes touches that line.

---

## 7. The 3D level

You said the map is not done. It is about one third done and it is fixable in the generator,
which is the right place because the scene is generated, never hand edited.

In priority order by visible improvement per hour:

1. **Lighting, roughly two hours.** Raise ambient from 0.29 to about 0.45 sRGB. Add a skybox so
   reflections are not black. Raise fixture intensity from 2.4 to about 5, and enable shadows on
   at least the four fixtures near the player path. Reduce `postExposure` from -0.35 to about
   -0.1. Reduce vignette from 0.36 to about 0.2. Reduce the flicker blackout from 0.22 seconds to
   about 0.08, and reduce how many fixtures can be dark at once. This alone changes the game from
   "I cannot see" to "it is moody."
2. **Depth of field, five minutes.** It is currently Bokeh at f/2.8, 35mm, focused at 5 metres.
   In a 104 by 72 metre level that permanently blurs everything past about 10 metres. This is a
   large part of why it reads as unfinished. Either turn it off or push focus distance far out.
3. **Fog, five minutes.** Linear fog ends at 98 metres on a 126 metre diagonal, so the far corner
   of the map is a grey wash. Push the end to 140.
4. **Textures with correct tiling, roughly one day.** Add a texture and tiling field to the
   material schema in `scavenge_scene_lib.py`, then derive `m_Scale` per renderer from world
   scale so a 75 metre floor gets 75 tiles and a 0.06 metre hook gets one. The three PNGs already
   exist and are unreferenced. **Do not assign them without the tiling fix or it will look worse
   than flat colour.**
5. **Fill the empty ground, roughly one day.** 36 percent of the map has fewer than six objects on
   it. The generator already knows how to place clutter clusters. More barrels, pallets, fallen
   signage, puddles, parked vehicles, a bus shelter, a queue barrier. In a bureaucratic Zone game,
   the best clutter is paperwork: scattered forms, a noticeboard, a queue of empty chairs outside
   an office.
6. **Interiors, one to two days.** Currently 3 desks, 3 chairs, 5 cabinets and one door across the
   whole site. Add rooms with doorways, more filing, a canteen, a locker room. Rooms read as
   "finished" far more than open ground does.

Note: all of this must go through the generator plan, not the scene YAML, and after regenerating
you must restore the materials or you will get 22 spurious modified files. The existing project
notes cover this.

---

## 8. Honest scoping against 31 August

Twenty days. Here is what I believe fits and what does not.

**Must do, roughly 5 days, and the game is transformed:**

| Task | Time | Why |
|---|---|---|
| Fix the 4.5 pixel Continue button and the choice overflow | 1 hour | It is a hard blocker on the entire 2D phase |
| Remove the Continue click entirely, auto dismiss on resolve | 1 hour | Cuts the day from 3 clicks to 2 |
| Lighting, DOF, fog pass on the 3D level | 3 hours | Fixes "too dark" |
| Add `speakerId` and render name plus portrait in the modal | 4 hours | Turns 1020 impersonal events into 1020 spoken ones |
| Add the story flag system to RunData and EventEngine | 4 hours | Unlocks all authored arcs |
| Write the premise screen and three character openings | 3 hours | The player finally knows what is happening |
| Rewrite the 75 choice labels and 326 narrative strings for reading level and dashes | 2 days | The whole game reads differently |
| Name the dead in every ending | 2 hours | Highest emotional return per hour in this document |
| Portraits, 8 to 12 images | 1 to 3 days or external | Everything above depends on faces existing |

**Should do, roughly 5 days:**
- 45 hand written story storylets, 15 per character
- Per character epilogues gated on flags
- Texture and tiling system in the generator
- Fill the empty third of the map
- The dash gate and the reading level gate

**Cut, and say so out loud:**
- Detroit scale branching. Not possible and not necessary. Flags plus epilogues gets the feeling.
- Three scavenge sites. Two of them have never been rendered, let alone played. Ship one that is
  good. Keep the other two as post launch content.
- Full voice acting, animation, cutscenes.
- The Russian localization staying in sync during a full prose rewrite. Freeze it, do it once at
  the end.

**One thing I want to flag as a risk you may not have priced:** the game has still never been run
end to end in Play mode. Every claim about how it feels is a claim about code, not about play.
The 4.5 pixel button survived because nobody has played it. There will be more like it. Budget
two days for "we played it and things were broken."

---

## 9. Recommended order of work

1. Play the game end to end, note everything. Half a day.
2. Fix the modal layout bugs. One hour, unblocks all 2D playtesting.
3. Lighting and post pass. Three hours, unblocks all 3D judgement.
4. Portraits commissioned or styled. Start this first because it has the longest lead time.
5. Speaker field plus flag system. One day of code.
6. Premise, openings, endings, name the dead. One day of writing.
7. The 400 string language rewrite plus dash removal. Two days.
8. The 45 story storylets. Three days.
9. Generator texture, tiling, and clutter pass. Two days.
10. Gates so none of it regresses. Half a day.
11. Play it again.

---

## 10. Sources

- [Robot Gentleman press kit, 60 Seconds! Reatomized](https://robotgentleman.com/presskit/60SecondsReatomized.htm)
- [60 Seconds! on Wikipedia](https://en.wikipedia.org/wiki/60_Seconds!)
- [Emily Short, Beyond Branching: Quality-Based, Salience-Based, and Waypoint Narrative Structures](https://emshort.blog/2016/04/12/beyond-branching-quality-based-and-salience-based-narrative-structures/)
- [Emily Short, Survey of Storylets-based Design](https://emshort.blog/2019/01/06/kreminski-on-storylets/)
- [Quality-Based Narrative, SimpleQBN reference](https://videlais.github.io/simple-qbn/qbn.html)
- [Detroit: Become Human flowchart and endings guide](https://www.mithrie.com/blogs/comprehensive-guide-detroit-become-human/)
- [Detroit: Become Human flowchart and replayability, PlayStation LifeStyle](https://www.playstationlifestyle.net/2019/07/15/detroit-become-human-flowchart/)
- [S.T.A.L.K.E.R. Call of Pripyat trademark registration, Justia](https://trademarks.justia.com/778/51/s-t-a-l-k-e-r-call-of-pripyat-77851592.html)
- [The latest games trademark controversy: S.T.A.L.K.E.R and STALKER, Gamer/Law](https://www.gamerlaw.co.uk/2014/the-latest-games-trademark-controversy-s-t-a-l-k-e-r-and-stalker/)
- [S.T.A.L.K.E.R. vs STALKER, Game Developer](https://www.gamedeveloper.com/business/the-latest-games-trademark-controversy-s-t-a-l-k-e-r-vs-stalker)
- [Roadside Picnic, Wikipedia](https://en.wikipedia.org/wiki/Roadside_Picnic)
- [Chernobylite 2: Exclusion Zone on Steam](https://store.steampowered.com/app/2075100/Chernobylite_2_Exclusion_Zone/)
- [Chernobylite on Metacritic](https://www.metacritic.com/game/chernobylite/)
- [Steam updates AI disclosure form, PC Gamer](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/)
- [AI stigma reduces Steam reviews by 53 percent, PC Gamer](https://www.pcgamer.com/software/ai/data-analyst-finds-ai-stigma-on-steam-can-reduce-the-number-of-reviews-a-game-gets-by-around-53-percent-and-the-reviews-it-does-get-are-more-negative/)
- [Steam AI disclosure rules 2026, indie developer guide](https://www.strayspark.studio/blog/steam-ai-disclosure-rules-2026-indie-developer-guide)
- [RPG dev pushes back against AI accusations, PC Gamer](https://www.pcgamer.com/games/rpg/rpg-dev-pushes-back-against-steam-review-ai-accusations-we-poured-years-of-our-lives-into-this-game-and-only-worked-with-real-human-artists-on-everything/)
