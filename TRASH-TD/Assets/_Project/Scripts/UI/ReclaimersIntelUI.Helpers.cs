using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Shared UI building helpers.
    public partial class ReclaimersIntelUI
    {
        private Button CreateHeaderButton(RectTransform card, string objectName, string label, float x, float width)
        {
            Button button = CreateTerminalButton(card, objectName, label, 12);
            PlaceTopLeft(button.GetComponent<RectTransform>(), x, 12f, width, 40f);
            AddCornerBrackets(button.GetComponent<RectTransform>(), 8f, 2f, LineColor);
            AttachFeedback(button, SfxId.UiBack, AccentBright);
            return button;
        }

        private static Button CreateButtonObject(Transform parent, string name, Color fill)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = fill;
            return buttonObject.GetComponent<Button>();
        }

        /// <summary>Flat terminal button: dark green fill, centred label (MenuButtonFeedback adds hover/glow).</summary>
        private static Button CreateTerminalButton(Transform parent, string name, string label, int fontSize)
        {
            Button button = CreateButtonObject(parent, name, TerminalButton);
            Text text = CreateText(button.transform, label, fontSize, TextAnchor.MiddleCenter, LabelColor);
            SetStretch(text.rectTransform);
            return button;
        }

        private static void SetToggleVisual(Button button, bool selected, Color accent, Color idle)
        {
            if (button == null) return;
            MenuButtonFeedback feedback = button.GetComponent<MenuButtonFeedback>();
            Color fill = selected ? accent : idle;
            if (feedback != null)
            {
                feedback.SetBaseColor(fill);
                feedback.glowAlways = selected;
            }
            else
            {
                button.GetComponent<Image>().color = fill;
            }

            foreach (Text label in button.GetComponentsInChildren<Text>())
                label.color = selected ? SelectedLabel : LabelColor;
            foreach (Image image in button.GetComponentsInChildren<Image>())
                if (image.gameObject != button.gameObject && image.name == "Star") image.color = selected ? SelectedLabel : AmberColor;
        }

        private static MenuButtonFeedback AttachFeedback(Button button, SfxId clickSound, Color glow)
        {
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            MenuButtonFeedback feedback = button.gameObject.AddComponent<MenuButtonFeedback>();
            feedback.hoverScale = 1.04f;
            feedback.respondToFocus = false;
            feedback.Hovered = () => AudioManager.Instance?.PlaySfx(SfxId.UiHover, 0.7f, UnityEngine.Random.Range(0.97f, 1.03f));
            feedback.Clicked = () => AudioManager.Instance?.PlaySfx(clickSound);

            Outline outline = button.GetComponent<Outline>();
            if (outline == null)
            {
                outline = button.gameObject.AddComponent<Outline>();
                outline.useGraphicAlpha = false;
                outline.effectDistance = new Vector2(2f, -2f);
                outline.effectColor = new Color(glow.r, glow.g, glow.b, 0f);
            }
            feedback.glowOutline = outline;
            feedback.glowColor = new Color(glow.r, glow.g, glow.b, 0.9f);
            return feedback;
        }

        private static void PlaySfx(SfxId id) => AudioManager.Instance?.PlaySfx(id);

        private static Image CreateHazardStripe(RectTransform parent, string name)
        {
            Image stripe = CreateImage(parent, name, Color.white);
            Sprite sprite = GetHazardSprite();
            if (sprite != null)
            {
                stripe.sprite = sprite;
                stripe.type = Image.Type.Tiled;
            }
            else
            {
                stripe.color = AmberColor;
            }
            return stripe;
        }

        private static Sprite GetHazardSprite()
        {
            if (hazardSprite != null) return hazardSprite;
            hazardSprite = Resources.Load<Sprite>("UI/decor/hazard_bar");
            if (hazardSprite == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>("UI/decor/hazard_bar");
                if (all != null && all.Length > 0) hazardSprite = all[0];
            }
            if (hazardSprite != null && hazardSprite.texture != null) hazardSprite.texture.filterMode = FilterMode.Point;
            return hazardSprite;
        }

        private static Sprite GetTileFrameSprite()
        {
            if (tileFrameSprite != null) return tileFrameSprite;
            Texture2D texture = Resources.Load<Texture2D>("UI/controls/checkbox_off");
            if (texture == null) return null;
            texture.filterMode = FilterMode.Point;
            tileFrameSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(3f, 3f, 3f, 3f));
            return tileFrameSprite;
        }

        private static Sprite GetGridSprite()
        {
            if (gridSprite != null) return gridSprite;
            const int size = 8;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = x == 0 || y == 0 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply();
            gridSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return gridSprite;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }

        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            return rectObject.GetComponent<RectTransform>();
        }

        /// <summary>Four L-shaped terminal corner brackets; a negative inset pushes them outside the rect.</summary>
        private static void AddCornerBrackets(RectTransform target, float length, float thickness, Color color, float inset = 0f)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var inward = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
                CreateBracketBar(target, corner, inward * inset, new Vector2(length, thickness), color);
                CreateBracketBar(target, corner, inward * inset, new Vector2(thickness, length), color);
            }
        }

        private static void CreateBracketBar(RectTransform parent, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            Image bar = CreateImage(parent, "CornerBracket", color);
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = corner;
            rect.anchorMax = corner;
            rect.pivot = corner;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static void SetCentered(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
