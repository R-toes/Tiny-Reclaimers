using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TrashTD.Audio;
using TrashTD.Data;
using StageProgress = TrashTD.Core.GameLoop.StageProgress;

namespace TrashTD.UI
{
    // Starting a stage, exiting, button feedback, panel fades, entrance animations and the fade overlay.
    public partial class MainMenuController
    {
        private void PlaySelectedStage()
        {
            if (isLoadingStage || selectedStage == null || !IsStagePlayable(selectedStage.stageId))
            {
                return;
            }

            if (!IsSelectedDifficultyUnlocked)
            {
                var playFeedback = playStageButton != null ? playStageButton.GetComponent<MenuButtonFeedback>() : null;
                if (playFeedback != null) playFeedback.Wobble();
                PlayDenied();
                return;
            }

            SetPendingStage(selectedStage);
            SetPendingStageDifficulty(selectedDifficulty);
            isLoadingStage = true;

            if (overlayRoutine != null) StopCoroutine(overlayRoutine);

            string stageTag = selectedStage.stageId == SandboxStageId
                ? "QA SANDBOX"
                : "STAGE " + (IsTutorialStage(selectedStage) ? 0 : selectedStageIndex + 1).ToString("00");
            LoadingScreen.LoadStage(GameplaySceneName, selectedStage, stageTag, selectedDifficulty, GetDifficultyColor(selectedDifficulty));
        }

