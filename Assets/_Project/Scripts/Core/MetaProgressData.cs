// Assets/_Project/Scripts/Core/MetaProgressData.cs
using System;
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// Persistent progression that survives permadeath. Lives in its own save channel, separate
    /// from RunData (the bifurcated save design). Written by the run-end states (RunVictory_* /
    /// RunFailed) and loaded once by MainMenuState.
    /// </summary>
    [Serializable]
    public class MetaProgressData
    {
        public int totalRunsAttempted;
        public int totalRunsSurvived;
        public List<string> unlockedScavengeSites = new();
        public List<string> unlockedStartingKits = new();
        public List<string> unlockedCrewArchetypes = new();
        public List<string> discoveredAnomalyIds = new();
        public List<string> discoveredMutantIds = new();
        public List<string> recoveredDocumentIds = new();
        public List<string> unlockedEndings = new();
        public Dictionary<string, int> steamStats = new();
    }
}
