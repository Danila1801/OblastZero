#!/usr/bin/env python3
"""
localization_qa.py — OblastZero localization-table gate (read-only)

Three gates already guard this project's text: content_qa.py reads the JSON
content set, csharp_string_qa.py reads string literals in C#, and neither reads
the localization tables at all. This closes that gap, and adds the checks that
only a multi-language table set needs.

WHAT IT CHECKS, AND WHY EACH ONE EARNED ITS PLACE
-------------------------------------------------
1. KEY COVERAGE.  Assets/_Project/Scripts/Core/UIStringKeys.cs declares, as
   compile-time constants, every key the game's own screens look up. Every one
   must exist in every shipped language table. This is the check that matters
   most, because a missing key does not throw, does not warn, and does not fail
   any other gate: LocalizedStrings.Get returns the key itself, so the button
   reads "menu_main_quit" and ships that way. In a language a developer does not
   read, nobody notices.

2. TABLE PARITY.  Every table carries exactly the same key set. A key present in
   English and absent from Russian renders as a raw key for Russian players only
   — the failure mode above, restricted to the audience least able to report it.
   An extra key in one table is a warning, not a failure: it is dead weight, not
   a defect.

3. PLACEHOLDER PARITY.  For every key, the set of {0}/{1} placeholders must be
   identical across languages. A translator who drops a {1} produces a
   FormatException inside a HUD refresh at runtime, which reads in play as a
   frozen screen, not as a bad string. This is the check that turns that into a
   build failure.

4. VALUE SANITY.  No empty values; no value that is just its own key (the
   classic result of scaffolding a table and never filling it in).

5. IP FIREWALL AND VOICE.  The same term lists content_qa.py uses, imported from
   it rather than copied — a second hand-maintained copy of the banned-noun list
   is a guarantee of divergence. Tier-1 proper nouns fail on any casing; tier-2
   ordinary words fail only on named-entity use; the four bible-named cliches
   fail, the broader pulp list warns.

   Note the deliberate asymmetry: the named-entity heuristic reasons about
   English capitalization, so it is applied to English values only. Tier-1 terms
   are checked in every language, because "Pripyat" is a violation in Cyrillic
   transliteration too and a plain substring match finds it.

Exit code 0 = clean, 1 = hard violations, 2 = usage/IO error.

Run `--self-test` for the negative control: synthetic tables carrying every
failure this tool claims to catch, plus a clean pair it must pass. CLAUDE.md §12
— a gate never observed failing is decoration.

Stdlib only, plus content_qa.py (a sibling). Modifies nothing.
"""
from __future__ import annotations

import argparse
import glob
import json
import os
import re
import sys
from typing import Dict, List, Set, Tuple

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import content_qa  # noqa: E402  (sibling module; path set above)

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir))
LOCALE_DIR = os.path.join(PROJECT_ROOT, "Assets", "Data", "Resources", "Locale")
KEYS_SOURCE = os.path.join(
    PROJECT_ROOT, "Assets", "_Project", "Scripts", "Core", "UIStringKeys.cs"
)

# The reference language. Parity and placeholders are measured against it.
REFERENCE_LANG = "en"

# Keys beginning with this are file metadata, never display strings. Mirrors
# LocalizationJsonLoader.MetadataKeyPrefix — the loader skips them, so the gate
# must too, or every table fails on its own _comment.
METADATA_PREFIX = "_"

# public const string Foo = "bar";
_CONST_RE = re.compile(
    r'public\s+const\s+string\s+\w+\s*=\s*"([^"]+)"\s*;'
)

# {0}, {1:0.0}, {0,-8} — the index is what must match across languages; the
# alignment and format specifier are presentation and may legitimately differ.
_PLACEHOLDER_RE = re.compile(r"\{(\d+)(?:[,:][^}]*)?\}")


# ---------------------------------------------------------------------------
# Loading
# ---------------------------------------------------------------------------
def required_keys(source_path: str = KEYS_SOURCE) -> Set[str]:
    """Every key declared in UIStringKeys.cs. That file is the authority."""
    with open(source_path, encoding="utf-8") as handle:
        text = handle.read()
    return set(_CONST_RE.findall(text))


def load_table(path: str) -> Dict[str, str]:
    """Display keys from a language table, with metadata keys dropped."""
    with open(path, encoding="utf-8") as handle:
        raw = json.load(handle)

    if not isinstance(raw, dict):
        raise ValueError(f"{os.path.basename(path)} is not a JSON object")

    return {
        key: value
        for key, value in raw.items()
        if key and not key.startswith(METADATA_PREFIX)
    }


