#!/usr/bin/env python3
"""
Decimates the AI-generated prop GLBs into shippable, LOD-bearing meshes.

WHY: the four props under Assets/Art/Meshes/Props/ are raw generator output — 1.34M-1.49M
triangles and ~41 MB each, 165 MB for the set. The scavenge scene places 25 pickups; at
source density that is ~35M triangles and 165 MB of mesh streamed into a 60-second panic
sequence. CLAUDE.md §10 calls for quad-remeshing AI meshes before import; this tool is the
in-repo, reproducible substitute for that manual Blender step.

WHAT IT WRITES: one .bytes file per prop under Assets/Art/Resources/Props/, each containing
three LOD meshes as sibling nodes named <prop>_LOD0/_LOD1/_LOD2 sharing a single material and
one downscaled texture set. `.bytes` rather than `.glb` because Unity only exposes a file to
`Resources.Load<TextAsset>` if its extension maps to the text-script importer — a `.glb` in a
Resources folder imports as DefaultImporter and loads as null. GLTFast then parses the bytes
at runtime (see GLBPropLoader.cs).

Meshes are normalised: centred on their bounding-box centre and uniformly scaled so the
longest axis is exactly 1.0. That makes a prop's authored size irrelevant to the runtime — the
loader just applies the VisualArchetype footprint and the prop's bottom lands where the scene
generator put the primitive's bottom.

DETERMINISM: same inputs produce byte-identical outputs. Vertex clustering is grid-based with
sorted cluster ids, JSON keys are emitted sorted, and nothing samples time or randomness. That
is what makes `--check` a real drift gate. The one external variable is Pillow's JPEG encoder;
a Pillow major-version change can shift texture bytes, and `--check` will name the prop that
moved rather than failing opaquely.

USAGE
    python tools/decimate_props.py              # decimate and write outputs
    python tools/decimate_props.py --check      # regenerate in memory, fail on any drift
    python tools/decimate_props.py --self-test  # algorithm negative controls, touches no assets
"""

import argparse
import hashlib
import io
import json
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_lib  # noqa: E402

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SOURCE_DIR = os.path.join(REPO_ROOT, "Assets", "Art", "Meshes", "Props")
OUTPUT_DIR = os.path.join(REPO_ROOT, "Assets", "Art", "Resources", "Props")

# Unity's own LOD naming convention, which is also what PropLODManager looks for.
LOD_SUFFIX = "_LOD%d"

# Triangle budget per LOD. A pickup is a 0.26-0.86 m object read from 1-50 m in a dim
# interior; 8k triangles already carries far more silhouette than the player can resolve.
# 25 pickups at LOD0 is 200k triangles, which is comfortable next to the level geometry.
LOD_TRIANGLE_TARGETS = (8000, 3000, 1000)

# Longest edge of any embedded texture, in pixels. Shared across all three LODs — the meshes
# differ, the material does not, so paying for three texture sets would be pure waste.
MAX_TEXTURE_PX = 1024
JPEG_QUALITY = 92

# Clustering grid resolutions the binary search may pick from. Bounded because a grid finer
# than the source vertex spacing stops reducing anything and just burns time.
GRID_MIN, GRID_MAX = 4, 400

GENERATOR_TAG = "OblastZero tools/decimate_props.py"


# ══ MESH DECIMATION ════════════════════════════════════════════════════════════════════════

