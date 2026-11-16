using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Core.Grid;
using TrashTD.Core.Pathfinding;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Tests
{
    // Deployment tests: facing, drag and release, undeploy and the traffic environment flag.
    public partial class Stage5AndHoleTileTests
    {
        [Test]
        public void CalculateFacingFromDisplacement_ResolvesCardinalDirectionsAccurately()
        {
            // Zero displacement returns fallback
            Assert.AreEqual(OperatorFacing.Right, StageBootstrapper.CalculateFacingFromDisplacement(Vector2.zero, OperatorFacing.Right));
            Assert.AreEqual(OperatorFacing.Down, StageBootstrapper.CalculateFacingFromDisplacement(Vector2.zero, OperatorFacing.Down));

            // Positive X dominant => Right
            Assert.AreEqual(OperatorFacing.Right, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(10f, 0f)));
            Assert.AreEqual(OperatorFacing.Right, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(10f, 5f)));
            Assert.AreEqual(OperatorFacing.Right, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(10f, -5f)));

            // Negative X dominant => Left
            Assert.AreEqual(OperatorFacing.Left, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(-10f, 0f)));
            Assert.AreEqual(OperatorFacing.Left, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(-10f, 5f)));
            Assert.AreEqual(OperatorFacing.Left, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(-10f, -5f)));

            // Positive Y dominant => Up
            Assert.AreEqual(OperatorFacing.Up, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(0f, 10f)));
            Assert.AreEqual(OperatorFacing.Up, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(5f, 10f)));
            Assert.AreEqual(OperatorFacing.Up, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(-5f, 10f)));

            // Negative Y dominant => Down
            Assert.AreEqual(OperatorFacing.Down, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(0f, -10f)));
            Assert.AreEqual(OperatorFacing.Down, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(5f, -10f)));
            Assert.AreEqual(OperatorFacing.Down, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(-5f, -10f)));

            // Diagonal ties (|dx| >= |dy|)
            Assert.AreEqual(OperatorFacing.Right, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(10f, 10f)));
            Assert.AreEqual(OperatorFacing.Left, StageBootstrapper.CalculateFacingFromDisplacement(new Vector2(-10f, 10f)));
        }

        [Test]
        public void ArknightsDeployment_DragAndReleaseSweep_TracksFacingAndDeploysCorrectly()
        {
            var bootObj = new GameObject("Bootstrapper");
            var bootstrapper = bootObj.AddComponent<StageBootstrapper>();

            var gmObj = new GameObject("GM");
            var gm = TestUtility.AddAwakened<GameManager>(gmObj);
            typeof(GameManager).GetField("currentPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gm, StagePhase.Preparation);
            typeof(GameManager).GetField("currentState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gm, GamePlayState.Playing);

            var opMgrObj = new GameObject("OpManager");
            var opMgr = TestUtility.AddAwakened<OperatorManager>(opMgrObj);

            var deckObj = new GameObject("Deck");
            var deck = TestUtility.AddAwakened<PlayerDeck>(deckObj);

            var gridObj2 = new GameObject("GridManager2");
            var gmGrid = gridObj2.AddComponent<GridManager>();
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.gridWidth = 5;
            stage.gridHeight = 5;
            stage.tileLayout = new TileType[25]; // all LowGround
            gmGrid.InitializeFromStageData(stage);
            opMgr.Initialize(gmGrid);

            bootstrapper.gridManager = gmGrid;
            bootstrapper.gameManager = gm;
            bootstrapper.operatorManager = opMgr;
            bootstrapper.playerDeck = deck;

            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "TestGroundOp";
            opData.operatorClass = OperatorClass.Guard;
            opData.position = OperatorPosition.Melee;
            opData.rangePattern = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };
            var card = new DraftCard(opData, OperatorRarity.Star1);
            deck.AddCard(card);

            try
            {
                // Step 1: Click & drag card to desired tile -> preview sits at (2, 2)
                var cellPos = new Vector2Int(2, 2);
                bootstrapper.BeginOperatorPlacement(card, cellPos);

                Assert.IsTrue(bootstrapper.IsPlacementPreviewActive);
                Assert.IsFalse(bootstrapper.IsDraggingToDeploy, "Step 1 should place preview without immediately dragging direction.");
                Assert.AreEqual(cellPos, bootstrapper.PlacementGridPosition);
                Assert.AreEqual(OperatorFacing.Right, bootstrapper.PlacementFacing);

                Vector3 cellWorld = gmGrid.GetCell(cellPos).WorldPosition;

                // Step 2: Click and hold where operator is to drag facing direction
                // Drag Up: displacement (0, +1)
                bootstrapper.UpdateOperatorPlacementDrag(card, cellPos, cellWorld + new Vector3(0f, 1f, 0f));
                Assert.AreEqual(OperatorFacing.Up, bootstrapper.PlacementFacing);

                // Drag Left: displacement (-1, 0)
                bootstrapper.UpdateOperatorPlacementDrag(card, cellPos, cellWorld + new Vector3(-1f, 0f, 0f));
                Assert.AreEqual(OperatorFacing.Left, bootstrapper.PlacementFacing);

                // Drag Down: displacement (0, -1)
                bootstrapper.UpdateOperatorPlacementDrag(card, cellPos, cellWorld + new Vector3(0f, -1f, 0f));
                Assert.AreEqual(OperatorFacing.Down, bootstrapper.PlacementFacing);

                // Step 3: When you let go, it accepts automatically (simulates PointerUp)
                bootstrapper.ConfirmOperatorPlacement();

                Assert.IsFalse(bootstrapper.IsPlacementPreviewActive);
                Assert.IsNull(bootstrapper.PendingDeployCard);
                Assert.AreEqual(0, deck.CardCount, "Card should have been consumed from deck upon confirmation.");

                // Verify operator deployed in cell with Down facing
                var cell = gmGrid.GetCell(cellPos);
                Assert.IsNotNull(cell.OccupantOperator);
                var deployed = cell.OccupantOperator.GetComponent<OperatorBase>();
                Assert.IsNotNull(deployed);
                Assert.AreEqual(OperatorFacing.Down, deployed.Facing);
            }
            finally
            {
                TestUtility.ClearSingletons();
                Object.DestroyImmediate(opData);
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(bootObj);
                Object.DestroyImmediate(gmObj);
                Object.DestroyImmediate(opMgrObj);
                Object.DestroyImmediate(deckObj);
                Object.DestroyImmediate(gridObj2);
            }
        }

        [Test]
        public void OperatorUndeploy_PreparationRefundAndCooldownRules()
        {
            var gmObj = new GameObject("GM");
            var gm = TestUtility.AddAwakened<GameManager>(gmObj);
            typeof(GameManager).GetField("currentPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gm, StagePhase.Preparation);
            typeof(GameManager).GetField("currentState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gm, GamePlayState.Playing);

            var opMgrObj = new GameObject("OpManager");
            var opMgr = TestUtility.AddAwakened<OperatorManager>(opMgrObj);

            var deckObj = new GameObject("Deck");
            var deck = TestUtility.AddAwakened<PlayerDeck>(deckObj);

            var gridObj = new GameObject("GridManager");
            var grid = gridObj.AddComponent<GridManager>();
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.gridWidth = 5;
            stage.gridHeight = 5;
            stage.tileLayout = new TileType[25];
            grid.InitializeFromStageData(stage);
            opMgr.Initialize(grid);

            // Sniper has 1 round cooldown on retreat
            var sniperData = ScriptableObject.CreateInstance<OperatorData>();
            sniperData.operatorName = "TestSniper";
            sniperData.operatorClass = OperatorClass.Sniper;
            sniperData.position = OperatorPosition.Ranged;
            sniperData.rangePattern = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };

            try
            {
                // CASE 1: Deployed in Preparation, Undeployed in same Preparation -> 0 cooldown
                var card1 = new DraftCard(sniperData, OperatorRarity.Star1);
                deck.AddCard(card1);
                Assert.IsTrue(opMgr.TryDeployOperator(card1, new Vector2Int(1, 1), OperatorFacing.Right, out var deployed1));
                deck.RemoveCard(0);

                Assert.IsTrue(deployed1.DeployedDuringCurrentPreparation);
                Assert.IsFalse(deployed1.HasFoughtInWave);

                opMgr.RetreatOperator(deployed1);
                Assert.AreEqual(1, deck.CardCount);
                Assert.AreEqual(0, deck.GetCard(0).cooldownRoundsRemaining, "Retreated during same preparation phase without fighting must have 0 cooldown.");
                deck.RemoveCard(0);

                // CASE 2: Deployed in Preparation, Wave starts (WaveActive), undeployed during Wave -> gets cooldown
                var card2 = new DraftCard(sniperData, OperatorRarity.Star1);
                deck.AddCard(card2);
                Assert.IsTrue(opMgr.TryDeployOperator(card2, new Vector2Int(1, 1), OperatorFacing.Right, out var deployed2));
                deck.RemoveCard(0);

                // Simulate wave start
                gm.EnterWavePhase();
                Assert.IsTrue(deployed2.HasFoughtInWave);
                Assert.IsFalse(deployed2.DeployedDuringCurrentPreparation);

                opMgr.RetreatOperator(deployed2);
                Assert.AreEqual(1, deck.CardCount);
                Assert.AreEqual(1, deck.GetCard(0).cooldownRoundsRemaining, "Retreated during wave phase must incur class cooldown.");
                deck.RemoveCard(0);

                // CASE 3: Operator survived previous wave, now in new Preparation phase. Undeploying gets cooldown!
                var card3 = new DraftCard(sniperData, OperatorRarity.Star1);
                deck.AddCard(card3);
                Assert.IsTrue(opMgr.TryDeployOperator(card3, new Vector2Int(1, 1), OperatorFacing.Right, out var deployed3));
                deck.RemoveCard(0);

                // Survived wave
                opMgr.MarkAllDeployedOperatorsAsFought();

                // Now transition to new preparation phase
                gm.EnterPreparationPhase();
                Assert.IsTrue(deployed3.HasFoughtInWave, "Veteran operator must be marked as having fought.");
                Assert.IsFalse(deployed3.DeployedDuringCurrentPreparation);

                // Undeploy veteran during preparation
                opMgr.RetreatOperator(deployed3);
                Assert.AreEqual(1, deck.CardCount);
                Assert.AreEqual(1, deck.GetCard(0).cooldownRoundsRemaining, "Operator who already fought on a previous round must incur cooldown even if retreated during preparation.");
                deck.RemoveCard(0);

                // CASE 4: New operator deployed during that same preparation phase, undeployed in that preparation phase -> 0 cooldown
                var card4 = new DraftCard(sniperData, OperatorRarity.Star1);
                deck.AddCard(card4);
                Assert.IsTrue(opMgr.TryDeployOperator(card4, new Vector2Int(2, 2), OperatorFacing.Right, out var deployed4));
                deck.RemoveCard(0);

                Assert.IsTrue(deployed4.DeployedDuringCurrentPreparation);
                Assert.IsFalse(deployed4.HasFoughtInWave);

                opMgr.RetreatOperator(deployed4);
                Assert.AreEqual(1, deck.CardCount);
                Assert.AreEqual(0, deck.GetCard(0).cooldownRoundsRemaining, "Fresh operator deployed and undeployed in preparation phase must have 0 cooldown.");
            }
            finally
            {
                TestUtility.ClearSingletons();
                Object.DestroyImmediate(sniperData);
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(gmObj);
                Object.DestroyImmediate(opMgrObj);
                Object.DestroyImmediate(deckObj);
                Object.DestroyImmediate(gridObj);
            }
        }

        [Test]
        public void RightClickKeybind_WhenOperatorAlreadyClicked_UndeploysOperator()
        {
            var bootObj = new GameObject("Bootstrapper");
            var bootstrapper = bootObj.AddComponent<StageBootstrapper>();

            var gmObj = new GameObject("GM");
            var gm = TestUtility.AddAwakened<GameManager>(gmObj);
            typeof(GameManager).GetField("currentPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gm, StagePhase.Preparation);
            typeof(GameManager).GetField("currentState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(gm, GamePlayState.Playing);

            var opMgrObj = new GameObject("OpManager");
            var opMgr = TestUtility.AddAwakened<OperatorManager>(opMgrObj);

            var deckObj = new GameObject("Deck");
            var deck = TestUtility.AddAwakened<PlayerDeck>(deckObj);

            var gridObj = new GameObject("GridManager");
            var grid = gridObj.AddComponent<GridManager>();
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.gridWidth = 5;
            stage.gridHeight = 5;
            stage.tileLayout = new TileType[25];
            grid.InitializeFromStageData(stage);
            opMgr.Initialize(grid);

            bootstrapper.gridManager = grid;
            bootstrapper.gameManager = gm;
            bootstrapper.operatorManager = opMgr;
            bootstrapper.playerDeck = deck;

            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "TestOp";
            opData.operatorClass = OperatorClass.Guard;
            opData.position = OperatorPosition.Melee;
            opData.rangePattern = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) };

            var card = new DraftCard(opData, OperatorRarity.Star1);
            deck.AddCard(card);

            try
            {
                // 1. Deploy operator
                Assert.IsTrue(opMgr.TryDeployOperator(card, new Vector2Int(1, 1), OperatorFacing.Right, out var deployedOp));
                deck.RemoveCard(0);
                Assert.AreEqual(0, deck.CardCount);

                // 2. Click/select operator
                Assert.IsTrue(opMgr.SelectOperator(deployedOp));
                Assert.AreEqual(deployedOp, opMgr.SelectedOperator);

                // 3. Right-click undeploy trigger (simulates right-click when operator already selected)
                bootstrapper.ClearOperatorRangePreview();
                opMgr.RetreatOperator(opMgr.SelectedOperator);

                // Operator should be undeployed and returned to deck, range preview cleared
                Assert.IsNull(opMgr.SelectedOperator);
                Assert.AreEqual(1, deck.CardCount);
                Assert.IsNull(grid.GetCell(new Vector2Int(1, 1)).OccupantOperator);
            }
            finally
            {
                TestUtility.ClearSingletons();
                Object.DestroyImmediate(opData);
                Object.DestroyImmediate(stage);
                Object.DestroyImmediate(bootObj);
                Object.DestroyImmediate(gmObj);
                Object.DestroyImmediate(opMgrObj);
                Object.DestroyImmediate(deckObj);
                Object.DestroyImmediate(gridObj);
            }
        }
         [Test]
        public void Stage5TrafficCar_CalculateDamage_MatchesExpectedRamp()
        {
            Assert.AreEqual(40, Stage5TrafficCar.CalculateDamage(0));
            Assert.AreEqual(60, Stage5TrafficCar.CalculateDamage(2));
            Assert.AreEqual(80, Stage5TrafficCar.CalculateDamage(4));
            Assert.AreEqual(100, Stage5TrafficCar.CalculateDamage(6));
            Assert.AreEqual(120, Stage5TrafficCar.CalculateDamage(8));
            Assert.AreEqual(140, Stage5TrafficCar.CalculateDamage(10));
            Assert.AreEqual(160, Stage5TrafficCar.CalculateDamage(12));
        }

        [Test]
        public void TrafficEnvironment_IsIdentifiedByTheStageData_NotByItsName()
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = "STAGE_05";
            stage.name = "STAGE_05";
            stage.gridWidth = 13;
            stage.gridHeight = 8;

            // Name and id alone no longer make a stage a traffic stage: the environment field does
            Assert.IsFalse(stage.HasTraffic);

            stage.environment = StageEnvironment.Traffic;
            Assert.IsTrue(stage.HasTraffic);

            stage.stageId = "STAGE_01";
            stage.environment = StageEnvironment.Weather;
            Assert.IsFalse(stage.HasTraffic);

            Object.DestroyImmediate(stage);
        }
    }
}
