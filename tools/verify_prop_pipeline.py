#!/usr/bin/env python3
"""
Verifies the GLB prop pipeline end to end, from source mesh to the component that spawns it.

Covers what tools/verify_steam_layer.py does not: that gate proves the project compiles, which says
nothing about whether a prop is loadable, correctly sized, wired into the scene, or still in
agreement with the C# tables that describe it. Every check here fails the same way the bug would
show up in play — a pickup rendering as a primitive, a prop the size of a building, or a component
sitting in the scene doing nothing.

USAGE
    python tools/verify_prop_pipeline.py
    python tools/verify_prop_pipeline.py --self-test   # negative controls for the checks themselves
"""

import argparse
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_lib  # noqa: E402
import visual_archetypes as va  # noqa: E402
from decimate_props import LOD_TRIANGLE_TARGETS, MAX_TEXTURE_PX, OUTPUT_DIR, SOURCE_DIR  # noqa: E402

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KEYS_CS = os.path.join(REPO_ROOT, "Assets", "_Project", "Scripts", "OblastZero.Gameplay",
                       "Props", "PropResourceKeys.cs")
REGISTRY_ASSET = os.path.join(REPO_ROOT, "Assets", "Data", "Resources", "PropArchetypeRegistry.asset")
REGISTRY_META = os.path.join(REPO_ROOT, "Assets", "_Project", "Scripts", "OblastZero.Gameplay",
                             "Props", "PropArchetypeRegistry.cs.meta")
DRESSER_META = os.path.join(REPO_ROOT, "Assets", "_Project", "Scripts", "OblastZero.Gameplay",
                            "Props", "ScavengePropDresser.cs.meta")
SCENE_PATH = os.path.join(REPO_ROOT, "Assets", "Scenes", "Scavenge.unity")
GITATTRIBUTES = os.path.join(REPO_ROOT, ".gitattributes")

# A prop bigger than this is a packaging mistake, not a quality choice: the whole point of the
# pipeline is that a 34 cm crate does not cost 41 MB.
MAX_OUTPUT_BYTES = 2 * 1024 * 1024


class Report:
    def __init__(self):
        self.passed = 0
        self.failures = []

    def check(self, label, condition, detail=""):
        if condition:
            self.passed += 1
            print("  [PASS] %s%s" % (label, (" — " + detail) if detail else ""))
        else:
            self.failures.append(label + ((" — " + detail) if detail else ""))
            print("  [FAIL] %s%s" % (label, (" — " + detail) if detail else ""))
        return condition

    @property
    def total(self):
        return self.passed + len(self.failures)


def parse_default_keys_from_csharp(path=KEYS_CS):
    """
    Extracts the archetype -> resource-key table out of PropResourceKeys.cs.

    Same discipline as tools/visual_archetypes.py: the C# is the authority, this file mirrors it,
    and the mirror is proved rather than assumed. Without this, adding a fifth prop to the C# and
    forgetting the decimator's output would produce a warning at runtime and nothing at build time.
    """
    with open(path, "r", encoding="utf-8") as handle:
        text = handle.read()

    block = re.search(r"DefaultKeys\s*=\s*new Dictionary<VisualArchetype, string>\s*\{(.*?)\};",
                      text, re.S)
    if not block:
        raise SystemExit("could not find the DefaultKeys table in %s" % path)

    constants = dict(re.findall(r'public const string (\w+)\s*=\s*"([^"]+)"\s*;', text))
    pairs = re.findall(r"\{\s*VisualArchetype\.(\w+)\s*,\s*(\w+)\s*\}", block.group(1))
    return {archetype: constants[name] for archetype, name in pairs if name in constants}