def discover_tables(locale_dir: str = LOCALE_DIR) -> Dict[str, str]:
    """Language code -> file path, for every localization_<code>.json present."""
    tables: Dict[str, str] = {}
    if not os.path.isdir(locale_dir):
        return tables

    for name in sorted(os.listdir(locale_dir)):
        if not name.startswith("localization_") or not name.endswith(".json"):
            continue
        code = name[len("localization_"):-len(".json")]
        tables[code] = os.path.join(locale_dir, name)
    return tables


def placeholders(value: str) -> Set[str]:
    return set(_PLACEHOLDER_RE.findall(value))


# ---------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------
def check_coverage(required: Set[str], tables: Dict[str, Dict[str, str]]
                   ) -> List[str]:
    """Every key UIStringKeys declares exists in every table."""
    failures: List[str] = []
    for code in sorted(tables):
        missing = sorted(required - set(tables[code]))
        for key in missing:
            failures.append(
                f"localization_{code}.json: missing '{key}' "
                f"(declared in UIStringKeys.cs; would render as its raw key)"
            )
    return failures


def check_parity(tables: Dict[str, Dict[str, str]]) -> Tuple[List[str], List[str]]:
    """Every table carries the reference table's key set. Extras warn."""
    failures: List[str] = []
    warnings: List[str] = []

    reference = tables.get(REFERENCE_LANG)
    if reference is None:
        failures.append(
            f"no localization_{REFERENCE_LANG}.json — there is no reference "
            f"table to measure the others against"
        )
        return failures, warnings

    for code in sorted(tables):
        if code == REFERENCE_LANG:
            continue

        table = tables[code]
        for key in sorted(set(reference) - set(table)):
            failures.append(
                f"localization_{code}.json: missing '{key}' present in "
                f"localization_{REFERENCE_LANG}.json"
            )
        for key in sorted(set(table) - set(reference)):
            warnings.append(
                f"localization_{code}.json: '{key}' has no counterpart in "
                f"localization_{REFERENCE_LANG}.json (dead key)"
            )

    return failures, warnings


def check_placeholders(tables: Dict[str, Dict[str, str]]) -> List[str]:
    """Placeholder index sets match the reference language, per key."""
    failures: List[str] = []
    reference = tables.get(REFERENCE_LANG)
    if reference is None:
        return failures

    for code in sorted(tables):
        if code == REFERENCE_LANG:
            continue

        for key, value in sorted(tables[code].items()):
            if key not in reference:
                continue        # already reported by parity

            expected = placeholders(reference[key])
            actual = placeholders(value)
            if expected == actual:
                continue

            failures.append(
                f"localization_{code}.json: '{key}' has placeholders "
                f"{{{', '.join(sorted(actual)) or 'none'}}} but "
                f"localization_{REFERENCE_LANG}.json has "
                f"{{{', '.join(sorted(expected)) or 'none'}}} — "
                f"string.Format throws at runtime"
            )

    return failures


def check_values(tables: Dict[str, Dict[str, str]]) -> List[str]:
    """No empty values, no value that is merely a copy of its own key."""
    failures: List[str] = []
    for code in sorted(tables):
        for key, value in sorted(tables[code].items()):
            if not isinstance(value, str) or not value.strip():
                failures.append(
                    f"localization_{code}.json: '{key}' is empty — renders as "
                    f"a blank label, which reads as a broken screen"
                )
            elif value.strip() == key:
                failures.append(
                    f"localization_{code}.json: '{key}' has itself as its value "
                    f"— an unfilled scaffold, indistinguishable on screen from "
                    f"a missing key"
                )
    return failures


