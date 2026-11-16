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

namespace TrashTD.UI
{
    // Lifecycle, phase integration and showing the offered cards.
    public partial class CardDraftOverlayUI
    {
        // ============================
        // Lifecycle
        // ============================

        private void Awake()
        {
            draftSystem = FindAnyObjectByType<CardDraftSystem>();
            playerDeck = FindAnyObjectByType<PlayerDeck>();
            gameManager = FindAnyObjectByType<GameManager>();
            gameplayHudUI = FindAnyObjectByType<GameplayHUDUI>();
            if (gameplayHudUI != null)
                gameplayHudUI.EnvironmentBriefingDismissed += HandleEnvironmentBriefingDismissed;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            if (hoverClip == null) hoverClip = AudioManager.Instance?.GetClip(SfxId.UiHover);
            if (clickClip == null) clickClip = AudioManager.Instance?.GetClip(SfxId.UiClick);
            if (confirmClip == null) confirmClip = AudioManager.Instance?.GetClip(SfxId.UiConfirm);
            if (rerollClip == null) rerollClip = AudioManager.Instance?.GetClip(SfxId.UiReroll) ?? clickClip;
            // Card animation sounds fall back to the hover blip the overlay used before they had their own files.
            if (shuffleClip == null) shuffleClip = AudioManager.Instance?.GetClip(SfxId.UiCardShuffle) ?? hoverClip;
            if (dealClip == null) dealClip = AudioManager.Instance?.GetClip(SfxId.UiCardDeal) ?? hoverClip;
            if (flipClip == null) flipClip = AudioManager.Instance?.GetClip(SfxId.UiCardFlip) ?? hoverClip;
            // Odds step-up reuses the deck's rank-up sound.
            if (oddsUpClip == null) oddsUpClip = AudioManager.Instance?.GetClip(SfxId.GameplayCardUpgrade) ?? confirmClip;

            if (draftSystem != null)
            {
                draftSystem.OnCardsOffered += HandleCardsOffered;
            }

            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void Start()
        {
            BuildOverlay();
            overlayRoot.SetActive(false);

            if (playerDeck != null)
            {
                playerDeck.OnDeckChanged += HandleDeckChanged;
            }

            if (gameManager != null)
            {
                gameManager.OnPhaseChanged += HandlePhaseChanged;

                if (gameManager.CurrentState == GamePlayState.Playing && gameManager.CurrentPhase == StagePhase.CardPick)
                {
                    ShowDraftOverlay();
                }
            }
        }

        private void Update()
        {
            if (overlayRoot == null) return;

            // This canvas sorts above the HUD's pause menu, so hide it (restore bar included) while paused.
            // Otherwise the draft stays clickable during pause, and a confirm adds the card without leaving Card Pick.
            bool paused = IsGamePaused();
            if (canvas != null && canvas.enabled == paused) canvas.enabled = !paused;
            if (paused) return;

            if (minimized)
            {
                PositionRestoreButton();
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.mKey.wasPressedThisFrame) SetMinimized(false);
                return;
            }

            if (!overlayVisible) return;

            // Confirm button breathes while a card can be confirmed
            if (confirmFeedback != null && confirmButton != null)
            {
                confirmFeedback.restScale = confirmButton.interactable
                    ? 1f + 0.02f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f))
                    : 1f;
            }

            AnimateCards();
            HandleKeyboardShortcuts();
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (playerDeck != null)
            {
                playerDeck.OnDeckChanged -= HandleDeckChanged;
            }

            if (draftSystem != null)
            {
                draftSystem.OnCardsOffered -= HandleCardsOffered;
            }

            if (gameplayHudUI != null)
                gameplayHudUI.EnvironmentBriefingDismissed -= HandleEnvironmentBriefingDismissed;

            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

            DestroyGeneratedSprite(glowSprite);
            DestroyGeneratedSprite(stripeSprite);
            DestroyGeneratedSprite(gridSprite);
        }

