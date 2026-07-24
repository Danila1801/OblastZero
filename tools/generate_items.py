"""
Oblast Zero — Item Content Generator
Generates 500+ item JSON files for Assets/Data/Resources/Items/
Post-administrative bureaucratic horror tone. No post-apoc cliches.
"""
import json
import os
import random
import sys

random.seed(2026)  # reproducible

OUTPUT_DIR = os.path.normpath(os.path.join(
    os.path.dirname(__file__), "..", "Assets", "Data", "Resources", "Items"
))

# ─────────────────────────────────────────────
# CATEGORY DATA: names, adjectives, containers, materials
# ─────────────────────────────────────────────

FOOD_TEMPLATES = [
    # (base_name, weight_range, decay_range, tags, contamination_chance)
    ("Tinned {protein}", (0.3, 0.6), (1.5, 3.0), ["Eat", "Trade"], 0.05),
    ("Preserved {protein}", (0.2, 0.5), (0.8, 2.0), ["Eat", "Trade"], 0.08),
    ("Dried {protein}", (0.15, 0.3), (0.3, 1.0), ["Eat"], 0.03),
    ("Smoked {protein}", (0.2, 0.4), (1.0, 2.0), ["Eat", "Trade"], 0.06),
    ("Salted {protein}", (0.3, 0.5), (0.5, 1.5), ["Eat", "Trade"], 0.04),
    ("{protein} Jerky", (0.1, 0.25), (0.2, 0.8), ["Eat"], 0.05),
    ("Canned {vegetable}", (0.3, 0.5), (2.0, 4.0), ["Eat"], 0.02),
    ("Pickled {vegetable}", (0.4, 0.7), (0.3, 1.0), ["Eat"], 0.01),
    ("Dried {grain}", (0.2, 0.4), (0.1, 0.5), ["Eat"], 0.01),
    ("{grain} Hardtack", (0.15, 0.3), (0.05, 0.2), ["Eat"], 0.01),
    ("{grain} Porridge Mix", (0.2, 0.4), (0.2, 0.8), ["Eat"], 0.01),
    ("Compressed {food_ration}", (0.1, 0.2), (0.05, 0.1), ["Eat"], 0.02),
    ("Emergency {food_ration}", (0.15, 0.3), (0.05, 0.15), ["Eat"], 0.01),
    ("Issued {food_ration}", (0.2, 0.4), (0.1, 0.3), ["Eat", "Trade"], 0.03),
    ("Foraged {forage}", (0.05, 0.2), (0.5, 1.5), ["Eat"], 0.15),
    ("Wild {forage}", (0.05, 0.15), (0.3, 1.0), ["Eat"], 0.20),
    ("Mutant {protein}", (0.3, 0.6), (1.0, 2.5), ["Eat"], 0.35),
    ("Irradiated {protein}", (0.3, 0.5), (0.5, 1.5), ["Eat"], 0.90),
    ("Stale {bread}", (0.1, 0.2), (0.3, 0.8), ["Eat"], 0.02),
    ("Bunker {food_ration}", (0.2, 0.5), (0.1, 0.3), ["Eat"], 0.05),
    ("{condiment} Paste", (0.05, 0.15), (0.5, 1.0), ["Eat"], 0.01),
    ("Powdered {dairy}", (0.1, 0.25), (0.2, 0.5), ["Eat", "Drink"], 0.02),
    ("Concentrated {juice}", (0.15, 0.3), (0.3, 0.6), ["Eat", "Drink"], 0.03),
]

WATER_CONTAINERS = ["Flask", "Canteen", "Tin Bottle", "Jerrycan", "Glass Bottle",
                     "Field Bottle", "Thermos", "Jerry Jug", "Sealed Pouch", "Bladder"]
WATER_TYPES = ["Filtered", "Boiled", "Well", "Rain", "Condensed", "Distilled",
               "Decontaminated", "Potable", "Issued", "Emergency"]

