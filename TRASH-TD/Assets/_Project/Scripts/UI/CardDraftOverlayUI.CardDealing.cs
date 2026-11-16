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
    // The card faces: shuffle, deal, flip, reveal and rarity effects.
    public partial class CardDraftOverlayUI
    {
        // ============================
        // Card Faces: shuffle, deal and flip
        // ============================

        private void SetFaceVisual(int index, bool faceUp)
        {
            if (cardFronts[index] != null) cardFronts[index].SetActive(faceUp);
            if (cardBacks[index] != null) cardBacks[index].SetActive(!faceUp);
        }

        private void SetCardsInteractable(bool value)
        {
            for (int i = 0; i < 3; i++)
            {
                if (cardButtonsArr[i] != null) cardButtonsArr[i].interactable = value;
            }
        }

        private void SetCardPose(int index, Vector2 position, float rotationZ)
        {
            cardRects[index].anchoredPosition = position;
            cardRects[index].localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        }

        private static Vector2 StackPosition(int orderIndex, int count)
        {
            float offset = (orderIndex - (count - 1) * 0.5f) * 4f;
            return new Vector2(offset, -offset);
        }

        private void ApplySiblingOrder(List<int> bottomToTop)
        {
            for (int a = 0; a < bottomToTop.Count; a++) cardRects[bottomToTop[a]].SetAsLastSibling();
        }

        /// <summary>Puts every card face-down on one stack in the middle, hidden until the shuffle starts.</summary>
        private void ResetCardsForNewDraft()
        {
            if (dealRoutine != null)
            {
                StopCoroutine(dealRoutine);
                dealRoutine = null;
            }

            StopAllFlips();

            freshDraft = true;
            offerApplied = false;
            isShuffling = true;

            for (int i = 0; i < 3; i++)
            {
                if (cardPanels[i] == null) continue;

                cardPanels[i].SetActive(true);
                cardRevealed[i] = false;
                SetFaceVisual(i, false);
                cardRects[i].anchoredPosition = Vector2.zero;
                cardRects[i].localRotation = Quaternion.identity;
                cardFeedbacks[i].introScale = StackScale;
                cardFeedbacks[i].horizontalScale = 1f;
                cardGroups[i].alpha = 0f;
                cardLifts[i] = 0f;
            }

            SetCardsInteractable(false);
        }

        private void StartShuffleDeal()
        {
            StopAllFlips();

            if (dealRoutine != null)
            {
                StopCoroutine(dealRoutine);
                dealRoutine = null;
            }

            bool skipAnimation = SkipCardAnimation;
            if (!isActiveAndEnabled || skipAnimation)
            {
                // Can't (or shouldn't) animate: fill the cards and leave them in their slots,
                // face-up when the player skips card animations, otherwise face-down.
                ApplyOfferToCards();
                offerApplied = true;
                freshDraft = false;
                if (skipAnimation) RevealAllCardsInstantly();
                SettleCards();
                return;
            }

            isShuffling = true;
            offerApplied = false;
            for (int i = 0; i < 3; i++) cardLifts[i] = 0f;
            SetCardsInteractable(false);
            dealRoutine = StartCoroutine(ShuffleDealRoutine());
        }

        /// <summary>
        /// gather (flip back any revealed cards and slide them into a stack) -> fill with the new offer ->
        /// riffle shuffle -> deal back out, still face-down.
        /// </summary>
        private IEnumerator ShuffleDealRoutine()
        {
            bool fresh = freshDraft;

            var order = new List<int>(3); // draw order, bottom -> top
            for (int i = 0; i < 3; i++) if (cardPanels[i].activeSelf) order.Add(i);

            // --- 1. Gather into a stack (skipped for the first deal of a draft) ---
            if (!fresh && order.Count > 0)
            {
                var startPos = new Vector2[3];
                var wasRevealed = new bool[3];
                var flippedBack = new bool[3];
                for (int a = 0; a < order.Count; a++)
                {
                    int i = order[a];
                    startPos[i] = cardRects[i].anchoredPosition;
                    wasRevealed[i] = cardRevealed[i];
                }

                float t = 0f;
                while (t < GatherTime)
                {
                    t += Time.unscaledDeltaTime;
                    float k = EaseInOutCubic(Mathf.Clamp01(t / GatherTime));

                    for (int a = 0; a < order.Count; a++)
                    {
                        int i = order[a];
                        cardRects[i].anchoredPosition = Vector2.Lerp(startPos[i], StackPosition(a, order.Count), k);
                        cardFeedbacks[i].introScale = Mathf.Lerp(1f, StackScale, k);

                        if (wasRevealed[i])
                        {
                            float fk = Mathf.Clamp01(t / GatherFlipTime);
                            cardFeedbacks[i].horizontalScale = Mathf.Max(0.001f, Mathf.Abs(Mathf.Cos(fk * Mathf.PI)));
                            if (fk >= 0.5f && !flippedBack[i])
                            {
                                flippedBack[i] = true;
                                SetFaceVisual(i, false);
                            }
                        }
                    }

                    yield return null;
                }

                for (int a = 0; a < order.Count; a++)
                {
                    int i = order[a];
                    cardFeedbacks[i].horizontalScale = 1f;
                    SetFaceVisual(i, false);
                }
            }

            // --- 2. Every card is face-down: now it is safe to write the new offer onto them ---
            ApplyOfferToCards();
            offerApplied = true;
            freshDraft = false;

            order.Clear();
            for (int i = 0; i < 3; i++) if (cardPanels[i].activeSelf) order.Add(i);
            int n = order.Count;

            for (int a = 0; a < n; a++)
            {
                int i = order[a];
                SetCardPose(i, StackPosition(a, n), 0f);
                cardFeedbacks[i].introScale = StackScale;
                cardFeedbacks[i].horizontalScale = 1f;
            }

            ApplySiblingOrder(order);

            if (fresh)
            {
                float fade = 0f;
                while (fade < 0.18f)
                {
                    fade += Time.unscaledDeltaTime;
                    float alpha = Mathf.Clamp01(fade / 0.18f);
                    for (int a = 0; a < n; a++) cardGroups[order[a]].alpha = alpha;
                    yield return null;
                }
            }

            for (int a = 0; a < n; a++) cardGroups[order[a]].alpha = 1f;

            // --- 3. Riffle shuffle: the top and bottom cards swing out and trade places ---
            int cycles = fresh ? 2 : 3;
            if (n >= 2)
            {
                for (int c = 0; c < cycles; c++)
                {
                    int topCard = order[n - 1];
                    int bottomCard = order[0];
                    PlaySfx(shuffleClip, 0.85f + 0.12f * c);

                    float t = 0f;
                    while (t < RiffleOutTime)
                    {
                        t += Time.unscaledDeltaTime;
                        float k = EaseOutCubic(Mathf.Clamp01(t / RiffleOutTime));
                        SetCardPose(topCard, StackPosition(n - 1, n) + new Vector2(-RiffleSpread * k, 14f * k), 8f * k);
                        SetCardPose(bottomCard, StackPosition(0, n) + new Vector2(RiffleSpread * k, -14f * k), -8f * k);
                        yield return null;
                    }

                    // Trade depth while they are out of the stack
                    order[0] = topCard;
                    order[n - 1] = bottomCard;
                    ApplySiblingOrder(order);

                    Vector2 topOut = cardRects[topCard].anchoredPosition;
                    Vector2 bottomOut = cardRects[bottomCard].anchoredPosition;
                    t = 0f;
                    while (t < RiffleInTime)
                    {
                        t += Time.unscaledDeltaTime;
                        float k = EaseInOutCubic(Mathf.Clamp01(t / RiffleInTime));
                        SetCardPose(topCard, Vector2.Lerp(topOut, StackPosition(0, n), k), Mathf.Lerp(8f, 0f, k));
                        SetCardPose(bottomCard, Vector2.Lerp(bottomOut, StackPosition(n - 1, n), k), Mathf.Lerp(-8f, 0f, k));
                        yield return null;
                    }

                    SetCardPose(topCard, StackPosition(0, n), 0f);
                    SetCardPose(bottomCard, StackPosition(n - 1, n), 0f);
                }
            }

            // --- 4. Deal back out, top of the stack first, still face-down ---
            var stackStart = new Vector2[3];
            var dealSequence = new List<int>(n);
            for (int a = 0; a < n; a++) stackStart[order[a]] = StackPosition(a, n);
            for (int a = n - 1; a >= 0; a--) dealSequence.Add(order[a]);

            var soundPlayed = new bool[n];
            float total = DealTime + DealStagger * Mathf.Max(0, n - 1);
            float td = 0f;
            while (td < total)
            {
                td += Time.unscaledDeltaTime;

                for (int a = 0; a < n; a++)
                {
                    int i = dealSequence[a];
                    float k = Mathf.Clamp01((td - a * DealStagger) / DealTime);
                    float eased = EaseOutCubic(k);

                    Vector2 position = Vector2.Lerp(stackStart[i], cardSlots[i], eased);
                    position.y += Mathf.Sin(k * Mathf.PI) * 36f;
                    float side = Mathf.Clamp(cardSlots[i].x / (CardWidth + CardSpacing), -1f, 1f);
                    SetCardPose(i, position, (1f - eased) * side * -6f);
                    // A small overshoot on landing, so each card pops into its slot
                    cardFeedbacks[i].introScale = Mathf.LerpUnclamped(StackScale, 1f, EaseOutBack(k));

                    if (!soundPlayed[a] && k > 0f)
                    {
                        soundPlayed[a] = true;
                        PlaySfx(dealClip, 1.05f + 0.05f * a);
                    }
                }

                yield return null;
            }

            SettleCards();
        }

        /// <summary>Puts every card in its slot at rest, in whichever face (up/down) it should show.</summary>
        private void SettleCards()
        {
            isShuffling = false;

            for (int i = 0; i < 3; i++)
            {
                if (cardRects[i] == null) continue;

                cardRects[i].anchoredPosition = cardSlots[i];
                cardRects[i].localRotation = Quaternion.identity;
                cardFeedbacks[i].introScale = 1f;
                cardFeedbacks[i].horizontalScale = 1f;
                cardGroups[i].alpha = 1f;
                SetFaceVisual(i, cardRevealed[i]);
                cardRects[i].SetSiblingIndex(i);
            }

            SetCardsInteractable(true);
            UpdateRerollButton();
        }

        /// <summary>Turns every dealt card face-up at once, with no flip (Skip card animation).</summary>
        private void RevealAllCardsInstantly()
        {
            for (int i = 0; i < 3; i++)
            {
                if (cardPanels[i] == null || !cardPanels[i].activeSelf) continue;
                cardRevealed[i] = true;
                cardFeedbacks[i].glowColor = RarityGlow(i);
                SetFaceVisual(i, true);
            }
        }

        private void ToggleSkipCardAnimation()
        {
            bool skip = !SkipCardAnimation;
            SkipCardAnimation = skip;
            RefreshSkipAnimationToggle();
            PlayClick();

            // Turning it on mid-draft finishes the current shuffle/deal/flips and shows the hand face-up.
            if (!skip || !overlayVisible) return;
            FinishAnimations();
            if (!offerApplied) return;
            RevealAllCardsInstantly();
            SettleCards();
        }

        private void RefreshSkipAnimationToggle()
        {
            if (skipAnimationCheck == null) return;
            SetCheckboxSprite(skipAnimationCheck, SkipCardAnimation);
        }

        private void SetCheckboxSprite(Image check, bool on)
        {
            if (check == null) return;
            Sprite sprite = LoadUiSprite(on ? "UI/controls/checkbox_on" : "UI/controls/checkbox_off", Vector4.zero);
            check.sprite = sprite;
            check.color = sprite != null ? Color.white : (on ? ACCENT_COLOR : new Color(1f, 1f, 1f, 0.25f));
        }

        private void RevealCard(int index)
        {
            if (cardRevealed[index]) return;
            cardRevealed[index] = true;

            if (!isActiveAndEnabled)
            {
                SetFaceVisual(index, true);
                cardFeedbacks[index].glowColor = RarityGlow(index);
                return;
            }

            if (flipRoutines[index] != null) StopCoroutine(flipRoutines[index]);
            flipRoutines[index] = StartCoroutine(FlipRoutine(index));
        }

        private IEnumerator FlipRoutine(int index)
        {
            MenuButtonFeedback feedback = cardFeedbacks[index];
            RectTransform rect = cardRects[index];
            float tilt = (index == 0 ? -1f : index == 2 ? 1f : (UnityEngine.Random.value < 0.5f ? -1f : 1f)) * FlipTilt;
            // The 1.3 pitch turns the hover blip fallback into a flick; a real flip sound plays as recorded.
            PlaySfx(flipClip, flipClip == hoverClip ? 1.3f : UnityEngine.Random.Range(0.97f, 1.03f));

            // Half 1: the card lifts and tilts while the back squeezes away
            float t = 0f;
            while (t < FlipHalfOut)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / FlipHalfOut);
                feedback.horizontalScale = Mathf.Max(0.001f, 1f - EaseInCubic(k));
                feedback.introScale = 1f + FlipLift * EaseOutCubic(k);
                rect.localRotation = Quaternion.Euler(0f, 0f, tilt * k);
                yield return null;
            }

            // Swap to the front: rarity glow and flash, sparks, a shine sweep and the stars filling in
            feedback.horizontalScale = 0.001f;
            SetFaceVisual(index, true);
            feedback.glowColor = RarityGlow(index);
            Color rarityColor = cardRarityColors[index];
            StartCoroutine(BurstRoutine(index));
            StartShine(index);
            if (starPopRoutines[index] != null) StopCoroutine(starPopRoutines[index]);
            starPopRoutines[index] = StartCoroutine(StarPopRoutine(index));

            // Half 2: the front springs open and settles back down
            t = 0f;
            while (t < FlipHalfIn)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / FlipHalfIn);
                feedback.horizontalScale = Mathf.Max(0.001f, Mathf.LerpUnclamped(0.001f, 1f, EaseOutBack(k)));
                feedback.introScale = 1f + FlipLift * (1f - EaseOutCubic(k));
                rect.localRotation = Quaternion.Euler(0f, 0f, tilt * (1f - EaseOutCubic(k)));
                cardFlashes[index].color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, (1f - k) * 0.55f);
                yield return null;
            }

            feedback.horizontalScale = 1f;
            feedback.introScale = 1f;
            rect.localRotation = Quaternion.identity;
            cardFlashes[index].color = new Color(1f, 1f, 1f, 0f);
            flipRoutines[index] = null;
        }

        private void StopAllFlips()
        {
            for (int i = 0; i < 3; i++)
            {
                if (flipRoutines[i] != null)
                {
                    StopCoroutine(flipRoutines[i]);
                    flipRoutines[i] = null;
                }

                if (cardFeedbacks[i] != null)
                {
                    cardFeedbacks[i].horizontalScale = 1f;
                    cardFeedbacks[i].introScale = 1f;
                }

                if (cardFlashes[i] != null) cardFlashes[i].color = new Color(1f, 1f, 1f, 0f);
                if (cardRects[i] != null && !isShuffling) cardRects[i].localRotation = Quaternion.identity;
                if (cardRevealed[i]) SetFaceVisual(i, true);

                if (starPopRoutines[i] != null)
                {
                    StopCoroutine(starPopRoutines[i]);
                    starPopRoutines[i] = null;
                    rarityTexts[i].text = BuildStarString(cardStarCounts[i]);
                    rarityTexts[i].rectTransform.localScale = Vector3.one;
                }
            }
        }

        /// <summary>
        /// Idle motion once the hand is dealt: face-down cards float out of step, the selected card lifts,
        /// revealed portraits bob a pixel at a time, 3★+ cards catch a shine now and then, and the backs
        /// scan and pulse.
        /// </summary>
        private void AnimateCards()
        {
            float time = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            for (int i = 0; i < 3; i++)
            {
                if (cardPanels[i] == null || !cardPanels[i].activeSelf) continue;

                if (!isShuffling)
                {
                    float lift = i == selectedCardIndex ? SelectedLift : 0f;
                    cardLifts[i] = Mathf.Lerp(cardLifts[i], lift, 1f - Mathf.Exp(-12f * dt));
                    float bob = cardRevealed[i] ? 0f : Mathf.Sin(time * 1.8f + i * 2.1f) * FaceDownBob;
                    cardRects[i].anchoredPosition = cardSlots[i] + new Vector2(0f, cardLifts[i] + bob);
                }

                if (cardRevealed[i])
                {
                    float speed = i == selectedCardIndex ? 4f : 2.4f;
                    portraitImages[i].rectTransform.anchoredPosition = new Vector2(0f, Mathf.Round(Mathf.Sin(time * speed + i) * 3f));
                    if (cardStarCounts[i] >= 3 && time >= nextShineTimes[i]) StartShine(i);
                }
                else
                {
                    if (backScanLines[i] != null)
                    {
                        backScanLines[i].anchoredPosition = new Vector2(0f, -16f - Mathf.Repeat(time * 140f + i * 210f, CardHeight - 32f));
                    }

                    if (backEmblems[i] != null)
                    {
                        Color emblem = backEmblems[i].color;
                        emblem.a = 0.08f + 0.07f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f + i));
                        backEmblems[i].color = emblem;
                    }

                    if (backHints[i] != null) backHints[i].enabled = Mathf.Repeat(time + i * 0.3f, 1.1f) < 0.8f;
                }

                UpdateShine(i, time);
            }
        }

        /// <summary>Starts a shine sweep now and schedules the next one (rarer cards shine more often).</summary>
        private void StartShine(int index)
        {
            float now = Time.unscaledTime;
            shineStarts[index] = now;
            float rarity = Mathf.Clamp01((cardStarCounts[index] - 3) / 2f);
            nextShineTimes[index] = now + Mathf.Lerp(4.5f, 2.2f, rarity) + UnityEngine.Random.Range(0f, 0.8f);
        }

        private void UpdateShine(int index, float time)
        {
            RectTransform shine = shineRects[index];
            if (shine == null) return;

            float k = (time - shineStarts[index]) / ShineDuration;
            bool active = cardRevealed[index] && k >= 0f && k <= 1f;
            if (shine.gameObject.activeSelf != active) shine.gameObject.SetActive(active);
            if (active) shine.anchoredPosition = new Vector2(Mathf.Lerp(-CardWidth, CardWidth, EaseInOutCubic(k)), 0f);
        }

        /// <summary>The rarity stars fill in one at a time after a flip, each with a small pop.</summary>
        private IEnumerator StarPopRoutine(int index)
        {
            int stars = cardStarCounts[index];
            Text text = rarityTexts[index];
            RectTransform rect = text.rectTransform;

            text.text = BuildStarString(0);
            yield return new WaitForSecondsRealtime(0.08f);

            for (int s = 1; s <= stars; s++)
            {
                text.text = BuildStarString(s);
                float t = 0f;
                while (t < StarPopInterval)
                {
                    t += Time.unscaledDeltaTime;
                    rect.localScale = Vector3.one * (1f + 0.2f * (1f - Mathf.Clamp01(t / StarPopInterval)));
                    yield return null;
                }
            }

            rect.localScale = Vector3.one;
            starPopRoutines[index] = null;
        }

        /// <summary>Pixel sparks in the rarity colour burst out of a card as it turns over; rarer cards throw more.</summary>
        private IEnumerator BurstRoutine(int index)
        {
            int count = 6 + cardStarCounts[index] * 4;
            Color color = cardRarityColors[index];
            Transform parent = cardRects[index].parent; // the container, so the flip's squeeze doesn't squash them
            Vector2 origin = cardRects[index].anchoredPosition;

            var sparks = new RectTransform[count];
            var images = new Image[count];
            var velocities = new Vector2[count];
            for (int s = 0; s < count; s++)
            {
                images[s] = CreatePlainImage(parent, "Spark", color);
                sparks[s] = images[s].rectTransform;
                float size = Mathf.Round(UnityEngine.Random.Range(6f, 12f));
                sparks[s].sizeDelta = new Vector2(size, size);
                sparks[s].anchoredPosition = origin + new Vector2(
                    UnityEngine.Random.Range(-CardWidth * 0.3f, CardWidth * 0.3f),
                    UnityEngine.Random.Range(-CardHeight * 0.2f, CardHeight * 0.25f));
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                velocities[s] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(260f, 620f) + new Vector2(0f, 140f);
            }

            float t = 0f;
            while (t < BurstLifetime)
            {
                float dt = Time.unscaledDeltaTime;
                t += dt;
                float k = Mathf.Clamp01(t / BurstLifetime);
                for (int s = 0; s < count; s++)
                {
                    if (sparks[s] == null) continue;
                    velocities[s] *= 1f - Mathf.Min(1f, 2.5f * dt);
                    velocities[s].y -= 900f * dt;
                    sparks[s].anchoredPosition += velocities[s] * dt;
                    images[s].color = new Color(color.r, color.g, color.b, 1f - k * k);
                }
                yield return null;
            }

            for (int s = 0; s < count; s++)
            {
                if (sparks[s] != null) Destroy(sparks[s].gameObject);
            }
        }

        private Color RarityGlow(int index)
        {
            Color c = cardRarityColors[index];
            return new Color(c.r, c.g, c.b, 0.95f);
        }
    }
}
