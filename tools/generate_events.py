"""
Oblast Zero — Event Content Generator
Generates 1000+ expedition events as JSON for Assets/Data/Resources/Events/
Post-administrative bureaucratic horror. Three factions + anomalies + bunker life.
"""
import json
import os
import random

random.seed(2026)

OUTPUT_DIR = os.path.normpath(os.path.join(
    os.path.dirname(__file__), "..", "Assets", "Data", "Resources", "Events"
))

# ============================================================
# FACTIONS & TONE
# ============================================================

FACTIONS = {
    "ScaleSociety": {
        "tags": ["bureaucratic", "census", "registration", "demographic", "actuarial", "administrative"],
        "colors": ["grey", "beige", "faded"],
        "actors": ["clerk", "auditor", "registrar", "inspector", "archivist", "actuary", "surveyor"],
        "verbs": ["registers", "files", "stamps", "records", "catalogues", "processes", "audits"],
        "objects": ["folder", "dossier", "clipboard", "stamp", "form", "seal", "ledger", "triplicate", "carbon copy"],
    },
    "Cordon": {
        "tags": ["military", "interdiction", "patrol", "containment", "perimeter", "orders"],
        "colors": ["olive", "faded green", "khaki"],
        "actors": ["soldier", "corporal", "sergeant", "lieutenant", "sentry", "radio operator"],
        "verbs": ["patrols", "interdicts", "scans", "detains", "confiscates", "clears"],
        "objects": ["rifle", "radio", "binoculars", "ID badge", "map", "rucksack", "ammunition"],
    },
    "Kafedra": {
        "tags": ["scientific", "specimen", "adaptation", "modification", "devotional", "laboratory"],
        "colors": ["bone", "pale", "ivory"],
        "actors": ["researcher", "technician", "specimen", "devotee", "observer", "collector"],
        "verbs": ["measures", "catalogues", "samples", "observes", "extracts", "documents"],
        "objects": ["vial", "specimen jar", "notebook", "microscope", "electrode", "tape recorder"],
    },
}

ANOMALY_TYPES = ["gravitational", "thermal", "electrical", "temporal", "spatial",
                 "radiological", "biological", "acoustic", "optical", "chemical"]

BUNKER_THEMES = ["rationing", "maintenance", "crew_conflict", "sanity", "illness",
                 "equipment_failure", "discovery", "memory", "dream", "silence",
                 "noise", "darkness", "light", "cold", "heat"]

REGION_TAGS = ["bunker_interior", "access_road", "perimeter", "old_factory",
               "drainage_tunnel", "abandoned_school", "forest_edge", "riverbank",
               "collapsed_building", "transmission_tower", "railway_siding",
               "storage_depot", "kitchen_block", "basement_corridor"]

# ============================================================
# NARRATIVE FRAGMENTS (tone-locked, modular)
# ============================================================

# --- Scale Society scenes ---
SCALE_OPENERS = [
    "A grey sedan idles at the access road. Two {actor}s. The senior — registration {reg_id} — presents a folder.",
    "The quarterly audit arrives on schedule. A {actor} in a {color} suit sets a briefcase on the bunker table.",
    "A courier delivers a sealed envelope bearing the {color} seal of the Scale Society. No return address.",
    "The census office sends a replacement form. Form {form_id}, subsection {subsection}. The old one was 'misfiled'.",
    "A {actor} requests permission to install a demographic monitoring device on the bunker's exterior wall.",
    "Registration {reg_id} returns. This time with an assistant and a folding table.",
    "The monthly actuarial report arrives. Your bunker's survival probability has been revised.",
    "A {actor} from the regional office arrives with two folders. One is for you. The other is sealed.",
    "The Society's {actor} conducts a routine inspection of stored provisions.",
    "A transfer order arrives from the central office. Personnel relocation, effective immediately.",
    "The {actor} reads from a ledger. Your bunker's entries do not match their records.",
    "A grey envelope slides under the door. Inside: Form {form_id}, revision {revision}. Sign and return.",
    "The demographic survey team returns. They bring a third member this time.",
    "A {actor} arrives carrying an adding machine and a stack of forms.",
    "The society dispatches a team to verify reported personnel counts.",
]

