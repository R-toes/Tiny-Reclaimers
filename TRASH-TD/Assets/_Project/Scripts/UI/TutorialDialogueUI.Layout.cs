using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;

namespace TrashTD.UI
{
    // Layout and shared UI building helpers.
    public partial class TutorialDialogueUI
    {
        private void ApplyLayout()
        {
            RectTransform skipRect = skipButton.GetComponent<RectTransform>();
            if (layout == TutorialDialogueLayout.Compact)
            {
                // Bottom-left: Coalesce, bubble to the right, skip above Coalesce; slides off to the left
                float size = CompactSpriteSize;
                Anchor(speakerRig, new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(size, size));
                Anchor(bubble, new Vector2(0f, 0f), new Vector2(40f + size + 22f, 24f), new Vector2(CompactBubbleWidth, CompactBubbleHeight));
                tail.anchorMin = tail.anchorMax = new Vector2(0f, 0f);
                tail.anchoredPosition = new Vector2(0f, size * 0.5f);
                Anchor(skipRect, new Vector2(0f, 0f), new Vector2(40f, 24f + size + 10f), new Vector2(176f, 32f));
                slideSign = -1f;
                slideDistance = 40f + size + 22f + CompactBubbleWidth + 40f;
                return;
            }

            // Bottom-right, close to the camera: bubble left of the head; slides off to the right. Skip and the task
            // strip stay put while Coalesce comes and goes.
            bool map = layout == TutorialDialogueLayout.StageMap;
            float right = map ? StageMapRight : GameplayRight;
            Anchor(speakerRig, new Vector2(1f, 0f), new Vector2(SpriteOverhang, -SpriteSunk), new Vector2(SpriteSize, SpriteSize));
            Anchor(bubble, new Vector2(1f, 0f), new Vector2(-BubbleRight, BubbleBottom), new Vector2(BubbleWidth, BubbleHeight));
            tail.anchorMin = tail.anchorMax = new Vector2(1f, 0f);
            tail.anchoredPosition = new Vector2(0f, TailY);
            Anchor(skipRect, new Vector2(1f, 1f), new Vector2(-right, -(map ? StageMapSkipTop : GameplaySkipTop)), new Vector2(176f, 32f));
            Anchor(taskStripRect, new Vector2(1f, 0f), new Vector2(-right, map ? StageMapTaskBottom : GameplayTaskBottom), new Vector2(TaskStripWidth, TaskStripHeight));
            taskStripHome = taskStripRect.anchoredPosition;
            slideSign = 1f;
            slideDistance = BubbleRight + BubbleWidth + 60f;
        }

        // ─────────────────────────────────────────────────────
        //  UI helpers
        // ─────────────────────────────────────────────────────

        /// <summary>Anchors and pivots a rect at the same screen corner, offset from it.</summary>
        private static void Anchor(RectTransform rect, Vector2 corner, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            UIFontHelper.Apply(text);
            text.text = content;
            text.fontSize = size;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Color labelColor, Color plateColor)
        {
            Image plate = CreateImage(parent, name, plateColor);
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var outline = plate.gameObject.AddComponent<Outline>();
            outline.effectColor = PhosphorDim;
            outline.effectDistance = new Vector2(1f, -1f);
            Text text = CreateText(plate.rectTransform, "Label", label, 13, TextAnchor.MiddleCenter, labelColor);
            Stretch(text.rectTransform);
            return button;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Four L-shaped terminal corner brackets; a negative inset pushes them outside the rect.</summary>
        private static void AddCornerBrackets(RectTransform target, float length, float thickness, Color color, float inset)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var inward = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
                AddBracketBar(target, corner, inward * inset, new Vector2(length, thickness), color);
                AddBracketBar(target, corner, inward * inset, new Vector2(thickness, length), color);
            }
        }

        private static void AddBracketBar(RectTransform parent, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            Image bar = CreateImage(parent, "CornerBracket", color);
            bar.raycastTarget = false;
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = rect.anchorMax = corner;
            rect.pivot = corner;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
        }
    }
}
