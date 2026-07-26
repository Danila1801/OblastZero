#!/usr/bin/env python3
"""
visual_archetypes.py — scene-generator side of the item visual vocabulary.

Mirrors the two tables in
`Assets/Data/Scripts/Definitions/OblastZero.Data/VisualArchetype.cs`, so
`generate_scavenge_scene.py` can give each pickup a shape that matches what the
runtime spawner would build for the same item.

Two copies of one classification is exactly the hazard that has bitten this
project twice — the content seeder reverting rebalanced item weights, and the
state list duplicated between the Editor tool and the runtime fallback. So this
module does not merely restate the C# tables, it PARSES them and refuses to
agree unless they are identical (`assert_matches_csharp`). The generator calls
that before writing, and the scene is not emitted on a mismatch.

C# is the authority: it ships. This file is the follower.
"""
from __future__ import annotations

import os
import re

CSHARP_PATH = os.path.join(
    "Assets", "Data", "Scripts", "Definitions", "OblastZero.Data", "VisualArchetype.cs"
)

# Enum order must match the C# enum — the shape table is indexed by it.
ARCHETYPES = [
    "Auto", "Crate", "MetalCan", "AmmunitionBox", "Document", "WeaponSidearm",
    "WeaponLong", "Tool", "Artifact", "Clothing", "Medical", "Crew",
]

# (category or None, id substring or None, archetype). Ordered — first match wins.
RULES = [
    (None, "artifact", "Artifact"),
    ("Artifact", None, "Artifact"),
    (None, "ammo", "AmmunitionBox"),
    ("Ammunition", None, "AmmunitionBox"),
    (None, "pistol", "WeaponSidearm"),
    (None, "sidearm", "WeaponSidearm"),
    (None, "revolver", "WeaponSidearm"),
    ("Weapon", None, "WeaponLong"),
    (None, "dossier", "Document"),
    (None, "manifest", "Document"),
    (None, "intel", "Document"),
    ("Document", None, "Document"),
    (None, "medkit", "Medical"),
    (None, "bandage", "Medical"),
    (None, "syringe", "Medical"),
    (None, "suture", "Medical"),
    ("Medical", None, "Medical"),
    (None, "respirator", "Clothing"),
    (None, "coat", "Clothing"),
    (None, "jacket", "Clothing"),
    (None, "boots", "Clothing"),
    (None, "pry", "Tool"),
    (None, "wrench", "Tool"),
    ("Tool", None, "Tool"),
    (None, "canned", "MetalCan"),
    (None, "flask", "MetalCan"),
    ("Food", None, "MetalCan"),
    ("Water", None, "MetalCan"),
    ("Crafting", None, "Crate"),
    ("Special", None, "Crate"),
]

# archetype -> (primitive mesh name, (sx, sy, sz)). Indexed positionally in C#.
SHAPES = {
    "Auto":          ("Cube", (0.34, 0.34, 0.34)),
    "Crate":         ("Cube", (0.34, 0.34, 0.34)),
    "MetalCan":      ("Cylinder", (0.20, 0.13, 0.20)),
    "AmmunitionBox": ("Cube", (0.30, 0.18, 0.22)),
    "Document":      ("Cube", (0.30, 0.05, 0.24)),
    "WeaponSidearm": ("Cube", (0.26, 0.10, 0.13)),
    "WeaponLong":    ("Cube", (0.86, 0.11, 0.14)),
    "Tool":          ("Cube", (0.52, 0.12, 0.12)),
    "Artifact":      ("Sphere", (0.30, 0.30, 0.30)),
    "Clothing":      ("Capsule", (0.26, 0.16, 0.26)),
    "Medical":       ("Cube", (0.30, 0.20, 0.20)),
    "Crew":          ("Capsule", (0.78, 0.86, 0.78)),
}


def derive(category, item_id):
    """Classify an item exactly as VisualArchetypeMapping.Derive does."""
    low = (item_id or "").lower()
    for rule_cat, sub, arch in RULES:
        if rule_cat is not None and rule_cat != category:
            continue
        if sub is not None and sub not in low:
            continue
        return arch
    return "Crate"


