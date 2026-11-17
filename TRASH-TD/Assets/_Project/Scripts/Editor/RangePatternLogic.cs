using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>
    /// The maths behind the Range Pattern window, kept apart from the GUI so it can be tested. A range pattern is a list of
    /// grid offsets from the operator's tile, written for an operator FACING RIGHT: (1, 0) is the tile in front, (0, 1) the
    /// tile above it, (0, 0) the operator's own tile. The game rotates the offsets for the other facings
    /// (<c>GridManager.GetCellsInRange</c>); <see cref="Rotate"/> is the same rotation.
    /// Patterns keep the order they were authored in, so nothing here sorts or de-duplicates unless asked to.
    /// </summary>
    public static class RangePatternLogic
    {
        /// <summary>Patterns reaching further than this many tiles from the operator are flagged (the window draws up to this).</summary>
        public const int MaxRadius = 12;

        // ============================
        // Facing
        // ============================

        /// <summary>Rotates an offset written for a right-facing operator to the given facing (the rotation GridManager.GetCellsInRange uses).</summary>
        public static Vector2Int Rotate(Vector2Int offset, OperatorFacing facing)
        {
            switch (facing)
            {
                case OperatorFacing.Up: return new Vector2Int(-offset.y, offset.x);
                case OperatorFacing.Left: return new Vector2Int(-offset.x, -offset.y);
                case OperatorFacing.Down: return new Vector2Int(offset.y, -offset.x);
                default: return offset;
            }
        }

        /// <summary>The inverse of <see cref="Rotate"/>: which right-facing offset lands on this tile when the operator faces that way.</summary>
        public static Vector2Int Unrotate(Vector2Int tile, OperatorFacing facing)
        {
            switch (facing)
            {
                case OperatorFacing.Up: return new Vector2Int(tile.y, -tile.x);
                case OperatorFacing.Left: return new Vector2Int(-tile.x, -tile.y);
                case OperatorFacing.Down: return new Vector2Int(-tile.y, tile.x);
                default: return tile;
            }
        }

        // ============================
        // Reading a pattern
        // ============================

        public static bool Contains(Vector2Int[] pattern, Vector2Int cell)
        {
            if (pattern == null) return false;
            for (int i = 0; i < pattern.Length; i++)
                if (pattern[i] == cell) return true;
            return false;
        }

        /// <summary>The same set of cells, ignoring order and repeats.</summary>
        public static bool SameCells(Vector2Int[] a, Vector2Int[] b)
        {
            var setA = new HashSet<Vector2Int>(a ?? new Vector2Int[0]);
            var setB = new HashSet<Vector2Int>(b ?? new Vector2Int[0]);
            return setA.SetEquals(setB);
        }

        /// <summary>The furthest any cell is from the operator along either axis (0 for an empty pattern).</summary>
        public static int Radius(Vector2Int[] pattern)
        {
            int radius = 0;
            if (pattern == null) return radius;
            foreach (Vector2Int cell in pattern)
                radius = Mathf.Max(radius, Mathf.Abs(cell.x), Mathf.Abs(cell.y));
            return radius;
        }

        /// <summary>How many distinct tiles the pattern covers.</summary>
        public static int DistinctCount(Vector2Int[] pattern)
        {
            return pattern == null ? 0 : new HashSet<Vector2Int>(pattern).Count;
        }

        public static bool HasDuplicates(Vector2Int[] pattern)
        {
            return pattern != null && DistinctCount(pattern) != pattern.Length;
        }

        /// <summary>One sentence for the info panel, e.g. "7 tiles, up to 8 in front".</summary>
        public static string Describe(Vector2Int[] pattern)
        {
            int count = DistinctCount(pattern);
            if (count == 0) return "No tiles.";
            int forward = int.MinValue;
            foreach (Vector2Int cell in pattern) forward = Mathf.Max(forward, cell.x);
            string reach = forward > 0 ? ", up to " + forward + " in front" : forward == 0 ? ", none in front" : ", all behind";
            return count + " tile" + (count == 1 ? "" : "s") + reach + ".";
        }

        /// <summary>Things worth a second look: an empty pattern, repeated cells, or cells too far away to draw.</summary>
        public static List<string> Validate(Vector2Int[] pattern)
        {
            var notes = new List<string>();
            if (pattern == null || pattern.Length == 0)
            {
                notes.Add("The pattern is empty.");
                return notes;
            }
            if (HasDuplicates(pattern))
                notes.Add("Some cells are listed more than once (" + (pattern.Length - DistinctCount(pattern)) + " repeat" + (pattern.Length - DistinctCount(pattern) == 1 ? "" : "s") + ").");
            if (Radius(pattern) > MaxRadius)
                notes.Add("Some cells are more than " + MaxRadius + " tiles from the operator.");
            return notes;
        }

        // ============================
        // Editing a pattern (each returns a new array)
        // ============================

        public static Vector2Int[] Add(Vector2Int[] pattern, Vector2Int cell)
        {
            if (Contains(pattern, cell)) return pattern ?? new Vector2Int[0];
            var result = new List<Vector2Int>(pattern ?? new Vector2Int[0]) { cell };
            return result.ToArray();
        }

        /// <summary>Removes every copy of the cell.</summary>
        public static Vector2Int[] Remove(Vector2Int[] pattern, Vector2Int cell)
        {
            var result = new List<Vector2Int>();
            if (pattern != null)
                foreach (Vector2Int c in pattern)
                    if (c != cell) result.Add(c);
            return result.ToArray();
        }

        public static Vector2Int[] Toggle(Vector2Int[] pattern, Vector2Int cell)
        {
            return Contains(pattern, cell) ? Remove(pattern, cell) : Add(pattern, cell);
        }

        /// <summary>Keeps the first copy of each cell, in the original order.</summary>
        public static Vector2Int[] RemoveDuplicates(Vector2Int[] pattern)
        {
            var seen = new HashSet<Vector2Int>();
            var result = new List<Vector2Int>();
            if (pattern != null)
                foreach (Vector2Int cell in pattern)
                    if (seen.Add(cell)) result.Add(cell);
            return result.ToArray();
        }

        /// <summary>Every cell of <paramref name="extra"/> added to the pattern (cells already there are not repeated).</summary>
        public static Vector2Int[] Union(Vector2Int[] pattern, Vector2Int[] extra)
        {
            var result = new List<Vector2Int>(pattern ?? new Vector2Int[0]);
            var seen = new HashSet<Vector2Int>(result);
            if (extra != null)
                foreach (Vector2Int cell in extra)
                    if (seen.Add(cell)) result.Add(cell);
            return result.ToArray();
        }

        /// <summary>The pattern without any of the cells of <paramref name="removed"/>.</summary>
        public static Vector2Int[] Subtract(Vector2Int[] pattern, Vector2Int[] removed)
        {
            var gone = new HashSet<Vector2Int>(removed ?? new Vector2Int[0]);
            var result = new List<Vector2Int>();
            if (pattern != null)
                foreach (Vector2Int cell in pattern)
                    if (!gone.Contains(cell)) result.Add(cell);
            return result.ToArray();
        }

        public static Vector2Int[] FlipVertical(Vector2Int[] pattern) { return Map(pattern, c => new Vector2Int(c.x, -c.y)); }

        public static Vector2Int[] FlipHorizontal(Vector2Int[] pattern) { return Map(pattern, c => new Vector2Int(-c.x, c.y)); }

        /// <summary>Turns the whole pattern a quarter turn counter-clockwise (the same turn as facing Up).</summary>
        public static Vector2Int[] RotateQuarterTurn(Vector2Int[] pattern) { return Map(pattern, c => Rotate(c, OperatorFacing.Up)); }

        public static Vector2Int[] Shift(Vector2Int[] pattern, Vector2Int by) { return Map(pattern, c => c + by); }

        private static Vector2Int[] Map(Vector2Int[] pattern, Func<Vector2Int, Vector2Int> change)
        {
            if (pattern == null) return new Vector2Int[0];
            var result = new Vector2Int[pattern.Length];
            for (int i = 0; i < pattern.Length; i++) result[i] = change(pattern[i]);
            return result;
        }

        // ============================
        // Shapes (all written for a right-facing operator, never including the operator's own tile)
        // ============================

        /// <summary>A straight line in front: x from <paramref name="from"/> to <paramref name="to"/> on the operator's row.</summary>
        public static Vector2Int[] Line(int from, int to)
        {
            var result = new List<Vector2Int>();
            for (int x = Mathf.Min(from, to); x <= Mathf.Max(from, to); x++)
                if (x != 0) result.Add(new Vector2Int(x, 0));
            return result.ToArray();
        }

        /// <summary>The four straight lines out from the operator, <paramref name="radius"/> tiles long.</summary>
        public static Vector2Int[] Cross(int radius)
        {
            var result = new List<Vector2Int>();
            for (int i = 1; i <= radius; i++)
            {
                result.Add(new Vector2Int(i, 0));
                result.Add(new Vector2Int(-i, 0));
                result.Add(new Vector2Int(0, i));
                result.Add(new Vector2Int(0, -i));
            }
            return result.ToArray();
        }

        /// <summary>Every tile within <paramref name="radius"/> steps (up, down, left, right), a diamond.</summary>
        public static Vector2Int[] Diamond(int radius)
        {
            var result = new List<Vector2Int>();
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if ((x != 0 || y != 0) && Mathf.Abs(x) + Mathf.Abs(y) <= radius) result.Add(new Vector2Int(x, y));
            return result.ToArray();
        }

        /// <summary>Every tile within <paramref name="radius"/> tiles in both directions, a square ring around the operator.</summary>
        public static Vector2Int[] Square(int radius)
        {
            var result = new List<Vector2Int>();
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x != 0 || y != 0) result.Add(new Vector2Int(x, y));
            return result.ToArray();
        }

        /// <summary>A block in front: <paramref name="depth"/> tiles deep and <paramref name="width"/> tiles wide, centred on the operator's row.</summary>
        public static Vector2Int[] ForwardBlock(int depth, int width)
        {
            var result = new List<Vector2Int>();
            int below = (width - 1) / 2;
            int above = width - 1 - below;
            for (int x = 1; x <= depth; x++)
                for (int y = -below; y <= above; y++)
                    result.Add(new Vector2Int(x, y));
            return result.ToArray();
        }

        /// <summary>A widening wedge in front: one tile wide at x = 1, two more per step.</summary>
        public static Vector2Int[] Cone(int depth)
        {
            var result = new List<Vector2Int>();
            for (int x = 1; x <= depth; x++)
                for (int y = -(x - 1); y <= x - 1; y++)
                    result.Add(new Vector2Int(x, y));
            return result.ToArray();
        }
    }
}
