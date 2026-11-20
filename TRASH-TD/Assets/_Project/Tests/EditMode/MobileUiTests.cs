using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>The touch sizes (MobileUi) and the tap-area growth (TouchTargets), without a phone.</summary>
    public class MobileUiTests
    {
        [Test]
        public void Pick_GivesTheDesktopValueOffAPhoneAndTheTouchValueOnOne()
        {
            Assert.AreEqual(44f, MobileUi.Pick(false, 44f, 92f));
            Assert.AreEqual(92f, MobileUi.Pick(true, 44f, 92f));
        }

        [Test]
        public void InEditor_EverySizeIsTheDesktopOne()
        {
            Assert.IsFalse(MobileUi.IsMobile);
            Assert.AreEqual(1f, MobileUi.InfoTab);
            Assert.AreEqual(44f, MobileUi.Retreat);
            Assert.AreEqual(0.82f, MobileUi.Details(0.82f));
        }

        [Test]
        public void TouchSizes_AreBiggerThanDesktop()
        {
            Assert.Greater(MobileUi.InfoTabScale, 1f);
            Assert.Greater(MobileUi.RetreatButtonSize, 44f);
            Assert.Greater(MobileUi.DetailsPanelScale, 0.82f);
        }

        [Test]
        public void TouchPadding_IsZeroForAnElementThatIsBigEnough()
        {
            Assert.AreEqual(0f, MobileUi.TouchPadding(MobileUi.MinTouchTarget, 1f));
            Assert.AreEqual(0f, MobileUi.TouchPadding(300f, 1f));
        }

        [Test]
        public void TouchPadding_GrowsASmallElementToTheMinimum_UpToTheCap()
        {
            // 60 -> 84 needs 12 on each side
            Assert.AreEqual(12f, MobileUi.TouchPadding(60f, 1f), 0.001f);
            // 20 would need 32 each side, but the cap keeps neighbours apart
            Assert.AreEqual(MobileUi.MaxTouchPadding, MobileUi.TouchPadding(20f, 1f), 0.001f);
        }

        [Test]
        public void TouchPadding_ConvertsToTheElementsOwnUnits()
        {
            // an element drawn at half size: 1 canvas unit is 2 of its own units
            Assert.AreEqual(24f, MobileUi.TouchPadding(60f, 2f), 0.001f);
        }

        [Test]
        public void Grow_MakesTheTapAreaBiggerWithoutChangingTheButton()
        {
            var canvasObject = new GameObject("Canvas", typeof(Canvas));
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            try
            {
                buttonObject.transform.SetParent(canvasObject.transform, false);
                var rect = (RectTransform)buttonObject.transform;
                rect.sizeDelta = new Vector2(44f, 44f);
                Graphic graphic = buttonObject.GetComponent<Image>();

                TouchTargets.Grow(graphic);

                Vector4 padding = graphic.raycastPadding;
                Assert.Less(padding.x, 0f, "negative padding grows the tap area");
                Assert.AreEqual(padding.x, padding.z, 0.001f);
                Assert.AreEqual(padding.y, padding.w, 0.001f);
                Assert.AreEqual(44f, rect.sizeDelta.x, "the button itself is unchanged");
            }
            finally
            {
                Object.DestroyImmediate(canvasObject);
            }
        }
    }
}
