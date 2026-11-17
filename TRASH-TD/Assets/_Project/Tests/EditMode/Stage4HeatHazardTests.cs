using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using TrashTD.Combat;
using TrashTD.Core.GameLoop;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    public class Stage4HeatHazardTests
    {
        private GameObject playerDeckObj;
        private PlayerDeck deck;
        private GameObject operatorManagerObj;
        private OperatorManager operatorManager;
        private GameObject hazardObj;
        private GameObject operatorObj;
        private GameObject emberObj;
        private OperatorData operatorData;

        [SetUp]
        public void SetUp()
        {
            StageCombatModifiers.ConfigureForStage(null);
            playerDeckObj = new GameObject("PlayerDeckTest");
            deck = playerDeckObj.AddComponent<PlayerDeck>();

            operatorManagerObj = new GameObject("OperatorManagerTest");
            operatorManager = TestUtility.AddAwakened<OperatorManager>(operatorManagerObj);
            hazardObj = new GameObject("Stage4HeatHazardTest");
        }

        [TearDown]
        public void TearDown()
        {
            if (playerDeckObj != null)
            {
                Object.DestroyImmediate(playerDeckObj);
            }
            if (operatorObj != null) Object.DestroyImmediate(operatorObj);
            Stage4HeatHazard.Current = null;
            foreach (Object created in reactorTestObjects)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            reactorTestObjects.Clear();
            if (emberObj != null) Object.DestroyImmediate(emberObj);
            if (operatorData != null) Object.DestroyImmediate(operatorData);
            if (hazardObj != null) Object.DestroyImmediate(hazardObj);
            if (operatorManagerObj != null) Object.DestroyImmediate(operatorManagerObj);
            TestUtility.ClearSingletons();
            StageCombatModifiers.ConfigureForStage(null);
        }

        [Test]
        public void Stage4Configuration_ActivatesHeatHazard()
        {
            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.FactoryHeat);
            Assert.IsTrue(StageCombatModifiers.IsHeatHazardActive);

            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.Sandstorm);
            Assert.IsFalse(StageCombatModifiers.IsHeatHazardActive);
        }

        [Test]
        public void CardInDeck_HeatStacksFadeAwayOnePerRound()
        {
            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "HeatTestOp";

            var card = new DraftCard(opData, OperatorRarity.Star1, 0, 3);
            deck.AddCard(card);

            Assert.AreEqual(3, deck.DeckSlots[0].heatStacks);

            deck.AdvanceRedeployCooldownsOneRound();
            Assert.AreEqual(2, deck.DeckSlots[0].heatStacks);

            deck.AdvanceRedeployCooldownsOneRound();
            Assert.AreEqual(1, deck.DeckSlots[0].heatStacks);

            deck.AdvanceRedeployCooldownsOneRound();
            Assert.AreEqual(0, deck.DeckSlots[0].heatStacks);

            // Should not go below 0
            deck.AdvanceRedeployCooldownsOneRound();
            Assert.AreEqual(0, deck.DeckSlots[0].heatStacks);

            Object.DestroyImmediate(opData);
        }

        [Test]
        public void Operator_RetreatExtinguishesBurning()
        {
            var opObj = new GameObject("TestOperator");
            var op = opObj.AddComponent<GuardOperator>();
            var opData = ScriptableObject.CreateInstance<OperatorData>();
            opData.operatorName = "Guard";
            opData.baseRarity = OperatorRarity.Star1;
            opData.operatorClass = OperatorClass.Guard;
            opData.baseHP = 100;
            opData.baseATK = 10;
            opData.baseDEF = 0;
            opData.baseRES = 0;

            op.Initialize(opData, OperatorRarity.Star1, OperatorFacing.Right);
            op.HeatStacks = 5;
            op.IsBurning = true;

            Assert.IsTrue(op.IsBurning);

            // Retreat extinguishes burning
            op.Retreat();
            Assert.IsFalse(op.IsBurning);

            Object.DestroyImmediate(opObj);
            Object.DestroyImmediate(opData);
        }

        [Test]
        public void BurningOperator_TakesTwoTrueDamageEveryTick()
        {
            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.FactoryHeat);

            operatorData = ScriptableObject.CreateInstance<OperatorData>();
            operatorData.operatorName = "HighDefenseGuard";
            operatorData.baseHP = 100;
            operatorData.baseDEF = 100;

            operatorObj = new GameObject("BurningOperator");
            GuardOperator op = operatorObj.AddComponent<GuardOperator>();
            op.Initialize(operatorData, OperatorRarity.Star1, OperatorFacing.Right);
            op.Deploy(new GridCell(0, 0, TileType.LowGround, Vector3.zero));
            op.IsBurning = true;
            operatorManager.RegisterOperator(op);

            Stage4HeatHazard hazard = hazardObj.AddComponent<Stage4HeatHazard>();
            typeof(Stage4HeatHazard)
                .GetField("operatorManager", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hazard, operatorManager);

            int startingHP = op.CurrentHP;
            typeof(Stage4HeatHazard)
                .GetMethod("ApplyBurningDamage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hazard, null);

            Assert.AreEqual(startingHP - 2, op.CurrentHP);
        }

        [Test]
        public void WaveStartHeatStackGain_StartsMapFlare()
        {
            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.FactoryHeat);

            operatorData = ScriptableObject.CreateInstance<OperatorData>();
            operatorData.operatorName = "HeatFlareGuard";
            operatorData.baseHP = 100;

            operatorObj = new GameObject("HeatFlareOperator");
            GuardOperator op = operatorObj.AddComponent<GuardOperator>();
            op.Initialize(operatorData, OperatorRarity.Star1, OperatorFacing.Right);
            op.Deploy(new GridCell(0, 0, TileType.LowGround, Vector3.zero));
            operatorManager.RegisterOperator(op);

            Stage4HeatHazard hazard = hazardObj.AddComponent<Stage4HeatHazard>();
            emberObj = new GameObject("TestEmbers");
            ParticleSystem embers = emberObj.AddComponent<ParticleSystem>();
            var emberMain = embers.main;
            emberMain.playOnAwake = false;
            var emission = embers.emission;
            emission.rateOverTime = 10f;
            typeof(Stage4HeatHazard)
                .GetField("emberParticleSystem", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hazard, embers);
            typeof(Stage4HeatHazard)
                .GetField("operatorManager", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hazard, operatorManager);
            typeof(Stage4HeatHazard)
                .GetMethod("HandleWaveStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hazard, new object[] { 1, 5 });

            float flareTimer = (float)typeof(Stage4HeatHazard)
                .GetField("heatFlareTimer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(hazard);
            float flareIntensity = (float)typeof(Stage4HeatHazard)
                .GetMethod("GetHeatFlareIntensity", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hazard, null);
            var particles = new ParticleSystem.Particle[FlareParticleCapacity];
            int particleCount = embers.GetParticles(particles);

            Assert.AreEqual(1, op.HeatStacks);
            Assert.AreEqual(Stage4HeatHazard.HeatFlareDurationSeconds, flareTimer);
            Assert.AreEqual(1f, flareIntensity, "The map flare should peak with the round-start ember burst.");
            Assert.Greater(particleCount, 0, "Round-start heat should emit an immediate ember burst.");
            Assert.LessOrEqual(particles[0].remainingLifetime, Stage4HeatHazard.FlareEmberMaxLifetime, "Flare embers should fade with the flare rather than linger.");
        }

        // ---------- The reactor in the art flares when heat stacks up ----------

        private readonly System.Collections.Generic.List<Object> reactorTestObjects = new System.Collections.Generic.List<Object>();

        private GuardOperator CreateDeployedGuard(string name, int heatStacks = 0)
        {
            var data = ScriptableObject.CreateInstance<OperatorData>();
            data.operatorName = name;
            data.baseHP = 100;
            var obj = new GameObject(name);
            GuardOperator op = obj.AddComponent<GuardOperator>();
            op.Initialize(data, OperatorRarity.Star1, OperatorFacing.Right);
            op.Deploy(new GridCell(0, 0, TileType.LowGround, Vector3.zero));
            op.HeatStacks = heatStacks;
            reactorTestObjects.Add(data);
            reactorTestObjects.Add(obj);
            return op;
        }

        private Stage4HeatHazard CreateHazardAsCurrent()
        {
            var obj = new GameObject("ReactorHazard");
            reactorTestObjects.Add(obj);
            Stage4HeatHazard hazard = obj.AddComponent<Stage4HeatHazard>();
            Stage4HeatHazard.Current = hazard;
            return hazard;
        }

        [Test]
        public void ReactorEye_SitsWhereTheArtDrawsIt()
        {
            // Level4 Bounds.png is 640 x 480 px with its eye 320 px across and 68 px down, and the stage puts it (-3, -4) tiles from the grid corner
            Vector2 eye = Stage4HeatHazard.GetReactorWorldPosition(Vector2.zero, new Vector2(-3f, -4f), 1f, 32f);
            Assert.AreEqual(7f, eye.x, 0.001f);
            Assert.AreEqual(8.875f, eye.y, 0.001f);

            Vector2 scaled = Stage4HeatHazard.GetReactorWorldPosition(Vector2.zero, new Vector2(-3f, -4f), 2f, 32f);
            Assert.AreEqual(14f, scaled.x, 0.001f, "A bigger cell size scales the whole art, eye included.");
            Assert.AreEqual(17.75f, scaled.y, 0.001f);
        }

        [Test]
        public void ReactorLevel_FollowsTheStrongerOfThePulseAndTheRoundBlast()
        {
            Assert.AreEqual(0f, Stage4HeatHazard.GetReactorLevel(0f, 0f), 0.0001f);
            Assert.AreEqual(1f, Stage4HeatHazard.GetReactorLevel(1f, 0f), 0.0001f);
            Assert.AreEqual(0.6f, Stage4HeatHazard.GetReactorLevel(0f, 0.6f), 0.0001f, "The round-start envelope drives it too.");
            Assert.AreEqual(0.9f, Stage4HeatHazard.GetReactorLevel(0.2f, 0.9f), 0.0001f);
            Assert.Less(Stage4HeatHazard.GetReactorLevel(0.5f, 0f), 0.5f, "A fading pulse eases out.");
            Assert.Greater(Stage4HeatHazard.GetReactorLevel(0.8f, 0f), Stage4HeatHazard.GetReactorLevel(0.4f, 0f));
        }

        [Test]
        public void PulseReactor_RaisesButNeverLowersThePulse()
        {
            Stage4HeatHazard hazard = CreateHazardAsCurrent();
            Assert.AreEqual(0f, hazard.ReactorPulse);

            hazard.PulseReactor(0.5f);
            Assert.AreEqual(0.5f, hazard.ReactorPulse, 0.0001f);

            hazard.PulseReactor(0.2f);
            Assert.AreEqual(0.5f, hazard.ReactorPulse, 0.0001f, "A weaker pulse does not lower a running one.");

            hazard.PulseReactor(5f);
            Assert.AreEqual(1f, hazard.ReactorPulse, 0.0001f, "Strength is clamped to 1.");
        }

        [Test]
        public void HeatAddedToAnOperator_PulsesTheReactor()
        {
            Stage4HeatHazard hazard = CreateHazardAsCurrent();
            GuardOperator guard = CreateDeployedGuard("ReactorGuard");

            Stage4HeatHazard.AddHeatStack(guard);

            Assert.AreEqual(1, guard.HeatStacks);
            Assert.AreEqual(Stage4HeatHazard.ReactorStackPulse, hazard.ReactorPulse, 0.0001f);
        }

        [Test]
        public void AnIgnition_PulsesTheReactorHarderThanAStack()
        {
            Stage4HeatHazard hazard = CreateHazardAsCurrent();
            GuardOperator guard = CreateDeployedGuard("ReactorIgniter", Stage4HeatHazard.MaxHeatStacks - 1);

            Stage4HeatHazard.AddHeatStack(guard);

            Assert.IsTrue(guard.IsBurning);
            Assert.AreEqual(Stage4HeatHazard.ReactorIgnitePulse, hazard.ReactorPulse, 0.0001f);
            Assert.Greater(Stage4HeatHazard.ReactorIgnitePulse, Stage4HeatHazard.ReactorStackPulse);
        }

        [Test]
        public void HeatAlreadyAtTheMaximum_DoesNotPulseTheReactor()
        {
            Stage4HeatHazard hazard = CreateHazardAsCurrent();
            GuardOperator guard = CreateDeployedGuard("ReactorMaxed", Stage4HeatHazard.MaxHeatStacks);

            Stage4HeatHazard.AddHeatStack(guard);

            Assert.AreEqual(0f, hazard.ReactorPulse, "No stack was added, so nothing flares.");
        }

        [Test]
        public void AddingHeatWithNoHazardRunning_DoesNothingToTheReactor()
        {
            Stage4HeatHazard.Current = null;
            GuardOperator guard = CreateDeployedGuard("ReactorOrphan");

            Assert.DoesNotThrow(() => Stage4HeatHazard.AddHeatStack(guard));
            Assert.AreEqual(1, guard.HeatStacks);
        }

        [Test]
        public void WaveStart_PulsesTheReactorOnceAtFullStrength()
        {
            StageCombatModifiers.ConfigureForEnvironment(StageEnvironment.FactoryHeat);
            Stage4HeatHazard hazard = CreateHazardAsCurrent();
            operatorManager.RegisterOperator(CreateDeployedGuard("WaveGuardA"));
            operatorManager.RegisterOperator(CreateDeployedGuard("WaveGuardB"));
            typeof(Stage4HeatHazard)
                .GetField("operatorManager", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(hazard, operatorManager);

            typeof(Stage4HeatHazard)
                .GetMethod("HandleWaveStarted", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(hazard, new object[] { 1, 5 });

            Assert.AreEqual(Stage4HeatHazard.ReactorWavePulse, hazard.ReactorPulse, 0.0001f);
            float ringStrength = (float)typeof(Stage4HeatHazard)
                .GetField("reactorRingStrength", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(hazard);
            Assert.AreEqual(Stage4HeatHazard.ReactorWavePulse, ringStrength, 0.0001f,
                "One blast for the whole round, not a weaker pulse for each operator first.");
        }

        private const int FlareParticleCapacity = 128; // the burst is 96 embers
    }
}
