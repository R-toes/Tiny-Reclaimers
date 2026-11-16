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
    // The details panel, the player actions (pick, confirm, reroll, skip) and UI state updates.
    public partial class CardDraftOverlayUI
    {
        // ============================
        // Detail Panel
        // ============================

        private void UpdateDetailsPanel(bool animate)
        {
            if (detailContentRoot == null) return;

            DraftCard card = null;
            if (selectedCardIndex >= 0 && draftSystem != null && selectedCardIndex < draftSystem.CurrentOfferedCards.Count)
            {
                card = draftSystem.CurrentOfferedCards[selectedCardIndex];
            }

            bool hasCard = card != null && card.operatorData != null;
            detailContentRoot.SetActive(hasCard);
            detailEmptyRoot.SetActive(!hasCard);

            if (!hasCard)
            {
                detailAccent.color = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.35f);
                return;
            }

            OperatorData op = card.operatorData;
            int stars = Mathf.Clamp((int)card.rarity, 1, 5);
            Color rarityColor = GetRarityColor(stars);

            detailAccent.color = rarityColor;
            detailNameText.text = op.operatorName;
            detailStarsText.text = BuildStarString(stars);
            detailStarsText.color = rarityColor;

            // Type chips: class / position / damage type
            SetChip(detailClassChip, op.operatorClass.ToString().ToUpper(), GetClassColor(op.operatorClass));
            detailClassChip.icon.sprite = GameplayHUDUI.GetClassIconSprite(op.operatorClass);
            detailClassChip.icon.color = Color.white;
            detailClassChip.icon.enabled = detailClassChip.icon.sprite != null;
            bool isMelee = op.position == OperatorPosition.Melee;
            SetChip(detailPositionChip, isMelee ? "MELEE" : "RANGED", isMelee ? MELEE_COLOR : RANGED_COLOR);
            GetDamageType(op, out string damageLabel, out Color damageColor);
            SetChip(detailDamageChip, damageLabel, damageColor);

            detailRoleText.text = GetOperatorDescription(op);

            // Stats (scaled by rarity)
            detailHpText.text = op.GetScaledHP(card.rarity).ToString();
            detailAtkText.text = op.GetScaledATK(card.rarity).ToString();
            detailDefText.text = op.GetScaledDEF(card.rarity).ToString();
            detailResText.text = op.GetScaledRES(card.rarity).ToString();
            detailBlockText.text = op.blockCount.ToString();
            detailIntervalText.text = op.attackInterval.ToString("0.##") + "s";

            // Attack range pattern
            Vector2Int[] rangePattern = op.GetRangePatternForRarity(card.rarity);
            UpdateRangePreview(op, rangePattern);

            // Passive / ability text (only shown when the operator has one)
            string passive = FindStringMember(op, PassiveMemberNames);
            bool hasPassive = !string.IsNullOrWhiteSpace(passive);
            detailPassiveLabel.gameObject.SetActive(hasPassive);
            if (hasPassive)
            {
                detailPassiveLabel.color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.9f);
                detailPassiveText.text = passive.Trim();

                // A newly selected card starts at the top of its text
                if (detailPassiveScroll != null)
                {
                    detailPassiveScroll.gameObject.SetActive(true);
                    LayoutRebuilder.ForceRebuildLayoutImmediate(detailPassiveText.rectTransform);
                    detailPassiveScroll.verticalNormalizedPosition = 1f;
                }
            }
            else if (detailPassiveScroll != null)
            {
                detailPassiveScroll.gameObject.SetActive(false);
            }

            if (animate) PlayDetailsAnimation();
        }

        private static void SetChip(Chip chip, string label, Color color)
        {
            chip.text.text = label;
            chip.text.color = Color.Lerp(color, Color.white, 0.35f);
            chip.bg.color = Color.Lerp(TILE_BG, color, 0.22f);
            chip.outline.effectColor = new Color(color.r, color.g, color.b, 0.55f);
        }

        private static void GetDamageType(OperatorData op, out string label, out Color color)
        {
            // Uses a damage-type member on OperatorData if one exists (see DamageTypeMemberNames),
            // otherwise falls back to a guess based on the operator's class.
            string raw = FindStringMember(op, DamageTypeMemberNames);
            string upper = !string.IsNullOrEmpty(raw)
                ? raw.ToUpperInvariant()
                : (op.operatorClass == OperatorClass.Caster ? "ARTS" : op.operatorClass == OperatorClass.Medic ? "HEAL" : "PHYSICAL");

            if (upper.Contains("PHYS"))
            {
                label = "PHYSICAL";
                color = new Color(0.90f, 0.45f, 0.35f, 1f);
            }
            else if (upper.Contains("ART") || upper.Contains("MAGIC"))
            {
                label = "ARTS";
                color = new Color(0.70f, 0.50f, 1.00f, 1f);
            }
            else if (upper.Contains("HEAL"))
            {
                label = "HEAL";
                color = new Color(0.40f, 0.85f, 0.50f, 1f);
            }
            else
            {
                label = upper;
                color = STAT_LABEL_COLOR;
            }
        }

        /// <summary>
        /// Looks for a string/enum field or property by name (first match wins).
        /// Lets the panel pick up optional OperatorData members without hard-coding their names.
        /// </summary>
        private static string FindStringMember(object target, string[] memberNames)
        {
            if (target == null) return null;

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase;
            Type type = target.GetType();

            foreach (string memberName in memberNames)
            {
                object value = null;

                FieldInfo field = type.GetField(memberName, flags);
                if (field != null)
                {
                    value = field.GetValue(target);
                }
                else
                {
                    PropertyInfo property = type.GetProperty(memberName, flags);
                    if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                    {
                        value = property.GetValue(target);
                    }
                }

                if (value is string text && !string.IsNullOrWhiteSpace(text)) return text;
                if (value != null && value.GetType().IsEnum) return value.ToString();
            }

            return null;
        }

        private void PlayDetailsAnimation()
        {
            if (!isActiveAndEnabled) return;

            if (detailsRoutine != null) StopCoroutine(detailsRoutine);
            detailsRoutine = StartCoroutine(DetailsRoutine());
        }

        private IEnumerator DetailsRoutine()
        {
            const float duration = 0.22f;

            detailContentGroup.alpha = 0f;
            detailContentRect.anchoredPosition = new Vector2(24f, 0f);

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = EaseOutCubic(Mathf.Clamp01(t / duration));
                detailContentGroup.alpha = k;
                detailContentRect.anchoredPosition = new Vector2(24f * (1f - k), 0f);
                yield return null;
            }

            detailContentGroup.alpha = 1f;
            detailContentRect.anchoredPosition = Vector2.zero;
        }

        // ============================
        // User Actions
        // ============================

        private void OnCardClicked(int index)
        {
            ClearUiSelection();

            // Cards can't be touched while they are being shuffled and dealt.
            if (isShuffling) return;

            if (index < 0 || draftSystem == null || index >= draftSystem.CurrentOfferedCards.Count)
                return;

            // A face-down card flips over first.
            if (!cardRevealed[index])
            {
                RevealCard(index);
                if (!selectOnReveal) return;
            }

            bool changed = selectedCardIndex != index;
            selectedCardIndex = index;

            // Update card highlights + detail panel
            ApplySelectionVisuals();
            UpdateDetailsPanel(changed);

            UpdateConfirmButton();
        }

        private void OnConfirmClicked()
        {
            ClearUiSelection();

            // Only one pick per draft, and only while the draft can actually end (not paused):
            // EnterPreparationPhase does nothing outside Playing, which used to leave the offer up for another pick.
            if (pickConfirmed || !IsActiveGameplayCardPick())
                return;

            if (selectedCardIndex < 0 || draftSystem == null || playerDeck == null)
                return;

            if (IsTutorialDraft && !TutorialDirector.Instance.CanPickCard(GetSelectedOfferedCard()))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiDenied);
                return;
            }

            if (!playerDeck.CanAcceptCard(GetSelectedOfferedCard()))
            {
                Debug.LogWarning("PlayerDeck: Deck is full! Cannot add more cards.");
                return;
            }

            // Select the card from the draft
            int pickedIndex = selectedCardIndex;
            if (draftSystem.SelectCard(selectedCardIndex, out DraftCard selectedCard))
            {
                pickConfirmed = true;
                pickedCardIndex = pickedIndex;
                if (cardFeedbacks[pickedIndex] != null) cardFeedbacks[pickedIndex].Punch();
                // Add to player deck
                playerDeck.AddCard(selectedCard);

                // Also register with the rarity upgrade system
                // (already handled inside CardDraftSystem.SelectCard)

                PlaySfx(confirmClip, 1f);
                Debug.Log($"<color=cyan>[Draft] Added {selectedCard.operatorData.operatorName} ({selectedCard.rarity}) to deck!</color>");
            }

            // Transition to preparation phase
            if (gameManager != null)
            {
                gameManager.EnterPreparationPhase();
            }
        }

        private void OnRerollClicked()
        {
            ClearUiSelection();

            if (draftSystem == null || !IsActiveGameplayCardPick() || IsTutorialDraft) return;

            // Wait for the current shuffle to finish before shuffling again.
            if (isShuffling) return;

            if (draftSystem.IsOpeningDraft && draftSystem.RerollsRemaining > 0 && !rerollNoticeAcknowledged &&
                GameplaySettings.ShowOpeningRerollNotice)
            {
                ShowRerollNotice();
                return;
            }

            Reroll();
        }

        private void Reroll()
        {
            var newOffer = draftSystem.RerollOffer();
            if (newOffer == null)
            {
                Debug.LogWarning("No rerolls remaining!");
            }
            else
            {
                PlaySfx(rerollClip, 1f);
            }

            UpdateRerollButton();
        }

        private void OnSkipClicked()
        {
            ClearUiSelection();
            if (!IsActiveGameplayCardPick() || IsTutorialDraft) return;

            // Skip card pick and go straight to preparation
            if (gameManager != null)
            {
                gameManager.EnterPreparationPhase();
            }
        }

        // ============================
        // UI State Updates
        // ============================

        private void UpdateConfirmButton()
        {
            DraftCard selectedCard = GetSelectedOfferedCard();
            bool canConfirm = selectedCard != null && playerDeck != null && playerDeck.CanAcceptCard(selectedCard);
            confirmButton.interactable = canConfirm;

            GameplayHUDUI.ApplyButtonContainer(confirmButton, canConfirm ? GameplayHUDUI.ButtonContainerType.Primary : GameplayHUDUI.ButtonContainerType.Dark);
            confirmFeedback.SetBaseColor(Color.white);
            confirmFeedback.glowAlways = canConfirm;

            var checkIcon = confirmButton.transform.Find("Icon")?.GetComponent<Image>();
            if (checkIcon != null)
            {
                checkIcon.color = canConfirm ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            if (playerDeck != null && playerDeck.IsFull && canConfirm)
            {
                // A full deck still takes a card that merges on arrival.
                confirmButtonText.text = "CONFIRM: MERGES";
            }
            else if (playerDeck != null && playerDeck.IsFull)
            {
                confirmButtonText.text = "DECK FULL: DISCARD IN MAP & DECK";
            }
            else if (selectedCardIndex < 0)
            {
                confirmButtonText.text = "SELECT A CARD";
            }
            else
            {
                confirmButtonText.text = "CONFIRM CARD";
            }

            confirmButtonText.color = canConfirm ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }

        private DraftCard GetSelectedOfferedCard()
        {
            if (draftSystem == null || selectedCardIndex < 0 || selectedCardIndex >= draftSystem.CurrentOfferedCards.Count)
                return null;
            return draftSystem.CurrentOfferedCards[selectedCardIndex];
        }

        private void UpdateRerollButton()
        {
            if (draftSystem == null) return;

            int remaining = draftSystem.RerollsRemaining;
            bool canReroll = remaining > 0 && !isShuffling;
            rerollButton.interactable = canReroll;

            GameplayHUDUI.ApplyButtonContainer(rerollButton, canReroll ? GameplayHUDUI.ButtonContainerType.Warning : GameplayHUDUI.ButtonContainerType.Dark);
            rerollFeedback.SetBaseColor(Color.white);

            rerollButtonText.text = $"REROLL ({remaining})";
            rerollButtonText.color = canReroll ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        }

        private void UpdateDeckCount()
        {
            if (deckCountText != null && playerDeck != null)
            {
                deckCountText.text = $"DECK {playerDeck.CardCount}/{PlayerDeck.MAX_DECK_SIZE}";
                deckCountText.color = playerDeck.IsFull ? DECK_FULL_COLOR : Color.white;
            }
        }

        private void UpdateWaveInfo()
        {
            if (waveInfoText != null && gameManager != null)
            {
                var waveManager = FindAnyObjectByType<WaveManager>();
                if (waveManager != null)
                {
                    int nextWave = gameManager.GetCurrentWaveIndex() + 1;
                    waveInfoText.text = $"WAVE {nextWave}/{waveManager.TotalWaves}";
                }
            }
        }
    }
}
