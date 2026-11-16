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

namespace TrashTD.UI
{
    // Phase handling, deck slot selection and dragging, and the actions behind the HUD buttons, keys and pause menu.
    public partial class GameplayHUDUI
    {
        // ============================
        // Phase Handling
        // ============================

        private void HandlePhaseChanged(StagePhase phase)
        {
            UpdatePhaseUI(true);
        }

        private void UpdatePhaseUI(bool announce)
        {
            if (gameManager == null) return;
            if (surveillanceOverlay != null) surveillanceOverlay.SetStage(gameManager.CurrentStage);

            if (IsSandbox)
            {
                if (phaseText != null) phaseText.text = "QA SANDBOX";
                phaseColorTarget = PrepColor;
                SetSandboxWaveButton(waveManager != null && waveManager.IsWaveInProgress);
                return;
            }

            StagePhase phase = gameManager.CurrentPhase;
            SetWaveProgressFillSprite(phase);
            UpdateWaveReadout();
            UpdateHostileReadout();
            RefreshNextWavePreview();

            switch (phase)
            {
                case StagePhase.CardPick:
                    if (phaseText != null) phaseText.text = "CARD PICK";
                    phaseColorTarget = DraftColor;
                    startWaveButton.interactable = false;
                    if (startWaveButtonText != null) startWaveButtonText.text = "DRAFTING...";
                    ApplyButtonContainer(startWaveButton, ButtonContainerType.Dark);
                    startWaveFeedback.SetBaseColor(WaitingColor);
                    startWaveFeedback.glowAlways = false;
                    break;

                case StagePhase.Preparation:
                    if (phaseText != null) phaseText.text = "PREPARATION";
                    phaseColorTarget = PrepColor;
                    startWaveButton.interactable = true;
                    if (startWaveButtonText != null) startWaveButtonText.text = "START\nWAVE";
                    ApplyButtonContainer(startWaveButton, ButtonContainerType.Primary);
                    startWaveFeedback.SetBaseColor(Color.white);
                    startWaveFeedback.glowAlways = true;
                    break;

                case StagePhase.WaveActive:
                    if (phaseText != null) phaseText.text = "INVADING";
                    phaseColorTarget = WaveColor;
                    startWaveButton.interactable = false;
                    if (startWaveButtonText != null) startWaveButtonText.text = "WAVE\nACTIVE";
                    ApplyButtonContainer(startWaveButton, ButtonContainerType.Dark);
                    startWaveFeedback.SetBaseColor(WaitingColor);
                    startWaveFeedback.glowAlways = false;
                    break;
            }

            bool phaseChanged = !hasLastPhase || phase != lastPhase;
            if (phaseChanged)
            {
                // A new phase clears any leftover deck selection highlight.
                selectedDeckSlot = -1;
                UpdateDeckSelectionVisuals();

                // The draft screen already fills the view, so only announce the other phases.
                if (announce)
                {
                    if (phase == StagePhase.Preparation)
                    {
                        ShowPhaseBanner("PREPARATION", "DEPLOY YOUR SQUAD", PrepColor);
                    }
                    else if (phase == StagePhase.WaveActive)
                    {
                        ShowWaveBanner();
                        PlaySfx(waveStartClip, 1f);
                    }
                }
            }

            lastPhase = phase;
            hasLastPhase = true;
        }

        private void RetreatSelectedOperator()
        {
            OperatorBase selectedOperator = operatorManager != null ? operatorManager.SelectedOperator : null;
            if (selectedOperator != null)
            {
                operatorManager.RetreatOperator(selectedOperator);
            }
        }

        // ============================
        // Deck Slots
        // ============================

        private void HandleDeckChanged(IReadOnlyList<DraftCard> deck)
        {
            RefreshDeckSlots();
        }

