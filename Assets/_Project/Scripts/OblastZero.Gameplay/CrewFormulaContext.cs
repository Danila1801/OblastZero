// Assets/_Project/Scripts/Gameplay/CrewFormulaContext.cs
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Resolves the <c>crew.*</c> variables used in event success-chance formulas (design bible §6.2) for a
    /// single acting crew member. Bridges the runtime <see cref="CrewInstance"/> stats, the member's
    /// <see cref="CrewMemberData"/> base stats, and trait bonuses into the named values a formula can read.
    /// Unknown names return false so <see cref="FormulaEvaluator"/> throws and the engine falls back to the
    /// choice's static <c>successChance</c>.
    ///
    /// Supported variables (all doubles):
    ///   crew.health, crew.sanity, crew.fatigue, crew.radiation                       — raw 0..100
    ///   crew.health_norm, crew.sanity_norm, crew.fatigue_norm, crew.radiation_norm   — 0..1
    ///   crew.combat    — 0..100, from combatResolutionMultiplier + trait combat bonuses (x50 scale)
    ///   crew.charisma  — 0..100, social poise; derived from current sanity (a lucid negotiator persuades better)
    /// </summary>
    public sealed class CrewFormulaContext
    {
        private readonly CrewInstance _inst;
        private readonly CrewMemberData _data;
        private readonly GameDatabase _db;

        public CrewFormulaContext(CrewInstance inst, CrewMemberData data, GameDatabase db)
        {
            _inst = inst;
            _data = data;
            _db = db;
        }

        public bool TryResolve(string name, out double value)
        {
            value = 0;
            if (_inst == null) return false;

            switch (name)
            {
                case "crew.health": value = _inst.currentHealth; return true;
                case "crew.sanity": value = _inst.currentSanity; return true;
                case "crew.fatigue": value = _inst.currentFatigue; return true;
                case "crew.radiation": value = _inst.currentRadiation; return true;

                case "crew.health_norm": value = _inst.currentHealth / 100.0; return true;
                case "crew.sanity_norm": value = _inst.currentSanity / 100.0; return true;
                case "crew.fatigue_norm": value = _inst.currentFatigue / 100.0; return true;
                case "crew.radiation_norm": value = _inst.currentRadiation / 100.0; return true;

                case "crew.combat": value = Combat(); return true;
                case "crew.charisma": value = _inst.currentSanity; return true;

                default: return false;
            }
        }

        private double Combat()
        {
            float mult = _data != null ? _data.baseStats.combatResolutionMultiplier : 1f;
            if (_db != null && _inst.traitIds != null)
            {
                foreach (var tid in _inst.traitIds)
                {
                    if (string.IsNullOrEmpty(tid)) continue;
                    var t = _db.GetTrait(tid);
                    if (t != null) mult += t.modifiers.combatResolutionBonus;
                }
            }
            // combatResolutionMultiplier is 0..2 (1 = baseline), so x50 maps a baseline crew to ~50/100.
            return Mathf.Clamp((float)(mult * 50.0), 0f, 100f);
        }
    }
}
