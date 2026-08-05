#!/usr/bin/env python3
"""
Unity YAML emitters for the scavenge scene generator. This module knows how to write valid
Unity 6 scene documents, URP Lit materials, and a URP VolumeProfile; it knows nothing about
the level itself. The level plan lives in tools/generate_scavenge_scene.py, which is the
entry point.

Every reference this emits — script GUIDs, built-in mesh fileIDs, URP component GUIDs — was
harvested from the real .meta files and package assets in this project rather than assumed,
because a wrong GUID produces a scene that opens with silently missing components.
"""

import hashlib
import math
import os

# ─── Project-fixed references (harvested from the real .meta files / packages) ───────────

SCRIPT_GUIDS = {
    "ScavengePlayerController": "9e319e08079513c4290f2886347cdcc6",
    "ScavengeController":       "b3e96352afa1c784c8006ee4d525c0ac",
    "ScavengePickup":           "c2e4112c71b475c4da289fb9d60230cd",
    "BunkerEntranceTrigger":    "74b55b54648c09a40ba7988899b82992",
    "ScavengeHUD":              "dd3cbf194b2223e4bb413132b96e7089",
    "FluorescentFlicker":       "4c0db41a545e64afcab7ef35b065ae47",
    "ScavengePropDresser":      "c713638314d9e0c8e4973ae8a8f1a24f",
    "ScavengeDustField":        "1f8d9943febaff2bbd912c1614f130a2",
    # Unity was open when this one landed and minted its own GUID before tools/author_script_metas.py
    # could, so the value here is Unity's, not the derived one. The Editor's identity always wins once
    # it exists — rewriting the .meta to match a table would orphan every reference already made to it.
    # assert_script_guids() caught the mismatch on the first generation attempt, which is the whole
    # reason that gate exists (CLAUDE.md §14: a wrong GUID is a component that runs no code, silently).
    "PickupHoverHighlight":     "fb6bb5a39b1cbe3498e90ab89a8dcf3e",
    "EmissionVfxController":    "6a3cdde8fc6fa9988f912247232ee2f4",
    # URP / SRP core components
    "UniversalAdditionalCameraData": "a79441f348de89743a2939f4d699eac1",
    "UniversalAdditionalLightData":  "474bcb49853aa07438625e644c072ee6",
    "Volume":                        "172515602e62fb746b5d573b38a5fe58",
    "VolumeProfile":                 "d7fd9488000d3734a9e00ee676215985",
    # URP post-processing overrides. Harvested out of Assets/Settings/DefaultVolumeProfile.asset and
    # SampleSceneProfile.asset — URP 17.4 ships builtin (no Library/PackageCache copy), so the
    # package .cs.meta files this table is normally read from are not on disk at all.
    "Vignette":                  "899c54efeace73346a0a16faa3afe726",
    "ColorAdjustments":          "66f335fb1ffd8684294ad653bf1c7564",
    "FilmGrain":                 "29fa0085f50d5e54f8144f766051a691",
    "Tonemapping":               "97c23e3b12dc18c42a140437e53d3951",
    "Bloom":                     "0b2db86121404754db890f4c8dfe81b2",
    "DepthOfField":              "c01700fd266d6914ababb731e09af2eb",
    "ShadowsMidtonesHighlights": "558a8e2b6826cf840aae193990ba9f2e",
}

URP_LIT_SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"
URP_PARTICLES_UNLIT_SHADER_GUID = "0406db5a14f94604a8c57ccfbc9f3b46"

# Unity built-in primitive meshes (verified against real package assets, not assumed).
BUILTIN_MESH_GUID = "0000000000000000e000000000000000"
MESH = {"Cube": 10202, "Cylinder": 10206, "Sphere": 10207,
        "Capsule": 10208, "Plane": 10209, "Quad": 10210}

STATIC_ALL = 4294967295   # "Everything" in the Static dropdown — bake/occlusion/batching friendly


def guid_for(name):
    """Stable 32-hex GUID derived from a name, so regeneration never breaks references."""
    return hashlib.md5(("OblastZero::" + name).encode("utf-8")).hexdigest()


# SCRIPT_GUIDS entries that belong to this project rather than to a Unity package. Only these can be
# checked against a .meta on disk; the URP ones live in the package cache under a hashed folder name.
PROJECT_SCRIPTS = (
    "ScavengePlayerController", "ScavengeController", "ScavengePickup",
    "BunkerEntranceTrigger", "ScavengeHUD", "FluorescentFlicker", "ScavengePropDresser",
    "ScavengeDustField", "PickupHoverHighlight", "EmissionVfxController",
)

_ASSETS_ROOT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Assets")


def assert_script_guids():
    """
    Verifies every project script GUID in SCRIPT_GUIDS still matches its .cs.meta on disk.

    CLAUDE.md §14: a wrong script GUID yields a silently unassigned component, not an error — the
    scene loads, the object is there, and the behaviour simply never runs. That is the single most
    expensive failure mode in headless scene authoring, and until now the table was a set of
    hand-copied constants with nothing watching them. Deleting a script, or letting Unity reimport
    one into a fresh GUID, now fails the generator instead of shipping a dead component.
    """
    found, problems = {}, []
    for root, _dirs, files in os.walk(_ASSETS_ROOT):
        for filename in files:
            if not filename.endswith(".cs.meta"):
                continue
            stem = filename[:-len(".cs.meta")]
            if stem not in PROJECT_SCRIPTS:
                continue
            with open(os.path.join(root, filename), "r", encoding="utf-8") as handle:
                for line in handle:
                    if line.startswith("guid:"):
                        found[stem] = line.split(":", 1)[1].strip()
                        break

    for name in PROJECT_SCRIPTS:
        expected = SCRIPT_GUIDS.get(name)
        actual = found.get(name)
        if actual is None:
            problems.append("%s: no .cs.meta found under Assets/ (has Unity imported it?)" % name)
        elif actual != expected:
            problems.append("%s: table says %s, meta says %s" % (name, expected, actual))

    if problems:
        raise SystemExit("script GUID check FAILED:\n  " + "\n  ".join(problems))
    return "script guid check: %d project scripts match their .meta" % len(PROJECT_SCRIPTS)


