using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Operators;

namespace TrashTD.Editor
{
    /// <summary>
    /// Draws and edits operators' range patterns (TRASH TD -> Range Pattern): the tiles an operator attacks (OperatorData.rangePattern)
    /// and the tiles its talent reaches (talentRangePattern). Click or drag on the grid to add and remove tiles, build common shapes,
    /// flip, turn or shift the pattern, preview it for each facing and each star rank, and test it on a real stage to see which lane
    /// tiles it covers.
    /// Patterns are written for an operator facing RIGHT, in tiles from the operator: (1, 0) is the tile in front.
    /// Several operators change their range at higher stars in code (Mechonk, Lockburn, Mossmo, Progeny, Oquloct, Thornchin,
    /// Basurocket): those stars are shown read-only, with the class that defines them.
    /// Edits support Undo and are written to the operator asset like Inspector edits (use Save). The window is split into this
    /// file (list, grid, tools) and RangePatternWindow.Stage (the stage test). The maths lives in RangePatternLogic.
    /// </summary>
    public sealed partial class RangePatternWindow : EditorWindow
    {
        private const string OperatorPrefKey = "TrashTD.RangePattern.Operator";
        private const float ListWidth = 250f;
        private const float RightWidth = 400f;
        private const float GridPadding = 28f;

        private enum PatternTarget { Attack, Talent }

        private static readonly Color AttackColor = new Color(1f, 0.55f, 0.2f, 0.92f);
        private static readonly Color TalentColor = new Color(0.65f, 0.45f, 1f, 0.92f);
        private static readonly Color SelectedRowColor = new Color(0.25f, 0.45f, 0.75f, 0.45f);
        private static readonly Color WarningColor = new Color(0.95f, 0.75f, 0.3f);
        private static readonly string[] ShapeNames = { "Line in front", "Cross", "Diamond", "Square ring", "Block in front", "Cone in front" };
        private static readonly string[] ModeNames = { "Add", "Remove", "Replace" };
        private static readonly string[] FacingNames = { "Right", "Up", "Left", "Down" };
        private static readonly OperatorRarity[] Stars =
        {
            OperatorRarity.Star1, OperatorRarity.Star2, OperatorRarity.Star3, OperatorRarity.Star4, OperatorRarity.Star5
        };

        private List<OperatorData> operators = new List<OperatorData>();
        private OperatorData op;
        private string search = string.Empty;
        private PatternTarget target;
        private OperatorFacing facing = OperatorFacing.Right;
        private OperatorRarity viewStar = OperatorRarity.Star1;
        private bool editAssetField;
        private float cellPixels = 38f;

        private Vector2 listScroll;
        private Vector2 gridScroll;
        private Vector2 rightScroll;

        private int shapeIndex;
        private int shapeSize = 3;
        private int shapeWidth = 3;
        private int modeIndex;
        private int copySourceIndex;

        private string status = string.Empty;
        private MessageType statusType = MessageType.None;

        private bool stroking;
        private bool paintAdds;
        private int strokeGroup;
        private bool hasHover;
        private Vector2Int hoverPattern;

        private GUIStyle cellLabel;
        private GUIStyle wrapMini;

        [MenuItem("TRASH TD/Range Pattern")]
        public static void Open()
        {
            RangePatternWindow window = GetWindow<RangePatternWindow>("Range Pattern");
            window.minSize = new Vector2(1000f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "Some operators have unsaved edits. Save them?";
            RefreshOperators();
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        public override void SaveChanges()
        {
            SaveAll();
            base.SaveChanges();
        }

        private void OnProjectChange()
        {
            RefreshOperators();
            Repaint();
        }

        private void RefreshOperators()
        {
            operators = new List<OperatorData>(UnitIntelRegistry.FindProjectAssets<OperatorData>());
            if (op == null || !operators.Contains(op))
            {
                string path = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(OperatorPrefKey, string.Empty));
                OperatorData saved = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<OperatorData>(path);
                SelectOperator(saved != null && operators.Contains(saved) ? saved : (operators.Count > 0 ? operators[0] : null));
            }
        }

        private void SelectOperator(OperatorData next)
        {
            op = next;
            viewStar = OperatorRarity.Star1;
            editAssetField = false;
            hasHover = false;
            copySourceIndex = 0;
            if (op != null) EditorPrefs.SetString(OperatorPrefKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(op)));
        }