# --- Cordon scenes ---
CORDON_OPENERS = [
    "A patrol from the 14th Regiment appears on the perimeter road. Three soldiers, standard formation.",
    "A Cordon radio crackles at dawn. Frequency {freq}. A voice requests bunker identification.",
    "Two soldiers set up an observation post on the ridge overlooking the bunker.",
    "A {actor} from the 14th Regiment approaches with a clipboard and a sidearm.",
    "The patrol returns. Same route, same timing. This time they stop at the gate.",
    "A convoy passes on the access road. Military plates, {color} paint. They do not stop.",
    "A Cordon {actor} requests access to the bunker's stored provisions for 'inspection'.",
    "The regiment dispatches a decontamination team. Standard protocol, they say.",
    "A soldier leaves a sealed package at the perimeter fence. No note.",
    "Radio contact from the 14th: a curfew is in effect for all civilian structures in sector {sector}.",
    "A {actor} approaches the bunker under a white flag. Standard identification procedures.",
    "The Cordon {actor} sets up a checkpoint at the access road. All movement restricted.",
    "An officer from the 14th arrives with documents requiring countersignature.",
    "A patrol reports anomalous readings near the bunker. They request cooperation.",
    "The sentry at the perimeter road signals: military vehicle approaching.",
]

# --- Kafedra scenes ---
KAFEDRA_OPENERS = [
    "A {actor} from the Kafedra arrives carrying a specimen case lined with {color} cloth.",
    "The Kafedra sends a team to collect environmental samples near the bunker.",
    "A {color}-robed figure approaches. They carry a small glass jar sealed with wax.",
    "The Kafedra's {actor} requests tissue samples from bunker personnel. Voluntary, they emphasize.",
    "A researcher arrives with a portable microscope and a questionnaire about recent dreams.",
    "The Kafedra dispatches a {actor} to measure anomalous readings near the bunker foundation.",
    "A specimen collector arrives. They have your bunker's file — it is thick.",
    "The {actor} presents a vial. 'For mutual benefit,' they say. 'A sample of the adaptation process.'",
    "A Kafedra observer requests 72 hours of access to the bunker's interior. They bring their own equipment.",
    "The Kafedra sends a gift: a sealed container of {color} liquid. No instructions.",
    "A {actor} arrives with electrode pads and a notebook full of questions about sleep patterns.",
    "The Kafedra's {actor} requests permission to install a monitoring device in the bunker's main room.",
    "A researcher arrives with photographs of local flora. They ask if you've noticed changes.",
    "The Kafedra dispatches a team to study a reading anomaly near the bunker walls.",
    "A {actor} arrives carrying a tape recorder and a list of 47 questions.",
]

# --- Anomaly scenes ---
ANOMALY_OPENERS = [
    "The Geiger counter reads {geiger} microsieverts. The anomaly has shifted position overnight.",
    "A {type} anomaly pulses near the access road. The crew reports {symptom}.",
    "The anomaly field expands. Three new readings where yesterday there were none.",
    "A {type} distortion ripples across the bunker's eastern wall. Temperature drops by {temp}°C.",
    "The crew hears a sound. Low, rhythmic. It stops when someone approaches the source.",
    "An anomaly appears inside the bunker perimeter. It was not there this morning.",
    "The {type} anomaly emits a {color} light. It pulses at exactly {freq} Hz.",
    "A section of the corridor is wrong. The geometry does not add up.",
    "The anomaly speaks. Or something speaks through it. The words are in a language no one recognizes.",
    "An object from outside the bunker is now inside. No one moved it.",
    "The temporal anomaly repeats the same 40 seconds. The crew experiences it as déjà vu.",
    "A {type} field covers the drainage tunnel exit. The readings are off the scale.",
    "The bunker's instruments detect a new anomaly signature. It matches nothing in the database.",
    "Anomaly activity peaks at {time}. The bunker's lights flicker in response.",
    "The spatial anomaly warps the access corridor. Doors open to the wrong rooms.",
]

