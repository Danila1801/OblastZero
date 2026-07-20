// Assets/_Project/Scripts/Core/StateContext.cs
namespace OblastZero.Core
{
    /// <summary>
    /// The shared bag of data passed into every state on enter/exit.
    /// CurrentRun is the canonical permadeath run object (rebuilt every new run).
    /// MetaProgress is the persistent cross-run progression (loaded once, carried forward).
    /// This is the only object that travels across the whole state machine.
    /// </summary>
    public class StateContext
    {
        public RunData CurrentRun { get; set; }
        public MetaProgressData MetaProgress { get; set; }
    }
}
