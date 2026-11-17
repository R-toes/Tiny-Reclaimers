using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;

namespace TrashTD.Tests
{
    public class OquloctTalentTests
    {
        private GameObject oquloctObject;
        private GameObject normalOperatorObject;
        private GameObject attackerObject;
        private GameObject evasiveEnemyObject;

        private OperatorData oquloctData;
        private OperatorData normalOperatorData;
        private EnemyData evasiveEnemyData;
        private EnemyData attackerEnemyData;

        private OquloctOperator oquloct;
        private ThornchinOperator normalOperator;
        private GruntEnemy evasiveEnemy;
        private GruntEnemy attackerEnemy;
        private SpriteRenderer spriteRenderer;

        [SetUp]
        public void SetUp()
        {
            oquloctData = ScriptableObject.CreateInstance<OperatorData>();
            oquloctData.operatorName = "Oquloct";
            oquloctData.operatorClass = OperatorClass.Sniper;
            oquloctData.position = OperatorPosition.Ranged;
            oquloctData.baseHP = 100;
            oquloctData.baseATK = 150;
            oquloctData.baseDEF = 10;
            oquloctData.damageType = DamageType.Physical;
            oquloctData.baseRarity = OperatorRarity.Star2;
            oquloctData.rangePattern = new[]
            {
                new Vector2Int(1, -1),
                new Vector2Int(1, 0),
                new Vector2Int(1, 1)
            };

            normalOperatorData = ScriptableObject.CreateInstance<OperatorData>();
            normalOperatorData.operatorName = "NormalOp";
            normalOperatorData.operatorClass = OperatorClass.Defender;
            normalOperatorData.position = OperatorPosition.Melee;
            normalOperatorData.baseHP = 200;
            normalOperatorData.baseATK = 50;
            normalOperatorData.baseDEF = 20;
            normalOperatorData.damageType = DamageType.Physical;

            oquloctObject = new GameObject("TestOquloct");
            spriteRenderer = oquloctObject.AddComponent<SpriteRenderer>();
            spriteRenderer.color = Color.white;
            oquloct = oquloctObject.AddComponent<OquloctOperator>();
            oquloct.Initialize(oquloctData, OperatorRarity.Star2);

            normalOperatorObject = new GameObject("TestNormalOp");
            normalOperator = normalOperatorObject.AddComponent<ThornchinOperator>();
            normalOperator.Initialize(normalOperatorData, OperatorRarity.Star2);

            evasiveEnemyData = ScriptableObject.CreateInstance<EnemyData>();
            evasiveEnemyData.baseHP = 500;
            evasiveEnemyData.baseDEF = 0;
            evasiveEnemyData.operatorAttackMissChance = 1.0f; // 100% miss chance

            evasiveEnemyObject = new GameObject("TestEvasiveEnemy");
            evasiveEnemy = evasiveEnemyObject.AddComponent<GruntEnemy>();
            evasiveEnemy.Initialize(evasiveEnemyData, 1);

            attackerEnemyData = ScriptableObject.CreateInstance<EnemyData>();
            attackerEnemyData.baseHP = 200;
            attackerEnemyData.baseATK = 25;
            attackerEnemyData.baseDEF = 0;

            attackerObject = new GameObject("TestAttackerEnemy");
            attackerEnemy = attackerObject.AddComponent<GruntEnemy>();
            attackerEnemy.Initialize(attackerEnemyData, 1);
        }

        [TearDown]
        public void TearDown()
        {
            if (oquloctObject != null) Object.DestroyImmediate(oquloctObject);
            if (normalOperatorObject != null) Object.DestroyImmediate(normalOperatorObject);
            if (attackerObject != null) Object.DestroyImmediate(attackerObject);
            if (evasiveEnemyObject != null) Object.DestroyImmediate(evasiveEnemyObject);
            if (oquloctData != null) Object.DestroyImmediate(oquloctData);
            if (normalOperatorData != null) Object.DestroyImmediate(normalOperatorData);
            if (evasiveEnemyData != null) Object.DestroyImmediate(evasiveEnemyData);
            if (attackerEnemyData != null) Object.DestroyImmediate(attackerEnemyData);
        }

        [Test]
        public void IgnoresMissMechanic_ReturnsTrueForOquloct_AndFalseForDefaultOperator()
        {
            Assert.IsTrue(oquloct.IgnoresMissMechanic, "Oquloct should ignore miss mechanic.");
            Assert.IsFalse(normalOperator.IgnoresMissMechanic, "Default operator should not ignore miss mechanic.");
        }