#if UNITY_EDITOR || DEBUG
        public bool IsDeveloperPickerOpen => developerPickerRoot != null && developerPickerRoot.activeSelf;
        public void CloseDeveloperPicker()
        {
            if (developerPickerRoot != null) developerPickerRoot.SetActive(false);
        }
#endif

        private void HandleKeyboardShortcuts()
        {
            if (gameplayHudUI != null && gameplayHudUI.IsEnvironmentBriefingActive) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            if (rerollNoticeRoot != null && rerollNoticeRoot.activeSelf)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    CloseRerollNotice(false);
                    gameplayHudUI?.ConsumeEscapeKey();
                }
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame ||
                         keyboard.rKey.wasPressedThisFrame)
                {
                    CloseRerollNotice(true);
                }
                return;
            }

#if UNITY_EDITOR || DEBUG
            if (developerPickerRoot != null && developerPickerRoot.activeSelf)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    developerPickerRoot.SetActive(false);
                    gameplayHudUI?.ConsumeEscapeKey();
                }
                return;
            }
#endif

            if (gameplayHudUI != null && gameplayHudUI.IsPaused) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SelectCardByKey(0);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectCardByKey(1);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectCardByKey(2);

            if ((keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) && confirmButton.interactable)
            {
                OnConfirmClicked();
            }

            if (keyboard.rKey.wasPressedThisFrame && rerollButton.interactable)
            {
                OnRerollClicked();
            }

            if (keyboard.mKey.wasPressedThisFrame)
            {
                SetMinimized(true);
            }
        }

        private void SelectCardByKey(int index)
        {
            if (isShuffling) return;
            if (index < 0 || index >= cardPanels.Length || cardPanels[index] == null || !cardPanels[index].activeSelf) return;

            PlaySfx(clickClip, 1f);
            if (cardRevealed[index]) cardFeedbacks[index].Punch();
            OnCardClicked(index);
        }

        // ============================
        // Phase Integration
        // ============================

        private void HandlePhaseChanged(StagePhase phase)
        {
            if (IsActiveGameplayCardPick())
            {
                ShowDraftOverlay();
            }
            else if (gameManager == null || SceneManager.GetActiveScene() != gameObject.scene)
            {
                HideDraftOverlayImmediately();
            }
            else
            {
                HideDraftOverlay();
            }
        }

        private void HandleEnvironmentBriefingDismissed()
        {
            if (!waitingForEnvironmentBriefing || !IsActiveGameplayCardPick()) return;
            waitingForEnvironmentBriefing = false;
            ShowDraftOverlay();
        }

        private void HandleActiveSceneChanged(Scene previousScene, Scene activeScene)
        {
            if (activeScene != gameObject.scene)
            {
                HideDraftOverlayImmediately();
            }
        }

        private void ShowDraftOverlay()
        {
            if (!IsActiveGameplayCardPick())
            {
                HideDraftOverlayImmediately();
                return;
            }

            if (gameplayHudUI != null && gameplayHudUI.ShouldBlockDraftOverlay)
            {
                waitingForEnvironmentBriefing = true;
                HideDraftOverlayImmediately();
                return;
            }

            waitingForEnvironmentBriefing = false;
            pickConfirmed = false;
            ResetMinimized();
            rerollNoticeAcknowledged = false;
            if (rerollNoticeRoot != null) rerollNoticeRoot.SetActive(false);

#if UNITY_EDITOR || DEBUG
            if (developerPickerRoot != null) developerPickerRoot.SetActive(false);
#endif
            selectedCardIndex = -1;

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            // Start every draft with the cards stacked face-down; the shuffle deals them out.
            ResetCardsForNewDraft();

            overlayRoot.SetActive(true);
            overlayVisible = true;
            PlayIntro();

            // Generate a fresh draft offer and immediately populate the card UI
            if (draftSystem != null)
            {
                // During Card Pick the wave index is the number of waves cleared, which picks the odds row
                if (gameManager != null) draftSystem.SetWavesCleared(gameManager.GetCurrentWaveIndex());
                var offer = draftSystem.GenerateDraftOffer();
                HandleCardsOffered(offer);
            }

            UpdateConfirmButton();
            UpdateDeckCount();
            UpdateWaveInfo();
            UpdateDraftOdds();

            bool extras = !IsTutorialDraft;
            if (rerollButton != null) rerollButton.gameObject.SetActive(extras);
            if (skipButton != null) skipButton.gameObject.SetActive(extras);
            if (minimizeButton != null) minimizeButton.gameObject.SetActive(extras);
            if (skipAnimationToggleRect != null) skipAnimationToggleRect.gameObject.SetActive(extras);
            if (oddsToggleRect != null) oddsToggleRect.gameObject.SetActive(extras);
#if UNITY_EDITOR || DEBUG
            if (developerPickButton != null) developerPickButton.gameObject.SetActive(extras);
#endif
        }

        private bool IsGamePaused()
        {
            return (gameplayHudUI != null && gameplayHudUI.IsPaused)
                || (gameManager != null && gameManager.CurrentState == GamePlayState.Paused);
        }

        private bool IsActiveGameplayCardPick()
        {
            return gameManager != null
                && gameManager.CurrentState == GamePlayState.Playing
                && gameManager.CurrentPhase == StagePhase.CardPick
                && SceneManager.GetActiveScene() == gameObject.scene
                && gameManager.gameObject.scene == gameObject.scene;
        }

        private void HideDraftOverlay()
        {
            // A minimized overlay is already inactive; still close it so its keys stop working.
            ResetMinimized();
            if (overlayRoot == null || !overlayRoot.activeSelf)
            {
                overlayVisible = false;
                return;
            }

            overlayVisible = false;

            if (!isActiveAndEnabled)
            {
                overlayRoot.SetActive(false);
                return;
            }

            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideRoutine());
        }

        private void HideDraftOverlayImmediately()
        {
            ResetMinimized();
            overlayVisible = false;
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        public void HideForSandbox()
        {
            HideDraftOverlayImmediately();
        }

        // ============================
        // Card Display
        // ============================

        private void HandleCardsOffered(IReadOnlyList<DraftCard> cards)
        {
            // The offer event can fire before the overlay has been built.
            if (overlayRoot == null) return;

            selectedCardIndex = -1;

            // The new offer is stored and only written onto the cards once they are face-down
            // (see ShuffleDealRoutine), so a reroll never reveals the new hand early.
            pendingOffer.Clear();
            for (int i = 0; i < cards.Count; i++) pendingOffer.Add(cards[i]);

            ApplySelectionVisuals();
            UpdateDetailsPanel(false);
            StartShuffleDeal();

            UpdateConfirmButton();
            UpdateRerollButton();
            UpdateDeckCount();
        }

        /// <summary>Writes the pending offer onto the three cards (they must be face-down).</summary>
        private void ApplyOfferToCards()
        {
            for (int i = 0; i < 3; i++)
            {
                DraftCard card = i < pendingOffer.Count ? pendingOffer[i] : null;
                bool hasCard = card != null && card.operatorData != null;
                cardPanels[i].SetActive(hasCard);
                cardRevealed[i] = false;
                SetFaceVisual(i, false);

                if (!hasCard) continue;

                var opData = card.operatorData;
                int starCount = Mathf.Clamp((int)card.rarity, 1, 5);
                Color rarityColor = GetRarityColor(starCount);
                cardRarityColors[i] = rarityColor;

                // Name + rarity (the only text on the card)
                nameTexts[i].text = opData.operatorName;
                rarityTexts[i].text = BuildStarString(starCount);
                rarityTexts[i].color = rarityColor;

                // Rarity tint: frame, notches, name tick, portrait backdrop/glow/frame
                cardStarCounts[i] = starCount;
                TintRarityParts(i, rarityColor);
                cardAccents[i].color = rarityColor;
                namePlates[i].color = Color.Lerp(new Color(0.03f, 0.035f, 0.05f, 1f), rarityColor, 0.16f);
                portraitBgImages[i].color = Color.Lerp(PORTRAIT_BG, rarityColor, 0.14f);
                portraitGlows[i].color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.38f);
                portraitFrames[i].effectColor = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.6f);
                classBadgeBackgrounds[i].color = Color.clear;
                classBadgeIcons[i].sprite = GameplayHUDUI.GetClassIconSprite(opData.operatorClass);
                classBadgeIcons[i].color = Color.white;

                // Header: class and position
                classNameTexts[i].text = opData.operatorClass.ToString().ToUpper();
                classNameTexts[i].color = GetClassColor(opData.operatorClass);
                bool isMelee = opData.position == OperatorPosition.Melee;
                positionTexts[i].text = isMelee ? "MELEE" : "RANGED";
                positionTexts[i].color = isMelee ? MELEE_COLOR : RANGED_COLOR;

                if (starPopRoutines[i] != null)
                {
                    StopCoroutine(starPopRoutines[i]);
                    starPopRoutines[i] = null;
                }
                rarityTexts[i].rectTransform.localScale = Vector3.one;
                shineStarts[i] = -10f;
                nextShineTimes[i] = Time.unscaledTime + 1.5f + i * 0.4f;

                // The hover glow stays neutral while face-down so it can't give away the rarity.
                cardFeedbacks[i].glowColor = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.95f);

                SetPortrait(i, opData);
            }

            RecomputeSlots();
        }

        /// <summary>Slot position of each visible card, centered as a row (replaces the layout group).</summary>
        private void RecomputeSlots()
        {
            int count = 0;
            for (int i = 0; i < 3; i++) if (cardPanels[i].activeSelf) count++;

            int rank = 0;
            for (int i = 0; i < 3; i++)
            {
                if (!cardPanels[i].activeSelf) continue;
                cardSlots[i] = new Vector2((rank - (count - 1) * 0.5f) * (CardWidth + CardSpacing), 0f);
                rank++;
            }
        }

        /// <summary>
        /// Shows the portrait as large as the frame allows while staying centered.
        /// Small pixel-art sprites are scaled by whole numbers (when they fit) so they stay crisp.
        /// </summary>
        private void SetPortrait(int index, OperatorData opData)
        {
            Image image = portraitImages[index];
            Sprite sprite = opData.portrait;

            string initial = string.IsNullOrEmpty(opData.operatorName) ? "?" : opData.operatorName.Substring(0, 1).ToUpper();
            portraitFallbackTexts[index].text = initial;
            portraitFallbackTexts[index].enabled = sprite == null;

            if (sprite == null)
            {
                image.sprite = null;
                image.enabled = false;
                return;
            }

            // Pixel art should not be bilinear-smoothed when scaled up.
            // (For a permanent fix, set the sprite's Filter Mode to Point in its import settings.)
            if (sprite.texture != null && sprite.texture.filterMode != FilterMode.Point)
            {
                sprite.texture.filterMode = FilterMode.Point;
            }

            image.enabled = true;
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = false;

            Vector2 pixelSize = sprite.rect.size;
            float fit = Mathf.Min(PortraitFitArea.x / pixelSize.x, PortraitFitArea.y / pixelSize.y);
            float scale = fit >= 1f ? Mathf.Floor(fit) : fit;
            image.rectTransform.sizeDelta = pixelSize * scale;
        }

        private void ApplySelectionVisuals()
        {
            bool anySelected = selectedCardIndex >= 0;

            for (int i = 0; i < 3; i++)
            {
                if (cardFeedbacks[i] == null) continue;

                bool selected = i == selectedCardIndex;
                Color baseColor = selected
                    ? Color.Lerp(CARD_BG, cardRarityColors[i], 0.22f)
                    : (anySelected ? Color.Lerp(CARD_BG, Color.black, 0.3f) : CARD_BG);

                cardFeedbacks[i].SetBaseColor(baseColor);
                cardFeedbacks[i].glowAlways = selected;
                cardFeedbacks[i].restScale = selected ? 1.03f : (anySelected ? 0.97f : 1f);

                selectHintTexts[i].text = selected ? "✓ SELECTED" : "CLICK TO SELECT";
                selectHintTexts[i].color = selected ? ACCENT_BRIGHT : new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.6f);
            }
        }
    }
}