        /// <summary>The merge animation's layer (above the deck bar; it moves itself to the top while playing).</summary>
        private void CreateDeckMergeAnimator(Transform root)
        {
            var layerObject = new GameObject("DeckMergeFx", typeof(RectTransform));
            layerObject.transform.SetParent(root, false);
            var layer = layerObject.GetComponent<RectTransform>();
            layer.anchorMin = Vector2.zero;
            layer.anchorMax = Vector2.one;
            layer.offsetMin = Vector2.zero;
            layer.offsetMax = Vector2.zero;

            deckMergeAnimator = layerObject.AddComponent<DeckMergeAnimator>();
            deckMergeAnimator.Initialize(
                layer,
                slot => deckButtons != null && slot >= 0 && slot < deckButtons.Length ? deckButtons[slot].GetComponent<RectTransform>() : null,
                SetDeckSlotHidden,
                // The full draft screen covers the deck bar: wait until it is minimized or closed
                () => draftOverlayUI == null || !draftOverlayUI.IsShowing);
        }

        /// <summary>Three copies merged: queue the merge animation (the deck has already changed).</summary>
        private void HandleCardsMerged(DeckMerge merge)
        {
            if (deckMergeAnimator == null || merge?.Result?.operatorData == null) return;

            var request = new DeckMergeAnimator.Request
            {
                ResultSlot = merge.ResultSlot,
                ResultCard = merge.Result,
                ResultPortrait = merge.Result.operatorData.portrait,
                NewStars = Mathf.Clamp((int)merge.Result.rarity, 1, 5),
                OldColor = GetOperatorRarityColor(Mathf.Clamp((int)merge.Result.rarity - 1, 1, 5)),
                NewColor = GetOperatorRarityColor(Mathf.Clamp((int)merge.Result.rarity, 1, 5))
            };
            for (int i = 0; i < merge.SourceSlots.Count; i++)
            {
                DraftCard card = i < merge.SourceCards.Count ? merge.SourceCards[i] : null;
                request.SourceSlots.Add(merge.SourceSlots[i]);
                request.SourcePortraits.Add(card != null && card.operatorData != null ? card.operatorData.portrait : request.ResultPortrait);
                request.SourceCards.Add(card);
                // The deck bar still shows these cards (OnDeckChanged comes after this event): copy them now, since
                // the deck has already moved on by the time their animation plays
                request.SourceSnapshots.Add(SnapshotDeckSlot(merge.SourceSlots[i]));
            }
            if (merge.IncomingCard != null)
            {
                request.SourceSlots.Add(-1);
                request.SourcePortraits.Add(merge.IncomingCard.operatorData != null ? merge.IncomingCard.operatorData.portrait : request.ResultPortrait);
                request.SourceCards.Add(merge.IncomingCard);
                request.SourceSnapshots.Add(null);
            }

            deckMergeAnimator.Enqueue(request);
        }

        /// <summary>
        /// A static copy of a deck slot as it looks now (portrait, name, stars, badges), for the merge animation. It
        /// takes no input: the button, drag and hover components are stripped.
        /// </summary>
        private GameObject SnapshotDeckSlot(int slot)
        {
            if (deckButtons == null || slot < 0 || slot >= deckButtons.Length || deckButtons[slot] == null) return null;

            GameObject source = deckButtons[slot].gameObject;
            GameObject copy = Instantiate(source, source.transform.parent);
            copy.name = $"MergeSnapshot_{slot + 1}";
            foreach (Behaviour behaviour in copy.GetComponentsInChildren<Behaviour>(true))
            {
                if (behaviour is Selectable || behaviour is MenuButtonFeedback || behaviour is DeckSlotDragHandler)
                {
                    behaviour.enabled = false;
                    Destroy(behaviour);
                }
            }

            CanvasGroup group = copy.GetComponent<CanvasGroup>();
            if (group == null) group = copy.AddComponent<CanvasGroup>();
            group.alpha = 1f; // the real slot may be hidden by an earlier merge
            group.blocksRaycasts = false;
            group.interactable = false;
            copy.transform.localScale = Vector3.one;
            return copy;
        }

        /// <summary>Hides a deck slot's content while a merge animation flies into it.</summary>
        private void SetDeckSlotHidden(int slot, bool hidden)
        {
            if (deckButtons == null || slot < 0 || slot >= deckButtons.Length || deckButtons[slot] == null) return;
            CanvasGroup group = deckButtons[slot].GetComponent<CanvasGroup>();
            if (group == null) group = deckButtons[slot].gameObject.AddComponent<CanvasGroup>();
            group.alpha = hidden ? 0f : 1f;
        }

