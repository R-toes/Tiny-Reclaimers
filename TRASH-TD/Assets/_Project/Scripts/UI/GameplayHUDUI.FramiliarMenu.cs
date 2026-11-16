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
    // The Framiliar copy menu, the cooldown toast, deck-card dragging and the upgrade badge.
    public partial class GameplayHUDUI
    {
        private void CreateFramiliarCopySection(Transform parent)
        {
            framiliarCopyMenuRoot = new GameObject("FramiliarCopySection", typeof(RectTransform));
            framiliarCopyMenuRoot.transform.SetParent(parent, false);

            RectTransform menuRect = framiliarCopyMenuRoot.GetComponent<RectTransform>();
            menuRect.anchorMin = new Vector2(0f, 0f);
            menuRect.anchorMax = new Vector2(1f, 1f);
            menuRect.pivot = new Vector2(0.5f, 1f);
            menuRect.offsetMin = Vector2.zero;
            menuRect.offsetMax = Vector2.zero;

            // Subtitle / Header
            framiliarSubtitleText = CreateText(framiliarCopyMenuRoot.transform, "Subtitle", string.Empty, 10, TextAnchor.MiddleCenter);
            framiliarSubtitleText.fontStyle = FontStyle.Bold;
            framiliarSubtitleText.color = new Color(0.25f, 0.95f, 0.85f, 1f);
            SetPosition(framiliarSubtitleText.rectTransform, new Vector2(0f, -102f), new Vector2(0.5f, 1f), new Vector2(264f, 16f), new Vector2(0.5f, 1f));
            framiliarSubtitleText.raycastTarget = false;

            // Empty state message
            framiliarEmptyMessageText = CreateText(framiliarCopyMenuRoot.transform, "EmptyMessage", string.Empty, 11, TextAnchor.MiddleCenter);
            framiliarEmptyMessageText.color = new Color(0.85f, 0.85f, 0.85f, 0.90f);
            SetPosition(framiliarEmptyMessageText.rectTransform, new Vector2(0f, -122f), new Vector2(0.5f, 1f), new Vector2(264f, 20f), new Vector2(0.5f, 1f));
            framiliarEmptyMessageText.raycastTarget = false;
            framiliarEmptyMessageText.gameObject.SetActive(false);

            // Up to 8 copy buttons (arranged dynamically in up to 4 columns)
            framiliarCopyButtons = new Button[DeckSlotCount];
            framiliarCopyLabels = new Text[DeckSlotCount];
            framiliarCopyPortraits = new Image[DeckSlotCount];
            framiliarCopyClassBadges = new GameObject[DeckSlotCount];
            framiliarCopyClassBadgeImages = new Image[DeckSlotCount];
            framiliarCopyClassIconImages = new Image[DeckSlotCount];
            framiliarCopyFeedbacks = new MenuButtonFeedback[DeckSlotCount];

            Vector2 btnSize = new Vector2(54f, 60f);

            for (int i = 0; i < DeckSlotCount; i++)
            {
                int slotIndex = i;
                var slotBtn = CreateButton(framiliarCopyMenuRoot.transform, $"CopySlot_{i + 1}", string.Empty, btnSize, ButtonContainerType.Dark);
                RectTransform btnRect = slotBtn.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f, 1f);
                btnRect.anchorMax = new Vector2(0.5f, 1f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                btnRect.sizeDelta = btnSize;
                btnRect.anchoredPosition = Vector2.zero;

                // Portrait Image
                var portraitGo = new GameObject("OperatorPortrait", typeof(RectTransform), typeof(Image));
                portraitGo.transform.SetParent(slotBtn.transform, false);
                RectTransform portRect = portraitGo.GetComponent<RectTransform>();
                portRect.anchorMin = new Vector2(0.5f, 0.5f);
                portRect.anchorMax = new Vector2(0.5f, 0.5f);
                portRect.pivot = new Vector2(0.5f, 0.5f);
                portRect.sizeDelta = new Vector2(38f, 38f);
                portRect.anchoredPosition = new Vector2(0f, 6f);
                var portImg = portraitGo.GetComponent<Image>();
                portImg.preserveAspect = true;
                portImg.raycastTarget = false;
                framiliarCopyPortraits[i] = portImg;

                // Class Badge (top-left)
                framiliarCopyClassBadges[i] = CreateClassBadge(slotBtn.transform, out framiliarCopyClassBadgeImages[i], out framiliarCopyClassIconImages[i]);
                RectTransform badgeRect = framiliarCopyClassBadges[i].GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0f, 1f);
                badgeRect.anchorMax = new Vector2(0f, 1f);
                badgeRect.pivot = new Vector2(0f, 1f);
                badgeRect.sizeDelta = new Vector2(14f, 14f);
                badgeRect.anchoredPosition = new Vector2(2f, -2f);

                // Label (Stars only)
                var label = slotBtn.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = 11;
                    label.alignment = TextAnchor.MiddleCenter;
                    label.raycastTarget = false;
                    label.color = new Color(1f, 0.78f, 0.2f, 1f);
                    RectTransform lblRect = label.GetComponent<RectTransform>();
                    lblRect.anchorMin = new Vector2(0f, 0f);
                    lblRect.anchorMax = new Vector2(1f, 0f);
                    lblRect.pivot = new Vector2(0.5f, 0f);
                    lblRect.sizeDelta = new Vector2(0f, 16f);
                    lblRect.anchoredPosition = new Vector2(0f, 2f);
                    framiliarCopyLabels[i] = label;
                }

                framiliarCopyButtons[i] = slotBtn;
                framiliarCopyFeedbacks[i] = AttachFeedback(slotBtn, Color.white, 1.06f);
                slotBtn.onClick.AddListener(() => SelectFramiliarCopySlot(slotIndex));
                slotBtn.gameObject.SetActive(false);
            }

            framiliarCopyMenuRoot.SetActive(false);
        }

        public void ShowFramiliarCopyMenu(TrashTD.Operators.FramiliarOperator framiliar)
        {
            if (framiliar == null || framiliarCopyMenuRoot == null || selectedOperatorLabelRoot == null) return;
            activeFramiliarOperator = framiliar;

            GridCell cell = framiliar.DeployedCell;
            if (cell == null && operatorManager != null && operatorManager.GridManager != null)
            {
                cell = operatorManager.GridManager.GetCell(operatorManager.GridManager.WorldToGridPosition(framiliar.transform.position));
            }

            int currentRarity = (int)framiliar.CurrentRarity;
            int currentDeckCount = playerDeck != null && playerDeck.DeckSlots != null ? playerDeck.DeckSlots.Count : 0;

            if (lastFramiliarMenuOperator == framiliar &&
                lastFramiliarMenuRarity == currentRarity &&
                lastFramiliarMenuCell == cell &&
                lastFramiliarMenuDeckCount == currentDeckCount &&
                framiliarCopyMenuRoot.activeSelf)
            {
                return;
            }

            lastFramiliarMenuOperator = framiliar;
            lastFramiliarMenuRarity = currentRarity;
            lastFramiliarMenuCell = cell;
            lastFramiliarMenuDeckCount = currentDeckCount;
            activeFramiliarMatchingCards.Clear();

            bool isHighGround = cell != null && cell.TileType == TileType.HighGround;
            bool isLowGround = cell != null && (cell.TileType == TileType.LowGround || cell.TileType == TileType.EnemyPath);

            if (framiliarSubtitleText != null)
            {
                if (isHighGround)
                    framiliarSubtitleText.text = "COPY: HIGH GROUND (RANGED)";
                else if (isLowGround)
                    framiliarSubtitleText.text = "COPY: LOW GROUND (MELEE)";
                else
                    framiliarSubtitleText.text = "COPY OPERATOR";
            }

            var deck = playerDeck != null ? playerDeck.DeckSlots : null;
            if (deck != null)
            {
                for (int i = 0; i < deck.Count; i++)
                {
                    var card = deck[i];
                    if (card == null || card.operatorData == null) continue;
                    if (card.operatorData.operatorName == "Framiliar") continue;

                    // Enforce tile ground matching: only display matching operators, do not show non-matching!
                    if (cell != null && !cell.SupportsPosition(card.operatorData.position)) continue;

                    activeFramiliarMatchingCards.Add(card);
                }
            }

            int matchingCount = activeFramiliarMatchingCards.Count;
            RectTransform labelRect = selectedOperatorLabelRoot.GetComponent<RectTransform>();

            if (matchingCount == 0)
            {
                if (framiliarEmptyMessageText != null)
                {
                    framiliarEmptyMessageText.gameObject.SetActive(true);
                    framiliarEmptyMessageText.text = isHighGround
                        ? "No Ranged operators in deck."
                        : (isLowGround ? "No Melee operators in deck." : "No matching operators in deck.");
                }
                for (int i = 0; i < DeckSlotCount; i++)
                {
                    if (framiliarCopyButtons[i] != null) framiliarCopyButtons[i].gameObject.SetActive(false);
                }
                labelRect.sizeDelta = new Vector2(280f, 145f);
            }
            else
            {
                if (framiliarEmptyMessageText != null)
                {
                    framiliarEmptyMessageText.gameObject.SetActive(false);
                }

                int row0Count = Mathf.Min(matchingCount, 4);
                int row1Count = Mathf.Max(0, matchingCount - 4);
                bool hasTwoRows = row1Count > 0;

                // Dynamic size: 1 row is 196f (shorter/wider aspect), 2 rows is 260f (longer)
                labelRect.sizeDelta = new Vector2(280f, hasTwoRows ? 260f : 196f);

                int framiliarUpgradeLevels = framiliar.UpgradeLevel;

                const float btnWidth = 54f;
                const float btnSpacing = 8f;

                // Row 0 layout
                float row0Width = row0Count * btnWidth + Mathf.Max(0, row0Count - 1) * btnSpacing;
                float startX0 = -row0Width * 0.5f + (btnWidth * 0.5f);
                float row0CenterY = -152f;

                // Row 1 layout
                float row1Width = row1Count * btnWidth + Mathf.Max(0, row1Count - 1) * btnSpacing;
                float startX1 = -row1Width * 0.5f + (btnWidth * 0.5f);
                float row1CenterY = -218f;

                for (int i = 0; i < DeckSlotCount; i++)
                {
                    if (i < matchingCount)
                    {
                        var card = activeFramiliarMatchingCards[i];
                        framiliarCopyButtons[i].gameObject.SetActive(true);
                        RectTransform btnRect = framiliarCopyButtons[i].GetComponent<RectTransform>();

                        float posX;
                        float posY;
                        if (i < 4)
                        {
                            posX = startX0 + i * (btnWidth + btnSpacing);
                            posY = row0CenterY;
                        }
                        else
                        {
                            int colIndex = i - 4;
                            posX = startX1 + colIndex * (btnWidth + btnSpacing);
                            posY = row1CenterY;
                        }

                        btnRect.anchoredPosition = new Vector2(posX, posY);

                        framiliarCopyPortraits[i].sprite = card.operatorData.portrait;
                        framiliarCopyPortraits[i].enabled = card.operatorData.portrait != null;
                        framiliarCopyPortraits[i].color = Color.white;

                        Color classColor = GetClassColor(card.operatorData.operatorClass);
                        framiliarCopyClassBadgeImages[i].color = Color.clear;
                        framiliarCopyClassIconImages[i].sprite = GetClassIconSprite(card.operatorData.operatorClass);
                        framiliarCopyClassIconImages[i].color = Color.white;
                        framiliarCopyClassBadges[i].SetActive(true);

                        // Upgrade inheritance: always check which upgrade is higher: Framiliar or the target operator. Pick highest.
                        int cardUpgrade = Mathf.Clamp((int)card.rarity - (int)card.operatorData.baseRarity, 0, 2);
                        int effectiveUpgrade = TrashTD.Operators.FramiliarOperator.CalculateEffectiveUpgradeLevel(framiliarUpgradeLevels, cardUpgrade);
                        OperatorRarity effectiveRarity = TrashTD.Operators.FramiliarOperator.CalculateEffectiveRarity(card.operatorData, framiliarUpgradeLevels, cardUpgrade);

                        string stars = new string('★', (int)effectiveRarity);
                        if (effectiveUpgrade > cardUpgrade)
                        {
                            framiliarCopyLabels[i].text = $"<color=#FFD700>{stars}</color>";
                        }
                        else
                        {
                            framiliarCopyLabels[i].text = stars;
                        }
                        framiliarCopyLabels[i].color = new Color(1f, 0.78f, 0.2f, 1f);

                        framiliarCopyButtons[i].interactable = true;
                        framiliarCopyFeedbacks[i].glowColor = Color.Lerp(classColor, Color.white, 0.5f);
                    }
                    else
                    {
                        framiliarCopyButtons[i].gameObject.SetActive(false);
                    }
                }
            }

            framiliarCopyMenuRoot.SetActive(true);
        }

        public void HideFramiliarCopyMenu()
        {
            activeFramiliarOperator = null;
            lastFramiliarMenuOperator = null;
            lastFramiliarMenuRarity = -1;
            lastFramiliarMenuCell = null;
            lastFramiliarMenuDeckCount = -1;
            activeFramiliarMatchingCards.Clear();
            if (framiliarCopyMenuRoot != null)
            {
                framiliarCopyMenuRoot.SetActive(false);
            }
            if (selectedOperatorLabelRoot != null)
            {
                selectedOperatorLabelRoot.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 106f);
            }
        }

        private void SelectFramiliarCopySlot(int slotIndex)
        {
            if (activeFramiliarOperator == null) return;
            if (slotIndex < 0 || slotIndex >= activeFramiliarMatchingCards.Count) return;

            var card = activeFramiliarMatchingCards[slotIndex];
            var fram = activeFramiliarOperator;
            HideFramiliarCopyMenu();
            fram.CopyOperator(card);
        }

        /// <summary>The baked cooldown toast (Resources, so a build can load it). Missing = built in code.</summary>
        public const string CooldownToastPrefabAssetPath = "Assets/Resources/UI/Prefabs/CooldownToast.prefab";
        private const string CooldownToastPrefabResourcePath = "UI/Prefabs/CooldownToast";

        private void CreateCooldownToast(Transform root)
        {
            // Layout from the baked prefab when there is one (TRASH TD > UI > Bake Cooldown Toast Prefab), otherwise built in
            // code. The slide animation starts from wherever the toast is placed, so move it in the prefab to move it in game.
            GameObject toast = null;
            GameObject prefab = Resources.Load<GameObject>(CooldownToastPrefabResourcePath);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, root, false);
                instance.name = "CooldownToast";
                if (TryResolveCooldownToast(instance, out _, out string missing))
                {
                    toast = instance;
                }
                else
                {
                    Debug.LogError("GameplayHUDUI: the baked cooldown toast (" + CooldownToastPrefabAssetPath + ") is missing " + missing +
                                   ". Building it in code instead; rebake it with TRASH TD > UI > Bake Cooldown Toast Prefab.");
                    instance.SetActive(false);
                    Destroy(instance);
                }
            }
            if (toast == null) toast = BuildCooldownToastLayout(root);

            TryResolveCooldownToast(toast, out CooldownToastRefs refs, out _);
            cooldownToastRect = refs.rect;
            cooldownToastGroup = refs.group;
            cooldownToastTitle = refs.title;
            cooldownToastMessage = refs.message;
            cooldownToastShownPosition = refs.rect.anchoredPosition;

            cooldownToastGroup.alpha = 0f;
            cooldownToastGroup.interactable = false;
            cooldownToastGroup.blocksRaycasts = false;
        }

        private sealed class CooldownToastRefs
        {
            public RectTransform rect;
            public CanvasGroup group;
            public Text title;
            public Text message;
        }

        /// <summary>Builds the cooldown toast's layout in code under <paramref name="root"/>, for the bake command and the tests.</summary>
        public static GameObject BuildCooldownToastForPrefab(Transform root) => BuildCooldownToastLayout(root);

        /// <summary>True when <paramref name="candidate"/> has every named part the HUD binds to; otherwise lists what is missing.</summary>
        public static bool ValidateCooldownToast(GameObject candidate, out string missing) => TryResolveCooldownToast(candidate, out _, out missing);

        private static bool TryResolveCooldownToast(GameObject root, out CooldownToastRefs refs, out string missing)
        {
            var names = new UiPrefabLookup(root);
            refs = new CooldownToastRefs
            {
                rect = root.GetComponent<RectTransform>(),
                group = names.FindOnRoot<CanvasGroup>(),
                title = names.Find<Text>("Title"),
                message = names.Find<Text>("Message")
            };
            missing = names.Missing;
            return names.IsComplete && refs.rect != null;
        }

        private static GameObject BuildCooldownToastLayout(Transform root)
        {
            var toast = new GameObject("CooldownToast", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            toast.transform.SetParent(root, false);
            SetPosition(toast.GetComponent<RectTransform>(), new Vector2(0f, 188f), new Vector2(0.5f, 0f), new Vector2(560f, 76f), new Vector2(0.5f, 0f));

            Image background = toast.GetComponent<Image>();
            ApplyPanelContainer(toast, PanelContainerType.Tooltip);
            background.raycastTarget = false;

            Outline outline = toast.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.48f, 0.16f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            Text title = CreateText(toast.transform, "Title", "OPERATOR ON COOLDOWN", 13, TextAnchor.MiddleCenter);
            title.color = new Color(1f, 0.65f, 0.25f, 1f);
            title.raycastTarget = false;
            SetPosition(title.GetComponent<RectTransform>(), new Vector2(0f, -18f), new Vector2(0.5f, 1f), new Vector2(520f, 26f), new Vector2(0.5f, 0.5f));

            Text message = CreateText(toast.transform, "Message", string.Empty, 14, TextAnchor.MiddleCenter);
            message.raycastTarget = false;
            SetPosition(message.GetComponent<RectTransform>(), new Vector2(0f, -49f), new Vector2(0.5f, 1f), new Vector2(520f, 28f), new Vector2(0.5f, 0.5f));

            return toast;
        }

        public void ShowCooldownWarning(string operatorName, int remainingRounds)
        {
            if (remainingRounds <= 0 || cooldownToastGroup == null) return;

            AudioManager.Instance?.PlaySfx(SfxId.UiDenied, sfxVolume);

            string displayName = string.IsNullOrWhiteSpace(operatorName) ? "OPERATOR" : operatorName.ToUpperInvariant();
            string roundLabel = remainingRounds == 1 ? "ROUND" : "ROUNDS";
            cooldownToastMessage.text = $"{displayName} READY IN {remainingRounds} {roundLabel}";

            if (cooldownToastRoutine != null) StopCoroutine(cooldownToastRoutine);
            cooldownToastRoutine = StartCoroutine(CooldownToastRoutine());
        }

        private System.Collections.IEnumerator CooldownToastRoutine()
        {
            const float fadeDuration = 0.18f;
            const float visibleDuration = 1.8f;
            Vector2 shownPosition = cooldownToastShownPosition;
            Vector2 hiddenPosition = shownPosition + new Vector2(0f, -18f);
            cooldownToastRect.anchoredPosition = hiddenPosition;
            cooldownToastRect.localScale = Vector3.one * 0.94f;
            cooldownToastGroup.alpha = 0f;

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                float eased = EaseOutCubic(progress);
                cooldownToastGroup.alpha = eased;
                cooldownToastRect.anchoredPosition = Vector2.Lerp(hiddenPosition, shownPosition, eased);
                cooldownToastRect.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
                yield return null;
            }

            cooldownToastGroup.alpha = 1f;
            cooldownToastRect.anchoredPosition = shownPosition;
            cooldownToastRect.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(visibleDuration);

            elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / fadeDuration);
                cooldownToastGroup.alpha = 1f - progress;
                cooldownToastRect.anchoredPosition = Vector2.Lerp(shownPosition, hiddenPosition, progress);
                yield return null;
            }

            cooldownToastGroup.alpha = 0f;
            cooldownToastRect.anchoredPosition = hiddenPosition;
            cooldownToastRoutine = null;
        }

        public bool IsDraftMinimized => draftOverlayUI != null && draftOverlayUI.IsMinimized;

        /// <summary>Camera for screen-point conversions on the HUD canvas (null while it is an overlay canvas).</summary>
        private Camera GetCanvasCamera()
        {
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        }

        // Deck cards can be deployed in Preparation, and during Card Pick while the draft is minimized
        // (the map & deck view), so a deploy can free a deck slot for the card about to be picked.
        private bool IsDeckDeployPhase => gameManager != null &&
            (gameManager.CurrentPhase == StagePhase.Preparation ||
             (gameManager.CurrentPhase == StagePhase.CardPick && IsDraftMinimized));

        // During Card Pick with the full draft screen up, and during a wave, the deck can only be rearranged and
        // discarded from.
        private bool IsDeckEditOnly => gameManager != null &&
            ((gameManager.CurrentPhase == StagePhase.CardPick && !IsDraftMinimized) ||
             gameManager.CurrentPhase == StagePhase.WaveActive);

        // Any card can be dragged to rearrange or discard it, cards on cooldown included. Cooldown cards just don't
        // preview or deploy on the map; the cooldown warning comes from clicking them (SelectDeckSlot).
        private bool CanDragDeckCard(int slotIndex)
        {
            if (gameManager == null || playerDeck == null) return false;
            if (!IsDeckDeployPhase && !IsDeckEditOnly) return false;
            return playerDeck.GetCard(slotIndex) != null;
        }

        private static bool IsOnCooldown(DraftCard card) => card != null && card.cooldownRoundsRemaining > 0;

        private GameObject CreateUpgradeBadge(Transform parent, string objectName, Vector2 size, Vector2 position)
        {
            GameObject badge = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            badge.transform.SetParent(parent, false);
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = Vector2.one;
            badgeRect.anchorMax = Vector2.one;
            badgeRect.pivot = Vector2.one;
            badgeRect.sizeDelta = size;
            badgeRect.anchoredPosition = position;

            Image background = badge.GetComponent<Image>();
            background.color = new Color(1f, 0.78f, 0.2f, 1f);
            background.raycastTarget = false;

            GameObject arrows = new GameObject("Arrows", typeof(RectTransform), typeof(CanvasRenderer));
            arrows.transform.SetParent(badge.transform, false);
            RectTransform arrowsRect = arrows.GetComponent<RectTransform>();
            arrowsRect.anchorMin = Vector2.zero;
            arrowsRect.anchorMax = Vector2.one;
            arrowsRect.offsetMin = new Vector2(2f, 1f);
            arrowsRect.offsetMax = new Vector2(-2f, -1f);
            arrows.AddComponent<UpgradeArrowGraphic>().color = new Color(0.12f, 0.1f, 0.04f, 1f);

            badge.SetActive(false);
            return badge;
        }
    }
}