        private void ExitGame()
        {
            Debug.Log("Exit button pressed.");

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ─────────────────────────────────────────────────────
        //  Feedback (sounds + hover/press component)
        // ─────────────────────────────────────────────────────

        private void AttachMenuFeedback()
        {
            // PLAY / EXIT get theirs from StyleMainMenuButton
            MenuButtonFeedback backFeedback = AttachFeedback(backButton, null, TextDim, 1.04f);
            if (backFeedback != null) backFeedback.Clicked = null;
            MenuButtonFeedback detailsBackFeedback = AttachFeedback(stageDetailsBackButton, null, TextDim, 1.04f);
            if (detailsBackFeedback != null) detailsBackFeedback.Clicked = null;
            AttachFeedback(playStageButton, () => PlaySfx(clickClip, 1.15f), AccentBright, 1.05f);
        }

        private MenuButtonFeedback AttachFeedback(Button button, System.Action clicked, Color glow, float hoverScale)
        {
            if (button == null) return null;

            // Feedback component drives the visuals, so disable Unity's color tint transition.
            button.transition = Selectable.Transition.None;

            var feedback = button.GetComponent<MenuButtonFeedback>();
            if (feedback == null) feedback = button.gameObject.AddComponent<MenuButtonFeedback>();

            feedback.hoverScale = hoverScale;
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
        private void PlayBack() { PlaySfx(backClip != null ? backClip : clickClip, 0.9f); }
        private void PlayDenied() { PlaySfx(deniedClip, 1f); }

        private void PlaySfx(AudioClip clip, float pitch)
        {
            AudioManager.Instance?.PlaySfx(clip, sfxVolume, pitch);
        }

        // ─────────────────────────────────────────────────────
        //  Panel fades + staggered entrances
        // ─────────────────────────────────────────────────────

        private void SetPanelVisible(GameObject panel, bool visible, bool instant, RectTransform popTarget = null)
        {
            if (panel == null) return;

            if (panelRoutines.TryGetValue(panel, out Coroutine running))
            {
                if (running != null) StopCoroutine(running);
                panelRoutines.Remove(panel);
            }

            CanvasGroup group = GetOrAddCanvasGroup(panel);

            if (instant || !isActiveAndEnabled)
            {
                panel.SetActive(visible);
                group.alpha = visible ? 1f : 0f;
                group.interactable = visible;
                group.blocksRaycasts = visible;
                if (popTarget != null) popTarget.localScale = Vector3.one;
                return;
            }

            if (!visible && !panel.activeSelf) return;

            panelRoutines[panel] = StartCoroutine(FadePanel(panel, group, visible, popTarget));
        }

        private IEnumerator FadePanel(GameObject panel, CanvasGroup group, bool show, RectTransform popTarget)
        {
            float duration = show ? 0.30f : 0.18f;
            float from = panel.activeSelf ? group.alpha : 0f;
            float to = show ? 1f : 0f;

            group.alpha = from;
            group.interactable = show;
            group.blocksRaycasts = show;

            if (show) panel.SetActive(true);
            if (show && popTarget != null) popTarget.localScale = Vector3.one * 0.9f;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                group.alpha = Mathf.Lerp(from, to, EaseOutCubic(k));

                if (popTarget != null)
                {
                    float s = show ? Mathf.LerpUnclamped(0.9f, 1f, EaseOutBack(k)) : Mathf.Lerp(1f, 0.94f, k);
                    popTarget.localScale = new Vector3(s, s, 1f);
                }

                yield return null;
            }

            group.alpha = to;
            if (popTarget != null) popTarget.localScale = Vector3.one;
            if (!show) panel.SetActive(false);

            panelRoutines.Remove(panel);
        }

        private void RegisterEntranceItems()
        {
            mainEntrance.Clear();
            detailsEntrance.Clear();

            // Main menu
            RegisterEntrance(mainEntrance, titleBlock, new Vector2(0f, 40f), 0.05f);
            RegisterEntrance(mainEntrance, subtitleText, new Vector2(-50f, 0f), 0.12f);
            RegisterEntrance(mainEntrance, startButton, new Vector2(0f, -30f), 0.22f);
            RegisterEntrance(mainEntrance, intelUI != null ? intelUI.IntelButton : null, new Vector2(0f, -30f), 0.28f);
            RegisterEntrance(mainEntrance, audioSettingsUI != null ? audioSettingsUI.SettingsButton : null, new Vector2(0f, -30f), 0.34f);
            RegisterEntrance(mainEntrance, exitButton, new Vector2(0f, -30f), 0.40f);
            RegisterEntrance(mainEntrance, menuHintText, Vector2.zero, 0.48f);
            RegisterEntrance(mainEntrance, mainVersion, new Vector2(0f, -20f), 0.30f);
            RegisterEntrance(mainEntrance, menuFeedStatus, new Vector2(0f, 20f), 0.30f);

            // Intro: the logo and viewfinder text come in after the boot, the rows after the key press
            titleSceneEntrance.Clear();
            menuRowsEntrance.Clear();
            RegisterEntrance(titleSceneEntrance, titleBlock, new Vector2(0f, 40f), 0.05f);
            RegisterEntrance(titleSceneEntrance, mainVersion, new Vector2(0f, -20f), 0.20f);
            RegisterEntrance(titleSceneEntrance, menuFeedStatus, new Vector2(0f, 20f), 0.20f);
            RegisterEntrance(menuRowsEntrance, startButton, new Vector2(0f, -30f), 0.02f);
            RegisterEntrance(menuRowsEntrance, intelUI != null ? intelUI.IntelButton : null, new Vector2(0f, -30f), 0.08f);
            RegisterEntrance(menuRowsEntrance, audioSettingsUI != null ? audioSettingsUI.SettingsButton : null, new Vector2(0f, -30f), 0.14f);
            RegisterEntrance(menuRowsEntrance, exitButton, new Vector2(0f, -30f), 0.20f);
            RegisterEntrance(menuRowsEntrance, menuHintText, Vector2.zero, 0.28f);

            // Stage selector header (cards were registered in BuildStageButtons)
            RegisterEntrance(stageEntrance, stageSidePanel, new Vector2(-90f, 0f), 0.00f);
            RegisterEntrance(stageEntrance, stageTitlePlate, new Vector2(-60f, 0f), 0.00f);
            RegisterEntrance(stageEntrance, stageTickerPlate, new Vector2(0f, -30f), 0.20f);
            RegisterEntrance(stageEntrance, stageHeaderTitle, new Vector2(-60f, 0f), 0.00f);
            RegisterEntrance(stageEntrance, stageZoneTitle, new Vector2(-60f, 0f), 0.03f);
            RegisterEntrance(stageEntrance, stageHeaderSubtitle, new Vector2(-40f, 0f), 0.06f);
            RegisterEntrance(stageEntrance, backButton, new Vector2(-40f, 0f), 0.16f);
            RegisterEntrance(stageEntrance, stageHint, Vector2.zero, 0.30f);
            RegisterEntrance(stageEntrance, mapNavigator != null ? mapNavigator.HudRoot : null, new Vector2(40f, 0f), 0.18f);

            // Stage details modal
            RegisterEntrance(detailsEntrance, stageDetailsTitle, new Vector2(0f, 12f), 0.05f);
            RegisterEntrance(detailsEntrance, stageDetailsTitleRule, new Vector2(0f, 12f), 0.08f);
            RegisterEntrance(detailsEntrance, stageDetailsDescription, new Vector2(0f, 12f), 0.12f);
            RegisterEntrance(detailsEntrance, stagePreviewFrame, new Vector2(-24f, 0f), 0.02f);
            RegisterEntrance(detailsEntrance, difficultyLabel, new Vector2(0f, 12f), 0.16f);
            RegisterEntrance(detailsEntrance, difficultySliderRoot, new Vector2(0f, 12f), 0.22f);
            RegisterEntrance(detailsEntrance, difficultyBriefing, new Vector2(0f, 12f), 0.30f);
            RegisterEntrance(detailsEntrance, stageDetailsBackButton, new Vector2(0f, -12f), 0.38f);
            RegisterEntrance(detailsEntrance, playStageButton, new Vector2(0f, -12f), 0.42f);
        }

        private static void RegisterEntrance(List<EntranceItem> list, Component target, Vector2 offset, float delay, float scaleFrom = 1f)
        {
            if (target == null) return;

            var rt = target.GetComponent<RectTransform>();
            if (rt == null) return;

            // Layout groups own their children's positions, so only fade/scale those.
            if (rt.parent != null && rt.parent.GetComponent<LayoutGroup>() != null) offset = Vector2.zero;

            list.Add(new EntranceItem
            {
                rt = rt,
                group = GetOrAddCanvasGroup(rt.gameObject),
                feedback = rt.GetComponent<MenuButtonFeedback>(),
                target = rt.anchoredPosition,
                offset = offset,
                delay = delay,
                scaleFrom = scaleFrom
            });
        }

        private void PlayEntrance(List<EntranceItem> items, ref Coroutine routine)
        {
            if (items.Count == 0 || !isActiveAndEnabled) return;

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(RunEntrance(items));
        }

        private IEnumerator RunEntrance(List<EntranceItem> items)
        {
            float total = 0f;
            foreach (var item in items)
            {
                ApplyEntrance(item, 0f);
                total = Mathf.Max(total, item.delay + EntranceDuration);
            }

            float t = 0f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                foreach (var item in items)
                {
                    float k = Mathf.Clamp01((t - item.delay) / EntranceDuration);
                    ApplyEntrance(item, EaseOutCubic(k));
                }

                yield return null;
            }

            foreach (var item in items) ApplyEntrance(item, 1f);
        }

