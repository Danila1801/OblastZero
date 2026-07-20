// Assets/_Project/Scripts/Core/States/TransitionCutsceneState.cs
using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// The brief sealed-door cinematic between phases (bible §6.3). On enter it performs the actual 3D→2D
    /// data handoff — committing RunData.ScavengedInventory into BunkerInventory and RescuedCrew into
    /// ActiveCrew via the managers built in Step 2 — then, after a short beat, transitions into the bunker.
    /// This is the keystone that connects the front half (Blowout) to the back half (bunker survival).
    /// </summary>
    public class TransitionCutsceneState : BaseGameState
    {
        public override string StateId => "TransitionCutscene";
        public override GameState StateEnum => GameState.TransitionCutscene;

        [Tooltip("How long the sealing-door beat plays before entering the bunker.")]
        [SerializeField] private float cutsceneSeconds = 2f;

        private float _elapsed;
        private bool _advanced;

        protected override void HandleEnter()
        {
            _elapsed = 0f;
            _advanced = false;

            var gm = GameManager.Instance;
            if (gm != null && gm.Inventory != null && gm.Crew != null)
            {
                // The handoff: everything grabbed during the Blowout becomes the bunker's starting state.
                gm.Inventory.TransferScavengedToBunker();
                gm.Crew.CommitRescuedToBunker();

                var run = Context?.CurrentRun;
                Debug.Log($"[TransitionCutscene] Door sealed. Handoff complete — bunker items " +
                          $"{run?.BunkerInventory.Count ?? 0}, active crew {run?.ActiveCrew.Count ?? 0}.");
            }
            else
            {
                Debug.LogError("[TransitionCutscene] Managers unavailable; cannot perform the 3D→2D handoff.");
            }

            // Unload the 3D scavenge scene through ISceneLoader here once its API is wired.
        }

        protected override void HandleTick(float deltaTime)
        {
            if (_advanced) return;

            _elapsed += deltaTime;
            if (_elapsed >= cutsceneSeconds)
            {
                _advanced = true;
                RequestTransition(GameState.SurvivalPhase2D);
            }
        }

        protected override void HandleExit() { }
    }
}
