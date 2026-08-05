// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/DefectiveItemEffects.cs
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// What happens when a crew member uses something a Carbon Copy printed. This is the second half of
    /// ANM-Δ-07/CC and the reason the anomaly is a decision rather than a curiosity: the cost of grabbing
    /// four crates lands days later, in the bunker, on whichever crate the crew happened to reach for.
    ///
    /// <para><b>Which copy gets used is chance, not bookkeeping.</b> The bible is explicit that the crew
    /// cannot tell them apart — "the crate on the table looks correct, the crate on the floor also looks
    /// correct". So consumption picks a stack weighted by quantity, and a player holding one genuine med
    /// kit and three copies takes a 75% risk every time they use one. Nothing in the UI marks the
    /// difference, because nothing in the fiction can.</para>
    ///
    /// <para><b>The defects are per-category and drawn from the bible's own examples.</b> Each is a wrong
    /// detail rather than a broken object, which is the whole register: the tin is sealed and the label is
    /// in the wrong Cyrillic; the med kit's contents are correct and the syringes inject the wrong fluid;
    /// the document is signed by someone who could not have signed it.</para>
    /// </summary>
    public static class DefectiveItemEffects
    {
        /// <summary>What a defective item did when it was used. Reported into the resolution log.</summary>
        public struct Result
        {
            public bool Applied;
            public string Summary;

            /// <summary>True when the defect should turn a successful outcome into a failed one.</summary>
            public bool ForcesFailure;
        }

        /// <summary>
        /// Applies the defect for one consumed defective stack.
        ///
        /// <paramref name="roll"/> is a single pre-drawn value in [0,1) from the run's RNG stream — passed
        /// in rather than drawn here so the whole resolution stays reproducible from the run seed, which is
        /// the standing rule for anything that branches.
        /// </summary>
        public static Result Apply(ItemData data, CrewManager crew, FactionReputationManager reputation,
                                   string actingCrewInstanceId, float roll)
        {
            var result = new Result { Applied = false, Summary = string.Empty, ForcesFailure = false };
            if (data == null) return result;

            switch (data.category)
            {
                case ItemCategory.Medical:
                    return WrongFluid(data, crew, actingCrewInstanceId);

                case ItemCategory.Food:
                case ItemCategory.Water:
                    return WrongLabel(data, crew, actingCrewInstanceId, roll);

                case ItemCategory.Document:
                    return WrongSignature(data, reputation);

                case ItemCategory.Weapon:
                case ItemCategory.Ammunition:
                    return MisalignedSights(data, roll);

                default:
                    // Tools, crafting stock and the rest have no authored defect. Reported anyway: a copy
                    // that does nothing is still the player learning that some copies do nothing, which is
                    // what makes the ones that do something land.
                    result.Applied = true;
                    result.Summary = $"'{data.id}' was one of the copies. Nothing about it is obviously wrong.";
                    return result;
            }
        }

        /// <summary>
        /// Medical. Bible: "med kit with correct contents but syringes inject wrong fluid." Deterministic —
        /// a med kit either heals or it does not, and leaving that to a second roll would make the defect
        /// feel like bad luck rather than like the wrong bottle.
        /// </summary>
        private static Result WrongFluid(ItemData data, CrewManager crew, string actingCrewInstanceId)
        {
            var target = ResolveTarget(crew, actingCrewInstanceId);
            if (target == null)
            {
                return new Result
                {
                    Applied = true,
                    ForcesFailure = true,
                    Summary = $"'{data.id}' was a copy. The syringes hold the wrong fluid."
                };
            }

            crew.ApplyHealthDelta(target.instanceId, -BalanceConstants.DEFECT_MEDICAL_HEALTH_PENALTY);
            crew.ApplySanityDelta(target.instanceId, -BalanceConstants.DEFECT_MEDICAL_SANITY_PENALTY);

            return new Result
            {
                Applied = true,
                ForcesFailure = true,
                Summary = $"'{data.id}' was a copy. Contents correct, fluid wrong: " +
                          $"{BalanceConstants.DEFECT_MEDICAL_HEALTH_PENALTY} health and " +
                          $"{BalanceConstants.DEFECT_MEDICAL_SANITY_PENALTY} sanity off " +
                          $"'{target.instanceId}' instead of the treatment."
            };
        }

        /// <summary>Food and water. Bible: "tin of meat with wrong Cyrillic on label." Half the time it is edible.</summary>
        private static Result WrongLabel(ItemData data, CrewManager crew, string actingCrewInstanceId, float roll)
        {
            if (roll >= BalanceConstants.DEFECT_FOOD_POISONING_CHANCE)
            {
                return new Result
                {
                    Applied = true,
                    Summary = $"'{data.id}' was a copy. The label is in the wrong Cyrillic. " +
                              "The contents were edible regardless."
                };
            }

            var target = ResolveTarget(crew, actingCrewInstanceId);
            if (target != null)
            {
                crew.ApplyHealthDelta(target.instanceId, -BalanceConstants.DEFECT_FOOD_HEALTH_PENALTY);
                crew.ApplyFatigueDelta(target.instanceId, BalanceConstants.DEFECT_FOOD_FATIGUE_PENALTY);
            }

            return new Result
            {
                Applied = true,
                ForcesFailure = true,
                Summary = $"'{data.id}' was a copy and the contents did not match the label. " +
                          (target != null
                              ? $"'{target.instanceId}' is ill: {BalanceConstants.DEFECT_FOOD_HEALTH_PENALTY} health, " +
                                $"+{BalanceConstants.DEFECT_FOOD_FATIGUE_PENALTY} fatigue."
                              : "Nobody was on hand to be ill from it.")
            };
        }

        /// <summary>
        /// Documents. Bible: "document signed by someone who couldn't have signed it." Filing that with the
        /// Scale Society is the reputation hit — they are the faction that reads signatures.
        /// </summary>
        private static Result WrongSignature(ItemData data, FactionReputationManager reputation)
        {
            if (reputation == null)
            {
                return new Result
                {
                    Applied = true,
                    ForcesFailure = true,
                    Summary = $"'{data.id}' carried a signature that could not have been made."
                };
            }

            reputation.ApplyDelta(FactionId.ScaleSociety, -BalanceConstants.DEFECT_DOCUMENT_REPUTATION_PENALTY);

            return new Result
            {
                Applied = true,
                ForcesFailure = true,
                Summary = $"'{data.id}' was a copy. The countersignature belongs to someone who could not " +
                          $"have made it, and it was filed anyway: " +
                          $"{BalanceConstants.DEFECT_DOCUMENT_REPUTATION_PENALTY} standing with the Scale Society."
            };
        }

        /// <summary>Weapons. Bible-adjacent: the copy's sights do not agree with its barrel.</summary>
        private static Result MisalignedSights(ItemData data, float roll)
        {
            bool fails = roll < BalanceConstants.DEFECT_WEAPON_FAILURE_CHANCE;
            return new Result
            {
                Applied = true,
                ForcesFailure = fails,
                Summary = fails
                    ? $"'{data.id}' was a copy. The sights do not agree with the barrel; the attempt failed."
                    : $"'{data.id}' was a copy. It shot low and left, and it was enough."
            };
        }

        private static CrewInstance ResolveTarget(CrewManager crew, string actingCrewInstanceId)
        {
            if (crew == null) return null;

            if (!string.IsNullOrEmpty(actingCrewInstanceId))
            {
                var acting = crew.GetMember(actingCrewInstanceId);
                if (acting != null && acting.isAlive) return acting;
            }

            var roster = crew.ActiveCrew;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].isAlive) return roster[i];

            return null;
        }
    }
}
