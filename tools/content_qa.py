#!/usr/bin/env python3
"""
content_qa.py — OblastZero content QA (read-only)

Validates generated JSON content for the OblastZero Unity game:
  1. Parses every Events/*.json and Items/*.json under Assets/Data/Resources/.
  2. Schema check for required top-level fields (real schema, observed across
     all 1020 events + 691 items on 2026-07-25; see CLAUDE.md Stage 5).
  3. IP firewall: flags S.T.A.L.K.E.R. proper nouns / faction names / mutants
     that must NOT appear in OblastZero content (trademark separation).
  4. Design bible §7 (line ~1290 forbidden cliches): flags banned voice phrases.
  5. successChanceFormula lint (grammar of Core/FormulaEvaluator.cs):
        empty parens, unbalanced parens, unknown variables (only crew.* are
        valid), stray operators, invalid number literals, trailing tokens.
     Notes: NO event currently carries a successChanceFormula field (choices use
     numeric `successChance`); the lint runs defensively for future content.
  6. Prints a full report with file paths + 1-based line refs for each hit.

Stdlib only. Does not touch any game file.
"""
from __future__ import annotations

import glob
import json
import os
import re
import sys
from dataclasses import dataclass, field
from typing import Any, Iterable

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
PROJECT_ROOT = os.path.abspath(
    os.path.join(os.path.dirname(__file__), os.pardir)
)
RESOURCES = os.path.join(PROJECT_ROOT, "Assets", "Data", "Resources")
EVENTS_DIR = os.path.join(RESOURCES, "Events")
ITEMS_DIR = os.path.join(RESOURCES, "Items")

# ---------------------------------------------------------------------------
# Schema (real observed schema — see Stage 5 content blitz in CLAUDE.md)
# ---------------------------------------------------------------------------
EVENT_REQUIRED = ("id", "title", "narrativeText", "prerequisites", "baseWeight", "choices")
ITEM_REQUIRED = (
    "id", "displayName", "category", "weightKg", "durability", "decayPerDay",
    "utilityTags", "radiationContaminated", "radiationContaminationLevel",
    "baseTradeValueScale", "baseTradeValueCordon", "baseTradeValueKafedra",
)

CHOICE_REQUIRED = (
    "choiceLabel", "successChance", "requiredTraitsAny", "blockedByTraits",
    "successOutcome", "failureOutcome",
)
OUTCOME_REQUIRED = (
    "sanityDelta", "fatigueDelta", "radiationDelta", "healthDelta",
    "reputationFaction", "reputationDelta", "crewDeathChance", "followUpEventId",
)

# Task mentions displayNameKey/narrativeKey/successChanceFormula (the C# localization
# + formula path). The shipped JSON deliberately uses flat strings+numbers instead.
# Flag absence as INFO, not an error, since the live content is consistent.
EVENT_INFO_KEYS_MISSING = ("displayNameKey", "narrativeKey")

# ---------------------------------------------------------------------------
# IP firewall — S.T.A.L.K.E.R. proper nouns that must NOT appear in Oblast content
# ---------------------------------------------------------------------------
BANNED_IP_TERMS = [
    "Strelok", "Scar", "Sidorovich", "Pripyat", "ChNPP", "Duty", "Freedom",
    "Monolith", "Bandits", "Clear Sky", "Ecologists", "Military",
    "Lehavy", "Degtyarev", "Kovalsky", "Tachenko", "Petrenko", "Kalancha",
    "Beard", "Owl", "Hawaiian", "Garry", "Strider", "Vano", "Mitay", "Barge",
    "Garmata", "Chekhov", "Tariyev", "Sokolov",
    "Zombified", "Pseudogiant", "Controller", "Bloodsucker", "Snork",
    "Boar", "Flesh", "Blind Dog", "Pseudodog", "Chimera", "Poltergeist", "Burer",
]

# Design bible §7 (line ~1290) forbidden cliches.
BANNED_PHRASES = [
    "twisted metal",
    "eerie silence",
    "unnatural glow",
    "screams in the distance",
]

