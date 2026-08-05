// Assets/_Project/Scripts/OblastZero.Gameplay/Props/PropResourceKeys.cs
using System;
using System.Collections.Generic;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Resource keys for the decimated prop meshes, plus the alias table that maps designer-facing
    /// prop names onto the <see cref="VisualArchetype"/> vocabulary.
    ///
    /// <para>Props ship as <c>.bytes</c> rather than <c>.glb</c> under Assets/Art/Resources/Props/.
    /// That is not cosmetic: Unity only exposes a file to <c>Resources.Load&lt;TextAsset&gt;</c> if its
    /// extension maps to the text-script importer. A <c>.glb</c> in a Resources folder imports as
    /// DefaultImporter and loads as null — which is exactly the state the four source props under
    /// Assets/Art/Meshes/Props/ are in today. <c>tools/decimate_props.py</c> writes the .bytes files.</para>
    ///
    /// <para><b>Archetypes are the vocabulary, not free-form prop ids.</b> <see cref="VisualArchetypeMapping"/>
    /// is declared the authority for item appearance and is mirrored by tools/visual_archetypes.py, which
    /// refuses to regenerate the scene if the two drift. Introducing a second, parallel set of string ids
    /// would re-create precisely that drift risk with no gate watching it. So the registry keys on the
    /// enum, and the aliases below exist only so callers may pass a readable name.</para>
    /// </summary>
    public static class PropResourceKeys
    {
        /// <summary>Folder under a Resources root that holds the decimated props.</summary>
        public const string ResourceFolder = "Props";

        /// <summary>Manifest written by tools/decimate_props.py, describing every shipped prop.</summary>
        public const string ManifestKey = "Props/prop_manifest";

        /// <summary>
        /// Suffix marking a LOD node inside a prop file, matching Unity's own LOD naming convention.
        /// tools/decimate_props.py emits <c>&lt;prop&gt;_LOD0</c>/<c>_LOD1</c>/<c>_LOD2</c> as sibling nodes.
        /// </summary>
        public const string LodNodeSuffix = "_LOD";

        public const string Crate = "Props/prop_crate";
        public const string AmmunitionBox = "Props/prop_ammo_box";
        public const string Artifact = "Props/prop_artifact";
        public const string Tool = "Props/prop_pry_bar";

        /// <summary>
        /// Archetypes that have an authored mesh today. The remaining seven fall back to the primitive
        /// silhouettes in <see cref="VisualArchetypeMapping"/> — deliberately, so a missing prop degrades
        /// to a readable shape instead of an empty pickup the player cannot see.
        /// </summary>
        private static readonly Dictionary<VisualArchetype, string> DefaultKeys =
            new Dictionary<VisualArchetype, string>
            {
                { VisualArchetype.Crate, Crate },
                { VisualArchetype.AmmunitionBox, AmmunitionBox },
                { VisualArchetype.Artifact, Artifact },
                { VisualArchetype.Tool, Tool },
            };

        /// <summary>
        /// Readable prop names accepted by the string overloads, mapped onto the archetype vocabulary.
        /// These are machine identifiers, never shown to the player, so they sit outside the §9 content
        /// voice rules and outside what tools/content_qa.py scans (prose fields only).
        /// </summary>
        private static readonly Dictionary<string, VisualArchetype> Aliases =
            new Dictionary<string, VisualArchetype>(StringComparer.OrdinalIgnoreCase)
            {
                { "crate_wooden", VisualArchetype.Crate },
                { "barrel_metal", VisualArchetype.Crate },
                { "ammo_box_762", VisualArchetype.AmmunitionBox },
                { "gas_mask_gp5", VisualArchetype.Clothing },
                { "pistol_makarov", VisualArchetype.WeaponSidearm },
                { "medkit_soviet", VisualArchetype.Medical },
                { "canned_tushonka", VisualArchetype.MetalCan },
                { "water_flask", VisualArchetype.MetalCan },
                { "document_folder", VisualArchetype.Document },
                { "artifact_anomaly", VisualArchetype.Artifact },
            };

        /// <summary>
        /// Resource key for an archetype, or null when no mesh has been authored for it yet.
        /// A null result is a normal, expected outcome — callers fall back to primitives.
        /// </summary>
        public static string DefaultKeyFor(VisualArchetype archetype)
        {
            string key;
            return DefaultKeys.TryGetValue(archetype, out key) ? key : null;
        }

        /// <summary>True when an authored mesh exists for the archetype.</summary>
        public static bool HasAuthoredMesh(VisualArchetype archetype)
        {
            return DefaultKeys.ContainsKey(archetype);
        }

        /// <summary>Every archetype that currently ships a mesh, for preloading.</summary>
        public static IEnumerable<VisualArchetype> AuthoredArchetypes()
        {
            return DefaultKeys.Keys;
        }

        /// <summary>
        /// Resolves an archetype from either its enum name ("Crate", "ammunitionbox") or a readable
        /// alias ("crate_wooden"). Returns false rather than guessing, so a typo surfaces as a logged
        /// miss instead of silently rendering the wrong silhouette.
        /// </summary>
        public static bool TryParseArchetype(string archetypeId, out VisualArchetype archetype)
        {
            archetype = VisualArchetype.Crate;
            if (string.IsNullOrWhiteSpace(archetypeId)) return false;

            string trimmed = archetypeId.Trim();
            if (Aliases.TryGetValue(trimmed, out archetype)) return true;

            // Enum.TryParse with ignoreCase would also accept the underlying integer ("7"), which is
            // not a name and would silently succeed on any numeric junk. Compare names explicitly.
            foreach (VisualArchetype candidate in Enum.GetValues(typeof(VisualArchetype)))
            {
                if (string.Equals(candidate.ToString(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    archetype = candidate;
                    return true;
                }
            }

            archetype = VisualArchetype.Crate;
            return false;
        }
    }
}
