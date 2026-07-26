namespace OblastZero.Core.States
{
    /// <summary>Stabilization ending: Scale Society official contract fulfilled.</summary>
    public class RunVictoryStabilizationState : RunEndVictoryStateBase
    {
        public override string StateId => "RunVictory_Stabilization";
        public override GameState StateEnum => GameState.RunVictory_Stabilization;

        protected override string EndingName => "Stabilization";
        protected override string EndingTitle => "— STABILIZATION PROTOCOL COMPLETE —";
        protected override string EndingNarrative =>
            "The bunker is sealed. The Scale Society has filed your report.\n\n" +
            "\"Expeditionary tenure concluded. All objectives met per Protocol 7. " +
            "Commendation level: Administrative. Your crew is transferred to the Reserve Registry.\"\n\n" +
            "The buses will arrive tomorrow. Or the day after. The forms do not specify.";
    }
}
