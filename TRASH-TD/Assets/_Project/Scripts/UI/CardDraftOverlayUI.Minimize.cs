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
    // Minimizing the draft to view the map, the intro and hide animations, and feedback helpers.
    public partial class CardDraftOverlayUI
    {
        // ============================
        // Minimize (view the map)
        // ============================

        /// <summary>
        /// Hides the draft screen so the player can look at the stage, leaving a small bar to come back.
        /// The offer and the current selection are kept.
        /// </summary>
        private void SetMinimized(bool value)
        {
            if (overlayRoot == null || minimized == value) return;
            if (value && (!overlayVisible || IsTutorialDraft)) return;
            if (IsGamePaused()) return;

            ClearUiSelection();
            minimized = value;
            if (value)
            {
                FinishAnimations();
                overlayRoot.SetActive(false);
            }
            else
            {
                overlayRoot.SetActive(true);
                overlayGroup.alpha = 1f;
                overlayGroup.interactable = true;
                overlayGroup.blocksRaycasts = true;
                UpdateConfirmButton();
                UpdateDeckCount();
            }

            if (restoreBar != null) restoreBar.SetActive(value);
            if (value) PositionRestoreButton();
            PlayClick();
        }

        /// <summary>
        /// Puts RESUME in the free deck-bar strip between PREVIEW PATH and the bottom-right panel
        /// (GameplayHUDUI.TryGetDeckBarFreeSlot). Runs every frame while minimized, because that strip
        /// moves with the resolution. If the strip is too narrow (tall aspect ratios), the button sits just
        /// above it. Without the HUD it keeps its default top-centre spot.
        /// </summary>
        private void PositionRestoreButton()
        {
            if (restoreBar == null || canvas == null) return;
            if (gameplayHudUI == null) gameplayHudUI = FindAnyObjectByType<GameplayHUDUI>();
            if (gameplayHudUI == null || !gameplayHudUI.TryGetDeckBarFreeSlot(out Rect slot)) return;

            const float PreferredWidth = 200f;
            const float MinWidth = 140f;
            const float Height = 48f;
            const float Margin = 12f;

            var canvasRect = (RectTransform)canvas.transform;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, slot.min, cam, out Vector2 min) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, slot.max, cam, out Vector2 max))
            {
                return;
            }

            var rt = (RectTransform)restoreBar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            Vector2 center = (min + max) * 0.5f;
            float width = Mathf.Min(PreferredWidth, max.x - min.x - Margin * 2f);
            if (width >= MinWidth)
            {
                rt.sizeDelta = new Vector2(width, Height);
                rt.anchoredPosition = center;
            }
            else
            {
                rt.sizeDelta = new Vector2(PreferredWidth, Height);
                rt.anchoredPosition = new Vector2(center.x, max.y + Margin + Height * 0.5f);
            }
        }

        private void ResetMinimized()
        {
            minimized = false;
            if (restoreBar != null) restoreBar.SetActive(false);
        }

        /// <summary>Jumps intro/shuffle/flip animations to their end state so a restored overlay is laid out correctly.</summary>
        private void FinishAnimations()
        {
            if (introRoutine != null)
            {
                StopCoroutine(introRoutine);
                introRoutine = null;
                for (int i = 0; i < introRects.Length; i++) introRects[i].anchoredPosition = introTargets[i];
            }

            StopAllFlips();
            StopOddsAnimation();

            if (dealRoutine != null)
            {
                StopCoroutine(dealRoutine);
                dealRoutine = null;

                if (!offerApplied)
                {
                    ApplyOfferToCards();
                    offerApplied = true;
                    freshDraft = false;
                }

                SettleCards();
            }
        }

        /// <summary>Keeps the confirm button and deck count current while the deck is edited from the map view.</summary>
        private void HandleDeckChanged(IReadOnlyList<DraftCard> _)
        {
            if (!overlayVisible) return;
            UpdateConfirmButton();
            UpdateDeckCount();
        }

        // ============================
        // Animation
        // ============================

        private void PlayIntro()
        {
            if (!isActiveAndEnabled) return;

            if (introRoutine != null) StopCoroutine(introRoutine);
            introRoutine = StartCoroutine(IntroRoutine());
        }

        private IEnumerator IntroRoutine()
        {
            overlayGroup.alpha = 0f;
            overlayGroup.interactable = true;
            overlayGroup.blocksRaycasts = true;

            const float duration = 0.45f;
            float total = duration + 0.35f;
            float t = 0f;

            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                overlayGroup.alpha = Mathf.Clamp01(t / 0.25f);

                for (int i = 0; i < introRects.Length; i++)
                {
                    float k = EaseOutCubic(Mathf.Clamp01((t - introDelays[i]) / duration));
                    introRects[i].anchoredPosition = introTargets[i] + introOffsets[i] * (1f - k);
                }

                yield return null;
            }

            overlayGroup.alpha = 1f;
            for (int i = 0; i < introRects.Length; i++) introRects[i].anchoredPosition = introTargets[i];
        }

        private IEnumerator HideRoutine()
        {
            if (introRoutine != null) { StopCoroutine(introRoutine); introRoutine = null; }
            if (dealRoutine != null) { StopCoroutine(dealRoutine); dealRoutine = null; }

            overlayGroup.interactable = false;
            overlayGroup.blocksRaycasts = false;

            // The picked card rises and grows a little, the others drop away, and the screen fades out
            int picked = pickConfirmed ? pickedCardIndex : -1;
            var start = new Vector2[3];
            for (int i = 0; i < 3; i++) if (cardRects[i] != null) start[i] = cardRects[i].anchoredPosition;

            float from = overlayGroup.alpha;
            float t = 0f;
            while (t < PickOutTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / PickOutTime);
                for (int i = 0; i < 3; i++)
                {
                    if (cardRects[i] == null || !cardPanels[i].activeSelf) continue;
                    if (i == picked)
                    {
                        cardRects[i].anchoredPosition = start[i] + new Vector2(0f, 40f * EaseOutCubic(k));
                        cardFeedbacks[i].introScale = 1f + 0.08f * EaseOutCubic(k);
                    }
                    else
                    {
                        cardRects[i].anchoredPosition = start[i] + new Vector2(0f, -140f * k * k);
                        cardGroups[i].alpha = 1f - k;
                    }
                }
                overlayGroup.alpha = Mathf.Lerp(from, 0f, EaseInCubic(k));
                yield return null;
            }

            overlayGroup.alpha = 0f;
            overlayRoot.SetActive(false);
            hideRoutine = null;
        }

        private static float EaseInCubic(float t)
        {
            return t * t * t;
        }

        private static float EaseInOutCubic(float t)
        {
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        // ============================
        // Feedback helpers
        // ============================

        private MenuButtonFeedback AttachFeedback(Button button, Color glow, float hoverScale)
        {
            return UiKit.AttachFeedbackCore(button, glow, hoverScale, PlayHover, PlayClick, false); // clicked buttons stay "selected"; don't keep them lit
        }

        private void PlayHover() { PlaySfx(hoverClip, UnityEngine.Random.Range(0.97f, 1.03f)); }
        private void PlayClick() { PlaySfx(clickClip, 1f); }

        private void PlaySfx(AudioClip clip, float pitch)
        {
            AudioManager.Instance?.PlaySfx(clip, sfxVolume, pitch);
        }

        private static void ClearUiSelection()
        {
            // Prevents Enter/Space from re-triggering the button that was just clicked.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>1★ green, 2★ blue, 3★ gold, 4★ purple, 5★ red.</summary>
        private static Color GetRarityColor(int stars)
        {
            switch (stars)
            {
                case 1: return new Color(0.35f, 0.85f, 0.45f, 1f);
                case 2: return new Color(0.35f, 0.65f, 1.00f, 1f);
                case 3: return new Color(1.00f, 0.82f, 0.25f, 1f);
                case 4: return new Color(0.75f, 0.45f, 1.00f, 1f);
                default: return new Color(1.00f, 0.30f, 0.30f, 1f);
            }
        }

        private static string BuildStarString(int stars)
        {
            stars = Mathf.Clamp(stars, 0, 5);
            return new string('★', stars) + "<color=#3C424D>" + new string('☆', 5 - stars) + "</color>";
        }

        private static Color GetClassColor(OperatorClass opClass)
        {
            switch (opClass)
            {
                case OperatorClass.Guard: return new Color(0.80f, 0.28f, 0.28f, 1f);
                case OperatorClass.Defender: return new Color(0.30f, 0.45f, 0.85f, 1f);
                case OperatorClass.Sniper: return new Color(0.30f, 0.75f, 0.30f, 1f);
                case OperatorClass.Caster: return new Color(0.70f, 0.30f, 0.85f, 1f);
                case OperatorClass.Medic: return new Color(0.80f, 0.78f, 0.30f, 1f);
                case OperatorClass.Specialist: return new Color(0.20f, 0.75f, 0.70f, 1f);
                default: return CLASS_COLOR;
            }
        }
    }
}