def cluster_decimate(positions, normals, uvs, triangles, grid):
    """
    Rossignac-Borrel vertex clustering at ``grid`` cells per axis.

    Vertices falling in the same cell collapse to their centroid; triangles whose corners
    collapse together become degenerate and are dropped. Chosen over quadric-error decimation
    because it is O(n), trivially vectorised, and — critically — deterministic without needing
    a stable tie-break on edge collapse order. At these reduction ratios (1.4M -> 8k, ~99.4%)
    the two look the same on a 34 cm crate.

    Returns ``(positions, normals, uvs, triangles)`` for the reduced mesh.
    """
    lo = positions.min(axis=0)
    hi = positions.max(axis=0)
    extent = np.maximum(hi - lo, 1e-9)

    cell = np.floor((positions - lo) / extent * grid).astype(np.int64)
    cell = np.clip(cell, 0, grid - 1)
    keys = (cell[:, 0] * grid + cell[:, 1]) * grid + cell[:, 2]

    # np.unique returns sorted uniques, so cluster ids are a deterministic function of
    # geometry alone — no dependence on vertex order beyond what the source already fixed.
    unique_keys, inverse = np.unique(keys, return_inverse=True)
    cluster_count = unique_keys.size

    def average_by_cluster(values):
        sums = np.zeros((cluster_count, values.shape[1]), dtype=np.float64)
        np.add.at(sums, inverse, values.astype(np.float64))
        counts = np.bincount(inverse, minlength=cluster_count).astype(np.float64)[:, None]
        return sums / counts

    out_positions = average_by_cluster(positions).astype(np.float32)
    out_uvs = average_by_cluster(uvs).astype(np.float32) if uvs is not None else None

    out_normals = average_by_cluster(normals).astype(np.float64)
    lengths = np.linalg.norm(out_normals, axis=1, keepdims=True)
    # A cluster whose members' normals cancel out (a thin feature collapsed from both sides)
    # leaves a zero vector; +Y is arbitrary but finite, and the face-normal rebuild below
    # replaces it wherever the geometry still supports one.
    degenerate = lengths[:, 0] < 1e-8
    out_normals = np.where(lengths > 1e-8, out_normals / np.maximum(lengths, 1e-12), 0.0)
    out_normals[degenerate] = (0.0, 1.0, 0.0)
    out_normals = out_normals.astype(np.float32)

    remapped = inverse[triangles]
    keep = ((remapped[:, 0] != remapped[:, 1]) &
            (remapped[:, 1] != remapped[:, 2]) &
            (remapped[:, 0] != remapped[:, 2]))
    remapped = remapped[keep]

    if remapped.size:
        # Two source triangles can collapse onto the same corner triple; coincident faces
        # z-fight and cost fill rate for nothing.
        _, first = np.unique(np.sort(remapped, axis=1), axis=0, return_index=True)
        remapped = remapped[np.sort(first)]

    return out_positions, out_normals, out_uvs, remapped.astype(np.uint32)


def compact(positions, normals, uvs, triangles):
    """Drops vertices no surviving triangle references and reindexes."""
    if triangles.size == 0:
        empty_uv = np.zeros((0, 2), np.float32) if uvs is not None else None
        return (np.zeros((0, 3), np.float32), np.zeros((0, 3), np.float32),
                empty_uv, np.zeros((0, 3), np.uint32))

    used = np.unique(triangles.reshape(-1))
    lookup = np.zeros(positions.shape[0], dtype=np.uint32)
    lookup[used] = np.arange(used.size, dtype=np.uint32)
    return (positions[used], normals[used],
            uvs[used] if uvs is not None else None,
            lookup[triangles])


def decimate_to_target(positions, normals, uvs, triangles, target):
    """
    Finds the finest clustering grid whose output still fits ``target`` triangles.

    Binary search rather than a closed-form grid size because the relationship between grid
    resolution and surviving triangle count depends on how the source mesh distributes its
    density, which varies per prop.
    """
    if triangles.shape[0] <= target:
        return compact(positions, normals, uvs, triangles)

    best = None
    low, high = GRID_MIN, GRID_MAX
    while low <= high:
        mid = (low + high) // 2
        candidate = cluster_decimate(positions, normals, uvs, triangles, mid)
        if candidate[3].shape[0] <= target:
            best = candidate
            low = mid + 1
        else:
            high = mid - 1

    if best is None:
        # Even the coarsest grid overshoots — only possible on a pathological mesh. Fall back
        # to GRID_MIN and let the caller's assertion report the real number rather than
        # silently shipping something the budget check believes it verified.
        best = cluster_decimate(positions, normals, uvs, triangles, GRID_MIN)

    return compact(*best)


# ══ SOURCE LOADING ═════════════════════════════════════════════════════════════════════════