        private void RefreshDeckSlots()
        {
            if (playerDeck == null || deckButtons == null) return;

            var deck = playerDeck.DeckSlots;

            for (int i = 0; i < DeckSlotCount; i++)
            {
                if (i < deck.Count && deck[i] != null && deck[i].operatorData != null)
                {
                    var card = deck[i];
                    string stars = new string('★', (int)card.rarity);
                    bool isReady = card.cooldownRoundsRemaining <= 0;
                    deckPortraitImages[i].sprite = card.operatorData.portrait;
                    deckPortraitImages[i].enabled = card.operatorData.portrait != null;
                    deckPortraitImages[i].color = isReady ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
                    Color classColor = GetClassColor(card.operatorData.operatorClass);
                    deckClassBadgeImages[i].color = Color.clear;
                    deckClassIconImages[i].sprite = GetClassIconSprite(card.operatorData.operatorClass);
                    deckClassIconImages[i].color = Color.white;
                    deckClassBadges[i].SetActive(true);
                    deckButtonLabels[i].text = $"{card.operatorData.operatorName}\n{stars}";
                    deckButtons[i].interactable = true;
                    deckCooldownLabels[i].text = card.cooldownRoundsRemaining.ToString();
                    deckCooldownBadges[i].SetActive(!isReady);

                    if (deckHeatBadges != null && i < deckHeatBadges.Length && deckHeatBadges[i] != null)
                    {
                        bool hasHeat = card.heatStacks > 0;
                        deckHeatBadges[i].SetActive(hasHeat);
                        if (hasHeat && deckHeatLabels[i] != null)
                        {
                            deckHeatLabels[i].text = $"🔥{card.heatStacks}";
                        }
                    }

                    // No slot fill (the bay art shows through); the class color only drives the
                    // hover/selection glow and the drag ghost (dimmed while on cooldown).
                    Color slotColor = isReady
                        ? GetClassColor(card.operatorData.operatorClass)
                        : new Color(0.22f, 0.22f, 0.24f, 0.7f);
                    deckFeedbacks[i].SetBaseColor(Color.clear);
                    Color glow = Color.Lerp(slotColor, Color.white, 0.55f);
                    deckFeedbacks[i].glowColor = new Color(glow.r, glow.g, glow.b, 0.9f);

                    int upgradeLevels = Mathf.Clamp((int)card.rarity - (int)card.operatorData.baseRarity, 0, 2);
                    deckUpgradeBadgeLabels[i].SetArrowCount(upgradeLevels);
                    deckUpgradeBadges[i].SetActive(upgradeLevels > 0);
                    deckButtons[i].GetComponent<DeckSlotDragHandler>().SetCard(card, slotColor);
                }
                else
                {
                    Color emptyColor = new Color(0.06f, 0.07f, 0.10f, 0f);
                    deckButtonLabels[i].text = "";
                    deckPortraitImages[i].sprite = null;
                    deckPortraitImages[i].enabled = false;
                    deckClassBadges[i].SetActive(false);
                    deckCooldownLabels[i].text = "";
                    deckCooldownBadges[i].SetActive(false);
                    if (deckHeatBadges != null && i < deckHeatBadges.Length && deckHeatBadges[i] != null)
                    {
                        deckHeatBadges[i].SetActive(false);
                    }
                    deckButtons[i].interactable = false;
                    deckFeedbacks[i].SetBaseColor(emptyColor);
                    deckUpgradeBadges[i].SetActive(false);
                    deckButtons[i].GetComponent<DeckSlotDragHandler>().SetCard(null, emptyColor);
                }
            }

            UpdateDeckSelectionVisuals();
        }

        private void UpdateDeckSelectionVisuals()
        {
            if (deckFeedbacks == null || playerDeck == null) return;

            var deck = playerDeck.DeckSlots;
            for (int i = 0; i < DeckSlotCount; i++)
            {
                if (deckFeedbacks[i] == null) continue;

                DraftCard card = i < deck.Count ? deck[i] : null;
                bool selected = i == selectedDeckSlot && card != null && card.cooldownRoundsRemaining <= 0;
                deckFeedbacks[i].glowAlways = selected;
                deckFeedbacks[i].restScale = selected ? 1.06f : 1f;
            }
        }