# ---------------------------------------------------------------------------
# Formula grammar (mirror of Assets/_Project/Scripts/Core/FormulaEvaluator.cs)
#   expr    := term (('+'|'-') term)*
#   term    := unary (('*'|'/') unary)*
#   unary   := '-' unary | primary
#   primary := number | variable | '(' expr ')'
# Variables are dotted crew.* names (see CrewFormulaContext.TryResolve).
# ---------------------------------------------------------------------------
FORMULA_KNOWN_VARS = {
    "crew.health", "crew.sanity", "crew.fatigue", "crew.radiation",
    "crew.health_norm", "crew.sanity_norm", "crew.fatigue_norm",
    "crew.radiation_norm", "crew.combat", "crew.charisma",
}
_FORMULA_TOKEN_RE = re.compile(
    r"""\s*(?:
        (?P<num>\d+\.\d+|\d+\.|\.\d+|\d+)         # number literal (greedy)
      | (?P<var>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)  # dotted var, greedy
      | (?P<op>[+\-*/()])                          # operator / paren
    )""",
    re.VERBOSE,
)


# ---------------------------------------------------------------------------
# Reporting data
# ---------------------------------------------------------------------------
@dataclass
class Viol:
    kind: str          # ip | phrase | schema | formula | parse | info
    message: str
    file: str
    line: int = 0
    field: str = ""


@dataclass
class FileReport:
    path: str
    rel: str
    kind: str          # "event" | "item"
    parse_ok: bool = True
    schema_ok: bool = True
    violations: list[Viol] = field(default_factory=list)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def _line_of(text: str, needle: str) -> int:
    """1-based line number of first occurrence of needle (case-insensitive), 0 if absent."""
    low = needle.lower()
    for i, ln in enumerate(text.splitlines(), start=1):
        if low in ln.lower():
            return i
    return 0


def _line_of_re(text: str, pattern: re.Pattern) -> int:
    """1-based line of first regex match, else 0."""
    for i, ln in enumerate(text.splitlines(), start=1):
        if pattern.search(ln):
            return i
    return 0


def _word_bound(term: str) -> re.Pattern:
    """Case-insensitive regex matching `term` on word boundaries."""
    # Treat spaces inside multi-word terms as literal spaces; escape everything.
    esc = re.escape(term)
    # \b doesn't fire next to a space, so apply \b only at letter/digit edges.
    return re.compile(r"(?<![A-Za-z0-9])" + esc + r"(?![A-Za-z0-9])", re.IGNORECASE)


# ---------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------
def check_ip_and_phrases(raw_text: str, rel: str, rep: FileReport) -> None:
    for term in BANNED_IP_TERMS:
        pat = _word_bound(term)
        ln = _line_of_re(raw_text, pat)
        if ln:
            rep.violations.append(Viol(
                kind="ip",
                message=f"IP firewall hit: banned S.T.A.L.K.E.R. name '{term}'",
                file=rel, line=ln,
            ))
    for phrase in BANNED_PHRASES:
        ln = _line_of(raw_text, phrase)
        if ln:
            rep.violations.append(Viol(
                kind="phrase",
                message=f"§7 forbidden phrase: '{phrase}'",
                file=rel, line=ln,
            ))


