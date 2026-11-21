using NUnit.Framework;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;

namespace TrashTD.Tests
{
    public class OperatorManagerCooldownTests
    {
        private GameObject gridObject;
        private GameObject deckObject;
        private GameObject operatorManagerObject;
        private GridManager gridManager;
        private PlayerDeck playerDeck;
        private OperatorManager operatorManager;
        private StageData stageData;
        private OperatorData operatorData;
        private OperatorBase retreatedOperator;
        private OperatorBase deployedOperator;

        [SetUp]
        public void SetUp()
        {
            gridObject = new GameObject("TestGridManager");
            gridManager = gridObject.AddComponent<GridManager>();
            stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.gridWidth = 2;
            stageData.gridHeight = 1;
            stageData.tileLayout = new[] { TileType.HighGround, TileType.HighGround };
            gridManager.InitializeFromStageData(stageData);

            deckObject = new GameObject("TestPlayerDeck");
            playerDeck = TestUtility.AddAwakened<PlayerDeck>(deckObject);

            operatorManagerObject = new GameObject("TestOperatorManager");
            operatorManager = TestUtility.AddAwakened<OperatorManager>(operatorManagerObject);

            operatorData = ScriptableObject.CreateInstance<OperatorData>();
            operatorData.operatorName = "TestSniper";
            operatorData.operatorClass = OperatorClass.Sniper;
            operatorData.position = OperatorPosition.Ranged;
        }

        [TearDown]
        public void TearDown()
        {
            TestUtility.ClearSingletons();
            if (retreatedOperator != null) Object.DestroyImmediate(retreatedOperator.gameObject);
            if (deployedOperator != null) Object.DestroyImmediate(deployedOperator.gameObject);
            if (operatorManagerObject != null) Object.DestroyImmediate(operatorManagerObject);
            if (deckObject != null) Object.DestroyImmediate(deckObject);
            if (gridObject != null) Object.DestroyImmediate(gridObject);
            if (stageData != null) Object.DestroyImmediate(stageData);
            if (operatorData != null) Object.DestroyImmediate(operatorData);
        }

        [Test]
        public void RetreatedCopyCooldown_DoesNotBlockReadyCopyOfSameOperator()
        {
            var retreatedCopy = new DraftCard(operatorData, OperatorRarity.Star1);
            var readyCopy = new DraftCard(operatorData, OperatorRarity.Star1);
            playerDeck.AddCard(retreatedCopy);
            playerDeck.AddCard(readyCopy);

            Assert.IsTrue(operatorManager.TryDeployOperator(
                retreatedCopy,
                new Vector2Int(0, 0),
                OperatorFacing.Right,
                out retreatedOperator));
            playerDeck.RemoveCard(retreatedCopy);

            // An operator retreated before it has fought, between waves, is refunded with no cooldown. This one fought.
            retreatedOperator.HasFoughtInWave = true;
            TestUtility.ExpectEditModeDestroy();
            operatorManager.RetreatOperator(retreatedOperator);

            DraftCard coolingCopy = null;
            foreach (DraftCard card in playerDeck.DeckSlots)
            {
                if (card != null && card != readyCopy)
                {
                    coolingCopy = card;
                    break;
                }
            }

            Assert.IsNotNull(coolingCopy);
            Assert.AreEqual(1, coolingCopy.cooldownRoundsRemaining);
            Assert.AreEqual(0, readyCopy.cooldownRoundsRemaining);
            Assert.IsTrue(operatorManager.TryDeployOperator(
                readyCopy,
                new Vector2Int(1, 0),
                OperatorFacing.Right,
                out deployedOperator));
        }

        // Upgrades merge cards in the deck only (PROJECT_GUIDELINES.md, 1.4): an operator on the field is never upgraded.
        // Recall it, and its card can merge from the deck.

        [Test]
        public void DeployedCopy_DoesNotCountTowardMerge_AndIsNeverUpgradedOnTheField()
        {
            var upgradeObject = new GameObject("TestUpgradeSystem");
            TestUtility.AddAwakened<RarityUpgradeSystem>(upgradeObject);
            try
            {
                var fieldCopy = new DraftCard(operatorData, OperatorRarity.Star1);
                playerDeck.AddCard(fieldCopy);
                Assert.IsTrue(operatorManager.TryDeployOperator(
                    fieldCopy, new Vector2Int(0, 0), OperatorFacing.Right, out deployedOperator));
                playerDeck.RemoveCard(fieldCopy);
                int star1MaxHP = deployedOperator.MaxHP;

                // A field copy plus two cards is not three cards: nothing merges
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));

                Assert.AreEqual(OperatorRarity.Star1, deployedOperator.CurrentRarity);
                Assert.AreEqual(star1MaxHP, deployedOperator.MaxHP);
                Assert.AreEqual(2, playerDeck.CardCount, "The two deck cards wait for a third.");

