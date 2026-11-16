using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TrashTD.Audio;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Shared UI building helpers.
    public partial class CardDraftOverlayUI
    {
        /// <summary>
        /// Loads a UI-kit sprite (single or multiple sprite mode) with Point filtering, adding a
        /// 9-slice border when the imported sprite has none.
        /// </summary>
        private static Sprite LoadUiSprite(string path, Vector4 border)
        {
            Sprite source = Resources.Load<Sprite>(path);
            if (source == null)
            {
                Sprite[] all = Resources.LoadAll<Sprite>(path);
                if (all != null && all.Length > 0) source = all[0];
            }
            if (source == null || source.texture == null) return source;

            source.texture.filterMode = FilterMode.Point;
            if (border == Vector4.zero || source.border != Vector4.zero) return source;

            return Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
        }

        private Chip CreateChip(Transform parent, string name, float x, float y, float width, float height, bool showIcon = false)
        {
            var chip = new Chip();

            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            PlaceTopLeft(obj.transform, x, y, width, height);

            GameplayHUDUI.ApplyPanelContainer(obj, GameplayHUDUI.PanelContainerType.Dark);
            chip.bg = obj.GetComponent<Image>();
            chip.bg.raycastTarget = false;

            chip.outline = obj.AddComponent<Outline>();
            chip.outline.effectDistance = new Vector2(1.5f, -1.5f);
            chip.outline.effectColor = new Color(1f, 1f, 1f, 0.15f);

            if (showIcon)
            {
                var iconObject = new GameObject("ClassIcon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(obj.transform, false);
                chip.icon = iconObject.GetComponent<Image>();
                chip.icon.preserveAspect = true;
                chip.icon.raycastTarget = false;
                RectTransform iconRect = chip.icon.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(8f, 0f);
                iconRect.sizeDelta = new Vector2(22f, 22f);
            }

            chip.text = MakeText(obj.transform, "Label", "", 13, TextAnchor.MiddleCenter, Color.white);
            chip.text.fontStyle = FontStyle.Bold;
            chip.text.resizeTextForBestFit = true;
            chip.text.resizeTextMinSize = 9;
            chip.text.resizeTextMaxSize = 13;
            if (showIcon)
            {
                RectTransform textRect = chip.text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(32f, 0f);
                textRect.offsetMax = new Vector2(-4f, 0f);
            }
            else
            {
                StretchFill(chip.text.rectTransform);
            }

            return chip;
        }

        private Text CreateStatTile(Transform parent, string label, Color accent, float x, float y, float width, float height)
        {
            var tile = new GameObject($"Stat_{label}", typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(parent, false);
            PlaceTopLeft(tile.transform, x, y, width, height);

            GameplayHUDUI.ApplyPanelContainer(tile, GameplayHUDUI.PanelContainerType.Dark);
            var tileImage = tile.GetComponent<Image>();
            tileImage.raycastTarget = false;

            // Colored accent bar on the left edge inside the panel border
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(tile.transform, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(4f, -16f);
            barRect.anchoredPosition = new Vector2(10f, 0f);
            var barImage = bar.GetComponent<Image>();
            barImage.color = accent;
            barImage.raycastTarget = false;

            var labelText = MakeText(tile.transform, "Label", label, 11, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            labelText.fontStyle = FontStyle.Bold;
            PlaceTopLeft(labelText.transform, 22f, 6f, width - 30f, 18f);

            var valueText = MakeText(tile.transform, "Value", "0", 26, TextAnchor.MiddleLeft, STAT_VALUE_COLOR);
            valueText.fontStyle = FontStyle.Bold;
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 14;
            valueText.resizeTextMaxSize = 26;
            PlaceTopLeft(valueText.transform, 22f, 22f, width - 30f, height - 26f);

            return valueText;
        }

        private static void CreateDivider(Transform parent, float top, float margin)
        {
            var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(parent, false);
            StretchTopRT(divider.transform, margin, margin, top, 2f);
            var image = divider.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);
            image.raycastTarget = false;
        }

        // ============================
        // UI Helpers
        // ============================

        private static Text MakeText(Transform parent, string name, string value,
            int fontSize, TextAnchor alignment, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var text = obj.GetComponent<Text>();
            text.text = value;
            text.font = UIFontHelper.GetPixelFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button MakeButton(Transform parent, string name, string label,
            Vector2 size, GameplayHUDUI.ButtonContainerType containerType, string iconName = null)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<RectTransform>().sizeDelta = size;

            var button = obj.GetComponent<Button>();
            GameplayHUDUI.ApplyButtonContainer(button, containerType);

            float iconOffset = 0f;
            if (!string.IsNullOrEmpty(iconName))
            {
                var iconSprite = GameplayHUDUI.LoadIconSprite(iconName);
                if (iconSprite != null)
                {
                    var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                    iconObj.transform.SetParent(obj.transform, false);
                    var iconImg = iconObj.GetComponent<Image>();
                    iconImg.sprite = iconSprite;
                    iconImg.color = Color.white;
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;

                    var iconRect = iconObj.GetComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0f, 0.5f);
                    iconRect.anchorMax = new Vector2(0f, 0.5f);
                    iconRect.pivot = new Vector2(0f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(16f, 0f);
                    iconRect.sizeDelta = new Vector2(24f, 24f);
                    iconOffset = 28f;
                }
            }

            var text = MakeText(obj.transform, "Label", label, 16, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(iconOffset, 0f);
            textRect.offsetMax = Vector2.zero;
            text.raycastTarget = false;

            return button;
        }

        private static Button MakeButton(Transform parent, string name, string label,
            Vector2 size, Color bgColor)
        {
            var container = (bgColor.r > 0.4f && bgColor.g < 0.3f && bgColor.b < 0.3f) ? GameplayHUDUI.ButtonContainerType.Danger :
                            (bgColor.g > 0.4f && bgColor.g > bgColor.r) ? GameplayHUDUI.ButtonContainerType.Primary :
                            GameplayHUDUI.ButtonContainerType.Dark;
            return MakeButton(parent, name, label, size, container);
        }

        private static void PositionRT(Component comp, Vector2 anchoredPos, Vector2 anchor, Vector2 size)
        {
            var rt = comp.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
        }

        /// <summary>Full-width strip hanging from the top edge of its parent.</summary>
        private static void StretchTopRT(Component comp, float left, float right, float top, float height)
        {
            var rt = comp.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -(top + height));
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Fixed-size box positioned from the top-left corner of its parent.</summary>
        private static void PlaceTopLeft(Component comp, float x, float y, float width, float height)
        {
            var rt = comp.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
        }

        private static string GetOperatorDescription(OperatorData opData)
        {
            if (opData == null) return "";
            if (opData.roleTags != null && opData.roleTags.Length > 0)
            {
                return string.Join(" / ", opData.roleTags);
            }

            return opData.operatorClass switch
            {
                OperatorClass.Guard => "Melee Combatant / Physical DPS",
                OperatorClass.Defender => "Heavy Defense / Blocks 3",
                OperatorClass.Sniper => "Ranged Sniper / High Range",
                OperatorClass.Caster => "Arts Damage / Magic Attacks",
                OperatorClass.Medic => "Combat Support / Restores HP",
                OperatorClass.Specialist => "Specialist / Fast Redeploy & Mimic",
                _ => "Operator"
            };
        }
    }
}
