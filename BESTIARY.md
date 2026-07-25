# Oblast Zero — Bestiary & Anomaly Reference
# Generated from DESIGN_BIBLE_Сlaude.Opus4.7.md §5
# This is the canonical reference for all anomalies and mutants.

---

## ANOMALIES (3)

### 1. THE CARBON COPY (Углеродная Копия)
- **Classification:** ANM-Δ-07/CC
- **Field names:** "The copy," "the carbon," "the desk drawer"
- **Hazard type:** Duplicative
- **Effective radius:** ~2 cubic meters
- **Visible:** No — invisible until interacted with
- **Geiger detectable:** Yes — characteristic non-radioactive "double-click" pattern

**Phase A behavior:**
- Environmental anomaly, occupies small volume
- Player encounters by picking up an item within the anomaly's volume
- Item picks up correctly, but a duplicate appears in the same position
- If player picks up the duplicate, another appears
- Time pressure causes players to grab 3-4 "copies" of same item
- In Phase B, only one copy is original — others have subtle errors:
  - Tin of meat with wrong Cyrillic on label
  - Med kit with correct contents but syringes inject wrong fluid
  - Document signed by someone who couldn't have signed it

**Phase B behavior:**
- Referenced in expedition events
- Crew member recovering duplicate documents = trust decision

**Artifact drop:** Margin Note (item_margin_note)
- Forms inside undisturbed Carbon Copy anomalies
- Allows re-roll of one expedition event outcome per in-game week

**Counter-tactics:**
- Geiger counter detection (double-click pattern)
- Notice items in positions they weren't before

**Expedition log example:**
> "Marina says she found the medical cache I had marked on the map. She says she found it twice. She is not sure which of the two crates she brought back, or whether she brought back both, or whether she brought back neither. The crate on the table looks correct. The crate on the floor also looks correct. Marina has not slept."

---

### 2. THE INTERVIEW (Собеседование)
- **Classification:** ANM-Ψ-12/IV
- **Field names:** "The interview," "the questionnaire," "the office," "the long room"
- **Hazard type:** Cognitive
- **Effective radius:** Room-scale
- **Visible:** Partially — room interior is larger than exterior suggests
- **Geiger detectable:** No

**Phase A behavior:**
- Room-scale anomaly in interior spaces (offices, classrooms, canteens, Bureau Quarter)
- Room interior is *larger* than exterior would suggest (factor varies with Field intensity)
- Inside: single desk, chair, stack of forms
- Player can walk past safely
- Player can sit down → screen fades to black, timer pauses, text prompts appear:
  - Questions start mundane (name, service number, prior employment)
  - Follow-up questions are NOT mundane
  - Completing interview → return to room with paperwork + permanent buff/debuff
  - Refusing to sit or leaving during interview = safe but forfeits reward

**Phase B behavior:**
- Crew can be assigned to "visit the Interview" as late-campaign expedition type
- Outcomes: Margin Note, permanent affliction lifted, new permanent trait, never return, return as someone else

**Artifact drops:**
- Notarized Heart (item_notarized_heart) — reduces personal radiation accumulation by 50%
- Stamped Tongue (item_stamped_tongue) — one-time "official override" of any Scale Society event

**Counter-tactics:**
- Do not enter rooms that feel too large
- Cordon maps mark Interview rooms but rooms are not stationary

**Expedition log example:**
> "Yefim came back. He brought a form. The form has his name on it. The form has my name on it as well, in his handwriting, in the box marked 'next of kin.' I am not his next of kin. I do not know where he learned my middle name."

---

### 3. THE BACKLOG (Долговой Слой)
- **Classification:** ANM-Χ-21/BL
- **Field names:** "The backlog," "the wait," "the queue"
- **Hazard type:** Temporal
- **Effective radius:** Volumetric
- **Visible:** Yes — subtly distorted air, dust motes hang motionless
- **Geiger detectable:** No

