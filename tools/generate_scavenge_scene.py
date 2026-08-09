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
import re
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from scavenge_scene_lib import (                                    # noqa: E402
    MATERIALS, MATERIAL_DIR, VOLUME_PROFILE_PATH, SCRIPT_GUIDS,
    DUST_MATERIAL_NAME, RANGE_RING_MATERIAL_NAME,
    SceneBuilder, guid_for, material_yaml, particle_material_yaml,
    volume_profile_yaml, f, assert_script_guids,
)

# Every validation gate lives in scene_verify_lib so all three site generators run the SAME copy.
# They were local to this file until the Census Office and Reservoir generators needed them; three
# divergent copies would have left the newest, least-played site running the oldest gates.
from scene_rig_lib import (                                         # noqa: E402
    add_rig, jitter as _jitter, standard_pickup_variation,
)
from scene_verify_lib import (                                      # noqa: E402
    NAV_GRID_DIR, NAV_GRID_MAGIC, NAV_IMPASSABLE, NavGrid,
    _rot_matrix, _apply, rotated_half_extents,
    verify, verify_placement, verify_reachability, verify_nav_grid,
    write, write_bytes,
)

# Site-specific: the resource name this level's grid ships under. Kept out of the shared library
# because a shared default is how two sites end up overwriting one another's navigation grid.
NAV_GRID_RESOURCE = "navgrid_scavenge"

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

# Key add_rig hashes for this site's volume-profile guid. Per-site so two levels can
# never reference one another's profile asset.
VOLUME_PROFILE_KEY = "VolumeProfile::Scavenge"

# Where the run ends. The trigger spans the stair mouth inside the headhouse; the beacon is the
# one warm light in the level and sits just outside the door.
BUNKER_BEACON_POS = (43.8, 5.4, 22.6)
BUNKER_TRIGGER_POS = (44, 1.4, 27)
BUNKER_TRIGGER_SIZE = (6, 3.6, 2.6)

# id -> ItemCategory name, loaded from the live database in main(). Empty until then.
ITEM_CATEGORIES = {}

PLAYER_SPAWN = (-44, 0.15, -30)
PLAYER_FACING = (0, 38, 0)
EYE_HEIGHT = 1.62

# Mirrors BalanceConstants so the scene never contradicts the balance file. Every name here must
# exist there with the same value; assert_balance_mirror() proves it before the scene is written.
#
# The mirror existed as a comment-only convention until the anomaly layer landed, and a comment is
# not a gate. A drifted mirror is the quiet kind of wrong: the scene serializes one interaction
# range onto the controller while BalanceConstants reports another to the HUD and the range ring,
# so the ring draws a circle the raycast does not honour and nothing anywhere logs a complaint.
SCAVENGE_TIMER_SECONDS = 60
SCAVENGE_TIMER_WARNING_THRESHOLD = 15
SCAVENGE_TIMER_CRITICAL_THRESHOLD = 5
SCAVENGE_INTERACTION_RANGE = 3
GEIGER_DETECTION_RANGE_M = 14
BACKLOG_TIME_DILATION_FACTOR = 0.02
CARBON_COPY_MAX_DUPLICATES = 4

# ─── Atmosphere ─────────────────────────────────────────────────────────────────────────
# Airborne dust. The emitter box is a fraction of the 104 x 72 m footprint and tracks the view,
# because 300 motes spread across the whole depot is roughly one per twenty-five cubic metres and
# reads as nothing at all; ScavengeDustField simulates in world space so they still hang in the air
# as the player runs through them. Sizes and lifetimes are the brief's.
DUST_TINT = (0.78, 0.78, 0.74, 0.30)
DUST_BOX = (30, 8, 30)
DUST_EMISSION_PER_SECOND = 15
DUST_MAX_PARTICLES = 300
DUST_LIFETIME = (10, 15)
DUST_DRIFT = (0.10, 0.30)

# Interaction-range ring. Faint, because it is a background affordance: the crosshair prompt and the
# hover label are what actually tell the player what is in reach, and this only says how far that is.
RANGE_RING_TINT = (0.35, 0.80, 0.85, 0.10)

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


