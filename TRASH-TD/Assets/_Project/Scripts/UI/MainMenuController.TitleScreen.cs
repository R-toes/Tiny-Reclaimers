using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TrashTD.Audio;
using TrashTD.Data;
using StageProgress = TrashTD.Core.GameLoop.StageProgress;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Building the menu UI and the title screen: buttons, backdrop, intro and label typing.
    public partial class MainMenuController
    {
        public void BuildDefaultUi()
        {
            var canvasGo = GetOrCreateCanvas();
            canvasGo.transform.SetParent(null, false);

            // ── Main Menu Panel ──────────────────────────────
            if (mainMenuPanel == null)
            {
                mainMenuPanel = new GameObject("MainMenuPanel", typeof(RectTransform), typeof(Image));
                mainMenuPanel.transform.SetParent(canvasGo.transform, false);
                StretchFill(mainMenuPanel.GetComponent<RectTransform>());
                mainMenuPanel.GetComponent<Image>().color = BackgroundColor;
            }

            BuildTitleScreen();

            // ── Stage Selection Panel ────────────────────────
            if (stageSelectionPanel == null)
            {
                stageSelectionPanel = new GameObject("StageSelectionPanel", typeof(RectTransform), typeof(Image));
                stageSelectionPanel.transform.SetParent(canvasGo.transform, false);
                stageSelectionPanel.GetComponent<Image>().color = BackgroundColor;
                StretchFill(stageSelectionPanel.GetComponent<RectTransform>());
                stageSelectionPanel.SetActive(false);
            }

            // Ambient motes behind the map
            CreateAmbientLayer(stageSelectionPanel.transform, 22);

            // --- Pip-Boy Map Container (replaces old grid) ---
            if (stageListRoot == null)
            {
                var mapContainerGo = new GameObject("StageMapContainer", typeof(RectTransform), typeof(Image));
                mapContainerGo.transform.SetParent(stageSelectionPanel.transform, false);
                stageListRoot = mapContainerGo.transform;

                var mapContainerRect = mapContainerGo.GetComponent<RectTransform>();
                // Fills the whole screen
                StretchFill(mapContainerRect);

                var mapBg = mapContainerGo.GetComponent<Image>();
                stageMapImage = mapBg;
                if (stageMapBackgroundSprite == null)
                {
                    stageMapBackgroundSprite = LoadSpriteSafely(TrashTD.Core.GameLoop.ActCatalog.Act1MapResourceName, TrashTD.Core.GameLoop.ActCatalog.Act1MapEditorPath);
                }

                if (stageMapBackgroundSprite != null)
                {
                    if (stageMapBackgroundSprite.texture != null)
                    {
                        stageMapBackgroundSprite.texture.filterMode = FilterMode.Point;
                    }
                    mapBg.sprite = stageMapBackgroundSprite;
                    mapBg.type = Image.Type.Simple;
                    mapBg.preserveAspect = false;
                    mapBg.color = Color.white;
                }
                else
                {
                    // Dark placeholder with subtle CRT-green tint if no art assigned
                    mapBg.color = new Color(0.02f, 0.05f, 0.03f, 1f);
                }

                // Add child Image stretched to fill the map sprite with 12-strip animation overlay
                var animOverlayGo = new GameObject("StageMap_Anim_Overlay", typeof(RectTransform), typeof(Image));
                animOverlayGo.transform.SetParent(mapContainerGo.transform, false);
                var animOverlayRect = animOverlayGo.GetComponent<RectTransform>();
                StretchFill(animOverlayRect);

                var animOverlayImage = animOverlayGo.GetComponent<Image>();
                animOverlayImage.sprite = null;
                animOverlayImage.type = Image.Type.Simple;
                animOverlayImage.preserveAspect = false;
                animOverlayImage.raycastTarget = false;
                animOverlayImage.color = Color.white;

                // Animates itself from Resources (12 fps), identically in the Editor and in builds
                var animOverlayComp = animOverlayGo.AddComponent<StageMapAnimOverlay>();
                animOverlayComp.FrameDuration = 0.0833f;

                // Add the layout driver so nodes scale with the map
                stageMapLayout = mapContainerGo.AddComponent<StageMapLayout>();

                // The CRT look comes from the game-wide CrtDisplay
            }

            // Terminal frame around the whole map screen
            AddCornerBrackets(stageSelectionPanel.GetComponent<RectTransform>(), 46f, 4f, TerminalLine, 18f);

            // Machine panel down the left edge; its joystick follows the map navigation
            stageSidePanel = StageMapSidePanel.Create(stageSelectionPanel.transform);

            // Backing plate so the title block reads over busy map art
            float headerX = 350f;
            float headerY = -115f;
            float headerWidth = 520f;
            float headerHeight = 124f;

            var titlePlate = new GameObject("StageSelectorTitlePlate", typeof(RectTransform), typeof(Image));
            titlePlate.transform.SetParent(stageSelectionPanel.transform, false);
            stageTitlePlate = titlePlate.GetComponent<RectTransform>();
            Place(stageTitlePlate, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(headerX, headerY), new Vector2(headerWidth, headerHeight));
            var titlePlateImage = titlePlate.GetComponent<Image>();
            titlePlateImage.color = TerminalPlate;
            titlePlateImage.raycastTarget = true;
            AddCornerBrackets(stageTitlePlate, 16f, 2f, AccentBright);

            // ACT 1 title (prominent large font size)
            var actTitle = MakeText(stageSelectionPanel.transform, "StageSelectorActTitle", string.Empty, 38, TextAnchor.MiddleLeft,
                SidebarYellow, FontStyle.Bold);
            actTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            actTitle.verticalOverflow = VerticalWrapMode.Overflow;
            Place(actTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(headerX + 18f, headerY - 10f), new Vector2(280f, 44f));
            stageHeaderTitle = actTitle.rectTransform;
            var actTitleShadow = actTitle.gameObject.AddComponent<Shadow>();
            actTitleShadow.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            actTitleShadow.effectDistance = new Vector2(0f, -3f);

            // NERC Development Zone on a new line below ACT 1
            var zoneTitle = MakeText(stageSelectionPanel.transform, "StageSelectorZoneTitle", string.Empty, 22, TextAnchor.MiddleLeft,
                new Color(0.92f, 0.98f, 0.94f, 1f), FontStyle.Bold);
            zoneTitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            zoneTitle.verticalOverflow = VerticalWrapMode.Overflow;
            Place(zoneTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(headerX + 20f, headerY - 56f), new Vector2(headerWidth - 36f, 30f));
            stageZoneTitle = zoneTitle.rectTransform;
            var zoneTitleShadow = zoneTitle.gameObject.AddComponent<Shadow>();
            zoneTitleShadow.effectColor = new Color(0f, 0f, 0f, 0.50f);
            zoneTitleShadow.effectDistance = new Vector2(0f, -2f);

            // Subtitle: Choose deployment
            stageHeaderSubtitleText = MakeText(stageSelectionPanel.transform, "StageSelectorSubtitle", StageSubtitleText, 12, TextAnchor.MiddleLeft, TerminalText);
            stageHeaderSubtitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            stageHeaderSubtitleText.verticalOverflow = VerticalWrapMode.Overflow;
            Place(stageHeaderSubtitleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(headerX + 20f, headerY - 92f), new Vector2(300f, 20f));
            stageHeaderSubtitle = stageHeaderSubtitleText.rectTransform;

            // Info prompt
            var intelBadge = MakeText(stageSelectionPanel.transform, "StageSelectorIntelBadge", "◆ [?] INTEL", 11, TextAnchor.MiddleRight,
                new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.75f), FontStyle.Bold);
            Place(intelBadge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(headerX + headerWidth - 120f, headerY - 92f), new Vector2(100f, 20f));

            // Hover lore card: auto-resizes to fit its text perfectly and appears beneath the title plate only when hovered
            var loreCard = new GameObject("StageSelectorLoreCard", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            loreCard.transform.SetParent(stageSelectionPanel.transform, false);
            stageZoneLore = loreCard.GetComponent<RectTransform>();
            stageZoneLore.anchorMin = new Vector2(0f, 1f);
            stageZoneLore.anchorMax = new Vector2(0f, 1f);
            stageZoneLore.pivot = new Vector2(0f, 1f);
            stageZoneLore.anchoredPosition = new Vector2(headerX, headerY - headerHeight - 8f);
            stageZoneLore.sizeDelta = new Vector2(headerWidth, 0f);

            var loreCardImage = loreCard.GetComponent<Image>();
            loreCardImage.color = new Color(0.012f, 0.035f, 0.024f, 0.96f);
            loreCardImage.raycastTarget = true;

            var loreVlg = loreCard.GetComponent<VerticalLayoutGroup>();
            loreVlg.padding = new RectOffset(18, 18, 14, 18);
            loreVlg.spacing = 8f;
            loreVlg.childControlWidth = true;
            loreVlg.childControlHeight = true;
            loreVlg.childForceExpandWidth = true;
            loreVlg.childForceExpandHeight = false;

            var loreCsf = loreCard.GetComponent<ContentSizeFitter>();
            loreCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            loreCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var loreOutline = loreCard.AddComponent<Outline>();
            loreOutline.effectColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            loreOutline.effectDistance = new Vector2(1f, -1f);

            // Bracket overlay that stretches to fill the auto-sized lore card without interfering with layout
            var bracketOverlay = new GameObject("BracketOverlay", typeof(RectTransform), typeof(LayoutElement));
            bracketOverlay.transform.SetParent(loreCard.transform, false);
            StretchFill(bracketOverlay.GetComponent<RectTransform>());
            var overlayLe = bracketOverlay.GetComponent<LayoutElement>();
            overlayLe.ignoreLayout = true;
            AddCornerBrackets(bracketOverlay.GetComponent<RectTransform>(), 12f, 2f, AccentBright);

            var loreTag = MakeText(loreCard.transform, "LoreTag", "◆ NATIONAL ENERGY RESOURCE CORPORATION", 11, TextAnchor.MiddleLeft, AccentBright, FontStyle.Bold);
            loreTag.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tagLe = loreTag.gameObject.AddComponent<LayoutElement>();
            tagLe.minHeight = 18f;
            tagLe.preferredHeight = 18f;
            tagLe.flexibleHeight = 0f;

            var loreDivider = new GameObject("LoreDivider", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            loreDivider.transform.SetParent(loreCard.transform, false);
            var dividerLe = loreDivider.GetComponent<LayoutElement>();
            dividerLe.minHeight = 1f;
            dividerLe.preferredHeight = 1f;
            dividerLe.flexibleHeight = 0f;
            var loreDividerImage = loreDivider.GetComponent<Image>();
            loreDividerImage.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f);
            loreDividerImage.raycastTarget = false;

            var loreBody = MakeText(loreCard.transform, "LoreBody", string.Empty, 11, TextAnchor.UpperLeft, new Color(TerminalText.r, TerminalText.g, TerminalText.b, 0.92f));
            loreBody.lineSpacing = 1.38f;
            loreBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            loreBody.verticalOverflow = VerticalWrapMode.Overflow;
            var bodyLe = loreBody.gameObject.AddComponent<LayoutElement>();
            bodyLe.preferredWidth = headerWidth - 36f;
            bodyLe.flexibleHeight = 1f;

            var loreGroup = loreCard.GetComponent<CanvasGroup>();
            var hoverController = titlePlate.AddComponent<StageHeaderLoreHover>();
            hoverController.Init(loreGroup);
            var childTrigger = loreCard.AddComponent<StageHeaderLoreChildTrigger>();
            childTrigger.parentHover = hoverController;

            LayoutRebuilder.ForceRebuildLayoutImmediate(stageZoneLore);

            // Status ticker: types out the stage under the pointer
            var tickerPlate = new GameObject("StageTicker", typeof(RectTransform), typeof(Image));
            tickerPlate.transform.SetParent(stageSelectionPanel.transform, false);
            stageTickerPlate = tickerPlate.GetComponent<RectTransform>();
            Place(stageTickerPlate, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(TickerWidth, 48f));
            var tickerImage = tickerPlate.GetComponent<Image>();
            tickerImage.color = TerminalPlate;
            tickerImage.raycastTarget = false;
            AddCornerBrackets(stageTickerPlate, 12f, 2f, TerminalLine);

            // Fixed-size window; long messages scroll inside it instead of widening the plate
            var tickerViewport = new GameObject("StageTickerViewport", typeof(RectTransform), typeof(RectMask2D));
            tickerViewport.transform.SetParent(tickerPlate.transform, false);
            var tickerViewportRect = tickerViewport.GetComponent<RectTransform>();
            tickerViewportRect.anchorMin = Vector2.zero;
            tickerViewportRect.anchorMax = Vector2.one;
            tickerViewportRect.offsetMin = new Vector2(TickerTextInset, 0f);
            tickerViewportRect.offsetMax = new Vector2(-TickerTextInset, 0f);

            stageTickerText = MakeText(tickerViewport.transform, "StageTickerText", StageTickerIdle, TickerFontSize, TextAnchor.MiddleLeft, AccentBright, FontStyle.Bold);
            stageTickerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var tickerTextRect = stageTickerText.rectTransform;
            tickerTextRect.anchorMin = new Vector2(0f, 0f);
            tickerTextRect.anchorMax = new Vector2(0f, 1f);
            tickerTextRect.pivot = new Vector2(0f, 0.5f);
            tickerTextRect.anchoredPosition = Vector2.zero;
            tickerTextRect.sizeDelta = new Vector2(TickerViewportWidth, 0f);

            if (sandboxButton == null)
            {
                sandboxButton = CreateButtonChild(stageSelectionPanel.transform, "SandboxButton", "⚙  QA SANDBOX");
            }
            StyleTerminalButton(sandboxButton, AccentBright, 15);
            Place(sandboxButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(200f, 48f));
            sandboxButton.onClick.RemoveAllListeners();
            sandboxButton.onClick.AddListener(() =>
            {
                PlayClick();
                int sandboxIndex = SandboxStageIndex;
                if (sandboxIndex >= 0) SelectStage(sandboxIndex);
            });
            AttachFeedback(sandboxButton, PlayClick, AccentBright, 1.05f);

            var hint = MakeText(stageSelectionPanel.transform, "StageHint", "ESC  BACK", 12, TextAnchor.MiddleRight,
                new Color(TerminalText.r, TerminalText.g, TerminalText.b, 0.6f));
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 96f), new Vector2(200f, 22f));
            stageHint = hint.rectTransform;

            if (backButton == null)
            {
                backButton = CreateButtonChild(stageSelectionPanel.transform, "BackButton", "◀  BACK");
            }
            StyleTerminalButton(backButton, TerminalText, 16);
            Place(backButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(StageBackButtonX, 40f), new Vector2(200f, 48f));

            if (stageDetailsPanel == null)
            {
                BuildStageDetailsUi();
            }
        }

        // ─────────────────────────────────────────────────────
        //  Title screen
        // ─────────────────────────────────────────────────────

        /// <summary>
        /// The start screen: a live stage-art backdrop with an enemy parade (MainMenuBackdrop) inside a camera
        /// viewfinder, the wide Tiny Reclaimers logo centred on screen, and the terminal-style
        /// menu rows centred under it.
        /// </summary>
        private void BuildTitleScreen()
        {
            Transform panel = mainMenuPanel.transform;

            // Live backdrop (filled in by InitializeMenuBackdrop once the stages are loaded), toxic motes over it
            var backdropGo = new GameObject("MenuBackdrop", typeof(RectTransform));
            backdropGo.transform.SetParent(panel, false);
            StretchFill(backdropGo.GetComponent<RectTransform>());
            menuBackdrop = backdropGo.AddComponent<MainMenuBackdrop>();
            CreateAmbientLayer(panel, 30);
            backdropGo.transform.SetAsFirstSibling();

            // Faint terminal-green tint over the live backdrop (above it, below the motes, viewfinder and menu).
            // It multiplies the picture instead of laying a translucent colour over it, so it adds a green cast
            // without washing the backdrop out or lifting its blacks.
            Shader multiplyShader = Shader.Find("TrashTD/UI/Multiply");
            if (multiplyShader == null) multiplyShader = Resources.Load<Shader>("Shaders/UIMultiply");
            Image greenTint = CreateBlock(panel, "BackdropGreenTint", Color.white);
            if (multiplyShader != null)
            {
                greenTint.material = new Material(multiplyShader);
                greenTint.color = Color.Lerp(Color.white, MenuBackdropTintColor, MenuBackdropTintStrength);
            }
            else
            {
                // No multiply shader available: a much fainter plain overlay
                greenTint.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.06f);
            }
            StretchFill(greenTint.rectTransform);
            greenTint.raycastTarget = false;
            greenTint.transform.SetSiblingIndex(1);

            // Camera viewfinder: corner brackets, REC light + feed caption top-right
            var viewfinder = new GameObject("Viewfinder", typeof(RectTransform));
            viewfinder.transform.SetParent(panel, false);
            var viewfinderRect = viewfinder.GetComponent<RectTransform>();
            viewfinderRect.anchorMin = Vector2.zero;
            viewfinderRect.anchorMax = Vector2.one;
            viewfinderRect.offsetMin = new Vector2(28f, 44f);
            viewfinderRect.offsetMax = new Vector2(-28f, -28f);
            AddCornerBrackets(viewfinderRect, 54f, 3f, TerminalLine);

            var versionText = MakeText(panel, "VersionText", "TINY RECLAIMERS TD  //  v0.1 EARLY ACCESS", 12, TextAnchor.LowerRight, TerminalText);
            Place(versionText.rectTransform, Vector2.one, Vector2.one, new Vector2(-56f, 64f), new Vector2(600f, 20f));
            mainVersion = versionText.rectTransform;

            var feedStatus = new GameObject("FeedStatus", typeof(RectTransform));
            feedStatus.transform.SetParent(panel, false);
            menuFeedStatus = feedStatus.GetComponent<RectTransform>();
            Place(menuFeedStatus, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-56f, -50f), new Vector2(720f, 52f));

            var recLabel = MakeText(menuFeedStatus, "RecLabel", "REC", 14, TextAnchor.UpperRight, RecRed);
            Place(recLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(80f, 20f));

            var recDot = new GameObject("RecDot", typeof(RectTransform), typeof(Image));
            recDot.transform.SetParent(menuFeedStatus, false);
            Place(recDot.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, 0f), new Vector2(14f, 14f));
            menuRecDot = recDot.GetComponent<Image>();
            menuRecDot.color = RecRed;
            menuRecDot.raycastTarget = false;

            menuFeedCaption = MakeText(menuFeedStatus, "FeedCaption", string.Empty, 12, TextAnchor.UpperRight, TerminalText);
            menuFeedCaption.horizontalOverflow = HorizontalWrapMode.Overflow;
            Place(menuFeedCaption.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(720f, 20f));

            titleFx = GetComponent<TitleScreenFx>();
            if (titleFx == null) titleFx = gameObject.AddComponent<TitleScreenFx>();

            // ── Emblem with the wide title over it, centred near the top of the stack ──
            var topCenter = new Vector2(0.5f, 1f);
            var logoRoot = new GameObject("TinyReclaimersLogoRoot", typeof(RectTransform));
            logoRoot.transform.SetParent(panel, false);
            titleBlock = logoRoot.GetComponent<RectTransform>();
            Place(titleBlock, topCenter, topCenter, new Vector2(0f, -TitleLogoTop), new Vector2(TitleEmblemSize, TitleEmblemSize));

            Sprite emblem = LoadSpriteSafely(
                "UI/Tiny Reclaimers Emblem Dim",
                "Assets/Resources/UI/Tiny Reclaimers Emblem Dim.png");
            if (emblem == null)
            {
                Debug.LogError("Main menu emblem sprite 'UI/Tiny Reclaimers Emblem Dim' could not be loaded.");
            }
            else
            {
                var emblemObject = new GameObject("TinyReclaimersEmblem", typeof(RectTransform), typeof(Image));
                emblemObject.transform.SetParent(titleBlock, false);
                StretchFill(emblemObject.GetComponent<RectTransform>());
                var emblemImage = emblemObject.GetComponent<Image>();
                emblemImage.sprite = emblem;
                emblemImage.preserveAspect = true;
                emblemImage.color = Color.white;
                emblemImage.raycastTarget = false;
            }

            // The title is animated (16 frames of 320x79); the still PNG is the fallback if the sheet can't be loaded
            RectTransform titleLogoRect = null;
            Image titleLogoGraphic = null;
            Sprite[] logoFrames = LoadTitleLogoFrames();
            Sprite logo = logoFrames != null ? logoFrames[0] : LoadSpriteSafely(
                "UI/Tiny Reclaimers Logo Wide",
                "Assets/Resources/UI/Tiny Reclaimers Logo Wide.png");
            if (logo == null)
            {
                Debug.LogError("Main menu logo sprite 'UI/Tiny Reclaimers Logo Wide' could not be loaded.");
            }
            else
            {
                // Added after the emblem, so it draws on top of it
                var logoObject = new GameObject("TinyReclaimersLogo", typeof(RectTransform), typeof(Image));
                logoObject.transform.SetParent(titleBlock, false);
                var centre = new Vector2(0.5f, 0.5f);
                float logoHeight = logoFrames != null ? TitleLogoAnimatedHeight : TitleLogoHeight;
                titleLogoRect = logoObject.GetComponent<RectTransform>();
                Place(titleLogoRect, centre, centre, new Vector2(0f, -TitleLogoDrop), new Vector2(TitleLogoWidth, logoHeight));
                var logoImage = logoObject.GetComponent<Image>();
                titleLogoGraphic = logoImage;
                logoImage.sprite = logo;
                logoImage.preserveAspect = true;
                logoImage.color = Color.white;
                logoImage.raycastTarget = false;

                if (logoFrames != null)
                {
                    var logoAnimation = logoObject.AddComponent<UiSpriteAnimation>();
                    logoAnimation.FrameDuration = TitleLogoFrameSeconds;
                    logoAnimation.Frames = logoFrames;
                }
            }
            // Glow behind the emblem and the occasional title glitch
            if (titleLogoRect != null)
                titleFx.BuildLogoEffects(titleBlock, titleLogoRect, titleLogoGraphic, TitleEmblemSize);
            // Bars that glide beside whichever menu row is highlighted
            titleFx.BuildSlideBars(panel, MenuButtonWidth * 0.5f);

            // ── Menu rows (INTEL is row 1 and SETTINGS row 2, added by ReclaimersIntelUI / AudioSettingsUI in Awake) ──
            if (startButton == null)
            {
                startButton = CreateButtonChild(panel, "StartButton", "PLAY");
            }
            PlaceMenuRow(startButton.GetComponent<RectTransform>(), 0);
            StyleMainMenuButton(startButton, "PLAY", true);

            if (exitButton == null)
            {
                exitButton = CreateButtonChild(panel, "ExitButton", "EXIT");
            }
            PlaceMenuRow(exitButton.GetComponent<RectTransform>(), 3);
            StyleMainMenuButton(exitButton, "EXIT", false);

            // Hint sits centred under the last row
            var hintAnchor = new Vector2(0.5f, 1f);
            menuHintText = MakeText(panel, "MenuHint", "[ARROWS] SELECT    [ENTER] CONFIRM", 11, TextAnchor.MiddleCenter,
                new Color(TerminalText.r, TerminalText.g, TerminalText.b, 0.7f));
            Place(menuHintText.rectTransform, hintAnchor, hintAnchor, new Vector2(0f, -MenuHintTop), new Vector2(MenuButtonWidth, 20f));
        }

        /// <summary>
        /// Terminal menu row: label and a caret plus bracket frame while the row
        /// is selected. Hovering selects the row, so mouse and keyboard share a single highlight.
        /// </summary>
        private void StyleMainMenuButton(Button button, string label, bool primary)
        {
            if (button == null) return;

            var image = button.GetComponent<Image>();
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = primary ? AccentColor : SidebarInk;

            Text labelText = button.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                labelText.text = label;
                labelText.font = UIFontHelper.GetPixelFont();
                labelText.fontSize = primary ? 22 : 16;
                labelText.fontStyle = FontStyle.Normal;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = primary ? PrimaryLabel : AccentColor;
                RectTransform labelRect = labelText.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(MenuButtonLabelInset, 0f);
                labelRect.offsetMax = new Vector2(-MenuButtonLabelInset, 0f);
            }

            if (!primary)
            {
                var bar = new GameObject("AccentBar", typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(button.transform, false);
                var barRect = bar.GetComponent<RectTransform>();
                barRect.anchorMin = Vector2.zero;
                barRect.anchorMax = new Vector2(0f, 1f);
                barRect.pivot = new Vector2(0f, 0.5f);
                barRect.offsetMin = Vector2.zero;
                barRect.offsetMax = new Vector2(4f, 0f);
                var barImage = bar.GetComponent<Image>();
                barImage.color = AccentColor;
                barImage.raycastTarget = false;
            }

            Text caret = MakeText(button.transform, "Caret", ">", primary ? 22 : 16, TextAnchor.MiddleLeft,
                new Color(primary ? PrimaryLabel.r : AccentBright.r, primary ? PrimaryLabel.g : AccentBright.g, primary ? PrimaryLabel.b : AccentBright.b, 0f));
            caret.rectTransform.offsetMin = new Vector2(18f, 0f);

            var frame = new GameObject("SelectFrame", typeof(RectTransform), typeof(CanvasGroup));
            frame.transform.SetParent(button.transform, false);
            var frameRect = frame.GetComponent<RectTransform>();
            StretchFill(frameRect);
            AddCornerBrackets(frameRect, 14f, 3f, AccentBright, -6f);
            var frameGroup = frame.GetComponent<CanvasGroup>();
            frameGroup.alpha = 0f;
            frameGroup.blocksRaycasts = false;
            frameGroup.interactable = false;

            MenuButtonFeedback feedback = AttachFeedback(button, null, primary ? AccentBright : AccentColor, 1.03f);
            feedback.Hovered = () =>
            {
                PlayHover();
                FocusUi(button.gameObject);
            };

            mainMenuButtons.Add(new MainMenuButtonView
            {
                feedback = feedback,
                selectFrame = frameGroup,
                caret = caret,
                label = labelText != null ? labelText.rectTransform : null,
                labelText = labelText,
                fullLabel = label
            });
        }

        /// <summary>Starts the backdrop on the playable stages and writes the boot line. Runs after BuildStageButtons.</summary>
        private void InitializeMenuBackdrop()
        {
            var showcase = new List<StageData>();
            foreach (StageData stage in availableStages)
            {
                if (IsTutorialStage(stage)) continue; // No map art to pan across, and not a numbered stage
                bool playableStage = stage != null && stage.stageId != SandboxStageId && IsStagePlayable(stage.stageId);
                showcase.Add(playableStage ? stage : null); // keep index i = Stage i+1 for the caption
            }

            if (menuBackdrop != null) menuBackdrop.Initialize(showcase, menuFeedCaption);
        }

        private void UpdateTitleScreen(float dt, float time)
        {
            bool cursorOn = Mathf.Repeat(time, 1f) < 0.5f;

            if (menuRecDot != null) menuRecDot.enabled = Mathf.Repeat(time, 1.4f) < 0.9f;

            float k = 1f - Mathf.Exp(-14f * dt);
            foreach (MainMenuButtonView view in mainMenuButtons)
            {
                if (view.feedback == null) continue;

                view.hot = Mathf.Lerp(view.hot, view.feedback.IsHot ? 1f : 0f, k);
                view.selectFrame.alpha = view.hot;

                Color caretColor = view.caret.color;
                caretColor.a = view.hot * (cursorOn ? 1f : 0.35f);
                view.caret.color = caretColor;

                if (view.label != null) view.label.offsetMin = new Vector2(MenuButtonLabelInset + 10f * view.hot, 0f);
            }

            if (titleFx != null)
            {
                MainMenuButtonView hottest = null;
                foreach (MainMenuButtonView view in mainMenuButtons)
                {
                    if (view.feedback != null && (hottest == null || view.hot > hottest.hot)) hottest = view;
                }
                titleFx.SetSlideTarget(hottest != null ? hottest.feedback.GetComponent<RectTransform>() : null, hottest != null ? hottest.hot : 0f);
            }
        }

        // ─────────────────────────────────────────────────────
        //  Title intro, typed labels
        // ─────────────────────────────────────────────────────

        private static void SetEntranceAlpha(List<EntranceItem> items, float alpha)
        {
            foreach (EntranceItem item in items)
            {
                if (item.group != null) item.group.alpha = alpha;
            }
        }

        private static void SetEntranceInteractive(List<EntranceItem> items, bool interactive)
        {
            foreach (EntranceItem item in items)
            {
                if (item.group == null) continue;
                item.group.interactable = interactive;
                item.group.blocksRaycasts = interactive;
            }
        }

        private const float IntroPromptOffset = 126f; // PRESS ANY KEY sits this far below where the menu rows start

        /// <summary>
        /// First open of the title screen in a session: the logo comes in with a blinking PRESS ANY KEY, and the menu
        /// rows only appear once a key or button is pressed.
        /// </summary>
        private IEnumerator RunTitleIntro()
        {
            SetEntranceAlpha(mainEntrance, 0f);
            SetEntranceInteractive(mainEntrance, false);

            // Bring the logo and viewfinder text in, then the prompt once they have settled
            SetEntranceInteractive(titleSceneEntrance, true);
            PlayEntrance(titleSceneEntrance, ref titleSceneRoutine);
            yield return new WaitForSecondsRealtime(0.7f);

            titleFx.ShowPrompt(mainMenuPanel.transform, MenuAreaTop + IntroPromptOffset);
            float waited = 0f;
            while (waited < 0.5f || !TitleScreenFx.AnyInputPressed())
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            titleFx.HidePrompt();
            PlayClick();
            SetEntranceInteractive(mainEntrance, true);
            titleIntroActive = false;
            TitleScreenFx.MarkIntroSeen();
            PlayEntrance(menuRowsEntrance, ref mainEntranceRoutine);
            StartMenuLabelTyping();
            FocusUi(startButton != null ? startButton.gameObject : null);
        }

        /// <summary>Types the menu row labels in, top row first, each a beat after the one above.</summary>
        private void StartMenuLabelTyping()
        {
            if (!isActiveAndEnabled) return;
            if (labelTypingRoutine != null) StopCoroutine(labelTypingRoutine);
            labelTypingRoutine = StartCoroutine(TypeMenuLabels());
        }

        private IEnumerator TypeMenuLabels()
        {
            var views = new List<MainMenuButtonView>();
            foreach (MainMenuButtonView view in mainMenuButtons)
            {
                if (view.labelText != null && !string.IsNullOrEmpty(view.fullLabel)) views.Add(view);
            }
            views.Sort((a, b) => b.feedback.GetComponent<RectTransform>().anchoredPosition.y
                .CompareTo(a.feedback.GetComponent<RectTransform>().anchoredPosition.y));

            foreach (MainMenuButtonView view in views) view.labelText.text = string.Empty;

            const float StartDelay = 0.2f;
            const float RowStagger = 0.09f;
            const float CharsPerSecond = 40f;
            float time = 0f;
            bool done = false;
            while (!done)
            {
                time += Time.unscaledDeltaTime;
                done = true;
                for (int i = 0; i < views.Count; i++)
                {
                    string full = views[i].fullLabel;
                    int count = Mathf.Clamp(Mathf.FloorToInt((time - StartDelay - i * RowStagger) * CharsPerSecond), 0, full.Length);
                    bool typing = count > 0 && count < full.Length;
                    views[i].labelText.text = full.Substring(0, count) + (typing ? "_" : string.Empty);
                    if (count < full.Length) done = false;
                }
                yield return null;
            }

            foreach (MainMenuButtonView view in views) view.labelText.text = view.fullLabel;
            labelTypingRoutine = null;
        }

        // ─────────────────────────────────────────────────────
        //  Panel navigation
        // ─────────────────────────────────────────────────────

        public void ShowMainMenu()
        {
            RestoreStageMapBehindDossier();
            bool instant = !menuInitialized;
            stageSelectionOpen = false;
            detailsOpen = false;
            worldMapOpen = false;
            SetBackgroundInteractable(true);

            SetPanelVisible(mainMenuPanel, true, instant);
            SetPanelVisible(stageSelectionPanel, false, instant);
            if (worldMap != null) SetPanelVisible(worldMap.Root, false, instant);
            SetPanelVisible(stageDetailsPanel, false, true, stageDetailsModal);

            if (titleIntroActive) return; // the intro brings the logo and rows in itself (RunTitleIntro)

            PlayEntrance(mainEntrance, ref mainEntranceRoutine);
            StartMenuLabelTyping();
            FocusUi(startButton != null ? startButton.gameObject : null);
        }
    }
}