def check_schema_event(data: dict, rel: str, rep: FileReport) -> None:
    missing_top = [k for k in EVENT_REQUIRED if k not in data]
    if missing_top:
        rep.schema_ok = False
        rep.violations.append(Viol(
            kind="schema", file=rel,
            message=f"Event missing required top-level field(s): {', '.join(missing_top)}",
        ))
    for k in EVENT_INFO_KEYS_MISSING:
        if k not in data:
            rep.violations.append(Viol(
                kind="info", file=rel,
                message=f"Event uses flat string instead of {k} (current content convention — not an error)",
            ))
    if "id" in data:
        base = os.path.splitext(os.path.basename(rel))[0]
        if data["id"] != base:
            rep.violations.append(Viol(
                kind="schema", file=rel,
                message=f"Event id '{data['id']}' != filename '{base}'",
            ))
    if "choices" in data:
        ch = data["choices"]
        if not isinstance(ch, list) or not ch:
            rep.schema_ok = False
            rep.violations.append(Viol(
                kind="schema", file=rel,
                message="Event 'choices' must be a non-empty array",
            ))
        else:
            for i, c in enumerate(ch):
                if not isinstance(c, dict):
                    rep.schema_ok = False
                    rep.violations.append(Viol(
                        kind="schema", file=rel,
                        message=f"choices[{i}] is not an object",
                    ))
                    continue
                m = [k for k in CHOICE_REQUIRED if k not in c]
                if m:
                    rep.schema_ok = False
                    rep.violations.append(Viol(
                        kind="schema", file=rel,
                        message=f"choices[{i}] missing: {', '.join(m)}",
                    ))
                if "successChance" in c:
                    sc = c["successChance"]
                    if not isinstance(sc, (int, float)) or not (0.0 <= float(sc) <= 1.0):
                        rep.violations.append(Viol(
                            kind="schema", file=rel,
                            message=f"choices[{i}].successChance out of [0,1]: {sc!r}",
                        ))
                for ok_name in ("successOutcome", "failureOutcome"):
                    if ok_name in c and isinstance(c[ok_name], dict):
                        m = [k for k in OUTCOME_REQUIRED if k not in c[ok_name]]
                        if m:
                            rep.violations.append(Viol(
                                kind="schema", file=rel,
                                message=f"choices[{i}].{ok_name} missing: {', '.join(m)}",
                            ))


def check_schema_item(data: dict, rel: str, rep: FileReport) -> None:
    missing = [k for k in ITEM_REQUIRED if k not in data]
    if missing:
        rep.schema_ok = False
        rep.violations.append(Viol(
            kind="schema", file=rel,
            message=f"Item missing required field(s): {', '.join(missing)}",
        ))
    if "id" in data:
        base = os.path.splitext(os.path.basename(rel))[0]
        if data["id"] != base:
            rep.violations.append(Viol(
                kind="schema", file=rel,
                message=f"Item id '{data['id']}' != filename '{base}'",
            ))
    if "weightKg" in data and not isinstance(data["weightKg"], (int, float)):
        rep.schema_ok = False
        rep.violations.append(Viol(
            kind="schema", file=rel, message=f"weightKg not numeric: {data['weightKg']!r}",
        ))
    if "durability" in data and not isinstance(data["durability"], (int, float)):
        rep.schema_ok = False
        rep.violations.append(Viol(
            kind="schema", file=rel, message=f"durability not numeric",
        ))


# ---- formula lint ---------------------------------------------------------
def _msg(s: str) -> str: return s


