using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;

namespace TrashTD.UI
{
    // Per-frame updates: typing, presenter animation, pointer, spotlight, emote and task strip.
    public partial class TutorialDialogueUI
    {
        private void Update()
        {
            if (!root.gameObject.activeSelf) return;
            float dt = Time.unscaledDeltaTime;

            UpdatePresenter(dt);
            UpdateTyping(dt);
            AnimateSpeaker(dt);
            UpdateEmote(dt);

            if (!IsSkipConfirmOpen && bubbleVisible && !isTask && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                HandleClick();
            }

            UpdatePointer();
            UpdateDim(dt);
            UpdateTaskStrip(dt);
        }

        /// <summary>Slides Coalesce and the bubble in while there's a line to say, and out otherwise.</summary>
        private void UpdatePresenter(float dt)
        {
            bool wanted = bubbleVisible && !IsHandingOverTask;
            slide = Mathf.MoveTowards(slide, wanted ? 0f : 1f, dt / SlideSeconds);
            // Squared: eases out on the way in, eases in on the way out
            presenter.anchoredPosition = new Vector2(slideSign * slideDistance * slide * slide, 0f);

            if (!bubbleVisible && slide >= 1f && bubble.gameObject.activeSelf) bubble.gameObject.SetActive(false);
        }

        private void UpdateTyping(float dt)
        {
            if (!bubbleVisible) return;

            // Nothing types until Coalesce is in place; the pose plays as the line starts
            if (!IsPresenterIn) return;
            if (emotePending)
            {
                emotePending = false;
                emoteTime = 0f;
            }

            if (typedChars < fullText.Length)
            {
                typedChars = Mathf.Min(fullText.Length, typedChars + CharsPerSecond * dt);
                int shown = Mathf.FloorToInt(typedChars);
                bodyText.text = fullText.Substring(0, shown);
                if (shown - lastBlipChar >= BlipEveryChars)
                {
                    lastBlipChar = shown;
                    if (!char.IsWhiteSpace(fullText[shown - 1])) PlayBlip();
                }
            }
            else
            {
                bodyText.text = fullText;
                if (HasChoices != choiceRow.gameObject.activeSelf) choiceRow.gameObject.SetActive(HasChoices);
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.6f;
                promptText.text = !isTask && blink ? "▼" : string.Empty;
                if (isTask) taskHold += dt;
            }
        }

        private void AnimateSpeaker(float dt)
        {
            if (frames == null || frames.Length == 0) return;

            if (IsTyping && frames.Length > 1)
            {
                frameTimer += dt;
                float speakingFrame = frameDuration / SpeakingFrameSpeedup;
                if (frameTimer >= speakingFrame)
                {
                    frameTimer -= speakingFrame;
                    frameIndex = (frameIndex + 1) % frames.Length;
                    speakerImage.sprite = frames[frameIndex];
                }
            }
            else if (frameIndex != 0)
            {
                frameIndex = 0;
                frameTimer = 0f;
                speakerImage.sprite = frames[0];
            }
        }

        private void HandleClick()
        {
            if (!bubbleVisible || isTask) return;

            if (IsTyping)
            {
                typedChars = fullText.Length;
                bodyText.text = fullText;
                return;
            }

            AudioManager.Instance?.PlaySfx(SfxId.UiClick, 0.5f);
            Advanced?.Invoke();
        }

        private void UpdatePointer()
        {
            if (!TryGetTargetBounds(pointTarget, out Vector2 min, out Vector2 max))
            {
                if (pointer.gameObject.activeSelf) pointer.gameObject.SetActive(false);
                return;
            }

            float grow = 10f + 4f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            pointer.anchoredPosition = (min + max) * 0.5f;
            pointer.sizeDelta = max - min + Vector2.one * grow * 2f;
            if (!pointer.gameObject.activeSelf) pointer.gameObject.SetActive(true);

            float alpha = Mathf.Lerp(0.55f, 1f, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
            foreach (Image part in pointerParts) part.color = new Color(HazardYellow.r, HazardYellow.g, HazardYellow.b, alpha);
        }

        /// <summary>A UI element's (on any canvas) bounds in this canvas's root space; false if it isn't showing.</summary>
        private bool TryGetTargetBounds(RectTransform target, out Vector2 min, out Vector2 max)
        {
            min = max = Vector2.zero;
            if (target == null || !target.gameObject.activeInHierarchy) return false;

            Canvas targetCanvas = target.GetComponentInParent<Canvas>();
            if (targetCanvas == null) return false;
            targetCanvas = targetCanvas.rootCanvas;
            Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
            Camera ownCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, corner);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, ownCamera, out Vector2 local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }
            return true;
        }

        private void ApplyBlocking()
        {
            catcher.gameObject.SetActive(bubbleVisible && !isTask);
        }

