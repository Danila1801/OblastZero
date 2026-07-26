// Assets/Data/Scripts/Definitions/OblastZero.Data/VisualArchetype.cs
using UnityEngine;

namespace OblastZero.Data
{
    /// <summary>
    /// Visual class for rendering an item in the 3D scavenge scene. 703 items cannot each carry a
    /// hand-authored mesh, so items are bucketed into a small shape vocabulary the player learns to read
    /// at a glance: a long silhouette on a shelf is a weapon or a tool, a flat one is paperwork, a
    /// glowing sphere in the pit is an artifact.
    ///
    /// The archetype is normally <b>derived</b> from category and id (see <see cref="VisualArchetypeMapping"/>),
    /// so no content authoring is required. <see cref="Auto"/> means "derive me" and is the default.
    ///
    /// Note the brief placed this under _Project/Scripts/Core with namespace OblastZero.Data; it lives in
    /// the Data definitions root instead, because CLAUDE.md §4 requires the namespace to match the folder
    /// and ItemData serializes this type.
    /// </summary>
    public enum VisualArchetype
    {
        /// <summary>Not authored — resolve by deriving from category and id. The default.</summary>
        Auto = 0,

        /// <summary>Wooden supply crate. Also the fallback for anything unclassified.</summary>
        Crate = 1,
        /// <summary>Small cylinder: tin, ration can, water flask.</summary>
        MetalCan = 2,
        /// <summary>Flat metal ammunition box.</summary>
        AmmunitionBox = 3,
        /// <summary>Folder, binder, dossier, manifest.</summary>
        Document = 4,
        /// <summary>Pistol-sized weapon.</summary>
        WeaponSidearm = 5,
        /// <summary>Rifle, carbine, long gun, axe.</summary>
        WeaponLong = 6,
        /// <summary>Pry bar, wrench, torch, hand instrument.</summary>
        Tool = 7,
        /// <summary>Anomaly artifact. Rendered emissive.</summary>
        Artifact = 8,
        /// <summary>Coats, masks, respirators, worn gear.</summary>
        Clothing = 9,
        /// <summary>Medkit, bandage box, syringe case.</summary>
        Medical = 10,
        /// <summary>A standing crew member. Never derived from ItemData — crew are not items.</summary>
        Crew = 11,
    }

    /// <summary>
    /// Classifies items into a <see cref="VisualArchetype"/> and maps each archetype to the primitive
    /// shape and local scale that represents it, so the scene generator and the runtime spawner agree
    /// on what an item looks like.
    ///
    /// <para><b>The two tables below are the single source of truth for item appearance.</b>
    /// <c>tools/generate_scavenge_scene.py</c> parses them out of this file and refuses to write the
    /// scene if its own copy has drifted, so the shapes baked into Scavenge.unity cannot silently
    /// disagree with the shapes spawned at runtime. Both tables are parsed line-by-line — keep one
    /// entry per line and keep the call shape intact.</para>
    /// </summary>
    public static class VisualArchetypeMapping
    {
        /// <summary>
        /// One classification test. A null <see cref="Category"/> means "any category"; a null
        /// <see cref="IdSubstring"/> means "any id". Both non-null means both must match.
        /// </summary>
        private readonly struct ClassificationRule
        {
            public readonly ItemCategory? Category;
            public readonly string IdSubstring;
            public readonly VisualArchetype Archetype;

            public ClassificationRule(ItemCategory? category, string idSubstring, VisualArchetype archetype)
            {
                Category = category;
                IdSubstring = idSubstring;
                Archetype = archetype;
            }
        }

        private static ClassificationRule Rule(ItemCategory? category, string idSubstring, VisualArchetype archetype)
        {
            return new ClassificationRule(category, idSubstring, archetype);
        }

        // Ordered — first match wins. Order is load-bearing in three places:
        //   * "ammo" precedes "pistol", so item_pistol_ammo is a box of rounds, not a sidearm.
        //   * the Ammunition category precedes the Weapon category, so a shotgun shell is not a shotgun.
        //   * "pistol" precedes the Weapon category, so a sidearm does not render as a rifle.
        // Deliberately absent: an id test for "gauge". item_handloaded_12_gauge and item_12_gauge_carbine
        // share it, and the carbine is a weapon — category already separates them correctly.
        // OZ-ARCHETYPE-RULES-BEGIN
        private static readonly ClassificationRule[] Rules =
        {
            Rule(null, "artifact", VisualArchetype.Artifact),
            Rule(ItemCategory.Artifact, null, VisualArchetype.Artifact),
            Rule(null, "ammo", VisualArchetype.AmmunitionBox),
            Rule(ItemCategory.Ammunition, null, VisualArchetype.AmmunitionBox),
            Rule(null, "pistol", VisualArchetype.WeaponSidearm),
            Rule(null, "sidearm", VisualArchetype.WeaponSidearm),
            Rule(null, "revolver", VisualArchetype.WeaponSidearm),
            Rule(ItemCategory.Weapon, null, VisualArchetype.WeaponLong),
            Rule(null, "dossier", VisualArchetype.Document),
            Rule(null, "manifest", VisualArchetype.Document),
            Rule(null, "intel", VisualArchetype.Document),
            Rule(ItemCategory.Document, null, VisualArchetype.Document),
            Rule(null, "medkit", VisualArchetype.Medical),
            Rule(null, "bandage", VisualArchetype.Medical),
            Rule(null, "syringe", VisualArchetype.Medical),
            Rule(null, "suture", VisualArchetype.Medical),
            Rule(ItemCategory.Medical, null, VisualArchetype.Medical),
            Rule(null, "respirator", VisualArchetype.Clothing),
            Rule(null, "coat", VisualArchetype.Clothing),
            Rule(null, "jacket", VisualArchetype.Clothing),
            Rule(null, "boots", VisualArchetype.Clothing),
            Rule(null, "pry", VisualArchetype.Tool),
            Rule(null, "wrench", VisualArchetype.Tool),
            Rule(ItemCategory.Tool, null, VisualArchetype.Tool),
            Rule(null, "canned", VisualArchetype.MetalCan),
            Rule(null, "flask", VisualArchetype.MetalCan),
            Rule(ItemCategory.Food, null, VisualArchetype.MetalCan),
            Rule(ItemCategory.Water, null, VisualArchetype.MetalCan),
            Rule(ItemCategory.Crafting, null, VisualArchetype.Crate),
            Rule(ItemCategory.Special, null, VisualArchetype.Crate),
        };
        // OZ-ARCHETYPE-RULES-END

