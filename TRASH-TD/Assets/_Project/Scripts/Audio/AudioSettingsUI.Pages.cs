using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TrashTD.UI;
using TrashTD.Core.GameLoop;
using static TrashTD.UI.UiKit;

namespace TrashTD.Audio
{
    // The tabs and their pages: controls, gameplay, the reset row, and the resolution and window mode rows.
    public partial class AudioSettingsUI
    {
        private void SelectTab(Tab tab)
        {
            lastTab = tab;
            for (int i = 0; i < pages.Length; i++)
            {
                bool selected = i == (int)tab;
                if (pages[i] != null) pages[i].SetActive(selected);
                SetToggleVisual(tabButtons[i], selected);
            }
        }

        private void SetPlacementScheme(PlacementControlScheme scheme)
        {
            ControlSettings.Scheme = scheme;
            RefreshControls();
        }

        private void RefreshControls()
        {
            PlacementControlScheme current = ControlSettings.Scheme;
            foreach (PlacementSchemeOption option in schemeOptions)
                option?.SetSelected(option.Scheme == current);
        }

        private void SetRerollNotice(bool show)
        {
            GameplaySettings.ShowOpeningRerollNotice = show;
            RefreshGameplay();
        }

        private void SetHideHud(bool hide)
        {
            GameplaySettings.HideHud = hide;
            RefreshGameplay();
        }

        private void RefreshGameplay()
        {
            bool show = GameplaySettings.ShowOpeningRerollNotice;
            SetToggleVisual(noticeOnButton, show);
            SetToggleVisual(noticeOffButton, !show);

            bool hideHud = GameplaySettings.HideHud;
            SetToggleVisual(hideHudOnButton, hideHud);
            SetToggleVisual(hideHudOffButton, !hideHud);
            RefreshResetProgress(true);
        }

        private static bool IsMainMenu => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == MainMenuSceneName;

        /// <summary>RESET asks twice: the first click arms it (red, "SURE?") for a few seconds, the second erases the save.</summary>
        private void OnResetProgressClicked()
        {
            if (!IsMainMenu) return;

            if (Time.unscaledTime >= resetConfirmUntil)
            {
                resetConfirmUntil = Time.unscaledTime + ResetConfirmSeconds;
                RefreshResetProgress(true);
                return;
            }

            resetConfirmUntil = 0f;
            PlayerProgress.ResetAll();
            // Reload the menu so the stage map, its locks and the tutorial guide start over
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void RefreshResetProgress(bool force)
        {
            if (resetProgressButton == null) return;

            // 0 = unavailable (in a stage), 1 = ready, 2 = armed
            int state = !IsMainMenu ? 0 : Time.unscaledTime < resetConfirmUntil ? 2 : 1;
            if (!force && state == shownResetState) return;
            shownResetState = state;

            resetProgressButton.interactable = state != 0;
            Text label = resetProgressButton.GetComponentInChildren<Text>();
            if (state == 2)
            {
                if (label != null) label.text = "SURE?";
                MenuButtonFeedback feedback = resetProgressButton.GetComponent<MenuButtonFeedback>();
                if (feedback != null) feedback.SetBaseColor(DangerColor);
                else resetProgressButton.GetComponent<Image>().color = DangerColor;
                if (label != null) label.color = Color.white;
            }
            else
            {
                if (label != null) label.text = "RESET";
                SetToggleVisual(resetProgressButton, false);
                if (state == 0 && label != null) label.color = new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.3f);
            }

            if (resetProgressHint != null)
            {
                resetProgressHint.text = state switch
                {
                    0 => "Available from the main menu.",
                    2 => "Click again to erase stage clears, stars, unlocks and Intel files.",
                    _ => "Erases stage clears, stars, unlocks and Intel files. Settings stay."
                };
            }
        }

        // ── Resolution ──

        /// <summary>
        /// RESOLUTION: [&lt;] 1920 x 1080 [&gt;] in the first two segments, APPLY in the third. The arrows only pick;
        /// APPLY switches (Screen.SetResolution, which the player remembers between launches).
        /// </summary>
        private void CreateResolutionRow(float centerY)
        {
            const float arrowWidth = 44f;
            CreateRowLabel("RESOLUTION", centerY);
            float pickerWidth = 2f * SegmentWidth + 12f;

            Button prev = CreateTerminalButton(pageRoot, "ResolutionPrevButton", "<", 14);
            PlaceTopLeft(prev.GetComponent<RectTransform>(), ControlX, centerY - 20f, arrowWidth, 40f);

            Button next = CreateTerminalButton(pageRoot, "ResolutionNextButton", ">", 14);
            PlaceTopLeft(next.GetComponent<RectTransform>(), ControlX + pickerWidth - arrowWidth, centerY - 20f, arrowWidth, 40f);

            Text value = CreateText(pageRoot, string.Empty, LabelSize, TextAnchor.MiddleCenter, AccentBright);
            value.gameObject.name = "ResolutionValue";
            PlaceTopLeft(value.rectTransform, ControlX + arrowWidth, centerY - 15f, pickerWidth - 2f * arrowWidth, 30f);

            Button apply = CreateTerminalButton(pageRoot, "ResolutionApplyButton", "APPLY", 13);
            PlaceTopLeft(apply.GetComponent<RectTransform>(), ControlX + 2f * (SegmentWidth + 12f), centerY - 20f, SegmentWidth, 40f);
        }

