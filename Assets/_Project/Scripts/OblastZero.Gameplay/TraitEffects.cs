// Assets/_Project/Scripts/Gameplay/TraitEffects.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Resolves the aggregate <see cref="CrewStatModifiers"/> a crew member's traits apply, and turns that
    /// aggregate into the specific numbers the managers need.
    ///
    /// <para><b>Why this is a resolver and not a rules table.</b> Traits already carry their own numbers:
    /// <see cref="TraitData.modifiers"/> is an authored struct on a ScriptableObject, editable by a designer,
    /// loaded through <see cref="GameDatabase"/>, and indexed by id. Re-encoding those numbers as a
    /// <c>switch</c> over hardcoded trait ids would put the balance in two places at once — the .asset the
    /// designer edits and the .cs nobody remembers to — and would inline magic numbers into system code
    /// against the standing rule in CLAUDE.md §5. It would also silently do nothing for the trait ids that
    /// do not appear in the switch, which is every trait authored after the switch was written. This class
    /// therefore knows the *shape* of a trait effect and nothing about any individual trait.</para>
    ///
    /// <para><b>What was actually broken.</b> Traits were never inert: <c>CrewManager.AddRescued</c> copies
    /// <c>startingTraits</c> onto every instance, <c>EventEngine</c> gates event availability on
    /// <c>requiredCrewTraitIds</c> and each choice's <c>requiredTraitsAny</c> / <c>blockedByTraits</c> (406
    /// of the shipped choices use them), and <c>CrewFormulaContext</c> already folds
    /// <c>combatResolutionBonus</c> into its <c>crew.combat</c> variable. What had no consumer was the rest
    /// of the modifier struct — <c>maxHealthDelta</c>, <c>maxSanityDelta</c>, <c>sanityRecoveryBonus</c>,
    /// <c>radiationResistanceBonus</c> and <c>carryCapacityBonus</c> were authored and then read by nobody.
    /// This class is their consumer.</para>
    ///
    /// <para><b>Combat is deliberately absent here.</b> <see cref="CrewFormulaContext"/> already sums
    /// <c>combatResolutionBonus</c> into the <c>crew.combat</c> formula variable. Adding a second combat
    /// application at event-resolution time would compound the same trait twice — a 20% bonus landing as
    /// 44% — which is exactly the kind of defect that reads as "the numbers feel off" and never gets traced.
    /// One consumer per modifier field.</para>
    ///
    /// <para>Static and pure: every method takes what it needs and mutates nothing, so the whole trait
    /// surface is testable headless without a run, a scene, or the state machine.</para>
    /// </summary>
    public static class TraitEffects
    {
        /// <summary>
        /// Sums the modifiers of every trait on a crew member. Unknown trait ids are skipped — content can
        /// reference a trait that has not been authored yet without taking the crew member's stats with it.
        ///
        /// <para>Bonuses are additive-to-one by the schema's own convention (0 = no change, +0.25 = +25%),
        /// so summing is the correct composition: two +20% traits give +40%, not +44%. Flat deltas sum
        /// likewise.</para>
        /// </summary>
        public static CrewStatModifiers Aggregate(CrewInstance crew, GameDatabase db)
        {
            var total = new CrewStatModifiers();
            if (crew?.traitIds == null || db == null) return total;

            foreach (var traitId in crew.traitIds)
            {
                if (string.IsNullOrEmpty(traitId)) continue;

                var trait = db.GetTrait(traitId);
                if (trait == null) continue; // GameDatabase already logged the miss

                var m = trait.modifiers;
                total.maxHealthDelta += m.maxHealthDelta;
                total.maxSanityDelta += m.maxSanityDelta;
                total.sanityRecoveryBonus += m.sanityRecoveryBonus;
                total.radiationResistanceBonus += m.radiationResistanceBonus;
                total.combatResolutionBonus += m.combatResolutionBonus;
                total.carryCapacityBonus += m.carryCapacityBonus;
            }

            return total;
        }

        /// <summary>
        /// A crew member's effective maximum health: the authored base, plus the run's unlock bonus, plus
        /// every trait's flat delta. Floored at one — a stack of afflictions may make someone fragile, but a
        /// zero maximum is a crew member who is dead on creation, which reads as a crash rather than as a
        /// trait.
        /// </summary>
        public static int MaxHealth(CrewInstance crew, CrewMemberData data, GameDatabase db, int runBonus)
        {
            int baseMax = data != null ? data.baseStats.maxHealth : BalanceConstants.CREW_HEALTH_MAX;
            int withTraits = baseMax + runBonus + Aggregate(crew, db).maxHealthDelta;
            return Mathf.Max(1, withTraits);
        }

        /// <summary>A crew member's effective maximum sanity. Same composition and floor as <see cref="MaxHealth"/>.</summary>
        public static int MaxSanity(CrewInstance crew, CrewMemberData data, GameDatabase db, int runBonus)
        {
            int baseMax = data != null ? data.baseStats.maxSanity : BalanceConstants.CREW_SANITY_MAX;
            int withTraits = baseMax + runBonus + Aggregate(crew, db).maxSanityDelta;
            return Mathf.Max(1, withTraits);
        }

        /// <summary>
        /// The multiplier applied to positive sanity recovery: the crew member's authored multiplier scaled
        /// by their traits' summed <c>sanityRecoveryBonus</c>. Clamped non-negative so a deep stack of
        /// afflictions costs a member their recovery rather than inverting it into sanity damage.
        /// </summary>
        public static float SanityRecoveryMultiplier(CrewInstance crew, CrewMemberData data, GameDatabase db)
        {
            float authored = data != null ? data.baseStats.sanityRecoveryMultiplier : 1f;
            if (authored <= 0f) authored = 1f;
            return Mathf.Max(0f, authored * (1f + Aggregate(crew, db).sanityRecoveryBonus));
        }

        /// <summary>
        /// The divisor applied to incoming radiation: the crew member's authored resistance scaled by their
        /// traits' summed <c>radiationResistanceBonus</c>. Higher is more resistant, matching
        /// <see cref="CrewManager.ApplyRadiation"/>'s existing <c>amount / resistance</c> convention.
        ///
        /// <para>Floored well above zero. A resistance approaching zero divides a modest dose into a lethal
        /// one, so a trait that is meant to make someone frail would instead delete them on the first
        /// contaminated day.</para>
        /// </summary>
        public static float RadiationResistance(CrewInstance crew, CrewMemberData data, GameDatabase db)
        {
            float authored = data != null ? data.baseStats.radiationResistanceMultiplier : 1f;
            if (authored <= 0f) authored = 1f;
            return Mathf.Max(kMinResistance, authored * (1f + Aggregate(crew, db).radiationResistanceBonus));
        }

        /// <summary>
        /// Carry capacity in kg after traits. Applied to the lead operator's authored capacity in
        /// <c>GameManager.BeginNewRun</c>, before the unlock bonus and before the
        /// <see cref="BalanceConstants.SCAVENGE_MIN_CARRY_WEIGHT_KG"/> floor.
        /// </summary>
        public static float ModifiedCarryCapacity(CrewInstance crew, float authoredKg, GameDatabase db)
        {
            return Mathf.Max(0f, authoredKg * (1f + Aggregate(crew, db).carryCapacityBonus));
        }

        /// <summary>
        /// The trait ids on a crew member that the database can actually resolve, with their display names.
        /// Used by the setup screen to show a roster's traits as badges. Unresolvable ids are skipped rather
        /// than rendered raw — a badge reading <c>trait_iron_stomach</c> is a bug report, not a UI.
        /// </summary>
        public static List<string> DisplayNames(IReadOnlyList<string> traitIds, GameDatabase db)
        {
            var names = new List<string>();
            if (traitIds == null || db == null) return names;

            foreach (var traitId in traitIds)
            {
                if (string.IsNullOrEmpty(traitId)) continue;
                var trait = db.GetTrait(traitId);
                if (trait == null) continue;
                names.Add(string.IsNullOrEmpty(trait.displayName) ? trait.id : trait.displayName);
            }
            return names;
        }

        /// <summary>
        /// Display names for a <see cref="CrewMemberData"/>'s authored starting traits — the setup screen's
        /// case, where no CrewInstance exists yet because nobody has been registered.
        /// </summary>
        public static List<string> StartingTraitNames(CrewMemberData data)
        {
            var names = new List<string>();
            if (data?.startingTraits == null) return names;

            foreach (var trait in data.startingTraits)
            {
                if (trait == null) continue;
                names.Add(string.IsNullOrEmpty(trait.displayName) ? trait.id : trait.displayName);
            }
            return names;
        }

        /// <summary>
        /// Floor under the radiation resistance divisor. Below roughly this, a routine bunker dose starts
        /// rounding into double-digit damage; see <see cref="RadiationResistance"/>.
        /// </summary>
        private const float kMinResistance = 0.25f;
    }
}