def check_voice(tables: Dict[str, Dict[str, str]]) -> Tuple[List[str], List[str]]:
    """IP firewall and voice, using content_qa's term lists."""
    failures: List[str] = []
    warnings: List[str] = []

    for code in sorted(tables):
        for key, value in sorted(tables[code].items()):
            if not isinstance(value, str):
                continue

            # Tier 1: proper nouns with no ordinary reading. Any casing, any
            # language — a transliteration is still the name.
            for term in content_qa.HARD_IP_TERMS:
                if content_qa._word_bound(term).search(value):
                    failures.append(
                        f"localization_{code}.json: '{key}' contains IP term "
                        f"'{term}' — {value[:70]!r}"
                    )

            # Tier 2 and the cliche lists reason about English capitalization
            # and English phrasing, so they run on the reference language only.
            if code != REFERENCE_LANG:
                continue

            for term in content_qa.CONTEXTUAL_IP_TERMS:
                for match in content_qa._word_bound(term, ignorecase=False).finditer(value):
                    if content_qa._is_named_entity_use(value, match):
                        failures.append(
                            f"localization_{code}.json: '{key}' uses '{term}' as "
                            f"a named entity — {value[:70]!r}"
                        )

            lowered = value.lower()
            for phrase in content_qa.HARD_CLICHES:
                if phrase in lowered:
                    failures.append(
                        f"localization_{code}.json: '{key}' contains the "
                        f"bible-§7 cliche '{phrase}'"
                    )
            for phrase in content_qa.SOFT_CLICHES:
                if content_qa._word_bound(phrase).search(value):
                    warnings.append(
                        f"localization_{code}.json: '{key}' uses '{phrase}' "
                        f"(pulp register — judgement call)"
                    )

    return failures, warnings


# ---------------------------------------------------------------------------
# Reporting
# ---------------------------------------------------------------------------
# ─────────────────────────────────────────────────────────────────────────────
# Screen coverage — the gap the other checks structurally cannot see
#
# check_coverage/parity/placeholders all reason about KEYS. They are silent about a screen that
# declares no keys at all, because from the tables' point of view such a screen does not exist.
# That is how five UI screens shipped as hardcoded English while every localization gate was green:
# nothing in the pipeline asked "does this screen look up any strings?"
#
# csharp_string_qa.py does not close it either — that is an IP/voice gate. Hardcoded English is
# perfectly in-voice, so it passes, correctly, and says nothing about translation.
#
# The debt is real and it is measured below. It is carried as an ALLOWLIST WITH EXACT COUNTS rather
# than as a red build, because a gate that cannot go green is a gate people learn to skip
# (CLAUDE.md, the same reasoning that made the Kafedra event gap a reported KNOWN GAP). The counts
# are the point: a NEW unlocalized screen fails, and an existing one that GROWS fails. The only
# direction the numbers may move without editing this table is down.
# ─────────────────────────────────────────────────────────────────────────────

UI_DIR = os.path.join(PROJECT_ROOT, "Assets", "_Project", "Scripts", "UI")

# screen -> prose literals still hardcoded, as measured 9 Aug 2026. Drive these to 0 and delete
# the entry. Do not raise a number to make the gate pass.
UNLOCALIZED_DEBT = {
    "ArtifactUseUI.cs": 30,
    "ExpeditionUI.cs": 17,
    "InterviewSequenceUI.cs": 30,
    "OblastUIAudio.cs": 1,
    "ScavengeHazardHUD.cs": 14,
}

_PROSE_RE = re.compile(r'"([A-Z][^"]{3,90})"')
_TOKENISH_RE = re.compile(r'^[A-Za-z0-9_.]+$')


def _prose_literals(source: str) -> int:
    """Rough count of player-facing prose literals. Deliberately crude — it is a TREND gate."""
    n = 0
    for match in _PROSE_RE.finditer(source):
        text = match.group(1)
        if _TOKENISH_RE.match(text) and len(text) < 12:
            continue
        if _TOKENISH_RE.match(text) and " " not in text:
            continue
        n += 1
    return n


def check_screen_coverage() -> Tuple[List[str], List[str]]:
    """Fails on a NEW unlocalized screen or on growth in a known one. Warns on the standing debt."""
    failures: List[str] = []
    warnings: List[str] = []

    if not os.path.isdir(UI_DIR):
        return ([f"UI source directory not found: {UI_DIR}"], [])

    remaining = 0
    for path in sorted(glob.glob(os.path.join(UI_DIR, "*.cs"))):
        name = os.path.basename(path)
        with open(path, "r", encoding="utf-8") as handle:
            source = handle.read()

        if "LocalizedStrings.Get" in source:
            if name in UNLOCALIZED_DEBT:
                failures.append(
                    f"{name} now uses LocalizedStrings but is still listed in UNLOCALIZED_DEBT. "
                    f"Finish it and remove the entry."
                )
            continue

        count = _prose_literals(source)
        if count == 0:
            continue

        budget = UNLOCALIZED_DEBT.get(name)
        if budget is None:
            failures.append(
                f"{name} has {count} player-facing prose literal(s) and never calls "
                f"LocalizedStrings.Get — a new screen that cannot be translated."
            )
        elif count > budget:
            failures.append(
                f"{name} grew from {budget} to {count} hardcoded prose literal(s). "
                f"Localization debt may only shrink."
            )
        else:
            remaining += count
            if count < budget:
                warnings.append(
                    f"{name} is down to {count} hardcoded literal(s) from {budget} — "
                    f"lower UNLOCALIZED_DEBT to lock the progress in."
                )

    if remaining:
        warnings.append(
            f"KNOWN GAP: {remaining} hardcoded prose literal(s) across "
            f"{len(UNLOCALIZED_DEBT)} screen(s) are not translatable. "
            f"These screens render English in every language."
        )
    return failures, warnings



