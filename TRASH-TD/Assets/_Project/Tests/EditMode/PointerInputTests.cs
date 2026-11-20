using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// PointerInput is what lets the game run on a phone: a finger must work like the left mouse button, and the
    /// code must not need a mouse to exist. These tests drive simulated mouse and touch devices.
    /// </summary>
    public class PointerInputTests : InputTestFixture
    {
        [Test]
        public void NoDevices_NothingIsAvailableAndNothingIsPressed()
        {
            Assert.IsFalse(PointerInput.Available);
            Assert.AreEqual(Vector2.zero, PointerInput.ScreenPosition);
            Assert.IsFalse(PointerInput.PressedThisFrame);
            Assert.IsFalse(PointerInput.IsPressed);
            Assert.IsFalse(PointerInput.ReleasedThisFrame);
            Assert.IsFalse(PointerInput.SecondaryPressedThisFrame);
            Assert.IsFalse(PointerInput.IsOverUi());
        }

        [Test]
        public void Mouse_LeftButtonPressHoldAndRelease()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Set(mouse.position, new Vector2(120f, 80f));
            Assert.IsTrue(PointerInput.Available);
            Assert.AreEqual(new Vector2(120f, 80f), PointerInput.ScreenPosition);

            Press(mouse.leftButton);
            Assert.IsTrue(PointerInput.PressedThisFrame);
            Assert.IsTrue(PointerInput.IsPressed);

            Release(mouse.leftButton);
            Assert.IsTrue(PointerInput.ReleasedThisFrame);
            Assert.IsFalse(PointerInput.IsPressed);
        }

        [Test]
        public void Mouse_RightButtonIsSecondaryNotPrimary()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.rightButton);

            Assert.IsTrue(PointerInput.SecondaryPressedThisFrame);
            Assert.IsTrue(PointerInput.AnyMouseButtonPressedThisFrame);
            Assert.IsFalse(PointerInput.PressedThisFrame);
            Assert.IsFalse(PointerInput.IsPressed);
        }

        [Test]
        public void Touch_WorksWithoutAMouse()
        {
            Touchscreen screen = InputSystem.AddDevice<Touchscreen>();
            Assert.IsNull(Mouse.current);
            Assert.IsTrue(PointerInput.Available);

            BeginTouch(1, new Vector2(300f, 200f), screen: screen);
            Assert.IsTrue(PointerInput.PressedThisFrame);
            Assert.IsTrue(PointerInput.IsPressed);
            Assert.AreEqual(new Vector2(300f, 200f), PointerInput.ScreenPosition);

            MoveTouch(1, new Vector2(340f, 210f), screen: screen);
            Assert.AreEqual(new Vector2(340f, 210f), PointerInput.ScreenPosition);

            EndTouch(1, new Vector2(340f, 210f), screen: screen);
            Assert.IsTrue(PointerInput.ReleasedThisFrame);
            Assert.IsFalse(PointerInput.IsPressed);
            Assert.AreEqual(new Vector2(340f, 210f), PointerInput.ScreenPosition, "the last touch position is kept after the finger lifts");
        }

        [Test]
        public void Touch_HasNoRightClick()
        {
            Touchscreen screen = InputSystem.AddDevice<Touchscreen>();
            BeginTouch(1, new Vector2(10f, 10f), screen: screen);

            Assert.IsFalse(PointerInput.SecondaryPressedThisFrame);
            Assert.IsFalse(PointerInput.AnyMouseButtonPressedThisFrame);
        }

        [Test]
        public void MouseAndTouch_FingerWinsWhileItIsDown()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Touchscreen screen = InputSystem.AddDevice<Touchscreen>();
            Set(mouse.position, new Vector2(500f, 500f));
            Assert.AreEqual(new Vector2(500f, 500f), PointerInput.ScreenPosition);

            BeginTouch(1, new Vector2(40f, 60f), screen: screen);
            Assert.AreEqual(new Vector2(40f, 60f), PointerInput.ScreenPosition);

            EndTouch(1, new Vector2(40f, 60f), screen: screen);
            InputSystem.Update(); // the frame after the lift: the mouse is the pointer again
            Assert.AreEqual(new Vector2(500f, 500f), PointerInput.ScreenPosition);
        }

        [Test]
        public void PictureRect_IsTheWholeScreenOutsideAPhone()
        {
            Assert.IsFalse(Application.isMobilePlatform);
            Rect rect = CrtDisplay.PictureRect;
            Assert.AreEqual(0f, rect.x);
            Assert.AreEqual(0f, rect.y);
            Assert.AreEqual(Mathf.Max(1, Screen.width), rect.width);
            Assert.AreEqual(Mathf.Max(1, Screen.height), rect.height);
        }
    }
}
