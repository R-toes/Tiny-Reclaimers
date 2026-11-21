using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;

namespace TrashTD.Systems
{
    /// <summary>
    /// Layers operators and enemies by the tile they stand on.
    /// Tiles are counted from the top-left tile (0, 0), x rightward and y downward; a tile's depth is
    /// x + y and deeper tiles draw on top. Units sharing a tile draw in arrival order, the first to
    /// arrive on top. Flyers form their own band above every ground unit and sort the same way among
    /// themselves. Each unit root gets a SortingGroup on the Units layer, so its child sprites (visual,
    /// aura, damage flashes) move with it.
    /// </summary>
    public sealed class GridSpriteSorter : MonoBehaviour
    {
        /// <summary>Sorting slots per tile; units past this many on one tile share the bottom slot.</summary>
        public const int SlotsPerTile = 16;

        /// <summary>Base sorting order of the flyer band, above the deepest ground tile.</summary>
        public const int AirBandStart = 10000;

        private sealed class Entry
        {
            public MonoBehaviour Unit;
            public SortingGroup Group;
            public Vector2Int Tile;
            public bool IsAir;
            public long ArrivalOrder;
            public int LastSeenFrame;
            public int Order; // The sorting order last worked out for the unit
        }

        private GridManager gridManager;
        private readonly Dictionary<MonoBehaviour, Entry> entries = new Dictionary<MonoBehaviour, Entry>();
        private readonly List<Entry> current = new List<Entry>();
        private readonly List<MonoBehaviour> stale = new List<MonoBehaviour>();
        private long arrivalCounter;
        private int frame;
        private bool orderChanged; // A unit appeared, left or changed tile since the orders were last worked out

        public void Initialize(GridManager manager)
        {
            gridManager = manager;
        }

        /// <summary>Depth of a grid cell: x + y with y counted downward from the top row.</summary>
        public static int GetTileDepth(Vector2Int gridPosition, int gridHeight)
        {
            return gridPosition.x + (gridHeight - 1 - gridPosition.y);
        }

        /// <summary>Sorting order for a unit on a tile, given its arrival rank there (0 = first).</summary>
        public static int GetSortingOrder(int depth, int arrivalRank, bool isAir)
        {
            int slot = SlotsPerTile - 1 - Mathf.Clamp(arrivalRank, 0, SlotsPerTile - 1);
            return (isAir ? AirBandStart : 0) + depth * SlotsPerTile + slot;
        }

        private void LateUpdate()
        {
            if (gridManager == null || gridManager.Width <= 0 || gridManager.Height <= 0) return;

            frame++;
            current.Clear();
            orderChanged = false;

            if (OperatorManager.Instance != null)
            {
                var operators = OperatorManager.Instance.DeployedOperators;
                for (int i = 0; i < operators.Count; i++)
                {
                    OperatorBase op = operators[i];
                    if (op == null) continue;
                    Vector2Int tile = op.DeployedCell != null
                        ? op.DeployedCell.GridPosition
                        : gridManager.WorldToGridPosition(op.transform.position);
                    Track(op, tile, false);
                }
            }

            if (EnemyManager.Instance != null)
            {
                var enemies = EnemyManager.Instance.ActiveEnemies;
                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyBase enemy = enemies[i];
                    if (enemy == null) continue;
                    Track(enemy, gridManager.WorldToGridPosition(enemy.transform.position),
                        enemy.MovementType == EnemyMovementType.Air);
                }
            }

            RemoveStaleEntries();
            ApplySortingOrders();
        }

        private void Track(MonoBehaviour unit, Vector2Int tile, bool isAir)
        {
            tile.x = Mathf.Clamp(tile.x, 0, gridManager.Width - 1);
            tile.y = Mathf.Clamp(tile.y, 0, gridManager.Height - 1);

            if (!entries.TryGetValue(unit, out Entry entry))
            {
                SortingGroup group = unit.GetComponent<SortingGroup>();
                if (group == null) group = unit.gameObject.AddComponent<SortingGroup>();
                group.sortingLayerID = GridSorting.UnitsLayerId;

                entry = new Entry { Unit = unit, Group = group, Tile = tile, IsAir = isAir, ArrivalOrder = ++arrivalCounter };
                entries.Add(unit, entry);
                orderChanged = true;
            }
            else if (entry.Tile != tile || entry.IsAir != isAir)
            {
                entry.Tile = tile;
                entry.IsAir = isAir;
                entry.ArrivalOrder = ++arrivalCounter;
                orderChanged = true;
            }

            entry.LastSeenFrame = frame;
            current.Add(entry);
        }

        private void RemoveStaleEntries()
        {
            stale.Clear();
            foreach (var pair in entries)
            {
                if (pair.Key == null || pair.Value.LastSeenFrame != frame)
                    stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                entries.Remove(stale[i]);
            if (stale.Count > 0) orderChanged = true;
        }

        private void ApplySortingOrders()
        {
            // The order only depends on which tile each unit is on and who arrived first, so it is worked out again
            // only when a unit appeared, left or moved to another tile (most frames none did).
            if (orderChanged)
            {
                current.Sort(CompareByTileThenArrival);

                int rank = 0;
                for (int i = 0; i < current.Count; i++)
                {
                    Entry entry = current[i];
                    bool sameTileAsPrevious = i > 0 &&
                        current[i - 1].IsAir == entry.IsAir &&
                        current[i - 1].Tile == entry.Tile;
                    rank = sameTileAsPrevious ? rank + 1 : 0;

                    int depth = GetTileDepth(entry.Tile, gridManager.Height);
                    entry.Order = GetSortingOrder(depth, rank, entry.IsAir);
                }
            }

            // Still checked every frame, because something else may have written a group's order (Mlem's grab does).
            // Reading is cheap; writing the same value again would make the renderer re-sort for nothing.
            for (int i = 0; i < current.Count; i++)
            {
                Entry entry = current[i];
                if (entry.Group == null) continue;
                if (entry.Group.sortingOrder != entry.Order) entry.Group.sortingOrder = entry.Order;
            }
        }

        private static int CompareByTileThenArrival(Entry a, Entry b)
        {
            int result = a.IsAir.CompareTo(b.IsAir);
            if (result != 0) return result;
            result = a.Tile.x.CompareTo(b.Tile.x);
            if (result != 0) return result;
            result = a.Tile.y.CompareTo(b.Tile.y);
            if (result != 0) return result;
            return a.ArrivalOrder.CompareTo(b.ArrivalOrder);
        }
    }
}
