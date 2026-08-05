"""
Minimal, dependency-light glTF-Binary (.glb) reader and writer.

Why this exists rather than `pygltflib`: every other tool in this repo runs on the stdlib plus
numpy, and the decimator needs byte-level control over chunk padding to stay deterministic.
A third-party writer that reorders JSON keys or stamps a generator version would break the
`--check` gate in tools/decimate_props.py, which is the whole point of that gate.

Scope is deliberately narrow — enough to read the four AI-generated props in
Assets/Art/Meshes/Props/ and to write back a clean, tightly-packed, non-interleaved GLB.
Sparse accessors, animations, skins and Draco compression are NOT supported; `read_glb`
raises on any of them rather than silently dropping data.
"""

import json
import struct

import numpy as np

GLB_MAGIC = 0x46546C67          # 'glTF'
CHUNK_JSON = 0x4E4F534A         # 'JSON'
CHUNK_BIN = 0x004E4942          # 'BIN\0'

# glTF componentType -> (numpy dtype, byte size)
COMPONENT_TYPES = {
    5120: (np.int8, 1),
    5121: (np.uint8, 1),
    5122: (np.int16, 2),
    5123: (np.uint16, 2),
    5125: (np.uint32, 4),
    5126: (np.float32, 4),
}

# glTF accessor type -> component count
TYPE_COMPONENTS = {
    "SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4,
    "MAT2": 4, "MAT3": 9, "MAT4": 16,
}

UNSUPPORTED_EXTENSIONS = (
    "KHR_draco_mesh_compression",
    "EXT_meshopt_compression",
)


class GlbError(Exception):
    """Raised when a GLB is malformed or uses a feature this reader deliberately refuses."""


def read_glb(path):
    """
    Parses a .glb into ``(gltf_json_dict, binary_blob_bytes)``.

    Raises GlbError rather than returning partial data: a prop that silently loses its
    normals because of an unhandled extension would show up as a shading bug three steps
    later, which is far more expensive to diagnose than a hard failure here.
    """
    with open(path, "rb") as fh:
        blob = fh.read()

    if len(blob) < 12:
        raise GlbError("%s: too short to be a GLB (%d bytes)" % (path, len(blob)))

    magic, version, declared_length = struct.unpack_from("<III", blob, 0)
    if magic != GLB_MAGIC:
        raise GlbError("%s: bad magic 0x%08X, expected 'glTF'" % (path, magic))
    if version != 2:
        raise GlbError("%s: glTF version %d, only version 2 is supported" % (path, version))
    if declared_length != len(blob):
        raise GlbError("%s: header declares %d bytes, file is %d"
                       % (path, declared_length, len(blob)))

    gltf = None
    binary = b""
    offset = 12
    while offset + 8 <= len(blob):
        chunk_length, chunk_type = struct.unpack_from("<II", blob, offset)
        offset += 8
        if offset + chunk_length > len(blob):
            raise GlbError("%s: chunk at %d overruns the file" % (path, offset - 8))
        payload = blob[offset:offset + chunk_length]
        offset += chunk_length
        if chunk_type == CHUNK_JSON:
            gltf = json.loads(payload.decode("utf-8"))
        elif chunk_type == CHUNK_BIN:
            binary = payload
        # Unknown chunk types are skipped, which is what the spec requires.

    if gltf is None:
        raise GlbError("%s: no JSON chunk" % path)

    for ext in UNSUPPORTED_EXTENSIONS:
        if ext in gltf.get("extensionsRequired", []):
            raise GlbError("%s: requires %s, which this reader does not decode" % (path, ext))

    if gltf.get("animations") or gltf.get("skins"):
        raise GlbError("%s: contains animations or skins, which this reader would drop" % path)

    return gltf, binary


def read_accessor(gltf, binary, accessor_index):
    """
    Returns an accessor's data as an ``(count, components)`` numpy array, de-interleaved.

    Handles ``byteStride`` — the AI-exported props store POSITION/NORMAL/TEXCOORD in a single
    interleaved bufferView, so ignoring stride here would read garbage that still has the
    right shape and would pass every downstream shape assertion.
    """
    accessor = gltf["accessors"][accessor_index]

    if "sparse" in accessor:
        raise GlbError("accessor %d is sparse, which this reader does not decode" % accessor_index)

    dtype, component_size = COMPONENT_TYPES[accessor["componentType"]]
    components = TYPE_COMPONENTS[accessor["type"]]
    count = accessor["count"]

    if "bufferView" not in accessor:
        # Spec-legal: an accessor with no bufferView reads as all zeros.
        return np.zeros((count, components), dtype=dtype)

    view = gltf["bufferViews"][accessor["bufferView"]]
    base = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    stride = view.get("byteStride") or (component_size * components)

    element_size = component_size * components
    if stride == element_size:
        flat = np.frombuffer(binary, dtype=dtype, count=count * components, offset=base)
        return flat.reshape(count, components)

    # Interleaved: gather one element at a time via a strided byte view.
    raw = np.frombuffer(binary, dtype=np.uint8, offset=base, count=(count - 1) * stride + element_size)
    indices = (np.arange(count)[:, None] * stride) + np.arange(element_size)[None, :]
    return raw[indices].copy().view(dtype).reshape(count, components)


