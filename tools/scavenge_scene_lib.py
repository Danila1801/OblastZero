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

# ─── Project-fixed references (harvested from the real .meta files / packages) ───────────

SCRIPT_GUIDS = {
    "ScavengePlayerController": "9e319e08079513c4290f2886347cdcc6",
    "ScavengeController":       "b3e96352afa1c784c8006ee4d525c0ac",
    "ScavengePickup":           "c2e4112c71b475c4da289fb9d60230cd",
    "BunkerEntranceTrigger":    "74b55b54648c09a40ba7988899b82992",
    "ScavengeHUD":              "dd3cbf194b2223e4bb413132b96e7089",
    "FluorescentFlicker":       "4c0db41a545e64afcab7ef35b065ae47",
    # URP / SRP core components
    "UniversalAdditionalCameraData": "a79441f348de89743a2939f4d699eac1",
    "UniversalAdditionalLightData":  "474bcb49853aa07438625e644c072ee6",
    "Volume":                        "172515602e62fb746b5d573b38a5fe58",
    "VolumeProfile":                 "d7fd9488000d3734a9e00ee676215985",
    # URP post-processing overrides
    "Vignette":         "899c54efeace73346a0a16faa3afe726",
    "ColorAdjustments": "66f335fb1ffd8684294ad653bf1c7564",
    "FilmGrain":        "29fa0085f50d5e54f8144f766051a691",
    "Tonemapping":      "97c23e3b12dc18c42a140437e53d3951",
}

URP_LIT_SHADER_GUID = "933532a4fcc9baf4fa0491de14d08ed7"

# Unity built-in primitive meshes (verified against real package assets, not assumed).
BUILTIN_MESH_GUID = "0000000000000000e000000000000000"
MESH = {"Cube": 10202, "Cylinder": 10206, "Sphere": 10207,
        "Capsule": 10208, "Plane": 10209, "Quad": 10210}

STATIC_ALL = 4294967295   # "Everything" in the Static dropdown — bake/occlusion/batching friendly


def guid_for(name):
    """Stable 32-hex GUID derived from a name, so regeneration never breaks references."""
    return hashlib.md5(("OblastZero::" + name).encode("utf-8")).hexdigest()


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


VOLUME_PROFILE_PATH = "Assets/Settings/ScavengeVolumeProfile.asset"


def volume_profile_yaml():
    """Desaturated, green-grey, vignetted, grainy — the bible's 'тяжесть' (heaviness)."""
    ids = {"Tonemapping": 4820001, "ColorAdjustments": 4820002,
           "Vignette": 4820003, "FilmGrain": 4820004}

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
                          for k in ("Tonemapping", "ColorAdjustments", "Vignette", "FilmGrain"))))

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
