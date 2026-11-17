using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    // Drawing a spawn's route by hand, and moving spawns and exits.
    // A hand-drawn route is stored in StageData.enemyPaths as a contiguous list of cells from the spawn to an exit;
    // the steps between the tiles you click are filled in with the game's own A* (StageMakerPathfinder).
    public sealed partial class StageMakerWindow
    {
        private int editingSpawn = -1;
        private readonly List<Vector2Int> draft = new List<Vector2Int>();
        // The length of the draft after each click, so one Backspace removes one click (with the steps it added).
        private readonly List<int> draftMarks = new List<int>();

        private List<Vector2Int> hoverSegment;
        private Vector2Int hoverSegmentFrom;
        private Vector2Int hoverSegmentTo;
        private int hoverSegmentRevision = -1;

        private bool relocating;
        private bool relocatingSpawn;
        private int relocatingIndex;

        private void HighlightCell(Vector2Int cell)
        {
            highlightCell = cell;
            hasHighlight = true;
            highlightUntil = EditorApplication.timeSinceStartup + 2.5;
            Repaint();
        }

        // ============================
        // Hand-drawn routes
        // ============================

        private void StartRouteEdit(int spawn)
        {
            if (stage.spawnPoints == null || spawn < 0 || spawn >= stage.spawnPoints.Length) return;
            relocating = false;
            editingSpawn = spawn;
            ResetDraft();
            SetStatus(string.Empty, MessageType.None);
            Repaint();
        }

        private void ResetDraft()
        {
            draft.Clear();
            draftMarks.Clear();
            hoverSegment = null;
            if (editingSpawn < 0) return;
            draft.Add(stage.spawnPoints[editingSpawn]);
            draftMarks.Add(1);
        }

        private void CancelRouteEdit()
        {
            editingSpawn = -1;
            draft.Clear();
            draftMarks.Clear();
            hoverSegment = null;
            Repaint();
        }

        private void ClickRouteCell(Vector2Int cell)
        {
            if (draft.Count == 0 || !StageMakerLogic.InBounds(stage, cell)) return;

            TileType tile = stage.GetTile(cell.x, cell.y);
            if (!StageMakerLogic.IsWalkable(tile))
            {
                SetStatus("Enemies cannot walk on " + StageMakerTiles.Label(tile) + " tiles. Pick a low ground, trap, hole, spawn or exit tile.", MessageType.Warning);
                return;
            }

            Vector2Int last = draft[draft.Count - 1];
            if (cell == last) return;

            List<Vector2Int> segment = GetPathfinder().FindPath(last, cell);
            if (segment == null || segment.Count < 2)
            {
                SetStatus("There is no walkable route from the last tile to that one.", MessageType.Warning);
                return;
            }

            for (int i = 1; i < segment.Count; i++) draft.Add(segment[i]);
            draftMarks.Add(draft.Count);
            SetStatus(string.Empty, MessageType.None);

            if (StageMakerLogic.IndexOf(stage.exitPoints, cell) >= 0) ApplyRoute();
        }

        private void UndoRoutePoint()
        {
            if (draftMarks.Count <= 1) return;
            draftMarks.RemoveAt(draftMarks.Count - 1);
            int keep = draftMarks[draftMarks.Count - 1];
            draft.RemoveRange(keep, draft.Count - keep);
            hoverSegment = null;
            Repaint();
        }

        private void AutoFinishRoute()
        {
            if (draft.Count == 0) return;
            List<Vector2Int> rest = GetPathfinder().FindPathToNearestExit(draft[draft.Count - 1]);
            if (rest == null || rest.Count == 0)
            {
                SetStatus("There is no walkable route from the last tile to any exit.", MessageType.Warning);
                return;
            }
            for (int i = 1; i < rest.Count; i++) draft.Add(rest[i]);
            draftMarks.Add(draft.Count);
            ApplyRoute();
        }

        private bool DraftReachesExit()
        {
            return draft.Count >= 2 && StageMakerLogic.IndexOf(stage.exitPoints, draft[draft.Count - 1]) >= 0;
        }

        private void ApplyRoute()
        {
            if (!DraftReachesExit())
            {
                SetStatus("The route has to end on an exit. Click an exit tile, or use Auto-finish.", MessageType.Warning);
                return;
            }

            int spawn = editingSpawn;
            int tiles = draft.Count;
            RecordForEdit("Draw route");
            StageMakerLogic.SetCustomWaypoints(stage, spawn, draft);
            FinishEdit();
            CancelRouteEdit();
            SetStatus("Saved a hand-drawn route for S" + spawn + " (" + tiles + " tiles). Reset it from the spawn list to go back to automatic routing.", MessageType.Info);
        }

        private void ResetRoute(int spawn)
        {
            RecordForEdit("Reset route");
            StageMakerLogic.SetAutoRoute(stage, spawn, -1);
            FinishEdit();
            SetStatus("S" + spawn + " now uses automatic routing to the nearest exit.", MessageType.Info);
        }

        /// <summary>Draws the route being built, plus a preview of the next step to the tile under the mouse.</summary>
        private void DrawRouteDraft()
        {
            if (editingSpawn < 0 || draft.Count == 0) return;

            Vector2Int last = draft[draft.Count - 1];
            if (hasHover && StageMakerLogic.InBounds(stage, hoverCell) && hoverCell != last &&
                StageMakerLogic.IsWalkable(stage.GetTile(hoverCell.x, hoverCell.y)))
            {
                if (hoverSegment == null || hoverSegmentFrom != last || hoverSegmentTo != hoverCell || hoverSegmentRevision != revision)
                {
                    hoverSegment = GetPathfinder().FindPath(last, hoverCell);
                    hoverSegmentFrom = last;
                    hoverSegmentTo = hoverCell;
                    hoverSegmentRevision = revision;
                }
                if (hoverSegment != null && hoverSegment.Count > 1) DrawPolyline(hoverSegment, new Color(1f, 1f, 1f, 0.6f), 3f);
            }

            Color color = RouteColor(editingSpawn);
            if (draft.Count >= 2) DrawPolyline(draft, color, 6f);

            // A dot on every tile you clicked, and a ring on the spawn.
            foreach (int mark in draftMarks)
            {
                Vector2 point = CellCenter(draft[mark - 1]);
                Handles.color = Color.black;
                Handles.DrawSolidDisc(point, Vector3.forward, cellPixels * 0.16f + 1.5f);
                Handles.color = Color.white;
                Handles.DrawSolidDisc(point, Vector3.forward, cellPixels * 0.16f);
            }
        }

        // ============================
        // Moving spawns and exits
        // ============================

        private void StartRelocate(bool spawn, int index)
        {
            if (editingSpawn >= 0) CancelRouteEdit();
            relocating = true;
            relocatingSpawn = spawn;
            relocatingIndex = index;
            SetStatus(string.Empty, MessageType.None);
            Repaint();
        }

        private void CompleteRelocate(Vector2Int cell)
        {
            bool spawn = relocatingSpawn;
            int index = relocatingIndex;
            RecordForEdit("Move " + (spawn ? "spawn" : "exit"));
            if (StageMakerLogic.MovePoint(stage, spawn, index, cell))
            {
                FinishEdit();
                SetStatus((spawn ? "S" : "E") + index + " moved to (" + cell.x + ", " + cell.y + ")." +
                          (spawn && StageMakerLogic.HasCustomWaypoints(stage, index) ? " Its hand-drawn route still starts at the old tile: redraw or reset it." : string.Empty),
                    MessageType.Info);
            }
            else
            {
                SetStatus("Pick a plain tile (not another spawn or exit) to move it to.", MessageType.Warning);
            }
            relocating = false;
        }
    }
}
