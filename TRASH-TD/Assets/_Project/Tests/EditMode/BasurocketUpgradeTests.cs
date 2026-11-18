using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Operators;

namespace TrashTD.Tests
{
    /// <summary>Basurocket's rank upgrades: faster rockets and +1 range at 4★, cluster bombs at 5★.</summary>
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
            StringAssert.Contains("Base (3★)", data.skillDescription);
            StringAssert.Contains("1st upgrade (4★)", data.skillDescription);
            StringAssert.Contains("every 2.3 seconds", data.skillDescription);
            StringAssert.Contains("2nd upgrade (5★)", data.skillDescription);
            StringAssert.Contains("cluster bombs", data.skillDescription);
            StringAssert.Contains("100% of ATK", data.skillDescription);
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
        public void ClusterBombs_DealNormalDamage()
        {
            Assert.AreEqual(1f, BasurocketOperator.ClusterDamageMultiplier, 0.0001f);
        }

        [Test]
        public void ClusterBombs_StartOnTheImpactAndLandOnTheRimOfTheMainBlast()
        {
            const float cellSize = 1.5f;
            Vector3 center = new Vector3(2f, -1f, 0f);
            Vector3[] bombs = BasurocketOperator.GetClusterBombPositions(center, cellSize, 30f);

            Assert.AreEqual(BasurocketOperator.ClusterBombCount, bombs.Length);
            Assert.AreEqual(center, bombs[0], "The first bomb lands on the impact.");

            float rim = BasurocketOperator.BlastRadiusTiles * cellSize;
            float reach = BasurocketOperator.ClusterRadiusTiles * cellSize;
            float bombRadius = BasurocketOperator.ClusterBombRadiusTiles * cellSize;
            for (int i = 1; i < bombs.Length; i++)
            {
                float distance = Vector3.Distance(center, bombs[i]);
                Assert.AreEqual(rim, distance, 0.001f, $"Bomb {i}");
                Assert.AreEqual(reach, distance + bombRadius, 0.001f, $"Bomb {i} reaches the cluster radius.");
            }
        }

        [TestCase(0f)]
        [TestCase(17f)]
        [TestCase(200f)]
        public void ClusterBombs_CoverTheWholeMainBlast(float startAngle)
        {
            const float cellSize = 1f;
            Vector3[] bombs = BasurocketOperator.GetClusterBombPositions(Vector3.zero, cellSize, startAngle);
            float blastRadius = BasurocketOperator.BlastRadiusTiles * cellSize;
            float bombRadius = BasurocketOperator.ClusterBombRadiusTiles * cellSize;

            // Every enemy the main blast can hit can also be caught by a bomb.
            for (float distance = 0f; distance <= blastRadius; distance += 0.05f)
            {
                for (int degrees = 0; degrees < 360; degrees += 3)
                {
                    float angle = degrees * Mathf.Deg2Rad;
                    Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * distance;

                    bool covered = false;
                    for (int i = 0; i < bombs.Length && !covered; i++)
                        covered = Vector3.Distance(point, bombs[i]) <= bombRadius;

                    Assert.IsTrue(covered, $"No bomb reaches {point}.");
                }
            }
        }
    }
}
