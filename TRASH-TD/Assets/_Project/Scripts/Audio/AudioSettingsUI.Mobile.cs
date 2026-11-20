using System;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;
using static TrashTD.UI.UiKit;

namespace TrashTD.Audio
{
    // Settings > Display on a phone: UI size, frame rate and picture quality instead of the desktop SCREEN section.
    // These rows are added in code when the panel is wired (not part of the baked layout), so the baked prefab is unchanged.
    public partial class AudioSettingsUI
    {
        private const float MobileBandBottom = 290f;   // the desktop SCREEN rows end at y = 286; the CRT STYLE header starts at 298
        private const float MobileCrtShiftDown = 40f;  // room for a third row and a hint line above the CRT section

        private readonly Button[] uiSizeButtons = new Button[3];
        private readonly Button[] frameRateButtons = new Button[3];
        private readonly Button[] pictureButtons = new Button[3];

        /// <summary>
        /// Phones and tablets always fill their screen, so the desktop SCREEN section (resolution, fullscreen or windowed)
        /// is hidden and replaced by what matters on a phone: UI SIZE, FRAME RATE and PICTURE. The CRT section moves down.
        /// </summary>
        private void BuildMobileDisplayRows()
        {
            GameObject displayPage = pages[(int)Tab.Display];
            if (displayPage == null) return;

            foreach (Transform child in displayPage.transform)
            {
                RectTransform rect = child as RectTransform;
                if (rect == null) continue;
                float y = -rect.anchoredPosition.y;
                if (y < MobileBandBottom) child.gameObject.SetActive(false);
                else rect.anchoredPosition += Vector2.down * MobileCrtShiftDown;
            }

            Transform previousRoot = pageRoot;
            pageRoot = displayPage.transform;
            CreateSectionHeader("TOUCH AND BATTERY", 150f);

            CreateRowLabel("UI SIZE", 196f);
            string[] sizeLabels = { "SMALL", "NORMAL", "LARGE" };
            for (int i = 0; i < 3; i++)
            {
                UiSize size = (UiSize)i;
                uiSizeButtons[i] = CreateSegmentButton("UiSize" + sizeLabels[i] + "Button", sizeLabels[i], i, 196f);
                Bind(uiSizeButtons[i], () => SetUiSize(size));
            }

            CreateRowLabel("FRAME RATE", 244f);
            for (int i = 0; i < 3; i++)
            {
                int fps = MobileSettings.FrameRates[i];
                frameRateButtons[i] = CreateSegmentButton("Fps" + fps + "Button", fps + " FPS", i, 244f);
                Bind(frameRateButtons[i], () => SetFrameRate(fps));
            }

            CreateRowLabel("PICTURE", 292f);
            string[] pictureLabels = { "FULL", "75%", "50%" };
            for (int i = 0; i < 3; i++)
            {
                PictureQuality quality = (PictureQuality)i;
                pictureButtons[i] = CreateSegmentButton("Picture" + i + "Button", pictureLabels[i], i, 292f);
                Bind(pictureButtons[i], () => SetPicture(quality));
            }

            Text hint = CreateText(pageRoot, "A lower frame rate and a smaller picture save battery and keep the phone cool.", 10,
                TextAnchor.MiddleLeft, HintColor);
            PlaceTopLeft(hint.rectTransform, ControlX, 318f, CardWidth - Padding - ControlX, 16f);

            pageRoot = previousRoot;
            RefreshMobileControls();
        }

        private void SetUiSize(UiSize size)
        {
            MobileSettings.UiSizeSetting = size;
            RefreshMobileControls();
        }

        private void SetFrameRate(int fps)
        {
            MobileSettings.FrameRate = fps;
            RefreshMobileControls();
        }

        private void SetPicture(PictureQuality quality)
        {
            MobileSettings.Picture = quality;
            RefreshMobileControls();
        }

        /// <summary>Lights the chosen option in each of the phone rows (nothing to do off a phone).</summary>
        private void RefreshMobileControls()
        {
            if (uiSizeButtons[0] == null) return;

            for (int i = 0; i < 3; i++)
            {
                SetToggleVisual(uiSizeButtons[i], (int)MobileSettings.UiSizeSetting == i);
                SetToggleVisual(frameRateButtons[i], MobileSettings.FrameRate == MobileSettings.FrameRates[i]);
                SetToggleVisual(pictureButtons[i], (int)MobileSettings.Picture == i);
            }
        }
    }
}
