#!/usr/bin/env python3
"""
csharp_string_qa.py — OblastZero IP/voice gate for strings in C# source (read-only)

content_qa.py validates the JSON content set. It does not read a single line of
C#, and says so in its own source:

    "Added after two shipped victory-screen narratives were found saying 'the
     Zone' where the setting is the Oblast; they were prose in C# rather than in
     content JSON, which this scanner does not read at all"

Those two narratives were caught by a human reading the file. This tool is the
gate that would have caught them, and it closes that gap for good:

  1. Lexes every .cs file under Assets/ and extracts string literals properly —
     regular, verbatim (@""), interpolated ($""), and both @$ orders — skipping
     comments, char literals and escape sequences. A regex over raw text cannot
     do this: it reports the contents of comments (which are full of the banned
     words, legitimately, because they discuss the firewall) and mis-terminates
     on every escaped quote.
  2. Classifies each literal as PLAYER-FACING or DIAGNOSTIC by looking at the
     call it sits inside. This is the distinction that decides whether a hit
     matters: prose in a victory narrative ships to the player, the same word in
     a Debug.LogError never leaves the console.
  3. Applies the SAME term lists as content_qa.py, imported from it rather than
     copied, so the JSON gate and the C# gate cannot drift apart. A second
     hand-maintained copy of the banned-noun list is a guarantee of divergence.
  4. Reports file, 1-based line, the enclosing member, and the matched text.

Exit code 0 = clean, 1 = hard violations, 2 = usage/IO error.

Run `--self-test` for the negative control: synthetic C# that every detector
must catch, plus clean C# it must not flag. CLAUDE.md §12 — a gate never
observed failing is decoration.

Stdlib only (plus content_qa.py, a sibling). Modifies nothing.

--------------------------------------------------------------------------
Why the player-facing / diagnostic split is load-bearing
--------------------------------------------------------------------------
This codebase logs heavily and deliberately (CLAUDE.md §5: "Robust Debug.Log on
critical state changes"). Those messages name systems, quote ids, and discuss
the firewall itself. Scanning them at the same tier as shipped prose produces a
report dominated by strings no player can ever see — the exact false-positive
flood content_qa.py was rewritten to avoid, where 87 hits on the ordinary word
"military" trained the reader to skim past the one line that said Strelok.

So: a hit in a player-facing literal FAILS the gate. The same hit in a
diagnostic literal is reported as info, visible but non-blocking. Test harnesses
and Editor-only scripts are diagnostic wholesale — they compile into the game in
this project, but nothing they say reaches a player.
"""
from __future__ import annotations

import argparse
import os
import re
import sys
from dataclasses import dataclass
from typing import Iterable

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), os.pardir))
ASSETS = os.path.join(PROJECT_ROOT, "Assets")
TOOLS_DIR = os.path.dirname(os.path.abspath(__file__))

# ---------------------------------------------------------------------------
# Term lists — imported, never copied. See module docstring, point 4.
# ---------------------------------------------------------------------------
if TOOLS_DIR not in sys.path:
    sys.path.insert(0, TOOLS_DIR)
try:
    import content_qa
except ImportError as exc:  # pragma: no cover - environment problem, not a finding
    print(f"[error] cannot import content_qa.py from {TOOLS_DIR}: {exc}", file=sys.stderr)
    print("[error] the two gates share one term list by design; fix the import "
          "rather than pasting the lists in here.", file=sys.stderr)
    sys.exit(2)

HARD_IP_TERMS = content_qa.HARD_IP_TERMS
CONTEXTUAL_IP_TERMS = content_qa.CONTEXTUAL_IP_TERMS
HARD_CLICHES = content_qa.HARD_CLICHES