MEDICAL_TEMPLATES = [
    ("{med_type} Bandage", (0.05, 0.15), ["Heal"], 0.01),
    ("{med_type} Splint", (0.1, 0.3), ["Heal"], 0.02),
    ("{med_type} Suture Kit", (0.05, 0.15), ["Heal"], 0.02),
    ("{med_type} Antiseptic", (0.05, 0.2), ["Heal", "Decontaminate"], 0.01),
    ("{med_type} Painkiller", (0.01, 0.05), ["Heal"], 0.01),
    ("{med_type} Sedative", (0.01, 0.05), ["Heal"], 0.02),
    ("{med_type} Stimulant", (0.01, 0.03), ["Heal"], 0.03),
    ("Anti-Rad {med_inject}", (0.02, 0.05), ["Heal", "Decontaminate"], 0.02),
    ("Decontamination {med_inject}", (0.05, 0.2), ["Decontaminate", "Heal"], 0.01),
    ("Psychiatric {med_pill}", (0.01, 0.03), ["Heal"], 0.01),
    ("Improvised {med_type} Kit", (0.1, 0.4), ["Heal"], 0.05),
    ("Field Surgery {med_type}", (0.2, 0.5), ["Heal"], 0.03),
    ("Emergency {med_inject}", (0.02, 0.05), ["Heal"], 0.05),
    ("Kafedra {med_inject}", (0.02, 0.05), ["Heal", "Decontaminate"], 0.10),
    ("Issued {med_type} Kit", (0.15, 0.4), ["Heal"], 0.02),
    ("Expired {med_type} Kit", (0.1, 0.3), ["Heal"], 0.15),
]

WEAPON_TEMPLATES = [
    ("{caliber} {gun_type}", (0.6, 1.2), ["Fight", "Defend", "Trade"], False),
    ("Modified {gun_type}", (0.7, 1.4), ["Fight", "Defend"], False),
    ("Worn {gun_type}", (0.5, 1.0), ["Fight", "Defend"], False),
    ("Issued {gun_type}", (0.7, 1.3), ["Fight", "Defend", "Trade"], False),
    ("Improvised {melee}", (1.5, 3.0), ["Fight", "Defend", "Repair"], False),
    ("{melee}", (1.0, 3.5), ["Fight", "Defend"], False),
    ("Hunting {gun_type}", (0.8, 1.5), ["Fight", "Trade"], False),
    ("Sidearm {gun_type}", (0.4, 0.8), ["Fight", "Defend"], False),
    ("Ceremonial {gun_type}", (0.8, 1.2), ["Fight", "Trade", "Ritual"], False),
    ("Rusted {melee}", (1.5, 3.0), ["Fight"], False),
    ("Sharpened {melee}", (0.8, 2.0), ["Fight", "Defend"], False),
    ("Electrified {melee}", (1.0, 2.5), ["Fight", "Defend"], True),
]

AMMO_TEMPLATES = [
    ("{caliber} Rounds", (0.015, 0.03), ["Fight"], 0.0),
    ("{caliber} Ammunition", (0.02, 0.04), ["Fight"], 0.0),
    ("Box of {caliber}", (0.3, 0.5), ["Fight", "Trade"], 0.0),
    ("Scattered {caliber}", (0.01, 0.02), ["Fight"], 0.0),
    ("Handloaded {caliber}", (0.02, 0.03), ["Fight", "Trade"], 0.0),
    ("Arrows", (0.03, 0.05), ["Fight"], 0.0),
    ("Bolts", (0.03, 0.05), ["Fight"], 0.0),
    ("Flares", (0.05, 0.15), ["Fight", "Defend"], 0.0),
    ("Molotov Components", (0.3, 0.6), ["Fight"], 0.0),
]

TOOL_TEMPLATES = [
    ("{tool_adj} {tool}", (0.8, 3.5), ["Repair"], 0.03),
    ("{tool}", (0.5, 2.5), ["Repair", "Fight"], 0.02),
    ("{tool_adj} {electronic}", (0.3, 2.0), ["Repair", "Read"], 0.05),
    ("Improvised {tool}", (0.5, 2.0), ["Repair"], 0.08),
    ("Issued {tool}", (0.8, 3.0), ["Repair", "Trade"], 0.02),
    ("{electronic}", (0.2, 1.5), ["Read", "Repair"], 0.03),
    ("Worn {tool}", (0.5, 2.0), ["Repair"], 0.05),
    ("Kafedra {electronic}", (0.3, 1.0), ["Read", "Ritual"], 0.10),
    ("Salvaged {tool}", (0.5, 2.0), ["Repair"], 0.06),
]

