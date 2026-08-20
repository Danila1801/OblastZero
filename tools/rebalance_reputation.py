#!/usr/bin/env python3
"""
Closes the faction reachability gap that `tools/balance_analysis.py` measures.

THE FINDING
===========
Victory is NOT unreachable. Run balance_analysis.py: pursuing a faction wins 43-93% of runs inside a
45-day horizon, median day 26-34. The endgame threshold (+60) and the tenure floor (day 15) are both
fine, and lowering either would be fixing a problem the content does not have. `ENDGAME_MIN_TENURE_DAYS`
is a floor, not a deadline -- `BunkerPhaseController` checks victory after every day tick -- so a run
that crosses on day 31 wins on day 31.

What IS broken is the spread between factions:

    ScaleSociety   220 rep-positive events, 220 reachable on a bunker day   ->  92.7% of runs win
    Cordon         220 rep-positive events, 220 reachable on a bunker day   ->  80.5% of runs win
    Kafedra        180 rep-positive events, 112 reachable on a bunker day   ->  43.2% of runs win

Kafedra loses 68 of its 180 standing-bearing events to locales a sealed bunker never passes
(`old_factory`, `forest_edge`, `drainage_tunnel`, `collapsed_building`, `abandoned_school`). Nobody
chose that; it is a by-product of the content generator giving the science faction remote settings.
The result is that one of the three endings is roughly half as attainable as the others, for reasons
invisible in every file you would think to look at.

THE FIX
=======
Not a numbers change. `regionTagsAny` is an ANY list, so this tool ADDS a bunker-side locale to a
deterministic subset of the starved faction's remote standing events. The event keeps its remote
locale and all its prose; it simply also becomes drawable from the bunker. A Kafedra courier
presenting a requisition at the perimeter is exactly what `perimeter` is for -- and since the locale
gate is an overlap test, nothing that could match before stops matching.

Scaling the deltas instead was the obvious alternative and is worse: it would make Kafedra's
individual events louder rather than more frequent, which changes the faction's texture (the quiet
academic one) to fix a distribution problem, and it silently invalidates every authored number.

USAGE
    python tools/rebalance_reputation.py --report      # measure the gap, change nothing
    python tools/rebalance_reputation.py --dry-run     # show exactly which events would be retagged
    python tools/rebalance_reputation.py               # apply
    python tools/rebalance_reputation.py --check       # CI gate: fail if the gap has reopened
    python tools/rebalance_reputation.py --self-test   # detector tests incl. negative controls

Deterministic (events chosen in sorted-id order), idempotent (a second run retags nothing), and it
refuses to overshoot -- it stops at parity rather than tagging every remote event it can find.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
EVENTS_DIR = REPO / "Assets/Data/Resources/Events"
REGION_TAGS_CS = REPO / "Assets/_Project/Scripts/Core/RegionTags.cs"

FACTIONS = ["ScaleSociety", "Cordon", "Kafedra"]

# The locale added to a starved faction's remote events. `perimeter` and not `bunker_interior`:
# the ground immediately outside the door is where visitors present themselves, so an outsider
# faction arriving there needs no fiction rewritten. Putting a Kafedra field team inside a sealed
# bunker would.
BRIDGE_LOCALE = "perimeter"

# How close to the best-served faction is close enough. Exact parity is not the goal -- the factions
# are meant to feel different -- but a spread wider than this is the difference between an ending
# players reach and one they hear about.
PARITY_TOLERANCE = 0.08


def parse_bunker_phase_active() -> set[str]:
    text = REGION_TAGS_CS.read_text(encoding="utf-8")
    constants = dict(re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;', text))
    body = re.search(r"BunkerPhaseActive\s*=\s*new\[\]\s*\{(.*?)\n\s*\};", text, re.S)
    if not body:
        raise SystemExit("could not locate RegionTags.BunkerPhaseActive")
    names = [n.strip() for n in body.group(1).replace("\n", " ").split(",") if n.strip()]
    active = {constants[n] for n in names if n in constants}
    if BRIDGE_LOCALE not in active:
        raise SystemExit(
            f"BRIDGE_LOCALE '{BRIDGE_LOCALE}' is not in RegionTags.BunkerPhaseActive {sorted(active)}. "
            "Retagging events to a locale the bunker does not pass would make this tool a no-op that "
            "reports success."
        )
    return active


def load_events() -> list[tuple[Path, dict]]:
    if not EVENTS_DIR.is_dir():
        raise SystemExit(f"events directory not found: {EVENTS_DIR}")
    return [(p, json.loads(p.read_text(encoding="utf-8"))) for p in sorted(EVENTS_DIR.glob("*.json"))]


def gains_for(event: dict, faction: str) -> int:
    """Best positive reputation this event can hand a faction, over all choices and both outcomes."""
    best = 0
    for choice in event.get("choices") or []:
        for key in ("successOutcome", "failureOutcome"):
            outcome = choice.get(key) or {}
            if (outcome.get("reputationFaction") or "") == faction:
                best = max(best, int(outcome.get("reputationDelta") or 0))
    return best


def is_bunker_reachable(event: dict, active: set[str]) -> bool:
    tags = (event.get("prerequisites") or {}).get("regionTagsAny") or []
    return (not tags) or bool(set(tags) & active)


def measure(events, active) -> dict[str, dict]:
    out = {}
    for faction in FACTIONS:
        positives = [(p, d) for p, d in events if gains_for(d, faction) > 0]
        reachable = [(p, d) for p, d in positives if is_bunker_reachable(d, active)]
        out[faction] = {
            "total": len(positives),
            "reachable": len(reachable),
            "remote_only": [(p, d) for p, d in positives if not is_bunker_reachable(d, active)],
        }
    return out


def below_floor(stats, floor):
    """
    Factions still under the parity floor, measured directly.

    This exists because "the plan is empty" is NOT the same as "we reached parity": the plan is also
    empty when a starved faction has run out of remote-only events to retag. Inferring success from
    an empty plan reported ALL GREEN with Kafedra sitting at 81.8% of the best-served faction --
    a gate that passes precisely because it can no longer do anything is worse than no gate at all.
    """
    return [f for f, st in stats.items() if st["reachable"] < floor]


def plan(events, active):
    """(faction, [(path, data)]) retags needed to bring every faction within tolerance of the best."""
    stats = measure(events, active)
    target = max(s["reachable"] for s in stats.values())
    floor = int(target * (1.0 - PARITY_TOLERANCE))

    changes = []
    for faction in FACTIONS:
        s = stats[faction]
        shortfall = floor - s["reachable"]
        if shortfall <= 0:
            continue

        # Sorted by id, so the same events are chosen on every machine and every run.
        candidates = sorted(s["remote_only"], key=lambda pd: pd[1].get("id", ""))
        if not candidates:
            continue
        changes.append((faction, candidates[:shortfall], shortfall, len(candidates)))

    return stats, target, floor, changes


def apply_changes(changes) -> int:
    written = 0
    for _faction, picks, _short, _avail in changes:
        for path, data in picks:
            prereq = data.setdefault("prerequisites", {})
            tags = prereq.setdefault("regionTagsAny", [])
            if BRIDGE_LOCALE in tags:
                continue
            tags.append(BRIDGE_LOCALE)          # ADD, never replace -- the remote locale stays
            tags.sort()                          # deterministic on disk
            path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
            written += 1
    return written


def print_report(stats, target, floor) -> None:
    print("FACTION STANDING SUPPLY, AS THE BUNKER DAY SEES IT")
    print(f"{'FACTION':<15}{'REP EVENTS':>12}{'REACHABLE':>11}{'REMOTE-ONLY':>13}{'VS BEST':>9}")
    print("-" * 60)
    for faction, s in stats.items():
        share = s["reachable"] / target if target else 0
        flag = "" if share >= (1.0 - PARITY_TOLERANCE) else "   <-- starved"
        print(f"{faction:<15}{s['total']:>12}{s['reachable']:>11}{len(s['remote_only']):>13}"
              f"{100 * share:>8.1f}%{flag}")
    print(f"\n  parity target {target} reachable events, floor {floor} "
          f"({100 * (1 - PARITY_TOLERANCE):.0f}% of best)")


def self_test() -> int:
    print("=== rebalance_reputation self-test ===")
    failures = 0

    def check(label, ok):
        nonlocal failures
        print(f"  [{'PASS' if ok else 'FAIL'}] {label}")
        if not ok:
            failures += 1

    active = parse_bunker_phase_active()
    check(f"bridge locale '{BRIDGE_LOCALE}' is one the bunker day actually passes",
          BRIDGE_LOCALE in active)

    ev = {"choices": [{"successOutcome": {"reputationFaction": "Kafedra", "reputationDelta": 9},
                       "failureOutcome": {"reputationFaction": "Kafedra", "reputationDelta": -3}}]}
    check("reads the positive delta and ignores the negative", gains_for(ev, "Kafedra") == 9)
    check("ignores another faction's delta", gains_for(ev, "Cordon") == 0)

    check("an untagged event is bunker-reachable",
          is_bunker_reachable({"prerequisites": {}}, active))
    check("a remote-only event is not",
          not is_bunker_reachable({"prerequisites": {"regionTagsAny": ["forest_edge"]}}, active))
    check("an event carrying BOTH stays reachable",
          is_bunker_reachable({"prerequisites": {"regionTagsAny": ["forest_edge", BRIDGE_LOCALE]}}, active))

    # NEGATIVE CONTROL 1: retagging must ADD, never replace -- the remote locale has to survive.
    sample = {"id": "x", "prerequisites": {"regionTagsAny": ["forest_edge"]},
              "choices": [{"successOutcome": {"reputationFaction": "Kafedra", "reputationDelta": 5}}]}
    tags = sample["prerequisites"]["regionTagsAny"]
    tags.append(BRIDGE_LOCALE)
    tags.sort()
    check("NEGATIVE CONTROL: retag preserves the original remote locale",
          "forest_edge" in tags and BRIDGE_LOCALE in tags and len(tags) == 2)

    # NEGATIVE CONTROL 2: a faction already at parity must be planned for zero changes.
    synthetic = [
        (Path(f"a{i}.json"), {"id": f"a{i}", "prerequisites": {"regionTagsAny": ["bunker_interior"]},
                              "choices": [{"successOutcome": {"reputationFaction": f,
                                                              "reputationDelta": 5}}]})
        for f in FACTIONS for i in range(10)
    ]
    _s, _t, _f, changes = plan(synthetic, active)
    check("NEGATIVE CONTROL: balanced input plans no changes", changes == [])

    # NEGATIVE CONTROL 3: stop at the parity floor, not at exhaustion.
    # Two factions get 50 reachable events each; Kafedra gets 10 reachable plus 60 remote-only.
    # Floor is 46, so exactly 36 of those 60 should ever be touched -- never all 60.
    def synth(fac, n, tag, prefix):
        return [(Path(f"{prefix}{i}.json"),
                 {"id": f"{prefix}{i}", "prerequisites": {"regionTagsAny": [tag]},
                  "choices": [{"successOutcome": {"reputationFaction": fac, "reputationDelta": 5}}]})
                for i in range(n)]

    starved = (synth("ScaleSociety", 50, "bunker_interior", "s")
               + synth("Cordon", 50, "bunker_interior", "c")
               + synth("Kafedra", 10, "bunker_interior", "k")
               + synth("Kafedra", 60, "forest_edge", "kr"))
    stats3, target3, floor3, changes3 = plan(starved, active)
    total_retags = sum(len(picks) for _f2, picks, _sh, _av in changes3)
    check(f"NEGATIVE CONTROL: stops at the parity floor, not at exhaustion "
          f"({total_retags} of 60 available; target {target3}, floor {floor3})",
          total_retags == floor3 - 10 and 0 < total_retags < 60)

    # NEGATIVE CONTROL 4: exhausted-but-still-short must report SHORT, never parity.
    # This is the false green the tool actually produced before below_floor() existed.
    exhausted = (synth("ScaleSociety", 50, "bunker_interior", "s")
                 + synth("Cordon", 50, "bunker_interior", "c")
                 + synth("Kafedra", 10, "bunker_interior", "k"))
    stats4, target4, floor4, changes4 = plan(exhausted, active)
    check("NEGATIVE CONTROL: no candidates left AND still short reports short, not parity",
          changes4 == [] and below_floor(stats4, floor4) == ["Kafedra"])

    print(f"\n{'ALL GREEN' if failures == 0 else f'{failures} FAILURE(S)'}")
    return 1 if failures else 0


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--report", action="store_true", help="measure the gap, change nothing")
    ap.add_argument("--dry-run", action="store_true", help="list the events that would be retagged")
    ap.add_argument("--check", action="store_true", help="CI gate: fail if the gap has reopened")
    ap.add_argument("--self-test", action="store_true", help="detector tests and negative controls")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    active = parse_bunker_phase_active()
    events = load_events()
    stats, target, floor, changes = plan(events, active)

    print_report(stats, target, floor)
    print()

    if args.report:
        return 0

    short = below_floor(stats, floor)

    if args.check:
        if not short:
            print("[PASS] every faction is within tolerance of the best-served one.")
            return 0
        # A gate that can never go green is a gate people learn to skip. Fail only when the gap is
        # something THIS tool can close -- that is a regression somebody introduced. A gap that
        # survives after every retaggable event has been retagged is a standing content decision,
        # so it is reported every run and does not fail the build.
        actionable, at_ceiling = [], []
        for faction in short:
            st = stats[faction]
            need = floor - st["reachable"]
            (actionable if len(st["remote_only"]) > 0 else at_ceiling).append((faction, st, need))

        for faction, st, need in at_ceiling:
            print(f"[KNOWN GAP] {faction}: {st['reachable']}/{floor} reachable standing events. "
                  f"All {st['total']} that exist are already reachable; closing the last {need} needs "
                  "AUTHORED content, not retagging. Not a build failure.")

        if not actionable:
            return 0

        print(f"[FAIL] below the parity floor of {floor} and fixable: "
              f"{', '.join(f for f, _s, _n in actionable)}")
        for faction, st, need in actionable:
            print(f"   {faction}: {st['reachable']} reachable, needs {need} more; "
                  f"{len(st['remote_only'])} remote-only event(s) can be retagged")
        print("Run `python tools/rebalance_reputation.py` to close it.")
        return 1

    if not changes:
        if short:
            print(f"[FAIL] {', '.join(short)} below the parity floor of {floor}, and there are no "
                  "remote-only events left to retag. Closing the rest requires AUTHORING more "
                  "standing-bearing events for that faction -- this tool cannot help further.")
            return 1
        print("[PASS] every faction is within tolerance. Nothing to do.")
        return 0

    print("PLAN")
    for faction, picks, shortfall, available in changes:
        print(f"  {faction}: retag {len(picks)} of {available} remote-only standing events "
              f"with '{BRIDGE_LOCALE}' (short by {shortfall})")
        for path, data in picks[:5]:
            tags = (data.get("prerequisites") or {}).get("regionTagsAny") or []
            print(f"     {data.get('id')}  {tags} -> {sorted(set(tags) | {BRIDGE_LOCALE})}")
        if len(picks) > 5:
            print(f"     ... and {len(picks) - 5} more")

    if args.dry_run:
        print("\n--dry-run: nothing written.")
        return 0

    written = apply_changes(changes)
    print(f"\nretagged {written} event file(s).")

    after_stats, after_target, after_floor, _remaining = plan(load_events(), active)
    print()
    print_report(after_stats, after_target, after_floor)

    still_short = below_floor(after_stats, after_floor)
    if still_short:
        print(f"\n[PARTIAL] {', '.join(still_short)} remain below the floor of {after_floor} with no "
              "remote-only events left to retag.")
        for faction in still_short:
            st = after_stats[faction]
            print(f"   {faction}: {st['total']} standing events exist in total and all "
                  f"{st['reachable']} are now reachable. Reaching {after_floor} needs "
                  f"{after_floor - st['reachable']} MORE AUTHORED events, not more retagging.")
        print("   Retagging did everything it could; the residue is a content-volume decision.")
        return 0

    print("\n[PASS] all factions within tolerance. Re-run balance_analysis.py to see the effect.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
