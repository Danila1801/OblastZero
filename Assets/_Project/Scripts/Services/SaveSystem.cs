using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace OblastZero.Core
{
    public interface ISaveService : IService
    {
        void SaveProfile(MetaProgressData profile);
        MetaProgressData LoadProfile();

        void SaveExpedition(RunData run);
        RunData LoadExpedition();

        bool HasExpeditionSave();
        void DeleteExpeditionSave();
    }

    /// <summary>
    /// Dual-channel save service.
    ///   Profile channel: META progression (durable, atomic write with .bak backup, autosaved on every meaningful change)
    ///   Expedition channel: current run (overwritten on each autosave, deleted on run end)
    ///
    /// Both serialize via Newtonsoft JSON. Files live in Application.persistentDataPath/Saves/.
    /// </summary>
    public class SaveService : ISaveService
    {
        private static readonly JsonSerializerSettings kSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            NullValueHandling = NullValueHandling.Include,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        private string SaveFolder => Path.Combine(Application.persistentDataPath, BalanceConstants.SAVE_FOLDER_NAME);
        private string ProfilePath => Path.Combine(SaveFolder, BalanceConstants.PROFILE_SAVE_FILE);
        private string ExpeditionPath => Path.Combine(SaveFolder, BalanceConstants.EXPEDITION_SAVE_FILE);

        public SaveService()
        {
            EnsureSaveFolderExists();
            Debug.Log($"[SaveService] Save folder: {SaveFolder}");
        }

        private void EnsureSaveFolderExists()
        {
            try
            {
                if (!Directory.Exists(SaveFolder))
                {
                    Directory.CreateDirectory(SaveFolder);
                    Debug.Log($"[SaveService] Created save folder at {SaveFolder}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Failed to create save folder: {e.Message}");
            }
        }

        // ─── Profile (META progression) ───────────────────────────────────────
        public void SaveProfile(MetaProgressData profile)
        {
            if (profile == null)
            {
                Debug.LogError("[SaveService] SaveProfile called with null profile.");
                return;
            }

            if (BalanceConstants.VERBOSE_SAVE_LOGGING)
            {
                Debug.Log($"[SaveService] Saving profile (attempts={profile.totalRunsAttempted}, survived={profile.totalRunsSurvived})");
            }

            AtomicWrite(ProfilePath, profile, isProfile: true);
        }

        public MetaProgressData LoadProfile()
        {
            return LoadJson<MetaProgressData>(ProfilePath, hasBackup: true);
        }

        // ─── Expedition (current run) ─────────────────────────────────────────
        public void SaveExpedition(RunData run)
        {
            if (run == null)
            {
                Debug.LogError("[SaveService] SaveExpedition called with null run.");
                return;
            }

            // Stamp the format before writing, so the version on disk always describes the shape on disk.
            // Doing it here rather than in the RunData initializer is what lets a legacy save deserialize to
            // 0 and be recognised as legacy.
            run.saveFormatVersion = RunDataMigrator.CurrentVersion;

            if (BalanceConstants.VERBOSE_SAVE_LOGGING)
            {
                Debug.Log($"[SaveService] Saving expedition (day={run.currentDay}, runId={run.runId}, " +
                          $"v{run.saveFormatVersion}, pendingEvent={run.pendingEventId ?? "none"})");
            }

            AtomicWrite(ExpeditionPath, run, isProfile: false);
        }

        /// <summary>
        /// Loads the current run and brings it up to the current save format. Migration happens here rather
        /// than at the call site because every consumer of a loaded run needs it, and a caller that forgot
        /// would get a run that looks fine and behaves subtly wrong.
        /// </summary>
        public RunData LoadExpedition()
        {
            return RunDataMigrator.Migrate(LoadJson<RunData>(ExpeditionPath, hasBackup: false));
        }

        public bool HasExpeditionSave() => File.Exists(ExpeditionPath);

        public void DeleteExpeditionSave()
        {
            try
            {
                if (File.Exists(ExpeditionPath))
                {
                    File.Delete(ExpeditionPath);
                    Debug.Log("[SaveService] Expedition save deleted.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Failed to delete expedition save: {e.Message}");
            }
        }

        // ─── Internal: atomic write with optional backup ──────────────────────
        private void AtomicWrite<T>(string path, T data, bool isProfile)
        {
            try
            {
                string json = JsonConvert.SerializeObject(data, kSerializerSettings);
                string tempPath = path + ".tmp";

                File.WriteAllText(tempPath, json);

                if (isProfile && File.Exists(path))
                {
                    string backupPath = path + BalanceConstants.SAVE_BACKUP_SUFFIX;
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(path, backupPath);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);

                EventBus.Raise(new SaveCompletedEvent { FilePath = path, IsProfile = isProfile });
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Atomic write failed for {path}: {e.Message}");
                EventBus.Raise(new SaveFailedEvent { FilePath = path, ErrorMessage = e.Message });
            }
        }

        private T LoadJson<T>(string path, bool hasBackup) where T : class
        {
            if (!File.Exists(path))
            {
                if (hasBackup)
                {
                    string backupPath = path + BalanceConstants.SAVE_BACKUP_SUFFIX;
                    if (File.Exists(backupPath))
                    {
                        Debug.LogWarning($"[SaveService] Primary save missing at {path} — falling back to backup.");
                        return LoadJsonFromPath<T>(backupPath);
                    }
                }
                Debug.Log($"[SaveService] No save at {path} — returning null (will create fresh).");
                return null;
            }

            return LoadJsonFromPath<T>(path);
        }

        private T LoadJsonFromPath<T>(string path) where T : class
        {
            try
            {
                string json = File.ReadAllText(path);
                var result = JsonConvert.DeserializeObject<T>(json, kSerializerSettings);
                if (BalanceConstants.VERBOSE_SAVE_LOGGING)
                {
                    Debug.Log($"[SaveService] Loaded {typeof(T).Name} from {path}");
                }
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Failed to deserialize {path}: {e.Message}");
                return null;
            }
        }
    }
}
