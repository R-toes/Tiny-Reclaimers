using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TrashTD.Editor
{
    // The Lore & Text Sheet's tools: find and replace across the sheet, the list of old names to flag, and CSV export / import.
    public sealed partial class LoreSheetWindow
    {
        private bool showTools;
        private string findText = string.Empty;
        private string replaceText = string.Empty;
        private bool matchCase;
        private bool wholeWord = true;
        private bool allTabs;
        private Vector2 oldTermsScroll;

        private void DrawTools()
        {
            EditorGUILayout.BeginHorizontal();
            showTools = EditorGUILayout.Foldout(showTools, "Find & replace, old names, CSV", true);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (!showTools) return;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            DrawFindReplace();
            GUILayout.Space(6f);
            DrawOldTerms();
            GUILayout.Space(6f);
            DrawCsvButtons();
            EditorGUILayout.EndVertical();
        }

        // ============================
        // Find and replace
        // ============================

        private IEnumerable<KeyValuePair<Row, Cell>> CellsInScope()
        {
            foreach (KeyValuePair<Tab, List<Row>> pair in rows)
            {
                if (!allTabs && pair.Key != tab) continue;
                foreach (Row row in pair.Value)
                    foreach (Cell cell in row.cells)
                        yield return new KeyValuePair<Row, Cell>(row, cell);
            }
        }

        private void DrawFindReplace()
        {
            EditorGUILayout.LabelField("Find & replace", EditorStyles.boldLabel);
            findText = EditorGUILayout.TextField("Find", findText);
            replaceText = EditorGUILayout.TextField("Replace with", replaceText);

            EditorGUILayout.BeginHorizontal();
            matchCase = GUILayout.Toggle(matchCase, "Match case", GUILayout.Width(90f));
            wholeWord = GUILayout.Toggle(wholeWord, new GUIContent("Whole word", "Do not match inside longer words."), GUILayout.Width(90f));
            allTabs = GUILayout.Toggle(allTabs, new GUIContent("All tabs", "Search every tab, not just this one."), GUILayout.Width(70f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            int matches = 0;
            int fields = 0;
            int lockedSkipped = 0;
            if (!string.IsNullOrEmpty(findText))
            {
                foreach (KeyValuePair<Row, Cell> pair in CellsInScope())
                {
                    int count = LoreTextRules.CountMatches(GetText(pair.Key, pair.Value), findText, matchCase, wholeWord);
                    if (count == 0) continue;
                    if (IsLocked(pair.Value)) { lockedSkipped += count; continue; }
                    matches += count;
                    fields++;
                }
            }

            string summary = string.IsNullOrEmpty(findText) ? "Type something to find." :
                matches + " match" + (matches == 1 ? "" : "es") + " in " + fields + " field" + (fields == 1 ? "" : "s") +
                (lockedSkipped > 0 ? " (" + lockedSkipped + " more in locked names)" : string.Empty) + ".";
            EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);

            using (new EditorGUI.DisabledScope(matches == 0))
            {
                if (GUILayout.Button("Replace all", GUILayout.Width(110f))) ReplaceAll(matches, fields);
            }
        }

        private void ReplaceAll(int matches, int fields)
        {
            if (!EditorUtility.DisplayDialog("Replace all?",
                    "Replace " + matches + " match" + (matches == 1 ? "" : "es") + " of \"" + findText + "\" with \"" + replaceText + "\" in " + fields + " field" + (fields == 1 ? "" : "s") +
                    "? You can undo it with Ctrl+Z.", "Replace", "Cancel"))
            {
                GUIUtility.ExitGUI();
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            int replaced = 0;
            foreach (KeyValuePair<Row, Cell> pair in CellsInScope())
            {
                if (IsLocked(pair.Value)) continue;
                string text = GetText(pair.Key, pair.Value);
                string changed = LoreTextRules.Replace(text, findText, replaceText, matchCase, wholeWord, out int count);
                if (count == 0) continue;
                SetText(pair.Key, pair.Value, changed);
                replaced += count;
            }
            Undo.CollapseUndoOperations(group);

            SetStatus("Replaced " + replaced + " match" + (replaced == 1 ? "" : "es") + ".", MessageType.Info);
            GUIUtility.ExitGUI();
        }

        // ============================
        // Old names
        // ============================

        private void DrawOldTerms()
        {
            EditorGUILayout.LabelField("Old names to flag (one per line)", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Text that still mentions one of these gets a warning. Matched as whole words, case-sensitively.", EditorStyles.miniLabel);

            string current = string.Join("\n", oldTerms);
            oldTermsScroll = EditorGUILayout.BeginScrollView(oldTermsScroll, GUILayout.Height(70f));
            EditorGUI.BeginChangeCheck();
            string edited = EditorGUILayout.TextArea(current, GUILayout.ExpandHeight(true));
            if (EditorGUI.EndChangeCheck())
            {
                var terms = new List<string>();
                foreach (string line in edited.Split('\n'))
                    if (!string.IsNullOrWhiteSpace(line)) terms.Add(line.Trim());
                oldTerms = terms.ToArray();
                EditorPrefs.SetString(OldTermsPrefKey, string.Join("|", oldTerms));
                issuesDirty = true;
            }
            EditorGUILayout.EndScrollView();
        }

        // ============================
        // CSV
        // ============================

        private void DrawCsvButtons()
        {
            EditorGUILayout.LabelField("Spreadsheet", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Export every field to a CSV, edit it in a spreadsheet, then import it back: only the fields whose text changed are updated.", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Export CSV...", GUILayout.Width(120f))) ExportCsv();
            if (GUILayout.Button("Import CSV...", GUILayout.Width(120f))) ImportCsv();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private static string AssetKey(Row row, Cell cell)
        {
            return AssetDatabase.GetAssetPath(row.asset) + "|" + cell.path;
        }

        private void ExportCsv()
        {
            string file = EditorUtility.SaveFilePanel("Export text to CSV", string.Empty, "tiny-reclaimers-text", "csv");
            if (string.IsNullOrEmpty(file)) return;

            var lines = new List<string[]> { new[] { "Tab", "Asset", "Field", "Label", "Text", "Locked" } };
            foreach (KeyValuePair<Tab, List<Row>> pair in rows)
            {
                foreach (Row row in pair.Value)
                {
                    row.so.Update();
                    foreach (Cell cell in row.cells)
                    {
                        lines.Add(new[]
                        {
                            TabNames[(int)pair.Key], AssetDatabase.GetAssetPath(row.asset), cell.path,
                            row.title + " / " + cell.label, GetText(row, cell), cell.isName ? "locked" : string.Empty
                        });
                    }
                }
            }

            File.WriteAllText(file, LoreTextRules.WriteCsv(lines), new UTF8Encoding(true)); // with a byte-order mark so Excel reads it as UTF-8
            SetStatus("Exported " + (lines.Count - 1) + " fields to " + file + ".", MessageType.Info);
            GUIUtility.ExitGUI();
        }

        private void ImportCsv()
        {
            string file = EditorUtility.OpenFilePanel("Import text from CSV", string.Empty, "csv");
            if (string.IsNullOrEmpty(file)) return;

            List<string[]> table = LoreTextRules.ParseCsv(File.ReadAllText(file, Encoding.UTF8));
            if (table.Count < 2 || table[0].Length < 5 || table[0][1] != "Asset" || table[0][2] != "Field" || table[0][4] != "Text")
            {
                EditorUtility.DisplayDialog("Not imported", "That does not look like a CSV exported from this window: it needs the columns Tab, Asset, Field, Label, Text.", "OK");
                GUIUtility.ExitGUI();
                return;
            }

            var incoming = new Dictionary<string, string>();
            for (int i = 1; i < table.Count; i++)
            {
                if (table[i].Length < 5) continue;
                incoming[table[i][1] + "|" + table[i][2]] = table[i][4];
            }

            var changes = new List<KeyValuePair<KeyValuePair<Row, Cell>, string>>();
            int locked = 0;
            int matched = 0;
            foreach (KeyValuePair<Tab, List<Row>> pair in rows)
            {
                foreach (Row row in pair.Value)
                {
                    row.so.Update();
                    foreach (Cell cell in row.cells)
                    {
                        if (!incoming.TryGetValue(AssetKey(row, cell), out string newText)) continue;
                        matched++;
                        if (LoreTextRules.NormalizeLineEndings(newText) == LoreTextRules.NormalizeLineEndings(GetText(row, cell))) continue;
                        if (IsLocked(cell)) { locked++; continue; }
                        changes.Add(new KeyValuePair<KeyValuePair<Row, Cell>, string>(new KeyValuePair<Row, Cell>(row, cell), LoreTextRules.NormalizeLineEndings(newText)));
                    }
                }
            }

            int notFound = incoming.Count - matched;
            if (changes.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to import",
                    "No field's text differs from the project." + (locked > 0 ? " " + locked + " changed name" + (locked == 1 ? " was" : "s were") + " skipped because names are locked." : string.Empty) +
                    (notFound > 0 ? " " + notFound + " row" + (notFound == 1 ? "" : "s") + " in the file match nothing in the project." : string.Empty), "OK");
                GUIUtility.ExitGUI();
                return;
            }

            var preview = new StringBuilder();
            for (int i = 0; i < Mathf.Min(changes.Count, 6); i++)
                preview.Append("- ").Append(changes[i].Key.Key.title).Append(" / ").Append(changes[i].Key.Value.label).Append('\n');
            if (changes.Count > 6) preview.Append("...and ").Append(changes.Count - 6).Append(" more\n");

            string message = changes.Count + " field" + (changes.Count == 1 ? "" : "s") + " will change:\n" + preview +
                             (locked > 0 ? "\n" + locked + " changed name" + (locked == 1 ? "" : "s") + " skipped (names are locked)." : string.Empty) +
                             (notFound > 0 ? "\n" + notFound + " row" + (notFound == 1 ? "" : "s") + " in the file match nothing in the project." : string.Empty) +
                             "\n\nYou can undo it with Ctrl+Z.";
            if (!EditorUtility.DisplayDialog("Import text?", message, "Import", "Cancel"))
            {
                GUIUtility.ExitGUI();
                return;
            }

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            foreach (KeyValuePair<KeyValuePair<Row, Cell>, string> change in changes)
                SetText(change.Key.Key, change.Key.Value, change.Value);
            Undo.CollapseUndoOperations(group);

            SetStatus("Imported " + changes.Count + " changed field" + (changes.Count == 1 ? "" : "s") + ".", MessageType.Info);
            GUIUtility.ExitGUI();
        }
    }
}
