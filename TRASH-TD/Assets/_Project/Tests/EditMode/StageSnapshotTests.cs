using NUnit.Framework;
using UnityEngine;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    /// <summary>Save &amp; Exit: the saved-run data, its rules, and how it is stored in the save file and handed to the next stage.</summary>
    public class StageSnapshotTests
    {
        private static StageSnapshot Run(string stage = "STAGE 1", int difficulty = 1, int wave = 4, int lives = 3, int phase = 1)
        {
            var run = new StageSnapshot { stageId = stage, difficulty = difficulty, waveIndex = wave, totalWaves = 12, lives = lives, phase = phase };
            run.deck.Add(new CardSnapshot { operatorName = "Guard", rarity = 1, cooldown = 2, heat = 0, savedHP = 40 });
            run.deck.Add(new CardSnapshot());
            run.deployed.Add(new DeployedSnapshot { operatorName = "Sniper", rarity = 2, x = 3, y = 5, facing = 1, hp = 77, heat = 1 });
            return run;
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void IsUsable_AcceptsAnOrdinaryRun()
        {
            Assert.IsTrue(StageSnapshotRules.IsUsable(Run()));
            Assert.IsTrue(StageSnapshotRules.IsUsable(Run(phase: StageSnapshotRules.PhaseCardPick)));
        }

        [Test]
        public void IsUsable_RefusesAnythingTheGameCannotRebuild()
        {
            Assert.IsFalse(StageSnapshotRules.IsUsable(null));
            Assert.IsFalse(StageSnapshotRules.IsUsable(Run(stage: "")));
            Assert.IsFalse(StageSnapshotRules.IsUsable(Run(lives: 0)));
            Assert.IsFalse(StageSnapshotRules.IsUsable(Run(wave: -1)));
            Assert.IsFalse(StageSnapshotRules.IsUsable(Run(phase: 2)), "a wave is never resumed mid-way");

            StageSnapshot newer = Run();
            newer.version = StageSnapshotRules.CurrentVersion + 1;
            Assert.IsFalse(StageSnapshotRules.IsUsable(newer));

            StageSnapshot noDeck = Run();
            noDeck.deck = null;
            Assert.IsFalse(StageSnapshotRules.IsUsable(noDeck));
        }

        [Test]
        public void DraftRoundToSave_TakesBackTheRoundOfAnOpenDraft()
        {
            Assert.AreEqual(2, StageSnapshotRules.DraftRoundToSave(StageSnapshotRules.PhaseCardPick, 3));
            Assert.AreEqual(0, StageSnapshotRules.DraftRoundToSave(StageSnapshotRules.PhaseCardPick, 0));
            Assert.AreEqual(3, StageSnapshotRules.DraftRoundToSave(StageSnapshotRules.PhasePreparation, 3));
        }

        [Test]
        public void DescribeWave_IsTheWaveToPlayNext()
        {
            Assert.AreEqual("WAVE 5 OF 12", StageSnapshotRules.DescribeWave(Run(wave: 4)));
            Assert.AreEqual("WAVE 1 OF 12", StageSnapshotRules.DescribeWave(Run(wave: 0)));
            Assert.AreEqual("WAVE 12 OF 12", StageSnapshotRules.DescribeWave(Run(wave: 40)), "never past the last wave");

            StageSnapshot unknownTotal = Run(wave: 2);
            unknownTotal.totalWaves = 0;
            Assert.AreEqual("WAVE 3", StageSnapshotRules.DescribeWave(unknownTotal));
            Assert.AreEqual(string.Empty, StageSnapshotRules.DescribeWave(null));
        }

        // ------------------------------------------------------------------ the save file

        [Test]
        public void SaveData_StoresOneRunPerStageAndDifficulty()
        {
            var data = new SaveData();
            data.SetRun(Run("STAGE 1", 1, wave: 2));
            data.SetRun(Run("STAGE 1", 2, wave: 3));
            data.SetRun(Run("STAGE 2", 1, wave: 4));
            Assert.AreEqual(3, data.suspendedRuns.Count);

            data.SetRun(Run("STAGE 1", 1, wave: 9)); // replaces the first
            Assert.AreEqual(3, data.suspendedRuns.Count);
            Assert.AreEqual(9, data.FindRun("STAGE 1", 1).waveIndex);
            Assert.AreEqual(3, data.FindRun("STAGE 1", 2).waveIndex);
        }

        [Test]
        public void SaveData_FindAndClearRun()
        {
            var data = new SaveData();
            Assert.IsNull(data.FindRun("STAGE 1", 1));
            Assert.IsFalse(data.ClearRun("STAGE 1", 1));

            data.SetRun(Run());
            Assert.IsNotNull(data.FindRun("STAGE 1", 1));
            Assert.IsNull(data.FindRun("STAGE 1", 0), "another difficulty is another run");
            Assert.IsTrue(data.ClearRun("STAGE 1", 1));
            Assert.IsNull(data.FindRun("STAGE 1", 1));
        }

        [Test]
        public void SaveData_KeepsEverythingThroughJson()
        {
            var data = new SaveData();
            data.SetRun(Run());

            SaveData loaded = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
            StageSnapshot run = loaded.FindRun("STAGE 1", 1);

            Assert.IsNotNull(run);
            Assert.AreEqual(4, run.waveIndex);
            Assert.AreEqual(12, run.totalWaves);
            Assert.AreEqual(2, run.deck.Count);
            Assert.AreEqual("Guard", run.deck[0].operatorName);
            Assert.AreEqual(40, run.deck[0].savedHP);
            Assert.IsTrue(run.deck[1].IsEmpty, "an empty slot stays empty and keeps its place");
            Assert.AreEqual(1, run.deployed.Count);
            Assert.AreEqual(new Vector2Int(3, 5), new Vector2Int(run.deployed[0].x, run.deployed[0].y));
            Assert.AreEqual(77, run.deployed[0].hp);
        }

        [Test]
        public void SaveData_AnOldSaveWithoutRunsStillLoads()
        {
            SaveData loaded = JsonUtility.FromJson<SaveData>("{\"version\":1,\"tutorialDone\":true}");
            Assert.IsNotNull(loaded);
            Assert.IsTrue(loaded.tutorialDone);
            Assert.IsNull(loaded.FindRun("STAGE 1", 1) ?? null);
        }

        [Test]
        public void EmptyCardSnapshot_IsEmpty_AndAFullOneIsNot()
        {
            Assert.IsTrue(new CardSnapshot().IsEmpty);
            Assert.IsFalse(new CardSnapshot { operatorName = "Guard" }.IsEmpty);
        }

        // ------------------------------------------------------------------ handing a run to the next stage

        [SetUp]
        public void ClearRequest() => StageResume.Request(null);

        [TearDown]
        public void ClearRequestAfter() => StageResume.Request(null);

        [Test]
        public void Resume_IsGivenToTheMatchingStageOnce()
        {
            StageSnapshot run = Run();
            StageResume.Request(run);

            Assert.AreSame(run, StageResume.Consume("STAGE 1", 1));
            Assert.IsNull(StageResume.Consume("STAGE 1", 1), "taken once");
        }

        [Test]
        public void Resume_IsThrownAwayForAnotherStageOrDifficulty()
        {
            StageResume.Request(Run("STAGE 1", 1));
            Assert.IsNull(StageResume.Consume("STAGE 2", 1));
            Assert.IsFalse(StageResume.HasPending, "the request is gone, so it cannot apply to a later stage");

            StageResume.Request(Run("STAGE 1", 1));
            Assert.IsNull(StageResume.Consume("STAGE 1", 2));
        }

        [Test]
        public void Resume_RefusesAnUnusableRun()
        {
            StageResume.Request(Run(lives: 0));
            Assert.IsNull(StageResume.Consume("STAGE 1", 1));
        }

        [Test]
        public void Resume_WithNothingRequestedGivesNull()
        {
            Assert.IsNull(StageResume.Consume("STAGE 1", 1));
        }
    }
}
