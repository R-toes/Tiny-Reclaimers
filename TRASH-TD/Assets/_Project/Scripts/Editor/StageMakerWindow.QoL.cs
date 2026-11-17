using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    // Quality-of-life: resizable side panels, keyboard shortcuts, fit-to-window zoom, and the grid box (size, copy layout, clear).
    //
    // Keyboard (while the Stage Maker window has focus and no text field is being edited):
    //   1-8 pick a tile (the number is shown in the palette)     B / R / F / I   Brush, Rectangle, Fill, Pick
    //   [ and ]  brush size down / up                            0   fit the grid to the window
    //   Ctrl+S   save the edited stages                          Esc cancel a route / move      Backspace  undo the last route click
    // Mouse: Ctrl + scroll wheel zooms the grid; right-click or Alt+click picks the tile under the mouse; drag the thin bars
    // between the panels to resize them.
    public sealed partial class StageMakerWindow
    {
        private int copySourceIndex;

        // ============================
        // Resizable panels
        // ============================

        /// <summary>A thin draggable bar between two columns. <paramref name="left"/>: the bar after the left panel, otherwise the one before the right panel.</summary>
        private void DrawSplitter(bool left)
        {
            Rect bar = GUILayoutUtility.GetRect(8f, 8f, GUILayout.Width(8f), GUILayout.ExpandHeight(true));
            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;

            switch (e.GetTypeForControl(id))
            {
                case EventType.Repaint:
                    bool active = GUIUtility.hotControl == id;
                    EditorGUI.DrawRect(new Rect(bar.x + 3f, bar.y, 2f, bar.height), active ? new Color(0.35f, 0.6f, 0.95f, 0.9f) : new Color(0f, 0f, 0f, 0.35f));
                    EditorGUIUtility.AddCursorRect(bar, MouseCursor.ResizeHorizontal);
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && bar.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (left) leftWidth = Mathf.Clamp(leftWidth + e.delta.x, MinPanelWidth, MaxPanelWidth);
                        else rightWidth = Mathf.Clamp(rightWidth - e.delta.x, MinPanelWidth, MaxPanelWidth);
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        EditorPrefs.SetFloat(left ? LeftWidthPrefKey : RightWidthPrefKey, left ? leftWidth : rightWidth);
                        e.Use();
                    }
                    break;
            }
        }

        // ============================
        // Zoom
        // ============================

        /// <summary>Sizes the tiles so the whole grid fits in the middle of the window.</summary>
        private void FitZoom()
        {
            if (!StageMakerLogic.HasGrid(stage)) return;
            float availableWidth = position.width - leftWidth - rightWidth - 16f - GridPadding * 2f - 24f;
            float availableHeight = position.height - 150f - GridPadding * 2f;
            float fitted = Mathf.Floor(Mathf.Min(availableWidth / stage.gridWidth, availableHeight / stage.gridHeight));
            cellPixels = Mathf.Clamp(fitted, 20f, 80f);
            EditorPrefs.SetFloat(ZoomPrefKey, cellPixels);
            Repaint();
        }

        // ============================
        // Keyboard shortcuts
        // ============================

        private static int NumberFromKey(KeyCode key)
        {
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9) return key - KeyCode.Alpha0;
            if (key >= KeyCode.Keypad0 && key <= KeyCode.Keypad9) return key - KeyCode.Keypad0;
            return -1;
        }

        private void HandleShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown || EditorGUIUtility.editingTextField) return;

            bool command = e.control || e.command;
            bool busy = editingSpawn >= 0 || relocating;
            bool hasGrid = StageMakerLogic.HasGrid(stage);

            if (e.keyCode == KeyCode.Escape)
            {
                if (editingSpawn >= 0) { CancelRouteEdit(); e.Use(); }
                else if (relocating) { relocating = false; SetStatus("Move cancelled.", MessageType.None); e.Use(); Repaint(); }
                return;
            }

            if ((e.keyCode == KeyCode.Backspace || e.keyCode == KeyCode.Delete) && editingSpawn >= 0)
            {
                UndoRoutePoint();
                e.Use();
                return;
            }

            if (command && e.keyCode == KeyCode.S)
            {
                SaveAll();
                e.Use();
                Repaint();
                return;
            }

            if (command || e.alt || !hasGrid) return;

            int number = NumberFromKey(e.keyCode);
            if (number == 0)
            {
                FitZoom();
                e.Use();
                return;
            }
            if (number >= 1 && number <= 8 && !busy)
            {
                brush = StageMakerTiles.PaletteOrder[number - 1];
                if (tool == Tool.Pick) tool = Tool.Brush;
                e.Use();
                Repaint();
                return;
            }

            if (busy) return;
            switch (e.keyCode)
            {
                case KeyCode.B: tool = Tool.Brush; break;
                case KeyCode.R: tool = Tool.Rectangle; break;
                case KeyCode.F: tool = Tool.Fill; break;
                case KeyCode.I: tool = Tool.Pick; break;
                case KeyCode.LeftBracket: brushSize = Mathf.Max(1, brushSize - 1); break;
                case KeyCode.RightBracket: brushSize = Mathf.Min(3, brushSize + 1); break;
                default: return;
            }
            e.Use();
            Repaint();
        }

        // ============================
        // The grid box: size, copy layout, clear
        // ============================

        private void DrawLayoutBox()
        {
            EditorGUILayout.LabelField("GRID", EditorStyles.boldLabel);
            bool busy = editingSpawn >= 0 || relocating;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Size", GUILayout.Width(32f));
            newWidth = Mathf.Clamp(EditorGUILayout.IntField(newWidth, GUILayout.Width(46f)), StageMakerLogic.MinSize, StageMakerLogic.MaxSize);
            GUILayout.Label("x", GUILayout.Width(12f));
            newHeight = Mathf.Clamp(EditorGUILayout.IntField(newHeight, GUILayout.Width(46f)), StageMakerLogic.MinSize, StageMakerLogic.MaxSize);
            GUILayout.Label(new GUIContent("new tiles", "What the tiles added by growing the grid (and a cleared grid) are filled with."), EditorStyles.miniLabel, GUILayout.Width(52f));
            fillChoice = EditorGUILayout.Popup(fillChoice, FillNames);
            EditorGUILayout.EndHorizontal();

            bool changed = newWidth != stage.gridWidth || newHeight != stage.gridHeight;
            using (new EditorGUI.DisabledScope(!changed || busy))
            {
                if (GUILayout.Button(new GUIContent("Resize (keeps tiles from the bottom-left)", "Spawns, exits and routes outside a smaller grid are removed."))) ApplyResize();
            }

            DrawCopyLayout(busy);

            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button(new GUIContent("Clear grid", "Fill every tile with the 'new tiles' type and remove all spawns, exits and routes."))) ClearGrid();
            }

            if (stage.mapVisualSprite != null)
                EditorGUILayout.LabelField("This stage has map art sized for the current grid. Resizing does not move or scale the art; the Checks panel tells you if it no longer fits.", hintStyle);
        }

        private void DrawCopyLayout(bool busy)
        {
            var candidates = new List<StageData>();
            foreach (StageData s in stages)
                if (s != stage && StageMakerLogic.HasGrid(s)) candidates.Add(s);
            if (candidates.Count == 0) return;

            var names = new string[candidates.Count];
            for (int i = 0; i < names.Length; i++) names[i] = StageName(candidates[i]);
            copySourceIndex = Mathf.Clamp(copySourceIndex, 0, names.Length - 1);

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            copySourceIndex = EditorGUILayout.Popup(copySourceIndex, names);
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button(new GUIContent("Copy layout", "Start from another stage's map: grid size, tiles, spawns, exits and routes. Art and waves are kept."), GUILayout.Width(96f)))
                    CopyLayoutFrom(candidates[copySourceIndex]);
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ApplyResize()
        {
            bool shrinking = newWidth < stage.gridWidth || newHeight < stage.gridHeight;
            if (shrinking && !EditorUtility.DisplayDialog("Shrink the grid?",
                    "Tiles outside the new " + newWidth + "x" + newHeight + " size are removed, including any spawn, exit or hand-drawn route there. You can undo it with Ctrl+Z.",
                    "Shrink", "Cancel"))
                return;

            RecordForEdit("Resize grid");
            string notes = StageMakerLogic.Resize(stage, newWidth, newHeight, FillChoices[Mathf.Clamp(fillChoice, 0, FillChoices.Length - 1)]);
            FinishEdit();
            SetStatus("Grid is now " + stage.gridWidth + "x" + stage.gridHeight + (notes != null ? ". " + notes + "." : "."), notes != null ? MessageType.Warning : MessageType.Info);
        }

        private void CopyLayoutFrom(StageData source)
        {
            if (!EditorUtility.DisplayDialog("Copy the layout?",
                    "Replace this stage's grid, tiles, spawns, exits and routes with those of " + StageName(source) + "? Art and waves are kept. You can undo it with Ctrl+Z.",
                    "Copy", "Cancel"))
            {
                GUIUtility.ExitGUI();
                return;
            }

            RecordForEdit("Copy layout");
            StageMakerLogic.CopyLayout(stage, source);
            FinishEdit();
            newWidth = stage.gridWidth;
            newHeight = stage.gridHeight;
            SetStatus("Copied the layout of " + StageName(source) + " (" + stage.gridWidth + "x" + stage.gridHeight + "). Check the art and the waves' spawn numbers.", MessageType.Info);
            GUIUtility.ExitGUI();
        }

        private void ClearGrid()
        {
            if (!EditorUtility.DisplayDialog("Clear the grid?",
                    "Every tile is replaced and all spawns, exits and routes are removed. You can undo it with Ctrl+Z.", "Clear", "Cancel"))
            {
                GUIUtility.ExitGUI();
                return;
            }

            RecordForEdit("Clear grid");
            StageMakerLogic.CreateBlank(stage, stage.gridWidth, stage.gridHeight, FillChoices[Mathf.Clamp(fillChoice, 0, FillChoices.Length - 1)]);
            FinishEdit();
            SetStatus("Cleared the grid. Add a spawn and an exit.", MessageType.Info);
            GUIUtility.ExitGUI();
        }
    }
}
