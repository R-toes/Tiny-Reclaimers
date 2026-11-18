using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Core.GameLoop;

namespace TrashTD.Editor
{
    // Generating the operator data assets.
    public partial class InitialAssetGenerator
    {
        private static void GenerateOperators()
        {
            // 1. Guard (Scrapper)
            var scrapperFrame0 = LoadSprite("Scrapper-Sheet.png", "Scrapper-Sheet_0") ?? LoadSprite("Scrapper-Sheet.png", "Scrapper_0") ?? LoadSprite("tex_op_guard.png");
            var scrapperFrame1 = LoadSprite("Scrapper-Sheet.png", "Scrapper-Sheet_1") ?? LoadSprite("Scrapper-Sheet.png", "Scrapper_1");
            var scrapperFrames = scrapperFrame1 != null ? new[] { scrapperFrame0, scrapperFrame1 } : new[] { scrapperFrame0 };
            var guardPrefab = CreateScrapperPrefab(scrapperFrames);
            var scrapper = CreateOperator("OP_Guard_Scrapper", "Scrapper", OperatorClass.Guard, OperatorPosition.Melee, OperatorRarity.Star1,
                hp: 140, atk: 85, def: 20, res: 0, blockCount: 2, range: 2, interval: 1.1f,
                new[] { new Vector2Int(1, 0), new Vector2Int(2, 0) },
                scrapperFrame0, guardPrefab);
            scrapper.skillDescription = "<color=#FFD700>Base (1★):</color> When attacking, has a 20% chance to Stun the target for 2 seconds.\n<color=#FFD700>1st upgrade (2★):</color> Stun chance increases to 30%.\n<color=#FFD700>2nd upgrade (3★):</color> Stun chance increases to 40% and attacks on the adjacent tile trigger a shockwave dealing 50% damage to all enemies on that tile.";
            EditorUtility.SetDirty(scrapper);

            Sprite[] echosquireIdleFrames = LoadSpriteFrames(
                "Echosquire_Idle_Aligned_64x32.png",
                "Echosquire_Idle_Aligned_64x32",
                EchosquireSpriteAnimation.IdleFrameCount);
            Sprite[] echosquireAttackFrames = LoadSpriteFrames(
                "Echosquire_Attack_Aligned_64x32.png",
                "Echosquire_Attack_Aligned_64x32",
                EchosquireSpriteAnimation.AttackFrameCount);
            if (System.Array.Exists(echosquireIdleFrames, frame => frame == null) ||
                System.Array.Exists(echosquireAttackFrames, frame => frame == null))
            {
                Debug.LogError("Echosquire aligned idle and attack sheets must be imported and contain every frame.");
            }
            else
            {
                var echosquireFrameOne = echosquireIdleFrames[0];
                var echosquirePrefab = CreateEchosquirePrefab(echosquireIdleFrames, echosquireAttackFrames);
                var echosquire = CreateOperator(
                    "OP_Guard_Echosquire",
                    "Echosquire",
                    OperatorClass.Guard,
                    OperatorPosition.Melee,
                    OperatorRarity.Star2,
                    hp: 130,
                    atk: 55,
                    def: 18,
                    res: 0,
                    blockCount: 2,
                    range: 1,
                    interval: 1.2f,
                    new[] { new Vector2Int(1, 0) },
                    echosquireFrameOne,
                    echosquirePrefab);
                echosquire.roleTags = new[] { "Lifesteal", "Survival" };
                echosquire.skillDescription =
                    "<color=#FFD700>Base (2★):</color> ATK, DEF, and RES increase as HP falls: +1% per 1% HP missing, up to +60% at 40% HP or lower. Attack speed is not affected. Restores 20% of physical damage dealt as HP and cannot be healed by other operators.\n<color=#FFD700>1st upgrade (3★):</color> after landing a hit, has a 100% chance to dodge incoming attacks for 0.5 seconds.\n<color=#FFD700>2nd upgrade (4★):</color> the first time HP reaches 0 each wave, revives with at least 50% HP; reaching 0 again that wave is fatal.";
                EditorUtility.SetDirty(echosquire);
            }

            var qihouFrames = new Sprite[8];
            bool hasAllQihouFrames = true;
            for (int i = 0; i < qihouFrames.Length; i++)
            {
                qihouFrames[i] = LoadSprite("Qihou_Sheet.png", $"Qihou_Sheet_{i}");
                hasAllQihouFrames &= qihouFrames[i] != null;
            }

            if (!hasAllQihouFrames)
            {
                Debug.LogError("Qihou_Sheet.png must contain the imported Qihou_Sheet_0 through Qihou_Sheet_7 sprites.");
            }
            else
            {
                var qihouAttackFrames = new Sprite[QihouSpriteAnimation.AttackFrameCount];
                bool hasAllQihouAttackFrames = true;
                for (int i = 0; i < qihouAttackFrames.Length; i++)
                {
                    qihouAttackFrames[i] = LoadSprite("Qihou Attack Sheet.png", $"Qihou Attack Sheet_{i}");
                    hasAllQihouAttackFrames &= qihouAttackFrames[i] != null;
                }

                if (!hasAllQihouAttackFrames)
                {
                    Debug.LogError("Qihou Attack Sheet.png must contain the imported Qihou Attack Sheet_0 through Qihou Attack Sheet_10 sprites.");
                }
                else
                {
                    var qihouPrefab = CreateQihouPrefab(qihouFrames, qihouAttackFrames);
                    var qihou = CreateOperator(
                        "OP_Guard_Qihou",
                        "Qihou",
                        OperatorClass.Guard,
                        OperatorPosition.Melee,
                        OperatorRarity.Star1,
                        hp: 140,
                        atk: 65,
                        def: 20,
                        res: 0,
                        blockCount: 2,
                        range: 1,
                        interval: 1.1f,
                        new[] { new Vector2Int(1, 0) },
                        qihouFrames[0],
                        qihouPrefab);
                    qihou.damageType = DamageType.Physical;
                    qihou.roleTags = new[] { "Chi", "Knockback" };
                    qihou.skillDescription = "<color=#FFD700>Base (1★):</color> After every 4 landed hits, channels a green Qihou to push the struck enemy back 1 tile.\n<color=#FFD700>1st upgrade (2★):</color> triggers after every 3 landed hits.\n<color=#FFD700>2nd upgrade (3★):</color> triggers after every 2 landed hits and pushes the enemy back 2 tiles.";
                    EditorUtility.SetDirty(qihou);
                }
            }

            // 1d. Guard (Stag-ger)
            var staggerFrame0 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_0");
            var staggerFrame1 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_1");
            var staggerFrame2 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_2");
            var staggerFrame3 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_3");
            var staggerFrame4 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_4");
            var staggerFrame5 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_5");
            var staggerFrame6 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_6");
            var staggerFrame7 = LoadSprite("Stag-ger-Sheet.png", "Stag-ger-Sheet_7");
            if (staggerFrame0 != null && staggerFrame1 != null && staggerFrame2 != null && staggerFrame3 != null)
            {
                var staggerFrames = (staggerFrame4 != null && staggerFrame5 != null && staggerFrame6 != null && staggerFrame7 != null)
                    ? new[] { staggerFrame0, staggerFrame1, staggerFrame2, staggerFrame3, staggerFrame4, staggerFrame5, staggerFrame6, staggerFrame7 }
                    : new[] { staggerFrame0, staggerFrame1, staggerFrame2, staggerFrame3 };
                var staggerPrefab = CreateStaggerPrefab(staggerFrames);
                var stagger = CreateOperator(
                    "OP_Guard_Stagger",
                    "Stag-ger",
                    OperatorClass.Guard,
                    OperatorPosition.Melee,
                    OperatorRarity.Star2,
                    hp: 145,
                    atk: 75,
                    def: 22,
                    res: 0,
                    blockCount: 2,
                    range: 1,
                    interval: 1.2f,
                    new[] { new Vector2Int(1, 0) },
                    staggerFrame0,
                    staggerPrefab);
                stagger.damageType = DamageType.Physical;
                stagger.roleTags = new[] { "Multi-Target", "Brawler" };
                stagger.skillDescription =
                    "<color=#FFD700>Base (2★):</color> When attacking, strikes all enemies on his tile and in range ahead simultaneously. Has a 20% chance to dodge any attack; a successful dodge increases the next attack's damage by 50%.\n<color=#FFD700>1st upgrade (3★):</color> Block count increases by 1. Dodge chance increases to 30% and heals 5% of max HP when successfully dodging.\n<color=#FFD700>2nd upgrade (4★):</color> Dodge chance increases to 40% and heals 10% of max HP when killing an enemy. Every enemy that comes to the tile before or to the sides of his front tile range gets pulled into it and stopped for 1 second (except tanks and flying enemies).";
                EditorUtility.SetDirty(stagger);
            }

            // 1e. Guard (Lockburn)
            // Idle loop: 8 frames (Lockburn-Sheet_0 .. _7)
            var lockburnFramesList = new System.Collections.Generic.List<Sprite>();
            for (int lockburnIndex = 0; lockburnIndex < 8; lockburnIndex++)
            {
                var lockburnSprite = LoadSprite("Lockburn-Sheet.png", $"Lockburn-Sheet_{lockburnIndex}") ?? LoadSprite("Lockburn-Sheet.png", $"Lockburn_{lockburnIndex}");
                if (lockburnSprite != null) lockburnFramesList.Add(lockburnSprite);
            }
            if (lockburnFramesList.Count == 8)
            {
                var lockburnFrames = lockburnFramesList.ToArray();
                var lockburnPrefab = CreateLockburnPrefab(lockburnFrames);
                var lockburn = CreateOperator(
                    "OP_Guard_Lockburn",
                    "Lockburn",
                    OperatorClass.Guard,
                    OperatorPosition.Melee,
                    OperatorRarity.Star2,
                    hp: 115,
                    atk: 60,
                    def: 18,
                    res: 10,
                    blockCount: 1,
                    range: 2,
                    interval: 1.2f,
                    new[]
                    {
                        new Vector2Int(1, 0),
                        new Vector2Int(1, 1),
                        new Vector2Int(1, -1),
                        new Vector2Int(2, 0)
                    },
                    lockburnFrames[0],
                    lockburnPrefab);
                lockburn.damageType = DamageType.Arts;
                lockburn.roleTags = new[] { "Arts", "Melee", "Anti-Air" };
                lockburn.skillDescription =
                    "<color=#FFD700>Base (2★):</color> Reduces attack interval when not blocking, and increases ATK by 25% when blocking. Deals Physical damage with a melee slash to enemies on his tile, and Arts damage with ranged attacks to all forward tiles in range.\n<color=#FFD700>1st upgrade (3★):</color> Extends attack range by 1.\n<color=#FFD700>2nd upgrade (4★):</color> Both buffs are now applied and deals physical/arts damage based on target's DEF/RES.";
                EditorUtility.SetDirty(lockburn);
            }

            // 2. Defender (IRONHOWL MK-IX)
            var defenderSprite = LoadSprite("tex_op_defender.png");
            var defenderPrefab = CreateOrGetOperatorPrefab("Prefab_OP_Ironhowl", defenderSprite, OperatorClass.Defender);
            var ironhowl = CreateOperator("OP_Defender_Ironhowl", "IRONHOWL MK-IX", OperatorClass.Defender, OperatorPosition.Melee, OperatorRarity.Star1,
                hp: 280, atk: 35, def: 45, res: 10, blockCount: 4, range: 1, interval: 1.4f,
                new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) },
                defenderSprite, defenderPrefab);
            ironhowl.skillDescription = IronhowlSkillDescription;
            EditorUtility.SetDirty(ironhowl);

