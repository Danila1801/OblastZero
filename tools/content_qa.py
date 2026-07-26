#!/usr/bin/env python3
"""
content_qa.py — OblastZero content QA (read-only)

Validates generated JSON content for the OblastZero Unity game:
  1. Parses every Events/*.json and Items/*.json under Assets/Data/Resources/.
  2. Schema check for required top-level fields (real schema, observed across
     all 1020 events + 703 items; see CLAUDE.md Stage 5).
  3. IP firewall (CLAUDE.md §8): flags S.T.A.L.K.E.R. proper nouns that must
     not appear in OblastZero content. Two tiers — see "Why two tiers" below.
  4. Voice check (design bible §7 / CLAUDE.md §9): forbidden pulp cliches.
  5. successChanceFormula lint against the grammar of Core/FormulaEvaluator.cs.
  6. Prints a report with file path, field, 1-based line ref and matched text.

Run `--self-test` for the negative control: synthetic violations that every
detector must catch, and clean strings it must not. CLAUDE.md §14 — a gate
never observed failing is decoration.

Stdlib only. Does not modify any game file.

--------------------------------------------------------------------------
Why prose-only scanning
--------------------------------------------------------------------------
Scans run over PROSE fields (title, narrativeText, choiceLabel, outcomeText,
displayName, designerNotes, description) — never over ids, region tags, or
enum values. Those are machine identifiers: `abandoned_school` is a region tag
that EventEngine.SelectNextEvent gates on, and it accounts for all 123
occurrences of "abandoned" in the content set. A raw-text scan reports those
as voice violations, and "fixing" them silently breaks event gating.

--------------------------------------------------------------------------
Why two tiers of banned term
--------------------------------------------------------------------------
The earlier revision matched every banned term case-insensitively against raw
file text, which produced 87 hits — all of them the ordinary English adjective
in "military plates", "military vehicle approaching", "Follow military
protocol." and item names like "Issued Military Kit". None reference the
S.T.A.L.K.E.R. *Military* faction; there is no trademark exposure in the
English word "military".

A gate with 87 false positives is worse than no gate, because it trains you to
skim past the one line that says Strelok. So:

  HARD_IP_TERMS       unambiguous proper nouns (Strelok, Sidorovich, Pripyat,
                      ChNPP, Bloodsucker, ...). Any casing, always a violation.
  CONTEXTUAL_IP_TERMS ordinary English words that are only a violation when
                      used as a named entity (Military, Duty, Freedom,
                      Monolith, Controller, Lens, ...). Flagged only when
                      capitalized, not sentence-initial, and not inside a
                      title-cased phrase — i.e. "the Military refused" trips,
                      "Issued Military Kit" and "Military plates," do not.

Cliches split the same way: the four phrases named in the bible fail the gate;
the broader pulp-adjective list is reported as a warning and does not.
"""
from __future__ import annotations

import argparse
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
PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir))
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

# The C# side reads these as localization keys; the shipped JSON stores flat prose
# in them instead (EventJsonLoader maps `title` -> titleKey verbatim). Consistent
# across the whole set, so it is reported as INFO rather than an error.
EVENT_INFO_KEYS_MISSING = ("displayNameKey", "narrativeKey")

# ---------------------------------------------------------------------------
# Prose fields — the only text the IP and voice scans look at.
# ---------------------------------------------------------------------------
EVENT_PROSE_TOP = ("title", "narrativeText", "designerNotes", "description")
EVENT_PROSE_CHOICE = ("choiceText", "choiceLabel")
EVENT_PROSE_OUTCOME = ("outcomeText", "resultText")
ITEM_PROSE_TOP = ("displayName", "designerNotes", "description", "flavorText")

# ---------------------------------------------------------------------------
# IP firewall — CLAUDE.md §8
# ---------------------------------------------------------------------------
# Tier 1: unambiguous. These are proper nouns with no ordinary-English reading,
# so any casing anywhere in prose is a violation.
HARD_IP_TERMS = [
    # People / traders / named characters
    "Strelok", "Sidorovich", "Degtyarev", "Kovalsky", "Tachenko", "Petrenko",
    "Kalancha", "Garmata", "Chekhov", "Tariyev", "Sokolov", "Lehavy", "Vano",
    "Mitay", "Sakharov", "Ghost", "Fang",
    # Places / installations
    "Pripyat", "ChNPP", "Chernobyl", "Yantar", "Rostok", "Agroprom",
    "Limansk", "Zaton", "Jupiter",
    # Factions with no common-noun reading
    "Clear Sky", "Renegades",
    # Mutants / anomalies with no common-noun reading
    "Zombified", "Pseudogiant", "Pseudodog", "Bloodsucker", "Snork", "Burer",
    "Poltergeist", "Blind Dog", "Whirligig", "Springboard", "Vortex Anomaly",
    # Franchise shorthand
    "EMR", "S.T.A.L.K.E.R", "STALKER",
]

