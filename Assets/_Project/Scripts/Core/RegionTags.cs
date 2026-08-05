// Assets/_Project/Scripts/Core/RegionTags.cs
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// The region-tag vocabulary that <see cref="OblastZero.Data.EventPrerequisite.regionTagsAny"/> is authored
    /// against, plus the subset that is active during a bunker day.
    ///
    /// Why this class exists: every one of the shipped events carries a non-empty <c>regionTagsAny</c>, and
    /// <c>EventEngine.PassesPrerequisites</c> rejects a tagged event when the caller supplies no tags at all.
    /// A caller that omits its tags therefore does not get "fewer events" — it gets *none*, silently, for the
    /// whole run. The bunker day loop used to omit them, which took the entire Phase-2 narrative layer offline
    /// without a single error in the console. Passing <see cref="BunkerPhaseActive"/> is what keeps it online,
    /// so the set lives here as named constants rather than as a string literal at the call site.
    ///
    /// The vocabulary is mirrored from <c>tools/generate_events.py</c> (REGION_TAGS and the per-category
    /// region pools). Python authors the content, this file is what the runtime gates on — if you add a tag
    /// there, add it here, and if you change which tags the generator hands to bunker-flavoured events,
    /// revisit <see cref="BunkerPhaseActive"/>. <see cref="All"/> exists so a boot-time check can prove the
    /// two vocabularies still agree instead of discovering the drift as another silent content blackout.
    /// </summary>
    public static class RegionTags
    {
        // ---- Bunker complex: where the crew actually is during Phase 2 ----

        /// <summary>Inside the bunker proper. The single most-used tag in the shipped event set.</summary>
        public const string BunkerInterior = "bunker_interior";

        /// <summary>The bunker's galley/food stores. Interior.</summary>
        public const string KitchenBlock = "kitchen_block";

        /// <summary>The service corridor below the main floor. Interior.</summary>
        public const string BasementCorridor = "basement_corridor";

        // ---- Immediate approaches: close enough to reach the door on a bunker day ----

        /// <summary>The ground immediately outside the bunker — where visitors and patrols present themselves.</summary>
        public const string Perimeter = "perimeter";

        /// <summary>The road in to the bunker. How anyone with a vehicle or a clipboard arrives.</summary>
        public const string AccessRoad = "access_road";

        // ---- Remote locations: expedition ground, not reachable from a sealed bunker ----

        /// <summary>Remote. Expedition-only.</summary>
        public const string OldFactory = "old_factory";

        /// <summary>Remote. Expedition-only.</summary>
        public const string AbandonedSchool = "abandoned_school";

        /// <summary>Remote. Expedition-only.</summary>
        public const string CollapsedBuilding = "collapsed_building";

        /// <summary>Remote. Expedition-only.</summary>
        public const string DrainageTunnel = "drainage_tunnel";

        /// <summary>Remote. Expedition-only.</summary>
        public const string ForestEdge = "forest_edge";

        /// <summary>
        /// The tags in play while the player is running bunker days: the three interior spaces plus the two
        /// immediate approaches. The five remote tags are deliberately excluded — an anomaly in a collapsed
        /// building on the far side of the oblast is not something a sealed-in crew can act on, and admitting
        /// those events here would present expedition fiction during a management turn.
        ///
        /// Against the shipped corpus this set makes the large majority of events reachable while still
        /// leaving location a real filter rather than a no-op.
        /// </summary>
        public static readonly IReadOnlyList<string> BunkerPhaseActive = new[]
        {
            BunkerInterior,
            KitchenBlock,
            BasementCorridor,
            Perimeter,
            AccessRoad,
        };

        /// <summary>
        /// Every tag in the vocabulary. Used by boot-time content diagnostics to detect drift between this
        /// file and the generator: a tag that appears in authored content but not here can never be matched
        /// by any caller, which is the same silent-blackout failure this class was written to prevent.
        /// </summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            BunkerInterior,
            KitchenBlock,
            BasementCorridor,
            Perimeter,
            AccessRoad,
            OldFactory,
            AbandonedSchool,
            CollapsedBuilding,
            DrainageTunnel,
            ForestEdge,
        };
    }
}