        /// <summary>A button in one of the three control segments of a row (like the CRT style buttons).</summary>
        private Button CreateSegmentButton(string objectName, string label, int index, float centerY)
        {
            Button button = CreateTerminalButton(pageRoot, objectName, label, 13);
            PlaceTopLeft(button.GetComponent<RectTransform>(), ControlX + index * (SegmentWidth + 12f), centerY - 20f, SegmentWidth, 40f);
            return button;
        }

        /// <summary>The monitor's resolutions (one per size, smallest first) plus the current one; selects the current.</summary>
        private void SyncResolutions()
        {
            Vector2Int current = new Vector2Int(Screen.width, Screen.height);
            shownScreenSize = current;
            resolutions.Clear();
            foreach (Resolution resolution in Screen.resolutions)
            {
                var size = new Vector2Int(resolution.width, resolution.height);
                if (size.y >= 540 && !resolutions.Contains(size)) resolutions.Add(size);
            }
            if (!resolutions.Contains(current)) resolutions.Add(current);
            resolutions.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            resolutionIndex = resolutions.IndexOf(current);
            RefreshResolution();
        }

        private void StepResolution(int step)
        {
            if (resolutions.Count == 0) return;
            resolutionIndex = Mathf.Clamp(resolutionIndex + step, 0, resolutions.Count - 1);
            RefreshResolution();
        }

        private void ApplyResolution()
        {
            if (resolutionIndex < 0 || resolutionIndex >= resolutions.Count) return;
            Vector2Int size = resolutions[resolutionIndex];
            if (size == new Vector2Int(Screen.width, Screen.height)) return;
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
        }

        /// <summary>
        /// Phones and tablets always fill their screen, so the SCREEN section (resolution, fullscreen/windowed) has
        /// nothing to set. Hides it and moves the CRT section up into its place.
        /// </summary>
        private void HideDesktopScreenRows()
        {
            const float screenBandBottom = 290f; // the SCREEN rows end at y = 286; the CRT STYLE header starts at 298
            const float shiftUp = 160f;          // from the SCREEN header (150) to the CRT STYLE header (310)
            GameObject displayPage = pages[(int)Tab.Display];
            if (displayPage == null) return;

            foreach (Transform child in displayPage.transform)
            {
                RectTransform rect = child as RectTransform;
                if (rect == null) continue;
                float y = -rect.anchoredPosition.y;
                if (y < screenBandBottom) child.gameObject.SetActive(false);
                else rect.anchoredPosition += Vector2.up * shiftUp;
            }
        }

        private void SetWindowMode(bool fullscreen)
        {
            FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (IsFullscreen(Screen.fullScreenMode) != fullscreen) Screen.SetResolution(Screen.width, Screen.height, mode);
            RefreshWindowMode(fullscreen);
        }

        private static bool IsFullscreen(FullScreenMode mode) => mode != FullScreenMode.Windowed;

        private void RefreshResolution()
        {
            bool valid = resolutionIndex >= 0 && resolutionIndex < resolutions.Count;
            Vector2Int picked = valid ? resolutions[resolutionIndex] : new Vector2Int(Screen.width, Screen.height);
            bool pending = picked != new Vector2Int(Screen.width, Screen.height);

            if (resolutionText != null)
            {
                resolutionText.text = picked.x + " x " + picked.y;
                resolutionText.color = pending ? LabelColor : AccentBright;
            }
            SetArrowEnabled(resolutionPrevButton, valid && resolutionIndex > 0);
            SetArrowEnabled(resolutionNextButton, valid && resolutionIndex < resolutions.Count - 1);
            if (resolutionApplyButton != null)
            {
                resolutionApplyButton.interactable = pending;
                SetToggleVisual(resolutionApplyButton, pending);
            }
            RefreshWindowMode(IsFullscreen(Screen.fullScreenMode));
        }

        private static void SetArrowEnabled(Button button, bool enabled)
        {
            if (button == null) return;
            button.interactable = enabled;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null) label.color = enabled ? LabelColor : new Color(LabelColor.r, LabelColor.g, LabelColor.b, 0.3f);
        }

        private void RefreshWindowMode(bool fullscreen)
        {
            SetToggleVisual(fullscreenButton, fullscreen);
            SetToggleVisual(windowedButton, !fullscreen);
        }

        private void Update()
        {
            if (panel != null && panel.activeSelf) RefreshResetProgress(false);

            // The screen size changes a frame or so after APPLY (or when the window is resized)
            if (panel != null && panel.activeSelf && shownScreenSize != new Vector2Int(Screen.width, Screen.height))
                SyncResolutions();

            // Quick pop-in on unscaled time (the panel opens while the game is paused)
            if (panel == null || !panel.activeSelf || showTime >= ShowDuration) return;

            showTime += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(showTime / ShowDuration);
            float eased = 1f - (1f - k) * (1f - k) * (1f - k);
            panelGroup.alpha = eased;
            card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
        }
    }
}