# ─── Anomaly zones (bible §5 / BESTIARY.md) ─────────────────────────────────────────────
#
# One of each of the three, placed against the decision each is meant to create. Format:
#   (name, script_key, class_id, position, trigger_size, extra_yaml_fields)
#
# Placement reasoning, which is the part worth reviewing:
#
#   Carbon Copy — the warehouse floor, over the shelving row at X -3. It has to sit on a *cluster*
#   of pickups or the duplication never fires: the zone only acts when something is grabbed inside
#   it, so a volume over bare concrete is a volume that does nothing. The shelf rows are also where
#   a player is already making take/leave decisions against the carry cap, which is exactly the
#   judgement the copies are there to corrupt.
#
#   Interview — the admin/office room, which is the only interior space in the depot with a desk,
#   and the bible names offices specifically. It is off the direct route, so it costs a detour to
#   find: the reward is meant to be something you go looking for, not something you trip over.
#
#   Backlog — the EAST doorway of the Z=+5 wall (X +38..+43), which is the last gate on the fastest
#   line from the spawn to the bunker door. This is the placement that makes the anomaly a decision
#   rather than scenery. The direct route is ~18 s of the 60 s budget; routing around it through the
#   central doorway at X -4..+2 and back east costs roughly six seconds of sprint. So a player with
#   time in hand goes around and pays six seconds, and a player at fifteen seconds has to look at the
#   haze and decide whether they believe it. Both readings are correct play. Putting it anywhere off
#   the critical path would have made it free to ignore.
#
# The bible gives the Carbon Copy "~2 cubic meters", which as a literal trigger is a 1.26 m cube —
# smaller than the shelf bay it sits in and effectively impossible to grab from on purpose. The
# volume below is sized to the shelf section instead. That is a deliberate deviation from the
# physical description in service of the mechanic it exists to support.
ANOMALY_ZONES = [
    ("Anomaly_CarbonCopy_Warehouse", "CarbonCopyAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.CarbonCopyAnomaly",
     (-3.0, 1.6, 19.0), (4.0, 3.2, 5.0),
     "  maxDuplicates: 4\n"),

    ("Anomaly_Interview_AdminRoom", "InterviewAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.InterviewAnomaly",
     (-4.0, 1.6, -25.0), (9.0, 3.2, 7.0),
     "  sitPosition: {fileID: 0}\n"),

    ("Anomaly_Backlog_EastDoorway", "BacklogAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.BacklogAnomaly",
     (40.5, 2.0, 5.0), (6.0, 4.0, 5.5),
     "  timeDilationFactor: 0.02\n"),
]

# ─── Mutant spawn points (bible §5 / BESTIARY.md "MUTANTS") ─────────────────────────────
#
# Four corners of the open yard, on ground the walkability flood-fill already proves is standable
# and connected. They are all >= 25 m from the SW spawn and none sits on the direct route to the
# bunker, so a Census-Taker is something the player becomes aware of rather than something that
# appears on top of them.
#
# MutantSpawner rejects any point within 18 m of the player at spawn time and falls back to the
# navigation grid if all four are rejected, so these are a preference rather than a contract. The
# depot's own threat profile places zero Census-Takers (wrong region for them, per the bible), so
# at this site the points go unused -- but they ship regardless, so raising the depot's threat is
# one number in ScavengeSiteCatalog and not a scene regeneration.
MUTANT_SPAWN_POINTS = [
    (-46.0, 0.15, -4.0),    # west yard, behind the silo line
    (34.0, 0.15, -8.0),     # east yard, by the dock ramp
    (-30.0, 0.15, 24.0),    # north-west, deep in the silo base
    (18.0, 0.15, 30.0),     # north-east, far end of the warehouse floor
]

# The Backlog's haze child. Sized from the zone's own trigger at runtime (BacklogAnomaly.Awake), so
# the number here is only the pre-Awake default and cannot drift away from the collider.
BACKLOG_MOTE_COUNT = 260






# ─── Deterministic placement variety ────────────────────────────────────────────────────
#
# A depot nobody has swept in years does not lay its stock out on a grid. Every pickup already
# carried an authored yaw (contrary to the Phase 3 brief, which described them as unrotated), so
# what was actually missing was tilt and scale spread: five identical ration tins at five
# identical sizes read as instanced, not as stock.
#
# Everything here is seeded from the object's own name, never from `random` or the clock, because
# the scene is required to regenerate byte-identically (CLAUDE.md §14). Two runs of this script
# must produce the same tilt on the same crate or the md5 determinism check is meaningless.