# ---------------------------------------------------------------------------
# Which calls make a literal a diagnostic rather than player prose
# ---------------------------------------------------------------------------
# Matched against the callee chain immediately left of the enclosing '('.
DIAGNOSTIC_CALLEES = (
    "Debug.Log", "Debug.LogWarning", "Debug.LogError", "Debug.LogFormat",
    "Debug.LogWarningFormat", "Debug.LogErrorFormat", "Debug.LogException",
    "Debug.Assert", "Debug.AssertFormat",
    "Assert.IsTrue", "Assert.IsFalse", "Assert.AreEqual", "Assert.NotNull",
    "Check",                     # the repo's own per-test assertion helper
    "nameof", "typeof",
    "ArgumentNullException", "ArgumentException", "InvalidOperationException",
    "NotSupportedException", "FormulaException", "Exception",
)

# Whole files whose strings can never reach a player. Test harnesses are
# MonoBehaviours that do compile into the game here, but they are dev tools.
DIAGNOSTIC_FILE_PATTERNS = (
    re.compile(r"Test\.cs$"),
    re.compile(r"[\\/]Editor[\\/]"),
    re.compile(r"DebugRunLauncher\.cs$"),
    re.compile(r"[\\/]TutorialInfo[\\/]"),
)

# Literals that are plainly machine identifiers, not prose. Region tags, scene
# names, resource keys, ids. A named-entity check over these is meaningless and
# "fixing" one silently breaks gating (content_qa.py learned this the hard way:
# 'abandoned_school' is a region tag EventEngine gates on).
IDENTIFIER_RE = re.compile(r"^[A-Za-z0-9_./:\\-]*$")

SENTENCE_ENDERS = content_qa.SENTENCE_ENDERS
ARTICLES = content_qa.ARTICLES


# ---------------------------------------------------------------------------
# Findings
# ---------------------------------------------------------------------------
@dataclass
class Finding:
    kind: str            # ip | ip_ctx | cliche | dash
    message: str
    file: str
    line: int
    member: str
    matched: str
    player_facing: bool

    @property
    def hard(self) -> bool:
        """Only a player-facing IP or named cliche violation fails the gate."""
        return self.player_facing and self.kind in ("ip", "ip_ctx", "cliche")


@dataclass
class Literal:
    text: str            # decoded contents, without the quotes
    line: int            # 1-based line of the opening quote
    member: str          # nearest enclosing method/property/field, best effort
    player_facing: bool


# ---------------------------------------------------------------------------
# C# lexer — enough of one to find string literals and nothing else
# ---------------------------------------------------------------------------
def strip_and_collect(source: str) -> tuple[str, list[tuple[int, int, str]]]:
    """
    Single pass over a .cs file. Returns:
      * a 'skeleton' copy of the source with every comment and literal body
        replaced by spaces (newlines preserved), so it can be scanned for
        enclosing syntax without literals confusing the parens; and
      * the literals as (start_offset, line, decoded_text).

    Handles: // and /* */ comments, 'c' char literals, "regular" with escapes,
    @"verbatim" with "" escapes, $"interpolated", and $@ / @$ in either order.
    """
    out: list[str] = []
    literals: list[tuple[int, int, str]] = []

    i, n, line = 0, len(source), 1

    def blank(chunk: str) -> None:
        """Keep offsets and line numbers aligned by preserving newlines only."""
        out.append("".join("\n" if ch == "\n" else " " for ch in chunk))

    while i < n:
        ch = source[i]

        # ---- comments ----
        if ch == "/" and i + 1 < n and source[i + 1] == "/":
            j = source.find("\n", i)
            j = n if j < 0 else j
            blank(source[i:j])
            i = j
            continue

        if ch == "/" and i + 1 < n and source[i + 1] == "*":
            j = source.find("*/", i + 2)
            j = n if j < 0 else j + 2
            chunk = source[i:j]
            line += chunk.count("\n")
            blank(chunk)
            i = j
            continue

        # ---- char literal: skipped, never prose ----
        if ch == "'":
            j = i + 1
            while j < n:
                if source[j] == "\\":
                    j += 2
                    continue
                if source[j] == "'":
                    j += 1
                    break
                j += 1
            blank(source[i:j])
            i = j
            continue

        # ---- string literal, with any prefix combination ----
        prefix_start = i
        verbatim = False
        seen = 0
        while i + seen < n and source[i + seen] in "@$":
            if source[i + seen] == "@":
                verbatim = True
            seen += 1

        if i + seen < n and source[i + seen] == '"':
            quote = i + seen
            start_line = line
            body: list[str] = []
            j = quote + 1

            if verbatim:
                while j < n:
                    if source[j] == '"':
                        if j + 1 < n and source[j + 1] == '"':
                            body.append('"')
                            j += 2
                            continue
                        j += 1
                        break
                    body.append(source[j])
                    j += 1
            else:
                while j < n:
                    c = source[j]
                    if c == "\\" and j + 1 < n:
                        body.append(_unescape(source[j + 1]))
                        j += 2
                        continue
                    if c == '"':
                        j += 1
                        break
                    if c == "\n":  # unterminated; bail rather than run to EOF
                        break
                    body.append(c)
                    j += 1

            chunk = source[prefix_start:j]
            literals.append((prefix_start, start_line, "".join(body)))
            line += chunk.count("\n")
            blank(chunk)
            i = j
            continue

        # ---- ordinary code ----
        out.append(ch)
        if ch == "\n":
            line += 1
        i += 1

    return "".join(out), literals


