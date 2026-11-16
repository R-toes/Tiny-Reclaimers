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
    // The deck bar: deck slots and their badges, the bottom-right panel, trash zone, path preview button, discard confirmation and speed controls.
    public partial class GameplayHUDUI
    {
        private void CreateDeckBar(Transform root)
        {
            const float bgHeight = 170f;
            const float bgAspect = 378f / 58f;
            const float bgScale = bgHeight / 58f;

            // Bottom bar background
            var bar = new GameObject("DeckBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(root, false);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(0f, bgHeight);
            barRect.anchoredPosition = Vector2.zero;
            Image barImage = bar.GetComponent<Image>();
            barImage.color = Color.clear;
            barImage.raycastTarget = false;
            deckBarRect = barRect;

            // Deck background sprite-sheet animation — aspect-correct, anchored to
            // the bottom-left corner of the deck bar. Width is derived from the first
            // frame's aspect ratio so the sprite is never distorted.
            if (deckBgSprites != null && deckBgSprites.Length > 0)
            {
                var deckBgObj = new GameObject("DeckBgAnim", typeof(RectTransform), typeof(Image));
                deckBgObj.transform.SetParent(bar.transform, false);
                var deckBgRect = deckBgObj.GetComponent<RectTransform>();
                deckBgRect.anchorMin = new Vector2(0f, 0f);
                deckBgRect.anchorMax = new Vector2(0f, 0f);
                deckBgRect.pivot   = new Vector2(0f, 0f);
                Sprite firstFrame = deckBgSprites[0];
                float aspect = firstFrame != null && firstFrame.rect.height > 0f
                    ? firstFrame.rect.width / firstFrame.rect.height
                    : bgAspect;
                deckBgRect.sizeDelta = new Vector2(bgHeight * aspect, bgHeight);
                deckBgRect.anchoredPosition = Vector2.zero;
                deckBgAnimImage = deckBgObj.GetComponent<Image>();
                deckBgAnimImage.sprite = firstFrame;
                deckBgAnimImage.color = Color.white;
                deckBgAnimImage.preserveAspect = false;
                deckBgAnimImage.raycastTarget = false;
                deckBgAnimImage.type = Image.Type.Simple;
            }

            // Deck slots container matching the DeckBgAnim sprite dimensions
            var slotsContainer = new GameObject("DeckSlots", typeof(RectTransform));
            slotsContainer.transform.SetParent(bar.transform, false);
            var slotsRect = slotsContainer.GetComponent<RectTransform>();
            slotsRect.anchorMin = new Vector2(0f, 0f);
            slotsRect.anchorMax = new Vector2(0f, 0f);
            slotsRect.pivot = new Vector2(0f, 0f);
            slotsRect.sizeDelta = new Vector2(bgHeight * bgAspect, bgHeight);
            slotsRect.anchoredPosition = Vector2.zero;

            deckButtons = new Button[DeckSlotCount];
            deckButtonLabels = new Text[DeckSlotCount];
            deckButtonImages = new Image[DeckSlotCount];
            deckPortraitImages = new Image[DeckSlotCount];
            deckClassBadgeImages = new Image[DeckSlotCount];
            deckClassIconImages = new Image[DeckSlotCount];
            deckClassBadges = new GameObject[DeckSlotCount];
            deckCooldownBadges = new GameObject[DeckSlotCount];
            deckCooldownLabels = new Text[DeckSlotCount];
            deckHeatBadges = new GameObject[DeckSlotCount];
            deckHeatLabels = new Text[DeckSlotCount];
            deckUpgradeBadges = new GameObject[DeckSlotCount];
            deckUpgradeBadgeLabels = new UpgradeArrowGraphic[DeckSlotCount];
            deckFeedbacks = new MenuButtonFeedback[DeckSlotCount];

            // In the 378x58 sprite, each bay's dark interior spans 35px between pillars (x = 8..42 for bay 1,
            // pitch 42px, so bays are centered at x = 25.5 + 42i) and 40px from the caution tape to the
            // bottom ledge (y = 7..46 from the top). The slot covers exactly that interior.
            const float bayLeftPx = 8f;
            const float bayWidthPx = 35f;
            const float bayPitchPx = 42f;
            const float bayTopPx = 7f;
            const float bayHeightPx = 40f;
            float slotWidth = bayWidthPx * bgScale;
            float slotHeight = bayHeightPx * bgScale;
            float slotCenterY = (58f - bayTopPx - bayHeightPx * 0.5f) * bgScale;

            for (int i = 0; i < DeckSlotCount; i++)
            {
                int slotIndex = i;
                float slotCenterX = (bayLeftPx + bayWidthPx * 0.5f + i * bayPitchPx) * bgScale;
                var slotBtn = CreateDeckSlot(slotsContainer.transform, $"DeckSlot_{i + 1}");
                var btnRect = slotBtn.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0f, 0f);
                btnRect.anchorMax = new Vector2(0f, 0f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                btnRect.sizeDelta = new Vector2(slotWidth, slotHeight);
                btnRect.anchoredPosition = new Vector2(slotCenterX, slotCenterY);

                deckButtons[i] = slotBtn;
                deckButtonLabels[i] = slotBtn.GetComponentInChildren<Text>();
                deckButtonImages[i] = slotBtn.GetComponent<Image>();
                deckPortraitImages[i] = slotBtn.transform.Find("OperatorPortrait").GetComponent<Image>();
                deckClassBadges[i] = CreateClassBadge(slotBtn.transform, out deckClassBadgeImages[i], out deckClassIconImages[i]);
                deckCooldownBadges[i] = CreateCooldownBadge(
                    slotBtn.transform,
                    out _,
                    out deckCooldownLabels[i]);
                deckHeatBadges[i] = CreateDeckHeatBadge(
                    slotBtn.transform,
                    out deckHeatLabels[i]);
                deckUpgradeBadges[i] = CreateUpgradeBadge(slotBtn.transform, $"DeckUpgradeBadge_{i + 1}", new Vector2(32f, 18f), new Vector2(-4f, -4f));
                deckUpgradeBadgeLabels[i] = deckUpgradeBadges[i].GetComponentInChildren<UpgradeArrowGraphic>();
                slotBtn.onClick.AddListener(() => SelectDeckSlot(slotIndex));
                var dragHandler = slotBtn.gameObject.AddComponent<DeckSlotDragHandler>();
                dragHandler.Bind(
                    slotIndex,
                    () => CanDragDeckCard(slotIndex),
                    HandleDeckCardDrag,
                    FinishDeckCardDrag);

                // No press-shrink on deck slots so it doesn't fight with dragging.
                deckFeedbacks[i] = AttachFeedback(slotBtn, Color.white, 1.04f);
                deckFeedbacks[i].pressScale = 1f;

                slotBtn.interactable = false;
            }

            CreateTrashDropZone(bar.transform);
            CreatePreviewPathButton(bar.transform);
            CreateBottomRightPanel(bar.transform, bgScale);
        }

        // Bottom Right Panel BG is 140x113 px per frame: a gauge housing on top (x = 47..139, y = 0..55 from
        // the top) over a full-width base the same 57-58px height as the Deck BG, so it uses the deck's scale.
        // It is parented to the deck bar so it slides in with it. Screens on the sprite (px, from the top-left):
        //   speed screen  x = 51..138, y = 26..51  -> 1x / 2x / 3x buttons
        //   left screen   x = 20..75,  y = 70..93  -> squad count
        //   hazard screen x = 80..136, y = 64..101 -> Start Wave button
        private const float BottomRightPanelWidthPx = 140f;
        private const float BottomRightPanelHeightPx = 113f;

        private void CreateBottomRightPanel(Transform deckBar, float pxScale)
        {
            var panel = new GameObject("BottomRightPanel", typeof(RectTransform));
            panel.transform.SetParent(deckBar, false);
            bottomRightPanelRect = panel.GetComponent<RectTransform>();
            SetPosition(bottomRightPanelRect, Vector2.zero, new Vector2(1f, 0f),
                new Vector2(BottomRightPanelWidthPx * pxScale, BottomRightPanelHeightPx * pxScale), new Vector2(1f, 0f));

            if (HasAnyBottomRightSprites())
            {
                bottomRightBgFromImage = CreateBottomRightBgLayer(panel.transform, "BottomRightBgFrom");
                bottomRightBgToImage = CreateBottomRightBgLayer(panel.transform, "BottomRightBgTo");
            }
            else
            {
                // Sprite missing: fall back to the kit's dark panel so the controls stay readable.
                panel.AddComponent<Image>().raycastTarget = false;
                ApplyPanelContainer(panel, PanelContainerType.Dark);
            }

            // Converts a point on the sprite (px from its top-left) to a position anchored at the panel's bottom-right.
            Vector2 PanelPx(float x, float yFromTop) => new Vector2(
                (x - BottomRightPanelWidthPx) * pxScale,
                (BottomRightPanelHeightPx - yFromTop) * pxScale);

            // Start Wave button fills the hazard-striped screen
            startWaveButton = CreateButton(panel.transform, "StartWaveButton", "START\nWAVE", new Vector2(152f, 100f), ButtonContainerType.Primary);
            startWaveButtonText = startWaveButton.GetComponentInChildren<Text>();
            SetPosition(startWaveButton.GetComponent<RectTransform>(), PanelPx(108f, 82.5f), new Vector2(1f, 0f),
                new Vector2(152f, 100f), new Vector2(0.5f, 0.5f));
            ApplyButtonContainer(startWaveButton, ButtonContainerType.Primary);
            startWaveButton.onClick.AddListener(StartWave);
            startWaveFeedback = AttachFeedback(startWaveButton, AccentColor, 1.06f, () => PlaySfx(clickClip, 1.1f));

            squadCountText = CreateText(panel.transform, "SquadCountText", "SQUAD 0/8", 16, TextAnchor.MiddleCenter);
            SetPosition(squadCountText.GetComponent<RectTransform>(), PanelPx(47.5f, 81.5f), new Vector2(1f, 0f),
                new Vector2(150f, 60f), new Vector2(0.5f, 0.5f));
            squadCountText.raycastTarget = false;

            var buttonsRoot = new GameObject("SpeedButtons", typeof(RectTransform));
            buttonsRoot.transform.SetParent(panel.transform, false);
            var buttonsRect = buttonsRoot.GetComponent<RectTransform>();
            SetPosition(buttonsRect, PanelPx(94.5f, 38.5f), new Vector2(1f, 0f), new Vector2(240f, 48f), new Vector2(0.5f, 0.5f));
            var layout = buttonsRoot.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            speedButtons = new Button[3];
            speedFeedbacks = new MenuButtonFeedback[3];
            for (int i = 0; i < speedButtons.Length; i++)
            {
                float speed = i + 1;
                var button = CreateButton(buttonsRoot.transform, $"Speed{speed:0}xButton", $"{speed:0}x", new Vector2(72f, 44f), ButtonContainerType.Dark);
                int index = i;
                button.onClick.AddListener(() => SetGameSpeed(index + 1));
                speedButtons[i] = button;
                speedFeedbacks[i] = AttachFeedback(button, AccentColor, 1.03f, () =>
                {
                    SetGameSpeed(index + 1);
                    PlayClick();
                });
            }

            UpdateSpeedSelection(gameManager != null ? gameManager.GameSpeed : 1f);
        }

        private static Image CreateBottomRightBgLayer(Transform parent, string objectName)
        {
            var layer = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(parent, false);
            Stretch(layer.GetComponent<RectTransform>());
            Image image = layer.GetComponent<Image>();
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
            return image;
        }

        // Sets the speed animation set (0 = 1x, 1 = 2x, 2 = 3x). The first call snaps; later calls crossfade
        // smoothly over 0.2s while continuing the 12 fps animation loop on unscaled time.
        private void SetBottomRightBgSpeed(int speedIndex)
        {
            if (bottomRightBgToImage == null || !HasAnyBottomRightSprites()) return;

            speedIndex = Mathf.Clamp(speedIndex, 0, 2);
            if (bottomRightBgToSpeed == speedIndex && bottomRightBgFade >= 1f && bottomRightBgAnimFrame >= 0) return;

            if (bottomRightBgAnimFrame < 0)
            {
                bottomRightBgFromSpeed = speedIndex;
                bottomRightBgToSpeed = speedIndex;
                bottomRightBgFade = 1f;
                bottomRightBgAnimFrame = 0;
                Sprite initialSprite = GetBottomRightSprite(speedIndex, 0);
                if (initialSprite != null)
                {
                    if (bottomRightBgFromImage != null)
                    {
                        bottomRightBgFromImage.sprite = initialSprite;
                        SetImageAlpha(bottomRightBgFromImage, 1f);
                    }
                    if (bottomRightBgToImage != null)
                    {
                        bottomRightBgToImage.sprite = initialSprite;
                        SetImageAlpha(bottomRightBgToImage, 1f);
                    }
                }
            }
            else
            {
                // Mid-fade, start from whichever speed is currently more visible.
                bottomRightBgFromSpeed = bottomRightBgFade >= 0.5f
                    ? bottomRightBgToSpeed
                    : bottomRightBgFromSpeed;
                bottomRightBgToSpeed = speedIndex;
                bottomRightBgFade = 0f;
                if (bottomRightBgToImage != null)
                {
                    SetImageAlpha(bottomRightBgToImage, 0f);
                }
            }
        }

        private Sprite GetBottomRightSprite(int speedIndex, int animIndex)
        {
            Sprite[] set = speedIndex switch
            {
                0 => bottomRightBg1xSprites,
                1 => bottomRightBg2xSprites,
                2 => bottomRightBg3xSprites,
                _ => null
            };

            if (set != null && set.Length > 0)
            {
                return set[animIndex % set.Length];
            }

            if (bottomRightBgSprites != null && bottomRightBgSprites.Length > 0)
            {
                int frame = Mathf.Clamp(speedIndex, 0, bottomRightBgSprites.Length - 1);
                return bottomRightBgSprites[frame];
            }

            return null;
        }

        private bool HasAnyBottomRightSprites()
        {
            return (bottomRightBg1xSprites != null && bottomRightBg1xSprites.Length > 0) ||
                   (bottomRightBg2xSprites != null && bottomRightBg2xSprites.Length > 0) ||
                   (bottomRightBg3xSprites != null && bottomRightBg3xSprites.Length > 0) ||
                   (bottomRightBgSprites != null && bottomRightBgSprites.Length > 0);
        }

        private static Sprite[] LoadAndSortSprites(string resourcePath)
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites != null && sprites.Length > 1)
            {
                System.Array.Sort(sprites, (a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return -1;
                    if (b == null) return 1;
                    return GetSpriteIndex(a).CompareTo(GetSpriteIndex(b));
                });
            }
            return sprites;
        }

        private static int GetSpriteIndex(Sprite s)
        {
            if (s == null) return 0;
            int idx = s.name.LastIndexOf('_');
            if (idx >= 0 && int.TryParse(s.name.Substring(idx + 1), out int num))
                return num;
            return 0;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            Color c = image.color;
            c.a = alpha;
            image.color = c;
        }

        private void CreateTrashDropZone(Transform parent)
        {
            var zone = new GameObject("TrashDropZone", typeof(RectTransform), typeof(Image), typeof(Button));
            zone.transform.SetParent(parent, false);
            trashDropZone = zone.GetComponent<RectTransform>();
            // Aligned precisely to the discard monitor screen on the Deck BG sprite sheet
            // Sprite discard screen: x=345..373 (center ~359), y=10..45 from top (center ~30.5 from bottom)
            // At scale (170/58): Center X = 1052.2f, Center Y = 89.4f, inner screen ~84x102f
            SetPosition(trashDropZone, new Vector2(1052f, 89f), new Vector2(0f, 0f),
                new Vector2(80f, 98f), new Vector2(0.5f, 0.5f));

            var image = zone.GetComponent<Image>();
            image.color = new Color(0.10f, 0.05f, 0.07f, 0.25f);
            image.raycastTarget = true;

            var icon = new GameObject("TrashIcon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(zone.transform, false);
            var iconRect = icon.GetComponent<RectTransform>();
            // icon_discard is 16x16 pixel art: draw it at exactly 2x in its own colours, so it stays crisp
            SetPosition(iconRect, new Vector2(0f, 10f), new Vector2(0.5f, 0.5f),
                new Vector2(32f, 32f), new Vector2(0.5f, 0.5f));
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = CreateTrashIconSprite();
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            // Size 11 needed ~77 px for "DISCARD" and clipped the last letter; 10 fits the 80 px screen
            var discardLabel = CreateText(zone.transform, "DiscardLabel", "DISCARD", 10, TextAnchor.MiddleCenter);
            discardLabel.color = new Color(1f, 0.78f, 0.78f, 1f);
            discardLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            discardLabel.raycastTarget = false;
            SetPosition(discardLabel.GetComponent<RectTransform>(), new Vector2(0f, -24f), new Vector2(0.5f, 0.5f),
                new Vector2(80f, 18f), new Vector2(0.5f, 0.5f));

            var button = zone.GetComponent<Button>();
            button.interactable = false;
            AttachFeedback(button, new Color(1f, 0.3f, 0.35f, 1f), 1.04f);
        }

        private void CreatePathPreviewOverlay(Transform root)
        {
            pathPreviewOverlay = GameplayPathPreviewOverlay.Create(
                root,
                canvas,
                gridManager,
                stageBootstrapper != null ? stageBootstrapper.stageData : null);
        }

        private void CreatePreviewPathButton(Transform parent)
        {
            previewPathButton = CreateButton(parent, "PreviewPathButton", "PREVIEW\nPATH", new Vector2(110f, 52f), ButtonContainerType.Dark);
            SetPosition(
                previewPathButton.GetComponent<RectTransform>(),
                new Vector2(1125f, 89f),
                new Vector2(0f, 0f),
                new Vector2(110f, 52f),
                new Vector2(0f, 0.5f));

            previewPathButtonText = previewPathButton.GetComponentInChildren<Text>();
            if (previewPathButtonText != null)
            {
                previewPathButtonText.fontSize = 14;
                previewPathButtonText.lineSpacing = 0.9f;
                previewPathButtonText.color = TextDim;
            }

            previewPathFeedback = AttachFeedback(previewPathButton, AccentColor, 1.05f, () => PlaySfx(clickClip, 1.1f));
            previewPathButton.onClick.AddListener(TogglePathPreview);
            previewPathRestColor = previewPathButton.targetGraphic != null ? previewPathButton.targetGraphic.color : Color.white;
        }

        /// <summary>Greys PREVIEW PATH out (and blocks P) on the tutorial stage, where Coalesce reveals the route.</summary>
        private void RefreshPreviewPathLock()
        {
            bool locked = !TutorialDirector.IsPathPreviewAllowed;
            if (previewPathButton == null || locked == previewPathLocked) return;

            if (locked && pathPreviewOverlay != null && pathPreviewOverlay.IsVisible) TogglePathPreview();
            previewPathLocked = locked;
            previewPathButton.interactable = !locked;
            UpdatePathPreviewButtonVisuals(false);
        }

        /// <summary>
        /// Screen rect of the free strip on the deck bar between the PREVIEW PATH button (or the discard screen,
        /// if that button is missing) and the bottom-right panel, at the discard screen's height. The card draft
        /// puts its RESUME button here while minimized. The bottom-right panel is anchored to the right edge, so
        /// the strip's width depends on the aspect ratio.
        /// </summary>
        public bool TryGetDeckBarFreeSlot(out Rect screenRect)
        {
            screenRect = default;
            RectTransform left = previewPathButton != null ? (RectTransform)previewPathButton.transform : trashDropZone;
            if (left == null || trashDropZone == null || bottomRightPanelRect == null || canvas == null) return false;

            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Rect leftRect = GetScreenRect(left, cam);
            Rect discardRect = GetScreenRect(trashDropZone, cam);
            Rect panelRect = GetScreenRect(bottomRightPanelRect, cam);
            if (panelRect.xMin <= leftRect.xMax) return false;

            screenRect = Rect.MinMaxRect(leftRect.xMax, discardRect.yMin, panelRect.xMin, discardRect.yMax);
            return true;
        }

        private static Rect GetScreenRect(RectTransform rect, Camera cam)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private void EnsurePathPreviewOverlay()
        {
            if (stageBootstrapper == null) stageBootstrapper = FindAnyObjectByType<StageBootstrapper>();
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>();

            StageData currentStage = stageBootstrapper != null && stageBootstrapper.stageData != null
                ? stageBootstrapper.stageData
                : (gameManager != null ? gameManager.CurrentStage : null);

            if (pathPreviewOverlay != null)
            {
                if (currentStage != null)
                {
                    pathPreviewOverlay.SetStage(currentStage, gridManager);
                }
                return;
            }
            if (canvas == null) return;

            pathPreviewOverlay = GameplayPathPreviewOverlay.Create(canvas.transform, canvas, gridManager, currentStage);
            if (topBarRect != null)
            {
                int topBarIndex = topBarRect.GetSiblingIndex();
                pathPreviewOverlay.transform.SetSiblingIndex(Mathf.Max(0, topBarIndex));
            }
        }

        public void TogglePathPreview()
        {
            if (previewPathLocked) return;
            EnsurePathPreviewOverlay();
            if (pathPreviewOverlay == null) return;

            pathPreviewOverlay.Toggle();
            UpdatePathPreviewButtonVisuals(pathPreviewOverlay.IsVisible);
        }

        private void UpdatePathPreviewButtonVisuals(bool active)
        {
            if (previewPathButton == null) return;

            ApplyButtonContainer(previewPathButton, active ? ButtonContainerType.Primary : ButtonContainerType.Dark);
            if (previewPathFeedback != null)
            {
                previewPathFeedback.glowAlways = active;
                previewPathFeedback.glowColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 1f);
                previewPathFeedback.SetBaseColor(previewPathLocked ? WaitingColor : previewPathRestColor);
            }
            if (previewPathButtonText != null)
            {
                previewPathButtonText.color = previewPathLocked ? WaitingColor : active ? Color.white : TextDim;
            }
        }

        /// <summary>The baked discard confirmation (Resources, so a build can load it). Missing = built in code.</summary>
        public const string DiscardPanelPrefabAssetPath = "Assets/Resources/UI/Prefabs/DiscardConfirmationPanel.prefab";
        private const string DiscardPanelPrefabResourcePath = "UI/Prefabs/DiscardConfirmationPanel";

        private void CreateDiscardConfirmationPanel(Transform root)
        {
            // Layout from the baked prefab when there is one (TRASH TD > UI > Bake Discard Confirmation Prefab), otherwise
            // built in code; the two buttons are found by name and wired below.
            Button confirm = null;
            Button cancel = null;
            GameObject panelObject = null;
            GameObject prefab = Resources.Load<GameObject>(DiscardPanelPrefabResourcePath);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, root, false);
                instance.name = "DiscardConfirmationPanel";
                if (TryResolveDiscardPanel(instance, out confirm, out cancel, out string missing))
                {
                    panelObject = instance;
                }
                else
                {
                    Debug.LogError("GameplayHUDUI: the baked discard confirmation (" + DiscardPanelPrefabAssetPath + ") is missing " + missing +
                                   ". Building it in code instead; rebake it with TRASH TD > UI > Bake Discard Confirmation Prefab.");
                    instance.SetActive(false);
                    Destroy(instance);
                }
            }
            if (panelObject == null)
            {
                panelObject = BuildDiscardPanelLayout(root);
                TryResolveDiscardPanel(panelObject, out confirm, out cancel, out _);
            }

            discardConfirmationPanel = panelObject;
            StyleButton(confirm, ButtonContainerType.Danger, LifeColor, 1.05f, ConfirmDiscard);
            StyleButton(cancel, ButtonContainerType.Dark, TextDim, 1.05f, CancelDiscard);
            discardConfirmationPanel.SetActive(false);
        }

        /// <summary>Builds the discard confirmation's layout in code under <paramref name="root"/>, for the bake command and the tests.</summary>
        public static GameObject BuildDiscardPanelForPrefab(Transform root) => BuildDiscardPanelLayout(root);

        /// <summary>True when <paramref name="candidate"/> has every named part the HUD binds to; otherwise lists what is missing.</summary>
        public static bool ValidateDiscardPanel(GameObject candidate, out string missing) => TryResolveDiscardPanel(candidate, out _, out _, out missing);

        private static bool TryResolveDiscardPanel(GameObject root, out Button confirm, out Button cancel, out string missing)
        {
            var names = new UiPrefabLookup(root);
            names.Find<RectTransform>("DiscardCard");
            names.Find<Text>("Message"); // ShowDiscardConfirmation fills it in by the path "DiscardCard/Message"
            confirm = names.Find<Button>("ConfirmDiscardButton");
            cancel = names.Find<Button>("CancelDiscardButton");
            missing = names.Missing;
            return names.IsComplete;
        }

        private static GameObject BuildDiscardPanelLayout(Transform root)
        {
            var panelObject = new GameObject("DiscardConfirmationPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panelObject.transform.SetParent(root, false);
            Stretch(panelObject.GetComponent<RectTransform>());
            panelObject.GetComponent<Image>().color = new Color(0.01f, 0.012f, 0.02f, 0.78f);

            var card = new GameObject("DiscardCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(panelObject.transform, false);
            var cardRect = card.GetComponent<RectTransform>();
            SetPosition(cardRect, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(480f, 260f), new Vector2(0.5f, 0.5f));
            ApplyModalCard(card);
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = AccentColor;
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var title = CreateText(card.transform, "Title", "DISCARD OPERATOR?", 24, TextAnchor.MiddleCenter);
            SetPosition(title.GetComponent<RectTransform>(), new Vector2(0f, 74f), new Vector2(0.5f, 0.5f),
                new Vector2(430f, 40f), new Vector2(0.5f, 0.5f));
            var message = CreateText(card.transform, "Message", string.Empty, 17, TextAnchor.MiddleCenter);
            message.color = TextDim;
            SetPosition(message.GetComponent<RectTransform>(), new Vector2(0f, 28f), new Vector2(0.5f, 0.5f),
                new Vector2(430f, 32f), new Vector2(0.5f, 0.5f));

            var confirm = CreateButton(card.transform, "ConfirmDiscardButton", "DISCARD", new Vector2(175f, 52f), ButtonContainerType.Danger);
            SetPosition(confirm.GetComponent<RectTransform>(), new Vector2(-100f, -60f), new Vector2(0.5f, 0.5f),
                new Vector2(175f, 52f), new Vector2(0.5f, 0.5f));

            var cancel = CreateButton(card.transform, "CancelDiscardButton", "CANCEL", new Vector2(175f, 52f), ButtonContainerType.Dark);
            SetPosition(cancel.GetComponent<RectTransform>(), new Vector2(100f, -60f), new Vector2(0.5f, 0.5f),
                new Vector2(175f, 52f), new Vector2(0.5f, 0.5f));

            return panelObject;
        }

        private void ShowDiscardConfirmation(int slotIndex, DraftCard card)
        {
            if (discardConfirmationPanel == null || card == null || card.operatorData == null) return;

            pendingDiscardSlot = slotIndex;
            pendingDiscardCard = card;
            var message = discardConfirmationPanel.transform.Find("DiscardCard/Message")?.GetComponent<Text>();
            if (message != null)
                message.text = $"Discard {card.operatorData.operatorName} from your deck?";
            discardConfirmationPanel.SetActive(true);
        }

        private void ConfirmDiscard()
        {
            if (playerDeck != null && pendingDiscardSlot >= 0 &&
                playerDeck.GetCard(pendingDiscardSlot) == pendingDiscardCard)
            {
                playerDeck.RemoveCard(pendingDiscardSlot);
                AudioManager.Instance?.PlaySfx(SfxId.UiCardDiscard, sfxVolume);
            }

            CancelDiscard();
        }

        private void CancelDiscard()
        {
            pendingDiscardCard = null;
            pendingDiscardSlot = -1;
            if (discardConfirmationPanel != null)
                discardConfirmationPanel.SetActive(false);
        }

        private void SetGameSpeed(int speed)
        {
            if (gameManager == null) return;

            gameManager.SetGameSpeed(speed);
            UpdateSpeedSelection(speed);
        }

        private void UpdateSpeedSelection(float speed)
        {
            if (speedButtons == null) return;

            int selectedIndex = Mathf.Clamp(Mathf.RoundToInt(speed) - 1, 0, speedButtons.Length - 1);
            currentSpeedSelectionIndex = selectedIndex;
            SetBottomRightBgSpeed(selectedIndex);
            for (int i = 0; i < speedButtons.Length; i++)
            {
                bool selected = i == selectedIndex;
                ApplyButtonContainer(speedButtons[i], selected ? ButtonContainerType.Primary : ButtonContainerType.Dark);
                speedFeedbacks[i].restScale = selected ? 1.12f : 1f;
                speedFeedbacks[i].glowAlways = selected;
                speedFeedbacks[i].glowColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 1f);
                var label = speedButtons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = selected ? Color.white : TextDim;
                    label.fontSize = selected ? 21 : 18;
                }
            }
        }
    }
}
