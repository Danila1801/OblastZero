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

        /// <summary>Additive 3D scavenge level — "Collapsed Grain Depot" (registered in Build Settings).</summary>
        private const string ScavengeSceneName = "Scavenge";

        private EmissionTimer _timer;
        private ISceneLoader _sceneLoader;
        private bool _ending;

        protected override void HandleEnter()
        {
            _ending = false;
            _timer = new EmissionTimer(BalanceConstants.SCAVENGE_TIMER_SECONDS);

            EventBus.Subscribe<ReachBunkerEvent>(OnReachBunker);

            // Load the 3D level additively on top of _Bootstrap. The clock is owned here rather than in
            // the scene, so it keeps running even if the level is slow to stream in — the player simply
            // loses that time, which is the honest behaviour for a real-time phase.
            _sceneLoader = ServiceLocator.TryGet<ISceneLoader>(out var loader) ? loader : null;
            if (_sceneLoader != null)
                _sceneLoader.LoadSceneAdditive(ScavengeSceneName);
            else
                Debug.LogWarning("[ScavengePhase3D] No ISceneLoader registered — scavenge level not loaded " +
                                 "(the timer still runs, so the phase resolves headless).");

            Debug.Log($"[ScavengePhase3D] The Blowout begins. {BalanceConstants.SCAVENGE_TIMER_SECONDS:0}s " +
                      "to grab what you can and reach the bunker.");
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

            // Unload before the cutscene commits the haul: RunData already holds everything picked up,
            // so tearing down the level cannot cost the player anything. SceneLoader guards the
            // not-loaded case, which is what happens when the phase ran headless.
            if (_sceneLoader != null) _sceneLoader.UnloadScene(ScavengeSceneName);
            _sceneLoader = null;
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
