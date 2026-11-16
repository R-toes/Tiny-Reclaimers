using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    public class SaveSystemTests
    {
        private static readonly string[] LegacyKeys =
        {
            "TrashTD.Progress.STAGE_02.Hard",
            "TrashTD.TutorialDone",
            "TrashTD.Intel.Index",
            "TrashTD.Intel.Met.LegacyEnemy",
            "TrashTD.Intel.Sighted.LegacyEnemy"
        };

        private string folder;
        private string path;
        private readonly System.Collections.Generic.Dictionary<string, string> stashedKeys = new System.Collections.Generic.Dictionary<string, string>();

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "trashtd_save_test_" + System.Guid.NewGuid().ToString("N"));
            path = Path.Combine(folder, "save.json");
            // These keys are migrated into the save on first read; start from a known state, and give them back after
            // (the editor's own PlayerPrefs may hold real progress that was never migrated)
            stashedKeys.Clear();
            foreach (string key in LegacyKeys)
            {
                if (PlayerPrefs.HasKey(key)) stashedKeys[key] = PlayerPrefs.GetString(key, null) ?? PlayerPrefs.GetInt(key).ToString();
            }
            ClearLegacyKeys();
            SaveSystem.UseSavePath(path);
            EnemyIntelProgress.ResetAll(); // forgets what the Intel session cache saw in earlier tests
        }

        [TearDown]
        public void TearDown()
        {
            ClearLegacyKeys();
            SaveSystem.UseSavePath(null);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);

            foreach (var pair in stashedKeys)
            {
                if (int.TryParse(pair.Value, out int number) && !pair.Key.Contains("Index") && !pair.Key.Contains("Sighted")) PlayerPrefs.SetInt(pair.Key, number);
                else PlayerPrefs.SetString(pair.Key, pair.Value);
            }
            PlayerPrefs.Save();
        }

        private static void ClearLegacyKeys()
        {
            foreach (string key in LegacyKeys) PlayerPrefs.DeleteKey(key);
        }

        /// <summary>Forgets the loaded copy so the next access reads the file, as a new launch does.</summary>
        private void Relaunch() => SaveSystem.UseSavePath(path);

        [Test]
        public void StageClear_IsSavedToTheFile_AndSurvivesARelaunch()
        {
            StageProgress.RecordClear("STAGE_03", StageDifficulty.Normal, 2);
            Assert.IsTrue(File.Exists(path));

            Relaunch();

            Assert.AreEqual(2, StageProgress.GetBestStars("STAGE_03", StageDifficulty.Normal));
            Assert.AreEqual(0, StageProgress.GetBestStars("STAGE_03", StageDifficulty.Hard));
        }

        [Test]
        public void StageClear_KeepsTheBestRating()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 3);
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 1);

            Relaunch();

            Assert.AreEqual(3, StageProgress.GetBestStars("STAGE_01", StageDifficulty.Easy));
        }

        [Test]
        public void TheSandboxAndTutorial_AreNotTracked()
        {
            StageProgress.RecordClear("SANDBOX", StageDifficulty.Easy, 3);
            StageProgress.RecordClear("TUTORIAL", StageDifficulty.Easy, 3);

            Assert.AreEqual(0, SaveSystem.Data.stageProgress.Count);
        }

        [Test]
        public void TutorialFlag_IsSavedAndCanBeReset()
        {
            Assert.IsFalse(TutorialDirector.IsTutorialDone);
            TutorialDirector.MarkTutorialDone();

            Relaunch();
            Assert.IsTrue(TutorialDirector.IsTutorialDone);

            TutorialDirector.ResetTutorialProgress();
            Relaunch();
            Assert.IsFalse(TutorialDirector.IsTutorialDone);
        }

        [Test]
        public void EnemyIntel_RecordsStages_AndTheTutorialOnlyUnlocksTheFile()
        {
            var enemy = ScriptableObject.CreateInstance<EnemyData>();
            enemy.name = "TestGrunt";
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = "STAGE_02";
            var tutorial = ScriptableObject.CreateInstance<StageData>();
            tutorial.stageId = TutorialDirector.TutorialStageId;

            EnemyIntelProgress.RecordEncounter(enemy, tutorial);
            Relaunch();
            Assert.IsNotNull(SaveSystem.Data.FindIntel("TestGrunt"));
            Assert.IsEmpty(EnemyIntelProgress.GetSightedStageIds(enemy));

            EnemyIntelProgress.RecordEncounter(enemy, stage);
            Relaunch();
            CollectionAssert.AreEqual(new[] { "STAGE_02" }, EnemyIntelProgress.GetSightedStageIds(enemy));

            Object.DestroyImmediate(enemy);
            Object.DestroyImmediate(stage);
            Object.DestroyImmediate(tutorial);
        }

        [Test]
        public void ResetAll_DeletesTheFile_AndForgetsEverything()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Hard, 3);
            TutorialDirector.MarkTutorialDone();
            Assert.IsTrue(File.Exists(path));

            PlayerProgress.ResetAll();

            Assert.IsFalse(File.Exists(path));
            Assert.IsFalse(File.Exists(path + ".bak"));
            Assert.AreEqual(0, StageProgress.GetBestStars("STAGE_01", StageDifficulty.Hard));
            Assert.IsFalse(TutorialDirector.IsTutorialDone);
        }

        [Test]
        public void SavingTwice_KeepsTheLastSaveAsABackup()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 1);
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 2);

            Assert.IsTrue(File.Exists(path + ".bak"));
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void UnreadableSave_FallsBackToTheBackup_AndKeepsTheBrokenFile()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 1);
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 2); // the first save is now the backup
            File.WriteAllText(path, "{ this is not json");

            // The broken file is reported in the console (an error, then a warning for the restore)
            LogAssert.ignoreFailingMessages = true;
            try
            {
                Relaunch();

                Assert.AreEqual(1, StageProgress.GetBestStars("STAGE_01", StageDifficulty.Easy));
                Assert.IsTrue(File.Exists(path + ".corrupt"));
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        [Test]
        public void OldPlayerPrefsProgress_IsMovedIntoTheSave_AndRemovedFromPlayerPrefs()
        {
            PlayerPrefs.SetInt("TrashTD.Progress.STAGE_02.Hard", 3);
            PlayerPrefs.SetInt("TrashTD.TutorialDone", 1);
            PlayerPrefs.SetString("TrashTD.Intel.Index", "LegacyEnemy");
            PlayerPrefs.SetInt("TrashTD.Intel.Met.LegacyEnemy", 1);
            PlayerPrefs.SetString("TrashTD.Intel.Sighted.LegacyEnemy", "STAGE_01|STAGE_02");

            SaveSystem.UseSavePath(path, true); // the migration is off for test saves unless asked for

            Assert.AreEqual(3, StageProgress.GetBestStars("STAGE_02", StageDifficulty.Hard));
            Assert.IsTrue(TutorialDirector.IsTutorialDone);
            EnemyIntelEntry intel = SaveSystem.Data.FindIntel("LegacyEnemy");
            Assert.IsNotNull(intel);
            CollectionAssert.AreEqual(new[] { "STAGE_01", "STAGE_02" }, intel.sightedStageIds);

            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(PlayerPrefs.HasKey("TrashTD.Progress.STAGE_02.Hard"));
            Assert.IsFalse(PlayerPrefs.HasKey("TrashTD.TutorialDone"));
            Assert.IsFalse(PlayerPrefs.HasKey("TrashTD.Intel.Index"));
        }
    }
}
