using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;
using TrashTD.Core.GameLoop;
using static TrashTD.UI.UiKit;

namespace TrashTD.Audio
{
    // CRT style and volume rows, the toggle look, show and hide, and the UI building helpers.
    public partial class AudioSettingsUI
    {
        private Button CreateStyleButton(string objectName, string label, int index, float centerY)
        {
            Button button = CreateTerminalButton(pageRoot, objectName, label, 13);
            PlaceTopLeft(button.GetComponent<RectTransform>(), ControlX + index * (SegmentWidth + 12f), centerY - 20f, SegmentWidth, 40f);
            return button;
        }

        private void SetCrtStyle(CrtStyle style)
        {
            GameGraphicsSettings.CrtStyle = style;
            RefreshGraphicsControls();
        }

        private void RefreshGraphicsControls()
        {
            CrtStyle style = GameGraphicsSettings.CrtStyle;
            SetToggleVisual(fullTvButton, style == CrtStyle.FullTv);
            SetToggleVisual(classicButton, style == CrtStyle.Classic);
            SetToggleVisual(offButton, style == CrtStyle.Off);

            if (crtStyleHint != null)
            {
                crtStyleHint.text = style switch
                {
                    CrtStyle.Classic => "A lighter, crisper old TV: soft scanlines and a faint RGB tint.",
                    CrtStyle.Off => "No CRT effect. Brightness still applies.",
                    _ => "A full old TV: strong scanlines, RGB phosphors and glow."
                };
            }

            RefreshValueTexts();
        }

        private void RefreshValueTexts()
        {
            if (musicValueText != null) musicValueText.text = Percent(musicSlider.value);
            if (sfxValueText != null) sfxValueText.text = Percent(sfxSlider.value);
            if (brightnessValueText != null) brightnessValueText.text = Percent(GameGraphicsSettings.Brightness);
        }

        private static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";

        private static void SetToggleVisual(Button button, bool selected)
        {
            if (button == null) return;
            MenuButtonFeedback feedback = button.GetComponent<MenuButtonFeedback>();
            Color fill = selected ? AccentColor : TerminalButton;
            if (feedback != null)
            {
                feedback.SetBaseColor(fill);
                feedback.glowAlways = selected;
            }
            else
            {
                button.GetComponent<Image>().color = fill;
            }

            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = selected ? SelectedLabel : LabelColor;
        }

        private void CreateSectionHeader(string label, float y)
        {
            Text header = CreateText(pageRoot, "// " + label, 12, TextAnchor.MiddleLeft, AccentColor);
            PlaceTopLeft(header.rectTransform, Padding, y - 12f, 400f, 24f);

            float lineX = Padding + header.preferredWidth + 16f;
            Image line = CreateImage(pageRoot, label + "Line", new Color(LineColor.r, LineColor.g, LineColor.b, 0.3f));
            PlaceTopLeft(line.rectTransform, lineX, y - 1f, CardWidth - Padding - lineX, 2f);
        }

        private void CreateRowLabel(string label, float centerY)
        {
            Text text = CreateText(pageRoot, label, LabelSize, TextAnchor.MiddleLeft, LabelColor);
            PlaceTopLeft(text.rectTransform, Padding + 4f, centerY - 15f, LabelWidth, 30f);
        }