DOC_TEMPLATES = [
    ("{doc_adj} {doc_type}", (0.05, 0.3), ["Read", "Trade"], 0.02),
    ("{doc_type}: {doc_subject}", (0.05, 0.25), ["Read", "Trade"], 0.01),
    ("{faction_adj} {doc_type}", (0.1, 0.3), ["Read", "Trade"], 0.03),
    ("Sealed {doc_type}", (0.05, 0.2), ["Read", "Trade"], 0.01),
    ("Partial {doc_type}", (0.05, 0.15), ["Read"], 0.01),
    ("Water-Damaged {doc_type}", (0.1, 0.3), ["Read"], 0.01),
    ("Classified {doc_type}", (0.1, 0.3), ["Read", "Trade"], 0.02),
]

ARTIFACT_TEMPLATES = [
    ("Artifact: {art_name}", (0.2, 1.5), ["Trade", "Ritual"], True),
    ("Anomaly Fragment: {art_frag}", (0.1, 0.8), ["Trade", "Ritual"], True),
    ("{art_adj} {art_name}", (0.3, 2.0), ["Trade", "Ritual"], True),
    ("Kafedra Specimen: {art_specimen}", (0.2, 1.0), ["Trade", "Ritual", "Read"], True),
    ("Inert {art_name}", (0.3, 1.0), ["Trade"], False),
    ("Dormant {art_name}", (0.2, 1.0), ["Trade", "Ritual"], True),
]

CRAFTING_TEMPLATES = [
    ("{craft_adj} {craft_mat}", (0.05, 0.5), ["Repair"], 0.02),
    ("Raw {craft_mat}", (0.1, 0.8), ["Repair"], 0.05),
    ("Salvaged {craft_mat}", (0.05, 0.5), ["Repair", "Trade"], 0.04),
    ("Processed {craft_mat}", (0.05, 0.3), ["Repair"], 0.02),
    ("{craft_mat} Scrap", (0.02, 0.3), ["Repair"], 0.03),
    ("Filtered {craft_mat}", (0.05, 0.2), ["Repair", "Decontaminate"], 0.01),
    ("Contaminated {craft_mat}", (0.1, 0.5), ["Repair"], 0.20),
]

SPECIAL_TEMPLATES = [
    ("{special_adj} {special_item}", (0.1, 1.0), ["Trade", "Ritual"], 0.05),
    ("Key: {key_name}", (0.05, 0.2), ["Read"], 0.0),
    ("{special_item}", (0.1, 0.5), ["Trade"], 0.02),
]

# ─────────────────────────────────────────────
# VOCABULARY POOLS
# ─────────────────────────────────────────────

PROTEINS = ["Beef", "Pork", "Fish", "Chicken", "Venison", "Rabbit", "Duck",
            "Mutton", "Sausage", "Liver", "Kidney", "Brain", "Tripe", "Tongue",
            "Carp", "Perch", "Pike", "Sturgeon", "Catfish", "Crab", "Mussel"]
VEGETABLES = ["Beet", "Cabbage", "Potato", "Carrot", "Onion", "Garlic",
              "Turnip", "Radish", "Cucumber", "Tomato", "Pepper", "Mushroom",
              "Pumpkin", "Squash", "Lentil", "Pea", "Bean", "Corn"]
GRAINS = ["Rye", "Wheat", "Buckwheat", "Barley", "Oat", "Millet", "Semolina"]
FOOD_RATIONS = ["Ration", "Meal Pack", "Sustenance Unit", "Provision Pack",
                "Calorie Bar", "Field Meal", "Bunker Provisions"]
FORAGE = ["Berries", "Mushrooms", "Herbs", "Roots", "Nuts", "Leaves",
          "Moss", "Lichen", "Fern", "Wild Garlic", "Sorrel", "Nettle"]
BREADS = ["Bread", "Cracker", "Biscuit", "Flatbread", "Pirozhki", "Sukhar"]
CONDIMENTS = ["Mustard", "Pepper", "Salt", "Lard", "Tallow", "Vinegar",
              "Sugar", "Honey", "Mayonnaise", "Ketchup"]
