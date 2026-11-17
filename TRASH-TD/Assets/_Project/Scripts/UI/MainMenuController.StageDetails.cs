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
    // The stage details panel: selecting a stage, the difficulty selector and slider, and the difficulty readout.
    public partial class MainMenuController
    {
        private void SelectStage(int stageIndex)
        {
            dossierFromActScreen = false; // a marker opens its own dossier; OpenSandboxFromActScreen sets this after
            if (stageIndex < 0 || stageIndex >= availableStages.Count)
            {
                return;
            }

            // Until the tutorial is done or skipped, Coalesce only lets STAGE 00 open
            if (GuideRequiresTutorialStage && !IsTutorialStage(availableStages[stageIndex]))
            {
                StageMapNode waitingNode = FindStageNode(availableStages[stageIndex]);
                if (waitingNode != null) waitingNode.GetComponent<MenuButtonFeedback>()?.Wobble();
                PlayDenied();
                return;
            }

            if (!IsStageOpen(availableStages[stageIndex]))
            {
                // Wobble the node's feedback if it exists
                StageMapNode lockedNode = FindStageNode(availableStages[stageIndex]);
                if (lockedNode != null)
                {
                    var fb = lockedNode.GetComponent<MenuButtonFeedback>();
                    if (fb != null) fb.Wobble();
                }
                return;
            }

            selectedStageIndex = stageIndex;
            selectedStage = availableStages[stageIndex];
            stageDetailsTitle.text = selectedStage.mapName;
            stageDetailsDescription.text = selectedStage.shortDescription;
            bool tutorial = IsTutorialStage(selectedStage);
            if (!tutorial) selectedDifficulty = PickOpenDifficulty(selectedStage, selectedDifficulty);
            UpdateDifficultyDisplay();

            // The tutorial's dossier is just the preview, name, brief and Back / Play
            SetDetailsBlockVisible(stageDetailsDivider, !tutorial);
            SetDetailsBlockVisible(difficultyLabel, !tutorial);
            SetDetailsBlockVisible(difficultySliderRoot, !tutorial);
            SetDetailsBlockVisible(difficultyBriefing, !tutorial);

            // Highlight the selected node, dim others
            foreach (StageMapNode node in stageMapNodes)
            {
                if (node != null) node.SetSelected(node.Stage == selectedStage);
            }

            detailsOpen = true;
            SetBackgroundInteractable(false);
            SetPanelVisible(stageDetailsPanel, true, false, stageDetailsModal);
            PlayEntrance(detailsEntrance, ref detailsEntranceRoutine);

            // Boot the tactical map once the panel is active, so the preview lays out against its real size
            if (stagePreview != null)
            {
                stagePreview.Show(selectedStage);
                if (stagePreviewInfo != null)
                {
                    // One coloured entry per route; spawn markers share their route's colour
                    var legend = new System.Text.StringBuilder("ROUTES ");
                    for (int i = 0; i < stagePreview.RouteCount; i++)
                    {
                        string hex = ColorUtility.ToHtmlStringRGB(StageMapPreview.GetRouteColor(i));
                        legend.Append($" <color=#{hex}>▶{i + 1}</color>");
                    }
                    legend.Append("     [!] SPAWN (ROUTE COLOUR)     <color=#52FF99>[+]</color> EXIT");
                    stagePreviewInfo.text = legend.ToString();
                }
            }
            FocusUi(playStageButton != null ? playStageButton.gameObject : null);
        }

        // ─────────────────────────────────────────────────────
        //  Stage details modal + difficulty picker
        // ─────────────────────────────────────────────────────

        private static void SetDetailsBlockVisible(Component block, bool visible)
        {
            if (block != null && block.gameObject.activeSelf != visible) block.gameObject.SetActive(visible);
        }

        private void BuildStageDetailsUi()
        {
            stageDetailsPanel = new GameObject("StageDetailsPanel", typeof(RectTransform), typeof(Image));
            stageDetailsPanel.transform.SetParent(stageSelectionPanel.transform, false);
            StretchFill(stageDetailsPanel.GetComponent<RectTransform>());
            stageDetailsPanel.GetComponent<Image>().color = new Color(0.004f, 0.012f, 0.008f, 0.90f);

            // Click outside the card to close
            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(stageDetailsPanel.transform, false);
            StretchFill(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var backdropButton = backdrop.GetComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.navigation = new Navigation { mode = Navigation.Mode.None };
            backdropButton.onClick.AddListener(() => { PlayBack(); CloseStageDetails(); });

            // Centered terminal window: stage preview on the left, dossier + difficulty on the right
            var modal = new GameObject("StageDetailsModal", typeof(RectTransform), typeof(Image));
            modal.transform.SetParent(stageDetailsPanel.transform, false);
            stageDetailsModal = modal.GetComponent<RectTransform>();
            Place(stageDetailsModal, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DossierWidth, DossierHeight));
            modal.GetComponent<Image>().color = TerminalPanel;

            var modalBorder = modal.AddComponent<Outline>();
            modalBorder.effectColor = TerminalLine;
            modalBorder.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(stageDetailsModal, 30f, 3f, AccentBright, -6f);

            // Title bar
            var header = new GameObject("DossierHeader", typeof(RectTransform), typeof(Image));
            header.transform.SetParent(modal.transform, false);
            StretchTop(header.GetComponent<RectTransform>(), 0f, 0f, 0f, DossierHeaderHeight);
            var headerImage = header.GetComponent<Image>();
            headerImage.color = TerminalHeader;
            headerImage.raycastTarget = false;

            var headerLabel = MakeText(header.transform, "DossierHeaderLabel", "◆ STAGE DOSSIER", 15, TextAnchor.MiddleLeft, AccentBright, FontStyle.Bold);
            headerLabel.rectTransform.offsetMin = new Vector2(DossierPadding, 0f);
            var headerStatus = MakeText(header.transform, "DossierHeaderStatus", "UPLINK STABLE  //  ROUTE SCAN ACTIVE", 12, TextAnchor.MiddleRight, TerminalText);
            headerStatus.rectTransform.offsetMax = new Vector2(-DossierPadding, 0f);

            // Accent stripe under the title bar, tinted by difficulty
            var modalAccent = new GameObject("ModalAccent", typeof(RectTransform), typeof(Image));
            modalAccent.transform.SetParent(modal.transform, false);
            StretchTop(modalAccent.GetComponent<RectTransform>(), 0f, 0f, DossierHeaderHeight, 3f);
            modalAccentImage = modalAccent.GetComponent<Image>();
            modalAccentImage.color = AccentColor;
            modalAccentImage.raycastTarget = false;

            // Stage preview (left)
            var previewFrame = new GameObject("StagePreviewFrame", typeof(RectTransform), typeof(Image));
            previewFrame.transform.SetParent(modal.transform, false);
            stagePreviewFrame = previewFrame.GetComponent<RectTransform>();
            Place(stagePreviewFrame, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(DossierPadding, -(DossierHeaderHeight + 24f)), new Vector2(PreviewFrameWidth, DossierHeight - DossierHeaderHeight - 24f - DossierPadding));
            var previewFrameImage = previewFrame.GetComponent<Image>();
            previewFrameImage.color = new Color(0.008f, 0.022f, 0.015f, 1f);
            previewFrameImage.raycastTarget = false;
            var previewOutline = previewFrame.AddComponent<Outline>();
            previewOutline.effectColor = new Color(TerminalLine.r, TerminalLine.g, TerminalLine.b, 0.30f);
            previewOutline.effectDistance = new Vector2(1f, -1f);
            AddCornerBrackets(stagePreviewFrame, 18f, 2f, AccentBright);

            var previewTitle = MakeText(previewFrame.transform, "PreviewTitle", "TACTICAL MAP  //  ENEMY ROUTES", 12, TextAnchor.MiddleLeft, TerminalText, FontStyle.Bold);
            StretchTop(previewTitle.rectTransform, 16f, 16f, 8f, 20f);

            // No Graphic here: StageMapPreview puts its CRT monitor (a RawImage) on this object
            var screen = new GameObject("StagePreviewScreen", typeof(RectTransform));
            screen.transform.SetParent(previewFrame.transform, false);
            var screenRect = screen.GetComponent<RectTransform>();
            screenRect.anchorMin = Vector2.zero;
            screenRect.anchorMax = Vector2.one;
            screenRect.offsetMin = new Vector2(12f, 40f);
            screenRect.offsetMax = new Vector2(-12f, -34f);
            stagePreview = screen.AddComponent<StageMapPreview>();

            stagePreviewInfo = MakeText(previewFrame.transform, "PreviewLegend",
                "<color=#FF5C42>▶▶</color> ENEMY ROUTE    <color=#FF5C42>[!]</color> SPAWN    <color=#52FF99>[+]</color> EXIT", 12,
                TextAnchor.MiddleLeft, TerminalText);
            StretchBottom(stagePreviewInfo.rectTransform, 16f, 16f, 10f, 22f);

            // Dossier column (right)
            var column = new GameObject("DossierColumn", typeof(RectTransform));
            column.transform.SetParent(modal.transform, false);
            stageDetailsColumn = column.GetComponent<RectTransform>();
            stageDetailsColumn.anchorMin = Vector2.zero;
            stageDetailsColumn.anchorMax = Vector2.one;
            stageDetailsColumn.offsetMin = new Vector2(DossierPadding + PreviewFrameWidth + 32f, DossierPadding);
            stageDetailsColumn.offsetMax = new Vector2(-DossierPadding, -(DossierHeaderHeight + 24f));

            stageDetailsTitle = MakeText(column.transform, "StageDetailsTitle", "Select a stage", 38, TextAnchor.UpperLeft, Color.white, FontStyle.Bold);
            stageDetailsTitle.resizeTextForBestFit = true;
            stageDetailsTitle.resizeTextMinSize = 22;
            stageDetailsTitle.resizeTextMaxSize = 46;
            // Room for two lines (a long name wraps instead of shrinking to a sliver), with air between them
            stageDetailsTitle.lineSpacing = 1.1f;
            StretchTop(stageDetailsTitle.rectTransform, 0f, 0f, 0f, DetailsTitleHeight);

            // Accent rule under the name, as wide as the divider further down (colour follows the difficulty,
            // like the stripe under the title bar)
            var titleRule = new GameObject("StageDetailsTitleRule", typeof(RectTransform), typeof(Image));
            titleRule.transform.SetParent(column.transform, false);
            StretchTop(titleRule.GetComponent<RectTransform>(), 0f, 0f, DetailsRuleTop, DetailsRuleHeight);
            stageDetailsTitleRule = titleRule.GetComponent<Image>();
            stageDetailsTitleRule.color = AccentColor;
            stageDetailsTitleRule.raycastTarget = false;

            // Column blocks (622 px tall) are spaced evenly, DetailsGap apart: name + accent rule, description,
            // divider, difficulty label + slider, info strip, and the Back / Play buttons at the bottom.
            stageDetailsDescription = MakeText(column.transform, "StageDetailsDescription", "", 18, TextAnchor.UpperLeft, new Color(0.78f, 0.90f, 0.83f, 1f));
            FitText(stageDetailsDescription, 13, 18);
            stageDetailsDescription.lineSpacing = 1.4f;
            StretchTop(stageDetailsDescription.rectTransform, 0f, 0f, DetailsDescriptionTop, DetailsDescriptionHeight);

            var divider = new GameObject("DetailsDivider", typeof(RectTransform), typeof(Image));
            divider.transform.SetParent(column.transform, false);
            stageDetailsDivider = divider.transform;
            StretchTop(divider.GetComponent<RectTransform>(), 0f, 0f, DetailsDividerTop, 2f);
            var dividerImage = divider.GetComponent<Image>();
            dividerImage.color = new Color(TerminalLine.r, TerminalLine.g, TerminalLine.b, 0.25f);
            dividerImage.raycastTarget = false;

            // Footer buttons
            stageDetailsBackButton = CreateButtonChild(column.transform, "StageDetailsBackButton", "◀  BACK");
            StyleTerminalButton(stageDetailsBackButton, TerminalText, 15);
            Place(stageDetailsBackButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(140f, 62f));

            playStageButton = CreateButtonChild(column.transform, "PlayStageButton", "▶  PLAY");
            playStageButton.GetComponent<Image>().color = AccentColor;
            var playBtnLabel = playStageButton.GetComponentInChildren<Text>();
            playStageLabel = playBtnLabel;
            if (playBtnLabel != null) { playBtnLabel.fontSize = 22; playBtnLabel.fontStyle = FontStyle.Bold; playBtnLabel.color = new Color(0.02f, 0.08f, 0.04f, 1f); }
            Place(playStageButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(250f, 62f));
            AddCornerBrackets(playStageButton.GetComponent<RectTransform>(), 12f, 2f, new Color(0.85f, 1f, 0.90f, 0.9f), -5f);

            stageDetailsPanel.SetActive(false);
        }

        private const float DossierWidth = 1300f;
        private const float DossierHeight = 720f;
        private const float DossierHeaderHeight = 46f;
        private const float DossierPadding = 28f;

        // Details column layout (column pixels from the top; the column is 622 px tall, Back / Play fill 560..622).
        // Blocks, top to bottom: name 92 + full-width accent rule 3, description 146, divider 2, difficulty label 20 +
        // slider 52, info strip 80. The rule sits 8 px under the name and the description 22 px under the rule; every
        // block after that is exactly DetailsGap (32) from the one above, and 31 px of air remains above the buttons.
        private const float DetailsGap = 32f;
        private const float DetailsTitleHeight = 92f;
        private const float DetailsRuleTop = DetailsTitleHeight + 8f;                                   // 100
        private const float DetailsRuleHeight = 3f;
        private const float DetailsDescriptionTop = DetailsRuleTop + DetailsRuleHeight + 22f;           // 125
        private const float DetailsDescriptionHeight = 146f;
        private const float DetailsDividerTop = DetailsDescriptionTop + DetailsDescriptionHeight + DetailsGap; // 303
        private const float DifficultyLabelTop = DetailsDividerTop + 2f + DetailsGap;                  // 337
        private const float DifficultyBriefingTop = DifficultyLabelTop + 80f + DetailsGap;             // 449 (label 20 + 8 + slider 52 = 80)
        private const float DifficultyBriefingHeight = 80f;
        private const float PreviewFrameWidth = 800f;

        /// <summary>Four L-shaped terminal corner brackets; a negative inset pushes them outside the rect.</summary>
        private static void AddCornerBrackets(RectTransform target, float length, float thickness, Color color, float inset = 0f)
        {
            if (target == null) return;

            for (int i = 0; i < 4; i++)
            {
                var corner = new Vector2(i % 2, i / 2);
                var inward = new Vector2(corner.x < 0.5f ? 1f : -1f, corner.y < 0.5f ? 1f : -1f);
                CreateBracketBar(target, corner, inward * inset, new Vector2(length, thickness), color);
                CreateBracketBar(target, corner, inward * inset, new Vector2(thickness, length), color);
            }
        }

        private static void CreateBracketBar(RectTransform parent, Vector2 corner, Vector2 offset, Vector2 size, Color color)
        {
            var go = new GameObject("CornerBracket", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), corner, corner, offset, size);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>Dark green terminal button with bracket corners (MenuButtonFeedback still owns hover/glow).</summary>
        private static void StyleTerminalButton(Button button, Color labelColor, int fontSize)
        {
            if (button == null) return;

            button.GetComponent<Image>().color = TerminalButton;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = fontSize;
                label.color = labelColor;
            }

            if (button.transform.Find("CornerBracket") == null)
            {
                AddCornerBrackets(button.GetComponent<RectTransform>(), 10f, 2f, TerminalLine);
            }
        }

        /// <summary>
        /// Difficulty picker in the stage details column, kept compact: a "> DIFFICULTY" label, a stepped pixel
        /// slider (EASY / NORMAL / HARD notches; press anywhere on it, drag, or use the arrow keys) and one framed
        /// strip with the choice, its waves and lives, and a one-line blurb. Colours follow the selection.
        /// </summary>
        private void BuildDifficultySelector()
        {
            if (stageDetailsPanel == null || difficultySlider != null) return;

            Transform parent = stageDetailsColumn != null ? (Transform)stageDetailsColumn
                : stageDetailsModal != null ? (Transform)stageDetailsModal : stageDetailsPanel.transform;

            difficultyLabel = MakeText(parent, "DifficultyLabel", "> DIFFICULTY", 12, TextAnchor.MiddleLeft, TerminalText, FontStyle.Bold);
            StretchTop(difficultyLabel.rectTransform, 0f, 0f, DifficultyLabelTop, 20f);

            BuildDifficultySlider(parent);

            // Info strip under the slider: three cells, each a small label over a large value:
            // WAVES | LIVES | BEST (or LOCKED, with what unlocks it)
            var briefing = new GameObject("DifficultyBriefing", typeof(RectTransform), typeof(Image));
            briefing.transform.SetParent(parent, false);
            difficultyBriefing = briefing.GetComponent<RectTransform>();
            StretchTop(difficultyBriefing, 0f, 0f, DifficultyBriefingTop, DifficultyBriefingHeight);
            var briefingImage = briefing.GetComponent<Image>();
            briefingImage.color = TerminalPlate;
            briefingImage.raycastTarget = false;
            AddCornerBrackets(difficultyBriefing, 10f, 2f, TerminalLine);

            Text wavesLabel;
            Text livesLabel;
            BuildDifficultyCell(briefing.transform, "Waves", "WAVES", 0f, 0.26f, out wavesLabel, out difficultyWavesValue, 28);
            BuildDifficultyCell(briefing.transform, "Lives", "LIVES", 0.26f, 0.50f, out livesLabel, out difficultyLivesValue, 28);
            BuildDifficultyCell(briefing.transform, "Record", "BEST", 0.50f, 1f, out difficultyRecordLabel, out difficultyRecord, 26);

            // Hairlines between the cells
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 0.26f : 0.50f;
                var line = new GameObject("CellDivider", typeof(RectTransform), typeof(Image));
                line.transform.SetParent(briefing.transform, false);
                var lineRect = line.GetComponent<RectTransform>();
                lineRect.anchorMin = new Vector2(x, 0f);
                lineRect.anchorMax = new Vector2(x, 1f);
                lineRect.pivot = new Vector2(0.5f, 0.5f);
                lineRect.sizeDelta = new Vector2(2f, -24f);
                lineRect.anchoredPosition = Vector2.zero;
                var lineImage = line.GetComponent<Image>();
                lineImage.color = new Color(TerminalLine.r, TerminalLine.g, TerminalLine.b, 0.3f);
                lineImage.raycastTarget = false;
            }
        }

        /// <summary>One cell of the info strip: a small dim label over a large value, between two x anchors.</summary>
        private void BuildDifficultyCell(Transform strip, string name, string label, float xMin, float xMax,
            out Text labelText, out Text valueText, int valueSize)
        {
            labelText = MakeText(strip, name + "Label", label, 11, TextAnchor.MiddleLeft, TextDim, FontStyle.Bold);
            StretchTop(labelText.rectTransform, 18f, 18f, 12f, 16f);
            labelText.rectTransform.anchorMin = new Vector2(xMin, 1f);
            labelText.rectTransform.anchorMax = new Vector2(xMax, 1f);

            valueText = MakeText(strip, name + "Value", "", valueSize, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            FitText(valueText, 12, valueSize);
            StretchTop(valueText.rectTransform, 18f, 18f, 30f, 40f);
            valueText.rectTransform.anchorMin = new Vector2(xMin, 1f);
            valueText.rectTransform.anchorMax = new Vector2(xMax, 1f);
        }

        /// <summary>Lets a Text shrink (down to minSize) instead of being cut off by its rect.</summary>
        private static void FitText(Text text, int minSize, int maxSize)
        {
            if (text == null) return;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = minSize;
            text.resizeTextMaxSize = maxSize;
        }

        // Difficulty slider geometry (column pixels): the slide area is inset so the knob and the end labels stay
        // inside the column; notches, labels and the knob all use the same 0 / 0.5 / 1 positions.
        private const float DifficultySliderTop = DifficultyLabelTop + 28f;
        private const float DifficultySliderInset = 46f;
        private const float DifficultyTrackHeight = 10f;
        private static readonly Color DifficultyTrackColor = new Color(0.020f, 0.050f, 0.034f, 1f);

        /// <summary>
        /// Stepped pixel slider: an outlined track with a fill in the selected colour, three square notches, a
        /// blocky knob (outline, colour, highlight row) and the tier names under the notches. A Unity Slider
        /// (whole numbers 0-2) drives it, with a transparent hit area over the whole row so a press anywhere jumps
        /// to the nearest notch.
        /// </summary>
        private void BuildDifficultySlider(Transform parent)
        {
            var root = new GameObject("DifficultySlider", typeof(RectTransform), typeof(Image), typeof(Slider));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            StretchTop(rootRect, 0f, 0f, DifficultySliderTop, 52f);
            Image hitArea = root.GetComponent<Image>();
            hitArea.color = new Color(0f, 0f, 0f, 0f);
            hitArea.raycastTarget = true;
            difficultySliderRoot = rootRect;

            // Track: dark channel with a 2 px outline
            var trackOutline = CreateBlock(root.transform, "TrackOutline", new Color(TerminalLine.r, TerminalLine.g, TerminalLine.b, 0.45f));
            StretchTop(trackOutline.rectTransform, DifficultySliderInset - 4f, DifficultySliderInset - 4f, 7f, DifficultyTrackHeight + 4f);
            var track = CreateBlock(root.transform, "Track", DifficultyTrackColor);
            StretchTop(track.rectTransform, DifficultySliderInset - 2f, DifficultySliderInset - 2f, 9f, DifficultyTrackHeight);

            // Fill and knob live in a slide area matching the notch positions
            var slideArea = new GameObject("SlideArea", typeof(RectTransform));
            slideArea.transform.SetParent(root.transform, false);
            var slideRect = slideArea.GetComponent<RectTransform>();
            StretchTop(slideRect, DifficultySliderInset, DifficultySliderInset, 9f, DifficultyTrackHeight);

            difficultyFill = CreateBlock(slideArea.transform, "Fill", AccentColor);
            difficultyFill.rectTransform.anchorMin = Vector2.zero;
            difficultyFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            difficultyFill.rectTransform.offsetMin = new Vector2(0f, 2f);
            difficultyFill.rectTransform.offsetMax = new Vector2(0f, -2f);

            // Tier names sit in a strip with the same insets as the slide area, so they line up with the notches
            var labelArea = new GameObject("LabelArea", typeof(RectTransform));
            labelArea.transform.SetParent(root.transform, false);
            var labelAreaRect = labelArea.GetComponent<RectTransform>();
            StretchTop(labelAreaRect, DifficultySliderInset, DifficultySliderInset, 34f, 16f);

            // Notches at 0 / 0.5 / 1
            difficultyNotches = new Image[DifficultyOrder.Length];
            difficultyNotchLabels = new Text[DifficultyOrder.Length];
            for (int i = 0; i < DifficultyOrder.Length; i++)
            {
                float fraction = DifficultyOrder.Length > 1 ? i / (float)(DifficultyOrder.Length - 1) : 0f;
                Image notch = CreateBlock(slideArea.transform, "Notch" + i, Color.white);
                notch.rectTransform.anchorMin = notch.rectTransform.anchorMax = new Vector2(fraction, 0.5f);
                notch.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                notch.rectTransform.sizeDelta = new Vector2(6f, 6f);
                difficultyNotches[i] = notch;

                Text label = MakeText(labelArea.transform, "NotchLabel" + i, DifficultyOrder[i].ToString().ToUpperInvariant(), 11,
                    TextAnchor.MiddleCenter, TextDim, FontStyle.Bold);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = labelRect.anchorMax = new Vector2(fraction, 0.5f);
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(100f, 16f);
                difficultyNotchLabels[i] = label;
            }

            // Knob: outline block, colour block, highlight row, dark centre slit
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            StretchTop(handleAreaRect, DifficultySliderInset, DifficultySliderInset, 0f, 28f);

            Image knobOutline = CreateBlock(handleArea.transform, "Knob", new Color(0.02f, 0.05f, 0.03f, 1f));
            knobOutline.rectTransform.sizeDelta = new Vector2(20f, 0f);
            difficultyKnob = CreateBlock(knobOutline.transform, "KnobFace", AccentColor);
            SetBlockInset(difficultyKnob.rectTransform, 2f);
            difficultyKnobHighlight = CreateBlock(difficultyKnob.transform, "KnobHighlight", new Color(1f, 1f, 1f, 0.45f));
            difficultyKnobHighlight.rectTransform.anchorMin = new Vector2(0f, 1f);
            difficultyKnobHighlight.rectTransform.anchorMax = new Vector2(1f, 1f);
            difficultyKnobHighlight.rectTransform.pivot = new Vector2(0.5f, 1f);
            difficultyKnobHighlight.rectTransform.offsetMin = new Vector2(0f, -3f);
            difficultyKnobHighlight.rectTransform.offsetMax = Vector2.zero;
            Image slit = CreateBlock(difficultyKnob.transform, "KnobSlit", new Color(0f, 0f, 0f, 0.35f));
            slit.rectTransform.anchorMin = new Vector2(0.5f, 0.2f);
            slit.rectTransform.anchorMax = new Vector2(0.5f, 0.75f);
            slit.rectTransform.sizeDelta = new Vector2(2f, 0f);

            difficultySlider = root.GetComponent<Slider>();
            difficultySlider.fillRect = difficultyFill.rectTransform;
            difficultySlider.handleRect = knobOutline.rectTransform;
            difficultySlider.targetGraphic = difficultyKnob;
            difficultySlider.transition = Selectable.Transition.None;
            difficultySlider.direction = Slider.Direction.LeftToRight;
            difficultySlider.minValue = 0f;
            difficultySlider.maxValue = DifficultyOrder.Length - 1;
            difficultySlider.wholeNumbers = true;
            difficultySlider.SetValueWithoutNotify(System.Array.IndexOf(DifficultyOrder, selectedDifficulty));
            difficultySlider.onValueChanged.AddListener(value =>
            {
                int index = Mathf.Clamp(Mathf.RoundToInt(value), 0, DifficultyOrder.Length - 1);
                if (DifficultyOrder[index] == selectedDifficulty) return;
                PlaySfx(clickClip, 0.9f + 0.1f * index);
                SetDifficulty(DifficultyOrder[index]);
            });
        }

        private static Image CreateBlock(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetBlockInset(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void SetDifficulty(StageDifficulty difficulty)
        {
            selectedDifficulty = difficulty;
            UpdateDifficultyDisplay();
        }

        private static readonly Color LockedDifficultyColor = new Color(0.55f, 0.58f, 0.56f, 1f);

        private bool IsDifficultyOpen(StageDifficulty difficulty) =>
            selectedStage == null || IsTutorialStage(selectedStage) || StageProgress.IsUnlocked(selectedStage.stageId, difficulty);

        /// <summary>Keeps the current difficulty if it's open on this stage, else Normal, Easy, then Hard.</summary>
        private static StageDifficulty PickOpenDifficulty(StageData stage, StageDifficulty preferred)
        {
            if (StageProgress.IsUnlocked(stage.stageId, preferred)) return preferred;
            foreach (StageDifficulty difficulty in new[] { StageDifficulty.Normal, StageDifficulty.Easy, StageDifficulty.Hard })
            {
                if (StageProgress.IsUnlocked(stage.stageId, difficulty)) return difficulty;
            }
            return preferred;
        }

        private void UpdateDifficultyDisplay()
        {
            Color selectedTint = GetDifficultyColor(selectedDifficulty);
            modalAccentTarget = selectedTint;

            if (difficultySlider != null)
            {
                int selectedIndex = System.Array.IndexOf(DifficultyOrder, selectedDifficulty);
                difficultySlider.SetValueWithoutNotify(selectedIndex);
                difficultyFill.color = new Color(selectedTint.r, selectedTint.g, selectedTint.b, 0.85f);
                difficultyKnob.color = selectedTint;

                for (int i = 0; i < DifficultyOrder.Length; i++)
                {
                    Color tint = GetDifficultyColor(DifficultyOrder[i]);
                    bool reached = i <= selectedIndex;
                    bool isSelected = i == selectedIndex;
                    difficultyNotches[i].color = reached ? Color.Lerp(tint, Color.white, 0.35f) : new Color(1f, 1f, 1f, 0.18f);
                    bool open = IsDifficultyOpen(DifficultyOrder[i]);
                    difficultyNotchLabels[i].color = isSelected ? (open ? tint : LockedDifficultyColor)
                        : new Color(TextDim.r, TextDim.g, TextDim.b, open ? 0.8f : 0.35f);
                }
            }

            bool unlocked = IsSelectedDifficultyUnlocked;
            bool hasStage = selectedStage != null && !IsTutorialStage(selectedStage);
            Color gold = new Color(0.98f, 0.78f, 0.28f, 1f);

            if (difficultyRecord != null)
            {
                if (!hasStage)
                {
                    difficultyRecordLabel.text = string.Empty;
                    difficultyRecord.text = string.Empty;
                }
                else if (!unlocked)
                {
                    difficultyRecordLabel.text = "LOCKED";
                    difficultyRecord.text = StageProgress.GetUnlockRequirement(selectedStage.stageId, selectedDifficulty);
                    difficultyRecord.color = LockedDifficultyColor;
                }
                else
                {
                    int best = StageProgress.GetBestStars(selectedStage.stageId, selectedDifficulty);
                    difficultyRecordLabel.text = "BEST";
                    difficultyRecord.text = best > 0
                        ? new string('★', best) + "<color=#3C424D>" + new string('☆', 3 - best) + "</color>"
                        : "NOT CLEARED";
                    difficultyRecord.color = best > 0 ? gold : TextDim;
                }
            }

            if (playStageButton != null)
            {
                Color playColor = unlocked ? AccentColor : new Color(0.20f, 0.24f, 0.22f, 1f);
                playStageButton.GetComponent<Image>().color = playColor;
                var playFeedback = playStageButton.GetComponent<MenuButtonFeedback>();
                if (playFeedback != null) playFeedback.SetBaseColor(playColor); // it lerps the image back to its base
                if (playStageLabel != null) playStageLabel.text = unlocked ? "▶  PLAY" : "LOCKED";
            }

            if (difficultyWavesValue != null && difficultyLivesValue != null)
            {
                int waves = GetSelectedStageWaveCount(selectedDifficulty);
                int lives = selectedStage != null ? selectedStage.GetLifePoints(selectedDifficulty) : 0;
                difficultyWavesValue.text = selectedStage != null ? (waves > 0 ? waves.ToString() : "?") : string.Empty;
                difficultyLivesValue.text = selectedStage != null ? lives.ToString() : string.Empty;
                difficultyWavesValue.color = selectedTint;
                difficultyLivesValue.color = selectedTint;
            }
        }

        private int GetSelectedStageWaveCount(StageDifficulty difficulty)
        {
            if (selectedStage == null) return 0;

            WaveData[] waves = selectedStage.GetWaves(difficulty);
            return waves != null ? waves.Length : 0; // 0 shows as "?" until the stage has waves (Wave Planner)
        }

        private static Color GetDifficultyColor(StageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case StageDifficulty.Easy: return new Color(0.30f, 0.86f, 0.58f, 1f);
                case StageDifficulty.Hard: return new Color(0.95f, 0.33f, 0.36f, 1f);
                default: return new Color(0.98f, 0.78f, 0.28f, 1f);
            }
        }

        private static int GetDifficultyLevel(StageDifficulty difficulty)
        {
            switch (difficulty)
            {
                case StageDifficulty.Easy: return 1;
                case StageDifficulty.Hard: return 3;
                default: return 2;
            }
        }
    }
}
