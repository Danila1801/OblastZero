#!/usr/bin/env python3
"""Prove the bunker event modal lays out correctly, without opening Unity.

WHY THIS EXISTS
---------------
The Continue button in EventModalUI shipped at 4.5 pixels tall. It was not a
rendering fault and not a timer: the outcome container carried a LayoutElement
with preferredHeight = 1, a LayoutElement outranks a LayoutGroup in Unity's
LayoutUtility, and the button's own minHeight was 0, so it resolved to
Lerp(0, 72, 0.0625). TextMeshPro overflows its rect, so the word CONTINUE
rendered at full size over a 4.5px hitbox. Players reported it as "you have to
wait a long time before you can press continue", because a missed click and a
wait feel identical from the other side of the screen.

A compile check cannot see any of that, and neither can a human reading the
file: the number 1 looks harmless next to a number 72. The only thing that
catches it is doing the arithmetic Unity does.

This script re-implements UnityEngine.UI.HorizontalOrVerticalLayoutGroup's
vertical pass and LayoutUtility's property resolution, reads the real constants
out of EventModalUI.cs (it parses the C#, it does not mirror it, so drift in the
source fails the gate instead of being silently ignored), and asserts:

  1. The Continue button is its full height for every plausible outcome-text
     size, never a sliver.
  2. Every choice button is fully inside the card, for one through four
     choices. A fourth choice used to render off the bottom of the screen.

Negative control at the bottom: the pre-fix values must FAIL both assertions.
A gate never observed failing is decoration.

Exit 0 = green.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
MODAL = REPO / "Assets" / "_Project" / "Scripts" / "UI" / "EventModalUI.cs"

UNSET = -1.0


# --------------------------------------------------------------------------
# Unity layout model
# --------------------------------------------------------------------------

class Elem:
    """One row in a layout group.

    min/preferred of UNSET mean "this row has no LayoutElement override", which
    is how a nested LayoutGroup gets to report its own measured size. Unity's
    LayoutUtility skips negative values when resolving a property, which is the
    entire mechanism the fix relies on.
    """

    def __init__(self, name: str, min_h: float, pref_h: float, flex_h: float,
                 group: "Group | None" = None):
        self.name = name
        self._min = min_h
        self._pref = pref_h
        self.flex = flex_h
        self.group = group

    def min_size(self) -> float:
        if self._min >= 0:
            return self._min
        return self.group.total_min() if self.group else 0.0

    def pref_size(self) -> float:
        # LayoutUtility.GetPreferredHeight returns max(min, preferred).
        own = self._pref if self._pref >= 0 else (
            self.group.total_pref() if self.group else 0.0)
        return max(self.min_size(), own)


class Group:
    """A VerticalLayoutGroup with childControlHeight and no force-expand."""

    def __init__(self, name: str, spacing: float, pad_top: float, pad_bottom: float,
                 children: list[Elem]):
        self.name = name
        self.spacing = spacing
        self.pad_v = pad_top + pad_bottom
        self.children = children

    def _spacing_total(self) -> float:
        return self.spacing * max(0, len(self.children) - 1)

    def total_min(self) -> float:
        return self.pad_v + self._spacing_total() + sum(c.min_size() for c in self.children)

    def total_pref(self) -> float:
        return self.pad_v + self._spacing_total() + sum(c.pref_size() for c in self.children)

    def total_flex(self) -> float:
        return sum(c.flex for c in self.children)

    def resolve(self, size: float) -> dict[str, float]:
        """Port of HorizontalOrVerticalLayoutGroup.SetChildrenAlongAxis, axis 1."""
        total_min = self.total_min()
        total_pref = self.total_pref()
        total_flex = self.total_flex()

        surplus = size - total_pref
        flex_multiplier = 0.0
        if surplus > 0 and total_flex > 0:
            flex_multiplier = surplus / total_flex

        minmax_lerp = 0.0
        if total_min != total_pref:
            minmax_lerp = max(0.0, min(1.0, (size - total_min) / (total_pref - total_min)))

        out: dict[str, float] = {}
        for c in self.children:
            child_size = c.min_size() + (c.pref_size() - c.min_size()) * minmax_lerp
            child_size += c.flex * flex_multiplier
            out[c.name] = child_size
        return out


# --------------------------------------------------------------------------
# Parse the real numbers out of the C#
# --------------------------------------------------------------------------

def parse_modal() -> dict[str, float]:
    if not MODAL.exists():
        fail(f"cannot find {MODAL}")
    src = MODAL.read_text(encoding="utf-8")

    def grab(pattern: str, what: str) -> str:
        m = re.search(pattern, src)
        if not m:
            fail(f"could not parse {what} out of EventModalUI.cs. The source moved; "
                 f"update this gate rather than deleting it.")
        return m.group(1)

    def num(pattern: str, what: str) -> float:
        raw = grab(pattern, what)
        if raw.strip() == "UNSET":
            return UNSET
        if raw.strip() == "CONTINUE_HEIGHT":
            return float(grab(r"CONTINUE_HEIGHT\s*=\s*([0-9.]+)f", "CONTINUE_HEIGHT"))
        return float(raw.rstrip("f"))

    v = {}
    v["card_w"], v["card_h"] = (
        float(grab(r"crt\.sizeDelta\s*=\s*new Vector2\(([0-9.]+)f", "card width")),
        float(grab(r"crt\.sizeDelta\s*=\s*new Vector2\([0-9.]+f,\s*([0-9.]+)f", "card height")),
    )
    v["card_pad_top"] = float(grab(r"vlg\.padding\s*=\s*new RectOffset\(\d+,\s*\d+,\s*(\d+)", "card pad top"))
    v["card_pad_bot"] = float(grab(r"vlg\.padding\s*=\s*new RectOffset\(\d+,\s*\d+,\s*\d+,\s*(\d+)", "card pad bottom"))
    v["card_spacing"] = float(grab(r"vlg\.spacing\s*=\s*([0-9.]+)f", "card spacing"))
    v["choices_spacing"] = float(grab(r"cvlg\.spacing\s*=\s*([0-9.]+)f", "choices spacing"))
    v["outcome_spacing"] = float(grab(r"ovlg\.spacing\s*=\s*([0-9.]+)f", "outcome spacing"))

    v["title_min"] = num(r"AddFlexibleHeight\(_title\.gameObject,\s*([-\w.]+)f?,", "title min")
    v["title_pref"] = num(r"AddFlexibleHeight\(_title\.gameObject,\s*[-\w.]+f?,\s*([-\w.]+)f?\)", "title preferred")

    v["narr_min"] = num(r"AddFlexibleHeight\(_narrative\.gameObject,\s*([-\w.]+)f?,", "narrative min")
    v["narr_pref"] = num(r"AddFlexibleHeight\(_narrative\.gameObject,\s*[-\w.]+f?,\s*([-\w.]+)f?,", "narrative preferred")
    v["narr_flex"] = num(r"AddFlexibleHeight\(_narrative\.gameObject,[^)]*flexible:\s*([-\w.]+)f?\)", "narrative flexible")

    v["choices_min"] = num(r"AddFlexibleHeight\(choicesGO,\s*([-\w.]+)f?,", "choices min")
    v["choices_pref"] = num(r"AddFlexibleHeight\(choicesGO,\s*[-\w.]+f?,\s*([-\w.]+)f?,", "choices preferred")

    v["outcome_min"] = num(r"AddFlexibleHeight\(outcomeGO,\s*([-\w.]+)f?,", "outcome min")
    v["outcome_pref"] = num(r"AddFlexibleHeight\(outcomeGO,\s*[-\w.]+f?,\s*([-\w.]+)f?,", "outcome preferred")

    v["otext_min"] = num(r"AddFlexibleHeight\(_outcomeText\.gameObject,\s*([-\w.]+)f?,", "outcome text min")
    v["otext_pref"] = num(r"AddFlexibleHeight\(_outcomeText\.gameObject,\s*[-\w.]+f?,\s*([-\w.]+)f?,", "outcome text preferred")

    v["cont_min"] = num(r"AddFlexibleHeight\(_continueButton\.gameObject,\s*([-\w.]+)f?,", "continue min")
    v["cont_pref"] = num(r"AddFlexibleHeight\(_continueButton\.gameObject,\s*[-\w.]+f?,\s*([-\w.]+)f?\)", "continue preferred")

    v["choice_btn_min"] = float(grab(r"le\.minHeight\s*=\s*([0-9.]+)f", "choice button min"))
    v["choice_btn_pref"] = float(grab(r"le\.preferredHeight\s*=\s*([0-9.]+)f", "choice button preferred"))
    return v


# --------------------------------------------------------------------------
# Scenario builders
# --------------------------------------------------------------------------

# The constant set as it shipped before the fix, kept verbatim so the negative
# control reproduces the real defect rather than an approximation of it. A
# partial override does not reproduce it: LayoutUtility returns max(min,
# preferred), so pinning preferred to 1 while leaving min unset is silently
# neutralised by the container's own measured minimum. Both numbers have to be
# wrong together, which is exactly why the bug was hard to see by reading.
LEGACY = {
    "title_min": 0.0, "title_pref": 64.0,
    "narr_min": 240.0, "narr_pref": 1.0, "narr_flex": 1.0,
    "choices_min": 0.0, "choices_pref": 1.0,
    "outcome_min": 0.0, "outcome_pref": 1.0, "outcome_flex": 1.0,
    "otext_min": 120.0, "otext_pref": 1.0, "otext_flex": 1.0,
    "cont_min": 0.0, "cont_pref": 72.0,
}


def _vals(v: dict, legacy: bool) -> dict:
    """Current parsed constants, or the pre-fix set for the negative control."""
    if not legacy:
        return v
    merged = dict(v)
    merged.update(LEGACY)
    return merged


def build_outcome_card(v: dict, outcome_text_natural: float,
                       legacy: bool = False) -> tuple[Group, Group]:
    """The card as it stands while an outcome is on screen (choices hidden)."""
    c = _vals(v, legacy)
    otext_pref = (max(c["otext_min"], outcome_text_natural)
                  if c["otext_pref"] == UNSET else c["otext_pref"])
    otext = Elem("OutcomeText", c["otext_min"], otext_pref, c.get("otext_flex", 0.0))
    cont = Elem("Continue", c["cont_min"], c["cont_pref"], 0.0)
    outcome_group = Group("Outcome", c["outcome_spacing"], 0, 0, [otext, cont])

    outcome_row = Elem("Outcome", c["outcome_min"], c["outcome_pref"],
                       c.get("outcome_flex", 0.0), group=outcome_group)

    card = Group("Card", c["card_spacing"], c["card_pad_top"], c["card_pad_bot"], [
        Elem("Title", c["title_min"], c["title_pref"], 0.0),
        Elem("Narrative", c["narr_min"], c["narr_pref"], c["narr_flex"]),
        outcome_row,
    ])
    return card, outcome_group


def build_choices_card(v: dict, n_choices: int, legacy: bool = False) -> tuple[Group, Group]:
    """The card as it stands while the choices are on screen (outcome hidden)."""
    c = _vals(v, legacy)
    buttons = [Elem(f"Choice{i}", c["choice_btn_min"], c["choice_btn_pref"], 0.0)
               for i in range(n_choices)]
    choices_group = Group("Choices", c["choices_spacing"], 0, 0, buttons)

    choices_row = Elem("Choices", c["choices_min"], c["choices_pref"], 0.0, group=choices_group)

    card = Group("Card", c["card_spacing"], c["card_pad_top"], c["card_pad_bot"], [
        Elem("Title", c["title_min"], c["title_pref"], 0.0),
        Elem("Narrative", c["narr_min"], c["narr_pref"], c["narr_flex"]),
        choices_row,
    ])
    return card, choices_group


# --------------------------------------------------------------------------
# Checks
# --------------------------------------------------------------------------

PASS, FAILED = 0, 0


def check(ok: bool, msg: str) -> bool:
    global PASS, FAILED
    if ok:
        PASS += 1
        print(f"  [PASS] {msg}")
    else:
        FAILED += 1
        print(f"  [FAIL] {msg}")
    return ok


def fail(msg: str):
    print(f"\n[FATAL] {msg}")
    sys.exit(2)


def continue_height(v: dict, natural: float, legacy: bool = False) -> float:
    card, outcome_group = build_outcome_card(v, natural, legacy=legacy)
    rows = card.resolve(v["card_h"])
    inner = outcome_group.resolve(rows["Outcome"])
    return inner["Continue"]


def choices_overflow(v: dict, n: int, legacy: bool = False) -> float:
    """How many pixels the choice list spills past the bottom of the card. <= 0 is good."""
    card, choices_group = build_choices_card(v, n, legacy=legacy)
    rows = card.resolve(v["card_h"])
    allotted = rows["Choices"]
    needed = choices_group.total_min()
    return needed - allotted


def main() -> int:
    v = parse_modal()

    print("=== parsed from EventModalUI.cs ===")
    print(f"  card {v['card_w']:.0f} x {v['card_h']:.0f}, padding top/bottom "
          f"{v['card_pad_top']:.0f}/{v['card_pad_bot']:.0f}, spacing {v['card_spacing']:.0f}")
    print(f"  continue min={v['cont_min']:.0f} preferred={v['cont_pref']:.0f}")
    print(f"  outcome container min={v['outcome_min']:.0f} preferred={v['outcome_pref']:.0f}")
    print(f"  choices container min={v['choices_min']:.0f} preferred={v['choices_pref']:.0f}")

    print("\n=== 1. Continue button is never a sliver ===")
    target = v["cont_pref"]
    worst = None
    for natural in range(0, 401, 20):
        h = continue_height(v, float(natural))
        if worst is None or h < worst[1]:
            worst = (natural, h)
    check(worst[1] >= target - 0.01,
          f"Continue is {worst[1]:.1f}px at its worst (outcome text natural {worst[0]}px), "
          f"target {target:.0f}px")
    check(worst[1] >= 44.0,
          f"Continue clears the 44px minimum touch/click target ({worst[1]:.1f}px)")

    print("\n=== 2. Every choice fits inside the card ===")
    for n in (1, 2, 3, 4):
        over = choices_overflow(v, n)
        check(over <= 0.01, f"{n} choice(s): spill past card bottom = {max(0.0, over):.1f}px")

    print("\n=== 3. Negative control: the pre-fix values must FAIL ===")
    bad_h = continue_height(v, 120.0, legacy=True)
    check(4.0 <= bad_h <= 5.0,
          f"old values reproduce the reported bug exactly: Continue resolves to {bad_h:.1f}px "
          f"(expected ~4.5px; this check PASSES by detecting the bug, proving the gate can fail)")
    bad_over = choices_overflow(v, 4, legacy=True)
    check(bad_over > 100.0,
          f"old values reproduce the overflow: 4 choices spill {bad_over:.1f}px past the card")

    print("\n" + "=" * 46)
    print(f"{PASS}/{PASS + FAILED} checks passed")
    if FAILED:
        print("FAILED")
        return 1
    print("ALL GREEN")
    return 0


if __name__ == "__main__":
    sys.exit(main())
