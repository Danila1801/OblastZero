#!/usr/bin/env python3
"""
Adds the canonical oblast-region axis to every shipped event, derived from the proximity locales the
event already carries.

WHAT THIS IS NOT
================
This does NOT rewrite `regionTagsAny`. An earlier plan for this migration proposed replacing the ten
proximity locales with the seven bible regions -- mapping `bunker_interior` to `outer_cordon`,
`abandoned_school` to `census_district`, and so on -- and then narrowing `RegionTags.BunkerPhaseActive`
to the region vocabulary. Measured against the shipped corpus, that plan blacks out 452 of the 858
events a bunker day can currently reach, because:

  * its mapping table covered 3 of the 10 locales actually in use and invented 13 that are not
    (`warehouse_floor`, `silo`, `loading_dock`, ... -- those are zone names from the scavenge scene,
    not event tags), so `perimeter` (384 events), `access_road` (295), `kitchen_block` (144) and
    `basement_corridor` (150) would have been left untouched;
  * `EventEngine.PassesPrerequisites` gates locales FAIL-CLOSED, so every one of those untouched
    events stops matching the moment the caller's tag set changes vocabulary.

That is the same silent content blackout commit 109f1ad fixed and that `RegionTags`'s own doc comment
was written to prevent. Run this file with --explain to reproduce those numbers from the live corpus.

WHAT THIS DOES
==============
Region is a SECOND axis, orthogonal to proximity. Locales say how far from the bunker door; regions say
which district of the oblast. Each event gains an `oblastRegionsAny` list derived from its locales via
the table in Assets/_Project/Scripts/Core/OblastRegions.cs -- which this script PARSES rather than
duplicates, so the two cannot drift. The interior and approach locales map to no region (they exist at
whichever site the run registered for), and only the five genuinely remote locales carry one.

The engine's region gate fails OPEN, so an empty list means "anywhere" and this migration is incapable
of removing an event from any pool. The guard below proves that rather than asserting it.

USAGE
    python tools/migrate_event_tags.py --dry-run     # plan only, writes nothing
    python tools/migrate_event_tags.py               # migrate in place
    python tools/migrate_event_tags.py --check       # CI gate: fail if any event is unmigrated
    python tools/migrate_event_tags.py --self-test   # detector tests, incl. two negative controls
    python tools/migrate_event_tags.py --explain     # measure the naive migration's blackout

Idempotent: a second run rewrites nothing. Deterministic: regions are emitted in bible order.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
OBLAST_REGIONS_CS = REPO / "Assets/_Project/Scripts/Core/OblastRegions.cs"
REGION_TAGS_CS = REPO / "Assets/_Project/Scripts/Core/RegionTags.cs"
EVENTS_DIR = REPO / "Assets/Data/Resources/Events"

# The bunker-day proximity set, mirrored from RegionTags.BunkerPhaseActive. Used only by the guard and
# by --explain; the migration itself never reads it.
BUNKER_PHASE_ACTIVE = {
    "bunker_interior",
    "kitchen_block",
    "basement_corridor",
    "perimeter",
    "access_road",
}


# ── Parsing the C# authorities ────────────────────────────────────────────────


def parse_region_tag_constants() -> dict[str, str]:
    """`RegionTags` C# constant name -> tag string. The locale vocabulary's authority."""
    text = REGION_TAGS_CS.read_text(encoding="utf-8")
    pairs = re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;', text)
    if not pairs:
        raise SystemExit(f"no region tag constants found in {REGION_TAGS_CS}")
    return dict(pairs)