def load_source_mesh(gltf, binary, path):
    """
    Flattens every mesh primitive in the file into one vertex/triangle soup.

    The props are single-material, so merging primitives loses nothing and spares the LOD
    builder from tracking submesh boundaries. Raises if a primitive is missing POSITION or
    indices — a non-indexed prop would silently produce zero triangles otherwise.
    """
    all_positions, all_normals, all_uvs, all_triangles = [], [], [], []
    vertex_base = 0

    for mesh in gltf.get("meshes", []):
        for primitive in mesh.get("primitives", []):
            if primitive.get("mode", 4) != 4:
                raise glb_lib.GlbError("%s: primitive mode %d is not TRIANGLES"
                                       % (path, primitive.get("mode")))
            attributes = primitive.get("attributes", {})
            if "POSITION" not in attributes:
                raise glb_lib.GlbError("%s: primitive without POSITION" % path)
            if "indices" not in primitive:
                raise glb_lib.GlbError("%s: non-indexed primitive" % path)

            positions = glb_lib.read_accessor(gltf, binary, attributes["POSITION"]).astype(np.float32)
            count = positions.shape[0]

            if "NORMAL" in attributes:
                normals = glb_lib.read_accessor(gltf, binary, attributes["NORMAL"]).astype(np.float32)
            else:
                normals = np.tile(np.array([[0, 1, 0]], np.float32), (count, 1))

            if "TEXCOORD_0" in attributes:
                uvs = glb_lib.read_accessor(gltf, binary, attributes["TEXCOORD_0"]).astype(np.float32)
            else:
                uvs = np.zeros((count, 2), np.float32)

            triangles = glb_lib.normalize_indices(
                glb_lib.read_accessor(gltf, binary, primitive["indices"]))

            all_positions.append(positions)
            all_normals.append(normals)
            all_uvs.append(uvs)
            all_triangles.append(triangles + vertex_base)
            vertex_base += count

    if not all_positions:
        raise glb_lib.GlbError("%s: no mesh primitives" % path)

    return (np.concatenate(all_positions), np.concatenate(all_normals),
            np.concatenate(all_uvs), np.concatenate(all_triangles).astype(np.uint32))


def normalize(positions):
    """Centres on the bounding-box centre and uniformly scales the longest axis to 1.0."""
    lo = positions.min(axis=0)
    hi = positions.max(axis=0)
    centre = (lo + hi) * 0.5
    longest = float(np.max(hi - lo))
    scale = 1.0 / longest if longest > 1e-9 else 1.0
    return ((positions - centre) * scale).astype(np.float32), longest


def resize_texture(payload):
    """Downscales an embedded image to MAX_TEXTURE_PX on its longest edge, re-encoded as JPEG."""
    image = Image.open(io.BytesIO(payload))
    image = image.convert("RGB")
    if max(image.size) > MAX_TEXTURE_PX:
        ratio = MAX_TEXTURE_PX / float(max(image.size))
        target = (max(1, int(round(image.size[0] * ratio))),
                  max(1, int(round(image.size[1] * ratio))))
        image = image.resize(target, Image.LANCZOS)
    out = io.BytesIO()
    image.save(out, format="JPEG", quality=JPEG_QUALITY, optimize=True, progressive=False)
    return out.getvalue(), image.size


# ══ OUTPUT ASSEMBLY ════════════════════════════════════════════════════════════════════════