**Phase A behavior:**
- Volumetric time-distortion anomaly
- Inside: subjective time runs 40x-100x slower than external time
- Player movement and interaction speed drop to a crawl
- Timer keeps running at normal speed
- Stepping into Backlog with 30s left = forfeited run
- Visible: distorted air, hanging dust motes — skilled players identify and avoid

**Phase B behavior:**
- Crew trapped in Backlog return *late* — days after expected
- Returning crew have not aged the same number of days as bunker
- Generates events: don't recognize new arrivals, rations shifted, relationships changed

**Artifact drops:** None directly
- Items left inside Backlog can *change*: perishables emerge fresh decades later, documents with wet ink, items with unwritten content

**Counter-tactics:**
- Visual identification (slow dust)
- Throw small object in (if it slows visibly = anomalous)
- Never enter under time pressure

**Expedition log example:**
> "Olga left for Vykhod-3 on day twelve. It is day twenty-one. She has not returned. The note she left at the Backlog edge, in her own handwriting, says she will be back on day fifteen. The note is dated tomorrow."

---

## MUTANTS (2)

### 4. THE DROWNED CENSUS-TAKER (Утопший Переписчик)
- **Classification:** MTN-Β-04/DC
- **Field names:** "The drowned man," "the wet clerk," "переписчик"
- **Behavior type:** SlowStalker
- **Move speed:** ~Walking pace
- **Sight range:** Line of sight
- **Aggro range:** Proximity-based (within ~10m)

**Phase A behavior:**
- Slow-moving humanoid mutant, formerly Scale Society census enforcer/clerk
- Killed in/near water (most commonly Reservoir), reanimated by Field
- Carries waterlogged clipboard and fountain pen
- Does NOT attack directly
- Follows the player, takes notes
- If player stops moving >10 seconds within line of sight:
  - Census-Taker catches up, raises clipboard
  - Begins *writing the player's name* (~15 seconds)
  - On completion: permanent stat penalty for remainder of run ("registered")
  - Multiple registrations stack
  - Penalty applies in both Phase A and Phase B

**Phase B behavior:**
- Appears in Reservoir, Census District, water-crossing expeditions
- Sanity-drain events first, combat events second
- "Wet clerk" sighting → small permanent Sanity hit
- Caught and registered → *Compromised* affliction

**Loot:** Waterlogged clipboard
- Contains partially filled registration forms for Zone inhabitants
- Sometimes includes player's own crew members
- Options: turn in to Scale Society (reputation), burn (morale boost), read carefully (unlock dialogue/events)

**Counter-tactics:**
- Keep moving. Do not stop.
- Firearms work but noise attracts other Drowned
- Edged weapons quieter but require proximity (proximity = clipboard range)
- Most reliable: never let one within 10 meters

**Expedition log example:**
> "Pavel reports a sighting near the eastern overflow. He says it was wearing a Scale coat. He says it was writing as he ran. He says he heard it speak — quietly, conversationally, as if confirming a spelling — and what it said was the name of his mother."

---

### 5. THE EDITOR (Редактор)
- **Classification:** MTN-Ψ-09/ED
- **Field names:** "The editor," "the corrector," "правка"
- **Behavior type:** PsychicHazard
- **Move speed:** Unknown (appears and disappears)
- **Sight range:** Line of sight (effect triggers on eye contact)
- **Aggro range:** N/A — does not pursue

**Phase A behavior:**
- Rare, mid-to-late-campaign mutant
- Humanoid silhouette, average adult height
- Wears partial remains of 1970s scholar's tweed jacket
- Face obscured by sheet of paper
- Does NOT attack player
- When Editor enters line of sight:
  - Player's HUD glitches
  - Inventory items progressively *redacted*, then *deleted*, then *replaced* with different items
  - Actual bunker inventory will differ from what player remembers
  - Effect proportional to how long Editor was on screen
- Cannot be killed by conventional means
- Can be *distracted* briefly by throwing certain documents (it stops to read)

