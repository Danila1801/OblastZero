// Assets/_Project/Scripts/OblastZero.Gameplay/Props/PropLODManager.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Builds an <see cref="LODGroup"/> over a loaded prop from the LOD nodes baked into its mesh file.
    ///
    /// <para>tools/decimate_props.py emits three sibling nodes per prop — <c>&lt;name&gt;_LOD0</c>,
    /// <c>_LOD1</c>, <c>_LOD2</c> at roughly 8k/3k/1k triangles — so the LOD meshes are produced offline,
    /// deterministically, and are verifiable in the repo. Nothing is decimated at runtime; runtime
    /// simplification of a 1.4M-triangle source would cost seconds per prop and produce a different
    /// result on every machine.</para>
    ///
    /// <para><b>Distance thresholds are converted, not hardcoded.</b> LODGroup selects on screen-relative
    /// height, not on distance, so a fixed fraction means a different distance for every prop size and
    /// every field of view. <see cref="ScreenHeightAtDistance"/> does the conversion from the design
    /// intent ("swap at 10 m") to the number LODGroup actually wants.</para>
    /// </summary>
    public static class PropLODManager
    {
        /// <summary>Distance in metres at which LOD0 gives way to LOD1.</summary>
        public const float Lod0MaxDistance = 10f;
        /// <summary>Distance in metres at which LOD1 gives way to LOD2.</summary>
        public const float Lod1MaxDistance = 25f;
        /// <summary>Distance in metres beyond which the prop is culled entirely.</summary>
        public const float CullDistance = 50f;

        /// <summary>Field of view assumed when no camera is available (Unity's own default).</summary>
        public const float FallbackFieldOfView = 60f;

        private static readonly float[] DefaultDistances = { Lod0MaxDistance, Lod1MaxDistance, CullDistance };

        /// <summary>
        /// Converts a viewing distance into the screen-relative height LODGroup compares against.
        ///
        /// <para>Unity's test is <c>relativeHeight = worldSize / (2 * distance * tan(fov/2))</c>, so this
        /// is that identity solved in the direction the designer thinks in. Note Unity additionally
        /// divides distance by <see cref="QualitySettings.lodBias"/>, so a project-wide bias change
        /// shifts the real switch distance without changing these numbers — intended, that is what the
        /// bias is for.</para>
        /// </summary>
        public static float ScreenHeightAtDistance(float worldSize, float distance, float fieldOfView)
        {
            if (distance <= 0.0001f) return 1f;
            float halfAngle = Mathf.Deg2Rad * Mathf.Clamp(fieldOfView, 1f, 179f) * 0.5f;
            float denominator = 2f * distance * Mathf.Tan(halfAngle);
            if (denominator <= 0.0001f) return 1f;
            return Mathf.Clamp01(worldSize / denominator);
        }

        /// <summary>
        /// Collects the renderers of each LOD level under <paramref name="root"/>, indexed by level.
        ///
        /// <para>Matches on the <c>_LOD&lt;n&gt;</c> suffix anywhere in the hierarchy because glTFast
        /// wraps an imported scene in its own root object, so the LOD nodes are grandchildren rather
        /// than children of the instantiated prop.</para>
        /// </summary>
        public static Dictionary<int, List<Renderer>> CollectLodRenderers(Transform root)
        {
            var levels = new Dictionary<int, List<Renderer>>();
            if (root == null) return levels;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                int level = LevelOf(renderers[i].transform);
                if (level < 0) continue;
                List<Renderer> bucket;
                if (!levels.TryGetValue(level, out bucket))
                {
                    bucket = new List<Renderer>();
                    levels[level] = bucket;
                }
                bucket.Add(renderers[i]);
            }
            return levels;
        }

        /// <summary>
        /// LOD level encoded in a transform's name or any ancestor's, or -1 when none is.
        /// Walks up so a renderer on a child of an <c>_LOD1</c> node is still attributed to level 1.
        /// </summary>
        private static int LevelOf(Transform transform)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                string name = current.name;
                int marker = name.LastIndexOf(PropResourceKeys.LodNodeSuffix, System.StringComparison.Ordinal);
                if (marker < 0) continue;

                int digitStart = marker + PropResourceKeys.LodNodeSuffix.Length;
                int value = 0;
                int digits = 0;
                while (digitStart + digits < name.Length && char.IsDigit(name[digitStart + digits]))
                {
                    value = value * 10 + (name[digitStart + digits] - '0');
                    digits++;
                }
                if (digits > 0) return value;
            }
            return -1;
        }

        /// <summary>
        /// Wires an <see cref="LODGroup"/> onto <paramref name="root"/> from its LOD nodes.
        ///
        /// <para>Returns the group, or null when the prop carries no LOD nodes — in which case the
        /// renderers are simply left alone and draw at all distances, which is correct behaviour for a
        /// single-LOD prop rather than something to warn about.</para>
        /// </summary>
        /// <param name="root">Instantiated prop root.</param>
        /// <param name="maxLevels">Cap on LOD levels to wire, from the registry entry.</param>
        /// <param name="camera">Camera whose FOV drives the distance conversion; null uses the fallback.</param>
        /// <param name="distances">Switch distances in metres, ascending. Null uses the defaults.</param>
        public static LODGroup Build(GameObject root, int maxLevels, Camera camera, float[] distances = null)
        {
            if (root == null) return null;

            var levels = CollectLodRenderers(root.transform);
            if (levels.Count == 0) return null;

            var present = new List<int>(levels.Keys);
            present.Sort();
            if (maxLevels > 0 && present.Count > maxLevels) present.RemoveRange(maxLevels, present.Count - maxLevels);
            if (present.Count == 0) return null;

            // Only the highest-quality level should be visible before LODGroup takes over; levels the
            // cap excluded must be hidden, or a trimmed LOD2 keeps drawing on top of LOD0 forever.
            foreach (var pair in levels)
            {
                bool included = present.Contains(pair.Key);
                for (int i = 0; i < pair.Value.Count; i++) pair.Value[i].enabled = included;
            }

            var group = root.GetComponent<LODGroup>();
            if (group == null) group = root.AddComponent<LODGroup>();

            // Seed size and reference point from the actual renderer bounds before computing thresholds;
            // this is the same measurement Unity itself uses, so the two cannot disagree.
            var seedLods = new LOD[present.Count];
            for (int i = 0; i < present.Count; i++) seedLods[i] = new LOD(0.01f, levels[present[i]].ToArray());
            group.SetLODs(seedLods);
            group.RecalculateBounds();

            float maxAxisScale = Mathf.Max(Mathf.Abs(root.transform.lossyScale.x),
                                  Mathf.Max(Mathf.Abs(root.transform.lossyScale.y),
                                            Mathf.Abs(root.transform.lossyScale.z)));
            float worldSize = group.size * Mathf.Max(maxAxisScale, 0.0001f);
            float fieldOfView = camera != null ? camera.fieldOfView : FallbackFieldOfView;
            float[] switchDistances = distances ?? DefaultDistances;

            var lods = new LOD[present.Count];
            float previousThreshold = 1f;
            for (int i = 0; i < present.Count; i++)
            {
                // The last wired level always uses the cull distance, so trimming to fewer levels
                // shortens the chain rather than silently culling the prop at 10 m.
                float distance = (i == present.Count - 1)
                    ? switchDistances[switchDistances.Length - 1]
                    : switchDistances[Mathf.Min(i, switchDistances.Length - 1)];

                float threshold = ScreenHeightAtDistance(worldSize, distance, fieldOfView);
                // LODGroup requires strictly descending thresholds; equal values throw.
                threshold = Mathf.Min(threshold, previousThreshold - 0.0001f);
                threshold = Mathf.Max(threshold, 0.000001f);
                previousThreshold = threshold;

                lods[i] = new LOD(threshold, levels[present[i]].ToArray());
            }

            group.SetLODs(lods);
            group.fadeMode = LODFadeMode.None;
            return group;
        }
    }
}
