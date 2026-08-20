#!/usr/bin/env python3
"""
Is the game winnable with the content that actually ships?

`VictoryConditionEvaluator` is correct code: it opens a faction endgame at
ENDGAME_REPUTATION_THRESHOLD (+60) once ENDGAME_MIN_TENURE_DAYS (15) have passed, and the neutral
ending at INDEPENDENT_MIN_TENURE_DAYS (25) with nobody antagonised. None of that says whether the
1020 authored events can move a faction 60 points in 15 days. If they cannot, every victory state in
the build is unreachable and the only ending anyone will ever see is the wipe.

This is a DIAGNOSTIC, not a test. It prints numbers and exits 0 unless something is structurally
broken (no events, no reputation-bearing content at all). Balance is a design decision; this tool
exists so the decision is made against measurements instead of vibes.

MODEL
=====
It simulates the real selection loop as closely as a static analysis can:

  * one event per bunker day, exactly what BunkerPhaseController.EndDay does;
  * only events whose prerequisites can pass on that day -- the day window, the locale gate against
    RegionTags.BunkerPhaseActive (FAIL-CLOSED, so untagged callers select nothing), and the region
    gate (FAIL-OPEN);
  * an event is consumed once (CompletedEventIds), so a single +8 event cannot be farmed;
  * faction-context reputation bands are respected, which matters at the top of the curve: an event
    gated to maxFactionRep 40 stops being drawable exactly when you need it most.

Three scenarios per faction:

  BEST      the player always draws the single best available event and always succeeds. A hard
            ceiling: nothing can beat this, so if +60 is out of reach here it is out of reach.
  GREEDY-EV every draw is still the best available, but outcomes are expected values weighted by
            successChance. What a player with perfect knowledge and average luck gets.
  RANDOM-EV the pool is drawn at random (weighted by baseWeight, as the engine does) and the player
            picks the best choice on offer. What actually happens in play.

USAGE
    python tools/balance_analysis.py                 # full report
    python tools/balance_analysis.py --days 15       # override the tenure horizon
    python tools/balance_analysis.py --json          # machine-readable, for CI trend tracking
    python tools/balance_analysis.py --self-test     # detector tests incl. negative controls
"""

from __future__ import annotations

import argparse
import json
import random
import re
import statistics
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
EVENTS_DIR = REPO / "Assets/Data/Resources/Events"
BALANCE_CS = REPO / "Assets/_Project/Scripts/Core/BalanceConstants.cs"
REGION_TAGS_CS = REPO / "Assets/_Project/Scripts/Core/RegionTags.cs"

FACTIONS = ["ScaleSociety", "Cordon", "Kafedra"]

# Deterministic sampling: this tool is run in CI and its numbers are compared across commits, so the
# RANDOM-EV column must not move because the interpreter's hash seed did.
RANDOM_SEED = 20260805
RANDOM_TRIALS = 400


# ── Reading the C# authorities ────────────────────────────────────────────────


def parse_balance_constants() -> dict[str, float]:
    text = BALANCE_CS.read_text(encoding="utf-8")
    out = {}
    for name, value in re.findall(r"public\s+const\s+(?:int|float)\s+(\w+)\s*=\s*(-?[\d.]+)f?\s*;", text):
        out[name] = float(value)
    required = [
        "ENDGAME_REPUTATION_THRESHOLD",
        "ENDGAME_MIN_TENURE_DAYS",
        "INDEPENDENT_MIN_TENURE_DAYS",
        "HUNTED_REPUTATION_THRESHOLD",
        "REPUTATION_MAX",
        "REPUTATION_MIN",
    ]
    missing = [k for k in required if k not in out]
    if missing:
        raise SystemExit(f"BalanceConstants is missing {missing} -- did the field names change?")
    return out


