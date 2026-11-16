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

namespace TrashTD.UI
{
    // Building the details panel and the range preview.
    public partial class CardDraftOverlayUI
    {
        private void BuildDetailsPanel(Transform parent)
        {
            var panel = new GameObject("DetailsPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            detailsPanelRect = panel.GetComponent<RectTransform>();
            detailsPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
            detailsPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
            detailsPanelRect.pivot = new Vector2(0.5f, 0.5f);
            detailsPanelRect.sizeDelta = new Vector2(PanelWidth, CardHeight);
            detailsPanelRect.anchoredPosition = new Vector2(PanelCenterX, ContentCenterY);

            GameplayHUDUI.ApplyPanelContainer(panel, GameplayHUDUI.PanelContainerType.Metal);

            var border = panel.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.08f);
            border.effectDistance = new Vector2(2f, -2f);

            // Rarity-colored stripe along the top edge
            var accent = new GameObject("DetailAccent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(panel.transform, false);
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(-32f, 4f);
            accentRect.anchoredPosition = new Vector2(0f, -14f);
            detailAccent = accent.GetComponent<Image>();
            detailAccent.color = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.35f);
            detailAccent.raycastTarget = false;

            // --- Placeholder (nothing selected) ---
            detailEmptyRoot = new GameObject("Empty", typeof(RectTransform));
            detailEmptyRoot.transform.SetParent(panel.transform, false);
            StretchFill(detailEmptyRoot.GetComponent<RectTransform>());

            var emptyTitle = MakeText(detailEmptyRoot.transform, "EmptyTitle", "SELECT A CARD", 24,
                TextAnchor.MiddleCenter, new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.8f));
            emptyTitle.fontStyle = FontStyle.Bold;
            PositionRT(emptyTitle, new Vector2(0f, 14f), new Vector2(0.5f, 0.5f), new Vector2(PanelWidth - 60f, 40f));

            var emptySub = MakeText(detailEmptyRoot.transform, "EmptySubtitle", "to see its full details here", 14,
                TextAnchor.MiddleCenter, new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.5f));
            PositionRT(emptySub, new Vector2(0f, -22f), new Vector2(0.5f, 0.5f), new Vector2(PanelWidth - 60f, 28f));

            // --- Content (operator selected) ---
            detailContentRoot = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            detailContentRoot.transform.SetParent(panel.transform, false);
            detailContentRect = detailContentRoot.GetComponent<RectTransform>();
            StretchFill(detailContentRect);
            detailContentGroup = detailContentRoot.GetComponent<CanvasGroup>();
            Transform content = detailContentRoot.transform;

            const float margin = 28f;
            float innerWidth = PanelWidth - margin * 2f;

            // Name + stars
            detailNameText = MakeText(content, "DetailName", "", 32, TextAnchor.MiddleLeft, Color.white);
            detailNameText.fontStyle = FontStyle.Bold;
            detailNameText.resizeTextForBestFit = true;
            detailNameText.resizeTextMinSize = 18;
            detailNameText.resizeTextMaxSize = 32;
            StretchTopRT(detailNameText, margin, margin, 28f, 42f);

            detailStarsText = MakeText(content, "DetailStars", "", 22, TextAnchor.MiddleLeft, STAR_COLOR);
            StretchTopRT(detailStarsText, margin, margin, 72f, 28f);

            // Separate standalone class icon and 3 type chips (class / position / damage type)
            const float classIconSize = 34f;
            var classIconObj = new GameObject("DetailClassIcon", typeof(RectTransform), typeof(Image));
            classIconObj.transform.SetParent(content, false);
            var detailClassIconImg = classIconObj.GetComponent<Image>();
            detailClassIconImg.preserveAspect = true;
            detailClassIconImg.raycastTarget = false;
            detailClassIconImg.color = Color.white;
            PlaceTopLeft(classIconObj.transform, margin, 114f, classIconSize, classIconSize);

            float chipsStartX = margin + classIconSize + 10f;
            float chipWidth = (innerWidth - classIconSize - 10f - 20f) / 3f;
            detailClassChip = CreateChip(content, "ClassChip", chipsStartX, 114f, chipWidth, 34f, false);
            detailClassChip.icon = detailClassIconImg;
            detailPositionChip = CreateChip(content, "PositionChip", chipsStartX + chipWidth + 10f, 114f, chipWidth, 34f, false);
            detailDamageChip = CreateChip(content, "DamageChip", chipsStartX + (chipWidth + 10f) * 2f, 114f, chipWidth, 34f, false);

