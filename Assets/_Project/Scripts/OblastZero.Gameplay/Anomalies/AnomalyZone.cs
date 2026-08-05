// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/AnomalyZone.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// Base for the three Phase-A environmental hazards (design bible §5 / BESTIARY.md "ANOMALIES").
    /// Owns the three things all of them share: a trigger volume, a place in the scene-wide registry, and
    /// the player enter/exit bookkeeping that drives detection.
    ///
    /// <para><b>Why a registry rather than physics queries.</b> Carbon Copy has to answer "was this pickup
    /// taken from inside me?" at the moment of the grab, and the Geiger has to answer "is anything anomalous
    /// within earshot?" every frame. Both are point-in-volume tests against a handful of static volumes that
    /// never move, so an <c>OverlapSphere</c> per pickup and per frame would be paying the broadphase for a
    /// list we can simply keep. The registry is populated on enable and drained on disable, so a scene
    /// teardown between runs cannot leave a dead zone behind — which matters because the scavenge scene is
    /// loaded and unloaded once per run and a leaked static reference would survive into the next one.</para>
    ///
    /// <para><b>Detection is separate from visibility.</b> Two of the three anomalies are invisible
    /// (<see cref="CarbonCopyAnomaly"/> entirely, <see cref="InterviewAnomaly"/> until you are inside a room
    /// whose proportions are wrong) and the third is visible but easy to misread under time pressure. So
    /// <see cref="IsGeigerDetectable"/> is a per-anomaly property rather than a blanket behaviour: the bible
    /// gives the Geiger to Carbon Copy only, and handing it the other two would delete the reason the other
    /// two are frightening.</para>
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class AnomalyZone : MonoBehaviour
    {
        private static readonly List<AnomalyZone> _active = new List<AnomalyZone>();

        /// <summary>Every anomaly currently live in a loaded scene. Empty outside the Blowout.</summary>
        public static IReadOnlyList<AnomalyZone> Active { get { return _active; } }

        /// <summary>
        /// Bible classification code (e.g. ANM-Δ-07/CC). Stable identifier, never localized.
        ///
        /// <para>A per-type constant rather than a serialized field. The code is intrinsic to what the
        /// anomaly <i>is</i>, not something a level designer chooses, so there is nothing to author and no
        /// way for a scene to disagree with the bestiary. It also keeps the Greek letters out of the scene
        /// YAML: the generator's output is byte-deterministic and currently pure ASCII, and there is no
        /// reason to make it the first file in that pipeline to carry multibyte characters for a string
        /// that only ever reaches a log line.</para>
        /// </summary>
        public abstract string ClassificationCode { get; }

        /// <summary>
        /// True when a Geiger counter clicks near this anomaly. Bible: Carbon Copy yes (a characteristic
        /// non-radioactive double-click), Interview no, Backlog no.
        /// </summary>
        public abstract bool IsGeigerDetectable { get; }

        /// <summary>True while the player is inside this anomaly's volume.</summary>
        public bool PlayerInside { get; private set; }

        protected Collider Volume { get; private set; }

        protected virtual void Awake()
        {
            Volume = GetComponent<Collider>();

            // A non-trigger anomaly is a solid wall the player bumps into instead of an effect they walk
            // through, and it fails silently — the zone simply never fires. Correcting it here rather than
            // warning keeps a hand-placed zone from being a dead prop.
            if (Volume != null && !Volume.isTrigger)
            {
                Debug.LogWarning($"[{ClassificationCode}] Collider on '{name}' was not a trigger. " +
                                 "Forcing isTrigger — an anomaly the player collides with cannot fire.");
                Volume.isTrigger = true;
            }
        }

        protected virtual void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        protected virtual void OnDisable()
        {
            _active.Remove(this);

            // A zone torn down while the player is standing in it must undo whatever it applied, or the
            // effect outlives the scene. Backlog is the sharp case: leaving the slow on would carry a 2%
            // walk speed into the next run.
            if (PlayerInside)
            {
                PlayerInside = false;
                OnPlayerExit(null);
            }
        }

        /// <summary>
        /// Whether a world point lies inside this anomaly's volume. Uses the collider's own closest-point
        /// solve rather than <c>bounds.Contains</c>, because bounds is the world-axis-aligned box around a
        /// possibly rotated volume and reports points outside a tilted zone as inside it.
        /// </summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (Volume == null) return false;
            return (Volume.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude <= kContainmentEpsilonSqr;
        }

        /// <summary>Distance from a world point to the nearest surface of this volume, 0 when inside.</summary>
        public float DistanceTo(Vector3 worldPoint)
        {
            if (Volume == null) return Mathf.Infinity;
            return Vector3.Distance(Volume.ClosestPoint(worldPoint), worldPoint);
        }

        /// <summary>
        /// ClosestPoint returns the query point itself for an interior point, so containment is a
        /// zero-distance test. The epsilon absorbs float error on the surface, where a point can land a
        /// fraction of a millimetre outside its own projection.
        /// </summary>
        private const float kContainmentEpsilonSqr = 1e-6f;

        private void OnTriggerEnter(Collider other)
        {
            if (PlayerInside || !IsPlayer(other)) return;
            PlayerInside = true;
            OnPlayerEnter(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!PlayerInside || !IsPlayer(other)) return;
            PlayerInside = false;
            OnPlayerExit(other);
        }

        /// <summary>
        /// The player is identified by its controller component rather than by tag. The scavenge scene is
        /// generated, and a tag is a string the generator would have to keep in sync with a project setting
        /// that lives outside the repo's own validation; the component is checked by the compiler.
        /// </summary>
        protected static bool IsPlayer(Collider other)
        {
            if (other == null) return false;
            return other.GetComponentInParent<ScavengePlayerController>() != null;
        }

        /// <summary>Called once when the player enters. Override to apply the anomaly's effect.</summary>
        protected virtual void OnPlayerEnter(Collider player) { }

        /// <summary>
        /// Called once when the player leaves, and also on teardown while inside — in which case
        /// <paramref name="player"/> is null. Any override that dereferences it must null-check.
        /// </summary>
        protected virtual void OnPlayerExit(Collider player) { }

        /// <summary>
        /// The anomaly whose volume contains <paramref name="worldPoint"/>, or null. First match wins:
        /// zones are placed apart by the generator, and two overlapping volumes would be a level bug that
        /// arbitrary resolution here would hide.
        /// </summary>
        public static T ZoneAt<T>(Vector3 worldPoint) where T : AnomalyZone
        {
            for (int i = 0; i < _active.Count; i++)
            {
                var zone = _active[i] as T;
                if (zone != null && zone.Contains(worldPoint)) return zone;
            }
            return null;
        }

        /// <summary>
        /// Distance from <paramref name="worldPoint"/> to the nearest Geiger-detectable anomaly, or
        /// <see cref="Mathf.Infinity"/> when none is in the scene. Drives <see cref="AnomalyAudioCue"/>.
        /// </summary>
        public static float NearestDetectableDistance(Vector3 worldPoint, out AnomalyZone nearest)
        {
            nearest = null;
            float best = Mathf.Infinity;

            for (int i = 0; i < _active.Count; i++)
            {
                var zone = _active[i];
                if (zone == null || !zone.IsGeigerDetectable) continue;

                float d = zone.DistanceTo(worldPoint);
                if (d >= best) continue;
                best = d;
                nearest = zone;
            }

            return best;
        }
    }
}