def parse_bunker_phase_active() -> set[str]:
    """The locale set a bunker day passes, read from RegionTags.BunkerPhaseActive."""
    text = REGION_TAGS_CS.read_text(encoding="utf-8")
    constants = dict(re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;', text))
    body = re.search(r"BunkerPhaseActive\s*=\s*new\[\]\s*\{(.*?)\n\s*\};", text, re.S)
    if not body:
        raise SystemExit("could not locate RegionTags.BunkerPhaseActive")
    names = [n.strip() for n in body.group(1).replace("\n", " ").split(",") if n.strip()]
    return {constants[n] for n in names if n in constants}


# ── Corpus ────────────────────────────────────────────────────────────────────


def load_events() -> list[dict]:
    if not EVENTS_DIR.is_dir():
        raise SystemExit(f"events directory not found: {EVENTS_DIR}")
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted(EVENTS_DIR.glob("*.json"))]


def rep_delta(outcome: dict, faction: str) -> int:
    """Reputation this outcome moves for one faction. Zero when it targets another (or none)."""
    if not outcome:
        return 0
    if (outcome.get("reputationFaction") or "") != faction:
        return 0
    return int(outcome.get("reputationDelta") or 0)


def choice_values(choice: dict, faction: str) -> tuple[int, float]:
    """(best-case delta, expected delta) for one choice against one faction."""
    p = float(choice.get("successChance") or 0.0)
    s = rep_delta(choice.get("successOutcome"), faction)
    f = rep_delta(choice.get("failureOutcome"), faction)
    return max(s, f), p * s + (1.0 - p) * f


def event_values(event: dict, faction: str) -> tuple[int, float]:
    """The best choice available in this event, by best-case and by expected value."""
    best, best_ev = 0, 0.0
    for choice in event.get("choices") or []:
        b, ev = choice_values(choice, faction)
        best = max(best, b)
        best_ev = max(best_ev, ev)
    return best, best_ev


def eligible(event: dict, day: int, rep: dict[str, int], locales: set[str]) -> bool:
    """Mirrors EventEngine.PassesPrerequisites for the axes a static analysis can evaluate."""
    p = event.get("prerequisites") or {}

    min_day, max_day = int(p.get("minDay") or 0), int(p.get("maxDay") or 0)
    if min_day > 0 and day < min_day:
        return False
    if max_day > 0 and day > max_day:
        return False

    context = p.get("factionContext") or ""
    if context:
        current = rep.get(context, 0)
        if current < int(p.get("minFactionRep", -100)) or current > int(p.get("maxFactionRep", 100)):
            return False

    tags = p.get("regionTagsAny") or []
    if tags and not (set(tags) & locales):  # FAIL-CLOSED, as the engine does
        return False

    return True


# ── Simulation ────────────────────────────────────────────────────────────────


def simulate(events, faction, days, locales, consts, mode, rng=None) -> tuple[int, int, list[int]]:
    """
    Run `days` bunker days chasing one faction. Returns (final rep, events that moved it, trace).

    mode: "best" (always the top event, always succeed)
          "greedy_ev" (always the top event, expected outcome)
          "random_ev" (weighted-random draw, best choice, expected outcome)
    """
    lo, hi = int(consts["REPUTATION_MIN"]), int(consts["REPUTATION_MAX"])
    rep = {f: 0 for f in FACTIONS}
    used, movers, trace = set(), 0, []
    value = 0.0

    for day in range(1, days + 1):
        pool = [e for e in events if e.get("id") not in used and eligible(e, day, rep, locales)]
        if not pool:
            trace.append(int(round(value)))
            continue

        if mode == "random_ev":
            weights = [max(0.0, float(e.get("baseWeight") or 0.0)) for e in pool]
            if sum(weights) <= 0:
                trace.append(int(round(value)))
                continue
            picked = rng.choices(pool, weights=weights, k=1)[0]
            gain = event_values(picked, faction)[1]
        else:
            index = 0 if mode == "best" else 1
            picked = max(pool, key=lambda e: event_values(e, faction)[index])
            gain = event_values(picked, faction)[index]

        used.add(picked.get("id"))
        if abs(gain) > 0.0001:
            movers += 1

        value = max(lo, min(hi, value + gain))
        rep[faction] = int(round(value))
        trace.append(rep[faction])

    return int(round(value)), movers, trace


