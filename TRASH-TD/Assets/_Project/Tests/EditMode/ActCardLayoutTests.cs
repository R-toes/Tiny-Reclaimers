using NUnit.Framework;
using UnityEngine;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// The act selection screen shows one fixed-size card per act in a horizontal scroll view. The cards never change size
    /// with the number of acts; they stay centred while they fit and scroll when they do not. All sizes are for the 1920 x
    /// 1080 reference.
    /// </summary>
    public class ActCardLayoutTests
    {
        [Test]
        public void TheCardsAreAlwaysTheSameSize()
        {
            // The card size is a constant, not a function of how many acts there are
            Assert.AreEqual(800f, ActWorldMapScreen.GetCardWidth(), 0.001f);
            Assert.Greater(ActWorldMapScreen.CardHeight, 0f);
        }

        [Test]
        public void OneCard_IsCentred()
        {
            Assert.AreEqual(0f, ActWorldMapScreen.GetCardCentreX(0, 1), 0.001f);
        }

        [Test]
        public void TwoCards_SitSideBySide_AroundTheCentre()
        {
            float step = ActWorldMapScreen.GetCardWidth() + ActWorldMapScreen.CardGap;

            Assert.AreEqual(-step * 0.5f, ActWorldMapScreen.GetCardCentreX(0, 2), 0.001f);
            Assert.AreEqual(step * 0.5f, ActWorldMapScreen.GetCardCentreX(1, 2), 0.001f);
        }

        [Test]
        public void Cards_KeepTheSameSpacing_WhateverTheCount()
        {
            float step = ActWorldMapScreen.GetCardWidth() + ActWorldMapScreen.CardGap;
            for (int count = 2; count <= 6; count++)
            {
                for (int i = 1; i < count; i++)
                {
                    float distance = ActWorldMapScreen.GetCardCentreX(i, count) - ActWorldMapScreen.GetCardCentreX(i - 1, count);
                    Assert.AreEqual(step, distance, 0.001f, i + " of " + count);
                }
            }
        }

        [Test]
        public void TheCardsAreCentredInTheirContent_WhateverTheCount()
        {
            for (int count = 1; count <= 6; count++)
            {
                float first = ActWorldMapScreen.GetCardCentreX(0, count);
                float last = ActWorldMapScreen.GetCardCentreX(count - 1, count);
                Assert.AreEqual(0f, first + last, 0.001f, "count " + count);
            }
        }

        [Test]
        public void OneAndTwoActs_FitTheViewWithoutScrolling_ThreeOrMoreScroll()
        {
            Assert.IsFalse(ActWorldMapScreen.NeedsScrolling(1));
            Assert.IsFalse(ActWorldMapScreen.NeedsScrolling(2));
            Assert.IsTrue(ActWorldMapScreen.NeedsScrolling(3));
            Assert.IsTrue(ActWorldMapScreen.NeedsScrolling(8));
        }

        [Test]
        public void TheContentGrowsByOneCardAndGapPerAct()
        {
            float step = ActWorldMapScreen.GetCardWidth() + ActWorldMapScreen.CardGap;

            for (int count = 1; count < 6; count++)
            {
                Assert.AreEqual(step, ActWorldMapScreen.GetContentWidth(count + 1) - ActWorldMapScreen.GetContentWidth(count), 0.001f);
            }
        }

        [Test]
        public void TheCardsFitTheScrollViewsHeight_AndTheViewClearsTheTitleBlockAndFooter()
        {
            // With room for the card's brackets and the hover scale
            Assert.LessOrEqual(ActWorldMapScreen.CardHeight * 1.02f, ActWorldMapScreen.ViewportHeight);
            // The view is what is left of the 1080 screen below the title block (~200 px) and above the footer (~200 px)
            Assert.GreaterOrEqual(ActWorldMapScreen.ViewportHeight, 600f);
            Assert.LessOrEqual(ActWorldMapScreen.ViewportWidth, 1920f);
        }
    }
}