# --- Bunker life scenes ---
BUNKER_OPENERS = [
    "The ration stores are lower than expected. Someone has been taking more than their share.",
    "A pipe bursts in the lower corridor. Water pools around stored equipment.",
    "The generator sputters and dies. Fuel reserves at {fuel}%.",
    "A crew member reports nightmares. Vivid, recurring. The same {color} door opening.",
    "The water filtration system requires maintenance. The filter is {condition}.",
    "Someone has written on the bunker wall. The message is in a language no one taught them.",
    "The radio picks up a signal. A voice recites numbers. Then silence.",
    "A crew member disappears for six hours. They return with no memory of where they went.",
    "The bunker's structural supports creak. A new crack appears in the ceiling.",
    "Someone finds a storage room that was not on the bunker's original plans.",
    "The crew argues about rationing. The argument becomes personal.",
    "A crew member falls ill. Symptoms match nothing in the medical reference.",
    "The lights go out for eleven minutes. When they return, the furniture has been rearranged.",
    "A crew member reports seeing a figure in the corridor. It was not a crew member.",
    "The bunker's temperature drops without warning. The heating system shows no faults.",
]

# ============================================================
# CHOICE TEMPLATES
# ============================================================

CHOICE_PATTERNS = {
    "ScaleSociety": [
        ("{submit}", 1.0, {"sanityDelta": -4, "reputationDelta": 12, "reputationFaction": "ScaleSociety"}, {}),
        ("{decline}", 0.65, {"sanityDelta": -8, "reputationDelta": -8, "reputationFaction": "ScaleSociety"},
         {"sanityDelta": -15, "reputationDelta": -20, "reputationFaction": "ScaleSociety"}),
        ("{resist}", 0.5, {"sanityDelta": 0, "reputationDelta": -30, "reputationFaction": "ScaleSociety"},
         {"healthDelta": -25, "reputationDelta": -35, "reputationFaction": "ScaleSociety"}),
    ],
    "Cordon": [
        ("{cooperate}", 1.0, {"sanityDelta": -2, "fatigueDelta": 5, "reputationDelta": 10, "reputationFaction": "Cordon"}, {}),
        ("{delay}", 0.6, {"sanityDelta": -5, "reputationDelta": -5, "reputationFaction": "Cordon"},
         {"fatigueDelta": 15, "reputationDelta": -15, "reputationFaction": "Cordon"}),
        ("{evade}", 0.45, {"reputationDelta": -25, "reputationFaction": "Cordon"},
         {"healthDelta": -15, "radiationDelta": 5, "reputationDelta": -30, "reputationFaction": "Cordon"}),
    ],
    "Kafedra": [
        ("{accept}", 0.85, {"sanityDelta": -6, "radiationDelta": 3, "reputationDelta": 15, "reputationFaction": "Kafedra"},
         {"radiationDelta": 8, "reputationDelta": -5, "reputationFaction": "Kafedra"}),
        ("{negotiate}", 0.55, {"sanityDelta": -3, "reputationDelta": 5, "reputationFaction": "Kafedra"},
         {"sanityDelta": -10, "reputationDelta": -10, "reputationFaction": "Kafedra"}),
        ("{refuse}", 0.7, {"reputationDelta": -20, "reputationFaction": "Kafedra"},
         {"sanityDelta": -8, "radiationDelta": 5, "reputationDelta": -25, "reputationFaction": "Kafedra"}),
    ],
    "Anomaly": [
        ("{investigate}", 0.6, {"sanityDelta": -5, "radiationDelta": 5, "reputationDelta": 0},
         {"healthDelta": -20, "radiationDelta": 15, "sanityDelta": -10}),
        ("{avoid}", 0.9, {"fatigueDelta": 5, "sanityDelta": -2},
         {"fatigueDelta": 10, "sanityDelta": -5}),
        ("{contain}", 0.45, {"fatigueDelta": 10, "sanityDelta": -3},
         {"healthDelta": -10, "radiationDelta": 8, "sanityDelta": -8}),
    ],
    "Bunker": [
        ("{fix}", 0.7, {"fatigueDelta": 8, "sanityDelta": -2},
         {"fatigueDelta": 15, "sanityDelta": -5, "healthDelta": -5}),
        ("{ignore}", 0.95, {"sanityDelta": -3},
         {"sanityDelta": -8, "healthDelta": -5}),
        ("{delegate}", 0.55, {"sanityDelta": 0, "fatigueDelta": 2},
         {"sanityDelta": -10, "fatigueDelta": 5}),
    ],
}

