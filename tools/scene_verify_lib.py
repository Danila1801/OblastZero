#!/usr/bin/env python3
"""
Shared validation gates for every generated scavenge scene.

WHY THIS MODULE EXISTS
======================
`generate_scavenge_scene.py` grew these gates one real failure at a time — six pickups buried
inside solids, two routes sealed by box colliders on debris meshes, a pit that was reachable but
not escapable, a nav grid whose bytes described a different level than the object they came from.
Each one is here because it caught something.

The Oblast has three scavenge sites. Copy-pasting ~600 lines of validation into each generator
would mean three copies of every gate, drifting apart at three different rates, with the newest
site — the one least play-tested and most in need of them — running the oldest copy. So the gates
live here once and every generator imports them.

DESIGN RULE FOR THIS FILE: everything in it is site-agnostic. Nothing here may know about the
Grain Depot's pit ramp, the Census Office's flooded basement or the Reservoir's catwalk. A gate
that needs site knowledge takes it as an argument. If you find yourself adding a site name to this
module, the parameter is missing, not the special case.

The CharacterController metrics the walkability flood-fill models (height 1.8, radius 0.35, step
offset 0.32, slope limit 45 degrees) are the real ones off ScavengePlayerController, and climbs are
modelled as capped while drops are free — which is what makes the flood-fill prove pickups are
ESCAPABLE rather than merely reachable. A pit you can enter and not leave is a run-ending trap that
reads as fine on every other check.
"""

import math
import os
import re
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from scavenge_scene_lib import (                                    # noqa: E402
    MATERIALS, SCRIPT_GUIDS, DUST_MATERIAL_NAME, RANGE_RING_MATERIAL_NAME, guid_for,
)


# ─── Geometry helpers ───────────────────────────────────────────────────────────────────
def _rot_matrix(x, y, z):
    """Unity's ZXY intrinsic euler order as a 3x3 row-major matrix."""
    import math
    cx, sx = math.cos(math.radians(x)), math.sin(math.radians(x))
    cy, sy = math.cos(math.radians(y)), math.sin(math.radians(y))
    cz, sz = math.cos(math.radians(z)), math.sin(math.radians(z))
    rx = ((1, 0, 0), (0, cx, -sx), (0, sx, cx))
    ry = ((cy, 0, sy), (0, 1, 0), (-sy, 0, cy))
    rz = ((cz, -sz, 0), (sz, cz, 0), (0, 0, 1))
    def mul(a, b):
        return tuple(tuple(sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3))
                     for i in range(3))
    return mul(ry, mul(rx, rz))


def _apply(m, v):
    return tuple(sum(m[i][k] * v[k] for k in range(3)) for i in range(3))

def rotated_half_extents(half, rot):
    """
    World AABB half-extents of an oriented box: sum_j |R[i][j]| * half[j].

    This has to be fed to the placement gates rather than the un-rotated half, or tilting a pickup
    would shrink the box the burial and support checks reason about at exactly the moment the object
    actually started occupying more space. A gate that gets easier when the geometry gets harder is
    worse than no gate.
    """
    m = _rot_matrix(*rot)
    return tuple(sum(abs(m[i][j]) * half[j] for j in range(3)) for i in range(3))


# ─── Navigation grid ────────────────────────────────────────────────────────────────────
def _column_span(solid, x, z):
    """Where a vertical line at (x, z) enters and leaves this solid's oriented box, as a
    (low_y, high_y) pair, or None if it misses. Slab test in the box's own frame, so a
    rotated ramp or a stair tread reports its real surface height at that column rather
    than the flat top of the box that bounds it."""
    _, c, R, half, _, _ = solid
    d0 = (x - c[0], -c[1], z - c[2])
    a = tuple(sum(R[k][i] * d0[k] for k in range(3)) for i in range(3))
    b = tuple(R[1][i] for i in range(3))

    lo, hi = -1e9, 1e9
    for i in range(3):
        if abs(b[i]) < 1e-9:
            if abs(a[i]) > half[i]:
                return None
            continue
        t1 = (-half[i] - a[i]) / b[i]
        t2 = (half[i] - a[i]) / b[i]
        if t1 > t2:
            t1, t2 = t2, t1
        lo, hi = max(lo, t1), min(hi, t2)
        if lo > hi:
            return None
    return lo, hi


