using System.Collections.Generic;
using UnityEngine;
using OblastZero.Data;
using OblastZero.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Run setup. Spawns <see cref="RunSetupUI"/>, feeds it the site catalogue and the crew roster, and on
    /// confirmation calls <see cref="GameManager.BeginNewRun"/> before handing off to Phase A.
    ///
    /// The state owns everything the UI is not allowed to: reading the GameDatabase, generating the seed,
    /// and starting the run. The screen only reports which id the player picked.
    /// </summary>
    public class RunSetupState : BaseGameState
    {
        public override string StateId => "RunSetup";
        public override GameState StateEnum => GameState.RunSetup;

        private RunSetupUI _ui;

        protected override void HandleEnter()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Database == null)
            {
                Debug.LogError("[RunSetupState] GameManager or Database unavailable — returning to MainMenu.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            Debug.Log("[RunSetupState] Entered — showing run setup screen.");

            var host = new GameObject("RunSetupUI");
            host.transform.SetParent(transform, false);
            _ui = host.AddComponent<RunSetupUI>();

            _ui.RunConfirmed += OnRunConfirmed;
            _ui.Cancelled += OnCancelPressed;

            _ui.Populate(ScavengeSiteCatalog.All, AvailableCrew(gm.Database), NewSeed());
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.RunConfirmed -= OnRunConfirmed;
                _ui.Cancelled -= OnCancelPressed;
                Destroy(_ui.gameObject);
                _ui = null;
            }

            Debug.Log("[RunSetupState] Exited.");
        }

        /// <summary>
        /// The crew the player may register as lead operator. Every authored crew member is offered today;
        /// once MetaProgressData.unlockedCrewArchetypes is populated by meta-progression this filters on it.
        /// </summary>
        private List<CrewMemberData> AvailableCrew(GameDatabase database)
        {
            var roster = new List<CrewMemberData>();
            var all = database.AllCrew;
            if (all == null) return roster;

            var unlocked = Context?.MetaProgress?.unlockedCrewArchetypes;
            bool filter = unlocked != null && unlocked.Count > 0;

            foreach (var member in all)
            {
                if (member == null || string.IsNullOrEmpty(member.id)) continue;
                if (filter && !unlocked.Contains(member.id)) continue;
                roster.Add(member);
            }

            if (roster.Count == 0)
                Debug.LogError("[RunSetupState] No crew members in the database — the roster will be empty.");

            return roster;
        }

        /// <summary>A fresh run seed. Runs are reproducible from this value alone.</summary>
        private static int NewSeed() => Random.Range(1, int.MaxValue);

        private void OnRunConfirmed(string siteId, string leadCrewId, int seed)
        {
            var site = ScavengeSiteCatalog.Get(siteId);
            if (site == null || !site.IsAvailable)
            {
                Debug.LogError($"[RunSetupState] Site '{siteId}' is not available for entry. Staying on setup.");
                return;
            }

            Debug.Log($"[RunSetupState] Run committed: site={siteId}, lead={leadCrewId}, seed={seed}");

            GameManager.Instance.BeginNewRun(siteId, seed, leadCrewId);

            // Phase A. ScavengePhase3DState owns the emission clock and loads the level additively.
            RequestTransition(GameState.ScavengePhase3D);
        }

        private void OnCancelPressed()
        {
            Debug.Log("[RunSetupState] Setup cancelled — returning to MainMenu.");
            RequestTransition(GameState.MainMenu);
        }
    }
}
