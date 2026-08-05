#!/usr/bin/env python3
"""
Authors the missing `.cs.meta` sidecar for every C# file under Assets/ that does not have one.

Why this exists: Unity mints a script's GUID on import, and every headless authoring tool in this
repo needs that GUID *before* the Editor has ever seen the file — tools/scavenge_scene_lib.py
resolves each component it emits through SCRIPT_GUIDS, and CLAUDE.md §14 records what a wrong or
missing GUID costs: a component that is present in the scene, raises no error, and never runs.
With Unity closed there is no other way to get one.

The GUID is derived deterministically from the asset path (md5 of "OblastZero::meta::<path>"),
matching the derivation tools/scavenge_scene_lib.py already uses for materials, so re-running this
script can never hand the same file two different identities. Every generated GUID is checked
against every GUID already present in the project before anything is written; a collision aborts
the run rather than silently repointing an existing asset.

Usage:
    python tools/author_script_metas.py            # write the missing metas
    python tools/author_script_metas.py --check     # report only, exit 1 if any are missing
"""

import hashlib
import os
import re
import sys

PROJ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(PROJ, "Assets")

GUID_RE = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.M)

META_TEMPLATE = """fileFormatVersion: 2
guid: %s
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:%s
  assetBundleName:%s
  assetBundleVariant:%s
"""


def rel(path):
    """Unity-style forward-slash path relative to the project root."""
    return os.path.relpath(path, PROJ).replace("\\", "/")


def guid_for_path(asset_path):
    return hashlib.md5(("OblastZero::meta::" + asset_path).encode("utf-8")).hexdigest()


def collect():
    """Returns (existing_guids, missing) where missing is a list of .cs paths with no .cs.meta."""
    existing, missing = {}, []
    for root, dirs, files in os.walk(ASSETS):
        dirs[:] = [d for d in dirs if d != ".git"]
        for name in files:
            full = os.path.join(root, name)
            if name.endswith(".meta"):
                try:
                    with open(full, "r", encoding="utf-8", errors="replace") as handle:
                        match = GUID_RE.search(handle.read())
                except OSError:
                    continue
                if match:
                    existing[match.group(1)] = rel(full)
            elif name.endswith(".cs"):
                if not os.path.exists(full + ".meta"):
                    missing.append(full)
    return existing, sorted(missing)


def main():
    check_only = "--check" in sys.argv
    existing, missing = collect()

    if not missing:
        print("script metas: all %d recorded GUIDs intact, nothing missing" % len(existing))
        return 0

    print("script metas: %d C# file(s) without a .cs.meta" % len(missing))

    planned = {}
    for path in missing:
        asset_path = rel(path)
        guid = guid_for_path(asset_path)
        if guid in existing:
            print("  FAIL %s -> %s collides with %s" % (asset_path, guid, existing[guid]))
            return 1
        if guid in planned:
            print("  FAIL %s -> %s collides with %s" % (asset_path, guid, planned[guid]))
            return 1
        planned[guid] = asset_path
        print("  %s %s -> %s" % ("would write" if check_only else "writing", asset_path, guid))

    if check_only:
        return 1

    # The trailing fields Unity writes are empty; keep the shape identical to an Editor-authored
    # meta so a later reimport produces no diff.
    for guid, asset_path in planned.items():
        with open(os.path.join(PROJ, asset_path) + ".meta", "w",
                  encoding="utf-8", newline="\n") as handle:
            handle.write(META_TEMPLATE % (guid, "", "", ""))

    print("script metas: wrote %d" % len(planned))
    return 0


if __name__ == "__main__":
    sys.exit(main())
