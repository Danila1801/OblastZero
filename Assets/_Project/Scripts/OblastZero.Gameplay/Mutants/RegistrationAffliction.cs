// Assets/_Project/Scripts/OblastZero.Gameplay/Mutants/RegistrationAffliction.cs
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay.Mutants
{
    /// <summary>
    /// "Registered" — the permanent penalty a Drowned Census-Taker (MTN-Β-04/DC) applies by completing
    /// an entry for the player. The bible: a permanent stat penalty for the remainder of the run,
    /// multiple registrations stack, and the penalty applies in both phases.
    ///
    /// <para><b>It is applied to the crew member, not to the player object, and that is what makes it
    /// permanent.</b> The scavenge scene is destroyed at the transition cutscene, so anything held on a
    /// scene component ends with the Blowout. Writing the loss into the operator's
    /// <c>CrewInstance</c> through <c>CrewManager</c> puts it in <c>RunData</c>, which is serialized,
    /// survives save/load, and is the same health and sanity the bunker phase reads. That is the
    /// bible's "applies in both Phase A and Phase B", achieved by not having two representations of it
    /// in the first place.</para>
    ///
    /// <para><b>The count lives on the run, not here.</b> Registrations stack across an entire run and
    /// across scene loads, so a static counter on this class would be wrong twice over: it would
    /// survive into the *next* run (a static outlives a scene) while not surviving a save/load (a
    /// static is not serialized). <c>RunData.registrationCount</c> is the only correct home.</para>
    /// </summary>
    public static class RegistrationAffliction
    {
        /// <summary>
        /// Records one completed registration and applies its penalties. Returns the run's new total.
        /// Returns 0 and applies nothing when there is no run or no living operator.
        /// </summary>
        public static int Register(string sourceClassificationCode)
        {
            var gm = GameManager.Instance;
            var run = gm != null ? gm.CurrentRun : null;
            var crew = gm != null ? gm.Crew : null;

            if (run == null || crew == null)
            {
                Debug.LogWarning($"[{sourceClassificationCode}] Registration completed with no active " +
                                 "run — nothing to record it against.");
                return 0;
            }

            var operatorInstance = crew.FieldOperator();
            if (operatorInstance == null)
            {
                Debug.LogWarning($"[{sourceClassificationCode}] Registration completed with nobody in " +
                                 "the field. The entry has no subject.");
                return run.registrationCount;
            }

            run.registrationCount++;

            crew.ApplyHealthDelta(operatorInstance.instanceId, -BalanceConstants.REGISTRATION_HEALTH_PENALTY);
            crew.ApplySanityDelta(operatorInstance.instanceId, -BalanceConstants.REGISTRATION_SANITY_PENALTY);

            Debug.Log($"[{sourceClassificationCode}] '{operatorInstance.instanceId}' has been entered in " +
                      $"the register (x{run.registrationCount}). " +
                      $"-{BalanceConstants.REGISTRATION_HEALTH_PENALTY} health, " +
                      $"-{BalanceConstants.REGISTRATION_SANITY_PENALTY} sanity, for the remainder of the run.");

            EventBus.Raise(new PlayerRegisteredEvent
            {
                TotalRegistrations = run.registrationCount,
                HealthPenaltyApplied = BalanceConstants.REGISTRATION_HEALTH_PENALTY,
                SanityPenaltyApplied = BalanceConstants.REGISTRATION_SANITY_PENALTY
            });

            return run.registrationCount;
        }

        /// <summary>Registrations recorded against the current run. 0 outside a run.</summary>
        public static int CountForCurrentRun()
        {
            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            return run != null ? run.registrationCount : 0;
        }
    }
}