def _unescape(c: str) -> str:
    return {"n": "\n", "t": "\t", "r": "\r", "0": "\0", "\\": "\\",
            '"': '"', "'": "'", "a": "\a", "b": "\b", "f": "\f", "v": "\v"}.get(c, c)


_CALLEE_RE = re.compile(r"([A-Za-z_][A-Za-z0-9_]*(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)*)\s*$")


def enclosing_callee(skeleton: str, offset: int) -> str:
    """
    The callee of the innermost call whose argument list contains `offset`, or ""
    when the literal is not inside a call at all (an initializer, a return, an
    expression-bodied member).

    Walks left counting parens over the literal-free skeleton, so quotes and
    parens inside strings cannot throw the count off.
    """
    depth = 0
    i = offset - 1
    while i >= 0:
        c = skeleton[i]
        if c == ")":
            depth += 1
        elif c == "(":
            if depth == 0:
                m = _CALLEE_RE.search(skeleton[:i])
                return re.sub(r"\s+", "", m.group(1)) if m else ""
            depth -= 1
        elif c in ";{}" and depth == 0:
            return ""  # statement boundary — not inside a call
        i -= 1
    return ""


_MEMBER_RE = re.compile(
    r"(?:^|\n)[ \t]*(?:\[[^\]]*\][ \t]*)*"
    r"(?:public|private|protected|internal|static|abstract|virtual|override|sealed|readonly|const|partial|async|extern|new)[\w<>,\[\] \t]*?"
    r"([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(|=>|=|\{)"
)


def enclosing_member(skeleton: str, offset: int) -> str:
    """Best-effort name of the member a literal sits in, for the report only."""
    last = ""
    for m in _MEMBER_RE.finditer(skeleton, 0, offset):
        last = m.group(1)
    return last or "(file scope)"


def is_diagnostic_file(path: str) -> bool:
    return any(p.search(path) for p in DIAGNOSTIC_FILE_PATTERNS)


def extract_literals(path: str, source: str) -> list[Literal]:
    skeleton, raw = strip_and_collect(source)
    diagnostic_file = is_diagnostic_file(path)

    result: list[Literal] = []
    for offset, line, text in raw:
        callee = enclosing_callee(skeleton, offset)
        is_diag = diagnostic_file or any(
            callee == d or callee.endswith("." + d.split(".")[-1]) and d in DIAGNOSTIC_CALLEES
            for d in DIAGNOSTIC_CALLEES
        ) or callee in DIAGNOSTIC_CALLEES
        result.append(Literal(
            text=text,
            line=line,
            member=enclosing_member(skeleton, offset),
            player_facing=not is_diag,
        ))
    return result


# ---------------------------------------------------------------------------
# Detectors — same semantics as content_qa.py, applied to C# literals
# ---------------------------------------------------------------------------
def looks_like_identifier(text: str) -> bool:
    """A literal with no spaces and only id-safe characters is a machine key."""
    stripped = text.strip()
    return bool(stripped) and " " not in stripped and bool(IDENTIFIER_RE.match(stripped))


