// Assets/_Project/Scripts/Editor/OblastZeroContentSeeder.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using OblastZero.Data;

namespace OblastZero.EditorTools
{
    /// <summary>
    /// One-click starter content for Oblast Zero. Generates lore-accurate seed assets (the three factions
    /// from bible §3, a cross-category item set, a small crew, a few traits), drops them under
    /// Assets/Data/Definitions/, then creates/refreshes the GameDatabase at Assets/Data/GameDatabase.asset
    /// and wires every list. Re-running is safe: existing assets are loaded and updated, not duplicated.
    ///
    /// Run via the menu: OblastZero → Seed Starter Content.
    /// </summary>
    public static class OblastZeroContentSeeder
    {
        private const string DataRoot = "Assets/Data";
        private const string DefRoot = "Assets/Data/Definitions";

        [MenuItem("OblastZero/Seed Starter Content")]
        public static void Seed()
        {
            EnsureFolders();

            var traits = SeedTraits();
            var items = SeedItems();
            var crew = SeedCrew(traits);
            var factions = SeedFactions(items);

            var db = CreateOrLoad<GameDatabase>($"{DataRoot}/GameDatabase.asset");
            db.items = items;
            db.crew = crew;
            db.traits = traits;
            db.factions = factions;
            db.voiceGroups = new List<VoiceLineGroup>();
            db.anomalies = new List<AnomalyData>();
            db.mutants = new List<MutantData>();
            db.events = new List<ExpeditionEventData>();
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            db.Initialize(force: true);

            Debug.Log($"[Seeder] Done. Seeded {items.Count} items, {crew.Count} crew, {traits.Count} traits, " +
                      $"{factions.Count} factions. GameDatabase at {DataRoot}/GameDatabase.asset.");
            Selection.activeObject = db;
            EditorGUIUtility.PingObject(db);
        }

        // ---- Traits ----

        private static List<TraitData> SeedTraits()
        {
            var list = new List<TraitData>();

            var steady = CreateOrLoad<TraitData>($"{DefRoot}/Traits/Trait_SteadyHands.asset");
            steady.id = "trait_steady_hands";
            steady.displayName = "Steady Hands";
            steady.description = "Years on the line. The shakes never come at the wrong moment.";
            steady.isAffliction = false;
            steady.modifiers = new CrewStatModifiers { combatResolutionBonus = 0.20f };
            steady.behaviorTags = new List<string> { "steady_hands" };
            EditorUtility.SetDirty(steady);
            list.Add(steady);

            var claustrophobic = CreateOrLoad<TraitData>($"{DefRoot}/Traits/Trait_Claustrophobic.asset");
            claustrophobic.id = "trait_claustrophobic";
            claustrophobic.displayName = "Claustrophobic";
            claustrophobic.description = "The bunker walls sit closer on some days than others.";
            claustrophobic.isAffliction = true;
            claustrophobic.modifiers = new CrewStatModifiers { sanityRecoveryBonus = -0.25f };
            claustrophobic.behaviorTags = new List<string> { "claustrophobic" };
            EditorUtility.SetDirty(claustrophobic);
            list.Add(claustrophobic);

            var ironStomach = CreateOrLoad<TraitData>($"{DefRoot}/Traits/Trait_IronStomach.asset");
            ironStomach.id = "trait_iron_stomach";
            ironStomach.displayName = "Iron Stomach";
            ironStomach.description = "Eats what the others will not. Holds it down.";
            ironStomach.isAffliction = false;
            ironStomach.modifiers = new CrewStatModifiers { radiationResistanceBonus = 0.20f };
            ironStomach.behaviorTags = new List<string> { "iron_stomach" };
            EditorUtility.SetDirty(ironStomach);
            list.Add(ironStomach);

            return list;
        }

        // ---- Items ----

        private static List<ItemData> SeedItems()
        {
            var list = new List<ItemData>
            {
                MakeItem("Item_CannedMeat", "item_canned_meat", "Canned Meat", ItemCategory.Food,
                    0.4f, 100, 2f, new[] { UtilityTag.Eat, UtilityTag.Trade }, false, 0f, 4, 1, 2),
                MakeItem("Item_WaterFlask", "item_water_flask", "Water Flask", ItemCategory.Water,
                    1.0f, 100, 0f, new[] { UtilityTag.Drink }, false, 0f, 3, 1, 2),
                MakeItem("Item_Bandage", "item_bandage", "Field Bandage", ItemCategory.Medical,
                    0.1f, 100, 0f, new[] { UtilityTag.Heal }, false, 0f, 6, 2, 3),
                MakeItem("Item_ServicePistol", "item_service_pistol", "9mm Service Pistol", ItemCategory.Weapon,
                    0.8f, 100, 0f, new[] { UtilityTag.Fight, UtilityTag.Defend, UtilityTag.Trade }, false, 0f, 30, 40, 10),
                MakeItem("Item_PistolAmmo", "item_pistol_ammo", "9mm Rounds", ItemCategory.Ammunition,
                    0.02f, 100, 0f, new[] { UtilityTag.Fight }, false, 0f, 8, 10, 4),
                MakeItem("Item_PryBar", "item_pry_bar", "Pry Bar", ItemCategory.Tool,
                    2.0f, 100, 0f, new[] { UtilityTag.Repair, UtilityTag.Fight }, false, 0f, 12, 12, 6),
                MakeItem("Item_BureauDossier", "item_bureau_dossier", "Pre-Incident Bureau Dossier", ItemCategory.Document,
                    0.2f, 100, 0f, new[] { UtilityTag.Read, UtilityTag.Trade }, false, 0f, 50, 5, 55),
                MakeItem("Item_ArtifactBallast", "item_artifact_ballast", "Artifact: Ballast", ItemCategory.Artifact,
                    0.6f, 100, 0f, new[] { UtilityTag.Trade, UtilityTag.Ritual }, true, 35f, 80, 40, 90),
            };
            return list;
        }

