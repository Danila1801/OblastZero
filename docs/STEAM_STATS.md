# Steam Stats — Oblast Zero

**15 stats, all INT, all wired.** The API Name column is what goes into the Steamworks Admin panel.

Steam supports float stats. Nothing here is fractional, and an int survives a partial write unambiguously
and renders correctly in the panel's own charts, so every stat is an int.

Source of truth: `Assets/_Project/Scripts/Steam/SteamConfig.cs`. Written by
`Assets/_Project/Scripts/Steam/SteamEventBridge.cs`.

---

## Cumulative

| API Name | Type | Increment | Written on |
|---|---|---|---|
| `stat_total_runs` | INT | +1 | `RunStartedEvent` |
| `stat_runs_ended` | INT | +1 | `RunEndedEvent` |
| `stat_total_wins` | INT | +1 | `RunEndedEvent` with any `Victory*` reason |
| `stat_days_survived_total` | INT | +1 | every `DayAdvancedEvent` |
| `stat_deaths_total` | INT | +1 | `CrewDiedEvent` |
| `stat_items_scavenged_total` | INT | +1 | `ItemPickedUpEvent` |
| `stat_crew_rescued_total` | INT | +1 | `CrewRescuedEvent` |
| `stat_artifacts_found_total` | INT | +1 | `ItemPickedUpEvent` where the item's category is `Artifact` |

## Per-ending tallies

| API Name | Type | Written on |
|---|---|---|
| `stat_wins_by_stabilization` | INT | `VictoryStabilization` |
| `stat_wins_by_relief` | INT | `VictoryRelief` |
| `stat_wins_by_adaptation` | INT | `VictoryAdaptation` |
| `stat_wins_by_independent` | INT | `VictoryIndependent` |

The four tallies always sum to `stat_total_wins`. That redundancy is deliberate: it is the cheapest
available check that the ending branch fired the arm it thinks it did, and a divergence in the panel's
charts is visible without instrumenting anything.

## Records and balances

| API Name | Type | Semantics | Written on |
|---|---|---|---|
| `stat_longest_run_days` | INT | max, never decreases | `DayAdvancedEvent` and `RunEndedEvent`, via `SetIntIfHigher` |
| `stat_highest_rep` | INT | max across all factions and runs | `FactionReputationChangedEvent`, via `SetIntIfHigher` |
| `stat_salvage_tokens_earned` | INT | mirrors `MetaProgressData.lifetimeSalvageTokens` | `RunEndedEvent`, via `SetIntIfHigher` |

`stat_salvage_tokens_earned` uses `SetIntIfHigher` against the profile's lifetime total rather than
incrementing per award. Incrementing would double-count on a run resumed after a crash — the profile
already holds the authoritative lifetime figure, so mirroring it is both simpler and correct under replay.

---

## Panel setup

1. Create each stat with the exact API name above, type INT, default 0.
2. Leave "Increment only" **off** for the three `SetIntIfHigher` stats — the service writes an absolute
   value for those, and an increment-only stat would reject the write.
3. Publish the configuration. Unpublished stats accept `SetStat` locally and never appear anywhere, which
   looks exactly like a typo.

## Verifying at runtime

Stats are batched by Steam and flushed by `StoreStats()`. To see a value change during a Play-mode run,
watch the console: `SteamStatsService` logs every write. Without the `STEAMWORKS` define every call is a
no-op and the log stays silent — which is the expected state in the Editor unless the define is set for
Standalone and the Editor is running with it.