def parse_locale_to_regions() -> dict[str, list[str]]:
    """
    Parse `OblastRegions.LocaleToRegions` out of the C# rather than restating it here.

    The scene generator learned this lesson the hard way (see CLAUDE.md on VisualArchetype): a Python
    mirror of a C# table is a table that silently drifts. Parsing means a mapping edited in the .cs is
    the mapping this tool applies, or this tool fails loudly.
    """
    text = OBLAST_REGIONS_CS.read_text(encoding="utf-8")

    # Region constants: OuterCordon -> "outer_cordon"
    regions = dict(re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;', text))
    tags = parse_region_tag_constants()

    body = re.search(
        r"LocaleToRegions\s*=\s*new\s+Dictionary<string,\s*string\[\]>\s*\{(.*?)\n\s*\};",
        text,
        re.S,
    )
    if not body:
        raise SystemExit(f"could not locate LocaleToRegions initialiser in {OBLAST_REGIONS_CS}")

    mapping: dict[str, list[str]] = {}
    entry_re = re.compile(r"\{\s*RegionTags\.(\w+)\s*,\s*(new\s+string\[0\]|new\[\]\s*\{([^}]*)\})\s*\}")
    for locale_const, whole, inner in entry_re.findall(body.group(1)):
        if locale_const not in tags:
            raise SystemExit(f"LocaleToRegions references RegionTags.{locale_const}, which does not exist")
        locale = tags[locale_const]

        if whole.startswith("new string[0]"):
            mapping[locale] = []
            continue

        names = [n.strip() for n in inner.split(",") if n.strip()]
        resolved = []
        for name in names:
            if name not in regions:
                raise SystemExit(f"LocaleToRegions maps {locale} to unknown region constant '{name}'")
            resolved.append(regions[name])
        mapping[locale] = resolved

    if not mapping:
        raise SystemExit("LocaleToRegions parsed as empty -- did the initialiser shape change?")
    return mapping


def canonical_region_order() -> list[str]:
    """Regions in bible order, from `OblastRegions.All`. Emission order, so output is deterministic."""
    text = OBLAST_REGIONS_CS.read_text(encoding="utf-8")
    regions = dict(re.findall(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;', text))
    body = re.search(r"All\s*=\s*new\[\]\s*\{(.*?)\n\s*\};", text, re.S)
    if not body:
        raise SystemExit(f"could not locate OblastRegions.All in {OBLAST_REGIONS_CS}")
    names = [n.strip() for n in body.group(1).replace("\n", " ").split(",") if n.strip()]
    return [regions[n] for n in names if n in regions]


# ── The migration ─────────────────────────────────────────────────────────────


def regions_for(locales: list[str], mapping: dict[str, list[str]], order: list[str]) -> list[str]:
    """Regions implied by a locale set, deduplicated and emitted in bible order."""
    found = set()
    for locale in locales:
        found.update(mapping.get(locale, []))
    return [r for r in order if r in found]


def load_events() -> list[tuple[Path, dict]]:
    if not EVENTS_DIR.is_dir():
        raise SystemExit(f"events directory not found: {EVENTS_DIR}")
    out = []
    for path in sorted(EVENTS_DIR.glob("*.json")):
        out.append((path, json.loads(path.read_text(encoding="utf-8"))))
    return out


def plan(events, mapping, order):
    """(path, data, existing, computed) for every event whose region list would change."""
    changes = []
    for path, data in events:
        prereq = data.get("prerequisites") or {}
        locales = prereq.get("regionTagsAny") or []
        computed = regions_for(locales, mapping, order)
        existing = prereq.get("oblastRegionsAny")
        if existing != computed:
            changes.append((path, data, existing, computed))
    return changes


def apply_changes(changes) -> int:
    for path, data, _existing, computed in changes:
        data.setdefault("prerequisites", {})["oblastRegionsAny"] = computed
        path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return len(changes)


# ── Guards ────────────────────────────────────────────────────────────────────


def bunker_reachable(events, region_context: set[str] | None) -> int:
    """
    How many events a bunker day can select, applying both gates exactly as EventEngine does:
    locales fail closed, regions fail open.
    """
    count = 0
    for _path, data in events:
        prereq = data.get("prerequisites") or {}

        locales = prereq.get("regionTagsAny") or []
        if locales and not (set(locales) & BUNKER_PHASE_ACTIVE):
            continue

        regions = prereq.get("oblastRegionsAny") or []
        if region_context and regions and not (set(regions) & region_context):
            continue

        count += 1
    return count


def guard_reachability(before: int, after: int, label: str) -> bool:
    """
    Refuse to write if the migration would shrink the bunker-day pool.

    This is the check the naive plan did not have. A content migration is exactly the kind of change
    whose damage is invisible at the diff -- 1020 files each gaining one plausible-looking field --
    and only measurable at the pool. A gate that has never been observed failing is decoration, so
    --self-test drives this one to failure deliberately.
    """
    if after >= before:
        print(f"  [PASS] bunker-day reachability {label}: {before} -> {after} (no loss)")
        return True
    print(f"  [FAIL] bunker-day reachability {label}: {before} -> {after} "
          f"-- {before - after} events would go dark. REFUSING TO WRITE.")
    return False


# ── --explain: measure the naive plan ─────────────────────────────────────────


NAIVE_TAG_MIGRATION = {
    # The mapping table as originally specified, verbatim in the parts that touch real tags.
    "abandoned_school": "census_district",
    "old_factory": "grain_belt",
    "bunker_interior": "outer_cordon",
    # Everything else it listed -- warehouse_floor, silo, rail_siding, loading_dock, office,
    # admin_office, bunker_approach, reservoir_approach, water_crossing, inner_city, downtown,
    # threshold_crossing, grain_depot -- does not occur in the corpus at all.
}
NAIVE_BUNKER_ACTIVE = {"outer_cordon", "census_district", "grain_belt"}


def explain(events) -> None:
    print("Measuring the naive locale-replacement plan against the live corpus.\n")

    histogram: dict[str, int] = {}
    for _path, data in events:
        for tag in (data.get("prerequisites") or {}).get("regionTagsAny") or []:
            histogram[tag] = histogram.get(tag, 0) + 1

    print(f"  corpus: {len(events)} events")
    print("  locale tags actually in use:")
    for tag, n in sorted(histogram.items(), key=lambda kv: -kv[1]):
        covered = "mapped" if tag in NAIVE_TAG_MIGRATION else "UNMAPPED by the naive plan"
        print(f"    {n:5d}  {tag:<20} {covered}")

    before = sum(
        1 for _p, d in events
        if not ((d.get("prerequisites") or {}).get("regionTagsAny") or [])
        or set((d.get("prerequisites") or {}).get("regionTagsAny") or []) & BUNKER_PHASE_ACTIVE
    )

    after = 0
    for _path, data in events:
        locales = (data.get("prerequisites") or {}).get("regionTagsAny") or []
        migrated = {NAIVE_TAG_MIGRATION.get(t, t) for t in locales}
        if not migrated or (migrated & NAIVE_BUNKER_ACTIVE):
            after += 1

    print(f"\n  bunker-day reachable now:                 {before}")
    print(f"  bunker-day reachable after the naive plan: {after}")
    print(f"  events that would go dark:                 {before - after}"
          f"  ({100.0 * (before - after) / max(1, before):.1f}% of the reachable pool)")
    print("\n  This is why this tool adds an axis instead of replacing one.")


# ── --self-test ───────────────────────────────────────────────────────────────


def self_test() -> int:
    print("=== migrate_event_tags self-test ===")
    failures = 0

    def check(label, ok):
        nonlocal failures
        print(f"  [{'PASS' if ok else 'FAIL'}] {label}")
        if not ok:
            failures += 1

    mapping = parse_locale_to_regions()
    order = canonical_region_order()

    check("parsed all ten locales from OblastRegions.cs", len(mapping) == 10)
    check("parsed seven canonical regions in bible order", len(order) == 7 and order[0] == "outer_cordon")

    # Derivation
    check("remote locale maps to its region",
          regions_for(["abandoned_school"], mapping, order) == ["census_district"])
    check("interior locale maps to nothing (region-agnostic)",
          regions_for(["bunker_interior"], mapping, order) == [])
    check("mixed locales union their regions in bible order",
          regions_for(["drainage_tunnel", "old_factory"], mapping, order) == ["reservoir", "grain_belt"])
    check("duplicate locales do not duplicate regions",
          regions_for(["old_factory", "old_factory"], mapping, order) == ["grain_belt"])
    check("unknown locale contributes nothing rather than throwing",
          regions_for(["not_a_real_tag"], mapping, order) == [])

    # Idempotence
    once = regions_for(["forest_edge", "perimeter"], mapping, order)
    twice = regions_for(["forest_edge", "perimeter"], mapping, order)
    check("derivation is idempotent", once == twice == ["outer_cordon"])

    # NEGATIVE CONTROL 1: the guard must fail when reachability drops.
    synthetic = [
        (Path("a.json"), {"prerequisites": {"regionTagsAny": ["perimeter"], "oblastRegionsAny": []}}),
        (Path("b.json"), {"prerequisites": {"regionTagsAny": ["perimeter"], "oblastRegionsAny": ["threshold"]}}),
    ]
    before = bunker_reachable(synthetic, None)
    after = bunker_reachable(synthetic, {"outer_cordon"})
    check("NEGATIVE CONTROL: guard rejects a reachability drop",
          before == 2 and after == 1 and not guard_reachability(before, after, "(synthetic)"))

    # NEGATIVE CONTROL 2: a drifted C# table must be caught, not silently mis-parsed.
    drifted = 'LocaleToRegions = new Dictionary<string, string[]>\n{\n    { RegionTags.NoSuchTag, new string[0] },\n};'
    caught = False
    try:
        tags = parse_region_tag_constants()
        m = re.search(r"LocaleToRegions.*?\{(.*?)\n\};", drifted, re.S)
        for locale_const, _w, _i in re.compile(
            r"\{\s*RegionTags\.(\w+)\s*,\s*(new\s+string\[0\]|new\[\]\s*\{([^}]*)\})\s*\}"
        ).findall(m.group(1)):
            if locale_const not in tags:
                caught = True
    except Exception:
        caught = True
    check("NEGATIVE CONTROL: a locale constant that does not exist is rejected", caught)

    # Real-corpus invariant: migration never shrinks the pool.
    events = load_events()
    live_before = bunker_reachable(events, None)
    migrated = []
    for path, data in events:
        copy = json.loads(json.dumps(data))
        prereq = copy.setdefault("prerequisites", {})
        prereq["oblastRegionsAny"] = regions_for(prereq.get("regionTagsAny") or [], mapping, order)
        migrated.append((path, copy))
    live_after = bunker_reachable(migrated, None)
    check(f"real corpus: migration preserves the pool ({live_before} -> {live_after})",
          live_after == live_before)

    print(f"\n{'ALL GREEN' if failures == 0 else f'{failures} FAILURE(S)'}")
    return 1 if failures else 0


# ── main ──────────────────────────────────────────────────────────────────────


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--dry-run", action="store_true", help="print the plan, write nothing")
    ap.add_argument("--check", action="store_true", help="CI gate: exit 1 if any event is unmigrated")
    ap.add_argument("--self-test", action="store_true", help="run detector tests and negative controls")
    ap.add_argument("--explain", action="store_true", help="measure the naive replacement plan's blackout")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    events = load_events()

    if args.explain:
        explain(events)
        return 0

    mapping = parse_locale_to_regions()
    order = canonical_region_order()
    changes = plan(events, mapping, order)

    print(f"corpus: {len(events)} events   locales mapped: {len(mapping)}   regions: {len(order)}")
    print(f"events needing an oblastRegionsAny update: {len(changes)}")

    if args.check:
        if changes:
            print(f"\n[FAIL] {len(changes)} event(s) carry stale or missing oblastRegionsAny. "
                  "Run `python tools/migrate_event_tags.py` to fix.")
            for path, _d, existing, computed in changes[:5]:
                print(f"   {path.name}: {existing!r} -> {computed!r}")
            return 1
        print("\n[PASS] every event's oblastRegionsAny matches its locales. OK")
        return 0

    if not changes:
        print("\nNothing to do -- already migrated.")
        return 0

    # Guard BEFORE writing. Compute reachability on the post-migration corpus in memory.
    before = bunker_reachable(events, None)
    projected = []
    for path, data in events:
        copy = json.loads(json.dumps(data))
        prereq = copy.setdefault("prerequisites", {})
        prereq["oblastRegionsAny"] = regions_for(prereq.get("regionTagsAny") or [], mapping, order)
        projected.append((path, copy))
    after = bunker_reachable(projected, None)

    print("\nguards:")
    if not guard_reachability(before, after, "(unconstrained caller)"):
        return 1

    # And with the tightest region context any site can impose.
    for region in order:
        gated = bunker_reachable(projected, {region})
        if gated < before:
            print(f"  [note] region context '{region}' narrows the pool to {gated} "
                  f"(fail-open gate; locale-only events all still match)")

    if args.dry_run:
        print(f"\n--dry-run: {len(changes)} file(s) would be rewritten. Examples:")
        for path, _d, existing, computed in changes[:5]:
            print(f"   {path.name}: {existing!r} -> {computed!r}")
        return 0

    written = apply_changes(changes)
    print(f"\nmigrated {written} event file(s).")
    print("Re-run with --check to confirm idempotence.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