        private void FinishDeckCardDrag(int slotIndex, Vector2 screenPosition)
        {
            isDeckCardDragging = false;
            if (gameManager == null || playerDeck == null) return;
            if (!IsDeckDeployPhase && !IsDeckEditOnly) return;

            DraftCard card = playerDeck.GetCard(slotIndex);
            if (card == null) return;

            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (trashDropZone != null &&
                RectTransformUtility.RectangleContainsScreenPoint(trashDropZone, screenPosition, uiCamera))
            {
                stageBootstrapper?.CancelOperatorPlacementPreview(card);
                ShowDiscardConfirmation(slotIndex, card);
                return;
            }

            for (int targetIndex = 0; targetIndex < deckButtons.Length; targetIndex++)
            {
                RectTransform targetRect = deckButtons[targetIndex].GetComponent<RectTransform>();
                if (!RectTransformUtility.RectangleContainsScreenPoint(targetRect, screenPosition, uiCamera)) continue;

                stageBootstrapper?.CancelOperatorPlacementPreview(card);
                playerDeck.MoveCard(slotIndex, targetIndex);
                return;
            }

            // Edit-only phases and cooldown cards don't deploy: dropped anywhere else, the card stays in its slot
            if (IsDeckEditOnly || IsOnCooldown(card) || gridManager == null || stageBootstrapper == null) return;
            Camera gameCamera = Camera.main;
            if (gameCamera == null) return;

            Vector3 worldPosition = gameCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -gameCamera.transform.position.z));
            Vector2Int gridPosition = gridManager.WorldToGridPosition(worldPosition);
            if (!gridManager.IsInBounds(gridPosition))
            {
                stageBootstrapper.CancelOperatorPlacementPreview(card);
                return;
            }