SUBMIT_OPTIONS = [
    "Submit the registration as requested.",
    "Sign the forms and return them.",
    "Comply with the audit requirements.",
    "Provide the requested documentation.",
    "Accept the revised assessment.",
]
DECLINE_OPTIONS = [
    "Decline, citing an absence of countersignature.",
    "Request proper authorization documents.",
    "Refuse on procedural grounds.",
    "Return the forms unsigned.",
    "Cite jurisdictional limitations.",
]
RESIST_OPTIONS = [
    "Turn them away at the perimeter. [Steady Hands]",
    "Confront the auditors directly. [Combat]",
    "Destroy the forms in their presence.",
    "Lock the bunker and wait them out.",
    "Threaten to contact the Cordon instead.",
]
COOPERATE_OPTIONS = [
    "Cooperate with the inspection.",
    "Allow the patrol entry.",
    "Provide the requested information.",
    "Submit to standard procedures.",
    "Follow military protocol.",
]
DELAY_OPTIONS = [
    "Stall for time.",
    "Request a formal appointment.",
    "Claim the bunker is sealed for decontamination.",
    "Direct them to a different authority.",
    "Plead illness.",
]
EVADE_OPTIONS = [
    "Evade the patrol through the drainage tunnels.",
    "Hide personnel in the sub-basement.",
    "Create a diversion.",
    "Use the secondary exit.",
    "Black out all light and remain silent.",
]
ACCEPT_OPTIONS = [
    "Accept the Kafedra's terms.",
    "Submit to the sampling procedure.",
    "Allow installation of monitoring equipment.",
    "Take the offered substance.",
    "Cooperate with the research protocol.",
]
NEGOTIATE_OPTIONS = [
    "Negotiate modified terms. [Charisma]",
    "Request time to consider.",
    "Counter-propose an alternative arrangement.",
    "Demand compensation for cooperation.",
    "Seek a third party as witness.",
]
REFUSE_OPTIONS = [
    "Refuse all contact with the Kafedra.",
    "Bar them from the bunker.",
    "Destroy their equipment.",
    "Report them to the Cordon.",
    "Silently ignore their presence.",
]
INVESTIGATE_OPTIONS = [
    "Investigate the anomaly directly.",
    "Send a crew member to take readings.",
    "Approach with decontamination gear.",
    "Use equipment to analyze from a distance.",
    "Enter the anomaly field.",
]
AVOID_OPTIONS = [
    "Avoid the anomaly entirely.",
    "Seal off the affected area.",
    "Reroute through alternative passages.",
    "Wait for the anomaly to dissipate.",
    "Mark it and move on.",
]
CONTAIN_OPTIONS = [
    "Attempt to contain the anomaly. [Technical]",
    "Build a barrier around it.",
    "Use equipment to dampen its effects.",
    "Isolate the affected zone.",
    "Experiment with countermeasures.",
]
FIX_OPTIONS = [
    "Repair it immediately.",
    "Rig a temporary solution.",
    "Replace the damaged component.",
    "Improvise with available materials.",
    "Follow the maintenance manual.",
]
IGNORE_OPTIONS = [
    "Ignore the problem.",
    "Hope it resolves itself.",
    "Prioritize other concerns.",
    "Accept the degradation.",
    "Wait and observe.",
]
DELEGATE_OPTIONS = [
    "Assign it to a crew member.",
    "Form a maintenance team.",
    "Trade for outside help.",
    "Request assistance from a faction.",
    "Hold a vote on who handles it.",
]

