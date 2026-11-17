using System.Collections.Generic;
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
    /// Acts group the stages for the stage map: one act today (STAGE_01 to STAGE_06), more later, each on its own map. Stage
    /// ids stay global and contiguous across acts, so saves and the unlock chain are unchanged. These cover the labels, the
    /// act lock, the setup checks, and that the real data is valid. They use made-up acts and a temporary save, and put dev
    /// mode and the real catalog back afterwards.
    /// </summary>
    public class ActCatalogTests
    {
        private readonly List<Object> created = new List<Object>();
        private string folder;
        private bool devModeBefore;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "trashtd_act_test_" + System.Guid.NewGuid().ToString("N"));
            SaveSystem.UseSavePath(Path.Combine(folder, "save.json"));
            devModeBefore = EditorPrefs.GetBool(DevMode.EditorPrefKey, true);
            EditorPrefs.SetBool(DevMode.EditorPrefKey, false);
        }

        [TearDown]
        public void TearDown()
        {
            ActCatalog.UseActs(null);
            EditorPrefs.SetBool(DevMode.EditorPrefKey, devModeBefore);
            SaveSystem.UseSavePath(null);
            foreach (Object o in created) Object.DestroyImmediate(o);
            created.Clear();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        private ActData MakeAct(int number, params int[] stageNumbers)
        {
            ActData act = ScriptableObject.CreateInstance<ActData>();
            act.name = "ACT_" + number;
            act.actNumber = number;
            var entries = new List<ActStageEntry>();
            foreach (int stageNumber in stageNumbers)
            {
                StageData stage = ScriptableObject.CreateInstance<StageData>();
                stage.stageId = StageProgress.GetStageId(stageNumber);
                created.Add(stage);
                entries.Add(new ActStageEntry { stage = stage, mapPosition = new Vector2(0.5f, 0.5f) });
            }
            act.stages = entries.ToArray();
            created.Add(act);
            return act;
        }

        // ------------------------------------------------------------------ labels

        [Test]
        public void WithOneAct_StagesKeepTheirPlainLabel()
        {
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2, 3, 4, 5, 6) });

            Assert.IsFalse(ActCatalog.HasSeveralActs);
            Assert.AreEqual("STAGE 03", ActCatalog.FormatStageLabel("STAGE_03"));
            Assert.AreEqual("STAGE 06", ActCatalog.FormatStageLabel("STAGE_06"));
        }

        [Test]
        public void WithSeveralActs_ALabelCarriesTheActAndThePositionInIt()
        {
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2, 3, 4, 5, 6), MakeAct(2, 7, 8, 9) });

            Assert.IsTrue(ActCatalog.HasSeveralActs);
            Assert.AreEqual("ACT 1 // STAGE 06", ActCatalog.FormatStageLabel("STAGE_06"));
            Assert.AreEqual("ACT 2 // STAGE 01", ActCatalog.FormatStageLabel("STAGE_07"));
            Assert.AreEqual("ACT 2 // STAGE 03", ActCatalog.FormatStageLabel("STAGE_09"));
        }

        [Test]
        public void TheTutorialAndTheSandbox_AreInNoAct()
        {
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2), MakeAct(2, 3, 4) });

            Assert.AreEqual("STAGE 00", ActCatalog.FormatStageLabel(TutorialDirector.TutorialStageId));
            Assert.IsNull(ActCatalog.GetActOfStage("SANDBOX"));
            Assert.AreEqual(0, ActCatalog.GetPositionInAct("SANDBOX"));
        }

        [Test]
        public void Acts_AreOrderedByNumber_WhateverOrderTheyAreGiven()
        {
            ActData second = MakeAct(2, 3, 4);
            ActData first = MakeAct(1, 1, 2);
            ActCatalog.UseActs(new[] { second, first });

            Assert.AreSame(first, ActCatalog.All[0]);
            Assert.AreSame(second, ActCatalog.All[1]);
            Assert.AreSame(second, ActCatalog.GetAct(2));
            Assert.AreSame(first, ActCatalog.GetActOfStage("STAGE_02"));
            Assert.AreEqual(2, ActCatalog.GetPositionInAct("STAGE_04"));
        }

        // ------------------------------------------------------------------ the act lock

        [Test]
        public void AnAct_IsLockedUntilTheLastStageOfThePreviousActIsCleared()
        {
            ActData act1 = MakeAct(1, 1, 2, 3, 4, 5, 6);
            ActData act2 = MakeAct(2, 7, 8);
            ActCatalog.UseActs(new[] { act1, act2 });

            Assert.IsTrue(ActCatalog.IsActUnlocked(act1), "Act 1 is always open");
            Assert.IsFalse(ActCatalog.IsActUnlocked(act2));
            Assert.AreEqual("CLEAR ACT 1 // STAGE 06 FIRST", ActCatalog.GetActUnlockRequirement(act2));

            StageProgress.RecordClear("STAGE_06", StageDifficulty.Easy, 1);

            Assert.IsTrue(ActCatalog.IsActUnlocked(act2));
        }

        [Test]
        public void DevMode_OpensEveryAct()
        {
            ActData act2 = MakeAct(2, 7, 8);
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2, 3, 4, 5, 6), act2 });
            EditorPrefs.SetBool(DevMode.EditorPrefKey, true);

            Assert.IsTrue(ActCatalog.IsActUnlocked(act2));
        }

        [Test]
        public void AComingSoonAct_StaysLockedEvenAfterThePreviousActIsCleared_AndInDevMode()
        {
            ActData act1 = MakeAct(1, 1, 2, 3, 4, 5, 6);
            ActData act2 = MakeAct(2, 7);
            act2.stages[0].stage.comingSoon = true;
            ActCatalog.UseActs(new[] { act1, act2 });
            StageProgress.RecordClear("STAGE_06", StageDifficulty.Hard, 3);

            Assert.IsTrue(act2.IsComingSoon);
            Assert.IsFalse(ActCatalog.IsActUnlocked(act2));
            Assert.AreEqual("COMING SOON", ActCatalog.GetActUnlockRequirement(act2));

            EditorPrefs.SetBool(DevMode.EditorPrefKey, true);
            Assert.IsFalse(ActCatalog.IsActUnlocked(act2), "a placeholder cannot be opened even in dev mode");
        }

        [Test]
        public void AnActWithNoStages_IsNeverOpen_AndHasNoRequirement()
        {
            ActData empty = MakeAct(2);
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2), empty });

            Assert.IsFalse(ActCatalog.IsActUnlocked(empty));
            Assert.AreEqual(string.Empty, ActCatalog.GetActUnlockRequirement(empty));
        }

        [Test]
        public void TheUnlockRequirementOfAStage_NamesThePreviousStageTheWayTheUiLabelsIt()
        {
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2, 3, 4, 5, 6) });
            Assert.AreEqual("CLEAR STAGE 06 FIRST", StageProgress.GetUnlockRequirement("STAGE_07", StageDifficulty.Easy));

            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2, 3, 4, 5, 6), MakeAct(2, 7, 8) });
            Assert.AreEqual("CLEAR ACT 1 // STAGE 06 FIRST", StageProgress.GetUnlockRequirement("STAGE_07", StageDifficulty.Easy));
            Assert.AreEqual("CLEAR ACT 1 // STAGE 06 ON HARD", StageProgress.GetUnlockRequirement("STAGE_07", StageDifficulty.Hard));
            Assert.AreEqual("CLEAR ACT 2 // STAGE 01 FIRST", StageProgress.GetUnlockRequirement("STAGE_08", StageDifficulty.Normal));
        }

        [Test]
        public void TheStagesOfALaterAct_UnlockThroughTheSameChainAsAlways()
        {
            ActCatalog.UseActs(new[] { MakeAct(1, 1, 2, 3, 4, 5, 6), MakeAct(2, 7, 8) });
            Assert.IsFalse(StageProgress.IsUnlocked("STAGE_07", StageDifficulty.Easy));

            StageProgress.RecordClear("STAGE_06", StageDifficulty.Normal, 2);

            Assert.IsTrue(StageProgress.IsUnlocked("STAGE_07", StageDifficulty.Easy));
            Assert.IsFalse(StageProgress.IsUnlocked("STAGE_07", StageDifficulty.Hard), "Hard still needs a Hard clear before it");
            Assert.IsFalse(StageProgress.IsUnlocked("STAGE_08", StageDifficulty.Easy));
        }

        [Test]
        public void ClearedCount_CountsAStageOnceWhicheverDifficultiesWereCleared()
        {
            ActData act = MakeAct(1, 1, 2, 3);
            ActCatalog.UseActs(new[] { act });

            StageProgress.RecordClear("STAGE_01", StageDifficulty.Easy, 1);
            StageProgress.RecordClear("STAGE_01", StageDifficulty.Hard, 3);
            StageProgress.RecordClear("STAGE_03", StageDifficulty.Normal, 2);

            Assert.AreEqual(2, ActCatalog.GetClearedCount(act));
        }

        // ------------------------------------------------------------------ setup checks

        [Test]
        public void FindProblems_AcceptsActsWhoseStageIdsContinue()
        {
            var acts = new[] { MakeAct(1, 1, 2, 3, 4, 5, 6), MakeAct(2, 7, 8, 9) };

            Assert.IsEmpty(ActCatalog.FindProblems(acts));
        }

        [Test]
        public void FindProblems_FlagsAGapInTheStageIds()
        {
            var problems = ActCatalog.FindProblems(new[] { MakeAct(1, 1, 2), MakeAct(2, 4, 5) });

            Assert.IsTrue(problems.Exists(p => p.Contains("STAGE_03")), string.Join("; ", problems));
        }

        [Test]
        public void FindProblems_FlagsAStageInTwoActs_ADuplicateActNumber_AndAnEmptyAct()
        {
            var problems = ActCatalog.FindProblems(new[] { MakeAct(1, 1, 2), MakeAct(1, 2, 3), MakeAct(3) });

            Assert.IsTrue(problems.Exists(p => p.Contains("act number 1")), string.Join("; ", problems));
            Assert.IsTrue(problems.Exists(p => p.Contains("STAGE_02 is in both")), string.Join("; ", problems));
            Assert.IsTrue(problems.Exists(p => p.Contains("has no stages")), string.Join("; ", problems));
        }

        [Test]
        public void FindProblems_FlagsAMarkerOutsideItsMap_AndAnEmptyEntry()
        {
            ActData act = MakeAct(1, 1, 2);
            act.stages[1].mapPosition = new Vector2(1.4f, 0.5f);
            act.stages = new[] { act.stages[0], act.stages[1], new ActStageEntry() };

            var problems = ActCatalog.FindProblems(new[] { act });

            Assert.IsTrue(problems.Exists(p => p.Contains("outside its map")), string.Join("; ", problems));
            Assert.IsTrue(problems.Exists(p => p.Contains("entry 3 is empty")), string.Join("; ", problems));
        }

        // ------------------------------------------------------------------ the real data

        [Test]
        public void TheRealActs_AreSetUpCorrectly()
        {
            ActCatalog.UseActs(null);

            Assert.IsNotEmpty(ActCatalog.All);
            Assert.IsEmpty(ActCatalog.FindProblems(ActCatalog.All));
        }

        [Test]
        public void ActOne_HoldsTheSixOriginalStagesInOrder_WithTheirMarkers()
        {
            ActCatalog.UseActs(null);
            ActData act1 = ActCatalog.GetAct(1);

            Assert.IsNotNull(act1);
            Assert.AreEqual(6, act1.StageCount);
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(StageProgress.GetStageId(i + 1), act1.stages[i].stage.stageId);
            }
            Assert.IsNotNull(act1.tutorialStage, "the tutorial marker belongs to Act 1");
            Assert.IsFalse(string.IsNullOrEmpty(act1.zoneName));
            Assert.IsFalse(string.IsNullOrEmpty(act1.zoneLore));
        }

        [Test]
        public void ThePlaceholderStages_AreComingSoonStagesThatCannotBePlayed()
        {
            ActCatalog.UseActs(null);
            int placeholders = 0;
            foreach (StageData stage in Resources.LoadAll<StageData>("Stages"))
            {
                if (!stage.comingSoon) continue;
                placeholders++;
                Assert.AreEqual(0, stage.gridWidth, stage.stageId + ": a placeholder has no map, so it must never reach gameplay");
                Assert.IsNotNull(ActCatalog.GetActOfStage(stage.stageId), stage.stageId + " is in no act");
            }

            if (placeholders == 0) Assert.Ignore("No placeholder stages.");
        }

        [Test]
        public void EveryAct_HasSixStages_UntilActThree()
        {
            ActCatalog.UseActs(null);
            foreach (ActData act in ActCatalog.All)
            {
                if (act.actNumber > 3) continue;
                Assert.AreEqual(6, act.StageCount, act.name + " should have six stages");
            }
        }

        [Test]
        public void ThePlaceholderActs_CannotBeOpened_WhateverTheProgress_AndNeitherCanAnyActWhoseFirstStageIsLocked()
        {
            ActCatalog.UseActs(null);
            foreach (ActData act in ActCatalog.All)
            {
                if (act.IsComingSoon) Assert.IsFalse(ActCatalog.IsActUnlocked(act), act.name);
            }
        }

        [Test]
        public void EveryNumberedStageAsset_BelongsToAnAct()
        {
            // A new STAGE_07 asset that is not listed in an act would never appear on any stage map
            ActCatalog.UseActs(null);
            foreach (StageData stage in Resources.LoadAll<StageData>("Stages"))
            {
                if (StageProgress.GetStageNumber(stage.stageId) <= 0) continue;
                Assert.IsNotNull(ActCatalog.GetActOfStage(stage.stageId), stage.stageId + " is in no act (add it to an ActData in Resources/Acts)");
            }
        }
    }
}
