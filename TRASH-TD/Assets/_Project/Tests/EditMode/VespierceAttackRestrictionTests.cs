using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    public class VespierceAttackRestrictionTests
    {
        private GameObject vespierceObject;
        private GameObject gridObject;
        private GameObject operatorManagerObject;
        private VespierceOperator vespierce;
        private OperatorData vespierceData;
        private GridManager gridManager;
        private OperatorManager operatorManager;
        private StageData stageData;
        private readonly List<GameObject> enemyObjects = new List<GameObject>();
        private readonly List<EnemyData> enemyDataAssets = new List<EnemyData>();

        [SetUp]
        public void SetUp()
        {
            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.None);

            gridObject = new GameObject("TestGridManager");
            gridManager = gridObject.AddComponent<GridManager>();
            stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.gridWidth = 3;
            stageData.gridHeight = 3;
            stageData.tileLayout = new TileType[stageData.gridWidth * stageData.gridHeight];
            gridManager.InitializeFromStageData(stageData);

            operatorManagerObject = new GameObject("TestOperatorManager");
            operatorManager = TestUtility.AddAwakened<OperatorManager>(operatorManagerObject);
            operatorManager.Initialize(gridManager);

            vespierceData = ScriptableObject.CreateInstance<OperatorData>();
            vespierceData.operatorName = "Vespierce";
            vespierceData.position = OperatorPosition.Melee;
            vespierceData.baseHP = 300;
            vespierceData.blockCount = 1;

            vespierceObject = new GameObject("TestVespierce");
            vespierce = vespierceObject.AddComponent<VespierceOperator>();
            vespierce.Initialize(vespierceData, OperatorRarity.Star3);
            vespierce.Deploy(gridManager.GetCell(0, 0));
            operatorManager.RegisterOperator(vespierce);
        }

        [TearDown]
        public void TearDown()
        {
            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.None);
            TestUtility.ClearSingletons();
            if (vespierceObject != null) Object.DestroyImmediate(vespierceObject);
            if (vespierceData != null) Object.DestroyImmediate(vespierceData);
            if (operatorManagerObject != null) Object.DestroyImmediate(operatorManagerObject);
            if (gridObject != null) Object.DestroyImmediate(gridObject);
            if (stageData != null) Object.DestroyImmediate(stageData);

            foreach (GameObject enemyObject in enemyObjects)
                if (enemyObject != null) Object.DestroyImmediate(enemyObject);
            foreach (EnemyData enemyData in enemyDataAssets)
                if (enemyData != null) Object.DestroyImmediate(enemyData);

            enemyObjects.Clear();
            enemyDataAssets.Clear();
        }

        [Test]
        public void NonTankEnemy_CanDamageVespierceOnlyWhileBlockedByHim()
        {
            GruntEnemy attacker = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            int startingHP = vespierce.CurrentHP;

            Assert.IsFalse(vespierce.TryTakeAttackDamage(30, DamageType.Physical, attacker));
            Assert.AreEqual(startingHP, vespierce.CurrentHP);

            Assert.IsTrue(vespierce.TryBlock(attacker));
            Assert.IsTrue(vespierce.TryTakeAttackDamage(30, DamageType.Physical, attacker));
            Assert.Less(vespierce.CurrentHP, startingHP);
        }

        [Test]
        public void TankEnemy_CanDamageVespierceAndStillBeBlocked()
        {
            TankEnemy attacker = CreateEnemy<TankEnemy>(EnemyArchetype.Tank);
            int startingHP = vespierce.CurrentHP;

            Assert.IsTrue(vespierce.TryBlock(attacker), "Vespierce should still block tank enemies.");
            Assert.IsTrue(vespierce.TryTakeAttackDamage(30, DamageType.Physical, attacker));

            Assert.Less(vespierce.CurrentHP, startingHP);
        }

        [Test]
        public void EnvironmentalDamage_CanDamageVespierce()
        {
            int startingHP = vespierce.CurrentHP;

            Assert.IsTrue(vespierce.TakeTrueDamage(30));
            Assert.Less(vespierce.CurrentHP, startingHP);
        }

        [Test]
        public void AreaAttackFromUnblockedEnemy_CanDamageVespierce()
        {
            GruntEnemy attacker = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            int startingHP = vespierce.CurrentHP;

            Assert.IsTrue(vespierce.TryTakeAreaAttackDamage(30, DamageType.Physical, attacker));

            Assert.Less(vespierce.CurrentHP, startingHP);
        }

        [Test]
        public void PerfectParry_ReducesDirectDamageAndReflectsWithPerAttackerCooldown()
        {
            SetRarity(OperatorRarity.Star5);
            TankEnemy firstAttacker = CreateEnemy<TankEnemy>(EnemyArchetype.Tank);
            TankEnemy secondAttacker = CreateEnemy<TankEnemy>(EnemyArchetype.Tank);
            int startingHP = vespierce.CurrentHP;
            int firstAttackerHP = firstAttacker.CurrentHP;
            int secondAttackerHP = secondAttacker.CurrentHP;

            Assert.IsTrue(vespierce.TryTakeAttackDamage(100, DamageType.True, firstAttacker));
            Assert.AreEqual(startingHP - 65, vespierce.CurrentHP);
            Assert.AreEqual(firstAttackerHP - 20, firstAttacker.CurrentHP);

            Assert.IsTrue(vespierce.TryTakeAttackDamage(100, DamageType.True, firstAttacker));
            Assert.AreEqual(startingHP - 130, vespierce.CurrentHP);
            Assert.AreEqual(firstAttackerHP - 20, firstAttacker.CurrentHP,
                "The same attacker must respect the 0.6 second reflection cooldown.");

            Assert.IsTrue(vespierce.TryTakeAttackDamage(100, DamageType.True, secondAttacker));
            Assert.AreEqual(secondAttackerHP - 20, secondAttacker.CurrentHP,
                "A different attacker has an independent reflection cooldown.");
        }

        [Test]
        public void PerfectParry_DoesNotReduceOrReflectAreaOrEnvironmentalDamage()
        {
            SetRarity(OperatorRarity.Star5);
            TankEnemy attacker = CreateEnemy<TankEnemy>(EnemyArchetype.Tank);
            int attackerHP = attacker.CurrentHP;
            int startingHP = vespierce.CurrentHP;

            Assert.IsTrue(vespierce.TryTakeAreaAttackDamage(100, DamageType.True, attacker));
            Assert.AreEqual(startingHP - 100, vespierce.CurrentHP);
            Assert.AreEqual(attackerHP, attacker.CurrentHP);

            Assert.IsTrue(vespierce.TakeTrueDamage(100));
            Assert.AreEqual(startingHP - 200, vespierce.CurrentHP);
            Assert.AreEqual(attackerHP, attacker.CurrentHP);
        }

        [Test]
        public void PerfectParry_IsLockedAtVespiercesBaseRarity()
        {
            TankEnemy attacker = CreateEnemy<TankEnemy>(EnemyArchetype.Tank);
            int startingHP = vespierce.CurrentHP;
            int attackerHP = attacker.CurrentHP;

            Assert.IsTrue(vespierce.TryTakeAttackDamage(100, DamageType.True, attacker));
            Assert.AreEqual(startingHP - 100, vespierce.CurrentHP);
            Assert.AreEqual(attackerHP, attacker.CurrentHP);
        }

        [Test]
        public void PhantomPursuit_UsesExitProximityAndAppliesIsolationMultiplierOnce()
        {
            GruntEnemy fartherEnemy = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            GruntEnemy closestEnemy = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            fartherEnemy.SetPath(new List<Vector3> { Vector3.zero, new Vector3(3f, 0f, 0f) });
            closestEnemy.SetPath(new List<Vector3> { Vector3.zero, new Vector3(2f, 0f, 0f) });
            typeof(EnemyBase).GetField("currentPathIndex", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(closestEnemy, 1);
            fartherEnemy.transform.position = Vector3.zero;
            closestEnemy.transform.position = new Vector3(1.9f, 0f, 0f);

            Assert.AreSame(closestEnemy,
                VespierceOperator.FindClosestToExitTarget(new EnemyBase[] { fartherEnemy, closestEnemy }));
            Assert.AreEqual(330, VespierceOperator.CalculatePhantomAttackPower(110, 2f, false));
            Assert.AreEqual(660, VespierceOperator.CalculatePhantomAttackPower(110, 2f, true),
                "The existing 2x isolation multiplier must only be applied once.");
        }

        [Test]
        public void PhantomPursuit_CounterPersistsAcrossTargetsAndResetsEveryThirdSuccessfulHit()
        {
            SetRarity(OperatorRarity.Star4);
            TankEnemy attacker = CreateEnemy<TankEnemy>(EnemyArchetype.Tank);
            int startingHP = vespierce.CurrentHP;
            int attackerHP = attacker.CurrentHP;
            // Perfect Parry is the 2nd upgrade (5 stars); the 1st upgrade (4 stars) is the phantom, so a direct hit lands in full
            Assert.IsTrue(vespierce.TryTakeAttackDamage(100, DamageType.True, attacker));
            Assert.AreEqual(startingHP - 100, vespierce.CurrentHP);
            Assert.AreEqual(attackerHP, attacker.CurrentHP,
                "Nothing is reflected before the 2nd upgrade's Perfect Parry.");

            GruntEnemy firstTarget = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            GruntEnemy secondTarget = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            MethodInfo applyAttackDamage = typeof(VespierceOperator).GetMethod(
                "ApplyAttackDamage",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(applyAttackDamage);

            applyAttackDamage.Invoke(vespierce, new object[] { firstTarget, 1 });
            Assert.AreEqual(1, vespierce.SuccessfulNormalAttackCount);
            applyAttackDamage.Invoke(vespierce, new object[] { secondTarget, 1 });
            Assert.AreEqual(2, vespierce.SuccessfulNormalAttackCount);
            applyAttackDamage.Invoke(vespierce, new object[] { secondTarget, 1 });
            Assert.AreEqual(0, vespierce.SuccessfulNormalAttackCount);
        }

        [Test]
        public void FlyingEnemy_CannotTargetUnblockedVespierce()
        {
            RangedFlyingEnemy attacker = CreateEnemy<RangedFlyingEnemy>(EnemyArchetype.Flyer);

            Assert.IsFalse(vespierce.CanBeTargetedByEnemy(attacker));
        }

        [Test]
        public void CasterTargetSelection_IgnoresVespierceUntilBlocked()
        {
            AskalaserEnemy attacker = CreateEnemy<AskalaserEnemy>(EnemyArchetype.Caster);

            Assert.IsNull(attacker.FindState1Target());
            Assert.IsTrue(vespierce.TryBlock(attacker));
            Assert.AreSame(vespierce, attacker.FindState1Target());
        }

        [Test]
        public void HextensionBindTargetSelection_IgnoresVespierceUntilBlocked()
        {
            HextensionEnemy attacker = CreateEnemy<HextensionEnemy>(EnemyArchetype.Caster);
            attacker.transform.position = gridManager.GridToWorldPosition(1, 0);

            Assert.IsNull(attacker.FindBindTarget());
            Assert.IsTrue(vespierce.TryBlock(attacker));
            Assert.AreSame(vespierce, attacker.FindBindTarget());
        }

        [Test]
        public void RoboflameConeAttack_IgnoresVespierceUntilBlocked()
        {
            RoboflameEnemy attacker = CreateEnemy<RoboflameEnemy>(EnemyArchetype.Caster);
            attacker.SetGridManager(gridManager);
            attacker.transform.position = gridManager.GridToWorldPosition(0, 0);

            Assert.IsEmpty(attacker.GetOperatorsInCone(Vector2Int.right));
            Assert.IsTrue(vespierce.TryBlock(attacker));
            CollectionAssert.Contains(attacker.GetOperatorsInCone(Vector2Int.right), vespierce);
        }

        [Test]
        public void ExplodingBugDeathBlast_DamagesVespierce()
        {
            ExplodingBugEnemy attacker = CreateEnemy<ExplodingBugEnemy>(EnemyArchetype.Rusher);
            attacker.transform.position = gridManager.GridToWorldPosition(1, 0);
            int startingHP = vespierce.CurrentHP;

            TestUtility.ExpectEditModeDestroy();
            attacker.TakeDamage(1000, DamageType.True);

            Assert.Less(vespierce.CurrentHP, startingHP);
        }

        [Test]
        public void AttackAnimation_UsesRequestedFrameTimingAndTotalDuration()
        {
            float[] expectedDurations =
            {
                0.08f, 0.05f, 0.05f, 0.07f, 0.10f, 0.03f, 0.03f,
                0.04f, 0.05f, 0.09f, 0.07f, 0.04f, 0.06f, 0.09f
            };

            Assert.AreEqual(14, VespierceSpriteAnimation.AttackFrameCount);
            Assert.AreEqual(8, VespierceSpriteAnimation.ImpactFrameIndex);
            Assert.AreEqual(0.45f, VespierceSpriteAnimation.WindupSeconds, 0.0001f);
            Assert.AreEqual(0.85f, VespierceSpriteAnimation.AttackDuration, 0.0001f);
            for (int i = 0; i < expectedDurations.Length; i++)
                Assert.AreEqual(expectedDurations[i], VespierceSpriteAnimation.GetAttackFrameDuration(i), 0.0001f);
        }

        [Test]
        public void AttackAnimation_StartsWhenTargetEntersRangeAfterAttackIsReady()
        {
            const string prefabPath = "Assets/_Project/Prefabs/Operators/Prefab_OP_Vespierce.prefab";
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.IsNotNull(prefab);

            VespierceSpriteAnimation prefabAnimation = prefab.GetComponent<VespierceSpriteAnimation>();
            Assert.IsNotNull(prefabAnimation);
            vespierceObject.AddComponent<SpriteRenderer>();
            VespierceSpriteAnimation animation = vespierceObject.AddComponent<VespierceSpriteAnimation>();
            animation.Configure(prefabAnimation.Frames, prefabAnimation.AttackFrames);
            TestUtility.CallAwake(animation);
            Assert.IsTrue(animation.HasAttackAnimation);

            GruntEnemy attacker = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            Assert.IsTrue(vespierce.TryBlock(attacker));

            float attackInterval = vespierce.GetAttackInterval();
            typeof(OperatorBase).GetField("attackTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(vespierce, attackInterval);
            typeof(VespierceOperator).GetMethod("UpdateAttackWindup", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(vespierce, null);

            Assert.IsTrue(animation.IsWindingUp);
            Assert.AreEqual(attackInterval - VespierceSpriteAnimation.WindupSeconds,
                (float)typeof(OperatorBase).GetField("attackTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(vespierce),
                0.0001f);
        }

        [TestCase(OperatorFacing.Left, -1f)]
        [TestCase(OperatorFacing.Right, 1f)]
        public void AttackProjectile_TravelsFixedDistanceInFacingDirectionWithoutHoming(
            OperatorFacing facing,
            float expectedDirection)
        {
            vespierceData.projectileOrigin = new Vector2(-0.34f, 0.28f);
            vespierce.SetFacing(facing);
            GruntEnemy attacker = CreateEnemy<GruntEnemy>(EnemyArchetype.Grunt);
            attacker.transform.position = vespierce.transform.position + new Vector3(3f, 2f, 0f);

            var existingProjectiles = new HashSet<CombatProjectileVisual>(
                Object.FindObjectsByType<CombatProjectileVisual>());
            vespierce.Attack(attacker);

            CombatProjectileVisual[] projectiles = Object.FindObjectsByType<CombatProjectileVisual>();
            CombatProjectileVisual attackProjectile = null;
            foreach (CombatProjectileVisual projectile in projectiles)
            {
                if (!existingProjectiles.Contains(projectile))
                {
                    attackProjectile = projectile;
                    break;
                }
            }

            Assert.IsNotNull(attackProjectile);
            try
            {
                var targetEnemyField = typeof(CombatProjectileVisual).GetField(
                    "targetEnemy",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var targetPositionField = typeof(CombatProjectileVisual).GetField(
                    "targetPosition",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(targetEnemyField);
                Assert.IsNotNull(targetPositionField);
                Assert.IsNull(targetEnemyField.GetValue(attackProjectile));

                Vector3 expectedStart = vespierce.transform.position + Vector3.down * (2f / 32f);
                Vector3 expectedEnd = expectedStart + Vector3.right * expectedDirection;
                Assert.AreEqual(expectedStart, attackProjectile.transform.position);
                Assert.AreEqual(expectedEnd, (Vector3)targetPositionField.GetValue(attackProjectile));
                Assert.AreEqual(expectedStart.y, expectedEnd.y);
                Assert.AreNotEqual(attacker.VisualPosition, expectedEnd);
            }
            finally
            {
                Object.DestroyImmediate(attackProjectile.gameObject);
            }
        }

        private TEnemy CreateEnemy<TEnemy>(EnemyArchetype archetype) where TEnemy : EnemyBase
        {
            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = archetype.ToString();
            data.archetype = archetype;
            data.baseHP = 100;
            data.baseATK = 20;
            data.baseDEF = 0;
            data.baseRES = 0;
            enemyDataAssets.Add(data);

            var enemyObject = new GameObject($"Test{archetype}");
            enemyObjects.Add(enemyObject);
            TEnemy enemy = enemyObject.AddComponent<TEnemy>();
            enemy.Initialize(data, 1);
            return enemy;
        }

        private void SetRarity(OperatorRarity rarity)
        {
            typeof(OperatorBase).GetField("currentRarity", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(vespierce, rarity);
        }
    }
}
