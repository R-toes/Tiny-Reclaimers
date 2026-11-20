using NUnit.Framework;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>The phone-only options (UI size, frame rate, picture quality): their rules, without a phone.</summary>
    public class MobileSettingsTests
    {
        [Test]
        public void UiSizeMultiplier_SmallIsSmallerAndLargeIsBiggerThanNormal()
        {
            Assert.AreEqual(1f, MobileSettings.UiSizeMultiplier(UiSize.Normal));
            Assert.Less(MobileSettings.UiSizeMultiplier(UiSize.Small), 1f);
            Assert.Greater(MobileSettings.UiSizeMultiplier(UiSize.Large), 1f);
        }

        [Test]
        public void PictureScale_FullIsOneAndEachStepIsSmaller()
        {
            Assert.AreEqual(1f, MobileSettings.PictureScale(PictureQuality.Full));
            Assert.Less(MobileSettings.PictureScale(PictureQuality.Medium), 1f);
            Assert.Less(MobileSettings.PictureScale(PictureQuality.Low), MobileSettings.PictureScale(PictureQuality.Medium));
            Assert.Greater(MobileSettings.PictureScale(PictureQuality.Low), 0f);
        }

        [TestCase(30, 30)]
        [TestCase(60, 60)]
        [TestCase(90, 90)]
        [TestCase(0, 30)]
        [TestCase(45, 30)]
        [TestCase(75, 60)]
        [TestCase(144, 90)]
        [TestCase(-5, 30)]
        public void ClampFrameRate_PicksTheNearestAllowedRate(int saved, int expected)
        {
            Assert.AreEqual(expected, MobileSettings.ClampFrameRate(saved));
        }

        [Test]
        public void FrameRates_IncludeTheDefault()
        {
            Assert.Contains(MobileSettings.DefaultFrameRate, MobileSettings.FrameRates);
        }

        [Test]
        public void OffAPhone_NothingChanges()
        {
            Assert.AreEqual(1f, MobileSettings.CurrentUiSizeMultiplier);
            Assert.AreEqual(1f, MobileSettings.CurrentPictureScale);
        }

        [Test]
        public void TouchSizes_FollowTheUiSizeMultiplier()
        {
            // MobileUi multiplies by CurrentUiSizeMultiplier; the constants are the Normal sizes
            Assert.AreEqual(MobileUi.RetreatButtonSize * MobileSettings.UiSizeMultiplier(UiSize.Large),
                MobileUi.RetreatButtonSize * 1.3f, 0.001f);
            Assert.Less(MobileUi.RetreatButtonSize * MobileSettings.UiSizeMultiplier(UiSize.Small), MobileUi.RetreatButtonSize);
        }
    }
}
