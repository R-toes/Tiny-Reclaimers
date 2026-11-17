using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrashTD.Combat;
using TrashTD.Data;
using TrashTD.Operators;

namespace TrashTD.Tests
{
    /// <summary>Castiel's rank upgrades: a faster, stronger laser at 3★ and a second laser at 4★.</summary>
    public class CastielUpgradeTests
    {
        private const string CastielDataPath = "Assets/_Project/Data/Operators/OP_Caster_Castiel.asset";

        private static readonly int[] BaseDamage = { 5, 15, 30, 80, 100, 150 };
        private static readonly int[] UpgradedDamage = { 6, 18, 36, 96, 120, 180 };

        [Test]
        public void SkillDescription_ListsBaseAndBothUpgradeTiers()
        {
            var data = AssetDatabase.LoadAssetAtPath<OperatorData>(CastielDataPath);
            Assert.IsNotNull(data);
            Assert.AreEqual(OperatorRarity.Star2, data.baseRarity);
            StringAssert.Contains("Base (2\u2605)", data.skillDescription);
            StringAssert.Contains("1st upgrade (3\u2605)", data.skillDescription);
            StringAssert.Contains("every 0.3 seconds", data.skillDescription);
            StringAssert.Contains("2nd upgrade (4\u2605)", data.skillDescription);
            StringAssert.Contains("second laser", data.skillDescription);
        }

        [Test]
        public void Upgrades_UnlockAtThreeAndFourStars()
        {
            var castielObject = new GameObject("TestCastiel");
            try
            {
                var castiel = castielObject.AddComponent<CastielOperator>();

                castiel.SetRarityForTesting(OperatorRarity.Star2);
                Assert.IsFalse(castiel.HasFirstUpgrade);
                Assert.IsFalse(castiel.HasSecondUpgrade);
                Assert.AreEqual(CastielOperator.BaseTickInterval, castiel.TickInterval);
                Assert.AreEqual(CastielOperator.BaseLockStepDuration, castiel.LockStepDuration);
                Assert.AreEqual(1, castiel.LaserCount);

                castiel.SetRarityForTesting(OperatorRarity.Star3);
                Assert.IsTrue(castiel.HasFirstUpgrade);
                Assert.IsFalse(castiel.HasSecondUpgrade);
                Assert.AreEqual(CastielOperator.UpgradedTickInterval, castiel.TickInterval);
                Assert.AreEqual(CastielOperator.UpgradedLockStepDuration, castiel.LockStepDuration);
                Assert.AreEqual(1, castiel.LaserCount);

                castiel.SetRarityForTesting(OperatorRarity.Star4);
                Assert.IsTrue(castiel.HasFirstUpgrade);
                Assert.IsTrue(castiel.HasSecondUpgrade, "The 2nd upgrade keeps the 1st.");
                Assert.AreEqual(CastielOperator.UpgradedLaserCount, castiel.LaserCount);
            }
            finally
            {
                Object.DestroyImmediate(castielObject);
            }
        }

        [Test]
        public void FirstUpgrade_TicksFasterRampsSoonerAndHitsHarder()
        {
            Assert.Less(CastielOperator.UpgradedTickInterval, CastielOperator.BaseTickInterval);
            Assert.Less(CastielOperator.UpgradedLockStepDuration, CastielOperator.BaseLockStepDuration);

            for (int stage = 0; stage < BaseDamage.Length; stage++)
            {
                Assert.AreEqual(BaseDamage[stage], CastielOperator.GetBeamDamageForStage(stage, false), $"base stage {stage}");
                Assert.AreEqual(UpgradedDamage[stage], CastielOperator.GetBeamDamageForStage(stage, true), $"upgraded stage {stage}");
                Assert.AreEqual(Mathf.RoundToInt(BaseDamage[stage] * 1.2f), UpgradedDamage[stage], $"+20% at stage {stage}");
            }
        }

        [Test]
        public void BeamStage_StepsOncePerLockStepAndStopsAtTheLastStage()
        {
            // Just before and just after each step, for both the base and the upgraded ramp.
            foreach (bool upgraded in new[] { false, true })
            {
                float step = CastielOperator.GetLockStepDuration(upgraded);
                for (int stage = 0; stage <= LightLaserVisual.MaxStage; stage++)
                {
                    Assert.AreEqual(stage, CastielOperator.GetBeamStage(step * stage + 0.01f, upgraded), $"upgraded {upgraded}, stage {stage}");
                    if (stage > 0)
                        Assert.AreEqual(stage - 1, CastielOperator.GetBeamStage(step * stage - 0.01f, upgraded));
                }

                Assert.AreEqual(LightLaserVisual.MaxStage, CastielOperator.GetBeamStage(999f, upgraded));
                Assert.AreEqual(0, CastielOperator.GetBeamStage(0f, upgraded));
            }

            // The same lock time is a higher stage once upgraded: 1.3 s is still the first step at base rank.
            Assert.AreEqual(0, CastielOperator.GetBeamStage(1.3f, false));
            Assert.AreEqual(1, CastielOperator.GetBeamStage(1.3f, true));
        }
    }
}
