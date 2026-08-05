// Assets/_Project/Scripts/Core/ArtifactIds.cs
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// The four bible artifacts, by item id (BESTIARY.md "ARTIFACTS REFERENCE"). These four are singled out
    /// from the ~700 shipped items because code reasons about them individually: each has a bespoke use
    /// effect, each drops from a specific anomaly or mutant, and the artifact screen enumerates exactly
    /// these and nothing else.
    ///
    /// <para><b>Why a constants table rather than a category query.</b> The item corpus already has an
    /// Artifact category and it contains dozens of members — anomaly fragments, field debris, the inert and
    /// dormant series. Those are loot with a sale value. These four are systems. Selecting them by category
    /// would sweep the loot in with them and give the use screen forty entries with nothing to do, so the
    /// membership is declared here, once, and <see cref="All"/> is the single list every consumer reads.</para>
    ///
    /// <para>The ids are verified against <c>Assets/Data/Resources/Items/</c> — all four ship. A
    /// misspelling here would produce an artifact that can never be found and a use screen that is always
    /// empty, with no error anywhere, which is why <c>ArtifactSystem</c> validates the table against the
    /// live database at bind time rather than trusting it.</para>
    /// </summary>
    public static class ArtifactIds
    {
        /// <summary>Forms inside undisturbed Carbon Copy anomalies. Re-rolls one event outcome per week.</summary>
        public const string MarginNote = "item_margin_note";

        /// <summary>Interview drop. Halves personal radiation accumulation for one crew member.</summary>
        public const string NotarizedHeart = "item_notarized_heart";

        /// <summary>Interview drop. One-time official override of a Scale Society event.</summary>
        public const string StampedTongue = "item_stamped_tongue";

        /// <summary>The Editor's face. Permanently rewrites one crew stat. Consumed on use.</summary>
        public const string FinalDraft = "item_final_draft";

        /// <summary>All four, in bible table order.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            MarginNote, NotarizedHeart, StampedTongue, FinalDraft
        };

        /// <summary>True when the id is one of the four systems artifacts.</summary>
        public static bool IsArtifact(string itemDataId)
        {
            if (string.IsNullOrEmpty(itemDataId)) return false;
            for (int i = 0; i < All.Count; i++)
                if (All[i] == itemDataId) return true;
            return false;
        }
    }

    /// <summary>
    /// Detection equipment the anomaly layer checks for by id. Separate from <see cref="ArtifactIds"/>
    /// because this is ordinary kit with a mechanical side effect, not a systems artifact.
    ///
    /// <para>The shipped id is <c>item_kafedra_geiger_counter</c> — Kafedra issue. There is no bare
    /// <c>item_geiger_counter</c> in the corpus, and coding against that name yields a detector that never
    /// detects, because <c>GameDatabase</c> logs the miss and returns null while the anomaly silently
    /// concludes the player has no counter.</para>
    /// </summary>
    public static class DetectionItemIds
    {
        /// <summary>Kafedra-issue Geiger counter. Clicks near Geiger-detectable anomalies.</summary>
        public const string GeigerCounter = "item_kafedra_geiger_counter";
    }
}
