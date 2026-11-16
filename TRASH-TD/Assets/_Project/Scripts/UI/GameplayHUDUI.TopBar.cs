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
    // The top bar: wave and hostile readouts, next-wave preview, wave progress bar and the lives tube.
    public partial class GameplayHUDUI
    {
        private void CreateTopBar(Transform root)
        {
            // Background strip
            stageInfoPanel = new GameObject("TopBar", typeof(RectTransform), typeof(Image));
            stageInfoPanel.transform.SetParent(root, false);
            var barRect = stageInfoPanel.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.sizeDelta = new Vector2(0f, 90f);
            barRect.anchoredPosition = Vector2.zero;
            if (topBarBgSprites != null && topBarBgSprites.Length > 0)
            {
                topBarBgAnimImage = stageInfoPanel.GetComponent<Image>();
                topBarBgAnimImage.sprite = topBarBgSprites[0];
                topBarBgAnimImage.type = Image.Type.Simple;
                topBarBgAnimImage.color = Color.white;
            }
            topBarRect = barRect;

            // Pause button on the art's small left screen (art x 3..20)
            var pauseButton = CreateButton(stageInfoPanel.transform, "PauseButton", string.Empty, new Vector2(50f, 50f), ButtonContainerType.IconDark);
            SetPosition(pauseButton.GetComponent<RectTransform>(), new Vector2(0f, -41f), new Vector2(12f / TopArtWidth, 1f), new Vector2(50f, 50f), new Vector2(0.5f, 0.5f));
            pauseButton.onClick.AddListener(PauseGame);
            StyleButton(pauseButton, ButtonContainerType.IconDark, Color.white, 1.08f);

            var pauseIcon = GetPauseIconSprite();
            if (pauseIcon != null)
            {
                CreateIconImage(pauseButton.transform, "Icon", pauseIcon, Color.white, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(26f, 26f), new Vector2(0.5f, 0.5f));
            }
            else
            {
                var label = pauseButton.GetComponentInChildren<Text>();
                if (label != null) label.text = "||";
            }

            // Readouts sit on the art's four dark screens as labelled terminal readouts: a small dim label and a
            // bright value, with scanlines and a faint glow in the phase colour. Each screen is anchored as a
            // fraction of the bar, so it stays on its screen however the art is stretched (any aspect ratio).
            RectTransform phaseScreen = CreateTopScreen("PhaseScreen", 26f, 77f, 4f, 24f);
            RectTransform waveScreen = CreateTopScreen("WaveScreen", 113f, 168f, 6f, 23f);
            RectTransform hostileScreen = CreateTopScreen("HostileScreen", 300f, 334f, 6f, 23f);
            RectTransform livesScreen = CreateTopScreen("LivesScreen", 591f, 636f, 6f, 23f);

            // ── Phase: PHASE / CARD PICK, PREPARATION, INVADING, with a status light ──
            phaseHeaderText = CreateScreenLabel(phaseScreen, "PHASE", TextAnchor.MiddleLeft, 6f);
            phaseLed = CreateTopImage(phaseScreen, "StatusLight", PrepColor);
            phaseLed.rectTransform.anchorMin = phaseLed.rectTransform.anchorMax = new Vector2(1f, 1f);
            phaseLed.rectTransform.pivot = new Vector2(1f, 1f);
            phaseLed.rectTransform.anchoredPosition = new Vector2(-8f, -8f);
            phaseLed.rectTransform.sizeDelta = new Vector2(8f, 8f);

            phaseText = CreateText(phaseScreen, "PhaseText", "PREPARATION", 15, TextAnchor.MiddleLeft);
            phaseText.color = PrepColor;
            UseBestFit(phaseText, 9, 15);
            PlaceScreenRow(phaseText.rectTransform, 24f, 32f);

            // ── Wave: WAVE (or NEXT between waves) + "07/20" on one row, the progress bar under it ──
            waveLabelText = CreateScreenLabel(waveScreen, "WAVE", TextAnchor.MiddleLeft, 9f);
            waveText = CreateText(waveScreen, "WaveText", "01/01", 18, TextAnchor.MiddleRight);
            UseBestFit(waveText, 10, 18);
            PlaceScreenRow(waveText.rectTransform, 2f, 26f, 60f);
            CreateWaveProgressBar(waveScreen);

            // ── Hostiles: on the field (+ still to spawn); between waves, the next wave's size ──
            enemyLabelText = CreateScreenLabel(hostileScreen, "HOSTILES", TextAnchor.MiddleCenter, 5f);
            enemyText = CreateText(hostileScreen, "EnemyText", "0", 20, TextAnchor.MiddleCenter);
            UseBestFit(enemyText, 10, 20);
            enemyIncomingText = CreateText(hostileScreen, "EnemyIncomingText", string.Empty, 11, TextAnchor.MiddleLeft);
            enemyIncomingText.color = DraftColor;
            UseBestFit(enemyIncomingText, 8, 11);
            SetHostileReadoutSplit(false);

            // ── Lives: the tube, with the count on top of it. Sandbox shows "LIVES LOCKED" instead. ──
            CreateLivesTube(livesScreen);
            lpText = CreateText(livesScreen, "LivesText", string.Empty, 16, TextAnchor.MiddleCenter);
            UseBestFit(lpText, 9, 16);
            Stretch(lpText.rectTransform);
            var livesOutline = lpText.gameObject.AddComponent<Outline>();
            livesOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            livesOutline.effectDistance = new Vector2(2f, -2f);

            CreateNextWavePreview();

            // Texts should never eat clicks
            foreach (Text text in stageInfoPanel.GetComponentsInChildren<Text>(true)) text.raycastTarget = false;
        }

        // Top Panel BG frame size in art pixels; it is drawn across the full bar width, 3x at 1920x90.
        private const float TopArtWidth = 640f;
        private const float TopArtHeight = 30f;

        /// <summary>
        /// A readout area over one of the art's dark screens (art pixels, inclusive). Anchored as a fraction of
        /// the bar, so it follows the art when the bar is stretched. Adds scanlines and a phase-colour glow.
        /// </summary>
        private RectTransform CreateTopScreen(string objectName, float artX0, float artX1, float artY0, float artY1)
        {
            var screen = new GameObject(objectName, typeof(RectTransform));
            screen.transform.SetParent(stageInfoPanel.transform, false);
            var rect = screen.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(artX0 / TopArtWidth, 1f - (artY1 + 1f) / TopArtHeight);
            rect.anchorMax = new Vector2((artX1 + 1f) / TopArtWidth, 1f - artY0 / TopArtHeight);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image glow = CreateTopImage(rect, "Glow", new Color(PrepColor.r, PrepColor.g, PrepColor.b, TopScreenGlowAlpha));
            Stretch(glow.rectTransform);
            topScreenGlows.Add(glow);

            Image scanlines = CreateTopImage(rect, "Scanlines", Color.white);
            scanlines.sprite = GetScanlineSprite();
            scanlines.type = Image.Type.Tiled;
            Stretch(scanlines.rectTransform);
            return rect;
        }

        private const float TopScreenGlowAlpha = 0.07f;

        private static Image CreateTopImage(Transform parent, string objectName, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Small dim caps label on a screen's top row.</summary>
        private static Text CreateScreenLabel(RectTransform screen, string label, TextAnchor alignment, float top)
        {
            Text text = CreateText(screen, label + "Label", label, 9, alignment);
            text.fontStyle = FontStyle.Normal;
            text.color = TextDim;
            UseBestFit(text, 7, 9);
            PlaceScreenRow(text.rectTransform, top, 12f);
            return text;
        }

        /// <summary>Stretches a rect across its screen (8 px side insets) at a fixed row from the top.</summary>
        private static void PlaceScreenRow(RectTransform rect, float top, float height, float leftInset = 8f, float rightInset = 8f)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(leftInset, -(top + height));
            rect.offsetMax = new Vector2(-rightInset, -top);
        }

        private static void UseBestFit(Text text, int min, int max)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = min;
            text.resizeTextMaxSize = max;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>
        /// Hostiles screen value row: one centred number, or (during a wave with spawns still to come)
        /// the on-field count on the left and "+N" incoming on the right.
        /// </summary>
        private void SetHostileReadoutSplit(bool split)
        {
            if (enemyText == null || enemyIncomingText == null) return;
            RectTransform main = enemyText.rectTransform;
            PlaceScreenRow(main, 20f, 28f, 4f, 4f);
            if (split)
            {
                main.anchorMax = new Vector2(0.58f, 1f);
                enemyText.alignment = TextAnchor.MiddleRight;
                PlaceScreenRow(enemyIncomingText.rectTransform, 22f, 26f, 4f, 4f);
                enemyIncomingText.rectTransform.anchorMin = new Vector2(0.62f, 1f);
            }
            else
            {
                enemyText.alignment = TextAnchor.MiddleCenter;
            }
            enemyIncomingText.gameObject.SetActive(split);
        }

        private static Sprite scanlineSprite;

        /// <summary>1 dark line every 3 px (one art pixel row at 3x), tiled over a screen.</summary>
        private static Sprite GetScanlineSprite()
        {
            if (scanlineSprite != null) return scanlineSprite;
            var texture = new Texture2D(1, 3, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            texture.SetPixels32(new[]
            {
                new Color32(0, 0, 0, 70),
                new Color32(0, 0, 0, 0),
                new Color32(0, 0, 0, 0)
            });
            texture.Apply();
            scanlineSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 3f), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            return scanlineSprite;
        }

        /// <summary>
        /// "NEXT WAVE 08" tab hanging a little under the bar (centred under the hostiles screen) between waves:
        /// each enemy type in the next wave with its count (first sprite frame + "x8"). One row of up to
        /// NextWavePreviewMaxTypes types; with more, "+N MORE" makes the tab clickable, and a click expands it to
        /// extra rows of the same size (and back).
        /// </summary>
        private void CreateNextWavePreview()
        {
            nextWavePreview = new GameObject("NextWavePreview", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(Button));
            nextWavePreview.transform.SetParent(stageInfoPanel.transform, false);
            var rect = nextWavePreview.GetComponent<RectTransform>();
            float centerX = (300f + 335f) * 0.5f / TopArtWidth;
            // 10 px gap under the bar; the height follows the rows
            SetPosition(rect, new Vector2(0f, -NextWavePreviewGap), new Vector2(centerX, 0f), new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            ApplyPanelContainer(nextWavePreview, PanelContainerType.Dark);
            nextWavePreview.GetComponent<Image>().raycastTarget = false;

            // Stand out from the map: an amber rim and a drop shadow
            var rim = nextWavePreview.AddComponent<Outline>();
            rim.effectColor = new Color(DraftColor.r, DraftColor.g, DraftColor.b, 0.55f);
            rim.effectDistance = new Vector2(2f, -2f);
            var shadow = nextWavePreview.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(0f, -5f);

            var layout = nextWavePreview.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 7, 7);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = nextWavePreview.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            nextWavePreviewButton = nextWavePreview.GetComponent<Button>();
            nextWavePreviewButton.targetGraphic = nextWavePreview.GetComponent<Image>();
            nextWavePreviewButton.transition = Selectable.Transition.ColorTint;
            // Hover brightens an expandable tab; a tab that can't expand (not interactable) looks the same as normal
            Color rest = new Color(0.88f, 0.88f, 0.88f, 1f);
            ColorBlock colors = nextWavePreviewButton.colors;
            colors.normalColor = rest;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.selectedColor = rest;
            colors.disabledColor = rest;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            nextWavePreviewButton.colors = colors;
            nextWavePreviewButton.navigation = new Navigation { mode = Navigation.Mode.None };
            nextWavePreviewButton.onClick.AddListener(ToggleNextWavePreview);
            nextWavePreviewButton.interactable = false;
            nextWavePreview.SetActive(false);
        }

        private const int NextWavePreviewMaxTypes = 6;
        private const float NextWavePreviewGap = 10f;
        private const float NextWaveIconSize = 40f;

        private void RefreshNextWavePreview()
        {
            if (nextWavePreview == null) return;

            WaveData next = null;
            bool show = !IsSandbox && gameManager != null && waveManager != null
                && gameManager.CurrentPhase != StagePhase.WaveActive;
            if (show) next = waveManager.PeekNextWave();
            show = show && WaveManager.CountEnemies(next) > 0;
            nextWavePreview.SetActive(show);
            if (!show)
            {
                nextWavePreviewExpanded = false;
                return;
            }

            int total = waveManager.TotalWaves;
            int number = gameManager.GetCurrentWaveIndex() + 1;
            bool final = total > 0 && number >= total;
            nextWaveTitle = final ? "FINAL WAVE" : $"NEXT WAVE {number:00}";
            nextWaveTitleColor = final ? LifeColor : DraftColor;
            // A new wave starts collapsed
            if (number != nextWavePreviewNumber) nextWavePreviewExpanded = false;
            nextWavePreviewNumber = number;

            // Enemy types in spawn-list order, with their total counts
            nextWaveOrder.Clear();
            nextWaveCounts.Clear();
            foreach (WaveEntry entry in next.entries)
            {
                if (entry == null || entry.enemyData == null || entry.count <= 0) continue;
                if (!nextWaveCounts.ContainsKey(entry.enemyData))
                {
                    nextWaveCounts[entry.enemyData] = 0;
                    nextWaveOrder.Add(entry.enemyData);
                }
                nextWaveCounts[entry.enemyData] += entry.count;
            }

            BuildNextWaveRows();
        }

        private void ToggleNextWavePreview()
        {
            if (nextWaveOrder.Count <= NextWavePreviewMaxTypes) return;
            nextWavePreviewExpanded = !nextWavePreviewExpanded;
            BuildNextWaveRows();
        }

        /// <summary>
        /// Lays out the rows: the title and the first NextWavePreviewMaxTypes types, then (expanded) the rest in
        /// rows of the same width. A "+N MORE" / "LESS" hint ends the last row when the tab can expand.
        /// </summary>
        private void BuildNextWaveRows()
        {
            foreach (GameObject item in nextWavePreviewItems)
            {
                if (item == null) continue;
                item.SetActive(false); // out of this frame's layout; Destroy waits for the end of the frame
                Destroy(item);
            }
            nextWavePreviewItems.Clear();

            bool expandable = nextWaveOrder.Count > NextWavePreviewMaxTypes;
            if (!expandable) nextWavePreviewExpanded = false;
            nextWavePreviewButton.interactable = expandable;
            // Only an expandable tab takes clicks; otherwise clicks fall through to the map
            nextWavePreview.GetComponent<Image>().raycastTarget = expandable;

            int shown = nextWavePreviewExpanded ? nextWaveOrder.Count : Mathf.Min(nextWaveOrder.Count, NextWavePreviewMaxTypes);
            Transform row = CreateNextWaveRow();
            Text title = CreateText(row, "NextWaveLabel", nextWaveTitle, 12, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            title.color = nextWaveTitleColor;
            title.raycastTarget = false;
            var titleLayout = title.gameObject.AddComponent<LayoutElement>();
            titleLayout.minWidth = 150f; // later rows indent past the title, so the icons line up in columns

            for (int i = 0; i < shown; i++)
            {
                if (i > 0 && i % NextWavePreviewMaxTypes == 0)
                {
                    row = CreateNextWaveRow();
                    var indent = new GameObject("Indent", typeof(RectTransform), typeof(LayoutElement));
                    indent.transform.SetParent(row, false);
                    indent.GetComponent<LayoutElement>().minWidth = titleLayout.minWidth;
                }
                CreateNextWaveItem(row, nextWaveOrder[i], nextWaveCounts[nextWaveOrder[i]]);
            }

            if (expandable)
            {
                string hint = nextWavePreviewExpanded ? "LESS" : $"+{nextWaveOrder.Count - shown} MORE";
                Text more = CreateText(row, "More", hint, 11, TextAnchor.MiddleLeft);
                more.fontStyle = FontStyle.Bold;
                more.color = DraftColor;
                more.raycastTarget = false;
            }
        }

        private Transform CreateNextWaveRow()
        {
            var row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(nextWavePreview.transform, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.GetComponent<LayoutElement>().minHeight = NextWaveIconSize;
            nextWavePreviewItems.Add(row);
            return row.transform;
        }

        private GameObject CreateNextWaveItem(Transform row, EnemyData enemy, int count)
        {
            var item = new GameObject("Next_" + enemy.enemyName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            item.transform.SetParent(row, false);
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
            iconLayout.preferredWidth = NextWaveIconSize;
            iconLayout.preferredHeight = NextWaveIconSize;

            Text countText = CreateText(item.transform, "Count", "x" + count, 13, TextAnchor.MiddleLeft);
            countText.fontStyle = FontStyle.Bold;
            countText.color = Color.white;
            countText.raycastTarget = false;
            var countLayout = countText.gameObject.AddComponent<LayoutElement>();
            countLayout.minWidth = 44f; // "x88" wide, so the expanded rows keep their columns
            return item;
        }

        /// <summary>WAVE label + "07/20" during a wave; NEXT + the upcoming wave between waves.</summary>
        private void UpdateWaveReadout()
        {
            if (waveText == null || waveManager == null) return;

            if (IsSandbox)
            {
                if (waveLabelText != null) waveLabelText.text = "WAVE";
                waveText.text = Mathf.Max(1, waveManager.SandboxWaveNumber).ToString("00");
                return;
            }

            int total = waveManager.TotalWaves;
            if (total <= 0) return;

            bool betweenWaves = gameManager != null && gameManager.CurrentPhase != StagePhase.WaveActive;
            int number = betweenWaves && gameManager != null
                ? Mathf.Clamp(gameManager.GetCurrentWaveIndex() + 1, 1, total)
                : Mathf.Clamp(waveManager.CurrentWaveNumber, 1, total);
            if (waveLabelText != null) waveLabelText.text = betweenWaves ? "NEXT" : "WAVE";
            waveText.text = $"{number:00}/{total:00}";
        }

        /// <summary>HOSTILES: on the field (+ still to spawn). Between waves: INCOMING with the next wave's size.</summary>
        private void UpdateHostileReadout()
        {
            if (enemyText == null || enemyManager == null) return;

            bool betweenWaves = !IsSandbox && gameManager != null && gameManager.CurrentPhase != StagePhase.WaveActive;
            int nextWaveSize = betweenWaves && waveManager != null ? WaveManager.CountEnemies(waveManager.PeekNextWave()) : 0;
            if (betweenWaves && enemyManager.ActiveEnemyCount == 0 && nextWaveSize > 0)
            {
                if (enemyLabelText != null) enemyLabelText.text = "INCOMING";
                enemyText.text = nextWaveSize.ToString();
                SetHostileReadoutSplit(false);
                return;
            }

            int pending = waveManager != null ? waveManager.PendingSpawnCount : 0;
            if (enemyLabelText != null) enemyLabelText.text = "HOSTILES";
            enemyText.text = enemyManager.ActiveEnemyCount.ToString();
            bool split = pending > 0;
            if (split) enemyIncomingText.text = "+" + pending;
            SetHostileReadoutSplit(split);
        }

        // UI/bars sprites are drawn at 2x. bar_frame is 24x10 with its inner channel at x 2..21, y 2..7;
        // tube_frame is 22x64 (8 px end caps) with its inner channel at x 3..16, y 8..56 from the bottom.
        private const float BarPixelScale = 2f;
        private const float TubeInnerX = 3f;
        private const float TubeInnerBottom = 8f;
        private const float TubeInnerWidth = 14f;
        private const float TubeInnerHeight = 49f;
        // The lives tube is drawn bigger than its screen on the top bar (it overflows the dark screen onto the bar art):
        // LivesTubeScale times the usual art-pixel scale, nudged left so its far end stays on screen.
        private const float LivesTubeScale = 1.5f;
        private const float LivesTubeShiftLeft = 26f;
        private const float LivesTubePixelScale = BarPixelScale * LivesTubeScale;

        private void CreateWaveProgressBar(Transform parent)
        {
            // Framed bar on the wave screen's bottom row; the fill sprite follows the phase (see SetWaveProgressFillSprite)
            var frame = new GameObject("WaveProgressBar", typeof(RectTransform), typeof(Image));
            frame.transform.SetParent(parent, false);
            PlaceScreenRow(frame.GetComponent<RectTransform>(), 30f, 10f * BarPixelScale, 6f, 6f);
            Image frameImage = frame.GetComponent<Image>();
            frameImage.raycastTarget = false;
            Sprite frameSprite = LoadWholeTextureSprite("UI/bars/bar_frame", new Vector4(4f, 4f, 4f, 4f));
            if (frameSprite != null)
            {
                frameImage.sprite = frameSprite;
                frameImage.type = Image.Type.Sliced;
                frameImage.pixelsPerUnitMultiplier = 1f / BarPixelScale;
            }
            else
            {
                frameImage.color = new Color(1f, 1f, 1f, 0.07f);
            }

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(frame.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f) * BarPixelScale;
            fillAreaRect.offsetMax = new Vector2(-2f, -2f) * BarPixelScale;

            var fill = new GameObject("WaveProgressFill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            waveProgressFillRect = fill.GetComponent<RectTransform>();
            waveProgressFillRect.anchorMin = Vector2.zero;
            waveProgressFillRect.anchorMax = new Vector2(0f, 1f);
            waveProgressFillRect.offsetMin = Vector2.zero;
            waveProgressFillRect.offsetMax = Vector2.zero;
            waveProgressFill = fill.GetComponent<Image>();
            waveProgressFill.raycastTarget = false;
            waveProgressFill.type = Image.Type.Tiled;
            waveProgressFill.pixelsPerUnitMultiplier = 1f / BarPixelScale;
            SetWaveProgressFillSprite(StagePhase.Preparation);
        }

        private void SetWaveProgressFillSprite(StagePhase phase)
        {
            if (waveProgressFill == null) return;

            string fillName = phase switch
            {
                StagePhase.CardPick => "bar_fill_yellow",
                StagePhase.WaveActive => "bar_fill_red",
                _ => "bar_fill_blue"
            };

            Sprite sprite = LoadWholeTextureSprite("UI/bars/" + fillName);
            if (sprite != null)
            {
                waveProgressFill.sprite = sprite;
                waveProgressFill.color = Color.white;
            }
            else
            {
                waveProgressFill.sprite = null;
                waveProgressFill.color = phase switch
                {
                    StagePhase.CardPick => DraftColor,
                    StagePhase.WaveActive => WaveColor,
                    _ => PrepColor
                };
            }
        }

        private void CreateLivesTube(Transform parent)
        {
            // tube_frame is vertical; the root is built upright and turned -90 degrees, so the liquid
            // fills from the left (the tube's bottom) and drains toward it as lives are lost.
            var root = new GameObject("LivesTube", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            livesTubeRect = root.GetComponent<RectTransform>();
            // Centred on the lives screen (the Top Panel BG's right-hand screen)
            SetPosition(livesTubeRect, new Vector2(-LivesTubeShiftLeft, 0f), new Vector2(0.5f, 0.5f), new Vector2(22f, 64f) * LivesTubePixelScale, new Vector2(0.5f, 0.5f));
            livesTubeRect.localRotation = Quaternion.Euler(0f, 0f, -90f);

            livesTubeFrame = root.GetComponent<Image>();
            livesTubeFrame.raycastTarget = false;
            Sprite frameSprite = LoadWholeTextureSprite("UI/bars/tube_frame");
            if (frameSprite != null) livesTubeFrame.sprite = frameSprite;
            else livesTubeFrame.color = new Color(0.1f, 0.12f, 0.15f, 1f);

            var liquid = new GameObject("Liquid", typeof(RectTransform), typeof(Image));
            liquid.transform.SetParent(root.transform, false);
            livesLiquidRect = liquid.GetComponent<RectTransform>();
            SetPosition(livesLiquidRect, new Vector2(TubeInnerX, TubeInnerBottom) * LivesTubePixelScale, Vector2.zero,
                new Vector2(TubeInnerWidth, TubeInnerHeight) * LivesTubePixelScale, Vector2.zero);
            Image liquidImage = liquid.GetComponent<Image>();
            liquidImage.raycastTarget = false;
            Sprite liquidSprite = LoadWholeTextureSprite("UI/bars/tube_liquid");
            if (liquidSprite != null)
            {
                liquidImage.sprite = liquidSprite;
                liquidImage.type = Image.Type.Tiled;
                liquidImage.pixelsPerUnitMultiplier = 1f / LivesTubePixelScale;
            }
            else
            {
                liquidImage.color = LifeColor;
            }

            // Bright meniscus on the liquid's leading edge
            livesSurface = new GameObject("Surface", typeof(RectTransform), typeof(Image));
            livesSurface.transform.SetParent(liquid.transform, false);
            var surfaceRect = livesSurface.GetComponent<RectTransform>();
            surfaceRect.anchorMin = new Vector2(0f, 1f);
            surfaceRect.anchorMax = new Vector2(1f, 1f);
            surfaceRect.pivot = new Vector2(0.5f, 1f);
            surfaceRect.sizeDelta = new Vector2(0f, 3f * LivesTubePixelScale);
            surfaceRect.anchoredPosition = Vector2.zero;
            Image surfaceImage = livesSurface.GetComponent<Image>();
            surfaceImage.raycastTarget = false;
            Sprite surfaceSprite = LoadWholeTextureSprite("UI/bars/tube_surface");
            if (surfaceSprite != null)
            {
                surfaceImage.sprite = surfaceSprite;
            }
            else
            {
                Destroy(livesSurface);
                livesSurface = null;
            }
        }

        private void ApplyLivesLiquid(float ratio)
        {
            if (livesLiquidRect == null) return;

            float height = Mathf.Round(Mathf.Clamp01(ratio) * TubeInnerHeight) * LivesTubePixelScale;
            livesLiquidRect.sizeDelta = new Vector2(TubeInnerWidth * LivesTubePixelScale, height);
            livesLiquidRect.gameObject.SetActive(height > 0f);
            if (livesSurface != null) livesSurface.SetActive(height >= 3f * LivesTubePixelScale);
        }
    }
}
