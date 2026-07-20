// Assets/_Project/Scripts/Gameplay/BunkerDayConfig.cs
namespace OblastZero.Gameplay
{
    /// <summary>
    /// Tunable rates for one bunker day advance. Plain injectable config with sane defaults so the turn
    /// engine never hard-codes magic numbers. These belong in BalanceConstants long-term — once the
    /// constant names are settled, swap the defaults here to read from BalanceConstants.
    /// </summary>
    public class BunkerDayConfig
    {
        /// <summary>Food units each living crew member needs per day.</summary>
        public int rationsPerCrewPerDay = 1;

        /// <summary>Fatigue gained per crew per day (life in the bunker is tiring).</summary>
        public int fatiguePerDay = 10;

        /// <summary>Baseline sanity lost per crew per day (the slow dread).</summary>
        public int sanityDrainPerDay = 3;

        /// <summary>Extra sanity lost when a crew member is suffering radiation sickness.</summary>
        public int sanityDrainFromSickness = 4;

        /// <summary>Extra sanity lost when a crew member goes unfed.</summary>
        public int sanityDrainFromStarvation = 6;

        /// <summary>Fraction of the bunker radiation pool each crew member absorbs per day (0.10 = 10%).</summary>
        public float radiationPoolBleedFactor = 0.10f;

        /// <summary>Radiation level at or above which a crew member takes daily health damage.</summary>
        public int radiationSicknessThreshold = 50;

        /// <summary>Health lost per day while radiation-sick.</summary>
        public int radiationHealthDamage = 5;

        /// <summary>Health lost per day while unfed.</summary>
        public int starvationHealthDamage = 8;

        /// <summary>Health regained per day when fed, rested, and not otherwise harmed.</summary>
        public int passiveHealthRegen = 3;

        /// <summary>Fatigue must be at or below this for passive health regen to apply.</summary>
        public int restedFatigueCeiling = 60;
    }
}
