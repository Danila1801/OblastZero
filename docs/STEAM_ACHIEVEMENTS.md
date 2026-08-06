# Steam Achievements — Oblast Zero

**Status: 20 achievements, all wired to a real trigger.**

This document and `Assets/_Project/Scripts/Steam/SteamConfig.cs` are the two halves of one contract. The
API Name column is what goes into the Steamworks Admin panel, character for character. A key that does not
exist in the panel **fails silently** — `Achievement.Trigger()` returns without unlocking and without
throwing — so a mismatch here ships as an achievement nobody can ever earn, with nothing in the log.

Verify after any edit:

```bash
python tools/verify_steam_layer.py          # compile + type presence
grep -c 'ACH_' Assets/_Project/Scripts/Steam/SteamConfig.cs   # expect 20
```

---

## Design rules applied

**No spoilers in visible achievements.** The four endings are named by their in-game *alignment* —
Stabilization, Relief, Adaptation, Unaligned — not by what happens in them. A player reading the store page
learns the game has four resolutions and nothing about what any of them costs.

**Hidden means "the description is the solution."** Six achievements are hidden. Each states a constraint
("without using a medical item"), and knowing the constraint is most of the work of meeting it. Everything
that merely records progress is visible, because a list of grey boxes with no readable names tells a
prospective buyer nothing about the game.

**Every one has a trigger that exists today.** Nothing in this list is aspirational. Where a condition
could not be observed, the achievement was cut rather than stubbed — see *Deliberately not included*.

**Understate rather than overstate.** The four "won a run while never doing X" awards are evaluated only
for runs `SteamEventBridge` observed from `RunStartedEvent`. A run resumed from disk did not raise that
event, so its accumulators are untrusted and those four are skipped. A missed unlock annoys one player; an
unearned one devalues the whole list for everyone.

---

## Catalogue

### Progression — visible

| API Name | Display Name | Description | Trigger | Icon |
|---|---|---|---|---|
| `ACH_FIRST_FILING` | Initial Registration | Register your first expedition. | `RunStartedEvent` | Filing form with a wet stamp |
| `ACH_FIRST_CLOSURE` | File Closed | Lose a bunker. | `RunEndedEvent`, reason `AllCrewDead` or `BunkerBreach` | Folder tied shut with string |
| `ACH_CASE_RESOLVED` | Case Resolved | Survive to day 10. | `DayAdvancedEvent` ≥ 10 | Desk calendar, one date circled |
| `ACH_EXTENDED_FILING` | Extended Filing | Survive to day 30. | `DayAdvancedEvent` ≥ 30 | Thick file folder, edges worn |
| `ACH_PERMANENT_RECORD` | Permanent Record | Survive to day 60. | `DayAdvancedEvent` ≥ 60 | Archive box on a high shelf |
| `ACH_FIRST_RESOLUTION` | Matter Resolved | Reach any of the four resolutions. | `RunEndedEvent`, any `Victory*` | Rubber stamp: RESOLVED |

### Endings — visible

| API Name | Display Name | Description | Trigger | Icon |
|---|---|---|---|---|
| `ACH_ALIGNMENT_STABILIZATION` | Alignment: Stabilization | Conclude a run under the Scale Society. | `VictoryStabilization` | Scale Society emblem |
| `ACH_ALIGNMENT_RELIEF` | Alignment: Relief | Conclude a run under the Cordon. | `VictoryRelief` | Cordon emblem |
| `ACH_ALIGNMENT_ADAPTATION` | Alignment: Adaptation | Conclude a run under the Kafedra. | `VictoryAdaptation` | Kafedra emblem |
| `ACH_ALIGNMENT_UNALIGNED` | Alignment: Unaligned | Conclude a run affiliated with nobody. | `VictoryIndependent` | Blank emblem, no device |
| `ACH_COMPLETE_ARCHIVE` | Complete Archive | Record all four resolutions. | All four ending achievements unlocked **on Steam** | Four emblems, filed in a row |

> `ACH_COMPLETE_ARCHIVE` reads Steam itself rather than `MetaProgressData.unlockedEndings`, so it survives a
> deleted local profile. A player who reinstalls has not un-seen three endings.

