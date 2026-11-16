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
    // Building the overlay: draft odds, toggles, the reroll notice and the minimize controls.
    public partial class CardDraftOverlayUI
    {
        // ============================
        // UI Construction
        // ============================

        private void BuildOverlay()
        {
            // Canvas
            var canvasObj = new GameObject("CardDraftOverlayCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // Above gameplay HUD

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Root overlay panel (fullscreen dark background)
            overlayRoot = new GameObject("OverlayRoot", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            overlayRoot.transform.SetParent(canvasObj.transform, false);
            var rootRect = overlayRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            overlayRoot.GetComponent<Image>().color = BG_OVERLAY;
            overlayGroup = overlayRoot.GetComponent<CanvasGroup>();

            // Top accent stripe
            var stripe = new GameObject("AccentStripe", typeof(RectTransform), typeof(Image));
            stripe.transform.SetParent(overlayRoot.transform, false);
            var stripeRect = stripe.GetComponent<RectTransform>();
            stripeRect.anchorMin = new Vector2(0f, 1f);
            stripeRect.anchorMax = new Vector2(1f, 1f);
            stripeRect.pivot = new Vector2(0.5f, 1f);
            stripeRect.sizeDelta = new Vector2(0f, 4f);
            var stripeImage = stripe.GetComponent<Image>();
            stripeImage.color = ACCENT_COLOR;
            stripeImage.raycastTarget = false;

            // Title (centered above the card column)
            titleText = MakeText(overlayRoot.transform, "Title", "CHOOSE YOUR CARD", 36,
                TextAnchor.MiddleCenter, Color.white);
            titleText.fontStyle = FontStyle.Bold;
            PositionRT(titleText, new Vector2(ColumnCenterX, TitleCenterY), new Vector2(0.5f, 1f), new Vector2(700f, 60f));
            var titleShadow = titleText.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.35f);
            titleShadow.effectDistance = new Vector2(0f, -4f);

            // Wave counter under the title: plain text, "WAVE 1/20"
            waveInfoText = MakeText(overlayRoot.transform, "WaveInfo", "WAVE 1/?", 22,
                TextAnchor.MiddleCenter, ACCENT_BRIGHT);
            PositionRT(waveInfoText, new Vector2(ColumnCenterX, WaveCounterCenterY), new Vector2(0.5f, 1f), new Vector2(500f, 28f));

            // Deck count
            deckCountText = MakeText(overlayRoot.transform, "DeckCount", "DECK 0/8", 16,
                TextAnchor.MiddleRight, Color.white);
            PositionRT(deckCountText, new Vector2(-28f, -32f), new Vector2(1f, 1f), new Vector2(300f, 48f));
            deckCountText.rectTransform.pivot = new Vector2(1f, 1f);
            deckCountText.resizeTextForBestFit = true;
            deckCountText.resizeTextMinSize = 12;
            deckCountText.resizeTextMaxSize = 16;

            var squadIconSprite = GameplayHUDUI.LoadIconSprite("icon_squad");
            if (squadIconSprite != null)
            {
                var squadIconObj = new GameObject("DeckHeaderIcon", typeof(RectTransform), typeof(Image));
                squadIconObj.transform.SetParent(overlayRoot.transform, false);
                var squadIconImg = squadIconObj.GetComponent<Image>();
                squadIconImg.sprite = squadIconSprite;
                squadIconImg.color = Color.white;
                squadIconImg.preserveAspect = true;
                squadIconImg.raycastTarget = false;
                PositionRT(squadIconImg, new Vector2(-155f, -32f), new Vector2(1f, 1f), new Vector2(24f, 24f));
                squadIconObj.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
            }

            // "Skip card animation" checkbox, under the deck count
            RectTransform skipToggleRect = BuildSkipAnimationToggle(overlayRoot.transform);
            skipAnimationToggleRect = skipToggleRect;

            // Card container (left side of the screen). Cards are positioned manually
            // (not by a layout group) so the shuffle/deal animation can move them freely.
            var cardContainer = new GameObject("CardContainer", typeof(RectTransform));
            cardContainer.transform.SetParent(overlayRoot.transform, false);
            var containerRect = cardContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(0.5f, 0.5f);
            containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.sizeDelta = new Vector2(CardWidth * 3f + CardSpacing * 2f, CardHeight);
            containerRect.anchoredPosition = new Vector2(ColumnCenterX, ContentCenterY);

            // Build 3 cards
            for (int i = 0; i < 3; i++)
            {
                BuildCard(cardContainer.transform, i);
            }

            // Detail panel (right side of the screen)
            BuildDetailsPanel(overlayRoot.transform);

            // Draft odds, above the detail panel
            BuildDraftOdds(overlayRoot.transform);

            // Reroll / Skip under the cards
            var buttonsContainer = new GameObject("ButtonsContainer", typeof(RectTransform));
            buttonsContainer.transform.SetParent(overlayRoot.transform, false);
            var buttonsRect = buttonsContainer.GetComponent<RectTransform>();
            buttonsRect.anchorMin = new Vector2(0.5f, 0f);
            buttonsRect.anchorMax = new Vector2(0.5f, 0f);
            buttonsRect.sizeDelta = new Vector2(640f, 64f);
            buttonsRect.anchoredPosition = new Vector2(ColumnCenterX, 120f);

            var btnHlg = buttonsContainer.AddComponent<HorizontalLayoutGroup>();
            btnHlg.spacing = 20f;
            btnHlg.childAlignment = TextAnchor.MiddleCenter;
            btnHlg.childControlWidth = false;
            btnHlg.childControlHeight = false;
            btnHlg.childForceExpandWidth = false;
            btnHlg.childForceExpandHeight = false;

            // Reroll button
            rerollButton = MakeButton(buttonsContainer.transform, "RerollButton", "REROLL (3)",
                new Vector2(220f, 60f), GameplayHUDUI.ButtonContainerType.Warning);
            rerollButtonText = rerollButton.GetComponentInChildren<Text>();
            rerollButton.onClick.AddListener(OnRerollClicked);
            rerollFeedback = AttachFeedback(rerollButton, new Color(1f, 0.85f, 0.35f, 1f), 1.05f);

            // Skip button
            skipButton = MakeButton(buttonsContainer.transform, "SkipButton", "SKIP",
                new Vector2(140f, 60f), GameplayHUDUI.ButtonContainerType.Dark, "icon_close");
            skipButton.onClick.AddListener(OnSkipClicked);
            skipFeedback = AttachFeedback(skipButton, Color.white, 1.05f);

#if UNITY_EDITOR || DEBUG
            if (DeveloperPickerEnabled)
            {
                developerPickButton = MakeButton(overlayRoot.transform, "DeveloperPickButton", "D",
                    new Vector2(48f, 48f), GameplayHUDUI.ButtonContainerType.Secondary);
                PositionRT(developerPickButton, new Vector2(28f, -32f), new Vector2(0f, 1f), new Vector2(48f, 48f));
                developerPickButton.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
                developerPickButton.onClick.AddListener(ShowDeveloperPicker);
                AttachFeedback(developerPickButton, new Color(0.75f, 0.55f, 1f, 1f), 1.05f);
                BuildDeveloperPicker();
            }
#endif

            // Confirm button (under the detail panel, where the decision is made)
            confirmButton = MakeButton(overlayRoot.transform, "ConfirmButton", "SELECT A CARD",
                new Vector2(PanelWidth, 64f), GameplayHUDUI.ButtonContainerType.Dark, "icon_check");
            PositionRT(confirmButton, new Vector2(PanelCenterX, 120f), new Vector2(0.5f, 0f), new Vector2(PanelWidth, 64f));
            confirmButtonText = confirmButton.GetComponentInChildren<Text>();
            confirmButtonText.fontSize = 20;
            confirmButton.onClick.AddListener(OnConfirmClicked);
            confirmButton.interactable = false;
            confirmFeedback = AttachFeedback(confirmButton, ACCENT_BRIGHT, 1.03f);

            // View map & deck controls
            BuildMinimizeControls(canvasObj.transform, buttonsContainer.transform);

            // Keyboard hint
            var hintText = MakeText(overlayRoot.transform, "KeyHints", "1 2 3  FLIP / SELECT     ENTER  CONFIRM     R  REROLL     M  MINIMIZE", 12,
                TextAnchor.MiddleCenter, new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.55f));
            PositionRT(hintText, new Vector2(0f, 28f), new Vector2(0.5f, 0f), new Vector2(900f, 24f));

            // Header/footer/panel entrance data (positions captured after layout above)
            introRects = new[]
            {
                titleText.rectTransform,
                waveInfoText.rectTransform,
                skipToggleRect,
                deckCountText.rectTransform,
                detailsPanelRect,
                buttonsRect,
                confirmButton.GetComponent<RectTransform>(),
                hintText.rectTransform,
                oddsRoot,
                oddsToggleRect
            };
            introTargets = new Vector2[introRects.Length];
            for (int i = 0; i < introRects.Length; i++) introTargets[i] = introRects[i].anchoredPosition;
            introOffsets = new[]
            {
                new Vector2(0f, 40f), new Vector2(0f, 40f), new Vector2(0f, 40f), new Vector2(0f, 40f),
                new Vector2(90f, 0f), new Vector2(0f, -60f), new Vector2(0f, -60f), new Vector2(0f, -30f),
                new Vector2(0f, 40f), new Vector2(0f, 40f)
            };
            introDelays = new[] { 0f, 0.06f, 0.1f, 0.1f, 0.15f, 0.25f, 0.3f, 0.35f, 0.12f, 0.1f };
        }

        /// <summary>
        /// "DRAFT ODDS" slot machine above the detail panel: a header row (label, "ODDS UP!" tag, next boost) over a
        /// cabinet with one reel per rarity (star plate on top, the percentage on the payline) and a lever on its
        /// right side. See UpdateDraftOdds.
        /// </summary>
        private void BuildDraftOdds(Transform parent)
        {
            var root = new GameObject("DraftOdds", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            oddsRoot = root.GetComponent<RectTransform>();
            oddsRoot.anchorMin = oddsRoot.anchorMax = new Vector2(0.5f, 1f);
            oddsRoot.pivot = new Vector2(0.5f, 1f);
            oddsRoot.anchoredPosition = new Vector2(PanelCenterX, -OddsTop);
            oddsRoot.sizeDelta = new Vector2(PanelWidth, OddsHeight);

            Text label = MakeText(root.transform, "Label", "DRAFT ODDS", 14, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            label.fontStyle = FontStyle.Bold;
            PlaceTopLeft(label, 0f, 0f, 140f, 20f);

            oddsNextText = MakeText(root.transform, "NextBoost", "", 12, TextAnchor.MiddleRight, ACCENT_BRIGHT);
            StretchTopRT(oddsNextText, 0f, 0f, 0f, 20f);

            // "ODDS UP!" tag next to the label, scaled from its centre when it pops in
            Image tag = CreatePlainImage(root.transform, "OddsUpTag", GetRarityColor(3));
            oddsUpTag = tag.rectTransform;
            oddsUpTag.anchorMin = oddsUpTag.anchorMax = new Vector2(0f, 1f);
            oddsUpTag.pivot = new Vector2(0.5f, 0.5f);
            oddsUpTag.sizeDelta = new Vector2(108f, 20f);
            oddsUpTag.anchoredPosition = new Vector2(146f + 54f, -10f);
            Text tagText = MakeText(oddsUpTag, "Text", "ODDS UP!", 12, TextAnchor.MiddleCenter, HAZARD_INK);
            tagText.fontStyle = FontStyle.Bold;
            StretchFill(tagText.rectTransform);
            oddsUpTag.gameObject.SetActive(false);

            Color chrome = new Color(HAZARD_YELLOW.r, HAZARD_YELLOW.g, HAZARD_YELLOW.b, 0.8f);
            Image cabinet = CreatePlainImage(root.transform, "Cabinet", CARD_BG);
            oddsCabinet = cabinet.rectTransform;
            StretchTopRT(cabinet, 0f, 0f, OddsCabinetTop, OddsHeight - OddsCabinetTop);
            var cabinetOutline = cabinet.gameObject.AddComponent<Outline>();
            cabinetOutline.effectColor = chrome;
            cabinetOutline.effectDistance = new Vector2(2f, -2f);
            CreateCornerNotches(cabinet.transform, 3f, 4f, new Color(chrome.r, chrome.g, chrome.b, 0.45f));

            float reelWidth = (PanelWidth - 2f * OddsReelMargin - 2f * OddsReelGap) / 3f;
            for (int i = 0; i < 3; i++)
            {
                var reel = new OddsReel { color = GetRarityColor(i + 1) };
                oddsReels[i] = reel;
                float x = OddsReelMargin + i * (reelWidth + OddsReelGap);

                reel.plate = MakeText(cabinet.transform, $"Plate{i + 1}", new string('★', i + 1), 14,
                    TextAnchor.MiddleCenter, reel.color);
                PlaceTopLeft(reel.plate, x, OddsPlateTop, reelWidth, OddsPlateHeight);

                Image window = CreatePlainImage(cabinet.transform, $"Reel{i + 1}", BG_OVERLAY);
                PlaceTopLeft(window, x, OddsWindowTop, reelWidth, OddsWindowHeight);
                var windowOutline = window.gameObject.AddComponent<Outline>();
                windowOutline.effectColor = new Color(1f, 1f, 1f, 0.12f);
                windowOutline.effectDistance = new Vector2(2f, -2f);
                reel.window = window.rectTransform;

                // The rows scroll inside a clipped strip (a child, so the window's outline isn't clipped)
                var strip = new GameObject("Strip", typeof(RectTransform), typeof(RectMask2D));
                strip.transform.SetParent(window.transform, false);
                StretchFill((RectTransform)strip.transform);

                for (int r = 0; r < OddsReelRows; r++)
                {
                    Text row = MakeText(strip.transform, $"Row{r}", "", 22, TextAnchor.MiddleCenter, reel.color);
                    row.fontStyle = FontStyle.Bold;
                    RectTransform rowRect = row.rectTransform;
                    rowRect.anchorMin = new Vector2(0f, 0.5f);
                    rowRect.anchorMax = new Vector2(1f, 0.5f);
                    rowRect.pivot = new Vector2(0.5f, 0.5f);
                    rowRect.sizeDelta = new Vector2(0f, OddsRowHeight);
                    reel.rows[r] = row;
                }

                // Two-step dark bands at the top and bottom round the drum off
                foreach (float bandHeight in new[] { 10f, 4f })
                {
                    for (int edge = 0; edge < 2; edge++)
                    {
                        Image band = CreatePlainImage(strip.transform, "Shade", new Color(0f, 0f, 0f, 0.4f));
                        RectTransform bandRect = band.rectTransform;
                        bandRect.anchorMin = new Vector2(0f, edge);
                        bandRect.anchorMax = new Vector2(1f, edge);
                        bandRect.pivot = new Vector2(0.5f, edge);
                        bandRect.anchoredPosition = Vector2.zero;
                        bandRect.sizeDelta = new Vector2(0f, bandHeight);
                    }
                }

                // Payline marks on both sides
                for (int side = 0; side < 2; side++)
                {
                    Image mark = CreatePlainImage(strip.transform, "Payline", chrome);
                    RectTransform markRect = mark.rectTransform;
                    markRect.anchorMin = markRect.anchorMax = new Vector2(side, 0.5f);
                    markRect.pivot = new Vector2(side, 0.5f);
                    markRect.anchoredPosition = Vector2.zero;
                    markRect.sizeDelta = new Vector2(4f, 10f);
                }
            }

            // Lever on the right side: a mount, a stick that flips down when pulled, and a red knob
            Image mount = CreatePlainImage(cabinet.transform, "LeverMount", TILE_BG);
            RectTransform mountRect = mount.rectTransform;
            mountRect.anchorMin = mountRect.anchorMax = new Vector2(1f, 0.5f);
            mountRect.pivot = new Vector2(0f, 0.5f);
            mountRect.anchoredPosition = new Vector2(2f, 0f);
            mountRect.sizeDelta = new Vector2(10f, 22f);

            Image stick = CreatePlainImage(cabinet.transform, "Lever", STAT_LABEL_COLOR);
            oddsLever = stick.rectTransform;
            oddsLever.anchorMin = oddsLever.anchorMax = new Vector2(1f, 0.5f);
            oddsLever.pivot = new Vector2(0.5f, 0f);
            oddsLever.anchoredPosition = new Vector2(8f, 0f);
            oddsLever.sizeDelta = new Vector2(4f, 28f);

            Image knob = CreatePlainImage(oddsLever, "Knob", DECK_FULL_COLOR);
            RectTransform knobRect = knob.rectTransform;
            knobRect.anchorMin = knobRect.anchorMax = new Vector2(0.5f, 1f);
            knobRect.pivot = new Vector2(0.5f, 0.5f);
            knobRect.anchoredPosition = Vector2.zero;
            knobRect.sizeDelta = new Vector2(12f, 12f);
            Image knobShine = CreatePlainImage(knobRect, "Shine", new Color(1f, 1f, 1f, 0.45f));
            PlaceTopLeft(knobShine, 2f, 2f, 4f, 4f);

            oddsFlash = CreatePlainImage(cabinet.transform, "Flash", new Color(1f, 1f, 1f, 0f));
            StretchFill(oddsFlash.rectTransform);

            ApplyOdds(new DraftOdds(100f, 0f, 0f));
            RefreshOddsVisibility();
        }

        /// <summary>
        /// Checkbox + label in the top-right header, <paramref name="y"/> from the top. Its right edge is
        /// <paramref name="rightInset"/> from the screen's right edge, and it is only as wide as its label.
        /// </summary>
        private RectTransform BuildCheckboxToggle(Transform parent, string objName, string label, float rightInset,
            float y, Action onClick, out Image check, out float width)
        {
            const float boxSize = 24f;
            const int fontSize = 12;

            var toggleObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Button));
            toggleObj.transform.SetParent(parent, false);
            toggleObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible hit area over box + label
            var toggleRect = toggleObj.GetComponent<RectTransform>();

            var boxObj = new GameObject("Box", typeof(RectTransform), typeof(Image));
            boxObj.transform.SetParent(toggleObj.transform, false);
            check = boxObj.GetComponent<Image>();
            check.raycastTarget = false;
            check.preserveAspect = true;
            var boxRect = boxObj.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(boxSize, boxSize);

            Text text = MakeText(toggleObj.transform, "Label", label, fontSize, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(boxSize + 10f, 0f);
            textRect.offsetMax = Vector2.zero;

            // Just wide enough for the label, so toggles in a row sit at an even distance from each other
            float textWidth = text.preferredWidth > 1f ? Mathf.Ceil(text.preferredWidth) + 4f : label.Length * fontSize * 0.7f;
            width = boxSize + 10f + textWidth;
            PositionRT(toggleRect, new Vector2(-rightInset, y), new Vector2(1f, 1f), new Vector2(width, 32f));
            toggleRect.pivot = new Vector2(1f, 1f);

            var button = toggleObj.GetComponent<Button>();
            button.onClick.AddListener(() => onClick());
            AttachFeedback(button, ACCENT_BRIGHT, 1.04f);
            return toggleRect;
        }

        /// <summary>
        /// Two checkboxes under the deck count (top-right): "SKIP CARD ANIMATION" (see <see cref="SkipCardAnimation"/>)
        /// at the edge and "DRAFT ODDS" (see <see cref="HideDraftOdds"/>) just left of it, on the same line.
        /// </summary>
        private RectTransform BuildSkipAnimationToggle(Transform parent)
        {
            const float rightInset = 28f;
            const float rowY = -92f;
            const float gap = 28f;

            RectTransform skipRect = BuildCheckboxToggle(parent, "SkipAnimationToggle", "SKIP CARD ANIMATION", rightInset,
                rowY, ToggleSkipCardAnimation, out skipAnimationCheck, out float skipWidth);
            RefreshSkipAnimationToggle();

            oddsToggleRect = BuildCheckboxToggle(parent, "DraftOddsToggle", "DRAFT ODDS", rightInset + skipWidth + gap,
                rowY, ToggleDraftOdds, out oddsToggleCheck, out _);
            RefreshOddsVisibility();
            return skipRect;
        }

        /// <summary>
        /// Minimize and restore controls for viewing the map and editing the deck while drafting.
        /// While minimized, the gameplay HUD's deck bar works as in Preparation: cards can be rearranged, discarded
        /// and deployed (StageBootstrapper.IsDeploymentAllowed), freeing deck slots for the card being picked.
        /// </summary>
        private void BuildMinimizeControls(Transform canvasRoot, Transform buttonsContainer)
        {
            // MINIMIZE always joins the Reroll / Skip row, in dev mode and player mode alike.
            Button minimize = MakeButton(buttonsContainer, "MinimizeDraftButton", "MINIMIZE",
                new Vector2(200f, 60f), GameplayHUDUI.ButtonContainerType.Dark, "icon_minus");
            minimizeButton = minimize;
            minimize.onClick.AddListener(() => StartCoroutine(MinimizeAfterClick()));
            AttachFeedback(minimize, Color.white, 1.05f);

            Button back = MakeButton(canvasRoot, "BackToCardPickButton", "RESUME", new Vector2(200f, 48f), GameplayHUDUI.ButtonContainerType.Primary);
            PositionRT(back, new Vector2(0f, -24f), new Vector2(0.5f, 1f), new Vector2(200f, 48f));
            back.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            back.onClick.AddListener(() => SetMinimized(false));
            MenuButtonFeedback backFeedback = AttachFeedback(back, ACCENT_BRIGHT, 1.04f);
            backFeedback.glowAlways = true;

            restoreBar = back.gameObject;
            restoreBar.SetActive(false);
        }

        private IEnumerator MinimizeAfterClick()
        {
            yield return new WaitForEndOfFrame();
            SetMinimized(true);
        }

        /// <summary>
        /// Shown the first time REROLL is pressed in an opening draft (CardDraftSystem.IsOpeningDraft): that draft
        /// only deals 1★ operators and no Medics or Specialists, rerolls included. REROLL goes ahead, CANCEL keeps the hand. "Don't show this
        /// again" turns GameplaySettings.ShowOpeningRerollNotice off (back on in Settings → Gameplay).
        /// </summary>
        private void ShowRerollNotice()
        {
            if (rerollNoticeRoot == null) BuildRerollNotice();
            rerollNoticeDontShow = false;
            RefreshRerollNoticeCheck();
            rerollNoticeRoot.transform.SetAsLastSibling();
            rerollNoticeRoot.SetActive(true);
            PlayClick();
        }

        private void CloseRerollNotice(bool reroll)
        {
            ClearUiSelection();
            if (rerollNoticeRoot != null) rerollNoticeRoot.SetActive(false);
            if (rerollNoticeDontShow) GameplaySettings.ShowOpeningRerollNotice = false;
            if (!reroll)
            {
                PlayClick();
                return;
            }

            rerollNoticeAcknowledged = true;
            if (draftSystem != null && IsActiveGameplayCardPick() && !isShuffling) Reroll();
        }

        private void RefreshRerollNoticeCheck()
        {
            if (rerollNoticeCheck == null) return;
            Sprite sprite = LoadUiSprite(rerollNoticeDontShow ? "UI/controls/checkbox_on" : "UI/controls/checkbox_off", Vector4.zero);
            rerollNoticeCheck.sprite = sprite;
            rerollNoticeCheck.color = sprite != null ? Color.white : (rerollNoticeDontShow ? ACCENT_COLOR : new Color(1f, 1f, 1f, 0.25f));
        }

        private void BuildRerollNotice()
        {
            const float panelWidth = 760f;
            const float panelHeight = 390f;
            const float textWidth = panelWidth - 80f;

            // Full-screen dimmer: blocks the draft underneath while the notice is up
            rerollNoticeRoot = new GameObject("OpeningRerollNotice", typeof(RectTransform), typeof(Image));
            rerollNoticeRoot.transform.SetParent(overlayRoot.transform, false);
            StretchFill(rerollNoticeRoot.GetComponent<RectTransform>());
            rerollNoticeRoot.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.72f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(rerollNoticeRoot.transform, false);
            PositionRT(panel.GetComponent<RectTransform>(), Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(panelWidth, panelHeight));
            GameplayHUDUI.ApplyPanelContainer(panel, GameplayHUDUI.PanelContainerType.Metal);

            void PlaceTop(RectTransform rect, float top, float width, float height)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -top);
                rect.sizeDelta = new Vector2(width, height);
            }

            Text title = MakeText(panel.transform, "Title", "OPENING DRAFT", 24, TextAnchor.MiddleCenter, HAZARD_YELLOW);
            title.fontStyle = FontStyle.Bold;
            PlaceTop(title.rectTransform, 28f, textWidth, 40f);

            Sprite warningSprite = GameplayHUDUI.LoadIconSprite("icon_warning");
            if (warningSprite != null)
            {
                Image icon = CreatePlainImage(panel.transform, "WarningIcon", HAZARD_YELLOW);
                icon.sprite = warningSprite;
                icon.preserveAspect = true;
                PlaceTop(icon.rectTransform, 30f, 36f, 36f);
                icon.rectTransform.anchoredPosition = new Vector2(-("OPENING DRAFT".Length * 24f) * 0.5f - 34f, -30f);
            }

            Text body = MakeText(panel.transform, "Body",
                "The first draft of every run only deals 1-star Guards, Defenders, Snipers and Casters, so " +
                "restarting for a better hand won't help. Rerolling now will only show more of the same.",
                14, TextAnchor.UpperCenter, STAT_VALUE_COLOR);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;
            body.lineSpacing = 1.5f;
            PlaceTop(body.rectTransform, 96f, textWidth, 120f);

            // "Don't show this again" checkbox
            const float boxSize = 24f;
            const int checkFontSize = 12;
            const string checkLabel = "DON'T SHOW THIS AGAIN";
            var toggleObj = new GameObject("DontShowAgainToggle", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleObj.transform.SetParent(panel.transform, false);
            toggleObj.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible hit area over box + label
            PlaceTop(toggleObj.GetComponent<RectTransform>(), 226f, boxSize + 10f + checkLabel.Length * checkFontSize + 8f, 32f);

            rerollNoticeCheck = CreatePlainImage(toggleObj.transform, "Box", Color.white);
            rerollNoticeCheck.preserveAspect = true;
            var boxRect = rerollNoticeCheck.rectTransform;
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0f, 0.5f);
            boxRect.pivot = new Vector2(0f, 0.5f);
            boxRect.anchoredPosition = Vector2.zero;
            boxRect.sizeDelta = new Vector2(boxSize, boxSize);

            Text checkText = MakeText(toggleObj.transform, "Label", checkLabel, checkFontSize, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            StretchFill(checkText.rectTransform);
            checkText.rectTransform.offsetMin = new Vector2(boxSize + 10f, 0f);

            var toggle = toggleObj.GetComponent<Button>();
            toggle.onClick.AddListener(() =>
            {
                rerollNoticeDontShow = !rerollNoticeDontShow;
                RefreshRerollNoticeCheck();
                PlayClick();
            });
            AttachFeedback(toggle, ACCENT_BRIGHT, 1.04f);

            Text settingsHint = MakeText(panel.transform, "SettingsHint", "Turn it back on in SETTINGS > GAMEPLAY.", 10,
                TextAnchor.MiddleCenter, new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.7f));
            PlaceTop(settingsHint.rectTransform, 262f, textWidth, 20f);

            Button reroll = MakeButton(panel.transform, "NoticeRerollButton", "REROLL", new Vector2(220f, 56f),
                GameplayHUDUI.ButtonContainerType.Warning);
            PlaceTop(reroll.GetComponent<RectTransform>(), panelHeight - 84f, 220f, 56f);
            reroll.GetComponent<RectTransform>().anchoredPosition = new Vector2(-125f, -(panelHeight - 84f));
            reroll.onClick.AddListener(() => CloseRerollNotice(true));
            AttachFeedback(reroll, new Color(1f, 0.85f, 0.35f, 1f), 1.05f);

            Button cancel = MakeButton(panel.transform, "NoticeCancelButton", "CANCEL", new Vector2(220f, 56f),
                GameplayHUDUI.ButtonContainerType.Dark);
            PlaceTop(cancel.GetComponent<RectTransform>(), panelHeight - 84f, 220f, 56f);
            cancel.GetComponent<RectTransform>().anchoredPosition = new Vector2(125f, -(panelHeight - 84f));
            cancel.onClick.AddListener(() => CloseRerollNotice(false));
            AttachFeedback(cancel, Color.white, 1.05f);

            rerollNoticeRoot.SetActive(false);
        }
    }
}
