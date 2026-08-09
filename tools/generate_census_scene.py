#!/usr/bin/env python3
"""
Generates the second 3D Blowout level (Phase A) — "Flooded Census Office", Census District.

DESIGN_BIBLE §2.2 gives the Census District as the Oblast's administrative organ: records annexes,
interview rooms, filing halls. Where the Grain Depot is industrial and open — long sightlines, big
volumes, loot on shelving you can see across a room — this is the opposite building. Narrow
corridors, sealed rooms, and a lower floor that took the water when the pumps stopped.

That contrast is the point of building it. The depot's 60 seconds are spent choosing a ROUTE across
open ground. The Census Office's 60 seconds are spent choosing whether to go DOWN. The basement
holds the document and medical density, and wading costs 40% of your speed both ways.

Why a generator rather than a hand-saved scene: see CLAUDE.md §14. The coordinate plan below is the
reviewable artifact; the YAML is output. Editing Assets/Scenes/CensusOffice.unity by hand is
overwritten by the next run.

This file is a PLAN. Everything a Blowout level always has — camera stack, player, HUDs, anomaly
volumes, mutant spawner, dust, pickup emitter — comes from scene_rig_lib.add_rig. Every validation
gate comes from scene_verify_lib. Both are shared with the Grain Depot and the Reservoir, so a fix
to a gate reaches all three sites.

Writes, all with deterministic GUIDs so nothing ever dangles:
  Assets/Scenes/CensusOffice.unity                    (+ .meta)
  Assets/Art/Materials/Scavenge/M_Water_Standing.mat  (+ .meta)
  Assets/Settings/CensusVolumeProfile.asset           (+ .meta)
  Assets/Data/Resources/Nav/navgrid_census.bytes      (+ .meta)

Run from the project root:  python tools/generate_census_scene.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from scavenge_scene_lib import (                                    # noqa: E402
    MATERIALS, MATERIAL_DIR, SCRIPT_GUIDS,
    DUST_MATERIAL_NAME, RANGE_RING_MATERIAL_NAME,
    SceneBuilder, guid_for, material_yaml, particle_material_yaml,
    volume_profile_yaml, f, assert_script_guids,
)
from scene_rig_lib import (                                         # noqa: E402
    add_rig, jitter as _jitter, standard_pickup_variation,
)
from scene_verify_lib import (                                      # noqa: E402
    NAV_GRID_DIR, NavGrid,
    _rot_matrix, _apply,
    verify, verify_placement, verify_reachability, verify_nav_grid,
    load_item_categories, verify_pickup_ids, assert_balance_mirror, verify_anomaly_zones,
    write, write_bytes,
)

import visual_archetypes as va                                      # noqa: E402

SCENE_PATH = "Assets/Scenes/CensusOffice.unity"
SCENE_KEY = "Scene::CensusOffice"
VOLUME_PROFILE_KEY = "VolumeProfile::Census"
VOLUME_PROFILE_PATH = "Assets/Settings/CensusVolumeProfile.asset"
NAV_GRID_RESOURCE = "navgrid_census"

# Standing water. A URP Particles/Unlit material rather than a Lit one: it has to be transparent and
# it must never take a shadow, because a shadow cast ONTO water reads as a solid surface and the one
# thing this material has to communicate is that you can walk into it.
WATER_MATERIAL_NAME = "M_Water_Standing"
WATER_TINT = (0.20, 0.30, 0.31, 0.62)

# ─── Level plan ─────────────────────────────────────────────────────────────────────────
#
# Footprint X -46..+46, Z -32..+32 (92 x 64 m), sealed by a 9 m perimeter wall.
# Three bands. Player spawns SOUTH-WEST in the public hall; the records vault door is NORTH-EAST.
#
#                                     NORTH  Z=+32
#   +--------------------------------------+-----------------------------+
#   |  RECORDS ANNEX                       |  VAULT LOBBY  (BUNKER)      |
#   |  four stack rows, X -46..+10         |  blast door, hazard stripes |
#   |  Z +8..+32                           |  X +10..+46                 |
#   +--- wall Z=+8, doorways X -26..-21 / +14..+19 ---------------------+
#   |            FILING STACKS  (Z -8..+8, full width)                   |
#   |            the corridor spine; four stack rows, tight aisles       |
#   +--- wall Z=-8, doorways X -32..-27 / +12..+17 (ramp head) ---------+
#   |  PUBLIC COUNTER HALL                 |  FLOODED BASEMENT (-2.5 m)  |
#   |  counter, queue rail, INTERVIEW ROOM |  X +8..+42, Z -30..-8       |
#   |  (SPAWN)  X -46..+8, Z -32..-8       |  standing water, 0.6x speed |
#   +--------------------------------------+-----------------------------+
#                                     SOUTH  Z=-32
#
# Routes from spawn (-40, -29) to the vault door (38, 26):
#   direct   west doorway -> spine -> east doorway     ~112 m   ~16 s   thin loot
#   stacks   spine aisles end to end                   ~128 m   ~18 s   document density
#   basement ramp down, wade, ramp up, spine           ~150 m   ~27 s   richest, and it is wet
#
# The basement is deliberately a DEAD END with one ramp. Two ways down would make it a shortcut and
# the wade would stop being a decision; with one, every metre of water is paid for twice. That is
# the whole risk/reward shape of this site, and it is why the loot down there is the good loot.
#
# Every doorway gap is at least 4 m, so the 0.7 m CharacterController never snags. The 5 m ramp head
# at X +12..+17 lines up with the ramp gap in the basement's north retaining wall.

BOX_LOCAL = {"Cube": (1, 1, 1), "Cylinder": (1, 2, 1), "Sphere": (1, 1, 1),
             "Capsule": (1, 2, 1), "Quad": (1, 1, 0), "Plane": (10, 0, 10)}

LEGACY_PICKUP_HALF_Y = 0.17
PICKUP_TRIGGER_WORLD_M = 0.646
PICKUP_SCALE_SPREAD = 0.10
PICKUP_TILT_FRACTION = 0.35
PICKUP_TILT_DEGREES = 12.0

ITEM_CATEGORIES = {}

PLAYER_SPAWN = (-40, 0.15, -29)
PLAYER_FACING = (0, 32, 0)
EYE_HEIGHT = 1.62

BUNKER_BEACON_POS = (38, 5.0, 23.0)
BUNKER_TRIGGER_POS = (38, 1.4, 26)
BUNKER_TRIGGER_SIZE = (6, 3.6, 2.6)
BUNKER_DOOR_XZ = (38.0, 26.0)

# Basement geometry, named once because six things depend on staying agreed about it.
BASEMENT_X = (8.0, 42.0)
BASEMENT_Z = (-30.0, -8.0)
BASEMENT_FLOOR_Y = -2.5          # walkable surface
WATER_SURFACE_Y = -2.30          # 20 cm of standing water: shin deep on the 1.8 m controller
RAMP_GAP_X = (12.0, 17.0)

# Mirrors BalanceConstants. assert_balance_mirror() proves each still equals its C# counterpart
# before anything is written — a drifted mirror produces two internally consistent halves that
# disagree with each other, and nothing logs a complaint.
SCAVENGE_TIMER_SECONDS = 60
SCAVENGE_TIMER_WARNING_THRESHOLD = 15
SCAVENGE_TIMER_CRITICAL_THRESHOLD = 5
SCAVENGE_INTERACTION_RANGE = 3
GEIGER_DETECTION_RANGE_M = 14
BACKLOG_TIME_DILATION_FACTOR = 0.02
CARBON_COPY_MAX_DUPLICATES = 4
WATER_SHALLOW_SPEED_FACTOR = 0.6

BALANCE_MIRROR = {
    "SCAVENGE_TIMER_SECONDS": SCAVENGE_TIMER_SECONDS,
    "SCAVENGE_TIMER_WARNING_THRESHOLD": SCAVENGE_TIMER_WARNING_THRESHOLD,
    "SCAVENGE_TIMER_CRITICAL_THRESHOLD": SCAVENGE_TIMER_CRITICAL_THRESHOLD,
    "SCAVENGE_INTERACTION_RANGE": SCAVENGE_INTERACTION_RANGE,
    "GEIGER_DETECTION_RANGE_M": GEIGER_DETECTION_RANGE_M,
    "BACKLOG_TIME_DILATION_FACTOR": BACKLOG_TIME_DILATION_FACTOR,
    "CARBON_COPY_MAX_DUPLICATES": CARBON_COPY_MAX_DUPLICATES,
    "WATER_SHALLOW_SPEED_FACTOR": WATER_SHALLOW_SPEED_FACTOR,
}

# ─── Atmosphere ─────────────────────────────────────────────────────────────────────────
# Denser and closer than the depot's. This is an interior: the motes are paper dust and damp, not
# grain chaff blowing across a yard, so the box is tighter and the drift is slower.
DUST_TINT = (0.74, 0.75, 0.73, 0.34)
DUST_BOX = (22, 7, 22)
DUST_EMISSION_PER_SECOND = 18
DUST_MAX_PARTICLES = 320
DUST_LIFETIME = (11, 16)
DUST_DRIFT = (0.06, 0.20)

RANGE_RING_TINT = (0.35, 0.80, 0.85, 0.10)

# ─── Pickup manifest ────────────────────────────────────────────────────────────────────
# (dataId, kind, quantity, durabilityOverride, contamination, position, yaw, material)
# kind 0 = Item, 1 = Crew. Every id is verified against the live database at generate time.
#
# Weighted to Documents and Medical per the site brief. Contamination values are copied from each
# item's own JSON so a Geiger reading in the bunker matches what the world object was carrying.
#
# NOTE ON CREW: the brief asks for "1 crew (census clerk)". There is no census clerk in the
# database — the roster is exactly crew_marina / crew_sasha / crew_yuri, and inventing a fourth id
# here would produce a pickup that fails the database gate, or worse, silently rescues nobody.
# Marina is used: her authored carry capacity is the lowest of the three (12 kg), which is the most
# interesting body to find at the site whose best loot is furthest from the door.

ITEM = 0
CREW = 1

PICKUPS = [
    # -- Public counter hall / spawn: something in hand inside the first two seconds ---------
    # Counter top is y=1.12, the two cabinets are y=1.80. Manifest Y is authored as if every
    # pickup were the old 0.34 cube, and add_rig drops each archetype by the difference so its
    # BASE lands on the surface named here -- hence surface + 0.17 throughout.
    ("item_classified_registration", ITEM, 3, -1, 0.0,  (-20, 1.29, -24),    16,  "M_Pickup_Document"),
    ("item_emergency_antiseptic",    ITEM, 2, -1, 0.0,  (-3.4, 1.97, -29.4),-22,  "M_Pickup_Medical"),
    ("item_improvised_pry_bar",      ITEM, 1, -1, 0.0,  (-18, 0.22, -26),    58,  "M_Pickup_Tool"),
    ("item_district_audit_sheet",    ITEM, 2, -1, 0.0,  (-6, 1.97, -29.4),  -12,  "M_Pickup_Document"),

    # -- Interview room: the artifact is the reward for finding the room at all --------------
    # Both sit on the desk (top y=0.78). The room is off the spawn band in the opposite direction
    # from the exit, so this is a detour that costs clock in the worst possible currency.
    ("item_artifact_mica",           ITEM, 1, -1, 48.0, (-34, 0.95, -19.5),  24,  "M_Pickup_Artifact"),
    ("item_classified_dossier",      ITEM, 1, -1, 0.0,  (-33.0, 0.95, -19.8), -8, "M_Pickup_Document"),

    # -- Filing stacks spine: document density on rows at X -30 / -15 / +3 / +24 -------------
    # Deck tops are 0.96 / 1.96 / 2.96, so pickup Y is 1.13 / 2.13 / 3.13. An X that is not one of
    # the four row centres is a pickup hanging in an aisle -- the support gate caught five of those
    # on the first run of this plan.
    ("item_classified_audit_sheet",  ITEM, 3, -1, 0.0,  (-30, 2.13, -3),     30,  "M_Pickup_Document"),
    ("item_classified_manifest",     ITEM, 2, -1, 0.0,  (-15, 1.13, 2),     -18,  "M_Pickup_Document"),
    ("item_anti_rad_syringe",        ITEM, 2, -1, 0.0,  (-15, 2.13, -1),     20,  "M_Pickup_Medical"),
    ("item_forged_manifest",         ITEM, 2, -1, 0.0,  (3, 2.13, -2),       44,  "M_Pickup_Document"),
    ("item_classified_transfer_order", ITEM, 3, -1, 0.0, (24, 1.13, 3),     -34,  "M_Pickup_Document"),
    ("item_flashlight",              ITEM, 1, 65, 0.0,  (24, 2.13, 1.5),    -40,  "M_Pickup_Tool"),
    ("crew_yuri",                    CREW, 1, -1, 0.0,  (30, 0.72, -4),      66,  "M_Crew_Coat"),

    # -- Records annex: rows at X -38 / -26 / -12 / 0 ----------------------------------------
    ("item_classified_incident_report", ITEM, 2, -1, 0.0, (-38, 2.13, 14),   -6,  "M_Pickup_Document"),
    ("item_decontamination_ampule",  ITEM, 3, -1, 0.0,  (-26, 1.13, 20),     36,  "M_Pickup_Medical"),
    ("item_industrial_radio",        ITEM, 1, 55, 0.0,  (-12, 1.13, 12),    -26,  "M_Pickup_Tool"),
    ("item_emergency_sedative",      ITEM, 2, -1, 0.0,  (0, 2.13, 24),       12,  "M_Pickup_Medical"),
    ("item_boiled_water_canteen",    ITEM, 2, -1, 0.0,  (0, 1.13, 17),      -30,  "M_Pickup_Water"),

    # -- Vault lobby: what somebody staged by the door and never carried through -------------
    # On the two crates (top y=0.80), not the floor: loot at the exit has to be visible from the
    # doorway or nobody detours the last four metres for it with the siren going.
    ("item_compressed_field_meal",   ITEM, 3, -1, 0.0,  (22, 0.97, 20),      48,  "M_Pickup_Food"),
    ("item_anti_rad_autoinjector",   ITEM, 2, -1, 0.0,  (30, 0.97, 14),     -14,  "M_Pickup_Medical"),

    # -- Flooded basement: the reason to go down. Everything here is paid for twice -----------
    # Floor is -2.50 and the three racks stand at -1.54. Marina is on the floor, in the water.
    ("crew_marina",                  CREW, 1, -1, 0.0,  (13, -1.78, -12),    52,  "M_Crew_Coat"),
    ("item_classified_report",       ITEM, 3, -1, 0.0,  (22, -1.37, -16),    22,  "M_Pickup_Document"),
    ("item_decontamination_drip",    ITEM, 2, -1, 0.0,  (31, -1.37, -17),   -28,  "M_Pickup_Medical"),
    ("item_anomaly_fragment_glass",  ITEM, 1, -1, 32.1, (35, -2.28, -22),    40,  "M_Pickup_Artifact"),
    ("item_key_archive",             ITEM, 1, -1, 0.0,  (24, -2.28, -26),   -18,  "M_Pickup_Document"),
]

# Overhead fixtures: (name, x, y, z, range). Fewer and dimmer than the depot — a records building
# on emergency power, not a lit warehouse.
FIXTURES = [
    ("Fixture_Hall_1",   -34, 5.4, -24, 13), ("Fixture_Hall_2",  -20, 5.4, -14, 13),
    ("Fixture_Hall_3",    -6, 5.4, -24, 13),
    ("Fixture_Interview", -34, 3.9, -21, 8),
    ("Fixture_Spine_1",  -32, 5.6, 0, 12),   ("Fixture_Spine_2",  -14, 5.6, 0, 12),
    ("Fixture_Spine_3",    4, 5.6, 0, 12),   ("Fixture_Spine_4",   22, 5.6, 0, 12),
    ("Fixture_Annex_1",  -34, 5.6, 14, 13),  ("Fixture_Annex_2",  -34, 5.6, 26, 13),
    ("Fixture_Annex_3",   -8, 5.6, 14, 13),  ("Fixture_Annex_4",   -8, 5.6, 26, 13),
    ("Fixture_Vault_1",   22, 5.4, 18, 12),  ("Fixture_Vault_2",   36, 4.6, 26, 10),
    # The only light below floor level. One fixture still burning over standing water is the whole
    # read of the basement: it is not dark down there, it is lit and wrong.
    ("Fixture_Basement", 22, 0.9, -18, 15),
]

# ─── Anomaly zones (bible §5 / BESTIARY.md) ─────────────────────────────────────────────
#
# (name, script_key, class_id, position, trigger_size, extra_yaml_fields)
#
#   Interview — the sealed room off the public hall, which is what this building is FOR. The bible
#   names interview rooms specifically and this is the only site that has one. It is behind a
#   doorway off the spawn band, so it costs a detour in the opposite direction from the exit: the
#   artifact inside is something you go looking for, never something you trip over on the way out.
#
#   Carbon Copy — over the centre stack row in the filing spine, on a cluster of document pickups.
#   The zone only acts when something is grabbed inside it, so a volume over bare floor does
#   nothing. Documents are also the right loot to corrupt here: they are light, so a player takes
#   all of them without thinking about the carry cap, which is exactly the inattention the copies
#   are there to punish.
#
#   Backlog — the EAST doorway of the Z=+8 wall (X +14..+19), the last gate on the fastest line to
#   the vault door. Routing around it through the west doorway at X -26..-21 and back east costs
#   roughly eight seconds of sprint. A player with time in hand pays the eight seconds; a player at
#   fifteen has to look at the haze and decide whether they believe it. Off the critical path it
#   would be free to ignore.
#
# The zones must be disjoint — AnomalyZone.ZoneAt takes the first match. verify_anomaly_zones()
# proves it before the scene is written.
ANOMALY_ZONES = [
    ("Anomaly_Interview_Room", "InterviewAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.InterviewAnomaly",
     (-34.0, 1.6, -21.0), (11.0, 3.2, 9.0),
     "  sitPosition: {fileID: 0}\n"),

    ("Anomaly_CarbonCopy_Stacks", "CarbonCopyAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.CarbonCopyAnomaly",
     (-6.0, 1.6, -1.0), (5.0, 3.2, 6.0),
     "  maxDuplicates: 4\n"),

    ("Anomaly_Backlog_EastDoorway", "BacklogAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.BacklogAnomaly",
     (16.5, 2.0, 8.0), (6.0, 4.0, 5.5),
     "  timeDilationFactor: 0.02\n"),
]

BACKLOG_MOTE_COUNT = 260

# ─── Mutant spawn points ────────────────────────────────────────────────────────────────
#
# ScavengeSiteCatalog gives this site CensusTakerCount = 1 and the base Editor chance, so unlike the
# depot these points are actually used. All are >= 25 m from spawn and none sits on the direct line
# to the vault, so a Census-Taker is something the player becomes aware of rather than something
# that appears on top of them.
#
# The fourth is IN the basement, below floor level. That is deliberate and it is the site's teeth:
# the registrar drowned here, the loot is down there, and the player's speed is 0.6x while its
# is not.
MUTANT_SPAWN_POINTS = [
    (-40.0, 0.15, 20.0),     # records annex, far west
    (-6.0, 0.15, 26.0),      # records annex, aisle between the rows at X -12 and X 0
    (30.0, 0.15, 0.0),       # filing spine, east end
    (25.0, -2.35, -19.0),    # flooded basement, mid-water
]

# ─── Clutter manifest ───────────────────────────────────────────────────────────────────
# (label, anchor_xyz, companion_count, mesh, half_scale, material)
CLUTTER_SPREAD_M = 0.55

CLUTTER_CLUSTERS = [
    ("Hall_Forms",     (-20.4, 1.29, -24.2), 3, "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Spine_Files_A",  (-15.0, 1.13, 2.6),   3, "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Spine_Files_B",  (3.0, 2.13, -2.4),     2, "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Annex_Medical",  (-26.0, 1.13, 20.5),  2, "Cube", (0.30, 0.20, 0.20), "M_Pickup_Medical"),
    ("Vault_Rations",  (22.0, 0.97, 20.4),   3, "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
]

# Floor scatter, on open ground well clear of every pickup so no prop reads as loot missed.
CLUTTER_FLOOR = [
    ("Floor_Folder_1", (-44.2, 0.03, -18.6), "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Floor_Folder_2", (-43.1, 0.03, -19.4), "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Floor_Folder_3", (8.6, 0.03, 28.4),    "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Floor_Crate_1",  (-44.0, 0.30, 4.2),   "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Floor_Crate_2",  (43.4, 0.30, 12.0),   "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Floor_Tin_1",    (-2.4, 0.13, -30.6),  "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    # Two below the waterline: what floated and settled. Placed on the basement floor, not the
    # surface, so they read as submerged rather than as flotsam sitting on top of the water.
    ("Floor_Sub_Folder", (17.2, -2.47, -24.4), "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    ("Floor_Sub_Tin",    (31.6, -2.37, -18.2), "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
]


def pickup_variation(name, base_scale, is_crew):
    """This site's placement variety. Thin wrapper: the maths is shared so all three sites agree."""
    return standard_pickup_variation(name, base_scale, is_crew,
                                     PICKUP_SCALE_SPREAD, PICKUP_TILT_FRACTION, PICKUP_TILT_DEGREES)