        /// <summary>Primitive shape and local scale for one archetype.</summary>
        public readonly struct ArchetypeShape
        {
            public readonly PrimitiveType Primitive;
            public readonly Vector3 LocalScale;

            public ArchetypeShape(PrimitiveType primitive, Vector3 localScale)
            {
                Primitive = primitive;
                LocalScale = localScale;
            }
        }

        private static ArchetypeShape Shape(PrimitiveType primitive, float x, float y, float z)
        {
            return new ArchetypeShape(primitive, new Vector3(x, y, z));
        }

        // Metres. Cylinder and Capsule primitive meshes are TWO units tall (CLAUDE.md §14), so their Y
        // scale is half what the finished height reads as — a 0.13 Y scale is a 0.26 m can.
        // OZ-ARCHETYPE-SHAPES-BEGIN
        private static readonly ArchetypeShape[] Shapes =
        {
            Shape(PrimitiveType.Cube, 0.34f, 0.34f, 0.34f),      // Auto — never rendered; resolves first
            Shape(PrimitiveType.Cube, 0.34f, 0.34f, 0.34f),      // Crate
            Shape(PrimitiveType.Cylinder, 0.20f, 0.13f, 0.20f),  // MetalCan
            Shape(PrimitiveType.Cube, 0.30f, 0.18f, 0.22f),      // AmmunitionBox
            Shape(PrimitiveType.Cube, 0.30f, 0.05f, 0.24f),      // Document
            Shape(PrimitiveType.Cube, 0.26f, 0.10f, 0.13f),      // WeaponSidearm
            Shape(PrimitiveType.Cube, 0.86f, 0.11f, 0.14f),      // WeaponLong
            Shape(PrimitiveType.Cube, 0.52f, 0.12f, 0.12f),      // Tool
            Shape(PrimitiveType.Sphere, 0.30f, 0.30f, 0.30f),    // Artifact
            Shape(PrimitiveType.Capsule, 0.26f, 0.16f, 0.26f),   // Clothing
            Shape(PrimitiveType.Cube, 0.30f, 0.20f, 0.20f),      // Medical
            Shape(PrimitiveType.Capsule, 0.78f, 0.86f, 0.78f),   // Crew
        };
        // OZ-ARCHETYPE-SHAPES-END

        /// <summary>
        /// Classifies an item from its category and id, ignoring any authored override. Returns
        /// <see cref="VisualArchetype.Crate"/> for anything no rule claims — a generic supply box is the
        /// one shape that is never wrong, only unspecific.
        /// </summary>
        public static VisualArchetype Derive(ItemData data)
        {
            if (data == null) return VisualArchetype.Crate;

            string id = data.id != null ? data.id.ToLowerInvariant() : string.Empty;

            for (int i = 0; i < Rules.Length; i++)
            {
                var rule = Rules[i];
                if (rule.Category.HasValue && rule.Category.Value != data.category) continue;
                if (rule.IdSubstring != null && id.IndexOf(rule.IdSubstring, System.StringComparison.Ordinal) < 0) continue;
                return rule.Archetype;
            }

            return VisualArchetype.Crate;
        }

        /// <summary>
        /// The archetype to actually render: the authored override when the item names one, otherwise
        /// the derived class. This is what callers should use.
        /// </summary>
        public static VisualArchetype Resolve(ItemData data)
        {
            if (data == null) return VisualArchetype.Crate;
            if (data.visualArchetype != VisualArchetype.Auto) return data.visualArchetype;
            return Derive(data);
        }

        /// <summary>Primitive shape and local scale for an archetype. Never throws on an unknown value.</summary>
        public static ArchetypeShape ShapeOf(VisualArchetype archetype)
        {
            int index = (int)archetype;
            if (index < 0 || index >= Shapes.Length) return Shapes[(int)VisualArchetype.Crate];
            return Shapes[index];
        }

        /// <summary>
        /// Builds a placeholder visual for a pickup: the archetype's primitive at the archetype's scale,
        /// parented to <paramref name="parent"/> with its collider stripped so it cannot block the
        /// CharacterController or steal the interaction raycast from the pickup's own trigger.
        /// Used by the runtime spawner until authored meshes replace the primitives.
        /// </summary>
        public static GameObject CreateVisual(VisualArchetype archetype, Transform parent, Material material)
        {
            var shape = ShapeOf(archetype);
            var go = GameObject.CreatePrimitive(shape.Primitive);
            go.name = "Visual_" + archetype;

            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }

            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = shape.LocalScale;

            if (material != null)
            {
                var renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }

            return go;
        }
    }
}
