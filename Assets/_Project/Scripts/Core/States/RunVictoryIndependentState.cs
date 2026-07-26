namespace OblastZero.Core.States
{
    /// <summary>Independent ending: Escape on your own terms, no faction.</summary>
    public class RunVictoryIndependentState : RunEndVictoryStateBase
    {
        public override string StateId => "RunVictory_Independent";
        public override GameState StateEnum => GameState.RunVictory_Independent;

        protected override string EndingName => "Independent";
        protected override string EndingTitle => "— UNSANCTIONED DEPARTURE —";
        protected override string EndingNarrative =>
            "No buses. No protocols. No one is coming.\n\n" +
            "You rig the hatch and go vertical. The surface wind tastes like metal and ash. " +
            "Your crew walks toward the horizon, carrying what matters.\n\n" +
            "The Zone does not permit departures. But you have learned to ask permission " +
            "in a language it understands.";
    }
}
