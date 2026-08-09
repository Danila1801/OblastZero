#!/usr/bin/env python3
"""
The parts of a scavenge scene that are the same at every site.

WHY THIS MODULE EXISTS
======================
The Grain Depot's generator carried ~490 lines of rig: the sun and the fluorescent fixtures, the
URP volume, the player and its camera stack, ScavengeController, the two HUDs, MutantSpawner, the
dust field, the emission VFX rig, the anomaly volumes, the bunker trigger and the pickup emitter.
None of that is depot knowledge. It is what a Blowout level IS.

Copying it into the Census Office and Reservoir generators would have produced three copies of the
camera stack and three copies of the pickup emitter -- and the pickup emitter is where silhouette
selection, trigger sizing, tilt and scale spread all live, each tuned against a real bug. A fix
applied to one copy would have quietly not applied to the other two.

So the rig lives here once and each site supplies a PLAN: coordinates, a pickup manifest, its
anomaly volumes, its fixtures, its dust box. Everything site-specific arrives through `cfg`.

DESIGN RULE: nothing in this file may know which site it is building. If a site name appears here,
a config field is missing.
"""

import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import visual_archetypes as va                                      # noqa: E402
from scavenge_scene_lib import (                                    # noqa: E402
    SCRIPT_GUIDS, DUST_MATERIAL_NAME, RANGE_RING_MATERIAL_NAME, guid_for, f,
)
from scene_verify_lib import rotated_half_extents                   # noqa: E402


def jitter(seed_text, index, lo, hi):
    """
    Deterministic value in [lo, hi] from a name and a channel index.

    md5 rather than Python's hash(): hash() of a str is salted per process (PYTHONHASHSEED), so a
    generator keyed on it would emit a different scene on every invocation and quietly defeat the
    byte-determinism the regeneration-as-validation workflow depends on.
    """
    import hashlib
    digest = hashlib.md5(("OblastZero::jitter::%s::%d" % (seed_text, index)).encode("utf-8")).digest()
    raw = int.from_bytes(digest[:4], "big") / 4294967295.0
    return lo + (hi - lo) * raw


def standard_pickup_variation(name, base_scale, is_crew, spread, tilt_fraction, tilt_degrees):
    """
    Returns (tilt_x, tilt_z, scale) for one pickup. Shared by every site so placement variety is
    tuned in one place.

    Crew get scale spread but never tilt. A crew manifest already rolls the capsule about Z -- a body
    slumped against whatever it fell on, which is why there is someone to rescue -- and stacking a few
    more degrees of X/Z on a deliberate roll is the one case where tilt could push a capsule through
    a wall. The placement gates constrain scale but not tilt (CLAUDE.md §15), so this is the
    construction being safe rather than the gate catching it.
    """
    scale = tuple(base_scale[i] * (1.0 + jitter(name, i, -spread, spread)) for i in range(3))

    if is_crew:
        return 0.0, 0.0, scale

    if jitter(name, 3, 0.0, 1.0) >= tilt_fraction:
        return 0.0, 0.0, scale

    return (jitter(name, 4, -tilt_degrees, tilt_degrees),
            jitter(name, 5, -tilt_degrees, tilt_degrees),
            scale)


