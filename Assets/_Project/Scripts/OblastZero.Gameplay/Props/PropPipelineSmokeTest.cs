// Assets/_Project/Scripts/OblastZero.Gameplay/Props/PropPipelineSmokeTest.cs
using System.Collections;
using System.Collections.Generic;
using OblastZero.Data;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Play-mode verification of the prop pipeline: that the decimated meshes actually load through
    /// GLTFast, that a loaded prop lands at the size its archetype reserves, that the LODGroup is wired
    /// with sane thresholds, and that releasing instances returns the memory.
    ///
    /// <para>Written as a <c>[ContextMenu]</c> MonoBehaviour rather than an NUnit test because this
    /// project has no test assembly — DataLayerSmokeTest, EventEngineSmokeTest and ScavengeLogicTest all
    /// follow the same shape, and the Test Runner does not see any of them.</para>
    ///
    /// <para><b>Must run in Play mode.</b> The loader awaits GLTFast, which needs the player loop
    /// ticking; in Edit mode the coroutine would never resume and the test would hang looking like a
    /// failure. The entry point checks and says so rather than leaving you guessing.</para>
    ///
    /// USAGE: attach to an empty GameObject, enter Play mode, right-click the component →
    /// "Run Prop Pipeline Smoke Test".
    /// </summary>
    public class PropPipelineSmokeTest : MonoBehaviour
    {
        private int _checks;
        private int _passed;
        private readonly List<string> _failures = new List<string>();

        [ContextMenu("Run Prop Pipeline Smoke Test")]
        public void Run()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PropPipelineSmokeTest] Enter Play mode first — the loader awaits " +
                                 "GLTFast, which needs the player loop running.");
                return;
            }
            StartCoroutine(RunRoutine());
        }

        private IEnumerator RunRoutine()
        {
            _checks = 0;
            _passed = 0;
            _failures.Clear();
            Debug.Log("──────── PROP PIPELINE SMOKE TEST ────────");

            var loader = GLBPropLoader.Instance;
            Check("loader singleton available", loader != null);
            if (loader == null) { Report(); yield break; }

            var registry = loader.Registry;
            Check("registry resolved", registry != null);
            Check("registry covers every renderable archetype",
                  registry != null && registry.Entries.Count == System.Enum.GetValues(typeof(VisualArchetype)).Length - 1,
                  registry != null ? registry.Entries.Count + " entries" : "none");

            // ── Preload ────────────────────────────────────────────────────────────────────────
            float startedAt = Time.realtimeSinceStartup;
            yield return loader.PreloadAllRoutine();
            float elapsed = Time.realtimeSinceStartup - startedAt;

            int authored = 0;
            foreach (var archetype in PropResourceKeys.AuthoredArchetypes()) authored++;
            Check("every authored prop loaded", loader.CachedTemplateCount == authored,
                  loader.CachedTemplateCount + "/" + authored + " in " + elapsed.ToString("0.00") + "s");

            // ── Fit: a loaded prop must occupy the footprint its archetype reserves ────────────
            foreach (var archetype in PropResourceKeys.AuthoredArchetypes())
            {
                var host = new GameObject("FitProbe_" + archetype);
                // Reproduce what the scene generator does: the pickup root is pre-scaled to the
                // archetype's local scale, and the visual is parented underneath it.
                host.transform.localScale = VisualArchetypeMapping.ShapeOf(archetype).LocalScale;

                var visual = loader.CreateVisual(archetype, host.transform, null);
                Check(archetype + ": produced a visual", visual != null);
                if (visual == null) { Destroy(host); continue; }

                Check(archetype + ": is a real mesh, not the primitive fallback",
                      visual.GetComponent<PropInstanceTag>() != null);

                Bounds bounds;
                bool measured = TryMeasureWorldBounds(visual, out bounds);
                Check(archetype + ": has renderable geometry", measured);

                if (measured)
                {
                    Vector3 footprint = PropArchetypeRegistry.FootprintOf(archetype);
                    float expected = Mathf.Max(footprint.x, Mathf.Max(footprint.y, footprint.z));
                    float actual = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                    // 5% tolerance: clustering pulls the silhouette in slightly, and the fit is
                    // measured from LOD0 bounds rather than the exact convex hull.
                    Check(archetype + ": fits its footprint's longest axis",
                          Mathf.Abs(actual - expected) <= expected * 0.05f,
                          "expected " + expected.ToString("0.000") + " m, got " + actual.ToString("0.000") + " m");
                    Check(archetype + ": is not degenerate",
                          bounds.size.x > 0.001f && bounds.size.y > 0.001f && bounds.size.z > 0.001f);
                }

                // ── LOD wiring ────────────────────────────────────────────────────────────────
                var group = visual.GetComponent<LODGroup>();
                Check(archetype + ": LODGroup built", group != null);
                if (group != null)
                {
                    var lods = group.GetLODs();
                    Check(archetype + ": three LOD levels", lods.Length == 3, lods.Length + " levels");
                    bool descending = true;
                    for (int i = 1; i < lods.Length; i++)
                    {
                        if (lods[i].screenRelativeTransitionHeight >= lods[i - 1].screenRelativeTransitionHeight)
                            descending = false;
                    }
                    Check(archetype + ": LOD thresholds strictly descend", descending);
                    Check(archetype + ": every LOD level owns renderers",
                          AllLevelsPopulated(lods));
                }

                loader.ReleaseProp(visual);
                Destroy(host);
            }

            // ── The distance -> screen-height conversion the thresholds rest on ────────────────
            float nearHeight = PropLODManager.ScreenHeightAtDistance(0.34f, PropLODManager.Lod0MaxDistance, 60f);
            float farHeight = PropLODManager.ScreenHeightAtDistance(0.34f, PropLODManager.CullDistance, 60f);
            Check("nearer distance yields a larger screen height", nearHeight > farHeight,
                  nearHeight.ToString("0.0000") + " > " + farHeight.ToString("0.0000"));
            Check("screen heights stay in the 0..1 range LODGroup expects",
                  nearHeight > 0f && nearHeight <= 1f && farHeight > 0f && farHeight <= 1f);
            Check("a bigger prop switches later at the same distance",
                  PropLODManager.ScreenHeightAtDistance(1.0f, 10f, 60f) >
                  PropLODManager.ScreenHeightAtDistance(0.34f, 10f, 60f));

            // ── Fallback: an archetype with no authored mesh must still produce something ──────
            var fallbackHost = new GameObject("FallbackProbe");
            var fallback = loader.CreateVisual(VisualArchetype.Document, fallbackHost.transform, null);
            Check("unauthored archetype still yields a visual", fallback != null);
            Check("unauthored archetype falls back to a primitive, not a mesh",
                  fallback != null && fallback.GetComponent<PropInstanceTag>() == null);
            Check("primitive fallback carries no collider to block the player",
                  fallback != null && fallback.GetComponentInChildren<Collider>() == null);
            Destroy(fallbackHost);

            // ── Alias resolution ──────────────────────────────────────────────────────────────
            VisualArchetype resolved;
            Check("alias 'crate_wooden' resolves to Crate",
                  PropResourceKeys.TryParseArchetype("crate_wooden", out resolved) && resolved == VisualArchetype.Crate);
            Check("enum name 'ammunitionbox' resolves case-insensitively",
                  PropResourceKeys.TryParseArchetype("ammunitionbox", out resolved) && resolved == VisualArchetype.AmmunitionBox);
            Check("a numeric string is NOT accepted as an archetype",
                  !PropResourceKeys.TryParseArchetype("7", out resolved));
            Check("nonsense is rejected", !PropResourceKeys.TryParseArchetype("not_a_prop", out resolved));

            // ── Release ───────────────────────────────────────────────────────────────────────
            int reclaimed = loader.ReleaseUnused();
            Check("released every idle template", reclaimed == authored,
                  reclaimed + "/" + authored + " reclaimed");
            Check("cache is empty after release", loader.CachedTemplateCount == 0);

            Report();
        }

        private static bool AllLevelsPopulated(LOD[] lods)
        {
            for (int i = 0; i < lods.Length; i++)
            {
                if (lods[i].renderers == null || lods[i].renderers.Length == 0) return false;
            }
            return true;
        }

        /// <summary>
        /// World bounds of the currently-enabled renderers. Only LOD0 is enabled after
        /// <see cref="PropLODManager.Build"/> runs, so this measures the high-detail silhouette —
        /// which is what the fit is defined against.
        /// </summary>
        private static bool TryMeasureWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            bool started = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                if (!started) { bounds = renderers[i].bounds; started = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            return started;
        }

        private void Check(string label, bool condition, string detail = null)
        {
            _checks++;
            if (condition)
            {
                _passed++;
                Debug.Log("  [PASS] " + label + (detail != null ? " — " + detail : ""));
            }
            else
            {
                _failures.Add(label + (detail != null ? " — " + detail : ""));
                Debug.LogError("  [FAIL] " + label + (detail != null ? " — " + detail : ""));
            }
        }

        private void Report()
        {
            Debug.Log("──────── " + _passed + "/" + _checks + " checks passed ────────");
            for (int i = 0; i < _failures.Count; i++) Debug.LogError("  FAILED: " + _failures[i]);
        }
    }
}