            GridCell targetCell = gridManager.GetCell(gridPosition);
            if (targetCell != null && targetCell.CanDeploy(card.operatorData.position))
            {
                stageBootstrapper.BeginOperatorPlacement(card, gridPosition);
                // The drop is where the arrow schemes' controls appear (they stay hidden mid-drag)
                stageBootstrapper.RefreshPlacement();
            }
            else
            {
                stageBootstrapper.CancelOperatorPlacementPreview(card);
            }
        }

        private void HandleDeckCardDrag(int slotIndex, Vector2 screenPosition)
        {
            if (IsDeckEditOnly || playerDeck == null || gridManager == null || stageBootstrapper == null) return;
            isDeckCardDragging = true;

            DraftCard card = playerDeck.GetCard(slotIndex);
            if (card == null || IsOnCooldown(card)) return;

            Camera gameCamera = Camera.main;
            if (gameCamera == null) return;

            Vector3 worldPosition = gameCamera.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                -gameCamera.transform.position.z));
            Vector2Int gridPosition = gridManager.WorldToGridPosition(worldPosition);
            if (gridManager.IsInBounds(gridPosition))
            {
                stageBootstrapper.BeginOperatorPlacement(card, gridPosition);
            }
            else
            {
                stageBootstrapper.CancelOperatorPlacementPreview(card);
            }
        }

        private Color GetClassColor(OperatorClass opClass)
        {
            return opClass switch
            {
                OperatorClass.Guard => new Color(0.55f, 0.15f, 0.15f, 1f),     // Red-tinted
                OperatorClass.Defender => new Color(0.15f, 0.25f, 0.55f, 1f),   // Blue-tinted
                OperatorClass.Sniper => new Color(0.15f, 0.50f, 0.15f, 1f),     // Green-tinted
                OperatorClass.Caster => new Color(0.45f, 0.15f, 0.55f, 1f),     // Purple-tinted
                OperatorClass.Medic => new Color(0.45f, 0.45f, 0.15f, 1f),      // Yellow-tinted
                OperatorClass.Specialist => new Color(0.15f, 0.45f, 0.45f, 1f), // Teal-tinted
                _ => new Color(0.06f, 0.07f, 0.10f, 1f)
            };
        }

        private void SelectDeckSlot(int index)
        {
            if (playerDeck == null) return;

            var card = playerDeck.GetCard(index);
            if (card == null) return;

            // Every click on a card toggles its info tab, in any phase and cooldown or not
            ToggleDeckCardInfo(index, card);

            if (IsOnCooldown(card))
            {
                ShowCooldownWarning(
                    card.operatorData != null ? card.operatorData.operatorName : null,
                    card.cooldownRoundsRemaining);
                return;
            }
            if (IsDeckEditOnly) return;

            // Notify deck and draft system that this card was selected for deployment
            playerDeck.SelectCardForDeployment(index);
            if (draftSystem != null)
            {
                draftSystem.NotifyCardSelected(card);
            }

            selectedDeckSlot = index;
            UpdateDeckSelectionVisuals();

            Debug.Log($"[Deck] Selected slot {index}: {card.operatorData.operatorName} ({card.rarity}) - click or drag to a valid tile to deploy.");
        }

        public void SetSelectedDeckSlot(int index)
        {
            selectedDeckSlot = index;
            UpdateDeckSelectionVisuals();
        }

        private bool IsDeckCardInfoOpen => deckInfoSlot >= 0;

        private void ToggleDeckCardInfo(int index, DraftCard card)
        {
            if (deckInfoSlot == index && ReferenceEquals(deckInfoCard, card) && selectedOperatorDetailsPanel != null && selectedOperatorDetailsPanel.activeSelf)
            {
                ClearDeckCardInfo();
                return;
            }

            // One info tab at a time: drop the field selection first (its handler clears the map previews,
            // so this must run before the card is selected for deployment and draws its tile hints)
            if (operatorManager != null && operatorManager.SelectedOperator != null) operatorManager.SelectOperator(null);
            HideFramiliarCopyMenu();

            deckInfoSlot = index;
            deckInfoCard = card;
            snapHealthFill = true;
            if (selectedOperatorDetailsPanel != null)
            {
                selectedOperatorDetailsPanel.SetActive(true);
                UpdateDeckCardDetails(card);
            }
        }

        private void ClearDeckCardInfo()
        {
            if (deckInfoSlot < 0 && (selectedOperatorDetailsPanel == null || !selectedOperatorDetailsPanel.activeSelf || selectedOperatorDetailsTarget != null)) return;
            deckInfoSlot = -1;
            deckInfoCard = null;
            SetSelectedOperatorName(string.Empty, Vector3.zero); // also closes the SHOW MORE panel
            if (selectedOperatorDetailsTarget == null && selectedOperatorDetailsPanel != null)
            {
                selectedOperatorDetailsPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Keeps the deck card's info tab above its slot. Closes it when the card leaves the slot (moved,
        /// discarded, deployed), when it goes onto the map, or on a click outside the UI. False when no tab shows.
        /// </summary>
        private bool RefreshDeckCardInfo()
        {
            if (deckInfoSlot < 0) return false;

            bool clickedOutside = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject());
            DraftCard card = playerDeck != null ? playerDeck.GetCard(deckInfoSlot) : null;
            if (card == null || card.operatorData == null || !ReferenceEquals(card, deckInfoCard) ||
                deckButtons == null || deckInfoSlot >= deckButtons.Length || canvas == null ||
                clickedOutside || IsDeckCardDragging)
            {
                ClearDeckCardInfo();
                return false;
            }

            // Bottom-centre of the tab sits just above the slot's top edge
            RectTransform slotRect = deckButtons[deckInfoSlot].GetComponent<RectTransform>();
            slotRect.GetWorldCorners(deckInfoSlotCorners);
            Vector3 topCentre = (deckInfoSlotCorners[1] + deckInfoSlotCorners[2]) * 0.5f;
            Camera canvasCamera = GetCanvasCamera();
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(canvasCamera, topCentre);
            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out Vector2 localPosition))
                return false;

            OperatorData data = card.operatorData;
            int maxHP = data.GetScaledHP(card.rarity);
            int currentHP = card.savedHP >= 0 ? Mathf.Min(card.savedHP, maxHP) : maxHP;
            selectedOperatorNameText.text = data.operatorName;
            PlaceSelectedOperatorLabel(localPosition + new Vector2(0f, 8f), false);
            ApplySelectedOperatorStats(currentHP, maxHP, data.GetScaledATK(card.rarity), card.rarity, data.baseRarity);
            return true;
        }

        // ============================
        // Actions
        // ============================

        private void StartWave()
        {
            if (IsSandbox)
            {
                sandboxController.ToggleWave();
                return;
            }
            if (gameManager == null || waveManager == null) return;
            if (gameManager.CurrentPhase != StagePhase.Preparation) return;
            if (TutorialDirector.Instance != null && !TutorialDirector.Instance.AllowsWaveStart)
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiDenied);
                return;
            }

            // Enter wave phase
            gameManager.EnterWavePhase();

            // Set wave index and start
            waveManager.SetWaveIndex(gameManager.GetCurrentWaveIndex());
            waveManager.StartNextWave();
        }

        public void RegisterSandboxController(TrashTD.Development.SandboxController controller)
        {
            sandboxController = controller;
            SetSandboxWaveButton(waveManager != null && waveManager.IsWaveInProgress);
        }

        /// <summary>
        /// Parents the sandbox control panel onto the HUD canvas, below the pause overlay.
        /// </summary>
        public void AttachSandboxPanel(RectTransform panel)
        {
            if (panel == null || canvas == null) return;
            panel.SetParent(canvas.transform, false);
            if (pausePanel != null)
                panel.SetSiblingIndex(pausePanel.transform.GetSiblingIndex());
        }

        public void SetSandboxWaveButton(bool active)
        {
            if (startWaveButton == null) return;
            startWaveButton.interactable = true;
            ApplyButtonContainer(startWaveButton, active ? ButtonContainerType.Danger : ButtonContainerType.Primary);
            if (startWaveButtonText != null)
                startWaveButtonText.text = active ? "END\nWAVE" : "START\nWAVE";
        }

        public bool IsPaused => (pausePanel != null && pausePanel.activeSelf) || (gameManager != null && gameManager.CurrentState == GamePlayState.Paused);
        public bool IsPauseMenuOpen => pausePanel != null && pausePanel.activeSelf;

        // Highlight targets for the tutorial (TutorialDialogueUI)
        public RectTransform StartWaveButtonRect => startWaveButton != null ? startWaveButton.GetComponent<RectTransform>() : null;
        public RectTransform LivesRect => livesTubeRect != null ? livesTubeRect.parent as RectTransform : null;
        public RectTransform RetreatButtonRect =>
            retreatOperatorButton != null && retreatOperatorButton.gameObject.activeInHierarchy ? retreatOperatorButton.GetComponent<RectTransform>() : null;

        /// <summary>The deck slot holding a card of this operator (any rarity; ready ones only by default), or null.</summary>
        public RectTransform GetDeckSlotRect(OperatorData operatorData, bool readyOnly = true)
        {
            if (playerDeck == null || deckButtons == null || operatorData == null) return null;
            var deck = playerDeck.DeckSlots;
            for (int i = 0; i < deck.Count && i < deckButtons.Length; i++)
            {
                if (deck[i] != null && deck[i].operatorData == operatorData && (!readyOnly || deck[i].cooldownRoundsRemaining <= 0) && deckButtons[i] != null)
                    return deckButtons[i].GetComponent<RectTransform>();
            }
            return null;
        }
        public bool HasConsumedEscapeThisFrame => Time.frameCount == lastEscapeHandledFrame;

        /// <summary>True while a text field (the sandbox search) has keyboard focus: typing mustn't trigger hotkeys.</summary>
        public static bool IsTypingInTextField()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null) return false;
            InputField field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        public void ConsumeEscapeKey()
        {
            lastEscapeHandledFrame = Time.frameCount;
            if (IsFramiliarCopyMenuOpen)
            {
                HideFramiliarCopyMenu();
                if (operatorManager != null) operatorManager.SelectOperator(null);
                return;
            }
        }

        public void TogglePause()
        {
            if (gameManager == null) return;
            if (gameManager.CurrentState == GamePlayState.Paused)
                ResumeGame();
            else if (gameManager.CurrentState == GamePlayState.Playing)
                PauseGame();
        }

        public void HandleEscapeKey()
        {
            if (Time.frameCount == lastEscapeHandledFrame) return;
            lastEscapeHandledFrame = Time.frameCount;
            if (IsFramiliarCopyMenuOpen)
            {
                HideFramiliarCopyMenu();
                if (operatorManager != null) operatorManager.SelectOperator(null);
                return;
            }

#if UNITY_EDITOR || DEBUG
            if (draftOverlayUI != null && draftOverlayUI.IsDeveloperPickerOpen)
            {
                draftOverlayUI.CloseDeveloperPicker();
                return;
            }
#endif

            if (intelUI != null && intelUI.HandleBack())
            {
                return;
            }

            if (audioSettingsUI != null && audioSettingsUI.IsOpen)
            {
                audioSettingsUI.Close();
                return;
            }

            if (IsPaused)
            {
                ResumeGame();
                return;
            }

            if (discardConfirmationPanel != null && discardConfirmationPanel.activeSelf)
            {
                CancelDiscard();
                return;
            }

            if (environmentBriefingPanel != null && environmentBriefingPanel.activeSelf)
            {
                DismissEnvironmentBriefing();
                return;
            }

            if (pathPreviewOverlay != null && pathPreviewOverlay.IsVisible)
            {
                TogglePathPreview();
                return;
            }

            // Closing a deck card's info tab also cancels that card's selection, if any, in the same press
            bool closedDeckInfo = IsDeckCardInfoOpen;
            if (closedDeckInfo) ClearDeckCardInfo();

            if (stageBootstrapper != null && stageBootstrapper.IsPlacementOrSelectionActive())
            {
                stageBootstrapper.CancelPlacementOrSelection();
                return;
            }

            if (closedDeckInfo) return;

            if (gameManager != null && gameManager.CurrentState == GamePlayState.Playing)
            {
                PauseGame();
            }
        }

        // ─────────────────────────────────────────────────────
        //  Hide / show the HUD (H)
        // ─────────────────────────────────────────────────────

        private bool hudHidden;
        private GameObject hudHiddenHint;

        /// <summary>True while the player has hidden the HUD with H (a clean view of the map).</summary>
        public bool IsHudHidden => hudHidden;

        /// <summary>
        /// Hides the whole HUD canvas (bars, deck, surveillance feed, notices) while <see cref="GameplaySettings.HideHud"/>
        /// is on (Settings → Gameplay), and H flips that setting. Only while a wave or preparation is being played: the
        /// HUD comes back by itself on pause, when the draft opens and when the stage ends (and hides again afterwards),
        /// and nothing hides it in the tutorial or while typing in a text field. The map stays clickable, and the
        /// deploy hotkeys keep working; a small hint stays up so the key isn't forgotten.
        /// </summary>
        private void HandleHudToggleKey(Keyboard keyboard)
        {
            bool canHide = gameManager != null && gameManager.CurrentState == GamePlayState.Playing && !IsPaused &&
                           !(draftOverlayUI != null && draftOverlayUI.IsShowing) && TutorialDirector.Instance == null;

            bool hPressed = keyboard != null && keyboard.hKey.wasPressedThisFrame && !IsTypingInTextField();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!hPressed && Input.GetKeyDown(KeyCode.H) && !IsTypingInTextField()) hPressed = true;