def lint_formula(formula: str, rel: str, field_name: str, rep: FileReport) -> None:
    """Static lint of a successChanceFormula string against the FormulaEvaluator grammar."""
    s = formula.strip()
    if not s:
        rep.violations.append(Viol(
            kind="formula", file=rel, message=f"{field_name}: empty formula",
        )); return
    pos = 0
    depth = 0
    n = len(s)
    last_op = True            # expect a primary (operand) at start
    while pos < n:
        m = _FORMULA_TOKEN_RE.match(s, pos)
        if not m or m.start() != pos:
            rep.violations.append(Viol(
                kind="formula", file=rel,
                message=f"{field_name}: unexpected character at pos {pos}: {s[pos]!r} in {s!r}",
            ))
            return
        if m.group("num") is not None:
            tok = m.group("num")
            if tok in (".", "..", ""):
                rep.violations.append(Viol(
                    kind="formula", file=rel,
                    message=f"{field_name}: invalid number literal {tok!r} in {s!r}",
                ))
            last_op = False
        elif m.group("var") is not None:
            tok = m.group("var")
            if tok not in FORMULA_KNOWN_VARS:
                rep.violations.append(Viol(
                    kind="formula", file=rel,
                    message=f"{field_name}: unknown variable {tok!r} in {s!r} (known: crew.*)",
                ))
            last_op = False
        elif m.group("op") is not None:
            tok = m.group("op")
            if tok == "(":
                if not last_op:
                    rep.violations.append(Viol(
                        kind="formula", file=rel,
                        message=f"{field_name}: '(' must follow operator, pos {pos} in {s!r}",
                    ))
                depth += 1
                last_op = True
            elif tok == ")":
                if last_op:
                    rep.violations.append(Viol(
                        kind="formula", file=rel,
                        message=f"{field_name}: ')' with nothing inside, pos {pos} in {s!r}",
                    ))
                if depth == 0:
                    rep.violations.append(Viol(
                        kind="formula", file=rel,
                        message=f"{field_name}: unbalanced ')' at pos {pos} in {s!r}",
                    ))
                depth -= 1
                last_op = False
            else:  # + - * /
                if last_op and tok != "-":
                    rep.violations.append(Viol(
                        kind="formula", file=rel,
                        message=f"{field_name}: binary '{tok}' without left operand at pos {pos} in {s!r}",
                    ))
                last_op = True
        pos = m.end()
    if depth != 0:
        rep.violations.append(Viol(
            kind="formula", file=rel,
            message=f"{field_name}: unbalanced parens (depth={depth}) in {s!r}",
        ))
    if last_op:
        rep.violations.append(Viol(
            kind="formula", file=rel,
            message=f"{field_name}: trailing operator in {s!r}",
        ))


def _walk_strings(obj: Any, prefix: str = "") -> Iterable[tuple[str, str]]:
    """Yield (field_path, value) for every string value in a nested dict/list."""
    if isinstance(obj, dict):
        for k, v in obj.items():
            p = f"{prefix}.{k}" if prefix else k
            if isinstance(v, str):
                yield (p, v)
            else:
                yield from _walk_strings(v, p)
    elif isinstance(obj, list):
        for i, v in enumerate(obj):
            p = f"{prefix}[{i}]"
            if isinstance(v, str):
                yield (p, v)
            else:
                yield from _walk_strings(v, p)


def check_formulas(data: dict, rel: str, rep: FileReport) -> None:
    """Lint any field whose name contains 'formula' (case-insensitive)."""
    for path, val in _walk_strings(data):
        if "formula" in path.lower():
            lint_formula(val, rel, path, rep)


# ---------------------------------------------------------------------------
# Driver
# ---------------------------------------------------------------------------
def process_file(path: str, kind: str) -> FileReport:
    rel = os.path.relpath(path, PROJECT_ROOT).replace("\\", "/")
    rep = FileReport(path=path, rel=rel, kind=kind)

    try:
        with open(path, "r", encoding="utf-8") as f:
            raw_text = f.read()
    except OSError as e:
        rep.parse_ok = False
        rep.violations.append(Viol(kind="parse", file=rel, message=f"Cannot read file: {e}"))
        return rep

    # IP firewall + §7 phrases scan the raw text (case-insensitive, word-bounded).
    check_ip_and_phrases(raw_text, rel, rep)

    # JSON parse
    try:
        data = json.loads(raw_text)
    except json.JSONDecodeError as e:
        rep.parse_ok = False
        rep.violations.append(Viol(
            kind="parse", file=rel, line=e.lineno,
            message=f"JSON parse error: {e.msg}",
        ))
        return rep

    if not isinstance(data, dict):
        rep.parse_ok = False
        rep.violations.append(Viol(kind="parse", file=rel, message="Top-level JSON must be an object"))
        return rep

    # Schema
    if kind == "event":
        check_schema_event(data, rel, rep)
    else:
        check_schema_item(data, rel, rep)

    # Formula lint
    check_formulas(data, rel, rep)

    return rep


