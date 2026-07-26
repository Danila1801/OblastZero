#!/usr/bin/env python3
"""
Generates the 3D Blowout level (Phase A) — "Collapsed Grain Depot", Outer Cordon.

DESIGN_BIBLE §2.2 describes the Grain Belt as collapsing agricultural processing plants —
flour mills, oil presses, fertilizer warehouses — strung along a single defunct rail line.
This is the first scavenge site: a grain depot whose intake silo came down across the
conveyor gantry, with a civil-defence stairwell in the north-east corner of the yard.

Why a generator rather than a hand-saved scene: the level is primitive geometry driven by a
coordinate plan, so the plan itself becomes the reviewable artifact and a layout change is a
readable diff instead of churn across thirty thousand lines of YAML. Unity re-serializes the
file the first time it saves it; that is expected — this script is the authoring source, not
a round-trip format.

Writes, all with deterministic GUIDs so nothing ever dangles:
  Assets/Scenes/Scavenge.unity                  (+ .meta)
  Assets/Art/Materials/Scavenge/*.mat           (+ .meta)  — URP Lit
  Assets/Settings/ScavengeVolumeProfile.asset   (+ .meta)

Run from the project root:  python tools/generate_scavenge_scene.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from scavenge_scene_lib import (                                    # noqa: E402
    MATERIALS, MATERIAL_DIR, VOLUME_PROFILE_PATH, SCRIPT_GUIDS,
    SceneBuilder, guid_for, material_yaml, volume_profile_yaml, f,
)

SCENE_PATH = "Assets/Scenes/Scavenge.unity"

# ─── Level plan ─────────────────────────────────────────────────────────────────────────
#
# Footprint X -52..+52, Z -36..+36 (104 x 72 m), sealed by a 9 m perimeter wall.
# Six zones in three bands. Player spawns SW on the rail siding; bunker headhouse is NE.
#
#                                 NORTH  Z=+36
#   +------------------+------------------------+------------------------+
#   |  SILO BASE       |  WAREHOUSE FLOOR       |  BUNKER STAIRWELL      |
#   |  3 silos, a      |  4 shelving rows,      |  headhouse, blast      |
#   |  fourth down     |  flour mill, oil       |  door, hazard stripes, |
#   |  across the      |  press, failing        |  14-riser stair shaft  |
#   |  gantry; sunken  |  fluorescents          |  down into the dark    |
#   |  grain intake    |                        |                        |
#   |  X -52..-18      |  X -18..+26            |  X +26..+52            |
#   +--- wall Z=+5, doorways at X -40..-35 / -4..+2 / +38..+43 ----------+
#   |                       OPEN YARD  (Z -15..+5)                       |
#   +--- wall Z=-15, doorways at X -46..-41 / -6..0 / +30..+35 ----------+
#   |  RAIL SIDING     |  ADMIN / OFFICE        |  LOADING DOCK          |
#   |  (SPAWN)         |  desks, filing, map    |  raised platform,      |
#   |                  |  board, respirators    |  ramp, crate stacks,   |
#   |                  |  still on their hooks  |  ammunition boxes      |
#   |  X -52..-24      |  X -24..+16            |  X +16..+52            |
#   +------------------+------------------------+------------------------+
#        |------- rail spine, Z -36..-30, runs clear through all three -------|
#                                 SOUTH  Z=-36
#
# Routes from spawn to the bunker door (sprint 7 m/s, walk 4.5):
#   direct   yard diagonal                 ~126 m   ~18 s   almost no loot
#   north    silo base -> warehouse         ~150 m  ~21 s   densest loot, tight aisles
#   south    rail spine -> dock -> yard     ~155 m  ~22 s   crate loot, ramp climb
#   detour   grain intake pit               +40 m round trip, both artifacts, contaminated
#
# Every doorway gap is at least 3.6 m, so the 0.7 m CharacterController never snags, and
# the 60 s clock leaves roughly 40 s of detour budget past the direct route.

BOX_LOCAL = {"Cube": (1, 1, 1), "Cylinder": (1, 2, 1), "Sphere": (1, 1, 1),
             "Capsule": (1, 2, 1), "Quad": (1, 1, 0), "Plane": (10, 0, 10)}

# Item silhouettes. This module is checked against VisualArchetype.cs before the scene is
# written; the C# file is the authority and generation aborts if the two have drifted.
import visual_archetypes as va  # noqa: E402  (kept next to the tables it belongs with)

# Y half-extent of the uniform 0.34 cube every pickup used to be. Archetype shapes are
# dropped by the difference so their BOTTOM stays on the surface the manifest was authored
# against, rather than their centre.
LEGACY_PICKUP_HALF_Y = 0.17

# World-space edge length of a pickup's trigger box, held constant across archetypes so a
# flat document is no harder to hit than a crate. Matches the old 0.34 x 1.9 local box.
PICKUP_TRIGGER_WORLD_M = 0.646

# id -> ItemCategory name, loaded from the live database in main(). Empty until then.
ITEM_CATEGORIES = {}

PLAYER_SPAWN = (-44, 0.15, -30)
PLAYER_FACING = (0, 38, 0)
EYE_HEIGHT = 1.62

# Mirrors BalanceConstants.SCAVENGE_* so the scene never contradicts the balance file.
SCAVENGE_TIMER_SECONDS = 60
SCAVENGE_TIMER_WARNING_THRESHOLD = 15
SCAVENGE_TIMER_CRITICAL_THRESHOLD = 5

# ─── Pickup manifest ────────────────────────────────────────────────────────────────────
# (dataId, kind, quantity, durabilityOverride, contamination, position, yaw, material)
# kind 0 = Item, 1 = Crew. Every id is verified against the live database at generate time.
# Contamination values are copied from each item's own JSON so a Geiger reading in the
# bunker matches what the world object was carrying.

ITEM = 0
CREW = 1

PICKUPS = [
    # -- Rail siding / spawn: something in hand inside the first two seconds --------------
    ("item_emergency_ration",      ITEM, 3, -1, 0.0,  (-40, 1.02, -32),   14,  "M_Pickup_Food"),
    ("item_pry_bar",               ITEM, 1, -1, 0.0,  (-31, 0.22, -20),   62,  "M_Pickup_Tool"),

    # -- Admin / office: documents and medical, the densest small-item room ---------------
    ("item_industrial_radio",      ITEM, 1, 60, 0.0,  (-18, 0.98, -20),  -18,  "M_Pickup_Tool"),
    ("item_bureau_dossier",        ITEM, 1, -1, 0.0,  (-8, 0.95, -30),    34,  "M_Pickup_Document"),
    # 1.97, not 1.92: Filing_Cabinet_2's top is at y=1.80, so the old value sank the pickup
    # 5 cm into the cabinet. A 0.34 cube hid that; a 5 cm-thick folder is swallowed whole.
    ("item_redacted_manifest",     ITEM, 2, -1, 0.0,  (-20.6, 1.97, -34), -8,  "M_Pickup_Document"),
    ("item_issued_bandage",        ITEM, 3, -1, 0.0,  (6, 0.96, -22),     26,  "M_Pickup_Medical"),
    ("item_anti_rad_syringe",      ITEM, 2, -1, 0.0,  (13, 1.97, -18),    44,  "M_Pickup_Medical"),
    ("item_service_pistol",        ITEM, 1, 55, 0.0,  (-8.3, 0.95, -29.2), 78, "M_Pickup_Weapon"),

    # -- Loading dock: the heavy, bulky haul ---------------------------------------------
    ("item_12_gauge_carbine",      ITEM, 1, 45, 0.0,  (22, 4.1, -19),    -24,  "M_Pickup_Weapon"),
    ("item_handloaded_12_gauge",   ITEM, 6, -1, 0.0,  (33, 1.72, -21),    12,  "M_Pickup_Ammo"),
    ("item_pistol_ammo",           ITEM, 3, -1, 0.0,  (35.5, 1.72, -18), -25,  "M_Pickup_Ammo"),
    ("item_canned_meat",           ITEM, 2, -1, 0.0,  (40, 4.1, -19),     35,  "M_Pickup_Food"),
    ("item_boiled_water_flask",    ITEM, 2, -1, 0.0,  (43, 2.72, -21),   -22,  "M_Pickup_Water"),
    ("crew_sasha",                 CREW, 1, -1, 0.0,  (34, 0.72, -28.5),  22,  "M_Crew_Coat"),

    # -- Silo base and the grain intake pit: both artifacts, both contaminated ------------
    ("item_field_torch",           ITEM, 1, 70, 0.0,  (-49, 1.12, 12),    40,  "M_Pickup_Tool"),
    ("item_preserved_perch",       ITEM, 2, -1, 0.0,  (-49, 2.12, 17),    -6,  "M_Pickup_Food"),
    ("item_artifact_ember",        ITEM, 1, 90, 38.6, (-37.5, -2.28, 22.5), 18, "M_Pickup_Artifact"),
    ("item_artifact_ballast",      ITEM, 1, -1, 35.0, (-45, -2.28, 24),  -30,  "M_Pickup_Artifact"),
    ("crew_marina",                CREW, 1, -1, 0.0,  (-22, 0.72, 19.5),  64,  "M_Crew_Coat"),

    # -- Warehouse floor: shelving rows at X -12 / -3 / +7 / +17 -------------------------
    ("item_kafedra_geiger_counter", ITEM, 1, 70, 38.8, (-12, 2.12, 21),   28,  "M_Pickup_Tool"),
    ("item_field_suture_kit",      ITEM, 2, -1, 0.0,  (-3, 1.12, 15),    -14,  "M_Pickup_Medical"),
    ("item_axe",                   ITEM, 1, 80, 0.0,  (7, 1.14, 25),      52,  "M_Pickup_Weapon"),
    ("item_water_flask",           ITEM, 2, -1, 0.0,  (17, 2.12, 13),    -36,  "M_Pickup_Water"),
    ("item_bandage",               ITEM, 2, -1, 0.0,  (17, 1.12, 29),     16,  "M_Pickup_Medical"),
    ("crew_yuri",                  CREW, 1, -1, 0.0,  (19, 0.72, 8),     -48,  "M_Crew_Coat"),
]

# Overhead fluorescent fixtures: (name, x, y, z, range)
FIXTURES = [
    ("Fixture_Warehouse_1", -12, 6.5, 14, 15), ("Fixture_Warehouse_2", -12, 6.5, 29, 15),
    ("Fixture_Warehouse_3",   2, 6.5, 14, 15), ("Fixture_Warehouse_4",   2, 6.5, 29, 15),
    ("Fixture_Warehouse_5",  17, 6.5, 14, 15), ("Fixture_Warehouse_6",  17, 6.5, 29, 15),
    ("Fixture_Admin_1",     -18, 6.5, -19, 13), ("Fixture_Admin_2",    -18, 6.5, -27, 13),
    ("Fixture_Admin_3",       6, 6.5, -19, 13), ("Fixture_Admin_4",      6, 6.5, -27, 13),
    ("Fixture_Dock_1",       26, 6.5, -20, 14), ("Fixture_Dock_2",      42, 6.5, -20, 14),
    ("Fixture_Silo_1",      -46, 4.5, 12, 12), ("Fixture_Silo_2",     -30, 4.5, 20, 12),
    ("Fixture_Headhouse",    44, 4.6, 29, 10),
]


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


def build():
    sb = SceneBuilder()

    # World transform per transform-id, so placement can be validated in world space.
    # Keyed the same way Unity composes: parent rotation and scale carry to children.
    world = {None: ((0, 0, 0), _rot_matrix(0, 0, 0), (1, 1, 1))}
    solids = []    # (name, world_centre, world_half_extents, mesh) for colliding geometry
    placed = []    # (name, world_centre, world_half_extents, is_crew) for pickups

    def _compose(parent, pos, rot, scale):
        ppos, prot, pscale = world[parent]
        local = tuple(pos[i] * pscale[i] for i in range(3))
        wpos = tuple(ppos[i] + _apply(prot, local)[i] for i in range(3))
        wrot = _rot_matrix(*rot)
        wrot = tuple(tuple(sum(prot[i][k] * wrot[k][j] for k in range(3)) for j in range(3))
                     for i in range(3))
        wscale = tuple(pscale[i] * scale[i] for i in range(3))
        return wpos, wrot, wscale

    def group(name, parent=None, pos=(0, 0, 0), rot=(0, 0, 0)):
        _, t = sb.obj(name, parent=parent, pos=pos, rot=rot)
        world[t] = _compose(parent, pos, rot, (1, 1, 1))
        return t

    def solid(name, parent, pos, scale, mat, mesh="Cube", rot=(0, 0, 0),
              collide=True, static=True, shadows=True, trigger=False, collider_scale=1.0):
        go, t = sb.obj(name, parent=parent, pos=pos, rot=rot, scale=scale, static=static)
        world[t] = wpos, wrot, wscale = _compose(parent, pos, rot, scale)
        sb.mesh_renderer(go, mesh, mat, cast_shadows=shadows)
        if collide:
            base = BOX_LOCAL[mesh]
            sb.box_collider(go, is_trigger=trigger,
                            size=tuple(c * collider_scale for c in base))
            if not trigger:
                # Keep both: the oriented box for "is this buried" (exact — a rotated
                # cylinder's AABB over-reports enormously and would false-positive), and
                # its world AABB for "is this supported", where the top face is what counts.
                half = tuple(abs(base[i] * wscale[i]) * 0.5 for i in range(3))
                ext = tuple(sum(abs(wrot[r][c]) * half[c] for c in range(3)) for r in range(3))
                solids.append((name, wpos, wrot, half, ext, mesh))
        return go, t

    def shelf_unit(name, parent, x, z0, z1):
        """Rusted industrial racking: three decks on paired uprights."""
        length = z1 - z0
        u = group(name, parent, (x, 0, (z0 + z1) * 0.5))
        for lvl, y in enumerate((0.9, 1.9, 2.9)):
            solid("%s_Deck_%d" % (name, lvl + 1), u, (0, y, 0),
                  (1.8, 0.12, length), "M_Steel_Rusted", shadows=False)
        posts = max(2, int(length // 4) + 1)
        for i in range(posts):
            z = -length * 0.5 + i * (length / (posts - 1))
            for side, tag in ((-0.8, "L"), (0.8, "R")):
                solid("%s_Post_%d%s" % (name, i + 1, tag), u, (side, 1.55, z),
                      (0.14, 3.1, 0.14), "M_Steel_Rusted", shadows=False)
        return u

    # ══ ENVIRONMENT ═════════════════════════════════════════════════════════════════════
    env = group("=== ENVIRONMENT ===")

    # Ground, split into slabs so the grain intake pit and the stair shaft are real holes.
    ground = group("Ground", env)
    for n, p, s in [
        ("Ground_RailSidingWest", (-49, -0.5, 0),   (6, 1, 72)),
        ("Ground_Main",           (3.5, -0.5, 0),   (75, 1, 72)),
        ("Ground_EastVerge",      (49.5, -0.5, 0),  (5, 1, 72)),
        ("Ground_ShaftSouth",     (44, -0.5, -5),   (6, 1, 62)),
        ("Ground_ShaftNorth",     (44, -0.5, 34.5), (6, 1, 3)),
        ("Ground_PitSouth",       (-40, -0.5, -11), (12, 1, 50)),
        ("Ground_PitNorth",       (-40, -0.5, 31),  (12, 1, 10)),
    ]:
        solid(n, ground, p, s, "M_Concrete_Floor")

    peri = group("Perimeter", env)
    for n, p, s in [
        ("Wall_North", (0, 4.5, 36.5),  (106, 9, 1)),
        ("Wall_South", (0, 4.5, -36.5), (106, 9, 1)),
        ("Wall_West",  (-52.5, 4.5, 0), (1, 9, 74)),
        ("Wall_East",  (52.5, 4.5, 0),  (1, 9, 74)),
    ]:
        solid(n, peri, p, s, "M_Concrete_Stained")

    div = group("Dividers", env)
    for n, p, s in [
        ("Div_Yard_North_A", (-46, 3.5, 5),    (12, 7, 0.6)),
        ("Div_Yard_North_B", (-19.5, 3.5, 5),  (31, 7, 0.6)),
        ("Div_Yard_North_C", (20, 3.5, 5),     (36, 7, 0.6)),
        ("Div_Yard_North_D", (47.5, 3.5, 5),   (9, 7, 0.6)),
        ("Div_Yard_South_A", (-49, 3.5, -15),   (6, 7, 0.6)),
        ("Div_Yard_South_B", (-23.5, 3.5, -15), (35, 7, 0.6)),
        ("Div_Yard_South_C", (15, 3.5, -15),    (30, 7, 0.6)),
        ("Div_Yard_South_D", (43.5, 3.5, -15),  (17, 7, 0.6)),
        ("Div_Silo_Warehouse_A",   (-18, 3.5, 10.5), (0.6, 7, 11)),
        ("Div_Silo_Warehouse_B",   (-18, 3.5, 28.5), (0.6, 7, 15)),
        ("Div_Warehouse_Bunker_A", (26, 3.5, 15.5),  (0.6, 7, 21)),
        ("Div_Warehouse_Bunker_B", (26, 3.5, 33.5),  (0.6, 7, 5)),
        # South-band internals stop at Z=-30 so the rail spine runs clear through.
        ("Div_Siding_Admin", (-24, 3.5, -22.5), (0.6, 7, 15)),
        ("Div_Admin_Dock",   (16, 3.5, -22.5),  (0.6, 7, 15)),
    ]:
        solid(n, div, p, s, "M_Concrete_Stained")

    roofs = group("Roofs", env)
    for n, p, s in [("Roof_Warehouse", (4, 7.2, 20.5),   (44, 0.4, 31)),
                    ("Roof_Admin",     (-4, 7.2, -22.5), (40, 0.4, 15)),
                    ("Roof_Dock",      (34, 7.2, -20.5), (36, 0.4, 11))]:
        solid(n, roofs, p, s, "M_Concrete_Stained")

    # ── Grain intake pit ────────────────────────────────────────────────────────────────
    pit = group("GrainIntakePit", env)
    solid("Pit_Floor", pit, (-40, -2.75, 20), (12, 0.5, 12), "M_Concrete_Floor")
    for n, p, s in [
        ("Pit_Wall_West",   (-46.25, -1.25, 20),   (0.5, 2.5, 12)),
        ("Pit_Wall_East",   (-33.75, -1.25, 20),   (0.5, 2.5, 12)),
        ("Pit_Wall_North",  (-40, -1.25, 26.25),   (12, 2.5, 0.5)),
        ("Pit_Wall_SouthW", (-44.5, -1.25, 13.75), (3, 2.5, 0.5)),
        ("Pit_Wall_SouthE", (-36.5, -1.25, 13.75), (5, 2.5, 0.5)),
    ]:
        solid(n, pit, p, s, "M_Concrete_Stained")
    # 2.5 m drop over 7 m of run: 19.65 degrees, well inside the 45 degree slope limit.
    solid("Pit_Ramp", pit, (-41, -1.35, 17.5), (4, 0.4, 7.43),
          "M_Concrete_Floor", rot=(19.65, 0, 0))
    # Grain spills are decorative: a box collider on a squashed sphere would turn an
    # ankle-deep spill into an impassable 0.85 m wall, since the controller's step offset is
    # 0.32. Walking through the mound is the cheaper lie than walling off half the pit.
    solid("Pit_GrainSpill", pit, (-40, -2.2, 20), (8, 1, 8),
          "M_Grain_Spill", mesh="Sphere", collide=False, shadows=False)

    # ── Silo base ───────────────────────────────────────────────────────────────────────
    silo = group("SiloBase", env)
    for n, x, z, d in (("Silo_1", -47, 31, 10), ("Silo_2", -36, 31, 9), ("Silo_3", -25, 30, 9)):
        solid(n, silo, (x, 8, z), (d, 8, d), "M_Concrete_Silo", mesh="Cylinder")
        solid(n + "_Skirt", silo, (x, 0.6, z), (d + 1.2, 0.6, d + 1.2),
              "M_Concrete_Stained", mesh="Cylinder")
    # Intake silo No. 4. Came down across the gantry. Registered; pending review.
    solid("Silo_4_Collapsed", silo, (-28, 3.6, 13), (7, 9, 7),
          "M_Concrete_Silo", mesh="Cylinder", rot=(0, 25, 78))
    solid("Grain_Spill_A", silo, (-40, 0.3, 10), (9, 1.1, 7),
          "M_Grain_Spill", mesh="Sphere", collide=False, shadows=False)
    solid("Grain_Spill_B", silo, (-31, 0.25, 18), (6, 0.9, 6),
          "M_Grain_Spill", mesh="Sphere", collide=False, shadows=False)
    # Roof debris, also decorative. A colliding beam at chest height is unclimbable at a
    # 0.32 step offset, and these run long enough to seal the western approach outright.
    for i, (p, r, s) in enumerate([((-44, 1.2, 8),  (0, 18, 6),   (16, 0.5, 0.5)),
                                   ((-24, 2.2, 22), (0, -35, 14), (14, 0.45, 0.45)),
                                   ((-43, 0.9, 28), (0, 62, 4),   (11, 0.4, 0.4))]):
        solid("Fallen_Beam_%d" % (i + 1), silo, p, s, "M_Steel_Rusted", rot=r,
              collide=False, shadows=False)
    shelf_unit("Shelving_Silo", silo, -49, 8, 20)

    gantry = group("ConveyorGantry", env)
    solid("Gantry_Beam", gantry, (-14, 5.8, 18.5), (26, 0.7, 1.6), "M_Steel_Rusted")
    for i, x in enumerate((-26, -20, -8, -2)):
        solid("Gantry_Column_%d" % (i + 1), gantry, (x, 2.9, 18.5),
              (0.6, 5.8, 0.6), "M_Steel_Rusted")

    # ── Warehouse floor ────────────────────────────────────────────────────────────────
    wh = group("WarehouseFloor", env)
    for i, x in enumerate((-12, -3, 7, 17)):
        shelf_unit("Shelving_Row_%d" % (i + 1), wh, x, 9, 33)

    mill = group("Machinery", wh)
    solid("Mill_Body", mill, (-15, 2, 34), (3.5, 4, 3.5), "M_Steel_Rusted")
    solid("Mill_Hopper", mill, (-15, 4.8, 34), (2.2, 1.6, 2.2),
          "M_Steel_Galvanised", rot=(0, 45, 0))
    solid("Press_Body", mill, (22, 1.3, 12), (5, 2.6, 3), "M_Steel_Rusted")
    solid("Press_Drum", mill, (22, 3.2, 12), (2.4, 1.8, 2.4),
          "M_Steel_Galvanised", mesh="Cylinder", rot=(0, 0, 90))
    solid("Mill_Secondary", mill, (2, 1.7, 35), (3, 3.4, 4.5), "M_Steel_Rusted")
    solid("Duct_Run", mill, (4, 6.3, 34), (22, 0.8, 0.8),
          "M_Steel_Galvanised", shadows=False)
    solid("Duct_Drop", mill, (14, 4.2, 34), (0.7, 3.4, 0.7),
          "M_Steel_Galvanised", shadows=False)

    # ── Admin / office ─────────────────────────────────────────────────────────────────
    adm = group("AdminOffice", env)
    for name, pos, rot in (("Desk_1", (-18, 0, -20), (0, 0, 0)),
                           ("Desk_2", (-8, 0, -30), (0, 90, 0)),
                           ("Desk_3", (6, 0, -22), (0, -20, 0))):
        d = group(name, adm, pos, rot)
        solid(name + "_Top", d, (0, 0.75, 0), (2.4, 0.1, 1.2),
              "M_Timber_Crate", shadows=False)
        for sx, xt in ((-1.1, "W"), (1.1, "E")):
            for sz, zt in ((-0.5, "S"), (0.5, "N")):
                solid("%s_Leg_%s%s" % (name, xt, zt), d, (sx, 0.375, sz),
                      (0.1, 0.75, 0.1), "M_Steel_Galvanised", collide=False, shadows=False)

    for i, (p, r) in enumerate([((-16.4, 0.45, -20.8), (0, 12, 0)),
                                ((-9.4, 0.45, -29.2), (0, 96, 0)),
                                ((4.6, 0.45, -22.9), (0, -34, 0))]):
        c = group("Chair_%d" % (i + 1), adm, p, r)
        solid("Chair_%d_Seat" % (i + 1), c, (0, 0, 0), (0.5, 0.08, 0.5),
              "M_Timber_Crate", shadows=False)
        solid("Chair_%d_Back" % (i + 1), c, (0, 0.4, -0.24), (0.5, 0.8, 0.08),
              "M_Timber_Crate", shadows=False)

    for i, (p, r) in enumerate([((-22, 0.9, -34), (0, 0, 0)),
                                ((-20.6, 0.9, -34), (0, 0, 0)),
                                ((13, 0.9, -18), (0, 90, 0)),
                                ((13, 0.9, -20.5), (0, 90, 0)),
                                ((-2, 0.3, -34), (0, 15, 90))]):
        solid("Filing_Cabinet_%d" % (i + 1), adm, p, (1.0, 1.8, 0.6),
              "M_Steel_Galvanised", rot=r)

    # Wall-mounted, on the inner face of the south perimeter wall (which stops at Z=-36).
    solid("Map_Board", adm, (-4, 3.4, -35.9), (4, 2.4, 0.12),
          "M_Paint_Institution", collide=False, shadows=False)
    solid("Notice_Board", adm, (-13, 3.2, -35.91), (2.2, 1.4, 0.1),
          "M_Timber_Crate", collide=False, shadows=False)
    solid("Wainscot_Admin", adm, (-14, 0.6, -15.4), (14, 1.2, 0.1),
          "M_Paint_Institution", collide=False, shadows=False)

    # Issued respirators, still on their hooks, still signed for.
    for i, x in enumerate((-20, -17, 8, 11)):
        h = group("Respirator_%d" % (i + 1), adm, (x, 2.4, -35.9))
        solid("Respirator_%d_Hook" % (i + 1), h, (0, 0.35, 0), (0.06, 0.3, 0.06),
              "M_Steel_Galvanised", collide=False, shadows=False)
        solid("Respirator_%d_Mask" % (i + 1), h, (0, 0, 0.08), (0.46, 0.46, 0.34),
              "M_Steel_Rusted", mesh="Sphere", collide=False, shadows=False)
        solid("Respirator_%d_Filter" % (i + 1), h, (0, -0.16, 0.26), (0.2, 0.34, 0.2),
              "M_Steel_Galvanised", mesh="Cylinder", collide=False, shadows=False)

    shelf_unit("Shelving_Admin", adm, 14.6, -34, -26)

    # ── Rail spine and the derailed hopper wagon ───────────────────────────────────────
    rail = group("RailSpine", env)
    for n, z in (("Rail_Left", -33.7), ("Rail_Right", -32.2)):
        solid(n, rail, (0, 0.08, z), (100, 0.16, 0.18),
              "M_Steel_Galvanised", collide=False, shadows=False)
    for i in range(20):
        solid("Sleeper_%02d" % (i + 1), rail, (-48 + i * 5, 0.06, -32.95),
              (0.25, 0.12, 2.6), "M_Timber_Crate", collide=False, shadows=False)

    wag = group("Hopper_Wagon", env, (2, 0, -33), (0, 0, 4))
    solid("Wagon_Body", wag, (0, 2.4, 0), (9, 3.2, 3), "M_Steel_Rusted")
    solid("Wagon_Underframe", wag, (0, 0.7, 0), (9.6, 0.4, 3.2),
          "M_Steel_Rusted", shadows=False)
    for i, (wx, wz) in enumerate([(-3.2, -1.3), (-3.2, 1.3), (3.2, -1.3), (3.2, 1.3)]):
        solid("Wagon_Wheel_%d" % (i + 1), wag, (wx, 0.5, wz), (1.0, 0.2, 1.0),
              "M_Steel_Galvanised", mesh="Cylinder", rot=(0, 0, 90),
              collide=False, shadows=False)

    # ── Loading dock ───────────────────────────────────────────────────────────────────
    dock = group("LoadingDock", env)
    solid("Dock_Platform", dock, (35, 0.6, -20), (34, 1.2, 9), "M_Concrete_Stained")
    solid("Dock_Ramp", dock, (30, 0.6, -26.5), (6, 0.4, 4.2),
          "M_Concrete_Floor", rot=(-16.7, 0, 0))
    solid("Dock_Lip", dock, (35, 1.32, -24.6), (34, 0.24, 0.3),
          "M_Hazard_Yellow", collide=False, shadows=False)

    for i, (p, r, s) in enumerate([((22, 1.9, -19), (0, 0, 0), 1.4),
                                   ((22, 3.3, -19), (0, 20, 0), 1.3),
                                   ((26, 1.9, -21), (0, -8, 0), 1.4),
                                   ((27.5, 1.9, -18), (0, 14, 0), 1.2),
                                   ((31, 1.9, -22), (0, -15, 0), 1.4),
                                   ((40, 1.9, -19), (0, 6, 0), 1.4),
                                   ((40, 3.3, -19), (0, 35, 0), 1.3),
                                   ((43, 1.9, -21), (0, -22, 0), 1.4),
                                   ((19, 0.7, -30), (0, 9, 0), 1.4),
                                   ((21.5, 0.7, -31), (0, -19, 0), 1.3),
                                   ((45, 0.7, -29), (0, 28, 0), 1.4),
                                   ((47, 2.0, -20), (0, -6, 0), 1.5)]):
        solid("Crate_%02d" % (i + 1), dock, p, (s, s, s), "M_Timber_Crate", rot=r)

    for i, (p, r) in enumerate([((33, 1.42, -21), (0, 12, 0)), ((35.5, 1.42, -18), (0, -25, 0)),
                                ((30, 1.42, -17.5), (0, 40, 0)), ((37.5, 1.42, -22), (0, -9, 0))]):
        solid("Ammunition_Box_%d" % (i + 1), dock, p, (0.9, 0.45, 0.55),
              "M_Steel_Galvanised", rot=r, shadows=False)

    # ── Loose barrels and the spawn crate ──────────────────────────────────────────────
    clutter = group("Clutter", env)
    for i, (p, r) in enumerate([((18.5, 0.6, -34), (0, 0, 0)), ((20.5, 0.6, -34.8), (0, 0, 0)),
                                ((24, 0.3, -35), (0, 0, 90)), ((44, 0.6, -34), (0, 0, 0)),
                                ((46.5, 0.3, -33), (0, 40, 90)), ((-30, 0.6, -22), (0, 0, 0)),
                                ((-31.6, 0.3, -21), (0, 25, 90)), ((-45, 0.6, 9), (0, 0, 0)),
                                ((-33, 0.6, 8), (0, 0, 0)), ((10, 0.6, 30), (0, 0, 0)),
                                ((-8, 0.6, -2), (0, 0, 0)), ((-7, 0.3, -4), (0, 55, 90))]):
        solid("Barrel_%02d" % (i + 1), clutter, p, (0.9, 0.6, 0.9),
              "M_Steel_Rusted", mesh="Cylinder", rot=r)
    solid("Crate_Spawn", clutter, (-40, 0.4, -32), (1.6, 0.8, 1.6), "M_Timber_Crate")

    # ── Bunker stairwell ───────────────────────────────────────────────────────────────
    bunk = group("BunkerStairwell", env)
    for n, p, s in [
        ("Headhouse_North",  (44, 2.5, 34.4),   (13, 5, 0.8)),
        ("Headhouse_East",   (50.4, 2.5, 29),   (0.8, 5, 11)),
        ("Headhouse_West",   (37.6, 2.5, 29),   (0.8, 5, 11)),
        ("Headhouse_SouthW", (40, 2.5, 23.6),   (4, 5, 0.8)),
        ("Headhouse_SouthE", (47.8, 2.5, 23.6), (4.4, 5, 0.8)),
        ("Headhouse_Roof",   (44, 5.2, 29),     (13, 0.4, 11)),
    ]:
        solid(n, bunk, p, s, "M_Concrete_Stained")
    solid("Door_Lintel", bunk, (43.8, 4.55, 23.6), (4.4, 0.9, 1.0), "M_Steel_Rusted")
    solid("Blast_Door", bunk, (41.9, 1.9, 25.4), (0.25, 3.6, 3.4),
          "M_Steel_Galvanised", rot=(0, 68, 0))
    solid("Sign_Bunker", bunk, (43.8, 4.55, 23.05), (3.2, 0.7, 0.12),
          "M_Sign_Bunker", collide=False, shadows=False)

    # 14 risers of 0.286 m clear the controller's 0.32 step offset in both directions.
    shaft = group("StairShaft", bunk)
    solid("Shaft_Floor", shaft, (44, -4.25, 29.5), (6, 0.5, 7), "M_Void_Dark")
    for n, p, s in [("Shaft_Wall_West",  (40.75, -2, 29.5), (0.5, 4, 7)),
                    ("Shaft_Wall_East",  (47.25, -2, 29.5), (0.5, 4, 7)),
                    ("Shaft_Wall_North", (44, -2, 32.75),   (6, 4, 0.5))]:
        solid(n, shaft, p, s, "M_Void_Dark")
    for i in range(14):
        solid("Stair_Step_%02d" % (i + 1), shaft,
              (44, -0.2857 * (i + 1) - 0.25, 26.25 + 0.5 * i),
              (5, 0.5, 0.5), "M_Concrete_Floor", shadows=False)

    stripes = group("HazardStripes", bunk)
    for i in range(10):
        solid("Stripe_%02d" % (i + 1), stripes, (44, 0.03, 14 + i), (5, 0.06, 0.8),
              "M_Hazard_Yellow" if i % 2 == 0 else "M_Hazard_Black",
              collide=False, shadows=False)

    # ══ LIGHTING ════════════════════════════════════════════════════════════════════════
    lights = group("=== LIGHTING ===")

    sun_go, _ = sb.obj("Overcast_Sun", parent=lights, pos=(0, 24, 0), rot=(52, -34, 0))
    # Soft shadows on the one directional light; every point light below casts none.
    sun_light_id = sb.light(sun_go, kind=1, color=(0.62, 0.66, 0.68, 1),
                            intensity=0.85, shadows=2, bounce=0.6)

    for name, x, y, z, rng in FIXTURES:
        fx_go, fx_tr = sb.obj(name, parent=lights, pos=(x, y, z))
        sb.light(fx_go, kind=2, color=(0.86, 0.88, 0.78, 1),
                 intensity=2.4, rng=rng, shadows=0, bounce=0.4)
        tube_go, _ = sb.obj(name + "_Tube", parent=fx_tr, pos=(0, 0.16, 0),
                            scale=(0.3, 0.14, 2.4), static=True)
        sb.mesh_renderer(tube_go, "Cube", "M_Fixture_Tube", cast_shadows=False)
        tube_renderer_id = sb._go_components[tube_go][-1]
        sb.mono(fx_go, "FluorescentFlicker",
                "Assembly-CSharp::OblastZero.Gameplay.FluorescentFlicker",
                "  nominalIntensity: 0\n"
                "  sagFloor: 0.45\n"
                "  noiseSpeed: 7.5\n"
                "  noiseDepth: 0.35\n"
                "  secondsBetweenDropouts: 6.5\n"
                "  dropoutDuration: 0.22\n"
                "  fixtureRenderer: {fileID: %d}\n" % tube_renderer_id)

    beacon_go, _ = sb.obj("Bunker_Door_Beacon", parent=lights, pos=(43.8, 5.4, 22.6))
    sb.light(beacon_go, kind=2, color=(1, 0.16, 0.1, 1),
             intensity=4.5, rng=16, shadows=0, bounce=0)

    # ══ ATMOSPHERE ══════════════════════════════════════════════════════════════════════
    atmo = group("=== ATMOSPHERE ===")

    vol_go, _ = sb.obj("Global_Volume", parent=atmo)
    vol_id = sb._fid()
    sb.component(vol_go,
                 "--- !u!114 &%d\nMonoBehaviour:\n"
                 "  m_ObjectHideFlags: 0\n"
                 "  m_CorrespondingSourceObject: {fileID: 0}\n"
                 "  m_PrefabInstance: {fileID: 0}\n"
                 "  m_PrefabAsset: {fileID: 0}\n"
                 "  m_GameObject: {fileID: %d}\n"
                 "  m_Enabled: 1\n"
                 "  m_EditorHideFlags: 0\n"
                 "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
                 "  m_Name: \n"
                 "  m_EditorClassIdentifier: \n"
                 "  m_IsGlobal: 1\n"
                 "  priority: 0\n"
                 "  blendDistance: 0\n"
                 "  weight: 1\n"
                 "  sharedProfile: {fileID: 11400000, guid: %s, type: 2}\n"
                 % (vol_id, vol_go, SCRIPT_GUIDS["Volume"],
                    guid_for("VolumeProfile::Scavenge")), vol_id)

    # Site ambience: wind across the silo mouths and the distant pre-emission rumble.
    # The clip slot is deliberately empty — audio lands in the polish pass (roadmap stage 7);
    # the source is otherwise fully configured, so dropping a clip in is the only step left.
    amb_go, _ = sb.obj("Site_Ambience", parent=atmo)
    amb_id = sb._fid()
    sb.component(amb_go,
                 "--- !u!82 &%d\nAudioSource:\n"
                 "  m_ObjectHideFlags: 0\n"
                 "  m_CorrespondingSourceObject: {fileID: 0}\n"
                 "  m_PrefabInstance: {fileID: 0}\n"
                 "  m_PrefabAsset: {fileID: 0}\n"
                 "  m_GameObject: {fileID: %d}\n"
                 "  m_Enabled: 1\n"
                 "  serializedVersion: 4\n"
                 "  OutputAudioMixerGroup: {fileID: 0}\n"
                 "  m_audioClip: {fileID: 0}\n"
                 "  m_Resource: {fileID: 0}\n"
                 "  m_PlayOnAwake: 1\n"
                 "  m_Volume: 0.45\n"
                 "  m_Pitch: 1\n"
                 "  Loop: 1\n"
                 "  Mute: 0\n"
                 "  Spatialize: 0\n"
                 "  SpatializePostEffects: 0\n"
                 "  Priority: 128\n"
                 "  DopplerLevel: 0\n"
                 "  MinDistance: 1\n"
                 "  MaxDistance: 500\n"
                 "  Pan2D: 0\n"
                 "  rolloffMode: 1\n"
                 "  BypassEffects: 0\n"
                 "  BypassListenerEffects: 0\n"
                 "  BypassReverbZones: 0\n"
                 "  panLevelCustomCurve:\n    serializedVersion: 2\n    m_Curve: []\n"
                 "    m_PreInfinity: 2\n    m_PostInfinity: 2\n    m_RotationOrder: 4\n"
                 "  spreadCustomCurve:\n    serializedVersion: 2\n    m_Curve: []\n"
                 "    m_PreInfinity: 2\n    m_PostInfinity: 2\n    m_RotationOrder: 4\n"
                 "  reverbZoneMixCustomCurve:\n    serializedVersion: 2\n    m_Curve: []\n"
                 "    m_PreInfinity: 2\n    m_PostInfinity: 2\n    m_RotationOrder: 4\n"
                 % (amb_id, amb_go), amb_id)

    # ══ SYSTEMS ═════════════════════════════════════════════════════════════════════════
    sysg = group("=== SYSTEMS ===")

    # -- Player. Tagged Player so BunkerEntranceTrigger's CompareTag hits; the camera is
    #    tagged MainCamera and wired into cameraPivot explicitly rather than left to
    #    Camera.main, because _Bootstrap stays loaded underneath this scene.
    player_go, player_tr = sb.obj("Player", pos=PLAYER_SPAWN, rot=PLAYER_FACING, tag="Player")

    cam_go, cam_tr = sb.obj("Main Camera", parent=player_tr,
                            pos=(0, EYE_HEIGHT, 0), tag="MainCamera")
    cam_id = sb._fid()
    sb.component(cam_go,
                 "--- !u!20 &%d\nCamera:\n"
                 "  m_ObjectHideFlags: 0\n"
                 "  m_CorrespondingSourceObject: {fileID: 0}\n"
                 "  m_PrefabInstance: {fileID: 0}\n"
                 "  m_PrefabAsset: {fileID: 0}\n"
                 "  m_GameObject: {fileID: %d}\n"
                 "  m_Enabled: 1\n"
                 "  serializedVersion: 2\n"
                 "  m_ClearFlags: 2\n"
                 "  m_BackGroundColor: {r: 0.36, g: 0.385, b: 0.36, a: 1}\n"
                 "  m_projectionMatrixMode: 1\n"
                 "  m_GateFitMode: 2\n"
                 "  m_FOVAxisMode: 0\n"
                 "  m_Iso: 200\n  m_ShutterSpeed: 0.005\n  m_Aperture: 16\n"
                 "  m_FocusDistance: 10\n  m_FocalLength: 50\n  m_BladeCount: 5\n"
                 "  m_Curvature: {x: 2, y: 11}\n  m_BarrelClipping: 0.25\n"
                 "  m_Anamorphism: 0\n  m_SensorSize: {x: 36, y: 24}\n"
                 "  m_LensShift: {x: 0, y: 0}\n"
                 "  m_NormalizedViewPortRect:\n    serializedVersion: 2\n"
                 "    x: 0\n    y: 0\n    width: 1\n    height: 1\n"
                 "  near clip plane: 0.1\n"
                 "  far clip plane: 220\n"
                 "  field of view: 68\n"
                 "  orthographic: 0\n"
                 "  orthographic size: 5\n"
                 "  m_Depth: 0\n"
                 "  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n"
                 "  m_RenderingPath: -1\n"
                 "  m_TargetTexture: {fileID: 0}\n"
                 "  m_TargetDisplay: 0\n"
                 "  m_TargetEye: 3\n"
                 "  m_HDR: 1\n  m_AllowMSAA: 1\n  m_AllowDynamicResolution: 0\n"
                 "  m_ForceIntoRT: 0\n  m_OcclusionCulling: 1\n"
                 "  m_StereoConvergence: 10\n  m_StereoSeparation: 0.022\n"
                 % (cam_id, cam_go), cam_id)

    ucd_id = sb._fid()
    sb.component(cam_go,
                 "--- !u!114 &%d\nMonoBehaviour:\n"
                 "  m_ObjectHideFlags: 0\n"
                 "  m_CorrespondingSourceObject: {fileID: 0}\n"
                 "  m_PrefabInstance: {fileID: 0}\n"
                 "  m_PrefabAsset: {fileID: 0}\n"
                 "  m_GameObject: {fileID: %d}\n"
                 "  m_Enabled: 1\n"
                 "  m_EditorHideFlags: 0\n"
                 "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
                 "  m_Name: \n"
                 "  m_EditorClassIdentifier: \n"
                 "  m_RenderShadows: 1\n"
                 "  m_RequiresDepthTextureOption: 2\n"
                 "  m_RequiresOpaqueTextureOption: 2\n"
                 "  m_CameraType: 0\n"
                 "  m_Cameras: []\n"
                 "  m_RendererIndex: -1\n"
                 "  m_VolumeLayerMask:\n    serializedVersion: 2\n    m_Bits: 1\n"
                 "  m_VolumeTrigger: {fileID: 0}\n"
                 "  m_VolumeFrameworkUpdateModeOption: 2\n"
                 "  m_RenderPostProcessing: 1\n"
                 "  m_Antialiasing: 1\n"
                 "  m_AntialiasingQuality: 2\n"
                 "  m_StopNaN: 1\n"
                 "  m_Dithering: 1\n"
                 "  m_ClearDepth: 1\n"
                 "  m_AllowXRRendering: 1\n"
                 "  m_AllowHDROutput: 1\n"
                 "  m_UseScreenCoordOverride: 0\n"
                 "  m_ScreenSizeOverride: {x: 0, y: 0, z: 0, w: 0}\n"
                 "  m_ScreenCoordScaleBias: {x: 0, y: 0, z: 0, w: 0}\n"
                 "  m_RequiresDepthTexture: 0\n"
                 "  m_RequiresColorTexture: 0\n"
                 "  m_Version: 2\n"
                 "  m_TaaSettings:\n    quality: 3\n    frameInfluence: 0.1\n"
                 "    jitterScale: 1\n    mipBias: 0\n    varianceClampScale: 0.9\n"
                 "    contrastAdaptiveSharpening: 0\n"
                 % (ucd_id, cam_go, SCRIPT_GUIDS["UniversalAdditionalCameraData"]), ucd_id)

    al_id = sb._fid()
    sb.component(cam_go,
                 "--- !u!81 &%d\nAudioListener:\n"
                 "  m_ObjectHideFlags: 0\n"
                 "  m_CorrespondingSourceObject: {fileID: 0}\n"
                 "  m_PrefabInstance: {fileID: 0}\n"
                 "  m_PrefabAsset: {fileID: 0}\n"
                 "  m_GameObject: {fileID: %d}\n"
                 "  m_Enabled: 1\n" % (al_id, cam_go), al_id)

    cc_id = sb._fid()
    sb.component(player_go,
                 "--- !u!143 &%d\nCharacterController:\n"
                 "  m_ObjectHideFlags: 0\n"
                 "  m_CorrespondingSourceObject: {fileID: 0}\n"
                 "  m_PrefabInstance: {fileID: 0}\n"
                 "  m_PrefabAsset: {fileID: 0}\n"
                 "  m_GameObject: {fileID: %d}\n"
                 "  m_Material: {fileID: 0}\n"
                 "  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
                 "  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
                 "  m_LayerOverridePriority: 0\n"
                 "  m_ProvidesContacts: 0\n"
                 "  m_Enabled: 1\n"
                 "  serializedVersion: 3\n"
                 "  m_Height: 1.8\n"
                 "  m_Radius: 0.35\n"
                 "  m_SlopeLimit: 45\n"
                 "  m_StepOffset: 0.32\n"
                 "  m_SkinWidth: 0.08\n"
                 "  m_MinMoveDistance: 0.001\n"
                 "  m_Center: {x: 0, y: 0.92, z: 0}\n" % (cc_id, player_go), cc_id)

    player_ctrl_id = sb.mono(
        player_go, "ScavengePlayerController",
        "Assembly-CSharp::OblastZero.Gameplay.ScavengePlayerController",
        "  walkSpeed: 4.5\n"
        "  sprintSpeed: 7\n"
        "  gravity: -9.81\n"
        "  cameraPivot: {fileID: %d}\n"
        "  lookSensitivity: 0.1\n"
        "  pitchClampDegrees: 85\n"
        "  interactRange: 3\n"
        "  interactMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n" % cam_tr)

    # -- ScavengeController: routes pickups into InventoryManager / CrewManager.
    ctrl_go, _ = sb.obj("Scavenge_Controller", parent=sysg)
    sb.mono(ctrl_go, "ScavengeController",
            "Assembly-CSharp::OblastZero.Gameplay.ScavengeController",
            "  player: {fileID: %d}\n" % player_ctrl_id)

    # -- HUD: self-building canvas. Thresholds mirror BalanceConstants.SCAVENGE_*.
    hud_go, _ = sb.obj("Scavenge_HUD", parent=sysg)
    sb.mono(hud_go, "ScavengeHUD", "Assembly-CSharp::OblastZero.UI.ScavengeHUD",
            "  normalColor: {r: 0.9, g: 0.9, b: 0.86, a: 1}\n"
            "  warnColor: {r: 1, g: 0.7, b: 0.2, a: 1}\n"
            "  dangerColor: {r: 1, g: 0.25, b: 0.2, a: 1}\n"
            "  warnThreshold: %s\n"
            "  dangerThreshold: %s\n"
            "  initialSecondsDisplay: %s\n"
            % (f(SCAVENGE_TIMER_WARNING_THRESHOLD),
               f(SCAVENGE_TIMER_CRITICAL_THRESHOLD),
               f(SCAVENGE_TIMER_SECONDS)))

    # -- Bunker door trigger, spanning the stair mouth inside the headhouse.
    trig_go, _ = sb.obj("Bunker_Entrance_Trigger", parent=sysg, pos=(44, 1.4, 27))
    sb.box_collider(trig_go, is_trigger=True, size=(6, 3.6, 2.6))
    sb.mono(trig_go, "BunkerEntranceTrigger",
            "Assembly-CSharp::OblastZero.Gameplay.BunkerEntranceTrigger",
            "  playerTag: Player\n")

    # ══ PICKUPS ═════════════════════════════════════════════════════════════════════════
    # Shape comes from the item's VisualArchetype, so a pry bar reads as a bar on the shelf
    # and a dossier reads as paperwork, instead of 22 identical cubes. The archetype tables
    # are owned by VisualArchetype.cs and mirrored in tools/visual_archetypes.py, which
    # main() has already proved identical before we get here.
    #
    # Trigger colliders stay a fixed generous box regardless of the visual: they must not
    # block the CharacterController, but a flattened document still has to be easy to hit
    # with a 3 m crosshair raycast. Decoupling them is deliberate — shrinking the trigger to
    # match a 5 cm-thick folder would make it nearly unclickable under time pressure.
    pick = group("=== PICKUPS ===")
    archetype_census = {}
    for data_id, kind, qty, dur, contam, pos, yaw, mat in PICKUPS:
        is_crew = kind == CREW
        name = ("Crew_" if is_crew else "Pickup_") + data_id

        archetype = "Crew" if is_crew else va.derive(ITEM_CATEGORIES.get(data_id), data_id)
        mesh, scale = va.SHAPES[archetype]
        archetype_census[archetype] = archetype_census.get(archetype, 0) + 1

        # Half-extents in world units, accounting for non-unit primitive meshes: a Cylinder
        # or Capsule mesh is 2 units tall, so its Y half-extent is the Y scale, not half of it.
        base = BOX_LOCAL[mesh]
        half = tuple(scale[i] * base[i] / 2.0 for i in range(3))

        # The manifest's Y values were authored against the old 0.34 cube resting on a surface.
        # Keep each pickup's BOTTOM where it was and let the silhouette change above it —
        # otherwise a flattened document hovers 14 cm in the air and the support check fails.
        drop = LEGACY_PICKUP_HALF_Y - half[1]
        pos = (pos[0], pos[1] - drop, pos[2]) if not is_crew else pos

        if is_crew:
            go, _ = sb.obj(name, parent=pick, pos=pos, rot=(0, yaw, 58), scale=scale)
            sb.mesh_renderer(go, mesh, mat, cast_shadows=True)
            sb.capsule_collider(go, is_trigger=True, radius=0.62, height=2.3)
            placed.append((name, pos, (0.62, 0.72, 0.62), True))
        else:
            go, _ = sb.obj(name, parent=pick, pos=pos, rot=(0, yaw, 0), scale=scale)
            sb.mesh_renderer(go, mesh, mat, cast_shadows=False)
            # Collider size is LOCAL, so a fixed number would scale with the mesh and give a
            # 1.6 m trigger on a rifle and a 0.3 m one on a can. Divide out the scale to keep
            # every pickup the same size to the crosshair.
            sb.box_collider(go, is_trigger=True,
                            size=tuple(PICKUP_TRIGGER_WORLD_M / s for s in scale))
            placed.append((name, pos, half, False))

        sb.mono(go, "ScavengePickup", "Assembly-CSharp::OblastZero.Gameplay.ScavengePickup",
                "  kind: %d\n"
                "  dataId: %s\n"
                "  quantity: %d\n"
                "  durabilityOverride: %d\n"
                "  contamination: %s\n" % (kind, data_id, qty, dur, f(contam)))

    print("pickup silhouettes: " + ", ".join(
        "%s x%d" % (k, v) for k, v in sorted(archetype_census.items())))

    return sb, sun_light_id, solids, placed


# ─── Verification ───────────────────────────────────────────────────────────────────────

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
    return problems, reachable * step * step


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

def verify(scene_text):
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
    known.add(guid_for("VolumeProfile::Scavenge"))
    for g in guids - known:
        problems.append("unrecognised guid %s" % g)

    # Component lists must reference real components.
    for m in re.finditer(r"- component: \{fileID: (\d+)\}", scene_text):
        if int(m.group(1)) not in defined:
            problems.append("GameObject references missing component %s" % m.group(1))

    return problems


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


def verify_pickup_ids():
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
    for data_id, kind, _, _, _, _, _, _ in PICKUPS:
        pool = crew_ids if kind == CREW else item_ids
        if data_id not in pool:
            missing.append("%s (%s)" % (data_id, "crew" if kind == CREW else "item"))
    return missing, len(item_ids), len(crew_ids)


# ─── Entry point ────────────────────────────────────────────────────────────────────────

def write(path, text):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)


def main():
    if not os.path.isdir("Assets/Scenes"):
        raise SystemExit("run this from the project root (Assets/Scenes not found)")

    missing, n_items, n_crew = verify_pickup_ids()
    if missing:
        raise SystemExit("pickup ids not in the database: " + ", ".join(missing))
    print("database check: %d item ids, %d crew ids, all %d pickup ids resolve"
          % (n_items, n_crew, len(PICKUPS)))

    # Gate: the silhouettes baked into the scene must be the ones the runtime would spawn.
    # Aborts before anything is written if visual_archetypes.py has drifted from the C#.
    print("archetype check: " + va.assert_matches_csharp())

    global ITEM_CATEGORIES
    ITEM_CATEGORIES = load_item_categories()
    uncategorised = [d for d, k, *_ in PICKUPS if k == ITEM and not ITEM_CATEGORIES.get(d)]
    if uncategorised:
        raise SystemExit("pickup items with no category, cannot pick a silhouette: "
                         + ", ".join(uncategorised))
    print("category check: %d items categorised" % len(ITEM_CATEGORIES))

    # Materials
    for name, (base, smooth, metal, emis) in sorted(MATERIALS.items()):
        write("%s/%s.mat" % (MATERIAL_DIR, name), material_yaml(name, base, smooth, metal, emis))
        write("%s/%s.mat.meta" % (MATERIAL_DIR, name),
              "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
              "  externalObjects: {}\n  mainObjectFileID: 2100000\n"
              "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
              % guid_for("Material::" + name))
    print("wrote %d URP Lit materials -> %s" % (len(MATERIALS), MATERIAL_DIR))

    # Volume profile
    write(VOLUME_PROFILE_PATH, volume_profile_yaml())
    write(VOLUME_PROFILE_PATH + ".meta",
          "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
          "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
          "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
          % guid_for("VolumeProfile::Scavenge"))
    print("wrote %s" % VOLUME_PROFILE_PATH)

    # Scene
    sb, sun_id, solids, placed = build()
    scene_text = sb.emit(sun_id)

    problems = verify(scene_text)
    if problems:
        for p in sorted(set(problems)):
            print("  FAIL " + p)
        raise SystemExit("scene reference check failed (%d problems)" % len(set(problems)))

    place_problems = verify_placement(solids, placed)
    if place_problems:
        for p in place_problems:
            print("  FAIL " + p)
        raise SystemExit("pickup placement check failed (%d problems)" % len(place_problems))
    print("placement check: all %d pickups clear of geometry and supported" % len(placed))

    reach_problems, area = verify_reachability(solids, placed, PLAYER_SPAWN, (44.0, 27.0))
    if reach_problems:
        for p in reach_problems:
            print("  FAIL " + p)
        raise SystemExit("reachability check failed (%d problems)" % len(reach_problems))
    print("reachability check: bunker + all %d pickups reachable from spawn "
          "(%.0f m2 walkable)" % (len(placed), area))

    write(SCENE_PATH, scene_text)
    write(SCENE_PATH + ".meta",
          "fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n"
          "  externalObjects: {}\n  userData: \n"
          "  assetBundleName: \n  assetBundleVariant: \n" % guid_for("Scene::Scavenge"))

    n_go = scene_text.count("\nGameObject:\n")
    print("wrote %s — %d GameObjects, %d lines, all references resolve"
          % (SCENE_PATH, n_go, scene_text.count("\n")))
    print("scene guid: %s" % guid_for("Scene::Scavenge"))


if __name__ == "__main__":
    main()
