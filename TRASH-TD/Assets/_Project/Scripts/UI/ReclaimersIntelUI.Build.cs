using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // Building the dossier: the choice card, the gallery card and the detail pane.
    public partial class ReclaimersIntelUI
    {
        private void Build(Transform buttonParent, Action<Button> styleButton, Action<RectTransform> positionButton)
        {
            IntelButton = CreateTerminalButton(buttonParent, "ReclaimersIntelButton", "INTEL", 16);
            styleButton?.Invoke(IntelButton);
            positionButton?.Invoke(IntelButton.GetComponent<RectTransform>());
            IntelButton.onClick.AddListener(Open);

            Transform canvasRoot = transform.parent;
            panel = new GameObject("ReclaimersIntelPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            panel.transform.SetParent(canvasRoot, false);
            SetStretch(panel.GetComponent<RectTransform>());
            panel.GetComponent<Image>().color = DimColor;
            panelGroup = panel.GetComponent<CanvasGroup>();

            BuildChoiceCard();
            BuildGalleryCard();
            panel.SetActive(false);
        }

        private RectTransform CreateCard(string name, float width, float height, string title, out Text titleText, out Image headerLine)
        {
            Image cardImage = CreateImage(panel.transform, name, CardColor);
            cardImage.raycastTarget = true;
            RectTransform card = cardImage.rectTransform;
            SetCentered(card, width, height);
            Outline outline = cardImage.gameObject.AddComponent<Outline>();
            outline.effectColor = LineColor;
            outline.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(card, 26f, 3f, AccentBright, -7f);

            Image header = CreateImage(card, "Header", HeaderColor);
            PlaceTopLeft(header.rectTransform, 0f, 0f, width, HeaderHeight);
            headerLine = CreateImage(card, "HeaderLine", LineColor);
            PlaceTopLeft(headerLine.rectTransform, 0f, HeaderHeight, width, 2f);

            titleText = CreateText(header.transform, title, 20, TextAnchor.MiddleLeft, AccentBright);
            PlaceTopLeft(titleText.rectTransform, Padding - 8f, 0f, width * 0.6f, HeaderHeight);
            return card;
        }

        private void BuildChoiceCard()
        {
            choiceCard = CreateCard("IntelChoiceCard", ChoiceWidth, ChoiceHeight, "RECLAIMERS' INTEL", out _, out _);

            Button closeButton = CreateHeaderButton(choiceCard, "CloseButton", "CLOSE", ChoiceWidth - Padding - 140f, 140f);
            closeButton.onClick.AddListener(Close);

            Text subtitle = CreateText(choiceCard, "// SELECT A DOSSIER", 12, TextAnchor.MiddleLeft, AccentColor);
            PlaceTopLeft(subtitle.rectTransform, Padding, 84f, 300f, 24f);
            Image rule = CreateImage(choiceCard, "SubtitleRule", new Color(LineColor.r, LineColor.g, LineColor.b, 0.3f));
            PlaceTopLeft(rule.rectTransform, Padding + 260f, 95f, ChoiceWidth - Padding * 2f - 260f, 2f);

            float gap = ChoiceWidth - Padding * 2f - OptionWidth * 2f;
            operatorsOption = CreateDossierOption("OperatorsOption", Padding, "DOSSIER 01", "OPERATORS",
                AccentColor, AccentBright, PanelColor, false, out operatorsSprite, out operatorsCount);
            enemiesOption = CreateDossierOption("EnemiesOption", Padding + OptionWidth + gap, "DOSSIER 02", "ENEMIES",
                HostileColor, HostileBright, HostilePanel, true, out enemiesSprite, out enemiesCount);
            operatorsOption.onClick.AddListener(() => ShowGallery(Dossier.Operators));
            enemiesOption.onClick.AddListener(() => ShowGallery(Dossier.Enemies));
            operatorsOption.GetComponent<MenuButtonFeedback>().Hovered += () => SetChoiceFocus(0, false);
            enemiesOption.GetComponent<MenuButtonFeedback>().Hovered += () => SetChoiceFocus(1, false);

            Text hint = CreateText(choiceCard, "[ARROWS] SELECT    [ENTER] OPEN    [ESC] CLOSE", 10, TextAnchor.MiddleCenter, HintColor);
            PlaceTopLeft(hint.rectTransform, 0f, ChoiceHeight - 44f, ChoiceWidth, 24f);
        }

        private Button CreateDossierOption(string objectName, float x, string tag, string title, Color accent, Color bright,
            Color fill, bool hostile, out Image coverSprite, out Text countText)
        {
            Button option = CreateButtonObject(choiceCard, objectName, fill);
            RectTransform rect = option.GetComponent<RectTransform>();
            PlaceTopLeft(rect, x, 124f, OptionWidth, OptionHeight);
            Outline frame = option.gameObject.AddComponent<Outline>();
            frame.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            frame.effectDistance = new Vector2(2f, -2f);
            AddCornerBrackets(rect, 20f, 3f, bright, -6f);

            Text tagText = CreateText(rect, tag, 10, TextAnchor.MiddleLeft, new Color(accent.r, accent.g, accent.b, 0.85f));
            PlaceTopLeft(tagText.rectTransform, 20f, 14f, 200f, 20f);
            Text classified = CreateText(rect, hostile ? "THREAT FILE" : "PERSONNEL FILE", 10, TextAnchor.MiddleRight, new Color(accent.r, accent.g, accent.b, 0.6f));
            PlaceTopLeft(classified.rectTransform, OptionWidth - 220f, 14f, 200f, 20f);

            // Sprite on a gridded stage, still (no animation on the picker)
            float stageSize = 300f;
            CreateSpriteStage(rect, (OptionWidth - stageSize) * 0.5f, 44f, stageSize, accent, hostile, out _, out coverSprite, out _);

            Text titleText = CreateText(rect, title, 28, TextAnchor.MiddleCenter, bright);
            PlaceTopLeft(titleText.rectTransform, 0f, 356f, OptionWidth, 40f);
            countText = CreateText(rect, string.Empty, 11, TextAnchor.MiddleCenter, new Color(accent.r, accent.g, accent.b, 0.85f));
            PlaceTopLeft(countText.rectTransform, 0f, 400f, OptionWidth, 20f);

            if (hostile)
            {
                Image stripe = CreateHazardStripe(rect, "HazardStripe");
                PlaceTopLeft(stripe.rectTransform, 0f, OptionHeight - 14f, OptionWidth, 14f);
            }
            else
            {
                Image bar = CreateImage(rect, "AccentBar", new Color(accent.r, accent.g, accent.b, 0.8f));
                PlaceTopLeft(bar.rectTransform, 0f, OptionHeight - 6f, OptionWidth, 6f);
            }

            MenuButtonFeedback feedback = AttachFeedback(option, SfxId.UiConfirm, bright);
            feedback.hoverScale = 1.03f;
            feedback.glowOutline = frame;
            feedback.glowColor = new Color(bright.r, bright.g, bright.b, 1f);
            return option;
        }

        private void SetChoiceFocus(int index, bool playSound = true)
        {
            if (index == choiceFocus) return;
            choiceFocus = index;
            if (playSound && index >= 0) PlaySfx(SfxId.UiHover);
            SetGlow(operatorsOption, index == 0);
            SetGlow(enemiesOption, index == 1);
        }

        private static void SetGlow(Button button, bool on)
        {
            if (button == null) return;
            MenuButtonFeedback feedback = button.GetComponent<MenuButtonFeedback>();
            if (feedback != null) feedback.glowAlways = on;
        }

        private void BuildGalleryCard()
        {
            galleryCard = CreateCard("IntelGalleryCard", GalleryWidth, GalleryHeight, string.Empty, out galleryTitle, out galleryHeaderLine);

            Button closeButton = CreateHeaderButton(galleryCard, "CloseButton", "CLOSE", GalleryWidth - Padding - 140f, 140f);
            closeButton.onClick.AddListener(Close);
            Button backButton = CreateHeaderButton(galleryCard, "BackButton", "< BACK", GalleryWidth - Padding - 140f - 16f - 160f, 160f);
            backButton.onClick.AddListener(ShowChoice);

            // ── Left: filters + grid ──
            tabRow = CreateRect(galleryCard, "TabRow");
            PlaceTopLeft(tabRow, ListX, 84f, ListWidth, 40f);

            listCountText = CreateText(galleryCard, string.Empty, 10, TextAnchor.MiddleLeft, HintColor);
            PlaceTopLeft(listCountText.rectTransform, ListX, 136f, ListWidth, 20f);

            GameObject scrollObject = new GameObject("GridScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(galleryCard, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            PlaceTopLeft(scrollRect, ListX, GridTop, ListWidth, GalleryHeight - GridTop - 32f);
            scrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);

            gridContent = CreateRect(scrollObject.transform, "GridContent");
            gridContent.anchorMin = new Vector2(0f, 1f);
            gridContent.anchorMax = new Vector2(1f, 1f);
            gridContent.pivot = new Vector2(0.5f, 1f);
            gridContent.sizeDelta = Vector2.zero;
            var grid = gridContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(TileWidth, TileHeight);
            grid.spacing = new Vector2(TileGap, TileGap);
            grid.padding = new RectOffset(16, 16, 16, 16);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            var fitter = gridContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            gridScroll = scrollObject.GetComponent<ScrollRect>();
            gridScroll.content = gridContent;
            gridScroll.viewport = scrollRect;
            gridScroll.horizontal = false;
            gridScroll.vertical = true;
            gridScroll.movementType = ScrollRect.MovementType.Clamped;
            gridScroll.scrollSensitivity = 40f;

            Image divider = CreateImage(galleryCard, "Divider", new Color(LineColor.r, LineColor.g, LineColor.b, 0.3f));
            PlaceTopLeft(divider.rectTransform, DetailX - 24f, 84f, 2f, GalleryHeight - 116f);

            BuildDetailPane();
        }

        private void BuildDetailPane()
        {
            // ── Sprite stage ──
            CreateSpriteStage(galleryCard, DetailX, 84f, StageSize, AccentColor, false, out stageBackground, out stageSprite, out stageFloor);

            // ── Identity column ──
            fileText = CreateText(galleryCard, string.Empty, 10, TextAnchor.MiddleLeft, HintColor);
            PlaceTopLeft(fileText.rectTransform, InfoX, 88f, InfoWidth, 20f);
            nameText = CreateText(galleryCard, string.Empty, 28, TextAnchor.MiddleLeft, AccentBright);
            PlaceTopLeft(nameText.rectTransform, InfoX, 112f, InfoWidth, 40f);
            chipRow = CreateRect(galleryCard, "ChipRow");
            PlaceTopLeft(chipRow, InfoX, 164f, InfoWidth, 32f);
            rarityRow = CreateRect(galleryCard, "RarityRow");
            PlaceTopLeft(rarityRow, InfoX, 210f, InfoWidth, 24f);
            statusText = CreateText(galleryCard, string.Empty, 10, TextAnchor.MiddleLeft, LabelColor);
            PlaceTopLeft(statusText.rectTransform, InfoX, 212f, InfoWidth, 20f);

            // ── Field notes (beside the sprite) ──
            Image notesBar = CreateImage(galleryCard, "FieldNotesBar", new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.6f));
            PlaceTopLeft(notesBar.rectTransform, InfoX, 248f, 4f, 148f);
            Text fieldNotesHeader = CreateText(galleryCard, "// FIELD NOTES", 12, TextAnchor.MiddleLeft, AccentColor);
            PlaceTopLeft(fieldNotesHeader.rectTransform, InfoX + 20f, 248f, InfoWidth - 20f, 20f);
            // Scrolls when the notes are longer than the box, so nothing is cut off
            GameObject loreScrollObject = new GameObject("FieldNotesScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            loreScrollObject.transform.SetParent(galleryCard, false);
            RectTransform loreScrollRect = loreScrollObject.GetComponent<RectTransform>();
            PlaceTopLeft(loreScrollRect, InfoX + 20f, 274f, InfoWidth - 20f, 122f);
            loreScrollObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f); // invisible, but catches the mouse wheel

            loreText = CreateText(loreScrollRect, string.Empty, LoreFontSize, TextAnchor.UpperLeft, BodyColor);
            loreText.horizontalOverflow = HorizontalWrapMode.Wrap;
            loreText.verticalOverflow = VerticalWrapMode.Overflow;
            loreText.lineSpacing = 1.35f;
            RectTransform loreTextRect = loreText.rectTransform;
            loreTextRect.anchorMin = new Vector2(0f, 1f);
            loreTextRect.anchorMax = new Vector2(1f, 1f);
            loreTextRect.pivot = new Vector2(0.5f, 1f);
            loreTextRect.offsetMin = new Vector2(0f, 0f);
            loreTextRect.offsetMax = new Vector2(-10f, 0f); // right gutter so text never sits under the scrolling edge
            loreText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            loreScroll = loreScrollObject.GetComponent<ScrollRect>();
            loreScroll.content = loreTextRect;
            loreScroll.viewport = loreScrollRect;
            loreScroll.horizontal = false;
            loreScroll.movementType = ScrollRect.MovementType.Clamped;
            loreScroll.scrollSensitivity = 30f;
            AttachScrollbar(loreScroll);

            // ── Combat data ──
            Image statsPanel = CreateImage(galleryCard, "StatsPanel", new Color(0f, 0f, 0f, 0.22f));
            PlaceTopLeft(statsPanel.rectTransform, DetailX, DataTop, StatsWidth, DataHeight);
            statsHeader = CreateSectionHeader(statsPanel.rectTransform, "COMBAT DATA", 16f, 14f, 200f);
            for (int i = 0; i < 3; i++) tierButtons.Add(CreateTierButton(statsPanel.rectTransform, i));

            string[] statNames = { "HP", "ATK", "DEF", "RES" };
            for (int i = 0; i < statNames.Length; i++) statRows.Add(CreateStatRow(statsPanel.rectTransform, statNames[i], 56f + i * 34f));

            for (int i = 0; i < 4; i++) factBoxes.Add(CreateFactBox(statsPanel.rectTransform, 0f, 200f, 0f));
            LayoutFacts(4);

            // ── Range ──
            Image rangePanel = CreateImage(galleryCard, "RangePanel", new Color(0f, 0f, 0f, 0.22f));
            PlaceTopLeft(rangePanel.rectTransform, RangeX, DataTop, RangeWidth, DataHeight);
            rangeHeader = CreateSectionHeader(rangePanel.rectTransform, "RANGE", 16f, 14f, 120f);
            rangeReadout = CreateText(rangePanel.rectTransform, string.Empty, 10, TextAnchor.MiddleRight, LabelColor);
            PlaceTopLeft(rangeReadout.rectTransform, 140f, 14f, RangeWidth - 156f, 24f);
            rangeGrid = CreateRect(rangePanel.rectTransform, "RangeGrid");
            rangeGrid.anchorMin = rangeGrid.anchorMax = new Vector2(0.5f, 1f);
            rangeGrid.pivot = new Vector2(0.5f, 0.5f);
            rangeGrid.anchoredPosition = new Vector2(0f, -144f);
            rangeGrid.sizeDelta = new Vector2(RangeWidth - 32f, 176f);
            rangeLegend = CreateRect(rangePanel.rectTransform, "RangeLegend");
            PlaceTopLeft(rangeLegend, 16f, DataHeight - 34f, RangeWidth - 32f, 20f);

            // ── Notes (scrolls if long) ──
            GameObject notesObject = new GameObject("NotesScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            notesObject.transform.SetParent(galleryCard, false);
            RectTransform notesRect = notesObject.GetComponent<RectTransform>();
            PlaceTopLeft(notesRect, DetailX, NotesTop, DetailWidth, GalleryHeight - NotesTop - 32f);
            notesObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.22f);

            RectTransform notesContent = CreateRect(notesObject.transform, "NotesContent");
            notesContent.anchorMin = new Vector2(0f, 1f);
            notesContent.anchorMax = new Vector2(1f, 1f);
            notesContent.pivot = new Vector2(0.5f, 1f);
            notesContent.sizeDelta = Vector2.zero;
            var layout = notesContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            notesContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            notesScroll = notesObject.GetComponent<ScrollRect>();
            notesScroll.content = notesContent;
            notesScroll.viewport = notesRect;
            notesScroll.horizontal = false;
            notesScroll.movementType = ScrollRect.MovementType.Clamped;
            notesScroll.scrollSensitivity = 30f;
            AttachScrollbar(notesScroll);

            abilityHeader = CreateNotesText(notesContent, "// ABILITY", 12, AccentColor);
            abilityText = CreateNotesText(notesContent, string.Empty, NotesFontSize, BodyColor);
            sightedHeader = CreateNotesText(notesContent, "// SIGHTED AT", 12, AccentColor);
            sightedText = CreateNotesText(notesContent, string.Empty, NotesFontSize, BodyColor);
        }

        /// <summary>
        /// Adds a thin, dim scrollbar along the right edge of a vertical scroll view. It only shows when the
        /// content is longer than the view, so it doubles as the "this scrolls" hint.
        /// </summary>
        private static void AttachScrollbar(ScrollRect scroll)
        {
            const float width = 4f;
            const float inset = 3f;

            GameObject barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(scroll.transform, false);
            RectTransform barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(width, -inset * 2f);
            barRect.anchoredPosition = new Vector2(-inset, 0f);

            Image track = barObject.GetComponent<Image>();
            track.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.12f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(barObject.transform, false);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.sizeDelta = Vector2.zero;
            Image handle = handleObject.GetComponent<Image>();
            handle.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.55f);

            Scrollbar bar = barObject.GetComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handleRect;
            bar.targetGraphic = handle;

            scroll.verticalScrollbar = bar;
            // AutoHide (not ...AndExpandViewport) keeps the text width fixed whether or not the bar shows
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private Text CreateNotesText(RectTransform parent, string content, int size, Color color)
        {
            Text text = CreateText(parent, content, size, TextAnchor.UpperLeft, color);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.45f;
            return text;
        }

        private Text CreateSectionHeader(RectTransform parent, string label, float x, float y, float width)
        {
            Text header = CreateText(parent, "// " + label, 12, TextAnchor.MiddleLeft, AccentColor);
            PlaceTopLeft(header.rectTransform, x, y, width, 24f);
            return header;
        }

        private TierButton CreateTierButton(RectTransform parent, int index)
        {
            const float width = 88f;
            Button button = CreateButtonObject(parent, "TierButton_" + index, TerminalButton);
            PlaceTopLeft(button.GetComponent<RectTransform>(), StatsWidth - 16f - (3 - index) * (width + 6f) + 6f, 10f, width, 30f);

            Image star = CreateImage(button.transform, "Star", AmberColor);
            star.sprite = GameplayHUDUI.LoadIconSprite("icon_star");
            star.preserveAspect = true;
            PlaceTopLeft(star.rectTransform, 10f, 7f, 16f, 16f);

            Text label = CreateText(button.transform, string.Empty, 10, TextAnchor.MiddleCenter, LabelColor);
            SetStretch(label.rectTransform);
            int captured = index;
            button.onClick.AddListener(() => SetTier(captured));
            AttachFeedback(button, SfxId.UiClick, AccentBright);
            return new TierButton { Button = button, Label = label, Star = star };
        }

        private StatRow CreateStatRow(RectTransform parent, string label, float y)
        {
            Text name = CreateText(parent, label, 12, TextAnchor.MiddleLeft, LabelColor);
            PlaceTopLeft(name.rectTransform, 16f, y, 60f, 24f);

            const float trackX = 84f;
            float trackWidth = StatsWidth - trackX - 16f - 88f;
            Image track = CreateImage(parent, label + "Track", new Color(0.02f, 0.06f, 0.04f, 1f));
            PlaceTopLeft(track.rectTransform, trackX, y + 6f, trackWidth, 12f);
            Outline outline = track.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(LineColor.r, LineColor.g, LineColor.b, 0.35f);
            outline.effectDistance = new Vector2(1f, -1f);

            Image fill = CreateImage(track.rectTransform, "Fill", AccentColor);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;

            Text value = CreateText(parent, "0", 12, TextAnchor.MiddleRight, AccentBright);
            PlaceTopLeft(value.rectTransform, StatsWidth - 16f - 80f, y, 80f, 24f);
            return new StatRow { Fill = fill, Value = value };
        }

        private FactBox CreateFactBox(RectTransform parent, float x, float y, float width)
        {
            Image box = CreateImage(parent, "Fact", TerminalButton);
            PlaceTopLeft(box.rectTransform, x, y, width, 56f);
            Outline outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(LineColor.r, LineColor.g, LineColor.b, 0.25f);
            outline.effectDistance = new Vector2(1f, -1f);

            Text label = CreateText(box.rectTransform, string.Empty, 8, TextAnchor.MiddleCenter, HintColor);
            PlaceTopLeft(label.rectTransform, 0f, 8f, width, 16f);
            Text value = CreateText(box.rectTransform, string.Empty, 13, TextAnchor.MiddleCenter, AccentBright);
            PlaceTopLeft(value.rectTransform, 0f, 26f, width, 22f);
            return new FactBox { Root = box.rectTransform, Label = label, Value = value };
        }

        // Spreads the first `count` fact boxes evenly across the stats panel and hides the rest.
        private void LayoutFacts(int count)
        {
            float factWidth = (StatsWidth - 32f - (count - 1) * 8f) / count;
            for (int i = 0; i < factBoxes.Count; i++)
            {
                FactBox fact = factBoxes[i];
                bool shown = i < count;
                fact.Root.gameObject.SetActive(shown);
                if (!shown) continue;
                PlaceTopLeft(fact.Root, 16f + i * (factWidth + 8f), 200f, factWidth, 56f);
                PlaceTopLeft(fact.Label.rectTransform, 0f, 8f, factWidth, 16f);
                PlaceTopLeft(fact.Value.rectTransform, 0f, 26f, factWidth, 22f);
            }
        }

        // ─────────────────────────────────────────────────────
        //  Gallery: theme, tabs, grid
        // ─────────────────────────────────────────────────────

        private bool Hostile => dossier == Dossier.Enemies;
        private Color Accent => Hostile ? HostileColor : AccentColor;
        private Color Bright => Hostile ? HostileBright : AccentBright;
        private List<IntelEntry> Entries => Hostile ? enemyEntries : operatorEntries;

        private void ApplyDossierTheme()
        {
            galleryTitle.text = Hostile ? "RECLAIMERS' INTEL // ENEMIES" : "RECLAIMERS' INTEL // OPERATORS";
            galleryTitle.color = Bright;
            galleryHeaderLine.color = new Color(Accent.r, Accent.g, Accent.b, 0.55f);
            stageBackground.color = Hostile ? new Color(0.03f, 0.012f, 0.014f, 1f) : StageColor;
            stageFloor.color = new Color(Accent.r, Accent.g, Accent.b, 0.8f);
            nameText.color = Bright;
            statsHeader.text = Hostile ? "// THREAT DATA" : "// COMBAT DATA";
        }
    }
}
