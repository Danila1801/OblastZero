# Gemini Pro (Imagen) Prompts — OblastZero Prop Reference Images

Paste each prompt into Gemini's image generation. Download the result as a PNG, name it as shown, and drop it into `C:\Users\danil\projects\OblastZero\Assets\Art\References\`.

All prompts follow the same formula: **isolated object on pure white background, 3/4 angle, even studio lighting, no text/watermark** — this is critical for clean 3D mesh extraction later.

---

## 1. MetalCan → `ref_metal_can.png`

```
A weathered Soviet-era condensed milk tin can, rusty and dented, standing upright on a plain pure white background, 3/4 front angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The can is cylindrical, about 10cm tall, with a faded blue and white painted label showing Cyrillic text that is illegible from age. Rust marks around the rim and a dent on one side. Game asset reference image style.
```

## 2. Document → `ref_document.png`

```
A battered manila folder, closed, lying flat on a plain pure white background, top-down 3/4 angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The folder is faded cardboard with a torn paper label and a rusted paper clip. Some torn edges visible. Soviet bureaucratic document style, slightly yellowed paper. Game asset reference image style.
```

## 3. WeaponSidearm → `ref_pistol.png`

```
A weathered Soviet Makarov pistol, side profile, lying on a plain pure white background, 3/4 angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The pistol is dull gunmetal finish with visible wear marks and grime. Slide is forward, magazine removed. Old, well-used but functional. Game asset reference image, realistic style, no people, no hands.
```

## 4. WeaponLong → `ref_rifle.png`

```
An old hunting rifle, weathered and worn, side profile, lying on a plain pure white background, 3/4 angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The rifle has a wooden stock wrapped in frayed electrical tape at the grip, a dull blued metal barrel with patina and surface rust. About 90cm long. Old, well-used but functional. Game asset reference image, realistic style, no people, no hands.
```

## 5. Clothing → `ref_coat.png`

```
A folded wool military greatcoat, olive drab color, standing folded upright on a plain pure white background, 3/4 front angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The coat is Soviet-era, heavy wool, visible wear and dust, brass buttons tarnished, collar turned up. Folded neatly as if displayed on a shelf. Game asset reference image style.
```

## 6. Medical → `ref_medkit.png`

```
A Soviet travmatologiya first aid kit, a faded red canvas pouch with a white cross, lying on a plain pure white background, 3/4 front angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The kit is a small rectangular canvas bag, about 15cm wide, visible stains and wear, the white paint cross is partially worn off. Slightly open showing gauze inside. Game asset reference image style.
```

## 7. Crew (mannequin placeholder) → `ref_crew.png`

```
A plain grey shop mannequin dressed in a Soviet-era worker outfit — quilted vest, work pants, heavy boots — standing upright on a plain pure white background, full body 3/4 front angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The mannequin is faceless, neutral standing pose. The outfit is olive and brown, dusty and worn. Game asset reference image style.
```

## 8. Tool (wrench variant) → `ref_wrench.png`

```
A large rusty adjustable wrench, lying on a plain pure white background, 3/4 angle view, product photography lighting, clean isolated single object, no other objects, no text, no watermark, no shadow on background. The wrench is about 30cm long, heavy forged steel with a corroded blackened finish and visible rust pitting. Open jaw adjustable. Old, well-used industrial tool. Game asset reference image, realistic style.
```

---

## BONUS: Texture Images (for scene materials)

These are texture-map style images — tileable/seamless surfaces:

### 9. Concrete Floor — Stained → `tex_concrete_floor_stained.png`

```
Top-down seamless tileable texture of a stained concrete floor, grey with dark oil stains, dust, hairline cracks, and discoloration. Soviet industrial warehouse floor. Photorealistic, even flat lighting, no shadows, tileable edges match. 1024x1024 game texture.
```

### 10. Metal — Rusted Sheet → `tex_rusted_sheet.png`

```
Top-down seamless tileable texture of a rusted corrugated metal sheet, orange-brown rust patches, peeling paint, pitted surface. Photorealistic, even flat lighting, no shadows, tileable edges match. 1024x1024 game texture.
```

### 11. Hazard Stripes — Grime → `tex_hazard_stripes.png`

```
Top-down seamless tileable texture of yellow and black diagonal hazard warning stripes, weathered and grimy, paint chipped and faded, dust overlay. Industrial safety marking. Photorealistic, even flat lighting, tileable edges match. 1024x1024 game texture.
```

### 12. Wood — Old Plank → `tex_wood_plank.png`

```
Top-down seamless tileable texture of old wooden planks, weathered grey-brown wood grain, gaps between boards, dust and grime in the grain. Soviet warehouse shelving wood. Photorealistic, even flat lighting, tileable edges match. 1024x1024 game texture.
```

---

## How to Use

1. Copy each prompt
2. Paste into Gemini's image generation (Imagen)
3. Download the generated image
4. Save with the filename shown (e.g., `ref_metal_can.png`)
5. Drop into `C:\Users\danil\projects\OblastZero\Assets\Art\References\`

Once all 8 archetype ref images are done, we convert them to 3D meshes (Higgsfield when topped up, or free alternatives like Rodin/Luma, or feed them to a local 3D model pipeline).

The 4 bonus texture images can be dropped into `Assets/Art/Textures/` directly — they're usable as-is in Unity URP Lit material textures.