### Standing — visible

| API Name | Display Name | Description | Trigger | Icon |
|---|---|---|---|---|
| `ACH_STANDING_SOCIETY` | In Good Standing: Society | Reach endgame standing with the Scale Society. | rep ≥ `ENDGAME_REPUTATION_THRESHOLD` (60) | Countersigned letter |
| `ACH_STANDING_CORDON` | In Good Standing: Cordon | Reach endgame standing with the Cordon. | same, Cordon | Countersigned letter |
| `ACH_STANDING_KAFEDRA` | In Good Standing: Kafedra | Reach endgame standing with the Kafedra. | same, Kafedra | Countersigned letter |
| `ACH_DESIGNATION_HUNTED` | Designation: Hunted | Hold hunted status with all three factions at once. | all three ≤ `HUNTED_REPUTATION_THRESHOLD` (−60), evaluated on any rep change | Heavily redacted stamp |

> The thresholds come from `BalanceConstants`, not from literals in the bridge. Retuning the endgame
> threshold moves the achievement with it, which is the only behaviour that stays correct.

### Challenge — hidden

| API Name | Display Name | Description | Trigger | Icon |
|---|---|---|---|---|
| `ACH_FULL_ROSTER` | Full Roster | Conclude a run with no personnel lost. | victory **and** no `CrewDiedEvent` since run start | Group photograph, nobody crossed out |
| `ACH_RESOURCEFUL` | Resourceful | Conclude a run without drawing on medical stores. | victory **and** no `BunkerItemConsumedEvent` of category `Medical` | Unopened medical tin |
| `ACH_EXPRESS_FILING` | Express Filing | Conclude a run on or before day 20. | victory **and** `currentDay ≤ 20` | Stopwatch on a desk blotter |
| `ACH_LOGISTICS_SPECIALIST` | Logistics Specialist | Leave a site carrying 95% of capacity or more. | peak `ScavengeLoadChangedEvent` fraction ≥ 0.95 during a won run | Platform scale at its limit |
| `ACH_ARTIFACT_COLLECTOR` | Artifact Collector | Recover five artifacts. | `stat_artifacts_found_total` ≥ 5 (cross-run) | Object in a specimen jar |
| `ACH_PROVISIONING_COMPLETE` | Provisioning Complete | Obtain every entry in the supply office catalogue. | `purchasedUnlockIds.Count ≥ MetaUnlockCatalog.All.Count` | Requisition book, every line ticked |

---

## Deliberately not included

Three achievements from the original design brief were cut because no trigger exists for them. They are
listed so the decision is visible rather than looking like an oversight, and so that adding the trigger is
a known follow-up rather than a rediscovery.

| Proposed | Why cut | What it would need |
|---|---|---|
| Escape the Backlog anomaly without losing time | `BacklogStateChangedEvent` reports entry and exit and the dilation factor, but nothing accumulates *time lost*, so "without losing time" has no measurable definition. | A cumulative dilated-seconds figure on the Backlog zone, raised on exit. |
| Complete the Interview anomaly unharmed | The interview's completion path does not raise a distinct event; `AnomalyRewardEvent` fires for the payout, which is not the same condition. | A completion event carrying whether stats were docked. |
| Be registered by the Drowned Census-Taker | `PlayerRegisteredEvent` exists and would work. Cut on design grounds, not technical: an achievement for suffering a penalty reads as encouragement to seek it out, and the Census-Taker is a hazard the phase is tuned around avoiding. | Nothing technical — reinstate if the design intent changes. |

---

## Before the store page goes live

1. Replace App ID `480` in `SteamConfig.asset` **and** in `steam_appid.txt`. They must match; the file is
   what the process reads when launched outside the Steam client.
2. Create all 20 achievements in the Admin panel with the API names above, exactly.
3. Mark the six challenge achievements hidden.
4. Upload icons (256×256 achieved, 256×256 locked/grey).
5. Publish the stats and achievements configuration — **unpublished configuration does not reach clients**,
   and the symptom is identical to a typo: nothing unlocks and nothing logs.
