namespace OblastZero.Core.States
{
    /// <summary>Relief ending: Cordon military escort.</summary>
    public class RunVictoryReliefState : RunEndVictoryStateBase
    {
        public override string StateId => "RunVictory_Relief";
        public override GameState StateEnum => GameState.RunVictory_Relief;

        protected override string EndingName => "Relief";
        protected override string EndingTitle => "— EXTRACTION PROTOCOL AUTHORIZED —";
        protected override string EndingNarrative =>
            "The Cordon checkpoint lights turn green. A vehicle radio crackles.\n\n" +
            "\"Relief convoy en route. Stand by for extraction in twelve hours. " +
            "Pack what you can carry. Leave the rest.\"\n\n" +
            "Your crew has survived. The objective is complete. The rest is paperwork.";
    }
}