#endif
            if (hPressed && canHide)
            {
                GameplaySettings.HideHud = !GameplaySettings.HideHud;
                AudioManager.Instance?.PlaySfx(SfxId.UiClick, sfxVolume);
            }

            SetHudHidden(canHide && GameplaySettings.HideHud);
        }

        private void SetHudHidden(bool hidden)
        {
            if (hidden == hudHidden || canvas == null) return;

            hudHidden = hidden;
            canvas.enabled = !hidden;
            if (hudHiddenHint != null) hudHiddenHint.SetActive(hidden);
        }

        /// <summary>A small, faint "UI HIDDEN [H]" reminder on a canvas of its own (the HUD canvas is switched off).</summary>
        private void CreateHudHiddenHint()
        {
            hudHiddenHint = new GameObject("HudHiddenHint", typeof(Canvas), typeof(CanvasScaler));
            var hintCanvas = hudHiddenHint.GetComponent<Canvas>();
            hintCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hintCanvas.sortingOrder = 101;
            var hintScaler = hudHiddenHint.GetComponent<CanvasScaler>();
            hintScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hintScaler.referenceResolution = new Vector2(1920f, 1080f);
            hintScaler.matchWidthOrHeight = 0.5f;
            hudHiddenHint.transform.SetParent(transform, false);

            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(hudHiddenHint.transform, false);
            var text = textObject.GetComponent<Text>();
            text.text = "UI HIDDEN   [ H ]";
            text.font = UIFontHelper.GetPixelFont();
            text.fontSize = 12;
            text.alignment = TextAnchor.LowerLeft;
            text.color = new Color(1f, 1f, 1f, 0.4f);
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(16f, 12f);
            rect.sizeDelta = new Vector2(260f, 20f);

            hudHiddenHint.SetActive(false);
        }

        private void HandleKeyboardInput()
        {
            var keyboard = Keyboard.current;
            HandleHudToggleKey(keyboard);
            bool escapePressed = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!escapePressed && Input.GetKeyDown(KeyCode.Escape)) escapePressed = true;
