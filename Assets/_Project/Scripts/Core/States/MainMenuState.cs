using UnityEngine;
using OblastZero.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Title/main menu state. Spawns <see cref="MainMenuUI"/>, listens to its intents, and turns them into
    /// transitions. The screen builds its own canvas, so there is nothing to wire in a scene.
    ///
    /// "Continue" is only offered when an expedition save exists on disk; picking it restores that run and
    /// drops the player back into the bunker rather than starting a fresh registration.
    /// </summary>
    public class MainMenuState : BaseGameState
    {
        public override string StateId => "MainMenu";
        public override GameState StateEnum => GameState.MainMenu;

        private MainMenuUI _ui;
        private MetaUnlockUI _supplyOffice;
        private OptionsUI _options;

        protected override void HandleEnter()
        {
            Debug.Log("[MainMenuState] Entered — showing title screen.");

            var host = new GameObject("MainMenuUI");
            host.transform.SetParent(transform, false);
            _ui = host.AddComponent<MainMenuUI>();

            _ui.NewRunRequested += OnNewRunPressed;
            _ui.ContinueRequested += OnContinuePressed;
            _ui.SupplyOfficeRequested += OnSupplyOfficePressed;
            _ui.OptionsRequested += OnOptionsPressed;
            _ui.QuitRequested += OnQuitPressed;

            _ui.SetContinueAvailable(HasResumableRun());
            RefreshMenuFromProfile();
        }

        /// <summary>Pushes the profile's counters onto the title screen. Called on entry and after any purchase.</summary>
        private void RefreshMenuFromProfile()
        {
            var meta = Context?.MetaProgress;
            if (_ui == null || meta == null) return;
            _ui.SetRecord(meta.totalRunsAttempted, meta.totalRunsSurvived);
            _ui.SetTokenBalance(meta.salvageTokens);
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.NewRunRequested -= OnNewRunPressed;
                _ui.ContinueRequested -= OnContinuePressed;
                _ui.SupplyOfficeRequested -= OnSupplyOfficePressed;
                _ui.OptionsRequested -= OnOptionsPressed;
                _ui.QuitRequested -= OnQuitPressed;
                Destroy(_ui.gameObject);
                _ui = null;
            }

            CloseSupplyOffice();
            CloseOptions();

            Debug.Log("[MainMenuState] Exited.");
        }

        /// <summary>True when a saved expedition is on disk and can be resumed.</summary>
        private static bool HasResumableRun()
        {
            if (!ServiceLocator.TryGet<ISaveService>(out var save) || save == null)
            {
                Debug.LogWarning("[MainMenuState] No save service registered — Continue disabled.");
                return false;
            }
            return save.HasExpeditionSave();
        }

        private void OnNewRunPressed()
        {
            Debug.Log("[MainMenuState] New Run selected.");
            RequestTransition(GameState.RunSetup);
        }

        private void OnContinuePressed()
        {
            if (!ServiceLocator.TryGet<ISaveService>(out var save) || save == null)
            {
                Debug.LogError("[MainMenuState] Continue pressed with no save service. Staying on the menu.");
                return;
            }

            var run = save.LoadExpedition();
            if (run == null)
            {
                Debug.LogError("[MainMenuState] Continue pressed but the expedition save failed to load. " +
                               "Disabling Continue and staying on the menu.");
                _ui?.SetContinueAvailable(false);
                return;
            }

            // Restore the run and re-point every run-scoped manager at it before anything reads it.
            Context.CurrentRun = run;
            GameManager.Instance?.RebindManagersToCurrentRun();

            Debug.Log($"[MainMenuState] Resumed run '{run.runId}' at day {run.currentDay} — entering the bunker.");
            RequestTransition(GameState.SurvivalPhase2D);
        }

        // ── Supply office ─────────────────────────────────────────────────────
        // The office is a panel over the menu rather than a GameState of its own. It starts no run, ends no
        // run, and loads no scene — the three things a state exists to sequence — so a state would buy a
        // transition, a registration and an enum member in exchange for nothing. It is spawned, listened to,
        // and destroyed by the state that owns the screen behind it.

        private void OnSupplyOfficePressed()
        {
            if (_supplyOffice != null) return; // already open; ignore a double-click

            var meta = Context?.MetaProgress;
            if (meta == null)
            {
                Debug.LogError("[MainMenuState] Supply office requested with no MetaProgress on the context. " +
                               "Staying on the menu.");
                return;
            }

            Debug.Log($"[MainMenuState] Supply office opened. Balance {meta.salvageTokens} token(s), " +
                      $"{meta.purchasedUnlockIds?.Count ?? 0} unlock(s) on file.");

            var host = new GameObject("MetaUnlockUI");
            host.transform.SetParent(transform, false);
            _supplyOffice = host.AddComponent<MetaUnlockUI>();

            _supplyOffice.PurchaseRequested += OnPurchaseRequested;
            _supplyOffice.BackRequested += OnSupplyOfficeClosed;
            _supplyOffice.Present(meta);
        }

        /// <summary>
        /// Buys an unlock and persists immediately. The save is not deferred to some later flush: a player
        /// who spends fifty tokens and then closes the game has bought the thing, and a profile that records
        /// the deduction without the purchase — or neither — is the worst outcome available here.
        /// </summary>
        private void OnPurchaseRequested(string unlockId)
        {
            var meta = Context?.MetaProgress;
            if (meta == null || _supplyOffice == null) return;

            bool bought = meta.Purchase(unlockId);
            var unlock = MetaUnlockCatalog.Get(unlockId);
            string name = unlock != null ? LocalizedStrings.Get(unlock.DisplayNameKey) : unlockId;

            if (bought)
            {
                if (ServiceLocator.TryGet<ISaveService>(out var save) && save != null)
                {
                    save.SaveProfile(meta);
                }
                else
                {
                    Debug.LogWarning("[MainMenuState] No save service — the purchase is in memory only and " +
                                     "will be lost when the process exits.");
                }

                _supplyOffice.SetNotice(LocalizedStrings.Get(UIStringKeys.SupplyPurchased, name), false);
            }
            else
            {
                _supplyOffice.SetNotice(LocalizedStrings.Get(UIStringKeys.SupplyRefused, name), true);
            }

            // Re-present either way: a refusal still needs the board redrawn, because the refusal itself is
            // evidence the screen's affordability picture disagreed with the profile's.
            _supplyOffice.Present(meta);
            RefreshMenuFromProfile();
        }

        private void OnSupplyOfficeClosed()
        {
            Debug.Log("[MainMenuState] Supply office closed.");
            CloseSupplyOffice();
        }

        private void CloseSupplyOffice()
        {
            if (_supplyOffice == null) return;
            _supplyOffice.PurchaseRequested -= OnPurchaseRequested;
            _supplyOffice.BackRequested -= OnSupplyOfficeClosed;
            Destroy(_supplyOffice.gameObject);
            _supplyOffice = null;
        }

        // ── Operating parameters ──────────────────────────────────────────────
        // A panel over the menu, for the same reason the supply office is one: it starts no run, ends no
        // run and loads no scene. It is also opened from the pause overlay mid-run, where a GameState
        // transition would be actively wrong — leaving and re-entering ScavengePhase3D to change a volume
        // would reload the scene and end the expedition.

        private void OnOptionsPressed()
        {
            if (_options != null) return;       // already open; ignore a double-click

            var host = new GameObject("OptionsUI");
            host.transform.SetParent(transform, false);
            _options = host.AddComponent<OptionsUI>();
            _options.CloseRequested += OnOptionsClosed;

            Debug.Log("[MainMenuState] Operating parameters opened.");
        }

        private void OnOptionsClosed()
        {
            Debug.Log("[MainMenuState] Operating parameters closed.");
            CloseOptions();
        }

        private void CloseOptions()
        {
            if (_options == null) return;
            _options.CloseRequested -= OnOptionsClosed;
            Destroy(_options.gameObject);
            _options = null;
        }

        private void OnQuitPressed()
        {
            Debug.Log("[MainMenuState] Quit selected.");
            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #else
                Application.Quit();
            #endif
        }
    }
}
