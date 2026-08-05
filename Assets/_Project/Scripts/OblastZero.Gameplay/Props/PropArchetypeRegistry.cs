// Assets/_Project/Scripts/OblastZero.Gameplay/Props/PropArchetypeRegistry.cs
using System.Collections.Generic;
using OblastZero.Data;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// How a loaded prop mesh is fitted into the footprint its <see cref="VisualArchetype"/> reserves.
    /// </summary>
    public enum PropFitMode
    {
        /// <summary>
        /// Scale uniformly so the prop's longest axis matches the footprint's longest axis. Preserves
        /// the authored proportions. The default, and the right answer for every prop shipped so far:
        /// the pry bar is already long and thin, and forcing it into the Tool footprint's 0.52 x 0.12 x
        /// 0.12 box would squash its cross-section into a ribbon.
        /// </summary>
        Uniform = 0,

        /// <summary>
        /// Stretch each axis independently to fill the archetype footprint exactly. Matches what the
        /// primitive placeholder did. Use only for props authored as a unit cube.
        /// </summary>
        Stretch = 1,
    }

    /// <summary>
    /// Maps <see cref="VisualArchetype"/> values to prop meshes and the transform corrections applied
    /// when one is instantiated.
    ///
    /// <para>Keyed on the archetype enum rather than free-form prop ids, because
    /// <see cref="VisualArchetypeMapping"/> is the declared authority for item appearance and is already
    /// mirrored (and drift-checked) by tools/visual_archetypes.py. Callers that prefer a readable name
    /// can still pass one — <see cref="PropResourceKeys.TryParseArchetype"/> resolves aliases such as
    /// "crate_wooden" onto the same vocabulary.</para>
    ///
    /// <para>An archetype with no entry, or an entry with an empty key, is not an error: the loader
    /// falls back to the primitive silhouette so an unauthored pickup stays visible and grabbable.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "OblastZero/Prop Archetype Registry", fileName = "PropArchetypeRegistry")]
    public class PropArchetypeRegistry : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("Archetype this entry describes. One entry per archetype; later duplicates are ignored.")]
            public VisualArchetype archetype = VisualArchetype.Crate;

            [Tooltip("Resources key of the decimated prop, e.g. 'Props/prop_crate'. " +
                     "Leave empty to force the primitive fallback for this archetype.")]
            public string resourceKey = string.Empty;

            [Tooltip("How the mesh is fitted into the archetype's footprint. Uniform preserves proportions.")]
            public PropFitMode fitMode = PropFitMode.Uniform;

            [Tooltip("Extra per-axis scale applied after fitting. (1,1,1) leaves the fit untouched.")]
            public Vector3 extraScale = Vector3.one;

            [Tooltip("Local offset in metres, applied after fitting. Use to sit a prop on its base " +
                     "when the mesh's bounding-box centre is not its visual centre.")]
            public Vector3 positionOffset = Vector3.zero;

            [Tooltip("Local euler rotation in degrees, applied to the visual only. The pickup's own " +
                     "yaw from the scene generator is preserved underneath.")]
            public Vector3 rotationEuler = Vector3.zero;

            [Tooltip("Build an LODGroup for this prop from its _LOD0/_LOD1/_LOD2 nodes.")]
            public bool useLOD = true;

            [Tooltip("How many LOD levels to wire up, capped by how many the mesh file actually contains.")]
            [Range(1, 4)] public int lodCount = 3;

            [Tooltip("Keep the material the scene generator assigned instead of the mesh's own PBR " +
                     "material. Needed for Artifact, whose emissive read is what makes it findable in " +
                     "the dark; everything else looks better with its authored textures.")]
            public bool useSceneMaterial = false;
        }

        [Tooltip("One entry per archetype. Archetypes with no entry fall back to primitive silhouettes.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        /// <summary>Read-only view of the authored entries.</summary>
        public IReadOnlyList<Entry> Entries { get { return entries; } }

        private Dictionary<VisualArchetype, Entry> _index;

        /// <summary>
        /// Builds the archetype lookup. Called lazily and idempotently; also re-run by
        /// <see cref="OnValidate"/> so Inspector edits take effect without a domain reload.
        /// </summary>
        private void BuildIndex()
        {
            _index = new Dictionary<VisualArchetype, Entry>();
            if (entries == null) return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;
                if (_index.ContainsKey(entry.archetype))
                {
                    Debug.LogWarning("[PropArchetypeRegistry] Duplicate entry for archetype " +
                                     entry.archetype + " at index " + i + "; the first one wins.", this);
                    continue;
                }
                _index.Add(entry.archetype, entry);
            }
        }

        private void OnEnable()
        {
            BuildIndex();
        }

        private void OnValidate()
        {
            BuildIndex();
        }

        /// <summary>Entry for an archetype, or null when none is authored.</summary>
        public Entry Find(VisualArchetype archetype)
        {
            if (_index == null) BuildIndex();
            Entry entry;
            return _index.TryGetValue(archetype, out entry) ? entry : null;
        }

        /// <summary>Entry for an archetype name or alias, or null when the name does not resolve.</summary>
        public Entry Find(string archetypeId)
        {
            VisualArchetype archetype;
            return PropResourceKeys.TryParseArchetype(archetypeId, out archetype) ? Find(archetype) : null;
        }

        /// <summary>
        /// Resource key for an archetype: the authored override when the registry names one, otherwise
        /// the built-in default from <see cref="PropResourceKeys"/>. Null means "use a primitive".
        /// </summary>
        public string ResolveResourceKey(VisualArchetype archetype)
        {
            var entry = Find(archetype);
            if (entry != null && !string.IsNullOrWhiteSpace(entry.resourceKey)) return entry.resourceKey.Trim();
            if (entry != null && string.IsNullOrWhiteSpace(entry.resourceKey)) return null;
            return PropResourceKeys.DefaultKeyFor(archetype);
        }

        /// <summary>
        /// The world-space footprint an archetype's silhouette occupies, in metres.
        ///
        /// <para>This is <b>not</b> just <c>ShapeOf(archetype).LocalScale</c>: Unity's Cylinder and Capsule
        /// primitive meshes are two units tall (CLAUDE.md §14), so a MetalCan authored at a 0.13 Y scale
        /// is a 0.26 m can. Getting this wrong would fit every cylindrical prop at half height.</para>
        /// </summary>
        public static Vector3 FootprintOf(VisualArchetype archetype)
        {
            var shape = VisualArchetypeMapping.ShapeOf(archetype);
            float verticalExtent =
                (shape.Primitive == PrimitiveType.Cylinder || shape.Primitive == PrimitiveType.Capsule)
                    ? 2f : 1f;
            return new Vector3(shape.LocalScale.x,
                               shape.LocalScale.y * verticalExtent,
                               shape.LocalScale.z);
        }

        /// <summary>
        /// Builds a registry populated with sane defaults for every archetype. Used by the editor asset
        /// generator and by the smoke test, so the shipped asset and the tested configuration cannot
        /// drift apart.
        /// </summary>
        public static PropArchetypeRegistry CreateDefault()
        {
            var registry = CreateInstance<PropArchetypeRegistry>();
            registry.entries = new List<Entry>();

            foreach (VisualArchetype archetype in System.Enum.GetValues(typeof(VisualArchetype)))
            {
                if (archetype == VisualArchetype.Auto) continue;  // never rendered; resolves first
                registry.entries.Add(new Entry
                {
                    archetype = archetype,
                    resourceKey = PropResourceKeys.DefaultKeyFor(archetype) ?? string.Empty,
                    fitMode = PropFitMode.Uniform,
                    extraScale = Vector3.one,
                    positionOffset = Vector3.zero,
                    rotationEuler = Vector3.zero,
                    useLOD = true,
                    lodCount = 3,
                    useSceneMaterial = archetype == VisualArchetype.Artifact,
                });
            }

            registry.BuildIndex();
            return registry;
        }
    }
}