DAIRY = ["Milk", "Cream", "Cheese", "Butter", "Curd", "Yogurt"]
JUICES = ["Berry", "Apple", "Birch Sap", "Nettle", "Cranberry", "Sea Buckthorn"]

WATER_SPECIAL = ["Irradiated", "Contaminated", "Stagnant", "Unknown-Source",
                 "Gray", "Brown", "Yellowish"]

MED_TYPES = ["Field", "Issued", "Standard", "Improvised", "Expired",
             "Military", "Veterinary", "Veterinary-Surplus", "Emergency"]
MED_INJECTS = ["Injector", "Autoinjector", "Syringe", "Ampule", "Vial", "Drip"]
MED_PILLS = ["Tablets", "Capsules", "Draught", "Drops", "Paste", "Powder"]

CALIBERS = ["9mm", "7.62mm", "5.45mm", "12 Gauge", ".308", "7.92mm",
            "5.56mm", ".380", "14.5mm", ".22 LR"]
GUN_TYPES = ["Pistol", "Revolver", "Rifle", "Carbine", "Shotgun", "SMG",
             "LMG", "Sniper Rifle", "Bolt-Action", "Semi-Auto"]
MELEES = ["Machete", "Axe", "Crowbar", "Pipe", "Knife", "Bayonet",
          "Shovel", "Sledgehammer", "Hatchet", "Club", "Spear", "Baton",
          "Rebar", "Hunting Knife", "Combat Knife", "Trench Knife"]

TOOL_ADJS = ["Heavy", "Standard", "Precision", "Rusted", "Kafedra-Issue",
             "Field", "Industrial", "Compact", "Modified", "Worn"]
TOOLS = ["Pry Bar", "Wrench", "Shovel", "Hammer", "Pliers", "Screwdriver",
         "Wire Cutter", "Tape Measure", "Level", "Clamp", "Torch", "Chisel",
         "Saw", "Drill Bit", "Nail Set", "Caulking Gun", "Bolt Cutter"]
ELECTRONICS = ["Geiger Counter", "Radio", "Flashlight", "Battery Pack", "Detector",
               "Scope", "Night Vision", "Sensor Unit", "Dosimeter", "Spectrometer",
               "Anomaly Scanner", "Signal Receiver", "Transmitter", "Oscilloscope"]

DOC_ADJS = ["Pre-Incident", "Post-Incident", "Classified", "Declassified",
            "Redacted", "Stamped", "Handwritten", "Typed", "Forged", "Official"]
DOC_TYPES = ["Dossier", "Manifest", "Report", "Map", "Log", "Permit",
             "Directive", "Memorandum", "Census Form", "Incident Report",
             "Registration", "Transfer Order", "Audit Sheet", "Inventory"]
DOC_SUBJECTS = ["Bunker 14", "Anomaly Cluster 7", "Demographic Shift",
                "Supply Chain Delta", "Personnel Transfer", "Zone Boundary",
                "Radiation Survey", "Faction Contact", "Artifact Recovery",
                "Incident 1148", "Sector C", "Registration #2291"]
FACTION_ADJS = ["Scale Society", "Cordon Regiment", "Kafedra", "Bureau",
                "Municipal", "District", "Regional"]

ART_NAMES = ["Ballast", "Compass", "Echo", "Lighthouse", "Graviton",
             "Wisp", "Sparkler", "Moonlight", "Jellyfish", "Battery",
             "Soul", "Mica", "Prism", "Gimlet", "Droplet", "Vortex",
             "Shell", "Lullaby", "Night Star", "Ember", "Shard",
             "Cocoon", "Blanket", "Crown", "Eye", "Heart", "Pulse"]
ART_FRAGS = ["Crystal", "Bone", "Bark", "Glass", "Metal", "Stone",
             "Resin", "Amber", "Obsidian", "Quartz"]
ART_ADJS = ["Pulsing", "Dormant", "Luminous", "Resonant", "Humid",
            "Cold", "Warm", "Vibrating", "Silent", "Whispering"]
ART_SPECIMENS = ["Cell Culture", "Tissue Sample", "Soil Core", "Water Analysis",
                 "Mutant DNA", "Artifact Residue", "Anomaly Print", "Spore Print"]

