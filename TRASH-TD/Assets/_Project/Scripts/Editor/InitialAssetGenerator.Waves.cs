using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Wave building helpers shared by the stages.
    public partial class InitialAssetGenerator
    {
        private static WaveData[] DistributeWavesToLanes(WaveData[] sourceWaves)
        {
            if (sourceWaves == null) return new WaveData[0];
            var result = new WaveData[sourceWaves.Length];
            for (int i = 0; i < sourceWaves.Length; i++)
            {
                var src = sourceWaves[i];
                var entries = new List<WaveEntry>();
                if (src.entries != null)
                {
                    foreach (var srcEntry in src.entries)
                    {
                        if (srcEntry == null || srcEntry.enemyData == null) continue;
                        int distributedCount = Mathf.Max(1, Mathf.CeilToInt(srcEntry.count * 0.35f));
                        for (int lane = 0; lane < 3; lane++)
                        {
                            entries.Add(new WaveEntry
                            {
                                enemyData = srcEntry.enemyData,
                                count = distributedCount,
                                spawnInterval = srcEntry.spawnInterval,
                                startDelay = srcEntry.startDelay + lane * 0.4f,
                                spawnPointIndex = lane
                            });
                        }
                    }
                }
                result[i] = new WaveData
                {
                    waveName = src.waveName,
                    preWaveDelay = src.preWaveDelay,
                    entries = entries.ToArray()
                };
            }
            return result;
        }

        private static WaveData[] BuildStage3Waves(int difficultyTier, EnemyData grunt, EnemyData rusher, EnemyData tank)
        {
            int multiplier = difficultyTier + 1;
            return new[]
            {
                new WaveData
                {
                    waveName = "Wave 1: Crossing Patrol",
                    preWaveDelay = 2f,
                    entries = new[]
                    {
                        new WaveEntry { enemyData = grunt, count = 2 * multiplier, spawnInterval = 2f, spawnPointIndex = 0 }
                    }
                },
                new WaveData
                {
                    waveName = "Wave 2: Toxic Rush",
                    preWaveDelay = 3f,
                    entries = new[]
                    {
                        new WaveEntry { enemyData = rusher != null ? rusher : grunt, count = multiplier, spawnInterval = 1.5f, spawnPointIndex = 1 },
                        new WaveEntry { enemyData = grunt, count = 2 * multiplier, spawnInterval = 2f, startDelay = 1f, spawnPointIndex = 0 }
                    }
                },
                new WaveData
                {
                    waveName = "Wave 3: Heavy Crossing",
                    preWaveDelay = 3f,
                    entries = new[]
                    {
                        new WaveEntry { enemyData = tank != null ? tank : grunt, count = 1, spawnInterval = 0f, spawnPointIndex = 0 },
                        new WaveEntry { enemyData = grunt, count = 2 * multiplier, spawnInterval = 1.5f, startDelay = 1f, spawnPointIndex = 1 }
                    }
                }
            };
        }

        private static WaveData[] BuildStage2Waves(int targetWaveCount, int difficultyTier,
            EnemyData grunt, EnemyData rusher, EnemyData tank)
        {
            var waves = new List<WaveData>();

            for (int w = 1; w <= targetWaveCount; w++)
            {
                int waveNumber = w;
                int threat = waveNumber + difficultyTier * 2;

                float preWaveDelay = Mathf.Max(1.5f, 4.0f - difficultyTier * 0.5f - waveNumber * 0.05f);
                float spawnInterval = Mathf.Max(0.7f, 2.2f - difficultyTier * 0.2f - waveNumber * 0.035f);

                var entries = new List<WaveEntry>();
                string waveCategory;

                bool isTankWave = threat >= 8 && waveNumber % 5 == 0 && tank != null;
                bool isRusherWave = !isTankWave && threat >= 4 && waveNumber % 3 == 0 && rusher != null;

                // Alternate primary lane between spawn 0 (Top-Right) and spawn 1 (Bottom-Left)
                int primaryLane = (waveNumber % 2 == 1) ? 0 : 1;
                int secondaryLane = (primaryLane == 0) ? 1 : 0;

                float primaryDelay = 0f;
                float secondaryDelay = (waveNumber == 1) ? 2.0f : 1.5f;

                if (isTankWave)
                {
                    bool doubleTank = threat >= 16;
                    waveCategory = doubleTank ? "Heavy Dual Incursion" : "Heavy Pincer Incursion";

                    if (!doubleTank)
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = tank,
                            count = 1,
                            spawnInterval = 0f,
                            startDelay = primaryDelay,
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
                                startDelay = primaryDelay + 1.2f,
                                spawnPointIndex = primaryLane
                            });
                        }

                        EnemyData flankEnemy = (rusher != null) ? rusher : grunt;
                        int flankCount = 2 + threat / 3;
                        entries.Add(new WaveEntry
                        {
                            enemyData = flankEnemy,
                            count = flankCount,
                            spawnInterval = spawnInterval * 0.85f,
                            startDelay = secondaryDelay,
                            spawnPointIndex = secondaryLane
                        });
                    }
                    else
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = tank,
                            count = 1,
                            spawnInterval = 0f,
                            startDelay = primaryDelay,
                            spawnPointIndex = primaryLane
                        });
                        entries.Add(new WaveEntry
                        {
                            enemyData = tank,
                            count = 1,
                            spawnInterval = 0f,
                            startDelay = secondaryDelay,
                            spawnPointIndex = secondaryLane
                        });

                        int supportCount = 2 + threat / 4;
                        if (grunt != null)
                        {
                            entries.Add(new WaveEntry
                            {
                                enemyData = grunt,
                                count = supportCount,
                                spawnInterval = spawnInterval,
                                startDelay = primaryDelay + 1.2f,
                                spawnPointIndex = primaryLane
                            });
                        }
                        if (rusher != null)
                        {
                            entries.Add(new WaveEntry
                            {
                                enemyData = rusher,
                                count = supportCount,
                                spawnInterval = spawnInterval * 0.8f,
                                startDelay = secondaryDelay + 1.2f,
                                spawnPointIndex = secondaryLane
                            });
                        }
                    }
                }
                else if (isRusherWave)
                {
                    waveCategory = "Converging Toxic Surge";
                    int rusherPrimary = 2 + threat / 4;
                    int rusherSecondary = 1 + threat / 4;

                    entries.Add(new WaveEntry
                    {
                        enemyData = rusher,
                        count = rusherPrimary,
                        spawnInterval = spawnInterval * 0.8f,
                        startDelay = primaryDelay,
                        spawnPointIndex = primaryLane
                    });
                    entries.Add(new WaveEntry
                    {
                        enemyData = rusher,
                        count = rusherSecondary,
                        spawnInterval = spawnInterval * 0.8f,
                        startDelay = secondaryDelay,
                        spawnPointIndex = secondaryLane
                    });

                    if (threat >= 6 && grunt != null)
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = grunt,
                            count = 2 + threat / 5,
                            spawnInterval = spawnInterval,
                            startDelay = primaryDelay + 1.8f,
                            spawnPointIndex = primaryLane
                        });
                    }
                }
                else
                {
                    waveCategory = (waveNumber == 1) ? "Converging Scouts" : "Crossfire Swarm";
                    int countPrimary = (waveNumber == 1) ? 2 : (2 + threat / 4);
                    int countSecondary = (waveNumber == 1) ? 2 : (1 + threat / 4);

                    entries.Add(new WaveEntry
                    {
                        enemyData = grunt != null ? grunt : rusher,
                        count = countPrimary,
                        spawnInterval = spawnInterval,
                        startDelay = primaryDelay,
                        spawnPointIndex = primaryLane
                    });
                    entries.Add(new WaveEntry
                    {
                        enemyData = grunt != null ? grunt : rusher,
                        count = countSecondary,
                        spawnInterval = spawnInterval,
                        startDelay = secondaryDelay,
                        spawnPointIndex = secondaryLane
                    });

                    if (threat >= 7 && rusher != null)
                    {
                        entries.Add(new WaveEntry
                        {
                            enemyData = rusher,
                            count = 1 + threat / 6,
                            spawnInterval = spawnInterval * 0.85f,
                            startDelay = secondaryDelay + 1.5f,
                            spawnPointIndex = secondaryLane
                        });
                    }
                }

                waves.Add(new WaveData
                {
                    waveName = $"Wave {waveNumber}: {waveCategory}",
                    preWaveDelay = preWaveDelay,
                    entries = entries.ToArray()
                });
            }

            return waves.ToArray();
        }

        private static WaveData[] ExtendWaveCampaign(WaveData[] openingWaves, int targetWaveCount, int difficultyTier,
            EnemyData grunt, EnemyData rusher, EnemyData tank)
        {
            var waves = new List<WaveData>(openingWaves ?? new WaveData[0]);

            while (waves.Count < targetWaveCount)
            {
                int waveNumber = waves.Count + 1;
                int threat = waveNumber + difficultyTier * 2;
                EnemyData waveEnemy = threat >= 9 && waveNumber % 5 == 0 && tank != null
                    ? tank
                    : threat >= 5 && waveNumber % 3 == 0 && rusher != null
                        ? rusher
                        : grunt;
                int enemyCount = 2 + threat / 3;
                string enemyGroup = waveEnemy == tank ? "Heavy Incursion" : waveEnemy == rusher ? "Toxic Surge" : "Debris Swarm";

                waves.Add(new WaveData
                {
                    waveName = $"Wave {waveNumber}: {enemyGroup}",
                    preWaveDelay = Mathf.Max(1f, 4f - difficultyTier * 0.75f - waveNumber * 0.08f),
                    entries = new[]
                    {
                        new WaveEntry
                        {
                            enemyData = waveEnemy,
                            count = enemyCount,
                            spawnInterval = Mathf.Max(0.7f, 2.4f - difficultyTier * 0.25f - waveNumber * 0.04f),
                            startDelay = 0f,
                            spawnPointIndex = 0
                        }
                    }
                });
            }

            return waves.ToArray();
        }
    }
}