def euler_to_quat(x, y, z):
    """Unity's ZXY intrinsic euler order, matching what the Inspector shows."""
    rx, ry, rz = math.radians(x) * 0.5, math.radians(y) * 0.5, math.radians(z) * 0.5
    sx, cx = math.sin(rx), math.cos(rx)
    sy, cy = math.sin(ry), math.cos(ry)
    sz, cz = math.sin(rz), math.cos(rz)
    return (
        cy * sx * cz + sy * cx * sz,
        sy * cx * cz - cy * sx * sz,
        cy * cx * sz - sy * sx * cz,
        cy * cx * cz + sy * sx * sz,
    )


def f(v):
    """Unity writes floats without trailing zeros; keep the file tidy and diff-stable."""
    if isinstance(v, int) or float(v).is_integer():
        return str(int(v))
    return repr(round(float(v), 6)).rstrip("0").rstrip(".")


def v3(t):
    return "{x: %s, y: %s, z: %s}" % (f(t[0]), f(t[1]), f(t[2]))


def col(c):
    return "{r: %s, g: %s, b: %s, a: %s}" % (f(c[0]), f(c[1]), f(c[2]), f(c[3] if len(c) > 3 else 1))


# ─── Materials ──────────────────────────────────────────────────────────────────────────
# name: (baseColor, smoothness, metallic, emissionColor or None)
MATERIALS = {
    "M_Concrete_Stained":  ((0.34, 0.34, 0.32), 0.14, 0.0,  None),
    "M_Concrete_Floor":    ((0.26, 0.27, 0.25), 0.10, 0.0,  None),
    "M_Concrete_Silo":     ((0.41, 0.40, 0.37), 0.12, 0.0,  None),
    "M_Steel_Rusted":      ((0.31, 0.19, 0.12), 0.22, 0.35, None),
    "M_Steel_Galvanised":  ((0.44, 0.46, 0.47), 0.42, 0.70, None),
    "M_Paint_Institution": ((0.23, 0.29, 0.24), 0.20, 0.0,  None),
    "M_Timber_Crate":      ((0.34, 0.25, 0.15), 0.08, 0.0,  None),
    "M_Grain_Spill":       ((0.51, 0.43, 0.25), 0.05, 0.0,  None),
    "M_Hazard_Yellow":     ((0.60, 0.48, 0.07), 0.30, 0.0,  None),
    "M_Hazard_Black":      ((0.05, 0.05, 0.05), 0.30, 0.0,  None),
    "M_Void_Dark":         ((0.02, 0.02, 0.02), 0.02, 0.0,  None),
    # Emissive fixtures / signage
    "M_Fixture_Tube":      ((0.80, 0.82, 0.75), 0.35, 0.0,  (1.45, 1.50, 1.25)),
    "M_Sign_Bunker":       ((0.35, 0.06, 0.05), 0.25, 0.0,  (2.60, 0.28, 0.18)),
    # Pickup proxies — colour-coded by database category so the loot reads at a glance
    "M_Pickup_Food":       ((0.55, 0.41, 0.21), 0.18, 0.10, (0.16, 0.11, 0.04)),
    "M_Pickup_Water":      ((0.26, 0.41, 0.47), 0.55, 0.05, (0.05, 0.11, 0.14)),
    "M_Pickup_Medical":    ((0.72, 0.72, 0.68), 0.25, 0.0,  (0.18, 0.18, 0.16)),
    "M_Pickup_Weapon":     ((0.17, 0.18, 0.19), 0.45, 0.80, (0.05, 0.05, 0.06)),
    "M_Pickup_Ammo":       ((0.54, 0.43, 0.17), 0.50, 0.90, (0.12, 0.09, 0.03)),
    "M_Pickup_Tool":       ((0.47, 0.37, 0.13), 0.30, 0.30, (0.13, 0.10, 0.03)),
    "M_Pickup_Document":   ((0.70, 0.68, 0.57), 0.12, 0.0,  (0.16, 0.15, 0.12)),
    "M_Pickup_Artifact":   ((0.30, 0.72, 0.62), 0.70, 0.10, (0.42, 1.55, 1.20)),
    "M_Crew_Coat":         ((0.29, 0.26, 0.21), 0.15, 0.0,  (0.10, 0.09, 0.07)),
}

MATERIAL_DIR = "Assets/Art/Materials/Scavenge"

MAT_TEX_ENVS = ["_BaseMap", "_BumpMap", "_DetailAlbedoMap", "_DetailMask", "_DetailNormalMap",
                "_EmissionMap", "_MainTex", "_MetallicGlossMap", "_OcclusionMap", "_ParallaxMap",
                "_SpecGlossMap", "unity_Lightmaps", "unity_LightmapsInd", "unity_ShadowMasks"]