CRAFT_ADJS = ["Raw", "Processed", "Scrap", "Filtered", "Refined",
              "Crude", "Synthetic", "Organic", "Salvaged", "Recovered"]
CRAFT_MATS = ["Wire", "Circuit Board", "Rubber", "Plastic", "Glass",
              "Metal Plate", "Fabric", "Leather", "Resin", "Solder",
              "Chemical Compound", "Adhesive", "Insulation", "Gasket",
              "Spring", "Bearing", "Tube", "Pipe", "Sheet Metal", "Epoxy"]

SPECIAL_ADJS = ["Unknown", "Sealed", "Marked", "Numbered", "Stamped",
                "Unclassified", "Anomalous", "Prototype"]
SPECIAL_ITEMS = ["Device", "Key", "Sample", "Beacon", "Relic",
                 "Instrument", "Container", "Module", "Core", "Chip"]
KEY_NAMES = ["Bunker 14 Access", "Depot 7", "Lab Wing", "Vault 3",
             "Gate Delta", "Archive", "Generator Room", "Observation"]

# Trade value ranges per category
TRADE_RANGES = {
    "Food":       {"scale": (2, 12), "cordon": (1, 8), "kafedra": (1, 6)},
    "Water":      {"scale": (2, 10), "cordon": (2, 8), "kafedra": (1, 5)},
    "Medical":    {"scale": (8, 30), "cordon": (6, 25), "kafedra": (10, 40)},
    "Weapon":     {"scale": (20, 60), "cordon": (15, 50), "kafedra": (10, 30)},
    "Ammunition": {"scale": (5, 15), "cordon": (8, 20), "kafedra": (3, 10)},
    "Tool":       {"scale": (5, 20), "cordon": (8, 15), "kafedra": (6, 15)},
    "Document":   {"scale": (20, 80), "cordon": (5, 20), "kafedra": (30, 90)},
    "Artifact":   {"scale": (50, 120), "cordon": (20, 60), "kafedra": (60, 150)},
    "Crafting":   {"scale": (2, 8), "cordon": (3, 10), "kafedra": (4, 12)},
    "Special":    {"scale": (30, 100), "cordon": (20, 80), "kafedra": (40, 120)},
}

# ─────────────────────────────────────────────
# GENERATION LOGIC
# ─────────────────────────────────────────────

def make_id(name):
    """Convert display name to asset ID."""
    return "item_" + name.lower().replace(" ", "_").replace(":", "").replace("-", "_").replace(".", "").replace("'", "").replace("#", "")

def pick(pool):
    return random.choice(pool)

def rand_range(lo, hi):
    return round(random.uniform(lo, hi), 2)

def rand_int(lo, hi):
    return random.randint(lo, hi)

def generate_item(template_tuple, category, pool_dict, idx):
    """Generate a single item from a template + pools."""
    if len(template_tuple) == 5:
        name_template, weight_range, _decay, tags, contam_or_flag = template_tuple
    else:
        name_template, weight_range, tags, contam_or_flag = template_tuple

    # Fill placeholders
    name = name_template
    for key, pool in pool_dict.items():
        name = name.replace("{" + key + "}", pick(pool))
    # Safety: fill any remaining placeholders
    while "{" in name:
        start = name.index("{")
        end = name.index("}")
        placeholder = name[start+1:end]
        name = name[:start] + pick(PROTEINS) + name[end+1:]

    # Avoid duplicates
    base_name = name
    name = f"{base_name}"

    item_id = make_id(name)
    weight = rand_range(*weight_range)
    tags_list = tags if isinstance(tags, list) else tags

    # Contamination
    if category in ("Artifact", "Special"):
        contaminated = contam_or_flag  # bool
        contamination = random.uniform(15, 60) if contaminated else 0
    elif isinstance(contam_or_flag, (int, float)) and contam_or_flag <= 1.0:
        contaminated = random.random() < contam_or_flag
        contamination = random.uniform(10, 40) if contaminated else 0
    else:
        contaminated = False
        contamination = 0

    trade = TRADE_RANGES[category]
    durability = random.choice([60, 70, 80, 90, 100])
    decay = rand_range(0.0, 0.5) if category in ("Food", "Water") else 0.0

    return {
        "id": item_id,
        "displayName": name,
        "category": category,
        "weightKg": weight,
        "durability": durability,
        "decayPerDay": decay,
        "utilityTags": tags_list,
        "radiationContaminated": contaminated,
        "radiationContaminationLevel": round(contamination, 1),
        "baseTradeValueScale": rand_int(*trade["scale"]),
        "baseTradeValueCordon": rand_int(*trade["cordon"]),
        "baseTradeValueKafedra": rand_int(*trade["kafedra"]),
    }