PICKUP_TILT_FRACTION = 0.30     # share of pickups that have been knocked askew
PICKUP_TILT_DEGREES = 5.0       # peak tilt on X and Z for those that have
PICKUP_SCALE_SPREAD = 0.10      # ±10% on all axes

# What the placement gates do and do not constrain here, measured rather than assumed:
#
#   * PICKUP_SCALE_SPREAD is gated. Raising it past ~0.5 starts tripping the burial check
#     (item_bureau_dossier into Desk_2_Top first, then item_axe into Shelving_Row_3_Deck_1, and
#     nine failures by 4.0). The shipped 0.10 therefore sits about five times below the first
#     observed failure — a margin that was checked, not guessed.
#
#   * PICKUP_TILT_DEGREES is NOT gated, and cranking it to 75 degrees still passes both checks.
#     That is the construction being provably safe rather than the gate being decoration, and the
#     reason is worth stating so nobody later reads a passing run as a licence to tilt further.
#     Rotating a box about X or Z lifts its world AABB and shrinks the horizontal reach of its
#     dominant axis; it cannot grow the footprint. The support check tests the AABB base, and the
#     `drop` arithmetic below re-seats that base on the authored surface for any tilt, so the check
#     is satisfied identically at 5 degrees and at 75. The burial check tests whether the object's
#     CENTRE is inside a solid, and tilt does not move the centre.
#     A real tilt gate would need box-vs-box overlap rather than centre containment. At 5 degrees
#     the largest excursion in the set is the 0.86 m WeaponLong lifting its tip 3.7 cm, so that gate
#     is not worth writing today — but a tilt large enough to matter would need it first.


def pickup_variation(name, base_scale, is_crew):
    """This site's placement variety. Thin wrapper: the maths is shared so all three sites agree."""
    return standard_pickup_variation(name, base_scale, is_crew,
                                     PICKUP_SCALE_SPREAD, PICKUP_TILT_FRACTION, PICKUP_TILT_DEGREES)


# ─── Clutter manifest ───────────────────────────────────────────────────────────────────
# (label, anchor_xyz, companion_count, mesh, half_scale, material)
#
# Each cluster puts companion stock around a real pickup's authored position. The anchor Y is the
# surface the pickup rests on; companions are offset laterally by up to CLUTTER_SPREAD_M and never
# vertically, so a companion cannot end up floating over the shelf edge or sunk into it.
CLUTTER_SPREAD_M = 0.55

