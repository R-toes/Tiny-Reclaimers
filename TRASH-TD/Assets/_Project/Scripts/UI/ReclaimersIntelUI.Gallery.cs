using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Data;

namespace TrashTD.UI
{
    // The gallery: tabs, filters, tiles, selecting an entry and the locked-file view.
    public partial class ReclaimersIntelUI
    {
        private void BuildTabs()
        {
            foreach (Button tab in tabButtons) Destroy(tab.gameObject);
            tabButtons.Clear();

            var labels = new List<string> { "ALL" };
            if (Hostile)
            {
                foreach (EnemyArchetype archetype in ArchetypeOrder) labels.Add(ArchetypeLabel(archetype));
            }
            else
            {
                foreach (OperatorClass operatorClass in ClassOrder) labels.Add(operatorClass.ToString().ToUpperInvariant());
            }

            const int fontSize = 9;
            const float tabGap = 6f;
            float textWidth = 0f;
            foreach (string label in labels) textWidth += label.Length * fontSize;
            float padding = Mathf.Max(8f, (ListWidth - textWidth - tabGap * (labels.Count - 1)) / (labels.Count * 2f));

            float x = 0f;
            for (int i = 0; i < labels.Count; i++)
            {
                float width = labels[i].Length * fontSize + padding * 2f;
                Button tab = CreateButtonObject(tabRow, "Tab_" + labels[i], Hostile ? HostileButton : TerminalButton);
                PlaceTopLeft(tab.GetComponent<RectTransform>(), x, 0f, width, 40f);
                Text text = CreateText(tab.transform, labels[i], fontSize, TextAnchor.MiddleCenter, LabelColor);
                SetStretch(text.rectTransform);
                int captured = i;
                tab.onClick.AddListener(() =>
                {
                    filterIndex = captured;
                    ApplyFilter();
                });
                AttachFeedback(tab, SfxId.UiClick, Bright);
                tabButtons.Add(tab);
                x += width + tabGap;
            }
        }

        private void ApplyFilter()
        {
            filtered.Clear();
            foreach (IntelEntry entry in Entries)
            {
                if (filterIndex == 0) filtered.Add(entry);
                // Locked files only show under ALL, so a tab can't give away what they are
                else if (Hostile && !entry.Locked && entry.Enemy.archetype == ArchetypeOrder[filterIndex - 1]) filtered.Add(entry);
                else if (!Hostile && entry.Operator.operatorClass == ClassOrder[filterIndex - 1]) filtered.Add(entry);
            }

            for (int i = 0; i < tabButtons.Count; i++) SetToggleVisual(tabButtons[i], i == filterIndex, Accent, Hostile ? HostileButton : TerminalButton);
            listCountText.text = filtered.Count + (filtered.Count == 1 ? " ENTRY" : " ENTRIES") + "    [ARROWS] BROWSE";

            BuildTiles();
            gridScroll.verticalNormalizedPosition = 1f;
            selectedIndex = -1;
            if (filtered.Count > 0) SelectEntry(0);
            else ShowEmptyDetail();
        }

