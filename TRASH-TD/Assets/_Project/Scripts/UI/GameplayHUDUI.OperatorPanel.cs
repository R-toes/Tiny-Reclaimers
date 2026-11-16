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
    // HUD assembly, the selected-operator label and details panel, effect badges and the placement controls.
    public partial class GameplayHUDUI
    {
        // ============================
        // HUD Construction
        // ============================

        private void BuildHud()
        {
            var canvasObject = new GameObject("GameplayHUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var root = canvasObject.transform;
            surveillanceOverlay = SurveillanceOverlayUI.Create(root); // first, so the rest of the HUD draws over it
            CreateDangerFlash(root);
            CreatePathPreviewOverlay(root);
            CreateTopBar(root);
            CreateDeckBar(root);
            CreateDeckMergeAnimator(root);
            // Trash drop zone is created and parented inside CreateDeckBar to align with the deck background
            CreateDiscardConfirmationPanel(root);
            CreatePhaseBanner(root);
            CreateEnvironmentBriefing(root);
            CreatePlacementControls(root);
            CreateSelectedOperatorLabel(root);
            CreateSelectedOperatorDetailsPanel(root);
            CreateCooldownToast(root);
            CreateHudHiddenHint();
            OperatorDeathNoticeUI.Create(root, operatorManager); // "OPERATOR DOWN" kill feed on the left
            CreatePausePanel(root); // last, so it renders above everything else
        }

        private void CreateDangerFlash(Transform root)
        {
            var flash = new GameObject("DangerFlash", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(root, false);
            Stretch(flash.GetComponent<RectTransform>());

            dangerFlashImage = flash.GetComponent<Image>();
            dangerFlashImage.sprite = CreateVignetteSprite();
            dangerFlashImage.color = new Color(1f, 0.1f, 0.1f, 0f);
            dangerFlashImage.raycastTarget = false;
        }

        private void CreateSelectedOperatorLabel(Transform root)
        {
            selectedOperatorLabelRoot = new GameObject("SelectedOperatorName", typeof(RectTransform), typeof(Image));
            selectedOperatorLabelRoot.transform.SetParent(root, false);
            RectTransform labelRect = selectedOperatorLabelRoot.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0.5f);
            labelRect.anchorMax = new Vector2(0.5f, 0.5f);
            labelRect.pivot = new Vector2(0.5f, 0f); // Pivot at bottom-center so panel grows upwards and never covers the operator!
            labelRect.sizeDelta = new Vector2(280f, 106f);

            Image background = selectedOperatorLabelRoot.GetComponent<Image>();
            ApplyPanelContainer(selectedOperatorLabelRoot, PanelContainerType.Tooltip);
            background.raycastTarget = false;

            var labelBorder = selectedOperatorLabelRoot.AddComponent<Outline>();
            labelBorder.effectColor = new Color(1f, 1f, 1f, 0.10f);
            labelBorder.effectDistance = new Vector2(1.5f, -1.5f);

            // Row 1: Name and Rarity (stars)
            selectedOperatorNameText = CreateText(selectedOperatorLabelRoot.transform, "Name", string.Empty, 15, TextAnchor.MiddleLeft);
            selectedOperatorNameText.fontStyle = FontStyle.Bold;
            selectedOperatorNameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetPosition(selectedOperatorNameText.GetComponent<RectTransform>(), new Vector2(12f, -8f), new Vector2(0f, 1f), new Vector2(180f, 22f), new Vector2(0f, 1f));
            selectedOperatorNameText.raycastTarget = false;

            selectedOperatorRarityText = CreateText(selectedOperatorLabelRoot.transform, "Rarity", string.Empty, 16, TextAnchor.MiddleRight);
            selectedOperatorRarityText.color = new Color(1f, 0.78f, 0.2f, 1f);
            SetPosition(selectedOperatorRarityText.GetComponent<RectTransform>(), new Vector2(-12f, -8f), new Vector2(1f, 1f), new Vector2(76f, 22f), new Vector2(1f, 1f));
            selectedOperatorRarityText.raycastTarget = false;

            selectedOperatorUpgradeBadge = CreateUpgradeBadge(selectedOperatorLabelRoot.transform, "SelectedOperatorUpgradeBadge", new Vector2(30f, 16f), new Vector2(-88f, -11f));
            selectedOperatorUpgradeBadgeGraphic = selectedOperatorUpgradeBadge.GetComponentInChildren<UpgradeArrowGraphic>();

            // Row 2: HP (text and health bar)
            selectedOperatorHealthText = CreateText(selectedOperatorLabelRoot.transform, "Health", string.Empty, 12, TextAnchor.MiddleLeft);
            selectedOperatorHealthText.fontStyle = FontStyle.Bold;
            SetPosition(selectedOperatorHealthText.GetComponent<RectTransform>(), new Vector2(12f, -31f), new Vector2(0f, 1f), new Vector2(110f, 18f), new Vector2(0f, 1f));
            selectedOperatorHealthText.raycastTarget = false;

            GameObject healthTrack = new GameObject("HealthTrack", typeof(RectTransform), typeof(Image));
            healthTrack.transform.SetParent(selectedOperatorLabelRoot.transform, false);
            Image healthTrackImage = healthTrack.GetComponent<Image>();
            healthTrackImage.color = new Color(0.12f, 0.14f, 0.16f, 1f);
            healthTrackImage.raycastTarget = false;
            SetPosition(healthTrack.GetComponent<RectTransform>(), new Vector2(-12f, -34f), new Vector2(1f, 1f), new Vector2(140f, 12f), new Vector2(1f, 1f));

            // The fill is width-driven (anchors) because an Image set to "Filled" with no sprite
            // ignores fillAmount and always draws full.
            GameObject healthFill = new GameObject("HealthFill", typeof(RectTransform), typeof(Image));
            healthFill.transform.SetParent(healthTrack.transform, false);
            selectedOperatorHealthFill = healthFill.GetComponent<Image>();
            selectedOperatorHealthFill.color = new Color(0.25f, 0.95f, 0.36f, 1f);
            selectedOperatorHealthFill.raycastTarget = false;
            selectedOperatorHealthFillRect = healthFill.GetComponent<RectTransform>();
            selectedOperatorHealthFillRect.anchorMin = Vector2.zero;
            selectedOperatorHealthFillRect.anchorMax = Vector2.one;
            selectedOperatorHealthFillRect.offsetMin = Vector2.zero;
            selectedOperatorHealthFillRect.offsetMax = Vector2.zero;

            // Row 3: ATK
            selectedOperatorAtkText = CreateText(selectedOperatorLabelRoot.transform, "Atk", string.Empty, 12, TextAnchor.MiddleLeft);
            selectedOperatorAtkText.fontStyle = FontStyle.Bold;
            selectedOperatorAtkText.color = new Color(1f, 0.72f, 0.35f, 1f);
            SetPosition(selectedOperatorAtkText.GetComponent<RectTransform>(), new Vector2(12f, -53f), new Vector2(0f, 1f), new Vector2(256f, 18f), new Vector2(0f, 1f));
            selectedOperatorAtkText.raycastTarget = false;

            // Row 4: Show More
            selectedOperatorShowMoreButton = CreateButton(selectedOperatorLabelRoot.transform, "ShowOperatorDetailsButton", "SHOW MORE", new Vector2(140f, 22f), ButtonContainerType.Dark);
            SetPosition(selectedOperatorShowMoreButton.GetComponent<RectTransform>(), new Vector2(0f, -74f), new Vector2(0.5f, 1f), null, new Vector2(0.5f, 1f));
            Text showMoreLabel = selectedOperatorShowMoreButton.GetComponentInChildren<Text>();
            showMoreLabel.fontSize = 10;
            showMoreLabel.fontStyle = FontStyle.Bold;
            showMoreLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            showMoreLabel.verticalOverflow = VerticalWrapMode.Truncate;
            StyleButton(selectedOperatorShowMoreButton, ButtonContainerType.Dark, PrepColor, 1.06f);
            selectedOperatorShowMoreButton.onClick.AddListener(ToggleSelectedOperatorDetails);

            // Row 5+: Framiliar Copy Section (inside the tab)
            CreateFramiliarCopySection(selectedOperatorLabelRoot.transform);

            // Retreat button (positioned to the right of the tab, horizontally aligned with Row 1-4)
            retreatOperatorButton = CreateButton(root, "RetreatOperatorButton", string.Empty, new Vector2(44f, 44f), ButtonContainerType.IconDanger);
            SetPosition(retreatOperatorButton.GetComponent<RectTransform>(), new Vector2(151f, 53f), new Vector2(0.5f, 0.5f));
            SetPlacementButtonStyle(retreatOperatorButton, ButtonContainerType.IconDanger, 28);
            retreatOperatorButton.onClick.AddListener(RetreatSelectedOperator);
            retreatFeedback = AttachFeedback(retreatOperatorButton, new Color(1f, 0.45f, 0.4f, 1f), 1.1f);

            var exitIcon = GetExitIconSprite();
            if (exitIcon != null)
            {
                CreateIconImage(retreatOperatorButton.transform, "Icon", exitIcon, Color.white, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), new Vector2(0.5f, 0.5f));
            }
            else
            {
                var label = retreatOperatorButton.GetComponentInChildren<Text>();
                if (label != null) label.text = "↶";
            }

            selectedOperatorLabelRoot.SetActive(false);
            retreatOperatorButton.gameObject.SetActive(false);
        }

        private const float DetailsPanelScale = 0.82f;

        private void CreateSelectedOperatorDetailsPanel(Transform root)
        {
            selectedOperatorDetailsPanel = new GameObject(
                "SelectedOperatorDetailsPanel",
                typeof(RectTransform),
                typeof(Image),
                typeof(Outline));
            selectedOperatorDetailsPanel.transform.SetParent(root, false);

            RectTransform panelRect = selectedOperatorDetailsPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.sizeDelta = new Vector2(500f, 640f);
            panelRect.anchoredPosition = new Vector2(-24f, 0f);
            // The whole panel, effects column included, is drawn at this scale about its right edge
            panelRect.localScale = Vector3.one * DetailsPanelScale;

            Image panelImage = selectedOperatorDetailsPanel.GetComponent<Image>();
            ApplyPanelContainer(selectedOperatorDetailsPanel, PanelContainerType.Metal);
            panelImage.raycastTarget = true;
            Outline panelOutline = selectedOperatorDetailsPanel.GetComponent<Outline>();
            panelOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
            panelOutline.effectDistance = new Vector2(2f, -2f);

            var accent = new GameObject("DetailAccent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(selectedOperatorDetailsPanel.transform, false);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 4f);
            selectedOperatorDetailsAccent = accent.GetComponent<Image>();
            selectedOperatorDetailsAccent.color = new Color(0.18f, 0.82f, 0.45f, 0.35f);
            selectedOperatorDetailsAccent.raycastTarget = false;

            Transform content = selectedOperatorDetailsPanel.transform;

            const float detailsMargin = 28f;
            const float detailsInnerWidth = 444f;
            var portraitObject = new GameObject("OperatorPortrait", typeof(RectTransform), typeof(Image));
            portraitObject.transform.SetParent(content, false);
            selectedOperatorDetailsPortrait = portraitObject.GetComponent<Image>();
            selectedOperatorDetailsPortrait.preserveAspect = true;
            selectedOperatorDetailsPortrait.raycastTarget = false;
            selectedOperatorDetailsPortrait.color = Color.white;
            SetPosition(selectedOperatorDetailsPortrait.rectTransform, new Vector2(-detailsMargin, -16f), new Vector2(1f, 1f), new Vector2(72f, 88f), new Vector2(1f, 1f));

            selectedOperatorDetailsName = CreateText(content, "OperatorName", string.Empty, 32, TextAnchor.MiddleLeft);
            selectedOperatorDetailsName.fontStyle = FontStyle.Bold;
            selectedOperatorDetailsName.resizeTextForBestFit = true;
            selectedOperatorDetailsName.resizeTextMinSize = 18;
            selectedOperatorDetailsName.resizeTextMaxSize = 32;
            selectedOperatorDetailsName.raycastTarget = false;
            SetPosition(selectedOperatorDetailsName.rectTransform, new Vector2(detailsMargin, -28f), new Vector2(0f, 1f), new Vector2(detailsInnerWidth - 90f, 42f), new Vector2(0f, 1f));

            selectedOperatorDetailsRarity = CreateText(content, "Rarity", string.Empty, 22, TextAnchor.MiddleLeft);
            selectedOperatorDetailsRarity.raycastTarget = false;
            SetPosition(selectedOperatorDetailsRarity.rectTransform, new Vector2(detailsMargin, -72f), new Vector2(0f, 1f), new Vector2(detailsInnerWidth - 90f, 28f), new Vector2(0f, 1f));

            const float classIconSize = 34f;
            var iconObject = new GameObject("OperatorClassIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(content, false);
            selectedOperatorDetailsClassIcon = iconObject.GetComponent<Image>();
            selectedOperatorDetailsClassIcon.preserveAspect = true;
            selectedOperatorDetailsClassIcon.raycastTarget = false;
            selectedOperatorDetailsClassIcon.color = Color.white;
            SetPosition(selectedOperatorDetailsClassIcon.rectTransform, new Vector2(detailsMargin, -114f), new Vector2(0f, 1f), new Vector2(classIconSize, classIconSize), new Vector2(0f, 1f));

            float chipsStartX = detailsMargin + classIconSize + 10f;
            float chipsTotalWidth = detailsInnerWidth - classIconSize - 10f;
            const float chipGap = 10f;
            float detailChipWidth = (chipsTotalWidth - chipGap * 2f) / 3f;

            selectedOperatorDetailsChipBackgrounds = new Image[3];
            selectedOperatorDetailsChipLabels = new Text[3];
            selectedOperatorDetailsChipOutlines = new Outline[3];
            for (int i = 0; i < selectedOperatorDetailsChipLabels.Length; i++)
            {
                var chip = new GameObject($"TypeChip_{i}", typeof(RectTransform), typeof(Image), typeof(Outline));
                chip.transform.SetParent(content, false);
                float chipX = chipsStartX + (detailChipWidth + chipGap) * i;
                SetPosition(chip.GetComponent<RectTransform>(), new Vector2(chipX, -114f), new Vector2(0f, 1f), new Vector2(detailChipWidth, 34f), new Vector2(0f, 1f));
                selectedOperatorDetailsChipBackgrounds[i] = chip.GetComponent<Image>();
                selectedOperatorDetailsChipBackgrounds[i].color = new Color(0.07f, 0.085f, 0.115f, 1f);
                selectedOperatorDetailsChipBackgrounds[i].raycastTarget = false;
                selectedOperatorDetailsChipOutlines[i] = chip.GetComponent<Outline>();
                selectedOperatorDetailsChipOutlines[i].effectDistance = new Vector2(1.5f, -1.5f);
                selectedOperatorDetailsChipOutlines[i].effectColor = new Color(1f, 1f, 1f, 0.2f);

                selectedOperatorDetailsChipLabels[i] = CreateText(chip.transform, $"Label_{i}", string.Empty, 13, TextAnchor.MiddleCenter);
                selectedOperatorDetailsChipLabels[i].fontStyle = FontStyle.Bold;
                selectedOperatorDetailsChipLabels[i].resizeTextForBestFit = true;
                selectedOperatorDetailsChipLabels[i].resizeTextMinSize = 9;
                selectedOperatorDetailsChipLabels[i].resizeTextMaxSize = 13;
                selectedOperatorDetailsChipLabels[i].raycastTarget = false;
                RectTransform chipTextRect = selectedOperatorDetailsChipLabels[i].rectTransform;
                chipTextRect.anchorMin = Vector2.zero;
                chipTextRect.anchorMax = Vector2.one;
                chipTextRect.offsetMin = Vector2.zero;
                chipTextRect.offsetMax = Vector2.zero;
            }

            selectedOperatorDetailsRoles = CreateText(content, "Roles", string.Empty, 12, TextAnchor.MiddleLeft);
            selectedOperatorDetailsRoles.fontStyle = FontStyle.Normal;
            selectedOperatorDetailsRoles.resizeTextForBestFit = true;
            selectedOperatorDetailsRoles.resizeTextMinSize = 9;
            selectedOperatorDetailsRoles.resizeTextMaxSize = 12;
            selectedOperatorDetailsRoles.raycastTarget = false;
            SetPosition(selectedOperatorDetailsRoles.rectTransform, new Vector2(detailsMargin, -156f), new Vector2(0f, 1f), new Vector2(detailsInnerWidth, 22f), new Vector2(0f, 1f));

            CreateOperatorDetailsDivider(content, detailsMargin, detailsInnerWidth, 190f);

            Text statsHeading = CreateText(content, "StatsHeading", "STATS", 11, TextAnchor.MiddleLeft);
            statsHeading.color = new Color(0.6f, 0.65f, 0.7f, 1f);
            statsHeading.raycastTarget = false;
            statsHeading.fontStyle = FontStyle.Bold;
            SetPosition(statsHeading.rectTransform, new Vector2(detailsMargin, -202f), new Vector2(0f, 1f), new Vector2(detailsInnerWidth, 18f), new Vector2(0f, 1f));

            // Stats: three columns, two rows. They show the operator's current maximums (rank-ups, buffs, passives), with
            // the change against the rank it was drafted at beside each one.
            selectedOperatorDetailsStatValues = new Text[6];
            selectedOperatorDetailsStatDeltas = new Text[6];
            Color[] statColors =
            {
                new Color(0.35f, 0.85f, 0.45f, 1f),
                new Color(0.95f, 0.40f, 0.35f, 1f),
                new Color(0.35f, 0.60f, 1f, 1f),
                new Color(0.70f, 0.45f, 1f, 1f),
                new Color(1f, 0.8f, 0.3f, 1f),
                new Color(0.4f, 0.85f, 0.9f, 1f)
            };
            string[] statNames = { "HP", "ATK", "DEF", "RES", "BLOCK", "ATK SPD" };
            const float tileGap = 10f;
            float tileWidth = (detailsInnerWidth - tileGap * 2f) / 3f;
            float tileHeight = 58f;
            float rowStep = tileHeight + 8f;
            float gridTop = 226f;
            for (int i = 0; i < selectedOperatorDetailsStatValues.Length; i++)
            {
                int row = i / 3;
                int col = i % 3;
                float x = detailsMargin + (tileWidth + tileGap) * col;
                selectedOperatorDetailsStatValues[i] = CreateOperatorDetailStatTile(
                    content,
                    statNames[i],
                    statColors[i],
                    x,
                    gridTop + rowStep * row,
                    tileWidth,
                    tileHeight,
                    out selectedOperatorDetailsStatDeltas[i]);
            }

            CreateOperatorDetailsDivider(content, detailsMargin, detailsInnerWidth, 362f);

            selectedOperatorDetailsSkillHeading = CreateText(content, "SkillHeading", "ABILITY", 11, TextAnchor.MiddleLeft);
            selectedOperatorDetailsSkillHeading.color = new Color(0.6f, 0.65f, 0.7f, 1f);
            selectedOperatorDetailsSkillHeading.raycastTarget = false;
            selectedOperatorDetailsSkillHeading.fontStyle = FontStyle.Bold;
            SetPosition(selectedOperatorDetailsSkillHeading.rectTransform, new Vector2(detailsMargin, -372f), new Vector2(0f, 1f), new Vector2(detailsInnerWidth, 18f), new Vector2(0f, 1f));

            // The skill text keeps one readable size and scrolls (wheel, drag or the thin bar) when it does not fit
            var skillScrollObject = new GameObject("SkillScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            skillScrollObject.transform.SetParent(content, false);
            RectTransform skillScrollRect = skillScrollObject.GetComponent<RectTransform>();
            SetPosition(skillScrollRect, new Vector2(detailsMargin, -396f), new Vector2(0f, 1f), new Vector2(detailsInnerWidth, 216f), new Vector2(0f, 1f));
            skillScrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but catches the mouse wheel

            selectedOperatorDetailsSkill = CreateText(skillScrollRect, "SkillDescription", string.Empty, 15, TextAnchor.UpperLeft);
            selectedOperatorDetailsSkill.fontStyle = FontStyle.Normal;
            selectedOperatorDetailsSkill.lineSpacing = 1.3f;
            selectedOperatorDetailsSkill.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedOperatorDetailsSkill.verticalOverflow = VerticalWrapMode.Overflow;
            selectedOperatorDetailsSkill.color = new Color(0.88f, 0.92f, 0.96f, 1f);
            selectedOperatorDetailsSkill.raycastTarget = false;
            RectTransform skillTextRect = selectedOperatorDetailsSkill.rectTransform;
            skillTextRect.anchorMin = new Vector2(0f, 1f);
            skillTextRect.anchorMax = new Vector2(1f, 1f);
            skillTextRect.pivot = new Vector2(0.5f, 1f);
            skillTextRect.offsetMin = Vector2.zero;
            skillTextRect.offsetMax = new Vector2(-12f, 0f); // right gutter so text never sits under the scroll bar
            selectedOperatorDetailsSkill.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            selectedOperatorDetailsSkillScroll = skillScrollObject.GetComponent<ScrollRect>();
            selectedOperatorDetailsSkillScroll.content = skillTextRect;
            selectedOperatorDetailsSkillScroll.viewport = skillScrollRect;
            selectedOperatorDetailsSkillScroll.horizontal = false;
            selectedOperatorDetailsSkillScroll.movementType = ScrollRect.MovementType.Clamped;
            selectedOperatorDetailsSkillScroll.scrollSensitivity = 30f;
            AttachDetailsScrollbar(selectedOperatorDetailsSkillScroll);

            BuildOperatorEffectsColumn(selectedOperatorDetailsPanel.transform);
            selectedOperatorDetailsPanel.SetActive(false);
        }

        private Text CreateOperatorDetailStatTile(Transform parent, string label, Color accentColor, float x, float y, float width, float height, out Text deltaText)
        {
            var tile = new GameObject($"Stat_{label}", typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(parent, false);
            SetPosition(tile.GetComponent<RectTransform>(), new Vector2(x, -y), new Vector2(0f, 1f), new Vector2(width, height), new Vector2(0f, 1f));
            tile.GetComponent<Image>().color = new Color(0.07f, 0.085f, 0.115f, 1f);
            tile.GetComponent<Image>().raycastTarget = false;

            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(tile.transform, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 0.5f);
            barRect.sizeDelta = new Vector2(4f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            Image barImage = bar.GetComponent<Image>();
            barImage.color = accentColor;
            barImage.raycastTarget = false;

            Text labelText = CreateText(tile.transform, "Label", label, 11, TextAnchor.MiddleLeft);
            labelText.color = new Color(0.6f, 0.65f, 0.7f, 1f);
            labelText.raycastTarget = false;
            labelText.fontStyle = FontStyle.Bold;
            SetPosition(labelText.rectTransform, new Vector2(18f, -6f), new Vector2(0f, 1f), new Vector2(width * 0.5f, 18f), new Vector2(0f, 1f));

            // Change against the rank the operator was drafted at: green when higher, red when lower
            deltaText = CreateText(tile.transform, "Delta", string.Empty, 11, TextAnchor.MiddleRight);
            deltaText.raycastTarget = false;
            deltaText.fontStyle = FontStyle.Bold;
            SetPosition(deltaText.rectTransform, new Vector2(-8f, -6f), new Vector2(1f, 1f), new Vector2(width * 0.5f - 8f, 18f), new Vector2(1f, 1f));

            Text valueText = CreateText(tile.transform, "Value", "0", 26, TextAnchor.MiddleLeft);
            valueText.color = new Color(0.95f, 0.95f, 0.98f, 1f);
            valueText.raycastTarget = false;
            valueText.fontStyle = FontStyle.Bold;
            valueText.resizeTextForBestFit = true;
            valueText.resizeTextMinSize = 14;
            valueText.resizeTextMaxSize = 26;
            SetPosition(valueText.rectTransform, new Vector2(18f, -22f), new Vector2(0f, 1f), new Vector2(width - 28f, height - 26f), new Vector2(0f, 1f));
            return valueText;
        }

        /// <summary>A thin scroll bar down the right edge of a scroll view; it hides itself when everything fits.</summary>
        private static void AttachDetailsScrollbar(ScrollRect scroll)
        {
            const float width = 4f;
            const float inset = 2f;
            Color accent = new Color(0.18f, 0.82f, 0.45f, 1f);

            var barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(scroll.transform, false);
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(width, -inset * 2f);
            barRect.anchoredPosition = new Vector2(-inset, 0f);
            barObject.GetComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.12f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(barObject.transform, false);
            handleObject.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var handle = handleObject.GetComponent<Image>();
            handle.color = new Color(accent.r, accent.g, accent.b, 0.6f);

            var bar = barObject.GetComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handleObject.GetComponent<RectTransform>();
            bar.targetGraphic = handle;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        // ── Active stat effects: dark square badges down the left of the details panel; hover one to widen it into its details ──
        private const float EffectsColumnWidth = 280f;
        private const float EffectBadgeSize = 64f;
        private const float EffectBadgeGap = 12f;
        private const float EffectAccentWidth = 4f;
        private const int MaxEffectCards = 7;
        private const float EffectExpandSpeed = 9f;
        private static readonly Color EffectPositiveColor = new Color(0.40f, 0.90f, 0.50f, 1f);
        private static readonly Color EffectNegativeColor = new Color(0.95f, 0.42f, 0.36f, 1f);
        private static readonly Color EffectFillColor = new Color(0.035f, 0.045f, 0.065f, 0.98f);

        private void BuildOperatorEffectsColumn(Transform panel)
        {
            var rootObject = new GameObject("StatEffectsColumn", typeof(RectTransform));
            rootObject.transform.SetParent(panel, false);
            selectedOperatorEffectsRoot = rootObject.GetComponent<RectTransform>();
            selectedOperatorEffectsRoot.anchorMin = new Vector2(0f, 1f);
            selectedOperatorEffectsRoot.anchorMax = new Vector2(0f, 1f);
            selectedOperatorEffectsRoot.pivot = new Vector2(1f, 1f);
            selectedOperatorEffectsRoot.sizeDelta = new Vector2(EffectsColumnWidth, 640f);
            selectedOperatorEffectsRoot.anchoredPosition = new Vector2(-14f, 0f); // just outside the panel's left edge

            selectedOperatorEffectsHeader = CreateText(selectedOperatorEffectsRoot, "EffectsHeader", "EFFECTS", 10, TextAnchor.MiddleRight);
            selectedOperatorEffectsHeader.color = new Color(0.6f, 0.65f, 0.7f, 0.9f);
            selectedOperatorEffectsHeader.raycastTarget = false;
            SetPosition(selectedOperatorEffectsHeader.rectTransform, new Vector2(0f, -4f), new Vector2(1f, 1f), new Vector2(EffectBadgeSize + 24f, 16f), new Vector2(1f, 1f));

            for (int i = 0; i < MaxEffectCards; i++)
            {
                var view = new EffectCardView();

                // Dark tile; everything inside is clipped to it, so nothing can spill out while it widens
                view.root = new GameObject($"EffectBadge_{i}", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                view.root.transform.SetParent(selectedOperatorEffectsRoot, false);
                view.rect = view.root.GetComponent<RectTransform>();
                // Anchored to the column's right edge, so widening grows the badge to the left, away from the panel
                SetPosition(view.rect, new Vector2(0f, -(28f + i * (EffectBadgeSize + EffectBadgeGap))),
                    new Vector2(1f, 1f), new Vector2(EffectBadgeSize, EffectBadgeSize), new Vector2(1f, 1f));
                Image fill = view.root.GetComponent<Image>();
                fill.color = EffectFillColor;
                fill.raycastTarget = true; // hover target

                // Buff / debuff colour bar down the left edge, like the stat tiles
                var accentObject = new GameObject("Accent", typeof(RectTransform), typeof(Image));
                accentObject.transform.SetParent(view.root.transform, false);
                RectTransform accentRect = accentObject.GetComponent<RectTransform>();
                accentRect.anchorMin = new Vector2(0f, 0f);
                accentRect.anchorMax = new Vector2(0f, 1f);
                accentRect.pivot = new Vector2(0f, 0.5f);
                accentRect.sizeDelta = new Vector2(EffectAccentWidth, 0f);
                accentRect.anchoredPosition = Vector2.zero;
                view.accent = accentObject.GetComponent<Image>();
                view.accent.raycastTarget = false;

                // The icon stays in the square at the right-hand end (the whole badge while it is collapsed)
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(view.root.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(1f, 0.5f);
                iconRect.anchorMax = new Vector2(1f, 0.5f);
                iconRect.pivot = new Vector2(1f, 0.5f);
                iconRect.sizeDelta = new Vector2(32f, 32f); // the 16 px art at exactly 2x
                iconRect.anchoredPosition = new Vector2(-(EffectBadgeSize - 32f) * 0.5f, 0f); // centred in the right-hand square
                view.icon = iconObject.GetComponent<Image>();
                view.icon.preserveAspect = true;
                view.icon.raycastTarget = false;

                // Source and effect fill the space left of the icon; they follow the badge's width and fade in as it widens
                var detailsObject = new GameObject("Details", typeof(RectTransform), typeof(CanvasGroup));
                detailsObject.transform.SetParent(view.root.transform, false);
                RectTransform detailsRect = detailsObject.GetComponent<RectTransform>();
                detailsRect.anchorMin = Vector2.zero;
                detailsRect.anchorMax = Vector2.one;
                detailsRect.offsetMin = new Vector2(EffectAccentWidth + 16f, 0f);
                detailsRect.offsetMax = new Vector2(-EffectBadgeSize, 0f);
                view.details = detailsObject.GetComponent<CanvasGroup>();
                view.details.alpha = 0f;
                view.details.blocksRaycasts = false;

                view.source = CreateText(detailsObject.transform, "Source", string.Empty, 11, TextAnchor.LowerLeft);
                view.source.raycastTarget = false;
                view.source.resizeTextForBestFit = true;
                view.source.resizeTextMinSize = 8;
                view.source.resizeTextMaxSize = 11;
                view.source.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.source.verticalOverflow = VerticalWrapMode.Truncate;
                StretchRow(view.source.rectTransform, 0.52f, 0.92f, 6f);

                view.description = CreateText(detailsObject.transform, "Description", string.Empty, 13, TextAnchor.UpperLeft);
                view.description.fontStyle = FontStyle.Normal;
                view.description.color = new Color(0.9f, 0.93f, 0.97f, 1f);
                view.description.raycastTarget = false;
                view.description.resizeTextForBestFit = true;
                view.description.resizeTextMinSize = 8;
                view.description.resizeTextMaxSize = 13;
                view.description.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.description.verticalOverflow = VerticalWrapMode.Truncate;
                StretchRow(view.description.rectTransform, 0.08f, 0.48f, 6f);

                var hover = view.root.AddComponent<EffectBadgeHover>();
                hover.Changed = hovered =>
                {
                    view.hovered = hovered;
                    if (hovered) view.root.transform.SetAsLastSibling(); // a widened badge draws over the others
                };

                view.root.SetActive(false);
                effectCards.Add(view);
            }
        }

        /// <summary>Fills a horizontal band of its parent (anchors as fractions of the height), with a little right padding.</summary>
        private static void StretchRow(RectTransform rect, float yMin, float yMax, float padding)
        {
            rect.anchorMin = new Vector2(0f, yMin);
            rect.anchorMax = new Vector2(1f, yMax);
            rect.offsetMin = new Vector2(0f, 0f);
            rect.offsetMax = new Vector2(-padding, 0f);
        }

        /// <summary>
        /// The pixel icon for an effect, by what it touches: heart (HP), sword (ATK), shield (DEF; tinted violet for
        /// RES), lightning (attack speed), squad (block), and an arrow (rank-ups and other mixed effects) or a warning
        /// (mixed debuffs).
        /// </summary>
        private static Sprite GetEffectIcon(StatEffect effect, out Color tint)
        {
            tint = Color.white;
            string stats = effect.Stats ?? string.Empty;
            switch (stats)
            {
                case "HP": return LoadIconSprite("icon_heart");
                case "ATK": return LoadIconSprite("icon_damage");
                case "DEF": return LoadIconSprite("icon_armor");
                case "RES":
                    tint = new Color(0.82f, 0.62f, 1f, 1f);
                    return LoadIconSprite("icon_armor");
                case "ATK SPD": return LoadIconSprite("icon_speed");
                case "BLOCK": return LoadIconSprite("icon_squad");
            }

            if (stats.IndexOf(' ') >= 0) return LoadIconSprite(effect.Positive ? "icon_upgrade" : "icon_warning");
            return LoadIconSprite("icon_info");
        }

        private void ShowOperatorEffects(List<StatEffect> effects)
        {
            if (selectedOperatorEffectsRoot == null) return;

            // Nothing is affecting the operator: no column at all
            selectedOperatorEffectsRoot.gameObject.SetActive(effects.Count > 0);

            int shown = Mathf.Min(effects.Count, MaxEffectCards);
            for (int i = 0; i < effectCards.Count; i++)
            {
                EffectCardView view = effectCards[i];
                bool active = i < shown;
                if (view.root.activeSelf != active)
                {
                    view.root.SetActive(active);
                    view.expand = 0f;
                    view.hovered = false;
                    ApplyEffectBadgeWidth(view);
                }
                if (!active) continue;

                StatEffect effect = effects[i];
                Color accent = effect.Positive ? EffectPositiveColor : EffectNegativeColor;
                view.accent.color = accent;
                Sprite icon = GetEffectIcon(effect, out Color iconTint);
                view.icon.sprite = icon;
                view.icon.color = iconTint;
                view.icon.enabled = icon != null;
                view.source.color = accent;
                view.source.text = effect.Source;
                view.description.text = effect.Description;
            }

            selectedOperatorEffectsHeader.text = effects.Count > MaxEffectCards ? $"EFFECTS +{effects.Count - MaxEffectCards}" : "EFFECTS";
        }

        private void ApplyEffectBadgeWidth(EffectCardView view)
        {
            float eased = view.expand * view.expand * (3f - 2f * view.expand); // smoothstep
            view.rect.sizeDelta = new Vector2(Mathf.Lerp(EffectBadgeSize, EffectsColumnWidth, eased), EffectBadgeSize);
            view.details.alpha = Mathf.Clamp01((eased - 0.5f) / 0.5f);
        }

        /// <summary>Eases every badge towards square (not hovered) or widened (hovered). Unscaled time, so it works while paused.</summary>
        private void AnimateEffectBadges()
        {
            if (selectedOperatorEffectsRoot == null || !selectedOperatorEffectsRoot.gameObject.activeInHierarchy) return;

            float step = Time.unscaledDeltaTime * EffectExpandSpeed;
            for (int i = 0; i < effectCards.Count; i++)
            {
                EffectCardView view = effectCards[i];
                if (!view.root.activeSelf) continue;

                float target = view.hovered ? 1f : 0f;
                if (Mathf.Approximately(view.expand, target)) continue;
                view.expand = Mathf.MoveTowards(view.expand, target, step);
                ApplyEffectBadgeWidth(view);
            }
        }
        private static void CreateOperatorDetailsDivider(Transform parent, float margin, float width, float top)
        {
            var divider = new GameObject("DetailsDivider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(parent, false);
            SetPosition(divider.GetComponent<RectTransform>(), new Vector2(margin, -top), new Vector2(0f, 1f), new Vector2(width, 2f), new Vector2(0f, 1f));
            Image image = divider.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.08f);
            image.raycastTarget = false;
        }
    }
}
