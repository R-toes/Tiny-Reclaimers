using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;

namespace TrashTD.Editor
{
    // "Test on a stage": drops the operator on a real stage map, rotated to the chosen facing, and shows which tiles its pattern
    // reaches, how many of them enemies actually walk through (the routes the game would use) and whether the operator could be
    // deployed on that tile. Only the pattern shown in the main grid is tested (the chosen star or the talent range).
    public sealed partial class RangePatternWindow
    {
        private bool showStageTest = true;
        private List<StageData> testStages;
        private int testStageIndex;
        private StageData testStage;
        private List<StageRoute> testRoutes = new List<StageRoute>();
        private HashSet<Vector2Int> routeCells = new HashSet<Vector2Int>();
        private Vector2Int testCell;
        private bool hasTestCell;

        private void EnsureTestStages()
        {
            if (testStages != null) return;
            testStages = new List<StageData>();
            foreach (string guid in AssetDatabase.FindAssets("t:StageData"))
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageData>(AssetDatabase.GUIDToAssetPath(guid));
                if (stage != null && StageMakerLogic.HasGrid(stage) && StageMakerLogic.LayoutMatchesGrid(stage)) testStages.Add(stage);
            }
            testStages.Sort((a, b) => string.CompareOrdinal(a.stageId ?? a.name, b.stageId ?? b.name));
        }

        private void SelectTestStage(int index)
        {
            testStageIndex = Mathf.Clamp(index, 0, Mathf.Max(0, testStages.Count - 1));
            testStage = testStages.Count > 0 ? testStages[testStageIndex] : null;
            hasTestCell = false;
            routeCells = new HashSet<Vector2Int>();
            testRoutes = new List<StageRoute>();
            if (testStage == null) return;

            try
            {
                testRoutes = StageMakerRouting.ComputeRoutes(testStage);
            }
            catch (System.Exception)
            {
                testRoutes = new List<StageRoute>();
            }
            foreach (StageRoute route in testRoutes)
                if (route.cells != null)
                    foreach (Vector2Int cell in route.cells) routeCells.Add(cell);
        }

        private bool CanDeployOn(Vector2Int cell)
        {
            if (testStage == null || !StageMakerLogic.InBounds(testStage, cell)) return false;
            return new GridCell(cell.x, cell.y, testStage.GetTile(cell.x, cell.y), Vector3.zero).SupportsPosition(op.position);
        }

        private void DrawStageTest()
        {
            showStageTest = EditorGUILayout.Foldout(showStageTest, "TEST ON A STAGE", true, EditorStyles.foldoutHeader);
            if (!showStageTest) return;

            EnsureTestStages();
            if (testStages.Count == 0)
            {
                EditorGUILayout.HelpBox("No stage with a grid was found.", MessageType.Info);
                return;
            }
            if (testStage == null) SelectTestStage(testStageIndex);

            var names = new string[testStages.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = string.IsNullOrEmpty(testStages[i].mapName) ? testStages[i].name : testStages[i].stageId + "  " + testStages[i].mapName;

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("Stage", testStageIndex, names);
            if (EditorGUI.EndChangeCheck()) SelectTestStage(picked);
            if (testStage == null) return;

            if (!hasTestCell) ChooseDefaultTestCell();

            Vector2Int[] pattern = DisplayPattern();
            HashSet<Vector2Int> inRange = CellsInRange(pattern);

            float available = RightWidth - 40f;
            float cell = Mathf.Min(34f, Mathf.Floor(available / testStage.gridWidth));
            float width = cell * testStage.gridWidth;
            float height = cell * testStage.gridHeight;
            Rect map = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));

            Event e = Event.current;
            if (e.type == EventType.Repaint) DrawTestMap(map, cell, inRange);
            if (e.type == EventType.MouseDown && e.button == 0 && map.Contains(e.mousePosition))
            {
                testCell = new Vector2Int(Mathf.FloorToInt((e.mousePosition.x - map.x) / cell), testStage.gridHeight - 1 - Mathf.FloorToInt((e.mousePosition.y - map.y) / cell));
                hasTestCell = true;
                e.Use();
                Repaint();
            }

