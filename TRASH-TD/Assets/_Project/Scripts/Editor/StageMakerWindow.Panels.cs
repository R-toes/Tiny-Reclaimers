using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    // The right-hand side of the Stage Maker: stage info, spawns and their routes, exits, and the checks.
    public sealed partial class StageMakerWindow
    {
        private void DrawRightColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(rightWidth));
            rightScroll = EditorGUILayout.BeginScrollView(rightScroll);

            DrawStageInfo();
            GUILayout.Space(8f);
            DrawArtPanel();

            if (StageMakerLogic.HasGrid(stage))
            {
                if (editingSpawn >= 0)
                {
                    GUILayout.Space(8f);
                    DrawRouteEditorPanel();
                }
                GUILayout.Space(8f);
                DrawSpawnList();
                GUILayout.Space(8f);
                DrawExitList();
            }

            GUILayout.Space(8f);
            DrawChecks();

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        // ============================
        // Stage info
        // ============================

        private void DrawStageInfo()
        {
            EditorGUILayout.LabelField("STAGE", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Asset", stage.name);
            if (GUILayout.Button(new GUIContent("Select", "Select the stage asset in the project (to see it in the Inspector)."), GUILayout.Width(56f)))
            {
                Selection.activeObject = stage;
                EditorGUIUtility.PingObject(stage);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField("Map name", stage.mapName);
            bool soon = EditorGUILayout.Toggle(new GUIContent("Coming soon", "A placeholder: it shows as a locked marker on the stage map and cannot be played."), stage.comingSoon);
            if (EditorGUI.EndChangeCheck())
            {
                RecordForEdit("Stage info");
                stage.mapName = newName;
                stage.comingSoon = soon;
                FinishEdit();
            }

            if (stage.stageId == "SANDBOX")
                EditorGUILayout.HelpBox("The sandbox takes its layout from the map it runs on (Stage 1 by default), so edit that stage instead. Changes here are overwritten when the sandbox starts.", MessageType.Info);
            else if (stage.stageId == "TUTORIAL")
                EditorGUILayout.HelpBox("The tutorial walks the player through this exact map. Play the tutorial again after changing it.", MessageType.Info);
        }

        // ============================
        // Route being drawn
        // ============================

        private void DrawRouteEditorPanel()
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = RouteColor(editingSpawn);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = old;

            EditorGUILayout.LabelField("DRAWING ROUTE FOR S" + editingSpawn, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(draft.Count + " tiles so far" + (DraftReachesExit() ? ", reaches an exit." : "."), EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(draftMarks.Count <= 1))
            {
                if (GUILayout.Button("Undo last click")) UndoRoutePoint();
                if (GUILayout.Button("Clear")) { ResetDraft(); Repaint(); }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Auto-finish", "Walk the rest of the way to the nearest exit and save."))) AutoFinishRoute();
            using (new EditorGUI.DisabledScope(!DraftReachesExit()))
            {
                if (GUILayout.Button("Save route")) ApplyRoute();
            }
            if (GUILayout.Button("Cancel")) CancelRouteEdit();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        // ============================
        // Spawns, exits
        // ============================

        private StageRoute FindRoute(int spawn)
        {
            foreach (StageRoute route in routes)
                if (route.spawn == spawn) return route;
            return null;
        }

        private void DrawSpawnList()
        {
            EditorGUILayout.LabelField("SPAWNS & ROUTES", EditorStyles.boldLabel);
            if (stage.spawnPoints == null || stage.spawnPoints.Length == 0)
            {
                EditorGUILayout.LabelField("No spawns yet. Pick the Spawn tile and click the grid.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            bool busy = editingSpawn >= 0 || relocating;
            for (int i = 0; i < stage.spawnPoints.Length; i++)
            {
                Vector2Int point = stage.spawnPoints[i];
                StagePathMode mode = StageMakerLogic.GetPathMode(stage, i, out int exitIndex);
                StageRoute route = FindRoute(i);

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                Rect swatch = GUILayoutUtility.GetRect(12f, 14f, GUILayout.Width(12f), GUILayout.Height(14f));
                EditorGUI.DrawRect(new Rect(swatch.x, swatch.y + 1f, 12f, 12f), RouteColor(i));
                GUILayout.Label("S" + i + "  (" + point.x + ", " + point.y + ")", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                string summary;
                Color summaryColor = Color.clear;
                if (route == null) { summary = "NO ROUTE"; summaryColor = ErrorColor; }
                else summary = route.cells.Count + " tiles" + (route.custom ? ", by hand" : ", auto");
                if (summaryColor != Color.clear) GUI.contentColor = summaryColor;
                GUILayout.Label(summary, EditorStyles.miniLabel);
                GUI.contentColor = Color.white;
                EditorGUILayout.EndHorizontal();

                using (new EditorGUI.DisabledScope(busy))
                {
                    DrawRouteModePopup(i, mode, exitIndex);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button(mode == StagePathMode.Custom ? "Redraw route" : "Draw route")) StartRouteEdit(i);
                    using (new EditorGUI.DisabledScope(mode == StagePathMode.AutoNearestExit))
                    {
                        if (GUILayout.Button("Reset to auto"))
                        {
                            if (mode != StagePathMode.Custom || EditorUtility.DisplayDialog("Reset route?", "This removes the hand-drawn route for S" + i + ". You can undo it with Ctrl+Z.", "Reset", "Cancel"))
                                ResetRoute(i);
                        }
                    }
                    if (GUILayout.Button("Move")) StartRelocate(true, i);
                    if (GUILayout.Button("Show", GUILayout.Width(46f))) HighlightCell(point);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawRouteModePopup(int spawn, StagePathMode mode, int exitIndex)
        {
            int exitCount = stage.exitPoints != null ? stage.exitPoints.Length : 0;
            var options = new List<string> { "Auto: nearest exit" };
            for (int e = 0; e < exitCount; e++) options.Add("Auto: always exit E" + e);
            if (mode == StagePathMode.Custom) options.Add("Drawn by hand");

            int current = mode == StagePathMode.Custom ? options.Count - 1 : mode == StagePathMode.AutoSpecificExit ? 1 + exitIndex : 0;
            int picked = EditorGUILayout.Popup(current, options.ToArray());
            if (picked == current || (mode == StagePathMode.Custom && picked == options.Count - 1)) return;

            if (mode == StagePathMode.Custom &&
                !EditorUtility.DisplayDialog("Replace the hand-drawn route?", "S" + spawn + " has a hand-drawn route. Switching to automatic routing removes it. You can undo it with Ctrl+Z.", "Replace", "Cancel"))
                return;

            RecordForEdit("Route mode");
            StageMakerLogic.SetAutoRoute(stage, spawn, picked == 0 ? -1 : picked - 1);
            FinishEdit();
        }

        private void DrawExitList()
        {
            EditorGUILayout.LabelField("EXITS", EditorStyles.boldLabel);
            if (stage.exitPoints == null || stage.exitPoints.Length == 0)
            {
                EditorGUILayout.LabelField("No exits yet. Pick the Exit tile and click the grid.", EditorStyles.wordWrappedMiniLabel);
                return;
            }

            bool busy = editingSpawn >= 0 || relocating;
            for (int i = 0; i < stage.exitPoints.Length; i++)
            {
                Vector2Int point = stage.exitPoints[i];
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUILayout.Label("E" + i + "  (" + point.x + ", " + point.y + ")", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("Move", GUILayout.Width(56f))) StartRelocate(false, i);
                }
                if (GUILayout.Button("Show", GUILayout.Width(46f))) HighlightCell(point);
                EditorGUILayout.EndHorizontal();
            }
        }

        // ============================
        // Checks
        // ============================

        private void DrawChecks()
        {
            EditorGUILayout.LabelField("CHECKS", EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(routeError))
                EditorGUILayout.HelpBox("Could not compute the routes: " + routeError, MessageType.Error);

            int errors = 0;
            int warnings = 0;
            foreach (StageIssue issue in issues)
            {
                if (issue.severity == StageIssueSeverity.Error) errors++;
                else if (issue.severity == StageIssueSeverity.Warning) warnings++;
            }

            if (issues.Count == 0 && string.IsNullOrEmpty(routeError))
            {
                EditorGUILayout.HelpBox("All good: the stage has a spawn and an exit, and enemies can reach an exit from every spawn.", MessageType.Info);
                return;
            }

            if (errors + warnings > 0)
                EditorGUILayout.LabelField(errors + " error" + (errors == 1 ? "" : "s") + ", " + warnings + " warning" + (warnings == 1 ? "" : "s"), EditorStyles.miniLabel);

            foreach (StageIssue issue in issues)
            {
                MessageType type = issue.severity == StageIssueSeverity.Error ? MessageType.Error
                    : issue.severity == StageIssueSeverity.Warning ? MessageType.Warning : MessageType.Info;
                EditorGUILayout.HelpBox(issue.message, type);

                if (!issue.hasCell && issue.fix == StageIssueFix.None) continue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (issue.hasCell && GUILayout.Button("Show", GUILayout.Width(52f))) HighlightCell(issue.cell);
                if (issue.fix == StageIssueFix.RepairLayout && GUILayout.Button("Repair layout", GUILayout.Width(100f))) RepairLayout();
                if (issue.fix == StageIssueFix.SyncSpawnsAndExits && GUILayout.Button("Match lists to tiles", GUILayout.Width(130f))) SyncSpawnsAndExits();
                EditorGUILayout.EndHorizontal();
            }
        }

        private void RepairLayout()
        {
            RecordForEdit("Repair layout");
            StageMakerLogic.RepairLayout(stage, FillChoices[Mathf.Clamp(fillChoice, 0, FillChoices.Length - 1)]);
            FinishEdit();
            SetStatus("Tile layout repaired to " + stage.gridWidth * stage.gridHeight + " tiles. Check that the tiles look right.", MessageType.Warning);
            GUIUtility.ExitGUI();
        }

        private void SyncSpawnsAndExits()
        {
            if (!EditorUtility.DisplayDialog("Match spawn and exit lists to the tiles?",
                    "Listed points that are not on a matching tile are removed (later numbers shift down, and waves and routes follow), and Spawn / Exit tiles missing from the lists are added at the end. You can undo it with Ctrl+Z.",
                    "Match", "Cancel"))
            {
                GUIUtility.ExitGUI();
                return;
            }

            RecordForEdit("Match lists to tiles");
            StageMakerLogic.SyncSpawnsAndExits(stage);
            FinishEdit();
            SetStatus("Spawn and exit lists now match the tiles.", MessageType.Info);
            GUIUtility.ExitGUI();
        }
    }
}
