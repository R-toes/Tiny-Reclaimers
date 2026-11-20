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
        private const float MobileRowStep = 42f;       // three rows of 40-unit buttons fit above the CRT section without moving it

        private readonly Button[] uiSizeButtons = new Button[3];
        private readonly Button[] frameRateButtons = new Button[3];
        private readonly Button[] pictureButtons = new Button[3];

        /// <summary>
        /// Phones and tablets always fill their screen, so the desktop SCREEN section (resolution, fullscreen or windowed)
        /// is hidden and replaced by what matters on a phone: UI SIZE, FRAME RATE and PICTURE. The CRT section stays where it is
        /// (moving it down would put the brightness slider on top of the CLOSE button).
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
            }

            Transform previousRoot = pageRoot;
            pageRoot = displayPage.transform;
            CreateSectionHeader("TOUCH AND BATTERY", 150f);

            const float row1 = 192f; // buttons span 172-212, 214-254 and 256-296; the CRT STYLE header's text starts at 298
            const float row2 = row1 + MobileRowStep;
            const float row3 = row2 + MobileRowStep;

            CreateRowLabel("UI SIZE", row1);
            string[] sizeLabels = { "SMALL", "NORMAL", "LARGE" };
            for (int i = 0; i < 3; i++)
            {
                UiSize size = (UiSize)i;
                uiSizeButtons[i] = CreateSegmentButton("UiSize" + sizeLabels[i] + "Button", sizeLabels[i], i, row1);
                Bind(uiSizeButtons[i], () => SetUiSize(size));
            }

            CreateRowLabel("FRAME RATE", row2);
            for (int i = 0; i < 3; i++)
            {
                int fps = MobileSettings.FrameRates[i];
                frameRateButtons[i] = CreateSegmentButton("Fps" + fps + "Button", fps + " FPS", i, row2);
                Bind(frameRateButtons[i], () => SetFrameRate(fps));
            }

            CreateRowLabel("PICTURE", row3);
            string[] pictureLabels = { "FULL", "75%", "50%" };
            for (int i = 0; i < 3; i++)
            {
                PictureQuality quality = (PictureQuality)i;
                pictureButtons[i] = CreateSegmentButton("Picture" + i + "Button", pictureLabels[i], i, row3);
                Bind(pictureButtons[i], () => SetPicture(quality));
            }

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