        // ============================
        // Which pattern is shown and whether it can be edited
        // ============================

        /// <summary>The pattern the game uses: this star's attack pattern, or the talent pattern.</summary>
        private Vector2Int[] EffectivePattern()
        {
            if (op == null) return null;
            return target == PatternTarget.Attack ? op.GetRangePatternForRarity(viewStar) : op.GetTalentRangePattern();
        }

        /// <summary>The pattern stored in the asset's own field.</summary>
        private Vector2Int[] AssetPattern()
        {
            if (op == null) return null;
            return target == PatternTarget.Attack ? op.rangePattern : op.talentRangePattern;
        }

        private Vector2Int[] DisplayPattern()
        {
            return editAssetField ? AssetPattern() : EffectivePattern();
        }

        /// <summary>Whether clicks on the grid edit the asset's field. When not, <paramref name="reason"/> says why.</summary>
        private bool IsEditable(out string reason)
        {
            reason = null;
            if (op == null) { reason = "No operator selected."; return false; }
            if (editAssetField) return true;

            if (target == PatternTarget.Attack)
            {
                if (RangePatternLogic.SameCells(EffectivePattern(), op.rangePattern)) return true;
                reason = "At " + (int)viewStar + " star" + ((int)viewStar == 1 ? "" : "s") + " the game uses a pattern defined in code (" + DefiningClassName() + "), not the asset's field. " +
                         "Pick another star to edit the base pattern, or tick \"Edit the asset's field\".";
                return false;
            }

            if (op.talentRangePattern != null && op.talentRangePattern.Length > 0) return true;
            if (op.GetTalentRangePattern() == null) return true; // nothing yet: painting creates it
            reason = "This operator uses a built-in default talent range. Customize copies it into the asset so you can change it.";
            return false;
        }

        private string DefiningClassName()
        {
            if (op != null && op.operatorPrefab != null)
            {
                var behaviour = op.operatorPrefab.GetComponent<OperatorBase>();
                if (behaviour != null) return behaviour.GetType().Name;
            }
            return "the operator's class";
        }

        private void SetPattern(Vector2Int[] pattern, string undoName)
        {
            Undo.RecordObject(op, "Range Pattern: " + undoName);
            if (target == PatternTarget.Attack) op.rangePattern = pattern;
            else op.talentRangePattern = pattern;
            EditorUtility.SetDirty(op);
            Repaint();
        }

        private Vector2Int[] CurrentEditable()
        {
            return AssetPattern() ?? new Vector2Int[0];
        }

        private void SaveAll()
        {
            int saved = 0;
            foreach (OperatorData o in operators)
            {
                if (o == null || !EditorUtility.IsDirty(o)) continue;
                AssetDatabase.SaveAssetIfDirty(o);
                saved++;
            }
            hasUnsavedChanges = false;
            SetStatus(saved > 0 ? "Saved " + saved + " operator" + (saved == 1 ? "" : "s") + "." : "Nothing to save.", MessageType.Info);
        }

        private bool AnyDirty()
        {
            foreach (OperatorData o in operators)
                if (o != null && EditorUtility.IsDirty(o)) return true;
            return false;
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
        }

        // ============================
        // Layout
        // ============================

        private void EnsureStyles()
        {
            if (cellLabel != null) return;
            cellLabel = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
            wrapMini = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (op == null)
            {
                EditorGUILayout.HelpBox("No OperatorData assets found.", MessageType.Info);
                return;
            }

            bool dirty = AnyDirty();
            if (hasUnsavedChanges != dirty) hasUnsavedChanges = dirty;

            EditorGUILayout.BeginHorizontal();
            DrawOperatorList();
            EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            DrawCenter();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(GUILayout.Width(RightWidth));
            DrawRight();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S)
            {
                SaveAll();
                e.Use();
            }
        }

