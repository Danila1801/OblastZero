# OblastZero Prop Generation Prompts
# 7 missing VisualArchetype meshes for Scavenge scene
# Use with Gemini / Nano Banana / any image generator
# Then place generated images in Assets/Art/Meshes/Props_Source/
# Run: python tools/generate_triposr.py
# Then: python tools/decimate_props.py

---

## MASTER TEMPLATE STRUCTURE
Copy and paste this structure. Replace the [BRACKETED] sections with your specific object details.

```
(Minimalist product photography of a [SUBJECT]), (centered full shot), (object isolated against a pure solid white background), (high contrast isolation), (flat ambient studio lighting), (uniform illumination), (shadowless), (zero shadows), (no depth of field), (sharp focus), (high-resolution diffuse texture), (8k), (gritty), (utilitarian), (Soviet-era aesthetic), (weathered), (used condition), (materials: [MATERIALS LIST]), (colors: [COLOR PALETTE]), (slight 3/4 front view for maximum geometric detail) --ar 1:1
```

---

## MATERIAL AND COLOR SPECIFICS (for Soviet Realism)

**Standard Palette Keywords:**
Faded olive drab, oxidized steel gray, chipping crimson red, aged cream, industrial brown, corroded brass, dull aluminum, bakelite orange.

**Gritty Texture Keywords:**
Heavy weathering, rust spots, peeling paint, scratched metal, grease stains, dirt build-up, impact dents, serialized stamp numbers, worn edges.

---

## SEVEN SPECIFIC PROMPTS

### Prop 1: MetalCan — Military Ration Tin (Тушёнка)
Maps to: VisualArchetype.MetalCan (cylinder 0.20×0.13×0.20 m)

```
Minimalist product photography of a single cylindrical Soviet military ration tin, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: aged tin metal with corrosion, colors: dull silver and oxidized rust, paper label with faded Cyrillic text, slight dents on the rim, slight 3/4 front view --ar 1:1
```

**Output filename:** `prop_ration_tin.png` → becomes `prop_ration_tin.glb`

---

### Prop 2: Medical — First Aid Kit (AI-2 Orange Box)
Maps to: VisualArchetype.Medical (cube 0.30×0.20×0.20 m)

```
Minimalist product photography of a rectangular Soviet AI-2 individual first aid kit box, closed, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: aged plastic, colors: bright bakelite orange but faded and scratched, embossed Cyrillic letters on the lid, dirt in the crevices, slight 3/4 front view --ar 1:1
```

**Output filename:** `prop_first_aid_kit.png` → becomes `prop_first_aid_kit.glb`

---

### Prop 3: Clothing — Civilian Gas Mask (GP-5)
Maps to: VisualArchetype.Clothing (capsule 0.26×0.16×0.26 m)

```
Minimalist product photography of a Soviet GP-5 gas mask, complete with attached circular filter canister, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: aging grey rubber and rusted metal filter, colors: pale grey and dark corroded green, oxidation on the filter thread, slight 3/4 front view showing the eye lenses and the filter connection --ar 1:1
```

**Output filename:** `prop_gas_mask.png` → becomes `prop_gas_mask.glb`

---

### Prop 4: WeaponSidearm — Makarov PM Pistol
Maps to: VisualArchetype.WeaponSidearm (cube 0.26×0.10×0.13 m)

```
Minimalist product photography of a Soviet Makarov PM pistol, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: blued steel and bakelite grips, colors: oxidized steel gray and aged bakelite orange, wear on the slide and grip panels, Cyrillic factory markings, slight 3/4 front view showing the slide profile --ar 1:1
```

**Output filename:** `prop_makarov_pm.png` → becomes `prop_makarov_pm.glb`

---

### Prop 5: WeaponLong — AK-74 Assault Rifle
Maps to: VisualArchetype.WeaponLong (cube 0.86×0.11×0.14 m)

```
Minimalist product photography of a Soviet AK-74 assault rifle, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: stamped steel receiver and laminate wood furniture, colors: faded olive drab and aged wood brown, scratches on the receiver, serialized numbers on the side, slight 3/4 front view showing the magazine and muzzle brake --ar 1:1
```

**Output filename:** `prop_ak74.png` → becomes `prop_ak74.glb`

---

### Prop 6: Tool — Heavy Adjustable Wrench
Maps to: VisualArchetype.Tool (cube 0.52×0.12×0.12 m)

```
Minimalist product photography of a heavy, solid forged steel adjustable wrench, Soviet industrial style, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: dark forged steel and grease stains, colors: industrial gray and brown, pitting and heavy use marks on the handle, slight 3/4 front view lying flat --ar 1:1
```

**Output filename:** `prop_adjustable_wrench.png` → becomes `prop_adjustable_wrench.glb`

---

### Prop 7: Tool — Entrenching Shovel (MPL-50)
Maps to: VisualArchetype.Tool (cube 0.52×0.12×0.12 m)

```
Minimalist product photography of a Soviet-style military entrenching shovel MPL-50, with a pointed metal blade and a short wooden handle, centered full shot, object isolated against a pure solid white background, high contrast isolation, flat ambient studio lighting, uniform illumination, shadowless, zero shadows, no depth of field, sharp focus, high-resolution diffuse texture, 8k, gritty, utilitarian, Soviet-era aesthetic, weathered, used condition, materials: chipped painted metal blade and raw scratched wood, colors: dark army green and weathered wood brown, dirt caked near the rivets, slight 3/4 front view --ar 1:1
```

**Output filename:** `prop_entrenching_shovel.png` → becomes `prop_entrenching_shovel.glb`

---

## POST-GENERATION CHECKLIST

For each generated image:
1. [ ] Verify pure white background (no shadows, no gradient)
2. [ ] Verify 1:1 aspect ratio
3. [ ] Verify object is centered and fully visible
4. [ ] If background isn't perfect → run through remove.bg first
5. [ ] Save as PNG to `Assets/Art/Meshes/Props_Source/` with exact filename above
6. [ ] Run `python tools/generate_triposr.py`
7. [ ] Run `python tools/decimate_props.py`
8. [ ] Verify in Unity: prop appears in Scavenge scene via `PropArchetypeRegistry`

---

## VISUAL ARCHETYPE REGISTRY MAPPING

After decimation, add entries to `PropArchetypeRegistry.asset` (Unity Editor):

| VisualArchetype | Resource Key | Source File |
|---|---|---|
| MetalCan | Props/prop_ration_tin | prop_ration_tin.bytes |
| Medical | Props/prop_first_aid_kit | prop_first_aid_kit.bytes |
| Clothing | Props/prop_gas_mask | prop_gas_mask.bytes |
| WeaponSidearm | Props/prop_makarov_pm | prop_makarov_pm.bytes |
| WeaponLong | Props/prop_ak74 | prop_ak74.bytes |
| Tool | Props/prop_adjustable_wrench | prop_adjustable_wrench.bytes |
| Tool | Props/prop_entrenching_shovel | prop_entrenching_shovel.bytes |

---

## NOTES

- The "slight 3/4 front view" is critical — pure orthographic front views confuse single-image 3D models
- Flat lighting = "grit" comes from texture (rust, scratches), not baked shadows
- TripoSR runs on ~6 GB VRAM (fits your RTX 4060 8 GB)
- Decimation targets: LOD0=8k, LOD1=3k, LOD2=1k triangles
- Output goes to Unity Resources as `.bytes` (GLTFast loads at runtime)