using NUnit.Framework;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Tests
{
    /// <summary>
    /// What a map's environment does is data on its StageData asset (StageData.environment), not a stage id check. These
    /// pin the real assets so a stage can't silently lose its hazard.
    /// </summary>
    public class StageEnvironmentDataTests
    {
        [TestCase("STAGE_01", StageEnvironment.Weather)]
        [TestCase("STAGE_02", StageEnvironment.FallingItems)]
        [TestCase("STAGE_03", StageEnvironment.Sandstorm)]
        [TestCase("STAGE_04", StageEnvironment.FactoryHeat)]
        [TestCase("STAGE_05", StageEnvironment.Traffic)]
        [TestCase("STAGE_06", StageEnvironment.AcidRain)]
        [TestCase("STAGE_TUTORIAL", StageEnvironment.None)]
        [TestCase("STAGE_SANDBOX", StageEnvironment.None)]
        public void StageAsset_HasItsEnvironment(string assetName, StageEnvironment expected)
        {
            StageData stage = Resources.Load<StageData>("Stages/" + assetName);

            Assert.IsNotNull(stage, assetName);
            Assert.AreEqual(expected, stage.environment, assetName);
        }

        [Test]
        public void OnlyTheTrafficStage_HasTraffic()
        {
            for (int number = 1; number <= 6; number++)
            {
                StageData stage = Resources.Load<StageData>("Stages/STAGE_" + number.ToString("00"));
                Assert.AreEqual(number == 5, stage.HasTraffic, stage.stageId);
            }
        }

        [Test]
        public void GameplayHazards_AreEverythingExceptNoneAndWeather()
        {
            Assert.IsFalse(StageData.HasGameplayHazard(StageEnvironment.None));
            Assert.IsFalse(StageData.HasGameplayHazard(StageEnvironment.Weather));
            Assert.IsTrue(StageData.HasGameplayHazard(StageEnvironment.FallingItems));
            Assert.IsTrue(StageData.HasGameplayHazard(StageEnvironment.Sandstorm));
            Assert.IsTrue(StageData.HasGameplayHazard(StageEnvironment.FactoryHeat));
            Assert.IsTrue(StageData.HasGameplayHazard(StageEnvironment.Traffic));
            Assert.IsTrue(StageData.HasGameplayHazard(StageEnvironment.AcidRain));
        }
    }
}
