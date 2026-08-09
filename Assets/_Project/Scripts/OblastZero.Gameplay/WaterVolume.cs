// Assets/_Project/Scripts/OblastZero.Gameplay/WaterVolume.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Standing water the player wades through — the Census Office's flooded basement and the
    /// Reservoir's tunnels and basin margins. Scales movement speed while the player is inside.
    ///
    /// <para><b>Why this is not an anomaly.</b> Water is terrain, not a hazard with a bestiary entry.
    /// It has no classification code, it is visible from outside, it never surprises anyone, and it
    /// must be able to overlap an anomaly — the Reservoir's shortcut is a Backlog in a flooded
    /// tunnel. Deriving it from <c>AnomalyZone</c> would put it in the anomaly registry, where the
    /// Geiger and Carbon Copy would both have to learn to ignore it.</para>
    /// </summary>
    /// <remarks>
    /// <para><b>Occupancy is counted, not toggled.</b> The naive version sets the slow in
    /// <c>OnTriggerEnter</c> and restores 1 in <c>OnTriggerExit</c>. That breaks the moment two water
    /// volumes touch — and they do, because a large flooded room is tiled from several boxes so the
    /// walkability flood-fill can reason about it. Crossing an internal seam fires Exit on the old
    /// box after Enter on the new one, so the player walks out of the water they are standing in.
    /// Instead every occupied volume is registered and the SLOWEST wins, which also gives deep water
    /// priority over shallow where they overlap at a shelf edge.</para>
    /// <para><b>The registry is drained on disable.</b> The scavenge scene is loaded and unloaded once
    /// per run. A volume that vanished while occupied would otherwise leave the run's last wade
    /// applied to a player who is no longer in any water — and, because the scene is additive, into
    /// the next run. Same reasoning as <c>AnomalyZone</c>'s registry.</para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public class WaterVolume : MonoBehaviour
    {
        /// <summary>Every volume the player is currently standing in. Usually empty, rarely above two.</summary>
        private static readonly List<WaterVolume> _occupied = new List<WaterVolume>();

        /// <summary>The controller whose terrain factor this system owns. Null outside the Blowout.</summary>
        private static ScavengePlayerController _player;

        [Tooltip("Walk/sprint scale while the player is inside. Use BalanceConstants.WATER_*_SPEED_FACTOR; " +
                 "the scene generator serializes those values so this never drifts from the balance file.")]
        [SerializeField] private float speedFactor = 0.6f;

        [Tooltip("Log a line the first time the player enters this volume. Useful when tuning a route; " +
                 "noisy if left on across every tile of a large flooded room.")]
        [SerializeField] private bool logEntry;

        /// <summary>Walk/sprint scale this volume applies. Clamped to a sane wade, never a speed-up.</summary>
        public float SpeedFactor
        {
            get { return Mathf.Clamp(speedFactor, 0.05f, 1f); }
        }

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (!col.isTrigger)
            {
                // A solid water box is a wall the walkability gate never modelled: the flood-fill
                // treats water as open floor, so the level would verify clean and be unplayable.
                col.isTrigger = true;
                Debug.LogWarning($"[WaterVolume] {name} had a solid collider. Forced to trigger — " +
                                 "solid water seals the room the generator proved was walkable.");
            }
        }

        private void OnDisable()
        {
            if (_occupied.Remove(this)) Reapply();
        }

        private void OnTriggerEnter(Collider other)
        {
            var controller = other != null ? other.GetComponentInParent<ScavengePlayerController>() : null;
            if (controller == null) return;

            _player = controller;
            if (!_occupied.Contains(this)) _occupied.Add(this);
            Reapply();

            if (logEntry)
                Debug.Log($"[WaterVolume] Player entered standing water at {transform.position}. " +
                          $"Speed → {SpeedFactor:P0} of normal.");
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || other.GetComponentInParent<ScavengePlayerController>() == null) return;
            if (_occupied.Remove(this)) Reapply();
        }

        /// <summary>
        /// Pushes the slowest occupied volume's factor onto the player, or 1 when none are occupied.
        /// The single write site for the terrain half of the speed product.
        /// </summary>
        private static void Reapply()
        {
            if (_player == null) return;

            float slowest = 1f;
            for (int i = 0; i < _occupied.Count; i++)
            {
                var vol = _occupied[i];
                if (vol == null) continue;
                if (vol.SpeedFactor < slowest) slowest = vol.SpeedFactor;
            }

            // Assigns the TERRAIN factor only. The anomaly factor is owned by BacklogAnomaly and the
            // two compose, so wading through a Backlog is slower than either — see
            // ScavengePlayerController.SpeedMultiplier.
            _player.TerrainSpeedFactor = slowest;

            if (_occupied.Count == 0) _player = null;
        }
    }
}