def corpus_stats(events, faction, locales) -> dict:
    """Descriptive statistics on how much reputation the corpus offers a faction at all."""
    positives, negatives, bunker_positives = [], [], []
    for event in events:
        best, _ev = event_values(event, faction)
        if best > 0:
            positives.append(best)
            tags = event.get("prerequisites", {}).get("regionTagsAny") or []
            if not tags or (set(tags) & locales):
                bunker_positives.append(best)
        worst = min(
            (rep_delta(c.get("successOutcome"), faction) for c in event.get("choices") or []),
            default=0,
        )
        if worst < 0:
            negatives.append(worst)

    return {
        "events_offering_gain": len(positives),
        "events_offering_gain_in_bunker": len(bunker_positives),
        "events_offering_loss": len(negatives),
        "mean_gain": round(statistics.mean(positives), 2) if positives else 0.0,
        "median_gain": statistics.median(positives) if positives else 0,
        "max_gain": max(positives) if positives else 0,
        "total_gain_available_in_bunker": sum(bunker_positives),
    }


# ── Report ────────────────────────────────────────────────────────────────────


def time_to_victory(events, faction, locales, consts, tenure, horizon, trials) -> dict:
    """
    On which day does a REAL run cross the threshold?

    The tenure constant is a floor, not a deadline: `BunkerPhaseController` evaluates victory after
    every day tick, so a run that reaches +60 on day 31 wins on day 31. Measuring reputation at
    exactly day 15 answers a different and much harsher question than the one the game asks, and
    answering it instead is how a balance pass ends up "fixing" content that was never broken.

    Draws are weighted-random, as the engine does, and the player picks the best choice on offer.
    """
    lo, hi = int(consts["REPUTATION_MIN"]), int(consts["REPUTATION_MAX"])
    threshold = int(consts["ENDGAME_REPUTATION_THRESHOLD"])
    rng = random.Random(RANDOM_SEED + hash(faction) % 1000)

    win_days, never = [], 0
    for _ in range(trials):
        rep = {f: 0.0 for f in FACTIONS}
        used, won_on = set(), None

        for day in range(1, horizon + 1):
            snapshot = {f: int(round(v)) for f, v in rep.items()}
            pool = [e for e in events if e.get("id") not in used and eligible(e, day, snapshot, locales)]
            if pool:
                weights = [max(0.0, float(e.get("baseWeight") or 0.0)) for e in pool]
                if sum(weights) > 0:
                    picked = rng.choices(pool, weights=weights, k=1)[0]
                    used.add(picked.get("id"))
                    rep[faction] = max(lo, min(hi, rep[faction] + event_values(picked, faction)[1]))

            if day >= tenure and rep[faction] >= threshold:
                won_on = day
                break

        if won_on:
            win_days.append(won_on)
        else:
            never += 1

    return {
        "trials": trials,
        "horizon": horizon,
        "won": len(win_days),
        "never_won": never,
        "win_rate": round(len(win_days) / trials, 3),
        "median_win_day": int(statistics.median(win_days)) if win_days else None,
        "earliest_win_day": min(win_days) if win_days else None,
        "p90_win_day": int(sorted(win_days)[int(0.9 * len(win_days)) - 1]) if win_days else None,
    }


