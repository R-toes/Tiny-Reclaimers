using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TrashTD.Combat;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Development
{
    // Building the sandbox UI: toolbar, readouts, tooltip and the dropdowns.
    public partial class SandboxController
    {
        // ============================
        // UI construction
        // ============================

        private void BuildUI()
        {
            GameObject root = MakeObject("SandboxUI", null);
            uiRoot = root.GetComponent<RectTransform>();
            if (!AttachToCanvas(uiRoot))
            {
                Destroy(root);
                uiRoot = null;
                return;
            }
            Stretch(uiRoot, 0f, 0f, 0f, 0f);
            Canvas canvas = root.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                canvasRect = canvas.GetComponent<RectTransform>();
                uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }

            BuildToolbar();
            BuildReadout();
            dropdownPanels[(int)Dropdown.Enemies] = BuildEnemiesDropdown();
            dropdownPanels[(int)Dropdown.Operators] = BuildOperatorsDropdown();
            dropdownPanels[(int)Dropdown.Waves] = BuildWavesDropdown();
            dropdownPanels[(int)Dropdown.Queue] = BuildQueueDropdown();
            dropdownPanels[(int)Dropdown.Map] = BuildMapDropdown();
            BuildStatsCard();
            BuildShowButton();
            BuildTooltip();
            BuildToast();
            BuildHint();

            ApplyEnemyFilter();
            ApplyOperatorFilter();
            ApplyVisibility();
        }

        private bool AttachToCanvas(RectTransform rect)
        {
            if (gameplayHud != null)
            {
                gameplayHud.AttachSandboxPanel(rect);
                return rect.parent != null;
            }

            Canvas canvas = CrtDisplay.FindGameCanvas();
            if (canvas == null) return false;
            rect.SetParent(canvas.transform, false);
            return true;
        }

        private void BuildToolbar()
        {
            toolbar = MakeObject("Toolbar", uiRoot);
            Place(toolbar.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-ScreenMargin, -ToolbarTop), new Vector2(0f, ToolbarHeight));
            MakePanelBackground(toolbar, PanelTint);
            HorizontalLayoutGroup layout = toolbar.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            toolbar.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            Sprite[] icons =
            {
                GameplayHUDUI.GetEnemyClassIconSprite(EnemyArchetype.Grunt),
                GameplayHUDUI.LoadIconSprite("icon_squad"),
                GameplayHUDUI.LoadIconSprite("icon_wave"),
                GameplayHUDUI.LoadIconSprite("icon_menu"),
                null
            };
            for (int i = 0; i < DropdownLabels.Length; i++)
            {
                Dropdown dropdown = (Dropdown)i;
                int widestLabel = dropdown == Dropdown.Queue ? "QUEUE 00".Length : DropdownLabels[i].Length;
                dropdownButtons[i] = MakeToolbarButton(toolbar.transform, icons[i], DropdownLabels[i], widestLabel);
                dropdownButtons[i].onClick.AddListener(() => ToggleDropdown(dropdown));
            }
            queueButtonText = dropdownButtons[(int)Dropdown.Queue].GetComponentInChildren<Text>();

            Image divider = MakeImage(toolbar.transform, "Divider", LineColor);
            Size(divider.gameObject, -1f, 1f, 0f);

            killAllButton = MakeToolbarIcon(toolbar.transform, "icon_skull", DangerColor, "Kill all enemies (the wave stays live)");
            killAllButton.onClick.AddListener(KillAllEnemies);

            GetHazardDescription(bootstrapper != null ? bootstrapper.Environment : StageEnvironment.None, out bool hasHazard);
            hazardButton = MakeToolbarIcon(toolbar.transform, "icon_warning", WarnFill, "Map hazard on/off");
            hazardIcon = hazardButton.transform.Find("Icon").GetComponent<Image>();
            hazardButton.onClick.AddListener(ToggleHazard);
            hazardButton.gameObject.SetActive(hasHazard);

            statsButton = MakeToolbarIcon(toolbar.transform, "icon_damage", ControlColor, "Wave stats and damage meter");
            statsButton.onClick.AddListener(ToggleStats);

            Button hide = MakeToolbarIcon(toolbar.transform, "icon_minus", ControlColor, "Hide the sandbox UI");
            hide.onClick.AddListener(() => SetUiHidden(true));
        }

        private void BuildReadout()
        {
            readout = MakeObject("Readout", uiRoot);
            Place(readout.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-ScreenMargin, -ReadoutTop), new Vector2(0f, ReadoutHeight));
            Image background = readout.AddComponent<Image>();
            background.color = new Color(PanelTint.r, PanelTint.g, PanelTint.b, 0.82f);
            background.raycastTarget = false;
            HorizontalLayoutGroup layout = readout.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            readout.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            readoutText = MakeText(readout.transform, string.Empty, 11, TextColor, TextAnchor.MiddleRight);
            readoutText.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        private void BuildToast()
        {
            toast = MakeObject("Toast", uiRoot);
            Place(toast.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -ToolbarTop), new Vector2(0f, 36f));
            MakePanelBackground(toast, PanelTint).raycastTarget = false;
            toastGroup = toast.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            HorizontalLayoutGroup layout = toast.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            toast.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            toastText = MakeText(toast.transform, string.Empty, 13, GoldColor, TextAnchor.MiddleCenter);
            toastText.horizontalOverflow = HorizontalWrapMode.Overflow;
            toast.SetActive(false);
        }

        private void BuildHint()
        {
            GameObject hint = MakeObject("Hint", uiRoot);
            hintRect = hint.GetComponent<RectTransform>();
            Place(hintRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.one, Vector2.zero, new Vector2(0f, 26f));
            Image background = hint.AddComponent<Image>();
            background.color = new Color(0.01f, 0.015f, 0.02f, 0.95f);
            background.raycastTarget = false;
            HorizontalLayoutGroup layout = hint.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            hint.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            hintText = MakeText(hint.transform, string.Empty, 11, TextColor, TextAnchor.MiddleCenter);
            hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
            hint.SetActive(false);
        }

        private void BuildShowButton()
        {
            Button button = MakeButton(uiRoot, "SANDBOX", ToolbarFontSize, AccentFill, TextColor);
            showButton = button.gameObject;
            Place(showButton.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-ScreenMargin, -ToolbarTop), new Vector2(130f, ToolbarHeight - 6f));
            button.onClick.AddListener(() => SetUiHidden(false));
            AddHint(button, "Show the sandbox controls");
        }

        private void BuildStatsCard()
        {
            statsCard = MakeObject("StatsCard", uiRoot);
            Place(statsCard.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(ScreenMargin, -ToolbarTop), new Vector2(StatsCardWidth, 0f));
            MakePanelBackground(statsCard, new Color(PanelTint.r, PanelTint.g, PanelTint.b, 0.88f));
            Column(statsCard, new RectOffset(8, 8, 6, 8), 4f);
            statsCard.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Transform header = MakeStrip(statsCard.transform, 20f, 4f);
            Text title = MakeText(header, "WAVE STATS", 9, AccentColor, TextAnchor.MiddleLeft);
            Size(title.gameObject, -1f, -1f, 1f);
            Button close = MakeIconButton(header, "icon_close", ControlColor, 0f);
            close.onClick.AddListener(ToggleStats);

            statsWaveText = MakeText(statsCard.transform, string.Empty, 8, TextColor, TextAnchor.UpperLeft);

            MakeGroupHeader(statsCard.transform, "OPERATOR DAMAGE");
            damageNamesText = MakeText(statsCard.transform, string.Empty, 8, TextColor, TextAnchor.UpperLeft);
            // Values share the names' rect (right-aligned), so each line lines up with its operator
            damageValuesText = MakeText(damageNamesText.transform, string.Empty, 8, AccentColor, TextAnchor.UpperRight);
            Stretch(damageValuesText.rectTransform, 0f, 0f, 0f, 0f);
            MakeText(statsCard.transform, "Attack damage after DEF/RES and auras. Hazard and DoT damage isn't counted.", 7, TextDim, TextAnchor.UpperLeft);
        }

        private void BuildTooltip()
        {
            GameObject root = MakeObject("EnemyTooltip", uiRoot);
            tooltip = root.GetComponent<RectTransform>();
            Place(tooltip, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(TooltipWidth, 0f));
            MakePanelBackground(root, new Color(PanelTint.r, PanelTint.g, PanelTint.b, 0.92f)).raycastTarget = false;
            Column(root, new RectOffset(7, 7, 5, 7), 3f);
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tooltipTitle = MakeText(root.transform, string.Empty, 9, GoldColor, TextAnchor.MiddleLeft);
            FitText(tooltipTitle, 7);
            Size(tooltipTitle.gameObject, 13f);

            Image bar = MakeImage(root.transform, "HpBar", LineColor);
            Size(bar.gameObject, 4f);
            tooltipHpFillImage = MakeImage(bar.transform, "Fill", AccentColor);
            tooltipHpFill = tooltipHpFillImage.rectTransform;
            tooltipHpFill.anchorMin = Vector2.zero;
            tooltipHpFill.anchorMax = Vector2.one;
            tooltipHpFill.offsetMin = Vector2.zero;
            tooltipHpFill.offsetMax = Vector2.zero;

            tooltipBody = MakeText(root.transform, string.Empty, 8, TextColor, TextAnchor.UpperLeft);
            root.SetActive(false);
        }

        // ---- Dropdowns ----

        /// <summary>A dropdown panel under the toolbar's right edge; its height follows its content (see <see cref="FitDropdown"/>).</summary>
        private Transform MakeDropdownPanel(Dropdown dropdown, out GameObject panel)
        {
            panel = MakeObject(DropdownLabels[(int)dropdown] + "Dropdown", uiRoot);
            Place(panel.GetComponent<RectTransform>(), Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-ScreenMargin, -DropdownTop), new Vector2(DropdownWidths[(int)dropdown], 0f));
            MakePanelBackground(panel, PanelTint);
            Column(panel, new RectOffset(8, 8, 8, 8), 5f);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Accent strip along the top edge (ignored by the layout)
            Image strip = MakeImage(panel.transform, "AccentStrip", AccentColor);
            strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(strip.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(-12f, 2f));
            return panel.transform;
        }

        private GameObject BuildEnemiesDropdown()
        {
            Transform panel = MakeDropdownPanel(Dropdown.Enemies, out GameObject root);

            Transform top = MakeStrip(panel, 22f, 3f);
            MakeSearchField(top, "Search enemies...", enemySearch, value =>
            {
                enemySearch = value;
                ApplyEnemyFilter();
            });
            for (int i = 0; i < SpawnAmounts.Length; i++)
            {
                int index = i;
                amountButtons[i] = MakeButton(top, "x" + SpawnAmounts[i], 8, ControlColor, TextColor);
                Size(amountButtons[i].gameObject, -1f, 30f, 0f);
                amountButtons[i].onClick.AddListener(() => SetSpawnAmount(index));
                AddHint(amountButtons[i], $"Each click queues or spawns {SpawnAmounts[i]}");
            }

            Transform chips = MakeStrip(panel, 20f, 2f);
            AddFilterChip(chips, enemyFilterButtons, null, "ALL", "All classes", () => { enemyFilter = -1; ApplyEnemyFilter(); });
            foreach (EnemyArchetype archetype in System.Enum.GetValues(typeof(EnemyArchetype)))
            {
                int value = (int)archetype;
                AddFilterChip(chips, enemyFilterButtons, GameplayHUDUI.GetEnemyClassIconSprite(archetype), null, archetype.ToString(),
                    () => { enemyFilter = value; ApplyEnemyFilter(); });
            }

            enemyHintText = MakeText(panel, string.Empty, 8, PrepColor, TextAnchor.MiddleLeft);
            FitText(enemyHintText, 6);
            Size(enemyHintText.gameObject, 12f);

            ScrollArea scroll = MakeScrollArea(panel);
            dropdownScrolls[(int)Dropdown.Enemies] = scroll;
            Transform grid = MakeTileGrid(scroll.content);
            foreach (EnemyData enemy in availableEnemies)
            {
                Button tile = MakeTile(grid, GetEnemySprite(enemy), GameplayHUDUI.GetEnemyClassIconSprite(enemy), enemy.enemyName,
                    enemy.baseHP.ToString(CultureInfo.InvariantCulture) + " HP", TextDim, out GameObject badge, out Text badgeText);
                tile.onClick.AddListener(() => HandleEnemyClicked(enemy));
                GetPointerEvents(tile.gameObject).RightClicked = () => RemoveQueuedEnemy(enemy);
                enemyTiles.Add(new UnitTile
                {
                    enemy = enemy, root = tile.gameObject, badge = badge, badgeText = badgeText,
                    searchName = (enemy.enemyName ?? string.Empty).ToLowerInvariant()
                });
            }
            enemyEmptyText = MakeText(scroll.content, "No enemies match.", 8, TextDim, TextAnchor.MiddleCenter);
            return root;
        }

        private GameObject BuildOperatorsDropdown()
        {
            Transform panel = MakeDropdownPanel(Dropdown.Operators, out GameObject root);

            Transform top = MakeStrip(panel, 22f, 3f);
            MakeSearchField(top, "Search operators...", operatorSearch, value =>
            {
                operatorSearch = value;
                ApplyOperatorFilter();
            });
            deckCountText = MakeText(top, "DECK", 8, AccentColor, TextAnchor.MiddleCenter);
            FitText(deckCountText, 6);
            Size(deckCountText.gameObject, -1f, 58f, 0f);
            Button clear = MakeIconButton(top, "icon_discard", DangerColor, 0f);
            Size(clear.gameObject, -1f, 24f, 0f);
            clear.onClick.AddListener(ClearDeck);
            AddHint(clear, "Clear the deck");

            Transform chips = MakeStrip(panel, 20f, 2f);
            AddFilterChip(chips, operatorFilterButtons, null, "ALL", "All classes", () => { operatorFilter = -1; ApplyOperatorFilter(); });
            foreach (OperatorClass operatorClass in System.Enum.GetValues(typeof(OperatorClass)))
            {
                int value = (int)operatorClass;
                AddFilterChip(chips, operatorFilterButtons, GameplayHUDUI.GetClassIconSprite(operatorClass), null, operatorClass.ToString(),
                    () => { operatorFilter = value; ApplyOperatorFilter(); });
            }

            Text help = MakeText(panel, "Click adds a card at base rarity. No squad cap; retreats refund at once.", 8, TextDim, TextAnchor.MiddleLeft);
            FitText(help, 6);
            Size(help.gameObject, 12f);

            ScrollArea scroll = MakeScrollArea(panel);
            dropdownScrolls[(int)Dropdown.Operators] = scroll;
            Transform grid = MakeTileGrid(scroll.content);
            foreach (OperatorData op in operatorData)
            {
                Sprite classIcon = GameplayHUDUI.GetClassIconSprite(op.operatorClass);
                string stars = new string('*', Mathf.Clamp((int)op.baseRarity, 1, 5));
                Button tile = MakeTile(grid, op.portrait != null ? op.portrait : classIcon, classIcon, op.operatorName, stars, GoldColor,
                    out GameObject badge, out Text badgeText);
                tile.onClick.AddListener(() => AddOperator(op));
                operatorTiles.Add(new UnitTile
                {
                    op = op, root = tile.gameObject, badge = badge, badgeText = badgeText,
                    searchName = (op.operatorName ?? string.Empty).ToLowerInvariant()
                });
            }
            operatorEmptyText = MakeText(scroll.content, "No operators match.", 8, TextDim, TextAnchor.MiddleCenter);
            return root;
        }

        private GameObject BuildWavesDropdown()
        {
            Transform panel = MakeDropdownPanel(Dropdown.Waves, out GameObject root);

            Transform difficulties = MakeStrip(panel, 20f, 3f);
            StageDifficulty[] values = { StageDifficulty.Easy, StageDifficulty.Normal, StageDifficulty.Hard };
            for (int i = 0; i < values.Length; i++)
            {
                StageDifficulty difficulty = values[i];
                difficultyButtons[i] = MakeButton(difficulties, difficulty.ToString().ToUpperInvariant(), 8, ControlColor, TextColor);
                difficultyButtons[i].onClick.AddListener(() => SelectDifficulty(difficulty));
            }

            waveStageText = MakeText(panel, string.Empty, 8, TextColor, TextAnchor.MiddleLeft);
            FitText(waveStageText, 6);
            Size(waveStageText.gameObject, 12f);

            ScrollArea scroll = MakeScrollArea(panel);
            dropdownScrolls[(int)Dropdown.Waves] = scroll;
            waveRowsRoot = scroll.content;

            // Selected wave: roster and load buttons, always in view under the list
            GameObject detail = MakeObject("WaveDetail", panel);
            detail.AddComponent<Image>().color = SurfaceColor;
            Column(detail, new RectOffset(6, 6, 6, 6), 4f);

            waveDetailTitle = MakeText(detail.transform, string.Empty, 9, TextColor, TextAnchor.MiddleLeft);
            FitText(waveDetailTitle, 6);
            Size(waveDetailTitle.gameObject, 13f);

            GameObject roster = MakeObject("Roster", detail.transform);
            GridLayoutGroup rosterGrid = roster.AddComponent<GridLayoutGroup>();
            rosterGrid.cellSize = new Vector2(36f, 34f);
            rosterGrid.spacing = new Vector2(3f, 3f);
            rosterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rosterGrid.constraintCount = 8;
            rosterRoot = roster.transform;

            waveDetailSummary = MakeText(detail.transform, string.Empty, 8, TextDim, TextAnchor.MiddleLeft);
            FitText(waveDetailSummary, 6);
            Size(waveDetailSummary.gameObject, 12f);

            Transform buttons = MakeStrip(detail.transform, 24f, 4f);
            loadWaveButton = MakeButton(buttons, "LOAD", 9, AccentFill, TextColor);
            loadWaveButton.onClick.AddListener(() => LoadSelectedWave(true));
            AddHint(loadWaveButton, "Replace the queue with this wave");
            addWaveButton = MakeButton(buttons, "+ ADD", 9, ControlColor, TextColor);
            addWaveButton.onClick.AddListener(() => LoadSelectedWave(false));
            AddHint(addWaveButton, "Add this wave to the queue");
            return root;
        }

        private void RebuildWaveRows(WaveData[] waves)
        {
            builtWaves = waves;
            wavesBuilt = true;
            builtRosterIndex = -1;
            ClearChildren(waveRowsRoot);
            waveRowButtons.Clear();
            if (waves == null) return;

            for (int i = 0; i < waves.Length; i++)
            {
                int index = i;
                WaveData wave = waves[i];
                MeasureWave(wave, out int enemies, out _, out float span);
                bool spike = (i + 1) % 5 == 0 || i == waves.Length - 1;

                Button row = MakeButton(waveRowsRoot, null, 0, RowColor, TextColor);
                row.gameObject.name = "Wave " + (i + 1);
                Size(row.gameObject, ListRowHeight);

                Text number = MakeText(row.transform, (i + 1).ToString("00"), 9, spike ? GoldColor : TextColor, TextAnchor.MiddleLeft);
                Place(number.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(24f, 0f));
                string waveName = wave != null && !string.IsNullOrEmpty(wave.waveName) ? wave.waveName : "-";
                Text name = MakeText(row.transform, waveName, 8, TextDim, TextAnchor.MiddleLeft);
                FitText(name, 6);
                Stretch(name.rectTransform, 34f, 0f, 92f, 0f);
                Text info = MakeText(row.transform, $"{enemies} foes  {Mathf.CeilToInt(span)}s", 8, TextColor, TextAnchor.MiddleRight);
                FitText(info, 6);
                Place(info.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(84f, 0f));

                row.onClick.AddListener(() => SelectWave(index));
                waveRowButtons.Add(row);
            }
        }

        /// <summary>The selected wave's enemies, one cell per enemy type with its total count.</summary>
        private void RebuildRoster(WaveData wave)
        {
            ClearChildren(rosterRoot);
            if (wave?.entries == null) return;

            var counts = new Dictionary<EnemyData, int>();
            var order = new List<EnemyData>();
            foreach (WaveEntry entry in wave.entries)
            {
                if (entry == null || entry.enemyData == null || entry.count <= 0) continue;
                if (!counts.ContainsKey(entry.enemyData))
                {
                    counts[entry.enemyData] = 0;
                    order.Add(entry.enemyData);
                }
                counts[entry.enemyData] += entry.count;
            }

            foreach (EnemyData enemy in order)
            {
                GameObject cell = MakeObject(enemy.enemyName, rosterRoot);
                cell.AddComponent<Image>().color = RowColor;
                Image icon = MakeImage(cell.transform, "Icon", Color.white);
                icon.sprite = GetEnemySprite(enemy);
                icon.preserveAspect = true;
                Place(icon.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(24f, 20f));
                Text count = MakeText(cell.transform, "x" + counts[enemy], 7, GoldColor, TextAnchor.LowerCenter);
                Stretch(count.rectTransform, 0f, 0f, 0f, 2f);
                AddHint(cell.GetComponent<Image>(), enemy.enemyName);
            }
        }

        private GameObject BuildQueueDropdown()
        {
            Transform panel = MakeDropdownPanel(Dropdown.Queue, out GameObject root);

            Transform header = MakeStrip(panel, 22f, 3f);
            queueTitleText = MakeText(header, "QUEUE", 9, TextColor, TextAnchor.MiddleLeft);
            FitText(queueTitleText, 6);
            Size(queueTitleText.gameObject, -1f, -1f, 1f);
            keepQueueButton = MakeIconButton(header, "icon_reroll", ControlColor, 0f);
            Size(keepQueueButton.gameObject, -1f, 24f, 0f);
            keepQueueButton.onClick.AddListener(ToggleKeepQueue);
            AddHint(keepQueueButton, "Keep the queue after START (replay the wave)");
            Button clear = MakeIconButton(header, "icon_discard", DangerColor, 0f);
            Size(clear.gameObject, -1f, 24f, 0f);
            clear.onClick.AddListener(ClearQueue);
            AddHint(clear, "Clear the queue");

            ScrollArea scroll = MakeScrollArea(panel);
            dropdownScrolls[(int)Dropdown.Queue] = scroll;
            GameObject rows = MakeObject("QueueRows", scroll.content);
            Column(rows, new RectOffset(0, 0, 0, 0), 3f);
            queueRowsRoot = rows.transform;
            queueEmptyText = MakeText(scroll.content, "Empty. Add enemies from ENEMIES or load one from WAVES.", 8, TextDim, TextAnchor.MiddleLeft);

            Text help = MakeText(panel, "Hand-queued: 1 per second, top to bottom. Wave lines keep their own timing and run alongside.", 7, TextDim, TextAnchor.UpperLeft);
            help.color = TextDim * new Color(1f, 1f, 1f, 0.85f);
            return root;
        }

        /// <summary>One queue line: icon, name over its timing, count stepper, reorder (hand-queued lines only) and remove.</summary>
        private void MakeQueueRow(Transform parent, SandboxSpawnGroup group, bool isFirstUntimed, bool isLastUntimed)
        {
            GameObject row = MakeObject("QueueRow", parent);
            row.AddComponent<Image>().color = group.timed ? RowColor : UntimedRowColor;
            Size(row, QueueRowHeight);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 5, 5);
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            Image icon = MakeImage(row.transform, "Icon", Color.white);
            icon.sprite = GetEnemySprite(group.enemy);
            icon.preserveAspect = true;
            Size(icon.gameObject, -1f, 20f, 0f);

            GameObject labels = MakeObject("Labels", row.transform);
            Size(labels, -1f, -1f, 1f);
            Text name = MakeText(labels.transform, group.enemy.enemyName, 8, TextColor, TextAnchor.UpperLeft);
            FitText(name, 6);
            Place(name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, 1f), new Vector2(2f, 0f), new Vector2(-2f, 10f));
            string spawn = group.spawnPoint >= 0 ? "Spawn " + (group.spawnPoint + 1) : "Any spawn";
            string timing = group.timed
                ? $"{spawn}, at {FormatSeconds(group.startDelay)}" + (group.count > 1 ? $", every {FormatSeconds(group.interval)}" : "")
                : "In order, 1 per second";
            Text detail = MakeText(labels.transform, timing, 7, TextDim, TextAnchor.LowerLeft);
            FitText(detail, 5);
            Place(detail.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(2f, 0f), new Vector2(-2f, 9f));

            MakeIconButton(row.transform, "icon_minus", ControlColor, 0f).onClick.AddListener(() => ChangeGroupCount(group, -1));
            Text count = MakeText(row.transform, "x" + group.count, 9, GoldColor, TextAnchor.MiddleCenter);
            Size(count.gameObject, -1f, 26f, 0f);
            MakeIconButton(row.transform, "icon_plus", ControlColor, 0f).onClick.AddListener(() => ChangeGroupCount(group, 1));

            if (!group.timed)
            {
                Button up = MakeIconButton(row.transform, "icon_play", ControlColor, 90f);
                up.interactable = !isFirstUntimed;
                up.onClick.AddListener(() => MoveGroup(group, -1));
                Button down = MakeIconButton(row.transform, "icon_play", ControlColor, -90f);
                down.interactable = !isLastUntimed;
                down.onClick.AddListener(() => MoveGroup(group, 1));
            }
            MakeIconButton(row.transform, "icon_close", DangerColor, 0f).onClick.AddListener(() => RemoveGroup(group));
        }

        private GameObject BuildMapDropdown()
        {
            Transform panel = MakeDropdownPanel(Dropdown.Map, out GameObject root);

            string description = GetHazardDescription(bootstrapper != null ? bootstrapper.Environment : StageEnvironment.None, out bool hasHazard);
            int split = description.IndexOf('\n');
            GameObject hazard = MakeObject("Hazard", panel);
            hazard.AddComponent<Image>().color = SurfaceColor;
            Column(hazard, new RectOffset(6, 6, 6, 6), 3f);
            MakeText(hazard.transform, split >= 0 ? description.Substring(0, split) : description, 9, GoldColor, TextAnchor.UpperLeft);
            if (split >= 0) MakeText(hazard.transform, description.Substring(split + 1), 8, TextDim, TextAnchor.UpperLeft);
            if (hasHazard)
            {
                mapHazardButton = MakeButton(hazard.transform, "HAZARD ON", 8, AccentFill, TextColor);
                Size(mapHazardButton.gameObject, 22f);
                mapHazardButton.onClick.AddListener(ToggleHazard);
                AddHint(mapHazardButton, "OFF keeps the visuals but stops damage, debuffs and spawns");
            }

            Text note = MakeText(panel, "Switching map restarts the sandbox: deck, queue and operators reset.", 7, TextDim, TextAnchor.UpperLeft);
            note.color = TextDim;

            ScrollArea scroll = MakeScrollArea(panel);
            dropdownScrolls[(int)Dropdown.Map] = scroll;
            foreach (StageData stage in mapStages)
            {
                bool isCurrent = stage == currentMap;
                Button row = MakeListRow(scroll.content, null, GetMapLabel(stage), isCurrent ? "CURRENT" : string.Empty, AccentColor,
                    isCurrent ? RowCurrentColor : RowColor);
                row.onClick.AddListener(() => SwitchMap(stage));
            }
            return root;
        }
    }
}
