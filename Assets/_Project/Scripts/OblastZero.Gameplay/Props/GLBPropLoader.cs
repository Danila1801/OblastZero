// Assets/_Project/Scripts/OblastZero.Gameplay/Props/GLBPropLoader.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GLTFast;
using OblastZero.Data;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Loads decimated prop meshes at runtime via GLTFast and hands out instances, caching one
    /// template per mesh file.
    ///
    /// <para><b>Why runtime loading at all.</b> The props are not Unity model assets: glTFast's scripted
    /// importer registers with <c>overrideExts</c> rather than claiming <c>.glb</c> outright, so the four
    /// files under Assets/Art/Meshes/Props/ import as DefaultImporter and cannot be referenced from a
    /// scene. Loading the bytes ourselves also keeps scene authoring headless (CLAUDE.md §14) — the prop
    /// a pickup shows is decided by data, not by a serialized prefab reference somebody has to wire in
    /// the Editor.</para>
    ///
    /// <para><b>Lifetime.</b> Each template owns a <see cref="GltfImport"/>, which owns the meshes and
    /// textures it created. Disposing it destroys those, so instances are reference-counted and an import
    /// is only released once nothing is using it. <see cref="ReleaseUnused"/> is the safe reclaim point;
    /// <see cref="ReleaseAll"/> tears everything down and is what the scavenge state calls on exit.</para>
    ///
    /// <para><b>Async idiom.</b> Unity 6 <see cref="Awaitable"/>, not UniTask (not in this project) and not
    /// Addressables (not installed). In-flight de-duplication is kept on <see cref="Task"/> rather than
    /// Awaitable deliberately: an Awaitable may only be awaited once, so two pickups asking for the same
    /// crate on the same frame would deadlock the second caller on a consumed handle.</para>
    /// </summary>
    public class GLBPropLoader : MonoBehaviour
    {
        /// <summary>Resources key of the registry asset. Absent is fine — defaults are built in code.</summary>
        public const string RegistryResourceKey = "PropArchetypeRegistry";

        private sealed class PropTemplate
        {
            public string Key;
            public GltfImport Import;
            public GameObject Template;
            public Vector3 LocalSize = Vector3.one;
            public int LiveInstances;
        }

        private static GLBPropLoader _instance;
        private static bool _applicationQuitting;

        /// <summary>
        /// Lazily-created singleton. Returns null while the application is quitting so a late
        /// <c>OnDestroy</c> cannot resurrect the loader into a scene that is being torn down.
        /// </summary>
        public static GLBPropLoader Instance
        {
            get
            {
                if (_applicationQuitting) return null;
                if (_instance == null)
                {
                    var host = new GameObject("[GLBPropLoader]");
                    _instance = host.AddComponent<GLBPropLoader>();
                    DontDestroyOnLoad(host);
                }
                return _instance;
            }
        }

        [Tooltip("Archetype -> mesh mapping. Left empty, the loader loads it from Resources and " +
                 "falls back to built-in defaults, so no Editor wiring is required.")]
        [SerializeField] private PropArchetypeRegistry registry;

        private readonly Dictionary<string, PropTemplate> _templates = new Dictionary<string, PropTemplate>();
        private readonly Dictionary<string, Task<PropTemplate>> _inFlight = new Dictionary<string, Task<PropTemplate>>();
        private Transform _templateRoot;

        /// <summary>Registry in use, resolved from the inspector, Resources, then built-in defaults.</summary>
        public PropArchetypeRegistry Registry
        {
            get
            {
                if (registry == null)
                {
                    registry = Resources.Load<PropArchetypeRegistry>(RegistryResourceKey);
                    if (registry == null)
                    {
                        registry = PropArchetypeRegistry.CreateDefault();
                        registry.name = "PropArchetypeRegistry (runtime default)";
                        Debug.Log("[GLBPropLoader] No PropArchetypeRegistry asset in Resources; " +
                                  "using built-in defaults.");
                    }
                }
                return registry;
            }
        }

        /// <summary>Number of mesh files currently resident. Diagnostics and the smoke test read this.</summary>
        public int CachedTemplateCount { get { return _templates.Count; } }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            var root = new GameObject("[PropTemplates]");
            root.transform.SetParent(transform, false);
            // Inactive, so template hierarchies never render, never tick, and never get raycast.
            root.SetActive(false);
            _templateRoot = root.transform;
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                ReleaseAll();
                _instance = null;
            }
        }

        // ══ PUBLIC API ═════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads a prop by resource key and returns a positioned instance, or null when the key does not
        /// resolve to a loadable mesh. Null is a normal outcome, not an exception — callers fall back to
        /// the primitive silhouette.
        /// </summary>
        public async Awaitable<GameObject> LoadPropAsync(
            string resourceKey,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            var template = await GetTemplateAsync(resourceKey, cancellationToken);
            if (template == null) return null;

            var instance = InstantiateTemplate(template, parent);
            if (instance == null) return null;

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            return instance;
        }

        /// <summary>
        /// Loads the prop registered for an archetype — accepting either an enum name ("Crate") or a
        /// readable alias ("crate_wooden") — and places it at <paramref name="position"/>, applying the
        /// registry's rotation and offset. Returns null when the archetype has no authored mesh.
        /// </summary>
        public async Awaitable<GameObject> LoadPropByArchetypeAsync(
            string archetypeId,
            Vector3 position,
            Transform parent = null,
            CancellationToken cancellationToken = default)
        {
            VisualArchetype archetype;
            if (!PropResourceKeys.TryParseArchetype(archetypeId, out archetype))
            {
                Debug.LogWarning("[GLBPropLoader] '" + archetypeId + "' is not a known archetype or alias.");
                return null;
            }

            string key = Registry.ResolveResourceKey(archetype);
            if (string.IsNullOrEmpty(key)) return null;

            var entry = Registry.Find(archetype);
            var template = await GetTemplateAsync(key, cancellationToken);
            if (template == null) return null;

            var instance = InstantiateTemplate(template, parent);
            if (instance == null) return null;

            var rotation = Quaternion.Euler(entry != null ? entry.rotationEuler : Vector3.zero);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = FitScale(template, entry, archetype, Vector3.one);
            if (entry != null) instance.transform.position += entry.positionOffset;

            ApplyLod(instance, entry);
            return instance;
        }

        /// <summary>
        /// Warms the cache for a set of archetypes before the scene needs them, so the first pickup the
        /// player walks past does not pop in. Unknown names are logged and skipped rather than throwing —
        /// a warm-up must never be the thing that fails a run.
        /// </summary>
        public async Awaitable PreloadScenePropsAsync(
            IEnumerable<string> archetypeIds,
            CancellationToken cancellationToken = default)
        {
            if (archetypeIds == null) return;

            var keys = new List<string>();
            foreach (string id in archetypeIds)
            {
                VisualArchetype archetype;
                if (!PropResourceKeys.TryParseArchetype(id, out archetype))
                {
                    Debug.LogWarning("[GLBPropLoader] Preload skipped unknown archetype '" + id + "'.");
                    continue;
                }
                string key = Registry.ResolveResourceKey(archetype);
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key)) keys.Add(key);
            }

            foreach (string key in keys)
            {
                if (cancellationToken.IsCancellationRequested) return;
                await GetTemplateAsync(key, cancellationToken);
            }
        }

        /// <summary>Preloads every archetype that ships a mesh.</summary>
        public Awaitable PreloadAllAsync(CancellationToken cancellationToken = default)
        {
            var names = new List<string>();
            foreach (var archetype in PropResourceKeys.AuthoredArchetypes()) names.Add(archetype.ToString());
            return PreloadScenePropsAsync(names, cancellationToken);
        }

        /// <summary>
        /// Coroutine bridge over <see cref="PreloadAllAsync"/>, for MonoBehaviours that would rather
        /// <c>StartCoroutine</c> than hold an async method alive across a scene unload.
        /// </summary>
        public IEnumerator PreloadAllRoutine()
        {
            var keys = new List<string>();
            foreach (var archetype in PropResourceKeys.AuthoredArchetypes())
            {
                string key = Registry.ResolveResourceKey(archetype);
                if (!string.IsNullOrEmpty(key) && !keys.Contains(key)) keys.Add(key);
            }

            for (int i = 0; i < keys.Count; i++)
            {
                Task<PropTemplate> task = GetTemplateTask(keys[i], CancellationToken.None);
                while (!task.IsCompleted) yield return null;
                if (task.IsFaulted)
                {
                    Debug.LogError("[GLBPropLoader] Preload of '" + keys[i] + "' failed: " +
                                   (task.Exception != null ? task.Exception.GetBaseException().Message : "unknown"));
                }
            }
        }

        /// <summary>
        /// Synchronous visual factory, drop-in compatible with
        /// <see cref="VisualArchetypeMapping.CreateVisual"/>: returns a GLB instance when the template is
        /// already cached, and the primitive silhouette otherwise.
        ///
        /// <para>This is what the scene dressing path calls per pickup, after a preload has populated the
        /// cache. Keeping it synchronous means the swap happens in one frame with no half-dressed scene
        /// visible to the player.</para>
        /// </summary>
        public GameObject CreateVisual(VisualArchetype archetype, Transform parent, Material sceneMaterial)
        {
            var entry = Registry.Find(archetype);
            string key = Registry.ResolveResourceKey(archetype);

            PropTemplate template = null;
            if (!string.IsNullOrEmpty(key)) _templates.TryGetValue(key, out template);

            if (template == null || template.Template == null)
            {
                return VisualArchetypeMapping.CreateVisual(archetype, parent, sceneMaterial);
            }

            var instance = InstantiateTemplate(template, parent);
            if (instance == null) return VisualArchetypeMapping.CreateVisual(archetype, parent, sceneMaterial);

            instance.name = "Visual_" + archetype;
            instance.transform.localPosition = entry != null ? entry.positionOffset : Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(entry != null ? entry.rotationEuler : Vector3.zero);
            instance.transform.localScale = FitScale(template, entry, archetype,
                parent != null ? parent.lossyScale : Vector3.one);

            if (entry != null && entry.useSceneMaterial && sceneMaterial != null)
            {
                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterial = sceneMaterial;
            }

            ApplyLod(instance, entry);
            return instance;
        }

        /// <summary>
        /// Destroys an instance and decrements its template's reference count. Safe on nulls and on
        /// objects this loader did not create.
        /// </summary>
        public void ReleaseProp(GameObject prop)
        {
            if (prop == null) return;

            var tag = prop.GetComponent<PropInstanceTag>();
            if (tag != null && !string.IsNullOrEmpty(tag.ResourceKey))
            {
                PropTemplate template;
                if (_templates.TryGetValue(tag.ResourceKey, out template))
                {
                    template.LiveInstances = Mathf.Max(0, template.LiveInstances - 1);
                }
            }

            if (Application.isPlaying) Destroy(prop);
            else DestroyImmediate(prop);
        }

        /// <summary>
        /// Disposes every template with no live instances. This is the only safe reclaim point: a
        /// GltfImport owns the meshes and textures its instances are still rendering, so disposing one
        /// early turns live props into missing-mesh renderers rather than freeing anything cleanly.
        /// </summary>
        public int ReleaseUnused()
        {
            var reclaimable = new List<string>();
            foreach (var pair in _templates)
            {
                if (pair.Value.LiveInstances <= 0) reclaimable.Add(pair.Key);
            }
            for (int i = 0; i < reclaimable.Count; i++) DisposeTemplate(reclaimable[i]);
            return reclaimable.Count;
        }

        /// <summary>Disposes every template regardless of live instances. Call on scavenge exit.</summary>
        public void ReleaseAll()
        {
            var keys = new List<string>(_templates.Keys);
            for (int i = 0; i < keys.Count; i++) DisposeTemplate(keys[i]);
            _templates.Clear();
            _inFlight.Clear();
        }

        // ══ INTERNALS ══════════════════════════════════════════════════════════════════════════════

        private async Awaitable<PropTemplate> GetTemplateAsync(string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return await GetTemplateTask(key, cancellationToken);
        }

        /// <summary>
        /// Returns the shared load Task for a key, starting one if needed. Task rather than Awaitable
        /// because several callers legitimately await the same load, and Awaitable is single-await.
        /// </summary>
        private Task<PropTemplate> GetTemplateTask(string key, CancellationToken cancellationToken)
        {
            PropTemplate cached;
            if (_templates.TryGetValue(key, out cached) && cached.Template != null)
            {
                return Task.FromResult(cached);
            }

            Task<PropTemplate> pending;
            if (_inFlight.TryGetValue(key, out pending)) return pending;

            pending = LoadTemplateAsync(key, cancellationToken);
            // Only park a task that actually suspended. The missing-TextAsset path returns before its
            // first await, so the task is already complete and its finally-block has run — storing it
            // would leave a permanently-cached failure that no later call could retry past.
            if (!pending.IsCompleted) _inFlight[key] = pending;
            return pending;
        }

        private async Task<PropTemplate> LoadTemplateAsync(string key, CancellationToken cancellationToken)
        {
            try
            {
                var textAsset = Resources.Load<TextAsset>(key);
                if (textAsset == null)
                {
                    Debug.LogWarning("[GLBPropLoader] No TextAsset at Resources/" + key +
                                     ". Props ship as .bytes (a .glb in Resources imports as " +
                                     "DefaultImporter and loads as null). Run tools/decimate_props.py.");
                    return null;
                }

                byte[] bytes = textAsset.bytes;
                // The TextAsset itself is dead weight once copied; the decimated props are ~0.8 MB each.
                Resources.UnloadAsset(textAsset);

                var import = new GltfImport();
                var settings = new ImportSettings
                {
                    GenerateMipMaps = true,
                    AnisotropicFilterLevel = 4,
                    TexturesReadable = false,
                };

                bool loaded = await import.Load(bytes, null, settings, cancellationToken);
                if (!loaded)
                {
                    Debug.LogError("[GLBPropLoader] GLTFast failed to parse Resources/" + key + ".");
                    import.Dispose();
                    return null;
                }

                var host = new GameObject("PropTemplate_" + key.Replace('/', '_'));
                host.transform.SetParent(_templateRoot, false);

                bool instantiated = await import.InstantiateMainSceneAsync(host.transform, cancellationToken);
                if (!instantiated)
                {
                    Debug.LogError("[GLBPropLoader] GLTFast parsed but could not instantiate " + key + ".");
                    import.Dispose();
                    if (Application.isPlaying) Destroy(host); else DestroyImmediate(host);
                    return null;
                }

                StripColliders(host);

                var template = new PropTemplate
                {
                    Key = key,
                    Import = import,
                    Template = host,
                    LocalSize = MeasureLocalSize(host.transform),
                    LiveInstances = 0,
                };
                _templates[key] = template;

                Debug.Log("[GLBPropLoader] Loaded " + key + " (" + FormatSize(template.LocalSize) +
                          " normalised units, " + CountLodLevels(host.transform) + " LOD levels).");
                return template;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GLBPropLoader] Loading " + key + " threw: " + exception);
                return null;
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        private GameObject InstantiateTemplate(PropTemplate template, Transform parent)
        {
            if (template == null || template.Template == null) return null;

            var instance = Instantiate(template.Template, parent, false);
            instance.SetActive(true);
            instance.name = template.Template.name.Replace("PropTemplate_", "Prop_");

            var tag = instance.AddComponent<PropInstanceTag>();
            tag.ResourceKey = template.Key;
            template.LiveInstances++;
            return instance;
        }

        /// <summary>
        /// Local scale that fits a template into its archetype's world footprint.
        ///
        /// <para>Divides out <paramref name="parentLossyScale"/> because a pickup's root is already scaled
        /// to the archetype footprint by the scene generator. Without that division a pry bar under a
        /// 0.52 x 0.12 x 0.12 root would be squashed to a ribbon — the primitive it replaces was authored
        /// as a stretched cube, the mesh was not.</para>
        /// </summary>
        private static Vector3 FitScale(PropTemplate template, PropArchetypeRegistry.Entry entry,
                                        VisualArchetype archetype, Vector3 parentLossyScale)
        {
            Vector3 footprint = PropArchetypeRegistry.FootprintOf(archetype);
            Vector3 size = template != null ? template.LocalSize : Vector3.one;
            size = new Vector3(Mathf.Max(size.x, 1e-5f), Mathf.Max(size.y, 1e-5f), Mathf.Max(size.z, 1e-5f));

            Vector3 world;
            var mode = entry != null ? entry.fitMode : PropFitMode.Uniform;
            if (mode == PropFitMode.Stretch)
            {
                world = new Vector3(footprint.x / size.x, footprint.y / size.y, footprint.z / size.z);
            }
            else
            {
                float longestFootprint = Mathf.Max(footprint.x, Mathf.Max(footprint.y, footprint.z));
                float longestSize = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                float uniform = longestFootprint / longestSize;
                world = new Vector3(uniform, uniform, uniform);
            }

            if (entry != null) world = Vector3.Scale(world, entry.extraScale);

            Vector3 parent = parentLossyScale;
            return new Vector3(
                world.x / Mathf.Max(Mathf.Abs(parent.x), 1e-5f),
                world.y / Mathf.Max(Mathf.Abs(parent.y), 1e-5f),
                world.z / Mathf.Max(Mathf.Abs(parent.z), 1e-5f));
        }

        private void ApplyLod(GameObject instance, PropArchetypeRegistry.Entry entry)
        {
            if (instance == null) return;
            if (entry != null && !entry.useLOD) return;
            int levels = entry != null ? entry.lodCount : 3;
            PropLODManager.Build(instance, levels, Camera.main);
        }

        /// <summary>
        /// Axis-aligned size of a template's LOD0 geometry in the template's own local space.
        ///
        /// <para>Reads <c>MeshFilter.sharedMesh.bounds</c> rather than <c>Renderer.bounds</c> because the
        /// template hierarchy is inactive — an inactive renderer's world bounds are not maintained and
        /// would measure as zero, silently scaling every prop to the clamp floor.</para>
        /// </summary>
        private static Vector3 MeasureLocalSize(Transform root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) return Vector3.one;

            bool started = false;
            Bounds bounds = new Bounds();
            Matrix4x4 toRoot = root.worldToLocalMatrix;

            // Prefer LOD0 alone: clustering pulls vertices inward, so LOD2 measures very slightly
            // smaller and would bias the fit if averaged in.
            for (int pass = 0; pass < 2 && !started; pass++)
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    var mesh = filters[i].sharedMesh;
                    if (mesh == null) continue;
                    if (pass == 0 && !filters[i].name.EndsWith(PropResourceKeys.LodNodeSuffix + "0",
                                                               StringComparison.Ordinal)) continue;

                    Matrix4x4 matrix = toRoot * filters[i].transform.localToWorldMatrix;
                    Bounds local = mesh.bounds;
                    Vector3 centre = local.center;
                    Vector3 extents = local.extents;

                    for (int corner = 0; corner < 8; corner++)
                    {
                        var offset = new Vector3(
                            (corner & 1) == 0 ? -extents.x : extents.x,
                            (corner & 2) == 0 ? -extents.y : extents.y,
                            (corner & 4) == 0 ? -extents.z : extents.z);
                        Vector3 point = matrix.MultiplyPoint3x4(centre + offset);
                        if (!started) { bounds = new Bounds(point, Vector3.zero); started = true; }
                        else bounds.Encapsulate(point);
                    }
                }
            }

            return started ? bounds.size : Vector3.one;
        }

        private static int CountLodLevels(Transform root)
        {
            return PropLODManager.CollectLodRenderers(root).Count;
        }

        private static void StripColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (Application.isPlaying) Destroy(colliders[i]);
                else DestroyImmediate(colliders[i]);
            }
        }

        private void DisposeTemplate(string key)
        {
            PropTemplate template;
            if (!_templates.TryGetValue(key, out template)) return;

            if (template.Template != null)
            {
                if (Application.isPlaying) Destroy(template.Template);
                else DestroyImmediate(template.Template);
            }
            if (template.Import != null) template.Import.Dispose();
            _templates.Remove(key);
        }

        private static string FormatSize(Vector3 size)
        {
            return size.x.ToString("0.00") + "x" + size.y.ToString("0.00") + "x" + size.z.ToString("0.00");
        }
    }
}