        private void BuildTiles()
        {
            foreach (Button tile in tileButtons) Destroy(tile.gameObject);
            tileButtons.Clear();
            scrambled.Clear(); // the tile names go with the tiles; SelectEntry re-adds the detail name

            for (int i = 0; i < filtered.Count; i++)
            {
                IntelEntry entry = filtered[i];
                Button tile = CreateButtonObject(gridContent, "Tile_" + entry.Name, Hostile ? HostilePanel : PanelColor);
                Outline outline = tile.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(Accent.r, Accent.g, Accent.b, 0.25f);
                outline.effectDistance = new Vector2(2f, -2f);

                Image portraitBack = CreateImage(tile.transform, "PortraitBack", new Color(0f, 0f, 0f, 0.3f));
                PlaceTopLeft(portraitBack.rectTransform, 10f, 10f, TileWidth - 20f, 112f);
                Image portrait = CreateImage(portraitBack.rectTransform, "Portrait", Color.white);
                portrait.rectTransform.anchorMin = portrait.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                SetPixelSprite(
                    portrait,
                    entry.Frames.Length > 0 ? entry.Frames[0] : null,
                    104f,
                    GetIntelSpriteReferenceWidth(entry));
                SetSilhouette(portrait, entry.Locked);

                if (entry.Operator != null)
                {
                    Image icon = CreateImage(tile.transform, "ClassIcon", Color.white);
                    icon.sprite = GameplayHUDUI.GetClassIconSprite(entry.Operator.operatorClass);
                    icon.enabled = icon.sprite != null;
                    icon.preserveAspect = true;
                    PlaceTopLeft(icon.rectTransform, 12f, 12f, 24f, 24f);

                    Text stars = CreateText(tile.transform, ((int)entry.Operator.baseRarity).ToString(), 10, TextAnchor.MiddleRight, AmberColor);
                    PlaceTopLeft(stars.rectTransform, TileWidth - 52f, 14f, 22f, 20f);
                    Image star = CreateImage(tile.transform, "Star", AmberColor);
                    star.sprite = GameplayHUDUI.LoadIconSprite("icon_star");
                    star.preserveAspect = true;
                    PlaceTopLeft(star.rectTransform, TileWidth - 28f, 15f, 16f, 16f);
                }
                else
                {
                    Text tag = CreateText(tile.transform, entry.Locked ? UnknownText : ArchetypeLabel(entry.Enemy.archetype), 8, TextAnchor.MiddleLeft, new Color(HostileBright.r, HostileBright.g, HostileBright.b, 0.85f));
                    PlaceTopLeft(tag.rectTransform, 14f, 14f, TileWidth - 28f, 16f);
                }

                string tileName = entry.Name.ToUpperInvariant();
                Text name = CreateText(tile.transform, entry.Locked ? Scramble(tileName) : tileName, 9, TextAnchor.MiddleCenter, LabelColor);
                if (entry.Locked) scrambled.Add((name, tileName));
                name.horizontalOverflow = HorizontalWrapMode.Wrap;
                name.verticalOverflow = VerticalWrapMode.Truncate;
                name.lineSpacing = 1.2f;
                PlaceTopLeft(name.rectTransform, 6f, 126f, TileWidth - 12f, 38f);

                int captured = i;
                tile.onClick.AddListener(() => SelectEntry(captured));
                MenuButtonFeedback feedback = AttachFeedback(tile, SfxId.UiClick, Bright);
                feedback.glowOutline = outline;
                feedback.hoverScale = 1.05f;
                tileButtons.Add(tile);
            }
        }

        private void ScrollToTile(int index)
        {
            Canvas.ForceUpdateCanvases();
            float viewHeight = gridScroll.viewport.rect.height;
            float contentHeight = gridContent.rect.height;
            if (contentHeight <= viewHeight) return;

            int row = index / GridColumns;
            float top = 16f + row * (TileHeight + TileGap);
            float bottom = top + TileHeight;
            float scrollY = gridContent.anchoredPosition.y;
            if (top < scrollY) scrollY = top - 8f;
            else if (bottom > scrollY + viewHeight) scrollY = bottom - viewHeight + 8f;
            gridContent.anchoredPosition = new Vector2(gridContent.anchoredPosition.x, Mathf.Clamp(scrollY, 0f, contentHeight - viewHeight));
        }

        // ─────────────────────────────────────────────────────
        //  Detail
        // ─────────────────────────────────────────────────────

        private void SelectEntry(int index)
        {
            if (index < 0 || index >= filtered.Count) return;
            selectedIndex = index;
            for (int i = 0; i < tileButtons.Count; i++) SetGlow(tileButtons[i], i == index);

            IntelEntry entry = filtered[index];
            playingFrames = entry.Frames;
            playingFrameDuration = Mathf.Max(0.04f, entry.FrameDuration);
            frameIndex = 0;
            frameTimer = 0f;
            SetPixelSprite(
                stageSprite,
                playingFrames.Length > 0 ? playingFrames[0] : null,
                StageSize - 28f,
                GetIntelSpriteReferenceWidth(entry));
            SetSilhouette(stageSprite, entry.Locked);
            scrambled.RemoveAll(item => item.text == nameText);

            if (entry.Locked)
            {
                ShowLockedDetail(entry);
                return;
            }

            fileText.text = "FILE " + entry.FileId + (Hostile ? "    // HOSTILE" : "    // RECLAIMER");
            nameText.text = entry.Name.ToUpperInvariant();
            BuildChips(entry);

            string lore = entry.Operator != null ? entry.Operator.intelLore : entry.Enemy.intelLore;
            loreText.text = string.IsNullOrWhiteSpace(lore) ? "No field notes recovered yet." : lore.Trim();

            string ability = entry.Operator != null ? entry.Operator.skillDescription : entry.Enemy.abilityDescription;
            abilityText.text = string.IsNullOrWhiteSpace(ability) ? "No special abilities on record." : ability.Trim();
            abilityHeader.text = entry.Operator != null ? "// SKILL" : "// ABILITY";

            bool showSightings = entry.Enemy != null;
            sightedHeader.gameObject.SetActive(showSightings);
            sightedText.gameObject.SetActive(showSightings);
            if (showSightings)
            {
                List<string> stages = GetSightings(entry.Enemy);
                sightedText.text = stages.Count > 0 ? string.Join("\n", stages) : "No confirmed sightings in the field yet.";
            }
            notesScroll.verticalNormalizedPosition = 1f;
            loreScroll.verticalNormalizedPosition = 1f;

            ConfigureTiers(entry);
            UpdateRange(entry);
        }