NAV_GRID_DIR = "Assets/Data/Resources/Nav"

# Resources key the runtime loads by. No extension: Resources.Load<TextAsset> takes the path
# without one, and the file must be .bytes on disk for Unity to import it as a TextAsset at all.
NAV_GRID_RESOURCE = "navgrid_scavenge"
NAV_GRID_MAGIC = b"OZNAV1\0\0"

# Sentinel written for a cell the Census-Taker may not stand on: solid, unsupported, or walkable
# but cut off from the spawn island. Chosen as int16 min so it can never collide with a real
# height: the level's vertical extent is a few metres, encoded in centimetres.
NAV_IMPASSABLE = -32768


class NavGrid(object):
    """
    The walkability height-field, packaged for export.

    Deliberately a dumb record rather than a second implementation of the flood-fill: it holds
    exactly what verify_reachability already computed, so the grid the Census-Taker walks on and
    the grid that decides whether the level ships can never be two different answers.
    """

    def __init__(self, x0, z0, step, nx, nz, height, walk, reachable):
        self.x0, self.z0, self.step = x0, z0, step
        self.nx, self.nz = nx, nz
        self.height, self.walk, self.reachable = height, walk, reachable

    def passable_count(self):
        return sum(1 for i in range(self.nx) for j in range(self.nz)
                   if self.walk[i][j] and self.reachable[i][j])

    def to_bytes(self):
        """
        Little-endian binary, in the order a reader consumes it:

            magic      8 bytes  "OZNAV1\\0\\0"
            originX    float32  world X of cell (0, *)
            originZ    float32  world Z of cell (*, 0)
            step       float32  metres per cell
            nx, nz     int32    grid dimensions
            cells      int16 x (nx*nz), row-major over x then z, height in centimetres,
                                NAV_IMPASSABLE for cells the agent may not occupy

        Heights are centimetres rather than floats: it halves the file, it is exact for a level
        authored on a 0.5 m grid, and an integer encoding cannot introduce a platform-dependent
        float formatting difference into a byte-deterministic pipeline.
        """
        out = bytearray(NAV_GRID_MAGIC)
        out += struct.pack("<fffii", self.x0, self.z0, self.step, self.nx, self.nz)

        for i in range(self.nx):
            for j in range(self.nz):
                if not (self.walk[i][j] and self.reachable[i][j]):
                    out += struct.pack("<h", NAV_IMPASSABLE)
                    continue
                # Clamped so a freak height cannot alias onto the sentinel.
                cm = int(round(self.height[i][j] * 100.0))
                out += struct.pack("<h", max(NAV_IMPASSABLE + 1, min(32767, cm)))

        return bytes(out)