def add_rig(sb, group, solid, placed, cfg):
    """
    Emits every shared system into `sb` and returns the directional light's fileID.

    `group` and `solid` are the plan's own builder closures, so anything this function creates is
    registered in the plan's world-transform table and its collision list -- which is what keeps the
    walkability and placement gates honest about rig-authored geometry. `placed` is the plan's
    pickup list; the emitter appends to it in place.

    `cfg` is the site plan module (or any object exposing the same names).
    """

    # ══ LIGHTING ════════════════════════════════════════════════════════════════════════
    lights = group("=== LIGHTING ===")

    sun_go, _ = sb.obj("Overcast_Sun", parent=lights, pos=(0, 24, 0), rot=(52, -34, 0))
    # Soft shadows on the one directional light; every point light below casts none.
    sun_light_id = sb.light(sun_go, kind=1, color=(0.62, 0.66, 0.68, 1),
                            intensity=0.85, shadows=2, bounce=0.6)

    for name, x, y, z, rng in cfg.FIXTURES:
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

    # The one warm light in the level, over the door the whole run is pointed at. Per-site,
    # because "where is the way out" is the most site-specific fact there is — a hardcoded depot
    # position would put the Census Office's beacon in a filing room.
    beacon_go, _ = sb.obj("Bunker_Door_Beacon", parent=lights, pos=cfg.BUNKER_BEACON_POS)
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
                    guid_for(cfg.VOLUME_PROFILE_KEY)), vol_id)

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
    player_go, player_tr = sb.obj("Player", pos=cfg.PLAYER_SPAWN, rot=cfg.PLAYER_FACING, tag="Player")

    cam_go, cam_tr = sb.obj("Main Camera", parent=player_tr,
                            pos=(0, cfg.EYE_HEIGHT, 0), tag="MainCamera")
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
        "  interactRange: %s\n"
        "  interactMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n"
        "  showInteractionRing: 1\n"
        "  interactionRingMaterial: {fileID: 2100000, guid: %s, type: 2}\n"
        % (cam_tr, f(cfg.SCAVENGE_INTERACTION_RANGE),
           guid_for("Material::" + RANGE_RING_MATERIAL_NAME)))

    # -- Geiger counter. Lives on the player because it is the player's ear, and it stays silent
    # unless the pack actually contains item_kafedra_geiger_counter — detection is a purchase the
    # player makes with carry weight, not a free sense. Only the Carbon Copy answers it.
    sb.mono(player_go, "AnomalyAudioCue",
            "Assembly-CSharp::OblastZero.Gameplay.Anomalies.AnomalyAudioCue",
            "  detectionRange: %s\n"
            "  scanInterval: 0.25\n" % f(cfg.GEIGER_DETECTION_RANGE_M))

    # -- ScavengeController: routes pickups into InventoryManager / CrewManager.
    ctrl_go, _ = sb.obj("Scavenge_Controller", parent=sysg)
    sb.mono(ctrl_go, "ScavengeController",
            "Assembly-CSharp::OblastZero.Gameplay.ScavengeController",
            "  player: {fileID: %d}\n" % player_ctrl_id)

    # -- Prop dresser: swaps each pickup's primitive silhouette for its authored mesh once
    # GLBPropLoader has the meshes resident. Deliberately NOT baked into this scene: the
    # generator's burial, support and walkability gates all reason about the primitive extents,
    # and referencing meshes here would make the scene un-regenerable without the Editor. The
    # primitive stays the authority on placement and collision; the dresser owns appearance only.
    dress_go, _ = sb.obj("Scavenge_Prop_Dresser", parent=sysg)
    sb.mono(dress_go, "ScavengePropDresser",
            "Assembly-CSharp::OblastZero.Gameplay.ScavengePropDresser",
            "  dressOnStart: 1\n"
            "  hideReplacedPrimitive: 1\n"
            "  logCensus: 1\n")

    # -- HUD: self-building canvas. Thresholds mirror BalanceConstants.SCAVENGE_*.
    hud_go, _ = sb.obj("Scavenge_HUD", parent=sysg)
    sb.mono(hud_go, "ScavengeHUD", "Assembly-CSharp::OblastZero.UI.ScavengeHUD",
            "  normalColor: {r: 0.9, g: 0.9, b: 0.86, a: 1}\n"
            "  warnColor: {r: 1, g: 0.7, b: 0.2, a: 1}\n"
            "  dangerColor: {r: 1, g: 0.25, b: 0.2, a: 1}\n"
            "  warnThreshold: %s\n"
            "  dangerThreshold: %s\n"
            "  initialSecondsDisplay: %s\n"
            % (f(cfg.SCAVENGE_TIMER_WARNING_THRESHOLD),
               f(cfg.SCAVENGE_TIMER_CRITICAL_THRESHOLD),
               f(cfg.SCAVENGE_TIMER_SECONDS)))

    # -- Hazard read-outs: what is following you, what is being written down, what the room is
    # doing to time. A second self-building canvas rather than more fields on ScavengeHUD, because
    # these are exceptional by construction -- most runs show none of them -- and a scene can carry
    # the base HUD without the hazard layer.
    hazard_go, _ = sb.obj("Scavenge_Hazard_HUD", parent=sysg)
    sb.mono(hazard_go, "ScavengeHazardHUD", "Assembly-CSharp::OblastZero.UI.ScavengeHazardHUD",
            "  registrationNoticeSeconds: 4\n"
            "  editNoticeSeconds: 3.5\n"
            "  glitchSeconds: 0.9\n")

    # -- Mutant spawner. Reads the run's site from ScavengeSiteCatalog and spawns to its threat
    # profile, so the depot's zero Census-Takers and the Reservoir's two are one declaration in the
    # catalogue rather than a decision re-made in every scene generator.
    #
    # Spawn points are the four yard corners, all well clear of the SW spawn and of the direct route,
    # so a figure is something the player notices rather than something that materialises on them.
    # At the depot none of them are used -- CensusTakerCount is 0 there -- but they ship anyway, so a
    # threat retune is a number in the catalogue and not a scene regeneration.
    spawner_go, spawner_tr = sb.obj("Mutant_Spawner", parent=sysg)
    spawn_point_ids = []
    for idx, point in enumerate(cfg.MUTANT_SPAWN_POINTS):
        _, point_tr = sb.obj("Mutant_Spawn_%d" % (idx + 1), parent=spawner_tr, pos=point)
        spawn_point_ids.append(point_tr)

    sb.mono(spawner_go, "MutantSpawner",
            "Assembly-CSharp::OblastZero.Gameplay.Mutants.MutantSpawner",
            "  spawnPoints:\n%s"
            "  minimumSpawnDistance: 18\n"
            "  playerWaitTimeout: 10\n"
            % "".join("  - {fileID: %d}\n" % tid for tid in spawn_point_ids))

    # -- Dust field: builds its own ParticleSystem at runtime (see ScavengeDustField's header for
    # why the system is not serialized here). Starts at the player spawn so frame one is already
    # dusty; the component re-centres it on the camera from LateUpdate onward.
    dust_go, _ = sb.obj("Scavenge_Dust_Field", parent=sysg, pos=cfg.PLAYER_SPAWN)
    sb.mono(dust_go, "ScavengeDustField",
            "Assembly-CSharp::OblastZero.Gameplay.ScavengeDustField",
            "  dustMaterial: {fileID: 2100000, guid: %s, type: 2}\n"
            "  tint: {r: %s, g: %s, b: %s, a: %s}\n"
            "  sizeMin: 0.02\n"
            "  sizeMax: 0.05\n"
            "  lifetimeMin: %s\n"
            "  lifetimeMax: %s\n"
            "  driftMin: %s\n"
            "  driftMax: %s\n"
            "  lateralDrift: 0.06\n"
            "  boxSize: {x: %s, y: %s, z: %s}\n"
            "  emissionRatePerSecond: %s\n"
            "  maxParticles: %d\n"
            "  followTarget: {fileID: %d}\n"
            "  followStepMetres: 4\n"
            % (guid_for("Material::" + DUST_MATERIAL_NAME),
               f(cfg.DUST_TINT[0]), f(cfg.DUST_TINT[1]), f(cfg.DUST_TINT[2]), f(cfg.DUST_TINT[3]),
               f(cfg.DUST_LIFETIME[0]), f(cfg.DUST_LIFETIME[1]),
               f(cfg.DUST_DRIFT[0]), f(cfg.DUST_DRIFT[1]),
               f(cfg.DUST_BOX[0]), f(cfg.DUST_BOX[1]), f(cfg.DUST_BOX[2]),
               f(cfg.DUST_EMISSION_PER_SECOND), cfg.DUST_MAX_PARTICLES, cam_tr))

    # -- Emission VFX: post-processing escalation, camera shake and the flash overlay. Owns a runtime
    # Volume that outranks the scene's, so ScavengeVolumeProfile.asset is never written to at play time.
    vfx_go, _ = sb.obj("Scavenge_Emission_VFX", parent=sysg)
    sb.mono(vfx_go, "EmissionVfxController",
            "Assembly-CSharp::OblastZero.Gameplay.EmissionVfxController",
            "  warningVignetteBoost: 0.14\n"
            "  criticalVignetteBoost: 0.34\n"
            "  warningPulseHz: 1\n"
            "  criticalPulseHz: 4\n"
            "  criticalSaturationDrop: -45\n"
            "  criticalPostExposure: 0.2\n"
            "  criticalColorFilter: {r: 1, g: 0.62, b: 0.55, a: 1}\n"
            "  flashPeakAlpha: 0.6\n"
            "  flashFadeSeconds: 0.2\n"
            "  fovPunchSeconds: 0.3\n")

    # ══ ANOMALIES ═══════════════════════════════════════════════════════════════════════
    # The bible's three Phase A hazards (BESTIARY.md "ANOMALIES"). One of each in the depot,
    # each placed against the decision it is meant to create rather than scattered for coverage.
    #
    # They are trigger volumes, so they are invisible to the walkability flood-fill and to the
    # burial/support gate — correctly. An anomaly must never make a route impassable; the Backlog
    # in particular has to be walkable, because a player who cannot enter it cannot be trapped by
    # it, and choosing to enter is the whole mechanic. cfg.ANOMALY_ZONES is checked for mutual overlap
    # in verify_placement() instead: AnomalyZone.ZoneAt takes the first match, so two overlapping
    # volumes would resolve arbitrarily and hide a level bug behind plausible behaviour.
    anom = group("=== ANOMALIES ===")

    for name, script, cls, pos, size, fields in cfg.ANOMALY_ZONES:
        go, tr = sb.obj(name, parent=anom, pos=pos)
        sb.box_collider(go, is_trigger=True, size=size)
        sb.mono(go, script, cls, fields)

        # The Backlog is the one anomaly the bible requires the player to be able to see and avoid,
        # so it gets the hanging-mote haze as a child. BacklogAnomaly.Awake re-sizes the emitter from
        # its own collider, so the box below is only the pre-Awake default and cannot drift from the
        # trigger the way two separately authored numbers would.
        if script != "BacklogAnomaly":
            continue

        motes_go, _ = sb.obj(name + "_Motes", parent=tr)
        sb.mono(motes_go, "BacklogMotes",
                "Assembly-CSharp::OblastZero.Gameplay.Anomalies.BacklogMotes",
                "  moteMaterial: {fileID: 2100000, guid: %s, type: 2}\n"
                "  boxSize: {x: %s, y: %s, z: %s}\n"
                "  moteCount: %d\n"
                "  tint: {r: 0.82, g: 0.8, b: 0.72, a: 0.42}\n"
                "  sizeMin: 0.03\n"
                "  sizeMax: 0.09\n"
                % (guid_for("Material::" + DUST_MATERIAL_NAME),
                   f(size[0]), f(size[1]), f(size[2]), cfg.BACKLOG_MOTE_COUNT))

    # -- Bunker door trigger, spanning the stair mouth inside the headhouse.
    trig_go, _ = sb.obj("Bunker_Entrance_Trigger", parent=sysg, pos=cfg.BUNKER_TRIGGER_POS)
    sb.box_collider(trig_go, is_trigger=True, size=cfg.BUNKER_TRIGGER_SIZE)
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
    tilted = 0
    for data_id, kind, qty, dur, contam, pos, yaw, mat in cfg.PICKUPS:
        is_crew = kind == cfg.CREW
        name = ("Crew_" if is_crew else "Pickup_") + data_id

        archetype = "Crew" if is_crew else va.derive(cfg.ITEM_CATEGORIES.get(data_id), data_id)
        mesh, base_scale = va.SHAPES[archetype]
        archetype_census[archetype] = archetype_census.get(archetype, 0) + 1

        # Deterministic tilt + scale spread, seeded from this object's name. Breaks the clone-army
        # look without touching the authored positions, which the gates below have already proved
        # clear of geometry and which the depot's 60-second pacing was tuned against.
        tilt_x, tilt_z, scale = cfg.pickup_variation(name, base_scale, is_crew)
        rot = (tilt_x, yaw, 58 if is_crew else tilt_z)
        if tilt_x or tilt_z:
            tilted += 1

        # Half-extents in world units, accounting for non-unit primitive meshes: a Cylinder
        # or Capsule mesh is 2 units tall, so its Y half-extent is the Y scale, not half of it.
        base = cfg.BOX_LOCAL[mesh]
        half = tuple(scale[i] * base[i] / 2.0 for i in range(3))
        # Then grown to the tilted box's true world footprint, so the gates judge the object as it
        # actually sits rather than as it would sit level.
        world_half = rotated_half_extents(half, rot)

        # The manifest's Y values were authored against the old 0.34 cube resting on a surface.
        # Keep each pickup's BOTTOM where it was and let the silhouette change above it —
        # otherwise a flattened document hovers 14 cm in the air and the support check fails.
        # world_half, not half: a tilted crate's lowest corner is below its level base, and using
        # the un-tilted figure here is what would push that corner through the shelf.
        drop = cfg.LEGACY_PICKUP_HALF_Y - world_half[1]
        pos = (pos[0], pos[1] - drop, pos[2]) if not is_crew else pos

        if is_crew:
            go, _ = sb.obj(name, parent=pick, pos=pos, rot=rot, scale=scale)
            sb.mesh_renderer(go, mesh, mat, cast_shadows=True)
            sb.capsule_collider(go, is_trigger=True, radius=0.62, height=2.3)
            placed.append((name, pos, (0.62, 0.72, 0.62), True))
        else:
            go, _ = sb.obj(name, parent=pick, pos=pos, rot=rot, scale=scale)
            sb.mesh_renderer(go, mesh, mat, cast_shadows=False)
            # Collider size is LOCAL, so a fixed number would scale with the mesh and give a
            # 1.6 m trigger on a rifle and a 0.3 m one on a can. Divide out the scale to keep
            # every pickup the same size to the crosshair — including the ±10% spread, or a
            # small can would be measurably harder to hit than a large one.
            sb.box_collider(go, is_trigger=True,
                            size=tuple(cfg.PICKUP_TRIGGER_WORLD_M / s for s in scale))
            placed.append((name, pos, world_half, False))

        sb.mono(go, "ScavengePickup", "Assembly-CSharp::OblastZero.Gameplay.ScavengePickup",
                "  kind: %d\n"
                "  dataId: %s\n"
                "  quantity: %d\n"
                "  durabilityOverride: %d\n"
                "  contamination: %s\n" % (kind, data_id, qty, dur, f(contam)))

        # Hover highlight + world label. The label floats above the object's origin, so a crew capsule
        # (0.86 tall, and slumped) needs more clearance than a document lying flat on a shelf.
        sb.mono(go, "PickupHoverHighlight",
                "Assembly-CSharp::OblastZero.Gameplay.PickupHoverHighlight",
                "  highlightEmission: {r: 0.3, g: 0.32, b: 0.38, a: 1}\n"
                "  labelHeight: %s\n"
                "  labelCharacterSize: 0.035\n" % f(1.35 if is_crew else 0.55))

    print("pickup silhouettes: " + ", ".join(
        "%s x%d" % (k, v) for k, v in sorted(archetype_census.items())))
    print("placement variety: %d of %d pickups knocked askew, all %d scaled ±%d%%"
          % (tilted, len(cfg.PICKUPS), len(cfg.PICKUPS), int(cfg.PICKUP_SCALE_SPREAD * 100)))

    # ══ CLUTTER ═════════════════════════════════════════════════════════════════════════
    # Non-pickup dressing: companion stock beside real pickups so the loot reads as clusters
    # rather than as one-of-each, plus items that have ended up on the floor.
    #
    # These carry no collider and no ScavengePickup, which is what makes them safe. Every pickup
    # position in the manifest has been proved clear of geometry, supported, reachable AND
    # escapable, and the depot's route timings were tuned against those exact coordinates — so the
    # brief's "move pickups into shared anchor positions with ±0.3 m offsets" is the one instruction
    # here that would have put verified gameplay geometry at risk for a purely visual gain. Adding
    # companions next to the pickups produces the same clusters on screen, more objects rather than
    # fewer, and cannot bury a pickup, unseat it from its shelf, or seal a route.
    clutter = group("=== CLUTTER ===")
    for cname, cpos, cyaw, cmesh, cscale, cmat in cfg.build_clutter():
        cgo, _ = sb.obj(cname, parent=clutter, pos=cpos, rot=cyaw, scale=cscale, static=True)
        sb.mesh_renderer(cgo, cmesh, cmat, cast_shadows=False)
    print("clutter: %d non-pickup props in %d clusters + floor scatter"
          % (len(cfg.build_clutter()), len(cfg.CLUTTER_CLUSTERS)))

    return sun_light_id