def build_clutter():
    """Expands the clutter manifest. Pure and deterministic — add_rig calls it to emit and to count."""
    props = []

    for label, anchor, count, mesh, scale, mat in CLUTTER_CLUSTERS:
        for i in range(count):
            name = "%s_%d" % (label, i + 1)
            dx = _jitter(name, 0, -CLUTTER_SPREAD_M, CLUTTER_SPREAD_M)
            dz = _jitter(name, 1, -CLUTTER_SPREAD_M, CLUTTER_SPREAD_M)
            yaw = _jitter(name, 2, 0.0, 360.0)
            tilt = _jitter(name, 3, -6.0, 6.0)
            varied = tuple(scale[k] * (1.0 + _jitter(name, 4 + k, -0.12, 0.12)) for k in range(3))
            props.append((name, (anchor[0] + dx, anchor[1], anchor[2] + dz),
                          (tilt, yaw, 0.0), mesh, varied, mat))

    for name, pos, mesh, scale, mat in CLUTTER_FLOOR:
        yaw = _jitter(name, 0, 0.0, 360.0)
        tilt_x = _jitter(name, 1, -14.0, 14.0)
        tilt_z = _jitter(name, 2, -14.0, 14.0)
        varied = tuple(scale[k] * (1.0 + _jitter(name, 3 + k, -0.12, 0.12)) for k in range(3))
        props.append((name, pos, (tilt_x, yaw, tilt_z), mesh, varied, mat))

    return props


