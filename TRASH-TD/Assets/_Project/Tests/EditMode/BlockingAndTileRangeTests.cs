using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    public class BlockingAndTileRangeTests
    {
        private GameObject gridObject;
        private GridManager gridManager;
        private GameObject enemyManagerObject;
        private EnemyManager enemyManager;
        private StageData stageData;

        [SetUp]
        public void SetUp()
        {
            gridObject = new GameObject("TestGridManager");
            gridManager = gridObject.AddComponent<GridManager>();

            stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.gridWidth = 6;
            stageData.gridHeight = 3;
            stageData.tileLayout = new TileType[18];

            // Row 0: LowGround (0..5)
            for (int x = 0; x < 6; x++)
            {
                stageData.tileLayout[0 * 6 + x] = TileType.LowGround;
            }
            // Rows 1 and 2: HighGround (6..17)
            for (int y = 1; y < 3; y++)
            {
                for (int x = 0; x < 6; x++)
                {
                    stageData.tileLayout[y * 6 + x] = TileType.HighGround;
                }
            }

            gridManager.InitializeFromStageData(stageData);

            enemyManagerObject = new GameObject("TestEnemyManager");
            enemyManager = enemyManagerObject.AddComponent<EnemyManager>();
            enemyManager.Initialize(gridManager);
        }

        [TearDown]
        public void TearDown()
        {
            TestUtility.ClearSingletons();
            if (enemyManagerObject != null) Object.DestroyImmediate(enemyManagerObject);
            if (gridObject != null) Object.DestroyImmediate(gridObject);
            if (stageData != null) Object.DestroyImmediate(stageData);
        }

        private List<Vector3> CreateStraightPath()
        {
            var path = new List<Vector3>();
            for (int x = 0; x < 6; x++)
            {
                path.Add(gridManager.GridToWorldPosition(x, 0));
            }
            return path;
        }

        private (GameObject obj, GruntEnemy enemy) CreateEnemy(List<Vector3> path)
        {
            var enemyObj = new GameObject("TestEnemy");
            enemyObj.AddComponent<SpriteRenderer>();
            var enemy = enemyObj.AddComponent<GruntEnemy>();

            var data = ScriptableObject.CreateInstance<EnemyData>();
            data.enemyName = "Test Grunt";
            data.baseHP = 200;
            data.baseATK = 20;
            data.baseDEF = 5;
            data.baseRES = 0;
            data.moveSpeed = 1.0f;
            data.movementType = EnemyMovementType.Ground;
            data.archetype = EnemyArchetype.Grunt;
            data.attackInterval = 1.0f;

            enemy.Initialize(data, 0);
            enemy.SetGridManager(gridManager);
            enemy.SetPath(path);
            enemyManager.RegisterActiveEnemyForTesting(enemy);

            return (enemyObj, enemy);
        }

        private (GameObject obj, GuardOperator op) CreateGuardOperator(Vector2Int gridPos, OperatorFacing facing)
        {
            var opObj = new GameObject($"Guard_{gridPos.x}_{gridPos.y}");
            var op = opObj.AddComponent<GuardOperator>();

            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "TestGuard";
            opData.position = OperatorPosition.Melee;
            opData.baseHP = 300;
            opData.baseATK = 50;
            opData.baseDEF = 20;
            opData.blockCount = 2;
            opData.attackRange = 1;
            opData.attackInterval = 1.0f;
            opData.rangePattern = new[] { new Vector2Int(1, 0) };

            GridCell cell = gridManager.GetCell(gridPos);
            opObj.transform.position = gridManager.GridToWorldPosition(gridPos);
            op.Initialize(opData, OperatorRarity.Star1, facing);
            op.Deploy(cell);
            op.SetFacing(facing);

            return (opObj, op);
        }

        private (GameObject obj, DeadeyeOperator op) CreateDeadeyeOperator(Vector2Int gridPos, OperatorFacing facing)
        {
            var opObj = new GameObject($"Deadeye_{gridPos.x}_{gridPos.y}");
            var op = opObj.AddComponent<DeadeyeOperator>();

            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "Deadeye";
            opData.position = OperatorPosition.Ranged;
            opData.baseHP = 100;
            opData.baseATK = 75;
            opData.blockCount = 0;
            opData.attackRange = 3;
            opData.attackInterval = 0.75f;
            opData.rangePattern = new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(1, -1),
                new Vector2Int(1, 1),
            };

            GridCell cell = gridManager.GetCell(gridPos);
            opObj.transform.position = gridManager.GridToWorldPosition(gridPos);
            op.Initialize(opData, OperatorRarity.Star1, facing);
            op.Deploy(cell);
            op.SetFacing(facing);

            return (opObj, op);
        }

        private (GameObject obj, StaggerOperator op) CreateStaggerOperator(Vector2Int gridPos, OperatorFacing facing)
        {
            var opObj = new GameObject($"Stagger_{gridPos.x}_{gridPos.y}");
            var op = opObj.AddComponent<StaggerOperator>();

            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "Stag-ger";
            opData.position = OperatorPosition.Melee;
            opData.baseHP = 110;
            opData.baseATK = 75;
            opData.baseDEF = 22;
            opData.baseRES = 0;
            opData.blockCount = 2;
            opData.attackRange = 1;
            opData.attackInterval = 1.2f;
            opData.rangePattern = new[] { new Vector2Int(1, 0) };

            GridCell cell = gridManager.GetCell(gridPos);
            opObj.transform.position = gridManager.GridToWorldPosition(gridPos);
            op.Initialize(opData, OperatorRarity.Star2, facing);
            op.Deploy(cell);
            op.SetFacing(facing);

            return (opObj, op);
        }

        [Test]
        public void BlockedEnemy_HeldInFrontOfOperator_CanBeTargetedByBothNeighborAndBlocker()
        {
            // Path: (0,0) -> (1,0) -> (2,0) -> (3,0) -> (4,0) -> (5,0)
            // Enemy approaches tile 3 from tile 2.
            // Ground operator on tile 3 blocks the enemy in front (boundary between tile 2 and tile 3).
            var path = CreateStraightPath();
            var (enemyObj, enemy) = CreateEnemy(path);
            var (op2Obj, op2) = CreateGuardOperator(new Vector2Int(2, 0), OperatorFacing.Right);
            var (op3Obj, op3) = CreateGuardOperator(new Vector2Int(3, 0), OperatorFacing.Right);

            // Block enemy with operator on tile 3
            bool blocked = op3.TryBlock(enemy);
            Assert.IsTrue(blocked, "Operator on tile 3 should block enemy.");
            Assert.IsTrue(enemy.IsBlocked, "Enemy should be in blocked state.");
            Assert.AreEqual(new Vector2Int(2, 0), enemy.BlockedAdjacentGridPos, "Adjacent grid pos in front should be tile 2.");

            // Verify position is at the boundary in front of operator (between tile 2 and tile 3: x = 3.0)
            Assert.AreEqual(3.0f, enemy.transform.position.x, 0.05f);

            // Both tile 2 and tile 3 queries must include this blocked enemy
            GridCell cell2 = gridManager.GetCell(2, 0);
            GridCell cell3 = gridManager.GetCell(3, 0);

            var enemiesInCell2 = enemyManager.GetEnemiesInCells(new[] { cell2 });
            var enemiesInCell3 = enemyManager.GetEnemiesInCells(new[] { cell3 });

            CollectionAssert.Contains(enemiesInCell2, enemy, "Operator on tile 2 must be able to target the enemy held in front between 2 and 3.");
            CollectionAssert.Contains(enemiesInCell3, enemy, "Operator on tile 3 must be able to target the enemy held in front between 2 and 3.");

            // Cleanup
            Object.DestroyImmediate(enemyObj);
            Object.DestroyImmediate(op2Obj);
            Object.DestroyImmediate(op3Obj);
        }

        [Test]
        public void RangedOperatorOnTile8_FacingRight_CanHitEnemyBlockedInFrontBetweenTile2AndTile3()
        {
            // Scenario: Ranged operator on tile 8 (2, 1) facing Right has range covering column 3 (tile 3: (3, 0)).
            // Enemy approaches tile 3 from tile 2.
            // Ground operator on tile 3 blocks enemy in front at boundary (3.0f). Ranged operator on tile 8 must hit it.
            var path = CreateStraightPath();
            var (enemyObj, enemy) = CreateEnemy(path);
            var (op3Obj, op3) = CreateGuardOperator(new Vector2Int(3, 0), OperatorFacing.Right);
            var (op8Obj, op8) = CreateDeadeyeOperator(new Vector2Int(2, 1), OperatorFacing.Right);

            // Block enemy on tile 3
            op3.TryBlock(enemy);
            Assert.IsTrue(enemy.IsBlocked);

            // Get Deadeye's range cells: from (2, 1) facing right with offsets (1, 0), (1, -1), (1, 1)
            // Offset (1, -1) maps to (2 + 1, 1 - 1) = (3, 0) which is tile 3!
            var rangeCells = gridManager.GetCellsInRange(new Vector2Int(2, 1), op8.Data.rangePattern, OperatorFacing.Right);
            var enemiesInRange = enemyManager.GetEnemiesInCells(rangeCells, OperatorPosition.Ranged);

            CollectionAssert.Contains(enemiesInRange, enemy, "Ranged operator on tile 8 covering tile 3 must hit the enemy held between tile 2 and tile 3.");

            // Cleanup
            Object.DestroyImmediate(enemyObj);
            Object.DestroyImmediate(op3Obj);
            Object.DestroyImmediate(op8Obj);
        }

        [Test]
        public void UnblockedEnemy_TransitioningBetweenTiles_TargetableByBothTilesBeforeLeaving()
        {
            var path = CreateStraightPath();
            var (enemyObj, enemy) = CreateEnemy(path);

            GridCell cell1 = gridManager.GetCell(1, 0);
            GridCell cell2 = gridManager.GetCell(2, 0);
            GridCell cell3 = gridManager.GetCell(3, 0);

            // 1. Center of tile 2 (x = 2.5) -> Only tile 2 matches
            enemy.transform.position = gridManager.GridToWorldPosition(2, 0);
            Assert.IsTrue(enemyManager.GetEnemiesInCells(new[] { cell2 }).Contains(enemy));
            Assert.IsFalse(enemyManager.GetEnemiesInCells(new[] { cell1 }).Contains(enemy));
            Assert.IsFalse(enemyManager.GetEnemiesInCells(new[] { cell3 }).Contains(enemy));

            // 2. Boundary between tile 2 and tile 3 (x = 3.0) -> BOTH tile 2 and tile 3 match
            enemy.transform.position = new Vector3(3.0f, 0.5f, 0f);
            Assert.IsTrue(enemyManager.GetEnemiesInCells(new[] { cell2 }).Contains(enemy), "Tile 2 should still be able to attack enemy transitioning at boundary.");
            Assert.IsTrue(enemyManager.GetEnemiesInCells(new[] { cell3 }).Contains(enemy), "Tile 3 should already be able to attack enemy transitioning at boundary.");
            Assert.IsFalse(enemyManager.GetEnemiesInCells(new[] { cell1 }).Contains(enemy));

            // 3. Deep inside tile 3 (x = 3.5) -> Left tile 2, only tile 3 matches
            enemy.transform.position = gridManager.GridToWorldPosition(3, 0);
            Assert.IsFalse(enemyManager.GetEnemiesInCells(new[] { cell2 }).Contains(enemy), "Tile 2 should no longer target enemy once fully inside tile 3.");
            Assert.IsTrue(enemyManager.GetEnemiesInCells(new[] { cell3 }).Contains(enemy));

            // Cleanup
            Object.DestroyImmediate(enemyObj);
        }

        [Test]
        public void StaggerOperator_OnlyChecksOwnTileAndForwardRange_DoesNotCheckSidesOrBack()
        {
            // Stag-ger is an area brawler: his targets are the enemies on his own tile and on the tiles of his range ahead
            // (the old version only looked at enemies other operators had blocked). EnemyManager.Instance is what he asks.
            var instanceProperty = typeof(EnemyManager).GetProperty("Instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            instanceProperty.SetValue(null, enemyManager);

            var path = CreateStraightPath();
            // Stag-ger on (2, 0) facing Right: his range covers his own tile and (3, 0)
            var (stagObj, stagger) = CreateStaggerOperator(new Vector2Int(2, 0), OperatorFacing.Right);
            var (ownEnemyObj, ownEnemy) = CreateEnemy(path);
            var (frontEnemyObj, frontEnemy) = CreateEnemy(path);
            var (backEnemyObj, backEnemy) = CreateEnemy(path);
            var (farEnemyObj, farEnemy) = CreateEnemy(path);

            ownEnemy.transform.position = gridManager.GridToWorldPosition(2, 0);
            frontEnemy.transform.position = gridManager.GridToWorldPosition(3, 0);
            backEnemy.transform.position = gridManager.GridToWorldPosition(1, 0);
            farEnemy.transform.position = gridManager.GridToWorldPosition(4, 0);

            try
            {
                List<EnemyBase> targets = stagger.GetTargetsInRange();

                CollectionAssert.Contains(targets, ownEnemy, "Stag-ger should include an enemy on his own tile.");
                CollectionAssert.Contains(targets, frontEnemy, "Stag-ger should include an enemy in his forward range.");
                CollectionAssert.DoesNotContain(targets, backEnemy, "Stag-ger should NOT include an enemy on the tile behind him.");
                CollectionAssert.DoesNotContain(targets, farEnemy, "Stag-ger should NOT include an enemy beyond his range.");
            }
            finally
            {
                instanceProperty.SetValue(null, null);
                Object.DestroyImmediate(ownEnemyObj);
                Object.DestroyImmediate(frontEnemyObj);
                Object.DestroyImmediate(backEnemyObj);
                Object.DestroyImmediate(farEnemyObj);
                Object.DestroyImmediate(stagObj);
            }
        }

        [Test]
        public void Stagger_EnemyAttack_CanDodge_AndIncreasesNextAttackATKBy50Percent()
        {
            var path = CreateStraightPath();
            var (stagObj, stagger) = CreateStaggerOperator(new Vector2Int(2, 0), OperatorFacing.Right);
            var (enemyObj, enemy) = CreateEnemy(path);

            // 75 base ATK, scaled by the rank this helper deploys him at (2 stars: +15%)
            int baseAtk = stagger.CurrentATK;
            Assert.AreEqual(stagger.Data.GetScaledATK(OperatorRarity.Star2), baseAtk, "Stag-ger's ATK should be his base ATK scaled for his rank.");

            // 1. Force dodge to succeed (< 0.20)
            stagger.RandomValueProvider = () => 0.10f;
            int hpBefore = stagger.CurrentHP;

            stagger.TakeDamage(40, DamageType.Physical, attacker: enemy);

            Assert.AreEqual(hpBefore, stagger.CurrentHP, "Stag-ger should take 0 damage when dodge is successful.");
            Assert.IsTrue(stagger.HasDodgeAtkBuff, "Successful dodge should activate next-attack ATK buff.");

            // 2. Perform attack while buffed
            // The buff is +50% of his ATK, less the enemy's DEF.
            int enemyHPBefore = enemy.CurrentHP;
            stagger.Attack(enemy);

            int expectedDamage = Mathf.RoundToInt(baseAtk * 1.5f) - enemy.CurrentDEF;
            Assert.AreEqual(enemyHPBefore - expectedDamage, enemy.CurrentHP, "Empowered attack should deal +50% ATK damage.");
            Assert.IsFalse(stagger.HasDodgeAtkBuff, "Buff should be consumed after attacking.");

            // 3. Force dodge to fail (>= 0.20)
            stagger.RandomValueProvider = () => 0.50f;
            stagger.TakeDamage(40, DamageType.Physical, attacker: enemy);

            Assert.Less(stagger.CurrentHP, hpBefore, "Failed dodge should cause Stag-ger to take normal damage.");
            Assert.IsFalse(stagger.HasDodgeAtkBuff, "Failed dodge should not grant ATK buff.");

            // Cleanup
            Object.DestroyImmediate(enemyObj);
            Object.DestroyImmediate(stagObj);
        }

        [Test]
        public void Stagger_EnvironmentalDamage_AcidRainAcidTileOverheat_CannotDodge()
        {
            var (stagObj, stagger) = CreateStaggerOperator(new Vector2Int(2, 0), OperatorFacing.Right);

            // Even if random value is 0 (guaranteed dodge if dodgeable), environmental damage cannot be dodged
            stagger.RandomValueProvider = () => 0.0f;
            int maxHP = stagger.MaxHP;

            // 1. Stage 4 Overheat (TakeDamage with attacker == null)
            stagger.TakeDamage(10, DamageType.Arts, attacker: null);
            Assert.Less(stagger.CurrentHP, maxHP, "Stage 4 overheat burn cannot be dodged.");
            Assert.IsFalse(stagger.HasDodgeAtkBuff);

            int hpAfterOverheat = stagger.CurrentHP;

            // 2. Acid Tile / Acid Rain (TakeTrueDamage with ignoreShieldNegation == false)
            stagger.TakeTrueDamage(10, ignoreShieldNegation: false);
            Assert.AreEqual(hpAfterOverheat - 10, stagger.CurrentHP, "Acid tile / Acid rain cannot be dodged.");
            Assert.IsFalse(stagger.HasDodgeAtkBuff);

            // Cleanup
            Object.DestroyImmediate(stagObj);
        }

        [Test]
        public void Stagger_Stage5TrafficCar_CanDodge_AndPreventsStun()
        {
            var (stagObj, stagger) = CreateStaggerOperator(new Vector2Int(2, 0), OperatorFacing.Right);

            // 1. Stage 5 Vehicle (TakeTrueDamage with ignoreShieldNegation == true)
            // Roll succeeds (< 0.20)
            stagger.RandomValueProvider = () => 0.05f;
            int hpBefore = stagger.CurrentHP;

            bool damageDealt = stagger.TakeTrueDamage(100, ignoreShieldNegation: true);

            Assert.IsFalse(damageDealt, "Dodging Stage 5 vehicle should return false (no damage dealt).");
            Assert.AreEqual(hpBefore, stagger.CurrentHP, "Stag-ger should take 0 damage when dodging Stage 5 traffic car.");
            Assert.IsTrue(stagger.HasDodgeAtkBuff, "Dodging Stage 5 vehicle should grant next-attack ATK buff.");
            Assert.IsFalse(stagger.IsStunned, "Dodging vehicle prevents stun.");

            // 2. Roll fails (>= 0.20)
            stagger.RandomValueProvider = () => 0.80f;
            damageDealt = stagger.TakeTrueDamage(30, ignoreShieldNegation: true);

            Assert.IsTrue(damageDealt, "Failed dodge should deal vehicle true damage.");
            Assert.AreEqual(hpBefore - 30, stagger.CurrentHP);

            // Cleanup
            Object.DestroyImmediate(stagObj);
        }
    }
}