        [Test]
        public void AttackAnimation_UsesRequestedTimingsAndFiresOnFifthFrame()
        {
            Assert.AreEqual(8, OquloctSpriteAnimation.AttackFrameCount);
            Assert.AreEqual(4, OquloctSpriteAnimation.FireFrame);
            Assert.AreEqual(0.47f, OquloctSpriteAnimation.WindupDuration, 0.0001f);
            Assert.AreEqual(0.13f, OquloctSpriteAnimation.GetAttackFrameDuration(0), 0.0001f);
            Assert.AreEqual(0.11f, OquloctSpriteAnimation.GetAttackFrameDuration(1), 0.0001f);
            Assert.AreEqual(0.11f, OquloctSpriteAnimation.GetAttackFrameDuration(2), 0.0001f);
            Assert.AreEqual(0.12f, OquloctSpriteAnimation.GetAttackFrameDuration(3), 0.0001f);
            Assert.AreEqual(0.07f, OquloctSpriteAnimation.GetAttackFrameDuration(4), 0.0001f);
            Assert.AreEqual(0.10f, OquloctSpriteAnimation.GetAttackFrameDuration(5), 0.0001f);
            Assert.AreEqual(0.11f, OquloctSpriteAnimation.GetAttackFrameDuration(6), 0.0001f);
            Assert.AreEqual(0.14f, OquloctSpriteAnimation.GetAttackFrameDuration(7), 0.0001f);
        }

        [Test]
        public void TryTakeAttackDamage_FromOquloct_IgnoresMissChance_WhileNormalOperatorMisses()
        {
            int startingHP = evasiveEnemy.CurrentHP;

            // Normal operator attacks 100% miss enemy -> should miss
            bool normalHit = evasiveEnemy.TryTakeAttackDamage(20, DamageType.Physical, normalOperator);
            Assert.IsFalse(normalHit, "Normal operator attack should miss against 100% miss chance enemy.");
            Assert.AreEqual(startingHP, evasiveEnemy.CurrentHP, "Enemy HP should not change when attack misses.");

            // Oquloct attacks 100% miss enemy -> should ignore miss chance and hit
            bool oquloctHit = evasiveEnemy.TryTakeAttackDamage(20, DamageType.Physical, oquloct);
            Assert.IsTrue(oquloctHit, "Oquloct attack should ignore miss chance and hit.");
            Assert.Less(evasiveEnemy.CurrentHP, startingHP, "Enemy should have taken damage from Oquloct.");
        }

        [Test]
        public void TakeDamage_FromEnemyAttacker_ActivatesCamouflageAndLowersOpacity()
        {
            Assert.IsFalse(oquloct.IsCamouflaged, "Oquloct should not be camouflaged initially.");
            Assert.IsTrue(oquloct.IsTargetable, "Oquloct should be targetable initially.");
            Assert.AreEqual(1.0f, spriteRenderer.color.a, 0.001f, "Initial alpha should be 1.0.");

            oquloct.TakeDamage(15, DamageType.Physical, attackerEnemy);

            Assert.IsTrue(oquloct.IsCamouflaged, "Oquloct should be camouflaged after taking damage from an enemy.");
            Assert.IsFalse(oquloct.IsTargetable, "Oquloct should not be targetable while camouflaged.");
            Assert.Greater(oquloct.CamouflageTimeRemaining, 0f, "Camouflage time remaining should be positive.");
            Assert.AreEqual(OquloctOperator.CamouflageAlpha, spriteRenderer.color.a, 0.001f, "Sprite opacity should be lowered to CamouflageAlpha.");
        }

        [Test]
        public void TakeDamage_FromEnvironmentalDamage_DoesNotActivateCamouflage()
        {
            // Environmental damage has attacker == null
            oquloct.TakeDamage(15, DamageType.Physical, null);

            Assert.IsFalse(oquloct.IsCamouflaged, "Oquloct should not gain camouflage from environmental damage.");
            Assert.IsTrue(oquloct.IsTargetable, "Oquloct should remain targetable.");
            Assert.AreEqual(1.0f, spriteRenderer.color.a, 0.001f, "Sprite opacity should remain 1.0.");
        }

        [Test]
        public void DeployAndRetreat_ResetsCamouflageAndRestoresAlpha()
        {
            var cell = new GridCell(0, 0, TileType.HighGround, Vector3.zero);
            bool deployed = oquloct.Deploy(cell);
            Assert.IsTrue(deployed, "Oquloct should deploy successfully onto ranged platform.");

            oquloct.ActivateCamouflage();
            Assert.IsTrue(oquloct.IsCamouflaged, "Oquloct should be camouflaged.");
            Assert.AreEqual(OquloctOperator.CamouflageAlpha, spriteRenderer.color.a, 0.001f);

            oquloct.Retreat();
            Assert.IsFalse(oquloct.IsCamouflaged, "Camouflage should reset on retreat.");
            Assert.IsTrue(oquloct.IsTargetable, "Oquloct should be targetable again once retreated.");
            Assert.AreEqual(1.0f, spriteRenderer.color.a, 0.001f, "Sprite alpha should be restored to 1.0 on retreat.");
        }