def verify_outputs(report):
    print("\n=== 1. decimated props exist and parse ===")
    sources = sorted(f[:-4] for f in os.listdir(SOURCE_DIR) if f.lower().endswith(".glb")) \
        if os.path.isdir(SOURCE_DIR) else []
    report.check("source props found", len(sources) > 0, "%d .glb" % len(sources))

    parsed = {}
    for name in sources:
        path = os.path.join(OUTPUT_DIR, name + ".bytes")
        if not report.check("%s: decimated output exists" % name, os.path.exists(path),
                            os.path.relpath(path, REPO_ROOT)):
            continue
        report.check("%s: .meta exists" % name, os.path.exists(path + ".meta"))

        size = os.path.getsize(path)
        report.check("%s: output under the size ceiling" % name, size <= MAX_OUTPUT_BYTES,
                     "%.0f KB" % (size / 1024.0))
        try:
            parsed[name] = glb_lib.read_glb(path)
            report.check("%s: parses as glTF 2 binary" % name, True)
        except glb_lib.GlbError as error:
            report.check("%s: parses as glTF 2 binary" % name, False, str(error))
    return parsed


def verify_lods(report, parsed):
    print("\n=== 2. LOD structure and triangle budgets ===")
    for name in sorted(parsed):
        gltf, _binary = parsed[name]
        nodes = gltf.get("nodes", [])
        expected = [name + "_LOD%d" % i for i in range(len(LOD_TRIANGLE_TARGETS))]
        actual = [node.get("name") for node in nodes]
        report.check("%s: three LOD nodes, correctly named" % name, actual == expected,
                     ", ".join(str(a) for a in actual))

        accessors = gltf.get("accessors", [])
        for level, target in enumerate(LOD_TRIANGLE_TARGETS):
            if level >= len(gltf.get("meshes", [])):
                report.check("%s LOD%d: mesh present" % (name, level), False)
                continue
            primitives = gltf["meshes"][level].get("primitives", [])
            triangles = sum(accessors[p["indices"]]["count"] // 3 for p in primitives if "indices" in p)
            report.check("%s LOD%d: within the %d triangle budget" % (name, level, target),
                         0 < triangles <= target, "%d tris" % triangles)

        # Each LOD must actually be coarser than the one above it, or the LODGroup is doing nothing
        # but adding draw-call overhead.
        counts = []
        for level in range(min(len(LOD_TRIANGLE_TARGETS), len(gltf.get("meshes", [])))):
            primitives = gltf["meshes"][level].get("primitives", [])
            counts.append(sum(accessors[p["indices"]]["count"] // 3 for p in primitives if "indices" in p))
        report.check("%s: LODs strictly decrease" % name,
                     all(counts[i] > counts[i + 1] for i in range(len(counts) - 1)),
                     " > ".join(str(c) for c in counts))


def verify_normalization(report, parsed):
    print("\n=== 3. meshes are normalised for the runtime fit ===")
    for name in sorted(parsed):
        gltf, _binary = parsed[name]
        mesh = gltf["meshes"][0]["primitives"][0]
        accessor = gltf["accessors"][mesh["attributes"]["POSITION"]]

        if not report.check("%s: POSITION carries min/max" % name,
                            "min" in accessor and "max" in accessor,
                            "absent min/max makes props vanish under frustum culling"):
            continue

        low, high = accessor["min"], accessor["max"]
        size = [high[i] - low[i] for i in range(3)]
        centre = [(high[i] + low[i]) * 0.5 for i in range(3)]

        report.check("%s: longest axis is 1.0" % name, abs(max(size) - 1.0) < 0.01,
                     "%.4f" % max(size))
        report.check("%s: centred on origin" % name, max(abs(c) for c in centre) < 0.01,
                     "offset %.4f" % max(abs(c) for c in centre))


def verify_textures(report, parsed):
    print("\n=== 4. textures downscaled and embedded ===")
    try:
        from PIL import Image
        import io
    except ImportError:
        report.check("Pillow available for texture inspection", False)
        return

    for name in sorted(parsed):
        gltf, binary = parsed[name]
        images = gltf.get("images", [])
        report.check("%s: textures embedded" % name, len(images) > 0, "%d images" % len(images))
        for index, image in enumerate(images):
            view = gltf["bufferViews"][image["bufferView"]]
            start = view.get("byteOffset", 0)
            payload = binary[start:start + view["byteLength"]]
            with Image.open(io.BytesIO(payload)) as opened:
                report.check("%s: image %d within %dpx" % (name, index, MAX_TEXTURE_PX),
                             max(opened.size) <= MAX_TEXTURE_PX, "%dx%d" % opened.size)


def verify_csharp_mirror(report):
    print("\n=== 5. C# tables agree with the shipped files ===")
    print("  " + va.assert_matches_csharp())
    report.passed += 1

    keys = parse_default_keys_from_csharp()
    report.check("PropResourceKeys.DefaultKeys parsed", len(keys) > 0,
                 ", ".join(sorted(keys)))

    for archetype, key in sorted(keys.items()):
        report.check("%s -> %s resolves to a shipped file" % (archetype, key),
                     os.path.exists(os.path.join(REPO_ROOT, "Assets", "Art", "Resources", key + ".bytes")),
                     "expected Assets/Art/Resources/%s.bytes" % key)
        report.check("%s is a real archetype" % archetype, archetype in va.ARCHETYPES)

    # Every prop the decimator emits should be claimed by an archetype, or it ships dead weight.
    shipped = sorted(f[:-6] for f in os.listdir(OUTPUT_DIR) if f.endswith(".bytes")) \
        if os.path.isdir(OUTPUT_DIR) else []
    claimed = {os.path.basename(k) for k in keys.values()}
    orphans = [s for s in shipped if s not in claimed]
    report.check("no orphaned prop files", not orphans, ", ".join(orphans) or "none")


def verify_registry(report):
    print("\n=== 6. registry asset wired to its script ===")
    if not report.check("registry asset exists", os.path.exists(REGISTRY_ASSET),
                        os.path.relpath(REGISTRY_ASSET, REPO_ROOT)):
        return
    if not report.check("registry lives under a Resources/ folder",
                        "Resources" in REGISTRY_ASSET.replace(os.sep, "/").split("/")):
        return

    with open(REGISTRY_ASSET, "r", encoding="utf-8") as handle:
        asset = handle.read()

    meta_guid = None
    with open(REGISTRY_META, "r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                meta_guid = line.split(":", 1)[1].strip()
                break

    referenced = re.search(r"m_Script: \{fileID: 11500000, guid: (\w+), type: 3\}", asset)
    report.check("asset m_Script guid matches the script meta",
                 referenced is not None and meta_guid is not None
                 and referenced.group(1) == meta_guid,
                 "guid=%s" % (referenced.group(1) if referenced else "none"))

    entries = re.findall(r"^  - archetype: (\d+)$", asset, re.M)
    expected = len(va.ARCHETYPES) - 1  # Auto is never rendered and carries no entry
    report.check("one entry per renderable archetype", len(entries) == expected,
                 "%d entries, expected %d" % (len(entries), expected))

    indices = sorted(int(e) for e in entries)
    report.check("no duplicate archetype entries", len(set(indices)) == len(indices))
    report.check("Auto has no entry", 0 not in indices)

    for key in re.findall(r"^    resourceKey: (\S+)$", asset, re.M):
        report.check("registry key %s resolves" % key,
                     os.path.exists(os.path.join(REPO_ROOT, "Assets", "Art", "Resources", key + ".bytes")))


def verify_scene(report):
    print("\n=== 7. dresser present in the scavenge scene ===")
    if not report.check("scene exists", os.path.exists(SCENE_PATH)):
        return

    dresser_guid = None
    with open(DRESSER_META, "r", encoding="utf-8") as handle:
        for line in handle:
            if line.startswith("guid:"):
                dresser_guid = line.split(":", 1)[1].strip()
                break
    report.check("ScavengePropDresser.cs has a meta", dresser_guid is not None)

    with open(SCENE_PATH, "r", encoding="utf-8") as handle:
        scene = handle.read()

    report.check("scene names the dresser GameObject", "Scavenge_Prop_Dresser" in scene)
    report.check("scene references the dresser script guid",
                 dresser_guid is not None and dresser_guid in scene,
                 "a wrong guid yields a silently unassigned component, not an error")
    report.check("dresser is configured to run on start",
                 re.search(r"dressOnStart: 1", scene) is not None)


def verify_git_hygiene(report):
    print("\n=== 8. repository hygiene ===")
    with open(GITATTRIBUTES, "r", encoding="utf-8") as handle:
        attributes = handle.read()
    report.check("*.glb routed to Git LFS", re.search(r"^\*\.glb\s+lfs\s*$", attributes, re.M) is not None,
                 "source props are 40 MB each; without this they land in git as raw blobs")
    report.check("*.gltf routed to Git LFS",
                 re.search(r"^\*\.gltf\s+lfs\s*$", attributes, re.M) is not None)


def run_self_test():
    """Negative controls: each detector must reject input it is supposed to catch."""
    print("=== verify_prop_pipeline --self-test ===")
    report = Report()

    # The C# mirror parser must find a real table, and must fail loudly on a file without one.
    keys = parse_default_keys_from_csharp()
    report.check("parses the real DefaultKeys table", len(keys) == 4, ", ".join(sorted(keys)))

    import tempfile
    with tempfile.NamedTemporaryFile("w", suffix=".cs", delete=False, encoding="utf-8") as handle:
        handle.write("namespace X { public static class Y { } }")
        empty = handle.name
    try:
        parse_default_keys_from_csharp(empty)
        report.check("NEGATIVE CONTROL: a file with no table is rejected", False)
    except SystemExit:
        report.check("NEGATIVE CONTROL: a file with no table is rejected", True)
    finally:
        os.unlink(empty)

    # A truncated GLB must not parse as valid.
    prop = os.path.join(OUTPUT_DIR, "prop_crate.bytes")
    if os.path.exists(prop):
        with open(prop, "rb") as handle:
            good = handle.read()
        with tempfile.NamedTemporaryFile(suffix=".glb", delete=False) as handle:
            handle.write(good[:len(good) // 2])
            truncated = handle.name
        try:
            glb_lib.read_glb(truncated)
            report.check("NEGATIVE CONTROL: a truncated GLB is rejected", False)
        except glb_lib.GlbError:
            report.check("NEGATIVE CONTROL: a truncated GLB is rejected", True)
        finally:
            os.unlink(truncated)

        # And the intact one must still parse, so the control above is not passing for a dumb reason.
        try:
            glb_lib.read_glb(prop)
            report.check("the intact GLB still parses", True)
        except glb_lib.GlbError as error:
            report.check("the intact GLB still parses", False, str(error))

    print("\n%d/%d self-test checks passed" % (report.passed, report.total))
    for failure in report.failures:
        print("  FAILED: " + failure)
    return 1 if report.failures else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--self-test", action="store_true",
                        help="run negative controls for the checks themselves")
    args = parser.parse_args()

    if args.self_test:
        return run_self_test()

    print("=== OblastZero prop pipeline verification ===")
    report = Report()
    parsed = verify_outputs(report)
    verify_lods(report, parsed)
    verify_normalization(report, parsed)
    verify_textures(report, parsed)
    verify_csharp_mirror(report)
    verify_registry(report)
    verify_scene(report)
    verify_git_hygiene(report)

    print("\n==============================================")
    print("%d/%d checks passed" % (report.passed, report.total))
    if report.failures:
        for failure in report.failures:
            print("  FAILED: " + failure)
        return 1
    print("ALL GREEN")
    return 0


if __name__ == "__main__":
    sys.exit(main())