            // 2b. Defender (Mossmo)
            var mossmoFrameOne = LoadSprite("Mossmo-Sheet.png", "Mossmo-Sheet_0");
            var mossmoFrameTwo = LoadSprite("Mossmo-Sheet.png", "Mossmo-Sheet_1");
            if (mossmoFrameOne != null && mossmoFrameTwo != null)
            {
                var mossmoPrefab = CreateMossmoPrefab(mossmoFrameOne, mossmoFrameTwo);
                var mossmo = CreateOperator("OP_Defender_Mossmo", "Mossmo", OperatorClass.Defender, OperatorPosition.Melee, OperatorRarity.Star3,
                    hp: 160, atk: 10, def: 30, res: 5, blockCount: 3, range: 1, interval: 1.4f,
                    new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0), new Vector2Int(-1, 0),
                        new Vector2Int(0, 1), new Vector2Int(0, -1)
                    },
                    mossmoFrameOne, mossmoPrefab);
                mossmo.roleTags = new[] { "Defense", "Support" };
                mossmo.skillDescription =
                    "<color=#FFD700>Base (3★):</color> Does not attack and taunts nearby ranged enemies. When taking damage, heals up to 2 operators in range (including himself). If a blocked enemy is stalled for 20s, the enemy becomes Enraged: gains +35% ATK, but loses 5% max HP per second until defeated.\n<color=#FFD700>1st upgrade (4★):</color> Automatically heals operators in range if their HP is below 50%, and expands taunt range by 1 additional tile radius.\n<color=#FFD700>2nd upgrade (5★):</color> Expands healing range and reduces Enraged enemy damage by 10%.";
                EditorUtility.SetDirty(mossmo);
            }

            // 2c. Defender (Thornchin)
            var thornchinFrame0 = LoadSprite("Thornchin-Sheet.png", "Thornchin-Sheet_0");
            var thornchinFrame1 = LoadSprite("Thornchin-Sheet.png", "Thornchin-Sheet_1");
            var thornchinFrame2 = LoadSprite("Thornchin-Sheet.png", "Thornchin-Sheet_2");
            var thornchinFrame3 = LoadSprite("Thornchin-Sheet.png", "Thornchin-Sheet_3");
            if (thornchinFrame0 != null && thornchinFrame1 != null && thornchinFrame2 != null && thornchinFrame3 != null)
            {
                var thornchinFrames = new[] { thornchinFrame0, thornchinFrame1, thornchinFrame2, thornchinFrame3 };
                var thornchinPrefab = CreateThornchinPrefab(thornchinFrames);
                var thornchin = CreateOperator("OP_Defender_Thornchin", "Thornchin", OperatorClass.Defender, OperatorPosition.Melee, OperatorRarity.Star2,
                    hp: 150, atk: 40, def: 36, res: 5, blockCount: 3, range: 0, interval: 1.4f,
                    new[] { new Vector2Int(0, 0) },
                    thornchinFrame0, thornchinPrefab);
                thornchin.roleTags = new[] { "Defense", "Retaliation" };
                thornchin.skillDescription = ThornchinSkillDescription;
                EditorUtility.SetDirty(thornchin);
            }

            // 3. Sniper (Deadeye)
            var sniperSprite = LoadSprite("Deadeye-Sheet.png", "Deadeye-Sheet_0") ?? LoadSprite("tex_op_sniper.png");
            var sniperPrefab = CreateOrGetOperatorPrefab("Prefab_OP_Deadeye", sniperSprite, OperatorClass.Sniper);
            var deadeye = CreateOperator("OP_Sniper_Deadeye", "Deadeye", OperatorClass.Sniper, OperatorPosition.Ranged, OperatorRarity.Star1,
                hp: 85, atk: 90, def: 8, res: 0, blockCount: 0, range: 3, interval: 1.0f,
                new[]
                {
                    new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0),
                    new Vector2Int(1, 1), new Vector2Int(2, 1),
                    new Vector2Int(1, -1), new Vector2Int(2, -1)
                },
                sniperSprite, sniperPrefab);
            deadeye.skillDescription = DeadeyeSkillDescription;
            EditorUtility.SetDirty(deadeye);

            // 3a. Sniper (Pompoth)
            var pompothFrame0 = LoadSprite("Pompoth-Sheet.png", "Pompoth-Sheet_0");
            if (pompothFrame0 != null)
            {
                var pompothPrefab = CreateOrGetOperatorPrefab("Prefab_OP_Pompoth", pompothFrame0, OperatorClass.Sniper);
                var pompoth = CreateOperator("OP_Sniper_Pompoth", "Pompoth", OperatorClass.Sniper, OperatorPosition.Ranged, OperatorRarity.Star1,
                    hp: 85, atk: 95, def: 8, res: 0, blockCount: 0, range: 3, interval: 0.6f,
                    new[]
                    {
                        new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0),
                        new Vector2Int(1, 1), new Vector2Int(2, 1),
                        new Vector2Int(1, -1), new Vector2Int(2, -1),
                        new Vector2Int(3, -1), new Vector2Int(3, 1)
                    },
                    pompothFrame0, pompothPrefab);
                pompoth.skillDescription = PompothSkillDescription;
                EditorUtility.SetDirty(pompoth);
            }

            // 3b. Sniper (Basurocket)
            var basuFrame0 = LoadSprite("Basurocket-sheet.png", "Basurocket-sheet_0");
            var basuFrame1 = LoadSprite("Basurocket-sheet.png", "Basurocket-sheet_1");
            var basuFrame2 = LoadSprite("Basurocket-sheet.png", "Basurocket-sheet_2");
            var basuFrame3 = LoadSprite("Basurocket-sheet.png", "Basurocket-sheet_3");
            if (basuFrame0 != null && basuFrame1 != null && basuFrame2 != null && basuFrame3 != null)
            {
                var basuFrames = new[] { basuFrame0, basuFrame1, basuFrame2, basuFrame3 };
                var basuPrefab = CreateBasurocketPrefab(basuFrames);
                var basurocket = CreateOperator("OP_Sniper_Basurocket", "Basurocket", OperatorClass.Sniper, OperatorPosition.Ranged, OperatorRarity.Star3,
                    hp: 50, atk: 175, def: 5, res: 0, blockCount: 0, range: 7, interval: 2.8f,
                    new[]
                    {
                        new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0),
                        new Vector2Int(5, 0), new Vector2Int(6, 0), new Vector2Int(7, 0)
                    },
                    basuFrame0, basuPrefab);
                basurocket.roleTags = new[] { "Long Range", "AoE", "Explosive", "DPS" };
                basurocket.skillDescription =
                    "<color=#FFD700>Base (3★):</color> Long-range explosive sniper. Fires up to 7 tiles forward, cannot attack the adjacent tile, and deals high physical area damage on impact.\n<color=#FFD700>1st upgrade (4★):</color> Fires every 2.3 seconds (was 2.8) and reaches 1 tile further, up to 8 tiles forward.\n<color=#FFD700>2nd upgrade (5★):</color> After the blast, 8 cluster bombs explode at once on and around it, each dealing 30% of the main blast's damage in its own small circle. An enemy can take one bomb on top of the main blast.";
                EditorUtility.SetDirty(basurocket);
            }

            // 3c. Sniper (Oquloct / Oquloct)
            var oquloct = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Sniper_Oquloct.asset");
            if (oquloct != null)
            {
                oquloct.attackInterval = 2.3f;
                oquloct.skillDescription = "<color=#FFD700>Base (2★):</color> Octo-Scatter ignores enemy and stage miss mechanics. When attacked by an enemy, gains Camouflage for 5 seconds and cannot be targeted.\n<color=#FFD700>1st upgrade (3★):</color> On entering Camouflage, immediately fires a shotgun attack at enemies in range for 50% damage without ending Camouflage. Heals 25% of max HP over its duration. When Camouflage ends, gains +50% DEF for 3 seconds.\n<color=#FFD700>2nd upgrade (4★):</color> Range expands from the original 3-tile fan to a cone of 8 tiles, adding 5 tiles in the next forward column.";
                EditorUtility.SetDirty(oquloct);
            }

            // 4. Caster (Mechonk)
            var casterSprite = LoadSprite("Mechonk-Sheet.png", "Mechonk-Sheet_0") ?? LoadSprite("tex_op_caster.png");
            var casterPrefab = CreateOrGetOperatorPrefab("Prefab_OP_Mechonk", casterSprite, OperatorClass.Caster);
            var caster = CreateOperator("OP_Caster_Mechonk", "Mechonk", OperatorClass.Caster, OperatorPosition.Ranged, OperatorRarity.Star1,
                hp: 95, atk: 80, def: 10, res: 20, blockCount: 0, range: 2, interval: 1.6f,
                new[]
                {
                    new Vector2Int(1, 0), new Vector2Int(2, 0),
                    new Vector2Int(0, 1), new Vector2Int(1, 1),
                    new Vector2Int(0, -1), new Vector2Int(1, -1)
                },
                casterSprite, casterPrefab);
            caster.damageType = DamageType.Arts;
            caster.skillDescription = "<color=#FFD700>Base (1★):</color> Gains 3% ATK per enemy in range (up to 5 stacks) and deals AoE Arts damage.\n<color=#FFD700>1st upgrade (2★):</color> adds two tiles beside the furthest tile of his range.\n<color=#FFD700>2nd upgrade (3★):</color> each Arts AoE hit deals damage twice.";
            EditorUtility.SetDirty(caster);

            var nekhrost = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Caster_Nekhrost.asset");
            if (nekhrost == null)
            {
                Debug.LogError("OP_Caster_Nekhrost.asset was not found; Nekhrost's skill description could not be generated.");
            }
            else
            {
                nekhrost.skillDescription = NekhrostSkillDescription;
                EditorUtility.SetDirty(nekhrost);
            }

            // 4b. Caster (Resicochet)
            var resicochetFrame0 = LoadSprite("Resicochet-Sheet.png", "Resicochet_0");
            var resicochetFrame1 = LoadSprite("Resicochet-Sheet.png", "Resicochet_1");
            var resicochetFrame2 = LoadSprite("Resicochet-Sheet.png", "Resicochet_2");
            var resicochetFrame3 = LoadSprite("Resicochet-Sheet.png", "Resicochet_3");
            var resicochetFrame4 = LoadSprite("Resicochet-Sheet.png", "Resicochet_4");
            var resicochetFrame5 = LoadSprite("Resicochet-Sheet.png", "Resicochet_5");
            var resicochetFrame6 = LoadSprite("Resicochet-Sheet.png", "Resicochet_6");
            var resicochetFrame7 = LoadSprite("Resicochet-Sheet.png", "Resicochet_7");
            var resicochetFrame8 = LoadSprite("Resicochet-Sheet.png", "Resicochet_8");
            var resicochetFrame9 = LoadSprite("Resicochet-Sheet.png", "Resicochet_9");
            var resicochetFrame10 = LoadSprite("Resicochet-Sheet.png", "Resicochet_10");
            var resicochetFrame11 = LoadSprite("Resicochet-Sheet.png", "Resicochet_11");

            var resicochetC1 = new[] { resicochetFrame0, resicochetFrame1, resicochetFrame2, resicochetFrame3 };
            var resicochetC2 = new[] { resicochetFrame4, resicochetFrame5, resicochetFrame6, resicochetFrame7 };
            var resicochetC3 = new[] { resicochetFrame8, resicochetFrame9, resicochetFrame10, resicochetFrame11 };

            var resicochetPrefab = CreateResicochetPrefab(resicochetC1, resicochetC2, resicochetC3);
            var resicochet = CreateOperator("OP_Caster_Resicochet", "Resicochet", OperatorClass.Caster, OperatorPosition.Ranged, OperatorRarity.Star3,
                hp: 90, atk: 150, def: 40, res: 0, blockCount: 0, range: 2, interval: 1.5f,
                new[]
                {
                    new Vector2Int(1, 0), new Vector2Int(2, 0),
                    new Vector2Int(0, 1), new Vector2Int(1, 1),
                    new Vector2Int(0, -1), new Vector2Int(1, -1)
                },
                resicochetFrame0, resicochetPrefab);
            resicochet.damageType = DamageType.Arts;
            resicochet.roleTags = new[] { "Arts", "Bounce", "Burst" };
            resicochet.skillDescription = ResicochetSkillDescription;
            EditorUtility.SetDirty(resicochet);

            // 5. Medic (Coalesce)
            var coalesceFrame0 = LoadSprite("Coalesce-Sheet.png", "Coalesce-Sheet_0");
            var coalesceFrame1 = LoadSprite("Coalesce-Sheet.png", "Coalesce-Sheet_1");
            var coalesceFrame2 = LoadSprite("Coalesce-Sheet.png", "Coalesce-Sheet_2");
            var coalesceFrame3 = LoadSprite("Coalesce-Sheet.png", "Coalesce-Sheet_3");
            var coalesceFrames = (coalesceFrame0 != null && coalesceFrame1 != null && coalesceFrame2 != null && coalesceFrame3 != null)
                ? new[] { coalesceFrame0, coalesceFrame1, coalesceFrame2, coalesceFrame3 }
                : (coalesceFrame0 != null ? new[] { coalesceFrame0 } : null);
            var coalescePrefab = CreateCoalescePrefab(coalesceFrames);
            var coalesce = CreateOperator("OP_Medic_Coalesce", "Coalesce", OperatorClass.Medic, OperatorPosition.Ranged, OperatorRarity.Star1,
                hp: 90, atk: 55, def: 12, res: 15, blockCount: 0, range: 2, interval: 1.8f,
                new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(1, 0), new Vector2Int(-1, 0),
                    new Vector2Int(0, 1), new Vector2Int(0, -1),
                    new Vector2Int(1, 1), new Vector2Int(-1, -1)
                },
                coalesceFrame0 ?? LoadSprite("tex_op_medic.png"), coalescePrefab);
            coalesce.skillDescription = "<color=#FFD700>Base (1★):</color> Healing an operator with HP below 30% has a 20% chance to trigger an extra healing.\n<color=#FFD700>1st upgrade (2★):</color> Extra healing chance increases to 40%.\n<color=#FFD700>2nd upgrade (3★):</color> HP requirement increases to below 60% and increases the Max HP of operators in range by 10%.";
            EditorUtility.SetDirty(coalesce);

            // 5b. Medic (Bubblets)
            var bubbletsFrameOne = LoadSprite("Bubblets-Sheet.png", "Bubblets-Sheet_0");
            var bubbletsFrameTwo = LoadSprite("Bubblets-Sheet.png", "Bubblets-Sheet_1");
            if (bubbletsFrameOne != null && bubbletsFrameTwo != null)
            {
                var bubbletsPrefab = CreateBubbletsPrefab(bubbletsFrameOne, bubbletsFrameTwo);
                var bubblets = CreateOperator("OP_Medic_Bubblets", "Bubblets", OperatorClass.Medic, OperatorPosition.Ranged, OperatorRarity.Star3,
                    hp: 85, atk: 45, def: 10, res: 10, blockCount: 0, range: 1, interval: 2.0f,
                    new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0), new Vector2Int(-1, 0),
                        new Vector2Int(0, 1), new Vector2Int(0, -1),
                        new Vector2Int(1, 1), new Vector2Int(-1, 1),
                        new Vector2Int(1, -1), new Vector2Int(-1, -1)
                    },
                    bubbletsFrameOne, bubbletsPrefab);
                bubblets.damageType = DamageType.Arts;
                bubblets.roleTags = new[] { "Shield", "Support" };
                bubblets.skillDescription = BubbletsSkillDescription;
                EditorUtility.SetDirty(bubblets);
            }

            // 5c. Medic (Progeny)
            var progenyFrame0 = LoadSprite("Progeny-Sheet.png", "Progeny-Sheet_0");
            var progenyFrame1 = LoadSprite("Progeny-Sheet.png", "Progeny-Sheet_1");
            var progenyFrame2 = LoadSprite("Progeny-Sheet.png", "Progeny-Sheet_2");
            var progenyFrame3 = LoadSprite("Progeny-Sheet.png", "Progeny-Sheet_3");
            if (progenyFrame0 != null && progenyFrame1 != null && progenyFrame2 != null && progenyFrame3 != null)
            {
                var progenyFrames = new[] { progenyFrame0, progenyFrame1, progenyFrame2, progenyFrame3 };
                var progenyPrefab = CreateProgenyPrefab(progenyFrames);
                var progeny = CreateOperator("OP_Medic_Progeny", "Progeny", OperatorClass.Medic, OperatorPosition.Ranged, OperatorRarity.Star2,
                    hp: 95, atk: 35, def: 12, res: 12, blockCount: 0, range: 1, interval: 1.8f,
                    new[]
                    {
                        new Vector2Int(0, 0),
                        new Vector2Int(1, 0), new Vector2Int(-1, 0),
                        new Vector2Int(0, 1), new Vector2Int(0, -1)
                    },
                    progenyFrame0, progenyPrefab);
                progeny.roleTags = new[] { "Chain Heal", "Support" };
                progeny.skillDescription =
                    "<color=#FFD700>Base (2★):</color> Heals bounce between 2 operators (healing reduced by 50% when it bounces).\n<color=#FFD700>1st upgrade (3★):</color> Additional 1 bounce, and increases range by 1 tile at every adjacent tile.\n<color=#FFD700>2nd upgrade (4★):</color> Healing bounces are no longer reduced.";
                EditorUtility.SetDirty(progeny);
            }

            // 5d. Medic (Panacea)
            var panacea = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Medic_Panacea.asset");
            if (panacea != null)
            {
                panacea.skillDescription = PanaceaSkillDescription;
                EditorUtility.SetDirty(panacea);
            }

            // 6. Specialist (Framiliar)
            var framiliarFrame0 = LoadSprite("Framiliar-Sheet.png", "Framiliar-Sheet_0");
            var framiliarFrame1 = LoadSprite("Framiliar-Sheet.png", "Framiliar-Sheet_1");
            var framiliarFrame2 = LoadSprite("Framiliar-Sheet.png", "Framiliar-Sheet_2");
            var framiliarFrame3 = LoadSprite("Framiliar-Sheet.png", "Framiliar-Sheet_3");
            var framiliarFrames = (framiliarFrame0 != null && framiliarFrame1 != null && framiliarFrame2 != null && framiliarFrame3 != null)
                ? new[] { framiliarFrame0, framiliarFrame1, framiliarFrame2, framiliarFrame3 }
                : (framiliarFrame0 != null ? new[] { framiliarFrame0 } : null);

            var framiliarPrefab = CreateFramiliarPrefab(framiliarFrames);
            var framiliar = CreateOperator("OP_Specialist_Framiliar", "Framiliar", OperatorClass.Specialist, OperatorPosition.Any, OperatorRarity.Star2,
                hp: 100, atk: 50, def: 10, res: 0, blockCount: 1, range: 1, interval: 1.2f,
                new[]
                {
                    new Vector2Int(1, 0), new Vector2Int(0, 0)
                },
                framiliarFrame0, framiliarPrefab);
            framiliar.damageType = DamageType.Physical;
            framiliar.roleTags = new[] { "Specialist", "Mimic", "Deck" };
            framiliar.skillDescription = "<color=#FFD700>Base (2★):</color> Copies any operator in your deck, taking on their form in grayscale. When retreated in the same preparation phase, returns as Framiliar; if retreated in the next prep phase, returns as the copied operator. Specialists have no retreat cooldown.\n<color=#FFD700>1st upgrade (3★):</color> Copies any operator in your deck as 1x Upgraded (or their upgrade level if higher).\n<color=#FFD700>2nd upgrade (4★):</color> Copies any operator in your deck as 2x Upgraded.";
            EditorUtility.SetDirty(framiliar);

            var drawgoo = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Specialist_Drawgoo.asset");
            if (drawgoo != null)
            {
                drawgoo.skillDescription = DrawgooSkillDescription;
                EditorUtility.SetDirty(drawgoo);
            }

            var flurmonica = AssetDatabase.LoadAssetAtPath<OperatorData>($"{OperatorDataFolder}/OP_Specialist_Flurmonica.asset");
            if (flurmonica != null)
            {
                flurmonica.skillDescription = FlurmonicaSkillDescription;
                EditorUtility.SetDirty(flurmonica);
            }
        }
    }
}
