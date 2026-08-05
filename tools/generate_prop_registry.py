#!/usr/bin/env python3
"""
Emits Assets/Data/Resources/PropArchetypeRegistry.asset.

The registry maps each VisualArchetype onto a decimated prop mesh plus the transform corrections
applied when one is instantiated. It is generated rather than hand-authored for the same reason
Scavenge.unity is: the archetype list lives in VisualArchetype.cs and is already mirrored and
drift-checked by tools/visual_archetypes.py, so an entry list typed by hand is one rename away
from silently omitting an archetype.

The asset is OPTIONAL at runtime — GLBPropLoader falls back to PropArchetypeRegistry.CreateDefault()
when it is absent, and that C# method produces the same defaults this script does. Shipping the
asset makes the mapping visible and tweakable in the Inspector; deleting it changes nothing.

USAGE
    python tools/generate_prop_registry.py            # write the asset
    python tools/generate_prop_registry.py --check    # fail if on-disk output has drifted
"""

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import visual_archetypes as va  # noqa: E402

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_PATH = os.path.join(REPO_ROOT, "Assets", "Data", "Resources", "PropArchetypeRegistry.asset")
SCRIPT_META = os.path.join(REPO_ROOT, "Assets", "_Project", "Scripts", "OblastZero.Gameplay",
                           "Props", "PropArchetypeRegistry.cs.meta")

# Must match PropResourceKeys.DefaultKeys. Only four archetypes have authored meshes; the other
# seven intentionally resolve to an empty key and fall back to their primitive silhouette.
AUTHORED_MESHES = {
    "Crate": "Props/prop_crate",
    "AmmunitionBox": "Props/prop_ammo_box",
    "Artifact": "Props/prop_artifact",
    "Tool": "Props/prop_pry_bar",
}

# Archetypes whose scene material must survive the mesh swap. The artifact reads as findable in a
# dark pit only because its material is emissive; the glTF material would make it just another rock.
KEEP_SCENE_MATERIAL = {"Artifact"}

FIT_UNIFORM = 0


def script_guid():
    """Reads the PropArchetypeRegistry.cs GUID from its .meta — never guess a GUID (CLAUDE.md §14)."""
    if not os.path.exists(SCRIPT_META):
        raise SystemExit(
            "missing %s\nUnity has not imported PropArchetypeRegistry.cs yet; open the Editor once,\n"
            "or wait for the asset import to finish, then re-run." % os.path.relpath(SCRIPT_META, REPO_ROOT))
    with open(SCRIPT_META, "r", encoding="utf-8") as fh:
        for line in fh:
            if line.startswith("guid:"):
                return line.split(":", 1)[1].strip()
    raise SystemExit("no guid line in %s" % SCRIPT_META)


def build_asset():
    guid = script_guid()
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        "  m_Script: {fileID: 11500000, guid: %s, type: 3}" % guid,
        "  m_Name: PropArchetypeRegistry",
        "  m_EditorClassIdentifier: ",
        "  entries:",
    ]

    written = 0
    for index, archetype in enumerate(va.ARCHETYPES):
        if archetype == "Auto":
            continue  # never rendered; VisualArchetypeMapping.Resolve turns it into a real archetype
        key = AUTHORED_MESHES.get(archetype, "")
        lines.extend([
            "  - archetype: %d" % index,
            "    resourceKey: %s" % key,
            "    fitMode: %d" % FIT_UNIFORM,
            "    extraScale: {x: 1, y: 1, z: 1}",
            "    positionOffset: {x: 0, y: 0, z: 0}",
            "    rotationEuler: {x: 0, y: 0, z: 0}",
            "    useLOD: 1",
            "    lodCount: 3",
            "    useSceneMaterial: %d" % (1 if archetype in KEEP_SCENE_MATERIAL else 0),
        ])
        written += 1

    return "\n".join(lines) + "\n", written


def meta_for_asset():
    import hashlib
    relative = os.path.relpath(OUTPUT_PATH, REPO_ROOT).replace(os.sep, "/")
    guid = hashlib.md5(("OblastZero::" + relative).encode("utf-8")).hexdigest()
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "NativeFormatImporter:\n"
            "  externalObjects: {}\n"
            "  mainObjectFileID: 11400000\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n" % guid)


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="fail if the on-disk asset has drifted")
    args = parser.parse_args()

    # The archetype mirror must agree with the C# before we bake archetype indices into an asset;
    # a stale mirror would map every entry to the wrong enum value.
    print("archetype check: " + va.assert_matches_csharp())

    content, count = build_asset()

    if args.check:
        if not os.path.exists(OUTPUT_PATH):
            print("generate_prop_registry --check FAILED: %s is missing"
                  % os.path.relpath(OUTPUT_PATH, REPO_ROOT))
            return 1
        with open(OUTPUT_PATH, "r", encoding="utf-8") as fh:
            if fh.read() != content:
                print("generate_prop_registry --check FAILED: on-disk asset differs from a fresh build")
                return 1
        print("generate_prop_registry --check OK — %d entries match" % count)
        return 0

    os.makedirs(os.path.dirname(OUTPUT_PATH), exist_ok=True)
    with open(OUTPUT_PATH, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(content)
    if not os.path.exists(OUTPUT_PATH + ".meta"):
        with open(OUTPUT_PATH + ".meta", "w", encoding="utf-8", newline="\n") as fh:
            fh.write(meta_for_asset())

    authored = sum(1 for a in va.ARCHETYPES if a in AUTHORED_MESHES)
    print("wrote %s — %d entries, %d with authored meshes, %d falling back to primitives"
          % (os.path.relpath(OUTPUT_PATH, REPO_ROOT), count, authored, count - authored))
    return 0


if __name__ == "__main__":
    sys.exit(main())
