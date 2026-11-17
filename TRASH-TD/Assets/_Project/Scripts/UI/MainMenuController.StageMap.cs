using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TrashTD.Audio;
using TrashTD.Data;
using StageProgress = TrashTD.Core.GameLoop.StageProgress;
using ActCatalog = TrashTD.Core.GameLoop.ActCatalog;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // The stage selection map: stage nodes, map navigation, sprite loading and the status ticker.
    public partial class MainMenuController
    {
        /// <summary>
        /// PLAY: with one act straight to its stage map, as before; with several acts the world map first (an act is chosen
        /// there, and BACK from an act's map returns to it).
        /// </summary>
        public void ShowStageSelection()
        {
            if (ActCatalog.HasSeveralActs)
            {
                ShowWorldMap();
                return;
            }

            ShowStageSelectionInternal();
        }

        private void LeaveStageMap()
        {
            if (ActCatalog.HasSeveralActs) ShowWorldMap();
            else ShowMainMenu();
        }

        // ─────────────────────────────────────────────────────
        //  Acts: the world map and switching the stage map to another act
        // ─────────────────────────────────────────────────────

        private ActData CurrentAct => currentAct != null ? currentAct : (currentAct = ActCatalog.All[0]);

        /// <summary>The current act's own stage map art, or the project's default Stage Selector Map.</summary>
        private Sprite CurrentMapSprite => CurrentAct.stageMapSprite != null ? CurrentAct.stageMapSprite : stageMapBackgroundSprite;

        private void EnsureWorldMap()
        {
            if (worldMap != null || stageSelectionPanel == null) return;

            worldMap = ActWorldMapScreen.Create(stageSelectionPanel.transform.parent, OpenAct,
                () => { PlayBack(); ShowMainMenu(); }, OpenSandboxFromActScreen, PlayHover, PlayClick, PlayDenied);
            worldMap.Root.transform.SetSiblingIndex(stageSelectionPanel.transform.GetSiblingIndex() + 1);
            worldMap.Root.SetActive(false);
        }

        private void ShowWorldMap()
        {
            RestoreStageMapBehindDossier();
            EnsureWorldMap();
            if (worldMap == null)
            {
                ShowStageSelectionInternal();
                return;
            }

            CardDraftOverlayUI.HideAllForMenuTransition();
            stageSelectionOpen = false;
            detailsOpen = false;
            worldMapOpen = true;
            SetBackgroundInteractable(true);

            worldMap.Refresh(ActCatalog.All, stageMapBackgroundSprite);
            SetPanelVisible(mainMenuPanel, false, true);
            SetPanelVisible(stageSelectionPanel, false, true);
            SetPanelVisible(stageDetailsPanel, false, true, stageDetailsModal);
            SetPanelVisible(worldMap.Root, true, false);
        }

        /// <summary>
        /// QA SANDBOX on the act selection screen (dev mode): opens its dossier over the current act's map. The sandbox picks its
        /// own map, so it belongs to no act.
        /// </summary>
        private void OpenSandboxFromActScreen()
        {
            if (!TrashTD.Development.DevMode.IsEnabled) return;

            int sandboxIndex = SandboxStageIndex;
            if (sandboxIndex < 0) return;

            // The dossier lives on the stage map's panel, so that panel hosts it (behind the dossier's own dark backdrop) while
            // the act selection screen is hidden. No act is switched to and no map animation plays; closing the dossier
            // comes back to the act selection screen (CloseStageDetails).
            CardDraftOverlayUI.HideAllForMenuTransition();
            stageSelectionOpen = true;
            SetPanelVisible(mainMenuPanel, false, true);

            // The stage map's panel hosts the dossier, but its own layers (the map, its markers, the side panel, the header) are
            // hidden and the act selection screen is moved just under it, so the act selection is what shows behind the dossier
            HideStageMapBehindDossier();
            SetPanelVisible(stageSelectionPanel, true, true);
            if (worldMap != null)
            {
                SetPanelVisible(worldMap.Root, true, true);
                worldMap.Root.transform.SetSiblingIndex(stageSelectionPanel.transform.GetSiblingIndex());
            }

            SelectStage(sandboxIndex);
            if (detailsOpen) dossierFromActScreen = true;
            else ShowWorldMap(); // it did not open (for example Coalesce is guiding the map): stay on the act selection
        }

        private void HideStageMapBehindDossier()
        {
            RestoreStageMapBehindDossier();
            stageMapHidden = true;

            Image panelImage = stageSelectionPanel.GetComponent<Image>();
            stagePanelImageWasEnabled = panelImage != null && panelImage.enabled;
            if (panelImage != null) panelImage.enabled = false;

            foreach (Transform child in stageSelectionPanel.transform)
            {
                GameObject layer = child.gameObject;
                if (layer == stageDetailsPanel || !layer.activeSelf) continue;
                layer.SetActive(false);
                stageMapHiddenBehindDossier.Add(layer);
            }
        }

        /// <summary>Puts the stage map's layers back after the sandbox dossier, and the act selection screen above the stage map again.</summary>
        private void RestoreStageMapBehindDossier()
        {
            if (!stageMapHidden) return;
            stageMapHidden = false;

            foreach (GameObject layer in stageMapHiddenBehindDossier)
            {
                if (layer != null) layer.SetActive(true);
            }
            stageMapHiddenBehindDossier.Clear();

            if (stagePanelImageWasEnabled)
            {
                Image panelImage = stageSelectionPanel.GetComponent<Image>();
                if (panelImage != null) panelImage.enabled = true;
                stagePanelImageWasEnabled = false;
            }

            if (worldMap != null) worldMap.Root.transform.SetSiblingIndex(stageSelectionPanel.transform.GetSiblingIndex() + 1);
        }

        /// <summary>A click on an act of the world map: its stage map opens, unless the act is still locked.</summary>
        private void OpenAct(ActData act)
        {
            if (act == null) return;
            if (!ActCatalog.IsActUnlocked(act))
            {
                PlayDenied();
                return;
            }

            SwitchToAct(act);
            if (worldMap != null) SetPanelVisible(worldMap.Root, false, true);
            worldMapOpen = false;
            ShowStageSelectionInternal();
        }

        /// <summary>Makes the stage map show this act: its markers, map art, minimap and header text.</summary>
        private void SwitchToAct(ActData act)
        {
            if (act == null || act == currentAct) return;

            currentAct = act;
            selectedStage = null;
            selectedStageIndex = -1;
            RebuildActNodes();
            if (mapNavigator != null) mapNavigator.SetMap(CurrentMapSprite, stageMapNodes);
        }

        /// <summary>The act's name, zone and lore in the stage map's header.</summary>
        private void ApplyActHeader()
        {
            ActData act = CurrentAct;
            SetHeaderText(stageHeaderTitle, act.DisplayName);
            SetHeaderText(stageZoneTitle, act.zoneName);
            if (stageZoneLore != null)
            {
                SetHeaderText(stageZoneLore.Find("LoreBody"), act.zoneLore);
                SetHeaderText(stageZoneLore.Find("LoreTag"), act.loreTag);
            }
        }

        private static void SetHeaderText(Component target, string value)
        {
            Text text = target != null ? target.GetComponent<Text>() : null;
            if (text != null) text.text = value ?? string.Empty;
        }

        /// <summary>The act's map art on the full-screen map.</summary>
        private void ApplyActMapSprite()
        {
            if (stageMapImage == null) return;

            Sprite sprite = CurrentMapSprite;
            if (sprite != null)
            {
                if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
                stageMapImage.sprite = sprite;
                stageMapImage.type = Image.Type.Simple;
                stageMapImage.preserveAspect = false;
                stageMapImage.color = Color.white;
            }
            else
            {
                stageMapImage.sprite = null;
                stageMapImage.color = new Color(0.02f, 0.05f, 0.03f, 1f);
            }
        }

        /// <summary>Where the QA sandbox sits in the stage list (right after the current act's stages), or -1.</summary>
        private int SandboxStageIndex
        {
            get
            {
                for (int i = 0; i < availableStages.Count; i++)
                {
                    if (availableStages[i] != null && availableStages[i].stageId == SandboxStageId) return i;
                }
                return -1;
            }
        }

        // ── Coalesce's stage-map intro (MapTutorialGuide), until the tutorial is finished or skipped ──

        public bool IsStageSelectionOpen => stageSelectionOpen && !isLoadingStage;
        public bool IsTutorialDossierOpen => detailsOpen && IsTutorialStage(selectedStage);
        public RectTransform PlayStageButtonRect => playStageButton != null ? playStageButton.GetComponent<RectTransform>() : null;

        /// <summary>The map marker with this stage number (0 = the tutorial), for Coalesce's pointer.</summary>
        public RectTransform GetStageNodeRect(int stageNumber)
        {
            foreach (StageMapNode node in stageMapNodes)
            {
                if (node != null && node.StageNumber == stageNumber) return node.GetComponent<RectTransform>();
            }
            return null;
        }

        private bool GuideRequiresTutorialStage => mapGuide != null && mapGuide.RequiresTutorialStage;

        /// <summary>The map marker showing this stage, or null.</summary>
        private StageMapNode FindStageNode(StageData stage)
        {
            if (stage == null) return null;
            foreach (StageMapNode node in stageMapNodes)
            {
                if (node != null && node.Stage == stage) return node;
            }
            return null;
        }

        private static bool IsTutorialStage(StageData stage) => stage != null && stage.stageId == TutorialStageId;

        private void ShowStageSelectionInternal()
        {
            RestoreStageMapBehindDossier();
            CardDraftOverlayUI.HideAllForMenuTransition();
            stageSelectionOpen = true;
            detailsOpen = false;
            stageTickerTarget = string.Empty;
            SetStageTicker(StageTickerIdle);
            SetBackgroundInteractable(true);

            SetPanelVisible(mainMenuPanel, false, true);
            SetPanelVisible(stageSelectionPanel, true, true);
            SetPanelVisible(stageDetailsPanel, false, true, stageDetailsModal);
            ApplyDevModeVisibility();

            // Deselect all nodes when returning to the map
            foreach (var node in stageMapNodes)
            {
                if (node != null) node.SetSelected(false);
            }

            if (mapNavigator != null) mapNavigator.ResetView(true);
            PlayEntrance(stageEntrance, ref stageEntranceRoutine);
            PlayStageNodeIntros();

            // First visits: Coalesce introduces the map and asks for STAGE 00 (or a skip)
            if (mapGuide == null && !TrashTD.Core.GameLoop.TutorialDirector.IsTutorialDone)
            {
                mapGuide = MapTutorialGuide.Create(this);
            }

            StageMapNode focusNode = FindStageNode(selectedStage) ?? (stageMapNodes.Count > 0 ? stageMapNodes[0] : null);
            if (focusNode != null)
            {
                FocusUi(focusNode.gameObject);
            }
        }

        public void CloseStageDetails()
        {
            if (!detailsOpen) return;

            detailsOpen = false;
            SetBackgroundInteractable(true);
            SetPanelVisible(stageDetailsPanel, false, false, stageDetailsModal);

            // The sandbox's dossier was opened from the act selection screen, so closing it goes back there
            if (dossierFromActScreen)
            {
                dossierFromActScreen = false;
                ShowWorldMap(); // puts the stage map's layers back before hiding that panel
                return;
            }

            StageMapNode selectedNode = FindStageNode(selectedStage);
            if (selectedNode != null)
            {
                FocusUi(selectedNode.gameObject);
            }
        }

        // Players never see QA SANDBOX (DevMode). The ESC hint drops into its slot so the corner isn't left empty.
        private void ApplyDevModeVisibility()
        {
            // With several acts the sandbox lives on the act selection screen; with one there is no such screen, so it stays here
            bool dev = TrashTD.Development.DevMode.IsEnabled && !ActCatalog.HasSeveralActs;
            if (sandboxButton != null) sandboxButton.gameObject.SetActive(dev);
            if (stageHint == null) return;

            var hintPos = new Vector2(-60f, dev ? 96f : 52f);
            stageHint.anchoredPosition = hintPos;
            foreach (EntranceItem item in stageEntrance)
            {
                if (item.rt == stageHint) item.target = hintPos; // the entrance slides back to its stored target
            }
        }

        private void SetBackgroundInteractable(bool value)
        {
            // While the details modal is open, keep keyboard/gamepad navigation from drifting underneath it.
            if (stageListRoot != null) GetOrAddCanvasGroup(stageListRoot.gameObject).interactable = value;
            if (backButton != null) GetOrAddCanvasGroup(backButton.gameObject).interactable = value;
            if (sandboxButton != null) GetOrAddCanvasGroup(sandboxButton.gameObject).interactable = value;
        }

        private static void FocusUi(GameObject target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(target);
        }

        // ─────────────────────────────────────────────────────
        //  Stage selector
        // ─────────────────────────────────────────────────────

        private StageData sandboxStageStub;

        private void BuildStageButtons()
        {
            stageEntrance.Clear();
            RebuildActNodes();
        }

        /// <summary>
        /// Lists the current act's stages and builds their markers. The list keeps the order the rest of the menu relies on:
        /// the act's stages first (stage i at index i), then the QA sandbox, then the tutorial.
        /// </summary>
        private void RebuildActNodes()
        {
            availableStages.Clear();
            stageCards.Clear();
            stageMapNodes.Clear();

            ActData act = CurrentAct;
            var markerPositions = new List<Vector2>();
            if (act.stages != null)
            {
                foreach (ActStageEntry entry in act.stages)
                {
                    if (entry == null || entry.stage == null) continue;
                    availableStages.Add(entry.stage);
                    markerPositions.Add(entry.mapPosition);
                }
            }
            int actStageCount = availableStages.Count;

            if (sandboxStageStub == null)
            {
                sandboxStageStub = Resources.Load<StageData>("Stages/" + SandboxStageId);
                if (sandboxStageStub == null)
                {
                    sandboxStageStub = ScriptableObject.CreateInstance<StageData>();
                    sandboxStageStub.stageId = SandboxStageId;
                    sandboxStageStub.mapName = "QA Sandbox";
                    sandboxStageStub.shortDescription = "Development-only map for enemy and operator testing.";
                }
            }
            availableStages.Add(sandboxStageStub);

            StageData tutorialStage = act.tutorialStage;
            if (tutorialStage != null) availableStages.Add(tutorialStage);

            ApplyActHeader();
            ApplyActMapSprite();

            if (stageListRoot == null)
            {
                return;
            }

            // Clean up any old children (except the scanlines overlay and layout)
            for (int i = stageListRoot.childCount - 1; i >= 0; i--)
            {
                var child = stageListRoot.GetChild(i);
                if (child.GetComponent<StageMapScanlines>() != null) continue;
                if (child.GetComponent<StageMapAnimOverlay>() != null || child.name == "StageMap_Anim_Overlay") continue;
                if (child.name.StartsWith("StageNode_")) Destroy(child.gameObject);
            }

            for (int i = 0; i < actStageCount; i++)
            {
                var stage = availableStages[i];
                bool isPlayable = IsStageOpen(stage);

                var node = CreateStageMapNode(stage, i + 1, isPlayable, markerPositions[i]);
                stageMapNodes.Add(node);

                // Nodes run their own boot-in (PlayStageNodeIntros), not the generic entrance
                var nodeFeedback = node.GetComponent<MenuButtonFeedback>();
                if (nodeFeedback != null) stageCards.Add(nodeFeedback);
            }

            if (tutorialStage != null)
            {
                var tutorialNode = CreateStageMapNode(tutorialStage, 0, true, act.tutorialMapPosition);
                stageMapNodes.Add(tutorialNode);
                var tutorialFeedback = tutorialNode.GetComponent<MenuButtonFeedback>();
                if (tutorialFeedback != null) stageCards.Add(tutorialFeedback);
            }

            // Position the markers just built. RefreshNodes, not Apply: the layout caches its markers, and these are new
            // (switching acts rebuilds them), so Apply alone would leave them all at the middle of the map
            if (stageMapLayout != null) stageMapLayout.RefreshNodes();
        }

        /// <summary>
        /// Creates one stage marker (StageMapNode builds its own target + callout card) under the map container.
        /// The callout goes left of markers near the right edge so it stays on screen.
        /// </summary>
        private StageMapNode CreateStageMapNode(StageData stage, int stageNumber, bool isPlayable, Vector2 normalizedPos)
        {
            var go = new GameObject("StageNode_" + stageNumber, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(stageListRoot, false);

            // Invisible hit area; the visuals are children built by the node
            var hitArea = go.GetComponent<Image>();
            hitArea.color = new Color(1f, 1f, 1f, 0f);
            hitArea.raycastTarget = true;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = hitArea;
            btn.transition = Selectable.Transition.None;

            var node = go.AddComponent<StageMapNode>();
            node.Build(stage, stageNumber, normalizedPos, !isPlayable, normalizedPos.x > 0.7f);
            node.Clicked += OnStageMapNodeClicked;
            node.HoverChanged += OnStageMapNodeHoverChanged;

            // Press / wobble / sounds only: the node draws its own hover and glow, so no scale-up or outline here
            var feedback = go.AddComponent<MenuButtonFeedback>();
            feedback.hoverScale = 1f;
            feedback.pressScale = 0.96f;
            feedback.hoverBrighten = 0f;
            feedback.pressDarken = 0f;
            feedback.Hovered = PlayHover;
            feedback.Clicked = isPlayable ? (System.Action)PlayClick : PlayDenied;

            return node;
        }

        /// <summary>Scroll / drag / zoom navigation for the stage map, with its minimap and zoom bar (StageMapNavigator).</summary>
        private void SetUpMapNavigator()
        {
            if (stageListRoot == null || stageSelectionPanel == null || mapNavigator != null) return;

            mapNavigator = stageListRoot.gameObject.AddComponent<StageMapNavigator>();
            mapNavigator.InputBlocked = () => detailsOpen || isLoadingStage || (audioSettingsUI != null && audioSettingsUI.IsOpen)
                || (mapGuide != null && mapGuide.IsTalking);
            mapNavigator.Initialize(
                stageSelectionPanel.GetComponent<RectTransform>(),
                stageMapLayout,
                CurrentMapSprite,
                stageSelectionPanel.transform,
                stageMapNodes,
                button =>
                {
                    StyleTerminalButton(button, AccentBright, 14);
                    AttachFeedback(button, PlayClick, AccentBright, 1.05f);
                });

            if (stageSidePanel != null)
            {
                stageSidePanel.Navigator = mapNavigator;
                stageSidePanel.Grabbed = PlayClick;
            }

            // Under the stage dossier, which shares the panel
            if (stageDetailsPanel != null && stageDetailsPanel.transform.parent == mapNavigator.HudRoot.parent)
            {
                mapNavigator.HudRoot.SetSiblingIndex(stageDetailsPanel.transform.GetSiblingIndex());
            }
        }

        /// <summary>Staggered boot-in for the map markers, left to right in stage order.</summary>
        private void PlayStageNodeIntros()
        {
            for (int i = 0; i < stageMapNodes.Count; i++)
            {
                if (stageMapNodes[i] != null) stageMapNodes[i].PlayIntro(0.25f + stageMapNodes[i].StageNumber * 0.12f);
            }
        }

        private const string TitleLogoAnimationPath = "UI/Tiny Reclaimers Logo Wide animation";
        private const int TitleLogoFrameCount = 16;

        /// <summary>The title's animation frames in order, or null if the sheet is missing or not sliced into 16 frames.</summary>
        private static Sprite[] LoadTitleLogoFrames()
        {
            Sprite[] loaded = Resources.LoadAll<Sprite>(TitleLogoAnimationPath);
            if (loaded == null || loaded.Length != TitleLogoFrameCount)
            {
                if (loaded != null && loaded.Length > 0)
                    Debug.LogWarning($"Title logo animation has {loaded.Length} sprites, expected {TitleLogoFrameCount}; using the still logo.");
                return null;
            }

            System.Array.Sort(loaded, (a, b) => TrailingIndex(a.name).CompareTo(TrailingIndex(b.name)));
            if (loaded[0] != null && loaded[0].texture != null) loaded[0].texture.filterMode = FilterMode.Point;
            return loaded;
        }
        private static int TrailingIndex(string spriteName)
        {
            int underscore = spriteName.LastIndexOf('_');
            return underscore >= 0 && int.TryParse(spriteName.Substring(underscore + 1), out int index) ? index : 0;
        }

        /// <summary>
        /// Loads a sprite with multi-tier fallback: Resources single, Resources multiple/subassets, or direct disk loading.
        /// </summary>
        private static Sprite LoadSpriteSafely(string resourcePath, string projectRelativeFilePath)
        {
            // 1. Try single sprite load from Resources
            var s = Resources.Load<Sprite>(resourcePath);
            if (s != null)
            {
                if (s.texture != null) s.texture.filterMode = FilterMode.Point;
                return s;
            }

            // 2. Try multiple subassets from Resources
            var all = Resources.LoadAll<Sprite>(resourcePath);
            if (all != null && all.Length > 0 && all[0] != null)
            {
                if (all[0].texture != null) all[0].texture.filterMode = FilterMode.Point;
                return all[0];
            }

            // 3. Try reading directly from project folder on disk
            try
            {
                string fullPath = projectRelativeFilePath;
                if (!System.IO.Path.IsPathRooted(fullPath))
                {
                    fullPath = System.IO.Path.Combine(Application.dataPath, "..", projectRelativeFilePath);
                }
                if (System.IO.File.Exists(fullPath))
                {
                    byte[] bytes = System.IO.File.ReadAllBytes(fullPath);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.filterMode = FilterMode.Point;
                    if (tex.LoadImage(bytes))
                    {
                        tex.filterMode = FilterMode.Point;
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MainMenuController] Failed disk sprite load from {projectRelativeFilePath}: {ex.Message}");
            }

            return null;
        }

        /// <summary>Called when a StageMapNode dot is clicked on the Pip-Boy map.</summary>
        private void OnStageMapNodeClicked(StageMapNode node)
        {
            if (node == null || node.Stage == null) return;

            int index = availableStages.IndexOf(node.Stage);
            if (index < 0) return;

            SelectStage(index);
        }

        private void OnStageMapNodeHoverChanged(StageMapNode node, bool hovered)
        {
            if (node == null || node.Stage == null) return;

            string message;
            if (!hovered)
            {
                message = StageTickerIdle;
            }
            else
            {
                string stageName = string.IsNullOrEmpty(node.Stage.mapName) ? "UNKNOWN" : node.Stage.mapName.ToUpperInvariant();
                message = node.Stage.comingSoon
                    ? $"> {ActCatalog.FormatStageLabel(node.Stage.stageId)} // COMING SOON"
                    : node.IsLocked
                        ? $"> {ActCatalog.FormatStageLabel(node.Stage.stageId)} // {stageName}  -  SIGNAL LOCKED"
                        : $"> {ActCatalog.FormatStageLabel(node.Stage.stageId)} // {stageName}  -  CLICK TO OPEN DOSSIER";
            }

            SetStageTicker(message);

            // Keyboard focus can move to a marker that is off screen while zoomed in
            if (hovered && mapNavigator != null) mapNavigator.EnsureVisible(node);
        }

        private void SetStageTicker(string message)
        {
            if (message == stageTickerTarget) return;
            stageTickerTarget = message;
            stageTickerTime = 0f;
            stageTickerScrollOffset = 0f;
            stageTickerHoldTime = 0f;
            PrepareStageTickerScroll(message);
        }

        private const float TickerWidth = 820f;
        private const float TickerTextInset = 20f;
        private const float TickerViewportWidth = TickerWidth - 2f * TickerTextInset;
        private const int TickerFontSize = 15;
        private const string TickerLoopGap = "          ";
        private const float TickerScrollSpeed = 70f;  // canvas units per second
        private const float TickerScrollHold = 1.2f;  // pause after typing before the marquee starts

        /// <summary>
        /// Decides whether the message fits the fixed ticker window. If it doesn't, builds the looping
        /// marquee text (message + gap + message) and measures one loop period, so the scroll can wrap seamlessly.
        /// </summary>
        private void PrepareStageTickerScroll(string message)
        {
            if (stageTickerText == null) return;

            float messageWidth = MeasureStageTicker(message + "_");
            stageTickerScrolls = messageWidth > TickerViewportWidth;
            if (!stageTickerScrolls)
            {
                stageTickerText.rectTransform.sizeDelta = new Vector2(TickerViewportWidth, 0f);
                return;
            }

            // Period = distance from the first copy's start to the second's; measured as a difference so
            // it doesn't depend on whether trailing spaces count toward preferred width.
            stageTickerLoopText = message + TickerLoopGap + message;
            float loopWidth = MeasureStageTicker(stageTickerLoopText);
            stageTickerLoopPeriod = loopWidth - MeasureStageTicker(message);

            // RectMask2D culls by the text's rect, so it must cover the whole overflowing line
            stageTickerText.rectTransform.sizeDelta = new Vector2(Mathf.Max(loopWidth, messageWidth), 0f);
        }

        private float MeasureStageTicker(string text)
        {
            TextGenerationSettings settings = stageTickerText.GetGenerationSettings(Vector2.zero);
            return stageTickerText.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / stageTickerText.pixelsPerUnit;
        }

        /// <summary>
        /// Types the message out; if it's too long for the window, the line slides left to keep the cursor in view,
        /// holds briefly, then loops as a marquee from wherever typing left it.
        /// </summary>
        private void UpdateStageTicker(float dt, bool cursorOn)
        {
            stageTickerTime += dt;
            int visible = Mathf.Min(stageTickerTarget.Length, Mathf.FloorToInt(stageTickerTime * TickerCharsPerSecond));
            bool typing = visible < stageTickerTarget.Length;

            if (typing)
            {
                stageTickerText.text = stageTickerTarget.Substring(0, visible) + "_";
                stageTickerScrollOffset = stageTickerScrolls
                    ? Mathf.Max(0f, MeasureStageTicker(stageTickerText.text) - TickerViewportWidth)
                    : 0f;
            }
            else if (stageTickerScrolls)
            {
                stageTickerText.text = stageTickerLoopText;
                stageTickerHoldTime += dt;
                if (stageTickerHoldTime >= TickerScrollHold && stageTickerLoopPeriod > 0f)
                {
                    stageTickerScrollOffset = Mathf.Repeat(stageTickerScrollOffset + TickerScrollSpeed * dt, stageTickerLoopPeriod);
                }
            }
            else
            {
                stageTickerText.text = stageTickerTarget + (cursorOn ? "_" : " ");
                stageTickerScrollOffset = 0f;
            }

            stageTickerText.rectTransform.anchoredPosition = new Vector2(-Mathf.Round(stageTickerScrollOffset), 0f);
        }
    }
}