def build():
    sb = SceneBuilder()

    world = {None: ((0, 0, 0), _rot_matrix(0, 0, 0), (1, 1, 1))}
    solids = []
    placed = []

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
                half = tuple(abs(base[i] * wscale[i]) * 0.5 for i in range(3))
                ext = tuple(sum(abs(wrot[r][c]) * half[c] for c in range(3)) for r in range(3))
                solids.append((name, wpos, wrot, half, ext, mesh))
        return go, t

    def water_volume(name, parent, pos, size, speed_factor):
        """
        An invisible trigger carrying WaterVolume. No renderer: the visible water is a separate
        unlit plane at the surface height, because a trigger box that also renders would be a solid
        cube of water the player walks inside of.

        Registered in `world` but NOT in `solids` — the walkability flood-fill has to treat water as
        open floor. Wading is a speed cost, not an obstacle, and a water box in the collision list
        would seal the basement and the level would verify clean and be unplayable.
        """
        go, t = sb.obj(name, parent=parent, pos=pos)
        world[t] = _compose(parent, pos, (0, 0, 0), (1, 1, 1))
        sb.box_collider(go, is_trigger=True, size=size)
        sb.mono(go, "WaterVolume", "Assembly-CSharp::OblastZero.Gameplay.WaterVolume",
                "  speedFactor: %s\n  logEntry: 0\n" % f(speed_factor))
        return go, t

    def stack_row(name, parent, x, z0, z1):
        """Records shelving: three decks on paired uprights. Same construction as the depot's."""
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

    # Ground, split so the basement is a real hole rather than a floor with a texture on it.
    ground = group("Ground", env)
    for n, p, s in [
        ("Ground_West",      (-19, -0.5, 0),    (54, 1, 64)),   # X -46..+8
        ("Ground_EastNorth", (27, -0.5, 12),    (38, 1, 40)),   # X +8..+46, Z -8..+32
        ("Ground_EastRim",   (44, -0.5, -19),   (4, 1, 22)),    # X +42..+46, Z -30..-8
        ("Ground_EastSill",  (25, -0.5, -31),   (34, 1, 2)),    # X +8..+42, Z -32..-30
    ]:
        solid(n, ground, p, s, "M_Concrete_Floor")

    peri = group("Perimeter", env)
    for n, p, s in [
        ("Wall_North", (0, 4.5, 32.5),  (94, 9, 1)),
        ("Wall_South", (0, 4.5, -32.5), (94, 9, 1)),
        ("Wall_West",  (-46.5, 4.5, 0), (1, 9, 66)),
        ("Wall_East",  (46.5, 4.5, 0),  (1, 9, 66)),
    ]:
        solid(n, peri, p, s, "M_Concrete_Stained")

    div = group("Dividers", env)
    for n, p, s in [
        # Z=+8 band wall. Doorways X -26..-21 and X +14..+19 (the Backlog sits in the east one).
        ("Div_North_A", (-36, 3.5, 8),   (20, 7, 0.6)),
        ("Div_North_B", (-3.5, 3.5, 8),  (35, 7, 0.6)),
        ("Div_North_C", (32.5, 3.5, 8),  (27, 7, 0.6)),
        # Z=-8 band wall. Doorways X -32..-27 and X +12..+17 (the ramp head).
        ("Div_South_A", (-39, 3.5, -8),  (14, 7, 0.6)),
        ("Div_South_B", (-7.5, 3.5, -8), (39, 7, 0.6)),
        ("Div_South_C", (31.5, 3.5, -8), (29, 7, 0.6)),
        # Annex / vault lobby divider at X=+10. Doorway Z +16..+21.
        ("Div_Vault_A", (10, 3.5, 12),   (0.6, 7, 8)),
        ("Div_Vault_B", (10, 3.5, 26.5), (0.6, 7, 11)),
    ]:
        solid(n, div, p, s, "M_Paint_Institution")

    roofs = group("Roofs", env)
    for n, p, s in [("Roof_Annex",  (-18, 7.2, 20),   (56, 0.4, 24)),
                    ("Roof_Spine",  (0, 7.2, 0),      (92, 0.4, 16)),
                    ("Roof_Hall",   (-19, 7.2, -20),  (54, 0.4, 24)),
                    ("Roof_Vault",  (28, 7.2, 20),    (36, 0.4, 24))]:
        solid(n, roofs, p, s, "M_Concrete_Stained")

    # ── Interview room ──────────────────────────────────────────────────────────────────
    # Sealed on three sides with one 5 m doorway on its north face. The anomaly's "larger inside
    # than outside" is InterviewAnomaly's own effect at runtime; the built room is honest, which is
    # what makes the effect land.
    intr = group("InterviewRoom", env)
    for n, p, s in [
        ("Int_Wall_West",    (-40.25, 3.5, -21),   (0.5, 7, 10)),
        ("Int_Wall_East",    (-27.75, 3.5, -21),   (0.5, 7, 10)),
        ("Int_Wall_South",   (-34, 3.5, -26.25),   (12.5, 7, 0.5)),
        ("Int_Wall_North_A", (-38.25, 3.5, -15.75), (3.5, 7, 0.5)),
        ("Int_Wall_North_B", (-29.75, 3.5, -15.75), (3.5, 7, 0.5)),
    ]:
        solid(n, intr, p, s, "M_Paint_Institution")
    solid("Int_Desk", intr, (-34, 0.72, -19.5), (2.4, 0.12, 1.1), "M_Steel_Galvanised")
    for i, dx in enumerate((-0.9, 0.9)):
        for j, dz in enumerate((-0.42, 0.42)):
            solid("Int_Desk_Leg_%d%d" % (i, j), intr,
                  (-34 + dx, 0.33, -19.5 + dz), (0.1, 0.66, 0.1),
                  "M_Steel_Galvanised", shadows=False)
    solid("Int_Chair_Seat", intr, (-34, 0.46, -21.6), (0.55, 0.1, 0.55), "M_Steel_Rusted")
    solid("Int_Chair_Back", intr, (-34, 0.92, -21.9), (0.55, 0.82, 0.1), "M_Steel_Rusted")

    # ── Public counter hall ─────────────────────────────────────────────────────────────
    hall = group("CounterHall", env)
    solid("Counter_Top", hall, (-14, 1.05, -24), (18, 0.14, 0.9), "M_Steel_Galvanised")
    solid("Counter_Base", hall, (-14, 0.5, -24), (18, 1.0, 0.7), "M_Paint_Institution")
    for i in range(6):
        solid("Queue_Post_%d" % (i + 1), hall, (-24 + i * 4, 0.5, -29), (0.09, 1.0, 0.09),
              "M_Steel_Galvanised", shadows=False)
    solid("Hall_Cabinet_1", hall, (-6, 0.9, -29.4), (1.0, 1.8, 0.6), "M_Steel_Rusted")
    solid("Hall_Cabinet_2", hall, (-3.4, 0.9, -29.4), (1.0, 1.8, 0.6), "M_Steel_Rusted")

    # ── Filing stacks spine ─────────────────────────────────────────────────────────────
    # Four rows running E-W across the corridor. Aisles are 4.5 m, which is tight next to the
    # depot's warehouse but still twice the controller's diameter.
    spine = group("FilingStacks", env)
    for i, x in enumerate((-30, -15, 3, 24)):
        stack_row("Stack_Spine_%d" % (i + 1), spine, x, -4.5, 4.5)

    annex = group("RecordsAnnex", env)
    for i, x in enumerate((-38, -26, -12, 0)):
        stack_row("Stack_Annex_%d" % (i + 1), annex, x, 11, 27)

    # ── Vault lobby (the way out) ───────────────────────────────────────────────────────
    vault = group("VaultLobby", env)
    solid("Vault_Headhouse_West", vault, (34.5, 2.5, 26), (1, 5, 8), "M_Concrete_Stained")
    solid("Vault_Headhouse_East", vault, (41.5, 2.5, 26), (1, 5, 8), "M_Concrete_Stained")
    solid("Vault_Headhouse_Back", vault, (38, 2.5, 29.5), (8, 5, 1), "M_Concrete_Stained")
    solid("Vault_Headhouse_Roof", vault, (38, 5.2, 26), (8, 0.4, 8), "M_Concrete_Stained")
    solid("Vault_Crate_1", vault, (22, 0.4, 20), (1.2, 0.8, 1.2), "M_Timber_Crate")
    solid("Vault_Crate_2", vault, (30, 0.4, 14), (1.2, 0.8, 1.2), "M_Timber_Crate")
    solid("Vault_Sign", vault, (38, 3.6, 22.4), (3.2, 0.9, 0.12), "M_Sign_Bunker", shadows=False)
    stripes = group("HazardStripes", vault)
    for i in range(10):
        solid("Stripe_%02d" % (i + 1), stripes, (38, 0.03, 16 + i), (5, 0.06, 0.8),
              "M_Hazard_Yellow" if i % 2 == 0 else "M_Hazard_Black",
              collide=False, shadows=False)

    # ── Flooded basement ────────────────────────────────────────────────────────────────
    base = group("FloodedBasement", env)
    solid("Base_Floor", base, (25, -2.75, -19), (34, 0.5, 22), "M_Concrete_Floor")
    for n, p, s in [
        ("Base_Wall_West",    (7.75, -1.25, -19),   (0.5, 2.5, 22)),
        ("Base_Wall_East",    (42.25, -1.25, -19),  (0.5, 2.5, 22)),
        ("Base_Wall_South",   (25, -1.25, -30.25),  (34, 2.5, 0.5)),
        # North retaining wall, split around the ramp gap at X +12..+17.
        ("Base_Wall_North_A", (9.9, -1.25, -7.75),  (4.25, 2.5, 0.5)),
        ("Base_Wall_North_B", (29.6, -1.25, -7.75), (25.25, 2.5, 0.5)),
    ]:
        solid(n, base, p, s, "M_Void_Dark")

    # 2.5 m drop over 7 m of run: 19.65 degrees, well inside the 45 degree slope limit. Same
    # arithmetic as the depot's pit ramp, which the escapability check has already proven walkable
    # in both directions.
    solid("Base_Ramp", base, (14.5, -1.35, -11.5), (5, 0.4, 7.43),
          "M_Concrete_Floor", rot=(-19.65, 0, 0))

    # Shelving that stayed standing in the water. Decks only — no posts below the surface, because
    # a forest of 0.14 m uprights in a room the player wades through is a snag hazard the flood-fill
    # cannot see (it models the controller's radius, not its patience).
    for i, x in enumerate((13, 22, 31)):
        solid("Base_Rack_%d" % (i + 1), base, (x, -1.6, -20), (1.6, 0.12, 12),
              "M_Steel_Rusted", shadows=False)

    solid("Base_Cabinet_1", base, (37, -1.6, -12), (1.0, 1.8, 0.6), "M_Steel_Rusted")
    solid("Base_Cabinet_2", base, (18, -1.6, -28), (1.0, 1.8, 0.6), "M_Steel_Rusted")

    # The visible water: an unlit transparent plane at the surface height, no collider. Plane's
    # local mesh is 10x10, so scale 3.4 x 2.2 covers the 34 x 22 basement exactly.
    water = group("StandingWater", env)
    solid("Water_Surface", water, (25, WATER_SURFACE_Y, -19), (3.4, 1, 2.2),
          WATER_MATERIAL_NAME, mesh="Plane", collide=False, shadows=False)

    # The wade itself. Spans the full basement footprint from the floor up to 1.5 m, so the
    # controller's capsule (centre 0.9 above the floor) is inside it anywhere in the room.
    water_volume("Water_Basement", water, (25, -1.75, -19), (34, 1.5, 22),
                 WATER_SHALLOW_SPEED_FACTOR)

    # ══ SHARED RIG ══════════════════════════════════════════════════════════════════════
    sun_light_id = add_rig(sb, group, solid, placed, sys.modules[__name__])

    return sb, sun_light_id, solids, placed