def material_yaml(name, base, smoothness, metallic, emission):
    tex = "\n".join(
        "    - %s:\n        m_Texture: {fileID: 0}\n"
        "        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}" % t
        for t in MAT_TEX_ENVS)

    keywords = "  m_ValidKeywords:\n  - _EMISSION\n" if emission else "  m_ValidKeywords: []\n"
    lightmap_flags = 2 if emission else 4     # 2 = emissive contributes to GI, 4 = none
    emis = emission if emission else (0.0, 0.0, 0.0)

    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %(name)s
  m_Shader: {fileID: 4800000, guid: %(shader)s, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
%(keywords)s  m_InvalidKeywords: []
  m_LightmapFlags: %(lmflags)d
  m_EnableInstancingVariants: 1
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 2000
  stringTagMap:
    RenderType: Opaque
  disabledShaderPasses:
  - MOTIONVECTORS
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
%(tex)s
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _AlphaToMask: 0
    - _Blend: 0
    - _BlendModePreserveSpecular: 1
    - _BumpScale: 1
    - _ClearCoatMask: 0
    - _ClearCoatSmoothness: 0
    - _Cull: 2
    - _Cutoff: 0.5
    - _DetailAlbedoMapScale: 1
    - _DetailNormalMapScale: 1
    - _DstBlend: 0
    - _DstBlendAlpha: 0
    - _EnvironmentReflections: 1
    - _GlossMapScale: 0
    - _Glossiness: 0
    - _GlossyReflections: 0
    - _Metallic: %(metallic)s
    - _OcclusionStrength: 1
    - _Parallax: 0.005
    - _QueueOffset: 0
    - _ReceiveShadows: 1
    - _Smoothness: %(smoothness)s
    - _SmoothnessTextureChannel: 0
    - _SpecularHighlights: 1
    - _SrcBlend: 1
    - _SrcBlendAlpha: 1
    - _Surface: 0
    - _WorkflowMode: 1
    - _ZWrite: 1
    m_Colors:
    - _BaseColor: %(base)s
    - _Color: %(base)s
    - _EmissionColor: %(emis)s
    - _SpecColor: {r: 0.2, g: 0.2, b: 0.2, a: 1}
  m_BuildTextureStacks: []
""" % dict(name=name, shader=URP_LIT_SHADER_GUID, keywords=keywords, lmflags=lightmap_flags,
           tex=tex, metallic=f(metallic), smoothness=f(smoothness),
           base=col(base), emis=col(emis))


# Airborne dust. Alpha-blended rather than additive: additive motes over the depot's near-black
# shadows read as glowing sparks, and dust catches light, it does not emit it. Blend factors are the
# literal URP values for Transparent + Alpha (SrcAlpha / OneMinusSrcAlpha, ZWrite off, Cull off so a
# view-aligned billboard is visible from either side).
DUST_MATERIAL_NAME = "M_Dust"

# The interaction-range ring under the player. Same shader family as the dust, so there is exactly one
# transparent-particle surface in the build for PickupVfx to borrow for its bursts.
RANGE_RING_MATERIAL_NAME = "M_RangeRing"


def particle_material_yaml(name, base_color):
    """
    An unlit transparent particle material for ScavengeDustField.

    Kept separate from material_yaml() because that emitter is hardwired to URP Lit / Opaque: it
    writes RenderType Opaque, queue 2000, _ZWrite 1 and no transparency keyword. Reusing it and
    patching properties afterwards is how a "transparent" material ends up rendering as opaque
    quads — the keyword and the stringTagMap have to agree with the blend factors or the surface
    silently falls back.
    """
    tex = "\n".join(
        "    - %s:\n        m_Texture: {fileID: 0}\n"
        "        m_Scale: {x: 1, y: 1}\n        m_Offset: {x: 0, y: 0}" % t
        for t in ("_BaseMap", "_BumpMap", "_EmissionMap", "_MainTex"))

    return """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!21 &2100000
Material:
  serializedVersion: 8
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_Name: %(name)s
  m_Shader: {fileID: 4800000, guid: %(shader)s, type: 3}
  m_Parent: {fileID: 0}
  m_ModifiedSerializedProperties: 0
  m_ValidKeywords:
  - _SURFACE_TYPE_TRANSPARENT
  m_InvalidKeywords: []
  m_LightmapFlags: 4
  m_EnableInstancingVariants: 0
  m_DoubleSidedGI: 0
  m_CustomRenderQueue: 3000
  stringTagMap:
    RenderType: Transparent
  disabledShaderPasses:
  - SHADOWCASTER
  - DepthOnly
  - DepthNormals
  - MOTIONVECTORS
  m_LockedProperties:
  m_SavedProperties:
    serializedVersion: 3
    m_TexEnvs:
%(tex)s
    m_Ints: []
    m_Floats:
    - _AlphaClip: 0
    - _AlphaToMask: 0
    - _Blend: 0
    - _BlendOp: 0
    - _CameraFadingEnabled: 0
    - _ColorMode: 0
    - _Cull: 0
    - _Cutoff: 0.5
    - _DistortionEnabled: 0
    - _DstBlend: 10
    - _DstBlendAlpha: 10
    - _FlipbookBlending: 0
    - _QueueOffset: 0
    - _SoftParticlesEnabled: 0
    - _SrcBlend: 5
    - _SrcBlendAlpha: 1
    - _Surface: 1
    - _ZWrite: 0
    m_Colors:
    - _BaseColor: %(base)s
    - _Color: %(base)s
    - _EmissionColor: {r: 0, g: 0, b: 0, a: 1}
  m_BuildTextureStacks: []
""" % dict(name=name, shader=URP_PARTICLES_UNLIT_SHADER_GUID, tex=tex, base=col(base_color))


VOLUME_PROFILE_PATH = "Assets/Settings/ScavengeVolumeProfile.asset"

# Focus plane and lens for the depth-of-field override, in metres / millimetres / f-stop.
#
# These are named here rather than inlined because they are the one post-processing setting with a
# gameplay cost: the depot is a 60-second read-the-room sprint, and a shallow plane hides the very
# pickups and doorways the player is scanning for. At 35 mm / f2.8 focused at 5 m the acceptably
# sharp band runs roughly 3.7 m to 7.6 m, so the bunker door at the far end of the yard is soft.
# That is the brief's intent ("background shelves blur slightly, frames the player's attention") but
# it is deliberately a one-line change: raise DOF_APERTURE to f8 for a ~2.7 m-to-infinity band, or
# set DOF_MODE to 1 (Gaussian) with a far start if the blur ever reads as fog rather than as focus.
DOF_MODE = 2            # 0 Off, 1 Gaussian, 2 Bokeh (physical — honours focal length + aperture)
DOF_FOCUS_DISTANCE_M = 5.0
DOF_FOCAL_LENGTH_MM = 35.0
DOF_APERTURE_FSTOP = 2.8


def volume_profile_yaml():
    """
    Desaturated, green-grey, vignetted, grainy — the bible's 'тяжесть' (heaviness).

    Four overrides (Tonemapping, ColorAdjustments, Vignette, FilmGrain) predate this pass and are
    reproduced byte-for-byte. Their values were deliberately NOT retuned to the numbers quoted in
    the Phase 3 brief: that brief asks for contrast +10 / saturation -15 / exposure -0.3, which are
    each *weaker* than what already shipped here (contrast 8 / saturation -32 / exposure -0.35), so
    applying them would have lifted the grade back toward neutral and undone the look.

    Three are new: Bloom (fluorescent tubes read as light sources), DepthOfField, and
    ShadowsMidtonesHighlights, which supplies the warm-highlight / cold-shadow split the brief asked
    ColorCurves for. ColorCurves stores four TextureCurves as serialized AnimationCurve keyframe
    arrays; hand-authoring those blind is a large unverifiable payload for an effect three simple
    Vector4s reproduce exactly. Ambient occlusion is absent on purpose — in URP it is a
    ScriptableRendererFeature, not a VolumeComponent, and it is already enabled on
    Assets/Settings/PC_Renderer.asset (intensity 0.4, radius 0.3).
    """
    ids = {"Tonemapping": 4820001, "ColorAdjustments": 4820002,
           "Vignette": 4820003, "FilmGrain": 4820004,
           "Bloom": 4820005, "DepthOfField": 4820006,
           "ShadowsMidtonesHighlights": 4820007}

    def header(kind, fid):
        return ("--- !u!114 &%d\nMonoBehaviour:\n"
                "  m_ObjectHideFlags: 3\n"
                "  m_CorrespondingSourceObject: {fileID: 0}\n"
                "  m_PrefabInstance: {fileID: 0}\n"
                "  m_PrefabAsset: {fileID: 0}\n"
                "  m_GameObject: {fileID: 0}\n"
                "  m_Enabled: 1\n"
                "  m_EditorHideFlags: 0\n"
                "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
                "  m_Name: %s\n"
                "  m_EditorClassIdentifier: \n"
                "  active: 1\n" % (fid, SCRIPT_GUIDS[kind], kind))

    def ov(state, value):
        return "    m_OverrideState: %d\n    m_Value: %s\n" % (1 if state else 0, value)

    out = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]

    out.append(header("Tonemapping", ids["Tonemapping"]) +
               "  mode:\n" + ov(True, "1") +                     # Neutral
               "  neutralHDRRangeReductionMode:\n" + ov(False, "2") +
               "  acesPreset:\n" + ov(False, "3") +
               "  hueShiftAmount:\n" + ov(False, "0") +
               "  detectPaperWhite:\n" + ov(False, "0") +
               "  paperWhite:\n" + ov(False, "300") +
               "  detectBrightnessLimits:\n" + ov(False, "1") +
               "  minNits:\n" + ov(False, "0.005") +
               "  maxNits:\n" + ov(False, "1000"))

    out.append(header("ColorAdjustments", ids["ColorAdjustments"]) +
               "  postExposure:\n" + ov(True, "-0.35") +
               "  contrast:\n" + ov(True, "8") +
               "  colorFilter:\n" + ov(True, "{r: 0.85, g: 0.9, b: 0.82, a: 1}") +
               "  hueShift:\n" + ov(False, "0") +
               "  saturation:\n" + ov(True, "-32") +
               "\n")

    out.append(header("Vignette", ids["Vignette"]) +
               "  color:\n" + ov(True, "{r: 0.02, g: 0.025, b: 0.02, a: 1}") +
               "  center:\n" + ov(False, "{x: 0.5, y: 0.5}") +
               "  intensity:\n" + ov(True, "0.36") +
               "  smoothness:\n" + ov(True, "0.42") +
               "  rounded:\n" + ov(False, "0") +
               "\n")

    out.append(header("FilmGrain", ids["FilmGrain"]) +
               "  type:\n" + ov(True, "3") +                     # Medium1
               "  intensity:\n" + ov(True, "0.32") +
               "  response:\n" + ov(True, "0.75") +
               "  texture:\n" + ov(False, "{fileID: 0}") +
               "    dimension: 1\n")

    # Bloom. Threshold is above 1.0 on purpose: the grade already sits at -0.35 exposure, so only the
    # emissive fixture tubes (m_EmissionColor 1.45,1.50,1.25) and the bunker sign clear it. A
    # threshold of 0.9 would have bloomed the galvanised steel and the grain spill too, which reads
    # as haze rather than as a working light. Tint is cold white so the halo stays fluorescent.
    out.append(header("Bloom", ids["Bloom"]) +
               "  skipIterations:\n" + ov(True, "1") +
               "  threshold:\n" + ov(True, "1.05") +
               "  intensity:\n" + ov(True, "0.5") +
               "  scatter:\n" + ov(True, "0.62") +
               "  clamp:\n" + ov(False, "65472") +
               "  tint:\n" + ov(True, "{r: 0.909, g: 0.909, b: 0.941, a: 1}") +
               "  highQualityFiltering:\n" + ov(True, "1") +
               "  downscale:\n" + ov(False, "0") +
               "  maxIterations:\n" + ov(False, "6") +
               "  dirtTexture:\n" + ov(False, "{fileID: 0}") +
               "    dimension: 1\n" +
               "  dirtIntensity:\n" + ov(False, "0") +
               "\n")

    out.append(header("DepthOfField", ids["DepthOfField"]) +
               "  mode:\n" + ov(True, f(DOF_MODE)) +
               "  gaussianStart:\n" + ov(False, "10") +
               "  gaussianEnd:\n" + ov(False, "30") +
               "  gaussianMaxRadius:\n" + ov(False, "1") +
               "  highQualitySampling:\n" + ov(False, "0") +
               "  focusDistance:\n" + ov(True, f(DOF_FOCUS_DISTANCE_M)) +
               "  aperture:\n" + ov(True, f(DOF_APERTURE_FSTOP)) +
               "  focalLength:\n" + ov(True, f(DOF_FOCAL_LENGTH_MM)) +
               "  bladeCount:\n" + ov(False, "5") +
               "  bladeCurvature:\n" + ov(False, "1") +
               "  bladeRotation:\n" + ov(False, "0") +
               "\n")

    # The warm-light / cold-shadow split that gives the depot its only two temperatures. Vector4
    # channels are (r, g, b, exposure-ish bias); shadows lean blue, highlights lean toward the
    # fixtures' amber, midtones stay neutral so skin and concrete do not tint.
    out.append(header("ShadowsMidtonesHighlights", ids["ShadowsMidtonesHighlights"]) +
               "  shadows:\n" + ov(True, "{x: 0.92, y: 0.97, z: 1.12, w: 0}") +
               "  midtones:\n" + ov(True, "{x: 1, y: 1, z: 1, w: 0}") +
               "  highlights:\n" + ov(True, "{x: 1.08, y: 1.02, z: 0.9, w: 0}") +
               "  shadowsStart:\n" + ov(True, "0") +
               "  shadowsEnd:\n" + ov(True, "0.32") +
               "  highlightsStart:\n" + ov(True, "0.5") +
               "  highlightsEnd:\n" + ov(True, "1") +
               "\n")

    out.append("--- !u!114 &11400000\nMonoBehaviour:\n"
               "  m_ObjectHideFlags: 0\n"
               "  m_CorrespondingSourceObject: {fileID: 0}\n"
               "  m_PrefabInstance: {fileID: 0}\n"
               "  m_PrefabAsset: {fileID: 0}\n"
               "  m_GameObject: {fileID: 0}\n"
               "  m_Enabled: 1\n"
               "  m_EditorHideFlags: 0\n"
               "  m_Script: {fileID: 11500000, guid: %s, type: 3}\n"
               "  m_Name: ScavengeVolumeProfile\n"
               "  m_EditorClassIdentifier: \n"
               "  components:\n%s"
               % (SCRIPT_GUIDS["VolumeProfile"],
                  "".join("  - {fileID: %d}\n" % ids[k]
                          for k in ("Tonemapping", "ColorAdjustments", "Vignette", "FilmGrain",
                                    "Bloom", "DepthOfField", "ShadowsMidtonesHighlights"))))

    return "\n".join(out)


# ─── Scene graph builder ────────────────────────────────────────────────────────────────

class SceneBuilder:
    """Accumulates GameObjects + components and emits a valid Unity scene YAML document."""

    def __init__(self):
        self._next = 2000              # 1..4 are reserved by the scene-settings objects
        self._blocks = []              # emitted YAML chunks, in file order
        self._children = {}            # transform fileID -> [child transform fileIDs]
        self._roots = []               # root transform fileIDs, in hierarchy order
        self._go_components = {}       # gameObject fileID -> [component fileIDs]

    def _fid(self):
        self._next += 1
        return self._next

    # -- object creation -----------------------------------------------------------------

    def obj(self, name, parent=None, pos=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1),
            tag="Untagged", layer=0, static=False, active=True):
        """Creates a GameObject + Transform. Returns (go_id, transform_id)."""
        go_id, tr_id = self._fid(), self._fid()
        self._children[tr_id] = []

        if parent is None:
            self._roots.append(tr_id)
        else:
            self._children[parent].append(tr_id)

        q = euler_to_quat(*rot)
        self._blocks.append((go_id, "gameobject", dict(
            name=name, tag=tag, layer=layer, static=static, active=active, tr=tr_id)))
        self._blocks.append((tr_id,
                             "--- !u!4 &%d\nTransform:\n"
                             "  m_ObjectHideFlags: 0\n"
                             "  m_CorrespondingSourceObject: {fileID: 0}\n"
                             "  m_PrefabInstance: {fileID: 0}\n"
                             "  m_PrefabAsset: {fileID: 0}\n"
                             "  m_GameObject: {fileID: %d}\n"
                             "  serializedVersion: 2\n"
                             "  m_LocalRotation: {x: %s, y: %s, z: %s, w: %s}\n"
                             "  m_LocalPosition: %s\n"
                             "  m_LocalScale: %s\n"
                             "  m_ConstrainProportionsScale: 0\n"
                             "  m_Children: __CHILDREN_%d__\n"
                             "  m_Father: {fileID: %d}\n"
                             "  m_LocalEulerAnglesHint: %s\n"
                             % (tr_id, go_id, f(q[0]), f(q[1]), f(q[2]), f(q[3]),
                                v3(pos), v3(scale), tr_id, parent or 0, v3(rot))))
        return go_id, tr_id

    def component(self, go_id, yaml_text, comp_id):
        """Registers a component block and links it into its GameObject's component list."""
        self._blocks.append((None, yaml_text))
        self._go_components.setdefault(go_id, []).append(comp_id)
        return comp_id

    # -- mesh helpers --------------------------------------------------------------------

    def mesh_renderer(self, go_id, mesh, material, cast_shadows=True):
        mf_id, mr_id = self._fid(), self._fid()
        self.component(go_id,
                       "--- !u!33 &%d\nMeshFilter:\n"
                       "  m_ObjectHideFlags: 0\n"
                       "  m_CorrespondingSourceObject: {fileID: 0}\n"
                       "  m_PrefabInstance: {fileID: 0}\n"
                       "  m_PrefabAsset: {fileID: 0}\n"
                       "  m_GameObject: {fileID: %d}\n"
                       "  m_Mesh: {fileID: %d, guid: %s, type: 0}\n"
                       % (mf_id, go_id, MESH[mesh], BUILTIN_MESH_GUID), mf_id)
        self.component(go_id,
                       "--- !u!23 &%d\nMeshRenderer:\n"
                       "  m_ObjectHideFlags: 0\n"
                       "  m_CorrespondingSourceObject: {fileID: 0}\n"
                       "  m_PrefabInstance: {fileID: 0}\n"
                       "  m_PrefabAsset: {fileID: 0}\n"
                       "  m_GameObject: {fileID: %d}\n"
                       "  m_Enabled: 1\n"
                       "  m_CastShadows: %d\n"
                       "  m_ReceiveShadows: 1\n"
                       "  m_DynamicOccludee: 1\n"
                       "  m_StaticShadowCaster: 0\n"
                       "  m_MotionVectors: 1\n"
                       "  m_LightProbeUsage: 1\n"
                       "  m_ReflectionProbeUsage: 1\n"
                       "  m_RayTracingMode: 2\n"
                       "  m_RayTraceProcedural: 0\n"
                       "  m_RayTracingAccelStructBuildFlagsOverride: 0\n"
                       "  m_RayTracingAccelStructBuildFlags: 1\n"
                       "  m_SmallMeshCulling: 1\n"
                       "  m_RenderingLayerMask: 1\n"
                       "  m_RendererPriority: 0\n"
                       "  m_Materials:\n  - {fileID: 2100000, guid: %s, type: 2}\n"
                       "  m_StaticBatchInfo:\n    firstSubMesh: 0\n    subMeshCount: 0\n"
                       "  m_StaticBatchRoot: {fileID: 0}\n"
                       "  m_ProbeAnchor: {fileID: 0}\n"
                       "  m_LightProbeVolumeOverride: {fileID: 0}\n"
                       "  m_ScaleInLightmap: 1\n"
                       "  m_ReceiveGI: 1\n"
                       "  m_PreserveUVs: 0\n"
                       "  m_IgnoreNormalsForChartDetection: 0\n"
                       "  m_ImportantGI: 0\n"
                       "  m_StitchLightmapSeams: 1\n"
                       "  m_SelectedEditorRenderState: 3\n"
                       "  m_MinimumChartSize: 4\n"
                       "  m_AutoUVMaxDistance: 0.5\n"
                       "  m_AutoUVMaxAngle: 89\n"
                       "  m_LightmapParameters: {fileID: 0}\n"
                       "  m_SortingLayerID: 0\n"
                       "  m_SortingLayer: 0\n"
                       "  m_SortingOrder: 0\n"
                       "  m_AdditionalVertexStreams: {fileID: 0}\n"
                       % (mr_id, go_id, 1 if cast_shadows else 0, guid_for("Material::" + material)),
                       mr_id)

    def box_collider(self, go_id, is_trigger=False, size=(1, 1, 1), center=(0, 0, 0)):
        c_id = self._fid()
        self.component(go_id,
                       "--- !u!65 &%d\nBoxCollider:\n"
                       "  m_ObjectHideFlags: 0\n"
                       "  m_CorrespondingSourceObject: {fileID: 0}\n"
                       "  m_PrefabInstance: {fileID: 0}\n"
                       "  m_PrefabAsset: {fileID: 0}\n"
                       "  m_GameObject: {fileID: %d}\n"
                       "  m_Material: {fileID: 0}\n"
                       "  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
                       "  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
                       "  m_LayerOverridePriority: 0\n"
                       "  m_IsTrigger: %d\n"
                       "  m_ProvidesContacts: 0\n"
                       "  m_Enabled: 1\n"
                       "  serializedVersion: 3\n"
                       "  m_Size: %s\n"
                       "  m_Center: %s\n"
                       % (c_id, go_id, 1 if is_trigger else 0, v3(size), v3(center)), c_id)

    def capsule_collider(self, go_id, is_trigger=False, radius=0.5, height=2.0, direction=1):
        c_id = self._fid()
        self.component(go_id,
                       "--- !u!136 &%d\nCapsuleCollider:\n"
                       "  m_ObjectHideFlags: 0\n"
                       "  m_CorrespondingSourceObject: {fileID: 0}\n"
                       "  m_PrefabInstance: {fileID: 0}\n"
                       "  m_PrefabAsset: {fileID: 0}\n"
                       "  m_GameObject: {fileID: %d}\n"
                       "  m_Material: {fileID: 0}\n"
                       "  m_IncludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
                       "  m_ExcludeLayers:\n    serializedVersion: 2\n    m_Bits: 0\n"
                       "  m_LayerOverridePriority: 0\n"
                       "  m_IsTrigger: %d\n"
                       "  m_ProvidesContacts: 0\n"
                       "  m_Enabled: 1\n"
                       "  serializedVersion: 2\n"
                       "  m_Radius: %s\n"
                       "  m_Height: %s\n"
                       "  m_Direction: %d\n"
                       "  m_Center: {x: 0, y: 0, z: 0}\n"
                       % (c_id, go_id, 1 if is_trigger else 0, f(radius), f(height), direction), c_id)

    def mono(self, go_id, script_key, class_id, fields=""):
        c_id = self._fid()
        self.component(go_id,
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
                       "  m_EditorClassIdentifier: %s\n%s"
                       % (c_id, go_id, SCRIPT_GUIDS[script_key], class_id, fields), c_id)
        return c_id

    def light(self, go_id, kind, color, intensity, rng=10.0, spot=30.0,
              shadows=0, bounce=1.0):
        """kind: 0=Spot 1=Directional 2=Point 3=Rectangle."""
        l_id = self._fid()
        self.component(go_id,
                       "--- !u!108 &%d\nLight:\n"
                       "  m_ObjectHideFlags: 0\n"
                       "  m_CorrespondingSourceObject: {fileID: 0}\n"
                       "  m_PrefabInstance: {fileID: 0}\n"
                       "  m_PrefabAsset: {fileID: 0}\n"
                       "  m_GameObject: {fileID: %d}\n"
                       "  m_Enabled: 1\n"
                       "  serializedVersion: 11\n"
                       "  m_Type: %d\n"
                       "  m_Color: %s\n"
                       "  m_Intensity: %s\n"
                       "  m_Range: %s\n"
                       "  m_SpotAngle: %s\n"
                       "  m_InnerSpotAngle: %s\n"
                       "  m_CookieSize: 10\n"
                       "  m_Shadows:\n"
                       "    m_Type: %d\n"
                       "    m_Resolution: -1\n"
                       "    m_CustomResolution: -1\n"
                       "    m_Strength: 1\n"
                       "    m_Bias: 0.05\n"
                       "    m_NormalBias: 0.4\n"
                       "    m_NearPlane: 0.2\n"
                       "    m_CullingMatrixOverride:\n"
                       "      e00: 1\n      e01: 0\n      e02: 0\n      e03: 0\n"
                       "      e10: 0\n      e11: 1\n      e12: 0\n      e13: 0\n"
                       "      e20: 0\n      e21: 0\n      e22: 1\n      e23: 0\n"
                       "      e30: 0\n      e31: 0\n      e32: 0\n      e33: 1\n"
                       "    m_UseCullingMatrixOverride: 0\n"
                       "  m_Cookie: {fileID: 0}\n"
                       "  m_DrawHalo: 0\n"
                       "  m_Flare: {fileID: 0}\n"
                       "  m_RenderMode: 0\n"
                       "  m_CullingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n"
                       "  m_RenderingLayerMask: 1\n"
                       "  m_Lightmapping: 4\n"
                       "  m_LightShadowCasterMode: 0\n"
                       "  m_AreaSize: {x: 1, y: 1}\n"
                       "  m_BounceIntensity: %s\n"
                       "  m_ColorTemperature: 6570\n"
                       "  m_UseColorTemperature: 0\n"
                       "  m_BoundingSphereOverride: {x: 0, y: 0, z: 0, w: 0}\n"
                       "  m_UseBoundingSphereOverride: 0\n"
                       "  m_UseViewFrustumForShadowCasterCull: 1\n"
                       "  m_ForceVisible: 0\n"
                       "  m_ShadowRadius: 0\n"
                       "  m_ShadowAngle: 0\n"
                       % (l_id, go_id, kind, col(color), f(intensity), f(rng),
                          f(spot), f(spot * 0.727), shadows, f(bounce)), l_id)

        ald_id = self._fid()
        self.component(go_id,
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
                       "  m_Version: 3\n"
                       "  m_UsePipelineSettings: 1\n"
                       "  m_AdditionalLightsShadowResolutionTier: 2\n"
                       "  m_LightLayerMask: 1\n"
                       "  m_RenderingLayers: 1\n"
                       "  m_CustomShadowLayers: 0\n"
                       "  m_ShadowLayerMask: 1\n"
                       "  m_ShadowRenderingLayers: 1\n"
                       "  m_LightCookieSize: {x: 1, y: 1}\n"
                       "  m_LightCookieOffset: {x: 0, y: 0}\n"
                       "  m_SoftShadowQuality: 1\n"
                       % (ald_id, go_id, SCRIPT_GUIDS["UniversalAdditionalLightData"]), ald_id)
        return l_id

    # -- emit ----------------------------------------------------------------------------

    def emit(self, sun_light_id):
        out = [SCENE_SETTINGS % dict(sun=sun_light_id)]

        for block in self._blocks:
            if len(block) == 3 and block[1] == "gameobject":
                gid, _, d = block
                comps = "".join("  - component: {fileID: %d}\n" % c
                                for c in ([d["tr"]] + self._go_components.get(gid, [])))
                out.append("--- !u!1 &%d\nGameObject:\n"
                           "  m_ObjectHideFlags: 0\n"
                           "  m_CorrespondingSourceObject: {fileID: 0}\n"
                           "  m_PrefabInstance: {fileID: 0}\n"
                           "  m_PrefabAsset: {fileID: 0}\n"
                           "  serializedVersion: 6\n"
                           "  m_Component:\n%s"
                           "  m_Layer: %d\n"
                           "  m_Name: %s\n"
                           "  m_TagString: %s\n"
                           "  m_Icon: {fileID: 0}\n"
                           "  m_NavMeshLayer: 0\n"
                           "  m_StaticEditorFlags: %d\n"
                           "  m_IsActive: %d\n"
                           % (gid, comps, d["layer"], d["name"], d["tag"],
                              STATIC_ALL if d["static"] else 0, 1 if d["active"] else 0))
            else:
                out.append(block[1])

        text = "".join(out)

        # Resolve the deferred child lists now that the whole hierarchy is known.
        for tr_id, kids in self._children.items():
            if kids:
                rendered = "\n" + "\n".join("  - {fileID: %d}" % k for k in kids)
            else:
                rendered = " []"
            text = text.replace("__CHILDREN_%d__" % tr_id, rendered, 1)

        text += ("--- !u!1660057539 &9223372036854775807\nSceneRoots:\n"
                 "  m_ObjectHideFlags: 0\n  m_Roots:\n" +
                 "".join("  - {fileID: %d}\n" % r for r in self._roots))
        return text


SCENE_SETTINGS = """%%YAML 1.1
%%TAG !u! tag:unity3d.com,2011:
--- !u!29 &1
OcclusionCullingSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 2
  m_OcclusionBakeSettings:
    smallestOccluder: 5
    smallestHole: 0.25
    backfaceThreshold: 100
  m_SceneGUID: 00000000000000000000000000000000
  m_OcclusionCullingData: {fileID: 0}
--- !u!104 &2
RenderSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 10
  m_Fog: 1
  m_FogColor: {r: 0.4, g: 0.425, b: 0.4, a: 1}
  m_FogMode: 1
  m_FogDensity: 0.02
  m_LinearFogStart: 14
  m_LinearFogEnd: 98
  m_AmbientSkyColor: {r: 0.29, g: 0.31, b: 0.295, a: 1}
  m_AmbientEquatorColor: {r: 0.2, g: 0.21, b: 0.2, a: 1}
  m_AmbientGroundColor: {r: 0.1, g: 0.1, b: 0.09, a: 1}
  m_AmbientIntensity: 1
  m_AmbientMode: 3
  m_SubtractiveShadowColor: {r: 0.42, g: 0.478, b: 0.627, a: 1}
  m_SkyboxMaterial: {fileID: 0}
  m_HaloStrength: 0.5
  m_FlareStrength: 1
  m_FlareFadeSpeed: 3
  m_HaloTexture: {fileID: 0}
  m_SpotCookie: {fileID: 10001, guid: 0000000000000000e000000000000000, type: 0}
  m_DefaultReflectionMode: 0
  m_DefaultReflectionResolution: 128
  m_ReflectionBounces: 1
  m_ReflectionIntensity: 1
  m_CustomReflection: {fileID: 0}
  m_Sun: {fileID: %(sun)d}
  m_UseRadianceAmbientProbe: 0
--- !u!157 &3
LightmapSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 13
  m_BakeOnSceneLoad: 0
  m_GISettings:
    serializedVersion: 2
    m_BounceScale: 1
    m_IndirectOutputScale: 1
    m_AlbedoBoost: 1
    m_EnvironmentLightingMode: 0
    m_EnableBakedLightmaps: 1
    m_EnableRealtimeLightmaps: 0
  m_LightmapEditorSettings:
    serializedVersion: 12
    m_Resolution: 2
    m_BakeResolution: 20
    m_AtlasSize: 1024
    m_AO: 1
    m_AOMaxDistance: 1
    m_CompAOExponent: 1
    m_CompAOExponentDirect: 0
    m_ExtractAmbientOcclusion: 0
    m_Padding: 2
    m_LightmapParameters: {fileID: 0}
    m_LightmapsBakeMode: 1
    m_TextureCompression: 1
    m_ReflectionCompression: 2
    m_MixedBakeMode: 2
    m_BakeBackend: 2
    m_PVRSampling: 1
    m_PVRDirectSampleCount: 32
    m_PVRSampleCount: 256
    m_PVRBounces: 2
    m_PVREnvironmentSampleCount: 256
    m_PVREnvironmentReferencePointCount: 2048
    m_PVRFilteringMode: 1
    m_PVRDenoiserTypeDirect: 1
    m_PVRDenoiserTypeIndirect: 1
    m_PVRDenoiserTypeAO: 1
    m_PVRFilterTypeDirect: 0
    m_PVRFilterTypeIndirect: 0
    m_PVRFilterTypeAO: 0
    m_PVREnvironmentMIS: 1
    m_PVRCulling: 1
    m_PVRFilteringGaussRadiusDirect: 1
    m_PVRFilteringGaussRadiusIndirect: 1
    m_PVRFilteringGaussRadiusAO: 1
    m_PVRFilteringAtrousPositionSigmaDirect: 0.5
    m_PVRFilteringAtrousPositionSigmaIndirect: 2
    m_PVRFilteringAtrousPositionSigmaAO: 1
    m_ExportTrainingData: 0
    m_TrainingDataDestination: TrainingData
    m_LightProbeSampleCountMultiplier: 4
  m_LightingDataAsset: {fileID: 0}
  m_LightingSettings: {fileID: 0}
--- !u!196 &4
NavMeshSettings:
  serializedVersion: 2
  m_ObjectHideFlags: 0
  m_BuildSettings:
    serializedVersion: 3
    agentTypeID: 0
    agentRadius: 0.5
    agentHeight: 2
    agentSlope: 45
    agentClimb: 0.4
    ledgeDropHeight: 0
    maxJumpAcrossDistance: 0
    minRegionArea: 2
    manualCellSize: 0
    cellSize: 0.16666667
    manualTileSize: 0
    tileSize: 256
    buildHeightMesh: 0
    maxJobWorkers: 0
    preserveTilesOutsideBounds: 0
    debug:
      m_Flags: 0
  m_NavMeshData: {fileID: 0}
"""