def run(locale_dir: str = LOCALE_DIR, keys_source: str = KEYS_SOURCE,
        quiet: bool = False) -> Tuple[List[str], List[str]]:
    """Runs every check. Returns (failures, warnings)."""
    paths = discover_tables(locale_dir)
    if not paths:
        return ([f"no localization tables found under {locale_dir}"], [])

    tables: Dict[str, Dict[str, str]] = {}
    failures: List[str] = []

    for code, path in paths.items():
        try:
            tables[code] = load_table(path)
        except Exception as exc:                       # noqa: BLE001
            failures.append(f"localization_{code}.json failed to parse: {exc}")

    if failures:
        return failures, []

    required = required_keys(keys_source)
    if not quiet:
        print(f"UIStringKeys.cs declares {len(required)} key(s).")
        for code in sorted(tables):
            print(f"  localization_{code}.json: {len(tables[code])} display key(s)")

    warnings: List[str] = []

    failures += check_coverage(required, tables)

    parity_failures, parity_warnings = check_parity(tables)
    failures += parity_failures
    warnings += parity_warnings

    failures += check_placeholders(tables)
    failures += check_values(tables)

    voice_failures, voice_warnings = check_voice(tables)
    failures += voice_failures
    warnings += voice_warnings

    # The check the key-based ones structurally cannot perform: does each screen look anything up?
    coverage_failures, coverage_warnings = check_screen_coverage()
    failures += coverage_failures
    warnings += coverage_warnings
    if not quiet:
        print(f"  screen coverage: {len(UNLOCALIZED_DEBT)} screen(s) carrying localization debt")

    return failures, warnings


# ---------------------------------------------------------------------------
# Negative control
# ---------------------------------------------------------------------------
_SELF_TEST_KEYS_CS = '''
namespace OblastZero.Core
{
    public static class UIStringKeys
    {
        public const string Present = "st_present";
        public const string Missing = "st_missing";
        public const string Formatted = "st_formatted";
    }
}
'''

# Each entry: (label, en table, ru table, substring the failure must mention)
MUST_CATCH = [
    (
        "key declared in UIStringKeys but absent from a table",
        {"st_present": "Present", "st_formatted": "Filed {0}"},
        {"st_present": "Есть", "st_formatted": "Оформлено {0}"},
        "st_missing",
    ),
    (
        "key present in EN and missing from RU",
        {"st_present": "Present", "st_missing": "Missing", "st_formatted": "Filed {0}",
         "st_extra": "Extra"},
        {"st_present": "Есть", "st_missing": "Нет", "st_formatted": "Оформлено {0}"},
        "st_extra",
    ),
    (
        "dropped format placeholder in a translation",
        {"st_present": "P", "st_missing": "M", "st_formatted": "Filed {0} of {1}"},
        {"st_present": "P", "st_missing": "M", "st_formatted": "Оформлено"},
        "string.Format throws",
    ),
    (
        "empty value",
        {"st_present": "P", "st_missing": "M", "st_formatted": "Filed {0}"},
        {"st_present": "", "st_missing": "M", "st_formatted": "Оформлено {0}"},
        "is empty",
    ),
    (
        "value that is a copy of its own key",
        {"st_present": "st_present", "st_missing": "M", "st_formatted": "Filed {0}"},
        {"st_present": "P", "st_missing": "M", "st_formatted": "Оформлено {0}"},
        "itself as its value",
    ),
    (
        "tier-1 IP term in an English value",
        {"st_present": "Ask Sidorovich.", "st_missing": "M", "st_formatted": "Filed {0}"},
        {"st_present": "P", "st_missing": "M", "st_formatted": "Оформлено {0}"},
        "Sidorovich",
    ),
    (
        "tier-1 IP term in a Russian value",
        {"st_present": "P", "st_missing": "M", "st_formatted": "Filed {0}"},
        {"st_present": "Дорога на Pripyat закрыта.", "st_missing": "M",
         "st_formatted": "Оформлено {0}"},
        "Pripyat",
    ),
    (
        "tier-2 term used as a named entity",
        {"st_present": "Report to the Military at once.", "st_missing": "M",
         "st_formatted": "Filed {0}"},
        {"st_present": "P", "st_missing": "M", "st_formatted": "Оформлено {0}"},
        "named entity",
    ),
    (
        "bible-named cliche",
        {"st_present": "An eerie silence in the annex.", "st_missing": "M",
         "st_formatted": "Filed {0}"},
        {"st_present": "P", "st_missing": "M", "st_formatted": "Оформлено {0}"},
        "eerie silence",
    ),
]

