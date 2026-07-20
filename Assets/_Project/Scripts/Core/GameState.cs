namespace OblastZero.Core
{
    /// <summary>
    /// Top-level game flow states. Aligns with DESIGN_BIBLE Section 6.3.
    /// String IDs on the IGameState implementations match these names 1:1.
    /// </summary>
    public enum GameState
    {
        None,                        // Pre-boot only; never a live state
        Boot,                        // Bootstrap scene loading core systems
        MainMenu,                    // Title screen, profile select
        HomeBunker,                  // Meta hub — Darkest-Dungeon-style Hamlet
        RunSetup,                    // Crew/loadout/site selection
        ScavengePhase3D,             // 60-second 3D scavenge (Phase A)
        TransitionCutscene,          // 3D → 2D handoff cinematic
        SurvivalPhase2D,             // Bunker survival (Phase B)
        RunFailed,                   // All crew dead / breach / failure ending
        RunVictory_Stabilization,    // Scale Society ending
        RunVictory_Relief,           // Cordon ending
        RunVictory_Adaptation,       // Kafedra ending
        RunVictory_Independent,      // Rare neutral ending
        Paused,                      // Overlay state — does not replace previous
    }
}
