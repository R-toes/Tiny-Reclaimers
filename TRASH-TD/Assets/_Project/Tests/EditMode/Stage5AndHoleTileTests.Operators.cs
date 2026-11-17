using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.GameLoop;
using TrashTD.Core.Grid;
using TrashTD.Core.Pathfinding;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Tests
{
    // Operator tests: stats, true damage, IRONHOWL MK-IX, Drawgoo and Mossmo.
    public partial class Stage5AndHoleTileTests
    {
        [Test]
        public void OperatorStats_MossmoAndThornchin_HaveUpdatedHP()
        {
            var mossmo = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Defender_Mossmo.asset");
            var thornchin = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Defender_Thornchin.asset");

            if (mossmo != null)
            {
                Assert.AreEqual(160, mossmo.baseHP, "Mossmo base HP should be 160.");
            }

            if (thornchin != null)
            {
                Assert.AreEqual(150, thornchin.baseHP, "Thornchin base HP should be 150.");
            }
        }

        [Test]
        public void Stage5CarEncounter_DoesNotSpawnCarsOnFirst5Waves()
        {
            var encounterObj = new GameObject("TestCarEncounter");
            var encounter = encounterObj.AddComponent<Stage5CarEncounter>();

            Assert.AreEqual(6, Stage5CarEncounter.FirstCarWave, "First car wave should be Wave 6.");

            for (int wave = 1; wave <= 5; wave++)
            {
                Assert.IsFalse(encounter.CanSpawnCarsOnWave(wave), $"Car encounter should NOT spawn cars on wave {wave}.");
            }

            Assert.IsTrue(encounter.CanSpawnCarsOnWave(6), "Car encounter SHOULD spawn cars on wave 6.");
            Assert.IsTrue(encounter.CanSpawnCarsOnWave(10), "Car encounter SHOULD spawn cars on wave 10.");

            Object.DestroyImmediate(encounterObj);
        }

        [Test]
        public void DamageCalculator_TrueDamage_BypassesMitigation()
        {
            int damage = Combat.DamageCalculator.CalculateDamage(3, def: 999, res: 999, DamageType.True);
            Assert.AreEqual(3, damage, "True damage must ignore both DEF and RES completely.");

            int trapDamage = Combat.DamageCalculator.CalculateDamage(AcidTileRules.DamagePerTick, def: 50, res: 50, DamageType.True);
            Assert.AreEqual(AcidTileRules.DamagePerTick, trapDamage, "Acid/trap damage with True damage type must deal full unmitigated damage.");
        }

        [Test]
        public void OperatorAndEnemy_TakeTrueDamage_BypassesDEF()
        {
            // Operator with high DEF (50 DEF)
            var opObj = new GameObject("DefenderOp");
            var op = opObj.AddComponent<IronhowlOperator>();
            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.baseHP = 100;
            opData.baseDEF = 50;
            opData.baseRES = 50;
            op.Initialize(opData, OperatorRarity.Star1);

            // True damage via TakeTrueDamage: should lose exactly 3 HP, not chip damage (1)
            op.TakeTrueDamage(3);
            Assert.AreEqual(97, op.CurrentHP, "Operator should take unmitigated true damage from TakeTrueDamage.");

            // True damage via TakeDamage: should also lose exactly 3 HP
            op.TakeDamage(3, DamageType.True);
            Assert.AreEqual(94, op.CurrentHP, "Operator should take unmitigated true damage from TakeDamage(..., DamageType.True).");

            // Enemy with high DEF (50 DEF)
            var enemyObj = new GameObject("TestEnemy");
            var enemy = enemyObj.AddComponent<GruntEnemy>();
            var enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.baseHP = 100;
            enemyData.baseDEF = 50;
            enemyData.baseRES = 50;
            enemy.Initialize(enemyData, 0);

            enemy.TakeTrueDamage(3);
            Assert.AreEqual(97, enemy.CurrentHP, "Enemy should take unmitigated true damage from TakeTrueDamage.");

            enemy.TakeDamage(3, DamageType.True);
            Assert.AreEqual(94, enemy.CurrentHP, "Enemy should take unmitigated true damage from TakeDamage(..., DamageType.True).");

            Object.DestroyImmediate(opObj);
            Object.DestroyImmediate(opData);
            Object.DestroyImmediate(enemyObj);
            Object.DestroyImmediate(enemyData);
        }

        [Test]
        public void Ironhowl_Block4_And_FrenzyIncreasesAtkAndDef()
        {
            var ironhowlData = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Defender_Ironhowl.asset");
            if (ironhowlData != null)
            {
                Assert.AreEqual(4, ironhowlData.blockCount, "IRONHOWL MK-IX block count must be 4 in asset.");
                Assert.IsTrue(ironhowlData.skillDescription.Contains("blocking 3 or more enemies"), "IRONHOWL MK-IX skill description should mention 3 or more enemies.");
                StringAssert.Contains("1st upgrade (2\u2605)", ironhowlData.skillDescription);
                StringAssert.Contains("Every 8 enemy hits", ironhowlData.skillDescription);
                StringAssert.Contains("2nd upgrade (3\u2605)", ironhowlData.skillDescription);
                StringAssert.Contains("Block count increases to 6", ironhowlData.skillDescription);
            }

            var opObj = new GameObject("IronhowlTest");
            var op = opObj.AddComponent<IronhowlOperator>();
            var data = ScriptableObject.CreateInstance<OperatorData>();
            data.operatorName = "IRONHOWL MK-IX";
            data.baseHP = 200;
            data.baseATK = 50;
            data.baseDEF = 40;
            data.blockCount = 4;
            data.operatorClass = OperatorClass.Defender;
            op.Initialize(data, OperatorRarity.Star1);

            Assert.AreEqual(4, op.GetBlockCount(), "IRONHOWL MK-IX block capacity must be 4.");

            // Before blocking 3: ATK is 50, DEF is 40
            Assert.AreEqual(50, op.CurrentATK);
            Assert.AreEqual(40, op.CurrentDEF);

            // Create 4 enemies to block
            var enemies = new List<EnemyBase>();
            for (int i = 0; i < 4; i++)
            {
                var eObj = new GameObject($"Enemy_{i}");
                var e = eObj.AddComponent<GruntEnemy>();
                var eData = ScriptableObject.CreateInstance<EnemyData>();
                eData.baseHP = 100;
                e.Initialize(eData, 0);
                enemies.Add(e);
            }

            // Block 1
            op.Deploy(new GridCell(0, 0, TileType.LowGround, Vector3.zero));
            op.TryBlock(enemies[0]);
            Assert.IsFalse(op.IsFrenzyActive);
            Assert.AreEqual(50, op.CurrentATK);
            Assert.AreEqual(40, op.CurrentDEF);

            // Block 2
            op.TryBlock(enemies[1]);
            Assert.IsFalse(op.IsFrenzyActive);
            Assert.AreEqual(50, op.CurrentATK);
            Assert.AreEqual(40, op.CurrentDEF);

            // Block 3 -> Frenzy triggers: ATK * 1.20 = 60, DEF * 1.20 = 48
            op.TryBlock(enemies[2]);
            Assert.IsTrue(op.IsFrenzyActive);
            Assert.AreEqual(60, op.CurrentATK, "ATK should increase by 20% (50 -> 60).");
            Assert.AreEqual(48, op.CurrentDEF, "DEF should increase by 20% (40 -> 48).");

            // Block 4 -> Still frenzy
            op.TryBlock(enemies[3]);
            Assert.IsTrue(op.IsFrenzyActive);
            Assert.AreEqual(60, op.CurrentATK);
            Assert.AreEqual(48, op.CurrentDEF);

            // Cleanup
            for (int i = 0; i < enemies.Count; i++)
            {
                Object.DestroyImmediate(enemies[i].Data);
                Object.DestroyImmediate(enemies[i].gameObject);
            }
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(opObj);
        }

        [Test]
        public void Ironhowl_SecondUpgrade_IncreasesBlockCountToSix()
        {
            var opObj = new GameObject("IronhowlUpgradeTest");
            var op = opObj.AddComponent<IronhowlOperator>();
            var data = ScriptableObject.CreateInstance<OperatorData>();
            data.operatorName = "IRONHOWL MK-IX";
            data.baseHP = 200;
            data.baseATK = 50;
            data.baseDEF = 40;
            data.blockCount = 4;
            data.operatorClass = OperatorClass.Defender;

            op.Initialize(data, OperatorRarity.Star2);
            Assert.AreEqual(4, op.GetBlockCount(), "IRONHOWL MK-IX should retain its base block count before the second upgrade.");

            op.Initialize(data, OperatorRarity.Star3);
            Assert.AreEqual(6, op.GetBlockCount(), "IRONHOWL MK-IX's second upgrade should add two block slots.");
            op.ApplyStun(1f);
            Assert.AreEqual(0, op.GetBlockCount(), "A stunned operator must still have no block capacity.");

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(opObj);
        }

        [Test]
        public void Ironhowl_FirstUpgrade_ShockwaveStunsOnlyCurrentBlockedEnemiesEveryEightEnemyHits()
        {
            var opObj = new GameObject("IronhowlShockwaveTest");
            var op = opObj.AddComponent<IronhowlOperator>();
            var data = ScriptableObject.CreateInstance<OperatorData>();
            data.operatorName = "IRONHOWL MK-IX";
            data.baseHP = 1000;
            data.baseATK = 50;
            data.baseDEF = 0;
            data.blockCount = 4;
            data.operatorClass = OperatorClass.Defender;
            op.Initialize(data, OperatorRarity.Star2);
            op.Deploy(new GridCell(0, 0, TileType.LowGround, Vector3.zero));

            var blockedTwoObject = new GameObject("SecondBlockedEnemy");
            blockedTwoObject.AddComponent<SpriteRenderer>();
            var blockedTwo = blockedTwoObject.AddComponent<GruntEnemy>();
            blockedTwo.Initialize(enemyData, 0);

            var unblockedObject = new GameObject("UnblockedEnemy");
            unblockedObject.AddComponent<SpriteRenderer>();
            var unblocked = unblockedObject.AddComponent<GruntEnemy>();
            unblocked.Initialize(enemyData, 0);

            Assert.IsTrue(op.TryBlock(enemy));
            Assert.IsTrue(op.TryBlock(blockedTwo));

            for (int i = 0; i < 7; i++)
                op.TakeDamage(10, DamageType.Physical);
            Assert.AreEqual(0, op.HitsSinceShockwave, "Non-enemy damage should not count toward the shockwave.");

            for (int i = 0; i < 7; i++)
                op.TakeDamage(10, DamageType.Physical, enemy);
            Assert.IsFalse(enemy.IsStunned, "The shockwave should not trigger before the eighth enemy hit.");
            Assert.IsFalse(blockedTwo.IsStunned);

            op.TakeDamage(10, DamageType.Physical, enemy);
            Assert.IsTrue(enemy.IsStunned, "The eighth enemy hit should stun the first blocked enemy.");
            Assert.IsTrue(blockedTwo.IsStunned, "The shockwave should stun every enemy currently blocked.");
            Assert.IsFalse(unblocked.IsStunned, "The shockwave should not stun enemies IRONHOWL MK-IX is not blocking.");
            Assert.AreEqual(0, op.HitsSinceShockwave, "The hit counter should reset after the shockwave.");
            Assert.AreEqual(1.5f, IronhowlOperator.ShockwaveStunDuration, "The shockwave should stun for 1.5 seconds.");

            var visual = GameObject.Find("AoeBlastVisual");
            if (visual != null) Object.DestroyImmediate(visual);
            Object.DestroyImmediate(unblockedObject);
            Object.DestroyImmediate(blockedTwoObject);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(opObj);
        }

        [Test]
        public void Ironhowl_Prefab_Has8AnimationFrames()
        {
            var ironhowlPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Operators/Prefab_OP_Ironhowl.prefab");
            if (ironhowlPrefab != null)
            {
                var anim = ironhowlPrefab.GetComponent<IronhowlSpriteAnimation>();
                Assert.IsNotNull(anim, "IRONHOWL MK-IX prefab must have IronhowlSpriteAnimation component.");
                Assert.IsNotNull(anim.Frames, "IRONHOWL MK-IX animation frames must not be null.");
                Assert.AreEqual(8, anim.Frames.Length, "IRONHOWL MK-IX animation must have 8 frames.");
                Assert.IsNotNull(anim.AttackFrames, "IRONHOWL MK-IX attack frames must not be null.");
                Assert.AreEqual(10, anim.AttackFrames.Length, "IRONHOWL MK-IX attack animation must have 10 frames.");
                Assert.IsTrue(anim.HasAttackAnimation, "IRONHOWL MK-IX attack animation must be available.");
                Assert.AreEqual(0.125f, anim.FrameDuration, 0.001f, "IRONHOWL MK-IX animation frame duration should be 0.125s (8 FPS).");
                for (int i = 0; i < anim.Frames.Length; i++)
                {
                    Assert.IsNotNull(anim.Frames[i], $"IRONHOWL MK-IX frame {i} must be assigned.");
                }
                for (int i = 0; i < anim.AttackFrames.Length; i++)
                {
                    Assert.IsNotNull(anim.AttackFrames[i], $"IRONHOWL MK-IX attack frame {i} must be assigned.");
                }
            }
        }

        [Test]
        public void IronhowlSpriteAnimation_Cycles8Frames()
        {
            var animObj = new GameObject("TestIronhowlAnim");
            var sr = animObj.AddComponent<SpriteRenderer>();
            var anim = animObj.AddComponent<IronhowlSpriteAnimation>();

            var sprites = new Sprite[8];
            for (int i = 0; i < 8; i++)
            {
                sprites[i] = Sprite.Create(new Texture2D(32, 32), new Rect(0, 0, 32, 32), Vector2.one * 0.5f);
            }

            var framesField = typeof(IronhowlSpriteAnimation).GetField("frames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            framesField.SetValue(anim, sprites);
            var attackFramesField = typeof(IronhowlSpriteAnimation).GetField("attackFrames",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attackFramesField.SetValue(anim, sprites);

            TestUtility.CallAwake(anim);

            Assert.AreEqual(sprites[0], sr.sprite);
            Assert.AreEqual(8, anim.Frames.Length);
            Assert.AreEqual(0.125f, anim.FrameDuration, 0.001f);

            anim.Strike();
            Assert.AreSame(sprites[IronhowlSpriteAnimation.StrikeFrame], sr.sprite,
                "IRONHOWL MK-IX's attack frame must display as soon as its attack is triggered.");

            Object.DestroyImmediate(animObj);
            for (int i = 0; i < 8; i++)
            {
                Object.DestroyImmediate(sprites[i].texture);
                Object.DestroyImmediate(sprites[i]);
            }
        }

        [Test]
        public void Drawgoo_Prefab_Has8AnimationFrames()
        {
            var drawgooPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Operators/Prefab_OP_Drawgoo.prefab");
            if (drawgooPrefab != null)
            {
                var anim = drawgooPrefab.GetComponent<OperatorSpriteAnimation>();
                Assert.IsNotNull(anim, "Drawgoo prefab must have OperatorSpriteAnimation component.");
                Assert.IsNotNull(anim.Frames, "Drawgoo animation frames must not be null.");
                Assert.AreEqual(8, anim.Frames.Length, "Drawgoo animation must have 8 frames.");
                Assert.AreEqual(0.125f, anim.FrameDuration, 0.001f, "Drawgoo animation frame duration should be 0.125s (8 FPS).");
                for (int i = 0; i < anim.Frames.Length; i++)
                {
                    Assert.IsNotNull(anim.Frames[i], $"Drawgoo frame {i} must be assigned.");
                }
            }

            var drawgooData = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Specialist_Drawgoo.asset");
            if (drawgooData != null)
            {
                var frames = TrashTD.UI.UnitSpriteFrames.GetOperatorFrames(drawgooData, out float duration);
                Assert.AreEqual(8, frames.Length, "UnitSpriteFrames must resolve 8 frames for Drawgoo.");
                Assert.AreEqual(0.125f, duration, 0.001f, "UnitSpriteFrames must resolve 0.125s frame duration for Drawgoo.");
            }
        }

        [Test]
        public void Mossmo_SkillDescription_And_DoesNotAttack()
        {
            var mossmoData = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Defender_Mossmo.asset");
            if (mossmoData != null)
            {
                Assert.AreEqual("Mossmo does not attack and is likely to be attacked but when taking damage he heals up to 2 operators in its range including itself",
                    mossmoData.skillDescription);
            }

            var opObj = new GameObject("MossmoTest");
            var op = opObj.AddComponent<MossmoOperator>();
            var data = ScriptableObject.CreateInstance<OperatorData>();
            data.operatorName = "Mossmo";
            data.baseHP = 160;
            data.baseATK = 10;
            data.blockCount = 3;
            data.operatorClass = OperatorClass.Defender;
            op.Initialize(data, OperatorRarity.Star1);

            Assert.IsTrue(op.HasTaunt, "Mossmo must have HasTaunt enabled.");

            // Mossmo Attack method should do nothing
            var eObj = new GameObject("DummyEnemy");
            var e = eObj.AddComponent<GruntEnemy>();
            var eData = ScriptableObject.CreateInstance<EnemyData>();
            eData.baseHP = 100;
            e.Initialize(eData, 0);

            op.Attack(e);
            Assert.AreEqual(100, e.CurrentHP, "Mossmo must not deal damage on Attack.");

            Object.DestroyImmediate(eData);
            Object.DestroyImmediate(eObj);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(opObj);
        }

        [Test]
        public void Mossmo_TauntMechanic_RangedEnemyPrioritizesMossmoInSurroundingTile()
        {
            // Set up GridManager and OperatorManager
            var opMgrObj = new GameObject("OpManager");
            var opMgr = opMgrObj.AddComponent<OperatorManager>();
            opMgr.Initialize(gridManager);

            // Deploy Mossmo at (5, 5)
            var mossmoObj = new GameObject("Mossmo");
            mossmoObj.transform.position = gridManager.GridToWorldPosition(5, 5);
            var mossmo = mossmoObj.AddComponent<MossmoOperator>();
            var mossmoData = ScriptableObject.CreateInstance<OperatorData>();
            mossmoData.operatorName = "Mossmo";
            mossmoData.baseHP = 160;
            mossmoData.baseDEF = 30;
            mossmoData.operatorClass = OperatorClass.Defender;
            mossmoData.position = OperatorPosition.Melee;
            mossmo.Initialize(mossmoData, OperatorRarity.Star1);
            mossmo.Deploy(gridManager.GetCell(5, 5));
            opMgr.RegisterOperator(mossmo);

            // Deploy a Sniper (Ranged operator, normally preferred target) at (5, 6)
            var sniperObj = new GameObject("Sniper");
            sniperObj.transform.position = gridManager.GridToWorldPosition(5, 6);
            var sniper = sniperObj.AddComponent<DefenderOperator>();
            var sniperData = ScriptableObject.CreateInstance<OperatorData>();
            sniperData.operatorName = "Sniper";
            sniperData.baseHP = 80;
            sniperData.operatorClass = OperatorClass.Sniper;
            sniperData.position = OperatorPosition.Ranged;
            sniper.Initialize(sniperData, OperatorRarity.Star1);
            sniper.Deploy(gridManager.GetCell(5, 6));
            opMgr.RegisterOperator(sniper);

            // Create EnemyCaster with attack range 3
            var casterObj = new GameObject("EnemyCaster");
            var caster = casterObj.AddComponent<EnemyCaster>();
            var casterData = ScriptableObject.CreateInstance<EnemyData>();
            casterData.attackRange = 3;
            casterData.attackInterval = 2.0f;
            caster.Initialize(casterData, 0);
            caster.SetGridManager(gridManager);

            // 1. When EnemyCaster is in a surrounding tile of Mossmo (e.g. at (4, 5), dx=1, dy=0)
            casterObj.transform.position = gridManager.GridToWorldPosition(4, 5);
            Assert.IsTrue(mossmo.ShouldTauntAttacker(casterObj.transform.position, gridManager), "Mossmo should taunt when attacker is in surrounding tile.");

            // Use reflection to call FindOperatorTarget
            var findTargetMethod = typeof(EnemyBase).GetMethod("FindOperatorTarget",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var targetWhenSurrounding = (OperatorBase)findTargetMethod.Invoke(caster, new object[] { true });
            Assert.AreEqual(mossmo, targetWhenSurrounding, "Enemy caster should prioritize Mossmo because of taunt in surrounding tile, even if Sniper is preferred.");

            // 2. When EnemyCaster is NOT in a surrounding tile of Mossmo (e.g. at (3, 5), dx=2, dy=0, but still in attack range 3)
            casterObj.transform.position = gridManager.GridToWorldPosition(3, 5);
            Assert.IsFalse(mossmo.ShouldTauntAttacker(casterObj.transform.position, gridManager), "Mossmo should NOT taunt when attacker is beyond surrounding tiles.");

            var targetWhenFar = (OperatorBase)findTargetMethod.Invoke(caster, new object[] { true });
            Assert.AreEqual(sniper, targetWhenFar, "Enemy caster should target the preferred Ranged operator (Sniper) when outside Mossmo's surrounding tile.");

            // Clean up
            Object.DestroyImmediate(casterObj);
            Object.DestroyImmediate(casterData);
            Object.DestroyImmediate(sniperObj);
            Object.DestroyImmediate(sniperData);
            Object.DestroyImmediate(mossmoObj);
            Object.DestroyImmediate(mossmoData);
            Object.DestroyImmediate(opMgrObj);
        }

        [Test]
        public void Mossmo_Cooldowns_1sForEnemyDamage_3sForEnvironmentDamage()
        {
            var opObj = new GameObject("MossmoTest");
            var op = opObj.AddComponent<MossmoOperator>();
            var data = ScriptableObject.CreateInstance<OperatorData>();
            data.operatorName = "Mossmo";
            data.baseHP = 160;
            data.baseATK = 15;
            data.baseDEF = 0;
            data.operatorClass = OperatorClass.Defender;
            op.Initialize(data, OperatorRarity.Star1);

            // 1. Attacked by enemy -> Cooldown should be 1.0s
            var enemyObj = new GameObject("AttackingEnemy");
            var enemy = enemyObj.AddComponent<GruntEnemy>();
            var enemyData = ScriptableObject.CreateInstance<EnemyData>();
            enemyData.baseHP = 100;
            enemy.Initialize(enemyData, 0);

            // Deal 20 damage via enemy attack
            op.TakeDamage(20, DamageType.Physical, attacker: enemy);
            Assert.AreEqual(MossmoOperator.EnemyHealCooldownDuration, op.HealCooldownTimer, 0.05f, "Cooldown after enemy attack should be 1.0s.");

            // Force cooldown to expire
            typeof(MossmoOperator).GetField("healCooldownTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(op, 0f);

            // 2. Attacked by environment (TakeTrueDamage, e.g. acid rain or acid pool) -> Cooldown should be 3.0s
            op.TakeTrueDamage(3);
            Assert.AreEqual(MossmoOperator.EnvironmentHealCooldownDuration, op.HealCooldownTimer, 0.05f, "Cooldown after environmental damage should be 3.0s.");

            // Force cooldown to expire
            typeof(MossmoOperator).GetField("healCooldownTimer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(op, 0f);

            // 3. Attacked by environment via TakeDamage with null attacker -> Cooldown should be 3.0s
            op.TakeDamage(10, DamageType.Physical, attacker: null);
            Assert.AreEqual(MossmoOperator.EnvironmentHealCooldownDuration, op.HealCooldownTimer, 0.05f, "Cooldown after null attacker damage should be 3.0s.");

            Object.DestroyImmediate(enemyObj);
            Object.DestroyImmediate(enemyData);
            Object.DestroyImmediate(opObj);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Drawgoo_Asset_SkillDescription_And_UpgradeMerge_GrantsRandom3StarOnDeath()
        {
            var drawgooData = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>("Assets/_Project/Data/Operators/OP_Specialist_Drawgoo.asset");
            Assert.AreEqual(OperatorClass.Specialist, drawgooData.operatorClass, "Drawgoo should be Specialist class.");
            Assert.IsNotNull(drawgooData, "Drawgoo asset should exist.");
            Assert.IsTrue(drawgooData.skillDescription.Contains("3-star"), "Drawgoo asset description should mention 3-star upgrade.");

            var upgradeObj = new GameObject("UpgradeSystem");
            var upgradeSys = TestUtility.AddAwakened<RarityUpgradeSystem>(upgradeObj);

            var deckObj = new GameObject("Deck");
            var deck = TestUtility.AddAwakened<PlayerDeck>(deckObj);

            var opMgrObj = new GameObject("OpManager");
            var opMgr = TestUtility.AddAwakened<OperatorManager>(opMgrObj);
            opMgr.Initialize(gridManager);

            var draftObj = new GameObject("DraftSystem");
            var draftSys = TestUtility.AddAwakened<CardDraftSystem>(draftObj);

            var star3Op = ScriptableObject.CreateInstance<OperatorData>();
            star3Op.operatorName = "Mossmo";
            star3Op.operatorClass = OperatorClass.Defender;
            star3Op.baseRarity = OperatorRarity.Star3;

            var pool = new List<OperatorData> { drawgooData, star3Op };
            draftSys.SetOperatorPool(pool);

            try
            {
                // 1. Add 3 1★ Drawgoos to the deck
                deck.AddCard(new DraftCard(drawgooData, OperatorRarity.Star1));
                deck.AddCard(new DraftCard(drawgooData, OperatorRarity.Star1));
                deck.AddCard(new DraftCard(drawgooData, OperatorRarity.Star1));

                // 3 cards merge into 1 2★ card
                Assert.AreEqual(1, deck.CardCount);
                DraftCard mergedCard = deck.DeckSlots[0];
                Assert.IsNotNull(mergedCard);
                Assert.AreEqual(OperatorRarity.Star2, mergedCard.rarity);

                // 2. Deploy the 2★ Drawgoo
                Assert.IsTrue(opMgr.TryDeployOperator(mergedCard, new Vector2Int(0, 0), OperatorFacing.Right, out var deployedOp));
                deck.RemoveCard(0);
                Assert.AreEqual(OperatorRarity.Star2, deployedOp.CurrentRarity);
                Assert.AreEqual("When Drawgoo dies, draw 1 (60%) or 2 (40%) random 3-star operator cards from the pool.", deployedOp.GetSkillDescription());

                // 3. Kill the 2★ Drawgoo
                TestUtility.ExpectEditModeDestroy();
                deployedOp.TakeTrueDamage(deployedOp.MaxHP + 100);

                // Deck should now have 1 or 2 cards, and all must be the 3★ operator
                Assert.IsTrue(deck.CardCount >= 1 && deck.CardCount <= 2);
                foreach (DraftCard rewardCard in deck.DeckSlots)
                {
                    Assert.AreEqual("Mossmo", rewardCard.operatorData.operatorName);
                    Assert.AreEqual(OperatorRarity.Star3, rewardCard.rarity);
                }
            }
            finally
            {
                TestUtility.ClearSingletons();
                Object.DestroyImmediate(draftObj);
                Object.DestroyImmediate(opMgrObj);
                Object.DestroyImmediate(deckObj);
                Object.DestroyImmediate(upgradeObj);
                Object.DestroyImmediate(star3Op);
            }
        }
    }
}
