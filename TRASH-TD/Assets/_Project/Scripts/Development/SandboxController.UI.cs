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
    /// <summary>
    /// The sandbox UI. Nothing covers the map by default: a slim toolbar under the HUD's top-right corner,
    /// a one-line live readout under it, and dropdowns that open one at a time below that.
    /// Dropdowns stay open while the map is used (spawning mid-wave, deploying from the deck) and close
    /// from their toolbar button, another dropdown, or Esc. Messages are short toasts at the top centre.
    /// </summary>
    public sealed partial class SandboxController
    {
        private enum Dropdown { None = -1, Enemies, Operators, Waves, Queue, Map }

        private static readonly string[] DropdownLabels = { "ENEMIES", "OPERATORS", "WAVES", "QUEUE", "MAP" };
        private static readonly float[] DropdownWidths = { 404f, 404f, 340f, 340f, 300f };

        // Palette: the HUD's terminal colours on flat dark surfaces
        private static readonly Color PanelTint = new Color(0.03f, 0.04f, 0.06f, 0.96f);
        private static readonly Color SurfaceColor = new Color(0.055f, 0.07f, 0.1f, 1f);
        private static readonly Color RowColor = new Color(0.09f, 0.12f, 0.165f, 1f);
        private static readonly Color RowCurrentColor = new Color(0.08f, 0.22f, 0.15f, 1f);
        private static readonly Color UntimedRowColor = new Color(0.12f, 0.15f, 0.2f, 1f);
        private static readonly Color ControlColor = new Color(0.15f, 0.19f, 0.25f, 1f);
        private static readonly Color DangerColor = new Color(0.52f, 0.16f, 0.15f, 1f);
        private static readonly Color WarnFill = new Color(0.45f, 0.32f, 0.07f, 1f);
        private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color AccentFill = new Color(0.1f, 0.42f, 0.25f, 1f);
        private static readonly Color PrepColor = new Color(0.5f, 0.8f, 1f, 1f);
        private static readonly Color WaveColor = new Color(1f, 0.45f, 0.35f, 1f);
        private static readonly Color GoldColor = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color BadgeTextColor = new Color(0.1f, 0.07f, 0.02f, 1f);
        private static readonly Color TextColor = new Color(0.92f, 0.95f, 0.98f, 1f);
        private static readonly Color TextDim = new Color(0.5f, 0.58f, 0.68f, 1f);
        private static readonly Color LineColor = new Color(0.2f, 0.26f, 0.34f, 1f);
        // Declared after the colours they read (static fields initialise in order within a file)
        private static readonly string DimHex = "#" + ColorUtility.ToHtmlStringRGB(TextDim);
        private static readonly string PrepHex = "#" + ColorUtility.ToHtmlStringRGB(PrepColor);
        private static readonly string WaveHex = "#" + ColorUtility.ToHtmlStringRGB(WaveColor);

        // Layout, in 1920x1080 reference units
        private const float ScreenMargin = 10f;
        private const float ToolbarTop = 96f;        // just under the HUD top bar (90)
        private const float ToolbarHeight = 46f;
        private const int ToolbarFontSize = 14;       // PressStart2P: one glyph is as wide as the font size
        private const float ToolbarIconSize = 22f;
        private const float ReadoutTop = ToolbarTop + ToolbarHeight + 3f;
        private const float ReadoutHeight = 26f;
        private const float DropdownTop = ReadoutTop + ReadoutHeight + 5f;
        private const float DropdownBottom = 180f;   // clears the deck bar (170)
        private const int GridColumns = 5;
        private static readonly Vector2 TileSize = new Vector2(72f, 84f);
        private const float ListRowHeight = 22f;
        private const float QueueRowHeight = 30f;
        private const float SmallButtonSize = 20f;
        private const float StatsCardWidth = 240f;
        private const float TooltipWidth = 190f;
        private const float ToastSeconds = 3f;
        private const int DamageMeterRows = 10;

        // Kept across the scene reload a map switch does
        private static Dropdown openDropdown = Dropdown.None;
        private static bool statsVisible;
        private static bool uiHidden;
        private static int enemyFilter = -1;    // EnemyArchetype index, -1 = all
        private static int operatorFilter = -1; // OperatorClass index, -1 = all
        private static string enemySearch = string.Empty;
        private static string operatorSearch = string.Empty;

        private sealed class UnitTile
        {
            public EnemyData enemy;
            public OperatorData op;
            public GameObject root;
            public GameObject badge;
            public Text badgeText;
            public string searchName;
        }

        private struct ScrollArea
        {
            public GameObject page;
            public RectTransform content;
            public LayoutElement element;
        }

        private RectTransform uiRoot;
        private RectTransform canvasRect;
        private Camera uiCamera;
        private readonly Vector3[] corners = new Vector3[4];

        // Toolbar, readout, toast, hint
        private GameObject toolbar;
        private GameObject showButton;
        private readonly Button[] dropdownButtons = new Button[5];
        private readonly GameObject[] dropdownPanels = new GameObject[5];
        private readonly ScrollArea[] dropdownScrolls = new ScrollArea[5];
        private Text queueButtonText;
        private Button killAllButton, hazardButton, statsButton;
        private Image hazardIcon;
        private GameObject readout;
        private Text readoutText;
        private GameObject toast;
        private CanvasGroup toastGroup;
        private Text toastText;
        private float toastTimer;
        private RectTransform hintRect;
        private Text hintText;

        // ENEMIES
        private readonly List<UnitTile> enemyTiles = new List<UnitTile>();
        private readonly List<Button> enemyFilterButtons = new List<Button>();
        private readonly Button[] amountButtons = new Button[3];
        private Text enemyHintText, enemyEmptyText;

        // OPERATORS
        private readonly List<UnitTile> operatorTiles = new List<UnitTile>();
        private readonly List<Button> operatorFilterButtons = new List<Button>();
        private Text deckCountText, operatorEmptyText;

        // WAVES
        private readonly Button[] difficultyButtons = new Button[3];
        private readonly List<Button> waveRowButtons = new List<Button>();
        private Text waveStageText, waveDetailTitle, waveDetailSummary;
        private Transform waveRowsRoot, rosterRoot;
        private Button loadWaveButton, addWaveButton;
        private WaveData[] builtWaves;
        private bool wavesBuilt;
        private int builtRosterIndex = -1;

        // QUEUE
        private Text queueTitleText, queueEmptyText;
        private Button keepQueueButton;
        private Transform queueRowsRoot;

        // MAP
        private Button mapHazardButton;

        // Stats card
        private GameObject statsCard;
        private Text statsWaveText, damageNamesText, damageValuesText;

        // Enemy tooltip
        private RectTransform tooltip;
        private Text tooltipTitle, tooltipBody;
        private RectTransform tooltipHpFill;
        private Image tooltipHpFillImage;
        private EnemyBase tooltipTarget;

        /// <summary>True while the pointer is over the open dropdown (right-clicks there edit the queue, not operators).</summary>
        public bool IsPointerOverDropdown
        {
            get
            {
                if (uiHidden || openDropdown == Dropdown.None || !PointerInput.Available) return false;
                GameObject panel = dropdownPanels[(int)openDropdown];
                if (panel == null || !panel.activeInHierarchy) return false;
                Vector2 pointer = PointerInput.PicturePosition;
                return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)panel.transform, pointer, uiCamera);
            }
        }

        // ============================
        // Open / close
        // ============================

        private void ToggleDropdown(Dropdown dropdown)
        {
            openDropdown = openDropdown == dropdown ? Dropdown.None : dropdown;
            ApplyVisibility();
        }

        private void ToggleStats()
        {
            statsVisible = !statsVisible;
            ApplyVisibility();
            if (statsVisible) RefreshStats();
        }

        private void SetUiHidden(bool hidden)
        {
            uiHidden = hidden;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            HideHint();
            if (toolbar != null) toolbar.SetActive(!uiHidden);
            if (readout != null) readout.SetActive(!uiHidden);
            if (showButton != null) showButton.SetActive(uiHidden);
            if (uiHidden && toast != null) toast.SetActive(false);
            for (int i = 0; i < dropdownPanels.Length; i++)
            {
                bool open = !uiHidden && (int)openDropdown == i;
                if (dropdownPanels[i] != null) dropdownPanels[i].SetActive(open);
                if (dropdownButtons[i] != null) Paint(dropdownButtons[i], open ? AccentFill : ControlColor);
            }
            if (statsCard != null) statsCard.SetActive(!uiHidden && statsVisible);
            if (statsButton != null) Paint(statsButton, statsVisible ? AccentFill : ControlColor);
            if (!uiHidden && openDropdown != Dropdown.None) FitDropdown(openDropdown);
        }

        /// <summary>Esc closes the open dropdown, unless a placement is being cancelled or the game is paused.</summary>
        private void HandleEscape()
        {
            if (uiHidden || openDropdown == Dropdown.None) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
            if (gameplayHud != null && (gameplayHud.HasConsumedEscapeThisFrame || gameplayHud.IsPaused)) return;
            if (bootstrapper != null && bootstrapper.IsPlacementActive) return;

            openDropdown = Dropdown.None;
            ApplyVisibility();
            gameplayHud?.ConsumeEscapeKey();
        }

        /// <summary>Per-frame UI work: the toast fade and keeping the enemy tooltip next to its enemy.</summary>
        private void UpdateUiFrame()
        {
            if (toastTimer > 0f && toast != null)
            {
                toastTimer -= Time.unscaledDeltaTime;
                toastGroup.alpha = Mathf.Clamp01(toastTimer / 0.4f);
                if (toastTimer <= 0f) toast.SetActive(false);
            }
            PositionTooltip();
        }

        private void SetStatus(string message, bool isProblem = false)
        {
            if (toast == null) return;
            toastText.text = message;
            toastText.color = isProblem ? WaveColor : GoldColor;
            toastTimer = ToastSeconds;
            toastGroup.alpha = 1f;
            toast.SetActive(!uiHidden);
            toast.transform.SetAsLastSibling();
        }

        // ============================
        // Refresh
        // ============================

        private void RefreshAll()
        {
            lastWaveState = IsWaveActive;
            RefreshSpawnAmount();
            RefreshQueue();
            RefreshDeck();
            RefreshWaves();
            RefreshHazard();
            RefreshLive();
            gameplayHud?.SetSandboxWaveButton(lastWaveState);
        }

        private void RefreshLive()
        {
            int alive = enemyManager != null ? enemyManager.ActiveEnemyCount : 0;
            if (readoutText != null)
            {
                int pending = lastWaveState && waveManager != null ? waveManager.PendingSpawnCount : CountQueued();
                string mode = lastWaveState ? $"<color={WaveHex}>WAVE LIVE</color>" : $"<color={PrepHex}>PRE-WAVE</color>";
                string leaks = waveLeaks > 0 ? $"<color={WaveHex}>{waveLeaks}</color>" : "0";
                readoutText.text =
                    $"{mode}   <color={DimHex}>TIME</color> {FormatTime(waveTime)}   <color={DimHex}>ALIVE</color> {alive}   " +
                    $"<color={DimHex}>{(lastWaveState ? "TO SPAWN" : "QUEUED")}</color> {pending}   " +
                    $"<color={DimHex}>KILLS</color> {waveKills}   <color={DimHex}>LEAKS</color> {leaks}";
            }
            if (killAllButton != null && killAllButton.interactable != (lastWaveState && alive > 0))
            {
                killAllButton.interactable = lastWaveState && alive > 0;
                Paint(killAllButton, DangerColor);
            }
        }

        private void RefreshSpawnAmount()
        {
            for (int i = 0; i < amountButtons.Length; i++)
                SetSelected(amountButtons[i], i == spawnAmountIndex);
            if (enemyHintText == null) return;
            int amount = SpawnAmount;
            enemyHintText.text = lastWaveState
                ? $"WAVE LIVE: click spawns {amount} now"
                : $"PRE-WAVE: click queues {amount}, right-click removes {amount}";
            enemyHintText.color = lastWaveState ? WaveColor : PrepColor;
        }

        private void RefreshQueue()
        {
            int total = CountQueued();
            if (!queue.Exists(group => group.timed)) loadedWaveNumber = 0;

            if (queueButtonText != null) queueButtonText.text = total > 0 ? $"QUEUE {total}" : "QUEUE";
            if (queueTitleText != null)
            {
                bool hasUntimed = queue.Exists(group => !group.timed);
                string source = loadedWaveNumber > 0 ? loadedWaveLabel + (hasUntimed ? " + extras" : "") : total > 0 ? "Hand-built" : "";
                queueTitleText.text = total > 0 ? $"{total} ENEMIES  <color={DimHex}>{source}</color>" : "QUEUE EMPTY";
            }
            if (keepQueueButton != null) Paint(keepQueueButton, keepQueueAfterStart ? AccentFill : ControlColor);

            foreach (UnitTile tile in enemyTiles)
                SetBadge(tile, CountQueued(tile.enemy));

            if (queueRowsRoot == null) return;
            ClearChildren(queueRowsRoot);
            queueEmptyText.gameObject.SetActive(queue.Count == 0);
            for (int i = 0; i < queue.Count; i++)
                MakeQueueRow(queueRowsRoot, queue[i], IsFirstUntimed(i), IsLastUntimed(i));
            FitDropdown(Dropdown.Queue);
        }

        private bool IsFirstUntimed(int index)
        {
            for (int i = index - 1; i >= 0; i--) if (!queue[i].timed) return false;
            return true;
        }

        private bool IsLastUntimed(int index)
        {
            for (int i = index + 1; i < queue.Count; i++) if (!queue[i].timed) return false;
            return true;
        }

        private void RefreshDeck()
        {
            if (playerDeck == null) return;
            if (deckCountText != null)
            {
                bool full = playerDeck.CardCount >= PlayerDeck.MAX_DECK_SIZE;
                deckCountText.text = $"DECK {playerDeck.CardCount}/{PlayerDeck.MAX_DECK_SIZE}";
                deckCountText.color = full ? WaveColor : AccentColor;
            }

            IReadOnlyList<DraftCard> deck = playerDeck.DeckSlots;
            foreach (UnitTile tile in operatorTiles)
            {
                int copies = 0;
                for (int i = 0; i < deck.Count; i++)
                    if (deck[i] != null && deck[i].operatorData == tile.op) copies++;
                SetBadge(tile, copies);
            }
        }

        private void RefreshWaves()
        {
            if (waveRowsRoot == null) return;

            for (int i = 0; i < difficultyButtons.Length; i++)
                SetSelected(difficultyButtons[i], i == (int)waveDifficulty);

            WaveData[] waves = GetMapWaves();
            if (!wavesBuilt || waves != builtWaves) RebuildWaveRows(waves);

            bool hasWaves = waves != null && waves.Length > 0;
            loadWaveButton.interactable = hasWaves;
            addWaveButton.interactable = hasWaves;
            Paint(loadWaveButton, AccentFill);
            Paint(addWaveButton, ControlColor);
            string stage = currentMap != null ? "STAGE " + GetStageNumber(currentMap) : "THIS MAP";
            waveStageText.text = hasWaves
                ? $"{stage}  <color={DimHex}>{waves.Length} WAVES, gold = spike</color>"
                : $"{stage}  <color={DimHex}>no waves on this difficulty</color>";

            if (!hasWaves)
            {
                waveDetailTitle.text = "-";
                waveDetailSummary.text = string.Empty;
                ClearChildren(rosterRoot);
                builtRosterIndex = -1;
                FitDropdown(Dropdown.Waves);
                return;
            }

            waveIndex = Mathf.Clamp(waveIndex, 0, waves.Length - 1);
            for (int i = 0; i < waveRowButtons.Count; i++)
                PaintBackground(waveRowButtons[i], i == waveIndex ? RowCurrentColor : RowColor);

            WaveData wave = waves[waveIndex];
            string name = wave != null && !string.IsNullOrEmpty(wave.waveName) ? wave.waveName : "(unnamed wave)";
            waveDetailTitle.text = $"WAVE {waveIndex + 1}  <color={DimHex}>{name}</color>";
            MeasureWave(wave, out int enemies, out int spawns, out float span);
            waveDetailSummary.text = $"{enemies} enemies, {spawns} {(spawns == 1 ? "spawn" : "spawns")}, {Mathf.CeilToInt(span)}s of spawning";
            if (builtRosterIndex != waveIndex)
            {
                builtRosterIndex = waveIndex;
                RebuildRoster(wave);
            }
            FitDropdown(Dropdown.Waves);
        }

        private void RefreshHazard()
        {
            bool on = StageCombatModifiers.HazardsEnabled;
            if (hazardButton != null)
            {
                Paint(hazardButton, on ? WarnFill : ControlColor);
                if (hazardIcon != null) hazardIcon.color = on ? GoldColor : TextDim;
            }
            if (mapHazardButton != null)
            {
                mapHazardButton.GetComponentInChildren<Text>().text = on ? "HAZARD ON" : "HAZARD OFF";
                Paint(mapHazardButton, on ? AccentFill : DangerColor);
            }
        }

        private void RefreshStats()
        {
            if (statsWaveText == null) return;

            int totalDamage = 0;
            foreach (KeyValuePair<OperatorBase, int> pair in damageByOperator) totalDamage += pair.Value;
            float seconds = Mathf.Max(1f, waveTime);

            statsWaveText.text =
                $"{waveSource}\n" +
                $"Time {FormatTime(waveTime)}   Kills {waveKills}\n" +
                $"Leaks {waveLeaks}" + (waveLeakLives > 0 ? $" (would cost {waveLeakLives} {(waveLeakLives == 1 ? "life" : "lives")})" : "") + "\n" +
                $"Damage {FormatNumber(totalDamage)} ({FormatNumber(Mathf.RoundToInt(totalDamage / seconds))}/s)";

            RefreshDamageMeter(seconds);
        }

        private void RefreshDamageMeter(float seconds)
        {
            damageRanking.Clear();
            foreach (KeyValuePair<OperatorBase, int> pair in damageByOperator)
                if (pair.Key != null) damageRanking.Add(pair);
            damageRanking.Sort((a, b) => b.Value.CompareTo(a.Value));

            if (damageRanking.Count == 0)
            {
                damageNamesText.text = "No damage dealt this wave.";
                damageValuesText.text = string.Empty;
                return;
            }

            var names = new StringBuilder();
            var values = new StringBuilder();
            int shown = Mathf.Min(DamageMeterRows, damageRanking.Count);
            for (int i = 0; i < shown; i++)
            {
                OperatorBase op = damageRanking[i].Key;
                if (i > 0) { names.Append('\n'); values.Append('\n'); }
                names.Append(i + 1).Append(". ").Append(op.Data != null ? op.Data.operatorName : op.name);
                values.Append(FormatNumber(damageRanking[i].Value)).Append("  ")
                    .Append(FormatNumber(Mathf.RoundToInt(damageRanking[i].Value / seconds))).Append("/s");
            }
            if (damageRanking.Count > shown) names.Append("\n+").Append(damageRanking.Count - shown).Append(" more");
            damageNamesText.text = names.ToString();
            damageValuesText.text = values.ToString();
        }

        private void ApplyEnemyFilter()
        {
            string search = enemySearch.Trim().ToLowerInvariant();
            int shown = 0;
            foreach (UnitTile tile in enemyTiles)
            {
                bool show = (enemyFilter < 0 || (int)tile.enemy.archetype == enemyFilter) &&
                            (search.Length == 0 || tile.searchName.Contains(search));
                tile.root.SetActive(show);
                if (show) shown++;
            }
            if (enemyEmptyText != null) enemyEmptyText.gameObject.SetActive(shown == 0);
            for (int i = 0; i < enemyFilterButtons.Count; i++)
                SetSelected(enemyFilterButtons[i], i - 1 == enemyFilter);
            FitDropdown(Dropdown.Enemies);
        }

        private void ApplyOperatorFilter()
        {
            string search = operatorSearch.Trim().ToLowerInvariant();
            int shown = 0;
            foreach (UnitTile tile in operatorTiles)
            {
                bool show = (operatorFilter < 0 || (int)tile.op.operatorClass == operatorFilter) &&
                            (search.Length == 0 || tile.searchName.Contains(search));
                tile.root.SetActive(show);
                if (show) shown++;
            }
            if (operatorEmptyText != null) operatorEmptyText.gameObject.SetActive(shown == 0);
            for (int i = 0; i < operatorFilterButtons.Count; i++)
                SetSelected(operatorFilterButtons[i], i - 1 == operatorFilter);
            FitDropdown(Dropdown.Operators);
        }

        private static void SetBadge(UnitTile tile, int count)
        {
            if (tile.badge == null) return;
            tile.badge.SetActive(count > 0);
            if (count > 0) tile.badgeText.text = "x" + count;
        }

        /// <summary>
        /// Sizes the open dropdown's list to its content, capped so the whole panel stays above the deck bar.
        /// Short lists give a short panel; long ones scroll.
        /// </summary>
        private void FitDropdown(Dropdown dropdown)
        {
            if (dropdown == Dropdown.None) return;
            GameObject panel = dropdownPanels[(int)dropdown];
            ScrollArea scroll = dropdownScrolls[(int)dropdown];
            if (panel == null || !panel.activeInHierarchy || scroll.content == null) return;

            RectTransform panelRect = (RectTransform)panel.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
            // The panel rebuild stops at the Viewport (no layout component), so the list is its own
            // layout root. Rebuild it too, or a panel opened for the first time measures a list that was
            // never laid out and comes out cut off until it is reopened.
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            float contentHeight = LayoutUtility.GetPreferredHeight(scroll.content);

            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            float chrome = layout.padding.vertical;
            int visible = 0;
            foreach (Transform child in panel.transform)
            {
                if (!child.gameObject.activeSelf) continue;
                LayoutElement element = child.GetComponent<LayoutElement>();
                if (element != null && element.ignoreLayout) continue;
                visible++;
                if (child.gameObject != scroll.page) chrome += LayoutUtility.GetPreferredHeight((RectTransform)child);
            }
            chrome += layout.spacing * Mathf.Max(0, visible - 1);

            float screenHeight = canvasRect != null ? canvasRect.rect.height : 1080f;
            float available = screenHeight - DropdownTop - DropdownBottom - chrome;
            scroll.element.preferredHeight = Mathf.Max(40f, Mathf.Min(contentHeight, available));
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        }

        // ============================
        // Enemy tooltip
        // ============================

        /// <summary>Picks the enemy under the mouse (not over UI, not while placing) and fills the tooltip.</summary>
        private void RefreshTooltip()
        {
            if (tooltip == null) return;
            bool wasShown = tooltip.gameObject.activeSelf;
            tooltipTarget = CanShowTooltip() ? FindHoveredEnemy() : null;
            tooltip.gameObject.SetActive(tooltipTarget != null);
            if (tooltipTarget == null) return;
            if (!wasShown) tooltip.SetAsLastSibling();

            EnemyBase enemy = tooltipTarget;
            EnemyData data = enemy.Data;
            tooltipTitle.text = data != null ? data.enemyName : enemy.name;

            float hpRatio = enemy.MaxHP > 0 ? Mathf.Clamp01((float)enemy.CurrentHP / enemy.MaxHP) : 0f;
            tooltipHpFill.anchorMax = new Vector2(hpRatio, 1f);
            tooltipHpFillImage.color = Color.Lerp(WaveColor, AccentColor, hpRatio);

            var body = new StringBuilder();
            if (data != null)
                body.Append(data.archetype).Append(", ").Append(data.movementType).Append(", ").Append(data.lifePointCost)
                    .Append(data.lifePointCost == 1 ? " life\n" : " lives\n");
            body.Append("HP  ").Append(enemy.CurrentHP).Append(" / ").Append(enemy.MaxHP).Append("  (").Append(Mathf.RoundToInt(hpRatio * 100f)).Append("%)\n");
            body.Append("ATK ").Append(enemy.CurrentATK).Append("  DEF ").Append(enemy.CurrentDEF);
            if (data != null)
            {
                int baseDef = data.GetScaledDEF(enemy.DifficultyLevel);
                if (enemy.CurrentDEF != baseDef)
                    body.Append(" <color=").Append(enemy.CurrentDEF > baseDef ? PrepHex : WaveHex).Append(">(").Append(baseDef).Append(")</color>");
            }
            body.Append("  RES ").Append(enemy.CurrentRES).Append('\n');
            body.Append("SPD ").Append(enemy.CurrentMoveSpeed.ToString("0.00", CultureInfo.InvariantCulture));
            if (data != null && !Mathf.Approximately(enemy.CurrentMoveSpeed, data.moveSpeed))
                body.Append(" <color=").Append(DimHex).Append(">(base ").Append(data.moveSpeed.ToString("0.00", CultureInfo.InvariantCulture)).Append(")</color>");

            var effects = new List<string>();
            if (enemy.IsBlocked) effects.Add(enemy.BlockingOperator != null && enemy.BlockingOperator.Data != null
                ? "Blocked by " + enemy.BlockingOperator.Data.operatorName : "Blocked");
            if (enemy.IsFrozen) effects.Add("Frozen");
            if (enemy.IsStunned) effects.Add("Stunned");
            if (enemy.IsSluggish) effects.Add("Sluggish");
            if (enemy.ChillAmount > 0f) effects.Add("Chilled");
            float aura = JunkernautEnemy.GetDamageReductionPercent(enemy);
            if (aura > 0f) effects.Add($"Junkernaut aura -{Mathf.RoundToInt(aura * 100f)}% dmg");
            if (effects.Count > 0) body.Append("\n<color=#FFC740>").Append(string.Join(", ", effects)).Append("</color>");

            tooltipBody.text = body.ToString();
            PositionTooltip();
        }

        private bool CanShowTooltip()
        {
            if (uiHidden || enemyManager == null || enemyManager.ActiveEnemyCount == 0) return false;
            if (gameplayHud != null && gameplayHud.IsPaused) return false;
            if (bootstrapper != null && bootstrapper.IsPlacementActive) return false;
            return EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>The live enemy within ~half a tile of the mouse, or null.</summary>
        private EnemyBase FindHoveredEnemy()
        {
            Camera cam = Camera.main;
            if (cam == null || !PointerInput.Available) return null;

            Vector2 screen = PointerInput.PicturePosition;
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            float cell = bootstrapper != null && bootstrapper.gridManager != null ? bootstrapper.gridManager.CellSize : 1f;
            float bestDistance = (cell * 0.6f) * (cell * 0.6f);
            EnemyBase best = null;
            foreach (EnemyBase enemy in enemyManager.ActiveEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                float distance = ((Vector2)(enemy.VisualPosition - world)).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }

        /// <summary>Keeps the tooltip beside its enemy (flipping left near the screen edge), inside the screen.</summary>
        private void PositionTooltip()
        {
            if (tooltip == null || !tooltip.gameObject.activeSelf) return;
            if (tooltipTarget == null || tooltipTarget.IsDead)
            {
                tooltipTarget = null;
                tooltip.gameObject.SetActive(false);
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, tooltipTarget.VisualPosition);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(uiRoot, screen, uiCamera, out Vector2 local)) return;

            Rect bounds = uiRoot.rect;
            Vector2 size = tooltip.rect.size;
            float x = local.x + 26f;
            if (x + size.x > bounds.xMax - 8f) x = local.x - 26f - size.x;
            float y = Mathf.Clamp(local.y + 14f, bounds.yMin + 8f, bounds.yMax - 8f - size.y);
            tooltip.anchoredPosition = new Vector2(x, y);
        }

        // ============================
        // Hints
        // ============================

        private void AddHint(Component target, string text)
        {
            SandboxPointerEvents events = GetPointerEvents(target.gameObject);
            RectTransform rect = (RectTransform)target.transform;
            events.Entered += () => ShowHint(rect, text);
            events.Exited += HideHint;
        }

        private static SandboxPointerEvents GetPointerEvents(GameObject obj)
        {
            SandboxPointerEvents events = obj.GetComponent<SandboxPointerEvents>();
            return events != null ? events : obj.AddComponent<SandboxPointerEvents>();
        }

        /// <summary>Shows the hint right-aligned under the hovered control.</summary>
        private void ShowHint(RectTransform target, string text)
        {
            if (hintRect == null || target == null) return;
            hintText.text = text;
            hintRect.gameObject.SetActive(true);
            hintRect.SetAsLastSibling();
            target.GetWorldCorners(corners);
            hintRect.position = corners[3];
            hintRect.anchoredPosition += new Vector2(0f, -4f);
        }

        private void HideHint()
        {
            if (hintRect != null) hintRect.gameObject.SetActive(false);
        }
    }
}
