using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Development;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    /// <summary>
    /// The stage unlock rules (StageProgress): STAGE_01 is always open; clearing a stage on Easy or Normal opens Easy and
    /// Normal of the next; only clearing it on Hard opens Hard of the next. Dev mode opens everything, so these tests switch
    /// it off (and give it back), and they write to a temporary save, never the real one.
    /// </summary>
    public class StageUnlockTests
    {
        private string folder;
        private bool devModeBefore;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "trashtd_unlock_test_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.UseSavePath(Path.Combine(folder, "save.json"));

            devModeBefore = EditorPrefs.GetBool(DevMode.EditorPrefKey, true);
            EditorPrefs.SetBool(DevMode.EditorPrefKey, false);
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.SetBool(DevMode.EditorPrefKey, devModeBefore);
            SaveSystem.UseSavePath(null);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void StageOne_IsOpenOnEveryDifficulty()
        {
            foreach (StageDifficulty difficulty in System.Enum.GetValues(typeof(StageDifficulty)))
            {
                Assert.IsTrue(StageProgress.IsUnlocked("STAGE_01", difficulty), difficulty.ToString());
            }
        }

        [Test]
        public void StageTwo_IsLocked_UntilStageOneIsCleared()
        {
            foreach (StageDifficulty difficulty in System.Enum.GetValues(typeof(StageDifficulty)))
            {
                Assert.IsFalse(StageProgress.IsUnlocked("STAGE_02", difficulty), difficulty.ToString());
            }
            Assert.IsFalse(StageProgress.IsAnyDifficultyUnlocked("STAGE_02"));
        }

        [Test]
        public void ClearingOnEasyOrNormal_OpensEasyAndNormalOfTheNextStage_ButNotHard()
        {
            foreach (StageDifficulty cleared in new[] { StageDifficulty.Easy, StageDifficulty.Normal })
            {
                StageProgress.ResetAll();
                StageProgress.RecordClear("STAGE_01", cleared, 1);

                Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Easy), "easy after clearing " + cleared);
                Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Normal), "normal after clearing " + cleared);
                Assert.IsFalse(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Hard), "hard after clearing " + cleared);
                Assert.IsTrue(StageProgress.IsAnyDifficultyUnlocked("STAGE_02"));
            }
        }

        [Test]
        public void OnlyClearingOnHard_OpensHardOfTheNextStage()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Hard, 1);

            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Hard));
            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Easy));
            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Normal));
        }

        [Test]
        public void Unlocks_ChainOneStageAtATime()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 3);

            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Easy));
            Assert.IsFalse(StageProgress.IsUnlocked("STAGE_03", StageDifficulty.Easy), "stage 3 needs stage 2, not just stage 1");

            StageProgress.RecordClear("STAGE_02", StageDifficulty.Normal, 1);
            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_03", StageDifficulty.Easy));
            Assert.IsFalse(StageProgress.IsUnlocked("STAGE_04", StageDifficulty.Easy));
        }

        [Test]
        public void ResettingProgress_LocksTheStagesAgain()
        {
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Hard, 3);
            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Hard));

            PlayerProgress.ResetAll();

            Assert.IsFalse(StageProgress.IsUnlocked("STAGE_02", StageDifficulty.Easy));
        }

        [Test]
        public void DevMode_OpensEverything()
        {
            EditorPrefs.SetBool(DevMode.EditorPrefKey, true);

            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_06", StageDifficulty.Hard));
            Assert.IsTrue(StageProgress.IsAnyDifficultyUnlocked("STAGE_05"));
        }

        [Test]
        public void StarRatings_KeepTheBestAndStayInRange()
        {
            StageProgress.RecordClear("STAGE_03", StageDifficulty.Normal, 2);
            StageProgress.RecordClear("STAGE_03", StageDifficulty.Normal, 1);
            Assert.AreEqual(2, StageProgress.GetBestStars("STAGE_03", StageDifficulty.Normal));

            StageProgress.RecordClear("STAGE_03", StageDifficulty.Normal, 9);
            Assert.AreEqual(3, StageProgress.GetBestStars("STAGE_03", StageDifficulty.Normal), "clamped to three stars");

            StageProgress.RecordClear("STAGE_04", StageDifficulty.Easy, 0);
            Assert.AreEqual(1, StageProgress.GetBestStars("STAGE_04", StageDifficulty.Easy), "a clear is at least one star");
            Assert.IsTrue(StageProgress.IsCleared("STAGE_04", StageDifficulty.Easy));
            Assert.IsFalse(StageProgress.IsCleared("STAGE_04", StageDifficulty.Hard));
        }

        [Test]
        public void TheSandboxAndTutorial_AreNeitherTrackedNorLocked()
        {
            StageProgress.RecordClear("SANDBOX", StageDifficulty.Easy, 3);
            Assert.AreEqual(0, StageProgress.GetBestStars("SANDBOX", StageDifficulty.Easy));

            Assert.IsTrue(StageProgress.IsUnlocked("SANDBOX", StageDifficulty.Hard));
            Assert.IsTrue(StageProgress.IsUnlocked("TUTORIAL", StageDifficulty.Hard));
        }

        [Test]
        public void StageNumbers_AreReadFromTheId()
        {
            Assert.AreEqual(3, StageProgress.GetStageNumber("STAGE_03"));
            Assert.AreEqual(12, StageProgress.GetStageNumber("STAGE_12"));
            Assert.AreEqual(0, StageProgress.GetStageNumber("SANDBOX"));
            Assert.AreEqual(0, StageProgress.GetStageNumber("TUTORIAL"));
            Assert.AreEqual(0, StageProgress.GetStageNumber(""));
            Assert.AreEqual(0, StageProgress.GetStageNumber(null));
            Assert.AreEqual("STAGE_06", StageProgress.GetStageId(6));
        }

        [Test]
        public void UnlockRequirement_NamesTheStageBefore()
        {
            Assert.AreEqual(string.Empty, StageProgress.GetUnlockRequirement("STAGE_01", StageDifficulty.Hard));
            Assert.AreEqual("CLEAR STAGE 02 FIRST", StageProgress.GetUnlockRequirement("STAGE_03", StageDifficulty.Easy));
            Assert.AreEqual("CLEAR STAGE 02 FIRST", StageProgress.GetUnlockRequirement("STAGE_03", StageDifficulty.Normal));
            Assert.AreEqual("CLEAR STAGE 02 ON HARD", StageProgress.GetUnlockRequirement("STAGE_03", StageDifficulty.Hard));
        }
    }
}