        /// <summary>An enemy the player hasn't met: animated silhouette, scrambled name, and "???" in every field.</summary>
        private void ShowLockedDetail(IntelEntry entry)
        {
            fileText.text = "FILE " + entry.FileId + "    // HOSTILE    // UNIDENTIFIED";
            string template = entry.Name.ToUpperInvariant();
            nameText.text = Scramble(template);
            scrambled.Add((nameText, template));

            ClearChildren(chipRow);
            AddChip(0f, "UNIDENTIFIED", HintColor, null);
            ClearChildren(rarityRow);
            statusText.text = "STATUS: NOT YET ENCOUNTERED";
            statusText.color = HintColor;

            loreText.text = UnknownText;
            abilityHeader.text = "// ABILITY";
            abilityText.text = UnknownText;
            sightedHeader.gameObject.SetActive(true);
            sightedText.gameObject.SetActive(true);
            sightedText.text = UnknownText;
            notesScroll.verticalNormalizedPosition = 1f;
            loreScroll.verticalNormalizedPosition = 1f;

            foreach (TierButton tier in tierButtons) tier.Button.gameObject.SetActive(false);
            foreach (StatRow row in statRows)
            {
                row.Fill.rectTransform.anchorMax = new Vector2(0f, 1f);
                row.Value.text = UnknownText;
                row.Value.color = HintColor;
            }

            LayoutFacts(4);
            string[] factLabels = { "SPEED", "ATK SPD", "BLOCK WT", "LIFE COST" };
            for (int i = 0; i < factLabels.Length; i++) SetFact(i, factLabels[i], "?");

            ClearChildren(rangeGrid);
            ClearChildren(rangeLegend);
            rangeReadout.text = "NO DATA";
        }

        private void AnimateScrambledNames(float dt)
        {
            if (scrambled.Count == 0) return;
            scrambleTimer += dt;
            if (scrambleTimer < ScrambleInterval) return;
            scrambleTimer = 0f;

            foreach ((Text text, string template) item in scrambled)
            {
                if (item.text != null) item.text.text = Scramble(item.template);
            }
        }

        /// <summary>Same length and spacing as the name, with every letter or digit replaced by a random letter.</summary>
        private static string Scramble(string template)
        {
            var chars = template.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (char.IsLetterOrDigit(chars[i])) chars[i] = ScrambleGlyphs[UnityEngine.Random.Range(0, ScrambleGlyphs.Length)];
            }
            return new string(chars);
        }

        /// <summary>Draws the sprite as a flat silhouette (only its outline shows), or normally again.</summary>
        private void SetSilhouette(Image image, bool on)
        {
            if (image == null) return;
            Material material = on ? GetSilhouetteMaterial() : null;
            image.material = material;
            image.color = !on ? Color.white : material != null ? SilhouetteColor : SilhouetteFallbackColor;
        }

        private Material GetSilhouetteMaterial()
        {
            if (silhouetteMaterial != null || silhouetteShaderMissing) return silhouetteMaterial;

            Shader shader = Resources.Load<Shader>(SilhouetteShaderPath);
            if (shader == null)
            {
                silhouetteShaderMissing = true;
                Debug.LogWarning($"[Intel] Silhouette shader missing at Resources/{SilhouetteShaderPath}; locked enemies use a dark tint instead.");
                return null;
            }

            silhouetteMaterial = new Material(shader) { name = "Intel Silhouette" };
            return silhouetteMaterial;
        }

        private void ShowEmptyDetail()
        {
            playingFrames = null;
            SetPixelSprite(stageSprite, null, 0f);
            fileText.text = string.Empty;
            nameText.text = "NO ENTRIES";
            loreText.text = string.Empty;
            statusText.text = string.Empty;
            ClearChildren(chipRow);
            ClearChildren(rarityRow);
            ClearChildren(rangeGrid);
            ClearChildren(rangeLegend);
        }

        private void AnimateDetailSprite(float dt)
        {
            if (playingFrames == null || playingFrames.Length < 2 || !galleryCard.gameObject.activeSelf) return;

            frameTimer += dt;
            if (frameTimer < playingFrameDuration) return;
            int elapsed = Mathf.FloorToInt(frameTimer / playingFrameDuration);
            frameTimer %= playingFrameDuration;
            frameIndex = (frameIndex + elapsed) % playingFrames.Length;
            stageSprite.sprite = playingFrames[frameIndex];
        }
    }
}
