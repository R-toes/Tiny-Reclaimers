using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating Stage 6.
    public partial class InitialAssetGenerator
    {
        // No menu item: this only runs to create a missing STAGE_06.asset (AutoEnsureStages) or from Generate Sample Data
        // Assets. The stage asset is tuned by hand, so there is no menu command to reset it.
        public static void GenerateStage6()
        {
            string path = $"{StageDataFolder}/STAGE_06.asset";
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null && !ConfirmOverwrite("Regenerate " + stage.name, new List<string> { stage.name })) return;
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, path);
            }

            stage.stageId = "STAGE_06";
            stage.environment = StageEnvironment.AcidRain;
            stage.mapName = "Meltdown";
            stage.shortDescription = "Hold the line across the meltdown while avoiding the toxic hazard.";
            stage.gridWidth = 12;
            stage.gridHeight = 5;

            var levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/level 6 complete.png");
            if (levelSprites == null || levelSprites.Length == 0)
                levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/Level 6 complete.png");

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
            stage.visualTileOffset = new Vector2(0, -1);

            var fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/level 6 bottom.png");
            if (fgSprites == null || fgSprites.Length == 0)
                fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/Level 6 bottom.png");

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

            var midSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/Level 6 Sign-Barrel.png");
            if (midSprites == null || midSprites.Length == 0)
                midSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/level 6 Sign-Barrel.png");

            if (midSprites != null)
            {
                foreach (var obj in midSprites)
                {
                    if (obj is Sprite s)
                    {
                        stage.midgroundVisualSprites = new Sprite[] { s };
                        break;
                    }
                }
            }

            // The scenery around the map: the 576x288 expanded art holds the 384x256 map 3 tiles in from the left and 1 tile below
            var boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/Level 6 expanded.png");
            if (boundsSprites == null || boundsSprites.Length == 0)
                boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/level 6 expanded.png");
            if (boundsSprites == null || boundsSprites.Length == 0)
                boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/Level 6 extended.png");
            if (boundsSprites == null || boundsSprites.Length == 0)
                boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 6/level 6 extended.png");

            if (boundsSprites != null)
            {
                foreach (var obj in boundsSprites)
                {
                    if (obj is Sprite s)
                    {
                        stage.boundsVisualSprite = s;
                        break;
                    }
                }
            }
            stage.boundsTileOffset = new Vector2(-3f, -2f);

            stage.backgroundVisualSprite = null;
            stage.upperBackgroundVisualSprites = null;
            stage.squadSizeLimit = 8;
            stage.lifePointsEasy = 3;
            stage.lifePointsNormal = 3;
            stage.lifePointsHard = 1;

            string[] layoutRows =
            {
                "BHHHLLLLLLLS",
                "BHTTTHHHBHBB",
                "ELTBTLLLLLLS",
                "BHTTTHBHHHBB",
                "BHHHLLLLLLLS"
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
                            stage.tileLayout[index] = TileType.AcidLowGround;
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
                new Vector2Int(11, 4),
                new Vector2Int(11, 2),
                new Vector2Int(11, 0)
            };
            stage.exitPoints = new[]
            {
                new Vector2Int(0, 2)
            };

            stage.enemyPaths = new[]
            {
                new PathData
                {
                    spawnPointIndex = 0,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(11, 4), new Vector2Int(10, 4), new Vector2Int(9, 4),
                        new Vector2Int(8, 4), new Vector2Int(7, 4), new Vector2Int(6, 4),
                        new Vector2Int(5, 4), new Vector2Int(4, 4), new Vector2Int(4, 3),
                        new Vector2Int(4, 2), new Vector2Int(4, 1), new Vector2Int(3, 1),
                        new Vector2Int(2, 1), new Vector2Int(2, 2), new Vector2Int(1, 2),
                        new Vector2Int(0, 2)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 1,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(11, 2), new Vector2Int(10, 2), new Vector2Int(9, 2),
                        new Vector2Int(8, 2), new Vector2Int(7, 2), new Vector2Int(6, 2),
                        new Vector2Int(5, 2), new Vector2Int(4, 2), new Vector2Int(4, 3),
                        new Vector2Int(3, 3), new Vector2Int(2, 3), new Vector2Int(2, 2),
                        new Vector2Int(1, 2), new Vector2Int(0, 2)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 2,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(11, 0), new Vector2Int(10, 0), new Vector2Int(9, 0),
                        new Vector2Int(8, 0), new Vector2Int(7, 0), new Vector2Int(6, 0),
                        new Vector2Int(5, 0), new Vector2Int(4, 0), new Vector2Int(4, 1),
                        new Vector2Int(4, 2), new Vector2Int(4, 3), new Vector2Int(3, 3),
                        new Vector2Int(2, 3), new Vector2Int(2, 2), new Vector2Int(1, 2),
                        new Vector2Int(0, 2)
                    }
                }
            };

            var grunt = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Grunt_Sludge.asset");
            var rusher = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Rusher_Toxic.asset");
            var tank = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Tank_Pollution.asset");

            var stage1 = AssetDatabase.LoadAssetAtPath<StageData>($"{StageDataFolder}/STAGE_01.asset");
            bool generateWaves = ShouldGenerateWaves(stage);
            if (generateWaves && stage1 != null && stage1.wavesEasy != null && stage1.wavesEasy.Length > 0)
            {
                stage.wavesEasy = DistributeWavesToLanes(stage1.wavesEasy);
                stage.wavesNormal = DistributeWavesToLanes(stage1.wavesNormal);
                stage.wavesHard = DistributeWavesToLanes(stage1.wavesHard);
            }
            else if (generateWaves)
            {
                var baseEasy = new[]
                {
                    new WaveData
                    {
                        waveName = "Wave 1: Scouts",
                        preWaveDelay = 2.0f,
                        entries = new[] { new WaveEntry { enemyData = grunt, count = 3, spawnInterval = 3.0f } }
                    }
                };
                var baseNormal = new[]
                {
                    new WaveData
                    {
                        waveName = "Wave 1: Sludge Scouts",
                        preWaveDelay = 2.0f,
                        entries = new[] { new WaveEntry { enemyData = grunt, count = 3, spawnInterval = 2.5f } }
                    },
                    new WaveData
                    {
                        waveName = "Wave 2: Toxic Surge",
                        preWaveDelay = 4.0f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = grunt, count = 4, spawnInterval = 2.0f },
                            new WaveEntry { enemyData = rusher, count = 2, spawnInterval = 1.5f, startDelay = 3f }
                        }
                    }
                };
                var baseHard = new[]
                {
                    new WaveData
                    {
                        waveName = "Wave 1: Advance Swarm",
                        preWaveDelay = 1.5f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = grunt, count = 5, spawnInterval = 1.8f },
                            new WaveEntry { enemyData = rusher, count = 3, spawnInterval = 1.2f, startDelay = 2f }
                        }
                    },
                    new WaveData
                    {
                        waveName = "Wave 2: Heavy Incursion",
                        preWaveDelay = 3.0f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = tank, count = 1, spawnInterval = 0f },
                            new WaveEntry { enemyData = rusher, count = 4, spawnInterval = 1.2f, startDelay = 2f }
                        }
                    }
                };

                stage.wavesEasy = DistributeWavesToLanes(ExtendWaveCampaign(baseEasy, 10, 0, grunt, rusher, tank));
                stage.wavesNormal = DistributeWavesToLanes(ExtendWaveCampaign(baseNormal, 20, 1, grunt, rusher, tank));
                stage.wavesHard = DistributeWavesToLanes(ExtendWaveCampaign(baseHard, 30, 2, grunt, rusher, tank));
            }

            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>TRASH TD: Stage 6 (Meltdown) successfully generated!</color>");
        }
    }
}