**Phase B behavior:**
- Rare and unsettling expedition encounters
- Crew returns with inventory different from what they took
- Crew *traits* may be edited (Paranoid → Steady, or vice versa)
- Only known mechanism to remove afflictions *without* a curative event
- Both hazard and opportunity for desperate players

**Loot:** Final Draft (item_final_draft)
- Editor's "face" — the sheet of paper — sometimes falls
- Artifact-class item
- Used in bunker: permanently rewrite one stat of one crew member
- Destructive: consumed on use

**Counter-tactics:**
- Cover the screen. Look away.
- If player cannot see the Editor, inventory does not edit
- Phase A maps include rooms with broken mirrors, obscured doorways
- Phase B: high-literacy crew (Ex-Society Clerks, Ecologists) reduce Editor effects

**Expedition log example:**
> "Vera returned this morning. Her pack contains four tins of fish, one med kit, and a Cordon dispatch I did not authorize her to carry. Her pack, when I assigned it to her, contained six tins of fish, one med kit, and a Cordon dispatch I did not authorize her to carry. She does not remember the fish I am missing. She does not remember the fish she has. She does remember the dispatch, which is in her own handwriting, and which she insists she has been carrying since day one."

---

## ARTIFACTS REFERENCE (Bible-Specific)

| Artifact | Source | Effect | Item ID |
|---|---|---|---|
| Margin Note | Carbon Copy anomaly | Re-roll one expedition event outcome per week | item_margin_note |
| Notarized Heart | The Interview anomaly | -50% personal radiation accumulation | item_notarized_heart |
| Stamped Tongue | The Interview anomaly | One-time Scale Society event override | item_stamped_tongue |
| Final Draft | The Editor mutant | Permanently rewrite one crew stat (consumed) | item_final_draft |

## REGION TAGS (Bible — for event system)

Events should use these region tags (current events use wrong tags from pre-bible generation):

| Bible Region | Russian | Suggested tag |
|---|---|---|
| The Outer Cordon | Внешний Кордон | `outer_cordon` |
| The Census District | Перепись | `census_district` |
| The Reservoir | Водохранилище | `reservoir` |
| The Grain Belt | Зерновой Пояс | `grain_belt` |
| The Bureau Quarter | Бюро | `bureau_quarter` |
| The Inner Ring | Внутреннее Кольцо | `inner_ring` |
| The Threshold | Порог | `threshold` |

## TODO FOR OPUS 5 / NEXT SESSION

1. **Create AnomalyData .asset files in Unity Editor:**
   - `Assets/Data/Anomalies/Anomaly_CarbonCopy.asset`
   - `Assets/Data/Anomalies/Anomaly_Interview.asset`
   - `Assets/Data/Anomalies/Anomaly_Backlog.asset`
   - Reference the classification codes, hazard types, and artifact drop tables above

2. **Create MutantData .asset files in Unity Editor:**
   - `Assets/Data/Mutants/Mutant_DrownedCensusTaker.asset`
   - `Assets/Data/Mutants/Mutant_Editor.asset`

3. **Rename old IP-risky item files** — old files still exist alongside renamed versions. Need to delete the old ones after updating any event references:
   - `item_dormant_graviton.json` → `item_dormant_field_anchor.json`
   - `item_dormant_jellyfish.json` → `item_dormant_drift_bloom.json`
   - `item_dormant_moonlight.json` → `item_dormant_pale_register.json`
   - `item_inert_graviton.json` → `item_inert_field_anchor.json`
   - `item_inert_jellyfish.json` → `item_inert_drift_bloom.json`
   - `item_inert_moonlight.json` → `item_inert_pale_register.json`
   - `item_inert_night_star.json` → `item_inert_late_filing.json`
   - `item_pulsing_sparkler.json` → `item_pulsing_form_stamp.json`

4. **Update event region tags** — current events use generic tags (abandoned_school, old_factory) instead of bible regions (outer_cordon, reservoir, grain_belt, etc.)