        private static ItemData MakeItem(string file, string id, string name, ItemCategory category,
            float weightKg, int durability, float decayPerDay, UtilityTag[] tags,
            bool contaminated, float contamination, int valueScale, int valueCordon, int valueKafedra)
        {
            var item = CreateOrLoad<ItemData>($"{DefRoot}/Items/{file}.asset");
            item.id = id;
            item.displayName = name;
            item.category = category;
            item.weightKg = weightKg;
            item.durability = durability;
            item.decayPerDay = decayPerDay;
            item.utilityTags = new List<UtilityTag>(tags);
            item.radiationContaminated = contaminated;
            item.radiationContaminationLevel = contamination;
            item.baseTradeValueScale = valueScale;
            item.baseTradeValueCordon = valueCordon;
            item.baseTradeValueKafedra = valueKafedra;
            EditorUtility.SetDirty(item);
            return item;
        }

        // ---- Crew (note: per bible §3.1, crew can survive the Scale Society but cannot be ex-Society) ----

        private static List<CrewMemberData> SeedCrew(List<TraitData> traits)
        {
            TraitData Trait(string id) => traits.Find(t => t.id == id);

            var list = new List<CrewMemberData>
            {
                MakeCrew("Crew_Marina", "crew_marina", "Marina", "Marina", "Volkova", "Andreevna",
                    CrewBackground.FieldMedic, 90, 100, 22f, 1.3f, 1.0f, 0.9f,
                    "A field medic who stayed after the others left. She keeps a list of everyone she could not save.",
                    new[] { Trait("trait_steady_hands") }),

                MakeCrew("Crew_Yuri", "crew_yuri", "Yuri", "Yuri", "Lebedev", "Ignatevich",
                    CrewBackground.ExCordonSoldier, 100, 80, 28f, 0.9f, 1.1f, 1.3f,
                    "Walked away from a garrison still following its 1981 orders. He does not talk about it, and he still cleans his rifle every night.",
                    new[] { Trait("trait_steady_hands") }),

                MakeCrew("Crew_Sasha", "crew_sasha", "Sasha", "Aleksandr", "Morozov", "Pavlovich",
                    CrewBackground.LonerScavenger, 95, 85, 34f, 1.0f, 1.0f, 1.0f,
                    "A loner who knows every drainage culvert and collapsed stairwell in the cordon. Trusts the Oblast more than the people in it.",
                    new[] { Trait("trait_iron_stomach"), Trait("trait_claustrophobic") }),
            };
            return list;
        }