def scan_hard_ip(text: str) -> list[str]:
    hits = []
    lowered = text.lower()
    for term in HARD_IP_TERMS:
        if term.lower() in lowered:
            hits.append(term)
    return hits


def scan_contextual_ip(text: str) -> list[str]:
    """
    Tier 2: flagged only on named-entity use — capitalized, not sentence-initial,
    and not inside a title-cased phrase. Mirrors content_qa.py's rule, which
    exists because "the contaminated zone" is fine and "the Zone" is not.
    """
    hits = []
    for term in CONTEXTUAL_IP_TERMS:
        for m in re.finditer(r"\b" + re.escape(term) + r"\b", text):
            if not m.group(0)[0].isupper():
                continue
            if _sentence_initial(text, m.start()):
                continue
            if _in_title_case_phrase(text, m.start(), m.end()):
                continue
            hits.append(term)
            break
    return hits


def _sentence_initial(text: str, start: int) -> bool:
    j = start - 1
    while j >= 0 and text[j] in " \t\n":
        j -= 1
    return j < 0 or text[j] in SENTENCE_ENDERS


def _in_title_case_phrase(text: str, start: int, end: int) -> bool:
    """
    True when the neighbouring word is also capitalized — "Issued Military Kit"
    is an item name, not a faction reference.

    A capitalized ARTICLE before the term is exempt: "The" in "The Zone does not
    permit departures." is a determiner, not part of a name. That sentence is the
    exact prose this gate exists to catch — it shipped in a victory narrative —
    and treating its "The" as title case is what let it through on the first
    pass over the pre-fix code. Only the preceding-word test is relaxed; a
    capitalized word AFTER the term still suppresses, so "The Lens Assembly"
    stays clean.
    """
    before = re.search(r"([A-Za-z]+)\s+$", text[:start])
    if (before and before.group(1)[0].isupper()
            and before.group(1).lower() not in ARTICLES):
        return True
    after = re.match(r"\s+([A-Za-z]+)", text[end:])
    return bool(after and after.group(1)[0].isupper())


def scan_cliches(text: str) -> list[str]:
    lowered = text.lower()
    return [p for p in HARD_CLICHES if p in lowered]


def scan_em_dashes(text: str) -> list[str]:
    return ["—"] if "—" in text else []


def scan_literal(path: str, lit: Literal, check_dashes: bool) -> list[Finding]:
    if looks_like_identifier(lit.text):
        return []

    findings: list[Finding] = []

    def add(kind: str, matched: str, message: str) -> None:
        findings.append(Finding(kind=kind, message=message, file=path, line=lit.line,
                                member=lit.member, matched=matched,
                                player_facing=lit.player_facing))

    for term in scan_hard_ip(lit.text):
        add("ip", term, f"IP firewall (CLAUDE.md §8): franchise proper noun '{term}'")

    for term in scan_contextual_ip(lit.text):
        add("ip_ctx", term,
            f"IP firewall (CLAUDE.md §8): '{term}' used as a named entity — "
            f"the setting is the Oblast")

    for phrase in scan_cliches(lit.text):
        add("cliche", phrase, f"Voice (bible §7): forbidden cliche '{phrase}'")

    if check_dashes:
        for dash in scan_em_dashes(lit.text):
            add("dash", dash, "em dash in a shipped string (info only — the repo "
                              "uses them deliberately in victory captions)")

    return findings


# ---------------------------------------------------------------------------
# Walk
# ---------------------------------------------------------------------------
def iter_cs_files(root: str) -> Iterable[str]:
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in ("Library", "Temp", "obj", "bin")]
        for name in sorted(files):
            if name.endswith(".cs"):
                yield os.path.join(base, name)


def scan_tree(root: str, check_dashes: bool) -> tuple[list[Finding], int, int]:
    findings: list[Finding] = []
    files = 0
    literals = 0

    for path in iter_cs_files(root):
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                source = handle.read()
        except OSError as exc:
            print(f"[warn] cannot read {path}: {exc}", file=sys.stderr)
            continue

        files += 1
        for lit in extract_literals(path, source):
            literals += 1
            findings.extend(scan_literal(path, lit, check_dashes))

    return findings, files, literals


