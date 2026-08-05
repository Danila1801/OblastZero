// Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/DrownedCensusTaker.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Mutants
{
    /// <summary>
    /// MTN-Β-04/DC — The Drowned Census-Taker. A slow stalker that does not attack (BESTIARY.md §4).
    /// It follows, and if the player stands still in its line of sight for ten seconds it closes to
    /// clipboard range and spends fifteen seconds writing their name down. Completing the entry costs
    /// permanent health and sanity for the rest of the run, and registrations stack.
    ///
    /// <para><b>The threat is a stop-timer, so the counter-play is simply to keep moving.</b> It walks
    /// at 1.2 m/s against the player's 4.5 m/s walk, which means it can never catch anyone who is
    /// going anywhere — the bible is explicit that the counter-tactic is "keep moving, do not stop".
    /// What makes that a real cost in a sixty-second phase is that looting requires standing still:
    /// every shelf the player works is ten seconds they are not spending on the clock, and the mutant
    /// turns that into a decision rather than a formality.</para>
    ///
    /// <para><b>Stopped is measured from realised motion, not from input.</b>
    /// <c>ScavengePlayerController.HorizontalSpeed</c> is computed from what the CharacterController
    /// actually moved, so a player holding forward against a wall counts as stopped. That is correct:
    /// they are not going anywhere, and reading the input axis would let them defeat the mechanic by
    /// pressing a key.</para>
    ///
    /// <para><b>Navigation is the exported grid, not a NavMesh.</b> See <see cref="ScavengeNavGrid"/>
    /// for why. With no grid it falls back to direct steering, which is wrong around walls but keeps
    /// the mutant moving rather than deleting it from the level.</para>
    /// </summary>
    public class DrownedCensusTaker : MonoBehaviour
    {
        /// <summary>Bible classification: slow-stalker mutant, beta series.</summary>
        public const string ClassificationCode = "MTN-Β-04/DC";

        [Tooltip("Walking pace, m/s. Mirrors BalanceConstants.CENSUS_TAKER_MOVE_SPEED.")]
        [SerializeField] private float moveSpeed = BalanceConstants.CENSUS_TAKER_MOVE_SPEED;

        [Tooltip("Metres within which it begins following. Mirrors BalanceConstants.CENSUS_TAKER_AGGRO_RANGE_M.")]
        [SerializeField] private float aggroRange = BalanceConstants.CENSUS_TAKER_AGGRO_RANGE_M;

        [Tooltip("Layers that block line of sight. Should exclude the player and other triggers.")]
        [SerializeField] private LayerMask sightBlockers = ~0;

        [Tooltip("Seconds between path recalculations. The player outruns a stale path quickly, but " +
                 "re-pathing every frame on a 30k-cell grid is wasted work at 1.2 m/s.")]
        [SerializeField] private float repathInterval = 0.4f;

        /// <summary>What the mutant is currently doing. Read by the HUD and by tests.</summary>
        public enum State { Idle, Pursuing, Writing }

        /// <summary>Current behaviour state.</summary>
        public State CurrentState { get; private set; }

        /// <summary>Registration progress in [0,1]. 0 unless <see cref="CurrentState"/> is Writing.</summary>
        public float RegistrationProgress01
        {
            get
            {
                float total = Mathf.Max(0.01f, BalanceConstants.CENSUS_TAKER_REGISTRATION_SECONDS);
                return Mathf.Clamp01(_registrationTimer / total);
            }
        }

        private ScavengePlayerController _player;
        private CharacterController _playerController;
        private ScavengeNavGrid _grid;

        private System.Collections.Generic.List<Vector3> _path;
        private int _pathCursor;
        private float _nextRepathAt;

        private float _stopTimer;
        private float _registrationTimer;
        private bool _hadLineOfSight;

        private void Awake()
        {
            CurrentState = State.Idle;
        }

        /// <summary>
        /// Called by <see cref="MutantSpawner"/> after placement. The grid is passed in rather than
        /// loaded here so a site with several Census-Takers parses the 60 KB asset once.
        /// </summary>
        public void Initialize(ScavengePlayerController player, ScavengeNavGrid grid)
        {
            _player = player;
            _grid = grid;
            _playerController = player != null ? player.GetComponent<CharacterController>() : null;

            if (_grid == null)
                Debug.LogWarning($"[{ClassificationCode}] No navigation grid — falling back to direct " +
                                 "steering. It will follow, but it will not route around walls.");
        }

        private void Update()
        {
            if (_player == null) return;

            float dt = Time.deltaTime;
            Vector3 playerPos = _player.transform.position;
            float distance = Vector3.Distance(transform.position, playerPos);
            bool sees = HasLineOfSight(playerPos);

            if (sees != _hadLineOfSight)
            {
                _hadLineOfSight = sees;
                // Losing sight ends the stop timer. The bible's condition is "stops moving within
                // line of sight", so breaking the line is a second, spatial counter-play alongside
                // simply walking away — worth having in a level with this many blind corners.
                if (!sees) ResetStopTimer("line of sight broken");
            }

            switch (CurrentState)
            {
                case State.Writing:
                    TickWriting(dt, distance, sees);
                    break;

                default:
                    TickPursuit(dt, distance, sees, playerPos);
                    break;
            }
        }

        private void TickPursuit(float dt, float distance, bool sees, Vector3 playerPos)
        {
            bool shouldPursue = sees && distance <= aggroRange;

            if (shouldPursue && CurrentState != State.Pursuing)
            {
                CurrentState = State.Pursuing;
                Debug.Log($"[{ClassificationCode}] Has the player at {distance:0.#} m and is following.");
                EventBus.Raise(new CensusTakerPursuitEvent { Pursuing = true, DistanceMetres = distance });
            }
            else if (!shouldPursue && CurrentState == State.Pursuing)
            {
                CurrentState = State.Idle;
                EventBus.Raise(new CensusTakerPursuitEvent { Pursuing = false, DistanceMetres = distance });
            }

            if (CurrentState != State.Pursuing) return;

            MoveToward(playerPos, dt);

            // The stop timer only accumulates while it can see them AND they are not moving. Both
            // conditions come from the bible; either alone would make the mutant either trivial or
            // unavoidable.
            if (_player.HorizontalSpeed <= BalanceConstants.CENSUS_TAKER_STOP_SPEED_MS)
            {
                _stopTimer += dt;
                if (_stopTimer >= BalanceConstants.CENSUS_TAKER_STOP_THRESHOLD_SECONDS &&
                    distance <= BalanceConstants.CENSUS_TAKER_WRITING_RANGE_M)
                {
                    BeginWriting();
                }
            }
            else if (_stopTimer > 0f)
            {
                ResetStopTimer("player moved");
            }
        }

        private void TickWriting(float dt, float distance, bool sees)
        {
            // Any of the three conditions failing interrupts. Interrupting is free and total: the
            // entry is abandoned, not paused. A resumable registration would punish a player for
            // ever having stopped once, which is a different and much nastier mechanic.
            bool moved = _player.HorizontalSpeed > BalanceConstants.CENSUS_TAKER_STOP_SPEED_MS;
            bool tooFar = distance > BalanceConstants.CENSUS_TAKER_WRITING_RANGE_M * 1.5f;

            if (moved || tooFar || !sees)
            {
                string why = moved ? "the player moved" : tooFar ? "the player is out of reach"
                                                                 : "line of sight broke";
                Debug.Log($"[{ClassificationCode}] Entry abandoned at " +
                          $"{RegistrationProgress01:P0} — {why}.");
                EventBus.Raise(new RegistrationProgressEvent { Progress01 = 0f, Interrupted = true });
                _registrationTimer = 0f;
                CurrentState = State.Pursuing;
                ResetStopTimer(why);
                return;
            }

            _registrationTimer += dt;
            EventBus.Raise(new RegistrationProgressEvent
            {
                Progress01 = RegistrationProgress01,
                Interrupted = false
            });

            if (_registrationTimer < BalanceConstants.CENSUS_TAKER_REGISTRATION_SECONDS) return;

            RegistrationAffliction.Register(ClassificationCode);
            AudioManager.Play3D(AudioManager.CUE_PICKUP_PAPER, transform.position, 0.9f, 0.7f);

            _registrationTimer = 0f;
            CurrentState = State.Pursuing;
            ResetStopTimer("entry completed");
        }

        private void BeginWriting()
        {
            CurrentState = State.Writing;
            _registrationTimer = 0f;

            Debug.Log($"[{ClassificationCode}] The player has been stationary for " +
                      $"{BalanceConstants.CENSUS_TAKER_STOP_THRESHOLD_SECONDS:0}s. " +
                      $"It raises the clipboard. {BalanceConstants.CENSUS_TAKER_REGISTRATION_SECONDS:0}s " +
                      "to complete the entry.");

            EventBus.Raise(new RegistrationProgressEvent { Progress01 = 0f, Interrupted = false });
        }

        private void ResetStopTimer(string reason)
        {
            if (_stopTimer <= 0f) return;
            _stopTimer = 0f;
        }

        /// <summary>
        /// Steers one step along the current path, re-pathing on an interval. Rotation faces travel,
        /// which is also what makes the thing read as looking at you when it arrives.
        /// </summary>
        private void MoveToward(Vector3 target, float dt)
        {
            Vector3 step;

            if (_grid == null)
            {
                step = target - transform.position;
            }
            else
            {
                if (Time.time >= _nextRepathAt || _path == null || _pathCursor >= _path.Count)
                {
                    _nextRepathAt = Time.time + Mathf.Max(0.1f, repathInterval);

                    // FindPath returns a shared buffer the grid reuses, so it is copied: a second
                    // Census-Taker querying next frame would otherwise silently rewrite this one's
                    // route out from under it.
                    var found = _grid.FindPath(transform.position, target);
                    _path = new System.Collections.Generic.List<Vector3>(found);
                    _pathCursor = 0;
                }

                if (_path == null || _path.Count == 0) return;

                // Advance past waypoints already reached. A whole-cell tolerance keeps it from
                // stalling on a waypoint it can never land exactly on.
                while (_pathCursor < _path.Count &&
                       Horizontal(_path[_pathCursor] - transform.position).sqrMagnitude <
                       _grid.CellSize * _grid.CellSize)
                {
                    _pathCursor++;
                }

                if (_pathCursor >= _path.Count) return;
                step = _path[_pathCursor] - transform.position;
            }

            step = Horizontal(step);
            if (step.sqrMagnitude < 1e-4f) return;

            Vector3 direction = step.normalized;
            transform.position += direction * (moveSpeed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                                                  Quaternion.LookRotation(direction, Vector3.up),
                                                  dt * 4f);
        }

        private static Vector3 Horizontal(Vector3 v) { v.y = 0f; return v; }

        /// <summary>
        /// Raycast from chest height to the player's chest. Chest-to-chest rather than origin-to-origin
        /// because both origins sit at the feet, where a doorway threshold or a low kerb blocks a line
        /// that is plainly clear at eye level.
        /// </summary>
        private bool HasLineOfSight(Vector3 playerPos)
        {
            Vector3 from = transform.position + Vector3.up * 1.4f;
            Vector3 to = playerPos + Vector3.up * 1.2f;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.01f) return true;

            return !Physics.Raycast(from, delta / distance, distance, sightBlockers,
                                    QueryTriggerInteraction.Ignore);
        }
    }
}
