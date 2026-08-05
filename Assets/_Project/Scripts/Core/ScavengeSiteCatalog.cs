// Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// One selectable Phase-A location. <see cref="Id"/> is the stable string written to
    /// <see cref="RunData.currentScavengeSiteId"/> and handed to <see cref="GameManager.BeginNewRun"/>,
    /// so it must never be localized or renamed once a save exists that references it.
    ///
    /// <para>Three separate things can keep a site off the roster, and they are tracked separately because
    /// they have different fixes:</para>
    /// <list type="bullet">
    ///   <item><see cref="IsBuilt"/> — the level exists as a scene. False means the site is vapour; offering
    ///   it would load nothing or load the wrong map.</item>
    ///   <item><see cref="RequiredUnlockId"/> — a <see cref="MetaUnlockCatalog"/> entry the player has to buy.
    ///   Null for sites that are always on the roster.</item>
    ///   <item><see cref="UnavailableReason"/> — the in-fiction line shown on a greyed card, so a locked site
    ///   reads as administration rather than as a missing feature.</item>
    /// </list>
    /// </summary>
    public class ScavengeSite
    {
        public string Id;
        public string DisplayName;

        /// <summary>Canonical oblast region id — one of <see cref="OblastRegions.All"/>, never a display string.</summary>
        public string RegionId;

        public string Summary;

        /// <summary>True when a scene for this site actually ships. See the class remarks.</summary>
        public bool IsBuilt;

        /// <summary>Meta-unlock this site is gated behind, or null when it is always on the roster.</summary>
        public string RequiredUnlockId;

        public string UnavailableReason;

        /// <summary>Name of the scene this site loads additively. Empty when the site is not built.</summary>
        public string SceneName;

        /// <summary>Human-readable region name for the setup screen.</summary>
        public string RegionDisplayName => OblastRegions.DisplayNameOf(RegionId);

        // ─── Hazard profile (bible §5) ──────────────────────────────────────────────────────
        // Which mutants a site fields, declared here rather than in each scene generator.
        //
        // The bible ties both mutants to geography: the Drowned Census-Taker was a Scale Society clerk
        // drowned in the Reservoir and haunts the Reservoir and the Census District specifically, and
        // the Editor is rare and late-campaign everywhere. Putting that on the site means a new level
        // inherits a coherent threat profile by declaring which region it is in, instead of a scene
        // generator re-deciding it and drifting from the last one that did.
        //
        // It also keeps the threat readable to the setup screen, which is where the player is choosing
        // between sites and is the only place the trade-off can actually be presented.

        /// <summary>
        /// Drowned Census-Takers (MTN-Β-04/DC) that spawn here. Non-zero only in the regions the bible
        /// puts them in — a Census-Taker in the Grain Belt is the wrong side of the oblast.
        /// </summary>
        public int CensusTakerCount;

        /// <summary>Probability an Editor (MTN-Ψ-09/ED) appears during one Blowout at this site.</summary>
        public float EditorSpawnChance;

        /// <summary>
        /// One-line threat summary for the setup screen. Written in the register a duty officer would
        /// use, because the player is reading a posting notice, not a bestiary entry.
        /// </summary>
        public string ThreatSummary;
    }

    /// <summary>
    /// The list of scavenge sites the player can register for. A flat static table on purpose: sites are
    /// referenced by id from RunData and from the scene loader, and there are three of them. When sites
    /// grow their own content (per-site loot tables, hazard profiles) this becomes a ScriptableObject under
    /// Assets/Data/Definitions/Sites/ and everything reading <see cref="All"/> keeps working.
    ///
    /// <para>A site's <see cref="ScavengeSite.RegionId"/> is also the run's geographic context: it is passed
    /// to the event engine on every bunker day as the oblast-region axis, so which site you registered for
    /// keeps tinting the narrative long after the Blowout ends. That gate fails open (see
    /// <see cref="OblastRegions"/>), so a site whose region matches nothing in the corpus costs the player
    /// flavour, never events.</para>
    /// </summary>
    public static class ScavengeSiteCatalog
    {
        /// <summary>Id of the site used when a run is started without an explicit choice (debug launchers).</summary>
        public const string DefaultSiteId = "site_grain_depot";

        private static readonly List<ScavengeSite> _sites = new List<ScavengeSite>
        {
            new ScavengeSite
            {
                Id = "site_grain_depot",
                DisplayName = "Collapsed Grain Depot",
                RegionId = OblastRegions.GrainBelt,
                Summary = "Silo base, warehouse floor, rail siding. Intake pit reads hot. Stairwell to the bunker is clear as of last survey.",
                IsBuilt = true,
                RequiredUnlockId = null,
                SceneName = "Scavenge",
                UnavailableReason = string.Empty,

                // Wrong side of the oblast for the Census-Taker; the Editor turns up anywhere, rarely.
                CensusTakerCount = 0,
                EditorSpawnChance = BalanceConstants.EDITOR_BASE_SPAWN_CHANCE * 0.67f,
                ThreatSummary = "No standing hazard reports. Occasional unverified sightings."
            },
            new ScavengeSite
            {
                Id = "site_census_office",
                DisplayName = "Flooded Census Office",
                RegionId = OblastRegions.CensusDistrict,
                Summary = "District records annex. Standing water on the lower floor. Filing remains substantially intact. Interview room sealed pending review.",

                // IsBuilt was true with SceneName "CensusOffice" and no such scene in the project. That
                // is not a harmless placeholder: ScavengePhase3DState only falls back to the depot when
                // SceneName is *empty*, so a non-empty name for a missing scene passed the guard and
                // loaded nothing — sixty seconds of empty room with no error. Marked honestly until the
                // level exists; flipping IsBuilt is the last step of building it, not the first.
                IsBuilt = false,
                RequiredUnlockId = MetaUnlockCatalog.SecondSiteUnlockId,
                SceneName = string.Empty,
                UnavailableReason = "Entry permit not on file. Apply at the supply office.",

                CensusTakerCount = 1,
                EditorSpawnChance = BalanceConstants.EDITOR_BASE_SPAWN_CHANCE,
                ThreatSummary = "One registrar unaccounted for since the evacuation. Do not stand still."
            },
            new ScavengeSite
            {
                Id = "site_reservoir",
                DisplayName = "Abandoned Reservoir",
                RegionId = OblastRegions.Reservoir,
                Summary = "Municipal basin and pump house. Catwalk over open water. Control room dry, reachable only through the flooded tunnels.",
                IsBuilt = false,
                RequiredUnlockId = null,
                SceneName = string.Empty,
                UnavailableReason = "Survey incomplete. The basin has not been sounded since the Blowout.",

                // The bible's hunting ground: this is where the Census-Takers drowned. Highest threat
                // in the catalogue, and the reason the site is worth its risk.
                CensusTakerCount = 2,
                EditorSpawnChance = BalanceConstants.EDITOR_BASE_SPAWN_CHANCE * 1.33f,
                ThreatSummary = "Multiple registrars in the water. Standing orders: keep moving, keep to the catwalk."
            },
            new ScavengeSite
            {
                Id = "site_rail_terminal",
                DisplayName = "Abandoned Rail Terminal",
                RegionId = OblastRegions.OuterCordon,
                Summary = "Freight terminus. Rolling stock left loaded. Interdiction line runs through the yard.",
                IsBuilt = false,
                RequiredUnlockId = null,
                SceneName = string.Empty,
                UnavailableReason = "Interdiction order still in force. No entry permit on record.",

                CensusTakerCount = 0,
                EditorSpawnChance = BalanceConstants.EDITOR_BASE_SPAWN_CHANCE * 0.67f,
                ThreatSummary = "Cordon patrols reported. Hazard profile not otherwise established."
            },
        };

        /// <summary>Every site, available or not. The setup screen shows all of them and greys the rest.</summary>
        public static IReadOnlyList<ScavengeSite> All => _sites;

        /// <summary>Looks up a site by its stable id. Returns null when unknown.</summary>
        public static ScavengeSite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var site in _sites)
                if (site.Id == id) return site;
            return null;
        }

        /// <summary>
        /// Whether the player may register for this site right now: the level has to exist AND any gating
        /// unlock has to have been purchased. Passing a null profile treats every unlock as unpurchased,
        /// which is the safe reading — it greys a card rather than loading a scene the run cannot use.
        /// </summary>
        public static bool IsAvailableTo(ScavengeSite site, MetaProgressData meta)
        {
            if (site == null || !site.IsBuilt) return false;
            if (string.IsNullOrEmpty(site.RequiredUnlockId)) return true;
            return meta != null && meta.IsPurchased(site.RequiredUnlockId);
        }

        /// <summary>
        /// Why a site cannot be entered, phrased for the player. Distinguishes "not surveyed" from "no permit"
        /// so a locked-but-built site does not read as an unfinished one.
        /// </summary>
        public static string UnavailableReasonFor(ScavengeSite site, MetaProgressData meta)
        {
            if (site == null) return "No such site on record.";
            if (!site.IsBuilt) return site.UnavailableReason;
            if (!string.IsNullOrEmpty(site.RequiredUnlockId) && (meta == null || !meta.IsPurchased(site.RequiredUnlockId)))
                return site.UnavailableReason;
            return string.Empty;
        }

        /// <summary>The display name for an id, falling back to the id itself so a summary never renders blank.</summary>
        public static string DisplayNameOf(string id)
        {
            var site = Get(id);
            return site != null ? site.DisplayName : (string.IsNullOrEmpty(id) ? "Unrecorded Site" : id);
        }

        /// <summary>
        /// The oblast region a run is operating in, as a single-entry list ready to hand to the event engine.
        /// Empty when the site is unknown or carries no region, which the fail-open region gate reads as
        /// "no geographic constraint" rather than as "no events".
        /// </summary>
        public static IReadOnlyList<string> RegionContextFor(string siteId)
        {
            var site = Get(siteId);
            if (site == null || string.IsNullOrEmpty(site.RegionId)) return System.Array.Empty<string>();
            return new[] { site.RegionId };
        }
    }
}
