using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;

namespace TrashTD.Tests
{
    /// <summary>
    /// UiKit.MakeText, MakeImage and AttachFeedbackCore are what the screens' small CreateText / CreateImage / AttachFeedback
    /// helpers forward to (each passing its own defaults), so these pin what they apply, plus the shared kit helpers.
    /// </summary>
    public class UiKitBuilderTests
    {
        private GameObject canvas;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvas);
        }

        // ------------------------------------------------------------------ MakeText

        [Test]
        public void MakeText_AppliesTheProjectFont_AndEverySettingItIsGiven()
        {
            Text text = UiKit.MakeText(canvas.transform, "Label", "HELLO", 18, TextAnchor.MiddleRight, Color.red, FontStyle.Bold,
                HorizontalWrapMode.Overflow, VerticalWrapMode.Overflow, false);

            Assert.AreEqual("Label", text.name);
            Assert.AreEqual("HELLO", text.text);
            Assert.AreEqual(18, text.fontSize);
            Assert.AreEqual(TextAnchor.MiddleRight, text.alignment);
            Assert.AreEqual(Color.red, text.color);
            Assert.AreEqual(FontStyle.Bold, text.fontStyle);
            Assert.AreEqual(HorizontalWrapMode.Overflow, text.horizontalOverflow);
            Assert.AreEqual(VerticalWrapMode.Overflow, text.verticalOverflow);
            Assert.IsFalse(text.raycastTarget);
            Assert.AreSame(UIFontHelper.GetPixelFont(), text.font);
            Assert.AreSame(canvas.transform, text.transform.parent);
        }

        [Test]
        public void MakeText_WithNoColour_LeavesUnitysDefault_AndUnityDefaultsMatchAnUnsetText()
        {
            var untouched = new GameObject("Untouched", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            untouched.transform.SetParent(canvas.transform, false);

            Text text = UiKit.MakeText(canvas.transform, "Label", "x", 14, TextAnchor.UpperLeft, null, FontStyle.Normal,
                HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, true);

            // Passing Unity's own defaults must be the same as not setting them: that is how a screen keeps its look
            Assert.AreEqual(untouched.color, text.color);
            Assert.AreEqual(untouched.horizontalOverflow, text.horizontalOverflow);
            Assert.AreEqual(untouched.verticalOverflow, text.verticalOverflow);
            Assert.AreEqual(untouched.raycastTarget, text.raycastTarget);
            Assert.AreEqual(untouched.fontStyle, text.fontStyle);
        }

        // ------------------------------------------------------------------ MakeImage

        [Test]
        public void MakeImage_SetsOnlyWhatItIsGiven()
        {
            var texture = new Texture2D(4, 4);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));

            Image coloured = UiKit.MakeImage(canvas.transform, "A", new Color(0.1f, 0.2f, 0.3f, 0.4f), null, false);
            Image sprited = UiKit.MakeImage(canvas.transform, "B", null, sprite, true);

            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f, 0.4f), coloured.color);
            Assert.IsNull(coloured.sprite);
            Assert.IsFalse(coloured.raycastTarget);
            Assert.AreEqual(Color.white, sprited.color, "no colour given: Unity's default");
            Assert.AreSame(sprite, sprited.sprite);
            Assert.IsTrue(sprited.raycastTarget);

            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(texture);
        }

        // ------------------------------------------------------------------ AttachFeedbackCore

        private static Button MakeButton(Transform parent)
        {
            var go = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Button>();
        }

        [Test]
        public void AttachFeedbackCore_AddsHoverScaleCallbacksAndAGlowOutline()
        {
            Button button = MakeButton(canvas.transform);
            int hovered = 0, clicked = 0;

            MenuButtonFeedback feedback = UiKit.AttachFeedbackCore(button, new Color(0.2f, 0.4f, 0.6f), 1.07f, () => hovered++, () => clicked++, false);

            Assert.IsNotNull(feedback);
            Assert.AreEqual(Selectable.Transition.None, button.transition);
            Assert.AreEqual(1.07f, feedback.hoverScale, 0.0001f);
            Assert.IsFalse(feedback.respondToFocus);
            feedback.Hovered();
            feedback.Clicked();
            Assert.AreEqual(1, hovered);
            Assert.AreEqual(1, clicked);

            Outline outline = button.GetComponent<Outline>();
            Assert.IsNotNull(outline);
            Assert.AreSame(outline, feedback.glowOutline);
            Assert.AreEqual(0f, outline.effectColor.a, 0.0001f, "the glow starts hidden");
            Assert.AreEqual(0.9f, feedback.glowColor.a, 0.0001f);
            Assert.AreEqual(0.4f, feedback.glowColor.g, 0.0001f);
        }

        [Test]
        public void AttachFeedbackCore_CalledTwice_ReusesTheComponentAndTheOutline()
        {
            Button button = MakeButton(canvas.transform);

            MenuButtonFeedback first = UiKit.AttachFeedbackCore(button, Color.green, 1.05f, () => { }, () => { }, null);
            MenuButtonFeedback second = UiKit.AttachFeedbackCore(button, Color.red, 1.1f, () => { }, () => { }, null);

            Assert.AreSame(first, second);
            Assert.AreEqual(1, button.GetComponents<MenuButtonFeedback>().Length);
            Assert.AreEqual(1, button.GetComponents<Outline>().Length);
            Assert.AreEqual(1.1f, second.hoverScale, 0.0001f);
            Assert.AreEqual(Color.red.r, second.glowColor.r, 0.0001f, "the second call's glow colour wins");
        }

        [Test]
        public void AttachFeedbackCore_WithNoRespondToFocus_LeavesTheComponentsDefault()
        {
            Button button = MakeButton(canvas.transform);

            MenuButtonFeedback feedback = UiKit.AttachFeedbackCore(button, Color.white, 1.05f, () => { }, () => { }, null);

            Assert.IsTrue(feedback.respondToFocus, "MenuButtonFeedback's default");
        }

        [Test]
        public void AttachFeedbackCore_WithNoButton_DoesNothing()
        {
            Assert.IsNull(UiKit.AttachFeedbackCore(null, Color.white, 1f, () => { }, () => { }, true));
        }

        // ------------------------------------------------------------------ the shared kit helpers

        [Test]
        public void SharedCreateText_AndCreateImage_IgnoreClicks()
        {
            Text text = UiKit.CreateText(canvas.transform, "HELLO", 18, TextAnchor.MiddleRight, Color.red);
            Image image = UiKit.CreateImage(canvas.transform, "Block", Color.blue);

            Assert.IsFalse(text.raycastTarget, "labels do not take clicks");
            Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(HorizontalWrapMode.Overflow, text.horizontalOverflow);
        }

        [Test]
        public void SharedStretch_FillsTheParent()
        {
            Image image = UiKit.CreateImage(canvas.transform, "Block", Color.white);

            UiKit.Stretch(image.rectTransform);

            Assert.AreEqual(Vector2.zero, image.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, image.rectTransform.anchorMax);
            Assert.AreEqual(Vector2.zero, image.rectTransform.offsetMin);
            Assert.AreEqual(Vector2.zero, image.rectTransform.offsetMax);
        }

        [Test]
        public void SharedEasing_StartsAtZero_EndsAtOne_AndOvershootsWhenAsked()
        {
            Assert.AreEqual(0f, UiKit.EaseOutCubic(0f), 0.0001f);
            Assert.AreEqual(1f, UiKit.EaseOutCubic(1f), 0.0001f);
            Assert.AreEqual(0f, UiKit.EaseOutBack(0f), 0.0001f);
            Assert.AreEqual(1f, UiKit.EaseOutBack(1f), 0.0001f);

            bool overshoots = false;
            for (float t = 0f; t <= 1f; t += 0.05f) overshoots |= UiKit.EaseOutBack(t) > 1f;
            Assert.IsTrue(overshoots, "EaseOutBack goes past 1 before settling");
        }
    }
}
