// Assets/_Project/Scripts/Gameplay/FactionReputationManager.cs
using System;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The ONLY class permitted to write the faction-reputation fields on <see cref="RunData"/>
    /// (<c>repScaleSociety</c> / <c>repCordon</c> / <c>repKafedra</c>). The Event Engine and any future
    /// faction interaction route reputation changes through here, so mutation stays in one owner and every
    /// change fires an event. Reputation is clamped to
    /// [<see cref="BalanceConstants.REPUTATION_MIN"/>, <see cref="BalanceConstants.REPUTATION_MAX"/>].
    ///
    /// Only the three canonical factions (bible §2 — Scale Society, Cordon, Kafedra) carry tracked
    /// reputation. <see cref="FactionId.Loners"/> / <see cref="FactionId.Bandits"/> / <see cref="FactionId.None"/>
    /// are untracked: <see cref="Get"/> returns 0 and <see cref="ApplyDelta"/> is a logged no-op, so content
    /// can still reference them without corrupting state.
    ///
    /// Plain C# class, EventBus-free by design — <see cref="ManagerEventBridge"/> translates
    /// <see cref="ReputationChanged"/> into the global bus.
    /// </summary>
    public class FactionReputationManager
    {
        private RunData _run;

        /// <summary>faction, oldRep, newRep — fired only when the clamped value actually changes.</summary>
        public event Action<FactionId, int, int> ReputationChanged;

        public void Bind(RunData run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            Debug.Log($"[FactionReputationManager] Bound to run '{run.runId}'.");
        }

        public bool IsTracked(FactionId faction)
            => faction == FactionId.ScaleSociety || faction == FactionId.Cordon || faction == FactionId.Kafedra;

        /// <summary>Current reputation for a faction. Untracked factions always read 0.</summary>
        public int Get(FactionId faction)
        {
            if (!Ready(nameof(Get))) return 0;
            switch (faction)
            {
                case FactionId.ScaleSociety: return _run.repScaleSociety;
                case FactionId.Cordon: return _run.repCordon;
                case FactionId.Kafedra: return _run.repKafedra;
                default: return 0;
            }
        }

        /// <summary>Adds (or subtracts) reputation, clamped to the reputation bounds. No-op for untracked factions.</summary>
        public void ApplyDelta(FactionId faction, int delta)
        {
            if (!Ready(nameof(ApplyDelta))) return;
            if (delta == 0) return;
            if (!IsTracked(faction))
            {
                Debug.Log($"[FactionReputationManager] Ignoring rep delta {Signed(delta)} for untracked faction '{faction}'.");
                return;
            }

            int old = Get(faction);
            int updated = Mathf.Clamp(old + delta, BalanceConstants.REPUTATION_MIN, BalanceConstants.REPUTATION_MAX);
            if (updated == old) return; // already at the cap in that direction

            Set(faction, updated);
            Debug.Log($"[FactionReputationManager] {faction} rep {Signed(delta)} -> {updated} (was {old}).");
            ReputationChanged?.Invoke(faction, old, updated);
        }

        private void Set(FactionId faction, int value)
        {
            switch (faction)
            {
                case FactionId.ScaleSociety: _run.repScaleSociety = value; break;
                case FactionId.Cordon: _run.repCordon = value; break;
                case FactionId.Kafedra: _run.repKafedra = value; break;
            }
        }

        private bool Ready(string op)
        {
            if (_run != null) return true;
            Debug.LogError($"[FactionReputationManager] {op} called before Bind(RunData). No-op.");
            return false;
        }

        private static string Signed(int v) => v >= 0 ? $"+{v}" : v.ToString();
    }
}
