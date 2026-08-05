// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/AnomalyAudioCue.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// The Geiger counter's voice. Lives on the player and clicks faster the closer they are to a
    /// Geiger-detectable anomaly, going to the bible's characteristic double-click once inside the volume.
    ///
    /// <para><b>It is gated on actually carrying the counter.</b> The bible lists Geiger detection as the
    /// Carbon Copy's counter-tactic, which only means something if the counter is a thing you can fail to
    /// have. So the component checks the scavenged pack for
    /// <c>DetectionItemIds.GeigerCounter</c> — the shipped id is <c>item_kafedra_geiger_counter</c>, Kafedra
    /// issue, not the bare name — and stays silent without it. A player who grabs the counter mid-run gets
    /// the clicks from that moment on, which turns a 0.4 kg item into a real carry-cap decision: detection
    /// costs pack weight the same as food does.</para>
    ///
    /// <para><b>Only Carbon Copy answers it, by design.</b> <see cref="AnomalyZone.IsGeigerDetectable"/> is
    /// true for exactly one of the three anomalies. The Interview is a room you have to read and the
    /// Backlog is a haze you have to see; handing both of them to an item would collapse three distinct
    /// counter-tactics into one purchase.</para>
    ///
    /// <para><b>Polling, not triggers.</b> The click rate is a continuous function of distance, so it needs
    /// a distance every frame regardless of volume crossings. The scan is over
    /// <see cref="AnomalyZone.Active"/> — a handful of static volumes — at a fixed interval well under the
    /// fastest click period, so it costs a few distance solves a second, not a physics query.</para>
    /// </summary>
    public class AnomalyAudioCue : MonoBehaviour
    {
        [Tooltip("Beyond this distance the counter is silent. " +
                 "Mirrors BalanceConstants.GEIGER_DETECTION_RANGE_M.")]
        [SerializeField] private float detectionRange = BalanceConstants.GEIGER_DETECTION_RANGE_M;

        [Tooltip("Seconds between scans for the nearest anomaly. Cheaper than the click rate on purpose.")]
        [SerializeField] private float scanInterval = 0.25f;

        private float _nextScanTime;
        private float _nextClickTime;
        private float _nearestDistance = Mathf.Infinity;
        private bool _insideDetectable;
        private bool _hasCounter;
        private float _nextInventoryCheckTime;

        /// <summary>True while the player is carrying a counter and something is in range of it.</summary>
        public bool IsClicking
        {
            get { return _hasCounter && _nearestDistance <= detectionRange; }
        }

        private void Update()
        {
            float now = Time.time;

            // The pack is re-checked periodically rather than every frame: the counter can be picked up
            // mid-run, but not several times a second.
            if (now >= _nextInventoryCheckTime)
            {
                _nextInventoryCheckTime = now + BalanceConstants.GEIGER_INVENTORY_POLL_SECONDS;
                _hasCounter = PlayerHasCounter();
            }

            if (!_hasCounter) return;

            if (now >= _nextScanTime)
            {
                _nextScanTime = now + Mathf.Max(0.05f, scanInterval);
                AnomalyZone nearest;
                _nearestDistance = AnomalyZone.NearestDetectableDistance(transform.position, out nearest);
                _insideDetectable = nearest != null && nearest.PlayerInside;
            }

            if (_nearestDistance > detectionRange) return;
            if (now < _nextClickTime) return;

            EmitClick();
            _nextClickTime = now + ClickPeriod();
        }

        /// <summary>
        /// Inside the volume the counter produces the bible's double-click; outside it produces single
        /// clicks. Two cues 60 ms apart rather than one distinct sound, because that is what the pattern
        /// physically is and it means the player recognises the same click getting more agitated rather
        /// than learning a second sound.
        /// </summary>
        private void EmitClick()
        {
            AudioManager.Play(AudioManager.CUE_UI_HOVER, BalanceConstants.GEIGER_CLICK_VOLUME,
                              BalanceConstants.GEIGER_CLICK_PITCH);

            if (_insideDetectable) Invoke(nameof(EmitSecondClick), BalanceConstants.GEIGER_DOUBLE_CLICK_GAP);
        }

        private void EmitSecondClick()
        {
            AudioManager.Play(AudioManager.CUE_UI_HOVER, BalanceConstants.GEIGER_CLICK_VOLUME,
                              BalanceConstants.GEIGER_CLICK_PITCH);
        }

        /// <summary>
        /// Seconds between clicks, from lazy at the edge of range to urgent at the boundary. Linear in
        /// distance rather than inverse-square: this is a gameplay tell, and an inverse-square falloff
        /// spends most of its range inaudibly slow and then goes from nothing to a scream in the last metre.
        /// </summary>
        private float ClickPeriod()
        {
            float t = Mathf.Clamp01(_nearestDistance / Mathf.Max(0.01f, detectionRange));
            return Mathf.Lerp(BalanceConstants.GEIGER_CLICK_PERIOD_NEAR,
                              BalanceConstants.GEIGER_CLICK_PERIOD_FAR, t);
        }

        private static bool PlayerHasCounter()
        {
            var inventory = GameManager.Instance != null ? GameManager.Instance.Inventory : null;
            if (inventory == null) return false;

            var carried = inventory.Get(InventoryChannel.Scavenged);
            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i] != null && carried[i].itemDataId == DetectionItemIds.GeigerCounter)
                    return true;
            }
            return false;
        }
    }
}
