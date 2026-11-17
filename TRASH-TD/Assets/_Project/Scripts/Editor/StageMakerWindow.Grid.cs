using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    // The middle of the Stage Maker: toolbar, the grid (drawing + mouse input) and the status line.
    public sealed partial class StageMakerWindow
    {
        private const float GridPadding = 26f;
        private const float WalkSpeedTilesPerSecond = 3.5f;

        private static readonly Color[] RouteColors =
        {
            new Color(1f, 0.55f, 0.2f), new Color(0.3f, 0.85f, 1f), new Color(0.6f, 1f, 0.4f),
            new Color(1f, 0.4f, 0.8f), new Color(1f, 0.9f, 0.3f), new Color(0.7f, 0.55f, 1f)
        };

        private Rect gridRect;
        private bool hasHover;
        private Vector2Int hoverCell;
        private bool strokeActive;
        private int strokeUndoGroup;
        private bool hasLastPaint;
        private Vector2Int lastPaint;
        private bool rectActive;
        private Vector2Int rectStart;
        private Vector2Int rectEnd;
        private bool hasHighlight;
        private Vector2Int highlightCell;
        private double highlightUntil;

        private static Color RouteColor(int spawn)
        {
            return RouteColors[((spawn % RouteColors.Length) + RouteColors.Length) % RouteColors.Length];
        }

        // ============================
        // Center column
        // ============================

        private void DrawCenterColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCenterToolbar();

            if (editingSpawn >= 0)
                EditorGUILayout.HelpBox("Drawing the route for S" + editingSpawn + ": click the tiles you want it to pass through, in order. The steps in between are filled in " +
                                        "with the game's pathfinding. Click an exit to finish. Backspace removes the last click, Esc cancels.", MessageType.None);
            else if (relocating)
                EditorGUILayout.HelpBox("Click a plain tile to move " + (relocatingSpawn ? "S" : "E") + relocatingIndex + " there. Its number stays, so waves and routes still use it. Esc cancels.", MessageType.None);

            if (StageMakerLogic.HasGrid(stage)) DrawGrid();
            else DrawNoGridPanel();

            DrawStatusLine();
            EditorGUILayout.EndVertical();
        }

        private void DrawCenterToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(StageName(stage), EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (StageMakerLogic.HasGrid(stage))
            {
                EditorGUI.BeginChangeCheck();
                bool hasArt = stage.mapVisualSprite != null;
                using (new EditorGUI.DisabledScope(!hasArt))
                {
                    showArt = GUILayout.Toggle(showArt, new GUIContent("Map art", "Show the stage's map artwork under the tiles so you can line them up."), EditorStyles.toolbarButton, GUILayout.Width(64f));
                    if (showArt && hasArt)
                        tileOpacity = GUILayout.HorizontalSlider(tileOpacity, 0.15f, 1f, GUILayout.Width(60f));
                }
                if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(ArtPrefKey, showArt);

                showRoutes = GUILayout.Toggle(showRoutes, new GUIContent("Routes", "Show the route enemies take from each spawn."), EditorStyles.toolbarButton, GUILayout.Width(54f));
                animateRoutes = GUILayout.Toggle(animateRoutes, new GUIContent("Walk", "Animate a marker walking each route."), EditorStyles.toolbarButton, GUILayout.Width(44f));
                showCoords = GUILayout.Toggle(showCoords, new GUIContent("Coords", "Show each tile's (x, y) position."), EditorStyles.toolbarButton, GUILayout.Width(52f));

                GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(34f));
                EditorGUI.BeginChangeCheck();
                cellPixels = GUILayout.HorizontalSlider(cellPixels, 20f, 80f, GUILayout.Width(90f));
                if (EditorGUI.EndChangeCheck()) EditorPrefs.SetFloat(ZoomPrefKey, cellPixels);
                if (GUILayout.Button(new GUIContent("Fit", "Size the tiles so the whole grid fits the window (key 0). Ctrl + scroll wheel zooms."), EditorStyles.toolbarButton, GUILayout.Width(30f))) FitZoom();
            }

            using (new EditorGUI.DisabledScope(!AnyDirty()))
            {
                if (GUILayout.Button(new GUIContent("Save", "Write every edited stage to disk."), EditorStyles.toolbarButton, GUILayout.Width(50f))) SaveAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawNoGridPanel()
        {
            GUILayout.Space(14f);
            EditorGUILayout.HelpBox(
                stage.comingSoon
                    ? "This stage is a placeholder with no map yet. Create a grid to start building it, then untick \"Coming soon\" on the right when it is ready to play."
                    : "This stage has no grid yet. Create one to start.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Size", GUILayout.Width(36f));
            newWidth = Mathf.Clamp(EditorGUILayout.IntField(newWidth, GUILayout.Width(50f)), StageMakerLogic.MinSize, StageMakerLogic.MaxSize);
            GUILayout.Label("x", GUILayout.Width(12f));
            newHeight = Mathf.Clamp(EditorGUILayout.IntField(newHeight, GUILayout.Width(50f)), StageMakerLogic.MinSize, StageMakerLogic.MaxSize);
            GUILayout.Space(10f);
            GUILayout.Label("Fill with", GUILayout.Width(50f));
            fillChoice = EditorGUILayout.Popup(fillChoice, FillNames, GUILayout.Width(110f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create starter layout", GUILayout.Width(170f), GUILayout.Height(26f)))
            {
                RecordForEdit("Create starter layout");
                StageMakerLogic.CreateStarter(stage, newWidth, newHeight);
                FinishEdit();
                SetStatus("Created a " + stage.gridWidth + "x" + stage.gridHeight + " starter layout: high ground with one lane from a spawn to an exit.", MessageType.Info);
            }
            if (GUILayout.Button("Create blank grid", GUILayout.Width(150f), GUILayout.Height(26f)))
            {
                RecordForEdit("Create grid");
                StageMakerLogic.CreateBlank(stage, newWidth, newHeight, FillChoices[Mathf.Clamp(fillChoice, 0, FillChoices.Length - 1)]);
                FinishEdit();
                SetStatus("Created a blank " + stage.gridWidth + "x" + stage.gridHeight + " grid. Add a spawn and an exit.", MessageType.Info);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        private void DrawStatusLine()
        {
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

            string hover = string.Empty;
            if (hasHover && StageMakerLogic.HasGrid(stage) && StageMakerLogic.InBounds(stage, hoverCell))
            {
                TileType tile = StageMakerLogic.GetTile(stage, hoverCell);
                hover = "(" + hoverCell.x + ", " + hoverCell.y + ")  " + StageMakerTiles.Label(tile);
                int spawn = StageMakerLogic.IndexOf(stage.spawnPoints, hoverCell);
                int exit = StageMakerLogic.IndexOf(stage.exitPoints, hoverCell);
                if (spawn >= 0) hover += "  S" + spawn;
                if (exit >= 0) hover += "  E" + exit;
            }
            EditorGUILayout.LabelField(hover, EditorStyles.miniLabel);
        }

        // ============================
        // The grid
        // ============================

        private void DrawGrid()
        {
            float width = stage.gridWidth * cellPixels;
            float height = stage.gridHeight * cellPixels;

            gridScroll = EditorGUILayout.BeginScrollView(gridScroll);
            Rect content = GUILayoutUtility.GetRect(width + GridPadding * 2f, height + GridPadding * 2f,
                GUILayout.Width(width + GridPadding * 2f), GUILayout.Height(height + GridPadding * 2f));
            gridRect = new Rect(content.x + GridPadding, content.y + GridPadding, width, height);

            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            if (e.type == EventType.Repaint) DrawGridVisuals(content);
            HandleGridEvent(e, id);

            EditorGUILayout.EndScrollView();
        }

        private Rect CellRect(int x, int y)
        {
            return new Rect(gridRect.x + x * cellPixels, gridRect.y + (stage.gridHeight - 1 - y) * cellPixels, cellPixels, cellPixels);
        }

        private Vector2 CellCenter(Vector2Int cell)
        {
            return CellRect(cell.x, cell.y).center;
        }

        private bool TryGetCell(Vector2 position, out Vector2Int cell)
        {
            cell = default;
            if (!gridRect.Contains(position)) return false;
            cell = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((position.x - gridRect.x) / cellPixels), 0, stage.gridWidth - 1),
                Mathf.Clamp(stage.gridHeight - 1 - Mathf.FloorToInt((position.y - gridRect.y) / cellPixels), 0, stage.gridHeight - 1));
            return true;
        }

        private Vector2Int ClampedCell(Vector2 position)
        {
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((position.x - gridRect.x) / cellPixels), 0, stage.gridWidth - 1),
                Mathf.Clamp(stage.gridHeight - 1 - Mathf.FloorToInt((position.y - gridRect.y) / cellPixels), 0, stage.gridHeight - 1));
        }

        // ---------- drawing ----------

        private void DrawGridVisuals(Rect content)
        {
            EditorGUI.DrawRect(content, new Color(0.09f, 0.09f, 0.1f));
            int width = stage.gridWidth;
            int height = stage.gridHeight;

            bool art = showArt && stage.mapVisualSprite != null;
            if (art) DrawMapArt();

            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, art ? tileOpacity : 1f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    StageMakerTiles.Draw(CellRect(x, y), stage.GetTile(x, y));
            }
            GUI.color = oldColor;

            Color line = new Color(0f, 0f, 0f, 0.4f);
            for (int x = 0; x <= width; x++)
                EditorGUI.DrawRect(new Rect(gridRect.x + x * cellPixels - 0.5f, gridRect.y, 1f, gridRect.height), line);
            for (int y = 0; y <= height; y++)
                EditorGUI.DrawRect(new Rect(gridRect.x, gridRect.y + y * cellPixels - 0.5f, gridRect.width, 1f), line);

            DrawAxisLabels();
            DrawCellLabels();
            DrawHoleLink();
            if (showRoutes) DrawRoutes();
            DrawRouteDraft();
            DrawHoverAndSelection();
        }

        private void DrawMapArt()
        {
            Sprite sprite = stage.mapVisualSprite;
            Texture2D texture = sprite.texture;
            if (texture == null) return;

            Rect textureRect = sprite.textureRect;
            var uv = new Rect(textureRect.x / texture.width, textureRect.y / texture.height, textureRect.width / texture.width, textureRect.height / texture.height);
            float pixelsPerTile = Mathf.Max(1, stage.visualTilePixelSize);
            float tilesWide = textureRect.width / pixelsPerTile;
            float tilesHigh = textureRect.height / pixelsPerTile;

            float left = gridRect.x + stage.visualTileOffset.x * cellPixels;
            float bottom = gridRect.yMax - stage.visualTileOffset.y * cellPixels;
            var destination = new Rect(left, bottom - tilesHigh * cellPixels, tilesWide * cellPixels, tilesHigh * cellPixels);
            GUI.DrawTextureWithTexCoords(destination, texture, uv);
        }

        private void DrawAxisLabels()
        {
            if (cellPixels < 24f && !showCoords) return;
            for (int x = 0; x < stage.gridWidth; x++)
                GUI.Label(new Rect(gridRect.x + x * cellPixels, gridRect.yMax + 2f, cellPixels, 16f), x.ToString(), axisLabelStyle);
            for (int y = 0; y < stage.gridHeight; y++)
                GUI.Label(new Rect(gridRect.x - GridPadding + 2f, CellRect(0, y).y + cellPixels * 0.5f - 8f, GridPadding - 6f, 16f), y.ToString(), axisLabelStyle);
        }

        private void DrawOutlinedLabel(Rect rect, string text, Color color)
        {
            Color old = GUI.contentColor;
            GUI.contentColor = Color.black;
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, cellLabelStyle);
            GUI.contentColor = color;
            GUI.Label(rect, text, cellLabelStyle);
            GUI.contentColor = old;
        }

        private void DrawCellLabels()
        {
            if (cellPixels < 26f) return;

            // Numbered spawns and exits (waves and routes refer to these numbers).
            for (int i = 0; stage.spawnPoints != null && i < stage.spawnPoints.Length; i++)
            {
                Vector2Int p = stage.spawnPoints[i];
                if (StageMakerLogic.InBounds(stage, p)) DrawOutlinedLabel(CellRect(p.x, p.y), "S" + i, Color.white);
            }
            for (int i = 0; stage.exitPoints != null && i < stage.exitPoints.Length; i++)
            {
                Vector2Int p = stage.exitPoints[i];
                if (StageMakerLogic.InBounds(stage, p)) DrawOutlinedLabel(CellRect(p.x, p.y), "E" + i, Color.white);
            }

            for (int y = 0; y < stage.gridHeight; y++)
            {
                for (int x = 0; x < stage.gridWidth; x++)
                {
                    var cell = new Vector2Int(x, y);
                    TileType tile = stage.GetTile(x, y);
                    Rect rect = CellRect(x, y);
                    if (tile == TileType.SpawnPoint && StageMakerLogic.IndexOf(stage.spawnPoints, cell) < 0) DrawOutlinedLabel(rect, "S?", ErrorColor);
                    else if (tile == TileType.ExitPoint && StageMakerLogic.IndexOf(stage.exitPoints, cell) < 0) DrawOutlinedLabel(rect, "E?", ErrorColor);
                    else if (StageMakerTiles.Badge(tile).Length > 0) DrawOutlinedLabel(rect, StageMakerTiles.Badge(tile), Color.white);

                    if (showCoords && cellPixels >= 32f)
                        GUI.Label(new Rect(rect.x + 2f, rect.y, rect.width, 14f), x + "," + y, axisLabelStyle);
                }
            }
        }

        private void DrawRoutes()
        {
            foreach (StageRoute route in routes)
            {
                if (route.cells == null || route.cells.Count == 0) continue;
                // While a route is being drawn, the route it replaces is shown faint.
                float alpha = editingSpawn >= 0 ? (route.spawn == editingSpawn ? 0.35f : 0.25f) : 0.95f;
                Color color = RouteColor(route.spawn);
                color.a = alpha;
                DrawPolyline(route.cells, color, route.custom ? 5f : 3.5f);
                if (route.cells.Count > 1 && editingSpawn < 0) DrawArrows(route.cells, color);
            }

            if (animateRoutes && editingSpawn < 0)
            {
                double time = EditorApplication.timeSinceStartup;
                foreach (StageRoute route in routes)
                {
                    if (route.cells == null || route.cells.Count < 2) continue;
                    float last = route.cells.Count - 1;
                    float position = Mathf.Min((float)((time * WalkSpeedTilesPerSecond) % (last + 3f)), last);
                    int index = Mathf.Min(Mathf.FloorToInt(position), route.cells.Count - 2);
                    Vector2 point = Vector2.Lerp(CellCenter(route.cells[index]), CellCenter(route.cells[index + 1]), position - index);
                    Handles.color = Color.black;
                    Handles.DrawSolidDisc(point, Vector3.forward, cellPixels * 0.2f + 1.5f);
                    Handles.color = RouteColor(route.spawn);
                    Handles.DrawSolidDisc(point, Vector3.forward, cellPixels * 0.2f);
                }
            }
        }

        private void DrawPolyline(IList<Vector2Int> cells, Color color, float width)
        {
            var points = new Vector3[cells.Count];
            for (int i = 0; i < cells.Count; i++) points[i] = CellCenter(cells[i]);
            Handles.color = new Color(0f, 0f, 0f, color.a * 0.6f);
            Handles.DrawAAPolyLine(width + 2f, points);
            Handles.color = color;
            Handles.DrawAAPolyLine(width, points);
        }

        private void DrawArrows(IList<Vector2Int> cells, Color color)
        {
            float size = Mathf.Max(5f, cellPixels * 0.16f);
            for (int i = 1; i < cells.Count; i += 3)
            {
                Vector2 from = CellCenter(cells[i - 1]);
                Vector2 to = CellCenter(cells[i]);
                Vector2 direction = to - from;
                if (direction.sqrMagnitude < 0.01f || direction.magnitude > cellPixels * 1.5f) continue; // skip hole jumps
                direction.Normalize();
                Vector2 side = new Vector2(-direction.y, direction.x);
                Vector2 center = (from + to) * 0.5f;
                Handles.color = Color.black;
                Handles.DrawAAConvexPolygon(center + direction * (size + 1.5f), center - direction * size + side * (size + 1.5f) * 0.8f, center - direction * size - side * (size + 1.5f) * 0.8f);
                Handles.color = color;
                Handles.DrawAAConvexPolygon(center + direction * size, center - direction * size + side * size * 0.8f, center - direction * size - side * size * 0.8f);
            }
        }

        private void DrawOutline(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawHoverAndSelection()
        {
            bool busy = editingSpawn >= 0 || relocating;

            if (rectActive)
            {
                Rect box = RectBetween(rectStart, rectEnd);
                EditorGUI.DrawRect(box, new Color(1f, 1f, 1f, 0.18f));
                DrawOutline(box, new Color(1f, 0.9f, 0.3f), 2f);
                DrawOutlinedLabel(new Rect(box.center.x - 40f, box.y - 18f, 80f, 16f),
                    (Mathf.Abs(rectEnd.x - rectStart.x) + 1) + "x" + (Mathf.Abs(rectEnd.y - rectStart.y) + 1), Color.white);
            }
            else if (hasHover && StageMakerLogic.InBounds(stage, hoverCell))
            {
                Rect rect = CellRect(hoverCell.x, hoverCell.y);
                if (!busy && (tool == Tool.Brush || tool == Tool.Rectangle))
                {
                    Color old = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, 0.6f);
                    foreach (Vector2Int covered in BrushCells(hoverCell))
                    {
                        if (StageMakerLogic.InBounds(stage, covered)) StageMakerTiles.Draw(CellRect(covered.x, covered.y), brush);
                    }
                    GUI.color = old;
                }
                DrawOutline(rect, new Color(1f, 0.9f, 0.3f), 2f);
            }

            if (hasHighlight && StageMakerLogic.InBounds(stage, highlightCell))
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin((float)EditorApplication.timeSinceStartup * 8f);
                DrawOutline(CellRect(highlightCell.x, highlightCell.y), new Color(1f, 0.25f, 0.2f, pulse), 3f);
            }
        }

        private Rect RectBetween(Vector2Int a, Vector2Int b)
        {
            int minX = Mathf.Min(a.x, b.x);
            int maxX = Mathf.Max(a.x, b.x);
            int minY = Mathf.Min(a.y, b.y);
            int maxY = Mathf.Max(a.y, b.y);
            Rect topLeft = CellRect(minX, maxY);
            return new Rect(topLeft.x, topLeft.y, (maxX - minX + 1) * cellPixels, (maxY - minY + 1) * cellPixels);
        }

        // ---------- input ----------

        private void HandleGridEvent(Event e, int id)
        {
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseMove:
                    UpdateHover(e.mousePosition);
                    break;

                case EventType.MouseLeaveWindow:
                    if (hasHover) { hasHover = false; Repaint(); }
                    break;

                case EventType.ScrollWheel:
                    if (e.control || e.command)
                    {
                        cellPixels = Mathf.Clamp(cellPixels - e.delta.y * 2f, 20f, 80f);
                        EditorPrefs.SetFloat(ZoomPrefKey, cellPixels);
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseDown:
                    if (TryGetCell(e.mousePosition, out Vector2Int downCell)) OnGridMouseDown(e, id, downCell);
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id) OnGridMouseDrag(e);
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) OnGridMouseUp(e);
                    break;

                case EventType.DragUpdated:
                case EventType.DragPerform:
                    OnGridDrag(e);
                    break;

                case EventType.DragExited:
                    if (hasHover) { hasHover = false; Repaint(); }
                    break;
            }
        }

        private void UpdateHover(Vector2 mouse)
        {
            bool had = hasHover;
            Vector2Int old = hoverCell;
            hasHover = TryGetCell(mouse, out hoverCell);
            if (had != hasHover || old != hoverCell) Repaint();
        }

        private void OnGridMouseDown(Event e, int id, Vector2Int cell)
        {
            hasHover = true;
            hoverCell = cell;

            if (e.button == 1 || (e.button == 0 && e.alt))
            {
                PickAt(cell);
                e.Use();
                return;
            }
            if (e.button != 0) return;

            if (editingSpawn >= 0)
            {
                ClickRouteCell(cell);
                e.Use();
                Repaint();
                return;
            }
            if (relocating)
            {
                CompleteRelocate(cell);
                e.Use();
                Repaint();
                return;
            }

            bool pointBrush = brush == TileType.SpawnPoint || brush == TileType.ExitPoint;
            switch (tool)
            {
                case Tool.Pick:
                    PickAt(cell);
                    break;

                case Tool.Fill:
                    FloodFill(cell);
                    break;

                case Tool.Brush:
                    StartStroke();
                    PlaceBrush(cell, true);
                    lastPaint = cell;
                    hasLastPaint = true;
                    if (!pointBrush) GUIUtility.hotControl = id;
                    else EndStroke();
                    break;

                case Tool.Rectangle:
                    if (pointBrush)
                    {
                        StartStroke();
                        PlaceTile(cell, brush, true);
                        EndStroke();
                    }
                    else
                    {
                        rectActive = true;
                        rectStart = cell;
                        rectEnd = cell;
                        GUIUtility.hotControl = id;
                    }
                    break;
            }

            e.Use();
            Repaint();
        }

        private void OnGridMouseDrag(Event e)
        {
            Vector2Int cell = ClampedCell(e.mousePosition);
            hasHover = gridRect.Contains(e.mousePosition);
            hoverCell = cell;

            if (rectActive)
            {
                rectEnd = cell;
            }
            else if (strokeActive && tool == Tool.Brush)
            {
                Vector2Int from = hasLastPaint ? lastPaint : cell;
                foreach (Vector2Int step in Line(from, cell)) PlaceBrush(step, false);
                lastPaint = cell;
                hasLastPaint = true;
            }

            e.Use();
            Repaint();
        }

        private void OnGridMouseUp(Event e)
        {
            GUIUtility.hotControl = 0;
            if (rectActive)
            {
                rectActive = false;
                ApplyRectangle(rectStart, rectEnd);
            }
            EndStroke();
            e.Use();
            Repaint();
        }

        private void OnGridDrag(Event e)
        {
            if (!(DragAndDrop.GetGenericData(DragKey) is int tileValue)) return;

            bool allowed = editingSpawn < 0 && !relocating;
            if (!allowed || !TryGetCell(e.mousePosition, out Vector2Int cell))
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            hasHover = true;
            hoverCell = cell;

            if (e.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                brush = (TileType)tileValue;
                StartStroke();
                PlaceTile(cell, brush, true);
                EndStroke();
            }

            e.Use();
            Repaint();
        }

        // ---------- editing the grid ----------

        private void StartStroke()
        {
            Undo.IncrementCurrentGroup();
            strokeUndoGroup = Undo.GetCurrentGroup();
            strokeActive = true;
        }

        private void EndStroke()
        {
            if (strokeActive) Undo.CollapseUndoOperations(strokeUndoGroup);
            strokeActive = false;
            hasLastPaint = false;
        }

        /// <summary>The cells the brush covers around a centre cell (1x1, 2x2 or 3x3; spawn and exit are always one tile).</summary>
        private IEnumerable<Vector2Int> BrushCells(Vector2Int center)
        {
            bool pointBrush = brush == TileType.SpawnPoint || brush == TileType.ExitPoint;
            int size = pointBrush || tool != Tool.Brush ? 1 : brushSize;
            int low = -(size - 1) / 2;
            for (int dy = 0; dy < size; dy++)
                for (int dx = 0; dx < size; dx++)
                    yield return new Vector2Int(center.x + low + dx, center.y + low + dy);
        }

        private void PlaceBrush(Vector2Int center, bool fromClick)
        {
            foreach (Vector2Int cell in BrushCells(center))
            {
                if (!StageMakerLogic.InBounds(stage, cell)) continue;
                // Only the clicked tile may create or replace a numbered spawn/exit; the rest of a big brush never does.
                PlaceTile(cell, brush, fromClick && cell == center);
            }
        }

        /// <summary>A dotted line between the two holes: enemies that enter one come out of the other.</summary>
        private void DrawHoleLink()
        {
            Vector2Int? first = null;
            Vector2Int? second = null;
            int count = 0;
            for (int y = 0; y < stage.gridHeight; y++)
            {
                for (int x = 0; x < stage.gridWidth; x++)
                {
                    if (stage.GetTile(x, y) != TileType.Hole) continue;
                    count++;
                    if (first == null) first = new Vector2Int(x, y);
                    else if (second == null) second = new Vector2Int(x, y);
                }
            }
            if (count != 2) return;

            Handles.color = new Color(0.8f, 0.55f, 1f, 0.9f);
            Handles.DrawDottedLine(CellCenter(first.Value), CellCenter(second.Value), 5f);
        }

        private void PickAt(Vector2Int cell)
        {
            brush = stage.GetTile(cell.x, cell.y);
            SetStatus("Picked " + StageMakerTiles.Label(brush) + ".", MessageType.None);
        }

        /// <summary>
        /// Puts a tile on one cell. A click (or drop) may create or replace a numbered spawn/exit (asking first when that renumbers
        /// things); dragging a brush across the grid never creates or removes one.
        /// </summary>
        private void PlaceTile(Vector2Int cell, TileType type, bool fromClick)
        {
            if (!StageMakerLogic.LayoutMatchesGrid(stage))
            {
                SetStatus("The tile layout does not match the grid size. Use the repair button under Checks first.", MessageType.Warning);
                return;
            }

            bool isPoint = type == TileType.SpawnPoint || type == TileType.ExitPoint;
            bool onPoint = StageMakerLogic.IsPointCell(stage, cell);
            if (!fromClick && (isPoint || onPoint)) return;

            bool dialogShown = false;
            if (onPoint)
            {
                PointRemovalImpact impact = StageMakerLogic.GetRemovalImpact(stage, cell, type);
                if (impact.NeedsConfirm)
                {
                    dialogShown = true;
                    if (!EditorUtility.DisplayDialog("Remove " + (impact.isSpawn ? "spawn S" : "exit E") + impact.index + "?", impact.Describe(), "Remove", "Cancel"))
                    {
                        GUIUtility.ExitGUI();
                        return;
                    }
                }
            }

            RecordForEdit("Place " + StageMakerTiles.Label(type));
            if (StageMakerLogic.SetTile(stage, cell, type))
            {
                FinishEdit();
                if (type == TileType.SpawnPoint) SetStatus("Added spawn S" + (stage.spawnPoints.Length - 1) + ". Waves choose a spawn by its number.", MessageType.Info);
                else if (type == TileType.ExitPoint) SetStatus("Added exit E" + (stage.exitPoints.Length - 1) + ".", MessageType.Info);
                else if (onPoint) SetStatus("Removed a spawn/exit. Later numbers shifted down and waves and routes followed.", MessageType.Info);
            }

            if (dialogShown) GUIUtility.ExitGUI();
        }

        private void ApplyRectangle(Vector2Int a, Vector2Int b)
        {
            if (!StageMakerLogic.LayoutMatchesGrid(stage)) return;
            StartStroke();
            RecordForEdit("Fill rectangle");
            int changed = 0;
            for (int y = Mathf.Min(a.y, b.y); y <= Mathf.Max(a.y, b.y); y++)
            {
                for (int x = Mathf.Min(a.x, b.x); x <= Mathf.Max(a.x, b.x); x++)
                {
                    var cell = new Vector2Int(x, y);
                    if (StageMakerLogic.IsPointCell(stage, cell)) continue; // never overwrite a numbered spawn or exit in bulk
                    if (StageMakerLogic.SetTile(stage, cell, brush)) changed++;
                }
            }
            if (changed > 0) FinishEdit();
            EndStroke();
        }

        private void FloodFill(Vector2Int start)
        {
            if (!StageMakerLogic.LayoutMatchesGrid(stage)) return;
            if (brush == TileType.SpawnPoint || brush == TileType.ExitPoint) return;
            if (StageMakerLogic.IsPointCell(stage, start)) return;

            TileType target = stage.GetTile(start.x, start.y);
            if (target == brush) return;

            StartStroke();
            RecordForEdit("Fill");
            var queue = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int> { start };
            queue.Enqueue(start);
            int changed = 0;
            Vector2Int[] steps = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (StageMakerLogic.SetTile(stage, cell, brush)) changed++;
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (!StageMakerLogic.InBounds(stage, next) || seen.Contains(next)) continue;
                    if (stage.GetTile(next.x, next.y) != target || StageMakerLogic.IsPointCell(stage, next)) continue;
                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            if (changed > 0) FinishEdit();
            EndStroke();
        }

        /// <summary>The cells on the straight line between two cells, both included (Bresenham).</summary>
        private static IEnumerable<Vector2Int> Line(Vector2Int a, Vector2Int b)
        {
            int x = a.x;
            int y = a.y;
            int dx = Mathf.Abs(b.x - a.x);
            int dy = -Mathf.Abs(b.y - a.y);
            int sx = a.x < b.x ? 1 : -1;
            int sy = a.y < b.y ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                yield return new Vector2Int(x, y);
                if (x == b.x && y == b.y) yield break;
                int doubled = 2 * error;
                if (doubled >= dy) { error += dy; x += sx; }
                if (doubled <= dx) { error += dx; y += sy; }
            }
        }
    }
}
