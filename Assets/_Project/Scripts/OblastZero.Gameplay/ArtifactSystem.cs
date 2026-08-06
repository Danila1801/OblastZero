// Assets/_Project/Scripts/OblastZero.Gameplay/ArtifactSystem.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The four bible artifacts and what using one does (BESTIARY.md "ARTIFACTS REFERENCE").
    /// Single owner of the artifact fields on <see cref="RunData"/>, in line with the standing rule
    /// that nothing outside a manager writes run state.
    ///
    /// <para><b>Three of the four are armed rather than applied.</b> Margin Note, Stamped Tongue and
    /// Notarized Heart all modify something that has not happened yet, so using one files an
    /// intention and the effect is collected later by whichever system is actually doing the work —
    /// <c>EventEngine</c> for the first two, <c>CrewManager</c> for the third. Only Final Draft
    /// resolves immediately, because rewriting a stat has nothing to wait for.</para>
    ///
    /// <para><b>Why the Margin Note re-rolls forward instead of undoing a past outcome.</b> The bible
    /// describes it as re-rolling an expedition event outcome, and the obvious reading is "reverse the
    /// last result and roll again". That is not implementable honestly: an applied outcome has already
    /// killed crew, clamped reputation against its bounds, consumed items that other effects have
    /// since stacked into, and advanced the RNG stream. Several of those are lossy, so "undo" would
    /// mean reconstructing an approximation of a previous state and calling it the real one — a save
    /// system that quietly lies. Arming the re-roll gives the player exactly the thing the artifact is
    /// for (one bad outcome they get another shot at) with none of that, and it fits the register
    /// better besides: a note in the margin is filed before the decision is reviewed, not after.</para>
    ///
    /// <para><b>Stamped Tongue is filed in advance for the same reason,</b> and gains from it. The
    /// bible calls it an "official override of any Scale Society event", and an override that must be
    /// on file before the matter comes up is more in the Oblast's voice than a button that appears
    /// mid-conversation — as well as keeping the event modal free of artifact-specific wiring.</para>
    /// </summary>
    public class ArtifactSystem
    {
        /// <summary>Why a use was refused. The UI shows these; each is a distinct player-facing state.</summary>
        public enum UseResult
        {
            Success,
            NoRun,
            NotHeld,
            OnCooldown,
            AlreadyArmed,
            InvalidTarget,
            Failed
        }

        private readonly GameDatabase _db;
        private readonly InventoryManager _inventory;
        private readonly CrewManager _crew;
        private RunData _run;

        public ArtifactSystem(GameDatabase db, InventoryManager inventory, CrewManager crew)
        {
            _db = db;
            _inventory = inventory;
            _crew = crew;
        }

        /// <summary>Binds to a run. Called by <c>GameManager</c> alongside the other managers.</summary>
        public void Bind(RunData run)
        {
            _run = run;

            // The id table is validated against the live database once per run rather than trusted.
            // A misspelled constant produces an artifact that can never be found and a use screen
            // that is always empty, with nothing logged anywhere — the exact silent-nothing failure
            // this codebase keeps paying for elsewhere.
            if (_db == null) return;

            var missing = new List<string>();
            for (int i = 0; i < ArtifactIds.All.Count; i++)
            {
                ItemData ignored;
                if (!_db.TryGetItem(ArtifactIds.All[i], out ignored)) missing.Add(ArtifactIds.All[i]);
            }

            if (missing.Count > 0)
                Debug.LogError("[ArtifactSystem] These artifact ids are not in the database: " +
                               string.Join(", ", missing) + ". They can never be found or used.");
            else
                Debug.Log($"[ArtifactSystem] Bound to run '{run?.runId}'. " +
                          $"All {ArtifactIds.All.Count} artifact ids resolve.");
        }

        // ── Queries ──────────────────────────────────────────────────────────

        /// <summary>Artifact ids currently in the bunker inventory, in bible table order.</summary>
        public List<string> HeldArtifacts()
        {
            var held = new List<string>();
            if (_inventory == null) return held;

            var bunker = _inventory.Get(InventoryChannel.Bunker);
            for (int i = 0; i < ArtifactIds.All.Count; i++)
            {
                string id = ArtifactIds.All[i];
                for (int k = 0; k < bunker.Count; k++)
                {
                    if (bunker[k].itemDataId != id || bunker[k].quantity <= 0) continue;
                    held.Add(id);
                    break;
                }
            }
            return held;
        }

        /// <summary>How many of an artifact the bunker holds.</summary>
        public int CountOf(string artifactId)
        {
            if (_inventory == null) return 0;

            int n = 0;
            var bunker = _inventory.Get(InventoryChannel.Bunker);
            for (int i = 0; i < bunker.Count; i++)
                if (bunker[i].itemDataId == artifactId) n += Mathf.Max(0, bunker[i].quantity);
            return n;
        }

        /// <summary>Days until a Margin Note may be filed again. 0 when one may be filed now.</summary>
        public int MarginNoteDaysRemaining()
        {
            if (_run == null || _run.marginNoteLastUsedDay <= 0) return 0;
            int elapsed = _run.currentDay - _run.marginNoteLastUsedDay;
            return Mathf.Max(0, BalanceConstants.MARGIN_NOTE_COOLDOWN_DAYS - elapsed);
        }

        /// <summary>Whether an artifact can be used right now, and why not when it cannot.</summary>
        public UseResult CanUse(string artifactId, string targetCrewInstanceId = null)
        {
            if (_run == null) return UseResult.NoRun;
            if (CountOf(artifactId) <= 0) return UseResult.NotHeld;

            switch (artifactId)
            {
                case ArtifactIds.MarginNote:
                    if (_run.marginNoteArmed) return UseResult.AlreadyArmed;
                    return MarginNoteDaysRemaining() > 0 ? UseResult.OnCooldown : UseResult.Success;

                case ArtifactIds.StampedTongue:
                    return _run.stampedTongueArmed ? UseResult.AlreadyArmed : UseResult.Success;

                case ArtifactIds.NotarizedHeart:
                case ArtifactIds.FinalDraft:
                    return LivingTarget(targetCrewInstanceId) != null
                        ? UseResult.Success
                        : UseResult.InvalidTarget;

                default:
                    return UseResult.Failed;
            }
        }

        // ── Uses ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Files a Margin Note against the next event resolution, which will draw twice and keep the
        /// better result. Not consumed — the bible allows one use per in-game week, indefinitely.
        /// </summary>
        public UseResult UseMarginNote()
        {
            var check = CanUse(ArtifactIds.MarginNote);
            if (check != UseResult.Success) return check;

            _run.marginNoteArmed = true;
            _run.marginNoteLastUsedDay = Mathf.Max(1, _run.currentDay);

            Report(ArtifactIds.MarginNote, null,
                   "Filed against the next matter. It will be reviewed twice and the better reading kept.",
                   consumed: false);
            return UseResult.Success;
        }

        /// <summary>
        /// Puts an official override on file. The next Scale Society event resolves as a success
        /// regardless of the roll. Consumed when the override is actually spent, not when it is filed —
        /// so a player who arms it and never meets the Society keeps the artifact.
        /// </summary>
        public UseResult UseStampedTongue()
        {
            var check = CanUse(ArtifactIds.StampedTongue);
            if (check != UseResult.Success) return check;

            _run.stampedTongueArmed = true;

            Report(ArtifactIds.StampedTongue, null,
                   "Override on file. The next matter before the Scale Society is decided in your favour.",
                   consumed: false);
            return UseResult.Success;
        }

        /// <summary>
        /// Assigns the Notarized Heart to a crew member, halving their radiation accumulation. Moving
        /// it to someone else is free and immediate — it is worn, not spent.
        /// </summary>
        public UseResult UseNotarizedHeart(string targetCrewInstanceId)
        {
            var check = CanUse(ArtifactIds.NotarizedHeart, targetCrewInstanceId);
            if (check != UseResult.Success) return check;

            var target = LivingTarget(targetCrewInstanceId);
            string previous = _run.notarizedHeartBearerId;
            _run.notarizedHeartBearerId = target.instanceId;

            string note = previous != null && previous != target.instanceId
                ? $"Transferred from '{previous}'."
                : string.Empty;

            Report(ArtifactIds.NotarizedHeart, target.instanceId,
                   $"Worn by '{target.instanceId}'. Personal accumulation at " +
                   $"{BalanceConstants.NOTARIZED_HEART_RADIATION_MULTIPLIER:P0} of standard. {note}".TrimEnd(),
                   consumed: false);
            return UseResult.Success;
        }

        /// <summary>
        /// Rewrites one stat of one crew member and destroys the artifact. The value is clamped to
        /// [<see cref="BalanceConstants.FINAL_DRAFT_MIN_STAT_VALUE"/>,
        /// <see cref="BalanceConstants.FINAL_DRAFT_MAX_STAT_VALUE"/>] — the floor is not squeamishness,
        /// it is that rewriting health to zero is a rewrite into a corpse, and the bible's Final Draft
        /// edits a person rather than deleting one.
        /// </summary>
        public UseResult UseFinalDraft(string targetCrewInstanceId, CrewStat stat, int newValue)
        {
            var check = CanUse(ArtifactIds.FinalDraft, targetCrewInstanceId);
            if (check != UseResult.Success) return check;

            var target = LivingTarget(targetCrewInstanceId);
            int clamped = Mathf.Clamp(newValue,
                                      BalanceConstants.FINAL_DRAFT_MIN_STAT_VALUE,
                                      BalanceConstants.FINAL_DRAFT_MAX_STAT_VALUE);

            int before = ReadStat(target, stat);

            // Routed through CrewManager as a delta rather than written here. CrewManager is the sole
            // writer of crew stats, it clamps against the member's own maximum (which meta-unlocks
            // raise), and it raises the change events the HUD listens to. Writing the field directly
            // would bypass all three.
            int delta = clamped - before;
            ApplyStatDelta(target.instanceId, stat, delta);

            int after = ReadStat(target, stat);

            if (!_inventory.RemoveItem(InventoryChannel.Bunker, ArtifactIds.FinalDraft, 1))
            {
                // The stat is already rewritten at this point. Failing to destroy the artifact would
                // hand the player an infinite rewrite, so this is loud rather than ignored.
                Debug.LogError("[ArtifactSystem] Final Draft applied but could not be removed from the " +
                               "bunker inventory. The artifact should have been consumed.");
            }

            Report(ArtifactIds.FinalDraft, target.instanceId,
                   $"'{target.instanceId}' {StatName(stat)} rewritten {before} -> {after}. " +
                   "The sheet is consumed.",
                   consumed: true);
            return UseResult.Success;
        }

        // ── Collection points, called by the systems that do the work ────────

        /// <summary>
        /// Called by <c>EventEngine</c> immediately before a resolution rolls. Returns true and clears
        /// the flag when a Margin Note is on file, meaning the engine should draw twice.
        /// </summary>
        public bool ConsumeMarginNoteReroll()
        {
            if (_run == null || !_run.marginNoteArmed) return false;
            _run.marginNoteArmed = false;
            Debug.Log("[ArtifactSystem] Margin Note spent on this resolution — drawing twice.");
            return true;
        }

        /// <summary>
        /// Called by <c>EventEngine</c> when resolving an event whose reputation faction is the Scale
        /// Society. Returns true, clears the flag and destroys the artifact when an override is on file.
        /// </summary>
        public bool ConsumeStampedTongueOverride()
        {
            if (_run == null || !_run.stampedTongueArmed) return false;

            _run.stampedTongueArmed = false;
            if (_inventory != null)
                _inventory.RemoveItem(InventoryChannel.Bunker, ArtifactIds.StampedTongue, 1);

            Debug.Log("[ArtifactSystem] Stamped Tongue spent — the matter is decided in the player's " +
                      "favour and the artifact is consumed.");

            EventBus.Raise(new ArtifactUsedEvent
            {
                ItemDataId = ArtifactIds.StampedTongue,
                TargetCrewInstanceId = null,
                EffectSummary = "Override exercised. The Scale Society finds in your favour.",
                Consumed = true
            });
            return true;
        }

        /// <summary>
        /// Radiation multiplier for a crew member — 0.5 for the Notarized Heart's bearer, 1 otherwise.
        /// Called by <c>CrewManager.ApplyRadiation</c>, which is the single place radiation enters a
        /// crew member, so every source is covered without any of them knowing the artifact exists.
        /// </summary>
        public float RadiationMultiplierFor(string crewInstanceId)
        {
            if (_run == null || string.IsNullOrEmpty(_run.notarizedHeartBearerId)) return 1f;
            return _run.notarizedHeartBearerId == crewInstanceId
                ? BalanceConstants.NOTARIZED_HEART_RADIATION_MULTIPLIER
                : 1f;
        }

        // ── Internals ────────────────────────────────────────────────────────

        private CrewInstance LivingTarget(string instanceId)
        {
            if (_crew == null || string.IsNullOrEmpty(instanceId)) return null;
            var member = _crew.GetMember(instanceId);
            return member != null && member.isAlive ? member : null;
        }

        private static int ReadStat(CrewInstance c, CrewStat stat)
        {
            switch (stat)
            {
                case CrewStat.Health: return c.currentHealth;
                case CrewStat.Sanity: return c.currentSanity;
                case CrewStat.Fatigue: return c.currentFatigue;
                case CrewStat.Radiation: return c.currentRadiation;
                default: return 0;
            }
        }

        private void ApplyStatDelta(string instanceId, CrewStat stat, int delta)
        {
            if (delta == 0) return;
            switch (stat)
            {
                case CrewStat.Health: _crew.ApplyHealthDelta(instanceId, delta); break;
                case CrewStat.Sanity: _crew.ApplySanityDelta(instanceId, delta); break;
                case CrewStat.Fatigue: _crew.ApplyFatigueDelta(instanceId, delta); break;
                case CrewStat.Radiation: _crew.ApplyRadiation(instanceId, delta); break;
            }
        }

        private static string StatName(CrewStat stat)
        {
            switch (stat)
            {
                case CrewStat.Health: return "health";
                case CrewStat.Sanity: return "sanity";
                case CrewStat.Fatigue: return "fatigue";
                case CrewStat.Radiation: return "radiation";
                default: return "record";
            }
        }

        private static void Report(string artifactId, string targetId, string summary, bool consumed)
        {
            Debug.Log($"[ArtifactSystem] {artifactId}: {summary}");
            EventBus.Raise(new ArtifactUsedEvent
            {
                ItemDataId = artifactId,
                TargetCrewInstanceId = targetId,
                EffectSummary = summary,
                Consumed = consumed
            });
        }
    }
}