def main() -> int:
    if not os.path.isdir(PROJECT_ROOT):
        print(f"ERROR: project root not found: {PROJECT_ROOT}", file=sys.stderr)
        return 2

    print("=" * 78)
    print("OblastZero Content QA")
    print(f"Project root: {PROJECT_ROOT}")
    print("=" * 78)

    event_files = sorted(glob.glob(os.path.join(EVENTS_DIR, "*.json")))
    item_files = sorted(glob.glob(os.path.join(ITEMS_DIR, "*.json")))

    print(f"Events dir: {EVENTS_DIR}  ({len(event_files)} .json files)")
    print(f"Items  dir: {ITEMS_DIR}  ({len(item_files)} .json files)")
    print()

    all_reps: list[FileReport] = []
    for p in event_files:
        all_reps.append(process_file(p, "event"))
    for p in item_files:
        all_reps.append(process_file(p, "item"))

    # ---- Aggregate summary ------------------------------------------------
    total = len(all_reps)
    parse_ok = sum(1 for r in all_reps if r.parse_ok)
    schema_ok = sum(1 for r in all_reps if r.schema_ok)
    fully_valid = sum(1 for r in all_reps if r.parse_ok and r.schema_ok and not _has_hard_violation(r))
    hard_viol_files = [r for r in all_reps if _has_hard_violation(r)]

    cnt = lambda k: sum(1 for r in all_reps for v in r.violations if v.kind == k)
    n_ip = cnt("ip")
    n_phrase = cnt("phrase")
    n_formula = cnt("formula")
    n_schema = cnt("schema")
    n_parse = cnt("parse")
    n_info = cnt("info")

    print("-" * 78)
    print("SUMMARY")
    print("-" * 78)
    print(f"  Total files scanned:         {total}")
    print(f"    Events:                    {len(event_files)}")
    print(f"    Items:                     {len(item_files)}")
    print(f"  JSON parse OK:               {parse_ok}")
    print(f"  Schema OK (top+choices):     {schema_ok}")
    print(f"  Fully valid (no hard viol.): {fully_valid}")
    print()
    print(f"  Violations by kind:")
    print(f"    ip       (S.T.A.L.K.E.R.): {n_ip}")
    print(f"    phrase   (§7 cliches):     {n_phrase}")
    print(f"    schema   (missing/invalid):{n_schema}")
    print(f"    parse    (JSON):           {n_parse}")
    print(f"    formula  (chance):         {n_formula}")
    print(f"    info     (convention):     {n_info}")
    print()

    # ---- Detail: every violation, grouped by kind -------------------------
    def dump(kind: str, title: str) -> None:
        hits = [v for r in all_reps for v in r.violations if v.kind == kind]
        if not hits:
            print(f"  [{title}] none")
            return
        print(f"  [{title}] {len(hits)}:")
        for v in hits:
            loc = v.file
            if v.line:
                loc += f":{v.line}"
            extra = f" :: {v.field}" if v.field else ""
            print(f"    - {loc}{extra}  {v.message}")

    print("-" * 78)
    print("DETAIL")
    print("-" * 78)
    dump("ip", "IP FIREWALL")
    dump("phrase", "§7 FORBIDDEN PHRASES")
    dump("schema", "SCHEMA")
    dump("formula", "FORMULA")
    dump("parse", "PARSE")
    dump("info", "INFO (convention, not error)")

    print()
    print("-" * 78)
    print("RESULT")
    print("-" * 78)
    if hard_viol_files:
        print(f"  FAIL — {len(hard_viol_files)} file(s) with hard violations (ip/phrase/schema/parse/formula):")
        for r in hard_viol_files[:50]:
            print(f"    - {r.rel}")
        if len(hard_viol_files) > 50:
            print(f"    ... and {len(hard_viol_files) - 50} more")
        return 1
    print("  PASS — all files schema-valid; no IP / §7 / formula violations.")
    print("  (info-level convention notes only; those do not fail the QA.)")
    return 0


def _has_hard_violation(r: FileReport) -> bool:
    return any(v.kind != "info" for v in r.violations)


if __name__ == "__main__":
    raise SystemExit(main())