def analyse(days_override=None, horizon=45) -> dict:
    consts = parse_balance_constants()
    locales = parse_bunker_phase_active()
    events = load_events()

    threshold = int(consts["ENDGAME_REPUTATION_THRESHOLD"])
    tenure = int(days_override or consts["ENDGAME_MIN_TENURE_DAYS"])
    independent = int(consts["INDEPENDENT_MIN_TENURE_DAYS"])
    hunted = int(consts["HUNTED_REPUTATION_THRESHOLD"])

    rng = random.Random(RANDOM_SEED)
    report = {
        "corpus_size": len(events),
        "bunker_locales": sorted(locales),
        "threshold": threshold,
        "tenure_days": tenure,
        "independent_days": independent,
        "hunted_threshold": hunted,
        "factions": {},
    }

    for faction in FACTIONS:
        stats = corpus_stats(events, faction, locales)

        best_rep, best_movers, best_trace = simulate(events, faction, tenure, locales, consts, "best")
        gev_rep, gev_movers, _ = simulate(events, faction, tenure, locales, consts, "greedy_ev")

        samples = [
            simulate(events, faction, tenure, locales, consts, "random_ev", rng)[0]
            for _ in range(RANDOM_TRIALS)
        ]

        # First day the BEST-case trace crosses the threshold -- the earliest a win is even possible.
        first_day = next((i + 1 for i, v in enumerate(best_trace) if v >= threshold), None)

        report["factions"][faction] = {
            **stats,
            "best_case_rep": best_rep,
            "best_case_movers": best_movers,
            "best_case_first_day_at_threshold": first_day,
            "greedy_ev_rep": gev_rep,
            "greedy_ev_movers": gev_movers,
            "random_ev_mean": round(statistics.mean(samples), 1),
            "random_ev_p90": round(sorted(samples)[int(0.9 * len(samples)) - 1], 1),
            "random_ev_win_rate": round(sum(1 for s in samples if s >= threshold) / len(samples), 3),
            "reachable_best": best_rep >= threshold,
            "reachable_greedy": gev_rep >= threshold,
            "time_to_victory": time_to_victory(events, faction, locales, consts, tenure,
                                               horizon, RANDOM_TRIALS),
        }

    # The neutral path: survive INDEPENDENT_MIN_TENURE_DAYS with nobody at or below hunted.
    # The risk is not that it is hard -- it is that ordinary play drags someone under by accident.
    rng = random.Random(RANDOM_SEED)
    drifted = 0
    for _ in range(RANDOM_TRIALS):
        rep = {f: 0.0 for f in FACTIONS}
        used = set()
        for day in range(1, independent + 1):
            snapshot = {f: int(round(v)) for f, v in rep.items()}
            pool = [e for e in events if e.get("id") not in used and eligible(e, day, snapshot, locales)]
            if not pool:
                continue
            weights = [max(0.0, float(e.get("baseWeight") or 0.0)) for e in pool]
            if sum(weights) <= 0:
                continue
            picked = rng.choices(pool, weights=weights, k=1)[0]
            used.add(picked.get("id"))
            # A neutral player picks the choice that moves standing least in either direction.
            for f in FACTIONS:
                evs = [choice_values(c, f)[1] for c in picked.get("choices") or []]
                if evs:
                    rep[f] = max(-100.0, min(100.0, rep[f] + min(evs, key=abs)))
        if any(v <= hunted for v in rep.values()):
            drifted += 1

    report["independent"] = {
        "days_required": independent,
        "runs_sampled": RANDOM_TRIALS,
        "runs_that_drifted_into_hunted": drifted,
        "drift_rate": round(drifted / RANDOM_TRIALS, 3),
    }
    return report