                // Three cards in the deck merge into one 2-star card, and the operator on the field stays as it was
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));

                Assert.AreEqual(1, playerDeck.CardCount);
                Assert.AreEqual(OperatorRarity.Star2, FirstCard().rarity);
                Assert.AreEqual(OperatorRarity.Star1, deployedOperator.CurrentRarity);
                Assert.AreEqual(1, operatorManager.DeployedCount);
            }
            finally
            {
                Object.DestroyImmediate(upgradeObject);
            }
        }

        [Test]
        public void TwoDeployedCopies_StayAsTheyAre_WhileTheDeckMergesItsOwnCards()
        {
            var upgradeObject = new GameObject("TestUpgradeSystem");
            TestUtility.AddAwakened<RarityUpgradeSystem>(upgradeObject);
            try
            {
                var firstCard = new DraftCard(operatorData, OperatorRarity.Star1);
                playerDeck.AddCard(firstCard);
                Assert.IsTrue(operatorManager.TryDeployOperator(
                    firstCard, new Vector2Int(0, 0), OperatorFacing.Right, out deployedOperator));
                playerDeck.RemoveCard(firstCard);

                var secondCard = new DraftCard(operatorData, OperatorRarity.Star1);
                playerDeck.AddCard(secondCard);
                Assert.IsTrue(operatorManager.TryDeployOperator(
                    secondCard, new Vector2Int(1, 0), OperatorFacing.Right, out retreatedOperator));
                playerDeck.RemoveCard(secondCard);

                // Two field copies plus two cards: still only two cards in the deck, so no merge
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));
                Assert.AreEqual(2, playerDeck.CardCount);

                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));

                Assert.AreEqual(2, operatorManager.DeployedCount, "Nothing leaves the field.");
                Assert.AreEqual(1, playerDeck.CardCount, "The three deck cards became one.");
                Assert.AreEqual(OperatorRarity.Star2, FirstCard().rarity);
                Assert.AreEqual(OperatorRarity.Star1, deployedOperator.CurrentRarity);
                Assert.AreEqual(OperatorRarity.Star1, retreatedOperator.CurrentRarity);
            }
            finally
            {
                Object.DestroyImmediate(upgradeObject);
            }
        }

        private DraftCard FirstCard()
        {
            foreach (DraftCard card in playerDeck.DeckSlots)
                if (card != null) return card;
            return null;
        }

        [Test]
        public void RetreatedOperator_RedeploysWithItsSavedHP()
        {
            var card = new DraftCard(operatorData, OperatorRarity.Star1);
            playerDeck.AddCard(card);
            Assert.IsTrue(operatorManager.TryDeployOperator(
                card, new Vector2Int(0, 0), OperatorFacing.Right, out retreatedOperator));
            playerDeck.RemoveCard(card);

            retreatedOperator.TakeTrueDamage(37);
            int woundedHP = retreatedOperator.CurrentHP;
            Assert.Less(woundedHP, retreatedOperator.MaxHP);

            TestUtility.ExpectEditModeDestroy();
            operatorManager.RetreatOperator(retreatedOperator);

            DraftCard returnedCard = FindFirstDeckCard();
            Assert.IsNotNull(returnedCard);
            Assert.AreEqual(woundedHP, returnedCard.savedHP);

            returnedCard.cooldownRoundsRemaining = 0;
            Assert.IsTrue(operatorManager.TryDeployOperator(
                returnedCard, new Vector2Int(1, 0), OperatorFacing.Right, out deployedOperator));
            Assert.AreEqual(woundedHP, deployedOperator.CurrentHP);
        }

        [Test]
        public void FreshCard_DeploysAtFullHP()
        {
            var card = new DraftCard(operatorData, OperatorRarity.Star1);
            Assert.IsFalse(card.HasSavedHP);

            Assert.IsTrue(operatorManager.TryDeployOperator(
                card, new Vector2Int(0, 0), OperatorFacing.Right, out deployedOperator));
            Assert.AreEqual(deployedOperator.MaxHP, deployedOperator.CurrentHP);
        }

        [Test]
        public void MergingWoundedCopies_KeepsTheLowestHPFraction()
        {
            var upgradeObject = new GameObject("TestUpgradeSystem");
            TestUtility.AddAwakened<RarityUpgradeSystem>(upgradeObject);
            try
            {
                int star1MaxHP = operatorData.GetScaledHP(OperatorRarity.Star1);
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1, 0, 0, star1MaxHP / 4));
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));
                playerDeck.AddCard(new DraftCard(operatorData, OperatorRarity.Star1));

                Assert.AreEqual(1, playerDeck.CardCount);
                DraftCard merged = FindFirstDeckCard();
                Assert.AreEqual(OperatorRarity.Star2, merged.rarity);
                Assert.AreEqual(0.25f, merged.GetHealthRatio(), 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(upgradeObject);
            }
        }

        private DraftCard FindFirstDeckCard()
        {
            foreach (DraftCard card in playerDeck.DeckSlots)
            {
                if (card != null) return card;
            }
            return null;
        }

        [Test]
        public void TryDeployOperator_RejectsTheCopyThatIsOnCooldown()
        {
            var coolingCopy = new DraftCard(operatorData, OperatorRarity.Star1, 1);

            Assert.IsFalse(operatorManager.TryDeployOperator(
                coolingCopy,
                new Vector2Int(0, 0),
                OperatorFacing.Right,
                out deployedOperator));
            Assert.IsNull(deployedOperator);
        }
    }
}
