// Assets/_Project/Scripts/Core/States/ScavengePhase3DState.cs
using UnityEngine;
using OblastZero.Gameplay;
using OblastZero.UI;

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

        /// <summary>
        /// Scene loaded when the run names a site that has none — the Collapsed Grain Depot, which is the
        /// one level that has always shipped. A fallback rather than a hard failure: a run that reaches
        /// Phase A with an unrecognised site id should lose its choice of map, not its whole expedition.
        /// </summary>
        private const string FallbackSceneName = "Scavenge";

        private EmissionTimer _timer;
        private ISceneLoader _sceneLoader;
        private bool _ending;

        // ── Pause ────────────────────────────────────────────────────────────
        // The overlay and the options panel are owned here, not by the scene: the scavenge scene is
        // unloaded and reloaded per run, and a pause menu that died with its scene would take the player's
        // half-changed settings with it.
        private PauseMenuUI _pause;
        private OptionsUI _options;
        private ScavengePlayerController _player;

        /// <summary>
        /// Resolved on entry and remembered for the matching unload. Read from the catalogue rather than
        /// re-resolved in HandleExit, because EndCurrentRun can clear the run between the two — unloading by
        /// a freshly-resolved name would then miss the scene that is actually open and leave the level
        /// stacked under the bunker.
        /// </summary>
        private string _loadedSceneName;

        protected override void HandleEnter()
        {
            _ending = false;
            _timer = new EmissionTimer(BalanceConstants.SCAVENGE_TIMER_SECONDS);

            // Clears any clock hold a previous Blowout leaked — an abandoned run that quit mid-interview
            // would otherwise start this one with a frozen timer and no way to notice until it never ran out.
            Gameplay.Anomalies.ScavengeClockReadout.ResetForNewPhase();

            EventBus.Subscribe<ReachBunkerEvent>(OnReachBunker);

            // Load the 3D level additively on top of _Bootstrap. The clock is owned here rather than in
            // the scene, so it keeps running even if the level is slow to stream in — the player simply
            // loses that time, which is the honest behaviour for a real-time phase.
            _loadedSceneName = ResolveSceneName();

            _sceneLoader = ServiceLocator.TryGet<ISceneLoader>(out var loader) ? loader : null;
            if (_sceneLoader != null)
                _sceneLoader.LoadSceneAdditive(_loadedSceneName);
            else
                Debug.LogWarning("[ScavengePhase3D] No ISceneLoader registered — scavenge level not loaded " +
                                 "(the timer still runs, so the phase resolves headless).");

            BuildPauseOverlay();

            Debug.Log($"[ScavengePhase3D] The Blowout begins. {BalanceConstants.SCAVENGE_TIMER_SECONDS:0}s " +
                      "to grab what you can and reach the bunker.");
        }

        protected override void HandleTick(float deltaTime)
        {
            if (_timer == null || _ending) return;

            // The player controller is spawned with the scene, which streams in asynchronously, so it is
            // bound on the first tick that finds it rather than in HandleEnter.
            if (_player == null) BindPlayerController();

            // The clock keeps running while paused. This is the deliberate design decision the overlay's
            // own subtitle states outright: a Blowout that could be paused to plan is a different phase.
            //
            // An anomaly hold is the one exception, and it is a different thing entirely: the Interview
            // (ANM-Ψ-12/IV) stops time because the bible says the room does, and the player is inside a
            // scripted sequence with no access to the level while it lasts. They cannot plan with it. The
            // hold is counted and self-clearing, so an interrupted session cannot leave the clock stopped.
            if (Gameplay.Anomalies.ScavengeClockReadout.IsHeld) return;

            _timer.Tick(deltaTime);
            if (_timer.IsExpired) EndPhase("emission hit — time up");
        }

        protected override void HandleExit()
        {
            EventBus.Unsubscribe<ReachBunkerEvent>(OnReachBunker);

            TearDownPauseOverlay();

            // Unload before the cutscene commits the haul: RunData already holds everything picked up,
            // so tearing down the level cannot cost the player anything. SceneLoader guards the
            // not-loaded case, which is what happens when the phase ran headless.
            if (_sceneLoader != null && !string.IsNullOrEmpty(_loadedSceneName))
                _sceneLoader.UnloadScene(_loadedSceneName);
            _sceneLoader = null;
            _loadedSceneName = null;
            _timer = null;
        }

        /// <summary>
        /// The scene for the site this run registered for. Every failure mode falls back to the depot with a
        /// named reason rather than loading nothing: a Phase A with no level still resolves — the clock runs
        /// and the phase ends — but it hands the player an empty 60 seconds with no explanation in the log.
        /// </summary>
        private string ResolveSceneName()
        {
            string siteId = GameManager.Instance?.CurrentRun?.currentScavengeSiteId;

            if (string.IsNullOrEmpty(siteId))
            {
                Debug.LogWarning($"[ScavengePhase3D] Run names no scavenge site — loading '{FallbackSceneName}'.");
                return FallbackSceneName;
            }

            var site = ScavengeSiteCatalog.Get(siteId);
            if (site == null || string.IsNullOrEmpty(site.SceneName))
            {
                Debug.LogError($"[ScavengePhase3D] Site '{siteId}' has no scene on record — falling back to " +
                               $"'{FallbackSceneName}'. Check ScavengeSiteCatalog and Build Settings.");
                return FallbackSceneName;
            }

            Debug.Log($"[ScavengePhase3D] Site '{siteId}' ({site.DisplayName}, {site.RegionDisplayName}) " +
                      $"-> scene '{site.SceneName}'.");
            return site.SceneName;
        }

        // ── Pause overlay ────────────────────────────────────────────────────

        private void BuildPauseOverlay()
        {
            var host = new GameObject("ScavengePauseMenu");
            host.transform.SetParent(transform, false);
            _pause = host.AddComponent<PauseMenuUI>();

            _pause.ResumeRequested += OnResumePressed;
            _pause.OptionsRequested += OnOptionsPressed;
            _pause.AbandonRequested += OnAbandonPressed;
        }

        private void TearDownPauseOverlay()
        {
            CloseOptions();

            if (_player != null)
            {
                _player.PauseRequested -= OnPauseToggleRequested;
                _player.InputSuspended = false;
                _player = null;
            }

            if (_pause != null)
            {
                _pause.ResumeRequested -= OnResumePressed;
                _pause.OptionsRequested -= OnOptionsPressed;
                _pause.AbandonRequested -= OnAbandonPressed;
                Destroy(_pause.gameObject);
                _pause = null;
            }
        }

        /// <summary>
        /// Finds the controller once the scavenge scene has streamed in. FindObjectOfType is the wrong tool
        /// for wiring services (CLAUDE.md §3), but the player is a scene object this persistent state has no
        /// other handle on, and the lookup runs once per run rather than per frame.
        /// </summary>
        private void BindPlayerController()
        {
            var controller = Object.FindFirstObjectByType<ScavengePlayerController>();
            if (controller == null) return;

            _player = controller;
            _player.PauseRequested += OnPauseToggleRequested;
            Debug.Log("[ScavengePhase3D] Pause bound to the player controller.");
        }

        private void OnPauseToggleRequested()
        {
            if (_pause == null || _ending) return;

            // While the options panel is up, the pause key closes that first. Otherwise a player who opened
            // options from the pause menu and pressed Escape would drop straight back into a running
            // Blowout with the cursor still free.
            if (_options != null)
            {
                CloseOptions();
                return;
            }

            if (_pause.IsOpen) Resume();
            else Suspend();
        }

        private void Suspend()
        {
            _pause.Open();
            if (_player != null) _player.InputSuspended = true;
            Debug.Log("[ScavengePhase3D] Filing suspended. The emission clock is still running.");
        }

        private void Resume()
        {
            _pause.Close();
            if (_player != null)
            {
                _player.InputSuspended = false;
                // Re-lock the cursor: the overlay freed it so its buttons were clickable.
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnResumePressed() => Resume();

        private void OnOptionsPressed()
        {
            if (_options != null) return;

            var host = new GameObject("ScavengeOptionsUI");
            host.transform.SetParent(transform, false);
            _options = host.AddComponent<OptionsUI>();
            _options.CloseRequested += CloseOptions;
        }

        private void CloseOptions()
        {
            if (_options == null) return;
            _options.CloseRequested -= CloseOptions;
            Destroy(_options.gameObject);
            _options = null;
        }

        /// <summary>
        /// Abandoning mid-Blowout ends the phase rather than the run. The haul so far is already in
        /// RunData, so the cutscene commits it and the player lands in the bunker with whatever they had —
        /// the same outcome as the emission catching them, which is the honest price of walking away.
        /// </summary>
        private void OnAbandonPressed()
        {
            Resume();
            EndPhase("player abandoned the filing");
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
