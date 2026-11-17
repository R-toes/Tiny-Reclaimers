using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>
    /// A level editor for every StageData (TRASH TD -> Stage Maker): set the grid size, paint or drag tiles (low/high ground,
    /// acid low/high ground, blocked, spawn, exit, hole) onto it with the placeholder tile images, add the stage's map art and
    /// bounds art (refused when the size does not fit), preview the routes enemies take (the game's own A*) and redraw any route
    /// by hand. Checks flag a stage the game could not play.
    /// Edits support Undo and are written to the stage asset like Inspector edits (use Save, or File > Save Project).
    /// The window is split into partial files: this one (state, stage list, palette, tools), Grid (drawing and mouse input),
    /// Panels (the right-hand panels), Routes (hand-drawn route editing), Art (stage art and bounds art) and QoL (shortcuts,
    /// resizable panels, grid size, copy layout). The rules live in StageMakerLogic and StageArtRules.
    /// </summary>
    public sealed partial class StageMakerWindow : EditorWindow
    {
        private const string StagePrefKey = "TrashTD.StageMaker.StageGuid";
        private const string ZoomPrefKey = "TrashTD.StageMaker.CellPixels";
        private const string ArtPrefKey = "TrashTD.StageMaker.ShowArt";
        private const string LeftWidthPrefKey = "TrashTD.StageMaker.LeftWidth";
        private const string RightWidthPrefKey = "TrashTD.StageMaker.RightWidth";
        private const string BuiltOnlyPrefKey = "TrashTD.StageMaker.BuiltOnly";
        private const string DragKey = "TrashTD.StageMaker.Tile";
        private const float DefaultLeftWidth = 340f;
        private const float DefaultRightWidth = 400f;
        private const float MinPanelWidth = 260f;
        private const float MaxPanelWidth = 640f;

        private enum Tool { Brush, Rectangle, Fill, Pick }

        private static readonly GUIContent[] ToolContents =
        {
            new GUIContent("Brush (B)", "Click or drag to paint."),
            new GUIContent("Rectangle (R)", "Drag a box to fill it."),
            new GUIContent("Fill (F)", "Click to fill the connected area of the same tile."),
            new GUIContent("Pick (I)", "Click a tile to pick its type. Right-click or Alt+click does this from any tool.")
        };
        private static readonly GUIContent[] BrushSizeContents =
        {
            new GUIContent("1x1"), new GUIContent("2x2"), new GUIContent("3x3")
        };
        private static readonly TileType[] FillChoices = { TileType.LowGround, TileType.HighGround, TileType.Blocked };
        private static readonly string[] FillNames = { "Low ground", "High ground", "Blocked" };
        private static readonly Color SelectedRowColor = new Color(0.25f, 0.45f, 0.75f, 0.45f);
        private static readonly Color WarningColor = new Color(0.95f, 0.75f, 0.3f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.35f, 0.3f);

        private StageData[] stages = new StageData[0];
        private StageData stage;
        private TileType brush = TileType.LowGround;
        private Tool tool = Tool.Brush;
        private int brushSize = 1;

        private float cellPixels = 40f;
        private bool showArt = true;
        private bool showRoutes = true;
        private bool showCoords;
        private bool animateRoutes;
        private float tileOpacity = 0.55f;

        private float leftWidth = DefaultLeftWidth;
        private float rightWidth = DefaultRightWidth;
        private string stageFilter = string.Empty;
        private bool builtOnly;

        private Vector2 leftScroll;
        private Vector2 stageListScroll;
        private Vector2 gridScroll;
        private Vector2 rightScroll;

        private int newWidth = 12;
        private int newHeight = 7;
        private int fillChoice = 1;

        private string status = string.Empty;
        private MessageType statusType = MessageType.None;
        private double lastRepaintTime;

        // Bumped by every edit (and by Undo, focus and project changes) so cached routes and checks are rebuilt.
        private int revision;
        private int cacheRevision = -1;
        private StageData cacheStage;
        private List<StageRoute> routes = new List<StageRoute>();
        private List<StageIssue> issues = new List<StageIssue>();
        private string routeError;

        private GUIStyle rightLabelStyle;
        private GUIStyle cellLabelStyle;
        private GUIStyle axisLabelStyle;
        private GUIStyle hintStyle;

        [MenuItem("TRASH TD/Stage Maker")]
        public static void Open()
        {
            StageMakerWindow window = GetWindow<StageMakerWindow>("Stage Maker");
            window.minSize = new Vector2(1100f, 600f);
            if (window.position.width < 1300f || window.position.height < 700f)
                window.position = new Rect(window.position.x, window.position.y, 1500f, 820f);
            window.Show();
        }

        private void OnEnable()
        {
            wantsMouseMove = true;
            saveChangesMessage = "Some stages have unsaved edits. Save them?";
            cellPixels = Mathf.Clamp(EditorPrefs.GetFloat(ZoomPrefKey, 40f), 20f, 80f);
            showArt = EditorPrefs.GetBool(ArtPrefKey, true);
            leftWidth = Mathf.Clamp(EditorPrefs.GetFloat(LeftWidthPrefKey, DefaultLeftWidth), MinPanelWidth, MaxPanelWidth);
            rightWidth = Mathf.Clamp(EditorPrefs.GetFloat(RightWidthPrefKey, DefaultRightWidth), MinPanelWidth, MaxPanelWidth);
            builtOnly = EditorPrefs.GetBool(BuiltOnlyPrefKey, false);
            allowLargerArt = EditorPrefs.GetBool(AllowLargerArtPrefKey, false);
            RefreshStages();
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= OnEditorUpdate;
            DisposePathfinder();
        }

        /// <summary>Called by Unity when the window is closed with unsaved changes and the user chooses Save.</summary>
        public override void SaveChanges()
        {
            SaveAll();
            base.SaveChanges();
        }

        private void OnProjectChange()
        {
            RefreshStages();
            revision++;
            Repaint();
        }

        private void OnFocus()
        {
            // The stage may have been changed in the Inspector or the Wave Planner while this window was in the background.
            revision++;
        }

        private void OnUndoRedo()
        {
            revision++;
            if (editingSpawn >= 0) CancelRouteEdit();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            bool needsAnimation = (animateRoutes && showRoutes && routes.Count > 0) || (hasHighlight && EditorApplication.timeSinceStartup < highlightUntil);
            if (hasHighlight && EditorApplication.timeSinceStartup >= highlightUntil)
            {
                hasHighlight = false;
                Repaint();
            }

            if (needsAnimation && EditorApplication.timeSinceStartup - lastRepaintTime > 0.033)
            {
                lastRepaintTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        // ============================
        // Stage selection
        // ============================

        private void RefreshStages()
        {
            var found = new List<StageData>();
            foreach (string guid in AssetDatabase.FindAssets("t:StageData"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) found.Add(asset);
            }

            found.Sort((a, b) => string.CompareOrdinal(a.stageId ?? a.name, b.stageId ?? b.name));
            stages = found.ToArray();

            if (stage == null || Array.IndexOf(stages, stage) < 0)
            {
                string savedPath = AssetDatabase.GUIDToAssetPath(EditorPrefs.GetString(StagePrefKey, string.Empty));
                StageData saved = string.IsNullOrEmpty(savedPath) ? null : AssetDatabase.LoadAssetAtPath<StageData>(savedPath);
                SelectStage(saved != null ? saved : (stages.Length > 0 ? stages[0] : null));
            }
        }

        private void SelectStage(StageData newStage)
        {
            if (editingSpawn >= 0) CancelRouteEdit();
            relocating = false;
            rectActive = false;
            hasHover = false;
            hasHighlight = false;
            stage = newStage;
            revision++;
            status = string.Empty;
            copySourceIndex = 0;

            if (stage == null) return;
            EditorPrefs.SetString(StagePrefKey, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(stage)));
            if (StageMakerLogic.HasGrid(stage))
            {
                newWidth = stage.gridWidth;
                newHeight = stage.gridHeight;
            }
            else
            {
                newWidth = 12;
                newHeight = 7;
            }
        }

        private static string StageName(StageData s)
        {
            string id = string.IsNullOrEmpty(s.stageId) ? s.name : s.stageId;
            return string.IsNullOrEmpty(s.mapName) ? id : id + "  " + s.mapName;
        }

        private static string StageDetail(StageData s)
        {
            if (!StageMakerLogic.HasGrid(s)) return s.comingSoon ? "coming soon" : "no grid";
            return s.gridWidth + "x" + s.gridHeight;
        }

        private bool PassesStageFilter(StageData s)
        {
            if (builtOnly && !StageMakerLogic.HasGrid(s)) return false;
            if (string.IsNullOrEmpty(stageFilter)) return true;
            return StageName(s).IndexOf(stageFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   s.name.IndexOf(stageFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // ============================
        // Editing helpers
        // ============================

        /// <summary>Call before changing the stage: records it for Undo.</summary>
        private void RecordForEdit(string what)
        {
            Undo.RecordObject(stage, "Stage Maker: " + what);
        }

        /// <summary>Call after changing the stage: marks it dirty and refreshes the cached routes and checks.</summary>
        private void FinishEdit()
        {
            EditorUtility.SetDirty(stage);
            revision++;
            Repaint();
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
        }

        private void RefreshCaches()
        {
            if (cacheRevision == revision && cacheStage == stage) return;
            cacheRevision = revision;
            cacheStage = stage;
            routeError = null;
            try
            {
                routes = StageMakerRouting.ComputeRoutes(stage);
            }
            catch (Exception ex)
            {
                routes = new List<StageRoute>();
                routeError = ex.Message;
            }
            issues = StageMakerLogic.Validate(stage, routes);
        }

        private StageMakerPathfinder pathfinder;
        private int pathfinderRevision = -1;
        private StageData pathfinderStage;

        private StageMakerPathfinder GetPathfinder()
        {
            if (pathfinder == null || pathfinderRevision != revision || pathfinderStage != stage)
            {
                DisposePathfinder();
                pathfinder = new StageMakerPathfinder(stage);
                pathfinderRevision = revision;
                pathfinderStage = stage;
            }
            return pathfinder;
        }

        private void DisposePathfinder()
        {
            if (pathfinder != null) pathfinder.Dispose();
            pathfinder = null;
        }

        private void SaveAll()
        {
            int saved = 0;
            foreach (StageData s in stages)
            {
                if (s != null && EditorUtility.IsDirty(s))
                {
                    AssetDatabase.SaveAssetIfDirty(s);
                    saved++;
                }
            }
            hasUnsavedChanges = false;
            SetStatus(saved > 0 ? "Saved " + saved + " stage asset" + (saved == 1 ? "" : "s") + "." : "Nothing to save.", MessageType.Info);
        }

        private bool AnyDirty()
        {
            foreach (StageData s in stages)
                if (s != null && EditorUtility.IsDirty(s)) return true;
            return false;
        }

        // ============================
        // Main layout
        // ============================

        private void EnsureStyles()
        {
            if (rightLabelStyle != null) return;
            rightLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            // White text so GUI.contentColor can tint it (or darken it for the shadow) the same way in the light and dark editor themes.
            cellLabelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            cellLabelStyle.normal.textColor = Color.white;
            axisLabelStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 9 };
            hintStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true, alignment = TextAnchor.UpperLeft };
        }

        private void OnGUI()
        {
            EnsureStyles();

            if (stages.Length == 0 || stage == null)
            {
                EditorGUILayout.HelpBox("No StageData assets found. The stages live in Assets/Resources/Stages.", MessageType.Info);
                return;
            }

            bool dirty = AnyDirty();
            if (hasUnsavedChanges != dirty) hasUnsavedChanges = dirty;

            RefreshCaches();

            EditorGUILayout.BeginHorizontal();
            DrawLeftColumn();
            DrawSplitter(true);
            DrawCenterColumn();
            DrawSplitter(false);
            DrawRightColumn();
            EditorGUILayout.EndHorizontal();

            HandleShortcuts(Event.current);
        }

        // ============================
        // Left column: stages, palette, tools, size
        // ============================

        private void DrawLeftColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth));
            leftScroll = EditorGUILayout.BeginScrollView(leftScroll);

            DrawStageList();

            if (StageMakerLogic.HasGrid(stage))
            {
                GUILayout.Space(8f);
                bool busy = editingSpawn >= 0 || relocating;
                using (new EditorGUI.DisabledScope(busy))
                {
                    DrawPalette();
                    GUILayout.Space(6f);
                    DrawTools();
                }
                GUILayout.Space(6f);
                DrawLayoutBox();
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawStageList()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("STAGES (" + stages.Length + ")", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            builtOnly = GUILayout.Toggle(builtOnly, new GUIContent("Built only", "Hide the placeholder stages that have no grid yet."), EditorStyles.miniButton, GUILayout.Width(70f));
            if (EditorGUI.EndChangeCheck()) EditorPrefs.SetBool(BuiltOnlyPrefKey, builtOnly);
            EditorGUILayout.EndHorizontal();

            stageFilter = EditorGUILayout.TextField(stageFilter, EditorStyles.toolbarSearchField);

            float listHeight = Mathf.Clamp(position.height * 0.34f, 140f, 360f);
            stageListScroll = EditorGUILayout.BeginScrollView(stageListScroll, GUILayout.Height(listHeight));
            int shown = 0;
            foreach (StageData s in stages)
            {
                if (!PassesStageFilter(s)) continue;
                shown++;

                Rect row = GUILayoutUtility.GetRect(1f, 24f, GUILayout.ExpandWidth(true));
                bool selected = s == stage;
                if (selected) EditorGUI.DrawRect(row, SelectedRowColor);
                else if (row.Contains(Event.current.mousePosition)) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.06f));

                string dirty = EditorUtility.IsDirty(s) ? " *" : string.Empty;
                var nameRect = new Rect(row.x + 4f, row.y, row.width - 92f, row.height);
                var detailRect = new Rect(row.xMax - 88f, row.y, 84f, row.height);
                GUI.Label(nameRect, new GUIContent(StageName(s) + dirty, StageName(s)), selected ? EditorStyles.boldLabel : EditorStyles.label);
                GUI.Label(detailRect, StageDetail(s), rightLabelStyle);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && row.Contains(Event.current.mousePosition))
                {
                    if (!selected) SelectStage(s);
                    Event.current.Use();
                    GUIUtility.ExitGUI();
                }
            }
            if (shown == 0) EditorGUILayout.LabelField("No stage matches.", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPalette()
        {
            EditorGUILayout.LabelField("TILES (click, press its number, or drag onto the grid)", EditorStyles.boldLabel);
            int[] counts = StageMakerLogic.CountTiles(stage);

            for (int index = 0; index < StageMakerTiles.PaletteOrder.Length; index++)
            {
                TileType type = StageMakerTiles.PaletteOrder[index];
                // The legacy tile only shows up when a stage already uses it.
                if (type == TileType.EnemyPath && counts[(int)type] == 0 && brush != type) continue;

                Rect row = GUILayoutUtility.GetRect(1f, 52f, GUILayout.ExpandWidth(true));
                int id = GUIUtility.GetControlID(FocusType.Passive);
                Event e = Event.current;

                switch (e.GetTypeForControl(id))
                {
                    case EventType.Repaint:
                        if (brush == type) EditorGUI.DrawRect(row, SelectedRowColor);
                        else if (row.Contains(e.mousePosition)) EditorGUI.DrawRect(row, new Color(1f, 1f, 1f, 0.06f));
                        StageMakerTiles.Draw(new Rect(row.x + 4f, row.y + 6f, 40f, 40f), type);
                        GUI.Label(new Rect(row.x + 52f, row.y + 2f, row.width - 52f - 74f, 18f), StageMakerTiles.Label(type), EditorStyles.boldLabel);
                        GUI.Label(new Rect(row.x + 52f, row.y + 19f, row.width - 52f - 6f, 32f), StageMakerTiles.Hint(type), hintStyle);
                        string key = index < 8 ? "[" + (index + 1) + "]  " : string.Empty;
                        GUI.Label(new Rect(row.xMax - 74f, row.y + 2f, 70f, 18f), key + counts[(int)type], rightLabelStyle);
                        break;

                    case EventType.MouseDown:
                        if (e.button == 0 && row.Contains(e.mousePosition))
                        {
                            GUIUtility.hotControl = id;
                            brush = type;
                            if (tool == Tool.Pick) tool = Tool.Brush;
                            paletteDragOrigin = e.mousePosition;
                            e.Use();
                        }
                        break;

                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == id)
                        {
                            if ((e.mousePosition - paletteDragOrigin).sqrMagnitude > 36f)
                            {
                                GUIUtility.hotControl = 0;
                                DragAndDrop.PrepareStartDrag();
                                DragAndDrop.SetGenericData(DragKey, (int)type);
                                DragAndDrop.objectReferences = new UnityEngine.Object[0];
                                DragAndDrop.StartDrag(StageMakerTiles.Label(type));
                            }
                            e.Use();
                        }
                        break;

                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == id)
                        {
                            GUIUtility.hotControl = 0;
                            e.Use();
                        }
                        break;
                }

                if (e.type == EventType.Repaint) EditorGUIUtility.AddCursorRect(row, MouseCursor.Link);
            }
        }

        private Vector2 paletteDragOrigin;

        private void DrawTools()
        {
            EditorGUILayout.LabelField("TOOL", EditorStyles.boldLabel);
            tool = (Tool)GUILayout.Toolbar((int)tool, ToolContents);

            bool pointBrush = brush == TileType.SpawnPoint || brush == TileType.ExitPoint;
            using (new EditorGUI.DisabledScope(tool != Tool.Brush || pointBrush))
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(new GUIContent("Brush size", "How many tiles the Brush paints at once. Keys [ and ] change it."), GUILayout.Width(70f));
                brushSize = GUILayout.Toolbar(brushSize - 1, BrushSizeContents) + 1;
                EditorGUILayout.EndHorizontal();
            }

            string help;
            switch (tool)
            {
                case Tool.Rectangle: help = "Drag a box to fill it with the selected tile."; break;
                case Tool.Fill: help = "Click to fill the connected area of the same tile."; break;
                case Tool.Pick: help = "Click a tile on the grid to pick its type."; break;
                default: help = "Click or drag to paint. Right-click or Alt+click picks the tile under the mouse."; break;
            }
            if (pointBrush && (tool == Tool.Rectangle || tool == Tool.Fill))
                help = "Spawn and Exit tiles are placed one at a time with the Brush.";
            else if (pointBrush && tool == Tool.Brush)
                help = "Spawn and Exit tiles are placed one at a time (brush size 1).";
            EditorGUILayout.LabelField(help, hintStyle);
        }
    }
}
