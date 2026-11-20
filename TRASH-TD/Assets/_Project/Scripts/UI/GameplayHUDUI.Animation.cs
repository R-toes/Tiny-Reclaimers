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
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // HUD event handlers, per-frame animation and the phase banner routines.
    public partial class GameplayHUDUI
    {
        // ============================
        // Event Handlers
        // ============================

        private void HandleWaveStarted(int current, int total)
        {
            HideFramiliarCopyMenu();
            UpdateWaveReadout();
            UpdateHostileReadout();
            if (waveText != null) Punch(waveText);
        }

        private void HandleEnemyCountChanged(EnemyBase enemy)
        {
            RefreshCounters();
        }

        private void HandleLPChanged(int current, int max)
        {
            if (IsSandbox) return;
            UpdateLivesDisplay(current, max);
        }

        private void UpdateLivesDisplay(int current, int max)
        {
            if (livesTubeRect == null) return;

            if (lastLives >= 0 && current < lastLives) OnLifeLost();
            lastLives = current;

            int total = max > 0 ? max : 3;
            livesTarget = Mathf.Clamp01((float)current / total);
            if (lpText != null)
            {
                if (current != shownLivesCurrent || total != shownLivesTotal)
                {
                    shownLivesCurrent = current;
                    shownLivesTotal = total;
                    lpText.text = $"{current}/{total}";
                }
                lpText.color = livesTarget > 0.5f ? Color.white : livesTarget > 0.25f ? DraftColor : LifeColor;
            }
            if (livesDisplayed < 0f)
            {
                livesDisplayed = livesTarget;
                ApplyLivesLiquid(livesDisplayed);
            }
        }

        private void OnLifeLost()
        {
            dangerFlashTime = 0f;
            lifeFlashTime = 0f;
            Punch(livesTubeRect);
            PlaySfx(lifeLostClip, 1f);

            // Screen shake (harder on the last life) and feed interference on the surveillance overlay
            bool lastLife = gameManager != null && gameManager.CurrentLifePoints <= 0;
            GameplayCameraController.Instance?.Shake(lastLife ? 0.45f : 0.2f, lastLife ? 0.6f : 0.35f);
            if (surveillanceOverlay != null) surveillanceOverlay.PlayInterference();
        }

        /// <summary>A red burst where an enemy breaches the exit.</summary>
        private void HandleEnemyBreachFx(EnemyBase enemy)
        {
            if (enemy == null) return;
            Vector2 position = enemy.transform.position;
            var breachRed = new Color(1f, 0.25f, 0.2f, 1f);
            TrashTD.Combat.CombatFx.Flash(position, breachRed, large: true);
            TrashTD.Combat.CombatFx.Sparks(position, breachRed, 14, 3.5f, 0.35f);
            TrashTD.Combat.CombatFx.Pop(position, breachRed);
        }

        private void RefreshCounters()
        {
            UpdateHostileReadout();
            UpdateWaveReadout();

            if (IsSandbox)
            {
                // Sandbox has no wave count, squad cap or life loss; avoid "x/2147483647" style overflow.
                if (lpText != null)
                {
                    shownLivesCurrent = shownLivesTotal = int.MinValue; // the lives text is rebuilt if the sandbox is left
                    lpText.text = "LIVES LOCKED";
                    lpText.color = TextDim;
                }
                if (livesTubeRect != null)
                    livesTubeRect.gameObject.SetActive(false);
                if (squadCountText != null && operatorManager != null)
                {
                    squadCountText.text = $"SQUAD {operatorManager.DeployedCount}";
                    squadCountText.color = Color.white;
                }
                return;
            }

            if (gameManager != null)
            {
                UpdateLivesDisplay(gameManager.CurrentLifePoints, gameManager.MaxLifePoints);
            }

            if (squadCountText != null && operatorManager != null)
            {
                bool isFull = operatorManager.IsAtSquadLimit;
                int squadKey = (operatorManager.DeployedCount * 1000 + operatorManager.SquadLimit) * 2 + (isFull ? 1 : 0);
                if (squadKey != shownSquadKey)
                {
                    shownSquadKey = squadKey;
                    squadCountText.text = isFull
                        ? $"SQUAD FULL {operatorManager.DeployedCount}/{operatorManager.SquadLimit}"
                        : $"SQUAD {operatorManager.DeployedCount}/{operatorManager.SquadLimit}";
                }
                squadCountText.color = isFull ? new Color(1f, 0.28f, 0.24f) : Color.white;

                int deployed = operatorManager.DeployedCount;
                if (deployed != lastSquadCount)
                {
                    if (lastSquadCount >= 0) Punch(squadCountText);
                    lastSquadCount = deployed;
                }
            }
        }

        // ============================
        // Animation & Feedback
        // ============================

        private void UpdateAnimations(float dt, float time)
        {
            float k = 1f - Mathf.Exp(-8f * dt);

            // Top bar background sprite-sheet animation (~12 fps)
            if (topBarBgAnimImage != null && topBarBgSprites != null && topBarBgSprites.Length > 1)
            {
                const float topBarBgFps = 12f;
                topBarBgAnimTime += dt;
                int newFrame = Mathf.FloorToInt(topBarBgAnimTime * topBarBgFps) % topBarBgSprites.Length;
                if (newFrame != topBarBgAnimFrame)
                {
                    topBarBgAnimFrame = newFrame;
                    if (topBarBgSprites[topBarBgAnimFrame] != null)
                        topBarBgAnimImage.sprite = topBarBgSprites[topBarBgAnimFrame];
                }
            }

            // Deck background sprite-sheet animation (~12 fps)
            if (deckBgAnimImage != null && deckBgSprites != null && deckBgSprites.Length > 1)
            {
                const float deckBgFps = 12f;
                deckBgAnimTime += dt;
                int newFrame = Mathf.FloorToInt(deckBgAnimTime * deckBgFps) % deckBgSprites.Length;
                if (newFrame != deckBgAnimFrame)
                {
                    deckBgAnimFrame = newFrame;
                    if (deckBgSprites[deckBgAnimFrame] != null)
                        deckBgAnimImage.sprite = deckBgSprites[deckBgAnimFrame];
                }
            }

            // Bottom Right Panel animated sprite-sheet and speed crossfade
            if (bottomRightBgToImage != null && HasAnyBottomRightSprites())
            {
                const float bottomRightBgFps = 12f;
                bottomRightBgAnimTime += dt;
                int animIndex = Mathf.FloorToInt(bottomRightBgAnimTime * bottomRightBgFps);
                bottomRightBgAnimFrame = animIndex;

                if (bottomRightBgFade < 1f)
                {
                    const float speedGaugeFadeDuration = 0.2f;
                    bottomRightBgFade = Mathf.Min(1f, bottomRightBgFade + dt / speedGaugeFadeDuration);
                    SetImageAlpha(bottomRightBgToImage, Mathf.SmoothStep(0f, 1f, bottomRightBgFade));
                }

                Sprite toSprite = GetBottomRightSprite(bottomRightBgToSpeed, animIndex);
                if (toSprite != null)
                {
                    bottomRightBgToImage.sprite = toSprite;
                }

                if (bottomRightBgFromImage != null)
                {
                    if (bottomRightBgFade < 1f)
                    {
                        Sprite fromSprite = GetBottomRightSprite(bottomRightBgFromSpeed, animIndex);
                        if (fromSprite != null)
                        {
                            bottomRightBgFromImage.sprite = fromSprite;
                        }
                    }
                    else if (toSprite != null)
                    {
                        bottomRightBgFromImage.sprite = toSprite;
                    }
                }
            }

            // Phase color follows the current phase (the progress bar swaps its fill sprite instead).
            // The screens glow faintly in it, and the status light blinks (faster during a wave).
            if (phaseText != null) phaseText.color = Color.Lerp(phaseText.color, phaseColorTarget, k);
            Color glowTarget = new Color(phaseColorTarget.r, phaseColorTarget.g, phaseColorTarget.b, TopScreenGlowAlpha);
            for (int i = 0; i < topScreenGlows.Count; i++)
            {
                if (topScreenGlows[i] != null) topScreenGlows[i].color = Color.Lerp(topScreenGlows[i].color, glowTarget, k);
            }
            if (phaseLed != null)
            {
                bool waveActive = IsSandbox
                    ? waveManager != null && waveManager.IsWaveInProgress
                    : gameManager != null && gameManager.CurrentPhase == StagePhase.WaveActive;
                float blinkRate = waveActive ? 4f : 1.2f;
                bool on = Mathf.Repeat(time * blinkRate, 1f) < 0.6f;
                phaseLed.color = new Color(phaseColorTarget.r, phaseColorTarget.g, phaseColorTarget.b, on ? 1f : 0.25f);
            }

            // Wave progress
            if (waveProgressFillRect != null)
            {
                float target = 0f;
                if (waveManager != null && waveManager.TotalWaves > 0)
                {
                    target = Mathf.Clamp01((float)waveManager.CurrentWaveNumber / waveManager.TotalWaves);
                }

                waveProgressDisplayed = Mathf.Lerp(waveProgressDisplayed, target, 1f - Mathf.Exp(-6f * dt));
                waveProgressFillRect.anchorMax = new Vector2(Mathf.Clamp01(waveProgressDisplayed), 1f);
            }

            // Start-wave button breathes while a wave can be started
            if (startWaveFeedback != null && startWaveButton != null)
            {
                startWaveFeedback.restScale = startWaveButton.interactable
                    ? 1f + 0.025f * (0.5f + 0.5f * Mathf.Sin(time * 3f))
                    : 1f;
            }

            // Text punches
            for (int i = punches.Count - 1; i >= 0; i--)
            {
                PunchState p = punches[i];
                if (p.target == null)
                {
                    punches.RemoveAt(i);
                    continue;
                }

                p.time += dt;
                if (p.time >= 0.4f)
                {
                    p.target.localScale = Vector3.one;
                    punches.RemoveAt(i);
                    continue;
                }

                float s = 1f + Mathf.Sin(p.time * 30f) * Mathf.Exp(-p.time * 9f) * 0.18f;
                p.target.localScale = new Vector3(s, s, 1f);
            }

            // Lives tube drains toward the new level; its frame flashes red then settles back
            if (livesDisplayed >= 0f && !Mathf.Approximately(livesDisplayed, livesTarget))
            {
                livesDisplayed = Mathf.MoveTowards(livesDisplayed, livesTarget, dt * 1.5f);
                ApplyLivesLiquid(livesDisplayed);
            }

            if (lifeFlashTime >= 0f && livesTubeFrame != null)
            {
                lifeFlashTime += dt;
                float lk = Mathf.Clamp01(lifeFlashTime / 0.6f);
                livesTubeFrame.color = Color.Lerp(LifeColor, Color.white, lk);
                if (lk >= 1f) lifeFlashTime = -1f;
            }

            // Red vignette flash when a life is lost
            if (dangerFlashTime >= 0f && dangerFlashImage != null)
            {
                dangerFlashTime += dt;
                float dk = Mathf.Clamp01(dangerFlashTime / 0.7f);
                Color c = dangerFlashImage.color;
                c.a = (1f - dk) * (1f - dk) * 0.6f;
                dangerFlashImage.color = c;
                if (dk >= 1f) dangerFlashTime = -1f;
            }

            // Selected-operator label pop-in
            if (labelPopTime >= 0f)
            {
                labelPopTime += dt;
                float pk = Mathf.Clamp01(labelPopTime / 0.22f);
                ApplyLabelPop(pk);
                if (pk >= 1f)
                {
                    labelPopTime = -1f;
                    // Rests at the phone size (1 on desktop). Setting Vector3.one here shrank the tab back right after it popped in.
                    if (selectedOperatorLabelRoot != null) selectedOperatorLabelRoot.transform.localScale = Vector3.one * MobileUi.InfoTab;
                    if (retreatFeedback != null) retreatFeedback.introScale = 1f;
                }
            }

            // Smooth health bar
            if (selectedLabelShown)
            {
                selectedHealthDisplayed = Mathf.Lerp(selectedHealthDisplayed, selectedHealthTarget, 1f - Mathf.Exp(-10f * dt));
                ApplyHealthFill();
            }

            // Placement controls pop-in
            if (placementPopTime >= 0f && placementControlsRoot != null)
            {
                placementPopTime += dt;
                float k2 = Mathf.Clamp01(placementPopTime / 0.22f);
                float s = Mathf.LerpUnclamped(0.7f, 1f, EaseOutBack(k2));
                placementControlsRoot.transform.localScale = new Vector3(s, s, 1f);
                if (k2 >= 1f)
                {
                    placementControlsRoot.transform.localScale = Vector3.one;
                    placementPopTime = -1f;
                }
            }
        }

        private void Punch(Component target)
        {
            if (target == null) return;

            Transform t = target.transform;
            for (int i = 0; i < punches.Count; i++)
            {
                if (punches[i].target == t)
                {
                    punches[i].time = 0f;
                    return;
                }
            }

            punches.Add(new PunchState { target = t, time = 0f });
        }

        /// <summary>Plain phase announcement (Preparation): title + subtitle on the alert band, no alarm.</summary>
        private void ShowPhaseBanner(string title, string subtitle, Color color)
        {
            if (phaseBannerGroup == null) return;
            ClearPhaseBannerIcons();
            PlayPhaseBanner(title, subtitle, color, alarm: false, glitch: false);
        }

        /// <summary>
        /// Wave start: "INVADING", "38 HOSTILES INBOUND", and the wave's enemy types with counts. Sirens flash on
        /// both ends. The final wave is red with a glitch and "FINAL WAVE - 38 HOSTILES INBOUND". Called as the wave
        /// phase begins, before WaveManager advances, so PeekNextWave is the wave about to start.
        /// </summary>
        private void ShowWaveBanner()
        {
            if (phaseBannerGroup == null) return;

            int total = waveManager != null ? waveManager.TotalWaves : 0;
            int number = gameManager != null ? gameManager.GetCurrentWaveIndex() + 1 : 1;
            bool final = total > 0 && number >= total;
            WaveData wave = waveManager != null ? waveManager.PeekNextWave() : null;
            int hostiles = WaveManager.CountEnemies(wave);

            string subtitle = hostiles > 0 ? $"{hostiles} HOSTILES INBOUND" : "HOSTILES INBOUND";
            if (final) subtitle = "FINAL WAVE  -  " + subtitle;

            ClearPhaseBannerIcons();
            BuildPhaseBannerIcons(wave);
            PlayPhaseBanner("INVADING", subtitle, final ? LifeColor : WaveColor, alarm: true, glitch: final);
        }

        private void PlayPhaseBanner(string title, string subtitle, Color color, bool alarm, bool glitch)
        {
            phaseBannerColor = color;
            phaseBannerAlarm = alarm;
            phaseBannerGlitch = glitch;

            phaseBannerTitle.text = title;
            phaseBannerTitle.color = color;
            phaseBannerTitleGhost.text = title;
            phaseBannerTitleGhost.gameObject.SetActive(glitch);
            phaseBannerSubtitle.text = subtitle;
            foreach (RawImage stripe in phaseBannerStripes) stripe.color = new Color(color.r, color.g, color.b, 0.9f);
            foreach (Image light in phaseBannerLights) light.gameObject.SetActive(alarm);

            // Subtitle sits lower when the enemy icon row is shown
            bool hasIcons = phaseBannerIconItems.Count > 0;
            phaseBannerSubtitle.rectTransform.anchoredPosition = new Vector2(0f, hasIcons ? -10f : -30f);
            phaseBannerTitle.rectTransform.anchoredPosition = new Vector2(0f, hasIcons ? 36f : 18f);
            phaseBannerIcons.gameObject.SetActive(hasIcons);

            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(PhaseBannerRoutine());
        }

        private void ClearPhaseBannerIcons()
        {
            foreach (GameObject item in phaseBannerIconItems)
            {
                if (item != null) Destroy(item);
            }
            phaseBannerIconItems.Clear();
        }

        /// <summary>The wave's enemy types (first sprite frame + "x8"), in spawn-list order, up to 6.</summary>
        private void BuildPhaseBannerIcons(WaveData wave)
        {
            if (wave?.entries == null) return;

            var order = new List<EnemyData>();
            var counts = new Dictionary<EnemyData, int>();
            foreach (WaveEntry entry in wave.entries)
            {
                if (entry == null || entry.enemyData == null || entry.count <= 0) continue;
                if (!counts.ContainsKey(entry.enemyData))
                {
                    counts[entry.enemyData] = 0;
                    order.Add(entry.enemyData);
                }
                counts[entry.enemyData] += entry.count;
            }

            for (int i = 0; i < order.Count && i < 6; i++)
            {
                EnemyData enemy = order[i];
                var item = new GameObject("Banner_" + enemy.enemyName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
                item.transform.SetParent(phaseBannerIcons, false);
                var layout = item.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 4f;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;

                Sprite[] frames = UnitSpriteFrames.GetEnemyFrames(enemy, out _);
                Sprite icon = frames.Length > 0 ? frames[0] : null;
                if (icon != null && icon.texture != null) icon.texture.filterMode = FilterMode.Point;

                var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObj.transform.SetParent(item.transform, false);
                var iconImage = iconObj.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
                var iconLayout = iconObj.GetComponent<LayoutElement>();
                iconLayout.preferredWidth = 32f;
                iconLayout.preferredHeight = 32f;

                Text count = CreateText(item.transform, "Count", "x" + counts[enemy], 13, TextAnchor.MiddleLeft);
                count.raycastTarget = false;
                phaseBannerIconItems.Add(item);
            }

            if (order.Count > 6)
            {
                Text more = CreateText(phaseBannerIcons, "More", "+" + (order.Count - 6), 13, TextAnchor.MiddleLeft);
                more.color = TextDim;
                more.raycastTarget = false;
                phaseBannerIconItems.Add(more.gameObject);
            }
        }

        /// <summary>
        /// The band snaps open (height 0 → full), the title punches in, holds while the stripes march and the
        /// sirens flash, then the band collapses. Unscaled time scaled by the game speed (1x / 2x / 3x), so at
        /// higher speeds the banner plays and clears faster; it doesn't freeze while paused.
        /// </summary>
        private IEnumerator PhaseBannerRoutine()
        {
            const float openTime = 0.22f;
            const float hold = 1.7f;
            const float closeTime = 0.25f;
            const float total = openTime + hold + closeTime;

            float t = 0f;
            while (t < total)
            {
                float speed = gameManager != null ? Mathf.Max(1f, gameManager.GameSpeed) : 1f;
                float dt = Time.unscaledDeltaTime * speed;
                t += dt;

                float open;
                if (t < openTime) open = EaseOutCubic(t / openTime);
                else if (t < openTime + hold) open = 1f;
                else open = 1f - EaseOutCubic((t - openTime - hold) / closeTime);

                phaseBannerGroup.alpha = Mathf.Clamp01(open * 1.5f);
                phaseBannerRect.localScale = new Vector3(1f, Mathf.Max(0.001f, open), 1f);

                // Title punches in, then settles
                float punch = t < openTime + 0.25f ? Mathf.Lerp(1.35f, 1f, EaseOutBack(Mathf.Clamp01((t - openTime * 0.5f) / 0.3f))) : 1f;
                phaseBannerTitle.rectTransform.localScale = Vector3.one * punch;

                // Marching stripes (opposite directions top / bottom)
                for (int i = 0; i < phaseBannerStripes.Length; i++)
                {
                    Rect uv = phaseBannerStripes[i].uvRect;
                    uv.x += (i == 0 ? 1f : -1f) * dt * 1.6f;
                    phaseBannerStripes[i].uvRect = uv;
                }

                // Sirens
                if (phaseBannerAlarm)
                {
                    bool on = Mathf.Repeat(t * 3f, 1f) < 0.5f;
                    foreach (Image light in phaseBannerLights)
                    {
                        light.color = new Color(phaseBannerColor.r, phaseBannerColor.g, phaseBannerColor.b, on ? 1f : 0.2f);
                        light.rectTransform.localScale = Vector3.one * (on ? 1.15f : 1f);
                    }
                }

                // Final-wave glitch: the title and a red/cyan ghost jitter in short bursts
                if (phaseBannerGlitch)
                {
                    bool burst = Mathf.Repeat(t, 0.6f) < 0.12f;
                    Vector2 jitter = burst ? new Vector2(UnityEngine.Random.Range(-8f, 8f), UnityEngine.Random.Range(-2f, 2f)) : Vector2.zero;
                    Vector2 basePos = phaseBannerTitle.rectTransform.anchoredPosition;
                    phaseBannerContent.anchoredPosition = jitter * 0.5f;
                    phaseBannerTitleGhost.rectTransform.anchoredPosition = basePos + (burst ? new Vector2(-jitter.x - 4f, 2f) : new Vector2(-3f, 0f));
                    phaseBannerTitleGhost.color = new Color(0.3f, 0.95f, 1f, burst ? 0.75f : 0.25f);
                    phaseBannerTitleGhost.rectTransform.localScale = phaseBannerTitle.rectTransform.localScale;
                }
                else
                {
                    phaseBannerContent.anchoredPosition = Vector2.zero;
                }

                yield return null;
            }

            phaseBannerGroup.alpha = 0f;
            phaseBannerRect.localScale = Vector3.one;
            phaseBannerContent.anchoredPosition = Vector2.zero;
            bannerRoutine = null;
        }

        private static Texture2D bannerStripeTexture;

        /// <summary>White diagonal stripes on transparent (tinted per banner), wrap-repeat for scrolling.</summary>
        private static Texture2D GetBannerStripeTexture()
        {
            if (bannerStripeTexture != null) return bannerStripeTexture;
            const int size = 16;
            bannerStripeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = (x + y) % size < size / 2 ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            bannerStripeTexture.SetPixels32(pixels);
            bannerStripeTexture.Apply();
            return bannerStripeTexture;
        }

        private IEnumerator IntroRoutine()
        {
            if (topBarRect == null || deckBarRect == null) yield break;

            Vector2 topTarget = topBarRect.anchoredPosition;
            Vector2 deckTarget = deckBarRect.anchoredPosition;
            topBarRect.anchoredPosition = topTarget + new Vector2(0f, 100f);
            deckBarRect.anchoredPosition = deckTarget + new Vector2(0f, -180f);

            const float duration = 0.55f;
            const float deckDelay = 0.1f;
            float t = 0f;
            while (t < duration + deckDelay)
            {
                t += Time.unscaledDeltaTime;
                float topK = EaseOutCubic(Mathf.Clamp01(t / duration));
                float deckK = EaseOutCubic(Mathf.Clamp01((t - deckDelay) / duration));
                topBarRect.anchoredPosition = topTarget + new Vector2(0f, 100f * (1f - topK));
                deckBarRect.anchoredPosition = deckTarget + new Vector2(0f, -180f * (1f - deckK));
                yield return null;
            }

            topBarRect.anchoredPosition = topTarget;
            deckBarRect.anchoredPosition = deckTarget;
        }
    }
}