def build_prop(path, name):
    """Reads one source GLB and returns ``(glb_bytes, report_dict)``."""
    gltf, binary = glb_lib.read_glb(path)
    positions, normals, uvs, triangles = load_source_mesh(gltf, binary, path)
    positions, source_extent = normalize(positions)

    builder = glb_lib.GlbBuilder()
    meshes, nodes = [], []
    lod_report = []

    for level, target in enumerate(LOD_TRIANGLE_TARGETS):
        lod_positions, lod_normals, lod_uvs, lod_triangles = decimate_to_target(
            positions, normals, uvs, triangles, target)

        if lod_triangles.shape[0] > target:
            raise AssertionError(
                "%s LOD%d: %d triangles exceeds the %d budget — the clustering search failed"
                % (name, level, lod_triangles.shape[0], target))
        if lod_triangles.size and int(lod_triangles.max()) >= lod_positions.shape[0]:
            raise AssertionError("%s LOD%d: index out of range after compaction" % (name, level))

        position_accessor = builder.add_attribute(lod_positions, with_bounds=True)
        normal_accessor = builder.add_attribute(lod_normals)
        uv_accessor = builder.add_attribute(lod_uvs, accessor_type="VEC2")
        index_accessor = builder.add_indices(lod_triangles)

        meshes.append({
            "name": name + (LOD_SUFFIX % level),
            "primitives": [{
                "attributes": {
                    "POSITION": position_accessor,
                    "NORMAL": normal_accessor,
                    "TEXCOORD_0": uv_accessor,
                },
                "indices": index_accessor,
                "material": 0,
                "mode": 4,
            }],
        })
        nodes.append({"name": name + (LOD_SUFFIX % level), "mesh": level})
        lod_report.append({
            "level": level,
            "triangles": int(lod_triangles.shape[0]),
            "vertices": int(lod_positions.shape[0]),
        })

    images = []
    texture_sizes = []
    for image in gltf.get("images", []):
        if "bufferView" not in image:
            raise glb_lib.GlbError("%s: image without a bufferView (external URI)" % path)
        view = gltf["bufferViews"][image["bufferView"]]
        start = view.get("byteOffset", 0)
        payload, size = resize_texture(binary[start:start + view["byteLength"]])
        images.append({"bufferView": builder.add_raw(payload), "mimeType": "image/jpeg"})
        texture_sizes.append(size)

    document = {
        "asset": {"version": "2.0", "generator": GENERATOR_TAG},
        "scene": 0,
        "scenes": [{"nodes": list(range(len(nodes)))}],
        "nodes": nodes,
        "meshes": meshes,
    }
    # Materials, textures and samplers carry through unchanged; their indices into `images`
    # stay valid because the image list is rebuilt in source order.
    for key in ("materials", "textures", "samplers"):
        if gltf.get(key):
            document[key] = gltf[key]
    if images:
        document["images"] = images
    if not document.get("materials"):
        document["materials"] = [{"name": name, "pbrMetallicRoughness": {
            "baseColorFactor": [0.62, 0.58, 0.5, 1.0], "metallicFactor": 0.0,
            "roughnessFactor": 0.85}}]

    report = {
        "name": name,
        "source_bytes": os.path.getsize(path),
        "source_triangles": int(triangles.shape[0]),
        "source_longest_axis_m": round(source_extent, 4),
        "lods": lod_report,
        "textures": [{"px": list(size)} for size in texture_sizes],
    }
    return builder.finish(document), report


def deterministic_guid(asset_path):
    """Unity GUID derived from the asset path, matching the scene generator's convention."""
    return hashlib.md5(("OblastZero::" + asset_path).encode("utf-8")).hexdigest()


def meta_for_bytes(asset_path):
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "TextScriptImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n" % deterministic_guid(asset_path))


def meta_for_folder(asset_path):
    return ("fileFormatVersion: 2\n"
            "guid: %s\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n" % deterministic_guid(asset_path))


def discover_sources():
    if not os.path.isdir(SOURCE_DIR):
        raise SystemExit("source directory missing: %s" % SOURCE_DIR)
    names = sorted(f for f in os.listdir(SOURCE_DIR) if f.lower().endswith(".glb"))
    if not names:
        raise SystemExit("no .glb files under %s" % SOURCE_DIR)
    return [(os.path.join(SOURCE_DIR, n), os.path.splitext(n)[0]) for n in names]


def generate_all():
    """Builds every prop in memory. Returns ``{name: (bytes, report)}``."""
    results = {}
    for path, name in discover_sources():
        results[name] = build_prop(path, name)
    return results


def unity_relative(path):
    return os.path.relpath(path, REPO_ROOT).replace(os.sep, "/")