# ---------------------------------------------------------------------------
# Negative control (CLAUDE.md §12 — a gate never observed failing is decoration)
# ---------------------------------------------------------------------------
SELF_TEST_SOURCE = r'''
using UnityEngine;
namespace OblastZero.Core.States
{
    // A comment naming Strelok and the Zone must NOT be flagged; comments are
    // not shipped, and the firewall documentation legitimately discusses them.
    public class Fixture
    {
        /* Strelok in a block comment is likewise invisible. */
        protected string BadNarrative =>
            "You walk out of the Zone. Sidorovich is waiting.";

        protected string GoodNarrative =>
            "You walk out of the Oblast. The registrar is waiting.";

        protected string GoodLowerZone =>
            "The contaminated zone extends past the access road.";

        // The real shipped violation this gate exists for. A capitalized article
        // before the term must not read as title case.
        protected string DeterminerZone =>
            "The Zone does not permit departures. But you have learned to ask.";

        // ...while a genuine name starting with an article stays clean, because
        // the word AFTER the term is capitalized.
        protected string TitleCasedWithArticle => "The Lens Assembly, requisitioned.";

        protected string SentenceInitialZone =>
            "Zone of exclusion, per the standing order.";

        protected string TitleCasedItem => "Issued Military Kit";

        protected string ClicheProse => "Only twisted metal and eerie silence remained.";

        protected string RegionTag = "abandoned_school";

        protected string EscapedQuote = "He said \"registered\" and filed it.";

        protected string Verbatim = @"A path\to\nothing, no escapes here.";

        protected string DoubledQuote = @"He said ""pending review"" twice.";

        private void Log()
        {
            Debug.LogError("[Fixture] Strelok left the Zone — diagnostic, not shipped.");
        }
    }
}
'''

SELF_TEST_EXPECTATIONS = [
    # (line-matching substring, kind, must be player-facing/hard)
    ("Sidorovich is waiting", "ip", True),
    ("You walk out of the Zone", "ip_ctx", True),
    ("The Zone does not permit departures", "ip_ctx", True),
    ("twisted metal", "cliche", True),
    ("eerie silence", "cliche", True),
]

SELF_TEST_MUST_BE_CLEAN = [
    "You walk out of the Oblast",
    "The contaminated zone extends",
    "The Lens Assembly, requisitioned.",
    "Zone of exclusion, per the standing order.",
    "Issued Military Kit",
    "abandoned_school",
    'He said "registered" and filed it.',
    r"A path\to\nothing, no escapes here.",
    'He said "pending review" twice.',
]


