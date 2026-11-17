using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating Stage 1 and ensuring the other stages exist.
    public partial class InitialAssetGenerator
    {
        private static void GenerateStage()
        {
            string path = $"{StageDataFolder}/STAGE_01.asset";
            var stage = AssetDatabase.LoadAssetAtPath<StageData>(path);
            if (stage != null && !ConfirmOverwrite("Regenerate " + stage.name, new List<string> { stage.name })) return;
            if (stage == null)
            {
                stage = ScriptableObject.CreateInstance<StageData>();
                AssetDatabase.CreateAsset(stage, path);
            }

            stage.stageId = "STAGE_01";
            stage.environment = StageEnvironment.Weather;
            stage.mapName = "Landfill Outskirts";
            stage.gridWidth = 12;
            stage.gridHeight = 6;

            var levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 1/level1 complete.png");
            if (levelSprites == null || levelSprites.Length == 0)
                levelSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 1/Level1 complete.png");

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

            // The scenery around the map: the 576x416 bounds art holds the 384x224 map 3 tiles in from the left, bottom and top
            var boundsSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 1/Level1 Bounds.png");
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
            stage.boundsTileOffset = new Vector2(-3f, -3f);

            var fgSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 1/level 1 bottom.png");
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

            // Lives: 3 on Easy/Normal, 1 on Hard
            stage.lifePointsEasy = 3;
            stage.lifePointsNormal = 3;
            stage.lifePointsHard = 1;

            string[] layoutRows =
            {
                "HHHHHHHHHHHH",
                "SLLHLLLHHHHH",
                "HHLHLHLHLLLH",
                "HHLHLHLLLHLE",
                "HHLLLHHHHHHH",
                "HHHHHHHHHHHH"
            };
            stage.tileLayout = new TileType[12 * 6];
            var spawnPoints = new List<Vector2Int>();
            var exitPoints = new List<Vector2Int>();
            for (int row = 0; row < layoutRows.Length; row++)
            {
                for (int x = 0; x < 12; x++)
                {
                    int y = layoutRows.Length - 1 - row;
                    int index = y * 12 + x;
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
                        default:
                            stage.tileLayout[index] = TileType.LowGround;
                            break;
                    }
                }
            }
            stage.spawnPoints = spawnPoints.ToArray();
            stage.exitPoints = exitPoints.ToArray();

            var grunt = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Grunt_Sludge.asset");
            var rusher = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Rusher_Toxic.asset");
            var tank = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}/Enemy_Tank_Pollution.asset");

            if (grunt != null && ShouldGenerateWaves(stage))
            {
                stage.wavesEasy = new[]
                {
                    new WaveData
                    {
                        waveName = "Wave 1: Scouts",
                        preWaveDelay = 2.0f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = grunt, count = 3, spawnInterval = 3.0f, startDelay = 0f, spawnPointIndex = 0 }
                        }
                    }
                };

                stage.wavesNormal = new[]
                {
                    new WaveData
                    {
                        waveName = "Wave 1: Sludge Scouts",
                        preWaveDelay = 2.0f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = grunt, count = 3, spawnInterval = 2.5f, startDelay = 0f, spawnPointIndex = 0 }
                        }
                    },
                    new WaveData
                    {
                        waveName = "Wave 2: Toxic Surge",
                        preWaveDelay = 4.0f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = grunt, count = 4, spawnInterval = 2.0f, startDelay = 0f, spawnPointIndex = 0 },
                            new WaveEntry { enemyData = rusher, count = 2, spawnInterval = 1.5f, startDelay = 3f, spawnPointIndex = 0 }
                        }
                    }
                };

                stage.wavesHard = new[]
                {
                    new WaveData
                    {
                        waveName = "Wave 1: Advance Swarm",
                        preWaveDelay = 1.5f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = grunt, count = 5, spawnInterval = 1.8f, startDelay = 0f, spawnPointIndex = 0 },
                            new WaveEntry { enemyData = rusher, count = 3, spawnInterval = 1.2f, startDelay = 2f, spawnPointIndex = 0 }
                        }
                    },
                    new WaveData
                    {
                        waveName = "Wave 2: Heavy Incursion",
                        preWaveDelay = 3.0f,
                        entries = new[]
                        {
                            new WaveEntry { enemyData = tank, count = 1, spawnInterval = 0f, startDelay = 0f, spawnPointIndex = 0 },
                            new WaveEntry { enemyData = rusher, count = 4, spawnInterval = 1.2f, startDelay = 2f, spawnPointIndex = 0 }
                        }
                    }
                };

                stage.wavesEasy = ExtendWaveCampaign(stage.wavesEasy, 10, 0, grunt, rusher, tank);
                stage.wavesNormal = ExtendWaveCampaign(stage.wavesNormal, 20, 1, grunt, rusher, tank);
                stage.wavesHard = ExtendWaveCampaign(stage.wavesHard, 30, 2, grunt, rusher, tank);
            }

            EditorUtility.SetDirty(stage);
        }

        // On editor load, only create stage assets that are missing. Existing stages are never
        // regenerated automatically (that used to overwrite hand-edited waves on every recompile).
        // To reset one on purpose, delete its asset and let this recreate it.
        [InitializeOnLoadMethod]
        private static void AutoEnsureStages()
        {
            EditorApplication.delayCall += () =>
            {
                CreateStageIfMissing("STAGE_02.asset", GenerateStage2);
                CreateStageIfMissing("STAGE_03.asset", GenerateStage3);
                CreateStageIfMissing("STAGE_05.asset", GenerateStage5);
                CreateStageIfMissing("STAGE_06.asset", GenerateStage6);
            };
        }

        private static void CreateStageIfMissing(string fileName, System.Action generate)
        {
            if (AssetDatabase.LoadAssetAtPath<StageData>($"{StageDataFolder}/{fileName}") == null)
                generate();
        }
    }
}