# ─── Anti-drift gate ────────────────────────────────────────────────────────────────────

_RULE_RE = re.compile(
    r"Rule\(\s*(null|ItemCategory\.(?P<cat>\w+))\s*,\s*"
    r"(null|\"(?P<sub>[^\"]*)\")\s*,\s*VisualArchetype\.(?P<arch>\w+)\s*\)"
)
_SHAPE_RE = re.compile(
    r"Shape\(\s*PrimitiveType\.(?P<prim>\w+)\s*,\s*"
    r"(?P<x>-?[\d.]+)f\s*,\s*(?P<y>-?[\d.]+)f\s*,\s*(?P<z>-?[\d.]+)f\s*\)"
)
_ENUM_RE = re.compile(r"^\s*(?P<name>[A-Z]\w*)\s*=\s*(?P<val>\d+)\s*,", re.M)


def _block(text, tag):
    begin, end = "// OZ-ARCHETYPE-%s-BEGIN" % tag, "// OZ-ARCHETYPE-%s-END" % tag
    i, j = text.find(begin), text.find(end)
    if i < 0 or j < 0:
        raise SystemExit(
            "visual_archetypes: %s markers missing from %s — the anti-drift gate cannot read "
            "the C# tables. Restore the marker comments." % (tag, CSHARP_PATH)
        )
    return text[i + len(begin):j]


def parse_csharp(path=CSHARP_PATH):
    """Extract (enum order, rules, shapes) from the C# authority file."""
    with open(path, encoding="utf-8") as fh:
        text = fh.read()

    enum_body = text[text.find("public enum VisualArchetype"):text.find("public static class")]
    enum_names = [m.group("name") for m in _ENUM_RE.finditer(enum_body)]

    rules = []
    for m in _RULE_RE.finditer(_block(text, "RULES")):
        rules.append((m.group("cat"), m.group("sub"), m.group("arch")))

    shapes = []
    for m in _SHAPE_RE.finditer(_block(text, "SHAPES")):
        shapes.append((m.group("prim"),
                       (float(m.group("x")), float(m.group("y")), float(m.group("z")))))

    return enum_names, rules, shapes


def assert_matches_csharp(path=CSHARP_PATH):
    """
    Refuse to proceed unless this module is byte-for-byte equivalent to the C# tables.
    Returns a short confirmation string; raises SystemExit on any drift.
    """
    if not os.path.isfile(path):
        raise SystemExit("visual_archetypes: C# authority not found at %s" % path)

    enum_names, cs_rules, cs_shapes = parse_csharp(path)

    if enum_names != ARCHETYPES:
        raise SystemExit(
            "visual_archetypes: enum drift.\n  C#: %s\n  py: %s" % (enum_names, ARCHETYPES)
        )

    if cs_rules != RULES:
        lines = ["visual_archetypes: classification rule drift (C# is the authority)."]
        for i in range(max(len(cs_rules), len(RULES))):
            a = cs_rules[i] if i < len(cs_rules) else None
            b = RULES[i] if i < len(RULES) else None
            if a != b:
                lines.append("  [%d] C#=%r  py=%r" % (i, a, b))
        raise SystemExit("\n".join(lines))

    py_shapes = [SHAPES[name] for name in ARCHETYPES]
    if cs_shapes != py_shapes:
        lines = ["visual_archetypes: shape table drift (C# is the authority)."]
        for i, name in enumerate(ARCHETYPES):
            a = cs_shapes[i] if i < len(cs_shapes) else None
            b = py_shapes[i]
            if a != b:
                lines.append("  %-14s C#=%r  py=%r" % (name, a, b))
        raise SystemExit("\n".join(lines))

    return "archetype tables match C# (%d rules, %d shapes)" % (len(RULES), len(cs_shapes))


if __name__ == "__main__":
    print(assert_matches_csharp())
