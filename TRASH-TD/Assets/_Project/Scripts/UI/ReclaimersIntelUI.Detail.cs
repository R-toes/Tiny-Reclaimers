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
    // The detail pane: chips, tiers, stats, facts and the range preview.
    public partial class ReclaimersIntelUI
    {
        private void BuildChips(IntelEntry entry)
        {
            ClearChildren(chipRow);
            float x = 0f;
            if (entry.Operator != null)
            {
                OperatorData op = entry.Operator;
                x = AddChip(x, op.operatorClass.ToString().ToUpperInvariant(), AccentBright, GameplayHUDUI.GetClassIconSprite(op.operatorClass));
                x = AddChip(x, PositionLabel(op.position), LabelColor, null);
                AddChip(x, DamageLabel(op.damageType), DamageColor(op.damageType), null);
            }
            else
            {
                EnemyData enemy = entry.Enemy;
                x = AddChip(x, ArchetypeLabel(enemy.archetype), HostileBright, GameplayHUDUI.GetEnemyClassIconSprite(enemy));
                // Flyers' archetype chip already reads AIR, so only show movement when it adds something
                string movement = enemy.movementType == EnemyMovementType.Air ? "AIR" : "GROUND";
                if (movement != ArchetypeLabel(enemy.archetype)) x = AddChip(x, movement, LabelColor, null);
                x = AddChip(x, DamageLabel(enemy.damageType), DamageColor(enemy.damageType), null);
                if (enemy.isUnblockable) AddChip(x, "UNBLOCKABLE", AmberColor, null);
            }
        }

        private float AddChip(float x, string label, Color color, Sprite icon)
        {
            const int fontSize = 10;
            float iconSpace = icon != null ? 26f : 0f;
            float width = label.Length * fontSize + 24f + iconSpace;

            Image chip = CreateImage(chipRow, "Chip_" + label, new Color(0.07f, 0.085f, 0.115f, 1f));
            PlaceTopLeft(chip.rectTransform, x, 0f, width, 32f);
            Outline outline = chip.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(color.r, color.g, color.b, 0.7f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            if (icon != null)
            {
                Image iconImage = CreateImage(chip.rectTransform, "Icon", Color.white);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                PlaceTopLeft(iconImage.rectTransform, 8f, 5f, 22f, 22f);
            }

            Text text = CreateText(chip.rectTransform, label, fontSize, TextAnchor.MiddleCenter, color);
            PlaceTopLeft(text.rectTransform, iconSpace, 0f, width - iconSpace, 32f);
            return x + width + 10f;
        }

        private void ConfigureTiers(IntelEntry entry)
        {
            int count;
            if (entry.Operator != null)
            {
                count = (int)TopRarity(entry.Operator) - (int)entry.Operator.baseRarity + 1;
                for (int i = 0; i < tierButtons.Count; i++)
                {
                    tierButtons[i].Star.enabled = true;
                    tierButtons[i].Label.text = "  " + ((int)entry.Operator.baseRarity + i);
                }
            }
            else
            {
                count = DifficultyLabels.Length;
                for (int i = 0; i < tierButtons.Count; i++)
                {
                    tierButtons[i].Star.enabled = false;
                    tierButtons[i].Label.text = DifficultyLabels[i];
                }
            }

            for (int i = 0; i < tierButtons.Count; i++) tierButtons[i].Button.gameObject.SetActive(i < count && count > 1);
            SetTier(0, false);
        }

        private void SetTier(int index, bool playSound = true)
        {
            if (selectedIndex < 0 || selectedIndex >= filtered.Count) return;
            tierIndex = index;
            for (int i = 0; i < tierButtons.Count; i++)
                SetToggleVisual(tierButtons[i].Button, i == index, Accent, Hostile ? HostileButton : TerminalButton);
            if (playSound) PlaySfx(SfxId.UiClick);
            RefreshStats(filtered[selectedIndex]);
        }

        private void RefreshStats(IntelEntry entry)
        {
            int[] values;
            int[] max;
            if (entry.Operator != null)
            {
                OperatorData op = entry.Operator;
                OperatorRarity rarity = (OperatorRarity)Mathf.Min((int)op.baseRarity + tierIndex, (int)OperatorRarity.Star5);
                values = new[] { op.GetScaledHP(rarity), op.GetScaledATK(rarity), op.GetScaledDEF(rarity), op.GetScaledRES(rarity) };
                max = operatorStatMax;

                LayoutFacts(3);
                SetFact(0, "BLOCK", op.blockCount.ToString());
                SetFact(1, "ATK SPD", FormatSeconds(op.attackInterval));
                SetFact(2, "REDEPLOY", FormatWaves(TrashTD.Systems.OperatorManager.GetRetreatCooldownRounds(op.operatorClass)));

                statusText.text = string.Empty;
                BuildRarityStars((int)rarity, tierIndex > 0);
            }
            else
            {
                EnemyData enemy = entry.Enemy;
                int level = tierIndex + 1;
                values = new[] { enemy.GetScaledHP(level), enemy.GetScaledATK(level), enemy.GetScaledDEF(level), enemy.baseRES };
                max = enemyStatMax;

                LayoutFacts(4);
                SetFact(0, "SPEED", enemy.moveSpeed.ToString("0.##"));
                SetFact(1, "ATK SPD", FormatSeconds(enemy.attackInterval));
                SetFact(2, "BLOCK WT", enemy.isUnblockable ? "-" : enemy.blockWeight.ToString());
                SetFact(3, "LIFE COST", enemy.lifePointCost.ToString());

                ClearChildren(rarityRow);
                bool sighted = GetSightings(enemy).Count > 0;
                statusText.text = sighted ? "STATUS: ACTIVE THREAT" : "STATUS: UNCONFIRMED";
                statusText.color = sighted ? HostileBright : HintColor;
            }

            for (int i = 0; i < statRows.Count; i++)
            {
                float ratio = max[i] > 0 ? Mathf.Clamp01((float)values[i] / max[i]) : 0f;
                statRows[i].Fill.rectTransform.anchorMax = new Vector2(Mathf.Max(ratio, values[i] > 0 ? 0.02f : 0f), 1f);
                statRows[i].Fill.color = Accent;
                statRows[i].Value.text = values[i].ToString();
                statRows[i].Value.color = Bright;
            }
        }

        private void BuildRarityStars(int rarity, bool upgraded)
        {
            ClearChildren(rarityRow);
            Sprite star = GameplayHUDUI.LoadIconSprite("icon_star");
            for (int i = 0; i < rarity; i++)
            {
                Image image = CreateImage(rarityRow, "Star", AmberColor);
                image.sprite = star;
                image.preserveAspect = true;
                PlaceTopLeft(image.rectTransform, i * 26f, 0f, 22f, 22f);
            }

            if (upgraded)
            {
                Text note = CreateText(rarityRow, "UPGRADED", 9, TextAnchor.MiddleLeft, HintColor);
                PlaceTopLeft(note.rectTransform, rarity * 26f + 10f, 0f, 120f, 22f);
            }
        }

        private void SetFact(int index, string label, string value)
        {
            factBoxes[index].Label.text = label;
            factBoxes[index].Value.text = value;
            factBoxes[index].Value.color = Bright;
        }

        // ─────────────────────────────────────────────────────
        //  Range preview
        // ─────────────────────────────────────────────────────

        private void UpdateRange(IntelEntry entry)
        {
            ClearChildren(rangeGrid);
            ClearChildren(rangeLegend);

            var hitCells = new HashSet<Vector2Int>();
            var upgradeCells = new HashSet<Vector2Int>();
            var upgrade2Cells = new HashSet<Vector2Int>();
            var talentCells = new HashSet<Vector2Int>();
            Color selfColor;
            Color hitColor;
            Sprite selfIcon = null;

            if (entry.Operator != null)
            {
                OperatorData op = entry.Operator;
                if (op.rangePattern != null) foreach (Vector2Int cell in op.rangePattern) if (cell != Vector2Int.zero) hitCells.Add(cell);
                if (op.operatorName == "Mechonk")
                {
                    Vector2Int[] upgradedRange = TrashTD.Operators.MechonkOperator.GetRangePatternForRarity(op, OperatorRarity.Star2);
                    if (upgradedRange != null)
                        foreach (Vector2Int cell in upgradedRange)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell)) upgradeCells.Add(cell);
                }
                else if (op.operatorName == "Lockburn")
                {
                    Vector2Int[] upgradedRange = TrashTD.Operators.LockburnOperator.GetRangePatternForRarity(op, OperatorRarity.Star3);
                    if (upgradedRange != null)
                        foreach (Vector2Int cell in upgradedRange)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell)) upgradeCells.Add(cell);
                }
                else if (op.operatorName == "Mossmo")
                {
                    Vector2Int[] upgradedRange = TrashTD.Operators.MossmoOperator.GetRangePatternForRarity(op, OperatorRarity.Star5);
                    if (upgradedRange != null)
                        foreach (Vector2Int cell in upgradedRange)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell)) upgradeCells.Add(cell);
                }
                else if (op.operatorName == "Progeny")
                {
                    Vector2Int[] upgradedRange = TrashTD.Operators.ProgenyOperator.GetRangePatternForRarity(op, OperatorRarity.Star3);
                    if (upgradedRange != null)
                        foreach (Vector2Int cell in upgradedRange)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell)) upgradeCells.Add(cell);
                }
                else if (op.operatorName == "Oquloct")
                {
                    Vector2Int[] upgradedRange = TrashTD.Operators.OquloctOperator.GetRangePatternForRarity(op, OperatorRarity.Star4);
                    if (upgradedRange != null)
                        foreach (Vector2Int cell in upgradedRange)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell)) upgradeCells.Add(cell);
                }
                else if (op.operatorName == "Thornchin")
                {
                    // 1st upgrade (3★): adjacent 4 tiles (cross)
                    Vector2Int[] star3Range = TrashTD.Operators.ThornchinOperator.GetRangePatternForRarity(op, OperatorRarity.Star3);
                    if (star3Range != null)
                        foreach (Vector2Int cell in star3Range)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell)) upgradeCells.Add(cell);

                    // 2nd upgrade (4★): surrounding 8 tiles (adds the 4 diagonal corners)
                    Vector2Int[] star4Range = TrashTD.Operators.ThornchinOperator.GetRangePatternForRarity(op, OperatorRarity.Star4);
                    if (star4Range != null)
                        foreach (Vector2Int cell in star4Range)
                            if (cell != Vector2Int.zero && !hitCells.Contains(cell) && !upgradeCells.Contains(cell)) upgrade2Cells.Add(cell);
                }

                Vector2Int[] talent = op.GetTalentRangePattern();
                if (talent != null) foreach (Vector2Int cell in talent) if (cell != Vector2Int.zero && !hitCells.Contains(cell) && !upgradeCells.Contains(cell) && !upgrade2Cells.Contains(cell)) talentCells.Add(cell);

                selfColor = RangeSelfColor;
                hitColor = RangeHitColor;
                selfIcon = GameplayHUDUI.GetClassIconSprite(op.operatorClass);
                bool hitsSelf = op.rangePattern != null && Array.IndexOf(op.rangePattern, Vector2Int.zero) >= 0;
                int displayedRangeCount = hitCells.Count + upgradeCells.Count + upgrade2Cells.Count;
                rangeReadout.text = displayedRangeCount == 0
                    ? (hitsSelf ? "OWN TILE" : "NO ATTACK")
                    : op.operatorName == "Mechonk"
                        ? displayedRangeCount + " TILES AT 2\u2605, FACING RIGHT"
                        : op.operatorName == "Lockburn"
                            ? displayedRangeCount + " TILES AT 3\u2605, FACING RIGHT"
                            : op.operatorName == "Mossmo"
                                ? displayedRangeCount + " TILES AT 5\u2605, SURROUNDING"
                                : op.operatorName == "Progeny"
                                    ? displayedRangeCount + " TILES AT 3\u2605, CROSS"
                                    : op.operatorName == "Oquloct"
                                        ? displayedRangeCount + " TILES AT 4\u2605, CONE"
                                    : op.operatorName == "Thornchin"
                                        ? "8 TILES AT 4\u2605, SURROUNDING"
                                    : hitCells.Count + " TILES, FACING RIGHT";

                float x = AddLegend(0f, selfColor, "SELF");
                if (hitCells.Count > 0)
                {
                    x = AddLegend(x, hitColor, op.operatorClass == OperatorClass.Medic ? "HEAL" : "ATTACK");
                }
                if (op.operatorName == "Thornchin")
                {
                    x = AddLegend(x, RangeUpgradeColor, "3\u2605 ADJACENT");
                    x = AddLegend(x, RangeUpgrade2Color, "4\u2605 SURROUND");
                }
                else if (upgradeCells.Count > 0)
                {
                    string bonusLabel = (op.operatorName == "Lockburn" || op.operatorName == "Progeny") ? "3\u2605 BONUS"
                        : op.operatorName == "Oquloct" ? "4\u2605 BONUS"
                        : op.operatorName == "Mossmo" ? "5\u2605 BONUS"
                        : "2\u2605 BONUS";
                    x = AddLegend(x, RangeUpgradeColor, bonusLabel);
                }
                if (talentCells.Count > 0) AddLegend(x, RangeTalentColor, "TALENT");
            }
            else
            {
                EnemyData enemy = entry.Enemy;
                // Casters and plain flyers (Acid Flyer) shoot with EnemyBase's default ranged reach.
                bool plainFlyer = enemy.enemyPrefab != null &&
                    enemy.enemyPrefab.GetComponent<TrashTD.Enemies.FlyerEnemy>() is TrashTD.Enemies.FlyerEnemy flyer &&
                    !(flyer is TrashTD.Enemies.AirCombatEnemy);
                float reach = enemy.attackRange > 0 ? enemy.attackRange
                    : enemy.archetype == EnemyArchetype.Caster || plainFlyer ? 2.5f : 0f;
                if (reach > 0f)
                {
                    int r = Mathf.CeilToInt(reach);
                    for (int x = -r; x <= r; x++)
                        for (int y = -r; y <= r; y++)
                            if ((x != 0 || y != 0) && x * x + y * y <= reach * reach) hitCells.Add(new Vector2Int(x, y));
                    rangeReadout.text = "RANGED, " + reach.ToString("0.#") + " TILES";
                }
                else if (enemy.rangePattern != null && enemy.rangePattern.Length > 0)
                {
                    foreach (Vector2Int cell in enemy.rangePattern) hitCells.Add(cell);
                    rangeReadout.text = "LEAP, 3 TILES AHEAD";
                }
                else if (enemy.frontAreaAttackTiles > 0)
                {
                    for (int x = 1; x <= enemy.frontAreaAttackTiles; x++) hitCells.Add(new Vector2Int(x, 0));
                    rangeReadout.text = enemy.frontAreaAttackTiles + " TILES AHEAD";
                }
                else
                {
                    rangeReadout.text = enemy.movementType == EnemyMovementType.Air ? "MELEE, FROM THE AIR" : "MELEE, HITS ITS BLOCKER";
                }

                selfColor = EnemySelfColor;
                hitColor = AmberColor;
                float lx = AddLegend(0f, selfColor, "SELF");
                lx = AddLegend(lx, hitColor, "ATTACK");
                AddLegend(lx, new Color(1f, 1f, 1f, 0.5f), "PATH >");
            }

            int minX = 0, maxX = 0, minY = 0, maxY = 0;
            foreach (Vector2Int cell in hitCells) Expand(cell, ref minX, ref maxX, ref minY, ref maxY);
            foreach (Vector2Int cell in upgradeCells) Expand(cell, ref minX, ref maxX, ref minY, ref maxY);
            foreach (Vector2Int cell in upgrade2Cells) Expand(cell, ref minX, ref maxX, ref minY, ref maxY);
            foreach (Vector2Int cell in talentCells) Expand(cell, ref minX, ref maxX, ref minY, ref maxY);

            // Pad small patterns to at least 3x3 so the unit's own tile has context around it.
            while (maxX - minX + 1 < 3) { if (-minX <= maxX) minX--; else maxX++; }
            while (maxY - minY + 1 < 3) { if (-minY <= maxY) minY--; else maxY++; }

            int cols = maxX - minX + 1;
            int rows = maxY - minY + 1;
            Vector2 area = rangeGrid.sizeDelta;
            const float gap = 3f;
            float tileSize = Mathf.Floor(Mathf.Min(34f, (area.x - gap * (cols - 1)) / cols, (area.y - gap * (rows - 1)) / rows));
            float step = tileSize + gap;

            for (int gy = minY; gy <= maxY; gy++)
            {
                for (int gx = minX; gx <= maxX; gx++)
                {
                    var cell = new Vector2Int(gx, gy);
                    bool isSelf = cell == Vector2Int.zero;
                    bool hit = hitCells.Contains(cell);
                    bool upgrade = upgradeCells.Contains(cell);
                    bool upgrade2 = upgrade2Cells.Contains(cell);
                    bool talent = talentCells.Contains(cell);
                    Color color = isSelf ? selfColor : upgrade ? RangeUpgradeColor : upgrade2 ? RangeUpgrade2Color : hit ? hitColor : talent ? RangeTalentColor : RangeEmptyColor;
                    var position = new Vector2(
                        Mathf.Round((gx - minX - (cols - 1) * 0.5f) * step),
                        Mathf.Round((gy - minY - (rows - 1) * 0.5f) * step));
                    CreateRangeTile(position, tileSize, color, isSelf || hit || upgrade || upgrade2 || talent, isSelf ? selfIcon : null);
                }
            }
        }

        private static void Expand(Vector2Int cell, ref int minX, ref int maxX, ref int minY, ref int maxY)
        {
            minX = Mathf.Min(minX, cell.x);
            maxX = Mathf.Max(maxX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxY = Mathf.Max(maxY, cell.y);
        }

        private void CreateRangeTile(Vector2 position, float size, Color color, bool filled, Sprite icon)
        {
            Image frame = CreateImage(rangeGrid, "RangeTile", color);
            RectTransform rect = frame.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;

            Sprite tileSprite = GetTileFrameSprite();
            if (tileSprite != null)
            {
                frame.sprite = tileSprite;
                frame.type = Image.Type.Sliced;
            }
            else
            {
                frame.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
            }

            float inset = Mathf.Min(3f, size * 0.2f);
            if (filled)
            {
                Image fill = CreateImage(rect, "Fill", new Color(color.r, color.g, color.b, 0.3f));
                SetStretch(fill.rectTransform);
                fill.rectTransform.offsetMin = new Vector2(inset, inset);
                fill.rectTransform.offsetMax = new Vector2(-inset, -inset);
            }

            if (icon != null)
            {
                Image iconImage = CreateImage(rect, "Icon", Color.white);
                iconImage.sprite = icon;
                iconImage.preserveAspect = true;
                SetStretch(iconImage.rectTransform);
                iconImage.rectTransform.offsetMin = new Vector2(inset + 1f, inset + 1f);
                iconImage.rectTransform.offsetMax = new Vector2(-inset - 1f, -inset - 1f);
            }
        }

        private float AddLegend(float x, Color color, string label)
        {
            Image swatch = CreateImage(rangeLegend, "Swatch", color);
            PlaceTopLeft(swatch.rectTransform, x, 4f, 12f, 12f);
            Text text = CreateText(rangeLegend, label, 9, TextAnchor.MiddleLeft, HintColor);
            float width = label.Length * 9f + 4f;
            PlaceTopLeft(text.rectTransform, x + 18f, 0f, width, 20f);
            return x + 18f + width + 18f;
        }

        // ─────────────────────────────────────────────────────
        //  Labels
        // ─────────────────────────────────────────────────────

        private static string ArchetypeLabel(EnemyArchetype archetype) =>
            archetype == EnemyArchetype.Flyer ? "AIR" : archetype.ToString().ToUpperInvariant();

        private static string PositionLabel(OperatorPosition position) => position switch
        {
            OperatorPosition.Melee => "MELEE",
            OperatorPosition.Ranged => "RANGED",
            _ => "ANY TILE"
        };

        private static string DamageLabel(DamageType type) => type switch
        {
            DamageType.Arts => "ARTS",
            DamageType.True => "TRUE DMG",
            _ => "PHYSICAL"
        };

        private static Color DamageColor(DamageType type) => type switch
        {
            DamageType.Arts => new Color(0.72f, 0.48f, 1f, 1f),
            DamageType.True => new Color(1f, 0.95f, 0.6f, 1f),
            _ => new Color(1f, 0.72f, 0.45f, 1f)
        };

        private static string FormatSeconds(float seconds) => seconds.ToString("0.##") + "S";

        private static string FormatWaves(int waves) => waves <= 0 ? "NONE" : waves == 1 ? "1 WAVE" : waves + " WAVES";

        // ─────────────────────────────────────────────────────
        //  Widgets
        // ─────────────────────────────────────────────────────

        /// <summary>A dark square with a faint pixel grid, a floor line and corner brackets, holding a pixel-scaled sprite.</summary>
        private static void CreateSpriteStage(RectTransform parent, float x, float y, float size, Color accent, bool hostile,
            out Image background, out Image sprite, out Image floor)
        {
            background = CreateImage(parent, "SpriteStage", hostile ? new Color(0.03f, 0.012f, 0.014f, 1f) : StageColor);
            PlaceTopLeft(background.rectTransform, x, y, size, size);

            Image grid = CreateImage(background.rectTransform, "Grid", new Color(accent.r, accent.g, accent.b, 0.08f));
            grid.sprite = GetGridSprite();
            grid.type = Image.Type.Tiled;
            grid.pixelsPerUnitMultiplier = 0.25f;
            SetStretch(grid.rectTransform);

            floor = CreateImage(background.rectTransform, "Floor", new Color(accent.r, accent.g, accent.b, 0.8f));
            PlaceTopLeft(floor.rectTransform, 0f, size - 4f, size, 4f);
            Image glow = CreateImage(background.rectTransform, "FloorGlow", new Color(accent.r, accent.g, accent.b, 0.08f));
            PlaceTopLeft(glow.rectTransform, 0f, size * 0.62f, size, size * 0.38f - 4f);

            AddCornerBrackets(background.rectTransform, 18f, 3f, new Color(accent.r, accent.g, accent.b, 0.9f), 4f);

            sprite = CreateImage(background.rectTransform, "Sprite", Color.white);
            sprite.rectTransform.anchorMin = sprite.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            sprite.rectTransform.anchoredPosition = Vector2.zero;
            sprite.preserveAspect = true;
        }

        /// <summary>Shows a sprite at the largest whole-number scale that fits <paramref name="maxSize"/> (pixel art stays crisp).</summary>
        private static float GetIntelSpriteReferenceWidth(IntelEntry entry)
        {
            return entry.Operator != null && entry.Operator.operatorName == "Echosquire" ? 32f : 0f;
        }

        private static void SetPixelSprite(Image image, Sprite sprite, float maxSize, float referenceWidth = 0f)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.enabled = sprite != null;
            if (sprite == null) return;

            Vector2 pixels = sprite.rect.size;
            float sizingWidth = referenceWidth > 0f ? referenceWidth : pixels.x;
            float sizingDimension = Mathf.Max(sizingWidth, pixels.y);
            float scale = Mathf.Max(1f, Mathf.Floor(maxSize / sizingDimension));
            if (sizingDimension > maxSize) scale = maxSize / sizingDimension;
            image.rectTransform.sizeDelta = pixels * scale;
        }

        private static void UsePointFilter(Sprite[] frames)
        {
            if (frames == null) return;
            foreach (Sprite frame in frames)
            {
                if (frame != null && frame.texture != null) frame.texture.filterMode = FilterMode.Point;
            }
        }
    }
}
