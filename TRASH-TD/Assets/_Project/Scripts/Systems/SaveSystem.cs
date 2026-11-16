using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Systems
{
    [Serializable]
    public class OperatorSaveEntry
    {
        public string operatorName;
        public int highestRarity;
        public int copyCount;
    }

    [Serializable]
    public class StageProgressEntry
    {
        public string stageId;
        public int difficulty; // 0=Easy, 1=Normal, 2=Hard
        public int starsEarned; // 1-3
        public bool isCleared;
    }

    /// <summary>An enemy the player has met (Reclaimers' Intel), and the stages where it was met.</summary>
    [Serializable]
    public class EnemyIntelEntry
    {
        public string enemyName; // the EnemyData asset name
        public List<string> sightedStageIds = new List<string>();
    }

    [Serializable]
    public class SaveData
    {
        // TODO: economy beyond card-draft — awaiting design input from Raim (currencies, shop, etc.)
        // TODO: persistence model — awaiting design input (whether progression persists between runs or resets per run)
        public int version = SaveSystem.CurrentVersion;
        /// <summary>The old PlayerPrefs progress has been moved into this file (see SaveSystem.LegacyPrefs).</summary>
        public bool legacyPrefsImported;
        public bool tutorialDone;
        public int currency = 0;
        public List<OperatorSaveEntry> roster = new List<OperatorSaveEntry>();
        public List<StageProgressEntry> stageProgress = new List<StageProgressEntry>();
        public List<EnemyIntelEntry> enemyIntel = new List<EnemyIntelEntry>();

        /// <summary>Best stars on this stage and difficulty (0 = never cleared).</summary>
        public int GetStars(string stageId, int difficulty)
        {
            StageProgressEntry entry = FindStage(stageId, difficulty);
            return entry != null ? entry.starsEarned : 0;
        }

        public void SetStars(string stageId, int difficulty, int stars)
        {
            StageProgressEntry entry = FindStage(stageId, difficulty);
            if (entry == null)
            {
                entry = new StageProgressEntry { stageId = stageId, difficulty = difficulty };
                stageProgress.Add(entry);
            }
            entry.starsEarned = stars;
            entry.isCleared = stars > 0;
        }

        public EnemyIntelEntry FindIntel(string enemyName)
        {
            for (int i = 0; i < enemyIntel.Count; i++)
            {
                if (enemyIntel[i] != null && enemyIntel[i].enemyName == enemyName) return enemyIntel[i];
            }
            return null;
        }

        private StageProgressEntry FindStage(string stageId, int difficulty)
        {
            for (int i = 0; i < stageProgress.Count; i++)
            {
                StageProgressEntry entry = stageProgress[i];
                if (entry != null && entry.stageId == stageId && entry.difficulty == difficulty) return entry;
            }
            return null;
        }

        /// <summary>A file written by an older build (or by hand) may lack a list; JsonUtility leaves it null.</summary>
        internal void Normalize()
        {
            if (roster == null) roster = new List<OperatorSaveEntry>();
            if (stageProgress == null) stageProgress = new List<StageProgressEntry>();
            if (enemyIntel == null) enemyIntel = new List<EnemyIntelEntry>();
            for (int i = 0; i < enemyIntel.Count; i++)
            {
                if (enemyIntel[i] != null && enemyIntel[i].sightedStageIds == null) enemyIntel[i].sightedStageIds = new List<string>();
            }
        }
    }

    /// <summary>
    /// The player's progress, saved as one JSON file (GDD 1.9): stage clears and stars, the tutorial flag and the Intel
    /// enemy files. StageProgress, EnemyIntelProgress and TutorialDirector read and write it through <see cref="Data"/>.
    /// Settings (audio, graphics, controls, gameplay options) stay in PlayerPrefs: they belong to the machine, not the save.
    /// - The file lives in Application.persistentDataPath (on Windows: %USERPROFILE%\AppData\LocalLow\&lt;company&gt;\&lt;product&gt;).
    ///   TRASH TD → Open Save Folder shows it in the editor; Settings → Gameplay → SAVE DATA resets it in the game.
    /// - Writes go to a temporary file first and replace the save, which keeps the previous one as a ".bak" copy, so a
    ///   crash mid-write can't leave a half-written save. A save that can't be read is kept as ".corrupt" and the backup
    ///   is tried before starting over.
    /// - Progress used to live in PlayerPrefs (the registry on Windows). The first time the file is read, those values
    ///   are moved into it and removed from PlayerPrefs, so nothing is lost and nothing stays in the registry.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 1;

        private const string SaveFileName = "trash_td_save.json";

        private static string pathOverride;
        private static bool legacyImportForOverride;
        private static SaveData data;

        // The editor can keep statics between Play sessions (domain reload off): read the file again each session
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            data = null;
            pathOverride = null;
            legacyImportForOverride = false;
        }

        public static string SaveFilePath => pathOverride ?? Path.Combine(Application.persistentDataPath, SaveFileName);
        public static string SaveFolder => Path.GetDirectoryName(SaveFilePath);
        private static string BackupPath => SaveFilePath + ".bak";
        private static string CorruptPath => SaveFilePath + ".corrupt";

        /// <summary>
        /// Uses another file (tests); null goes back to the real save. The next access reads that file. The old PlayerPrefs
        /// progress is only touched on the real save, unless <paramref name="importLegacyPrefs"/> asks for it (the migration test).
        /// </summary>
        public static void UseSavePath(string path, bool importLegacyPrefs = false)
        {
            pathOverride = path;
            legacyImportForOverride = importLegacyPrefs;
            data = null;
        }

        private static bool LegacyPrefsEnabled => pathOverride == null || legacyImportForOverride;

        /// <summary>The loaded save (read from disk on first use). Change it, then call <see cref="Save"/>.</summary>
        public static SaveData Data
        {
            get
            {
                if (data == null) data = Load();
                return data;
            }
        }

        /// <summary>Writes <see cref="Data"/> to disk. False (and an error in the console) if the write failed.</summary>
        public static bool Save()
        {
            return TryWrite(Data);
        }

        /// <summary>
        /// Erases the save: the file, its backup, and any progress still in PlayerPrefs. The next access starts a new
        /// save. Callers that keep their own copy of progress (Intel's session cache, the map tutorial) use
        /// PlayerProgress.ResetAll, which also clears those.
        /// </summary>
        public static void DeleteSave()
        {
            try
            {
                DeleteFile(SaveFilePath);
                DeleteFile(BackupPath);
                DeleteFile(SaveFilePath + ".tmp");
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveSystem: Failed to delete the save: {ex.Message}");
            }

            if (LegacyPrefsEnabled) LegacyPrefs.Clear();
            data = new SaveData { legacyPrefsImported = true };
        }

        private static void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private static SaveData Load()
        {
            SaveData loaded = TryRead(SaveFilePath);
            if (loaded == null && File.Exists(SaveFilePath))
            {
                // The save is there but unreadable: keep it for inspection, then try the last good copy
                try { File.Copy(SaveFilePath, CorruptPath, true); }
                catch (Exception ex) { Debug.LogError($"SaveSystem: Could not keep the unreadable save: {ex.Message}"); }

                loaded = TryRead(BackupPath);
                if (loaded != null) Debug.LogWarning("SaveSystem: The save was unreadable; restored the backup copy.");
                else Debug.LogError("SaveSystem: The save and its backup are unreadable; starting a new save (the old file is kept as .corrupt).");
            }

            if (loaded == null) loaded = new SaveData();
            loaded.Normalize();

            if (!loaded.legacyPrefsImported && LegacyPrefsEnabled)
            {
                // Only forget the PlayerPrefs copy once the file is safely on disk
                LegacyPrefs.ImportInto(loaded);
                loaded.legacyPrefsImported = true;
                if (TryWrite(loaded)) LegacyPrefs.Clear();
            }

            return loaded;
        }

        private static SaveData TryRead(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveSystem: Failed to read {Path.GetFileName(path)}: {ex.Message}");
                return null;
            }
        }

        private static bool TryWrite(SaveData toWrite)
        {
            try
            {
                string path = SaveFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(toWrite, true));
                if (File.Exists(path)) File.Replace(temp, path, BackupPath);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"SaveSystem: Failed to save game data: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// The progress keys older builds kept in PlayerPrefs ("TrashTD.Progress.*", "TrashTD.TutorialDone",
        /// "TrashTD.Intel.*"): moved into the save file once, then deleted.
        /// </summary>
        private static class LegacyPrefs
        {
            private const string ProgressPrefix = "TrashTD.Progress.";
            private const string TutorialDoneKey = "TrashTD.TutorialDone";
            private const string IntelMetPrefix = "TrashTD.Intel.Met.";
            private const string IntelSightedPrefix = "TrashTD.Intel.Sighted.";
            private const string IntelIndexKey = "TrashTD.Intel.Index";
            private const char Separator = '|';
            private const int MaxStageNumber = 99;
            private static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };

            public static void ImportInto(SaveData target)
            {
                for (int number = 1; number <= MaxStageNumber; number++)
                {
                    string stageId = $"STAGE_{number:00}";
                    for (int difficulty = 0; difficulty < DifficultyNames.Length; difficulty++)
                    {
                        string key = ProgressKey(stageId, difficulty);
                        if (!PlayerPrefs.HasKey(key)) continue;

                        int stars = Mathf.Clamp(PlayerPrefs.GetInt(key, 0), 0, 3);
                        if (stars > target.GetStars(stageId, difficulty)) target.SetStars(stageId, difficulty, stars);
                    }
                }

                if (PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1) target.tutorialDone = true;

                foreach (string enemyName in PlayerPrefs.GetString(IntelIndexKey, string.Empty).Split(Separator))
                {
                    if (string.IsNullOrEmpty(enemyName) || PlayerPrefs.GetInt(IntelMetPrefix + enemyName, 0) != 1) continue;

                    EnemyIntelEntry entry = target.FindIntel(enemyName);
                    if (entry == null)
                    {
                        entry = new EnemyIntelEntry { enemyName = enemyName };
                        target.enemyIntel.Add(entry);
                    }

                    foreach (string stageId in PlayerPrefs.GetString(IntelSightedPrefix + enemyName, string.Empty).Split(Separator))
                    {
                        if (!string.IsNullOrEmpty(stageId) && !entry.sightedStageIds.Contains(stageId)) entry.sightedStageIds.Add(stageId);
                    }
                }
            }

            public static void Clear()
            {
                bool any = false;
                for (int number = 1; number <= MaxStageNumber; number++)
                {
                    string stageId = $"STAGE_{number:00}";
                    for (int difficulty = 0; difficulty < DifficultyNames.Length; difficulty++)
                    {
                        string key = ProgressKey(stageId, difficulty);
                        if (!PlayerPrefs.HasKey(key)) continue;
                        PlayerPrefs.DeleteKey(key);
                        any = true;
                    }
                }

                foreach (string enemyName in PlayerPrefs.GetString(IntelIndexKey, string.Empty).Split(Separator))
                {
                    if (string.IsNullOrEmpty(enemyName)) continue;
                    PlayerPrefs.DeleteKey(IntelMetPrefix + enemyName);
                    PlayerPrefs.DeleteKey(IntelSightedPrefix + enemyName);
                    any = true;
                }

                if (PlayerPrefs.HasKey(IntelIndexKey) || PlayerPrefs.HasKey(TutorialDoneKey)) any = true;
                PlayerPrefs.DeleteKey(IntelIndexKey);
                PlayerPrefs.DeleteKey(TutorialDoneKey);
                if (any) PlayerPrefs.Save();
            }

            private static string ProgressKey(string stageId, int difficulty) => ProgressPrefix + stageId + "." + DifficultyNames[difficulty];
        }
    }
}
