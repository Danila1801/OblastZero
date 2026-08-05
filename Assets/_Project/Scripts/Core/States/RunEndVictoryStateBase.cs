using UnityEngine;
using OblastZero.UI;

namespace OblastZero.Core.States
{
    /// <summary>
    /// Base class for all victory end states. Handles the common pattern:
    /// - Unlock the ending in MetaProgress.unlockedEndings (and persist it)
    /// - Show the closed case file, framed with this ending's prose
    /// - Wait for the player to acknowledge and return to MainMenu
    ///
    /// Each concrete ending lives in its own file. Unity binds a MonoBehaviour to the script asset
    /// whose file name matches the class name, so several MonoBehaviours sharing one file cannot be
    /// referenced from a scene at all.
    ///
    /// Reads <see cref="GameManager.LastRunSummary"/>, NOT <c>Context.CurrentRun</c>. This mirrors
    /// <see cref="RunFailedState"/> for the same reason: <see cref="GameManager.EndCurrentRun"/> clears the
    /// live run as part of closing it and runs BEFORE the transition into any run-end state, so a victory
    /// state that reads CurrentRun always finds null and bounces straight back to the menu. The summary is
    /// the snapshot GameManager takes while the numbers are still true.
    ///
    /// Presentation goes through <see cref="RunSummaryUI"/> — the same screen the failure path uses, built on
    /// the shared <c>OblastUI</c> vocabulary. The previous hand-rolled victory canvas could not render: it
    /// hung <c>LayoutElement</c>s off a root with no layout group (inert, per the standing rule in CLAUDE.md)
    /// and sourced its font from <c>Resources.Load&lt;Font&gt;("Arial")</c>, which no longer resolves in
    /// Unity 6 — a null font on a legacy Text draws nothing at all.
    /// </summary>
    public abstract class RunEndVictoryStateBase : BaseGameState
    {
        /// <summary>Stable key written to MetaProgress.unlockedEndings. Never localize or rename it.</summary>
        protected abstract string EndingName { get; }

        /// <summary>Ending-specific caption, shown under the summary's headline.</summary>
        protected abstract string EndingTitle { get; }

        /// <summary>The ending's closing prose, shown where the failure path shows its closing line.</summary>
        protected abstract string EndingNarrative { get; }

        private RunSummaryUI _ui;

        protected override void HandleEnter()
        {
            var gm = GameManager.Instance;
            var summary = gm != null ? gm.LastRunSummary : null;
            var meta = Context?.MetaProgress;

            if (summary == null)
            {
                Debug.LogError($"[{StateId}] Entered with no run summary — GameManager.EndCurrentRun should " +
                               "have run before this transition. Returning to MainMenu.");
                RequestTransition(GameState.MainMenu);
                return;
            }

            UnlockEnding(meta);

            Debug.Log($"[{StateId}] Victory. Days survived: {summary.DaysSurvived}, ending: {EndingName}, " +
                      $"crew remaining: {summary.CrewRemaining}, items recovered: {summary.ItemsRecovered}.");

            // Frame the shared summary with this ending's voice. Headline already differs per RunEndReason
            // (RunSummary.HeadlineFor), so the ending only supplies the caption and the closing prose.
            summary.Subheadline = EndingTitle;
            summary.ClosingLine = EndingNarrative;

            var host = new GameObject("RunSummaryUI");
            host.transform.SetParent(transform, false);
            _ui = host.AddComponent<RunSummaryUI>();
            _ui.AcknowledgeRequested += OnAcknowledged;
            _ui.Present(summary);
        }

        protected override void HandleExit()
        {
            if (_ui != null)
            {
                _ui.AcknowledgeRequested -= OnAcknowledged;
                Destroy(_ui.gameObject);
                _ui = null;
            }

            Debug.Log($"[{StateId}] Exited.");
        }

        /// <summary>
        /// Records the ending and flushes the profile. The flush matters: EndCurrentRun already saved
        /// MetaProgress on its way here, so an unlock added afterwards lives only in memory and is lost
        /// unless something else happens to save later. An ending the player earned must survive the process
        /// exiting, however it exits.
        /// </summary>
        private void UnlockEnding(MetaProgressData meta)
        {
            if (meta == null)
            {
                Debug.LogWarning($"[{StateId}] No MetaProgress on the context — ending '{EndingName}' not recorded.");
                return;
            }

            if (meta.unlockedEndings.Contains(EndingName)) return;

            meta.unlockedEndings.Add(EndingName);
            Debug.Log($"[{StateId}] Ending '{EndingName}' unlocked.");

            if (ServiceLocator.TryGet<ISaveService>(out var save) && save != null)
                save.SaveProfile(meta);
            else
                Debug.LogWarning($"[{StateId}] No save service — ending '{EndingName}' is unlocked in memory only.");
        }

        private void OnAcknowledged()
        {
            RequestTransition(GameState.MainMenu);
        }
    }
}