        [Test]
        public void GetRangePatternForRarity_AddsFiveTileConeAtFourStars()
        {
            Vector2Int[] baseRange = OquloctOperator.GetRangePatternForRarity(oquloctData, OperatorRarity.Star2);
            Vector2Int[] firstUpgradeRange = OquloctOperator.GetRangePatternForRarity(oquloctData, OperatorRarity.Star3);
            Vector2Int[] upgradedRange = oquloctData.GetRangePatternForRarity(OperatorRarity.Star4);

            Assert.AreEqual(3, baseRange.Length);
            Assert.AreEqual(3, firstUpgradeRange.Length);
            Assert.AreEqual(8, upgradedRange.Length);
            for (int y = -2; y <= 2; y++)
                CollectionAssert.Contains(upgradedRange, new Vector2Int(2, y));
        }

        [Test]
        public void CamouflageCounterattack_HalfMultiplierDealsHalfOfMitigatedDamage()
        {
            oquloct.Initialize(oquloctData, OperatorRarity.Star3);
            FieldInfo camouflageTimer = typeof(OquloctOperator).GetField(
                "camouflageTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(camouflageTimer);
            camouflageTimer.SetValue(oquloct, OquloctOperator.CamouflageDuration);
            int startingHP = evasiveEnemy.CurrentHP;
            MethodInfo applyShotgunDamage = typeof(OquloctOperator).GetMethod(
                "ApplyShotgunDamage", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(applyShotgunDamage);
            applyShotgunDamage.Invoke(oquloct, new object[]
            {
                new List<EnemyBase> { evasiveEnemy },
                0.5f
            });

            int fullDamage = TrashTD.Combat.DamageCalculator.CalculateDamage(
                oquloct.CurrentATK, evasiveEnemy.CurrentDEF);
            Assert.AreEqual(startingHP - Mathf.Max(1, Mathf.RoundToInt(fullDamage * 0.5f)), evasiveEnemy.CurrentHP);
            Assert.IsTrue(oquloct.IsCamouflaged, "The counterattack must not cancel camouflage.");
        }

        [Test]
        public void CamouflageDefenseBonus_AddsFiftyPercentOfCurrentDefense()
        {
            oquloct.Initialize(oquloctData, OperatorRarity.Star3);
            FieldInfo defenseTimer = typeof(OquloctOperator).GetField(
                "camouflageDefenseTimer", BindingFlags.Instance | BindingFlags.NonPublic);
            int baseDefense = oquloct.CurrentDEF;

            Assert.IsNotNull(defenseTimer);
            defenseTimer.SetValue(oquloct, 1f);
            Assert.AreEqual(Mathf.RoundToInt(baseDefense * 1.5f), oquloct.CurrentDEF);
            Assert.AreEqual(1f, oquloct.CamouflageDefenseTimeRemaining, 0.001f);

            defenseTimer.SetValue(oquloct, 0f);
            Assert.AreEqual(baseDefense, oquloct.CurrentDEF);
        }

        [Test]
        public void CamouflageHealing_HealsTwentyFivePercentOfMaxHpAcrossItsDuration()
        {
            oquloct.Initialize(oquloctData, OperatorRarity.Star3);
            oquloct.TakeDamage(40, DamageType.Physical);
            int startingHP = oquloct.CurrentHP;
            int expectedHealing = Mathf.RoundToInt(oquloct.MaxHP * OquloctOperator.CamouflageHealMaxHpRatio);
            MethodInfo updateHealing = typeof(OquloctOperator).GetMethod(
                "UpdateCamouflageHealing", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(updateHealing);
            oquloct.ActivateCamouflage();
            updateHealing.Invoke(oquloct, new object[] { OquloctOperator.CamouflageDuration * 0.5f });
            Assert.AreEqual(startingHP + expectedHealing / 2, oquloct.CurrentHP);

            updateHealing.Invoke(oquloct, new object[] { OquloctOperator.CamouflageDuration * 0.5f });
            Assert.AreEqual(startingHP + expectedHealing, oquloct.CurrentHP);
        }

#if UNITY_EDITOR
        [Test]
        public void OperatorStats_BasurocketAndOquloct_MatchExpectedValues()
        {
            var basurocket = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Sniper_Basurocket.asset");
            Assert.IsNotNull(basurocket, "OP_Sniper_Basurocket asset should exist.");
            Assert.AreEqual(50, basurocket.baseHP, "Basurocket HP should be 50.");
            Assert.AreEqual(165, basurocket.baseATK, "Basurocket ATK should be 165.");
            Assert.AreEqual(2.8f, basurocket.attackInterval, 0.001f, "Basurocket attack interval should be 2.8.");

            var oquloct = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Sniper_Oquloct.asset");
            Assert.IsNotNull(oquloct, "OP_Sniper_Oquloct asset should exist.");
            Assert.AreEqual(2.3f, oquloct.attackInterval, 0.001f, "Oquloct attack interval should be 2.3.");
            StringAssert.Contains("<color=#FFD700>Base (2★):</color>", oquloct.skillDescription);
            StringAssert.Contains("<color=#FFD700>1st upgrade (3★):</color>", oquloct.skillDescription);
            StringAssert.Contains("Heals 25% of max HP over its duration.", oquloct.skillDescription);
            StringAssert.Contains("<color=#FFD700>2nd upgrade (4★):</color>", oquloct.skillDescription);
        }
#endif
    }
}