            // Role line (role tags / class summary)
            detailRoleText = MakeText(content, "DetailRole", "", 12, TextAnchor.MiddleLeft,
                new Color(0.70f, 0.85f, 0.95f, 0.85f));
            detailRoleText.resizeTextForBestFit = true;
            detailRoleText.resizeTextMinSize = 9;
            detailRoleText.resizeTextMaxSize = 12;
            StretchTopRT(detailRoleText, margin, margin, 156f, 22f);

            CreateDivider(content, 190f, margin);

            // Stats: three columns, two rows (this leaves the bottom half of the panel for the ability text)
            var statsLabel = MakeText(content, "StatsLabel", "STATS", 11, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            statsLabel.fontStyle = FontStyle.Bold;
            StretchTopRT(statsLabel, margin, margin, 202f, 18f);

            const float tileGap = 10f;
            float tileWidth = (innerWidth - tileGap * 2f) / 3f;
            float tileHeight = 58f;
            float rowStep = tileHeight + 8f;
            float gridTop = 226f;
            float colX1 = margin + tileWidth + tileGap;
            float colX2 = margin + (tileWidth + tileGap) * 2f;

            detailHpText = CreateStatTile(content, "HP", new Color(0.35f, 0.85f, 0.45f, 1f), margin, gridTop, tileWidth, tileHeight);
            detailAtkText = CreateStatTile(content, "ATK", new Color(0.95f, 0.40f, 0.35f, 1f), colX1, gridTop, tileWidth, tileHeight);
            detailDefText = CreateStatTile(content, "DEF", new Color(0.35f, 0.60f, 1.00f, 1f), colX2, gridTop, tileWidth, tileHeight);
            detailResText = CreateStatTile(content, "RES", new Color(0.70f, 0.45f, 1.00f, 1f), margin, gridTop + rowStep, tileWidth, tileHeight);
            detailBlockText = CreateStatTile(content, "BLOCK", new Color(1.00f, 0.80f, 0.30f, 1f), colX1, gridTop + rowStep, tileWidth, tileHeight);
            detailIntervalText = CreateStatTile(content, "ATK SPD", new Color(0.40f, 0.85f, 0.90f, 1f), colX2, gridTop + rowStep, tileWidth, tileHeight);

            // Bottom section: range preview (left) + ability (right, scrolls)
            const float sectionLabelTop = 372f;
            const float sectionBodyTop = 396f;
            CreateDivider(content, 362f, margin);
            BuildRangePreview(content, margin, sectionLabelTop, sectionBodyTop);

            float abilityX = margin + RangeBoxWidth + 20f;
            float abilityBodyHeight = CardHeight - sectionBodyTop - 28f;

            // Passive / ability (hidden when the operator has none)
            detailPassiveLabel = MakeText(content, "PassiveLabel", "ABILITY", 11, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            detailPassiveLabel.fontStyle = FontStyle.Bold;
            StretchTopRT(detailPassiveLabel, abilityX, margin, sectionLabelTop, 18f);

            // The text keeps one readable size and scrolls (wheel, drag or the thin bar) when it doesn't fit
            var scrollObject = new GameObject("PassiveScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            scrollObject.transform.SetParent(content, false);
            RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
            PlaceTopLeft(scrollRect, abilityX, sectionBodyTop, PanelWidth - abilityX - margin, abilityBodyHeight);
            var scrollBackground = scrollObject.GetComponent<Image>();
            scrollBackground.color = new Color(0f, 0f, 0f, 0f); // invisible, but catches the mouse wheel

            detailPassiveText = MakeText(scrollRect, "PassiveText", "", 14, TextAnchor.UpperLeft,
                new Color(0.88f, 0.92f, 0.96f, 1f));
            detailPassiveText.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailPassiveText.verticalOverflow = VerticalWrapMode.Overflow;
            detailPassiveText.lineSpacing = 1.3f;
            RectTransform passiveRect = detailPassiveText.rectTransform;
            passiveRect.anchorMin = new Vector2(0f, 1f);
            passiveRect.anchorMax = new Vector2(1f, 1f);
            passiveRect.pivot = new Vector2(0.5f, 1f);
            passiveRect.offsetMin = Vector2.zero;
            passiveRect.offsetMax = new Vector2(-12f, 0f); // right gutter so text never sits under the scroll bar
            detailPassiveText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            detailPassiveScroll = scrollObject.GetComponent<ScrollRect>();
            detailPassiveScroll.content = passiveRect;
            detailPassiveScroll.viewport = scrollRect;
            detailPassiveScroll.horizontal = false;
            detailPassiveScroll.movementType = ScrollRect.MovementType.Clamped;
            detailPassiveScroll.scrollSensitivity = 30f;
            AttachDetailScrollbar(detailPassiveScroll);
            detailContentRoot.SetActive(false);
        }

        /// <summary>A thin scroll bar down the right edge of a scroll view; it hides itself when everything fits.</summary>
        private static void AttachDetailScrollbar(ScrollRect scroll)
        {
            const float width = 4f;
            const float inset = 2f;

            var barObject = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            barObject.transform.SetParent(scroll.transform, false);
            var barRect = barObject.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(1f, 0f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(1f, 0.5f);
            barRect.sizeDelta = new Vector2(width, -inset * 2f);
            barRect.anchoredPosition = new Vector2(-inset, 0f);

            var track = barObject.GetComponent<Image>();
            track.color = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.12f);

            var handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(barObject.transform, false);
            handleObject.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            var handle = handleObject.GetComponent<Image>();
            handle.color = new Color(ACCENT_COLOR.r, ACCENT_COLOR.g, ACCENT_COLOR.b, 0.6f);

            var bar = barObject.GetComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handleObject.GetComponent<RectTransform>();
            bar.targetGraphic = handle;

            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void BuildRangePreview(Transform parent, float x, float labelTop, float boxTop)
        {
            var rangeLabel = MakeText(parent, "RangeLabel", "RANGE", 11, TextAnchor.MiddleLeft, STAT_LABEL_COLOR);
            rangeLabel.fontStyle = FontStyle.Bold;
            PlaceTopLeft(rangeLabel.transform, x, labelTop, RangeBoxWidth, 18f);

            // Facing hint: the diagram is drawn facing right, the default placement facing.
            var facingText = MakeText(parent, "RangeFacing", "FACING", 10, TextAnchor.MiddleRight,
                new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.7f));
            PlaceTopLeft(facingText.transform, x, labelTop, RangeBoxWidth - 16f, 18f);

            Sprite arrowSprite = LoadUiSprite("UI/minis/mini_arrow_right", Vector4.zero);
            if (arrowSprite != null)
            {
                var arrow = new GameObject("RangeFacingArrow", typeof(RectTransform), typeof(Image));
                arrow.transform.SetParent(parent, false);
                PlaceTopLeft(arrow.transform, x + RangeBoxWidth - 12f, labelTop + 4f, 10f, 10f);
                var arrowImage = arrow.GetComponent<Image>();
                arrowImage.sprite = arrowSprite;
                arrowImage.preserveAspect = true;
                arrowImage.color = SELF_TILE_COLOR;
                arrowImage.raycastTarget = false;
            }

            var box = new GameObject("RangeBox", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(parent, false);
            PlaceTopLeft(box.transform, x, boxTop, RangeBoxWidth, RangeBoxHeight);
            GameplayHUDUI.ApplyPanelContainer(box, GameplayHUDUI.PanelContainerType.Dark);
            box.GetComponent<Image>().raycastTarget = false;

            var grid = new GameObject("RangeGrid", typeof(RectTransform));
            grid.transform.SetParent(box.transform, false);
            detailRangeGrid = grid.GetComponent<RectTransform>();
            detailRangeGrid.anchorMin = new Vector2(0.5f, 0.5f);
            detailRangeGrid.anchorMax = new Vector2(0.5f, 0.5f);
            detailRangeGrid.pivot = new Vector2(0.5f, 0.5f);
            detailRangeGrid.sizeDelta = Vector2.zero;
            detailRangeGrid.anchoredPosition = Vector2.zero;

            // Colour key under the box
            BuildRangeLegendEntry(parent, x, boxTop + RangeBoxHeight + 10f, SELF_TILE_COLOR, "OPERATOR");
            BuildRangeLegendEntry(parent, x, boxTop + RangeBoxHeight + 32f, RANGE_TILE_COLOR, "IN RANGE");
        }

        private void BuildRangeLegendEntry(Transform parent, float x, float y, Color color, string label)
        {
            var swatch = new GameObject("RangeLegendSwatch", typeof(RectTransform), typeof(Image));
            swatch.transform.SetParent(parent, false);
            PlaceTopLeft(swatch.transform, x + 2f, y + 4f, 10f, 10f);
            var swatchImage = swatch.GetComponent<Image>();
            swatchImage.color = color;
            swatchImage.raycastTarget = false;

            var text = MakeText(parent, "RangeLegendLabel", label, 11, TextAnchor.MiddleLeft,
                new Color(STAT_LABEL_COLOR.r, STAT_LABEL_COLOR.g, STAT_LABEL_COLOR.b, 0.9f));
            PlaceTopLeft(text.transform, x + 20f, y, RangeBoxWidth - 20f, 18f);
        }

        /// <summary>
        /// Draws <see cref="OperatorData.rangePattern"/> — the same offsets combat uses via
        /// GridManager.GetCellsInRange — as a tile grid facing right.
        /// </summary>
        private void UpdateRangePreview(OperatorData op, Vector2Int[] rangePattern)
        {
            if (detailRangeGrid == null) return;

            for (int i = detailRangeGrid.childCount - 1; i >= 0; i--)
            {
                Destroy(detailRangeGrid.GetChild(i).gameObject);
            }

            var rangeCells = new HashSet<Vector2Int>();
            if (rangePattern != null)
            {
                foreach (Vector2Int offset in rangePattern) rangeCells.Add(offset);
            }

            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            foreach (Vector2Int cell in rangeCells)
            {
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            // Pad small patterns to at least 3x3 so the operator's own tile has context around it.
            while (maxX - minX + 1 < 3) { if (-minX <= maxX) minX--; else maxX++; }
            while (maxY - minY + 1 < 3) { if (-minY <= maxY) minY--; else maxY++; }

            int cols = maxX - minX + 1;
            int rows = maxY - minY + 1;
            float availableWidth = RangeBoxWidth - RangeBoxPadding * 2f;
            float availableHeight = RangeBoxHeight - RangeBoxPadding * 2f;
            float tileSize = Mathf.Floor(Mathf.Min(MaxRangeTileSize,
                (availableWidth - RangeTileGap * (cols - 1)) / cols,
                (availableHeight - RangeTileGap * (rows - 1)) / rows));
            float step = tileSize + RangeTileGap;
            Sprite classIcon = GameplayHUDUI.GetClassIconSprite(op.operatorClass);

            for (int gy = minY; gy <= maxY; gy++)
            {
                for (int gx = minX; gx <= maxX; gx++)
                {
                    var cell = new Vector2Int(gx, gy);
                    var position = new Vector2(
                        Mathf.Round((gx - minX - (cols - 1) * 0.5f) * step),
                        Mathf.Round((gy - minY - (rows - 1) * 0.5f) * step));
                    bool isSelf = cell == Vector2Int.zero;
                    bool inRange = rangeCells.Contains(cell);
                    Color frame = isSelf ? SELF_TILE_COLOR : inRange ? RANGE_TILE_COLOR : EMPTY_TILE_COLOR;
                    CreateRangeTile(position, tileSize, frame, inRange, isSelf ? classIcon : null);
                }
            }
        }

        private void CreateRangeTile(Vector2 position, float size, Color frameColor, bool filled, Sprite icon)
        {
            if (rangeTileSprite == null)
            {
                rangeTileSprite = LoadUiSprite("UI/controls/checkbox_off", new Vector4(3f, 3f, 3f, 3f));
            }

            var tile = new GameObject("RangeTile", typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(detailRangeGrid, false);
            var tileRect = tile.GetComponent<RectTransform>();
            tileRect.sizeDelta = new Vector2(size, size);
            tileRect.anchoredPosition = position;

            var frame = tile.GetComponent<Image>();
            frame.raycastTarget = false;
            if (rangeTileSprite != null)
            {
                frame.sprite = rangeTileSprite;
                frame.type = Image.Type.Sliced;
                frame.color = frameColor;
            }
            else
            {
                frame.color = new Color(frameColor.r, frameColor.g, frameColor.b, frameColor.a * 0.5f);
            }

            float inset = Mathf.Min(3f, size * 0.2f);
            if (filled)
            {
                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                fill.transform.SetParent(tile.transform, false);
                var fillRect = fill.GetComponent<RectTransform>();
                StretchFill(fillRect);
                fillRect.offsetMin = new Vector2(inset, inset);
                fillRect.offsetMax = new Vector2(-inset, -inset);
                var fillImage = fill.GetComponent<Image>();
                fillImage.color = new Color(RANGE_TILE_COLOR.r, RANGE_TILE_COLOR.g, RANGE_TILE_COLOR.b, 0.3f);
                fillImage.raycastTarget = false;
            }

            if (icon != null)
            {
                var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(tile.transform, false);
                var iconRect = iconObject.GetComponent<RectTransform>();
                StretchFill(iconRect);
                float iconInset = inset + 1f;
                iconRect.offsetMin = new Vector2(iconInset, iconInset);
                iconRect.offsetMax = new Vector2(-iconInset, -iconInset);
                var iconImage = iconObject.GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
        }
    }
}