def generate_all_items():
    items = []
    seen_ids = set()
    counter = {"Food": 0, "Water": 0, "Medical": 0, "Weapon": 0,
               "Ammunition": 0, "Tool": 0, "Document": 0, "Artifact": 0,
               "Crafting": 0, "Special": 0}

    # Target counts per category
    targets = {
        "Food": 110, "Water": 50, "Medical": 80, "Weapon": 65,
        "Ammunition": 45, "Tool": 65, "Document": 65, "Artifact": 55,
        "Crafting": 55, "Special": 30,
    }

    # FOOD
    for t in FOOD_TEMPLATES:
        pool = {"protein": PROTEINS, "vegetable": VEGETABLES, "grain": GRAINS,
                "food_ration": FOOD_RATIONS, "forage": FORAGE, "bread": BREADS,
                "condiment": CONDIMENTS, "dairy": DAIRY, "juice": JUICES}
        for i in range(max(3, targets["Food"] // len(FOOD_TEMPLATES))):
            item = generate_item(t, "Food", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Food"] += 1

    # WATER
    for wtype in WATER_TYPES:
        for cont in WATER_CONTAINERS:
            name = f"{wtype} Water {cont}"
            item_id = make_id(name)
            if item_id not in seen_ids:
                seen_ids.add(item_id)
                contaminated = "Irradiated" in wtype or "Contaminated" in wtype
                items.append({
                    "id": item_id, "displayName": name, "category": "Water",
                    "weightKg": round(random.uniform(0.5, 2.0), 2),
                    "durability": 100, "decayPerDay": 0.0,
                    "utilityTags": ["Drink"],
                    "radiationContaminated": contaminated,
                    "radiationContaminationLevel": round(random.uniform(15, 45), 1) if contaminated else 0,
                    "baseTradeValueScale": rand_int(2, 10),
                    "baseTradeValueCordon": rand_int(2, 8),
                    "baseTradeValueKafedra": rand_int(1, 5),
                })
                counter["Water"] += 1
    # Extra bad water
    for wtype in WATER_SPECIAL:
        for cont in random.sample(WATER_CONTAINERS, 3):
            name = f"{wtype} Water {cont}"
            item_id = make_id(name)
            if item_id not in seen_ids:
                seen_ids.add(item_id)
                items.append({
                    "id": item_id, "displayName": name, "category": "Water",
                    "weightKg": round(random.uniform(0.5, 2.0), 2),
                    "durability": 100, "decayPerDay": 0.0,
                    "utilityTags": ["Drink"],
                    "radiationContaminated": True,
                    "radiationContaminationLevel": round(random.uniform(20, 60), 1),
                    "baseTradeValueScale": rand_int(1, 5),
                    "baseTradeValueCordon": rand_int(1, 4),
                    "baseTradeValueKafedra": rand_int(1, 3),
                })
                counter["Water"] += 1

    # MEDICAL
    for t in MEDICAL_TEMPLATES:
        pool = {"med_type": MED_TYPES, "med_inject": MED_INJECTS, "med_pill": MED_PILLS}
        for i in range(max(4, targets["Medical"] // len(MEDICAL_TEMPLATES))):
            item = generate_item(t, "Medical", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Medical"] += 1

    # WEAPON
    for t in WEAPON_TEMPLATES:
        pool = {"caliber": CALIBERS, "gun_type": GUN_TYPES, "melee": MELEES}
        for i in range(max(4, targets["Weapon"] // len(WEAPON_TEMPLATES))):
            # For weapons, contam_or_flag = is_melee or is_electrified bool
            template = (t[0], t[1], t[2])  # strip the bool
            item = generate_item(template + (t[3],), "Weapon", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Weapon"] += 1

    # AMMUNITION
    for t in AMMO_TEMPLATES:
        pool = {"caliber": CALIBERS}
        for i in range(max(4, targets["Ammunition"] // len(AMMO_TEMPLATES))):
            template = (t[0], t[1], t[2])
            item = generate_item(template + (False,), "Ammunition", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Ammunition"] += 1

    # TOOL
    for t in TOOL_TEMPLATES:
        pool = {"tool_adj": TOOL_ADJS, "tool": TOOLS, "electronic": ELECTRONICS}
        for i in range(max(4, targets["Tool"] // len(TOOL_TEMPLATES))):
            template = (t[0], t[1], t[2])
            item = generate_item(template + (t[3],), "Tool", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Tool"] += 1

    # DOCUMENT
    for t in DOC_TEMPLATES:
        pool = {"doc_adj": DOC_ADJS, "doc_type": DOC_TYPES,
                "doc_subject": DOC_SUBJECTS, "faction_adj": FACTION_ADJS}
        for i in range(max(4, targets["Document"] // len(DOC_TEMPLATES))):
            template = (t[0], t[1], t[2])
            item = generate_item(template + (t[3],), "Document", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Document"] += 1

    # ARTIFACT
    for t in ARTIFACT_TEMPLATES:
        pool = {"art_name": ART_NAMES, "art_frag": ART_FRAGS,
                "art_adj": ART_ADJS, "art_specimen": ART_SPECIMENS}
        for i in range(max(6, targets["Artifact"] // len(ARTIFACT_TEMPLATES))):
            item = generate_item(t, "Artifact", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Artifact"] += 1

    # CRAFTING
    for t in CRAFTING_TEMPLATES:
        pool = {"craft_adj": CRAFT_ADJS, "craft_mat": CRAFT_MATS}
        for i in range(max(4, targets["Crafting"] // len(CRAFTING_TEMPLATES))):
            template = (t[0], t[1], t[2])
            item = generate_item(template + (t[3],), "Crafting", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Crafting"] += 1

    # SPECIAL
    for t in SPECIAL_TEMPLATES:
        pool = {"special_adj": SPECIAL_ADJS, "special_item": SPECIAL_ITEMS, "key_name": KEY_NAMES}
        for i in range(max(5, targets["Special"] // len(SPECIAL_TEMPLATES))):
            template = (t[0], t[1], t[2])
            item = generate_item(template + (t[3],), "Special", pool, i)
            if item["id"] not in seen_ids:
                seen_ids.add(item["id"])
                items.append(item)
                counter["Special"] += 1

    # Fill shortfalls with variants
    for cat, target in targets.items():
        current = counter[cat]
        if current < target:
            shortfall = target - current
            for i in range(shortfall):
                variant_name = f"Issued {cat[:-1] if cat.endswith('ing') else cat} Variant {i+1}"
                item_id = make_id(variant_name)
                if item_id not in seen_ids:
                    seen_ids.add(item_id)
                    trade = TRADE_RANGES[cat]
                    items.append({
                        "id": item_id, "displayName": variant_name, "category": cat,
                        "weightKg": round(random.uniform(0.1, 1.0), 2),
                        "durability": random.choice([70, 80, 90, 100]),
                        "decayPerDay": round(random.uniform(0, 0.5), 2) if cat in ("Food", "Water") else 0,
                        "utilityTags": ["Trade"],
                        "radiationContaminated": False,
                        "radiationContaminationLevel": 0,
                        "baseTradeValueScale": rand_int(*trade["scale"]),
                        "baseTradeValueCordon": rand_int(*trade["cordon"]),
                        "baseTradeValueKafedra": rand_int(*trade["kafedra"]),
                    })
                    counter[cat] += 1

    return items, counter

def main():
    items, counts = generate_all_items()
    total = len(items)

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    for item in items:
        filename = f"{item['id']}.json"
        filepath = os.path.join(OUTPUT_DIR, filename)
        with open(filepath, "w", encoding="utf-8") as f:
            json.dump(item, f, indent=2, ensure_ascii=False)

    print(f"\n=== ITEM GENERATION COMPLETE ===")
    print(f"Total items: {total}")
    print(f"Output: {OUTPUT_DIR}")
    for cat, count in sorted(counts.items()):
        print(f"  {cat}: {count}")

if __name__ == "__main__":
    main()