CLUTTER_CLUSTERS = [
    # Loading dock: the crate stack the ammunition came out of.
    ("Dock_Crates", (32.2, 1.72, -21.8), 3, "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    # Loading dock: spare ammunition boxes alongside the two live pickups.
    ("Dock_Ammo", (35.0, 1.72, -19.0), 2, "Cube", (0.30, 0.18, 0.22), "M_Pickup_Ammo"),
    # Admin: a second and third folder in the pile the dossier came off.
    ("Admin_Files", (-8.6, 0.95, -30.4), 2, "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    # Admin: emptied medical packaging beside the bandages.
    ("Admin_Medical", (6.5, 0.96, -22.6), 2, "Cube", (0.30, 0.20, 0.20), "M_Pickup_Medical"),
    # Warehouse: ration tins that did not get requisitioned.
    ("Warehouse_Tins", (-3.4, 1.12, 15.5), 3, "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
]

# Things that ended up on the floor. Fixed coordinates on open concrete, well clear of the routes
# and of every pickup, so no clutter prop can be mistaken for loot the player failed to take.
CLUTTER_FLOOR = [
    ("Floor_Tin_1", (-38.4, 0.13, -28.2), "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    ("Floor_Folder_1", (-14.2, 0.03, -24.6), "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Floor_Folder_2", (-13.1, 0.03, -25.4), "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Floor_Crate_1", (9.6, 0.30, -26.8), "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Floor_Tin_2", (11.8, 0.13, 21.4), "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    ("Floor_Ammo_1", (27.6, 0.18, -24.2), "Cube", (0.30, 0.18, 0.22), "M_Pickup_Ammo"),
    ("Floor_Tin_3", (-46.2, 0.13, 8.6), "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    ("Floor_Crate_2", (20.4, 0.30, 6.2), "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
]


def build_clutter():
    """
    Expands the clutter manifest into concrete (name, pos, rot, mesh, scale, material) tuples.

    Pure and deterministic — every offset and angle comes from _jitter keyed on the prop's own name,
    so calling this twice returns identical lists. build() relies on that: it calls this once to
    emit and once to count.
    """
    props = []

    for label, anchor, count, mesh, scale, mat in CLUTTER_CLUSTERS:
        for i in range(count):
            name = "%s_%d" % (label, i + 1)
            dx = _jitter(name, 0, -CLUTTER_SPREAD_M, CLUTTER_SPREAD_M)
            dz = _jitter(name, 1, -CLUTTER_SPREAD_M, CLUTTER_SPREAD_M)
            yaw = _jitter(name, 2, 0.0, 360.0)
            tilt = _jitter(name, 3, -6.0, 6.0)
            varied = tuple(scale[k] * (1.0 + _jitter(name, 4 + k, -0.12, 0.12)) for k in range(3))
            props.append((name,
                          (anchor[0] + dx, anchor[1], anchor[2] + dz),
                          (tilt, yaw, 0.0),
                          mesh, varied, mat))

    for name, pos, mesh, scale, mat in CLUTTER_FLOOR:
        yaw = _jitter(name, 0, 0.0, 360.0)
        # Floor props lie at a lazier angle than shelf stock — they were dropped, not set down.
        tilt_x = _jitter(name, 1, -14.0, 14.0)
        tilt_z = _jitter(name, 2, -14.0, 14.0)
        varied = tuple(scale[k] * (1.0 + _jitter(name, 3 + k, -0.12, 0.12)) for k in range(3))
        props.append((name, pos, (tilt_x, yaw, tilt_z), mesh, varied, mat))

    return props


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
    # ══ SHARED RIG ══════════════════════════════════════════════════════════════════════
    # Lighting, atmosphere, systems, anomalies, the bunker trigger and the pickup emitter all live
    # in scene_rig_lib so every site runs one copy. This module owns the LEVEL; that one owns what
    # a Blowout level always has. See scene_rig_lib.add_rig for the cfg contract.
    sun_light_id = add_rig(sb, group, solid, placed, sys.modules[__name__])

    return sb, sun_light_id, solids, placed

    return sb, sun_light_id, solids, placed


# ─── Verification ───────────────────────────────────────────────────────────────────────










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

BALANCE_CONSTANTS_PATH = "Assets/_Project/Scripts/Core/BalanceConstants.cs"

# The Python mirrors above, paired with the C# constant each must equal.
BALANCE_MIRROR = {
    "SCAVENGE_TIMER_SECONDS": SCAVENGE_TIMER_SECONDS,
    "SCAVENGE_TIMER_WARNING_THRESHOLD": SCAVENGE_TIMER_WARNING_THRESHOLD,
    "SCAVENGE_TIMER_CRITICAL_THRESHOLD": SCAVENGE_TIMER_CRITICAL_THRESHOLD,
    "SCAVENGE_INTERACTION_RANGE": SCAVENGE_INTERACTION_RANGE,
    "GEIGER_DETECTION_RANGE_M": GEIGER_DETECTION_RANGE_M,
    "BACKLOG_TIME_DILATION_FACTOR": BACKLOG_TIME_DILATION_FACTOR,
    "CARBON_COPY_MAX_DUPLICATES": CARBON_COPY_MAX_DUPLICATES,
}


def assert_balance_mirror():
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
    for name, mirrored in BALANCE_MIRROR.items():
        if name not in declared:
            problems.append("%s: not declared in BalanceConstants.cs" % name)
        elif abs(declared[name] - float(mirrored)) > 1e-6:
            problems.append("%s: generator says %s, BalanceConstants says %s"
                            % (name, mirrored, declared[name]))

    if problems:
        raise SystemExit("balance mirror check FAILED:\n  " + "\n  ".join(problems))
    return "balance mirror: %d constants match BalanceConstants.cs" % len(BALANCE_MIRROR)


def verify_anomaly_zones():
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
    for name, _script, _cls, pos, size, _fields in ANOMALY_ZONES:
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

    # Gate: every project script GUID this scene references must still match its .cs.meta. A stale
    # entry produces a component that is present but never runs, which no other check would catch.
    print(assert_script_guids())

    # Gate: balance numbers serialized into the scene must equal the C# constants everything else
    # reads. A drifted mirror produces two internally-consistent halves that disagree with each other.
    print(assert_balance_mirror())

    # Gate: anomaly volumes must be disjoint. AnomalyZone.ZoneAt takes the first match and
    # BacklogAnomaly restores speed to an absolute 1 on exit, both of which are only correct while
    # no two zones overlap.
    overlaps = verify_anomaly_zones()
    if overlaps:
        raise SystemExit("anomaly zones overlap:\n  " + "\n  ".join(overlaps))
    print("anomaly check: %d zones, none overlapping" % len(ANOMALY_ZONES))

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

    # The dust material is a separate emitter (URP Particles/Unlit, transparent) and lands in the
    # same folder so CLAUDE.md §14's restore command still covers everything this script rewrites.
    for name, tint in ((DUST_MATERIAL_NAME, DUST_TINT),
                       (RANGE_RING_MATERIAL_NAME, RANGE_RING_TINT)):
        write("%s/%s.mat" % (MATERIAL_DIR, name), particle_material_yaml(name, tint))
        write("%s/%s.mat.meta" % (MATERIAL_DIR, name),
              "fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
              "  externalObjects: {}\n  mainObjectFileID: 2100000\n"
              "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
              % guid_for("Material::" + name))
    print("wrote 2 URP Particles/Unlit materials -> %s/{%s,%s}.mat"
          % (MATERIAL_DIR, DUST_MATERIAL_NAME, RANGE_RING_MATERIAL_NAME))

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

    problems = verify(scene_text, extra_known_guids=[guid_for("VolumeProfile::Scavenge")])
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

    reach_problems, area, nav_grid = verify_reachability(
        solids, placed, PLAYER_SPAWN, (44.0, 27.0))
    if reach_problems:
        for p in reach_problems:
            print("  FAIL " + p)
        raise SystemExit("reachability check failed (%d problems)" % len(reach_problems))
    print("reachability check: bunker + all %d pickups reachable from spawn "
          "(%.0f m2 walkable)" % (len(placed), area))

    # Navigation grid for the Drowned Census-Taker (MTN-B-04/DC). Same field the gate above just
    # used, so the stalker cannot path anywhere the level does not consider walkable. Shipped as
    # .bytes under Resources because a .bin in a Resources folder is not a TextAsset and
    # Resources.Load<TextAsset> returns null for it -- the same trap documented for the GLB props
    # in docs/PROP_PIPELINE.md, which cost a session there and is cheap to avoid here.
    nav_bytes = nav_grid.to_bytes()
    write_bytes("%s/%s" % (NAV_GRID_DIR, NAV_GRID_RESOURCE + ".bytes"), nav_bytes)
    write("%s/%s.bytes.meta" % (NAV_GRID_DIR, NAV_GRID_RESOURCE),
          "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n"
          "  externalObjects: {}\n  userData: \n"
          "  assetBundleName: \n  assetBundleVariant: \n"
          % guid_for("NavGrid::" + NAV_GRID_RESOURCE))
    print("nav grid: %d x %d cells @ %.2f m, %d passable, %d bytes -> %s/%s.bytes"
          % (nav_grid.nx, nav_grid.nz, nav_grid.step, nav_grid.passable_count(),
             len(nav_bytes), NAV_GRID_DIR, NAV_GRID_RESOURCE))

    nav_problems = verify_nav_grid(nav_grid, nav_bytes, PLAYER_SPAWN, (44.0, 27.0),
                                   mutant_spawn_points=MUTANT_SPAWN_POINTS)
    if nav_problems:
        raise SystemExit("nav grid check FAILED:\n  " + "\n  ".join(nav_problems))
    print("nav grid check: header round-trips, spawn and bunker both passable, "
          "bunker reachable by A* over the exported bytes")

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
