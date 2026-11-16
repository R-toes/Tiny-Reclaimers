using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating the enemy data assets.
    public partial class InitialAssetGenerator
    {
        private static void GenerateEnemies()
        {
            var gruntSprite = LoadSprite("tex_enemy_grunt.png");
            var gruntPrefab = CreateOrGetEnemyPrefab("Prefab_Enemy_Grunt", gruntSprite, EnemyArchetype.Grunt);
            CreateEnemy("Enemy_Grunt_Sludge", "Sludge Grunt", EnemyArchetype.Grunt, EnemyMovementType.Ground,
                hp: 100, atk: 25, def: 10, res: 0, speed: 1.0f, isUnblockable: false,
                gruntSprite, gruntPrefab);

            var robozombieFrames = LoadSpriteFrames("Robozombie Grunt Sheet.png", "Robozombie Grunt Sheet", 8);
            bool hasAllRobozombieFrames = true;
            for (int i = 0; i < robozombieFrames.Length; i++)
                hasAllRobozombieFrames &= robozombieFrames[i] != null;

            if (hasAllRobozombieFrames)
            {
                var robozombiePrefab = CreateOrGetRobozombiePrefab("Prefab_Enemy_Robozombie", robozombieFrames);
                var robozombie = CreateEnemy("Enemy_Grunt_Robozombie", "Robozombie", EnemyArchetype.Grunt,
                    EnemyMovementType.Ground, hp: 200, atk: 25, def: 10, res: 0, speed: 0.75f,
                    isUnblockable: false, robozombieFrames[0], robozombiePrefab);
                robozombie.visualOffset = new Vector3(0f, 0.2f, 0f);
                EditorUtility.SetDirty(robozombie);
            }

            var spoilcupineFrames = LoadSpriteFrames("Spoilcupine Sheet.png", "Spoilcupine Sheet", 8);
            bool hasAllSpoilcupineFrames = true;
            for (int i = 0; i < spoilcupineFrames.Length; i++)
                hasAllSpoilcupineFrames &= spoilcupineFrames[i] != null;

            if (hasAllSpoilcupineFrames)
            {
                var spoilcupinePrefab = CreateOrGetSpoilcupinePrefab("Prefab_Enemy_Spoilcupine", spoilcupineFrames);
                var spoilcupine = CreateEnemy("Enemy_Grunt_Spoilcupine", "Spoilcupine", EnemyArchetype.Grunt,
                    EnemyMovementType.Ground, hp: 320, atk: 25, def: 5, res: 0, speed: 1.0f,
                    isUnblockable: false, spoilcupineFrames[0], spoilcupinePrefab);
                spoilcupine.damageType = DamageType.Physical;
                spoilcupine.animationFrames = spoilcupineFrames;
                spoilcupine.animationFrameRate = 8f;
                EditorUtility.SetDirty(spoilcupine);
            }

            var rusherSprite = LoadSprite("tex_enemy_rusher.png");
            var rusherPrefab = CreateOrGetEnemyPrefab("Prefab_Enemy_Rusher", rusherSprite, EnemyArchetype.Rusher);
            CreateEnemy("Enemy_Rusher_Toxic", "Toxic Rusher", EnemyArchetype.Rusher, EnemyMovementType.Ground,
                hp: 60, atk: 20, def: 5, res: 0, speed: 1.8f, isUnblockable: false,
                rusherSprite, rusherPrefab);

            var tankSprite = LoadSprite("tex_enemy_tank.png");
            var tankPrefab = CreateOrGetEnemyPrefab("Prefab_Enemy_Tank", tankSprite, EnemyArchetype.Tank);
            CreateEnemy("Enemy_Tank_Pollution", "Pollution Tank", EnemyArchetype.Tank, EnemyMovementType.Ground,
                hp: 350, atk: 45, def: 35, res: 5, speed: 0.6f, isUnblockable: false,
                tankSprite, tankPrefab);

            var sandHulkFrames = LoadSpriteFrames("Sand Hulk Sheet.png", "Sand Hulk Sheet", 8);
            bool hasAllSandHulkFrames = true;
            for (int i = 0; i < sandHulkFrames.Length; i++)
                hasAllSandHulkFrames &= sandHulkFrames[i] != null;

            if (hasAllSandHulkFrames)
            {
                var sandHulk = CreateEnemy("Enemy_Tank_SandHulk", "Sand Hulk", EnemyArchetype.Tank,
                    EnemyMovementType.Ground, hp: 750, atk: 45, def: 35, res: 5, speed: 0.45f,
                    isUnblockable: false, sandHulkFrames[0], tankPrefab);
                sandHulk.damageType = DamageType.Physical;
                sandHulk.operatorAttackMissChance = 0.2f;
                sandHulk.frontAreaAttackTiles = 2;
                sandHulk.animationFrames = sandHulkFrames;
                sandHulk.animationFrameRate = 8f;
                sandHulk.visualScale = 2f;
                sandHulk.visualOffset = new Vector3(0.15f, 0.2f, 0f);
                EditorUtility.SetDirty(sandHulk);
            }

            var gooelistFrames = LoadSpriteFrames("Gooelist Sheet.png", "Gooelist Sheet", 8);
            bool hasAllGooelistFrames = true;
            for (int i = 0; i < gooelistFrames.Length; i++)
                hasAllGooelistFrames &= gooelistFrames[i] != null;

            if (hasAllGooelistFrames)
            {
                var gooelistPrefab = CreateOrGetGooelistPrefab("Prefab_Enemy_Gooelist", gooelistFrames);
                var gooelist = CreateEnemy("Enemy_Tank_Gooelist", "Gooelist", EnemyArchetype.Tank,
                    EnemyMovementType.Ground, hp: 650, atk: 45, def: 120, res: 0, speed: 0.9f,
                    isUnblockable: false, gooelistFrames[0], gooelistPrefab);
                gooelist.damageType = DamageType.Physical;
                gooelist.animationFrames = gooelistFrames;
                gooelist.animationFrameRate = 8f;
                EditorUtility.SetDirty(gooelist);
            }

            var smogCasterFrames = LoadSpriteFrames("smog caster sheet.png", "smog caster sheet", 8);
            bool hasAllSmogCasterFrames = true;
            for (int i = 0; i < smogCasterFrames.Length; i++)
                hasAllSmogCasterFrames &= smogCasterFrames[i] != null;

            var casterSprite = hasAllSmogCasterFrames
                ? smogCasterFrames[0]
                : LoadSprite("tex_enemy_caster.png");
            var casterPrefab = CreateOrGetEnemyPrefab("Prefab_Enemy_Caster", casterSprite, EnemyArchetype.Caster);
            var smogCaster = CreateEnemy("Enemy_Caster_Smog", "Smog Caster", EnemyArchetype.Caster, EnemyMovementType.Ground,
                hp: 110, atk: 35, def: 10, res: 15, speed: 0.85f, isUnblockable: false,
                casterSprite, casterPrefab);
            if (hasAllSmogCasterFrames)
            {
                smogCaster.animationFrames = smogCasterFrames;
                smogCaster.animationFrameRate = 8f;
            }
            EditorUtility.SetDirty(smogCaster);

            var sandPonchoSprite = LoadSprite("Sand Poncho Enemy Sheet.png", "Sand Poncho Enemy Sheet_0");
            var sandPoncho = CreateEnemy("Enemy_Caster_SandPoncho", "Sand Poncho", EnemyArchetype.Caster,
                EnemyMovementType.Ground, hp: 110, atk: 35, def: 10, res: 15, speed: 0.65f,
                isUnblockable: false, sandPonchoSprite, casterPrefab);
            sandPoncho.damageType = DamageType.Arts;
            sandPoncho.operatorAttackMissChance = 0.2f;
            sandPoncho.attackProjectileColor = new Color(0.95f, 0.72f, 0.34f);
            sandPoncho.usesSandProjectileVisual = true;
            sandPoncho.animationFrames = LoadSpriteFrames(
                "Sand Poncho Enemy Sheet.png",
                "Sand Poncho Enemy Sheet",
                8);
            EditorUtility.SetDirty(sandPoncho);

            var acidFlyerFrames = LoadSpriteFrames("acid flyer sheet.png", "acid flyer sheet", 8);
            bool hasAllAcidFlyerFrames = true;
            for (int i = 0; i < acidFlyerFrames.Length; i++)
                hasAllAcidFlyerFrames &= acidFlyerFrames[i] != null;

            var flyerSprite = hasAllAcidFlyerFrames
                ? acidFlyerFrames[0]
                : LoadSprite("tex_enemy_flyer.png");
            var flyerPrefab = CreateOrGetEnemyPrefab("Prefab_Enemy_Flyer", flyerSprite, EnemyArchetype.Flyer);
            var acidFlyer = CreateEnemy("Enemy_Flyer_Acid", "Acid Flyer", EnemyArchetype.Flyer, EnemyMovementType.Air,
                hp: 80, atk: 25, def: 5, res: 10, speed: 1.25f, isUnblockable: true,
                flyerSprite, flyerPrefab);
            acidFlyer.visualOffset = new Vector3(0f, 0.75f, 0f);
            if (hasAllAcidFlyerFrames)
            {
                acidFlyer.animationFrames = acidFlyerFrames;
                acidFlyer.animationFrameRate = 8f;
            }
            EditorUtility.SetDirty(acidFlyer);
        }

        private static EnemyData CreateEnemy(string assetName, string displayName, EnemyArchetype archetype,
            EnemyMovementType movementType, int hp, int atk, int def, int res, float speed, bool isUnblockable,
            Sprite sprite, GameObject prefab)
        {
            string path = $"{EnemyDataFolder}/{assetName}.asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemyData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.enemyName = displayName;
            data.archetype = archetype;
            data.movementType = movementType;
            data.baseHP = hp;
            data.baseATK = atk;
            data.baseDEF = def;
            data.baseRES = res;
            data.moveSpeed = speed;
            data.isUnblockable = isUnblockable;
            data.lifePointCost = 1;
            data.sprite = sprite;
            data.enemyPrefab = prefab;

            EditorUtility.SetDirty(data);
            return data;
        }
    }
}
