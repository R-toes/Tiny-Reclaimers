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
    // The phase banner, the environment briefing and the pause panel (building and showing them).
    public partial class GameplayHUDUI
    {
        /// <summary>
        /// Phase / wave-start alert band across the screen: dark band, marching diagonal stripes top and bottom
        /// (tinted per announcement), siren lights at both ends, title (+ glitch ghost), subtitle and an enemy
        /// icon row. See ShowPhaseBanner / ShowWaveBanner. Never takes clicks.
        /// </summary>
        private void CreatePhaseBanner(Transform root)
        {
            const float bandHeight = 176f;
            const float stripeHeight = 12f;

            var bannerRoot = new GameObject("PhaseBanner", typeof(RectTransform), typeof(CanvasGroup));
            bannerRoot.transform.SetParent(root, false);
            phaseBannerRect = bannerRoot.GetComponent<RectTransform>();
            phaseBannerRect.anchorMin = new Vector2(0f, 0.62f);
            phaseBannerRect.anchorMax = new Vector2(1f, 0.62f);
            phaseBannerRect.pivot = new Vector2(0.5f, 0.5f);
            phaseBannerRect.sizeDelta = new Vector2(0f, bandHeight);
            phaseBannerRect.anchoredPosition = Vector2.zero;

            phaseBannerGroup = bannerRoot.GetComponent<CanvasGroup>();
            phaseBannerGroup.alpha = 0f;
            phaseBannerGroup.interactable = false;
            phaseBannerGroup.blocksRaycasts = false;

            var band = new GameObject("Band", typeof(RectTransform), typeof(Image));
            band.transform.SetParent(bannerRoot.transform, false);
            Stretch(band.GetComponent<RectTransform>());
            var bandImage = band.GetComponent<Image>();
            bandImage.color = new Color(0.02f, 0.025f, 0.04f, 0.88f);
            bandImage.raycastTarget = false;

            phaseBannerStripes = new RawImage[2];
            for (int i = 0; i < 2; i++)
            {
                var stripe = new GameObject(i == 0 ? "TopStripes" : "BottomStripes", typeof(RectTransform), typeof(RawImage));
                stripe.transform.SetParent(bannerRoot.transform, false);
                var stripeRect = stripe.GetComponent<RectTransform>();
                stripeRect.anchorMin = new Vector2(0f, i == 0 ? 1f : 0f);
                stripeRect.anchorMax = new Vector2(1f, i == 0 ? 1f : 0f);
                stripeRect.pivot = new Vector2(0.5f, i == 0 ? 1f : 0f);
                stripeRect.sizeDelta = new Vector2(0f, stripeHeight);
                stripeRect.anchoredPosition = Vector2.zero;
                RawImage raw = stripe.GetComponent<RawImage>();
                raw.texture = GetBannerStripeTexture();
                // One 16-texel stripe period per 24 canvas units, scrolled in PhaseBannerRoutine
                raw.uvRect = new Rect(0f, 0f, 1920f / 24f, stripeHeight / 24f);
                raw.raycastTarget = false;
                phaseBannerStripes[i] = raw;
            }

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(bannerRoot.transform, false);
            phaseBannerContent = content.GetComponent<RectTransform>();
            Stretch(phaseBannerContent);

            // Siren lights at both ends of the title row
            Sprite warning = LoadIconSprite("icon_warning");
            phaseBannerLights = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                phaseBannerLights[i] = CreateIconImage(content.transform, i == 0 ? "SirenLeft" : "SirenRight", warning, Color.white,
                    new Vector2(side * 520f, 18f), new Vector2(0.5f, 0.5f), new Vector2(48f, 48f), new Vector2(0.5f, 0.5f));
                phaseBannerLights[i].raycastTarget = false;
                phaseBannerLights[i].preserveAspect = true;
                phaseBannerLights[i].gameObject.SetActive(false);
            }

            phaseBannerTitleGhost = CreateText(content.transform, "BannerTitleGhost", "", 56, TextAnchor.MiddleCenter);
            SetPosition(phaseBannerTitleGhost.GetComponent<RectTransform>(), new Vector2(-3f, 18f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 70f), new Vector2(0.5f, 0.5f));
            phaseBannerTitleGhost.raycastTarget = false;
            phaseBannerTitleGhost.gameObject.SetActive(false);

            phaseBannerTitle = CreateText(content.transform, "BannerTitle", "", 56, TextAnchor.MiddleCenter);
            SetPosition(phaseBannerTitle.GetComponent<RectTransform>(), new Vector2(0f, 18f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 70f), new Vector2(0.5f, 0.5f));
            phaseBannerTitle.raycastTarget = false;
            var titleShadow = phaseBannerTitle.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            titleShadow.effectDistance = new Vector2(0f, -4f);

            phaseBannerSubtitle = CreateText(content.transform, "BannerSubtitle", "", 18, TextAnchor.MiddleCenter);
            phaseBannerSubtitle.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            SetPosition(phaseBannerSubtitle.GetComponent<RectTransform>(), new Vector2(0f, -30f), new Vector2(0.5f, 0.5f), new Vector2(1200f, 28f), new Vector2(0.5f, 0.5f));
            phaseBannerSubtitle.raycastTarget = false;

            var icons = new GameObject("EnemyIcons", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            icons.transform.SetParent(content.transform, false);
            phaseBannerIcons = icons.GetComponent<RectTransform>();
            SetPosition(phaseBannerIcons, new Vector2(0f, -48f), new Vector2(0.5f, 0.5f), new Vector2(0f, 34f), new Vector2(0.5f, 0.5f));
            var iconsLayout = icons.GetComponent<HorizontalLayoutGroup>();
            iconsLayout.spacing = 22f;
            iconsLayout.childAlignment = TextAnchor.MiddleCenter;
            iconsLayout.childControlWidth = true;
            iconsLayout.childControlHeight = true;
            iconsLayout.childForceExpandWidth = false;
            iconsLayout.childForceExpandHeight = false;
            icons.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            icons.SetActive(false);
        }

        private void CreateEnvironmentBriefing(Transform root)
        {
            environmentBriefingPanel = new GameObject(
                "EnvironmentBriefing",
                typeof(RectTransform),
                typeof(Image),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            environmentBriefingPanel.transform.SetParent(root, false);
            Stretch(environmentBriefingPanel.GetComponent<RectTransform>());
            environmentBriefingPanel.GetComponent<Image>().color = new Color(0.01f, 0.015f, 0.025f, 0.82f);

            Canvas overlayCanvas = environmentBriefingPanel.GetComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 300;

            GameObject card = new GameObject("BriefingCard", typeof(RectTransform), typeof(Image), typeof(Outline));
            card.transform.SetParent(environmentBriefingPanel.transform, false);
            environmentBriefingCardRect = card.GetComponent<RectTransform>();
            SetPosition(environmentBriefingCardRect, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(740f, 440f), new Vector2(0.5f, 0.5f));
            ApplyModalCard(card);
            Outline cardOutline = card.GetComponent<Outline>();
            cardOutline.effectColor = new Color(WaveColor.r, WaveColor.g, WaveColor.b, 0.8f);
            cardOutline.effectDistance = new Vector2(3f, -3f);

            environmentBriefingTitle = CreateText(card.transform, "Title", "ENVIRONMENTAL EFFECT", 30, TextAnchor.MiddleCenter);
            environmentBriefingTitle.color = WaveColor;
            SetPosition(environmentBriefingTitle.rectTransform, new Vector2(0f, -40f), new Vector2(0.5f, 1f), new Vector2(660f, 52f), new Vector2(0.5f, 1f));
            environmentBriefingTitle.raycastTarget = false;

            environmentBriefingDescription = CreateText(card.transform, "Description", string.Empty, 18, TextAnchor.MiddleCenter);
            environmentBriefingDescription.fontStyle = FontStyle.Normal;
            environmentBriefingDescription.horizontalOverflow = HorizontalWrapMode.Wrap;
            environmentBriefingDescription.verticalOverflow = VerticalWrapMode.Overflow;
            environmentBriefingDescription.color = new Color(0.86f, 0.9f, 0.95f, 1f);
            SetPosition(environmentBriefingDescription.rectTransform, new Vector2(0f, -100f), new Vector2(0.5f, 1f), new Vector2(640f, 210f), new Vector2(0.5f, 1f));
            environmentBriefingDescription.raycastTarget = false;

            CreateStage2BriefingItemsDisplay(card.transform);
            CreateStage5BriefingTrafficDisplay(card.transform);

            Button dismissButton = CreateButton(card.transform, "DismissButton", "UNDERSTOOD", new Vector2(250f, 58f), ButtonContainerType.Primary);
            SetPosition(dismissButton.GetComponent<RectTransform>(), new Vector2(0f, 32f), new Vector2(0.5f, 0f), null, new Vector2(0.5f, 0f));
            StyleButton(dismissButton, ButtonContainerType.Primary, AccentColor, 1.05f);
            dismissButton.onClick.AddListener(DismissEnvironmentBriefing);

            environmentBriefingPanel.SetActive(false);
        }

        private void CreateStage2BriefingItemsDisplay(Transform parent)
        {
            stage2ThrownItemsRoot = new GameObject("Stage2ItemsRoot", typeof(RectTransform));
            stage2ThrownItemsRoot.transform.SetParent(parent, false);
            RectTransform rootRect = stage2ThrownItemsRoot.GetComponent<RectTransform>();
            SetPosition(rootRect, new Vector2(0f, -178f), new Vector2(0.5f, 1f), new Vector2(780f, 245f), new Vector2(0.5f, 1f));

            // Create 3 item cards side by side: Glass (-260), Metal (0), Tools (+260)
            stage2GlassIcon = CreateBriefingItemCard(stage2ThrownItemsRoot.transform, -260f, "Glass", "GLASS", new Color(0.4f, 0.9f, 1f, 1f), "<b>Weak DoT Damage</b>\nInflicts continuous bleeding damage over 5s.");
            stage2MetalIcon = CreateBriefingItemCard(stage2ThrownItemsRoot.transform, 0f, "Metal", "METAL", new Color(1f, 0.8f, 0.3f, 1f), "<b>Direct True Damage</b>\nDeals heavy burst damage upon impact.");
            stage2ToolsIcon = CreateBriefingItemCard(stage2ThrownItemsRoot.transform, 260f, "Tools", "TOOLS", new Color(1f, 0.45f, 0.45f, 1f), "<b>True Damage + Stun</b>\nDeals direct damage and stuns operators for 2s.");

            stage2ThrownItemsRoot.SetActive(false);
        }

        private void CreateStage5BriefingTrafficDisplay(Transform parent)
        {
            stage5TrafficRoot = new GameObject("Stage5TrafficRoot", typeof(RectTransform));
            stage5TrafficRoot.transform.SetParent(parent, false);
            RectTransform rootRect = stage5TrafficRoot.GetComponent<RectTransform>();
            SetPosition(rootRect, new Vector2(0f, -112f), new Vector2(0.5f, 1f), new Vector2(780f, 350f), new Vector2(0.5f, 1f));

            // Single card container
            GameObject box = new GameObject("Stage5TrafficCard", typeof(RectTransform), typeof(Image), typeof(Outline));
            box.transform.SetParent(stage5TrafficRoot.transform, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            SetPosition(boxRect, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(780f, 350f), new Vector2(0.5f, 0.5f));

            Image boxImg = box.GetComponent<Image>();
            boxImg.color = new Color(0.04f, 0.06f, 0.09f, 0.94f);

            Outline boxOutline = box.GetComponent<Outline>();
            boxOutline.effectColor = new Color(1f, 0.8f, 0.25f, 0.6f);
            boxOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // 3-Vehicle lineup: Left, Center (Red Car), Right
            stage5VehicleLeftIcon = CreateIconImage(box.transform, "VehicleLeft", null, Color.white, new Vector2(-175f, 114f), new Vector2(0.5f, 0.5f), new Vector2(130f, 65f), new Vector2(0.5f, 0.5f));
            stage5VehicleLeftIcon.preserveAspect = true;

            stage5VehicleCenterIcon = CreateIconImage(box.transform, "VehicleCenter", null, Color.white, new Vector2(0f, 114f), new Vector2(0.5f, 0.5f), new Vector2(130f, 65f), new Vector2(0.5f, 0.5f));
            stage5VehicleCenterIcon.preserveAspect = true;

            stage5VehicleRightIcon = CreateIconImage(box.transform, "VehicleRight", null, Color.white, new Vector2(175f, 114f), new Vector2(0.5f, 0.5f), new Vector2(130f, 65f), new Vector2(0.5f, 0.5f));
            stage5VehicleRightIcon.preserveAspect = true;

            // Content text (clean single-line alignment across 720px width)
            stage5TrafficBodyText = CreateText(box.transform, "BodyText", "", 13, TextAnchor.MiddleCenter);
            stage5TrafficBodyText.fontStyle = FontStyle.Normal;
            stage5TrafficBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            stage5TrafficBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            stage5TrafficBodyText.lineSpacing = 1.28f;
            stage5TrafficBodyText.color = new Color(0.88f, 0.91f, 0.95f, 1f);
            SetPosition(stage5TrafficBodyText.rectTransform, new Vector2(0f, -42f), new Vector2(0.5f, 0.5f), new Vector2(760f, 220f), new Vector2(0.5f, 0.5f));
            stage5TrafficBodyText.raycastTarget = false;

            stage5TrafficRoot.SetActive(false);
        }

        private Image CreateBriefingItemCard(Transform parent, float xPos, string cardName, string title, Color titleColor, string description)
        {
            GameObject box = new GameObject($"Card_{cardName}", typeof(RectTransform), typeof(Image), typeof(Outline));
            box.transform.SetParent(parent, false);
            RectTransform boxRect = box.GetComponent<RectTransform>();
            SetPosition(boxRect, new Vector2(xPos, 0f), new Vector2(0.5f, 0.5f), new Vector2(245f, 240f), new Vector2(0.5f, 0.5f));

            Image boxImg = box.GetComponent<Image>();
            boxImg.color = new Color(0.04f, 0.06f, 0.09f, 0.92f);

            Outline boxOutline = box.GetComponent<Outline>();
            boxOutline.effectColor = new Color(titleColor.r, titleColor.g, titleColor.b, 0.55f);
            boxOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Sprite preview container
            GameObject iconBg = new GameObject("IconBg", typeof(RectTransform), typeof(Image));
            iconBg.transform.SetParent(box.transform, false);
            RectTransform iconBgRect = iconBg.GetComponent<RectTransform>();
            SetPosition(iconBgRect, new Vector2(0f, 62f), new Vector2(0.5f, 0.5f), new Vector2(64f, 64f), new Vector2(0.5f, 0.5f));
            iconBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

            Image iconImg = CreateIconImage(iconBg.transform, "Icon", null, Color.white, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(52f, 52f), new Vector2(0.5f, 0.5f));

            // Title
            Text titleText = CreateText(box.transform, "Title", title, 18, TextAnchor.MiddleCenter);
            titleText.color = titleColor;
            SetPosition(titleText.rectTransform, new Vector2(0f, 12f), new Vector2(0.5f, 0.5f), new Vector2(220f, 26f), new Vector2(0.5f, 0.5f));
            titleText.raycastTarget = false;

            // Description
            Text descText = CreateText(box.transform, "Desc", description, 13, TextAnchor.MiddleCenter);
            descText.fontStyle = FontStyle.Normal;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Overflow;
            descText.lineSpacing = 1.15f;
            descText.color = new Color(0.85f, 0.88f, 0.92f, 1f);
            SetPosition(descText.rectTransform, new Vector2(0f, -50f), new Vector2(0.5f, 0.5f), new Vector2(225f, 90f), new Vector2(0.5f, 0.5f));
            descText.raycastTarget = false;

            return iconImg;
        }

        private IEnumerator ShowEnvironmentBriefingWhenReady()
        {
            if (gameManager == null) yield break;

            while (isActiveAndEnabled && gameManager.CurrentStage == null)
            {
                yield return null;
            }

            if (!isActiveAndEnabled || gameManager.CurrentStage == null) yield break;
            StageData stage = gameManager.CurrentStage;

            // The sandbox shows the briefing for the environment of the map it runs on.
            string environmentStageId = stageBootstrapper != null ? stageBootstrapper.EnvironmentStageId : stage.stageId;
            StageEnvironment environment = stageBootstrapper != null ? stageBootstrapper.Environment : stage.environment;
            string environmentName = stage.mapName;
            if (environmentStageId != stage.stageId)
            {
                StageData environmentStage = Resources.Load<StageData>("Stages/" + environmentStageId);
                if (environmentStage != null) environmentName = environmentStage.mapName;
            }

            if (environment == StageEnvironment.FallingItems)
            {
                if (environmentBriefingCardRect != null)
                {
                    environmentBriefingCardRect.sizeDelta = new Vector2(840f, 570f);
                }
                if (stage5TrafficRoot != null)
                {
                    stage5TrafficRoot.SetActive(false);
                }

                environmentBriefingTitle.text = "HAZARD ALERT";
                SetPosition(environmentBriefingTitle.rectTransform, new Vector2(0f, -36f), new Vector2(0.5f, 1f), new Vector2(700f, 40f), new Vector2(0.5f, 1f));

                environmentBriefingDescription.text =
                    $"<color=#FFD040>{environmentName}</color>\n" +
                    "Enemies are throwing scavenged items across the battlefield every 10 seconds!\n" +
                    "Watch out for the 3 types of thrown debris:";
                SetPosition(environmentBriefingDescription.rectTransform, new Vector2(0f, -86f), new Vector2(0.5f, 1f), new Vector2(760f, 80f), new Vector2(0.5f, 1f));
                environmentBriefingDescription.fontSize = 15;
                environmentBriefingDescription.lineSpacing = 1.15f;

                // Load sprites from stage or hazard
                Sprite[] itemSprites = stage.fallingItemSprites;
                if (itemSprites == null || itemSprites.Length == 0)
                {
                    var hazard = FindAnyObjectByType<TrashTD.Core.GameLoop.Stage2FallingItemsHazard>();
                    if (hazard != null) itemSprites = hazard.ItemSprites;
                }
                if (itemSprites == null || itemSprites.Length == 0)
                {
                    itemSprites = Resources.LoadAll<Sprite>("Levels/Level 2 thrown-items");
                }
#if UNITY_EDITOR
                if (itemSprites == null || itemSprites.Length == 0)
                {
                    var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 2/Level 2 thrown-items.png");
                    if (assets != null)
                    {
                        var list = new List<Sprite>();
                        foreach (var obj in assets)
                        {
                            if (obj is Sprite s) list.Add(s);
                        }
                        if (list.Count > 0)
                        {
                            list.Sort((a, b) => TrashTD.Core.GameLoop.Stage2FallingItemsHazard.ExtractTrailingIndex(a.name).CompareTo(TrashTD.Core.GameLoop.Stage2FallingItemsHazard.ExtractTrailingIndex(b.name)));
                            itemSprites = list.ToArray();
                        }
                    }
                }
#endif

                if (stage2ThrownItemsRoot != null)
                {
                    stage2ThrownItemsRoot.SetActive(true);

                    // Glass: index 0 (0..7)
                    if (stage2GlassIcon != null && itemSprites != null && itemSprites.Length > 0)
                    {
                        stage2GlassIcon.sprite = itemSprites[0];
                        stage2GlassIcon.color = Color.white;
                    }

                    // Metal: index 8 (8..15)
                    if (stage2MetalIcon != null && itemSprites != null && itemSprites.Length > 8)
                    {
                        stage2MetalIcon.sprite = itemSprites[8];
                        stage2MetalIcon.color = Color.white;
                    }

                    // Tools: index 16 (16..23)
                    if (stage2ToolsIcon != null && itemSprites != null && itemSprites.Length > 16)
                    {
                        stage2ToolsIcon.sprite = itemSprites[16];
                        stage2ToolsIcon.color = Color.white;
                    }
                }
            }
            else if (environment == StageEnvironment.Traffic)
            {
                if (environmentBriefingCardRect != null)
                {
                    environmentBriefingCardRect.sizeDelta = new Vector2(840f, 570f);
                }
                if (stage2ThrownItemsRoot != null)
                {
                    stage2ThrownItemsRoot.SetActive(false);
                }

                environmentBriefingTitle.text = "HAZARD ALERT";
                SetPosition(environmentBriefingTitle.rectTransform, new Vector2(0f, -36f), new Vector2(0.5f, 1f), new Vector2(700f, 40f), new Vector2(0.5f, 1f));

                environmentBriefingDescription.text = $"<color=#FFD040>{environmentName}</color>";
                SetPosition(environmentBriefingDescription.rectTransform, new Vector2(0f, -76f), new Vector2(0.5f, 1f), new Vector2(700f, 32f), new Vector2(0.5f, 1f));
                environmentBriefingDescription.fontSize = 17;
                environmentBriefingDescription.lineSpacing = 1f;

                // Load vehicle sprites
                Sprite[] vehicleSprites = stage.trafficVehicleSprites;
                if (vehicleSprites == null || vehicleSprites.Length == 0)
                {
                    var encounter = FindAnyObjectByType<TrashTD.Core.GameLoop.Stage5CarEncounter>();
                    if (encounter != null) vehicleSprites = encounter.VehicleSprites;
                }
                if (vehicleSprites == null || vehicleSprites.Length == 0)
                {
                    vehicleSprites = Resources.LoadAll<Sprite>("Levels/Level 5 vehicles");
                }
#if UNITY_EDITOR
                if (vehicleSprites == null || vehicleSprites.Length == 0)
                {
                    var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/Level 5 vehicles.png");
                    if (assets == null || assets.Length == 0)
                    {
                        assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Levels/Level 5/level 5 vehicles.png");
                    }
                    if (assets != null)
                    {
                        var list = new List<Sprite>();
                        foreach (var obj in assets)
                        {
                            if (obj is Sprite s) list.Add(s);
                        }
                        if (list.Count > 0)
                        {
                            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                            vehicleSprites = list.ToArray();
                        }
                    }
                }
#endif

                if (stage5TrafficRoot != null)
                {
                    stage5TrafficRoot.SetActive(true);

                    if (vehicleSprites != null && vehicleSprites.Length > 0)
                    {
                        // Center is the red car (index 1)
                        if (stage5VehicleCenterIcon != null)
                        {
                            stage5VehicleCenterIcon.sprite = vehicleSprites.Length > 1 ? vehicleSprites[1] : vehicleSprites[0];
                            stage5VehicleCenterIcon.color = Color.white;
                        }

                        // Left is index 0
                        if (stage5VehicleLeftIcon != null)
                        {
                            stage5VehicleLeftIcon.sprite = vehicleSprites[0];
                            stage5VehicleLeftIcon.color = Color.white;
                        }

                        // Right is index 2 (or index 0 if not enough)
                        if (stage5VehicleRightIcon != null)
                        {
                            int rightIdx = vehicleSprites.Length > 2 ? 2 : 0;
                            stage5VehicleRightIcon.sprite = vehicleSprites[rightIdx];
                            stage5VehicleRightIcon.color = Color.white;
                        }
                    }

                    if (stage5TrafficBodyText != null)
                    {
                        stage5TrafficBodyText.text =
                            "Fast Vehicles drive across the 2 middle highway road lanes starting at wave 6.\n" +
                            "They crash into both enemies and operators in their way.\n\n" +
                            "Starts at 20 true damage (+20 every 2 tiles):\n" +
                            "<color=#FFD040>20 → 40 → 60 → 80 → 100 → 120 → 140</color>\n" +
                            "Longer distance = heavier hit when unblocked by an operator.\n\n" +
                            "Hitting an operator slows the car down, then it recovers.\n" +
                            "Stuns the operator for 5 seconds and resets damage to 20.";
                    }
                }
            }
            else
            {
                if (environmentBriefingCardRect != null)
                {
                    environmentBriefingCardRect.sizeDelta = new Vector2(740f, 440f);
                }
                if (stage2ThrownItemsRoot != null)
                {
                    stage2ThrownItemsRoot.SetActive(false);
                }
                if (stage5TrafficRoot != null)
                {
                    stage5TrafficRoot.SetActive(false);
                }
                environmentBriefingTitle.text = "ENVIRONMENTAL EFFECT";
                SetPosition(environmentBriefingTitle.rectTransform, new Vector2(0f, -40f), new Vector2(0.5f, 1f), new Vector2(660f, 52f), new Vector2(0.5f, 1f));
                SetPosition(environmentBriefingDescription.rectTransform, new Vector2(0f, -100f), new Vector2(0.5f, 1f), new Vector2(640f, 210f), new Vector2(0.5f, 1f));
                environmentBriefingDescription.fontSize = 18;
                environmentBriefingDescription.lineSpacing = 1f;

                string effectDescription;
                switch (environment)
                {
                    case StageEnvironment.Sandstorm:
                        effectDescription =
                            "A sandstorm sweeps the battlefield. Attack intervals are 5% shorter, " +
                            "and each attack has a 10% chance to miss.";
                        break;
                    case StageEnvironment.FactoryHeat:
                        effectDescription =
                            "Extreme heat builds up on deployed operators each round. " +
                            "Overheated operators ignite and suffer continuous burn damage until retreated to the bench to cool down.";
                        break;
                    case StageEnvironment.AcidRain:
                        effectDescription =
                            "Acid rain periodically deals 3 true damage to deployed operators " +
                            "and active enemies every 2 seconds while a wave is running. Operators are protected between waves. " +
                            "In heavy downpours, acid pools seep up on random tiles for about 6 seconds and burn anyone standing in them.";
                        break;
                    default:
                        yield break;
                }

                environmentBriefingDescription.text = $"{environmentName}\n\n{effectDescription}";
            }

            environmentBriefingPanel.SetActive(true);
        }

        private void DismissEnvironmentBriefing()
        {
            environmentBriefingDismissed = true;
            if (environmentBriefingPanel != null) environmentBriefingPanel.SetActive(false);
            EnvironmentBriefingDismissed?.Invoke();
        }

        private void CreatePausePanel(Transform root)
        {
            pausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            pausePanel.transform.SetParent(root, false);
            Stretch(pausePanel.GetComponent<RectTransform>());
            pausePanel.GetComponent<Image>().color = new Color(0.01f, 0.012f, 0.02f, 0.86f);

            pauseGroup = pausePanel.GetComponent<CanvasGroup>();
            pauseGroup.alpha = 0f;

            // Centered card: the 256x256 pause menu panel art drawn at 2x, grown by one indicator row (see
            // BuildPauseCardArt). Its header screen spans y 37..101 (title), a divider sits at y 103, and the body
            // screen spans y 104..251 (buttons), with four indicator rows (LED on the left, a slot on the right).
            var card = new GameObject("PauseCard", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(pausePanel.transform, false);
            pauseCard = card.GetComponent<RectTransform>();
            SetPosition(pauseCard, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(PauseArtPixels, PauseCardHeightPx) * PauseCardScale, new Vector2(0.5f, 0.5f));
            if (!BuildPauseCardArt(pauseCard))
            {
                ApplyModalCard(card);
            }

            // Header screen: title and subtitle centred as one block, the subtitle between two short rules
            var title = CreateText(card.transform, "PauseTitle", "PAUSED", 48, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Normal;
            SetPosition(title.GetComponent<RectTransform>(), PausePx(0f, PauseTitleRowPx), new Vector2(0.5f, 1f), new Vector2(400f, 60f), new Vector2(0.5f, 0.5f));
            title.color = AccentColor;
            title.raycastTarget = false;
            var titleShadow = title.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = new Color(0.02f, 0.14f, 0.07f, 1f);
            titleShadow.effectDistance = new Vector2(0f, -4f);

            var subtitle = CreateText(card.transform, "PauseSubtitle", "THE OPERATION WAITS", 12, TextAnchor.MiddleCenter);
            subtitle.fontStyle = FontStyle.Normal;
            SetPosition(subtitle.GetComponent<RectTransform>(), PausePx(0f, PauseSubtitleRowPx), new Vector2(0.5f, 1f), new Vector2(400f, 20f), new Vector2(0.5f, 0.5f));
            subtitle.color = TextDim;
            subtitle.raycastTarget = false;

            float ruleInner = subtitle.preferredWidth * 0.5f + 14f;
            Color ruleColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f);
            foreach (float side in new[] { -1f, 1f })
            {
                CreateIconImage(card.transform, "PauseSubtitleRule", null, ruleColor,
                    PausePx(side * (ruleInner + 18f), PauseSubtitleRowPx), new Vector2(0.5f, 1f), new Vector2(36f, 2f), new Vector2(0.5f, 0.5f));
            }

            // Body screen: one row per indicator light, all the same width on the card's centre line.
            // Row 2 is Retry plus the square Settings button (added by AudioSettingsUI in Awake, see PauseSettingsX).
            var resumeButton = CreatePauseButton(card.transform, "ResumeButton", "RESUME", "icon_play",
                ButtonContainerType.Primary, new Color(0.4f, 1f, 0.65f, 1f), 0f, PauseRow1Px, PauseRowWidth);
            resumeButton.onClick.AddListener(ResumeGame);

            var retryButton = CreatePauseButton(card.transform, "RetryButton", "RETRY", "icon_reroll",
                ButtonContainerType.Secondary, new Color(0.55f, 0.75f, 1f, 1f),
                -(PauseButtonHeight + PauseRowGap) * 0.5f, PauseRow2Px, PauseRowWidth - PauseButtonHeight - PauseRowGap);
            retryButton.onClick.AddListener(RetryStage);

            // Row 3 is the Reclaimers' Intel button (added by ReclaimersIntelUI in Awake).
            var exitButton = CreatePauseButton(card.transform, "ExitButton", "EXIT TO MENU", "icon_exit",
                ButtonContainerType.Danger, new Color(1f, 0.4f, 0.4f, 1f), 0f, PauseRow4Px, PauseRowWidth);
            exitButton.onClick.AddListener(ExitToMenu);

            pausePanel.SetActive(false);
        }

        private Button CreatePauseButton(Transform parent, string objectName, string label, string iconName,
            ButtonContainerType containerType, Color glow, float x, float rowPx, float width)
        {
            Button button = CreateButton(parent, objectName, label, new Vector2(width, PauseButtonHeight), containerType);
            SetPosition(button.GetComponent<RectTransform>(), PausePx(x, rowPx), new Vector2(0.5f, 1f), null, new Vector2(0.5f, 0.5f));
            StyleButton(button, containerType, glow, 1.04f);
            ApplyPauseButtonLook(button, label, iconName);
            return button;
        }

        /// <summary>
        /// Pause-card button look: the frame drawn at the card's 2x pixel scale, and a 2x icon plus label centred
        /// together as one group. A null/empty label gives an icon-only button.
        /// </summary>
        private static void ApplyPauseButtonLook(Button button, string label, string iconName)
        {
            Image frame = button.GetComponent<Image>();
            if (frame != null) frame.pixelsPerUnitMultiplier = 1f / PauseCardScale;

            Text text = button.GetComponentInChildren<Text>();
            bool hasLabel = text != null && !string.IsNullOrEmpty(label);
            if (text != null)
            {
                text.text = hasLabel ? label : string.Empty;
                text.fontSize = 16;
                text.fontStyle = FontStyle.Normal;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.alignment = TextAnchor.MiddleLeft;
                if (text.GetComponent<Shadow>() == null)
                {
                    var shadow = text.gameObject.AddComponent<Shadow>();
                    shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
                    shadow.effectDistance = new Vector2(0f, -2f);
                }
            }

            Sprite icon = LoadIconSprite(iconName);
            if (icon != null && icon.texture != null) icon.texture.filterMode = FilterMode.Point;
            float iconSize = 16f * PauseCardScale;
            float labelWidth = hasLabel ? text.preferredWidth : 0f;
            float gap = icon != null && hasLabel ? 10f : 0f;
            float left = -((icon != null ? iconSize : 0f) + gap + labelWidth) * 0.5f;

            if (icon != null)
            {
                Image iconImage = CreateIconImage(button.transform, "Icon", icon, Color.white,
                    new Vector2(left + iconSize * 0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(iconSize, iconSize), new Vector2(0.5f, 0.5f));
                iconImage.raycastTarget = false;
                left += iconSize + gap;
            }

            if (text != null)
            {
                RectTransform textRect = text.rectTransform;
                textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
                textRect.pivot = new Vector2(0f, 0.5f);
                textRect.anchoredPosition = new Vector2(left, 0f);
                textRect.sizeDelta = new Vector2(labelWidth + 4f, PauseButtonHeight);
            }
        }

        private const string PauseCardSpritePath = "UI/panels/pause menu panel bg";
        private const float PauseArtPixels = 256f;
        private const float PauseCardScale = 2f;

        // The card repeats one 34 px band of the art (y 146..180, holding one LED and one slot) below itself,
        // so the body screen gets a fourth indicator row: 256 + 34 = 290 art px tall.
        private const float PauseBandTopPx = 146f;
        private const float PauseBandBottomPx = 180f;
        private const float PauseCardHeightPx = PauseArtPixels + PauseBandBottomPx - PauseBandTopPx;

        // Pause card layout. Rows are in art pixels from the top: the header block is centred in the header
        // screen (y 37..101); the button rows sit on the body's indicator lights (y 129 / 163 / 197 / 231).
        private const float PauseTitleRowPx = 58f;
        private const float PauseSubtitleRowPx = 86f;
        private const float PauseRow1Px = 129f;
        private const float PauseRow2Px = 163f;
        private const float PauseRow3Px = 197f;
        private const float PauseRow4Px = 231f;
        private const float PauseRowWidth = 324f;     // canvas units: clears the LEDs (x 40) and slots (x 214) evenly
        private const float PauseButtonHeight = 48f;  // 24 art px
        private const float PauseRowGap = 12f;
        private const float PauseSettingsX = (PauseRowWidth - PauseButtonHeight) * 0.5f; // square, right end of row 2

        /// <summary>
        /// Position on the pause card: <paramref name="x"/> in canvas units from the centre line,
        /// <paramref name="yFromTop"/> in pause-art pixels.
        /// </summary>
        private static Vector2 PausePx(float x, float yFromTop) => new Vector2(x, -yFromTop * PauseCardScale);

        /// <summary>
        /// Draws the pause art on the card in three strips: art y 0..180, the 146..180 band again, then 180..256.
        /// Returns false when the texture is missing.
        /// </summary>
        private static bool BuildPauseCardArt(RectTransform card)
        {
            var texture = Resources.Load<Texture2D>(PauseCardSpritePath);
            if (texture == null) return false;
            texture.filterMode = FilterMode.Point;

            card.GetComponent<Image>().color = Color.clear;
            AddPauseArtStrip(card, texture, "PauseArtTop", 0f, PauseBandBottomPx, 0f);
            AddPauseArtStrip(card, texture, "PauseArtBand", PauseBandTopPx, PauseBandBottomPx, PauseBandBottomPx);
            AddPauseArtStrip(card, texture, "PauseArtBottom", PauseBandBottomPx, PauseArtPixels,
                PauseBandBottomPx + PauseBandBottomPx - PauseBandTopPx);
            return true;
        }

        /// <summary>Art rows <paramref name="fromPx"/>..<paramref name="toPx"/> (from the top), placed at <paramref name="cardYPx"/> on the card.</summary>
        private static void AddPauseArtStrip(RectTransform card, Texture2D texture, string name, float fromPx, float toPx, float cardYPx)
        {
            float height = toPx - fromPx;
            Sprite sprite = Sprite.Create(texture, new Rect(0f, texture.height - toPx, texture.width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);

            var strip = new GameObject(name, typeof(RectTransform), typeof(Image));
            strip.transform.SetParent(card, false);
            strip.transform.SetAsFirstSibling();
            var image = strip.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            SetPosition(image.rectTransform, PausePx(0f, cardYPx), new Vector2(0.5f, 1f),
                new Vector2(PauseArtPixels, height) * PauseCardScale, new Vector2(0.5f, 1f));
        }
    }
}
