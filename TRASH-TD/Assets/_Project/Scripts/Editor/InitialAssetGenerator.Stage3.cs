using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating Stage 3.
    public partial class InitialAssetGenerator
    {
        [MenuItem("TRASH TD/Generate Stage 3 (Railyard Crossing)")]
        public static void GenerateStage3()
        {
            string path = $"{StageDataFolder}/STAGE_03.asset";
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null && !ConfirmOverwrite("Regenerate " + stage.name, new List<string> { stage.name })) return;
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, path);
            }

            stage.stageId = "STAGE_03";
            stage.environment = StageEnvironment.Sandstorm;
            stage.mapName = "Railyard Crossing";
            stage.shortDescription = "Defend the crossing while avoiding the toxic traps.";
            stage.gridWidth = 14;
            stage.gridHeight = 7;

            var backgroundSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 3/Level3-Backgroundt.png");
            stage.backgroundVisualSprite = null;
            if (backgroundSprites != null)
            {
                foreach (var obj in backgroundSprites)
                {
                    if (obj is Sprite sprite)
                    {
                        stage.backgroundVisualSprite = sprite;
                        break;
                    }
                }
            }

            var mainGroundSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 3/Level3-MainGroundt.png");
            stage.mapVisualSprite = null;
            if (mainGroundSprites != null)
            {
                foreach (var obj in mainGroundSprites)
                {
                    if (obj is Sprite sprite)
                    {
                        stage.mapVisualSprite = sprite;
                        break;
                    }
                }
            }

            stage.loadingScreenMapSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/Art/Levels/Level 3/Level3 Complete.png");

            var upperBackgroundSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 3/Level3-UpperBackgroundt.png");
            var upperBackgroundLayers = new List<Sprite>();
            if (upperBackgroundSprites != null)
            {
                foreach (var obj in upperBackgroundSprites)
                {
                    if (obj is Sprite sprite)
                    {
                        upperBackgroundLayers.Add(sprite);
                    }
                }
            }
            stage.upperBackgroundVisualSprites = upperBackgroundLayers.ToArray();

            // The scenery around the map: 640x416 ground art with the 448x224 map 3 tiles in from every side. It takes
            // the map artwork's place above the animated background, which is 640x416 per frame at the same offset.
            var boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 3/Level3 Bounds.png");
            stage.boundsVisualSprite = null;
            if (boundsSprites != null)
            {
                foreach (var obj in boundsSprites)
                {
                    if (obj is Sprite sprite)
                    {
                        stage.boundsVisualSprite = sprite;
                        break;
                    }
                }
            }
            stage.boundsTileOffset = new Vector2(-3f, -3f);
            stage.boundsReplacesMapArtwork = true;
            stage.backgroundTileOffset = new Vector2(-3f, -3f);
            stage.backgroundFrameSize = new Vector2Int(640, 416);

            stage.visualTilePixelSize = 32;
            stage.visualTileOffset = Vector2.zero;
            stage.foregroundVisualSprite = null;
            stage.squadSizeLimit = 8;
            stage.lifePointsEasy = 3;
            stage.lifePointsNormal = 3;
            stage.lifePointsHard = 1;

            string[] layoutRows =
            {
                "BBBBBBBBBBBBBB",
                "BHHHHHBBHHHHHB",
                "SLLHLLLLLLLLHH",
                "BBLLLBBBBBHLLH",
                "BBBBBBBBBBBBLH",
                "SLLTLLLLLLLLLH",
                "HHHTHHBBHHHHLE"
            };
            stage.tileLayout = new TileType[stage.gridWidth * stage.gridHeight];
            var spawnPoints = new List<Vector2Int>();
            var exitPoints = new List<Vector2Int>();
            for (int row = 0; row < layoutRows.Length; row++)
            {
                for (int x = 0; x < stage.gridWidth; x++)
                {
                    int y = layoutRows.Length - 1 - row;
                    int index = y * stage.gridWidth + x;
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
                        case 'T':
                            stage.tileLayout[index] = TileType.Trap;
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

            stage.spawnPoints = spawnPoints.ToArray();
            stage.exitPoints = exitPoints.ToArray();
            stage.enemyPaths = new[]
            {
                new PathData
                {
                    spawnPointIndex = 0,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(2, 4),
                        new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3),
                        new Vector2Int(4, 4), new Vector2Int(5, 4), new Vector2Int(6, 4),
                        new Vector2Int(7, 4), new Vector2Int(8, 4), new Vector2Int(9, 4),
                        new Vector2Int(10, 4), new Vector2Int(11, 4), new Vector2Int(11, 3),
                        new Vector2Int(12, 3), new Vector2Int(12, 2), new Vector2Int(12, 1),
                        new Vector2Int(12, 0), new Vector2Int(13, 0)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 1,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(0, 1), new Vector2Int(1, 1),
                        new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1),
                        new Vector2Int(6, 1), new Vector2Int(7, 1), new Vector2Int(8, 1),
                        new Vector2Int(9, 1), new Vector2Int(10, 1), new Vector2Int(11, 1),
                        new Vector2Int(12, 1), new Vector2Int(12, 0), new Vector2Int(13, 0)
                    }
                }
            };

            var grunt = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Grunt_Sludge.asset");
            var rusher = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Rusher_Toxic.asset");
            var tank = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Tank_Pollution.asset");
            if (ShouldGenerateWaves(stage))
            {
                stage.wavesEasy = BuildStage3Waves(0, grunt, rusher, tank);
                stage.wavesNormal = BuildStage3Waves(1, grunt, rusher, tank);
                stage.wavesHard = BuildStage3Waves(2, grunt, rusher, tank);
            }

            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();
        }
    }
}
