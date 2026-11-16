using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Loading the roster, the encounter list and the sightings.
    public partial class ReclaimersIntelUI
    {
        private void LoadRoster()
        {
            if (rosterLoaded) return;
            rosterLoaded = true;

            var operators = new List<OperatorData>();
            foreach (OperatorData op in UnitIntelRegistry.GetOperators())
            {
                if (op != null) operators.Add(op);
            }
            operators.Sort((a, b) =>
            {
                int result = Array.IndexOf(ClassOrder, a.operatorClass).CompareTo(Array.IndexOf(ClassOrder, b.operatorClass));
                if (result != 0) return result;
                result = ((int)a.baseRarity).CompareTo((int)b.baseRarity);
                return result != 0 ? result : string.Compare(a.operatorName, b.operatorName, StringComparison.OrdinalIgnoreCase);
            });

            var enemies = new List<EnemyData>();
            foreach (EnemyData enemy in UnitIntelRegistry.GetEnemies())
            {
                if (enemy != null) enemies.Add(enemy);
            }
            enemies.Sort((a, b) =>
            {
                int result = Array.IndexOf(ArchetypeOrder, a.archetype).CompareTo(Array.IndexOf(ArchetypeOrder, b.archetype));
                if (result != 0) return result;
                result = a.baseHP.CompareTo(b.baseHP);
                return result != 0 ? result : string.Compare(a.enemyName, b.enemyName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < operators.Count; i++)
            {
                OperatorData op = operators[i];
                Sprite[] frames = UnitSpriteFrames.GetOperatorFrames(op, out float duration);
                operatorEntries.Add(new IntelEntry { Operator = op, FileId = "OP-" + (i + 1).ToString("00"), Frames = frames, FrameDuration = duration });

                OperatorRarity top = TopRarity(op);
                operatorStatMax[0] = Mathf.Max(operatorStatMax[0], op.GetScaledHP(top));
                operatorStatMax[1] = Mathf.Max(operatorStatMax[1], op.GetScaledATK(top));
                operatorStatMax[2] = Mathf.Max(operatorStatMax[2], op.GetScaledDEF(top));
                operatorStatMax[3] = Mathf.Max(operatorStatMax[3], op.GetScaledRES(top));
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyData enemy = enemies[i];
                Sprite[] frames = UnitSpriteFrames.GetEnemyFrames(enemy, out float frameRate);
                enemyEntries.Add(new IntelEntry { Enemy = enemy, FileId = "EN-" + (i + 1).ToString("00"), Frames = frames, FrameDuration = 1f / Mathf.Max(0.01f, frameRate) });

                enemyStatMax[0] = Mathf.Max(enemyStatMax[0], enemy.GetScaledHP(DifficultyLabels.Length));
                enemyStatMax[1] = Mathf.Max(enemyStatMax[1], enemy.GetScaledATK(DifficultyLabels.Length));
                enemyStatMax[2] = Mathf.Max(enemyStatMax[2], enemy.GetScaledDEF(DifficultyLabels.Length));
                enemyStatMax[3] = Mathf.Max(enemyStatMax[3], enemy.baseRES);
            }

            foreach (IntelEntry entry in operatorEntries) UsePointFilter(entry.Frames);
            foreach (IntelEntry entry in enemyEntries) UsePointFilter(entry.Frames);

            CollectSightings();

            operatorsCount.text = operatorEntries.Count + " RECLAIMERS ON FILE";
            SetPixelSprite(operatorsSprite, FindCover(operatorEntries, "Coalesce"), 256f);
        }

        /// <summary>Locks the files of enemies not met yet, and updates the enemy dossier's count and cover.</summary>
        private void RefreshEncounters()
        {
            int identified = 0;
            foreach (IntelEntry entry in enemyEntries)
            {
                entry.Locked = !TrashTD.Core.GameLoop.EnemyIntelProgress.IsEncountered(entry.Enemy);
                if (!entry.Locked) identified++;
            }
            enemiesCount.text = identified + " / " + enemyEntries.Count + " HOSTILES IDENTIFIED";

            // The dossier cover is artwork, not a file: Toxic Rusher is always shown in full, met or not
            SetPixelSprite(enemiesSprite, FindCover(enemyEntries, "Toxic Rusher"), 256f);
        }

        /// <summary>
        /// Stage names and order, and the stages whose waves (any difficulty) include each enemy, from the stage assets
        /// in Resources. Players only see the stages where they met the enemy (<see cref="GetSightings"/>).
        /// </summary>
        private void CollectSightings()
        {
            foreach (StageData stage in Resources.LoadAll<StageData>("Stages"))
            {
                if (stage == null || string.IsNullOrEmpty(stage.stageId) || stage.stageId.ToUpperInvariant().Contains("SANDBOX")) continue;
                if (stage.stageId == TrashTD.Core.GameLoop.TutorialDirector.TutorialStageId) continue; // Training drill, not a sighting
                stageNames[stage.stageId] = string.IsNullOrEmpty(stage.mapName) ? stage.stageId : stage.mapName;
                stageOrder[stage.stageId] = TrashTD.Core.GameLoop.StageProgress.GetStageNumber(stage.stageId);

                foreach (WaveData[] waves in new[] { stage.wavesEasy, stage.wavesNormal, stage.wavesHard })
                {
                    if (waves == null) continue;
                    foreach (WaveData wave in waves)
                    {
                        if (wave?.entries == null) continue;
                        foreach (WaveEntry waveEntry in wave.entries)
                        {
                            EnemyData enemy = waveEntry?.enemyData;
                            if (enemy == null) continue;
                            if (!plannedSightings.TryGetValue(enemy, out List<string> stages))
                            {
                                stages = new List<string>();
                                plannedSightings[enemy] = stages;
                            }
                            if (!stages.Contains(stage.stageId)) stages.Add(stage.stageId);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Names of the stages where the player has met this enemy, in stage order. Dev mode also lists every stage
        /// whose waves include it (spawned minions such as Oculite Spawn are only known from encounters).
        /// </summary>
        private List<string> GetSightings(EnemyData enemy)
        {
            var ids = TrashTD.Core.GameLoop.EnemyIntelProgress.GetSightedStageIds(enemy);
            if (TrashTD.Development.DevMode.IsEnabled && plannedSightings.TryGetValue(enemy, out List<string> planned))
            {
                foreach (string id in planned)
                {
                    if (!ids.Contains(id)) ids.Add(id);
                }
            }

            ids.RemoveAll(id => !stageNames.ContainsKey(id));
            ids.Sort((a, b) => stageOrder[a].CompareTo(stageOrder[b]));
            return ids.ConvertAll(id => stageNames[id]);
        }

        private static Sprite FindCover(List<IntelEntry> entries, string unitName)
        {
            foreach (IntelEntry entry in entries)
            {
                if (string.Equals(entry.Name, unitName, StringComparison.OrdinalIgnoreCase) && entry.Frames.Length > 0)
                    return entry.Frames[0];
            }
            return entries.Count > 0 && entries[0].Frames.Length > 0 ? entries[0].Frames[0] : null;
        }

        private static OperatorRarity TopRarity(OperatorData op)
        {
            // Operators upgrade up to two tiers above their base rarity (see OperatorUpgradeBadge).
            return (OperatorRarity)Mathf.Min((int)op.baseRarity + 2, (int)OperatorRarity.Star5);
        }

        // ─────────────────────────────────────────────────────
        //  Build
        // ─────────────────────────────────────────────────────
    }
}