# Tier 2: ordinary English words that only infringe when used as a named entity.
CONTEXTUAL_IP_TERMS = [
    "Military", "Duty", "Freedom", "Monolith", "Bandits", "Ecologists",
    "Mercenaries", "Loners", "Controller", "Chimera", "Boar", "Flesh",
    "Lens", "Owl", "Beard", "Hawaiian", "Garry", "Strider", "Barge", "Scar",
]

# ---------------------------------------------------------------------------
# Voice — design bible §7 / CLAUDE.md §9
# ---------------------------------------------------------------------------
# Named in the bible. These fail the gate.
HARD_CLICHES = [
    "twisted metal",
    "eerie silence",
    "unnatural glow",
    "screams in the distance",
]

# Broader pulp register. Reported, but does not fail: each has a legitimate
# post-administrative reading ("the abandoned filing annex" is in voice), so
# failing on them would make the gate unusable. Judgement call for a human.
SOFT_CLICHES = [
    "abandoned", "desolate", "lurking", "ominous", "sinister", "otherworldly",
    "bone-chilling", "blood-curdling", "deathly quiet", "shadowy figure",
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
        (?P<num>\d+\.\d+|\d+\.|\.\d+|\d+)
      | (?P<var>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)
      | (?P<op>[+\-*/()])
    )""",
    re.VERBOSE,
)

SENTENCE_ENDERS = (".", "!", "?", '"', ":", ";", "—", "-", "(", "[")


# ---------------------------------------------------------------------------
# Reporting data
# ---------------------------------------------------------------------------
@dataclass
class Viol:
    kind: str           # ip | ip_ctx | phrase | cliche_soft | schema | formula | parse | info
    message: str
    file: str
    line: int = 0
    field: str = ""
    matched: str = ""

    @property
    def hard(self) -> bool:
        """Does this violation fail the gate?"""
        return self.kind not in ("info", "cliche_soft")


@dataclass
class FileReport:
    path: str
    rel: str
    kind: str           # "event" | "item"
    parse_ok: bool = True
    schema_ok: bool = True
    violations: list = field(default_factory=list)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
def _word_bound(term: str, ignorecase: bool = True) -> re.Pattern:
    """Match `term` on non-alphanumeric boundaries (\\b misbehaves next to spaces/dots)."""
    flags = re.IGNORECASE if ignorecase else 0
    return re.compile(r"(?<![A-Za-z0-9])" + re.escape(term) + r"(?![A-Za-z0-9])", flags)


def _line_of_snippet(raw_text: str, snippet: str) -> int:
    """1-based line of the first line containing `snippet` (case-insensitive), else 0."""
    if not snippet:
        return 0
    low = snippet.lower()
    for i, ln in enumerate(raw_text.splitlines(), start=1):
        if low in ln.lower():
            return i
    return 0


def _locate(raw_text: str, value: str, match: re.Match) -> int:
    """
    Best-effort 1-based line for a match inside a prose value. Tries a window around
    the hit first (unique enough to land on the right line), then the bare term.
    """
    start = max(0, match.start() - 24)
    window = value[start:match.end() + 24].strip()
    return _line_of_snippet(raw_text, window) or _line_of_snippet(raw_text, match.group(0))


def _is_named_entity_use(value: str, match: re.Match) -> bool:
    """
    True when a contextual term reads as a proper noun rather than an ordinary word.

    Requires the match to be capitalized, not sentence-initial, and not part of a
    title-cased phrase. That clears "Military plates," (sentence-initial),
    "military protocol" (lowercase) and "Issued Military Kit" (title case), while
    still tripping on "the Military sealed the road".
    """
    tok = match.group(0)
    if not tok[:1].isupper():
        return False

    before = value[:match.start()].rstrip()
    if not before or before.endswith(SENTENCE_ENDERS):
        return False        # sentence-initial: capitalization carries no meaning

    prev_word = re.search(r"([A-Za-z0-9'\-]+)\s*$", before)
    if prev_word and prev_word.group(1)[:1].isupper():
        return False        # inside a Title Cased Phrase

    after = value[match.end():].lstrip()
    next_word = re.match(r"([A-Za-z0-9'\-]+)", after)
    if next_word and next_word.group(1)[:1].isupper():
        return False        # start of a Title Cased Phrase

    return True


# ---------------------------------------------------------------------------
# Prose extraction
# ---------------------------------------------------------------------------
def iter_prose(data: dict, kind: str) -> Iterable[tuple]:
    """Yield (field_path, text) for every human-facing string. Never ids or tags."""
    if kind == "event":
        for key in EVENT_PROSE_TOP:
            val = data.get(key)
            if isinstance(val, str) and val:
                yield (key, val)
        choices = data.get("choices")
        if isinstance(choices, list):
            for i, choice in enumerate(choices):
                if not isinstance(choice, dict):
                    continue
                for key in EVENT_PROSE_CHOICE:
                    val = choice.get(key)
                    if isinstance(val, str) and val:
                        yield (f"choices[{i}].{key}", val)
                for outcome_key in ("successOutcome", "failureOutcome"):
                    outcome = choice.get(outcome_key)
                    if not isinstance(outcome, dict):
                        continue
                    for key in EVENT_PROSE_OUTCOME:
                        val = outcome.get(key)
                        if isinstance(val, str) and val:
                            yield (f"choices[{i}].{outcome_key}.{key}", val)
    else:
        for key in ITEM_PROSE_TOP:
            val = data.get(key)
            if isinstance(val, str) and val:
                yield (key, val)


# ---------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------
def scan_prose(data: dict, kind: str, raw_text: str, rel: str, rep: FileReport) -> None:
    """IP firewall + voice, over prose fields only."""
    for field_path, value in iter_prose(data, kind):

        for term in HARD_IP_TERMS:
            for m in _word_bound(term).finditer(value):
                rep.violations.append(Viol(
                    kind="ip", file=rel, field=field_path, line=_locate(raw_text, value, m),
                    matched=m.group(0),
                    message=f"IP firewall: banned proper noun '{term}' (CLAUDE.md §8)",
                ))
                break   # one report per term per field is enough to act on

        for term in CONTEXTUAL_IP_TERMS:
            for m in _word_bound(term, ignorecase=False).finditer(value):
                if not _is_named_entity_use(value, m):
                    continue
                rep.violations.append(Viol(
                    kind="ip_ctx", file=rel, field=field_path, line=_locate(raw_text, value, m),
                    matched=m.group(0),
                    message=f"IP firewall: '{term}' reads as a named entity here, not an "
                            f"ordinary word (CLAUDE.md §8)",
                ))
                break

        for phrase in HARD_CLICHES:
            m = _word_bound(phrase).search(value)
            if m:
                rep.violations.append(Viol(
                    kind="phrase", file=rel, field=field_path, line=_locate(raw_text, value, m),
                    matched=m.group(0),
                    message=f"§7 forbidden cliche: '{phrase}'",
                ))

        for phrase in SOFT_CLICHES:
            m = _word_bound(phrase).search(value)
            if m:
                rep.violations.append(Viol(
                    kind="cliche_soft", file=rel, field=field_path,
                    line=_locate(raw_text, value, m), matched=m.group(0),
                    message=f"pulp register: '{phrase}' — check it earns its place (warning only)",
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
                kind="schema", file=rel, message="Event 'choices' must be a non-empty array",
            ))
        else:
            for i, c in enumerate(ch):
                if not isinstance(c, dict):
                    rep.schema_ok = False
                    rep.violations.append(Viol(
                        kind="schema", file=rel, message=f"choices[{i}] is not an object",
                    ))
                    continue
                m = [k for k in CHOICE_REQUIRED if k not in c]
                if m:
                    rep.schema_ok = False
                    rep.violations.append(Viol(
                        kind="schema", file=rel, message=f"choices[{i}] missing: {', '.join(m)}",
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
            kind="schema", file=rel, message=f"Item missing required field(s): {', '.join(missing)}",
        ))
    if "id" in data:
        base = os.path.splitext(os.path.basename(rel))[0]
        if data["id"] != base:
            rep.violations.append(Viol(
                kind="schema", file=rel, message=f"Item id '{data['id']}' != filename '{base}'",
            ))
    if "weightKg" in data and not isinstance(data["weightKg"], (int, float)):
        rep.schema_ok = False
        rep.violations.append(Viol(
            kind="schema", file=rel, message=f"weightKg not numeric: {data['weightKg']!r}",
        ))
    if "durability" in data and not isinstance(data["durability"], (int, float)):
        rep.schema_ok = False
        rep.violations.append(Viol(kind="schema", file=rel, message="durability not numeric"))


# ---- formula lint ---------------------------------------------------------
def lint_formula(formula: str, rel: str, field_name: str, rep: FileReport) -> None:
    """Static lint of a successChanceFormula string against the FormulaEvaluator grammar."""
    s = formula.strip()
    if not s:
        rep.violations.append(Viol(kind="formula", file=rel, field=field_name,
                                   message=f"{field_name}: empty formula"))
        return
    pos, depth, n = 0, 0, len(s)
    last_op = True                      # expect an operand at the start
    while pos < n:
        m = _FORMULA_TOKEN_RE.match(s, pos)
        if not m or m.start() != pos:
            rep.violations.append(Viol(
                kind="formula", file=rel, field=field_name,
                message=f"{field_name}: unexpected character at pos {pos}: {s[pos]!r} in {s!r}",
            ))
            return
        if m.group("num") is not None:
            tok = m.group("num")
            if tok in (".", "..", ""):
                rep.violations.append(Viol(
                    kind="formula", file=rel, field=field_name,
                    message=f"{field_name}: invalid number literal {tok!r} in {s!r}",
                ))
            last_op = False
        elif m.group("var") is not None:
            tok = m.group("var")
            if tok not in FORMULA_KNOWN_VARS:
                rep.violations.append(Viol(
                    kind="formula", file=rel, field=field_name,
                    message=f"{field_name}: unknown variable {tok!r} in {s!r} (known: crew.*)",
                ))
            last_op = False
        else:
            tok = m.group("op")
            if tok == "(":
                if not last_op:
                    rep.violations.append(Viol(
                        kind="formula", file=rel, field=field_name,
                        message=f"{field_name}: '(' must follow an operator, pos {pos} in {s!r}",
                    ))
                depth += 1
                last_op = True
            elif tok == ")":
                if last_op:
                    rep.violations.append(Viol(
                        kind="formula", file=rel, field=field_name,
                        message=f"{field_name}: ')' with nothing inside, pos {pos} in {s!r}",
                    ))
                if depth == 0:
                    rep.violations.append(Viol(
                        kind="formula", file=rel, field=field_name,
                        message=f"{field_name}: unbalanced ')' at pos {pos} in {s!r}",
                    ))
                depth -= 1
                last_op = False
            else:
                if last_op and tok != "-":
                    rep.violations.append(Viol(
                        kind="formula", file=rel, field=field_name,
                        message=f"{field_name}: binary '{tok}' without a left operand at pos {pos} in {s!r}",
                    ))
                last_op = True
        pos = m.end()
    if depth != 0:
        rep.violations.append(Viol(
            kind="formula", file=rel, field=field_name,
            message=f"{field_name}: unbalanced parens (depth={depth}) in {s!r}",
        ))
    if last_op:
        rep.violations.append(Viol(
            kind="formula", file=rel, field=field_name,
            message=f"{field_name}: trailing operator in {s!r}",
        ))


def _walk_strings(obj: Any, prefix: str = "") -> Iterable[tuple]:
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

    try:
        data = json.loads(raw_text)
    except json.JSONDecodeError as e:
        rep.parse_ok = False
        rep.violations.append(Viol(
            kind="parse", file=rel, line=e.lineno, message=f"JSON parse error: {e.msg}",
        ))
        return rep

    if not isinstance(data, dict):
        rep.parse_ok = False
        rep.violations.append(Viol(kind="parse", file=rel, message="Top-level JSON must be an object"))
        return rep

    if kind == "event":
        check_schema_event(data, rel, rep)
    else:
        check_schema_item(data, rel, rep)

    scan_prose(data, kind, raw_text, rel, rep)
    check_formulas(data, rel, rep)
    return rep


def _has_hard_violation(r: FileReport) -> bool:
    return any(v.hard for v in r.violations)


# ---------------------------------------------------------------------------
# Negative control (CLAUDE.md §14 — a gate never observed failing is decoration)
# ---------------------------------------------------------------------------
MUST_CATCH = [
    ("ip", {"id": "evt_x", "title": "Strelok is asking after you",
            "narrativeText": "n", "prerequisites": {}, "baseWeight": 1.0,
            "choices": [{"choiceLabel": "c", "successChance": 0.5, "requiredTraitsAny": [],
                         "blockedByTraits": [], "successOutcome": {}, "failureOutcome": {}}]}),
    ("ip", {"id": "evt_x", "title": "t", "narrativeText": "A bloodsucker took the night shift.",
            "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("ip", {"id": "evt_x", "title": "t", "narrativeText": "Filed from Pripyat, apparently.",
            "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("ip_ctx", {"id": "evt_x", "title": "t",
                "narrativeText": "The road is closed; the Military sealed it Tuesday.",
                "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("ip_ctx", {"id": "evt_x", "title": "t", "narrativeText": "He signed with Duty last spring.",
                "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("phrase", {"id": "evt_x", "title": "t", "narrativeText": "An eerie silence over the yard.",
                "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("phrase", {"id": "evt_x", "title": "t", "narrativeText": "Twisted metal everywhere.",
                "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("formula", {"id": "evt_x", "title": "t", "narrativeText": "n", "prerequisites": {},
                 "baseWeight": 1.0,
                 "choices": [{"choiceLabel": "c", "successChanceFormula": "crew.combat * (0.4",
                              "successChance": 0.5, "requiredTraitsAny": [], "blockedByTraits": [],
                              "successOutcome": {}, "failureOutcome": {}}]}),
    ("formula", {"id": "evt_x", "title": "t", "narrativeText": "n", "prerequisites": {},
                 "baseWeight": 1.0,
                 "choices": [{"choiceLabel": "c", "successChanceFormula": "crew.luck + 1",
                              "successChance": 0.5, "requiredTraitsAny": [], "blockedByTraits": [],
                              "successOutcome": {}, "failureOutcome": {}}]}),
    ("schema", {"id": "evt_x", "title": "t", "narrativeText": "n", "prerequisites": {},
                "baseWeight": 1.0, "choices": []}),
]

MUST_NOT_CATCH = [
    ("ordinary adjective", {"id": "evt_x", "title": "t",
                            "narrativeText": "Military plates, olive paint. They do not stop.",
                            "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("lowercase adjective", {"id": "evt_x", "title": "t",
                             "narrativeText": "The sentry signals: military vehicle approaching.",
                             "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
    ("title-cased item name", {"id": "item_x", "displayName": "Issued Military Kit"}),
    ("in-voice duty usage", {"id": "evt_x", "title": "t",
                             "narrativeText": "He is on duty until the shift register says otherwise.",
                             "prerequisites": {}, "baseWeight": 1.0, "choices": []}),
]


def self_test() -> int:
    """Prove every detector fires on a real violation and stays quiet on clean prose."""
    print("=" * 78)
    print("content_qa self-test (negative control)")
    print("=" * 78)
    failures = 0

    print("\n-- must CATCH --")
    for expect_kind, payload in MUST_CATCH:
        kind = "item" if payload.get("id", "").startswith("item") else "event"
        rep = FileReport(path="<memory>", rel="<memory>", kind=kind)
        if kind == "event":
            check_schema_event(payload, "<memory>", rep)
        else:
            check_schema_item(payload, "<memory>", rep)
        scan_prose(payload, kind, json.dumps(payload, indent=2), "<memory>", rep)
        check_formulas(payload, "<memory>", rep)

        got = sorted({v.kind for v in rep.violations if v.hard})
        ok = expect_kind in got
        failures += 0 if ok else 1
        probe = payload.get("narrativeText") or payload.get("title") or payload.get("displayName") or ""
        print(f"  [{'PASS' if ok else 'FAIL'}] expect {expect_kind:8} got {got}  :: {probe[:56]!r}")

    print("\n-- must NOT catch --")
    for label, payload in MUST_NOT_CATCH:
        kind = "item" if payload.get("id", "").startswith("item") else "event"
        rep = FileReport(path="<memory>", rel="<memory>", kind=kind)
        scan_prose(payload, kind, json.dumps(payload, indent=2), "<memory>", rep)
        noisy = [v for v in rep.violations if v.kind in ("ip", "ip_ctx", "phrase")]
        ok = not noisy
        failures += 0 if ok else 1
        print(f"  [{'PASS' if ok else 'FAIL'}] {label:24} "
              f"{'clean' if ok else [v.matched for v in noisy]}")

    print()
    if failures:
        print(f"SELF-TEST FAILED — {failures} case(s) wrong. The gate is not trustworthy.")
        return 1
    print("SELF-TEST PASSED — every detector fires on real violations and stays quiet on clean prose.")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="OblastZero content QA (read-only)")
    ap.add_argument("--self-test", action="store_true",
                    help="run the negative control instead of scanning content")
    ap.add_argument("--max-detail", type=int, default=40,
                    help="max lines printed per violation section (default 40)")
    args = ap.parse_args()

    if args.self_test:
        return self_test()

    print("=" * 78)
    print("OblastZero Content QA")
    print(f"Project root: {PROJECT_ROOT}")
    print("=" * 78)

    event_files = sorted(glob.glob(os.path.join(EVENTS_DIR, "*.json")))
    item_files = sorted(glob.glob(os.path.join(ITEMS_DIR, "*.json")))
    print(f"Events dir: {EVENTS_DIR}  ({len(event_files)} .json files)")
    print(f"Items  dir: {ITEMS_DIR}  ({len(item_files)} .json files)")
    print("Scans run over prose fields only — ids, region tags and enum values are")
    print("machine identifiers and are deliberately excluded.")
    print()

    all_reps = [process_file(p, "event") for p in event_files]
    all_reps += [process_file(p, "item") for p in item_files]

    total = len(all_reps)
    parse_ok = sum(1 for r in all_reps if r.parse_ok)
    schema_ok = sum(1 for r in all_reps if r.schema_ok)
    fully_valid = sum(1 for r in all_reps if r.parse_ok and r.schema_ok and not _has_hard_violation(r))
    hard_viol_files = [r for r in all_reps if _has_hard_violation(r)]

    def cnt(k):
        return sum(1 for r in all_reps for v in r.violations if v.kind == k)

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
    print("  Violations by kind:")
    print(f"    ip       (proper noun):    {cnt('ip')}          [fails gate]")
    print(f"    ip_ctx   (named-entity):   {cnt('ip_ctx')}          [fails gate]")
    print(f"    phrase   (bible §7):       {cnt('phrase')}          [fails gate]")
    print(f"    schema   (missing/invalid):{cnt('schema')}          [fails gate]")
    print(f"    parse    (JSON):           {cnt('parse')}          [fails gate]")
    print(f"    formula  (chance):         {cnt('formula')}          [fails gate]")
    print(f"    cliche_soft (pulp register):{cnt('cliche_soft')}         [warning only]")
    print(f"    info     (convention):     {cnt('info')}       [warning only]")
    print()

    def dump(kind: str, title: str) -> None:
        hits = [v for r in all_reps for v in r.violations if v.kind == kind]
        if not hits:
            print(f"  [{title}] none")
            return
        print(f"  [{title}] {len(hits)}:")
        for v in hits[:args.max_detail]:
            loc = v.file + (f":{v.line}" if v.line else "")
            extra = f" :: {v.field}" if v.field else ""
            shown = f" -> {v.matched!r}" if v.matched else ""
            print(f"    - {loc}{extra}  {v.message}{shown}")
        if len(hits) > args.max_detail:
            print(f"    ... and {len(hits) - args.max_detail} more")

    print("-" * 78)
    print("DETAIL")
    print("-" * 78)
    dump("ip", "IP FIREWALL — proper nouns")
    dump("ip_ctx", "IP FIREWALL — named-entity usage")
    dump("phrase", "§7 FORBIDDEN CLICHES")
    dump("schema", "SCHEMA")
    dump("formula", "FORMULA")
    dump("parse", "PARSE")
    dump("cliche_soft", "PULP REGISTER (warning only)")

    print()
    print("-" * 78)
    print("RESULT")
    print("-" * 78)
    if hard_viol_files:
        print(f"  FAIL — {len(hard_viol_files)} file(s) with hard violations:")
        for r in hard_viol_files[:50]:
            print(f"    - {r.rel}")
        if len(hard_viol_files) > 50:
            print(f"    ... and {len(hard_viol_files) - 50} more")
        return 1
    print("  PASS — all files schema-valid; no IP / §7 / formula violations.")
    print("  (warning-level notes above, if any, do not fail the QA.)")
    print("  Run --self-test to confirm the detectors still fire.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