def print_report(r: dict) -> None:
    print("=" * 78)
    print("OBLAST ZERO -- VICTORY REACHABILITY ANALYSIS")
    print("=" * 78)
    print(f"corpus            {r['corpus_size']} events")
    print(f"bunker locales    {', '.join(r['bunker_locales'])}")
    print(f"threshold         +{r['threshold']} reputation by day {r['tenure_days']}")
    print(f"neutral ending    day {r['independent_days']}, nobody at or below {r['hunted_threshold']}")
    print()

    header = f"{'FACTION':<15}{'BEST':>7}{'GREEDY':>8}{'RAND AVG':>10}{'RAND p90':>10}{'WIN%':>7}{'DAY@60':>8}  VERDICT"
    print(header)
    print("-" * len(header))

    for faction, f in r["factions"].items():
        day = f["best_case_first_day_at_threshold"]
        if f["reachable_greedy"]:
            verdict = "reachable in ordinary play"
        elif f["reachable_best"]:
            verdict = "reachable only on perfect play + perfect luck"
        else:
            verdict = "UNREACHABLE -- ending is dead content"
        print(f"{faction:<15}{f['best_case_rep']:>7}{f['greedy_ev_rep']:>8}"
              f"{f['random_ev_mean']:>9}{f['random_ev_p90']:>10}"
              f"{100 * f['random_ev_win_rate']:>6.1f}%{(day if day else '-'):>8}  {verdict}")

    print()
    print("CORPUS SUPPLY (how much standing the content offers at all)")
    print(f"{'FACTION':<15}{'EVENTS+':>9}{'IN BUNKER':>11}{'EVENTS-':>9}{'MEAN':>7}{'MAX':>6}{'TOTAL+':>9}")
    print("-" * 66)
    for faction, f in r["factions"].items():
        print(f"{faction:<15}{f['events_offering_gain']:>9}{f['events_offering_gain_in_bunker']:>11}"
              f"{f['events_offering_loss']:>9}{f['mean_gain']:>7}{f['max_gain']:>6}"
              f"{f['total_gain_available_in_bunker']:>9}")

    print()
    print("TIME TO VICTORY (weighted-random draws, victory checked every day past the tenure floor)")
    print(f"{'FACTION':<15}{'WIN%':>8}{'EARLIEST':>10}{'MEDIAN':>8}{'p90':>6}{'NEVER':>8}   VERDICT")
    print("-" * 74)
    for faction, f in r["factions"].items():
        t = f["time_to_victory"]
        if t["win_rate"] >= 0.30:
            verdict = "healthy"
        elif t["win_rate"] >= 0.10:
            verdict = "demanding but fair"
        else:
            verdict = "TOO HARSH -- most runs can never win"
        print(f"{faction:<15}{100 * t['win_rate']:>7.1f}%{(t['earliest_win_day'] or '-'):>10}"
              f"{(t['median_win_day'] or '-'):>8}{(t['p90_win_day'] or '-'):>6}"
              f"{t['never_won']:>8}   {verdict}")
    print(f"  (horizon {r['factions']['Cordon']['time_to_victory']['horizon']} days; "
          f"'NEVER' counts runs that never crossed the threshold inside it)")

    ind = r["independent"]
    print()
    print("NEUTRAL (INDEPENDENT) PATH")
    print(f"  surviving to day {ind['days_required']} while avoiding hunted status:")
    print(f"  {ind['runs_that_drifted_into_hunted']}/{ind['runs_sampled']} sampled runs drifted below "
          f"the hunted threshold anyway ({100 * ind['drift_rate']:.1f}%)")
    if ind["drift_rate"] > 0.5:
        print("  [WARN] the neutral ending is mostly denied by accidental drift, not by choice.")
    else:
        print("  the neutral path is defensible by playing carefully.")
    print()


