using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Core.Pathfinding;
using TrashTD.Data;
using TrashTD.UI;

namespace TrashTD.Editor
{
    /// <summary>
    /// Route previews for the Stage Maker. The routes shown are computed by the same code the stage dossier uses
    /// (StageMapPreview.ComputeRoutes, which follows WaveManager: hand-drawn waypoints first, otherwise the game's own A*),
    /// so the editor never disagrees with the game.
    /// </summary>
    public static class StageMakerRouting
    {
        public static List<StageRoute> ComputeRoutes(StageData stage)
        {
            var result = new List<StageRoute>();
            if (!StageMakerLogic.HasGrid(stage)) return result;

            foreach ((int spawn, List<Vector2Int> cells) in StageMapPreview.ComputeRoutes(stage))
            {
                result.Add(new StageRoute
                {
                    spawn = spawn,
                    cells = cells,
                    custom = StageMakerLogic.HasCustomWaypoints(stage, spawn)
                });
            }
            return result;
        }
    }

    /// <summary>
    /// The game's A* (AStarPathfinder) on a throw-away copy of the stage's grid, for filling in the steps between the
    /// tiles a route is drawn through. Dispose it (or let the window drop it) so the hidden grid object is cleaned up.
    /// </summary>
    internal sealed class StageMakerPathfinder : IDisposable
    {
        private GameObject gridObject;
        private readonly GridManager grid;
        private readonly AStarPathfinder pathfinder;
        private readonly StageData stage;

        public StageMakerPathfinder(StageData stage)
        {
            this.stage = stage;
            gridObject = new GameObject("StageMakerGrid") { hideFlags = HideFlags.HideAndDontSave };
            grid = gridObject.AddComponent<GridManager>();
            grid.InitializeFromStageData(stage);
            pathfinder = new AStarPathfinder();
            pathfinder.Initialize(grid);
        }

        /// <summary>The cells of the shortest ground route from one cell to another (both included), or null when there is none.</summary>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            return ToCells(pathfinder.FindPath(from, to, EnemyMovementType.Ground));
        }

        /// <summary>The shortest ground route from a cell to whichever exit is nearest, or null.</summary>
        public List<Vector2Int> FindPathToNearestExit(Vector2Int from)
        {
            if (stage.exitPoints == null || stage.exitPoints.Length == 0) return null;
            return ToCells(pathfinder.FindBestPath(from, stage.exitPoints, EnemyMovementType.Ground));
        }

        private List<Vector2Int> ToCells(List<Vector3> worldPath)
        {
            if (worldPath == null || worldPath.Count == 0) return null;
            var cells = new List<Vector2Int>(worldPath.Count);
            foreach (Vector3 point in worldPath) cells.Add(grid.WorldToGridPosition(point));
            return cells;
        }

        public void Dispose()
        {
            if (gridObject != null) UnityEngine.Object.DestroyImmediate(gridObject);
            gridObject = null;
        }
    }
}