# ============================================================
# HELPERS
# ============================================================

def pick(pool):
    return random.choice(pool)

def rand_int(lo, hi):
    return random.randint(lo, hi)

def make_event_id(category, idx):
    return f"evt_{category.lower()}_{idx:04d}"

def generate_narrative(opener, faction_key):
    """Fill template placeholders with faction-appropriate values."""
    if faction_key in FACTIONS:
        f = FACTIONS[faction_key]
        replacements = {
            "{actor}": pick(f["actors"]),
            "{color}": pick(f["colors"]),
            "{reg_id}": str(rand_int(1000, 9999)),
            "{form_id}": str(rand_int(100, 999)),
            "{subsection}": f"{random.choice('ABCDEFGH')}.{rand_int(1,12)}",
            "{revision}": str(rand_int(1, 20)),
            "{freq}": f"{rand_int(80, 160)}.{rand_int(0,9)}",
            "{sector}": f"{random.choice('ABCDEFGH')}-{rand_int(1,9)}",
            "{time}": f"{rand_int(0,23):02d}:{rand_int(0,59):02d}",
        }
    else:  # anomaly/bunker
        replacements = {
            "{geiger}": str(rand_int(50, 5000)),
            "{type}": pick(ANOMALY_TYPES),
            "{symptom}": pick(["headaches", "visual static", "time distortion", "auditory hallucinations", "nausea", "skin irritation", "memory lapses"]),
            "{temp}": str(rand_int(5, 25)),
            "{color}": pick(["bone", "grey", "pale blue", "sickly green", "white", "violet"]),
            "{freq}": str(rand_int(1, 120)),
            "{time}": f"{rand_int(0,23):02d}:{rand_int(0,59):02d}",
            "{fuel}": str(rand_int(5, 40)),
            "{condition}": pick(["degraded", "contaminated", "near-failure", "missing", "rusted through"]),
        }

    text = opener
    for k, v in replacements.items():
        text = text.replace(k, v)
    return text

def make_outcome(delta_dict, items=None):
    outcome = {
        "sanityDelta": delta_dict.get("sanityDelta", 0),
        "fatigueDelta": delta_dict.get("fatigueDelta", 0),
        "radiationDelta": delta_dict.get("radiationDelta", 0),
        "healthDelta": delta_dict.get("healthDelta", 0),
        "reputationFaction": delta_dict.get("reputationFaction", ""),
        "reputationDelta": delta_dict.get("reputationDelta", 0),
        "crewDeathChance": delta_dict.get("crewDeathChance", 0.0),
        "followUpEventId": delta_dict.get("followUpEventId", ""),
    }
    return outcome

