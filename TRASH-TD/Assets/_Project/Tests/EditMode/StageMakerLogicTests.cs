using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Editor;

namespace TrashTD.Tests
{
    /// <summary>The rules behind the Stage Maker window (StageMakerLogic): placing tiles while keeping spawn/exit numbers, routes and waves consistent, resizing and the checks.</summary>
    public class StageMakerLogicTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private StageData NewStage(int width, int height, TileType fill = TileType.HighGround)
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            created.Add(stage);
            StageMakerLogic.CreateBlank(stage, width, height, fill);
            return stage;
        }

        private static WaveData WaveWith(params int[] spawnIndexes)
        {
            return new WaveData { entries = spawnIndexes.Select(i => new WaveEntry { spawnPointIndex = i }).ToArray() };
        }

        private static bool HasError(IEnumerable<StageIssue> issues, string containing)
        {
            return issues.Any(i => i.severity == StageIssueSeverity.Error && i.message.Contains(containing));
        }

        // ---------- placing tiles ----------

        [Test]
        public void SetTile_SpawnsAndExits_AreAppendedToTheirLists()
        {
            StageData stage = NewStage(6, 3);
            Assert.IsTrue(StageMakerLogic.SetTile(stage, new Vector2Int(0, 2), TileType.SpawnPoint));
            Assert.IsTrue(StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint));
            Assert.IsTrue(StageMakerLogic.SetTile(stage, new Vector2Int(5, 1), TileType.ExitPoint));

            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 2), new Vector2Int(0, 0) }, stage.spawnPoints, "A new spawn goes at the end: waves refer to spawns by number.");
            CollectionAssert.AreEqual(new[] { new Vector2Int(5, 1) }, stage.exitPoints);
            Assert.AreEqual(TileType.SpawnPoint, stage.GetTile(0, 2));
        }

        [Test]
        public void SetTile_SameTileAgain_ChangesNothing()
        {
            StageData stage = NewStage(4, 3);
            Assert.IsTrue(StageMakerLogic.SetTile(stage, new Vector2Int(1, 1), TileType.LowGround));
            Assert.IsFalse(StageMakerLogic.SetTile(stage, new Vector2Int(1, 1), TileType.LowGround));
            Assert.IsTrue(StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint));
            Assert.IsFalse(StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint));
            Assert.AreEqual(1, stage.spawnPoints.Length, "Placing a spawn on a spawn must not list it twice.");
        }

        [Test]
        public void SetTile_OutsideTheGrid_IsIgnored()
        {
            StageData stage = NewStage(4, 3);
            Assert.IsFalse(StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.LowGround));
            Assert.IsFalse(StageMakerLogic.SetTile(stage, new Vector2Int(0, -1), TileType.LowGround));
        }

        [Test]
        public void SetTile_PaintingOverASpawn_RemovesItAndRenumbersRoutesAndWaves()
        {
            StageData stage = NewStage(6, 3);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint); // S0
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint); // S1
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 2), TileType.SpawnPoint); // S2
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 1), TileType.ExitPoint);
            stage.enemyPaths = new[]
            {
                new PathData { spawnPointIndex = 0, waypoints = new[] { new Vector2Int(0, 0), new Vector2Int(1, 0) } },
                new PathData { spawnPointIndex = 2, waypoints = new[] { new Vector2Int(0, 2), new Vector2Int(1, 2) } }
            };
            stage.wavesNormal = new[] { WaveWith(0, 1, 2) };

            Assert.IsTrue(StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.LowGround));

            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 1), new Vector2Int(0, 2) }, stage.spawnPoints);
            Assert.AreEqual(1, stage.enemyPaths.Length, "The route of the removed spawn goes with it.");
            Assert.AreEqual(1, stage.enemyPaths[0].spawnPointIndex, "S2 is S1 now.");
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, stage.wavesNormal[0].entries.Select(e => e.spawnPointIndex).ToArray(),
                "Entries on the removed spawn move to the spawn that took its number; later ones shift down.");
        }

        [Test]
        public void SetTile_PaintingOverAnExit_RemovesItAndRenumbersRoutes()
        {
            StageData stage = NewStage(6, 3);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 0), TileType.ExitPoint); // E0
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 2), TileType.ExitPoint); // E1
            stage.enemyPaths = new[] { new PathData { spawnPointIndex = 0, exitPointIndex = 1, waypoints = new Vector2Int[0] } };

            StageMakerLogic.SetTile(stage, new Vector2Int(5, 0), TileType.Blocked);

            CollectionAssert.AreEqual(new[] { new Vector2Int(5, 2) }, stage.exitPoints);
            Assert.AreEqual(0, stage.enemyPaths[0].exitPointIndex, "E1 is E0 now.");
        }

        [Test]
        public void GetRemovalImpact_CountsWhatWouldShift()
        {
            StageData stage = NewStage(6, 3);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint);
            stage.wavesEasy = new[] { WaveWith(0, 0, 1) };
            stage.wavesHard = new[] { WaveWith(0) };

            PointRemovalImpact impact = StageMakerLogic.GetRemovalImpact(stage, new Vector2Int(0, 0), TileType.LowGround);
            Assert.IsTrue(impact.exists);
            Assert.IsTrue(impact.NeedsConfirm);
            Assert.AreEqual(1, impact.laterPoints);
            Assert.AreEqual(3, impact.waveEntries, "Entries on S0 in every difficulty.");

            Assert.IsFalse(StageMakerLogic.GetRemovalImpact(stage, new Vector2Int(3, 1), TileType.LowGround).exists, "A plain tile removes nothing.");
            Assert.IsFalse(StageMakerLogic.GetRemovalImpact(stage, new Vector2Int(0, 0), TileType.SpawnPoint).exists, "Painting a spawn over itself removes nothing.");
        }

        [Test]
        public void MovePoint_KeepsTheNumber()
        {
            StageData stage = NewStage(6, 3);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 1), TileType.ExitPoint);

            Assert.IsTrue(StageMakerLogic.MovePoint(stage, true, 0, new Vector2Int(3, 2)));
            Assert.AreEqual(new Vector2Int(3, 2), stage.spawnPoints[0], "S0 is still S0, so waves keep working.");
            Assert.AreEqual(new Vector2Int(0, 1), stage.spawnPoints[1]);
            Assert.AreEqual(TileType.SpawnPoint, stage.GetTile(3, 2));
            Assert.AreEqual(TileType.LowGround, stage.GetTile(0, 0), "The old tile is plain ground again.");

            Assert.IsFalse(StageMakerLogic.MovePoint(stage, true, 0, new Vector2Int(5, 1)), "Cannot move onto an exit.");
            Assert.IsFalse(StageMakerLogic.MovePoint(stage, true, 0, new Vector2Int(0, 1)), "Cannot move onto another spawn.");
        }

        // ---------- sizes ----------

        [Test]
        public void Resize_KeepsTilesFromTheBottomLeft_AndFillsNewOnes()
        {
            StageData stage = NewStage(3, 2, TileType.Blocked);
            StageMakerLogic.SetTile(stage, new Vector2Int(2, 1), TileType.LowGround);

            StageMakerLogic.Resize(stage, 5, 4, TileType.HighGround);

            Assert.AreEqual(5, stage.gridWidth);
            Assert.AreEqual(4, stage.gridHeight);
            Assert.AreEqual(20, stage.tileLayout.Length);
            Assert.AreEqual(TileType.LowGround, stage.GetTile(2, 1), "Existing tiles stay where they are.");
            Assert.AreEqual(TileType.Blocked, stage.GetTile(0, 0));
            Assert.AreEqual(TileType.HighGround, stage.GetTile(4, 3), "New tiles get the fill type.");
        }

        [Test]
        public void Resize_Smaller_DropsPointsAndRoutesOutside()
        {
            StageData stage = NewStage(5, 5);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.ExitPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 4), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(1, 1), TileType.SpawnPoint);
            stage.enemyPaths = new[]
            {
                new PathData { spawnPointIndex = 1, waypoints = new[] { new Vector2Int(1, 1), new Vector2Int(1, 0), new Vector2Int(0, 0) } },
                new PathData { spawnPointIndex = 0, waypoints = new[] { new Vector2Int(4, 4), new Vector2Int(3, 4) } }
            };
            stage.wavesNormal = new[] { WaveWith(0, 1) };

            string notes = StageMakerLogic.Resize(stage, 3, 3, TileType.HighGround);

            Assert.IsNotNull(notes);
            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 1) }, stage.spawnPoints, "The spawn outside is gone and the other is S0 now.");
            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 0) }, stage.exitPoints);
            Assert.AreEqual(9, stage.tileLayout.Length);
            Assert.AreEqual(1, stage.enemyPaths.Length);
            Assert.AreEqual(0, stage.enemyPaths[0].spawnPointIndex);
            Assert.AreEqual(3, stage.enemyPaths[0].waypoints.Length, "A route that stays inside is untouched.");
        }

        [Test]
        public void Resize_RouteThatLeavesTheGrid_GoesBackToAutomatic()
        {
            StageData stage = NewStage(5, 2, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(1, 0), TileType.ExitPoint);
            stage.enemyPaths = new[]
            {
                new PathData { spawnPointIndex = 0, waypoints = new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(4, 1), new Vector2Int(1, 0) } }
            };

            StageMakerLogic.Resize(stage, 3, 2, TileType.HighGround);

            Assert.AreEqual(StagePathMode.AutoNearestExit, StageMakerLogic.GetPathMode(stage, 0, out _));
        }

        [Test]
        public void CreateStarter_HasASpawn_AnExit_AndAWorkingRoute()
        {
            StageData stage = NewStage(1, 1);
            StageMakerLogic.CreateStarter(stage, 10, 5);

            Assert.AreEqual(1, stage.spawnPoints.Length);
            Assert.AreEqual(1, stage.exitPoints.Length);
            List<StageRoute> routes = StageMakerRouting.ComputeRoutes(stage);
            Assert.AreEqual(1, routes.Count);
            Assert.AreEqual(10, routes[0].cells.Count, "A straight lane across the grid.");
            Assert.IsFalse(StageMakerLogic.Validate(stage, routes).Any(i => i.severity == StageIssueSeverity.Error));
        }

        // ---------- routes ----------

        [Test]
        public void GetPathMode_FollowsTheGamesPrecedence()
        {
            StageData stage = NewStage(6, 3, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 0), TileType.ExitPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 2), TileType.ExitPoint);

            Assert.AreEqual(StagePathMode.AutoNearestExit, StageMakerLogic.GetPathMode(stage, 0, out _));

            var drawn = new PathData { spawnPointIndex = 0, waypoints = new[] { new Vector2Int(0, 1), new Vector2Int(1, 1) } };
            var toExit1 = new PathData { spawnPointIndex = 0, exitPointIndex = 1, waypoints = new Vector2Int[0] };

            stage.enemyPaths = new[] { drawn };
            Assert.AreEqual(StagePathMode.Custom, StageMakerLogic.GetPathMode(stage, 0, out _));

            stage.enemyPaths = new[] { drawn, toExit1 };
            Assert.AreEqual(StagePathMode.AutoSpecificExit, StageMakerLogic.GetPathMode(stage, 0, out int exit), "A later entry replaces an earlier one.");
            Assert.AreEqual(1, exit);

            stage.enemyPaths = new[] { toExit1, drawn };
            Assert.AreEqual(StagePathMode.Custom, StageMakerLogic.GetPathMode(stage, 0, out _));

            stage.enemyPaths = new[] { new PathData { spawnPointIndex = 0, exitPointIndex = 9, waypoints = new Vector2Int[0] } };
            Assert.AreEqual(StagePathMode.AutoNearestExit, StageMakerLogic.GetPathMode(stage, 0, out _), "An entry with no waypoints and a bad exit number does nothing.");
        }

        [Test]
        public void SetCustomWaypoints_ReplacesEarlierEntries_AndSetAutoRouteClearsThem()
        {
            StageData stage = NewStage(6, 3, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(5, 1), TileType.ExitPoint);
            var cells = Enumerable.Range(0, 6).Select(x => new Vector2Int(x, 1)).ToList();

            StageMakerLogic.SetCustomWaypoints(stage, 0, cells);
            StageMakerLogic.SetCustomWaypoints(stage, 0, cells);

            Assert.AreEqual(1, stage.enemyPaths.Length, "Redrawing a route does not pile up entries.");
            Assert.AreEqual(6, StageMakerLogic.GetCustomWaypoints(stage, 0).Count);
            Assert.AreEqual(0, stage.enemyPaths[0].exitPointIndex);

            StageMakerLogic.SetAutoRoute(stage, 0, 0);
            Assert.AreEqual(StagePathMode.AutoSpecificExit, StageMakerLogic.GetPathMode(stage, 0, out _));
            StageMakerLogic.SetAutoRoute(stage, 0, -1);
            Assert.AreEqual(0, stage.enemyPaths.Length);
        }

        [Test]
        public void ARouteWithAHandDrawnDetour_IsWhatTheGameWouldWalk()
        {
            StageData stage = NewStage(5, 3, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 1), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 1), TileType.ExitPoint);
            var detour = new List<Vector2Int>
            {
                new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(3, 2), new Vector2Int(4, 2), new Vector2Int(4, 1)
            };
            StageMakerLogic.SetCustomWaypoints(stage, 0, detour);

            List<StageRoute> routes = StageMakerRouting.ComputeRoutes(stage);

            Assert.AreEqual(1, routes.Count);
            Assert.IsTrue(routes[0].custom);
            CollectionAssert.AreEqual(detour, routes[0].cells);
            Assert.IsFalse(StageMakerLogic.Validate(stage, routes).Any(i => i.severity == StageIssueSeverity.Error || i.message.Contains("drawn route")));
        }

        // ---------- checks ----------

        [Test]
        public void Validate_NoSpawnOrExit_AreErrors()
        {
            StageData stage = NewStage(4, 3);
            List<StageIssue> issues = StageMakerLogic.Validate(stage, new List<StageRoute>());
            Assert.IsTrue(HasError(issues, "no spawn"));
            Assert.IsTrue(HasError(issues, "no exit"));
        }

        [Test]
        public void Validate_StageWithoutAGrid_OnlyAsksForOne()
        {
            var stage = ScriptableObject.CreateInstance<StageData>();
            created.Add(stage);
            stage.gridWidth = 0;
            stage.gridHeight = 0;
            List<StageIssue> issues = StageMakerLogic.Validate(stage, new List<StageRoute>());
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(StageIssueSeverity.Info, issues[0].severity);
        }

        [Test]
        public void Validate_WalledOffSpawn_HasNoRoute()
        {
            StageData stage = NewStage(5, 1, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(2, 0), TileType.Blocked);

            List<StageIssue> issues = StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage));

            Assert.IsTrue(HasError(issues, "S0 has no route"));
        }

        [Test]
        public void Validate_TilesAndListsOutOfStep_AreErrors_AndSyncFixesThem()
        {
            StageData stage = NewStage(5, 1, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);
            stage.tileLayout[2] = TileType.SpawnPoint;                     // a Spawn tile the list does not know
            stage.exitPoints = new[] { new Vector2Int(4, 0), new Vector2Int(1, 0) }; // an exit listed on a plain tile

            List<StageIssue> before = StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage));
            Assert.IsTrue(HasError(before, "is not in the spawn list"));
            Assert.IsTrue(HasError(before, "is not on a Exit tile"));

            StageMakerLogic.SyncSpawnsAndExits(stage);

            CollectionAssert.AreEqual(new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }, stage.spawnPoints, "Existing order is kept, the missing tile is appended.");
            CollectionAssert.AreEqual(new[] { new Vector2Int(4, 0) }, stage.exitPoints);
            Assert.IsFalse(StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage)).Any(i => i.severity == StageIssueSeverity.Error));
        }

        [Test]
        public void Validate_LayoutOfTheWrongLength_IsAnErrorWithARepair()
        {
            StageData stage = NewStage(4, 3);
            stage.tileLayout = new TileType[5];

            List<StageIssue> issues = StageMakerLogic.Validate(stage, new List<StageRoute>());
            Assert.IsTrue(issues.Any(i => i.severity == StageIssueSeverity.Error && i.fix == StageIssueFix.RepairLayout));

            StageMakerLogic.RepairLayout(stage, TileType.Blocked);
            Assert.AreEqual(12, stage.tileLayout.Length);
            Assert.IsTrue(StageMakerLogic.LayoutMatchesGrid(stage));
        }

        [Test]
        public void Validate_DrawnRouteOverABlockedTile_IsAnError()
        {
            StageData stage = NewStage(5, 1, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);
            StageMakerLogic.SetCustomWaypoints(stage, 0, Enumerable.Range(0, 5).Select(x => new Vector2Int(x, 0)).ToList());
            StageMakerLogic.SetTile(stage, new Vector2Int(2, 0), TileType.Blocked);

            List<StageIssue> issues = StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage));

            Assert.IsTrue(HasError(issues, "crosses a Blocked tile"));
        }

        [Test]
        public void Validate_DrawnRouteThatJumpsBetweenTwoHoles_IsFine_ButOtherJumpsAreNot()
        {
            StageData stage = NewStage(5, 2, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(1, 0), TileType.Hole);
            StageMakerLogic.SetTile(stage, new Vector2Int(3, 1), TileType.Hole);

            StageMakerLogic.SetCustomWaypoints(stage, 0, new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(4, 0)
            });
            Assert.IsFalse(StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage)).Any(i => i.severity == StageIssueSeverity.Error || i.message.Contains("drawn route")),
                "A hole teleports to the other hole, so this jump is legal.");

            StageMakerLogic.SetCustomWaypoints(stage, 0, new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(4, 0)
            });
            Assert.IsTrue(StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage)).Any(i => i.severity == StageIssueSeverity.Warning && i.message.Contains("jumps")));
        }

        [Test]
        public void Validate_WarnsAboutAnUnpairedHole_AndWavesUsingAMissingSpawn()
        {
            StageData stage = NewStage(5, 1, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(2, 0), TileType.Hole);
            stage.wavesNormal = new[] { WaveWith(0, 3) };

            List<StageIssue> issues = StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage));

            Assert.IsTrue(issues.Any(i => i.severity == StageIssueSeverity.Warning && i.message.Contains("Hole")));
            Assert.IsTrue(issues.Any(i => i.severity == StageIssueSeverity.Warning && i.message.Contains("S3")));
        }

        [Test]
        public void AcidLowGround_CarriesARoute_ButAcidHighGroundBlocksIt()
        {
            StageData stage = NewStage(5, 1, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);

            StageMakerLogic.SetTile(stage, new Vector2Int(2, 0), TileType.AcidLowGround);
            Assert.AreEqual(1, StageMakerRouting.ComputeRoutes(stage).Count, "Enemies walk through acid low ground.");

            StageMakerLogic.SetTile(stage, new Vector2Int(2, 0), TileType.AcidHighGround);
            Assert.AreEqual(0, StageMakerRouting.ComputeRoutes(stage).Count, "Acid high ground is high ground: not walkable.");
        }

        [Test]
        public void AcidHighGround_CountsAsHighGround_ForTheDeployCheck()
        {
            StageData stage = NewStage(5, 1, TileType.LowGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(0, 0), TileType.SpawnPoint);
            StageMakerLogic.SetTile(stage, new Vector2Int(4, 0), TileType.ExitPoint);
            Assert.IsTrue(StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage)).Any(i => i.message.Contains("no High ground")));

            StageMakerLogic.SetTile(stage, new Vector2Int(2, 0), TileType.AcidHighGround);
            StageMakerLogic.SetTile(stage, new Vector2Int(1, 0), TileType.AcidLowGround);
            List<StageIssue> issues = StageMakerLogic.Validate(stage, new List<StageRoute>());
            Assert.IsFalse(issues.Any(i => i.message.Contains("no High ground")), "Ranged operators can deploy on acid high ground.");
        }

        // ---------- the real stages ----------

        [Test]
        public void EveryBuiltStage_PassesTheChecks()
        {
            StageData[] all = Resources.LoadAll<StageData>("Stages");
            Assert.Greater(all.Length, 0, "The stages live in Resources/Stages.");

            int checkedStages = 0;
            foreach (StageData stage in all)
            {
                if (!StageMakerLogic.HasGrid(stage)) continue; // placeholders for stages that are not built yet
                checkedStages++;
                List<StageIssue> errors = StageMakerLogic.Validate(stage, StageMakerRouting.ComputeRoutes(stage))
                    .Where(i => i.severity == StageIssueSeverity.Error).ToList();
                Assert.IsEmpty(errors, stage.name + ": " + string.Join(" | ", errors.Select(e => e.message)));
            }
            Assert.Greater(checkedStages, 0);
        }
    }
}
