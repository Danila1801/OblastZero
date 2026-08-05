// Assets/_Project/Scripts/Core/RunDataMigrator.cs
using System.Collections.Generic;
using UnityEngine;

namespace OblastZero.Core
{
    /// <summary>
    /// Brings an expedition save written by an older build up to the current <see cref="RunData"/> shape.
    ///
    /// <para>Runs on every load, in <c>SaveService.LoadExpedition</c>, before the run reaches
    /// <c>GameManager.RebindManagersToCurrentRun</c>. Migration is idempotent: a save already at
    /// <see cref="CurrentVersion"/> passes through untouched apart from the null-collection repair, which is
    /// unconditional by design (see below).</para>
    ///
    /// <para><b>Why a version number at all</b>, when the one field added so far (<c>pendingEventId</c>)
    /// deserializes to a harmless null on an old save. Two reasons. A silent tolerant load cannot tell the
    /// difference between "this save predates the field" and "this save has no pending event", so the first
    /// migration that needs to *derive* a value rather than accept a default has nowhere to stand. And a
    /// mismatched save is the kind of bug that surfaces as inexplicable mid-run state rather than as an
    /// error, so it is worth one log line at the moment it is detected.</para>
    /// </summary>
    public static class RunDataMigrator
    {
        /// <summary>
        /// The revision this build writes. Bump when a change to <see cref="RunData"/> needs old saves
        /// adjusted rather than merely tolerated, and add the matching step to <see cref="Migrate"/>.
        ///
        /// <para>1 = pre-<c>pendingEventId</c>: an event held open across a save was lost on reload and
        /// re-rolled. 2 = <c>pendingEventId</c> serialized.</para>
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>Version stamped on saves written before <c>saveFormatVersion</c> existed (absent int → 0).</summary>
        public const int LegacyUnversioned = 0;

        /// <summary>
        /// Migrates in place and returns the same instance, or null if given null (so callers can pass a
        /// failed load straight through). Reports what it did, once, per load.
        /// </summary>
        public static RunData Migrate(RunData run)
        {
            if (run == null) return null;

            int from = run.saveFormatVersion;

            // Repair null collections regardless of version. Field initializers do run on deserialize — but
            // Newtonsoft overwrites the initialized list with null when the JSON explicitly holds null for
            // that key, and the serializer settings use NullValueHandling.Include, so nulls do get written.
            // Every consumer of these lists iterates them without a null check, on the reasonable assumption
            // that a field initialized at construction cannot be null.
            int repaired = RepairCollections(run);

            if (from > CurrentVersion)
            {
                // Forward-dated save: a newer build wrote it. Nothing sensible to do but load it as-is and
                // say so — silently downgrading the stamp would hide the real cause of any weirdness after.
                Debug.LogWarning($"[RunDataMigrator] Expedition save is version {from}, newer than this " +
                                 $"build's {CurrentVersion}. Loading as-is; unknown fields are ignored and " +
                                 "will be dropped on the next save.");
                return run;
            }

            if (from == CurrentVersion)
            {
                if (repaired > 0)
                    Debug.LogWarning($"[RunDataMigrator] Save at current version {CurrentVersion} but " +
                                     $"{repaired} collection(s) were null — repaired to empty.");
                return run;
            }

            // ---- Step: anything at or below 1 predates pendingEventId ----
            if (from <= 1)
            {
                // The open event itself is unrecoverable — the old format never stored it. What this step
                // guarantees is that the field holds null rather than uninitialized garbage, so the restore
                // path reads "nothing pending" and the next day advance draws normally, instead of trying to
                // rehydrate an id that was never written.
                run.pendingEventId = null;

                Debug.Log($"[RunDataMigrator] Migrated expedition save " +
                          $"{(from == LegacyUnversioned ? "(unversioned)" : $"v{from}")} → v{CurrentVersion}: " +
                          "no pending event recorded, so day " + run.currentDay + " resumes with none. " +
                          "An event that was open when this save was written is lost.");
            }

            run.saveFormatVersion = CurrentVersion;

            if (repaired > 0)
                Debug.LogWarning($"[RunDataMigrator] Repaired {repaired} null collection(s) during migration.");

            return run;
        }

        /// <summary>
        /// Replaces any null list on the run with an empty one. Returns how many were repaired, so the caller
        /// can report a save that arrived structurally damaged rather than merely old.
        /// </summary>
        private static int RepairCollections(RunData run)
        {
            int repaired = 0;

            if (run.ScavengedInventory == null) { run.ScavengedInventory = new List<ItemInstance>(); repaired++; }
            if (run.RescuedCrew == null) { run.RescuedCrew = new List<CrewInstance>(); repaired++; }
            if (run.BunkerInventory == null) { run.BunkerInventory = new List<ItemInstance>(); repaired++; }
            if (run.ActiveCrew == null) { run.ActiveCrew = new List<CrewInstance>(); repaired++; }
            if (run.ExpeditionsInFlight == null) { run.ExpeditionsInFlight = new List<ActiveExpedition>(); repaired++; }
            if (run.CompletedEventIds == null) { run.CompletedEventIds = new List<string>(); repaired++; }
            if (run.QueuedEventIds == null) { run.QueuedEventIds = new List<string>(); repaired++; }

            foreach (var member in run.ActiveCrew)
                if (member != null && member.traitIds == null) { member.traitIds = new List<string>(); repaired++; }

            foreach (var member in run.RescuedCrew)
                if (member != null && member.traitIds == null) { member.traitIds = new List<string>(); repaired++; }

            foreach (var expedition in run.ExpeditionsInFlight)
            {
                if (expedition == null) continue;
                if (expedition.loadoutItemInstanceIds == null)
                {
                    expedition.loadoutItemInstanceIds = new List<string>();
                    repaired++;
                }
                if (expedition.resolvedEventIds == null)
                {
                    expedition.resolvedEventIds = new List<string>();
                    repaired++;
                }
            }

            return repaired;
        }
    }
}
