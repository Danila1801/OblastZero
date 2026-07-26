#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
rebalance_weights.py — recompute weightKg for every item in Oblast Zero.

WHY THIS EXISTS
---------------
The generated content set shipped with placeholder weights: mean 0.64 kg, max 3.15 kg, and a
12-gauge carbine that weighed 0.73 kg. With numbers like that the Blowout carry cap could never
bind — the entire Collapsed Grain Depot came to 16.59 kg against a 25 kg cap, so "what do I leave
behind" was not a decision the player ever had to make. This script gives every item a weight that
is plausible for what it is, so the cap in BalanceConstants.SCAVENGE_MAX_CARRY_WEIGHT_KG becomes a
live constraint.

TWO SOURCES, BOTH HANDLED
-------------------------
Items live in two places and a rebalance that touched only one would leave the level half-light:
  * Assets/Data/Resources/Items/*.json   — the bulk generated content (loaded by ItemJsonLoader)
  * Assets/Data/Definitions/Items/*.asset — hand-authored ScriptableObjects (8 of them, and they
    include the pry bar, service pistol and water flask the depot leans on)

DETERMINISM / IDEMPOTENCY
-------------------------
A weight is a pure function of (item id, display name, category). The within-band jitter comes from
md5(item id), never from randomness or the clock, so re-running the script is a no-op: same inputs,
byte-identical outputs. `--check` asserts exactly that and is safe to wire into CI.

Writers preserve each format's existing byte layout — JSON stays CRLF with no trailing newline and
2-space indent; .asset files get a single substituted number on their `weightKg:` line with Unity's
trailing-zero-trimmed float formatting, so Unity does not see a spurious reimport diff.

USAGE
-----
    python tools/rebalance_weights.py            # rewrite weights, print a report
    python tools/rebalance_weights.py --report   # print the report, write nothing
    python tools/rebalance_weights.py --check    # exit 1 if any file would change (idempotency/CI)
"""

from __future__ import annotations

import argparse
import glob
import hashlib
import json
import os
import re
import statistics
import sys
from collections import defaultdict

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
JSON_ITEM_GLOB = os.path.join(REPO_ROOT, "Assets", "Data", "Resources", "Items", "*.json")
ASSET_ITEM_GLOB = os.path.join(REPO_ROOT, "Assets", "Data", "Definitions", "Items", "*.asset")
SCENE_GEN = os.path.join(REPO_ROOT, "tools", "generate_scavenge_scene.py")
BALANCE_CONSTANTS = os.path.join(
    REPO_ROOT, "Assets", "_Project", "Scripts", "Core", "BalanceConstants.cs"
)

# ItemCategory enum order — .asset files store the category as its integer index.
CATEGORY_BY_INDEX = [
    "Food", "Water", "Medical", "Weapon", "Ammunition",
    "Tool", "Document", "Artifact", "Crafting", "Special",
]

# ─────────────────────────────────────────────────────────────────────────────
# The weight model
#
# Bands are (low_kg, high_kg). An item lands at a deterministic point inside its band, so a shelf of
# "Issued Tool Variant N" has texture rather than 40 identical numbers, while every carbine still
# weighs about what a carbine weighs.
# ─────────────────────────────────────────────────────────────────────────────

# Default band per category, used when no noun rule matches.
CATEGORY_BANDS = {
    "Weapon":     (1.50, 4.50),
    "Ammunition": (0.06, 0.60),
    "Tool":       (1.00, 5.00),
    "Medical":    (0.30, 1.50),
    "Food":       (0.20, 1.00),
    "Water":      (0.50, 1.50),
    "Document":   (0.05, 0.20),
    "Artifact":   (0.10, 0.50),
    "Crafting":   (0.20, 2.00),
    "Special":    (0.50, 2.00),
}

# Absolute clamp per category. Noun rules are allowed to sit outside the default band (a pistol is
# lighter than the Weapon band floor, a med kit heavier than most Medical), but nothing escapes here.
CATEGORY_HARD_LIMITS = {
    "Weapon":     (0.25, 5.00),
    "Ammunition": (0.02, 2.60),
    "Tool":       (0.15, 5.00),
    "Medical":    (0.05, 1.60),
    "Food":       (0.15, 1.10),
    "Water":      (0.40, 1.60),
    "Document":   (0.03, 0.25),
    "Artifact":   (0.08, 0.60),
    "Crafting":   (0.15, 2.10),
    "Special":    (0.10, 2.10),
}

# Noun rules: (category, regex, low_kg, high_kg, label). First match within the category wins, so
# order is significance order — put the specific before the general.
NOUN_RULES = [
    # ── Weapons ──────────────────────────────────────────────────────────────
    ("Weapon", r"\bl\.?m\.?g\b|\bmachine gun\b",              3.80, 4.60, "lmg"),
    ("Weapon", r"\bsniper\b|\bbolt-?action\b",                3.20, 4.00, "sniper/bolt-action"),
    ("Weapon", r"\bcarbine\b|\bshotgun\b|\bgauge\b",          3.00, 3.60, "carbine/shotgun"),
    ("Weapon", r"\brifle\b|\bsemi-?auto\b",                   3.00, 3.80, "rifle"),
    ("Weapon", r"\bsmg\b|\bsubmachine\b",                     2.40, 3.00, "smg"),
    ("Weapon", r"\bpistol\b|\brevolver\b|\bsidearm\b",        0.85, 1.20, "pistol"),
    ("Weapon", r"\bsledge\b|\bmaul\b|\bpickaxe\b",            3.20, 4.20, "sledge"),
    ("Weapon", r"\baxe\b|\bhatchet\b",                        2.80, 3.60, "axe"),
    ("Weapon", r"\bcrowbar\b|\bpipe\b|\bclub\b|\bbaton\b",    1.30, 2.20, "blunt"),
    ("Weapon", r"\bmachete\b|\bbayonet\b",                    0.55, 0.95, "long blade"),
    ("Weapon", r"\bknife\b|\bblade\b|\bshiv\b|\bdagger\b",    0.25, 0.55, "knife"),
    ("Weapon", r"\bbow\b|\bcrossbow\b",                       1.60, 2.60, "bow"),

    # ── Ammunition ───────────────────────────────────────────────────────────
    ("Ammunition", r"\bbox\b|\bcrate\b|\bcase\b|\bcarton\b|\btin\b", 1.60, 2.40, "ammo box"),
    ("Ammunition", r"\bbelt\b|\bdrum\b",                      2.00, 2.60, "belt/drum"),
    ("Ammunition", r"\bmagazine\b|\bclip\b",                  0.35, 0.70, "magazine"),
    ("Ammunition", r"\bflare\b",                              0.18, 0.30, "flare"),
    ("Ammunition", r"\barrow\b|\bbolt\b",                     0.04, 0.09, "arrow/bolt"),
    ("Ammunition", r"14\.5\s*mm|12\.7\s*mm",                  0.12, 0.20, "heavy round"),
    ("Ammunition", r"\brounds?\b|\bammunition\b|\bhandloaded\b|\bshell\b|\bgauge\b|mm\b",
                                                              0.04, 0.10, "loose rounds"),

    # ── Tools ────────────────────────────────────────────────────────────────
    ("Tool", r"\bgenerator\b|\bwinch\b|\bjack\b",             4.00, 5.00, "heavy plant"),
    ("Tool", r"\bradio\b|\btransceiver\b|\bset\b",            3.00, 4.00, "radio set"),
    ("Tool", r"\bbolt cutter\b|\bcutter\b",                   2.60, 3.40, "bolt cutter"),
    ("Tool", r"\bsaw\b|\bwrench\b|\bhammer\b|\bmallet\b",     1.60, 2.60, "shop tool"),
    ("Tool", r"\bpry ?bar\b|\bcrowbar\b|\bbreaker bar\b",     2.20, 2.80, "pry bar"),
    ("Tool", r"\bnight vision\b|\boptic\b|\bscope\b",         1.10, 1.80, "optics"),
    ("Tool", r"\bgeiger\b|\bdosimeter\b|\bcounter\b",         0.65, 0.95, "geiger counter"),
    ("Tool", r"\bmeter\b|\bgauge\b|\blevel\b|\bcompass\b",    0.45, 1.10, "instrument"),
    ("Tool", r"\btorch\b|\blantern\b|\bflashlight\b|\blamp\b", 0.80, 1.30, "light"),
    ("Tool", r"\bplier\b|\bchisel\b|\bfile\b|\bspanner\b",    0.35, 0.80, "hand tool"),
    ("Tool", r"\bdrill bit\b|\bbit\b|\bblade set\b",          0.18, 0.45, "consumable bit"),

    # ── Medical ──────────────────────────────────────────────────────────────
    ("Medical", r"\bkit\b",                                   1.00, 1.40, "med kit"),
    ("Medical", r"\bdrip\b|\bplasma\b|\bsaline\b",            0.70, 1.10, "drip"),
    ("Medical", r"\bsplint\b|\btourniquet\b|\bbrace\b",       0.35, 0.60, "splint"),
    ("Medical", r"\bbandage\b|\bdressing\b|\bgauze\b",        0.12, 0.25, "bandage"),
    ("Medical", r"\bantiseptic\b|\bsolution\b|\bwash\b",      0.25, 0.45, "antiseptic"),
    ("Medical", r"\bampule\b|\bampoule\b|\bvial\b|\bsyringe\b|\bautoinjector\b|\binjector\b",
                                                              0.06, 0.14, "ampule/syringe"),
    ("Medical", r"\bpill\b|\btablet\b|\bsedative\b|\bstimulant\b|\bcapsule\b",
                                                              0.06, 0.12, "pills"),

    # ── Food ─────────────────────────────────────────────────────────────────
    ("Food", r"\bcanned\b|\bcan\b|\btinned\b",                0.45, 0.65, "canned"),
    ("Food", r"\bprovisions?\b|\bmeal pack\b|\bration\b|\bfield meal\b|\bprovision pack\b",
                                                              0.35, 0.55, "ration"),
    ("Food", r"\bporridge\b|\bsemolina\b|\bwheat\b|\boat\b|\bbarley\b|\bmix\b",
                                                              0.55, 0.85, "dry bulk"),
    ("Food", r"\bperch\b|\bpike\b|\bbeef\b|\btongue\b|\bjerky\b|\bdried\b",
                                                              0.35, 0.60, "preserved protein"),
    ("Food", r"\bbar\b|\bcalorie\b|\bcompressed\b|\bconcentrated\b",
                                                              0.18, 0.32, "concentrate"),

    # ── Water (band-respecting, ordered by container size) ────────────────────
    ("Water", r"\bjerry ?can\b|\bjerry ?jug\b|\bjug\b",       1.35, 1.55, "jerrycan"),
    ("Water", r"\bthermos\b|\bglass bottle\b",                1.15, 1.40, "thermos"),
    ("Water", r"\bcanteen\b|\bbladder\b",                     0.95, 1.20, "canteen"),
    ("Water", r"\bflask\b|\bfield bottle\b|\btin bottle\b|\bbottle\b", 0.85, 1.10, "flask"),
    ("Water", r"\bpouch\b|\bsachet\b",                        0.45, 0.65, "pouch"),

    # ── Documents ────────────────────────────────────────────────────────────
    ("Document", r"\bdossier\b|\bledger\b|\barchive\b|\bfile\b", 0.14, 0.20, "dossier"),
    ("Document", r"\bmanifest\b|\breport\b|\baudit\b|\bcensus\b", 0.08, 0.14, "report"),
    ("Document", r"\bform\b|\bsheet\b|\border\b|\bregistration\b|\bpermit\b|\bnote\b",
                                                              0.04, 0.08, "single sheet"),

    # ── Artifacts (bureaucratic relics — paper, resin, small stone) ───────────
    ("Artifact", r"\bballast\b|\bgraviton\b|\banchor\b",      0.40, 0.55, "dense relic"),
    ("Artifact", r"\bfragment\b|\bshard\b|\bchip\b|\bmica\b", 0.10, 0.20, "fragment"),
    ("Artifact", r"\bbloom\b|\bdroplet\b|\becho\b|\bvortex\b|\bblanket\b", 0.12, 0.28, "diffuse relic"),

    # ── Crafting ─────────────────────────────────────────────────────────────
    ("Crafting", r"\bpipe\b|\btube\b|\bplate\b|\bbar stock\b", 1.20, 2.00, "stock metal"),
    ("Crafting", r"\bcircuit\b|\bboard\b|\bmodule\b",         0.30, 0.60, "electronics"),
    ("Crafting", r"\bwire\b|\bfabric\b|\binsulation\b|\bplastic\b|\bgasket\b|\brubber\b",
                                                              0.25, 0.55, "soft stock"),
    ("Crafting", r"\bepoxy\b|\badhesive\b|\bresin\b|\bcompound\b|\bchemical\b",
                                                              0.40, 0.80, "chemical"),
    ("Crafting", r"\bglass\b|\bscrap\b",                      0.30, 0.70, "scrap"),

    # ── Special ──────────────────────────────────────────────────────────────
    ("Special", r"\bcore\b|\bbeacon\b|\bgenerator\b",         1.20, 2.00, "core/beacon"),
    ("Special", r"\binstrument\b|\bmodule\b|\bdevice\b",      0.70, 1.30, "instrument"),
    ("Special", r"\bkey\b|\bchip\b|\btoken\b|\bseal\b|\bstamp\b", 0.10, 0.25, "key/token"),
]

# Quality/state prefixes scale the result. "Industrial" gear is built heavy; "Improvised" gear is
# made from less. Applied after the band draw, before the hard clamp.
PREFIX_MULTIPLIERS = [
    (r"\bindustrial\b",   1.25),
    (r"\bheavy\b",        1.30),
    (r"\breinforced\b",   1.20),
    (r"\bceremonial\b",   1.15),
    (r"\bissued\b",       1.05),
    (r"\bimprovised\b",   0.88),
    (r"\bcompact\b",      0.80),
    (r"\bfield\b",        0.95),
    (r"\bexpired\b",      0.95),
    (r"\bempty\b",        0.55),
]

COMPILED_NOUN_RULES = [
    (cat, re.compile(pat, re.IGNORECASE), lo, hi, label)
    for cat, pat, lo, hi, label in NOUN_RULES
]
COMPILED_PREFIXES = [(re.compile(p, re.IGNORECASE), m) for p, m in PREFIX_MULTIPLIERS]


def jitter(item_id: str) -> float:
    """Stable [0,1) position inside a band, derived from the item id. Never random, never time-based."""
    digest = hashlib.md5(item_id.encode("utf-8")).hexdigest()
    return int(digest[:8], 16) / float(0x100000000)


def compute_weight(item_id: str, display_name: str, category: str):
    """Return (weight_kg, rule_label) for one item. Pure function — this is what makes --check work."""
    haystack = "%s %s" % (display_name or "", item_id.replace("_", " "))

    band = None
    label = "category default"
    for cat, pattern, lo, hi, rule_label in COMPILED_NOUN_RULES:
        if cat == category and pattern.search(haystack):
            band, label = (lo, hi), rule_label
            break

    if band is None:
        band = CATEGORY_BANDS.get(category, (0.20, 1.00))

    lo, hi = band
    weight = lo + jitter(item_id) * (hi - lo)

    for pattern, multiplier in COMPILED_PREFIXES:
        if pattern.search(haystack):
            weight *= multiplier
            break

    hard_lo, hard_hi = CATEGORY_HARD_LIMITS.get(category, (0.02, 5.00))
    weight = max(hard_lo, min(hard_hi, weight))
    return round(weight, 2), label


# ─────────────────────────────────────────────────────────────────────────────
# Readers / writers — each preserves its format's existing byte layout exactly.
# ─────────────────────────────────────────────────────────────────────────────

def serialize_json_item(obj) -> bytes:
    """Match how these files already sit on disk: 2-space indent, CRLF, no trailing newline."""
    text = json.dumps(obj, indent=2, ensure_ascii=False)
    return text.replace("\n", "\r\n").encode("utf-8")


def unity_float(value: float) -> str:
    """Unity writes 2 not 2.0, and 0.55 not 0.550000. Match it so .asset files stay diff-clean."""
    text = ("%.2f" % value).rstrip("0").rstrip(".")
    return text if text else "0"


def load_json_items():
    items = []
    for path in sorted(glob.glob(JSON_ITEM_GLOB)):
        with open(path, "r", encoding="utf-8") as handle:
            obj = json.load(handle)
        items.append({
            "path": path,
            "kind": "json",
            "obj": obj,
            "id": obj.get("id") or os.path.splitext(os.path.basename(path))[0],
            "displayName": obj.get("displayName", ""),
            "category": obj.get("category", "Special"),
            "old": float(obj.get("weightKg", 0.0)),
        })
    return items


ASSET_ID_RE = re.compile(r"^  id: (\S+)\s*$", re.MULTILINE)
ASSET_NAME_RE = re.compile(r"^  displayName: (.*)$", re.MULTILINE)
ASSET_CAT_RE = re.compile(r"^  category: (\d+)\s*$", re.MULTILINE)
ASSET_WEIGHT_RE = re.compile(r"^(  weightKg: )([0-9.eE+-]+)[ \t]*$", re.MULTILINE)


def load_asset_items():
    items = []
    for path in sorted(glob.glob(ASSET_ITEM_GLOB)):
        with open(path, "r", encoding="utf-8", newline="") as handle:
            text = handle.read()

        id_match = ASSET_ID_RE.search(text)
        weight_match = ASSET_WEIGHT_RE.search(text)
        if not id_match or not weight_match:
            continue  # not an ItemData asset

        name_match = ASSET_NAME_RE.search(text)
        cat_match = ASSET_CAT_RE.search(text)
        cat_index = int(cat_match.group(1)) if cat_match else 0
        category = CATEGORY_BY_INDEX[cat_index] if 0 <= cat_index < len(CATEGORY_BY_INDEX) else "Special"

        items.append({
            "path": path,
            "kind": "asset",
            "text": text,
            "id": id_match.group(1),
            "displayName": (name_match.group(1).strip().strip("'\"") if name_match else ""),
            "category": category,
            "old": float(weight_match.group(2)),
        })
    return items


def apply(items, write: bool):
    """Assign new weights. Returns the list of items whose on-disk bytes would change."""
    changed = []
    for item in items:
        new_weight, label = compute_weight(item["id"], item["displayName"], item["category"])
        item["new"] = new_weight
        item["rule"] = label

        if item["kind"] == "json":
            payload = dict(item["obj"])
            payload["weightKg"] = new_weight
            new_bytes = serialize_json_item(payload)
            with open(item["path"], "rb") as handle:
                old_bytes = handle.read()
            if new_bytes != old_bytes:
                changed.append(item)
                if write:
                    with open(item["path"], "wb") as handle:
                        handle.write(new_bytes)
        else:
            new_text = ASSET_WEIGHT_RE.sub(
                lambda m: m.group(1) + unity_float(new_weight), item["text"], count=1
            )
            if new_text != item["text"]:
                changed.append(item)
                if write:
                    with open(item["path"], "w", encoding="utf-8", newline="") as handle:
                        handle.write(new_text)
    return changed


# ─────────────────────────────────────────────────────────────────────────────
# Reporting — the level manifest is the part that actually proves the cap binds.
# ─────────────────────────────────────────────────────────────────────────────

def read_carry_cap():
    """Pull the live cap out of BalanceConstants.cs so the report can never drift from the game."""
    try:
        with open(BALANCE_CONSTANTS, "r", encoding="utf-8") as handle:
            match = re.search(r"SCAVENGE_MAX_CARRY_WEIGHT_KG\s*=\s*([0-9.]+)", handle.read())
        return float(match.group(1)) if match else None
    except OSError:
        return None


def read_level_pickups():
    """Pull the depot's pickup manifest out of the scene generator (its PICKUPS table is the truth)."""
    try:
        with open(SCENE_GEN, "r", encoding="utf-8") as handle:
            source = handle.read()
    except OSError:
        return []

    start = source.find("PICKUPS = [")
    if start < 0:
        return []
    end = source.find("\n]", start)
    block = source[start:end]
    rows = re.findall(r'\(\s*"([a-z0-9_]+)"\s*,\s*(ITEM|CREW)\s*,\s*(\d+)', block)
    return [(data_id, int(qty)) for data_id, kind, qty in rows if kind == "ITEM"]


def report(items, changed, cap):
    by_id = {item["id"]: item for item in items}

    print("== ITEM WEIGHT REBALANCE " + "=" * 55)
    print("sources: %d JSON + %d .asset = %d items"
          % (sum(1 for i in items if i["kind"] == "json"),
             sum(1 for i in items if i["kind"] == "asset"),
             len(items)))
    print("files whose bytes differ from disk: %d" % len(changed))
    print()

    buckets = defaultdict(list)
    for item in items:
        buckets[item["category"]].append(item)

    print("%-12s %5s   %-22s   %-22s" % ("CATEGORY", "N", "BEFORE min/mean/max", "AFTER min/mean/max"))
    for category in sorted(buckets):
        group = buckets[category]
        old = [i["old"] for i in group]
        new = [i["new"] for i in group]
        print("%-12s %5d   %6.2f %6.2f %6.2f   %6.2f %6.2f %6.2f"
              % (category, len(group),
                 min(old), statistics.mean(old), max(old),
                 min(new), statistics.mean(new), max(new)))

    all_old = [i["old"] for i in items]
    all_new = [i["new"] for i in items]
    print("%-12s %5d   %6.2f %6.2f %6.2f   %6.2f %6.2f %6.2f"
          % ("OVERALL", len(items),
             min(all_old), statistics.mean(all_old), max(all_old),
             min(all_new), statistics.mean(all_new), max(all_new)))

    pickups = read_level_pickups()
    if not pickups:
        print("\n(scene generator not readable - skipping level manifest)")
        return

    print("\n== COLLAPSED GRAIN DEPOT -- LOOT MANIFEST " + "=" * 39)
    print("%-30s %3s %8s %9s   %s" % ("ITEM", "QTY", "KG_EACH", "KG_STACK", "RULE"))

    total_old = total_new = 0.0
    heavy_units = heavy_stacks = 0
    for data_id, qty in pickups:
        item = by_id.get(data_id)
        if item is None:
            print("%-30s %3d  *** not found in either source ***" % (data_id, qty))
            continue
        stack = item["new"] * qty
        total_new += stack
        total_old += item["old"] * qty
        if item["new"] > 2.0:
            heavy_units += 1
        if stack > 2.0:
            heavy_stacks += 1
        print("%-30s %3d %8.2f %9.2f   %s" % (data_id, qty, item["new"], stack, item["rule"]))

    print("-" * 78)
    print("total loot on the floor: %.2f kg   (was %.2f kg)" % (total_new, total_old))
    if cap:
        print("carry cap:               %.2f kg   -> the player can take %.0f%% of it"
              % (cap, 100.0 * cap / total_new))
        print("over-cap by:             %.2f kg" % (total_new - cap))
    print("pickups costing >2 kg:   %d   (single units >2 kg: %d)" % (heavy_stacks, heavy_units))

    if cap and total_new <= cap:
        print("\nWARNING: total loot fits inside the cap — the cap does not bind. Retune the bands.")


def main():
    parser = argparse.ArgumentParser(description="Rebalance Oblast Zero item weights.")
    parser.add_argument("--report", action="store_true", help="print the report, write nothing")
    parser.add_argument("--check", action="store_true",
                        help="exit 1 if any file would change (idempotency / CI gate)")
    args = parser.parse_args()

    items = load_json_items() + load_asset_items()
    if not items:
        print("no items found — is this being run from the repo?", file=sys.stderr)
        return 2

    write = not (args.report or args.check)
    changed = apply(items, write=write)
    report(items, changed, read_carry_cap())

    if args.check:
        if changed:
            print("\nCHECK FAILED: %d file(s) would change:" % len(changed))
            for item in changed[:10]:
                print("   %s  %.2f -> %.2f"
                      % (os.path.relpath(item["path"], REPO_ROOT), item["old"], item["new"]))
            if len(changed) > 10:
                print("   ... and %d more" % (len(changed) - 10))
            return 1
        print("\nCHECK PASSED: weights on disk already match the model (script is idempotent).")
        return 0

    if write:
        print("\nwrote %d file(s)." % len(changed))
    return 0


if __name__ == "__main__":
    sys.exit(main())
