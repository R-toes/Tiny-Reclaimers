using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating Stage 2.
    public partial class InitialAssetGenerator
    {
        [MenuItem("TRASH TD/Generate Stage 2 (Scrapyard Junction)")]
        public static void GenerateStage2()
        {
            string path = $"{StageDataFolder}/STAGE_02.asset";
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null && !ConfirmOverwrite("Regenerate " + stage.name, new List<string> { stage.name })) return;
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, path);
            }

            stage.stageId = "STAGE_02";
            stage.environment = StageEnvironment.FallingItems;
            stage.mapName = "Scrapyard Junction";
            stage.shortDescription = "Secure the scrapyard junction against converging waves.";
            stage.gridWidth = 11;
            stage.gridHeight = 7;

            var levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 2/level2 complete.png");
            if (levelSprites == null || levelSprites.Length == 0)
                levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 2/Level2 complete.png");

            if (levelSprites != null)
            {
                foreach (var obj in levelSprites)
                {
                    if (obj is Sprite s)
                    {
                        stage.mapVisualSprite = s;
                        break;
                    }
                }
            }
            stage.visualTilePixelSize = 32;
            stage.visualTileOffset = Vector2.zero;

            // The scenery around the map: the 608x384 expanded art holds the 352x256 map 4 tiles in from the left and 2 tiles from the bottom
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 2/level 2 expanded.png"))
            {
                if (obj is Sprite s)
                {
                    stage.boundsVisualSprite = s;
                    break;
                }
            }
            stage.boundsTileOffset = new Vector2(-4f, -2f);

            var fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 2/level2 bottom.png");
            if (fgSprites == null || fgSprites.Length == 0)
                fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 2/Level2 bottom.png");

            if (fgSprites != null)
            {
                foreach (var obj in fgSprites)
                {
                    if (obj is Sprite s)
                    {
                        stage.foregroundVisualSprite = s;
                        break;
                    }
                }
            }
            stage.squadSizeLimit = 8;

            stage.lifePointsEasy = 3;
            stage.lifePointsNormal = 3;
            stage.lifePointsHard = 1;

            string[] layoutRows =
            {
                "BBBBBBBBBBS",
                "BBBBLLLLLLL",
                "BHHHLHHHHHH",
                "ELLLLLLLLLE",
                "HHHHHHLHHHB",
                "LLLLLLLBBBB",
                "SBBBBBBBBBB"
            };
            stage.tileLayout = new TileType[11 * 7];
            var spawnPoints = new List<Vector2Int>();
            var exitPoints = new List<Vector2Int>();
            for (int row = 0; row < layoutRows.Length; row++)
            {
                for (int x = 0; x < 11; x++)
                {
                    int y = layoutRows.Length - 1 - row;
                    int index = y * 11 + x;
                    switch (layoutRows[row][x])
                    {
                        case 'H':
                            stage.tileLayout[index] = TileType.HighGround;
                            break;
                        case 'S':
                            stage.tileLayout[index] = TileType.SpawnPoint;
                            spawnPoints.Add(new Vector2Int(x, y));
                            break;
                        case 'E':
                            stage.tileLayout[index] = TileType.ExitPoint;
                            exitPoints.Add(new Vector2Int(x, y));
                            break;
                        case 'B':
                            stage.tileLayout[index] = TileType.Blocked;
                            break;
                        default:
                            stage.tileLayout[index] = TileType.LowGround;
                            break;
                    }
                }
            }
            stage.spawnPoints = new[]
            {
                new Vector2Int(10, 6), // Spawn 0: Top-Right (Yellow arrow)
                new Vector2Int(0, 0)   // Spawn 1: Bottom-Left (Blue arrow)
            };
            stage.exitPoints = new[]
            {
                new Vector2Int(0, 3),  // Exit 0: Left exit (Blue arrow destination)
                new Vector2Int(10, 3)  // Exit 1: Right exit (Yellow arrow destination)
            };

            // Symmetrical paths crossing at Scrapyard Junction:
            // Path 0: Top-Right spawn (10, 6) -> Row 5 westward -> down col 4 -> Row 3 eastward -> Right exit (10, 3) [Yellow arrow]
            var path0Waypoints = new[]
            {
                new Vector2Int(10, 6),
                new Vector2Int(10, 5),
                new Vector2Int(9, 5),
                new Vector2Int(8, 5),
                new Vector2Int(7, 5),
                new Vector2Int(6, 5),
                new Vector2Int(5, 5),
                new Vector2Int(4, 5),
                new Vector2Int(4, 4),
                new Vector2Int(4, 3),
                new Vector2Int(5, 3),
                new Vector2Int(6, 3),
                new Vector2Int(7, 3),
                new Vector2Int(8, 3),
                new Vector2Int(9, 3),
                new Vector2Int(10, 3)
            };

            // Path 1: Bottom-Left spawn (0, 0) -> Row 1 eastward -> up col 6 -> Row 3 westward -> Left exit (0, 3) [Blue arrow]
            var path1Waypoints = new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(3, 1),
                new Vector2Int(4, 1),
                new Vector2Int(5, 1),
                new Vector2Int(6, 1),
                new Vector2Int(6, 2),
                new Vector2Int(6, 3),
                new Vector2Int(5, 3),
                new Vector2Int(4, 3),
                new Vector2Int(3, 3),
                new Vector2Int(2, 3),
                new Vector2Int(1, 3),
                new Vector2Int(0, 3)
            };

            stage.enemyPaths = new[]
            {
                new PathData
                {
                    spawnPointIndex = 0,
                    exitPointIndex = 1,
                    waypoints = path0Waypoints
                },
                new PathData
                {
                    spawnPointIndex = 1,
                    exitPointIndex = 0,
                    waypoints = path1Waypoints
                }
            };

            var grunt = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Grunt_Sludge.asset");
            var rusher = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Rusher_Toxic.asset");
            var tank = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Tank_Pollution.asset");

            if (ShouldGenerateWaves(stage))
            {
                stage.wavesEasy = BuildStage2Waves(10, 0, grunt, rusher, tank);
                stage.wavesNormal = BuildStage2Waves(20, 1, grunt, rusher, tank);
                stage.wavesHard = BuildStage2Waves(30, 2, grunt, rusher, tank);
            }

            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>TRASH TD: Stage 2 (Scrapyard Junction) successfully generated with dual-lane enemy waves!</color>");
        }
    }
}
