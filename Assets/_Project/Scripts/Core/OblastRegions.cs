// Assets/_Project/Scripts/Core/OblastRegions.cs
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// The seven canonical geographic regions of the oblast (design bible §2, mirrored in BESTIARY.md
    /// "REGION TAGS"). This is the *world* vocabulary: which part of the oblast a place is in.
    ///
    /// <para><b>This is a second axis, not a replacement for <see cref="RegionTags"/>.</b> The two answer
    /// different questions and neither can be derived from the other:</para>
    /// <list type="bullet">
    ///   <item><see cref="RegionTags"/> is a <i>proximity</i> vocabulary — how far a place is from the bunker
    ///   door (interior / immediate approach / remote). That is what a bunker day gates on, because a sealed-in
    ///   crew can act on the perimeter and cannot act on a drainage tunnel forty kilometres away.</item>
    ///   <item><see cref="OblastRegions"/> is a <i>geographic</i> vocabulary — which district of the oblast the
    ///   run is operating in. That is what a scavenge site declares, and what tints a run's narrative texture.</item>
    /// </list>
    ///
    /// <para>A crew in the bunker kitchen is simultaneously at <c>kitchen_block</c> (proximity: interior) and
    /// in whichever region their site sits in. Collapsing the two vocabularies into one field would force a
    /// choice between them, and losing the proximity axis takes the bunker-day event pool offline — the exact
    /// failure <see cref="RegionTags"/>'s doc comment was written to prevent and that commit 109f1ad fixed.
    /// Measured against the shipped corpus, a straight replacement blacks out 452 of the 858 events a bunker
    /// day can currently reach.</para>
    ///
    /// <para><b>The region gate fails OPEN, deliberately.</b> The locale gate in
    /// <c>EventEngine.PassesPrerequisites</c> fails closed: an event that names locales and a caller that names
    /// none produce no match. That is correct for proximity (an untagged caller genuinely has no location) but
    /// it is also the sharpest edge in the whole content pipeline, and one such edge is enough. An event that
    /// names regions and a caller that names none therefore <i>passes</i>. The consequence is that this axis
    /// can narrow a pool but can never empty one, so adding regions to content is incapable of causing a
    /// blackout no matter how the mapping is authored.</para>
    /// </summary>
    public static class OblastRegions
    {
        /// <summary>The administered edge. Checkpoints, the interdiction line, the road in.</summary>
        public const string OuterCordon = "outer_cordon";

        /// <summary>Records, schools, registry annexes. Where the oblast wrote itself down.</summary>
        public const string CensusDistrict = "census_district";

        /// <summary>Standing water, pumping stations, the drainage network beneath both.</summary>
        public const string Reservoir = "reservoir";

        /// <summary>Silos, depots, processing plant, the defunct rail line that served them.</summary>
        public const string GrainBelt = "grain_belt";

        /// <summary>Administrative buildings. Offices that outlived their ministries.</summary>
        public const string BureauQuarter = "bureau_quarter";

        /// <summary>Inside the exclusion perimeter proper. Nothing routine is filed from here.</summary>
        public const string InnerRing = "inner_ring";

        /// <summary>The Reality Distortion Field's near edge. Bible §6.3 endgame ground.</summary>
        public const string Threshold = "threshold";

        /// <summary>Every canonical region, in bible order. Used to validate authored content.</summary>
        public static readonly IReadOnlyList<string> All = new[]
        {
            OuterCordon,
            CensusDistrict,
            Reservoir,
            GrainBelt,
            BureauQuarter,
            InnerRing,
            Threshold,
        };

        /// <summary>
        /// Which oblast regions a proximity locale implies. Mirrored byte-for-byte by
        /// <c>tools/migrate_event_tags.py</c>, which derives every event's <c>oblastRegionsAny</c> from its
        /// existing <c>regionTagsAny</c> using this same table.
        ///
        /// <para><b>An empty list means region-agnostic, and that is the common case on purpose.</b> The
        /// bunker interior and its immediate approaches exist at whichever site the run registered for — a
        /// ration dispute in the kitchen block is the same ration dispute in the Grain Belt as in the Census
        /// District. Pinning those events to one region would make site choice silently delete two-thirds of
        /// the bunker-day corpus, which is the same defect as the naive migration, arriving by a slower road.
        /// Only the five genuinely remote locales carry a region, because only those name a real place.</para>
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string[]> LocaleToRegions =
            new Dictionary<string, string[]>
            {
                // Interior and approaches: region-agnostic. The bunker is wherever the run is.
                { RegionTags.BunkerInterior,    new string[0] },
                { RegionTags.KitchenBlock,      new string[0] },
                { RegionTags.BasementCorridor,  new string[0] },
                { RegionTags.Perimeter,         new string[0] },
                { RegionTags.AccessRoad,        new string[0] },

                // Remote locales name real ground, so they carry the region that ground is in.
                { RegionTags.OldFactory,        new[] { GrainBelt } },
                { RegionTags.AbandonedSchool,   new[] { CensusDistrict } },
                { RegionTags.CollapsedBuilding, new[] { BureauQuarter } },
                { RegionTags.DrainageTunnel,    new[] { Reservoir } },
                { RegionTags.ForestEdge,        new[] { OuterCordon } },
            };

        /// <summary>True when <paramref name="region"/> is one of the seven canonical regions.</summary>
        public static bool IsCanonical(string region)
        {
            if (string.IsNullOrEmpty(region)) return false;
            for (int i = 0; i < All.Count; i++)
                if (All[i] == region) return true;
            return false;
        }

        /// <summary>
        /// The regions implied by a set of proximity locales, deduplicated. Returns an empty list when every
        /// supplied locale is region-agnostic — which, per the fail-open rule above, means "matches anywhere".
        /// </summary>
        public static List<string> RegionsFor(IReadOnlyCollection<string> locales)
        {
            var result = new List<string>();
            if (locales == null) return result;

            foreach (var locale in locales)
            {
                if (string.IsNullOrEmpty(locale)) continue;
                if (!LocaleToRegions.TryGetValue(locale, out var regions) || regions == null) continue;
                foreach (var region in regions)
                    if (!result.Contains(region)) result.Add(region);
            }

            return result;
        }

        /// <summary>
        /// Display name for a region id, for UI that shows the player where they are registered to go.
        /// Falls back to the raw id so an unmapped region renders as itself rather than as blank.
        /// </summary>
        public static string DisplayNameOf(string region)
        {
            switch (region)
            {
                case OuterCordon:    return "The Outer Cordon";
                case CensusDistrict: return "The Census District";
                case Reservoir:      return "The Reservoir";
                case GrainBelt:      return "The Grain Belt";
                case BureauQuarter:  return "The Bureau Quarter";
                case InnerRing:      return "The Inner Ring";
                case Threshold:      return "The Threshold";
                default:             return string.IsNullOrEmpty(region) ? "Unsurveyed" : region;
            }
        }
    }
}
