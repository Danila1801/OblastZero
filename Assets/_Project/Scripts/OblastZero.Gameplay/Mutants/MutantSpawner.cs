// Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/MutantSpawner.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Mutants
{
    /// <summary>
    /// Populates a scavenge level with mutants according to the site's threat profile
    /// (<see cref="ScavengeSiteCatalog"/>). One of these sits in each scavenge scene; it reads which
    /// site the run registered for, spawns accordingly, and then gets out of the way.
    ///
    /// <para><b>Threat is a property of the site, not of the scene.</b> The bible places the Drowned
    /// Census-Taker in the Census District and the Reservoir and nowhere else, and makes the Editor
    /// rare and late. Encoding that in each scene generator would mean re-deciding it every time a
    /// level is authored, and the two would drift. Putting it on the site means a new level inherits
    /// a coherent threat profile by declaring which region it is in.</para>
    ///
    /// <para><b>Every draw comes off the run's RNG stream.</b> Whether an Editor appears at all is a
    /// single <c>RunRng</c> draw, so a seed reproduces the same run — including the runs where nothing
    /// showed up. Using <c>UnityEngine.Random</c> here would quietly make seeded runs irreproducible
    /// in exactly the way that is hardest to notice.</para>
    ///
    /// <para><b>Spawning is deferred until the player exists.</b> The scavenge scene streams in
    /// asynchronously and <c>ScavengePhase3DState</c> binds the controller on its first tick that finds
    /// one, so a spawner running in Awake would find no player and initialise every mutant with a null
    /// target — a scene full of things standing perfectly still.</para>
    /// </summary>
    public class MutantSpawner : MonoBehaviour
    {
        [Tooltip("Where Census-Takers may appear. Each is placed at one of these, in order. " +
                 "If empty, positions are drawn from the navigation grid instead.")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Metres from the player below which a spawn point is rejected as too close.")]
        [SerializeField] private float minimumSpawnDistance = 18f;

        [Tooltip("Seconds to wait for the player before giving up. The scene streams in async.")]
        [SerializeField] private float playerWaitTimeout = 10f;

        private ScavengePlayerController _player;
        private ScavengeNavGrid _grid;
        private RunRng _rng;
        private float _waitedFor;
        private bool _spawned;

        private readonly List<DrownedCensusTaker> _censusTakers = new List<DrownedCensusTaker>();
        private TheEditor _editor;

        /// <summary>Census-Takers currently alive in the scene. Read by the HUD and by tests.</summary>
        public IReadOnlyList<DrownedCensusTaker> CensusTakers { get { return _censusTakers; } }

        /// <summary>The Editor, if one appeared this run. Null is the common case.</summary>
        public TheEditor Editor { get { return _editor; } }

        private void Update()
        {
            if (_spawned) return;

            if (_player == null)
            {
                _player = FindObjectOfType<ScavengePlayerController>();
                _waitedFor += Time.deltaTime;

                if (_player == null)
                {
                    if (_waitedFor < playerWaitTimeout) return;

                    // A headless Phase A (no level loaded) is a supported state — the clock still runs
                    // and the phase resolves. Spawning nothing is correct there; saying so once is
                    // what keeps it from looking like the spawner failed.
                    Debug.Log("[MutantSpawner] No player after " +
                              $"{playerWaitTimeout:0}s — the phase is running headless. No mutants.");
                    _spawned = true;
                    return;
                }
            }

            _spawned = true;
            SpawnForCurrentSite();
        }

        private void SpawnForCurrentSite()
        {
            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            if (run == null)
            {
                Debug.LogWarning("[MutantSpawner] No active run — no site to read a threat profile from.");
                return;
            }

            _rng = new RunRng(run);

            var site = ScavengeSiteCatalog.Get(run.currentScavengeSiteId);
            if (site == null)
            {
                Debug.LogWarning($"[MutantSpawner] Site '{run.currentScavengeSiteId}' is not in the " +
                                 "catalogue. No mutants.");
                return;
            }

            _grid = ScavengeNavGrid.Load(ScavengeNavGrid.ScavengeResourceKey);

            Debug.Log($"[MutantSpawner] Site '{site.Id}' ({site.RegionDisplayName}): " +
                      $"{site.CensusTakerCount} Census-Taker(s), " +
                      $"Editor chance {site.EditorSpawnChance:P0}.");

            for (int i = 0; i < site.CensusTakerCount; i++) SpawnCensusTaker(i);
            MaybeSpawnEditor(site.EditorSpawnChance);
        }

        private void SpawnCensusTaker(int index)
        {
            Vector3 position;
            if (!ResolveSpawnPosition(index, out position))
            {
                Debug.LogWarning($"[MutantSpawner] No valid spawn position for Census-Taker {index + 1}.");
                return;
            }

            var go = BuildFigure("Mutant_DrownedCensusTaker_" + (index + 1), position,
                                 new Color(0.19f, 0.21f, 0.20f), 1.85f);

            var mutant = go.AddComponent<DrownedCensusTaker>();
            mutant.Initialize(_player, _grid);
            _censusTakers.Add(mutant);

            float distance = Vector3.Distance(position, _player.transform.position);
            Debug.Log($"[{DrownedCensusTaker.ClassificationCode}] Spawned at {position} " +
                      $"({distance:0.#} m from the player).");
        }

        private void MaybeSpawnEditor(float chance)
        {
            if (chance <= 0f) return;
            if (!_rng.Chance(chance))
            {
                Debug.Log($"[{TheEditor.ClassificationCode}] Did not appear this run.");
                return;
            }

            Vector3 position;
            if (!ResolveEditorPosition(out position))
            {
                Debug.LogWarning("[MutantSpawner] Nowhere valid to place the Editor. It stays away.");
                return;
            }

            var go = BuildFigure("Mutant_TheEditor", position, new Color(0.29f, 0.26f, 0.22f), 1.80f);

            _editor = go.AddComponent<TheEditor>();
            _editor.Initialize(_player, Camera.main, _rng);
        }

        /// <summary>
        /// A spawn point, or a grid cell far enough from the player. Authored points are preferred
        /// because a level designer knows which corners read as somewhere a figure could have come
        /// from; the grid fallback exists so a scene that has not been dressed with spawn points still
        /// produces a working mutant rather than none.
        /// </summary>
        private bool ResolveSpawnPosition(int index, out Vector3 position)
        {
            position = Vector3.zero;
            Vector3 playerPos = _player.transform.position;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                for (int offset = 0; offset < spawnPoints.Length; offset++)
                {
                    var candidate = spawnPoints[(index + offset) % spawnPoints.Length];
                    if (candidate == null) continue;
                    if (Vector3.Distance(candidate.position, playerPos) < minimumSpawnDistance) continue;
                    position = candidate.position;
                    return true;
                }
            }

            if (_grid == null) return false;

            // Behind the player, at spawn distance, snapped to the grid. Appearing in front would
            // announce the mutant before it has done anything; the bible's Census-Taker is something
            // you notice has been there a while.
            Vector3 behind = playerPos - _player.transform.forward * minimumSpawnDistance;
            Vector3 snapped = _grid.NearestPassable(behind, 8f);

            if (Vector3.Distance(snapped, playerPos) < minimumSpawnDistance * 0.5f) return false;

            position = snapped;
            return true;
        }

        private bool ResolveEditorPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (_player == null) return false;

            Vector3 playerPos = _player.transform.position;

            // In front and to one side: the Editor is meant to be seen. Which side is a run-stream
            // draw so a seeded replay puts it in the same place.
            float side = _rng.Chance(0.5f) ? 1f : -1f;
            Vector3 ahead = playerPos
                            + _player.transform.forward * BalanceConstants.EDITOR_SPAWN_DISTANCE_M
                            + _player.transform.right * (side * 4f);

            position = _grid != null ? _grid.NearestPassable(ahead, 10f) : ahead;
            return Vector3.Distance(position, playerPos) > 6f;
        }

        /// <summary>
        /// A capsule standing on the ground, with no collider.
        ///
        /// <para>Both mutants are deliberately non-colliding. The Census-Taker does not attack and the
        /// Editor is an apparition, so neither needs physics — and a collider on either would let the
        /// player body-block a thing that is supposed to be inevitable, or get wedged between a stalker
        /// and a wall in a phase where being stuck for four seconds loses the run.</para>
        ///
        /// <para>Built in code rather than as a prefab for the same reason the scene is generated: a
        /// prefab is a binary asset an outside process cannot author, and the figure is one capsule.</para>
        /// </summary>
        private static GameObject BuildFigure(string name, Vector3 groundPosition, Color tint, float height)
        {
            var go = new GameObject(name);
            go.transform.position = groundPosition + Vector3.up * (height * 0.5f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Silhouette";
            body.transform.SetParent(go.transform, false);

            // A Unity capsule mesh is 2 units tall, so the scale is height/2 — the trap CLAUDE.md §14
            // calls out for primitive extents, and the reason a naive height-as-scale figure would
            // stand two metres out of the floor.
            body.transform.localScale = new Vector3(0.55f, height * 0.5f, 0.55f);

            var collider = body.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            var renderer = body.GetComponent<Renderer>();
            if (renderer != null)
            {
                // MaterialPropertyBlock, not .material: writing the shared material would tint every
                // primitive capsule in the scene and leave the .mat dirty in the Editor (CLAUDE.md §14).
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                renderer.SetPropertyBlock(block);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }

            return go;
        }
    }
}