        // ─────────────────────────────────────────────────────
        //  Dim and spotlight
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// Talk lines fade the dim in. With the spotlight on and something pointed at, the dim is drawn as a ring of
        /// five pieces (a soft round hole plus four bars around it) that closes in on the target and follows it.
        /// </summary>
        private void UpdateDim(float dt)
        {
            bool block = bubbleVisible && !isTask;
            float target = !block ? 0f : spotlightEnabled ? SpotlightDimAlpha : DimAlpha;
            dimAlpha = Mathf.MoveTowards(dimAlpha, target, dt * DimFadeSpeed);

            bool spot = spotlightEnabled && dimAlpha > 0f && TryGetTargetBounds(pointTarget, out Vector2 min, out Vector2 max) && PlaceSpot(min, max, dt);
            if (!spot) spotPlaced = false;

            dim.color = new Color(0f, 0f, 0f, spot ? 0f : dimAlpha);
            if (spotlight.gameObject.activeSelf != spot) spotlight.gameObject.SetActive(spot);
            if (!spot) return;
            foreach (Image part in spotParts) part.color = new Color(0f, 0f, 0f, dimAlpha);
        }

        private bool PlaceSpot(Vector2 min, Vector2 max, float dt)
        {
            Vector2 center = (min + max) * 0.5f;
            float radius = Mathf.Max(SpotMinRadius, Mathf.Max(max.x - min.x, max.y - min.y) * 0.5f + SpotPadding);
            if (!spotPlaced)
            {
                spotPlaced = true;
                spotCenter = center;
                spotRadius = SpotIrisStartRadius;
            }
            float follow = 1f - Mathf.Exp(-SpotFollowSharpness * dt);
            spotCenter = Vector2.Lerp(spotCenter, center, follow);
            spotRadius = Mathf.Lerp(spotRadius, radius, follow);

            Rect area = root.rect;
            float c = spotCenter.x, m = spotCenter.y, r = spotRadius;
            SetSpotPart(0, spotCenter, new Vector2(r * 2f, r * 2f));
            SetSpotPart(1, new Vector2((area.xMin + c - r) * 0.5f, area.center.y), new Vector2(c - r - area.xMin, area.height));
            SetSpotPart(2, new Vector2((c + r + area.xMax) * 0.5f, area.center.y), new Vector2(area.xMax - c - r, area.height));
            SetSpotPart(3, new Vector2(c, (m + r + area.yMax) * 0.5f), new Vector2(r * 2f, area.yMax - m - r));
            SetSpotPart(4, new Vector2(c, (area.yMin + m - r) * 0.5f), new Vector2(r * 2f, m - r - area.yMin));
            return true;
        }

        private void SetSpotPart(int index, Vector2 center, Vector2 size)
        {
            RectTransform rect = spotParts[index].rectTransform;
            rect.anchoredPosition = center;
            rect.sizeDelta = Vector2.Max(size, Vector2.zero);
        }