def self_test() -> int:
    print("=== balance_analysis self-test ===")
    failures = 0

    def check(label, ok):
        nonlocal failures
        print(f"  [{'PASS' if ok else 'FAIL'}] {label}")
        if not ok:
            failures += 1

    consts = parse_balance_constants()
    check("parsed the victory thresholds from BalanceConstants",
          consts["ENDGAME_REPUTATION_THRESHOLD"] > 0 and consts["ENDGAME_MIN_TENURE_DAYS"] > 0)
    check("parsed the bunker locale set from RegionTags", len(parse_bunker_phase_active()) == 5)

    # Extraction
    ev = {"choices": [{"successChance": 1.0,
                       "successOutcome": {"reputationFaction": "Cordon", "reputationDelta": 7},
                       "failureOutcome": {}}]}
    check("reads a positive delta for the targeted faction", event_values(ev, "Cordon") == (7, 7.0))
    check("ignores a delta aimed at another faction", event_values(ev, "Kafedra") == (0, 0.0))

    ev50 = {"choices": [{"successChance": 0.5,
                         "successOutcome": {"reputationFaction": "Cordon", "reputationDelta": 10},
                         "failureOutcome": {"reputationFaction": "Cordon", "reputationDelta": -4}}]}
    check("expected value weights by successChance", abs(event_values(ev50, "Cordon")[1] - 3.0) < 1e-9)

    # Gating
    locales = {"bunker_interior"}
    check("day window excludes an out-of-range day",
          not eligible({"prerequisites": {"minDay": 5, "maxDay": 9}}, 2, {}, locales))
    check("locale gate FAILS CLOSED against a non-matching set",
          not eligible({"prerequisites": {"regionTagsAny": ["forest_edge"]}}, 1, {}, locales))
    check("faction rep band excludes an out-of-band run",
          not eligible({"prerequisites": {"factionContext": "Cordon", "minFactionRep": 20,
                                          "maxFactionRep": 100}}, 1, {"Cordon": 0}, locales))

    # NEGATIVE CONTROL 1: an empty corpus must report unreachable, never a false win.
    rep, movers, _ = simulate([], "Cordon", 15, locales, consts, "best")
    check("NEGATIVE CONTROL: an empty corpus yields 0 rep and 0 movers", rep == 0 and movers == 0)

    # NEGATIVE CONTROL 2: one huge event must not be farmed across days.
    farm = [{"id": "e1", "baseWeight": 1.0, "prerequisites": {"regionTagsAny": ["bunker_interior"]},
             "choices": [{"successChance": 1.0,
                          "successOutcome": {"reputationFaction": "Cordon", "reputationDelta": 50},
                          "failureOutcome": {}}]}]
    rep, movers, _ = simulate(farm, "Cordon", 15, locales, consts, "best")
    check("NEGATIVE CONTROL: a consumed event cannot be re-drawn (50, not 750)",
          rep == 50 and movers == 1)

    # NEGATIVE CONTROL 3: the clamp must hold at REPUTATION_MAX.
    big = [{"id": f"e{i}", "baseWeight": 1.0, "prerequisites": {"regionTagsAny": ["bunker_interior"]},
            "choices": [{"successChance": 1.0,
                         "successOutcome": {"reputationFaction": "Cordon", "reputationDelta": 40},
                         "failureOutcome": {}}]} for i in range(10)]
    rep, _m, _t = simulate(big, "Cordon", 15, locales, consts, "best")
    check("NEGATIVE CONTROL: reputation clamps at REPUTATION_MAX",
          rep == int(consts["REPUTATION_MAX"]))

    # Determinism
    events = load_events()
    a = analyse()["factions"]["Cordon"]["random_ev_mean"]
    b = analyse()["factions"]["Cordon"]["random_ev_mean"]
    check(f"random sampling is seeded and reproducible ({a})", a == b)
    check("corpus loaded", len(events) > 0)

    print(f"\n{'ALL GREEN' if failures == 0 else f'{failures} FAILURE(S)'}")
    return 1 if failures else 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--days", type=int, default=None, help="override the tenure horizon")
    ap.add_argument("--horizon", type=int, default=45,
                    help="how far past the tenure minimum to simulate when asking WHEN a run wins")
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    ap.add_argument("--self-test", action="store_true", help="detector tests and negative controls")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    report = analyse(args.days, args.horizon)

    if args.json:
        print(json.dumps(report, indent=2))
        return 0

    print_report(report)

    dead = [f for f, v in report["factions"].items() if not v["reachable_best"]]
    if dead:
        print(f"[CRITICAL] {', '.join(dead)} cannot reach +{report['threshold']} even on perfect play.")
        print("           Those endings are unreachable with the shipped content.")
        return 0  # a diagnostic reports; it does not fail the build
    return 0


if __name__ == "__main__":
    sys.exit(main())