        private void DrawOperatorList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(ListWidth));
            EditorGUILayout.LabelField("OPERATORS (" + operators.Count + ")", EditorStyles.boldLabel);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            foreach (OperatorData o in operators)
            {
                if (!string.IsNullOrEmpty(search) && o.operatorName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                Rect row = GUILayoutUtility.GetRect(1f, 34f, GUILayout.ExpandWidth(true));
                bool selected = o == op;
                if (selected) EditorGUI.DrawRect(row, SelectedRowColor);
                else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.06f));

                if (o.portrait != null && o.portrait.texture != null)
                {
                    Rect tr = o.portrait.textureRect;
                    Texture2D tex = o.portrait.texture;
                    GUI.DrawTextureWithTexCoords(new Rect(row.x + 3f, row.y + 3f, 28f, 28f), tex, new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
                }

                string dirty = EditorUtility.IsDirty(o) ? " *" : string.Empty;
                GUI.Label(new Rect(row.x + 36f, row.y + 1f, row.width - 90f, 18f), o.operatorName + dirty, selected ? EditorStyles.boldLabel : EditorStyles.label);
                GUI.Label(new Rect(row.x + 36f, row.y + 17f, row.width - 90f, 16f), o.position + ", " + o.operatorClass, EditorStyles.miniLabel);
                GUI.Label(new Rect(row.xMax - 52f, row.y + 8f, 48f, 18f), RangePatternLogic.DistinctCount(o.rangePattern) + " tiles", EditorStyles.miniLabel);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
                {
                    if (!selected) SelectOperator(o);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ============================
        // Center: target, facing, the grid, the star strip
        // ============================

        private void DrawCenter()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(op.operatorName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Toggle(target == PatternTarget.Attack, "Attack range", EditorStyles.toolbarButton, GUILayout.Width(90f))) target = PatternTarget.Attack;
            if (GUILayout.Toggle(target == PatternTarget.Talent, new GUIContent("Talent range", "Where the operator's talent reaches (talentRangePattern)."), EditorStyles.toolbarButton, GUILayout.Width(90f))) target = PatternTarget.Talent;
            GUILayout.Space(8f);
            GUILayout.Label("Facing", EditorStyles.miniLabel, GUILayout.Width(40f));
            facing = (OperatorFacing)GUILayout.Toolbar((int)facing, FacingNames, EditorStyles.toolbarButton, GUILayout.Width(180f));
            GUILayout.Space(8f);
            GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(34f));
            cellPixels = GUILayout.HorizontalSlider(cellPixels, 20f, 64f, GUILayout.Width(90f));
            using (new EditorGUI.DisabledScope(!AnyDirty()))
            {
                if (GUILayout.Button(new GUIContent("Save", "Write every edited operator to disk (Ctrl+S)."), EditorStyles.toolbarButton, GUILayout.Width(50f))) SaveAll();
            }
            EditorGUILayout.EndHorizontal();

            if (!IsEditable(out string reason))
                EditorGUILayout.HelpBox(reason, MessageType.Info);
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

            DrawGridEditor();
            if (target == PatternTarget.Attack) DrawStarStrip();
        }

        private int GridRadius(Vector2Int[] pattern)
        {
            return Mathf.Clamp(Mathf.Max(5, RangePatternLogic.Radius(pattern) + 1), 3, RangePatternLogic.MaxRadius + 1);
        }

        private void DrawGridEditor()
        {
            Vector2Int[] pattern = DisplayPattern();
            int radius = GridRadius(pattern);
            float size = (radius * 2 + 1) * cellPixels;

            gridScroll = EditorGUILayout.BeginScrollView(gridScroll, GUILayout.ExpandHeight(true));
            Rect content = GUILayoutUtility.GetRect(size + GridPadding * 2f, size + GridPadding * 2f, GUILayout.Width(size + GridPadding * 2f), GUILayout.Height(size + GridPadding * 2f));
            var grid = new Rect(content.x + GridPadding, content.y + GridPadding, size, size);

            int id = GUIUtility.GetControlID(FocusType.Passive);
            Event e = Event.current;
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(content, new Color(0.09f, 0.09f, 0.1f));
                DrawPatternGrid(grid, pattern, radius, facing, target == PatternTarget.Attack ? AttackColor : TalentColor, true);
                DrawHover(grid, radius);
            }
            HandleGridInput(e, id, grid, radius);
            EditorGUILayout.EndScrollView();

            string hover = hasHover ? "Offset (" + hoverPattern.x + ", " + hoverPattern.y + ")  " + (RangePatternLogic.Contains(pattern, hoverPattern) ? "in range" : "not in range") : " ";
            EditorGUILayout.LabelField(hover, EditorStyles.miniLabel);
        }

        private static Rect CellRect(Rect grid, int radius, int displayX, int displayY)
        {
            float cell = grid.width / (radius * 2 + 1);
            return new Rect(grid.x + (displayX + radius) * cell, grid.y + (radius - displayY) * cell, cell, cell);
        }

        /// <summary>
        /// Draws a pattern on a grid centred on the operator. <paramref name="detailed"/> adds grid lines, coordinates and the facing arrow;
        /// the small star previews leave them out.
        /// </summary>
        private void DrawPatternGrid(Rect grid, Vector2Int[] pattern, int radius, OperatorFacing patternFacing, Color fill, bool detailed)
        {
            float cell = grid.width / (radius * 2 + 1);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    bool shade = (dx + dy) % 2 == 0;
                    EditorGUI.DrawRect(CellRect(grid, radius, dx, dy), shade ? new Color(0.16f, 0.16f, 0.18f) : new Color(0.13f, 0.13f, 0.15f));
                }
            }

            if (pattern != null)
            {
                var seen = new HashSet<Vector2Int>();
                foreach (Vector2Int offset in pattern)
                {
                    if (!seen.Add(offset)) continue;
                    Vector2Int d = RangePatternLogic.Rotate(offset, patternFacing);
                    if (Mathf.Abs(d.x) > radius || Mathf.Abs(d.y) > radius) continue;
                    Rect r = CellRect(grid, radius, d.x, d.y);
                    float inset = detailed ? 2f : 0.5f;
                    EditorGUI.DrawRect(new Rect(r.x + inset, r.y + inset, r.width - inset * 2f, r.height - inset * 2f), fill);
                }
            }

            // the operator's own tile
            Rect self = CellRect(grid, radius, 0, 0);
            EditorGUI.DrawRect(new Rect(self.x + 1f, self.y + 1f, self.width - 2f, self.height - 2f), new Color(0.05f, 0.05f, 0.07f, 1f));
            if (op != null && op.portrait != null && op.portrait.texture != null)
            {
                Rect tr = op.portrait.textureRect;
                Texture2D tex = op.portrait.texture;
                float pad = self.width * 0.12f;
                GUI.DrawTextureWithTexCoords(new Rect(self.x + pad, self.y + pad, self.width - pad * 2f, self.height - pad * 2f), tex,
                    new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            }

            if (!detailed)
            {
                DrawBorder(grid, new Color(0f, 0f, 0f, 0.6f), 1f);
                return;
            }

            Color line = new Color(0f, 0f, 0f, 0.45f);
            for (int i = 0; i <= radius * 2 + 1; i++)
            {
                EditorGUI.DrawRect(new Rect(grid.x + i * cell - 0.5f, grid.y, 1f, grid.height), line);
                EditorGUI.DrawRect(new Rect(grid.x, grid.y + i * cell - 0.5f, grid.width, 1f), line);
            }

            DrawFacingArrow(self, patternFacing);

            // Coordinates are for a right-facing operator, so they only make sense in that view.
            if (patternFacing == OperatorFacing.Right && cell >= 24f)
            {
                for (int d = -radius; d <= radius; d++)
                {
                    GUI.Label(new Rect(CellRect(grid, radius, d, -radius).x, grid.yMax + 2f, cell, 16f), d.ToString(), cellLabel);
                    GUI.Label(new Rect(grid.x - GridPadding + 2f, CellRect(grid, radius, -radius, d).y + cell * 0.5f - 8f, GridPadding - 6f, 16f), d.ToString(), cellLabel);
                }
            }
        }

        private static void DrawBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private static void DrawFacingArrow(Rect self, OperatorFacing patternFacing)
        {
            Vector2 direction = new Vector2(RangePatternLogic.Rotate(Vector2Int.right, patternFacing).x, -RangePatternLogic.Rotate(Vector2Int.right, patternFacing).y);
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 center = self.center + direction * self.width * 0.36f;
            float s = self.width * 0.12f;
            Handles.color = Color.white;
            Handles.DrawAAConvexPolygon(center + direction * s, center - direction * s + side * s, center - direction * s - side * s);
        }

        private void DrawHover(Rect grid, int radius)
        {
            if (!hasHover) return;
            Vector2Int d = RangePatternLogic.Rotate(hoverPattern, facing);
            if (Mathf.Abs(d.x) > radius || Mathf.Abs(d.y) > radius) return;
            DrawBorder(CellRect(grid, radius, d.x, d.y), new Color(1f, 0.95f, 0.4f), 2f);
        }

        private bool TryPatternCell(Vector2 mouse, Rect grid, int radius, out Vector2Int patternCell)
        {
            patternCell = default;
            if (!grid.Contains(mouse)) return false;
            float cell = grid.width / (radius * 2 + 1);
            var display = new Vector2Int(Mathf.FloorToInt((mouse.x - grid.x) / cell) - radius, radius - Mathf.FloorToInt((mouse.y - grid.y) / cell));
            patternCell = RangePatternLogic.Unrotate(display, facing);
            return true;
        }

        private void HandleGridInput(Event e, int id, Rect grid, int radius)
        {
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseMove:
                    {
                        bool had = hasHover;
                        Vector2Int old = hoverPattern;
                        hasHover = TryPatternCell(e.mousePosition, grid, radius, out hoverPattern);
                        if (had != hasHover || old != hoverPattern) Repaint();
                        break;
                    }

                case EventType.MouseLeaveWindow:
                    if (hasHover) { hasHover = false; Repaint(); }
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && TryPatternCell(e.mousePosition, grid, radius, out Vector2Int downCell))
                    {
                        if (!IsEditable(out string reason))
                        {
                            SetStatus(reason, MessageType.Warning);
                            e.Use();
                            Repaint();
                            break;
                        }

                        Undo.IncrementCurrentGroup();
                        strokeGroup = Undo.GetCurrentGroup();
                        stroking = true;
                        paintAdds = !RangePatternLogic.Contains(CurrentEditable(), downCell);
                        GUIUtility.hotControl = id;
                        PaintCell(downCell);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        hasHover = TryPatternCell(e.mousePosition, grid, radius, out Vector2Int dragCell);
                        if (hasHover)
                        {
                            hoverPattern = dragCell;
                            PaintCell(dragCell);
                        }
                        e.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        if (stroking) Undo.CollapseUndoOperations(strokeGroup);
                        stroking = false;
                        e.Use();
                    }
                    break;
            }
        }

        private void PaintCell(Vector2Int patternCell)
        {
            Vector2Int[] current = CurrentEditable();
            bool has = RangePatternLogic.Contains(current, patternCell);
            if (has == paintAdds) return;
            SetPattern(paintAdds ? RangePatternLogic.Add(current, patternCell) : RangePatternLogic.Remove(current, patternCell), paintAdds ? "add tile" : "remove tile");
        }

        // ============================
        // The star strip
        // ============================

        private void DrawStarStrip()
        {
            EditorGUILayout.LabelField("BY STAR (what the game uses at each rank; click one to look at it)", EditorStyles.boldLabel);

            int radius = 3;
            foreach (OperatorRarity star in Stars) radius = Mathf.Max(radius, RangePatternLogic.Radius(op.GetRangePatternForRarity(star)));
            radius = Mathf.Min(radius, RangePatternLogic.MaxRadius);

            EditorGUILayout.BeginHorizontal();
            foreach (OperatorRarity star in Stars)
            {
                Vector2Int[] pattern = op.GetRangePatternForRarity(star);
                bool fromCode = !RangePatternLogic.SameCells(pattern, op.rangePattern);

                EditorGUILayout.BeginVertical(GUILayout.Width(112f));
                Rect box = GUILayoutUtility.GetRect(112f, 112f, GUILayout.Width(112f), GUILayout.Height(112f));
                if (viewStar == star && !editAssetField) EditorGUI.DrawRect(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f), new Color(1f, 0.9f, 0.3f));
                if (Event.current.type == EventType.Repaint)
                    DrawPatternGrid(new Rect(box.x + 1f, box.y + 1f, 110f, 110f), pattern, radius, facing, fromCode ? TalentColor : AttackColor, false);
                if (Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition))
                {
                    viewStar = star;
                    editAssetField = false;
                    Event.current.Use();
                }
                GUILayout.Label(((int)star) + " star" + (star == OperatorRarity.Star1 ? "" : "s") + "  " + RangePatternLogic.DistinctCount(pattern) + " tiles" + (fromCode ? "  (code)" : string.Empty), EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        // ============================
        // Right column: info, shapes, transforms, other fields, stage test
        // ============================

        private void DrawRight()
        {
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
            DrawInfo();
            GUILayout.Space(8f);
            DrawShapes();
            GUILayout.Space(8f);
            DrawTransforms();
            GUILayout.Space(8f);
            DrawFields();
            GUILayout.Space(8f);
            DrawStageTest();
            EditorGUILayout.EndScrollView();
        }

        private void DrawInfo()
        {
            EditorGUILayout.LabelField("PATTERN", EditorStyles.boldLabel);
            Vector2Int[] pattern = DisplayPattern();
            EditorGUILayout.LabelField(RangePatternLogic.Describe(pattern), wrapMini);

            foreach (string note in RangePatternLogic.Validate(pattern))
                EditorGUILayout.HelpBox(note, MessageType.Warning);

            if (target == PatternTarget.Attack)
            {
                bool baseFromCode = !RangePatternLogic.SameCells(op.GetRangePatternForRarity(OperatorRarity.Star1), op.rangePattern);
                if (baseFromCode)
                    EditorGUILayout.HelpBox("Even at 1 star the game uses a pattern defined in code (" + DefiningClassName() + "), so the asset's pattern field is ignored for that.", MessageType.Info);

                EditorGUI.BeginChangeCheck();
                editAssetField = EditorGUILayout.ToggleLeft(
                    new GUIContent("Edit the asset's field (the game may ignore it)", "Edit OperatorData.rangePattern directly, even at a star where code replaces it."), editAssetField);
                if (EditorGUI.EndChangeCheck()) Repaint();
            }

            if (RangePatternLogic.HasDuplicates(AssetPattern()) && IsEditable(out _))
            {
                if (GUILayout.Button("Remove repeated cells")) SetPattern(RangePatternLogic.RemoveDuplicates(CurrentEditable()), "remove repeats");
            }

            if (target == PatternTarget.Talent && !IsEditable(out _) && op.GetTalentRangePattern() != null)
            {
                if (GUILayout.Button("Customize (copy the default into the asset)"))
                    SetPattern((Vector2Int[])op.GetTalentRangePattern().Clone(), "customize talent range");
            }
        }

        private Vector2Int[] ShapeFor(int index)
        {
            switch (index)
            {
                case 0: return RangePatternLogic.Line(1, shapeSize);
                case 1: return RangePatternLogic.Cross(shapeSize);
                case 2: return RangePatternLogic.Diamond(shapeSize);
                case 3: return RangePatternLogic.Square(shapeSize);
                case 4: return RangePatternLogic.ForwardBlock(shapeSize, shapeWidth);
                default: return RangePatternLogic.Cone(shapeSize);
            }
        }

        private void DrawShapes()
        {
            EditorGUILayout.LabelField("SHAPES", EditorStyles.boldLabel);
            bool editable = IsEditable(out _);
            using (new EditorGUI.DisabledScope(!editable))
            {
                shapeIndex = EditorGUILayout.Popup("Shape", shapeIndex, ShapeNames);
                shapeSize = Mathf.Clamp(EditorGUILayout.IntField(shapeIndex == 4 ? "Depth (tiles)" : shapeIndex == 0 ? "Length (tiles)" : "Size / radius", shapeSize), 1, RangePatternLogic.MaxRadius);
                if (shapeIndex == 4) shapeWidth = Mathf.Clamp(EditorGUILayout.IntField("Width (tiles)", shapeWidth), 1, 2 * RangePatternLogic.MaxRadius + 1);
                modeIndex = GUILayout.Toolbar(modeIndex, ModeNames);

                if (GUILayout.Button("Apply shape"))
                {
                    Vector2Int[] shape = ShapeFor(shapeIndex);
                    Vector2Int[] current = CurrentEditable();
                    Vector2Int[] result = modeIndex == 0 ? RangePatternLogic.Union(current, shape)
                        : modeIndex == 1 ? RangePatternLogic.Subtract(current, shape) : shape;
                    SetPattern(result, "apply " + ShapeNames[shapeIndex].ToLowerInvariant());
                }
            }
        }

        private void DrawTransforms()
        {
            EditorGUILayout.LabelField("TRANSFORM", EditorStyles.boldLabel);
            bool editable = IsEditable(out _);
            using (new EditorGUI.DisabledScope(!editable))
            {
                Vector2Int[] current = CurrentEditable();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("Flip up/down", "Mirror the pattern across the operator's row."))) SetPattern(RangePatternLogic.FlipVertical(current), "flip up/down");
                if (GUILayout.Button(new GUIContent("Flip front/back", "Mirror the pattern across the operator's column."))) SetPattern(RangePatternLogic.FlipHorizontal(current), "flip front/back");
                if (GUILayout.Button(new GUIContent("Turn 90°", "Turn the pattern a quarter turn counter-clockwise."))) SetPattern(RangePatternLogic.RotateQuarterTurn(current), "turn");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Shift", GUILayout.Width(40f));
                if (GUILayout.Button("Back", EditorStyles.miniButton)) SetPattern(RangePatternLogic.Shift(current, Vector2Int.left), "shift back");
                if (GUILayout.Button("Front", EditorStyles.miniButton)) SetPattern(RangePatternLogic.Shift(current, Vector2Int.right), "shift forward");
                if (GUILayout.Button("Up", EditorStyles.miniButton)) SetPattern(RangePatternLogic.Shift(current, Vector2Int.up), "shift up");
                if (GUILayout.Button("Down", EditorStyles.miniButton)) SetPattern(RangePatternLogic.Shift(current, Vector2Int.down), "shift down");
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                bool hasSelf = RangePatternLogic.Contains(current, Vector2Int.zero);
                if (GUILayout.Button(hasSelf ? "Exclude the operator's own tile" : "Include the operator's own tile"))
                    SetPattern(RangePatternLogic.Toggle(current, Vector2Int.zero), hasSelf ? "exclude self" : "include self");
                if (GUILayout.Button("Clear")) SetPattern(new Vector2Int[0], "clear");
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawFields()
        {
            EditorGUILayout.LabelField("OTHER", EditorStyles.boldLabel);

            if (target == PatternTarget.Attack)
            {
                EditorGUI.BeginChangeCheck();
                int range = EditorGUILayout.IntField(new GUIContent("Attack range", "OperatorData.attackRange: the distance some operators use for targeting (Bubblets, Coalesce, ...). Separate from the pattern."), op.attackRange);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(op, "Range Pattern: attack range");
                    op.attackRange = Mathf.Max(0, range);
                    EditorUtility.SetDirty(op);
                }
            }

            var others = new List<OperatorData>();
            foreach (OperatorData o in operators)
                if (o != op) others.Add(o);
            if (others.Count == 0) return;

            var names = new string[others.Count];
            for (int i = 0; i < names.Length; i++) names[i] = others[i].operatorName;
            copySourceIndex = Mathf.Clamp(copySourceIndex, 0, names.Length - 1);

            EditorGUILayout.BeginHorizontal();
            copySourceIndex = EditorGUILayout.Popup("Copy from", copySourceIndex, names);
            using (new EditorGUI.DisabledScope(!IsEditable(out _)))
            {
                if (GUILayout.Button("Copy", GUILayout.Width(56f)))
                {
                    OperatorData source = others[copySourceIndex];
                    Vector2Int[] from = target == PatternTarget.Attack ? source.rangePattern : source.talentRangePattern;
                    SetPattern(from != null ? (Vector2Int[])from.Clone() : new Vector2Int[0], "copy from " + source.operatorName);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
