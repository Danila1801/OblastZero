// Assets/_Project/Scripts/Gameplay/CrewManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>Identifies which crew stat changed, so listeners (and the EventBus bridge) can react precisely.</summary>
    public enum CrewStat
    {
        Health,
        Sanity,
        Fatigue,
        Radiation
    }

    /// <summary>
    /// The ONLY class permitted to write <see cref="RunData.RescuedCrew"/> and
    /// <see cref="RunData.ActiveCrew"/>. Owns crew creation, the 3D→2D handoff, stat changes
    /// (health/sanity/fatigue/radiation), and death. Plain C# class for testability: construct with a
    /// <see cref="GameDatabase"/>, then <see cref="Bind"/> the active run. Register in your ServiceLocator.
    ///
    /// Stays free of EventBus on purpose — ManagerEventBridge translates these C# events into global events.
    /// </summary>
    public class CrewManager
    {
        private readonly GameDatabase _db;
        private RunData _run;

        public event Action<CrewInstance> CrewAdded;
        public event Action<CrewInstance> CrewCommittedToBunker;
        public event Action<CrewInstance, CrewStat, int, int> CrewStatsChanged; // member, stat, oldValue, newValue
        public event Action<CrewInstance> CrewDied;

        public CrewManager(GameDatabase db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public void Bind(RunData run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            Debug.Log($"[CrewManager] Bound to run '{run.runId}'.");
        }

        // ---- Creation & handoff ----

        /// <summary>
        /// Creates a fresh crew instance from a data id and places it in the rescued list at full
        /// health/sanity, copying starting-trait ids onto the instance. Called when a squadmate is
        /// grabbed during the 3D Blowout.
        /// </summary>
        public CrewInstance AddRescued(string crewDataId)
        {
            if (!Ready(nameof(AddRescued))) return null;

            var data = _db.GetCrew(crewDataId);
            if (data == null) return null; // GameDatabase already logged the miss

            var traitIds = new List<string>();
            if (data.startingTraits != null)
            {
                foreach (var trait in data.startingTraits)
                    if (trait != null && !string.IsNullOrEmpty(trait.id)) traitIds.Add(trait.id);
            }

            var inst = new CrewInstance
            {
                crewDataId = crewDataId,
                instanceId = Guid.NewGuid().ToString("N"),
                currentHealth = data.baseStats.maxHealth,
                currentSanity = data.baseStats.maxSanity,
                currentFatigue = 0,
                currentRadiation = 0,
                traitIds = traitIds,
                isAlive = true,
                locationTag = "rescued_incoming"
            };

            _run.RescuedCrew.Add(inst);
            Debug.Log($"[CrewManager] Rescued '{crewDataId}' (instance {Short(inst.instanceId)}).");
            CrewAdded?.Invoke(inst);
            return inst;
        }

        /// <summary>The 3D→2D handoff for crew: move every rescued member into the active bunker roster.</summary>
        public void CommitRescuedToBunker()
        {
            if (!Ready(nameof(CommitRescuedToBunker))) return;
            if (_run.RescuedCrew.Count == 0)
            {
                Debug.Log("[CrewManager] No rescued crew to commit.");
                return;
            }

            var snapshot = new List<CrewInstance>(_run.RescuedCrew);
            _run.RescuedCrew.Clear();
            foreach (var inst in snapshot)
            {
                inst.locationTag = "bunker";
                _run.ActiveCrew.Add(inst);
                CrewCommittedToBunker?.Invoke(inst);
            }
            Debug.Log($"[CrewManager] Committed {snapshot.Count} crew member(s) into the bunker.");
        }

        // ---- Stat mutations ----

        /// <summary>Adds (or subtracts) health, clamped to the member's max. Death triggers at &lt;= 0.</summary>
        public void ApplyHealthDelta(string instanceId, int delta)
        {
            var c = RequireAlive(instanceId, nameof(ApplyHealthDelta));
            if (c == null) return;

            int max = MaxHealth(c);
            int old = c.currentHealth;
            c.currentHealth = Mathf.Clamp(c.currentHealth + delta, 0, max);
            Debug.Log($"[CrewManager] {Name(c)} health {Signed(delta)} -> {c.currentHealth}/{max}.");

            CrewStatsChanged?.Invoke(c, CrewStat.Health, old, c.currentHealth);
            if (c.currentHealth <= 0 && c.isAlive) Kill(c);
        }

        /// <summary>Adds (or subtracts) sanity, clamped to max. Positive recovery is scaled by the member's recovery multiplier.</summary>
        public void ApplySanityDelta(string instanceId, int delta)
        {
            var c = RequireAlive(instanceId, nameof(ApplySanityDelta));
            if (c == null) return;

            var data = _db.GetCrew(c.crewDataId);
            int max = data != null ? data.baseStats.maxSanity : 100;

            int effective = delta;
            if (delta > 0 && data != null && data.baseStats.sanityRecoveryMultiplier > 0f)
                effective = Mathf.RoundToInt(delta * data.baseStats.sanityRecoveryMultiplier);

            int old = c.currentSanity;
            c.currentSanity = Mathf.Clamp(c.currentSanity + effective, 0, max);
            Debug.Log($"[CrewManager] {Name(c)} sanity {Signed(effective)} -> {c.currentSanity}/{max}.");
            CrewStatsChanged?.Invoke(c, CrewStat.Sanity, old, c.currentSanity);
        }

        /// <summary>
        /// Adds radiation (0–100), reduced by the member's resistance multiplier
        /// (higher multiplier = more resistant; effective = amount / multiplier).
        /// </summary>
        public void ApplyRadiation(string instanceId, int amount)
        {
            var c = RequireAlive(instanceId, nameof(ApplyRadiation));
            if (c == null) return;

            var data = _db.GetCrew(c.crewDataId);
            float resist = data != null ? data.baseStats.radiationResistanceMultiplier : 1f;
            if (resist <= 0f) resist = 1f;

            int effective = Mathf.RoundToInt(amount / Mathf.Max(0.01f, resist));
            int old = c.currentRadiation;
            c.currentRadiation = Mathf.Clamp(c.currentRadiation + effective, 0, 100);
            Debug.Log($"[CrewManager] {Name(c)} radiation +{effective} -> {c.currentRadiation}/100 (resist x{resist:0.##}).");
            CrewStatsChanged?.Invoke(c, CrewStat.Radiation, old, c.currentRadiation);
        }

        /// <summary>Adds (or subtracts) fatigue, clamped 0–100.</summary>
        public void ApplyFatigueDelta(string instanceId, int delta)
        {
            var c = RequireAlive(instanceId, nameof(ApplyFatigueDelta));
            if (c == null) return;

            int old = c.currentFatigue;
            c.currentFatigue = Mathf.Clamp(c.currentFatigue + delta, 0, 100);
            Debug.Log($"[CrewManager] {Name(c)} fatigue {Signed(delta)} -> {c.currentFatigue}/100.");
            CrewStatsChanged?.Invoke(c, CrewStat.Fatigue, old, c.currentFatigue);
        }

        /// <summary>Marks a member dead and recoverable. Fires <see cref="CrewDied"/>.</summary>
        public void Kill(CrewInstance c)
        {
            if (c == null || !c.isAlive) return;
            c.isAlive = false;
            c.currentHealth = 0;
            c.locationTag = "dead_recoverable";
            Debug.Log($"[CrewManager] {Name(c)} has died.");
            CrewDied?.Invoke(c);
        }

        // ---- Queries ----

        public CrewInstance GetMember(string instanceId)
        {
            if (!Ready(nameof(GetMember))) return null;
            foreach (var c in _run.ActiveCrew) if (c.instanceId == instanceId) return c;
            foreach (var c in _run.RescuedCrew) if (c.instanceId == instanceId) return c;
            return null;
        }

        public IReadOnlyList<CrewInstance> ActiveCrew
            => Ready(nameof(ActiveCrew)) ? _run.ActiveCrew : Array.Empty<CrewInstance>();

        public int AliveCount()
        {
            if (!Ready(nameof(AliveCount))) return 0;
            int n = 0;
            foreach (var c in _run.ActiveCrew) if (c.isAlive) n++;
            return n;
        }

        // ---- Internals ----

        private bool Ready(string op)
        {
            if (_run != null) return true;
            Debug.LogError($"[CrewManager] {op} called before Bind(RunData). No-op.");
            return false;
        }

        private CrewInstance Require(string instanceId, string op)
        {
            if (!Ready(op)) return null;
            var c = GetMember(instanceId);
            if (c == null) Debug.LogWarning($"[CrewManager] {op}: no crew with instanceId '{instanceId}'.");
            return c;
        }

        private CrewInstance RequireAlive(string instanceId, string op)
        {
            var c = Require(instanceId, op);
            if (c == null) return null;
            if (!c.isAlive)
            {
                Debug.LogWarning($"[CrewManager] {op}: {Name(c)} is dead. No-op.");
                return null;
            }
            return c;
        }

        private int MaxHealth(CrewInstance c)
        {
            var data = _db.GetCrew(c.crewDataId);
            return data != null ? data.baseStats.maxHealth : 100;
        }

        private string Name(CrewInstance c)
        {
            var data = _db.GetCrew(c.crewDataId);
            return data != null ? $"{data.lastName} ({Short(c.instanceId)})" : Short(c.instanceId);
        }

        private static string Short(string id)
            => string.IsNullOrEmpty(id) ? "?" : (id.Length >= 6 ? id.Substring(0, 6) : id);

        private static string Signed(int v) => v >= 0 ? $"+{v}" : v.ToString();
    }
}
