#!/usr/bin/env python3
"""
Generates the third 3D Blowout level (Phase A) — "Abandoned Reservoir", Reservoir district.

DESIGN_BIBLE §2.2 gives the Reservoir as the municipal water plant: a concrete basin, a pump house
that fed it, service tunnels underneath, and a control room on the high side. BESTIARY.md puts the
Drowned Census-Takers here — this is where they drowned — and ScavengeSiteCatalog gives the site
CensusTakerCount = 2 and a 1.33x Editor chance. Highest threat in the catalogue.

WHAT MAKES THIS SITE DIFFERENT FROM THE OTHER TWO
The Grain Depot asks which ROUTE. The Census Office asks whether to go DOWN. The Reservoir asks
both at once, and it charges for the answer:

  the catwalk    dry, fast, fully exposed over open water, no loot on it
  the tunnels    a real shortcut, shallow water, and the Backlog is inside it
  the basin      the loot, in 1.4 m of standing water at half speed

Three crossings between the south bank and the north bank, and each one is bad in a different way.
That is the whole level.

Why a generator rather than a hand-saved scene: see CLAUDE.md §14. The coordinate plan below is the
reviewable artifact; the YAML is output. Editing Assets/Scenes/Reservoir.unity by hand is
overwritten by the next run.

This file is a PLAN. The rig comes from scene_rig_lib, the gates from scene_verify_lib, both shared
with the other two sites.

Writes, all with deterministic GUIDs so nothing ever dangles:
  Assets/Scenes/Reservoir.unity                          (+ .meta)
  Assets/Settings/ReservoirVolumeProfile.asset           (+ .meta)
  Assets/Data/Resources/Nav/navgrid_reservoir.bytes      (+ .meta)

Run from the project root:  python tools/generate_reservoir_scene.py
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from scavenge_scene_lib import (                                    # noqa: E402
    MATERIAL_DIR, SceneBuilder, guid_for, particle_material_yaml,
    volume_profile_yaml, f, assert_script_guids,
)
from scene_rig_lib import (                                         # noqa: E402
    add_rig, jitter as _jitter, standard_pickup_variation,
)
from scene_verify_lib import (                                      # noqa: E402
    NAV_GRID_DIR,
    _rot_matrix, _apply,
    verify, verify_placement, verify_reachability, verify_nav_grid,
    load_item_categories, verify_pickup_ids, assert_balance_mirror, verify_anomaly_zones,
    write, write_bytes,
)

import visual_archetypes as va                                      # noqa: E402

SCENE_PATH = "Assets/Scenes/Reservoir.unity"
SCENE_KEY = "Scene::Reservoir"
VOLUME_PROFILE_KEY = "VolumeProfile::Reservoir"
VOLUME_PROFILE_PATH = "Assets/Settings/ReservoirVolumeProfile.asset"
NAV_GRID_RESOURCE = "navgrid_reservoir"

# Shared with the Census Office — same material asset, written by whichever generator runs. Deeper
# and colder here would need a second material; it does not earn one, and two near-identical water
# materials is how a later tweak gets applied to one site and not the other.
WATER_MATERIAL_NAME = "M_Water_Standing"
WATER_TINT = (0.20, 0.30, 0.31, 0.62)

# ─── Level plan ─────────────────────────────────────────────────────────────────────────
#
# Footprint X -50..+50, Z -34..+34 (100 x 68 m), sealed by a 9 m perimeter wall.
# Player spawns SOUTH-WEST on the south bank; the control room door is NORTH-EAST.
#
#                                     NORTH  Z=+34
#   +--------------------------------------------------+------------------+
#   |                  NORTH BANK  Z +14..+34           | CONTROL ROOM     |
#   |                                                   | (BUNKER) X30..46 |
#   +----------+------------------------------+---------+------+----------+
#   |  PUMP    |         THE BASIN            | inner   |TUNNEL| outer    |
#   |  HOUSE   |   X -28..+28, Z -14..+14     | verge   |X34-42| verge    |
#   |  X-48    |   floor -3.0, water -1.6     |         |floor |          |
#   |  ..-32   |   CATWALK at y=1.2 down X=0  |         |-1.2  |          |
#   +----------+------------------------------+---------+------+----------+
#   |                  SOUTH BANK  Z -34..-14           (SPAWN, SW)       |
#   +---------------------------------------------------------------------+
#                                     SOUTH  Z=-34
#
# Crossings from the south bank to the north bank:
#   catwalk   stairs at X 0 -> deck at y=1.2 -> stairs down    ~28 m   dry, exposed, no loot
#   tunnels   ramp at X 38 -> shallow water 0.6x -> ramp up    ~28 m   shortest, Backlog inside
#   basin     ramp at X -16 -> 1.4 m water at 0.5x -> ramp up  ~34 m   all the good loot
#
# The two Drowned Census-Takers spawn in the basin and the tunnel — in the water, where the player
# is at half speed and they are not. The catwalk is the only crossing they cannot reach, which is
# what makes "no loot on the catwalk" a deliberate cost rather than an oversight.
#
# Every ramp is 19.65 degrees (the depot's proven pit-ramp gradient, well inside the 45 degree
# slope limit). Catwalk stairs rise 0.24 per tread against a 0.32 step offset.

BOX_LOCAL = {"Cube": (1, 1, 1), "Cylinder": (1, 2, 1), "Sphere": (1, 1, 1),
             "Capsule": (1, 2, 1), "Quad": (1, 1, 0), "Plane": (10, 0, 10)}

LEGACY_PICKUP_HALF_Y = 0.17
PICKUP_TRIGGER_WORLD_M = 0.646
PICKUP_SCALE_SPREAD = 0.10
PICKUP_TILT_FRACTION = 0.35
PICKUP_TILT_DEGREES = 12.0

ITEM_CATEGORIES = {}

PLAYER_SPAWN = (-40, 0.15, -28)
PLAYER_FACING = (0, 40, 0)
EYE_HEIGHT = 1.62

BUNKER_BEACON_POS = (38, 5.0, 23.0)
BUNKER_TRIGGER_POS = (38, 1.4, 26)
BUNKER_TRIGGER_SIZE = (6, 3.6, 2.6)
BUNKER_DOOR_XZ = (38.0, 26.0)

BASIN_FLOOR_Y = -3.0
BASIN_WATER_Y = -1.62
TUNNEL_FLOOR_Y = -1.2
TUNNEL_WATER_Y = -0.92
CATWALK_TOP_Y = 1.2

SCAVENGE_TIMER_SECONDS = 60
SCAVENGE_TIMER_WARNING_THRESHOLD = 15
SCAVENGE_TIMER_CRITICAL_THRESHOLD = 5
SCAVENGE_INTERACTION_RANGE = 3
GEIGER_DETECTION_RANGE_M = 14
BACKLOG_TIME_DILATION_FACTOR = 0.02
CARBON_COPY_MAX_DUPLICATES = 4
WATER_SHALLOW_SPEED_FACTOR = 0.6
WATER_DEEP_SPEED_FACTOR = 0.5

BALANCE_MIRROR = {
    "SCAVENGE_TIMER_SECONDS": SCAVENGE_TIMER_SECONDS,
    "SCAVENGE_TIMER_WARNING_THRESHOLD": SCAVENGE_TIMER_WARNING_THRESHOLD,
    "SCAVENGE_TIMER_CRITICAL_THRESHOLD": SCAVENGE_TIMER_CRITICAL_THRESHOLD,
    "SCAVENGE_INTERACTION_RANGE": SCAVENGE_INTERACTION_RANGE,
    "GEIGER_DETECTION_RANGE_M": GEIGER_DETECTION_RANGE_M,
    "BACKLOG_TIME_DILATION_FACTOR": BACKLOG_TIME_DILATION_FACTOR,
    "CARBON_COPY_MAX_DUPLICATES": CARBON_COPY_MAX_DUPLICATES,
    "WATER_SHALLOW_SPEED_FACTOR": WATER_SHALLOW_SPEED_FACTOR,
    "WATER_DEEP_SPEED_FACTOR": WATER_DEEP_SPEED_FACTOR,
}

# ─── Atmosphere ─────────────────────────────────────────────────────────────────────────
# Mist off standing water, not dust. Bigger box than the interior sites and a slower drift: this is
# a 100 m open basin and the haze has to read at distance for the catwalk to feel exposed.
DUST_TINT = (0.72, 0.76, 0.78, 0.30)
DUST_BOX = (34, 9, 34)
DUST_EMISSION_PER_SECOND = 16
DUST_MAX_PARTICLES = 340
DUST_LIFETIME = (13, 19)
DUST_DRIFT = (0.05, 0.16)

RANGE_RING_TINT = (0.35, 0.80, 0.85, 0.10)

# ─── Pickup manifest ────────────────────────────────────────────────────────────────────
# (dataId, kind, quantity, durabilityOverride, contamination, position, yaw, material)
#
# Weighted wet per the site brief: contaminated water, submerged weapons, waterlogged documents.
# Contamination values are copied from each item's own JSON so a Geiger reading in the bunker
# matches what the world object was carrying.
#
# NOTE ON CREW: the brief asks for "1 crew (drowned clerk survivor)". The roster is exactly
# marina / sasha / yuri — there is no drowned clerk, and inventing an id would fail the database
# gate or silently rescue nobody. Sasha is used: 19 kg carry, the highest of the three, which is
# the body worth the swim.
#
# NOTHING IS ON THE CATWALK. That is the design: the dry crossing has to cost something, and what
# it costs is every pickup you did not stop for.

ITEM = 0
CREW = 1

PICKUPS = [
    # -- South bank / spawn: something in hand inside the first two seconds ------------------
    ("item_emergency_ration",        ITEM, 3, -1, 0.0,  (-30, 0.97, -26),    18,  "M_Pickup_Food"),
    ("item_crowbar",                 ITEM, 1, -1, 0.0,  (-22, 0.22, -22),    64,  "M_Pickup_Tool"),
    ("item_gray_water_canteen",      ITEM, 2, -1, 45.2, (-10, 0.97, -28),   -24,  "M_Pickup_Water"),
    ("item_issued_bandage",          ITEM, 3, -1, 0.0,  (6, 0.97, -24),      32,  "M_Pickup_Medical"),

    # -- Pump house: the machinery deck, dry and off every crossing --------------------------
    ("item_improvised_wrench",       ITEM, 1, -1, 0.0,  (-42, 1.37, -6),     46,  "M_Pickup_Tool"),
    ("item_industrial_radio",        ITEM, 1, 50, 0.0,  (-42, 1.37, 6),     -16,  "M_Pickup_Tool"),
    ("item_classified_manifest",     ITEM, 2, -1, 0.0,  (-36, 1.07, -2),     28,  "M_Pickup_Document"),
    ("item_anti_rad_syringe",        ITEM, 2, -1, 0.0,  (-36, 1.07, 4),     -34,  "M_Pickup_Medical"),

    # -- THE BASIN: 1.4 m of water at half speed. The reason to get wet ----------------------
    # Floor is -3.00, pallets stand at -2.70, the crate at -2.20.
    ("crew_sasha",                   CREW, 1, -1, 0.0,  (-8, -2.28, -4),     58,  "M_Crew_Coat"),
    ("item_762mm_semi_auto",         ITEM, 1, 40, 0.0,  (-14, -2.53, -6),    22,  "M_Pickup_Weapon"),
    ("item_762mm_ammunition",        ITEM, 4, -1, 0.0,  (-14, -2.53, -5),   -30,  "M_Pickup_Ammo"),
    ("item_artifact_droplet",        ITEM, 1, -1, 25.2, (12, -2.53, 6),      40,  "M_Pickup_Artifact"),
    ("item_brown_water_flask",       ITEM, 2, -1, 48.7, (18, -2.03, -8),    -12,  "M_Pickup_Water"),
    ("item_classified_report",       ITEM, 2, -1, 0.0,  (12, -2.53, 7),      14,  "M_Pickup_Document"),
    ("item_field_suture_kit",        ITEM, 2, -1, 0.0,  (-8, -2.83, 9),     -26,  "M_Pickup_Medical"),
    ("item_anomaly_fragment_quartz", ITEM, 1, -1, 20.1, (22, -2.83, 10),     36,  "M_Pickup_Artifact"),

    # -- Flooded service tunnel: the shortcut, and the Backlog is in it ----------------------
    # Floor is -1.20, the wall rack stands at -0.34.
    ("item_contaminated_water_thermos", ITEM, 2, -1, 27.8, (38, -0.17, -4), -20,  "M_Pickup_Water"),
    ("item_decontamination_vial",    ITEM, 3, -1, 0.0,  (38, -0.17, -2),     26,  "M_Pickup_Medical"),
    ("item_baton",                   ITEM, 1, -1, 0.0,  (38, -1.03, 8),      52,  "M_Pickup_Weapon"),

    # -- North bank: what got carried this far and no further --------------------------------
    ("item_classified_audit_sheet",  ITEM, 2, -1, 0.0,  (-20, 0.97, 22),    -18,  "M_Pickup_Document"),
    ("item_boiled_water_jerrycan",   ITEM, 1, -1, 0.0,  (-2, 0.97, 26),      30,  "M_Pickup_Water"),
    ("item_compressed_meal_pack",    ITEM, 2, -1, 0.0,  (14, 0.97, 20),     -38,  "M_Pickup_Food"),

    # -- Control room: staged at the door and never carried through --------------------------
    ("item_district_audit_sheet",    ITEM, 2, -1, 0.0,  (37, 1.07, 24),      12,  "M_Pickup_Document"),
    ("item_anti_rad_autoinjector",   ITEM, 2, -1, 0.0,  (44, 1.97, 28),     -22,  "M_Pickup_Medical"),
    ("item_key_archive",             ITEM, 1, -1, 0.0,  (39.5, 1.07, 24),    44,  "M_Pickup_Document"),
]

# Fixtures. Sparse and cold: an outdoor plant on failing emergency power. The two over the water are
# the level's only real light source at basin level, which is what makes the catwalk read as exposed.
FIXTURES = [
    ("Fixture_SouthBank_1", -30, 5.4, -24, 14), ("Fixture_SouthBank_2", 4, 5.4, -26, 14),
    ("Fixture_Pump_1",      -42, 4.6, -6, 12),  ("Fixture_Pump_2",      -38, 4.6, 6, 12),
    ("Fixture_Basin_West",  -14, 3.2, 0, 18),   ("Fixture_Basin_East",   16, 3.2, 0, 18),
    ("Fixture_Catwalk_S",     0, 3.4, -10, 10), ("Fixture_Catwalk_N",     0, 3.4, 10, 10),
    ("Fixture_Tunnel_S",     38, 1.4, -8, 11),  ("Fixture_Tunnel_N",     38, 1.4, 8, 11),
    ("Fixture_NorthBank_1", -20, 5.4, 22, 14),  ("Fixture_NorthBank_2",  10, 5.4, 24, 14),
    ("Fixture_Control",      38, 4.6, 26, 10),
]

# ─── Anomaly zones ──────────────────────────────────────────────────────────────────────
#
#   Backlog — inside the service tunnel, per the site brief. This is the strongest Backlog
#   placement in the game because the tunnel is the SHORTEST crossing: the anomaly is the price of
#   the shortcut, and it stacks multiplicatively with the tunnel's own wade (0.6 x 0.02 = 0.012).
#   That composition is exactly why ScavengePlayerController splits terrain and anomaly speed into
#   separate factors — a single shared multiplier would have let the Backlog's exit reset erase the
#   wade for the rest of the run.
#
#   Carbon Copy — the basin floor, over the pallet cluster at the west end. It only acts when
#   something is grabbed inside it, so it has to sit on loot; and the basin is where a player is
#   already over-committed, wet and slow, which is the worst moment to be handed four of something.
#
#   Interview — the pump house office. Off every crossing, in the one dry interior on the map.
#
# Zones must be disjoint; verify_anomaly_zones() proves it before the scene is written.
ANOMALY_ZONES = [
    ("Anomaly_Backlog_ServiceTunnel", "BacklogAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.BacklogAnomaly",
     (38.0, 0.0, 0.0), (7.0, 4.0, 7.0),
     "  timeDilationFactor: 0.02\n"),

    ("Anomaly_CarbonCopy_BasinFloor", "CarbonCopyAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.CarbonCopyAnomaly",
     (-13.0, -2.2, -5.0), (6.0, 3.0, 6.0),
     "  maxDuplicates: 4\n"),

    ("Anomaly_Interview_PumpOffice", "InterviewAnomaly",
     "Assembly-CSharp::OblastZero.Gameplay.Anomalies.InterviewAnomaly",
     (-40.0, 1.6, 0.0), (10.0, 3.2, 7.0),
     "  sitPosition: {fileID: 0}\n"),
]

BACKLOG_MOTE_COUNT = 260

# ─── Mutant spawn points ────────────────────────────────────────────────────────────────
#
# CensusTakerCount = 2 here, the highest in the catalogue, and this is the bible's hunting ground.
# Three of the four points are IN water. That is the site's teeth: where the player is at half
# speed, the thing hunting them is not.
MUTANT_SPAWN_POINTS = [
    (-20.0, -2.85, 8.0),     # basin floor, west end
    (20.0, -2.85, -8.0),     # basin floor, east end
    (38.0, -1.05, 10.0),     # service tunnel, north half
    (-30.0, 0.15, 24.0),     # north bank, dry, behind the player's likely exit line
]

# ─── Clutter manifest ───────────────────────────────────────────────────────────────────
CLUTTER_SPREAD_M = 0.55

CLUTTER_CLUSTERS = [
    ("Bank_Crates",   (-30.2, 0.97, -26.3), 3, "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Pump_Tools",    (-36.2, 1.07, -2.4),  2, "Cube", (0.30, 0.18, 0.22), "M_Pickup_Tool"),
    ("Basin_Ammo",    (-14.0, -2.53, -5.4), 3, "Cube", (0.30, 0.18, 0.22), "M_Pickup_Ammo"),
    ("Tunnel_Vials",  (38.0, -0.17, -2.4),  2, "Cube", (0.30, 0.20, 0.20), "M_Pickup_Medical"),
    ("Control_Files", (37.2, 1.07, 24.3),   3, "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
]

CLUTTER_FLOOR = [
    ("Floor_Crate_1",  (-46.0, 0.30, -30.0), "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Floor_Crate_2",  (26.0, 0.30, -30.6),  "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Floor_Tin_1",    (-6.0, 0.13, -31.4),  "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    ("Floor_Tin_2",    (18.0, 0.13, 30.2),   "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    ("Floor_Folder_1", (-34.0, 0.03, 28.6),  "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
    # Submerged: on the basin floor, not floating on the surface.
    ("Floor_Sub_Tin",    (4.0, -2.87, -10.0), "Cylinder", (0.20, 0.13, 0.20), "M_Pickup_Food"),
    ("Floor_Sub_Crate",  (-2.0, -2.70, 11.0), "Cube", (0.30, 0.30, 0.30), "M_Timber_Crate"),
    ("Floor_Sub_Folder", (24.0, -2.97, 2.0),  "Cube", (0.30, 0.05, 0.24), "M_Pickup_Document"),
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
        """Invisible wade trigger. Never added to `solids` — water is a speed cost, not an obstacle,
        and a water box in the collision list would seal the basin the walkability gate proved open."""
        go, t = sb.obj(name, parent=parent, pos=pos)
        world[t] = _compose(parent, pos, (0, 0, 0), (1, 1, 1))
        sb.box_collider(go, is_trigger=True, size=size)
        sb.mono(go, "WaterVolume", "Assembly-CSharp::OblastZero.Gameplay.WaterVolume",
                "  speedFactor: %s\n  logEntry: 0\n" % f(speed_factor))
        return go, t

    # ══ ENVIRONMENT ═════════════════════════════════════════════════════════════════════
    env = group("=== ENVIRONMENT ===")

    # Ground, split so the basin and the service tunnel are real holes.
    ground = group("Ground", env)
    for n, p, s in [
        ("Ground_South",     (0, -0.5, -24),   (100, 1, 20)),   # Z -34..-14
        ("Ground_North",     (0, -0.5, 24),    (100, 1, 20)),   # Z +14..+34
        ("Ground_West",      (-39, -0.5, 0),   (22, 1, 28)),    # X -50..-28
        ("Ground_EastInner", (31, -0.5, 0),    (6, 1, 28)),     # X +28..+34
        ("Ground_EastOuter", (46, -0.5, 0),    (8, 1, 28)),     # X +42..+50
    ]:
        solid(n, ground, p, s, "M_Concrete_Floor")

    peri = group("Perimeter", env)
    for n, p, s in [
        ("Wall_North", (0, 4.5, 34.5),  (102, 9, 1)),
        ("Wall_South", (0, 4.5, -34.5), (102, 9, 1)),
        ("Wall_West",  (-50.5, 4.5, 0), (1, 9, 70)),
        ("Wall_East",  (50.5, 4.5, 0),  (1, 9, 70)),
    ]:
        solid(n, peri, p, s, "M_Concrete_Stained")

    # ── Bank stock ──────────────────────────────────────────────────────────────────────
    # Pallet crates on both banks, top face at 0.80. The bank pickups sit on these rather than on
    # bare concrete: loot flat on a 100 m apron is invisible past about fifteen metres, and this
    # level asks the player to commit to a crossing before they can see what is on the far side.
    bank = group("BankStock", env)
    for i, (x, z) in enumerate([(-30, -26), (-10, -28), (6, -24), (-20, 22), (-2, 26), (14, 20)]):
        solid("Bank_Crate_%d" % (i + 1), bank, (x, 0.4, z), (1.4, 0.8, 1.4), "M_Timber_Crate")

    # ── The basin ───────────────────────────────────────────────────────────────────────
    basin = group("Basin", env)
    solid("Basin_Floor", basin, (0, -3.25, 0), (56, 0.5, 28), "M_Concrete_Floor")
    for n, p, s in [
        ("Basin_Wall_West",   (-28.25, -1.5, 0),    (0.5, 3, 28)),
        ("Basin_Wall_East",   (28.25, -1.5, 0),     (0.5, 3, 28)),
        # South wall, split around the entry ramp gap at X -20..-12.
        ("Basin_Wall_S_A",    (-24.1, -1.5, -14.25), (8.25, 3, 0.5)),
        ("Basin_Wall_S_B",    (8.1, -1.5, -14.25),   (40.25, 3, 0.5)),
        # North wall, split around the exit ramp gap at X +12..+20.
        ("Basin_Wall_N_A",    (-8.1, -1.5, 14.25),   (40.25, 3, 0.5)),
        ("Basin_Wall_N_B",    (24.1, -1.5, 14.25),   (8.25, 3, 0.5)),
    ]:
        solid(n, basin, p, s, "M_Concrete_Silo")

    # 3.0 m drop over 8.4 m of run: 19.65 degrees, the depot's proven pit-ramp gradient.
    # Two ramps, diagonally opposite, so crossing the basin is a real traversal rather than a
    # dip in and back out of the same corner.
    solid("Basin_Ramp_South", basin, (-16, -1.65, -9.8), (8, 0.4, 8.92),
          "M_Concrete_Floor", rot=(19.65, 0, 0))
    solid("Basin_Ramp_North", basin, (16, -1.65, 9.8), (8, 0.4, 8.92),
          "M_Concrete_Floor", rot=(-19.65, 0, 0))

    # Stock that was in the basin when it drained. Pallets at -2.70, a crate at -2.20.
    solid("Basin_Pallet_1", basin, (-14, -2.85, -6), (4, 0.3, 4), "M_Timber_Crate")
    solid("Basin_Pallet_2", basin, (12, -2.85, 6), (4, 0.3, 4), "M_Timber_Crate")
    solid("Basin_Crate_1", basin, (18, -2.6, -8), (1.4, 0.8, 1.4), "M_Timber_Crate")
    for i, x in enumerate((-22, 24)):
        solid("Basin_Silt_%d" % (i + 1), basin, (x, -2.9, 10 * (1 if i else -1)),
              (7, 0.7, 7), "M_Grain_Spill", mesh="Sphere", collide=False, shadows=False)

    # ── Catwalk ─────────────────────────────────────────────────────────────────────────
    # The dry crossing. Deck top at 1.2; stair treads rise 0.24 against a 0.32 step offset, so the
    # walkability flood-fill climbs them (it caps climbs at the step offset and treats drops as free).
    cat = group("Catwalk", env)
    solid("Catwalk_Deck", cat, (0, 1.08, 0), (3, 0.24, 28), "M_Steel_Galvanised")
    for i in range(5):
        y = 0.24 * (i + 1) - 0.12
        solid("Catwalk_Step_S_%d" % (i + 1), cat, (0, y, -16.7 + 0.6 * i),
              (3, 0.24, 0.6), "M_Steel_Galvanised", shadows=False)
        solid("Catwalk_Step_N_%d" % (i + 1), cat, (0, y, 16.7 - 0.6 * i),
              (3, 0.24, 0.6), "M_Steel_Galvanised", shadows=False)
    for i in range(5):
        z = -12 + i * 6
        solid("Catwalk_Pier_%d" % (i + 1), cat, (0, -0.9, z), (0.5, 4.2, 0.5),
              "M_Steel_Rusted", shadows=False)
    # Handrails: no collider. A 0.9 m rail with a box collider is a wall the flood-fill cannot
    # climb, which would make the whole catwalk unwalkable while still verifying as "built".
    for side in (-1.4, 1.4):
        solid("Catwalk_Rail_%s" % ("W" if side < 0 else "E"), cat, (side, 1.7, 0),
              (0.08, 1.0, 28), "M_Steel_Rusted", collide=False, shadows=False)

    # ── Service tunnel (the shortcut) ───────────────────────────────────────────────────
    tun = group("ServiceTunnel", env)
    solid("Tunnel_Floor", tun, (38, -1.45, 0), (8, 0.5, 28), "M_Concrete_Floor")
    for n, p, s in [
        ("Tunnel_Wall_West", (33.75, 0.9, 0), (0.5, 4.2, 28)),
        ("Tunnel_Wall_East", (42.25, 0.9, 0), (0.5, 4.2, 28)),
    ]:
        solid(n, tun, p, s, "M_Concrete_Stained")
    # Roof underside at 3.0, not 2.2. The ramp head meets ground level inside the tunnel
    # footprint, so a 2.0 m underside left the walkability flood-fill without a clear 1.8 m
    # controller at the entrance and cut the whole shortcut off from the level. Nothing else
    # reported it: the tunnel built fine, verified fine, and simply could not be walked into.
    solid("Tunnel_Roof", tun, (38, 3.2, 0), (8, 0.4, 28), "M_Concrete_Stained")
    # 1.2 m drop over 3.36 m of run: the same 19.65 degrees.
    solid("Tunnel_Ramp_South", tun, (38, -0.75, -12.3), (6, 0.4, 3.57),
          "M_Concrete_Floor", rot=(19.65, 0, 0))
    solid("Tunnel_Ramp_North", tun, (38, -0.75, 12.3), (6, 0.4, 3.57),
          "M_Concrete_Floor", rot=(-19.65, 0, 0))
    solid("Tunnel_Rack", tun, (38, -0.4, -3), (1.6, 0.12, 8), "M_Steel_Rusted", shadows=False)

    # ── Pump house ──────────────────────────────────────────────────────────────────────
    pump = group("PumpHouse", env)
    for n, p, s in [
        ("Pump_Wall_West",  (-48.25, 3.5, 0),   (0.5, 7, 24)),
        ("Pump_Wall_North", (-40, 3.5, 12.25),  (17, 7, 0.5)),
        ("Pump_Wall_South", (-40, 3.5, -12.25), (17, 7, 0.5)),
        # East face, split around a 5 m doorway at Z -2.5..+2.5.
        ("Pump_Wall_E_A",   (-31.75, 3.5, 7.25), (0.5, 7, 10)),
        ("Pump_Wall_E_B",   (-31.75, 3.5, -7.25), (0.5, 7, 10)),
    ]:
        solid(n, pump, p, s, "M_Paint_Institution")
    solid("Pump_Roof", pump, (-40, 7.2, 0), (17, 0.4, 25), "M_Concrete_Stained")
    solid("Pump_Housing_1", pump, (-42, 0.6, -6), (2.4, 1.2, 2.4), "M_Steel_Galvanised")
    solid("Pump_Housing_2", pump, (-42, 0.6, 6), (2.4, 1.2, 2.4), "M_Steel_Galvanised")
    solid("Pump_Bench", pump, (-36, 0.85, 1), (1.4, 0.1, 9), "M_Steel_Galvanised")
    for i, dz in enumerate((-3.5, 3.5)):
        solid("Pump_Bench_Leg_%d" % (i + 1), pump, (-36, 0.4, 1 + dz),
              (0.12, 0.8, 0.12), "M_Steel_Galvanised", shadows=False)

    # ── Control room (the way out) ──────────────────────────────────────────────────────
    ctrl = group("ControlRoom", env)
    for n, p, s in [
        ("Ctrl_Wall_West",  (30.25, 2.5, 26),  (0.5, 5, 13)),
        ("Ctrl_Wall_East",  (46.25, 2.5, 26),  (0.5, 5, 13)),
        ("Ctrl_Wall_North", (38, 2.5, 32.25),  (16.5, 5, 0.5)),
        # South face, split around a 5 m doorway at X +35.5..+40.5.
        ("Ctrl_Wall_S_A",   (32.75, 2.5, 19.75), (5.5, 5, 0.5)),
        ("Ctrl_Wall_S_B",   (43.25, 2.5, 19.75), (5.5, 5, 0.5)),
    ]:
        solid(n, ctrl, p, s, "M_Concrete_Stained")
    solid("Ctrl_Roof", ctrl, (38, 5.2, 26), (16.5, 0.4, 13), "M_Concrete_Stained")
    solid("Ctrl_Console", ctrl, (38, 0.85, 24), (6, 0.1, 1.4), "M_Steel_Galvanised")
    for i, dx in enumerate((-2.4, 2.4)):
        solid("Ctrl_Console_Leg_%d" % (i + 1), ctrl, (38 + dx, 0.4, 24),
              (0.12, 0.8, 0.12), "M_Steel_Galvanised", shadows=False)
    solid("Ctrl_Cabinet", ctrl, (44, 0.9, 28), (1.2, 1.8, 0.7), "M_Steel_Rusted")
    solid("Ctrl_Sign", ctrl, (38, 3.4, 20.1), (3.2, 0.9, 0.12), "M_Sign_Bunker", shadows=False)
    stripes = group("HazardStripes", ctrl)
    for i in range(8):
        solid("Stripe_%02d" % (i + 1), stripes, (38, 0.03, 15 + i), (5, 0.06, 0.8),
              "M_Hazard_Yellow" if i % 2 == 0 else "M_Hazard_Black",
              collide=False, shadows=False)

    # ── Water ───────────────────────────────────────────────────────────────────────────
    water = group("StandingWater", env)
    solid("Water_Basin_Surface", water, (0, BASIN_WATER_Y, 0), (5.6, 1, 2.8),
          WATER_MATERIAL_NAME, mesh="Plane", collide=False, shadows=False)
    solid("Water_Tunnel_Surface", water, (38, TUNNEL_WATER_Y, 0), (0.8, 1, 2.8),
          WATER_MATERIAL_NAME, mesh="Plane", collide=False, shadows=False)

    # Deep: 1.4 m over the basin floor. Top at -1.0 clears the catwalk deck (1.2) by a wide margin,
    # so crossing dry never triggers the wade.
    water_volume("Water_Basin", water, (0, -2.0, 0), (56, 2.0, 28),
                 WATER_DEEP_SPEED_FACTOR)
    # Shallow: the service tunnel. Composes multiplicatively with the Backlog inside it.
    water_volume("Water_Tunnel", water, (38, -0.6, 0), (8, 1.2, 28),
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

    scene_meta = ("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n"
                  "  externalObjects: {}\n  mainObjectFileID: %d\n"
                  "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

    if not check:
        # The water material is shared with the Census Office. Written by whichever generator runs;
        # identical content either way, so the order they run in cannot matter.
        write("%s/%s.mat" % (MATERIAL_DIR, WATER_MATERIAL_NAME),
              particle_material_yaml(WATER_MATERIAL_NAME, WATER_TINT))
        write("%s/%s.mat.meta" % (MATERIAL_DIR, WATER_MATERIAL_NAME),
              scene_meta % (guid_for("Material::" + WATER_MATERIAL_NAME), 2100000))
        write(VOLUME_PROFILE_PATH, volume_profile_yaml())
        write(VOLUME_PROFILE_PATH + ".meta",
              scene_meta % (guid_for(VOLUME_PROFILE_KEY), 11400000))
        print("wrote %s" % VOLUME_PROFILE_PATH)

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
    print("reachability check: control room + all %d pickups reachable AND escapable from spawn "
          "(%.0f m2 walkable)" % (len(placed), area))

    nav_bytes = nav_grid.to_bytes()
    nav_problems = verify_nav_grid(nav_grid, nav_bytes, PLAYER_SPAWN, BUNKER_DOOR_XZ,
                                   mutant_spawn_points=MUTANT_SPAWN_POINTS)
    if nav_problems:
        raise SystemExit("nav grid check FAILED:\n  " + "\n  ".join(nav_problems))
    print("nav grid check: header round-trips, spawn and control room both passable, "
          "reachable by A* over the exported bytes, all %d mutant points on live cells"
          % len(MUTANT_SPAWN_POINTS))

    if check:
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
        print("\nCHECK PASSED: Reservoir.unity and navgrid_reservoir.bytes match the plan.")
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
    write(SCENE_PATH + ".meta",
          "fileFormatVersion: 2\nguid: %s\nDefaultImporter:\n"
          "  externalObjects: {}\n  userData: \n"
          "  assetBundleName: \n  assetBundleVariant: \n" % guid_for(SCENE_KEY))

    n_go = scene_text.count("\nGameObject:\n")
    print("wrote %s — %d GameObjects, %d lines, all references resolve"
          % (SCENE_PATH, n_go, scene_text.count("\n")))
    print("scene guid: %s" % guid_for(SCENE_KEY))
    return 0


if __name__ == "__main__":
    sys.exit(main())