        /// <summary>White outside a soft-edged circle, clear inside; tinted by the dim's colour.</summary>
        private static Sprite GetSpotHoleSprite()
        {
            if (spotHoleSprite != null) return spotHoleSprite;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "TutorialSpotHole",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude / half;
                    byte alpha = (byte)Mathf.RoundToInt(255f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, distance)));
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            spotHoleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return spotHoleSprite;
        }

        // ─────────────────────────────────────────────────────
        //  Emotes
        // ─────────────────────────────────────────────────────

        private void UpdateEmote(float dt)
        {
            Vector2 offset = Vector2.zero;
            float angle = 0f;
            Vector2 scale = Vector2.one;
            if (emoteTime >= 0f)
            {
                emoteTime += dt;
                if (!Pose(emote, emoteTime, ref offset, ref angle, ref scale)) emoteTime = -1f;
            }

            // Poses are authored at full size, leaning toward the bubble: left of Coalesce, or right in the compact layout
            float toward = layout == TutorialDialogueLayout.Compact ? 1f : -1f;
            float unit = speakerRig.sizeDelta.y / SpriteSize;
            speaker.anchoredPosition = new Vector2(offset.x * toward, offset.y) * unit;
            speaker.localRotation = Quaternion.Euler(0f, 0f, -angle * toward);
            speaker.localScale = new Vector3(scale.x, scale.y, 1f);
        }

        /// <summary>
        /// The pose <paramref name="t"/> seconds into an emote: offset (x toward the bubble), angle (positive leans
        /// toward the bubble) and squash. False once the emote is over.
        /// </summary>
        private static bool Pose(TutorialEmote pose, float t, ref Vector2 offset, ref float angle, ref Vector2 scale)
        {
            switch (pose)
            {
                case TutorialEmote.Happy:
                {
                    // One hop, stretched in the air, squashed on landing
                    if (t >= 0.6f) return false;
                    if (t < 0.42f)
                    {
                        float h = Mathf.Sin(Mathf.PI * t / 0.42f);
                        offset = new Vector2(0f, 40f * h);
                        angle = 3f * Mathf.Sin(2f * Mathf.PI * t / 0.42f);
                        scale = new Vector2(1f - 0.04f * h, 1f + 0.05f * h);
                    }
                    else
                    {
                        float s = Mathf.Sin(Mathf.PI * (t - 0.42f) / 0.18f);
                        scale = new Vector2(1f + 0.07f * s, 1f - 0.07f * s);
                    }
                    return true;
                }
                case TutorialEmote.Curious:
                {
                    // Tilts the head toward the bubble and holds it
                    if (t >= 0.85f) return false;
                    float w = Envelope(t, 0.18f, 0.55f, 0.85f);
                    offset = new Vector2(10f * w, 6f * w);
                    angle = 10f * w;
                    return true;
                }
                case TutorialEmote.Worried:
                {
                    // Shrinks back from the bubble with a shiver
                    if (t >= 0.7f) return false;
                    float w = Envelope(t, 0.12f, 0.45f, 0.7f);
                    offset = new Vector2(-14f * w + 3f * Mathf.Sin(t * 70f) * w, -8f * w);
                    angle = -6f * w;
                    scale = new Vector2(1f + 0.04f * w, 1f - 0.06f * w);
                    return true;
                }
                case TutorialEmote.Excited:
                {
                    // Two quick hops, rocking each way
                    if (t >= 0.8f) return false;
                    float h = Mathf.Sin(Mathf.PI * Mathf.Repeat(t, 0.4f) / 0.4f);
                    offset = new Vector2(0f, 30f * h);
                    angle = (t < 0.4f ? 4f : -4f) * h;
                    scale = new Vector2(1f - 0.05f * h, 1f + 0.07f * h);
                    return true;
                }
                case TutorialEmote.Point:
                {
                    // Leans in toward the screen
                    if (t >= 0.6f) return false;
                    float w = Envelope(t, 0.15f, 0.35f, 0.6f);
                    offset = new Vector2(28f * w, 4f * w);
                    angle = 7f * w;
                    return true;
                }
                default:
                {
                    // Nod: a small dip toward the bubble
                    if (t >= 0.45f) return false;
                    float b = Mathf.Sin(Mathf.PI * t / 0.45f);
                    offset = new Vector2(6f * b, -16f * b);
                    angle = 4f * b;
                    return true;
                }
            }
        }

        /// <summary>0 → 1 by <paramref name="rise"/>, held until <paramref name="hold"/>, back to 0 by <paramref name="end"/>.</summary>
        private static float Envelope(float t, float rise, float hold, float end)
        {
            if (t < rise) return Mathf.SmoothStep(0f, 1f, t / rise);
            if (t < hold) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (t - hold) / (end - hold));
        }

        // ─────────────────────────────────────────────────────
        //  Task strip
        // ─────────────────────────────────────────────────────

        /// <summary>Once Coalesce has handed a task line over, the strip keeps it on screen until the next line.</summary>
        private void UpdateTaskStrip(float dt)
        {
            taskStrip.alpha = Mathf.MoveTowards(taskStrip.alpha, IsHandingOverTask ? 1f : 0f, dt / StripFadeSeconds);
            bool active = taskStrip.alpha > 0f;
            if (taskStrip.gameObject.activeSelf != active) taskStrip.gameObject.SetActive(active);
            taskStripRect.anchoredPosition = taskStripHome + new Vector2(0f, -12f * (1f - taskStrip.alpha));
        }

        // ─────────────────────────────────────────────────────
        //  Voice
        // ─────────────────────────────────────────────────────

        private static void PlayBlip()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;

            // A file at SFX/UI/TutorialVoice replaces the synth. Played as a clip: the per-sound repeat
            // throttle would swallow most of the blips.
            AudioClip clip = audio.GetClip(SfxId.TutorialVoice);
            if (clip == null) clip = GetSynthBlip();
            audio.PlaySfx(clip, BlipVolume, UnityEngine.Random.Range(0.92f, 1.1f));
        }

        /// <summary>A soft 45 ms square-ish blip with a fast attack and decay; Coalesce's placeholder voice.</summary>
        private static AudioClip GetSynthBlip()
        {
            if (synthBlip != null) return synthBlip;

            const int sampleRate = 44100;
            int samples = Mathf.RoundToInt(sampleRate * 0.045f);
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = Mathf.Min(1f, i / (sampleRate * 0.004f)) * Mathf.Exp(-t * 55f);
                float tone = Mathf.Sin(2f * Mathf.PI * 660f * t) * 0.65f + Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 330f * t)) * 0.18f;
                data[i] = tone * envelope * 0.6f;
            }

            synthBlip = AudioClip.Create("CoalesceVoiceBlip", samples, 1, sampleRate, false);
            synthBlip.SetData(data, 0);
            return synthBlip;
        }

        // ─────────────────────────────────────────────────────
        //  Skip
        // ─────────────────────────────────────────────────────

        private void OpenSkipConfirm()
        {
            AudioManager.Instance?.PlaySfx(SfxId.UiClick);
            skipConfirm.SetActive(true);
        }

        private void CloseSkipConfirm()
        {
            AudioManager.Instance?.PlaySfx(SfxId.UiBack);
            skipConfirm.SetActive(false);
        }

        // ─────────────────────────────────────────────────────
        //  Build
        // ─────────────────────────────────────────────────────
    }
}
