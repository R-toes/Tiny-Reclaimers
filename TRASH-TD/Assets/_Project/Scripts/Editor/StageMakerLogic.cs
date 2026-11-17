using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    public enum StageIssueSeverity { Info, Warning, Error }

    /// <summary>A button the Checks panel can offer next to an issue.</summary>
    public enum StageIssueFix { None, RepairLayout, SyncSpawnsAndExits }

    public sealed class StageIssue
    {
        public readonly StageIssueSeverity severity;
        public readonly string message;
        public readonly bool hasCell;
        public readonly Vector2Int cell;
        public readonly StageIssueFix fix;

        public StageIssue(StageIssueSeverity severity, string message, StageIssueFix fix = StageIssueFix.None)
        {
            this.severity = severity;
            this.message = message;
            this.fix = fix;
        }

        public StageIssue(StageIssueSeverity severity, string message, Vector2Int cell, StageIssueFix fix = StageIssueFix.None)
            : this(severity, message, fix)
        {
            hasCell = true;
            this.cell = cell;
        }
    }

    /// <summary>One ground route the game would use for a spawn point.</summary>
    public sealed class StageRoute
    {
        public int spawn;
        /// <summary>Drawn by hand (StageData.enemyPaths waypoints); otherwise the game finds it with A*.</summary>
        public bool custom;
        public List<Vector2Int> cells;
    }

    public enum StagePathMode { AutoNearestExit, AutoSpecificExit, Custom }

    /// <summary>What removing a spawn or exit tile would disturb: later numbers shift, and waves and routes follow them.</summary>
    public struct PointRemovalImpact
    {
        public bool exists;
        public bool isSpawn;
        public int index;
        public int laterPoints;
        public int waveEntries;
        public int routes;

        public bool NeedsConfirm => exists && (laterPoints > 0 || waveEntries > 0 || routes > 0);

        public string Describe()
        {
            string name = (isSpawn ? "S" : "E") + index;
            var parts = new List<string>();
            if (laterPoints > 0) parts.Add(laterPoints + " later " + (isSpawn ? "spawn" : "exit") + (laterPoints == 1 ? " is" : "s are") + " renumbered");
            if (waveEntries > 0) parts.Add(waveEntries + " wave entr" + (waveEntries == 1 ? "y uses" : "ies use") + " " + name + " and will move to the spawn that takes its number");
            if (routes > 0) parts.Add(routes + " route setting" + (routes == 1 ? "" : "s") + " for " + name + " " + (isSpawn ? "will be removed" : "will point at another exit"));
            return "Removing " + name + ": " + string.Join("; ", parts) + ". You can undo it with Ctrl+Z.";
        }
    }

    /// <summary>
    /// The rules behind the Stage Maker window, kept apart from the GUI so they can be tested: placing tiles while keeping
    /// StageData.spawnPoints / exitPoints / enemyPaths and the waves' spawn numbers consistent, resizing, and checking a stage.
    /// The spawn and exit LISTS are the source of truth for numbering (waves say "spawn 1"), so they are never reordered:
    /// a new spawn or exit is appended, and removing one renumbers the ones after it everywhere.
    /// </summary>
    public static class StageMakerLogic
    {
        public const int MinSize = 1;
        public const int MaxSize = 40;

        // ============================
        // Layout basics
        // ============================

        public static bool HasGrid(StageData stage)
        {
            return stage != null && stage.gridWidth > 0 && stage.gridHeight > 0;
        }

        /// <summary>True when tileLayout has exactly gridWidth * gridHeight tiles.</summary>
        public static bool LayoutMatchesGrid(StageData stage)
        {
            return HasGrid(stage) && stage.tileLayout != null && stage.tileLayout.Length == stage.gridWidth * stage.gridHeight;
        }

        public static bool InBounds(StageData stage, Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < stage.gridWidth && cell.y < stage.gridHeight;
        }

        public static TileType GetTile(StageData stage, Vector2Int cell)
        {
            return stage.GetTile(cell.x, cell.y);
        }

        /// <summary>True for tiles ground enemies can walk on (the game's own rule, TileTypeRules.IsWalkable).</summary>
        public static bool IsWalkable(TileType type)
        {
            return TileTypeRules.IsWalkable(type);
        }

        public static int[] CountTiles(StageData stage)
        {
            var counts = new int[Enum.GetValues(typeof(TileType)).Length];
            if (!LayoutMatchesGrid(stage)) return counts;
            foreach (TileType type in stage.tileLayout)
            {
                int index = (int)type;
                if (index >= 0 && index < counts.Length) counts[index]++;
            }
            return counts;
        }

        private static void SetRaw(StageData stage, Vector2Int cell, TileType type)
        {
            stage.tileLayout[cell.y * stage.gridWidth + cell.x] = type;
        }

        public static int IndexOf(Vector2Int[] points, Vector2Int cell)
        {
            if (points == null) return -1;
            for (int i = 0; i < points.Length; i++)
                if (points[i] == cell) return i;
            return -1;
        }

        private static Vector2Int[] Append(Vector2Int[] points, Vector2Int cell)
        {
            int length = points != null ? points.Length : 0;
            var result = new Vector2Int[length + 1];
            if (length > 0) Array.Copy(points, result, length);
            result[length] = cell;
            return result;
        }

        private static Vector2Int[] RemoveAt(Vector2Int[] points, int index)
        {
            var result = new Vector2Int[points.Length - 1];
            for (int i = 0, j = 0; i < points.Length; i++)
                if (i != index) result[j++] = points[i];
            return result;
        }

        /// <summary>The cell is a numbered spawn or exit (so painting over it renumbers things).</summary>
        public static bool IsPointCell(StageData stage, Vector2Int cell)
        {
            return IndexOf(stage.spawnPoints, cell) >= 0 || IndexOf(stage.exitPoints, cell) >= 0;
        }

        // ============================
        // Creating and resizing grids
        // ============================

        /// <summary>Gives a stage a fresh grid full of one tile type, with no spawns, exits or routes.</summary>
        public static void CreateBlank(StageData stage, int width, int height, TileType fill)
        {
            width = Mathf.Clamp(width, MinSize, MaxSize);
            height = Mathf.Clamp(height, MinSize, MaxSize);
            stage.gridWidth = width;
            stage.gridHeight = height;
            stage.tileLayout = new TileType[width * height];
            for (int i = 0; i < stage.tileLayout.Length; i++) stage.tileLayout[i] = fill;
            stage.spawnPoints = new Vector2Int[0];
            stage.exitPoints = new Vector2Int[0];
            stage.enemyPaths = new PathData[0];
        }

        /// <summary>
        /// Copies another stage's map into this one: grid size, tiles, spawns, exits and routes (deep copies). Art, waves and the
        /// rest of the stage are left alone. Wave entries may then point at spawns that do not exist; the checks flag that.
        /// </summary>
        public static void CopyLayout(StageData destination, StageData source)
        {
            destination.gridWidth = source.gridWidth;
            destination.gridHeight = source.gridHeight;
            destination.tileLayout = source.tileLayout != null ? (TileType[])source.tileLayout.Clone() : new TileType[0];
            destination.spawnPoints = source.spawnPoints != null ? (Vector2Int[])source.spawnPoints.Clone() : new Vector2Int[0];
            destination.exitPoints = source.exitPoints != null ? (Vector2Int[])source.exitPoints.Clone() : new Vector2Int[0];

            var paths = new List<PathData>();
            if (source.enemyPaths != null)
            {
                foreach (PathData path in source.enemyPaths)
                {
                    if (path == null) continue;
                    paths.Add(new PathData
                    {
                        waypoints = path.waypoints != null ? (Vector2Int[])path.waypoints.Clone() : new Vector2Int[0],
                        spawnPointIndex = path.spawnPointIndex,
                        exitPointIndex = path.exitPointIndex
                    });
                }
            }
            destination.enemyPaths = paths.ToArray();
        }

        /// <summary>A starting point: high ground everywhere with one straight lane from a spawn on the left to an exit on the right.</summary>
        public static void CreateStarter(StageData stage, int width, int height)
        {
            width = Mathf.Max(width, 3);
            CreateBlank(stage, width, height, TileType.HighGround);
            int row = stage.gridHeight / 2;
            for (int x = 0; x < stage.gridWidth; x++) SetTile(stage, new Vector2Int(x, row), TileType.LowGround);
            SetTile(stage, new Vector2Int(0, row), TileType.SpawnPoint);
            SetTile(stage, new Vector2Int(stage.gridWidth - 1, row), TileType.ExitPoint);
        }

        /// <summary>Makes tileLayout the right length for the grid, keeping tiles by position in the array. Used to repair a damaged layout.</summary>
        public static void RepairLayout(StageData stage, TileType fill)
        {
            if (!HasGrid(stage)) return;
            var repaired = new TileType[stage.gridWidth * stage.gridHeight];
            for (int i = 0; i < repaired.Length; i++)
                repaired[i] = stage.tileLayout != null && i < stage.tileLayout.Length ? stage.tileLayout[i] : fill;
            stage.tileLayout = repaired;
        }

        /// <summary>
        /// Changes the grid size, keeping every tile where it is (measured from the bottom-left corner, like the game).
        /// Spawns and exits outside the new size are removed (renumbering the rest); a route that leaves the grid goes back to automatic.
        /// </summary>
        public static string Resize(StageData stage, int width, int height, TileType fill)
        {
            width = Mathf.Clamp(width, MinSize, MaxSize);
            height = Mathf.Clamp(height, MinSize, MaxSize);
            if (!HasGrid(stage)) return null;
            if (!LayoutMatchesGrid(stage)) RepairLayout(stage, fill);

            int oldWidth = stage.gridWidth;
            int oldHeight = stage.gridHeight;
            if (width == oldWidth && height == oldHeight) return null;

            var layout = new TileType[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    layout[y * width + x] = x < oldWidth && y < oldHeight ? stage.tileLayout[y * oldWidth + x] : fill;
                }
            }

            int removed = 0;
            if (stage.spawnPoints != null)
            {
                for (int i = stage.spawnPoints.Length - 1; i >= 0; i--)
                {
                    Vector2Int p = stage.spawnPoints[i];
                    if (p.x >= width || p.y >= height) { RemoveSpawn(stage, i); removed++; }
                }
            }
            if (stage.exitPoints != null)
            {
                for (int i = stage.exitPoints.Length - 1; i >= 0; i--)
                {
                    Vector2Int p = stage.exitPoints[i];
                    if (p.x >= width || p.y >= height) { RemoveExit(stage, i); removed++; }
                }
            }

            var leavingGrid = new List<int>();
            if (stage.enemyPaths != null)
            {
                foreach (PathData path in stage.enemyPaths)
                {
                    if (path == null || path.waypoints == null || path.waypoints.Length == 0) continue;
                    foreach (Vector2Int waypoint in path.waypoints)
                    {
                        if (waypoint.x >= width || waypoint.y >= height)
                        {
                            leavingGrid.Add(path.spawnPointIndex);
                            break;
                        }
                    }
                }
            }
            foreach (int spawn in leavingGrid) SetAutoRoute(stage, spawn, -1);
            int rerouted = leavingGrid.Count;

            stage.gridWidth = width;
            stage.gridHeight = height;
            stage.tileLayout = layout;

            var notes = new List<string>();
            if (removed > 0) notes.Add(removed + " spawn/exit point" + (removed == 1 ? "" : "s") + " outside the new size removed");
            if (rerouted > 0) notes.Add(rerouted + " hand-drawn route" + (rerouted == 1 ? "" : "s") + " left the grid and went back to automatic");
            return notes.Count > 0 ? string.Join("; ", notes) : null;
        }

        // ============================
        // Placing tiles
        // ============================

        /// <summary>
        /// Sets a tile and keeps the spawn and exit lists in step: a spawn or exit tile is added to its list (at the end), and
        /// painting something else over one removes it (renumbering the rest). Returns false when nothing changed.
        /// </summary>
        public static bool SetTile(StageData stage, Vector2Int cell, TileType type)
        {
            if (!LayoutMatchesGrid(stage) || !InBounds(stage, cell)) return false;

            TileType current = GetTile(stage, cell);
            int spawnIndex = IndexOf(stage.spawnPoints, cell);
            int exitIndex = IndexOf(stage.exitPoints, cell);

            bool unchanged = current == type
                             && !(type == TileType.SpawnPoint && spawnIndex < 0)
                             && !(type == TileType.ExitPoint && exitIndex < 0)
                             && !(type != TileType.SpawnPoint && spawnIndex >= 0)
                             && !(type != TileType.ExitPoint && exitIndex >= 0);
            if (unchanged) return false;

            if (spawnIndex >= 0 && type != TileType.SpawnPoint) RemoveSpawn(stage, spawnIndex);
            if (exitIndex >= 0 && type != TileType.ExitPoint) RemoveExit(stage, exitIndex);

            SetRaw(stage, cell, type);

            if (type == TileType.SpawnPoint && IndexOf(stage.spawnPoints, cell) < 0) stage.spawnPoints = Append(stage.spawnPoints, cell);
            if (type == TileType.ExitPoint && IndexOf(stage.exitPoints, cell) < 0) stage.exitPoints = Append(stage.exitPoints, cell);
            return true;
        }

        /// <summary>What painting <paramref name="newTile"/> over this cell would disturb (exists = false when the cell is no numbered spawn or exit).</summary>
        public static PointRemovalImpact GetRemovalImpact(StageData stage, Vector2Int cell, TileType newTile)
        {
            var impact = new PointRemovalImpact();
            int spawnIndex = IndexOf(stage.spawnPoints, cell);
            if (spawnIndex >= 0 && newTile != TileType.SpawnPoint)
            {
                impact.exists = true;
                impact.isSpawn = true;
                impact.index = spawnIndex;
                impact.laterPoints = stage.spawnPoints.Length - 1 - spawnIndex;
                impact.waveEntries = CountWaveEntries(stage, spawnIndex);
                impact.routes = CountPaths(stage, p => p.spawnPointIndex == spawnIndex);
                return impact;
            }

            int exitIndex = IndexOf(stage.exitPoints, cell);
            if (exitIndex >= 0 && newTile != TileType.ExitPoint)
            {
                impact.exists = true;
                impact.index = exitIndex;
                impact.laterPoints = stage.exitPoints.Length - 1 - exitIndex;
                impact.routes = CountPaths(stage, p => p.exitPointIndex == exitIndex);
            }
            return impact;
        }

        /// <summary>
        /// Moves spawn or exit number <paramref name="index"/> to another cell, keeping its number (so waves and routes
        /// still point at it). The old tile becomes low ground. Returns false when the target is not a plain tile.
        /// </summary>
        public static bool MovePoint(StageData stage, bool spawn, int index, Vector2Int target)
        {
            Vector2Int[] list = spawn ? stage.spawnPoints : stage.exitPoints;
            if (!LayoutMatchesGrid(stage) || list == null || index < 0 || index >= list.Length || !InBounds(stage, target)) return false;

            TileType targetTile = GetTile(stage, target);
            if (targetTile == TileType.SpawnPoint || targetTile == TileType.ExitPoint) return false;

            TileType pointTile = spawn ? TileType.SpawnPoint : TileType.ExitPoint;
            Vector2Int old = list[index];
            if (InBounds(stage, old) && GetTile(stage, old) == pointTile) SetRaw(stage, old, TileType.LowGround);
            SetRaw(stage, target, pointTile);
            list[index] = target;
            return true;
        }

        public static void RemoveSpawn(StageData stage, int index)
        {
            if (stage.spawnPoints == null || index < 0 || index >= stage.spawnPoints.Length) return;
            stage.spawnPoints = RemoveAt(stage.spawnPoints, index);
            int count = stage.spawnPoints.Length;

            if (stage.enemyPaths != null)
            {
                var kept = new List<PathData>();
                foreach (PathData path in stage.enemyPaths)
                {
                    if (path == null || path.spawnPointIndex == index) continue;
                    if (path.spawnPointIndex > index) path.spawnPointIndex--;
                    kept.Add(path);
                }
                stage.enemyPaths = kept.ToArray();
            }

            ForEachWaveEntry(stage, entry =>
            {
                if (entry.spawnPointIndex > index) entry.spawnPointIndex--;
                else if (entry.spawnPointIndex == index) entry.spawnPointIndex = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
            });
        }

        public static void RemoveExit(StageData stage, int index)
        {
            if (stage.exitPoints == null || index < 0 || index >= stage.exitPoints.Length) return;
            stage.exitPoints = RemoveAt(stage.exitPoints, index);
            int count = stage.exitPoints.Length;

            if (stage.enemyPaths == null) return;
            foreach (PathData path in stage.enemyPaths)
            {
                if (path == null) continue;
                if (path.exitPointIndex > index) path.exitPointIndex--;
                else if (path.exitPointIndex == index) path.exitPointIndex = Mathf.Clamp(index, 0, Mathf.Max(0, count - 1));
            }
        }

        /// <summary>
        /// Makes the spawn and exit lists match the Spawn / Exit tiles: listed points that are not on such a tile are removed
        /// (renumbering the rest), and tiles missing from a list are appended. The order of the points that stay is kept.
        /// </summary>
        public static void SyncSpawnsAndExits(StageData stage)
        {
            if (!LayoutMatchesGrid(stage)) return;

            SyncList(stage, true);
            SyncList(stage, false);
        }

        private static void SyncList(StageData stage, bool spawn)
        {
            TileType tile = spawn ? TileType.SpawnPoint : TileType.ExitPoint;
            Vector2Int[] list = spawn ? stage.spawnPoints : stage.exitPoints;
            if (list != null)
            {
                for (int i = list.Length - 1; i >= 0; i--)
                {
                    bool valid = InBounds(stage, list[i]) && GetTile(stage, list[i]) == tile && IndexOf(list, list[i]) == i;
                    if (valid) continue;
                    if (spawn) RemoveSpawn(stage, i); else RemoveExit(stage, i);
                    list = spawn ? stage.spawnPoints : stage.exitPoints;
                }
            }

            for (int y = 0; y < stage.gridHeight; y++)
            {
                for (int x = 0; x < stage.gridWidth; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (GetTile(stage, cell) != tile) continue;
                    if (spawn && IndexOf(stage.spawnPoints, cell) < 0) stage.spawnPoints = Append(stage.spawnPoints, cell);
                    if (!spawn && IndexOf(stage.exitPoints, cell) < 0) stage.exitPoints = Append(stage.exitPoints, cell);
                }
            }
        }

        // ============================
        // Routes (StageData.enemyPaths)
        // ============================

        /// <summary>
        /// How the game picks the route for a spawn (the rules of WaveManager.PrecomputePaths): a later enemyPaths entry for
        /// the same spawn replaces an earlier one; an entry with waypoints is walked as written; an entry without waypoints
        /// but with a valid exit number runs A* to that exit; otherwise A* to the nearest exit.
        /// </summary>
        public static StagePathMode GetPathMode(StageData stage, int spawn, out int exitIndex)
        {
            exitIndex = -1;
            StagePathMode mode = StagePathMode.AutoNearestExit;
            if (stage.enemyPaths == null) return mode;

            foreach (PathData path in stage.enemyPaths)
            {
                if (path == null || path.spawnPointIndex != spawn) continue;
                if (path.waypoints != null && path.waypoints.Length > 0)
                {
                    mode = StagePathMode.Custom;
                    exitIndex = -1;
                }
                else if (stage.exitPoints != null && path.exitPointIndex >= 0 && path.exitPointIndex < stage.exitPoints.Length)
                {
                    mode = StagePathMode.AutoSpecificExit;
                    exitIndex = path.exitPointIndex;
                }
            }
            return mode;
        }

        public static bool HasCustomWaypoints(StageData stage, int spawn)
        {
            return GetPathMode(stage, spawn, out _) == StagePathMode.Custom;
        }

        /// <summary>The hand-drawn waypoints the game would use for a spawn, or null when it routes automatically.</summary>
        public static List<Vector2Int> GetCustomWaypoints(StageData stage, int spawn)
        {
            if (stage.enemyPaths == null) return null;
            List<Vector2Int> result = null;
            foreach (PathData path in stage.enemyPaths)
            {
                if (path == null || path.spawnPointIndex != spawn) continue;
                if (path.waypoints != null && path.waypoints.Length > 0) result = new List<Vector2Int>(path.waypoints);
                else if (stage.exitPoints != null && path.exitPointIndex >= 0 && path.exitPointIndex < stage.exitPoints.Length) result = null;
            }
            return result;
        }

        private static void RemovePathsFor(StageData stage, int spawn)
        {
            if (stage.enemyPaths == null) return;
            var kept = new List<PathData>();
            foreach (PathData path in stage.enemyPaths)
                if (path != null && path.spawnPointIndex != spawn) kept.Add(path);
            stage.enemyPaths = kept.ToArray();
        }

        private static void AddPath(StageData stage, PathData path)
        {
            int length = stage.enemyPaths != null ? stage.enemyPaths.Length : 0;
            var result = new PathData[length + 1];
            if (length > 0) Array.Copy(stage.enemyPaths, result, length);
            result[length] = path;
            stage.enemyPaths = result;
        }

        /// <summary>Gives a spawn a hand-drawn route (a contiguous list of cells from the spawn to an exit).</summary>
        public static void SetCustomWaypoints(StageData stage, int spawn, IList<Vector2Int> cells)
        {
            RemovePathsFor(stage, spawn);
            if (cells == null || cells.Count == 0) return;

            int exit = IndexOf(stage.exitPoints, cells[cells.Count - 1]);
            var waypoints = new Vector2Int[cells.Count];
            cells.CopyTo(waypoints, 0);
            AddPath(stage, new PathData { waypoints = waypoints, spawnPointIndex = spawn, exitPointIndex = Mathf.Max(0, exit) });
        }

        /// <summary>Puts a spawn back on automatic routing: to the nearest exit (<paramref name="exitIndex"/> &lt; 0) or to one exit.</summary>
        public static void SetAutoRoute(StageData stage, int spawn, int exitIndex)
        {
            RemovePathsFor(stage, spawn);
            if (exitIndex < 0) return;
            AddPath(stage, new PathData { waypoints = new Vector2Int[0], spawnPointIndex = spawn, exitPointIndex = exitIndex });
        }

        // ============================
        // Waves that point at spawns
        // ============================

        private static void ForEachWaveEntry(StageData stage, Action<WaveEntry> action)
        {
            foreach (WaveData[] waves in new[] { stage.wavesEasy, stage.wavesNormal, stage.wavesHard })
            {
                if (waves == null) continue;
                foreach (WaveData wave in waves)
                {
                    if (wave == null || wave.entries == null) continue;
                    foreach (WaveEntry entry in wave.entries)
                        if (entry != null) action(entry);
                }
            }
        }

        public static int CountWaveEntries(StageData stage, int spawn)
        {
            int count = 0;
            ForEachWaveEntry(stage, entry => { if (entry.spawnPointIndex == spawn) count++; });
            return count;
        }

        private static int CountPaths(StageData stage, Func<PathData, bool> match)
        {
            int count = 0;
            if (stage.enemyPaths == null) return count;
            foreach (PathData path in stage.enemyPaths)
                if (path != null && match(path)) count++;
            return count;
        }

        // ============================
        // Checks
        // ============================

        /// <summary>
        /// Everything wrong or suspicious about a stage's map: missing spawn/exit, lists out of step with the tiles, spawns with
        /// no route, hand-drawn routes that break the rules, holes that do not pair up and waves that use a spawn that is not there.
        /// <paramref name="routes"/> are the routes the game would use (see StageMakerRouting.ComputeRoutes).
        /// </summary>
        public static List<StageIssue> Validate(StageData stage, IList<StageRoute> routes)
        {
            var issues = new List<StageIssue>();
            if (stage == null) return issues;

            if (!HasGrid(stage))
            {
                issues.Add(new StageIssue(StageIssueSeverity.Info, "This stage has no grid yet. Create one to start."));
                return issues;
            }

            if (!LayoutMatchesGrid(stage))
            {
                int have = stage.tileLayout != null ? stage.tileLayout.Length : 0;
                issues.Add(new StageIssue(StageIssueSeverity.Error,
                    "The tile layout has " + have + " tiles but a " + stage.gridWidth + "x" + stage.gridHeight + " grid needs " + stage.gridWidth * stage.gridHeight +
                    ". The game treats missing tiles as Blocked.", StageIssueFix.RepairLayout));
                return issues;
            }

            CheckPoints(stage, issues, true);
            CheckPoints(stage, issues, false);

            int[] counts = CountTiles(stage);
            int highGround = counts[(int)TileType.HighGround] + counts[(int)TileType.AcidHighGround];
            int lowGround = counts[(int)TileType.LowGround] + counts[(int)TileType.AcidLowGround] + counts[(int)TileType.EnemyPath];
            if (highGround == 0 && lowGround == 0)
                issues.Add(new StageIssue(StageIssueSeverity.Warning, "There is no ground for operators to deploy on."));
            else if (highGround == 0)
                issues.Add(new StageIssue(StageIssueSeverity.Warning, "There is no High ground, so ranged operators have nowhere to deploy."));

            int holes = counts[(int)TileType.Hole];
            if (holes == 1 || holes > 2)
                issues.Add(new StageIssue(StageIssueSeverity.Warning,
                    "There are " + holes + " Hole tiles. A hole sends enemies to the first other hole, so use exactly two."));

            CheckRoutes(stage, routes, issues);
            CheckWaves(stage, issues);
            CheckArt(stage, issues);
            return issues;
        }

        /// <summary>The stage's map art and bounds art must still fit the grid (see StageArtRules); a resized grid is the usual cause.</summary>
        private static void CheckArt(StageData stage, List<StageIssue> issues)
        {
            int tile = StageArtRules.TilePixels(stage);
            if (stage.mapVisualSprite != null)
            {
                Rect size = stage.mapVisualSprite.rect;
                ArtCheck check = StageArtRules.CheckMapArt(Mathf.RoundToInt(size.width), Mathf.RoundToInt(size.height), tile, stage.gridWidth, stage.gridHeight, stage.visualTileOffset, true);
                if (!check.ok) issues.Add(new StageIssue(StageIssueSeverity.Warning, "Map art: " + check.message));
            }
            if (stage.boundsVisualSprite != null)
            {
                Rect size = stage.boundsVisualSprite.rect;
                ArtCheck check = StageArtRules.CheckBoundsArt(Mathf.RoundToInt(size.width), Mathf.RoundToInt(size.height), tile, stage.gridWidth, stage.gridHeight, stage.boundsTileOffset);
                if (!check.ok) issues.Add(new StageIssue(StageIssueSeverity.Warning, "Bounds art: " + check.message));
            }
        }

        private static void CheckPoints(StageData stage, List<StageIssue> issues, bool spawn)
        {
            string noun = spawn ? "spawn" : "exit";
            string letter = spawn ? "S" : "E";
            TileType tile = spawn ? TileType.SpawnPoint : TileType.ExitPoint;
            Vector2Int[] list = spawn ? stage.spawnPoints : stage.exitPoints;

            if (list == null || list.Length == 0)
            {
                issues.Add(new StageIssue(StageIssueSeverity.Error, "There is no " + noun + " point" + (spawn ? ": enemies have nowhere to enter." : ": enemies have nowhere to go.")));
            }

            if (list != null)
            {
                for (int i = 0; i < list.Length; i++)
                {
                    Vector2Int p = list[i];
                    string name = letter + i + " at (" + p.x + ", " + p.y + ")";
                    if (!InBounds(stage, p))
                        issues.Add(new StageIssue(StageIssueSeverity.Error, name + " is outside the grid.", StageIssueFix.SyncSpawnsAndExits));
                    else if (GetTile(stage, p) != tile)
                        issues.Add(new StageIssue(StageIssueSeverity.Error, name + " is not on a " + (spawn ? "Spawn" : "Exit") + " tile.", p, StageIssueFix.SyncSpawnsAndExits));
                    else if (IndexOf(list, p) != i)
                        issues.Add(new StageIssue(StageIssueSeverity.Warning, name + " is listed twice.", p, StageIssueFix.SyncSpawnsAndExits));
                }
            }

            for (int y = 0; y < stage.gridHeight; y++)
            {
                for (int x = 0; x < stage.gridWidth; x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (GetTile(stage, cell) == tile && IndexOf(list, cell) < 0)
                        issues.Add(new StageIssue(StageIssueSeverity.Error,
                            "The " + (spawn ? "Spawn" : "Exit") + " tile at (" + x + ", " + y + ") is not in the " + noun + " list, so the game ignores it.", cell, StageIssueFix.SyncSpawnsAndExits));
                }
            }
        }

        private static void CheckRoutes(StageData stage, IList<StageRoute> routes, List<StageIssue> issues)
        {
            if (stage.spawnPoints == null || stage.exitPoints == null || stage.exitPoints.Length == 0) return;

            for (int s = 0; s < stage.spawnPoints.Length; s++)
            {
                bool hasRoute = false;
                if (routes != null)
                {
                    foreach (StageRoute route in routes)
                        if (route.spawn == s) { hasRoute = true; break; }
                }

                if (!hasRoute)
                {
                    issues.Add(new StageIssue(StageIssueSeverity.Error,
                        "S" + s + " has no route to an exit, so its enemies would never leave the spawn.",
                        stage.spawnPoints[s]));
                    continue;
                }

                List<Vector2Int> waypoints = GetCustomWaypoints(stage, s);
                if (waypoints != null) CheckCustomRoute(stage, s, waypoints, issues);
            }
        }

        private static void CheckCustomRoute(StageData stage, int spawn, List<Vector2Int> waypoints, List<StageIssue> issues)
        {
            string name = "The drawn route for S" + spawn;
            if (waypoints[0] != stage.spawnPoints[spawn])
                issues.Add(new StageIssue(StageIssueSeverity.Warning, name + " does not start on its spawn.", waypoints[0]));
            if (IndexOf(stage.exitPoints, waypoints[waypoints.Count - 1]) < 0)
                issues.Add(new StageIssue(StageIssueSeverity.Warning, name + " does not end on an exit.", waypoints[waypoints.Count - 1]));

            for (int i = 0; i < waypoints.Count; i++)
            {
                Vector2Int p = waypoints[i];
                if (!InBounds(stage, p))
                {
                    issues.Add(new StageIssue(StageIssueSeverity.Error, name + " leaves the grid at (" + p.x + ", " + p.y + ")."));
                    return;
                }
                if (!IsWalkable(GetTile(stage, p)))
                {
                    issues.Add(new StageIssue(StageIssueSeverity.Error, name + " crosses a " + StageMakerTiles.Label(GetTile(stage, p)) + " tile at (" + p.x + ", " + p.y + ").", p));
                    return;
                }
                if (i == 0) continue;

                Vector2Int previous = waypoints[i - 1];
                bool adjacent = Mathf.Abs(previous.x - p.x) + Mathf.Abs(previous.y - p.y) == 1;
                bool holeJump = InBounds(stage, previous) && GetTile(stage, previous) == TileType.Hole && GetTile(stage, p) == TileType.Hole;
                if (!adjacent && !holeJump)
                {
                    issues.Add(new StageIssue(StageIssueSeverity.Warning,
                        name + " jumps from (" + previous.x + ", " + previous.y + ") to (" + p.x + ", " + p.y + "): enemies walk in a straight line between waypoints.", p));
                    return;
                }
            }
        }

        private static void CheckWaves(StageData stage, List<StageIssue> issues)
        {
            int spawnCount = stage.spawnPoints != null ? stage.spawnPoints.Length : 0;
            if (spawnCount == 0) return;

            int bad = 0;
            int firstBad = -1;
            ForEachWaveEntry(stage, entry =>
            {
                if (entry.spawnPointIndex >= spawnCount || entry.spawnPointIndex < 0)
                {
                    bad++;
                    if (firstBad < 0) firstBad = entry.spawnPointIndex;
                }
            });

            if (bad > 0)
                issues.Add(new StageIssue(StageIssueSeverity.Warning,
                    bad + " wave entr" + (bad == 1 ? "y sends" : "ies send") + " enemies from spawn S" + firstBad + ", which does not exist (the stage has " + spawnCount + ")."));
        }
    }
}