        private static CrewMemberData MakeCrew(string file, string id, string display, string first, string last,
            string patronymic, CrewBackground background, int maxHealth, int maxSanity, float carryKg,
            float sanityRecovery, float radiationResistance, float combat, string backstory, TraitData[] startingTraits)
        {
            var c = CreateOrLoad<CrewMemberData>($"{DefRoot}/Crew/{file}.asset");
            c.id = id;
            c.displayName = display;
            c.firstName = first;
            c.lastName = last;
            c.patronymic = patronymic;
            c.background = background;
            c.baseStats = new CrewBaseStats
            {
                maxHealth = Mathf.Clamp(maxHealth, 0, 100),
                maxSanity = Mathf.Clamp(maxSanity, 0, 100),
                carryCapacityKg = carryKg,
                sanityRecoveryMultiplier = sanityRecovery,
                radiationResistanceMultiplier = radiationResistance,
                combatResolutionMultiplier = combat
            };
            var traitList = new List<TraitData>();
            foreach (var t in startingTraits) if (t != null) traitList.Add(t);
            c.startingTraits = traitList;
            c.backstoryText = backstory;
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---- Factions (bible §3.1–3.4) ----

        private static List<FactionData> SeedFactions(List<ItemData> items)
        {
            ItemData Item(string id) => items.Find(i => i.id == id);

            var list = new List<FactionData>();

            // The Scale Society — bureaucratic exploiters. Office-furniture grey ("Reference Grey 14").
            var scale = CreateOrLoad<FactionData>($"{DefRoot}/Factions/Faction_ScaleSociety.asset");
            scale.id = "faction_scale_society";
            scale.displayName = "The Scale Society";
            scale.factionId = FactionId.ScaleSociety;
            scale.factionColor = new Color(0.42f, 0.43f, 0.40f);
            scale.ideologyTags = new List<string> { "bureaucratic", "actuarial", "demographic", "administrative" };
            scale.baseRelations = new List<FactionRelation>
            {
                new FactionRelation { other = FactionId.Cordon, defaultStanding = -40 },  // hostile but functional
                new FactionRelation { other = FactionId.Kafedra, defaultStanding = -80 }, // engage on sight
            };
            scale.thresholds = Bands("Hunted", "Demographic Anomaly", "Neutral", "Approved Contractor", "Stabilization");
            scale.signatureEquipment = new List<ItemData> { Item("item_bureau_dossier") };
            scale.endgameBranchId = "ending_stabilization";
            EditorUtility.SetDirty(scale);
            list.Add(scale);

            // The 14th Independent Cordon Regiment — militaristic, faded pale grey-green.
            var cordon = CreateOrLoad<FactionData>($"{DefRoot}/Factions/Faction_Cordon.asset");
            cordon.id = "faction_cordon";
            cordon.displayName = "The 14th Independent Cordon Regiment";
            cordon.factionId = FactionId.Cordon;
            cordon.factionColor = new Color(0.40f, 0.44f, 0.38f);
            cordon.ideologyTags = new List<string> { "militaristic", "containment", "interdiction", "orders" };
            cordon.baseRelations = new List<FactionRelation>
            {
                new FactionRelation { other = FactionId.ScaleSociety, defaultStanding = -50 }, // loathing
                new FactionRelation { other = FactionId.Kafedra, defaultStanding = 5 },         // delicate accommodation
            };
            cordon.thresholds = Bands("Shoot On Sight", "Hostile", "Detained", "Auxiliary", "Relief");
            cordon.signatureEquipment = new List<ItemData> { Item("item_service_pistol"), Item("item_pistol_ammo") };
            cordon.endgameBranchId = "ending_relief";
            EditorUtility.SetDirty(cordon);
            list.Add(cordon);

            // The Kafedra — scientific exploiters / adaptation cult. Pale bone tone.
            var kafedra = CreateOrLoad<FactionData>($"{DefRoot}/Factions/Faction_Kafedra.asset");
            kafedra.id = "faction_kafedra";
            kafedra.displayName = "The Kafedra";
            kafedra.factionId = FactionId.Kafedra;
            kafedra.factionColor = new Color(0.80f, 0.76f, 0.66f);
            kafedra.ideologyTags = new List<string> { "scientific", "adaptation", "modification", "devotional" };
            kafedra.baseRelations = new List<FactionRelation>
            {
                new FactionRelation { other = FactionId.ScaleSociety, defaultStanding = -60 }, // active hatred
                new FactionRelation { other = FactionId.Cordon, defaultStanding = 10 },          // cautious accommodation
            };
            kafedra.thresholds = Bands("Targeted", "Wary", "Neutral", "Invited", "Adaptation");
            kafedra.signatureEquipment = new List<ItemData> { Item("item_artifact_ballast") };
            kafedra.endgameBranchId = "ending_adaptation";
            EditorUtility.SetDirty(kafedra);
            list.Add(kafedra);

            return list;
        }

        private static List<ReputationThreshold> Bands(string hunted, string hostile, string neutral, string allied, string endgame)
        {
            return new List<ReputationThreshold>
            {
                new ReputationThreshold { thresholdName = hunted,  minReputation = -100, maxReputation = -60 },
                new ReputationThreshold { thresholdName = hostile, minReputation = -59,  maxReputation = -20 },
                new ReputationThreshold { thresholdName = neutral, minReputation = -19,  maxReputation =  19 },
                new ReputationThreshold { thresholdName = allied,  minReputation =  20,  maxReputation =  59 },
                new ReputationThreshold { thresholdName = endgame, minReputation =  60,  maxReputation = 100 },
            };
        }

        // ---- Asset plumbing ----

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "Definitions");
            EnsureFolder("Assets/Data/Definitions", "Factions");
            EnsureFolder("Assets/Data/Definitions", "Items");
            EnsureFolder("Assets/Data/Definitions", "Crew");
            EnsureFolder("Assets/Data/Definitions", "Traits");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
                AssetDatabase.CreateFolder(parent, name);
        }

        /// <summary>Loads the asset at <paramref name="path"/> if it exists, otherwise creates it. Keeps re-runs idempotent.</summary>
        private static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var instance = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(instance, path);
            return instance;
        }
    }
}
#endif
