# OBLAST ZERO: MASTER PROJECT BLUEPRINT

[cite_start]**Engine:** Unity 6 LTS (C#, URP) [cite: 51, 251]
[cite_start]**Genre:** Bureaucratic-Horror Survival Management Hybrid [cite: 202]
[cite_start]**Core Loop:** 3D Kinematic Scavenge (Phase A) -> 2D Bunker Survival (Phase B) -> Meta-Progression (Run to Run) [cite: 164, 165]
[cite_start]**Vibe/DNA:** S.T.A.L.K.E.R., 60 Seconds!, Darkest Dungeon, Pathologic, Roadside Picnic [cite: 71, 137, 212, 355]

---

## 1. PROJECT IDENTITY & PILLARS
[cite_start]Oblast Zero is not a post-apocalyptic game; it is a **post-administrative** game[cite: 465]. [cite_start]The horror stems not from monsters, but from the indifference of failing Soviet bureaucratic systems covering up an existential threat[cite: 202, 482].

### [cite_start]The 5 Design Pillars [cite: 236]
1. [cite_start]**Bureaucratic Horror:** Fear comes from systems, paperwork, and administration, not jump scares[cite: 237, 238].
2. [cite_start]**Mundane Realism:** Every object is used, old, repairable, and grounded in reality[cite: 239, 240].
3. [cite_start]**Human Desperation:** There are no heroes, only survivors adapting to impossible systems[cite: 205, 241, 242].
4. [cite_start]**Information Scarcity:** The player never fully understands the Zone; reality is distorted through contradictory paperwork[cite: 243, 244, 484].
5. [cite_start]**Atmosphere Before Action:** Silence, tension, and loneliness matter far more than constant combat[cite: 201, 245, 246].

### [cite_start]The "Anti-Reference" (What We Must NEVER Become) [cite: 719, 720]
* [cite_start]NO Marvel-style quippy dialogue or Fallout humor[cite: 723, 727].
* [cite_start]NO generic zombies, loud horror, or constant combat[cite: 724, 729, 730].
* [cite_start]NO clean sci-fi UI or tacticool military fantasy[cite: 725, 726].
* [cite_start]NO Ubisoft-style map markers or exposition dumping[cite: 728, 731].

---

## 2. FACTIONS & IDEOLOGY
There are no "good guys." All factions are dangerous and operate in a 3-way deadlock.

1. [cite_start]**The Scale Society ("The Clerks"):** A federalized bureaucratic entity that views the Zone as a resource for "demographic adjustment"[cite: 481, 532]. They use the Zone to erase undesirable people and administer the anomaly through strict, cold paperwork.
2. [cite_start]**The 14th Independent Cordon Regiment ("The Cordon"):** Soviet military remnants still blindly following 1981 containment orders because the government that issued them collapsed[cite: 533]. Tragic, strictly disciplined, and heavily armed.
3. [cite_start]**The Kafedra ("The Chair"):** Transhumanist ex-scientists and cultists who worship the Reality Distortion Field[cite: 534]. They use biological modification and artifacts to adapt to the Zone, seeking to eventually cross the "Threshold."

---

## 3. GAMEPLAY ARCHITECTURE
[cite_start]The game operates on a bifurcated progression architecture to ensure long-term retention[cite: 164].

### Phase A: 3D Scavenge (60 Seconds!)
* [cite_start]**Mechanics:** A frantic, 60-second real-time scavenge before an "Emission" wipes the surface[cite: 73, 160].
* **Tech:** Instantaneous trigger-based pickups and Kinematic manipulation (`Vector3.SmoothDamp`). [cite_start]Dynamic physics (Rigidbodies) are strictly avoided to prevent collision snagging and latency during the time crunch[cite: 160, 161]. 
* [cite_start]**Goal:** Grab items, crew, and documents, then throw them down the bunker hatch[cite: 74, 80].

### Phase B: 2D Bunker Survival (Darkest Dungeon)
* [cite_start]**Mechanics:** Day-to-day resource management, sanity tracking, and text-based expedition resolution[cite: 138].
* **Expeditions:** Sending crew into the Zone off-screen. Load weight affects speed and encounter rates. [cite_start]Gas masks mitigate radiation[cite: 74].
* **Sanity & Afflictions:** Crew subjected to horrors lose Sanity and gain permanent traits (e.g., Paranoid, Hollow, Compromised).
* [cite_start]**Meta-Progression:** Death is permanent for crew, but banked resources fund permanent bunker upgrades, ensuring productive failure and high player retention (D7/D30 metrics)[cite: 134, 135, 145].

---

## 4. ANOMALIES & MUTANTS
Anomalies bend physical and administrative rules. Mutants apply psychological pressure.

* **The Carbon Copy (Anomaly):** Invisibly duplicates items. Players might bring back 3 medical kits, only to find in Phase B that two are corrupted "copies" with fatal defects.
* **The Interview (Anomaly):** A room that is larger on the inside. Sitting at a desk triggers a terrifying text-based interview with an unseen entity that permanently alters stats.
* **The Backlog (Anomaly):** A temporal distortion. Crew members sent on expeditions who hit a backlog return days/weeks late, un-aged, carrying cold food.
* **The Drowned Census-Taker (Mutant):** Reanimated Scale Society clerks who slowly follow the player in flooded zones. If they catch you, they write your name on a clipboard, applying a permanent "Registered" debuff.
* **The Editor (Mutant):** A reality-bending entity with a paper face. Looking at it causes your HUD/inventory to glitch, replacing items you scavenged with random junk.

---

## 5. TECHNICAL STACK & DATA SCHEMAS
* **Render Pipeline:** Unity 6 URP Render Graph. [cite_start]Uses a custom `ScriptableRenderPass` to share Depth Buffers between 3D raster passes and 2D sprite lighting passes (true 2.5D integration without camera stacking)[cite: 90, 93, 94].
* [cite_start]**Data Layer:** Strict separation of concerns[cite: 259, 260]. [cite_start]Game definitions (Items, Factions, Traits) are `ScriptableObjects`[cite: 255].
* [cite_start]**Event System:** Text expeditions are driven by JSON payloads containing narrative text, prerequisites, choices, and outcome deltas[cite: 257]. 
* **State Machine:** Governed by `GameStateMachine` navigating between `ScavengePhase3DState` and `SurvivalPhase2DState`. [cite_start]`RunData` persists the current loop, while `MetaProgressData` tracks overarching hub unlocks[cite: 253].

---

## 6. SENSORY DOCTRINE (AUDIO & VISUALS)
* [cite_start]**Audio Identity:** Silence is pressure, not absence[cite: 522, 523]. [cite_start]Bunker ambience relies on pipe ticks, concrete settling, fluorescent hums, and radio hiss[cite: 525, 526, 527, 530]. [cite_start]Geiger counters click organically, not electronically[cite: 279, 513].
* [cite_start]**Visual Palette:** Nicotine yellow, oxidized green, wet concrete gray, sodium vapor orange[cite: 543, 544, 545, 546]. [cite_start]No saturated neon or heroic lighting[cite: 549, 554]. 
* **UI Philosophy:** The UI must feel like a decaying Soviet government terminal, not a video game HUD. [cite_start]Analog, archival, and oppressive[cite: 669, 670, 671, 672, 673].