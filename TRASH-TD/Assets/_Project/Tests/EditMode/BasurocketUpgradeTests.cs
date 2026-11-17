using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Operators;

namespace TrashTD.Tests
{
    /// <summary>Basurocket's rank upgrades: faster rockets and +1 range at 4★, cluster bombs around the blast at 5★.</summary>
    public class BasurocketUpgradeTests
    {
        private const string BasurocketDataPath = "Assets/_Project/Data/Operators/OP_Sniper_Basurocket.asset";

        private static OperatorData LoadData()
        {
            var data = AssetDatabase.LoadAssetAtPath<OperatorData>(BasurocketDataPath);
            Assert.IsNotNull(data, "OP_Sniper_Basurocket asset should exist.");
            return data;
        }

        [Test]
        public void SkillDescription_ListsBaseAndBothUpgradeTiers()
        {
            OperatorData data = LoadData();
            Assert.AreEqual(OperatorRarity.Star3, data.baseRarity);
            StringAssert.Contains("Base (3\u2605)", data.skillDescription);
            StringAssert.Contains("1st upgrade (4\u2605)", data.skillDescription);
            StringAssert.Contains("every 2.3 seconds", data.skillDescription);
            StringAssert.Contains("2nd upgrade (5\u2605)", data.skillDescription);
            StringAssert.Contains("cluster bombs", data.skillDescription);
        }

        [Test]
        public void Upgrades_UnlockAtFourAndFiveStars()
        {
            var basurocketObject = new GameObject("TestBasurocket");
            try
            {
                var basurocket = basurocketObject.AddComponent<BasurocketOperator>();

                basurocket.SetRarityForTesting(OperatorRarity.Star3);
                Assert.IsFalse(basurocket.HasFirstUpgrade);
                Assert.IsFalse(basurocket.HasSecondUpgrade);

                basurocket.SetRarityForTesting(OperatorRarity.Star4);
                Assert.IsTrue(basurocket.HasFirstUpgrade);
                Assert.IsFalse(basurocket.HasSecondUpgrade);

                basurocket.SetRarityForTesting(OperatorRarity.Star5);
                Assert.IsTrue(basurocket.HasFirstUpgrade, "The 2nd upgrade keeps the 1st.");
                Assert.IsTrue(basurocket.HasSecondUpgrade);
            }
            finally
            {
                Object.DestroyImmediate(basurocketObject);
            }
        }

        [Test]
        public void FirstUpgrade_ReachesOneTileFurther()
        {
            OperatorData data = LoadData();

            Vector2Int[] basePattern = data.GetRangePatternForRarity(OperatorRarity.Star3);
            Assert.AreEqual(data.rangePattern, basePattern);
            CollectionAssert.DoesNotContain(basePattern, new Vector2Int(8, 0));

            foreach (OperatorRarity rarity in new[] { OperatorRarity.Star4, OperatorRarity.Star5 })
            {
                Vector2Int[] upgraded = data.GetRangePatternForRarity(rarity);
                Assert.AreEqual(basePattern.Length + 1, upgraded.Length, $"{rarity}");
                CollectionAssert.IsSubsetOf(basePattern, upgraded, "The upgrade only adds a tile.");
                CollectionAssert.Contains(upgraded, new Vector2Int(8, 0));
                CollectionAssert.DoesNotContain(upgraded, new Vector2Int(1, 0), "It still cannot attack the adjacent tile.");
            }
        }

        [Test]
        public void FirstUpgrade_FiresFasterThanTheDataInterval()
        {
            OperatorData data = LoadData();
            Assert.Less(BasurocketOperator.UpgradedAttackInterval, data.attackInterval);
            Assert.AreEqual(2.3f, BasurocketOperator.UpgradedAttackInterval, 0.0001f);
        }

        [Test]
        public void ClusterRing_IsOneTileAroundTheMainBlast()
        {
            Assert.AreEqual(BasurocketOperator.BlastRadiusTiles + 1f, BasurocketOperator.ClusterRadiusTiles, 0.0001f);
            Assert.Less(BasurocketOperator.ClusterDamageMultiplier, 1f, "The cluster bombs hit for less than the main blast.");
            Assert.Greater(BasurocketOperator.ClusterDamageMultiplier, 0f);

            float blast = BasurocketOperator.BlastRadiusTiles;
            float cluster = BasurocketOperator.ClusterRadiusTiles;

            Assert.IsFalse(BasurocketOperator.IsInClusterRing(0f, blast, cluster), "The impact point belongs to the main blast.");
            Assert.IsFalse(BasurocketOperator.IsInClusterRing(blast, blast, cluster), "The edge of the main blast belongs to the main blast.");
            Assert.IsTrue(BasurocketOperator.IsInClusterRing(blast + 0.01f, blast, cluster));
            Assert.IsTrue(BasurocketOperator.IsInClusterRing(cluster, blast, cluster), "The edge of the ring is hit.");
            Assert.IsFalse(BasurocketOperator.IsInClusterRing(cluster + 0.01f, blast, cluster));
        }
    }
}
