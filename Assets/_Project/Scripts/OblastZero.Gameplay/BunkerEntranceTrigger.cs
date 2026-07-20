// Assets/_Project/Scripts/Gameplay/BunkerEntranceTrigger.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// A trigger volume at the bunker door. When the player enters, raises <see cref="ReachBunkerEvent"/>,
    /// which the ScavengePhase3DState listens for to end the Blowout early (player reached safety before the
    /// emission). Fires once. Put this on a GameObject with an isTrigger Collider sized to the doorway.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class BunkerEntranceTrigger : MonoBehaviour
    {
        [Tooltip("Tag identifying the player object. The player root should carry this tag.")]
        [SerializeField] private string playerTag = "Player";

        private bool _fired;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_fired) return;

            bool isPlayer = other.CompareTag(playerTag) ||
                            other.GetComponentInParent<ScavengePlayerController>() != null;
            if (!isPlayer) return;

            _fired = true;
            Debug.Log("[BunkerEntrance] Player reached the bunker. Raising ReachBunkerEvent.");
            EventBus.Raise(new ReachBunkerEvent());
        }
    }
}
