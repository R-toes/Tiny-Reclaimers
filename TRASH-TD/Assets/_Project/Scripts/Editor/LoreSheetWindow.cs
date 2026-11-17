using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>
    /// Every piece of player-facing text in one place (TRASH TD -> Lore &amp; Text Sheet): operator and enemy names, field notes and
    /// ability text, stage and act descriptions, and the tutorial's lines. Edit them side by side, with a live character count and
    /// checks (empty, too long, double spaces, placeholder text, names a unit used to have), find and replace across everything, and
    /// export to / import from a CSV so the text can be edited in a spreadsheet.
    /// Edits go through SerializedObject, so they support Undo and are written to the assets like Inspector edits (use Save).
    /// Unit names are locked by default: some code matches on them (see "Unlock names").
    /// The window is split into this file (tabs, rows, drawing) and LoreSheetWindow.Tools (find and replace, CSV, old names).
    /// </summary>
    public sealed partial class LoreSheetWindow : EditorWindow
    {
        private const string OldTermsPrefKey = "TrashTD.LoreSheet.OldTerms";
        private const string TabPrefKey = "TrashTD.LoreSheet.Tab";

        private enum Tab { Operators, Enemies, Stages, Acts, Tutorial }

        private static readonly string[] TabNames = { "Operators", "Enemies", "Stages", "Acts", "Tutorial" };
        private static readonly Color WarningColor = new Color(0.95f, 0.75f, 0.3f);
        private static readonly Color InfoColor = new Color(0.6f, 0.75f, 0.95f);
        private static readonly Color OverLimitColor = new Color(0.95f, 0.45f, 0.35f);

        private sealed class Cell
        {
            public string path;
            public string label;
            public float weight;
            public int softLimit;
            public bool required;
            /// <summary>A unit name: shown read-only until names are unlocked.</summary>
            public bool isName;
            public List<LoreIssue> issues = new List<LoreIssue>();
        }

        private sealed class Row
        {
            public Tab tab;
            public UnityEngine.Object asset;
            public SerializedObject so;
            public string title;
            public string subtitle;
            public readonly List<Cell> cells = new List<Cell>();
        }

        private readonly Dictionary<Tab, List<Row>> rows = new Dictionary<Tab, List<Row>>();
        private Tab tab;
        private string search = string.Empty;
        private bool issuesOnly;
        private bool unlockNames;
        private Vector2 scroll;
        private bool issuesDirty = true;
        private string[] oldTerms = LoreTextRules.DefaultOldTerms;
        private string status = string.Empty;
        private MessageType statusType = MessageType.None;

        private GUIStyle areaStyle;
        private GUIStyle smallLabel;
        private GUIStyle smallRight;
        private GUIStyle issueStyle;

        [MenuItem("TRASH TD/Lore and Text Sheet")]
        public static void Open()
        {
            LoreSheetWindow window = GetWindow<LoreSheetWindow>("Lore & Text");
            window.minSize = new Vector2(900f, 500f);
            window.Show();
        }

        private void OnEnable()
        {
            saveChangesMessage = "Some text has unsaved edits. Save them?";
            tab = (Tab)Mathf.Clamp(EditorPrefs.GetInt(TabPrefKey, 0), 0, TabNames.Length - 1);
            string saved = EditorPrefs.GetString(OldTermsPrefKey, string.Empty);
            if (!string.IsNullOrEmpty(saved)) oldTerms = saved.Split('|');
            RebuildAll();
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        public override void SaveChanges()
        {
            SaveAll();
            base.SaveChanges();
        }

        private void OnProjectChange()
        {
            RebuildAll();
            Repaint();
        }

        private void OnFocus()
        {
            issuesDirty = true;
        }

        private void OnUndoRedo()
        {
            issuesDirty = true;
            Repaint();
        }

        // ============================
        // Building the rows
        // ============================

        private void RebuildAll()
        {
            rows.Clear();
            foreach (Tab t in Enum.GetValues(typeof(Tab))) rows[t] = BuildRows(t);
            issuesDirty = true;
        }

        private static Cell MakeCell(string path, string label, float weight, int softLimit, bool required, bool isName = false)
        {
            return new Cell { path = path, label = label, weight = weight, softLimit = softLimit, required = required, isName = isName };
        }

        private static Row MakeRow(Tab t, UnityEngine.Object asset, string title, string subtitle, params Cell[] cells)
        {
            var row = new Row { tab = t, asset = asset, so = new SerializedObject(asset), title = title, subtitle = subtitle };
            row.cells.AddRange(cells);
            return row;
        }

        private List<Row> BuildRows(Tab t)
        {
            var result = new List<Row>();
            switch (t)
            {
                case Tab.Operators:
                    foreach (OperatorData op in UnitIntelRegistry.FindProjectAssets<OperatorData>())
                        result.Add(MakeRow(t, op, op.operatorName, op.operatorClass + ", " + op.baseRarity.ToString().Replace("Star", "") + "-star",
                            MakeCell("operatorName", "Name", 0.18f, 30, true, true),
                            MakeCell("intelLore", "Field notes", 0.41f, 700, true),
                            MakeCell("skillDescription", "Skill description", 0.41f, 750, true)));
                    break;

                case Tab.Enemies:
                    foreach (EnemyData enemy in UnitIntelRegistry.FindProjectAssets<EnemyData>())
                        result.Add(MakeRow(t, enemy, enemy.enemyName, enemy.archetype.ToString(),
                            MakeCell("enemyName", "Name", 0.18f, 30, true, true),
                            MakeCell("intelLore", "Field notes", 0.41f, 400, true),
                            MakeCell("abilityDescription", "Ability description", 0.41f, 350, true)));
                    break;

                case Tab.Stages:
                    foreach (StageData stage in LoadAll<StageData>())
                        result.Add(MakeRow(t, stage, string.IsNullOrEmpty(stage.stageId) ? stage.name : stage.stageId, stage.comingSoon ? "coming soon" : string.Empty,
                            MakeCell("mapName", "Map name", 0.2f, 30, !stage.comingSoon),
                            MakeCell("shortDescription", "Short description", 0.5f, 200, !stage.comingSoon),
                            MakeCell("threeStarCondition", "Three-star condition", 0.3f, 80, false)));
                    break;

                case Tab.Acts:
                    foreach (ActData act in LoadAll<ActData>())
                        result.Add(MakeRow(t, act, act.DisplayName, string.Empty,
                            MakeCell("zoneName", "Zone name", 0.2f, 40, true),
                            MakeCell("zoneLore", "Zone lore", 0.6f, 600, true),
                            MakeCell("loreTag", "Lore tag", 0.2f, 50, false)));
                    break;

                case Tab.Tutorial:
                    foreach (TutorialScript script in LoadAll<TutorialScript>())
                    {
                        result.Add(MakeRow(t, script, script.name + ": speaker", string.Empty,
                            MakeCell("speakerName", "Speaker name", 0.25f, 30, true),
                            MakeCell("emptyFieldWarning", "Warning when no operator is on the field", 0.75f, 200, false)));
                        foreach (string array in new[] { "mapIntro", "steps", "onDefeat", "onLifeLost" })
                            AddTutorialSteps(result, script, array);
                    }
                    break;
            }
            return result;
        }

        private void AddTutorialSteps(List<Row> result, TutorialScript script, string arrayName)
        {
            var so = new SerializedObject(script);
            SerializedProperty array = so.FindProperty(arrayName);
            if (array == null || !array.isArray) return;

            for (int i = 0; i < array.arraySize; i++)
            {
                SerializedProperty element = array.GetArrayElementAtIndex(i);
                SerializedProperty text = element.FindPropertyRelative("text");
                if (text == null || string.IsNullOrWhiteSpace(text.stringValue)) continue; // steps with no line are actions, not text

                SerializedProperty action = element.FindPropertyRelative("action");
                string actionName = action != null && action.enumValueIndex >= 0 && action.enumValueIndex < action.enumDisplayNames.Length
                    ? action.enumDisplayNames[action.enumValueIndex] : string.Empty;
                result.Add(MakeRow(Tab.Tutorial, script, arrayName + " #" + (i + 1), actionName,
                    MakeCell(text.propertyPath, "Line", 1f, 260, false)));
            }
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var found = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !AssetDatabase.GetAssetPath(asset).Contains("/Tests/")) found.Add(asset);
            }
            found.Sort((a, b) => string.CompareOrdinal(AssetDatabase.GetAssetPath(a), AssetDatabase.GetAssetPath(b)));
            return found;
        }

        // ============================
        // Reading and writing a cell
        // ============================

        private static string GetText(Row row, Cell cell)
        {
            SerializedProperty property = row.so.FindProperty(cell.path);
            return property != null && property.propertyType == SerializedPropertyType.String ? property.stringValue ?? string.Empty : string.Empty;
        }

        /// <summary>Writes a cell's text (recorded for Undo). False when the field does not exist.</summary>
        private bool SetText(Row row, Cell cell, string value)
        {
            SerializedProperty property = row.so.FindProperty(cell.path);
            if (property == null || property.propertyType != SerializedPropertyType.String) return false;
            property.stringValue = value;
            row.so.ApplyModifiedProperties();
            issuesDirty = true;
            return true;
        }

        private bool IsLocked(Cell cell)
        {
            return cell.isName && !unlockNames;
        }

        // ============================
        // Checks
        // ============================

        private void RecomputeIssues()
        {
            issuesDirty = false;
            foreach (List<Row> tabRows in rows.Values)
            {
                foreach (Row row in tabRows)
                {
                    row.so.Update();
                    foreach (Cell cell in row.cells)
                    {
                        string text = GetText(row, cell);
                        cell.issues = LoreTextRules.Check(text, cell.softLimit, cell.required, oldTerms);
                    }
                }
            }
        }

        private static int IssueCount(Row row, bool warningsOnly)
        {
            int count = 0;
            foreach (Cell cell in row.cells)
                foreach (LoreIssue issue in cell.issues)
                    if (!warningsOnly || issue.severity == LoreIssueSeverity.Warning) count++;
            return count;
        }

        private int TabIssueCount(Tab t)
        {
            int count = 0;
            foreach (Row row in rows[t]) count += IssueCount(row, true);
            return count;
        }

        // ============================
        // Saving
        // ============================

        private IEnumerable<UnityEngine.Object> AllAssets()
        {
            var seen = new HashSet<UnityEngine.Object>();
            foreach (List<Row> tabRows in rows.Values)
                foreach (Row row in tabRows)
                    if (row.asset != null && seen.Add(row.asset)) yield return row.asset;
        }

        private bool AnyDirty()
        {
            foreach (UnityEngine.Object asset in AllAssets())
                if (EditorUtility.IsDirty(asset)) return true;
            return false;
        }

        private void SaveAll()
        {
            int saved = 0;
            foreach (UnityEngine.Object asset in AllAssets())
            {
                if (!EditorUtility.IsDirty(asset)) continue;
                AssetDatabase.SaveAssetIfDirty(asset);
                saved++;
            }
            hasUnsavedChanges = false;
            SetStatus(saved > 0 ? "Saved " + saved + " asset" + (saved == 1 ? "" : "s") + "." : "Nothing to save.", MessageType.Info);
        }

        private void SetStatus(string message, MessageType type)
        {
            status = message;
            statusType = type;
        }

        // ============================
        // Drawing
        // ============================

        private void EnsureStyles()
        {
            if (areaStyle != null) return;
            areaStyle = new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            smallLabel = new GUIStyle(EditorStyles.miniBoldLabel);
            smallRight = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            issueStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (issuesDirty) RecomputeIssues();

            bool dirty = AnyDirty();
            if (hasUnsavedChanges != dirty) hasUnsavedChanges = dirty;

            DrawToolbar();
            DrawTools();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);

            List<Row> tabRows = rows[tab];
            DrawSummary(tabRows);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            float contentWidth = position.width - 48f;
            int shown = 0;
            foreach (Row row in tabRows)
            {
                if (!PassesFilter(row)) continue;
                shown++;
                DrawRow(row, contentWidth);
            }
            if (shown == 0) EditorGUILayout.HelpBox(tabRows.Count == 0 ? "Nothing here yet." : "No row matches the search.", MessageType.Info);
            EditorGUILayout.EndScrollView();

            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.control || e.command) && e.keyCode == KeyCode.S)
            {
                SaveAll();
                e.Use();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int warnings = TabIssueCount((Tab)i);
                string label = TabNames[i] + (warnings > 0 ? " (" + warnings + ")" : string.Empty);
                bool on = GUILayout.Toggle(tab == (Tab)i, new GUIContent(label, warnings > 0 ? warnings + " warning" + (warnings == 1 ? "" : "s") : string.Empty), EditorStyles.toolbarButton, GUILayout.Width(100f));
                if (on && tab != (Tab)i)
                {
                    tab = (Tab)i;
                    scroll = Vector2.zero;
                    EditorPrefs.SetInt(TabPrefKey, i);
                }
            }

            GUILayout.Space(10f);
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(120f), GUILayout.MaxWidth(260f));
            issuesOnly = GUILayout.Toggle(issuesOnly, new GUIContent("Issues only", "Only show rows that have a warning or note."), EditorStyles.toolbarButton, GUILayout.Width(80f));
            unlockNames = GUILayout.Toggle(unlockNames,
                new GUIContent("Unlock names", "Unit names are locked because some code matches on them (for example operatorName == \"Vespierce\"). Renaming one here breaks that code."),
                EditorStyles.toolbarButton, GUILayout.Width(95f));

            GUILayout.FlexibleSpace();
            using (new EditorGUI.DisabledScope(!AnyDirty()))
            {
                if (GUILayout.Button(new GUIContent("Save", "Write every edited asset to disk (Ctrl+S)."), EditorStyles.toolbarButton, GUILayout.Width(50f))) SaveAll();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary(List<Row> tabRows)
        {
            int words = 0;
            int characters = 0;
            int warnings = 0;
            int notes = 0;
            foreach (Row row in tabRows)
            {
                foreach (Cell cell in row.cells)
                {
                    string text = GetText(row, cell);
                    words += LoreTextRules.WordCount(text);
                    characters += text.Length;
                    foreach (LoreIssue issue in cell.issues)
                    {
                        if (issue.severity == LoreIssueSeverity.Warning) warnings++;
                        else notes++;
                    }
                }
            }
            EditorGUILayout.LabelField(tabRows.Count + " rows, " + words + " words, " + characters + " characters. " +
                                       warnings + " warning" + (warnings == 1 ? "" : "s") + ", " + notes + " note" + (notes == 1 ? "" : "s") + ".", EditorStyles.miniLabel);
        }

        private bool PassesFilter(Row row)
        {
            if (issuesOnly && IssueCount(row, false) == 0) return false;
            if (string.IsNullOrEmpty(search)) return true;
            if (row.title != null && row.title.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            foreach (Cell cell in row.cells)
                if (GetText(row, cell).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private void DrawRow(Row row, float contentWidth)
        {
            row.so.Update();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(row.title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(row.subtitle)) GUILayout.Label(row.subtitle, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            int warnings = IssueCount(row, true);
            if (warnings > 0)
            {
                Color old = GUI.contentColor;
                GUI.contentColor = WarningColor;
                GUILayout.Label(warnings + " warning" + (warnings == 1 ? "" : "s"), EditorStyles.miniLabel);
                GUI.contentColor = old;
            }
            if (EditorUtility.IsDirty(row.asset)) GUILayout.Label("*", EditorStyles.boldLabel, GUILayout.Width(10f));
            if (GUILayout.Button(new GUIContent("Select", "Select the asset in the project."), EditorStyles.miniButton, GUILayout.Width(50f)))
            {
                Selection.activeObject = row.asset;
                EditorGUIUtility.PingObject(row.asset);
            }
            EditorGUILayout.EndHorizontal();

            float totalWeight = 0f;
            foreach (Cell cell in row.cells) totalWeight += cell.weight;
            float available = contentWidth - 10f * row.cells.Count;

            EditorGUILayout.BeginHorizontal();
            foreach (Cell cell in row.cells)
            {
                float width = Mathf.Max(90f, available * cell.weight / totalWeight);
                EditorGUILayout.BeginVertical(GUILayout.Width(width));
                DrawCell(row, cell, width);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawCell(Row row, Cell cell, float width)
        {
            string text = GetText(row, cell);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(cell.label, smallLabel);
            GUILayout.FlexibleSpace();
            bool over = cell.softLimit > 0 && text.Length > cell.softLimit;
            Color old = GUI.contentColor;
            if (over) GUI.contentColor = OverLimitColor;
            GUILayout.Label(text.Length + (cell.softLimit > 0 ? " / " + cell.softLimit : string.Empty), smallRight);
            GUI.contentColor = old;
            EditorGUILayout.EndHorizontal();

            if (IsLocked(cell))
            {
                EditorGUILayout.LabelField(text, EditorStyles.boldLabel, GUILayout.Width(width));
            }
            else
            {
                float height = Mathf.Max(areaStyle.lineHeight * 1.6f, areaStyle.CalcHeight(new GUIContent(text), width - 8f)) + 4f;
                EditorGUI.BeginChangeCheck();
                string edited = EditorGUILayout.TextArea(text, areaStyle, GUILayout.Width(width), GUILayout.Height(height));
                if (EditorGUI.EndChangeCheck()) SetText(row, cell, edited);
            }

            foreach (LoreIssue issue in cell.issues)
            {
                GUI.contentColor = issue.severity == LoreIssueSeverity.Warning ? WarningColor : InfoColor;
                GUILayout.Label((issue.severity == LoreIssueSeverity.Warning ? "! " : "- ") + issue.message, issueStyle, GUILayout.Width(width));
            }
            GUI.contentColor = old;
        }
    }
}