def main():
    if not os.path.isdir("Assets/Scenes"):
        raise SystemExit("run this from the project root (Assets/Scenes not found)")

    check = "--check" in sys.argv

    missing, n_items, n_crew = verify_pickup_ids(PICKUPS, CREW)
    if missing:
        raise SystemExit("pickup ids not in the database: " + ", ".join(missing))
    print("database check: %d item ids, %d crew ids, all %d pickup ids resolve"
          % (n_items, n_crew, len(PICKUPS)))

    print("archetype check: " + va.assert_matches_csharp())
    print(assert_script_guids())
    print(assert_balance_mirror(BALANCE_MIRROR))

    overlaps = verify_anomaly_zones(ANOMALY_ZONES)
    if overlaps:
        raise SystemExit("anomaly zones overlap:\n  " + "\n  ".join(overlaps))
    print("anomaly check: %d zones, none overlapping" % len(ANOMALY_ZONES))

    global ITEM_CATEGORIES
    ITEM_CATEGORIES = load_item_categories()
    uncategorised = [d for d, k, *_ in PICKUPS if k == ITEM and not ITEM_CATEGORIES.get(d)]
    if uncategorised:
        raise SystemExit("pickup items with no category: " + ", ".join(uncategorised))
    print("category check: %d items categorised" % len(ITEM_CATEGORIES))

    # Assets. The Lit palette is shared with the depot — same institution, same concrete — so only
    # the water material and this site's volume profile are new.
    water_mat = particle_material_yaml(WATER_MATERIAL_NAME, WATER_TINT)
    profile = volume_profile_yaml()
    scene_meta = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
                  "  externalObjects: {}\n  mainObjectFileID: %d\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    if not check:
        write("%s/%s.mat" % (MATERIAL_DIR, WATER_MATERIAL_NAME), water_mat)
        write("%s/%s.mat.meta" % (MATERIAL_DIR, WATER_MATERIAL_NAME),
              scene_meta % (guid_for("Material::" + WATER_MATERIAL_NAME), 2100000))
        write(VOLUME_PROFILE_PATH, profile)
        write(VOLUME_PROFILE_PATH + ".meta",
              scene_meta % (guid_for(VOLUME_PROFILE_KEY), 11400000))
        print("wrote %s/%s.mat and %s" % (MATERIAL_DIR, WATER_MATERIAL_NAME, VOLUME_PROFILE_PATH))

    sb, sun_id, solids, placed = build()
    scene_text = sb.emit(sun_id)

    known = [guid_for(VOLUME_PROFILE_KEY), guid_for("Material::" + WATER_MATERIAL_NAME)]
    problems = verify(scene_text, extra_known_guids=known)
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
        solids, placed, PLAYER_SPAWN, BUNKER_DOOR_XZ)
    if reach_problems:
        for p in reach_problems:
            print("  FAIL " + p)
        raise SystemExit("reachability check failed (%d problems)" % len(reach_problems))
    print("reachability check: vault door + all %d pickups reachable AND escapable from spawn "
          "(%.0f m2 walkable)" % (len(placed), area))

    nav_bytes = nav_grid.to_bytes()
    nav_problems = verify_nav_grid(nav_grid, nav_bytes, PLAYER_SPAWN, BUNKER_DOOR_XZ,
                                   mutant_spawn_points=MUTANT_SPAWN_POINTS)
    if nav_problems:
        raise SystemExit("nav grid check FAILED:\n  " + "\n  ".join(nav_problems))
    print("nav grid check: header round-trips, spawn and vault both passable, "
          "vault reachable by A* over the exported bytes, all %d mutant points on live cells"
          % len(MUTANT_SPAWN_POINTS))

    scene_meta_default = ("fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n"
                          "  externalObjects: {}\n  userData: \n"
                          "  assetBundleName: \n  assetBundleVariant: \n")

    if check:
        # Drift gate: compare against what is on disk without touching it.
        stale = []
        for path, produced in ((SCENE_PATH, scene_text.encode("utf-8")),
                               ("%s/%s.bytes" % (NAV_GRID_DIR, NAV_GRID_RESOURCE), nav_bytes)):
            if not os.path.exists(path):
                stale.append("%s does not exist" % path)
            elif open(path, "rb").read() != produced:
                stale.append("%s differs from the plan that produces it" % path)
        if stale:
            print("\nCHECK FAILED:\n  " + "\n  ".join(stale))
            return 1
        print("\nCHECK PASSED: CensusOffice.unity and navgrid_census.bytes match the plan.")
        return 0

    write_bytes("%s/%s.bytes" % (NAV_GRID_DIR, NAV_GRID_RESOURCE), nav_bytes)
    write("%s/%s.bytes.meta" % (NAV_GRID_DIR, NAV_GRID_RESOURCE),
          "fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n"
          "  externalObjects: {}\n  userData: \n"
          "  assetBundleName: \n  assetBundleVariant: \n"
          % guid_for("NavGrid::" + NAV_GRID_RESOURCE))
    print("nav grid: %d x %d cells @ %.2f m, %d passable, %d bytes -> %s/%s.bytes"
          % (nav_grid.nx, nav_grid.nz, nav_grid.step, nav_grid.passable_count(),
             len(nav_bytes), NAV_GRID_DIR, NAV_GRID_RESOURCE))

    write(SCENE_PATH, scene_text)
    write(SCENE_PATH + ".meta", scene_meta_default % guid_for(SCENE_KEY))

    n_go = scene_text.count("\nGameObject:\n")
    print("wrote %s — %d GameObjects, %d lines, all references resolve"
          % (SCENE_PATH, n_go, scene_text.count("\n")))
    print("scene guid: %s" % guid_for(SCENE_KEY))
    return 0


if __name__ == "__main__":
    sys.exit(main())