def normalize_indices(array):
    """Flattens an index accessor to a contiguous uint32 triangle array of shape (n, 3)."""
    flat = np.asarray(array).reshape(-1).astype(np.uint32)
    if flat.size % 3 != 0:
        raise GlbError("index count %d is not a multiple of 3" % flat.size)
    return flat.reshape(-1, 3)


class GlbBuilder:
    """
    Accumulates buffer views and accessors into a tightly-packed, non-interleaved GLB.

    Every append returns the accessor index, so callers build meshes top-down without
    tracking offsets. Output is byte-deterministic for identical input: nothing samples time
    or randomness, JSON keys are emitted sorted, and padding bytes are fixed.
    """

    def __init__(self):
        self.buffer = bytearray()
        self.buffer_views = []
        self.accessors = []

    def _append_view(self, payload, target=None):
        # glTF requires each bufferView to start on a 4-byte boundary for the accessor
        # component types used here.
        while len(self.buffer) % 4 != 0:
            self.buffer.append(0)
        offset = len(self.buffer)
        self.buffer.extend(payload)
        view = {"buffer": 0, "byteOffset": offset, "byteLength": len(payload)}
        if target is not None:
            view["target"] = target
        self.buffer_views.append(view)
        return len(self.buffer_views) - 1

    def add_attribute(self, data, component_type=5126, accessor_type=None, with_bounds=False):
        """Appends a vertex attribute array and returns its accessor index."""
        data = np.ascontiguousarray(data)
        components = 1 if data.ndim == 1 else data.shape[1]
        accessor_type = accessor_type or {1: "SCALAR", 2: "VEC2", 3: "VEC3", 4: "VEC4"}[components]
        dtype, _ = COMPONENT_TYPES[component_type]
        data = data.astype(dtype, copy=False)

        view_index = self._append_view(data.tobytes(), target=34962)  # ARRAY_BUFFER
        accessor = {
            "bufferView": view_index,
            "componentType": component_type,
            "count": int(data.shape[0]),
            "type": accessor_type,
        }
        if with_bounds:
            # POSITION accessors MUST carry min/max per the spec; glTFast uses them to size
            # the renderer bounds, and an absent max makes props vanish under frustum culling.
            accessor["min"] = [float(v) for v in np.min(data, axis=0)]
            accessor["max"] = [float(v) for v in np.max(data, axis=0)]
        self.accessors.append(accessor)
        return len(self.accessors) - 1

    def add_indices(self, triangles):
        """Appends a triangle index array (n, 3) and returns its accessor index."""
        flat = np.ascontiguousarray(np.asarray(triangles).reshape(-1).astype(np.uint32))
        view_index = self._append_view(flat.tobytes(), target=34963)  # ELEMENT_ARRAY_BUFFER
        self.accessors.append({
            "bufferView": view_index,
            "componentType": 5125,
            "count": int(flat.size),
            "type": "SCALAR",
        })
        return len(self.accessors) - 1

    def add_raw(self, payload):
        """Appends an opaque byte payload (an embedded image) and returns its bufferView index."""
        return self._append_view(payload)

    def finish(self, gltf):
        """Serialises ``gltf`` plus the accumulated buffer into GLB bytes."""
        gltf = dict(gltf)
        gltf["bufferViews"] = self.buffer_views
        gltf["accessors"] = self.accessors
        gltf["buffers"] = [{"byteLength": len(self.buffer)}]

        # sort_keys keeps the JSON chunk stable across runs, which is what makes --check
        # a meaningful drift gate rather than a coin flip on dict ordering.
        json_bytes = json.dumps(gltf, sort_keys=True, separators=(",", ":")).encode("utf-8")
        json_pad = (4 - len(json_bytes) % 4) % 4
        json_bytes += b" " * json_pad

        bin_bytes = bytes(self.buffer)
        bin_pad = (4 - len(bin_bytes) % 4) % 4
        bin_bytes += b"\x00" * bin_pad

        total = 12 + 8 + len(json_bytes) + 8 + len(bin_bytes)
        out = bytearray()
        out += struct.pack("<III", GLB_MAGIC, 2, total)
        out += struct.pack("<II", len(json_bytes), CHUNK_JSON)
        out += json_bytes
        out += struct.pack("<II", len(bin_bytes), CHUNK_BIN)
        out += bin_bytes
        return bytes(out)