def run_self_test() -> int:
    print("=" * 74)
    print("SELF-TEST — negative control on synthetic C#")
    print("=" * 74)

    literals = extract_literals("Fixture.cs", SELF_TEST_SOURCE)
    findings: list[Finding] = []
    for lit in literals:
        findings.extend(scan_literal("Fixture.cs", lit, check_dashes=False))

    failures = 0

    # 1. Lexer sanity: the tricky literals must decode exactly.
    decoded = [lit.text for lit in literals]
    for expected in ('He said "registered" and filed it.',
                     r"A path\to\nothing, no escapes here.",
                     'He said "pending review" twice.'):
        if expected in decoded:
            print(f"  [PASS] lexer decoded: {expected[:44]!r}")
        else:
            print(f"  [FAIL] lexer did NOT decode: {expected!r}")
            failures += 1

    # 2. Comments must contribute no literals at all.
    if not any("block comment" in d or "not shipped, and the firewall" in d for d in decoded):
        print("  [PASS] comments produced no literals")
    else:
        print("  [FAIL] a comment was lexed as a literal")
        failures += 1

    # 3. Every planted violation must be caught, at the right tier.
    for needle, kind, want_hard in SELF_TEST_EXPECTATIONS:
        matched = [f for f in findings if f.kind == kind and needle.lower() in _line_text(literals, f).lower()]
        if not matched:
            print(f"  [FAIL] missed {kind} violation in: {needle!r}")
            failures += 1
            continue
        if any(f.hard for f in matched) != want_hard:
            print(f"  [FAIL] wrong tier for {kind} in {needle!r} (hard={not want_hard})")
            failures += 1
            continue
        print(f"  [PASS] caught {kind}: {needle!r}")

    # 4. Clean strings must stay clean.
    for needle in SELF_TEST_MUST_BE_CLEAN:
        noisy = [f for f in findings if needle.lower() in _line_text(literals, f).lower()]
        if noisy:
            kinds = ", ".join(sorted({f.kind for f in noisy}))
            print(f"  [FAIL] false positive ({kinds}) on: {needle!r}")
            failures += 1
        else:
            print(f"  [PASS] clean: {needle[:44]!r}")

    # 5. The diagnostic Debug.LogError must be found but non-blocking.
    diag = [f for f in findings if not f.player_facing]
    if diag and not any(f.hard for f in diag):
        print("  [PASS] Debug.LogError violations reported as info, not failures")
    else:
        print("  [FAIL] diagnostic classification wrong "
              f"({len(diag)} diagnostic finding(s), hard={any(f.hard for f in diag)})")
        failures += 1

    print("-" * 74)
    if failures:
        print(f"SELF-TEST FAILED — {failures} case(s) wrong. The gate is not trustworthy.")
        return 1
    print("SELF-TEST PASSED — every detector fires on real violations and stays quiet on clean C#.")
    return 0


def _line_text(literals: list[Literal], finding: Finding) -> str:
    for lit in literals:
        if lit.line == finding.line:
            return lit.text
    return ""


# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
def report(findings: list[Finding], files: int, literals: int) -> int:
    print("=" * 74)
    print("OblastZero — C# STRING QA (IP firewall §8 / voice §7)")
    print("=" * 74)
    print(f"Scanned {files} .cs file(s), {literals} string literal(s) under Assets/.")
    print("Player-facing literals fail the gate; diagnostics are reported as info.")
    print()

    hard = [f for f in findings if f.hard]
    info = [f for f in findings if not f.hard]

    def dump(items: list[Finding], title: str) -> None:
        if not items:
            return
        print(f"--- {title} ({len(items)}) ---")
        for f in sorted(items, key=lambda x: (x.file, x.line)):
            rel = os.path.relpath(f.file, PROJECT_ROOT)
            print(f"  {rel}:{f.line}  [{f.member}]")
            print(f"      {f.message}")
            print(f"      matched: {f.matched!r}")
        print()

    dump(hard, "VIOLATIONS — player-facing")
    dump(info, "INFO — diagnostic strings and advisories")

    print("=" * 74)
    if hard:
        by_file = len({f.file for f in hard})
        print(f"  FAIL — {len(hard)} player-facing violation(s) across {by_file} file(s).")
        return 1
    print("  PASS — no player-facing IP or voice violations in C# strings.")
    if info:
        print(f"  ({len(info)} informational hit(s) above; diagnostics never reach a player.)")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="IP firewall / voice gate for string literals in C# source.")
    parser.add_argument("--root", default=ASSETS,
                        help="directory to scan (default: the project's Assets/)")
    parser.add_argument("--self-test", action="store_true",
                        help="run the negative control and exit")
    parser.add_argument("--check-dashes", action="store_true",
                        help="also report em dashes in shipped strings (info only; "
                             "neither CLAUDE.md nor the bible forbids them, and the "
                             "victory captions use them as deliberate typography)")
    args = parser.parse_args()

    if args.self_test:
        return run_self_test()

    if not os.path.isdir(args.root):
        print(f"[error] not a directory: {args.root}", file=sys.stderr)
        return 2

    findings, files, literals = scan_tree(args.root, args.check_dashes)
    if files == 0:
        print(f"[error] no .cs files found under {args.root}", file=sys.stderr)
        return 2

    return report(findings, files, literals)


if __name__ == "__main__":
    sys.exit(main())