        /// <summary>Label, slider (thin track, bright handle) and a percentage readout on one row.</summary>
        private Slider CreateSliderRow(string label, float centerY, float min, float max, float value, out Text valueText)
        {
            CreateRowLabel(label, centerY);

            GameObject sliderObject = new GameObject(label + "Slider", typeof(RectTransform), typeof(Image), typeof(Slider));
            sliderObject.transform.SetParent(pageRoot, false);
            PlaceTopLeft(sliderObject.GetComponent<RectTransform>(), ControlX, centerY - 14f, ControlWidth, 28f);

            // Invisible hit area over the whole row height, so pressing anywhere on the bar jumps the handle
            // there (Slider moves to the pointer on press) and dragging continues from that point.
            Image hitArea = sliderObject.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;

            // The slider only drives x anchors, so the y anchors set here give the track and fill their height
            Image track = CreateImage(sliderObject.transform, "Track", TrackColor);
            track.rectTransform.anchorMin = new Vector2(0f, 0.3f);
            track.rectTransform.anchorMax = new Vector2(1f, 0.7f);
            track.rectTransform.offsetMin = Vector2.zero;
            track.rectTransform.offsetMax = Vector2.zero;
            Outline trackOutline = track.gameObject.AddComponent<Outline>();
            trackOutline.effectColor = new Color(LineColor.r, LineColor.g, LineColor.b, 0.35f);
            trackOutline.effectDistance = new Vector2(1f, -1f);

            Image fill = CreateImage(sliderObject.transform, "Fill", AccentColor);
            fill.rectTransform.anchorMin = new Vector2(0f, 0.3f);
            fill.rectTransform.anchorMax = new Vector2(1f, 0.7f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            Image handle = CreateImage(sliderObject.transform, "Handle", AccentBright);
            handle.raycastTarget = true;
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(14f, 0f);
            Outline handleOutline = handle.gameObject.AddComponent<Outline>();
            handleOutline.effectColor = new Color(0f, 0f, 0f, 0.6f);
            handleOutline.effectDistance = new Vector2(2f, -2f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(value);

            valueText = CreateText(pageRoot, Percent(value), LabelSize, TextAnchor.MiddleRight, AccentBright);
            valueText.gameObject.name = label + "Value";
            PlaceTopLeft(valueText.rectTransform, CardWidth - Padding - ValueWidth, centerY - 15f, ValueWidth, 30f);
            return slider;
        }

        private void Show()
        {
            if (panel == null) return;
            musicSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.MusicVolume : musicSlider.value);
            sfxSlider.SetValueWithoutNotify(AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : sfxSlider.value);
            brightnessSlider.SetValueWithoutNotify(GameGraphicsSettings.Brightness);
            RefreshGraphicsControls();
            RefreshControls();
            RefreshGameplay();
            SyncResolutions();
            SelectTab(lastTab);

            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            showTime = 0f;
            panelGroup.alpha = 0f;
            card.localScale = Vector3.one * 0.94f;
        }

        private void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>The button that opens the panel; the creating screen restyles and positions it.</summary>
        private static Button CreateButton(Transform parent, string name, string label, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<RectTransform>().sizeDelta = size;
            var image = buttonObject.GetComponent<Image>();
            var sprite = Resources.Load<Sprite>("UI/buttons/btn_dark_normal");
            if (sprite == null)
            {
                var all = Resources.LoadAll<Sprite>("UI/buttons/btn_dark_normal");
                if (all != null && all.Length > 0) sprite = all[0];
            }
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.color = ButtonColor;
            }
            Text text = CreateText(buttonObject.transform, label, 16, TextAnchor.MiddleCenter, Color.white);
            text.fontStyle = FontStyle.Bold;
            SetStretch(text.rectTransform);
            return buttonObject.GetComponent<Button>();
        }

        /// <summary>Flat terminal button: dark green fill, centred label (MenuButtonFeedback adds hover/glow).</summary>
        private static Button CreateTerminalButton(Transform parent, string name, string label, int fontSize)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = TerminalButton;
            Text text = CreateText(buttonObject.transform, label, fontSize, TextAnchor.MiddleCenter, LabelColor);
            SetStretch(text.rectTransform);
            return buttonObject.GetComponent<Button>();
        }

        private static void AttachFeedback(Button button, SfxId clickSound)
        {
            button.transition = Selectable.Transition.None;
            MenuButtonFeedback feedback = button.gameObject.AddComponent<MenuButtonFeedback>();
            feedback.hoverScale = 1.04f;
            feedback.respondToFocus = false;
            feedback.Hovered = () => AudioManager.Instance?.PlaySfx(SfxId.UiHover, 0.7f, UnityEngine.Random.Range(0.97f, 1.03f));
            feedback.Clicked = () => AudioManager.Instance?.PlaySfx(clickSound);

            Image image = button.GetComponent<Image>();
            if (image == null) return;

            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.useGraphicAlpha = false;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.effectColor = new Color(AccentBright.r, AccentBright.g, AccentBright.b, 0f);
            feedback.glowOutline = outline;
            feedback.glowColor = new Color(AccentBright.r, AccentBright.g, AccentBright.b, 0.9f);
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

        private static void SetPosition(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
