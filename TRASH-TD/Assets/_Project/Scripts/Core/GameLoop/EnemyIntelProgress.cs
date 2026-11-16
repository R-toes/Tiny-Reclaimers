using System.Collections.Generic;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Development;
using TrashTD.Systems;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// Which enemies the player has met, and on which stages (kept in the JSON save, see SaveSystem). Reclaimers' Intel
    /// keeps an enemy's file locked (silhouette, scrambled name, no data) until that enemy has spawned in one of the
    /// player's runs, and lists only the stages where it was met under SIGHTED AT.
    /// - An entry per enemy asset exists once it was met anywhere, the tutorial included.
    /// - Its sighted stage ids are where it was met, in the order they were first met. The tutorial is a training
    ///   drill, so it unlocks the file but isn't listed.
    /// The sandbox is a dev tool and records nothing. Dev mode shows every file as met.
    /// </summary>
    public static class EnemyIntelProgress
    {
        private const char Separator = '|';
        private const string SandboxStageId = "SANDBOX";

        // Enemy + stage pairs already saved this session, so repeat spawns cost a set lookup instead of a list search
        private static readonly HashSet<string> recorded = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() => recorded.Clear();

        /// <summary>Forgets the session cache (the save was erased, so everything must be recorded again).</summary>
        internal static void ClearSession() => recorded.Clear();

        /// <summary>True once the player has met this enemy (always true in dev mode).</summary>
        public static bool IsEncountered(EnemyData enemy)
        {
            if (enemy == null) return false;
            return DevMode.IsEnabled || SaveSystem.Data.FindIntel(KeyOf(enemy)) != null;
        }

        /// <summary>Stage ids where the player has met this enemy, in the order they were first met.</summary>
        public static List<string> GetSightedStageIds(EnemyData enemy)
        {
            var ids = new List<string>();
            if (enemy == null) return ids;

            EnemyIntelEntry entry = SaveSystem.Data.FindIntel(KeyOf(enemy));
            if (entry != null) ids.AddRange(entry.sightedStageIds);
            return ids;
        }

        /// <summary>Called for every enemy that spawns (EnemyManager.SpawnEnemy). Saves only what is new.</summary>
        public static void RecordEncounter(EnemyData enemy, StageData stage)
        {
            if (enemy == null || stage == null || string.IsNullOrEmpty(stage.stageId)) return;
            string stageId = stage.stageId;
            if (stageId == SandboxStageId) return;

            string key = KeyOf(enemy);
            if (!recorded.Add(key + Separator + stageId)) return;

            SaveData save = SaveSystem.Data;
            bool changed = false;
            EnemyIntelEntry entry = save.FindIntel(key);
            if (entry == null)
            {
                entry = new EnemyIntelEntry { enemyName = key };
                save.enemyIntel.Add(entry);
                changed = true;
            }

            if (stageId != TutorialDirector.TutorialStageId && !entry.sightedStageIds.Contains(stageId))
            {
                entry.sightedStageIds.Add(stageId);
                changed = true;
            }

            if (changed) SaveSystem.Save();
        }

        /// <summary>Forgets every encounter (TRASH TD → Reset Player Progress).</summary>
        public static void ResetAll()
        {
            SaveSystem.Data.enemyIntel.Clear();
            SaveSystem.Save();
            recorded.Clear();
        }

        // Asset names are unique and stable; display names can change
        private static string KeyOf(EnemyData enemy) => enemy.name;
    }
}
