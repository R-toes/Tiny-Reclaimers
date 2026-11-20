using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace TrashTD.UI
{
    /// <summary>
    /// One place to ask "where is the pointer and is it pressed?", whether the player uses a mouse or a finger.
    /// Gameplay code used to read Mouse.current directly, which is null on a phone. A finger counts as the
    /// "left button": press, hold and release all work the same way. The right mouse button has no touch
    /// equivalent; every action that used it has an on-screen button as well.
    /// When both devices exist (a laptop with a touch screen) the finger wins while it is down.
    /// </summary>
    public static class PointerInput
    {
        /// <summary>True when any pointing device (mouse or touch screen) is connected.</summary>
        public static bool Available => Touchscreen.current != null || Mouse.current != null;

        /// <summary>True on a phone or tablet build (the on-screen touch layout).</summary>
        public static bool IsMobileDevice => Application.isMobilePlatform;

        /// <summary>The finger is on the screen now, or lifted this frame.</summary>
        private static bool TouchActive()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null) return false;
            ButtonControl press = touchscreen.primaryTouch.press;
            return press.isPressed || press.wasReleasedThisFrame;
        }

        /// <summary>Screen position of the finger, or the mouse when no finger is down. Zero when there is no device.</summary>
        public static Vector2 ScreenPosition
        {
            get
            {
                Touchscreen touchscreen = Touchscreen.current;
                Mouse mouse = Mouse.current;
                if (touchscreen != null && (TouchActive() || mouse == null)) return touchscreen.primaryTouch.position.ReadValue();
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            }
        }

        /// <summary>The pointer position in game-picture coordinates (curved CRT glass applied), ready for cameras.</summary>
        public static Vector2 PicturePosition => CrtDisplay.ScreenToPicture(ScreenPosition);

        /// <summary>The finger went down, or the left mouse button was pressed, this frame.</summary>
        public static bool PressedThisFrame
        {
            get
            {
                Touchscreen touchscreen = Touchscreen.current;
                Mouse mouse = Mouse.current;
                return (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame) ||
                       (mouse != null && mouse.leftButton.wasPressedThisFrame);
            }
        }

        /// <summary>The finger is down, or the left mouse button is held.</summary>
        public static bool IsPressed
        {
            get
            {
                Touchscreen touchscreen = Touchscreen.current;
                Mouse mouse = Mouse.current;
                return (touchscreen != null && touchscreen.primaryTouch.press.isPressed) ||
                       (mouse != null && mouse.leftButton.isPressed);
            }
        }

        /// <summary>The finger lifted, or the left mouse button was released, this frame.</summary>
        public static bool ReleasedThisFrame
        {
            get
            {
                Touchscreen touchscreen = Touchscreen.current;
                Mouse mouse = Mouse.current;
                return (touchscreen != null && touchscreen.primaryTouch.press.wasReleasedThisFrame) ||
                       (mouse != null && mouse.leftButton.wasReleasedThisFrame);
            }
        }

        /// <summary>Any mouse button (left, right or middle) went down this frame. Always false for touch.</summary>
        public static bool AnyMouseButtonPressedThisFrame
        {
            get
            {
                Mouse mouse = Mouse.current;
                return mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
                                         mouse.middleButton.wasPressedThisFrame);
            }
        }

        /// <summary>The right mouse button went down this frame. Always false for touch (use the on-screen buttons).</summary>
        public static bool SecondaryPressedThisFrame
        {
            get
            {
                Mouse mouse = Mouse.current;
                return mouse != null && mouse.rightButton.wasPressedThisFrame;
            }
        }

        /// <summary>The pointer is on top of a UI element (a finger needs its own id to be checked).</summary>
        public static bool IsOverUi()
        {
            EventSystem system = EventSystem.current;
            if (system == null) return false;

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && TouchActive())
            {
                int touchId = touchscreen.primaryTouch.touchId.ReadValue();
                if (touchId != 0) return system.IsPointerOverGameObject(touchId);
            }

            return system.IsPointerOverGameObject();
        }
    }
}
