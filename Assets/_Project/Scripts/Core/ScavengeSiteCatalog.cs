// Assets/_Project/Scripts/Core/ScavengeSiteCatalog.cs
using System.Collections.Generic;

namespace OblastZero.Core
{
    /// <summary>
    /// One selectable Phase-A location. <see cref="Id"/> is the stable string written to
    /// <see cref="RunData.currentScavengeSiteId"/> and handed to <see cref="GameManager.BeginNewRun"/>,
    /// so it must never be localized or renamed once a save exists that references it.
    ///
    /// <see cref="IsAvailable"/> is false for a site whose level has not been built yet. Offering an
    /// unbuilt site would load the wrong scene, so the setup screen shows it greyed with
    /// <see cref="UnavailableReason"/> instead of silently substituting another map.
    /// </summary>
    public class ScavengeSite
    {
        public string Id;
        public string DisplayName;
        public string RegionTag;
        public string Summary;
        public bool IsAvailable;
        public string UnavailableReason;

        /// <summary>Name of the scene this site loads additively. Empty when the site is unavailable.</summary>
        public string SceneName;
    }

    /// <summary>
    /// The list of scavenge sites the player can register for. A flat static table on purpose: sites are
    /// referenced by id from RunData and from the scene loader, and there are three of them. When sites
    /// grow their own content (per-site loot tables, hazard profiles, region tags that gate events) this
    /// becomes a ScriptableObject under Assets/Data/Definitions/Sites/ and everything reading
    /// <see cref="All"/> keeps working.
    ///
    /// Exactly one site is available today: the Collapsed Grain Depot, which is what
    /// Assets/Scenes/Scavenge.unity actually contains (see tools/generate_scavenge_scene.py).
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
                RegionTag = "Outer Cordon",
                Summary = "Silo base, warehouse floor, rail siding. Intake pit reads hot. Stairwell to the bunker is clear as of last survey.",
                IsAvailable = true,
                SceneName = "Scavenge",
                UnavailableReason = string.Empty
            },
            new ScavengeSite
            {
                Id = "site_census_office",
                DisplayName = "Flooded Census Office",
                RegionTag = "Administrative Ring",
                Summary = "District records annex. Standing water on two floors. Filing remains substantially intact.",
                IsAvailable = false,
                SceneName = string.Empty,
                UnavailableReason = "Survey incomplete. Entry not authorised — resubmit next quarter."
            },
            new ScavengeSite
            {
                Id = "site_rail_terminal",
                DisplayName = "Abandoned Rail Terminal",
                RegionTag = "Northern Interdiction",
                Summary = "Freight terminus. Rolling stock left loaded. Interdiction line runs through the yard.",
                IsAvailable = false,
                SceneName = string.Empty,
                UnavailableReason = "Interdiction order still in force. No entry permit on record."
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

        /// <summary>The display name for an id, falling back to the id itself so a summary never renders blank.</summary>
        public static string DisplayNameOf(string id)
        {
            var site = Get(id);
            return site != null ? site.DisplayName : (string.IsNullOrEmpty(id) ? "Unrecorded Site" : id);
        }
    }
}