# Must NOT fail: an ordinary-word tier-2 use, a title-cased phrase, a
# sentence-initial capital, and differing format SPECIFIERS on a shared index.
MUST_NOT_CATCH = (
    {
        "st_present": "Military plates were issued. The zone of exclusion holds.",
        "st_missing": "Issued Military Kit, one per operator.",
        "st_formatted": "Filed {0:0.0} of {1}",
    },
    {
        "st_present": "Выданы армейские пластины.",
        "st_missing": "Комплект, по одному на исполнителя.",
        "st_formatted": "Оформлено {0} из {1}",
    },
)


def _write_case(tmp_dir: str, en: dict, ru: dict) -> Tuple[str, str]:
    """Writes one synthetic case; returns (locale_dir, keys_source)."""
    locale_dir = os.path.join(tmp_dir, "Locale")
    os.makedirs(locale_dir, exist_ok=True)

    for code, table in (("en", en), ("ru", ru)):
        payload = dict(table)
        payload["_comment"] = "self-test fixture"
        with open(os.path.join(locale_dir, f"localization_{code}.json"),
                  "w", encoding="utf-8") as handle:
            json.dump(payload, handle, ensure_ascii=False, indent=2)

    keys_source = os.path.join(tmp_dir, "UIStringKeys.cs")
    with open(keys_source, "w", encoding="utf-8") as handle:
        handle.write(_SELF_TEST_KEYS_CS)

    return locale_dir, keys_source


def self_test() -> int:
    import shutil
    import tempfile

    print("=" * 74)
    print("  localization_qa self-test — every detector must fire, and must not")
    print("  fire on clean tables")
    print("=" * 74)

    passed = 0
    failed = 0
    tmp_root = tempfile.mkdtemp(prefix="oz_locqa_")

    try:
        for index, (label, en, ru, expect) in enumerate(MUST_CATCH):
            case_dir = os.path.join(tmp_root, f"catch{index}")
            locale_dir, keys_source = _write_case(case_dir, en, ru)
            failures, _ = run(locale_dir, keys_source, quiet=True)

            hit = any(expect in f for f in failures)
            if hit:
                passed += 1
                print(f"  [CATCH] {label}")
            else:
                failed += 1
                print(f"  [MISS ] {label}")
                print(f"          expected a failure mentioning {expect!r}")
                for f in failures:
                    print(f"          got: {f}")

        clean_dir = os.path.join(tmp_root, "clean")
        locale_dir, keys_source = _write_case(clean_dir, *MUST_NOT_CATCH)
        failures, _ = run(locale_dir, keys_source, quiet=True)

        if failures:
            failed += 1
            print("  [FALSE] clean tables were reported as violations:")
            for f in failures:
                print(f"          {f}")
        else:
            passed += 1
            print("  [CLEAN] clean tables pass, as they must")

    finally:
        shutil.rmtree(tmp_root, ignore_errors=True)

    print("-" * 74)
    print(f"  {passed} passed, {failed} failed")
    return 0 if failed == 0 else 1


# ---------------------------------------------------------------------------
def main() -> int:
    parser = argparse.ArgumentParser(
        description="Validate OblastZero localization tables against UIStringKeys.cs."
    )
    parser.add_argument("--self-test", action="store_true",
                        help="run the negative control and exit")
    args = parser.parse_args()

    if args.self_test:
        return self_test()

    try:
        failures, warnings = run()
    except FileNotFoundError as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    print()
    if warnings:
        print(f"WARNINGS ({len(warnings)}) — non-blocking:")
        for w in warnings:
            print(f"  {w}")
        print()

    print("=" * 74)
    if failures:
        print(f"  FAIL — {len(failures)} localization violation(s):")
        for f in failures:
            print(f"    {f}")
        print()
        print("  Run --self-test to confirm the detectors still fire.")
        return 1

    print("  PASS — every declared key is present in every table, placeholders")
    print("  agree, and no value breaches the IP firewall or the voice rules.")
    print("  Run --self-test to confirm the detectors still fire.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
