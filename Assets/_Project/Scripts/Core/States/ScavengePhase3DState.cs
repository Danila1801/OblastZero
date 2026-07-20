// Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs
using UnityEngine;
using OblastZero.Gameplay;

namespace OblastZero.Core
{
    /// <summary>
    /// The 60-second 3D Blowout (Phase A). Owns the <see cref="EmissionTimer"/> and ends the phase on either
    /// of two conditions: the timer expiring, or the player reaching the bunker (<see cref="ReachBunkerEvent"/>
    /// from the in-scene trigger). Either way it transitions to the TransitionCutscene, which commits the haul.
    ///
    /// Pickups themselves are handled in-scene by ScavengeController (player → managers); this state only owns
    /// the clock and the phase's end conditions, keeping the persistent state free of scene-object references.
    /// </summary>
    public class ScavengePhase3DState : BaseGameState
    {
        public override string StateId => "ScavengePhase3D";
        public override GameState StateEnum => GameState.ScavengePhase3D;

        [Tooltip("Length of the Blowout countdown in seconds. Promote to BalanceConstants once settled.")]
        [SerializeField] private float emissionSeconds = 60f;

        private EmissionTimer _timer;
        private bool _ending;

        protected override void HandleEnter()
        {
            _ending = false;
            _timer = new EmissionTimer(emissionSeconds);

            EventBus.Subscribe<ReachBunkerEvent>(OnReachBunker);

            Debug.Log($"[ScavengePhase3D] The Blowout begins. {emissionSeconds:0}s to grab what you can and reach the bunker.");

            // Additive 3D scavenge-scene load goes through ISceneLoader here once its API is wired.
        }

        protected override void HandleTick(float deltaTime)
        {
            if (_timer == null || _ending) return;

            _timer.Tick(deltaTime);
            if (_timer.IsExpired) EndPhase("emission hit — time up");
        }

        protected override void HandleExit()
        {
            EventBus.Unsubscribe<ReachBunkerEvent>(OnReachBunker);
            _timer = null;
        }

        private void OnReachBunker(ReachBunkerEvent _) => EndPhase("player reached the bunker");

        private void EndPhase(string reason)
        {
            if (_ending) return;
            _ending = true;
            Debug.Log($"[ScavengePhase3D] Phase over — {reason}. Sealing the door.");
            RequestTransition(GameState.TransitionCutscene);
        }
    }
}
