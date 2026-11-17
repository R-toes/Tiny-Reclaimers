using UnityEngine;
using UnityEngine.UI;

namespace TrashTD.UI
{
    // Parameterised builders behind the screens' small CreateText / CreateImage / AttachFeedback helpers. Each screen keeps
    // its own helper with its own defaults (its font style, colour, overflow, raycast flag...) and passes them here, so the
    // screens look exactly as before while the construction code (the font, the GameObject setup) lives in one place.
    internal static partial class UiKit
    {
        /// <summary>
        /// A Text on a new child object. <paramref name="color"/> null leaves Unity's default colour; every other setting is
        /// always assigned, and the defaults a caller passes to reproduce "not set" are Unity's own (Wrap, Truncate,
        /// raycast target on, Normal style).
        /// </summary>
        internal static Text MakeText(Transform parent, string name, string content, int size, TextAnchor alignment, Color? color,
            FontStyle style, HorizontalWrapMode horizontal, VerticalWrapMode vertical, bool raycastTarget)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.font = UIFontHelper.GetPixelFont();
            text.text = content;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            if (color.HasValue) text.color = color.Value;
            text.horizontalOverflow = horizontal;
            text.verticalOverflow = vertical;
            text.raycastTarget = raycastTarget;
            return text;
        }

        /// <summary>An Image on a new child object. A null colour or sprite leaves Unity's default (white, none).</summary>
        internal static Image MakeImage(Transform parent, string name, Color? color, Sprite sprite, bool raycastTarget)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.GetComponent<Image>();
            if (sprite != null) image.sprite = sprite;
            if (color.HasValue) image.color = color.Value;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>
        /// The hover scale, glow outline and click/hover callbacks of a menu button (MenuButtonFeedback), shared by the HUD, the
        /// card draft and the main menu. Reuses an existing feedback component or outline. <paramref name="respondToFocus"/>
        /// null leaves the component's default.
        /// </summary>
        internal static MenuButtonFeedback AttachFeedbackCore(Button button, Color glow, float hoverScale,
            System.Action hovered, System.Action clicked, bool? respondToFocus)
        {
            if (button == null) return null;

            // The feedback component drives the visuals, so disable Unity's colour tint transition
            button.transition = Selectable.Transition.None;

            var feedback = button.GetComponent<MenuButtonFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<MenuButtonFeedback>();

            feedback.hoverScale = hoverScale;
            if (respondToFocus.HasValue) feedback.respondToFocus = respondToFocus.Value;
            feedback.Hovered = hovered;
            feedback.Clicked = clicked;

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                var outline = image.GetComponent<Outline>();
                if (outline == null) outline = image.gameObject.AddComponent<Outline>();
                outline.useGraphicAlpha = false;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.effectColor = new Color(glow.r, glow.g, glow.b, 0f);

                feedback.glowOutline = outline;
                feedback.glowColor = new Color(glow.r, glow.g, glow.b, 0.9f);
            }

            return feedback;
        }
    }
}