def generate_event(category, idx):
    """Generate a single event."""
    event_id = make_event_id(category, idx)

    # Pick opener and generate narrative
    if category == "ScaleSociety":
        opener = pick(SCALE_OPENERS)
        narrative = generate_narrative(opener, "ScaleSociety")
        faction_context = "ScaleSociety"
        region_pool = ["access_road", "bunker_interior", "perimeter"]
        choice_key = "ScaleSociety"
        choice_labels = [pick(SUBMIT_OPTIONS), pick(DECLINE_OPTIONS), pick(RESIST_OPTIONS)]

    elif category == "Cordon":
        opener = pick(CORDON_OPENERS)
        narrative = generate_narrative(opener, "Cordon")
        faction_context = "Cordon"
        region_pool = ["perimeter", "access_road", "forest_edge"]
        choice_key = "Cordon"
        choice_labels = [pick(COOPERATE_OPTIONS), pick(DELAY_OPTIONS), pick(EVADE_OPTIONS)]

    elif category == "Kafedra":
        opener = pick(KAFEDRA_OPENERS)
        narrative = generate_narrative(opener, "Kafedra")
        faction_context = "Kafedra"
        region_pool = ["bunker_interior", "old_factory", "abandoned_school"]
        choice_key = "Kafedra"
        choice_labels = [pick(ACCEPT_OPTIONS), pick(NEGOTIATE_OPTIONS), pick(REFUSE_OPTIONS)]

    elif category == "Anomaly":
        opener = pick(ANOMALY_OPENERS)
        narrative = generate_narrative(opener, "Anomaly")
        faction_context = ""
        region_pool = ["drainage_tunnel", "collapsed_building", "forest_edge", "perimeter"]
        choice_key = "Anomaly"
        choice_labels = [pick(INVESTIGATE_OPTIONS), pick(AVOID_OPTIONS), pick(CONTAIN_OPTIONS)]

    else:  # Bunker
        opener = pick(BUNKER_OPENERS)
        narrative = generate_narrative(opener, "Bunker")
        faction_context = ""
        region_pool = ["bunker_interior", "kitchen_block", "basement_corridor"]
        choice_key = "Bunker"
        choice_labels = [pick(FIX_OPTIONS), pick(IGNORE_OPTIONS), pick(DELEGATE_OPTIONS)]

    # Prerequisites
    min_day = rand_int(0, 5) if category == "Bunker" else rand_int(1, 10)
    max_day = rand_int(20, 80)
    min_rep = rand_int(-40, -10) if faction_context else -100
    max_rep = rand_int(20, 60) if faction_context else 100
    region_tags = random.sample(region_pool, min(2, len(region_pool)))

    # Choices
    patterns = CHOICE_PATTERNS[choice_key]
    choices = []
    for i, (label_template, success_chance, success_delta, failure_delta) in enumerate(patterns):
        success = make_outcome(success_delta)
        failure = make_outcome(failure_delta) if failure_delta else {}

        choice = {
            "choiceLabel": choice_labels[i] if i < len(choice_labels) else f"Option {i+1}",
            "successChance": success_chance,
            "requiredTraitsAny": [],
            "blockedByTraits": [],
            "successOutcome": success,
            "failureOutcome": failure,
        }

        # Add trait requirements for some choices
        if i == 2 and random.random() < 0.4:
            trait = pick(["trait_steady_hands", "trait_iron_stomach", "trait_claustrophobic"])
            choice["requiredTraitsAny"] = [trait]

        choices.append(choice)

    event = {
        "id": event_id,
        "title": narrative.split(".")[0] if "." in narrative else narrative[:60],
        "narrativeText": narrative,
        "prerequisites": {
            "minDay": min_day,
            "maxDay": max_day,
            "factionContext": faction_context,
            "minFactionRep": min_rep,
            "maxFactionRep": max_rep,
            "regionTagsAny": region_tags,
        },
        "baseWeight": round(random.uniform(0.5, 2.0), 2),
        "choices": choices,
    }

    return event


def main():
    """Generate 1000+ events across all categories."""
    targets = {
        "ScaleSociety": 220,
        "Cordon": 220,
        "Kafedra": 180,
        "Anomaly": 180,
        "Bunker": 220,
    }

    os.makedirs(OUTPUT_DIR, exist_ok=True)

    total = 0
    counts = {}

    for category, count in targets.items():
        counts[category] = 0
        for idx in range(count):
            event = generate_event(category, idx)
            filename = f"{event['id']}.json"
            filepath = os.path.join(OUTPUT_DIR, filename)
            with open(filepath, "w", encoding="utf-8") as f:
                json.dump(event, f, indent=2, ensure_ascii=False)
            counts[category] += 1
            total += 1

    print(f"\n=== EVENT GENERATION COMPLETE ===")
    print(f"Total events: {total}")
    print(f"Output: {OUTPUT_DIR}")
    for cat, count in sorted(counts.items()):
        print(f"  {cat}: {count}")


if __name__ == "__main__":
    main()
