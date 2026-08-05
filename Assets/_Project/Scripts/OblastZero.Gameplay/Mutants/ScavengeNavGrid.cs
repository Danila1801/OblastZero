// Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/ScavengeNavGrid.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Gameplay.Mutants
{
    /// <summary>
    /// The scavenge level's navigation grid: a walkability height-field emitted by
    /// <c>tools/generate_scavenge_scene.py</c> and loaded from Resources as a TextAsset.
    ///
    /// <para><b>Why this exists instead of a NavMesh.</b> A NavMesh can only be produced by an
    /// interactive Editor bake, and this project's levels are generated headlessly and validated for
    /// byte-determinism. Requiring a manual bake would put a human step in the middle of an automated
    /// pipeline, and — worse — it would create a <i>second</i> answer to "where can something walk
    /// here", derived differently from the one the generator already computes. The generator's
    /// flood-fill models the real CharacterController (height 1.8, radius 0.35, step offset 0.32)
    /// against the real geometry, and it carries a negative control proving it detects a sealed route.
    /// Exporting that field means the stalker and the shipping gate agree by construction; a baked
    /// NavMesh could quietly disagree with the check that decides whether the level is playable.</para>
    ///
    /// <para>Only cells reachable from the player spawn are marked passable. Walkable floor behind a
    /// sealed wall is walkable and useless: an agent pathing into it could never reach the player and
    /// would stand there looking broken.</para>
    ///
    /// <para><b>The asset must be <c>.bytes</c>.</b> A <c>.bin</c> under Resources is not imported as
    /// a TextAsset and <c>Resources.Load&lt;TextAsset&gt;</c> returns null for it, with no error —
    /// the same trap documented for the GLB props in <c>docs/PROP_PIPELINE.md</c>.</para>
    /// </summary>
    public sealed class ScavengeNavGrid
    {
        /// <summary>Resources path (no extension) of the depot's grid.</summary>
        public const string ScavengeResourceKey = "Nav/navgrid_scavenge";

        private const int MagicLength = 8;
        private const short Impassable = -32768;

        /// <summary>Cells an A* query will expand before giving up. Bounds a pathological search.</summary>
        private const int MaxExpansions = 6000;

        private readonly float _originX;
        private readonly float _originZ;
        private readonly float _step;
        private readonly int _nx;
        private readonly int _nz;
        private readonly short[] _cells;

        // Reused across queries. One agent asks for a path a few times a second at most, and the
        // grid is 30k cells, so allocating three arrays per request would be pure garbage.
        private readonly Dictionary<int, int> _cameFrom = new Dictionary<int, int>(512);
        private readonly Dictionary<int, float> _costSoFar = new Dictionary<int, float>(512);
        private readonly List<int> _open = new List<int>(512);
        private readonly List<Vector3> _path = new List<Vector3>(64);

        private ScavengeNavGrid(float originX, float originZ, float step, int nx, int nz, short[] cells)
        {
            _originX = originX;
            _originZ = originZ;
            _step = step;
            _nx = nx;
            _nz = nz;
            _cells = cells;
        }

        /// <summary>Grid dimensions, for logging and tests.</summary>
        public int CellsX { get { return _nx; } }
        public int CellsZ { get { return _nz; } }
        public float CellSize { get { return _step; } }

        /// <summary>
        /// Loads a grid from Resources, or null when the asset is absent or malformed. A null return
        /// is a survivable condition, not a crash: the caller falls back to direct steering, which is
        /// wrong around walls but is a mutant that moves badly rather than a scene that does not run.
        /// </summary>
        public static ScavengeNavGrid Load(string resourceKey)
        {
            var asset = Resources.Load<TextAsset>(resourceKey);
            if (asset == null)
            {
                Debug.LogWarning($"[ScavengeNavGrid] No TextAsset at Resources/{resourceKey}. " +
                                 "Check the file is '.bytes' — a '.bin' under Resources loads as null.");
                return null;
            }

            var grid = Parse(asset.bytes, resourceKey);
            if (grid != null)
                Debug.Log($"[ScavengeNavGrid] Loaded '{resourceKey}': {grid._nx}x{grid._nz} cells " +
                          $"@ {grid._step:0.##} m, origin ({grid._originX:0.#}, {grid._originZ:0.#}).");
            return grid;
        }

        /// <summary>Parses the binary format. Separated from <see cref="Load"/> so it is testable.</summary>
        public static ScavengeNavGrid Parse(byte[] blob, string label)
        {
            if (blob == null || blob.Length < MagicLength + 20)
            {
                Debug.LogError($"[ScavengeNavGrid] '{label}' is too short to be a grid.");
                return null;
            }

            if (blob[0] != 'O' || blob[1] != 'Z' || blob[2] != 'N' || blob[3] != 'A' ||
                blob[4] != 'V' || blob[5] != '1')
            {
                Debug.LogError($"[ScavengeNavGrid] '{label}' has the wrong magic bytes.");
                return null;
            }

            int offset = MagicLength;
            float originX = BitConverter.ToSingle(blob, offset); offset += 4;
            float originZ = BitConverter.ToSingle(blob, offset); offset += 4;
            float step = BitConverter.ToSingle(blob, offset); offset += 4;
            int nx = BitConverter.ToInt32(blob, offset); offset += 4;
            int nz = BitConverter.ToInt32(blob, offset); offset += 4;

            if (nx <= 0 || nz <= 0 || step <= 0f)
            {
                Debug.LogError($"[ScavengeNavGrid] '{label}' header is nonsense: " +
                               $"{nx}x{nz} @ {step}.");
                return null;
            }

            long expected = (long)offset + (long)nx * nz * 2;
            if (blob.Length != expected)
            {
                Debug.LogError($"[ScavengeNavGrid] '{label}' is {blob.Length} bytes, " +
                               $"expected {expected} for {nx}x{nz} cells.");
                return null;
            }

            var cells = new short[nx * nz];
            Buffer.BlockCopy(blob, offset, cells, 0, cells.Length * 2);

            return new ScavengeNavGrid(originX, originZ, step, nx, nz, cells);
        }

        // ── Queries ──────────────────────────────────────────────────────────

        private int Index(int i, int j) { return i * _nz + j; }

        private bool InBounds(int i, int j) { return i >= 0 && i < _nx && j >= 0 && j < _nz; }

        private bool Passable(int i, int j)
        {
            return InBounds(i, j) && _cells[Index(i, j)] != Impassable;
        }

        private void CellOf(Vector3 world, out int i, out int j)
        {
            i = Mathf.RoundToInt((world.x - _originX) / _step);
            j = Mathf.RoundToInt((world.z - _originZ) / _step);
        }

        private Vector3 WorldOf(int i, int j)
        {
            return new Vector3(_originX + i * _step,
                               _cells[Index(i, j)] * 0.01f,
                               _originZ + j * _step);
        }

        /// <summary>True when an agent may stand at this world position.</summary>
        public bool IsPassable(Vector3 world)
        {
            int i, j;
            CellOf(world, out i, out j);
            return Passable(i, j);
        }

        /// <summary>
        /// The nearest passable cell to a world position, as a world point, or the input unchanged
        /// when nothing passable is within <paramref name="searchRadiusMetres"/>.
        ///
        /// <para>Needed at both ends of every query. The player can stand on a crate whose own column
        /// is not standable, and a spawn point authored by hand lands wherever it lands; snapping is
        /// what stops either from producing "no path" for a route that plainly exists.</para>
        /// </summary>
        public Vector3 NearestPassable(Vector3 world, float searchRadiusMetres = 3f)
        {
            int ci, cj;
            CellOf(world, out ci, out cj);
            if (Passable(ci, cj)) return WorldOf(ci, cj);

            int r = Mathf.Max(1, Mathf.CeilToInt(searchRadiusMetres / _step));
            int bestI = -1, bestJ = -1, bestD = int.MaxValue;

            for (int di = -r; di <= r; di++)
            {
                for (int dj = -r; dj <= r; dj++)
                {
                    int i = ci + di, j = cj + dj;
                    if (!Passable(i, j)) continue;
                    int d = di * di + dj * dj;
                    if (d >= bestD) continue;
                    bestD = d; bestI = i; bestJ = j;
                }
            }

            return bestI < 0 ? world : WorldOf(bestI, bestJ);
        }

        /// <summary>
        /// A* from one world position to another. Returns a shared, reused list of waypoints — copy
        /// it if you need to keep it past the next query. Empty when no route exists.
        ///
        /// <para>Four-connected, matching the generator's flood-fill exactly. Eight-connectivity
        /// would produce visually nicer diagonals and would also cut corners the flood-fill treats
        /// as blocked, which is precisely how an agent ends up clipping a doorframe: the two would
        /// no longer be answering the same question.</para>
        /// </summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            _path.Clear();

            int si, sj, gi, gj;
            CellOf(NearestPassable(from), out si, out sj);
            CellOf(NearestPassable(to), out gi, out gj);

            if (!Passable(si, sj) || !Passable(gi, gj)) return _path;
            if (si == gi && sj == gj) { _path.Add(WorldOf(gi, gj)); return _path; }

            _cameFrom.Clear();
            _costSoFar.Clear();
            _open.Clear();

            int start = Index(si, sj), goal = Index(gi, gj);
            _costSoFar[start] = 0f;
            _open.Add(start);

            int expansions = 0;
            bool reached = false;

            while (_open.Count > 0)
            {
                if (++expansions > MaxExpansions)
                {
                    // A bounded search that gives up is better than a frame spike. The caller keeps
                    // its previous path, so the agent carries on rather than stopping dead.
                    Debug.LogWarning($"[ScavengeNavGrid] A* hit the {MaxExpansions}-cell budget; " +
                                     "returning no path this query.");
                    return _path;
                }

                // Linear scan for the best open node. The open set peaks in the low hundreds on a
                // 209x145 grid, so a binary heap would cost more in complexity than it saves.
                int bestAt = 0;
                float bestF = float.MaxValue;
                for (int k = 0; k < _open.Count; k++)
                {
                    int node = _open[k];
                    float f = _costSoFar[node] + Heuristic(node, gi, gj);
                    if (f >= bestF) continue;
                    bestF = f; bestAt = k;
                }

                int current = _open[bestAt];
                _open.RemoveAt(bestAt);

                if (current == goal) { reached = true; break; }

                int ci = current / _nz, cj = current % _nz;
                float cost = _costSoFar[current];

                for (int d = 0; d < 4; d++)
                {
                    int ni = ci + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int nj = cj + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (!Passable(ni, nj)) continue;

                    int neighbour = Index(ni, nj);
                    float next = cost + 1f;

                    float known;
                    if (_costSoFar.TryGetValue(neighbour, out known) && known <= next) continue;

                    _costSoFar[neighbour] = next;
                    _cameFrom[neighbour] = current;
                    if (!_open.Contains(neighbour)) _open.Add(neighbour);
                }
            }

            if (!reached) return _path;

            // Walk back, then reverse in place — cheaper than inserting at the head each step.
            int cursor = goal;
            while (cursor != start)
            {
                _path.Add(WorldOf(cursor / _nz, cursor % _nz));
                if (!_cameFrom.TryGetValue(cursor, out cursor)) break;
            }
            _path.Reverse();
            return _path;
        }

        private float Heuristic(int node, int gi, int gj)
        {
            return Mathf.Abs(node / _nz - gi) + Mathf.Abs(node % _nz - gj);
        }
    }
}
