using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating Stage 5 and its waves.
    public partial class InitialAssetGenerator
    {
        // No menu item: this only runs to create a missing STAGE_05.asset (AutoEnsureStages) or from Generate Sample Data
        // Assets. The stage asset is tuned by hand, so there is no menu command to reset it.
        public static void GenerateStage5()
        {
            string path = $"{StageDataFolder}/STAGE_05.asset";
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null && !ConfirmOverwrite("Regenerate " + stage.name, new List<string> { stage.name })) return;
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, path);
            }

            stage.stageId = "STAGE_05";
            stage.environment = StageEnvironment.Traffic;
            stage.mapName = "Boulevard";
            stage.shortDescription = "Navigate the pipes and hold the boulevard against toxic pollutants.";
            stage.gridWidth = 13;
            stage.gridHeight = 8;

            var levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/Level 5 complete.png");
            if (levelSprites == null || levelSprites.Length == 0)
                levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/level 5 complete.png");

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

            // The scenery around the map: the 864x448 expanded art holds the 416x384 map 7 tiles in from the left and 3 tiles from the bottom
            var boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/Level 5 expanded.png");
            if (boundsSprites == null || boundsSprites.Length == 0)
                boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/level 5 expanded.png");

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
            stage.boundsTileOffset = new Vector2(-7f, -3f);

            var fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/Level 5 bottom.png");
            if (fgSprites == null || fgSprites.Length == 0)
                fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/level 5 bottom.png");

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

            var vehicleAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/Level 5 vehicles.png");
            if (vehicleAssets == null || vehicleAssets.Length == 0)
                vehicleAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/level 5 vehicles.png");

            var vList = new List<Sprite>();
            if (vehicleAssets != null)
            {
                foreach (var obj in vehicleAssets)
                {
                    if (obj is Sprite s)
                    {
                        vList.Add(s);
                    }
                }
            }
            vList.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            stage.trafficVehicleSprites = vList.ToArray();

            stage.backgroundVisualSprite = null;
            stage.upperBackgroundVisualSprites = null;
            stage.squadSizeLimit = 8;
            stage.lifePointsEasy = 3;
            stage.lifePointsNormal = 3;
            stage.lifePointsHard = 1;

            string[] layoutRows =
            {
                "BHHSSHHHHHHHH",
                "HHHLLHHHOHHHE",
                "SLLLLLLLLLLLL",
                "SLLLLLLLLLLLL",
                "BHHLLHHHHHHHH",
                "BHHLLHHHHHHHH",
                "SLLLLLLLLLLLS",
                "BBBBBOBBBBBBB"
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
                        case 'B':
                            stage.tileLayout[index] = TileType.Blocked;
                            break;
                        case 'O':
                            stage.tileLayout[index] = TileType.Hole;
                            break;
                        default:
                            stage.tileLayout[index] = TileType.LowGround;
                            break;
                    }
                }
            }

            stage.spawnPoints = new[]
            {
                new Vector2Int(3, 7),
                new Vector2Int(4, 7),
                new Vector2Int(0, 5),
                new Vector2Int(0, 4),
                new Vector2Int(0, 1),
                new Vector2Int(12, 1)
            };
            stage.exitPoints = new[]
            {
                new Vector2Int(12, 6)
            };

            stage.enemyPaths = new[]
            {
                new PathData
                {
                    spawnPointIndex = 0,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(3, 7), new Vector2Int(3, 6), new Vector2Int(3, 5),
                        new Vector2Int(3, 4), new Vector2Int(3, 3), new Vector2Int(3, 2),
                        new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1),
                        new Vector2Int(5, 0),
                        new Vector2Int(8, 6),
                        new Vector2Int(8, 5), new Vector2Int(9, 5), new Vector2Int(10, 5),
                        new Vector2Int(11, 5), new Vector2Int(12, 5), new Vector2Int(12, 6)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 1,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(4, 7), new Vector2Int(4, 6), new Vector2Int(4, 5),
                        new Vector2Int(4, 4), new Vector2Int(4, 3), new Vector2Int(4, 2),
                        new Vector2Int(4, 1), new Vector2Int(5, 1),
                        new Vector2Int(5, 0),
                        new Vector2Int(8, 6),
                        new Vector2Int(8, 5), new Vector2Int(8, 4), new Vector2Int(9, 4),
                        new Vector2Int(10, 4), new Vector2Int(11, 4), new Vector2Int(12, 4),
                        new Vector2Int(12, 5), new Vector2Int(12, 6)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 2,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(2, 5),
                        new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5),
                        new Vector2Int(6, 5), new Vector2Int(7, 5), new Vector2Int(8, 5),
                        new Vector2Int(9, 5), new Vector2Int(10, 5), new Vector2Int(11, 5),
                        new Vector2Int(12, 5), new Vector2Int(12, 6)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 3,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(0, 4), new Vector2Int(1, 4), new Vector2Int(2, 4),
                        new Vector2Int(3, 4), new Vector2Int(4, 4), new Vector2Int(5, 4),
                        new Vector2Int(6, 4), new Vector2Int(7, 4), new Vector2Int(8, 4),
                        new Vector2Int(9, 4), new Vector2Int(10, 4), new Vector2Int(11, 4),
                        new Vector2Int(12, 4), new Vector2Int(12, 5), new Vector2Int(12, 6)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 4,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(0, 1), new Vector2Int(1, 1),
                        new Vector2Int(3, 1), new Vector2Int(3, 2), new Vector2Int(3, 3),
                        new Vector2Int(3, 4), new Vector2Int(3, 5), new Vector2Int(4, 5),
                        new Vector2Int(5, 5), new Vector2Int(6, 5), new Vector2Int(7, 5),
                        new Vector2Int(8, 5), new Vector2Int(9, 5), new Vector2Int(10, 5),
                        new Vector2Int(11, 5), new Vector2Int(12, 5), new Vector2Int(12, 6)
                    }
                },
                new PathData
                {
                    spawnPointIndex = 5,
                    exitPointIndex = 0,
                    waypoints = new[]
                    {
                        new Vector2Int(12, 1), new Vector2Int(11, 1), new Vector2Int(10, 1),
                        new Vector2Int(9, 1), new Vector2Int(8, 1), new Vector2Int(7, 1),
                        new Vector2Int(6, 1), new Vector2Int(5, 1), new Vector2Int(4, 1),
                        new Vector2Int(4, 2), new Vector2Int(4, 3), new Vector2Int(4, 4),
                        new Vector2Int(5, 4), new Vector2Int(6, 4), new Vector2Int(7, 4),
                        new Vector2Int(8, 4), new Vector2Int(9, 4), new Vector2Int(10, 4),
                        new Vector2Int(11, 4), new Vector2Int(12, 4), new Vector2Int(12, 5),
                        new Vector2Int(12, 6)
                    }
                }
            };

            var grunt = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Grunt_Sludge.asset");
            var rusher = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Rusher_Toxic.asset");
            var tank = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Tank_Pollution.asset");

            if (ShouldGenerateWaves(stage))
            {
                stage.wavesEasy = BuildStage5Waves(10, 0, grunt, rusher, tank, stage.spawnPoints.Length);
                stage.wavesNormal = BuildStage5Waves(20, 1, grunt, rusher, tank, stage.spawnPoints.Length);
                stage.wavesHard = BuildStage5Waves(30, 2, grunt, rusher, tank, stage.spawnPoints.Length);
            }

            EditorUtility.SetDirty(stage);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>TRASH TD: Stage 5 (Boulevard) successfully generated!</color>");
        }

        private static WaveData[] BuildStage5Waves(int targetWaveCount, int difficultyTier,
            EnemyData grunt, EnemyData rusher, EnemyData tank, int spawnCount)
        {
            var waves = new List<WaveData>();

            for (int w = 1; w <= targetWaveCount; w++)
            {
                int waveNumber = w;
                int threat = waveNumber + difficultyTier * 2;

                float preWaveDelay = Mathf.Max(1.5f, 4.0f - difficultyTier * 0.5f - waveNumber * 0.05f);
                float spawnInterval = Mathf.Max(0.7f, 2.2f - difficultyTier * 0.2f - waveNumber * 0.035f);

                var entries = new List<WaveEntry>();
                bool isTankWave = threat >= 8 && waveNumber % 5 == 0 && tank != null;
                bool isRusherWave = !isTankWave && threat >= 4 && waveNumber % 3 == 0 && rusher != null;

                int primaryLane = (waveNumber - 1) % spawnCount;
                int secondaryLane = (primaryLane + 2) % spawnCount;
                int holeLane = 5;

                if (isTankWave)
                {
                    entries.Add(new WaveEntry
                    {
                        enemyData = tank,
                        count = 1,
                        spawnInterval = 0f,
                        startDelay = 0f,
                        spawnPointIndex = primaryLane
                    });

                    int escortCount = 1 + threat / 4;
                    if (escortCount > 0 && grunt != null)
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = grunt,
                            count = escortCount,
                            spawnInterval = spawnInterval,
                            startDelay = 1.5f,
                            spawnPointIndex = secondaryLane
                        });
                    }

                    if (rusher != null)
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = rusher,
                            count = 1 + difficultyTier,
                            spawnInterval = 1.0f,
                            startDelay = 2.5f,
                            spawnPointIndex = holeLane
                        });
                    }
                }
                else if (isRusherWave)
                {
                    int rushCount = 2 + threat / 3;
                    entries.Add(new WaveEntry
                    {
                        enemyData = rusher,
                        count = rushCount,
                        spawnInterval = Mathf.Max(0.6f, spawnInterval * 0.6f),
                        startDelay = 0f,
                        spawnPointIndex = primaryLane
                    });

                    entries.Add(new WaveEntry
                    {
                        enemyData = rusher,
                        count = Mathf.Max(1, rushCount / 2),
                        spawnInterval = 0.8f,
                        startDelay = 1.2f,
                        spawnPointIndex = holeLane
                    });
                }
                else
                {
                    int gruntCount = 2 + threat / 2;
                    entries.Add(new WaveEntry
                    {
                        enemyData = grunt != null ? grunt : rusher,
                        count = gruntCount,
                        spawnInterval = spawnInterval,
                        startDelay = 0f,
                        spawnPointIndex = primaryLane
                    });

                    if (waveNumber >= 2)
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = grunt != null ? grunt : rusher,
                            count = Mathf.Max(1, gruntCount - 1),
                            spawnInterval = spawnInterval,
                            startDelay = 1.2f,
                            spawnPointIndex = (primaryLane == holeLane) ? secondaryLane : holeLane
                        });
                    }
                }

                waves.Add(new WaveData
                {
                    waveName = $"Wave {waveNumber}",
                    preWaveDelay = preWaveDelay,
                    entries = entries.ToArray()
                });
            }

            return waves.ToArray();
        }
    }
}
