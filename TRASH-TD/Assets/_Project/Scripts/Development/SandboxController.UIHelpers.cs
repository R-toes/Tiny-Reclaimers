using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Combat;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Development
{
    // Shared sandbox UI building helpers.
    public partial class SandboxController
    {
        // ============================
        // UI building blocks
        // ============================

        private static Sprite GetEnemySprite(EnemyData enemy)
        {
            if (enemy == null) return null;
            if (enemy.sprite != null) return enemy.sprite;
            if (enemy.animationFrames != null && enemy.animationFrames.Length > 0 && enemy.animationFrames[0] != null)
                return enemy.animationFrames[0];
            return GameplayHUDUI.GetEnemyClassIconSprite(enemy);
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        private static GameObject MakeObject(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            if (parent != null) obj.transform.SetParent(parent, false);
            return obj;
        }

        private static Image MakeImage(Transform parent, string name, Color color)
        {
            Image image = MakeObject(name, parent).AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>The kit's dark panel frame, tinted; blocks clicks to the map under it.</summary>
        private static Image MakePanelBackground(GameObject obj, Color tint)
        {
            Image image = obj.AddComponent<Image>();
            image.color = tint;
            GameplayHUDUI.ApplyPanelContainer(obj, GameplayHUDUI.PanelContainerType.Dark, new Color(1f, 1f, 1f, tint.a));
            return image;
        }

        private static void Column(GameObject obj, RectOffset padding, float spacing)
        {
            VerticalLayoutGroup layout = obj.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = obj.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        /// <summary>Horizontal strip; buttons share its width equally unless <see cref="Size"/> gives them a fixed width.</summary>
        private static Transform MakeStrip(Transform parent, float height, float spacing)
        {
            GameObject strip = MakeObject("Strip", parent);
            Size(strip, height);
            HorizontalLayoutGroup layout = strip.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return strip.transform;
        }

        /// <summary>Sets a layout size; -1 leaves a value unset.</summary>
        private static LayoutElement Size(GameObject obj, float height, float width = -1f, float flexibleWidth = -1f)
        {
            LayoutElement element = obj.GetComponent<LayoutElement>();
            if (element == null) element = obj.AddComponent<LayoutElement>();
            if (height >= 0f) element.preferredHeight = height;
            if (width >= 0f) { element.preferredWidth = width; element.minWidth = width; }
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>A scrolling list with a slim scrollbar. Its height is set by <see cref="FitDropdown"/>.</summary>
        private static ScrollArea MakeScrollArea(Transform parent)
        {
            GameObject page = MakeObject("Scroll", parent);
            page.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
            LayoutElement element = page.AddComponent<LayoutElement>();
            element.preferredHeight = 120f;

            GameObject viewport = MakeObject("Viewport", page.transform);
            viewport.AddComponent<RectMask2D>();
            Stretch(viewport.GetComponent<RectTransform>(), 0f, 0f, 8f, 0f);

            GameObject content = MakeObject("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            Column(content, new RectOffset(4, 2, 4, 4), 3f);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image track = MakeImage(page.transform, "Scrollbar", new Color(1f, 1f, 1f, 0.05f));
            track.raycastTarget = true;
            Place(track.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(4f, -8f));
            Image handle = MakeImage(track.transform, "Handle", LineColor);
            handle.raycastTarget = true;
            Stretch(handle.rectTransform, 0f, 0f, 0f, 0f);
            Scrollbar scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

            ScrollRect scroll = page.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            return new ScrollArea { page = page, content = contentRect, element = element };
        }

        private static Transform MakeTileGrid(Transform parent)
        {
            GameObject grid = MakeObject("Grid", parent);
            GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = TileSize;
            layout.spacing = new Vector2(3f, 3f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = GridColumns;
            layout.childAlignment = TextAnchor.UpperCenter;
            return grid.transform;
        }

        /// <summary>
        /// A unit tile: artwork, class icon (top left), a count badge (top right, hidden at 0), name and a subtitle.
        /// </summary>
        private static Button MakeTile(Transform parent, Sprite art, Sprite classIcon, string label, string subtitle, Color subtitleColor,
            out GameObject badge, out Text badgeText)
        {
            Button tile = MakeButton(parent, null, 0, RowColor, TextColor);
            tile.gameObject.name = label;

            Image artwork = MakeImage(tile.transform, "Art", Color.white);
            artwork.sprite = art;
            artwork.preserveAspect = true;
            artwork.enabled = art != null;
            Stretch(artwork.rectTransform, 8f, 8f, 8f, 26f);

            if (classIcon != null)
            {
                Image icon = MakeImage(tile.transform, "Class", Color.white);
                icon.sprite = classIcon;
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, -3f), new Vector2(12f, 12f));
            }

            Text name = MakeText(tile.transform, label, 7, TextColor, TextAnchor.LowerCenter);
            FitText(name, 5);
            Place(name.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(-4f, 10f));
            Text sub = MakeText(tile.transform, subtitle, 7, subtitleColor, TextAnchor.LowerCenter);
            FitText(sub, 5);
            Place(sub.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 3f), new Vector2(-4f, 9f));

            Image badgeImage = MakeImage(tile.transform, "Badge", GoldColor);
            Place(badgeImage.rectTransform, Vector2.one, Vector2.one, Vector2.one, new Vector2(-2f, -2f), new Vector2(24f, 12f));
            badgeText = MakeText(badgeImage.transform, string.Empty, 7, BadgeTextColor, TextAnchor.MiddleCenter);
            FitText(badgeText, 5);
            Stretch(badgeText.rectTransform, 1f, 0f, 1f, 0f);
            badge = badgeImage.gameObject;
            badge.SetActive(false);
            return tile;
        }

        private static InputField MakeSearchField(Transform parent, string placeholder, string value, UnityAction<string> onChanged)
        {
            GameObject obj = MakeObject("Search", parent);
            Image background = obj.AddComponent<Image>();
            background.color = RowColor;
            Size(obj, -1f, -1f, 1f);

            Text text = MakeText(obj.transform, string.Empty, 9, TextColor, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.lineSpacing = 1f;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
            Text hint = MakeText(obj.transform, placeholder, 8, TextDim, TextAnchor.MiddleLeft);
            Stretch(hint.rectTransform, 6f, 2f, 6f, 2f);

            InputField field = obj.AddComponent<InputField>();
            field.targetGraphic = background;
            field.textComponent = text;
            field.placeholder = hint;
            field.lineType = InputField.LineType.SingleLine;
            field.customCaretColor = true;
            field.caretColor = AccentColor;
            field.selectionColor = new Color(AccentFill.r, AccentFill.g, AccentFill.b, 0.6f);
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            field.text = value ?? string.Empty;
            field.onValueChanged.AddListener(onChanged);
            return field;
        }

        private void AddFilterChip(Transform parent, List<Button> chips, Sprite icon, string label, string hint, UnityAction onClick)
        {
            Button chip = MakeButton(parent, label, 7, ControlColor, TextColor);
            if (icon != null)
            {
                Image image = MakeImage(chip.transform, "Icon", Color.white);
                image.sprite = icon;
                image.preserveAspect = true;
                Stretch(image.rectTransform, 2f, 3f, 2f, 3f);
            }
            chip.onClick.AddListener(onClick);
            AddHint(chip, hint);
            chips.Add(chip);
        }

        /// <summary>Icon plus label, wide enough for <paramref name="widestLabel"/> characters.</summary>
        private static Button MakeToolbarButton(Transform parent, Sprite icon, string label, int widestLabel)
        {
            Button button = MakeButton(parent, null, 0, ControlColor, TextColor);
            button.gameObject.name = label;

            float left = 10f;
            if (icon != null)
            {
                Image image = MakeImage(button.transform, "Icon", Color.white);
                image.sprite = icon;
                image.preserveAspect = true;
                Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f),
                    new Vector2(ToolbarIconSize, ToolbarIconSize));
                left = 8f + ToolbarIconSize + 8f;
            }
            Size(button.gameObject, -1f, left + widestLabel * ToolbarFontSize + 10f, 0f);
            Text text = MakeText(button.transform, label, ToolbarFontSize, TextColor, TextAnchor.MiddleLeft);
            FitText(text, 10);
            Stretch(text.rectTransform, left, 0f, 6f, 0f);
            return button;
        }

        private Button MakeToolbarIcon(Transform parent, string iconName, Color baseColor, string hint)
        {
            Button button = MakeIconButton(parent, iconName, baseColor, 0f);
            Size(button.gameObject, -1f, ToolbarHeight - 6f, 0f);
            Stretch(button.transform.Find("Icon").GetComponent<RectTransform>(), 9f, 9f, 9f, 9f);
            AddHint(button, hint);
            return button;
        }

        /// <summary>Small caps label with a hairline under it, separating groups in a list.</summary>
        private static void MakeGroupHeader(Transform parent, string label)
        {
            GameObject header = MakeObject("Group " + label, parent);
            Size(header, 18f);
            Text text = MakeText(header.transform, label, 8, TextDim, TextAnchor.LowerLeft);
            Stretch(text.rectTransform, 0f, 0f, 0f, 4f);
            Image line = MakeImage(header.transform, "Line", LineColor);
            Place(line.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 1f));
        }

        /// <summary>Clickable list row: optional icon, label, and a right-aligned tag.</summary>
        private static Button MakeListRow(Transform parent, Sprite icon, string label, string tag, Color tagColor, Color background)
        {
            Button button = MakeButton(parent, null, 0, background, TextColor);
            button.gameObject.name = label;
            Size(button.gameObject, ListRowHeight);

            float left = 8f;
            if (icon != null)
            {
                Image image = MakeImage(button.transform, "Icon", Color.white);
                image.sprite = icon;
                image.preserveAspect = true;
                Place(image.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(16f, 16f));
                left = 28f;
            }

            const float tagWidth = 70f;
            Text text = MakeText(button.transform, label, 9, TextColor, TextAnchor.MiddleLeft);
            FitText(text, 6);
            Stretch(text.rectTransform, left, 0f, tagWidth + 6f, 0f);

            Text tagText = MakeText(button.transform, tag, 8, tagColor, TextAnchor.MiddleRight);
            FitText(tagText, 6);
            Place(tagText.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(tagWidth, 0f));
            return button;
        }

        private static Button MakeButton(Transform parent, string label, int fontSize, Color baseColor, Color textColor)
        {
            GameObject obj = MakeObject(string.IsNullOrEmpty(label) ? "Button" : label, parent);
            Image image = obj.AddComponent<Image>();
            image.color = Color.white;
            Button button = obj.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            // A selected button would fire again on Enter/Space, which also confirm placements
            button.onClick.AddListener(ClearUiSelection);
            PaintBackground(button, baseColor);
            Size(obj, -1f, -1f, 1f);

            if (!string.IsNullOrEmpty(label))
            {
                Text text = MakeText(obj.transform, label, fontSize, textColor, TextAnchor.MiddleCenter);
                FitText(text, Mathf.Max(5, fontSize - 3));
                Stretch(text.rectTransform, 3f, 0f, 3f, 0f);
            }
            return button;
        }

        private static void ClearUiSelection()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private static Button MakeIconButton(Transform parent, string iconName, Color baseColor, float rotation)
        {
            Button button = MakeButton(parent, null, 0, baseColor, TextColor);
            Size(button.gameObject, -1f, SmallButtonSize, 0f);
            Image icon = MakeImage(button.transform, "Icon", TextColor);
            icon.sprite = GameplayHUDUI.LoadIconSprite(iconName);
            icon.preserveAspect = true;
            Stretch(icon.rectTransform, 4f, 4f, 4f, 4f);
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
            return button;
        }

        /// <summary>Colours a button through its ColorBlock so hover, press and disabled states all derive from one base.</summary>
        private static void PaintBackground(Button button, Color baseColor)
        {
            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = Color.Lerp(baseColor, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(baseColor, Color.black, 0.25f);
            colors.selectedColor = baseColor;
            colors.disabledColor = Color.Lerp(baseColor, PanelTint, 0.6f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            button.colors = colors;
        }

        /// <summary><see cref="PaintBackground"/> plus the label colour (dim when disabled).</summary>
        private static void Paint(Button button, Color baseColor)
        {
            PaintBackground(button, baseColor);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = button.interactable ? TextColor : TextDim;
        }

        /// <summary>Difficulty, amount and filter chips: the selected one is filled with the accent.</summary>
        private static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            PaintBackground(button, selected ? AccentFill : ControlColor);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = selected ? TextColor : TextDim;
        }

        private static Text MakeText(Transform parent, string value, int size, Color color, TextAnchor alignment)
        {
            Text text = MakeObject("Text", parent).AddComponent<Text>();
            text.text = value;
            text.font = UIFontHelper.GetPixelFont();
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.lineSpacing = 1.35f;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Single-line text that shrinks (down to <paramref name="minSize"/>) instead of wrapping or spilling.</summary>
        private static void FitText(Text text, int minSize)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = text.fontSize;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