        private static void ApplyEntrance(EntranceItem item, float k)
        {
            if (item.rt == null) return;

            item.group.alpha = k;
            if (item.offset != Vector2.zero) item.rt.anchoredPosition = item.target + item.offset * (1f - k);
            if (item.feedback != null) item.feedback.introScale = Mathf.Lerp(item.scaleFrom, 1f, k);
        }

        // ─────────────────────────────────────────────────────
        //  Scene fade overlay (fade-in on load; starting a stage uses LoadingScreen)
        // ─────────────────────────────────────────────────────

        private void EnsureFadeOverlay()
        {
            if (fadeOverlay != null) return;

            Canvas canvas = mainMenuPanel != null ? mainMenuPanel.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;
            canvas = canvas.rootCanvas;

            var go = new GameObject("FadeOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas.transform, false);
            StretchFill(go.GetComponent<RectTransform>());

            fadeOverlay = go.GetComponent<Image>();
            fadeOverlay.color = new Color(0.01f, 0.012f, 0.02f, 0f);
            fadeOverlay.raycastTarget = false;
        }

        private void SetOverlayAlpha(float alpha)
        {
            Color c = fadeOverlay.color;
            c.a = alpha;
            fadeOverlay.color = c;
        }

        private IEnumerator FadeOverlayRoutine(float from, float to, float duration)
        {
            EnsureFadeOverlay();
            if (fadeOverlay == null) yield break;

            fadeOverlay.transform.SetAsLastSibling();
            fadeOverlay.raycastTarget = true;
            SetOverlayAlpha(from);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetOverlayAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            SetOverlayAlpha(to);
            fadeOverlay.raycastTarget = to > 0.01f;
        }

        // ─────────────────────────────────────────────────────
        //  Ambient background motes
        // ─────────────────────────────────────────────────────
    }
}