def run_write():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    for folder in (os.path.dirname(OUTPUT_DIR), OUTPUT_DIR):
        meta_path = folder + ".meta"
        if not os.path.exists(meta_path):
            with open(meta_path, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(meta_for_folder(unity_relative(folder)))

    results = generate_all()
    total_source = total_output = 0
    manifest = []

    for name in sorted(results):
        payload, report = results[name]
        asset_path = os.path.join(OUTPUT_DIR, name + ".bytes")
        with open(asset_path, "wb") as fh:
            fh.write(payload)
        with open(asset_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
            fh.write(meta_for_bytes(unity_relative(asset_path)))

        report["output_bytes"] = len(payload)
        report["resource_key"] = "Props/" + name
        manifest.append(report)
        total_source += report["source_bytes"]
        total_output += len(payload)

        lods = " / ".join("LOD%d %d tris" % (l["level"], l["triangles"]) for l in report["lods"])
        print("  %-16s %7.1f MB -> %6.1f KB   %s"
              % (name, report["source_bytes"] / 1048576.0, len(payload) / 1024.0, lods))

    manifest_path = os.path.join(OUTPUT_DIR, "prop_manifest.json")
    with open(manifest_path, "w", encoding="utf-8", newline="\n") as fh:
        json.dump({"props": manifest}, fh, indent=2, sort_keys=True)
        fh.write("\n")
    with open(manifest_path + ".meta", "w", encoding="utf-8", newline="\n") as fh:
        fh.write(meta_for_bytes(unity_relative(manifest_path)))

    print("\n  total %.1f MB -> %.2f MB  (%.1fx reduction)"
          % (total_source / 1048576.0, total_output / 1048576.0,
             total_source / float(max(total_output, 1))))
    print("  wrote %d props to %s" % (len(manifest), unity_relative(OUTPUT_DIR)))
    return 0


def run_check():
    """Regenerates in memory and fails if any on-disk output differs. The drift gate."""
    results = generate_all()
    problems = []

    for name in sorted(results):
        payload, _ = results[name]
        asset_path = os.path.join(OUTPUT_DIR, name + ".bytes")
        if not os.path.exists(asset_path):
            problems.append("%s: missing %s" % (name, unity_relative(asset_path)))
            continue
        with open(asset_path, "rb") as fh:
            on_disk = fh.read()
        if on_disk != payload:
            problems.append("%s: on-disk output differs from a fresh decimation "
                            "(%d bytes on disk vs %d regenerated)"
                            % (name, len(on_disk), len(payload)))
        meta_path = asset_path + ".meta"
        if not os.path.exists(meta_path):
            problems.append("%s: missing %s" % (name, unity_relative(meta_path)))

    if problems:
        print("decimate_props --check FAILED")
        for problem in problems:
            print("  " + problem)
        print("\n  Re-run `python tools/decimate_props.py` to regenerate. If the only change is\n"
              "  a Pillow version bump, the texture bytes moved and regenerating is correct.")
        return 1

    print("decimate_props --check OK — %d props match a fresh decimation" % len(results))
    return 0


# ══ SELF-TEST ══════════════════════════════════════════════════════════════════════════════

def make_test_sphere(subdivisions):
    """A UV sphere with a predictable triangle count, used as synthetic decimation input."""
    rings, segments = subdivisions, subdivisions * 2
    theta = np.linspace(0, np.pi, rings + 1)
    phi = np.linspace(0, 2 * np.pi, segments + 1)
    tt, pp = np.meshgrid(theta, phi, indexing="ij")
    positions = np.stack([np.sin(tt) * np.cos(pp), np.cos(tt), np.sin(tt) * np.sin(pp)],
                         axis=-1).reshape(-1, 3).astype(np.float32)
    normals = positions.copy()
    uvs = np.stack([pp / (2 * np.pi), tt / np.pi], axis=-1).reshape(-1, 2).astype(np.float32)

    triangles = []
    for r in range(rings):
        for s in range(segments):
            a = r * (segments + 1) + s
            b = a + segments + 1
            triangles.append((a, b, a + 1))
            triangles.append((a + 1, b, b + 1))
    return positions, normals, uvs, np.array(triangles, dtype=np.uint32)


def run_self_test():
    checks, failures = 0, []

    def check(label, condition):
        nonlocal checks
        checks += 1
        if not condition:
            failures.append(label)

    positions, normals, uvs, triangles = make_test_sphere(64)
    source_triangles = triangles.shape[0]
    check("synthetic sphere is dense enough to exercise reduction", source_triangles > 10000)

    for target in (8000, 3000, 1000, 200):
        p, n, u, t = decimate_to_target(positions, normals, uvs, triangles, target)
        check("target %d: respects the triangle budget" % target, t.shape[0] <= target)
        check("target %d: produced geometry" % target, t.shape[0] > 0)
        check("target %d: reduced below source" % target, t.shape[0] < source_triangles)
        check("target %d: no out-of-range index" % target, int(t.max()) < p.shape[0])
        check("target %d: no degenerate triangle" % target,
              bool(np.all((t[:, 0] != t[:, 1]) & (t[:, 1] != t[:, 2]) & (t[:, 0] != t[:, 2]))))
        check("target %d: no duplicate triangle" % target,
              np.unique(np.sort(t, axis=1), axis=0).shape[0] == t.shape[0])
        check("target %d: every vertex is referenced" % target,
              np.unique(t.reshape(-1)).size == p.shape[0])
        check("target %d: normals are unit length" % target,
              bool(np.allclose(np.linalg.norm(n, axis=1), 1.0, atol=1e-3)))
        check("target %d: uvs survive" % target, u is not None and u.shape[0] == p.shape[0])

    first = decimate_to_target(positions, normals, uvs, triangles, 1000)
    second = decimate_to_target(positions, normals, uvs, triangles, 1000)
    check("decimation is deterministic across runs",
          np.array_equal(first[0], second[0]) and np.array_equal(first[3], second[3]))

    # NEGATIVE CONTROL — a gate never observed failing is decoration (CLAUDE.md §14).
    # Clustering at a grid coarse enough to merge everything must NOT survive the budget
    # assertion as a valid mesh; it must collapse to zero triangles.
    collapsed = cluster_decimate(positions, normals, uvs, triangles, 1)
    check("NEGATIVE CONTROL: a 1-cell grid collapses the mesh to nothing",
          collapsed[3].shape[0] == 0)

    # NEGATIVE CONTROL — the reader must reject a corrupted header rather than return junk.
    import tempfile
    with tempfile.NamedTemporaryFile(suffix=".glb", delete=False) as fh:
        fh.write(b"NOPE" + b"\x00" * 32)
        broken = fh.name
    try:
        glb_lib.read_glb(broken)
        check("NEGATIVE CONTROL: bad magic is rejected", False)
    except glb_lib.GlbError:
        check("NEGATIVE CONTROL: bad magic is rejected", True)
    finally:
        os.unlink(broken)

    # Round-trip: a written GLB must read back with the geometry we put in.
    p, n, u, t = decimate_to_target(positions, normals, uvs, triangles, 1000)
    builder = glb_lib.GlbBuilder()
    pa = builder.add_attribute(p, with_bounds=True)
    na = builder.add_attribute(n)
    ua = builder.add_attribute(u, accessor_type="VEC2")
    ia = builder.add_indices(t)
    payload = builder.finish({
        "asset": {"version": "2.0", "generator": GENERATOR_TAG},
        "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"name": "t", "mesh": 0}],
        "meshes": [{"name": "t", "primitives": [{
            "attributes": {"POSITION": pa, "NORMAL": na, "TEXCOORD_0": ua},
            "indices": ia, "mode": 4}]}],
    })
    with tempfile.NamedTemporaryFile(suffix=".glb", delete=False) as fh:
        fh.write(payload)
        round_trip = fh.name
    try:
        rt_gltf, rt_bin = glb_lib.read_glb(round_trip)
        rt_positions = glb_lib.read_accessor(rt_gltf, rt_bin, pa)
        rt_triangles = glb_lib.normalize_indices(glb_lib.read_accessor(rt_gltf, rt_bin, ia))
        check("round-trip: positions survive", np.allclose(rt_positions, p, atol=1e-6))
        check("round-trip: triangles survive", np.array_equal(rt_triangles, t))
        check("round-trip: POSITION accessor carries min/max",
              "min" in rt_gltf["accessors"][pa] and "max" in rt_gltf["accessors"][pa])
    finally:
        os.unlink(round_trip)

    # Normalisation must centre and unit-scale, so the runtime can trust the fit.
    normalized, extent = normalize(positions * 37.0)
    check("normalize: longest axis becomes 1.0",
          abs(float(np.max(normalized.max(axis=0) - normalized.min(axis=0))) - 1.0) < 1e-4)
    check("normalize: centred on origin",
          bool(np.allclose((normalized.max(axis=0) + normalized.min(axis=0)) * 0.5, 0.0, atol=1e-5)))
    check("normalize: reports the source extent", abs(extent - 74.0) < 1e-2)

    print("decimate_props --self-test: %d/%d checks passed" % (checks - len(failures), checks))
    for failure in failures:
        print("  FAIL: " + failure)
    return 1 if failures else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true",
                        help="fail if on-disk outputs differ from a fresh decimation")
    parser.add_argument("--self-test", action="store_true",
                        help="run algorithm negative controls without touching any asset")
    args = parser.parse_args()

    if args.self_test:
        return run_self_test()
    if args.check:
        return run_check()
    return run_write()


if __name__ == "__main__":
    sys.exit(main())
