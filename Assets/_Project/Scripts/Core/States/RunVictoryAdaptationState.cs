namespace OblastZero.Core.States
{
    /// <summary>Adaptation ending: Kafedra transcendence.</summary>
    public class RunVictoryAdaptationState : RunEndVictoryStateBase
    {
        public override string StateId => "RunVictory_Adaptation";
        public override GameState StateEnum => GameState.RunVictory_Adaptation;

        protected override string EndingName => "Adaptation";
        protected override string EndingTitle => "— THE THRESHOLD CROSSED —";
        protected override string EndingNarrative =>
            "The Reality Field does not fight you. It inhales.\n\n" +
            "Your crew stands at the chamber's edge. The mutation is painless. " +
            "Already your thoughts synchronize with something vast and patient.\n\n" +
            "You are no longer leaving the Oblast. The Oblast is becoming you.";
    }
}
