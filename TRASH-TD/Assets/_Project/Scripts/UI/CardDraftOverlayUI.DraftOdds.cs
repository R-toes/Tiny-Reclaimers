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
    // The draft odds display: the slot-machine reels, the lever and the sparks.
    public partial class CardDraftOverlayUI
    {
        // ============================
        // Draft odds
        // ============================

        /// <summary>
        /// Shows the odds this draft was rolled with (CardDraftSystem.GetCurrentOdds) on the slot machine's reels, and
        /// when they next go up. Whenever they differ from the last draft's (the opening draft's 100/0/0 to the first
        /// table row, then every step), the reels start on the old odds, the lever is pulled and they spin to the new
        /// ones (instantly with Skip card animation); "ODDS UP!" follows if the higher rarities went up. The opening
        /// draft starts a new run, so it never spins. Rerolls keep the same odds, so this only runs when a draft opens.
        /// </summary>
        private void UpdateDraftOdds()
        {
            if (oddsRoot == null) return;

            StopOddsAnimation();
            bool show = draftSystem != null && !IsTutorialDraft;
            if (!show)
            {
                oddsRoot.gameObject.SetActive(false);
                return;
            }
            RefreshOddsVisibility();

            DraftOddsTable table = draftSystem.OddsTable;
            int cleared = draftSystem.WavesCleared;
            oddsTarget = draftSystem.GetCurrentOdds();

            var waveManager = FindAnyObjectByType<WaveManager>();
            int totalWaves = waveManager != null ? waveManager.TotalWaves : 0;
            if (draftSystem.IsOpeningDraft)
            {
                oddsNextText.text = "OPENING DRAFT: 1★ ONLY";
            }
            else if (table.TryGetNextStep(cleared, out int nextStep) && (totalWaves <= 0 || nextStep < totalWaves))
            {
                // A step after the stage's last wave never gets a draft, so it isn't announced
                oddsNextText.text = $"BOOST AFTER WAVE {nextStep}";
            }
            else
            {
                oddsNextText.text = "MAX ODDS";
            }

            DraftOdds from = oddsShown;
            bool changed = hasOddsShown && !draftSystem.IsOpeningDraft && !SameShownOdds(from, oddsTarget);
            oddsShown = oddsTarget;
            hasOddsShown = true;

            bool oddsUp = changed && (Mathf.RoundToInt(oddsTarget.Star3) > Mathf.RoundToInt(from.Star3) ||
                Mathf.RoundToInt(oddsTarget.Star2 + oddsTarget.Star3) > Mathf.RoundToInt(from.Star2 + from.Star3));
            oddsUpTag.gameObject.SetActive(oddsUp);
            oddsUpTag.localScale = Vector3.one;
            if (!changed || SkipCardAnimation || HideDraftOdds || !isActiveAndEnabled)
            {
                ApplyOdds(oddsTarget);
                return;
            }

            ApplyOdds(from);
            oddsUpTag.localScale = Vector3.zero;
            oddsRoutine = StartCoroutine(OddsSpinRoutine(oddsTarget, oddsUp));
        }

        /// <summary>Shows or hides the whole slot machine to match <see cref="HideDraftOdds"/>, and ticks the checkbox.</summary>
        private void RefreshOddsVisibility()
        {
            bool hidden = HideDraftOdds;
            if (oddsRoot != null) oddsRoot.gameObject.SetActive(!hidden && draftSystem != null && !IsTutorialDraft);
            SetCheckboxSprite(oddsToggleCheck, !hidden);
        }

        private void ToggleDraftOdds()
        {
            StopOddsAnimation(); // a running spin ends at rest on the final odds
            HideDraftOdds = !HideDraftOdds;
            RefreshOddsVisibility();
            PlayClick();
        }

        /// <summary>True if both odds read the same on the reels (whole percentages).</summary>
        private static bool SameShownOdds(DraftOdds a, DraftOdds b)
        {
            return Mathf.RoundToInt(a.Star1) == Mathf.RoundToInt(b.Star1) &&
                Mathf.RoundToInt(a.Star2) == Mathf.RoundToInt(b.Star2) &&
                Mathf.RoundToInt(a.Star3) == Mathf.RoundToInt(b.Star3);
        }

        /// <summary>Puts every reel at rest on its odds (0% tiers are dimmed).</summary>
        private void ApplyOdds(DraftOdds odds)
        {
            float[] values = { odds.Star1, odds.Star2, odds.Star3 };
            for (int i = 0; i < 3; i++)
            {
                SetReelAtRest(oddsReels[i], values[i]);
            }
        }

        /// <summary>Resets the reel's strip with this percentage on the payline and filler numbers around it.</summary>
        private static void SetReelAtRest(OddsReel reel, float percent)
        {
            int rounded = Mathf.RoundToInt(percent);
            reel.scroll = 0f;
            reel.landingRow = -1;
            reel.zero = rounded <= 0;
            for (int r = 0; r < OddsReelRows; r++)
            {
                reel.rowPositions[r] = r * OddsRowHeight; // strip positions at scroll 0
                reel.rows[r].text = r == OddsPaylineRow ? FormatOddsPercent(rounded) : RandomOddsFiller();
            }
            ApplyReelPlate(reel);
            LayoutReel(reel, 0f);
        }

        /// <summary>
        /// Places the reel's rows for its scroll. A row that wraps from the bottom back to the top (out of sight)
        /// gets a new filler number, or the landing number if it's the landing row. Rows fade away from the payline;
        /// blur (0..1) stretches and fades them while the reel spins fast.
        /// </summary>
        private static void LayoutReel(OddsReel reel, float blur)
        {
            const float stripLength = OddsReelRows * OddsRowHeight;
            for (int r = 0; r < OddsReelRows; r++)
            {
                float position = Mathf.Repeat(r * OddsRowHeight + reel.scroll, stripLength);
                // Only a big backwards jump is a wrap; small ones are the stop's overshoot settling back
                if (position < reel.rowPositions[r] - stripLength * 0.5f)
                {
                    reel.rows[r].text = r == reel.landingRow ? reel.landingText : RandomOddsFiller();
                }
                reel.rowPositions[r] = position;

                float y = OddsPaylinePosition - position;
                RectTransform rect = reel.rows[r].rectTransform;
                rect.anchoredPosition = new Vector2(0f, y);
                rect.localScale = new Vector3(1f, 1f + 0.35f * blur, 1f);

                float alpha = (1f - 0.7f * Mathf.Clamp01(Mathf.Abs(y) / OddsRowHeight)) * (1f - 0.35f * blur);
                if (reel.zero) alpha *= 0.35f;
                reel.rows[r].color = new Color(reel.color.r, reel.color.g, reel.color.b, alpha);
            }
        }

        private static void ApplyReelPlate(OddsReel reel)
        {
            Color color = reel.color;
            reel.plate.color = reel.zero ? new Color(color.r, color.g, color.b, 0.35f) : color;
        }

        private static string FormatOddsPercent(int percent) => $"{percent}%";

        private static string RandomOddsFiller() => FormatOddsPercent(UnityEngine.Random.Range(1, 100));

        /// <summary>
        /// Picks where a spinning reel comes to rest (on a row boundary, at least five rows on) and which row lands on
        /// the payline. Five rows of travel make that row wrap in at the top at least once, so it gets the percentage
        /// out of sight. Returns the scroll to stop at.
        /// </summary>
        private static float BeginReelStop(OddsReel reel, float percent)
        {
            int stopRow = Mathf.CeilToInt((reel.scroll + 5f * OddsRowHeight) / OddsRowHeight);
            // Row r rests on the payline when r + stopRow = OddsPaylineRow (mod OddsReelRows)
            reel.landingRow = ((OddsPaylineRow - stopRow) % OddsReelRows + OddsReelRows) % OddsReelRows;
            reel.landingText = FormatOddsPercent(Mathf.RoundToInt(percent));
            return stopRow * OddsRowHeight;
        }

        /// <summary>
        /// Odds change: the lever drops, the reels spin up, then stop one by one (1★, 2★, 3★) on the new odds with
        /// a small overshoot. Once all three land the cabinet flashes; if the odds went up, the "ODDS UP!" tag pops
        /// in with sparks and the rank-up sound.
        /// </summary>
        private IEnumerator OddsSpinRoutine(DraftOdds to, bool oddsUp)
        {
            yield return new WaitForSecondsRealtime(OddsSpinDelay);

            float[] values = { to.Star1, to.Star2, to.Star3 };
            var stopStart = new float[3];
            var stopFrom = new float[3];
            var stopTo = new float[3];
            var stopTime = new float[3];
            var landed = new bool[3];
            for (int i = 0; i < 3; i++)
            {
                stopStart[i] = -1f;
                oddsReels[i].zero = false;
                ApplyReelPlate(oddsReels[i]);
            }

            PlaySfx(clickClip, 0.75f); // lever
            bool whirPlayed = false;
            int landedCount = 0;
            float elapsed = 0f;
            while (landedCount < 3)
            {
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                UpdateOddsLever(elapsed);

                // The reels start once the lever is down
                float spinSpeed = OddsSpinSpeed * Mathf.Clamp01((elapsed - OddsLeverPullTime) / OddsSpinUpTime);
                if (!whirPlayed && elapsed >= OddsLeverPullTime)
                {
                    whirPlayed = true;
                    PlaySfx(shuffleClip, 1.2f);
                }

                for (int i = 0; i < 3; i++)
                {
                    if (landed[i]) continue;
                    OddsReel reel = oddsReels[i];
                    float before = reel.scroll;

                    if (stopStart[i] < 0f)
                    {
                        reel.scroll += spinSpeed * dt;
                        if (elapsed >= OddsFirstStop + OddsStopStagger * i)
                        {
                            stopStart[i] = elapsed;
                            stopFrom[i] = reel.scroll;
                            stopTo[i] = BeginReelStop(reel, values[i]);
                            // EaseOutBack starts at ~4.7x its average speed, so this carries on at the spin speed
                            stopTime[i] = 4.7f * (stopTo[i] - stopFrom[i]) / OddsSpinSpeed;
                        }
                    }
                    else
                    {
                        float k = Mathf.Clamp01((elapsed - stopStart[i]) / stopTime[i]);
                        reel.scroll = Mathf.LerpUnclamped(stopFrom[i], stopTo[i], EaseOutBack(k));
                        if (k >= 1f)
                        {
                            landed[i] = true;
                            landedCount++;
                            reel.landingRow = -1;
                            reel.zero = Mathf.RoundToInt(values[i]) <= 0;
                            ApplyReelPlate(reel);
                            PlaySfx(clickClip, 1f + 0.12f * i); // reel clunks into place
                        }
                    }

                    float speed = Mathf.Abs(reel.scroll - before) / Mathf.Max(dt, 0.0001f);
                    LayoutReel(reel, landed[i] ? 0f : Mathf.Clamp01(speed / OddsSpinSpeed));
                }
                yield return null;
            }

            oddsLever.localScale = Vector3.one;
            if (oddsUp)
            {
                PlaySfx(oddsUpClip, 1.1f);
                StartCoroutine(OddsSparksRoutine());
            }

            const float popTime = 0.35f;
            float t = 0f;
            while (t < popTime)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / popTime);
                if (oddsUp) oddsUpTag.localScale = Vector3.one * EaseOutBack(k);
                oddsFlash.color = new Color(1f, 1f, 1f, 0.5f * (1f - k));
                yield return null;
            }

            oddsUpTag.localScale = Vector3.one;
            oddsFlash.color = new Color(1f, 1f, 1f, 0f);
            oddsRoutine = null;
        }

        /// <summary>The lever stick flips down over OddsLeverPullTime, then springs back up.</summary>
        private void UpdateOddsLever(float elapsed)
        {
            float scale = elapsed < OddsLeverPullTime
                ? Mathf.Lerp(1f, -1f, EaseInCubic(elapsed / OddsLeverPullTime))
                : Mathf.LerpUnclamped(-1f, 1f, EaseOutBack(Mathf.Clamp01((elapsed - OddsLeverPullTime) / OddsLeverReturnTime)));
            oddsLever.localScale = new Vector3(1f, scale, 1f);
        }

        /// <summary>Pixel sparks in the 3★ colour fly up out of the 3★ reel.</summary>
        private IEnumerator OddsSparksRoutine()
        {
            const int count = 14;
            Color color = GetRarityColor(3);
            RectTransform window = oddsReels[2].window;
            Vector2 origin = oddsCabinet.InverseTransformPoint(window.TransformPoint(window.rect.center));

            var sparks = new RectTransform[count];
            var images = new Image[count];
            var velocities = new Vector2[count];
            for (int s = 0; s < count; s++)
            {
                images[s] = CreatePlainImage(oddsCabinet, "Spark", color);
                sparks[s] = images[s].rectTransform;
                sparks[s].anchorMin = sparks[s].anchorMax = new Vector2(0.5f, 1f); // the cabinet's pivot
                float size = Mathf.Round(UnityEngine.Random.Range(4f, 8f));
                sparks[s].sizeDelta = new Vector2(size, size);
                sparks[s].anchoredPosition = origin + new Vector2(UnityEngine.Random.Range(-40f, 40f), 0f);
                float angle = UnityEngine.Random.Range(0.15f, Mathf.PI - 0.15f); // upward half
                velocities[s] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * UnityEngine.Random.Range(160f, 380f);
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
                    velocities[s].y -= 700f * dt;
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

        /// <summary>Ends a running odds spin with the reels at rest on the final values.</summary>
        private void StopOddsAnimation()
        {
            if (oddsRoutine == null) return;
            StopCoroutine(oddsRoutine);
            oddsRoutine = null;
            ApplyOdds(oddsTarget);
            oddsUpTag.localScale = Vector3.one;
            oddsFlash.color = new Color(1f, 1f, 1f, 0f);
            oddsLever.localScale = Vector3.one;
        }
    }
}