#endif
            if (escapePressed)
            {
                HandleEscapeKey();
            }

            bool pPressed = keyboard != null && keyboard.pKey.wasPressedThisFrame && !IsTypingInTextField();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!pPressed && Input.GetKeyDown(KeyCode.P)) pPressed = true;
#endif
            if (pPressed && previewPathButton != null && previewPathButton.gameObject.activeInHierarchy)
            {
                TogglePathPreview();
            }
        }

        private void PauseGame()
        {
            if (gameManager == null) return;
            gameManager.PauseGame();
            AudioManager.Instance?.PlaySfx(SfxId.GameplayPause);
            SetPauseVisible(true);
        }

        private void ResumeGame()
        {
            if (gameManager == null) return;
            if (audioSettingsUI != null && audioSettingsUI.IsOpen)
            {
                audioSettingsUI.Close();
            }
            if (intelUI != null && intelUI.IsOpen)
            {
                intelUI.Close();
            }
            gameManager.ResumeGame();
            AudioManager.Instance?.PlaySfx(SfxId.GameplayResume);
            SetPauseVisible(false);
        }

        /// <summary>Restarts the current stage on the same difficulty — same flow as the results screen's Retry.</summary>
        private void RetryStage()
        {
            if (gameManager != null)
            {
                MainMenuController.SetPendingStage(gameManager.CurrentStage);
                MainMenuController.SetPendingStageDifficulty(gameManager.CurrentDifficulty);
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void ExitToMenu()
        {
            Time.timeScale = 1f;
            MainMenuController.ReturnToStageSelectorOnLoad();
            SceneManager.LoadScene("MainMenu");
        }

        private void SetPauseVisible(bool show)
        {
            if (pausePanel == null) return;

            if (pauseRoutine != null) StopCoroutine(pauseRoutine);
            pauseRoutine = StartCoroutine(PauseFadeRoutine(show));
        }

        // Uses unscaled time so it still animates while the game is paused (timeScale 0).
        private IEnumerator PauseFadeRoutine(bool show)
        {
            float duration = show ? 0.22f : 0.14f;
            float from = pauseGroup.alpha;
            float to = show ? 1f : 0f;

            if (show) pausePanel.SetActive(true);
            pauseGroup.interactable = show;
            pauseGroup.blocksRaycasts = show;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                pauseGroup.alpha = Mathf.Lerp(from, to, EaseOutCubic(k));

                float s = show ? Mathf.LerpUnclamped(0.92f, 1f, EaseOutBack(k)) : Mathf.Lerp(1f, 0.96f, k);
                pauseCard.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            pauseGroup.alpha = to;
            pauseCard.localScale = Vector3.one;
            if (!show) pausePanel.SetActive(false);
        }
    }
}