def verify_reachability(solids, placed, spawn, bunker_xz, step=0.5):
    """Flood-fill a walkability height-field from the player spawn and confirm the bunker
    trigger and every pickup are actually reachable. Without a live Editor this is the only
    way to know the level is playable rather than merely well-formed: a doorway sealed by a
    stray prop, or a pit with no way out, looks perfectly valid in YAML.

    Model: CharacterController height 1.8, radius 0.35, step offset 0.32. Climbing is capped
    at the step offset; drops are always allowed, since gravity handles them.
    """
    x0, x1, z0, z1 = -52.0, 52.0, -36.0, 36.0
    nx = int((x1 - x0) / step) + 1
    nz = int((z1 - z0) / step) + 1
    step_up, headroom, reach_ceiling = 0.32, 1.70, 3.2

    # Spatial buckets so each column only tests nearby geometry.
    bucket, bsize = {}, 4.0
    for s in solids:
        _, c, _, _, ext, _ = s
        for bx in range(int((c[0] - ext[0]) // bsize), int((c[0] + ext[0]) // bsize) + 1):
            for bz in range(int((c[2] - ext[2]) // bsize), int((c[2] + ext[2]) // bsize) + 1):
                bucket.setdefault((bx, bz), []).append(s)

    def surface(x, z):
        """Walkable height at this column, or None if there is no standable, clear surface."""
        spans = []
        for s in bucket.get((int(x // bsize), int(z // bsize)), ()):
            sp = _column_span(s, x, z)
            if sp is not None:
                spans.append(sp)
        if not spans:
            return None
        tops = [hi for _, hi in spans if hi <= reach_ceiling]
        if not tops:
            return None
        h = max(tops)
        # Headroom: nothing may occupy the body volume standing on that surface.
        for lo, hi in spans:
            if hi > h + 0.02 and lo < h + headroom:
                return None
        return h

    height = [[None] * nz for _ in range(nx)]
    for i in range(nx):
        x = x0 + i * step
        for j in range(nz):
            height[i][j] = surface(x, z0 + j * step)

    # Erode by one cell so the flood-fill respects the 0.35 m capsule radius instead of
    # squeezing the player's centre-line flush against a wall.
    solid_cell = [[height[i][j] is None for j in range(nz)] for i in range(nx)]
    walk = [[not solid_cell[i][j] for j in range(nz)] for i in range(nx)]
    for i in range(nx):
        for j in range(nz):
            if not walk[i][j]:
                continue
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ni, nj = i + di, j + dj
                if not (0 <= ni < nx and 0 <= nj < nz) or solid_cell[ni][nj]:
                    walk[i][j] = False
                    break

    def cell_of(x, z):
        return int(round((x - x0) / step)), int(round((z - z0) / step))

    si, sj = cell_of(spawn[0], spawn[2])
    if not walk[si][sj]:
        # Spawn may land on an eroded edge cell; accept the nearest open cell within 1.5 m.
        found = None
        r = int(1.5 / step)
        for di in range(-r, r + 1):
            for dj in range(-r, r + 1):
                ni, nj = si + di, sj + dj
                if 0 <= ni < nx and 0 <= nj < nz and walk[ni][nj]:
                    found = (ni, nj)
                    break
            if found:
                break
        if not found:
            return ["player spawn %s is not on walkable ground" % (spawn,)], 0
        si, sj = found

    def fill(seed_i, seed_j):
        seen = [[False] * nz for _ in range(nx)]
        seen[seed_i][seed_j] = True
        stack = [(seed_i, seed_j)]
        while stack:
            i, j = stack.pop()
            h = height[i][j]
            for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                ni, nj = i + di, j + dj
                if not (0 <= ni < nx and 0 <= nj < nz) or seen[ni][nj] or not walk[ni][nj]:
                    continue
                if height[ni][nj] - h > step_up:   # climbing is capped; falling never is
                    continue
                seen[ni][nj] = True
                stack.append((ni, nj))
        return seen

    def nearest_open(x, z, radius=2.4):
        """Closest walkable cell to a column — pickups sit on shelves and crates, whose own
        columns are not standable, so 'reachable' means 'reachable from right beside it'."""
        ci, cj = cell_of(x, z)
        r = int(radius / step)
        best = None
        for di in range(-r, r + 1):
            for dj in range(-r, r + 1):
                ni, nj = ci + di, cj + dj
                if 0 <= ni < nx and 0 <= nj < nz and walk[ni][nj]:
                    d = (di * di + dj * dj) ** 0.5
                    if best is None or d < best[0]:
                        best = (d, ni, nj)
        return best

    def touches(seen, x, z, radius=2.4):
        ci, cj = cell_of(x, z)
        r = int(radius / step)
        for di in range(-r, r + 1):
            for dj in range(-r, r + 1):
                ni, nj = ci + di, cj + dj
                if 0 <= ni < nx and 0 <= nj < nz and seen[ni][nj]:
                    return True
        return False

    from_spawn = fill(si, sj)
    problems = []

    if not touches(from_spawn, bunker_xz[0], bunker_xz[1]):
        problems.append("BUNKER ENTRANCE at %s is unreachable from spawn" % (bunker_xz,))

    for name, pos, _, _ in placed:
        if not touches(from_spawn, pos[0], pos[2]):
            problems.append("%s is unreachable from spawn" % name)

    # And the return leg. Traversal is asymmetric — a drop costs nothing but the climb back
    # is capped at the step offset — so "reachable" does not imply "escapable". Anywhere the
    # player can be lured for loot must still have a route to the door, or the grain pit is
    # a run-ending trap that no amount of YAML validation would reveal.
    for name, pos, _, _ in placed:
        spot = nearest_open(pos[0], pos[2])
        if spot is None:
            continue                          # already reported as unreachable above
        if not touches(fill(spot[1], spot[2]), bunker_xz[0], bunker_xz[1]):
            problems.append("cannot reach the bunker after taking %s — dead end" % name)

    reachable = sum(1 for i in range(nx) for j in range(nz) if from_spawn[i][j])

    # The same field, handed on as the runtime navigation grid. This is the whole reason the
    # Drowned Census-Taker needs no baked NavMesh: the flood-fill above already models the real
    # CharacterController (h 1.8 / r 0.35 / step 0.32) against the real level geometry, and it has
    # a negative control proving it detects a sealed route. A NavMesh would be a second, differently
    # derived answer to the same question, obtainable only from an interactive Editor bake -- a
    # manual step in an otherwise headless, byte-deterministic pipeline, and one that could silently
    # disagree with the gate that already decides whether the level is playable.
    #
    # Only cells reachable from spawn are exported. An island of walkable floor behind a sealed wall
    # is walkable and useless: a stalker spawned or pathing into it could never reach the player, and
    # would stand there looking broken.
    grid = NavGrid(x0, z0, step, nx, nz, height, walk, from_spawn)
    return problems, reachable * step * step, grid


def verify_placement(solids, placed):
    """Catch the two geometry bugs YAML validation cannot see: a pickup buried inside solid
    geometry (unreachable, or reachable only by clipping), and a pickup floating in mid-air
    with nothing under it. Both read as broken to a player and neither shows up in a diff."""
    problems = []

    for name, pos, half, is_crew in placed:
        # Buried? Transform the pickup centre into each solid's own space and test the
        # oriented box directly, so a diagonal beam or a toppled silo is judged by the
        # volume it actually occupies rather than by the box that bounds it.
        for sname, spos, srot, shalf, _, _ in solids:
            d = tuple(pos[i] - spos[i] for i in range(3))
            local = tuple(sum(srot[k][i] * d[k] for k in range(3)) for i in range(3))
            if all(abs(local[i]) < shalf[i] - half[i] * 0.35 for i in range(3)):
                problems.append("%s is buried inside %s" % (name, sname))
                break

        # Supported? Something solid must top out just under the pickup's base. The world
        # AABB is the right shape here — the top face is what a dropped object rests on.
        base = pos[1] - half[1]
        supported = False
        for sname, spos, _, _, sext, _ in solids:
            top = spos[1] + sext[1]
            near = (abs(pos[0] - spos[0]) < sext[0] + half[0]
                    and abs(pos[2] - spos[2]) < sext[2] + half[2])
            if near and (base - 0.30) <= top <= (base + 0.12):
                supported = True
                break
        if not supported:
            problems.append("%s has nothing supporting it (base y=%.2f)" % (name, base))

    return problems

def verify(scene_text, extra_known_guids=None):
    """Every {fileID: N} must resolve inside the file, and every guid must exist on disk."""
    import re

    defined = set(int(m) for m in re.findall(r"^--- !u!\d+ &(\d+)$", scene_text, re.M))
    problems = []

    for m in re.finditer(r"\{fileID: (-?\d+)(, guid: ([0-9a-f]{32}), type: (\d+))?\}", scene_text):
        fid, guid = int(m.group(1)), m.group(3)
        if guid is None:
            if fid != 0 and fid not in defined and fid != 9223372036854775807:
                problems.append("dangling local fileID %d" % fid)

    guids = set(re.findall(r"guid: ([0-9a-f]{32})", scene_text))
    known = set(SCRIPT_GUIDS.values()) | {"0000000000000000e000000000000000",
                                          "0000000000000000f000000000000000"}
    known |= {guid_for("Material::" + n) for n in MATERIALS}
    known |= {guid_for("Material::" + n)
              for n in (DUST_MATERIAL_NAME, RANGE_RING_MATERIAL_NAME)}
    # Each site ships its own volume profile, so the caller names the guids that are legitimate
    # for it. Defaulting this to a hardcoded "Scavenge" profile would let a Census Office scene
    # reference a Grain Depot asset and still pass — the check would be inspecting the wrong level.
    known |= set(extra_known_guids or ())
    for g in guids - known:
        problems.append("unrecognised guid %s" % g)

    # Component lists must reference real components.
    for m in re.finditer(r"- component: \{fileID: (\d+)\}", scene_text):
        if int(m.group(1)) not in defined:
            problems.append("GameObject references missing component %s" % m.group(1))

    return problems

def verify_nav_grid(grid, blob, spawn, bunker_xz, mutant_spawn_points=()):
    """
    Independent post-check on the exported navigation grid.

    Deliberately re-parses the *bytes*, not the Python object, and runs its own A* over them. The
    grid the C# will actually read is the byte blob; checking the in-memory object would prove only
    that the object is fine and leave every encoding bug — wrong endianness, wrong stride, an off-by-
    one in the row-major order — to be discovered as a Census-Taker walking through a wall.

    Three properties, each of which has been a real failure mode in this pipeline:
      1. The header round-trips and the payload is exactly nx*nz int16s.
      2. Spawn and the bunker door both land on passable cells. A grid whose origin or stride is
         wrong still parses; it just describes a different level.
      3. The bunker is reachable from spawn by A* over the decoded cells. This is the property the
         stalker depends on, and it is not implied by (2).
    """
    problems = []

    if not blob.startswith(NAV_GRID_MAGIC):
        return ["magic bytes missing"]

    x0, z0, step, nx, nz = struct.unpack_from("<fffii", blob, len(NAV_GRID_MAGIC))
    head = len(NAV_GRID_MAGIC) + struct.calcsize("<fffii")
    expected = head + nx * nz * 2

    if len(blob) != expected:
        problems.append("payload is %d bytes, expected %d for %dx%d cells"
                        % (len(blob), expected, nx, nz))
        return problems
    if (nx, nz) != (grid.nx, grid.nz):
        problems.append("header says %dx%d, grid is %dx%d" % (nx, nz, grid.nx, grid.nz))
    if abs(step - grid.step) > 1e-6:
        problems.append("header step %s != grid step %s" % (step, grid.step))

    cells = struct.unpack_from("<%dh" % (nx * nz), blob, head)

    def passable(i, j):
        return 0 <= i < nx and 0 <= j < nz and cells[i * nz + j] != NAV_IMPASSABLE

    def cell_of(x, z):
        return int(round((x - x0) / step)), int(round((z - z0) / step))

    def nearest_passable(x, z, radius_m=2.5):
        ci, cj = cell_of(x, z)
        r = int(radius_m / step)
        best = None
        for di in range(-r, r + 1):
            for dj in range(-r, r + 1):
                if not passable(ci + di, cj + dj):
                    continue
                d = di * di + dj * dj
                if best is None or d < best[0]:
                    best = (d, ci + di, cj + dj)
        return None if best is None else (best[1], best[2])

    start = nearest_passable(spawn[0], spawn[2])
    goal = nearest_passable(bunker_xz[0], bunker_xz[1])

    if start is None:
        problems.append("no passable cell within 2.5 m of the player spawn %s" % (spawn,))
    if goal is None:
        problems.append("no passable cell within 2.5 m of the bunker door %s" % (bunker_xz,))
    if problems:
        return problems

    # Breadth-first is sufficient and cheaper than A* for a pure connectivity proof; the C# side
    # uses A* because it wants a short path, not merely any path.
    seen = bytearray(nx * nz)
    seen[start[0] * nz + start[1]] = 1
    queue = [start]
    head_i = 0
    while head_i < len(queue):
        i, j = queue[head_i]
        head_i += 1
        if (i, j) == goal:
            break
        for di, dj in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ni, nj = i + di, j + dj
            if not passable(ni, nj) or seen[ni * nz + nj]:
                continue
            seen[ni * nz + nj] = 1
            queue.append((ni, nj))

    if not seen[goal[0] * nz + goal[1]]:
        problems.append("bunker door is not reachable from spawn across the exported grid")

    # 4. Every mutant spawn point must be on a passable, spawn-connected cell. MutantSpawner falls
    #    back to the navigation grid when a point is rejected, so a bad point is not fatal — which is
    #    exactly why it needs a gate. It would go silently unused, and the level would quietly place
    #    its stalkers somewhere nobody chose.
    for idx, point in enumerate(mutant_spawn_points):
        pi, pj = cell_of(point[0], point[2])
        if passable(pi, pj):
            continue
        if nearest_passable(point[0], point[2], radius_m=1.5) is not None:
            problems.append("mutant spawn point %d %s is not on a passable cell "
                            "(walkable ground is nearby, so it is probably a small offset)"
                            % (idx + 1, point))
        else:
            problems.append("mutant spawn point %d %s is inside geometry or cut off from spawn"
                            % (idx + 1, point))

    return problems


def write_bytes(path, blob):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as fh:
        fh.write(blob)


def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


# ─── Content and mirror gates ───────────────────────────────────────────────────────────
# Site-agnostic by construction: each takes its level's manifest as an argument. They were local to
# the depot generator until there were three sites; a per-site copy of "check the ids against the
# database" is a per-site opportunity to forget to.

BALANCE_CONSTANTS_PATH = "Assets/_Project/Scripts/Core/BalanceConstants.cs"

def load_item_categories():
    """
    id -> ItemCategory name for every item in the live database. Read from the shipped data
    rather than restated here, so a re-categorised item changes its silhouette on the next
    generation instead of quietly keeping the old one.

    The .asset items serialize category as the enum's integer, so the ordering below must
    match ItemCategory in ItemData.cs.
    """
    import glob
    import json
    import re

    enum_order = ["Food", "Water", "Medical", "Weapon", "Ammunition",
                  "Tool", "Document", "Artifact", "Crafting", "Special"]
    categories = {}

    for path in glob.glob("Assets/Data/Resources/Items/*.json"):
        with open(path, encoding="utf-8") as fh:
            data = json.load(fh)
        if data.get("id"):
            categories[data["id"]] = data.get("category")

    for path in glob.glob("Assets/Data/Definitions/Items/*.asset"):
        with open(path, encoding="utf-8") as fh:
            text = fh.read()
        m_id = re.search(r"^\s+id:\s*(\S+)", text, re.M)
        m_cat = re.search(r"^\s+category:\s*(\d+)", text, re.M)
        if not m_id or m_id.group(1) in categories:
            continue
        idx = int(m_cat.group(1)) if m_cat else -1
        categories[m_id.group(1)] = enum_order[idx] if 0 <= idx < len(enum_order) else None

    return categories


def verify_pickup_ids(pickups, crew_kind):
    """Fail loudly if a manifest id is not in the shipped item/crew database."""
    import glob
    import json

    item_ids = set()
    for path in glob.glob("Assets/Data/Resources/Items/*.json"):
        with open(path, encoding="utf-8") as fh:
            item_ids.add(json.load(fh)["id"])
    for path in glob.glob("Assets/Data/Definitions/Items/*.asset"):
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                if line.startswith("  id: "):
                    item_ids.add(line[6:].strip())
                    break

    crew_ids = set()
    for path in glob.glob("Assets/Data/Definitions/Crew/*.asset"):
        with open(path, encoding="utf-8") as fh:
            for line in fh:
                if line.startswith("  id: "):
                    crew_ids.add(line[6:].strip())
                    break

    missing = []
    for data_id, kind, _, _, _, _, _, _ in pickups:
        pool = crew_ids if kind == crew_kind else item_ids
        if data_id not in pool:
            missing.append("%s (%s)" % (data_id, "crew" if kind == crew_kind else "item"))
    return missing, len(item_ids), len(crew_ids)


def assert_balance_mirror(mirror):
    """
    Proves every mirrored constant still equals its BalanceConstants counterpart.

    The generator serializes some balance numbers directly into the scene — the controller's
    interaction range, the Geiger's detection radius, the Backlog's dilation factor. Those are the
    values the scene will use forever after; the C# constants are what every other system reads.
    When the two disagree nothing errors, because both are individually valid: the range ring draws
    one radius and the raycast honours another, and the bug presents as "the reach feels wrong"
    months later.

    CLAUDE.md §14's rule, applied to numbers instead of GUIDs: a mirror without a gate is a
    comment, and a comment does not survive a retune.
    """
    pattern = re.compile(
        r"public\s+const\s+(?:float|int)\s+([A-Z0-9_]+)\s*=\s*(-?[0-9.]+)f?\s*;")

    with open(BALANCE_CONSTANTS_PATH, encoding="utf-8") as handle:
        declared = {m.group(1): float(m.group(2)) for m in pattern.finditer(handle.read())}

    problems = []
    for name, mirrored in mirror.items():
        if name not in declared:
            problems.append("%s: not declared in BalanceConstants.cs" % name)
        elif abs(declared[name] - float(mirrored)) > 1e-6:
            problems.append("%s: generator says %s, BalanceConstants says %s"
                            % (name, mirrored, declared[name]))

    if problems:
        raise SystemExit("balance mirror check FAILED:\n  " + "\n  ".join(problems))
    return "balance mirror: %d constants match BalanceConstants.cs" % len(mirror)


def verify_anomaly_zones(zones):
    """
    Proves no two anomaly volumes overlap.

    AnomalyZone.ZoneAt<T> returns the first match in registration order, so two overlapping volumes
    resolve arbitrarily — and worse, BacklogAnomaly restores the player's speed multiplier to an
    absolute 1 on exit rather than unwinding a stack, which is only correct while zones are
    disjoint. Nested zones would let a player walk out of the inner one and leave the outer one's
    slow permanently cleared while still standing inside it.

    Overlap is tested on the AABBs, which is exact here: the zones are axis-aligned boxes with no
    rotation, so there is no tilted-solid false positive of the kind that forced the OBB test in
    verify_placement().
    """
    problems = []
    boxes = []
    for name, _script, _cls, pos, size, _fields in zones:
        half = (size[0] / 2.0, size[1] / 2.0, size[2] / 2.0)
        boxes.append((name,
                      (pos[0] - half[0], pos[1] - half[1], pos[2] - half[2]),
                      (pos[0] + half[0], pos[1] + half[1], pos[2] + half[2])))

    for i in range(len(boxes)):
        for j in range(i + 1, len(boxes)):
            name_a, min_a, max_a = boxes[i]
            name_b, min_b, max_b = boxes[j]
            if all(min_a[k] < max_b[k] and min_b[k] < max_a[k] for k in range(3)):
                problems.append("%s overlaps %s" % (name_a, name_b))

    return problems
