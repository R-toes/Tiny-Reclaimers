using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Core.Grid;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Shared button and panel styling, audio feedback and small UI helpers.
    public partial class GameplayHUDUI
    {
        private MenuButtonFeedback StyleButton(Button button, Color background, Color glow, float hoverScale, System.Action clicked = null)
        {
            ButtonContainerType container = ResolveContainerType(background);
            return StyleButton(button, container, glow, hoverScale, clicked);
        }

        private MenuButtonFeedback StyleButton(Button button, ButtonContainerType containerType, Color glow, float hoverScale = 1.05f, System.Action clicked = null)
        {
            ApplyButtonContainer(button, containerType);
            return AttachFeedback(button, glow, hoverScale, clicked);
        }

        private static ButtonContainerType ResolveContainerType(Color color)
        {
            if (color == ReadyColor || color == AccentColor || (color.g > 0.4f && color.g > color.r && color.g > color.b))
                return ButtonContainerType.Primary;
            if (color == LifeColor || (color.r > 0.35f && color.g < 0.25f && color.b < 0.25f))
                return ButtonContainerType.Danger;
            if (color == PrepColor)
                return ButtonContainerType.Secondary;
            if (color == DraftColor)
                return ButtonContainerType.Warning;
            return ButtonContainerType.Dark;
        }

        public static Sprite GetButtonContainerSprite(ButtonContainerType type)
        {
            if (ButtonContainerSprites.TryGetValue(type, out Sprite cached) && cached != null)
                return cached;

            string path = type switch
            {
                ButtonContainerType.Primary => "UI/buttons/btn_primary_normal",
                ButtonContainerType.Secondary => "UI/buttons/btn_secondary_normal",
                ButtonContainerType.Dark => "UI/buttons/btn_dark_normal",
                ButtonContainerType.Danger => "UI/buttons/btn_danger_normal",
                ButtonContainerType.Warning => "UI/buttons/btn_warning_normal",
                ButtonContainerType.IconDark => "UI/buttons/btn_icon_dark_normal",
                ButtonContainerType.IconDanger => "UI/buttons/btn_icon_danger_normal",
                ButtonContainerType.IconPrimary => "UI/buttons/btn_icon_primary_normal",
                ButtonContainerType.IconSecondary => "UI/buttons/btn_icon_secondary_normal",
                _ => "UI/buttons/btn_dark_normal"
            };

            Sprite sprite = LoadButtonSprite(path);
            if (sprite != null)
            {
                ButtonContainerSprites[type] = sprite;
            }
            return sprite;
        }

        private static Sprite LoadButtonSprite(string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>(path);
                if (all != null && all.Length > 0) sprite = all[0];
            }

            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(5f, 5f, 5f, 5f));
                }
            }
            else if (sprite.border == Vector4.zero && sprite.texture != null)
            {
                sprite = Sprite.Create(sprite.texture, sprite.rect, sprite.pivot, 100f, 0, SpriteMeshType.FullRect, new Vector4(5f, 5f, 5f, 5f));
            }

            return sprite;
        }

        public static void ApplyButtonContainer(Button button, ButtonContainerType type)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image == null) return;

            Sprite sprite = GetButtonContainerSprite(type);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }

            var feedback = button.GetComponent<MenuButtonFeedback>();
            if (feedback != null)
            {
                feedback.SetBaseColor(Color.white);
            }
        }

        public static Sprite GetPanelContainerSprite(PanelContainerType type)
        {
            if (PanelContainerSprites.TryGetValue(type, out Sprite cached) && cached != null)
                return cached;

            string filename = type switch
            {
                PanelContainerType.Dark => "panel_dark",
                PanelContainerType.Metal => "panel_metal",
                PanelContainerType.Glass => "panel_glass",
                PanelContainerType.Modal => "panel_modal",
                PanelContainerType.Rust => "panel_rust",
                PanelContainerType.Teal => "panel_teal",
                PanelContainerType.Tooltip => "panel_tooltip",
                _ => "panel_dark"
            };

            Vector4 border = type switch
            {
                PanelContainerType.Metal => new Vector4(16f, 16f, 16f, 16f),
                PanelContainerType.Rust => new Vector4(16f, 16f, 16f, 16f),
                PanelContainerType.Modal => new Vector4(12f, 14f, 12f, 14f),
                PanelContainerType.Teal => new Vector4(12f, 12f, 12f, 12f),
                PanelContainerType.Tooltip => new Vector4(10f, 10f, 10f, 10f),
                PanelContainerType.Glass => new Vector4(10f, 10f, 10f, 10f),
                _ => new Vector4(12f, 12f, 12f, 12f)
            };

            Sprite sprite = LoadPanelSprite("UI/panels/" + filename, border);
            if (sprite != null)
            {
                PanelContainerSprites[type] = sprite;
            }
            return sprite;
        }

        private static Sprite LoadPanelSprite(string path, Vector4 border)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>(path);
                if (all != null && all.Length > 0) sprite = all[0];
            }

            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                }
            }
            else if (sprite.border == Vector4.zero && sprite.texture != null)
            {
                sprite = Sprite.Create(sprite.texture, sprite.rect, sprite.pivot, 100f, 0, SpriteMeshType.FullRect, border);
            }

            return sprite;
        }

        private static readonly Dictionary<string, Sprite> WholeTextureSprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// Loads a UI texture from Resources as one Point-filtered sprite covering the whole image, ignoring any
        /// slicing in its importer, with an optional 9-slice border in source pixels. Cached; null if missing.
        /// </summary>
        private static Sprite LoadWholeTextureSprite(string path, Vector4 border = default)
        {
            if (WholeTextureSprites.TryGetValue(path, out Sprite cached) && cached != null)
                return cached;

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) return null;

            tex.filterMode = FilterMode.Point;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            WholeTextureSprites[path] = sprite;
            return sprite;
        }

        private static Sprite hazardBarSprite;

        private static Sprite GetHazardBarSprite()
        {
            if (hazardBarSprite != null) return hazardBarSprite;

            hazardBarSprite = Resources.Load<Sprite>("UI/decor/hazard_bar");
            if (hazardBarSprite == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>("UI/decor/hazard_bar");
                if (all != null && all.Length > 0) hazardBarSprite = all[0];
            }

            if (hazardBarSprite == null)
            {
                var tex = Resources.Load<Texture2D>("UI/decor/hazard_bar");
                if (tex != null)
                {
                    tex.filterMode = FilterMode.Point;
                    hazardBarSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
            }

            return hazardBarSprite;
        }

        private static void AttachHazardDecor(Transform card, float insetX = 16f, float insetY = 8f, float barHeight = 20f)
        {
            if (card == null) return;
            if (card.Find("TopHazardBar") != null || card.Find("BottomHazardBar") != null) return;

            Sprite sprite = GetHazardBarSprite();
            if (sprite == null) return;

            // Top Hazard Bar
            var topBar = new GameObject("TopHazardBar", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(card, false);
            var topRect = topBar.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 1f);
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.pivot = new Vector2(0.5f, 1f);
            topRect.anchoredPosition = new Vector2(0f, -insetY);
            topRect.sizeDelta = new Vector2(-insetX * 2f, barHeight);
            var topImage = topBar.GetComponent<Image>();
            topImage.sprite = sprite;
            topImage.type = Image.Type.Tiled;
            topImage.color = Color.white;
            topImage.raycastTarget = false;

            // Bottom Hazard Bar
            var bottomBar = new GameObject("BottomHazardBar", typeof(RectTransform), typeof(Image));
            bottomBar.transform.SetParent(card, false);
            var bottomRect = bottomBar.GetComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0f, 0f);
            bottomRect.anchorMax = new Vector2(1f, 0f);
            bottomRect.pivot = new Vector2(0.5f, 0f);
            bottomRect.anchoredPosition = new Vector2(0f, insetY);
            bottomRect.sizeDelta = new Vector2(-insetX * 2f, barHeight);
            var bottomImage = bottomBar.GetComponent<Image>();
            bottomImage.sprite = sprite;
            bottomImage.type = Image.Type.Tiled;
            bottomImage.color = Color.white;
            bottomImage.raycastTarget = false;
        }

        private static void ApplyModalCard(GameObject card, float insetX = 16f, float insetY = 8f, float barHeight = 20f)
        {
            if (card == null) return;
            ApplyPanelContainer(card, PanelContainerType.Metal);
            AttachHazardDecor(card.transform, insetX, insetY, barHeight);
        }

        public static void ApplyPanelContainer(GameObject target, PanelContainerType type, Color? tint = null)
        {
            if (target == null) return;
            if (type == PanelContainerType.Modal)
            {
                ApplyModalCard(target);
                return;
            }

            var image = target.GetComponent<Image>();
            if (image == null) return;

            Sprite sprite = GetPanelContainerSprite(type);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = tint ?? Color.white;
            }
        }

        private static Button CreateButton(Transform parent, string objectName, string label, Vector2 size, ButtonContainerType containerType = ButtonContainerType.Dark)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var button = buttonObject.GetComponent<Button>();
            buttonObject.GetComponent<RectTransform>().sizeDelta = size;

            ApplyButtonContainer(button, containerType);

            var text = CreateText(buttonObject.transform, "Label", label, 22, TextAnchor.MiddleCenter);
            text.raycastTarget = false;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private MenuButtonFeedback AttachFeedback(Button button, Color glow, float hoverScale = 1.05f, System.Action clicked = null)
        {
            if (button == null) return null;

            // The feedback component drives the visuals, so disable Unity's color tint transition.
            button.transition = Selectable.Transition.None;

            var feedback = button.GetComponent<MenuButtonFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<MenuButtonFeedback>();

            feedback.hoverScale = hoverScale;
            feedback.respondToFocus = false; // clicked HUD buttons stay "selected"; don't keep them lit
            feedback.Hovered = PlayHover;
            if (clicked != null) feedback.Clicked = clicked;
            else feedback.Clicked = PlayClick;

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

        private void PlayHover() { PlaySfx(hoverClip, Random.Range(0.97f, 1.03f)); }
        private void PlayClick() { PlaySfx(clickClip, 1f); }

        private void PlaySfx(AudioClip clip, float pitch)
        {
            AudioManager.Instance?.PlaySfx(clip, sfxVolume, pitch);
        }

        // ============================
        // UI Helpers
        // ============================

        private static Text CreateText(Transform parent, string objectName, string value, int fontSize, TextAnchor alignment)
        {
            var textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = UIFontHelper.GetPixelFont();
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            return text;
        }

        private static void SetPosition(RectTransform rect, Vector2 position, Vector2 anchor, Vector2? size = null, Vector2? pivot = null)
        {
            if (pivot.HasValue) rect.pivot = pivot.Value;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            if (size.HasValue) rect.sizeDelta = size.Value;
        }

        private static Image CreateIconImage(Transform parent, string objectName, Sprite sprite, Color color, Vector2 position, Vector2 anchor, Vector2 size, Vector2 pivot)
        {
            var iconObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(parent, false);
            Image image = iconObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetPosition(image.rectTransform, position, anchor, size, pivot);
            return image;
        }

        public static Sprite LoadIconSprite(string iconName)
        {
            var sprite = Resources.Load<Sprite>("UI/icons/" + iconName);
            if (sprite != null) return sprite;

            var tex = Resources.Load<Texture2D>("UI/icons/" + iconName);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height));
            }

            return null;
        }

        private static Sprite GetPauseIconSprite()
        {
            if (pauseIconSprite != null) return pauseIconSprite;
            pauseIconSprite = LoadIconSprite("icon_pause");
            return pauseIconSprite;
        }

        private static Sprite GetExitIconSprite()
        {
            if (exitIconSprite != null) return exitIconSprite;
            exitIconSprite = LoadIconSprite("icon_exit");
            return exitIconSprite;
        }

        private static Sprite CreateTrashIconSprite()
        {
            if (trashIconSprite != null) return trashIconSprite;

            trashIconSprite = LoadIconSprite("icon_discard");
            if (trashIconSprite != null) return trashIconSprite;

            const int width = 24;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            void Pixel(int x, int y)
            {
                if (x >= 0 && x < width && y >= 0 && y < height)
                    pixels[y * width + x] = Color.white;
            }

            // Main bin body: wide rim, slightly tapered sides, and a solid base.
            for (int y = 4; y <= 17; y++)
            {
                int left = y <= 6 ? 4 : 5;
                int right = y <= 6 ? 19 : 18;
                for (int x = left; x <= right; x++) Pixel(x, y);
            }
            for (int x = 6; x <= 17; x++) Pixel(x, 3);

            // Lid and handle at the top of the can.
            for (int x = 3; x <= 20; x++) Pixel(x, 18);
            for (int x = 7; x <= 16; x++) Pixel(x, 20);
            for (int x = 9; x <= 14; x++) Pixel(x, 21);

            // Three vertical cut-outs make the bin read clearly at small size.
            for (int y = 7; y <= 15; y++)
            {
                pixels[y * width + 8] = Color.clear;
                pixels[y * width + 11] = Color.clear;
                pixels[y * width + 14] = Color.clear;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 24f);
        }

        /// <summary>Transparent center, opaque edges — tinted red for the life-lost flash.</summary>
        private static Sprite CreateVignetteSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float ux = x / (size - 1f) * 2f - 1f;
                    float uy = y / (size - 1f) * 2f - 1f;
                    float d = Mathf.Sqrt(ux * ux + uy * uy) / 1.41421f;
                    float a = Mathf.SmoothStep(0.35f, 1f, d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
