// Assets/_Project/Scripts/OblastZero.Gameplay/Props/ScavengePropDresser.cs
using System.Collections;
using System.Collections.Generic;
using OblastZero.Core;
using OblastZero.Data;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Replaces the baked primitive silhouettes on every <see cref="ScavengePickup"/> in the scavenge
    /// scene with the authored prop meshes, once those have finished loading.
    ///
    /// <para><b>Why a dresser rather than generating the scene with meshes in it.</b> Assets/Scenes/Scavenge.unity
    /// is emitted by tools/generate_scavenge_scene.py and is byte-deterministic (CLAUDE.md §14); baking mesh
    /// references into it would mean the scene could no longer be regenerated without the Editor, and the
    /// generator's validation gates — burial, support, walkability — all reason about the primitive
    /// extents. So the generator keeps owning placement and collision, and this component owns appearance.
    /// The primitive stays the authority on where a pickup sits and how big its trigger is; only what the
    /// player sees changes.</para>
    ///
    /// <para>Archetypes with no authored mesh keep their primitive untouched — currently seven of eleven.
    /// That is a designed fallback, not a gap: an unauthored pickup must still be visible and grabbable.</para>
    /// </summary>
    public class ScavengePropDresser : MonoBehaviour
    {
        [Tooltip("Dress automatically on Start. Turn off to drive it from a state that wants to await the " +
                 "preload before revealing the level.")]
        [SerializeField] private bool dressOnStart = true;

        [Tooltip("Hide the primitive renderer a mesh replaces. Off leaves both visible, which is only " +
                 "useful for eyeballing how well a mesh fits its authored footprint.")]
        [SerializeField] private bool hideReplacedPrimitive = true;

        [Tooltip("Log a per-archetype census after dressing. Cheap, and the fastest way to see that a " +
                 "prop silently fell back to its primitive.")]
        [SerializeField] private bool logCensus = true;

        private bool _dressed;
        private readonly List<GameObject> _spawnedVisuals = new List<GameObject>();

        /// <summary>Visuals this dresser created, for teardown by the owning state.</summary>
        public IReadOnlyList<GameObject> SpawnedVisuals { get { return _spawnedVisuals; } }

        private void Start()
        {
            if (dressOnStart) StartCoroutine(DressRoutine());
        }

        private void OnDestroy()
        {
            ReleaseVisuals();
        }

        /// <summary>
        /// Preloads every authored prop, then swaps each pickup's silhouette in a single frame.
        ///
        /// <para>The swap is deliberately not interleaved with the load: dressing pickup-by-pickup as each
        /// mesh arrives would show the player a level that visibly assembles itself over several seconds,
        /// at the exact moment a 60-second timer is running.</para>
        /// </summary>
        public IEnumerator DressRoutine()
        {
            if (_dressed) yield break;
            _dressed = true;

            var loader = GLBPropLoader.Instance;
            if (loader == null) yield break;

            yield return loader.PreloadAllRoutine();
            Dress();
        }

        /// <summary>
        /// Swaps every pickup's visual using whatever the loader currently has cached. Safe to call
        /// without a preload — uncached archetypes simply keep their primitives.
        /// </summary>
        public void Dress()
        {
            var loader = GLBPropLoader.Instance;
            if (loader == null) return;

            var database = GameManager.Instance != null ? GameManager.Instance.Database : null;
            if (database == null)
            {
                Debug.LogWarning("[ScavengePropDresser] No GameDatabase; pickups keep their primitives.");
                return;
            }

            var pickups = FindObjectsByType<ScavengePickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var census = new Dictionary<VisualArchetype, int>();
            int replaced = 0;

            for (int i = 0; i < pickups.Length; i++)
            {
                var pickup = pickups[i];
                VisualArchetype archetype = ResolveArchetype(pickup, database);

                int seen;
                census[archetype] = census.TryGetValue(archetype, out seen) ? seen + 1 : 1;

                if (!PropResourceKeys.HasAuthoredMesh(archetype)) continue;

                var primitive = pickup.GetComponent<MeshRenderer>();
                // Preserve the level designer's shadow decision: the generator turns shadow casting off
                // for item pickups and on for crew, and a mesh swap must not quietly change that.
                var shadowMode = primitive != null
                    ? primitive.shadowCastingMode
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
                Material sceneMaterial = primitive != null ? primitive.sharedMaterial : null;

                var visual = loader.CreateVisual(archetype, pickup.transform, sceneMaterial);
                if (visual == null) continue;

                // A primitive fallback is not a replacement — leaving the baked renderer hidden behind
                // one would be identical geometry drawn twice.
                if (visual.GetComponent<PropInstanceTag>() == null)
                {
                    Destroy(visual);
                    continue;
                }

                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++) renderers[r].shadowCastingMode = shadowMode;

                if (hideReplacedPrimitive && primitive != null) primitive.enabled = false;

                _spawnedVisuals.Add(visual);
                replaced++;
            }

            if (logCensus)
            {
                var summary = new List<string>();
                foreach (var pair in census)
                {
                    summary.Add(pair.Key + " x" + pair.Value +
                                (PropResourceKeys.HasAuthoredMesh(pair.Key) ? "" : " (primitive)"));
                }
                summary.Sort();
                Debug.Log("[ScavengePropDresser] Dressed " + replaced + "/" + pickups.Length +
                          " pickups with authored meshes. Census: " + string.Join(", ", summary));
            }
        }

        /// <summary>
        /// The archetype a pickup should render as. Crew are never items, so they resolve directly rather
        /// than going through <see cref="VisualArchetypeMapping.Resolve"/>, which classifies ItemData.
        /// </summary>
        private static VisualArchetype ResolveArchetype(ScavengePickup pickup, GameDatabase database)
        {
            if (pickup.Kind == ScavengePickup.PickupKind.Crew) return VisualArchetype.Crew;

            ItemData item;
            if (database.TryGetItem(pickup.DataId, out item) && item != null)
            {
                return VisualArchetypeMapping.Resolve(item);
            }

            Debug.LogWarning("[ScavengePropDresser] Pickup '" + pickup.name + "' references unknown item id '" +
                             pickup.DataId + "'; falling back to Crate.");
            return VisualArchetype.Crate;
        }

        /// <summary>Destroys spawned visuals and re-enables the primitives they hid.</summary>
        public void ReleaseVisuals()
        {
            var loader = GLBPropLoader.Instance;
            for (int i = 0; i < _spawnedVisuals.Count; i++)
            {
                var visual = _spawnedVisuals[i];
                if (visual == null) continue;

                var parent = visual.transform.parent;
                if (parent != null)
                {
                    var primitive = parent.GetComponent<MeshRenderer>();
                    if (primitive != null) primitive.enabled = true;
                }

                if (loader != null) loader.ReleaseProp(visual);
                else Destroy(visual);
            }
            _spawnedVisuals.Clear();
            _dressed = false;
        }
    }
}