            DrawTestSummary(inRange);
        }

        private void ChooseDefaultTestCell()
        {
            hasTestCell = true;
            testCell = new Vector2Int(testStage.gridWidth / 2, testStage.gridHeight / 2);
            for (int y = 0; y < testStage.gridHeight; y++)
            {
                for (int x = 0; x < testStage.gridWidth; x++)
                {
                    if (!CanDeployOn(new Vector2Int(x, y))) continue;
                    testCell = new Vector2Int(x, y);
                    return;
                }
            }
        }

        /// <summary>The map tiles the pattern covers when the operator stands on the test tile facing the chosen way.</summary>
        private HashSet<Vector2Int> CellsInRange(Vector2Int[] pattern)
        {
            var result = new HashSet<Vector2Int>();
            if (pattern == null || testStage == null) return result;
            foreach (Vector2Int offset in pattern)
            {
                Vector2Int cell = testCell + RangePatternLogic.Rotate(offset, facing);
                if (StageMakerLogic.InBounds(testStage, cell)) result.Add(cell);
            }
            return result;
        }

        private static Rect MapCell(Rect map, float cell, StageData stage, Vector2Int c)
        {
            return new Rect(map.x + c.x * cell, map.y + (stage.gridHeight - 1 - c.y) * cell, cell, cell);
        }

        private void DrawTestMap(Rect map, float cell, HashSet<Vector2Int> inRange)
        {
            for (int y = 0; y < testStage.gridHeight; y++)
                for (int x = 0; x < testStage.gridWidth; x++)
                    StageMakerTiles.Draw(MapCell(map, cell, testStage, new Vector2Int(x, y)), testStage.GetTile(x, y));

            Color color = target == PatternTarget.Attack ? AttackColor : TalentColor;
            foreach (Vector2Int c in inRange)
            {
                Rect r = MapCell(map, cell, testStage, c);
                bool onRoute = routeCells.Contains(c);
                EditorGUI.DrawRect(r, new Color(color.r, color.g, color.b, onRoute ? 0.8f : 0.45f));
                if (onRoute) DrawBorder(r, Color.white, 1.5f);
            }

            Rect self = MapCell(map, cell, testStage, testCell);
            DrawBorder(self, CanDeployOn(testCell) ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.35f, 0.3f), 2.5f);
            if (op.portrait != null && op.portrait.texture != null)
            {
                Rect tr = op.portrait.textureRect;
                Texture2D tex = op.portrait.texture;
                GUI.DrawTextureWithTexCoords(new Rect(self.x + 2f, self.y + 2f, self.width - 4f, self.height - 4f), tex,
                    new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height));
            }
        }

        private void DrawTestSummary(HashSet<Vector2Int> inRange)
        {
            int onRoute = 0;
            int walkableOffRoute = 0;
            int other = 0;
            foreach (Vector2Int c in inRange)
            {
                if (routeCells.Contains(c)) onRoute++;
                else if (StageMakerLogic.IsWalkable(testStage.GetTile(c.x, c.y))) walkableOffRoute++;
                else other++;
            }

            TileType here = testStage.GetTile(testCell.x, testCell.y);
            bool canDeploy = CanDeployOn(testCell);
            EditorGUILayout.LabelField("Click the map to place " + op.operatorName + ". It is on (" + testCell.x + ", " + testCell.y + "), " + StageMakerTiles.Label(here) +
                                       (canDeploy ? ": a " + op.position + " operator can deploy here." : ": a " + op.position + " operator cannot deploy here."), wrapMini);
            EditorGUILayout.LabelField(inRange.Count + " tile" + (inRange.Count == 1 ? "" : "s") + " in range: " + onRoute + " on the enemy route, " +
                                       walkableOffRoute + " other walkable, " + other + " not walkable.", wrapMini);
            if (onRoute == 0 && inRange.Count > 0 && op.position != OperatorPosition.Any)
                EditorGUILayout.HelpBox("Nothing in range is on an enemy route from here. Try another tile or facing.", MessageType.Info);
        }
    }
}
