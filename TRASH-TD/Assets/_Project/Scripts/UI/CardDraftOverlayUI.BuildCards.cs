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
    // Building the developer picker and the cards (faces, backs and generated textures).
    public partial class CardDraftOverlayUI
    {
#if UNITY_EDITOR || DEBUG
        private void BuildDeveloperPicker()
        {
            developerPickerRoot = new GameObject(
                "DeveloperOperatorPicker",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup));
            developerPickerRoot.transform.SetParent(overlayRoot.transform, false);
            var rootRect = developerPickerRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            developerPickerRoot.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.025f, 0.92f);

            var panel = new GameObject("PickerPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(developerPickerRoot.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(760f, 820f);
            GameplayHUDUI.ApplyPanelContainer(panel, GameplayHUDUI.PanelContainerType.Metal);

            var panelBorder = panel.AddComponent<Outline>();
            panelBorder.effectColor = new Color(0.82f, 0.65f, 1f, 0.35f);
            panelBorder.effectDistance = new Vector2(2f, -2f);

            var title = MakeText(panel.transform, "Title", "DEVELOPER PICKER", 26,
                TextAnchor.MiddleCenter, new Color(0.82f, 0.65f, 1f, 1f));
            title.fontStyle = FontStyle.Bold;
            var titleRect = title.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -20f);
            titleRect.sizeDelta = new Vector2(480f, 38f);

            var closeButton = MakeButton(panel.transform, "CloseButton", "CLOSE",
                new Vector2(110f, 40f), GameplayHUDUI.ButtonContainerType.Danger, "icon_close");
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-20f, -18f);
            closeRect.sizeDelta = new Vector2(110f, 40f);
            closeButton.onClick.AddListener(() => developerPickerRoot.SetActive(false));
            AttachFeedback(closeButton, Color.white, 1.04f);

            var hint = MakeText(panel.transform, "Hint", "Choose an operator from this stage's draft pool.",
                14, TextAnchor.MiddleCenter, STAT_LABEL_COLOR);
            var hintRect = hint.GetComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 1f);
            hintRect.anchoredPosition = new Vector2(0f, -60f);
            hintRect.sizeDelta = new Vector2(680f, 26f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(panel.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = new Vector2(0f, 0f);
            viewportRect.anchorMax = new Vector2(1f, 1f);
            viewportRect.offsetMin = new Vector2(20f, 20f);
            viewportRect.offsetMax = new Vector2(-20f, -96f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            viewport.GetComponent<Mask>().showMaskGraphic = true;

            var scrollRect = panel.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 35f;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            developerPickerList = content.transform;
            scrollRect.content = contentRect;
            developerPickerRoot.SetActive(false);
        }

        private void ShowDeveloperPicker()
        {
            if (draftSystem == null || developerPickerRoot == null) return;

            if (developerPickerPopulated)
            {
                developerPickerRoot.SetActive(true);
                return;
            }

            IReadOnlyList<OperatorData> operators = draftSystem.AvailableOperatorPool;
            int addedCount = 0;
            if (operators != null)
            {
                var sortedOperators = operators
                    .Where(op => op != null)
                    .OrderBy(op => op.operatorClass)
                    .ThenBy(op => (int)op.baseRarity)
                    .ThenBy(op => op.operatorName)
                    .ToList();

                for (int i = 0; i < sortedOperators.Count; i++)
                {
                    OperatorData opData = sortedOperators[i];
                    int stars = Mathf.Clamp((int)opData.baseRarity, 1, 5);

                    Button option = MakeButton(
                        developerPickerList,
                        $"Operator_{addedCount}",
                        $"{opData.operatorName}  |  {opData.operatorClass}  |  {new string('★', stars)}",
                        new Vector2(680f, 64f),
                        CARD_BG);
                    option.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
                    option.onClick.AddListener(() => SelectDeveloperOperator(opData));
                    AttachFeedback(option, GetRarityColor(stars), 1.02f);
                    addedCount++;
                }
            }

            if (addedCount == 0)
            {
                Debug.LogError("CardDraftOverlayUI: Developer picker has no operators in the active draft pool.");
                var emptyText = MakeText(developerPickerList, "EmptyPool",
                    "No operators are available in the active draft pool.", 16, TextAnchor.MiddleCenter, Color.white);
                emptyText.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            }

            developerPickerPopulated = true;
            developerPickerRoot.SetActive(true);
        }

#if UNITY_EDITOR || DEBUG
        public void OpenDeveloperOperatorPicker()
        {
            ShowDeveloperPicker();
        }
#endif

        private void SelectDeveloperOperator(OperatorData operatorData)
        {
            if (draftSystem == null || !draftSystem.SetDeveloperOffer(operatorData))
            {
                Debug.LogError("CardDraftOverlayUI: Could not set the requested developer draft offer.");
                return;
            }

            developerPickerRoot.SetActive(false);
            selectedCardIndex = -1;
            UpdateConfirmButton();
        }
#endif

        private void BuildCard(Transform parent, int index)
        {
            // Card root. Anchored to the container's center; its position is driven by
            // the shuffle/deal animation (cardSlots) rather than a layout group.
            var cardObj = new GameObject($"Card_{index}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            cardObj.transform.SetParent(parent, false);
            var cardRect = cardObj.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardRect.anchoredPosition = Vector2.zero;
            cardRects[index] = cardRect;

            var cardImg = cardObj.GetComponent<Image>();
            cardImg.color = CARD_BG;

            var cardBtn = cardObj.GetComponent<Button>();
            int capturedIndex = index;
            cardBtn.onClick.AddListener(() => OnCardClicked(capturedIndex));
            cardButtonsArr[index] = cardBtn;

            // Hover / press / selection visuals come from MenuButtonFeedback (replaces the color tint).
            cardFeedbacks[index] = AttachFeedback(cardBtn, ACCENT_COLOR, 1.03f);
            cardGroups[index] = cardObj.GetComponent<CanvasGroup>();

            cardPanels[index] = cardObj;
            cardBackgrounds[index] = cardImg;
            cardRarityParts[index] = new List<TintedPart>();

            // The front of the card lives under one container so the whole face can be shown/hidden when the
            // card flips. It is masked so the shine sweep stays on the card.
            var front = new GameObject($"Front_{index}", typeof(RectTransform), typeof(RectMask2D));
            front.transform.SetParent(cardObj.transform, false);
            StretchFill(front.GetComponent<RectTransform>());
            cardFronts[index] = front;

            // --- Rarity frame: outer line, faint inner line and corner notches (recoloured per card) ---
            AddRarityParts(index, CreateFrame(front.transform, 6f, 3f, Color.white), 0.9f);
            AddRarityParts(index, CreateFrame(front.transform, 14f, 1f, Color.white), 0.3f);
            foreach (Image notch in CreateCornerNotches(front.transform, 6f, 12f, Color.white))
            {
                AddRarityPart(index, notch, 1f);
            }

            // --- Header: class icon and name on the left, position on the right ---
            var classBadge = new GameObject($"ClassBadge_{index}", typeof(RectTransform), typeof(Image));
            classBadge.transform.SetParent(front.transform, false);
            PlaceTopLeft(classBadge.transform, 22f, 20f, 34f, 34f);
            classBadgeBackgrounds[index] = classBadge.GetComponent<Image>();
            classBadgeBackgrounds[index].raycastTarget = false;
            classBadgeBackgrounds[index].color = Color.clear;

            var classIconObject = new GameObject($"ClassIcon_{index}", typeof(RectTransform), typeof(Image));
            classIconObject.transform.SetParent(classBadge.transform, false);
            classBadgeIcons[index] = classIconObject.GetComponent<Image>();
            classBadgeIcons[index].preserveAspect = true;
            classBadgeIcons[index].raycastTarget = false;
            classBadgeIcons[index].color = Color.white;
            StretchFill(classBadgeIcons[index].rectTransform);

            classNameTexts[index] = MakeText(front.transform, $"ClassName_{index}", "", 16, TextAnchor.MiddleLeft, CLASS_COLOR);
            classNameTexts[index].fontStyle = FontStyle.Bold;
            StretchTopRT(classNameTexts[index], 64f, 120f, 22f, 30f);

            positionTexts[index] = MakeText(front.transform, $"Position_{index}", "", 14, TextAnchor.MiddleRight, RANGED_COLOR);
            positionTexts[index].fontStyle = FontStyle.Bold;
            StretchTopRT(positionTexts[index], 180f, 24f, 22f, 30f);

            // --- Portrait window: rarity-tinted backdrop, faint grid, a glow behind the operator and a floor ---
            var portraitBg = new GameObject($"PortraitBg_{index}", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            portraitBg.transform.SetParent(front.transform, false);
            StretchTopRT(portraitBg.transform, PortraitMargin, PortraitMargin, PortraitTop, PortraitHeight);
            portraitBgImages[index] = portraitBg.GetComponent<Image>();
            portraitBgImages[index].color = PORTRAIT_BG;
            portraitBgImages[index].raycastTarget = false;

            // Rarity-colored frame around the portrait
            portraitFrames[index] = portraitBg.AddComponent<Outline>();
            portraitFrames[index].effectDistance = new Vector2(2f, -2f);
            portraitFrames[index].effectColor = new Color(1f, 1f, 1f, 0.15f);

            Image grid = CreatePlainImage(portraitBg.transform, "Grid", new Color(1f, 1f, 1f, 0.05f));
            grid.sprite = GetGridSprite();
            grid.type = Image.Type.Tiled;
            StretchFill(grid.rectTransform);

            portraitGlows[index] = CreatePlainImage(portraitBg.transform, "Glow", Color.clear);
            portraitGlows[index].sprite = GetGlowSprite();
            PositionRT(portraitGlows[index], new Vector2(0f, -30f), new Vector2(0.5f, 0.5f), new Vector2(340f, 320f));

            Image floor = CreatePlainImage(portraitBg.transform, "Floor", new Color(0f, 0f, 0f, 0.35f));
            RectTransform floorRect = floor.rectTransform;
            floorRect.anchorMin = new Vector2(0f, 0f);
            floorRect.anchorMax = new Vector2(1f, 0f);
            floorRect.pivot = new Vector2(0.5f, 0f);
            floorRect.offsetMin = Vector2.zero;
            floorRect.offsetMax = new Vector2(0f, 64f);

            Image floorLine = CreatePlainImage(portraitBg.transform, "FloorLine", Color.white);
            RectTransform floorLineRect = floorLine.rectTransform;
            floorLineRect.anchorMin = new Vector2(0f, 0f);
            floorLineRect.anchorMax = new Vector2(1f, 0f);
            floorLineRect.pivot = new Vector2(0.5f, 0f);
            floorLineRect.offsetMin = new Vector2(0f, 64f);
            floorLineRect.offsetMax = new Vector2(0f, 66f);
            AddRarityPart(index, floorLine, 0.45f);

            // Placeholder initial (shown when the operator has no portrait art)
            portraitFallbackTexts[index] = MakeText(portraitBg.transform, $"PortraitFallback_{index}", "?", 140,
                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.10f));
            portraitFallbackTexts[index].fontStyle = FontStyle.Bold;
            StretchFill(portraitFallbackTexts[index].rectTransform);

            // Portrait sprite: centered in the frame, size set per sprite in SetPortrait()
            var portraitObj = new GameObject($"Portrait_{index}", typeof(RectTransform), typeof(Image));
            portraitObj.transform.SetParent(portraitBg.transform, false);
            var portraitRect = portraitObj.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0.5f, 0.5f);
            portraitRect.anchorMax = new Vector2(0.5f, 0.5f);
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = PortraitFitArea;
            portraitImages[index] = portraitObj.GetComponent<Image>();
            portraitImages[index].preserveAspect = false;
            portraitImages[index].color = Color.white;
            portraitImages[index].raycastTarget = false;

            // --- Name plate: a tinted band with a rarity tick on the left ---
            namePlates[index] = CreatePlainImage(front.transform, $"NamePlate_{index}", CARD_BG);
            StretchTopRT(namePlates[index], PortraitMargin, PortraitMargin, 452f, 52f);

            var accent = CreatePlainImage(namePlates[index].transform, $"Accent_{index}", STAR_COLOR);
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.offsetMin = Vector2.zero;
            accentRect.offsetMax = new Vector2(6f, 0f);
            cardAccents[index] = accent;

            nameTexts[index] = MakeText(namePlates[index].transform, $"Name_{index}", "", 26,
                TextAnchor.MiddleCenter, Color.white);
            nameTexts[index].fontStyle = FontStyle.Bold;
            nameTexts[index].resizeTextForBestFit = true;
            nameTexts[index].resizeTextMinSize = 14;
            nameTexts[index].resizeTextMaxSize = 26;
            RectTransform nameRect = nameTexts[index].rectTransform;
            nameRect.anchorMin = Vector2.zero;
            nameRect.anchorMax = Vector2.one;
            nameRect.offsetMin = new Vector2(16f, 0f);
            nameRect.offsetMax = new Vector2(-10f, 0f);

            // --- Rarity stars ---
            rarityTexts[index] = MakeText(front.transform, $"Rarity_{index}", "", 30,
                TextAnchor.MiddleCenter, STAR_COLOR);
            StretchTopRT(rarityTexts[index], 14f, 14f, 510f, 42f);

            Image divider = CreatePlainImage(front.transform, "Divider", new Color(1f, 1f, 1f, 0.08f));
            StretchTopRT(divider, 40f, 40f, 560f, 1f);

            // --- Select hint / selected badge, and the hotkey ---
            selectHintTexts[index] = MakeText(front.transform, $"SelectHint_{index}", "CLICK TO SELECT", 13,
                TextAnchor.MiddleCenter, STAT_LABEL_COLOR);
            selectHintTexts[index].fontStyle = FontStyle.Bold;
            StretchTopRT(selectHintTexts[index], 14f, 14f, 570f, 24f);

            var keyText = MakeText(front.transform, $"Key_{index}", $"KEY [ {index + 1} ]", 11, TextAnchor.MiddleCenter,
                new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.45f));
            StretchTopRT(keyText, 14f, 14f, 598f, 18f);

            // --- Shine sweep: a slanted light band that crosses the face (on reveal, and now and then on 3★+) ---
            Image shine = CreatePlainImage(front.transform, $"Shine_{index}", new Color(1f, 1f, 1f, 0.16f));
            PositionRT(shine, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(70f, CardHeight * 1.5f));
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            shineRects[index] = shine.rectTransform;
            shine.gameObject.SetActive(false);

            // --- The back of the card (shown until the player clicks it) ---
            BuildCardBack(cardObj.transform, index);

            // Brief color flash that plays when the card turns over
            var flash = new GameObject($"Flash_{index}", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(cardObj.transform, false);
            StretchFill(flash.GetComponent<RectTransform>());
            cardFlashes[index] = flash.GetComponent<Image>();
            cardFlashes[index].color = new Color(1f, 1f, 1f, 0f);
            cardFlashes[index].raycastTarget = false;

            // Cards start face-down
            cardFronts[index].SetActive(false);
        }

        /// <summary>
        /// Card back, a sealed reclaimer file: dark plate with a faint grid, hazard-striped bands top and bottom,
        /// double frame with corner notches, a "TR" diamond emblem that pulses, a scan line and a blinking hint.
        /// It looks the same for every card, so it never gives away the rarity.
        /// </summary>
        private void BuildCardBack(Transform card, int index)
        {
            var back = new GameObject($"Back_{index}", typeof(RectTransform), typeof(Image));
            back.transform.SetParent(card, false);
            StretchFill(back.GetComponent<RectTransform>());
            var backImage = back.GetComponent<Image>();
            backImage.color = BACK_BG;
            backImage.raycastTarget = false;
            cardBacks[index] = back;

            Color frameColor = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.55f);
            Color innerFrameColor = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.22f);

            Image grid = CreatePlainImage(back.transform, "Grid", new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.08f));
            grid.sprite = GetGridSprite();
            grid.type = Image.Type.Tiled;
            StretchFill(grid.rectTransform);

            // Hazard bands
            for (int band = 0; band < 2; band++)
            {
                Image stripes = CreatePlainImage(back.transform, band == 0 ? "HazardTop" : "HazardBottom", new Color(1f, 1f, 1f, 0.5f));
                stripes.sprite = GetStripeSprite();
                stripes.type = Image.Type.Tiled;
                StretchTopRT(stripes, 16f, 16f, band == 0 ? 16f : CardHeight - 44f, 28f);
            }

            CreateFrame(back.transform, 6f, 3f, frameColor);
            CreateFrame(back.transform, 14f, 1f, innerFrameColor);
            CreateCornerNotches(back.transform, 6f, 12f, frameColor);

            // Diamond emblem (its fill pulses, see AnimateCards) with a smaller outline inside
            var diamond = CreatePlainImage(back.transform, "Emblem", new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.10f));
            PositionRT(diamond, new Vector2(0f, 14f), new Vector2(0.5f, 0.5f), new Vector2(196f, 196f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            CreateFrame(diamond.transform, 0f, 3f, frameColor);
            CreateFrame(diamond.transform, 22f, 1f, innerFrameColor);
            backEmblems[index] = diamond;

            var emblemText = MakeText(back.transform, "BackEmblem", "TR", 84, TextAnchor.MiddleCenter,
                new Color(ACCENT_BRIGHT.r, ACCENT_BRIGHT.g, ACCENT_BRIGHT.b, 0.9f));
            emblemText.fontStyle = FontStyle.Bold;
            PositionRT(emblemText, new Vector2(0f, 14f), new Vector2(0.5f, 0.5f), new Vector2(260f, 120f));

            var fileText = MakeText(back.transform, "BackFile", "RECLAIMER FILE", 15, TextAnchor.MiddleCenter,
                new Color(ACCENT_BRIGHT.r, ACCENT_BRIGHT.g, ACCENT_BRIGHT.b, 0.75f));
            fileText.fontStyle = FontStyle.Bold;
            PositionRT(fileText, new Vector2(0f, -136f), new Vector2(0.5f, 0.5f), new Vector2(300f, 24f));

            var classifiedText = MakeText(back.transform, "BackClassified", "// CLASSIFIED //", 12, TextAnchor.MiddleCenter,
                new Color(HAZARD_YELLOW.r, HAZARD_YELLOW.g, HAZARD_YELLOW.b, 0.7f));
            PositionRT(classifiedText, new Vector2(0f, -162f), new Vector2(0.5f, 0.5f), new Vector2(300f, 20f));

            // Hotkey label under the top band and the reveal hint above the bottom one
            var keyText = MakeText(back.transform, "BackKey", $"[ {index + 1} ]", 14, TextAnchor.MiddleCenter,
                new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.6f));
            keyText.fontStyle = FontStyle.Bold;
            StretchTopRT(keyText, 14f, 14f, 54f, 24f);

            backHints[index] = MakeText(back.transform, "BackHint", "CLICK TO REVEAL", 14, TextAnchor.MiddleCenter,
                new Color(ACCENT_BRIGHT.r, ACCENT_BRIGHT.g, ACCENT_BRIGHT.b, 0.8f));
            backHints[index].fontStyle = FontStyle.Bold;
            StretchTopRT(backHints[index], 14f, 14f, CardHeight - 82f, 26f);

            // Scan line sweeping down the back, inside the frame (AnimateCards)
            Image scan = CreatePlainImage(back.transform, "ScanLine", new Color(ACCENT_BRIGHT.r, ACCENT_BRIGHT.g, ACCENT_BRIGHT.b, 0.2f));
            RectTransform scanRect = scan.rectTransform;
            scanRect.anchorMin = new Vector2(0f, 1f);
            scanRect.anchorMax = new Vector2(1f, 1f);
            scanRect.pivot = new Vector2(0.5f, 0.5f);
            scanRect.sizeDelta = new Vector2(0f, 3f);
            backScanLines[index] = scanRect;
        }

        private void AddRarityPart(int index, Image image, float alpha)
        {
            cardRarityParts[index].Add(new TintedPart { image = image, alpha = alpha });
        }

        private void AddRarityParts(int index, Image[] images, float alpha)
        {
            foreach (Image image in images) AddRarityPart(index, image, alpha);
        }

        /// <summary>Recolours a card's frame, notches and floor line in its rarity colour, keeping each part's alpha.</summary>
        private void TintRarityParts(int index, Color color)
        {
            foreach (TintedPart part in cardRarityParts[index])
            {
                part.image.color = new Color(color.r, color.g, color.b, part.alpha);
            }
        }

        private static Image CreatePlainImage(Transform parent, string name, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Small squares in the four corners, <paramref name="inset"/> from the edges.</summary>
        private static Image[] CreateCornerNotches(Transform parent, float inset, float size, Color color)
        {
            var notches = new Image[4];
            for (int c = 0; c < 4; c++)
            {
                var corner = new Vector2(c & 1, (c & 2) >> 1);
                Image notch = CreatePlainImage(parent, $"Notch_{c}", color);
                RectTransform rect = notch.rectTransform;
                rect.anchorMin = corner;
                rect.anchorMax = corner;
                rect.pivot = corner;
                rect.sizeDelta = new Vector2(size, size);
                rect.anchoredPosition = new Vector2(corner.x > 0.5f ? -inset : inset, corner.y > 0.5f ? -inset : inset);
                notches[c] = notch;
            }
            return notches;
        }

        // ============================
        // Generated card textures (owned by this overlay and destroyed with it)
        // ============================

        private Sprite GetGlowSprite()
        {
            if (glowSprite == null)
            {
                glowSprite = CreateGeneratedSprite("Card Glow", 64, 64, FilterMode.Bilinear, (x, y) =>
                {
                    float dx = (x + 0.5f) / 32f - 1f;
                    float dy = (y + 0.5f) / 32f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    return new Color(1f, 1f, 1f, a * a);
                });
            }
            return glowSprite;
        }

        private Sprite GetStripeSprite()
        {
            if (stripeSprite == null)
            {
                stripeSprite = CreateGeneratedSprite("Card Hazard Stripes", 16, 16, FilterMode.Point,
                    (x, y) => (x + y) % 16 < 8 ? HAZARD_YELLOW : HAZARD_INK);
            }
            return stripeSprite;
        }

        private Sprite GetGridSprite()
        {
            if (gridSprite == null)
            {
                gridSprite = CreateGeneratedSprite("Card Grid", 16, 16, FilterMode.Point,
                    (x, y) => x == 0 || y == 0 ? Color.white : Color.clear);
            }
            return gridSprite;
        }

        /// <summary>100 pixels per unit, so a Tiled Image repeats it at its pixel size on the 1080p canvas.</summary>
        private static Sprite CreateGeneratedSprite(string name, int width, int height, FilterMode filter, Func<int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = filter,
                wrapMode = TextureWrapMode.Repeat
            };

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) pixels[y * width + x] = pixel(x, y);
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        private static void DestroyGeneratedSprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (sprite.texture != null) Destroy(sprite.texture);
            Destroy(sprite);
        }

        /// <summary>Hollow rectangle made of four thin strips, inset from the edges of its parent.</summary>
        private static Image[] CreateFrame(Transform parent, float inset, float thickness, Color color)
        {
            var strips = new Image[4];
            for (int side = 0; side < 4; side++)
            {
                var strip = new GameObject($"Frame_{side}", typeof(RectTransform), typeof(Image));
                strip.transform.SetParent(parent, false);
                var rect = strip.GetComponent<RectTransform>();

                switch (side)
                {
                    case 0: // top
                        rect.anchorMin = new Vector2(0f, 1f);
                        rect.anchorMax = new Vector2(1f, 1f);
                        rect.offsetMin = new Vector2(inset, -(inset + thickness));
                        rect.offsetMax = new Vector2(-inset, -inset);
                        break;
                    case 1: // bottom
                        rect.anchorMin = new Vector2(0f, 0f);
                        rect.anchorMax = new Vector2(1f, 0f);
                        rect.offsetMin = new Vector2(inset, inset);
                        rect.offsetMax = new Vector2(-inset, inset + thickness);
                        break;
                    case 2: // left
                        rect.anchorMin = new Vector2(0f, 0f);
                        rect.anchorMax = new Vector2(0f, 1f);
                        rect.offsetMin = new Vector2(inset, inset);
                        rect.offsetMax = new Vector2(inset + thickness, -inset);
                        break;
                    default: // right
                        rect.anchorMin = new Vector2(1f, 0f);
                        rect.anchorMax = new Vector2(1f, 1f);
                        rect.offsetMin = new Vector2(-(inset + thickness), inset);
                        rect.offsetMax = new Vector2(-inset, -inset);
                        break;
                }

                var image = strip.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                strips[side] = image;
            }
            return strips;
        }
    }
}
