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
    /// <summary>
    /// Reclaimers' Intel: an almanac of every operator and enemy, opened from the main menu and the pause menu.
    /// A dossier picker (Operators / Enemies) leads to a gallery: filter tabs and a portrait grid on the left,
    /// and the selected unit's file on the right (animated sprite, chips, field notes, stats by tier, range, ability).
    /// Units come from <see cref="UnitIntelRegistry"/>, which updates itself when unit assets change, and all text
    /// comes from the unit data assets. Built in code in the green terminal style of AudioSettingsUI; runs on
    /// unscaled time so it works while the game is paused. The creating screen styles and places the open button
    /// and forwards Esc to <see cref="HandleBack"/>.
    /// Enemy files stay locked until the player has met that enemy (<see cref="TrashTD.Core.GameLoop.EnemyIntelProgress"/>):
    /// an animated silhouette, a name of cycling random letters, "???" for every field, and only under the ALL tab so
    /// the archetype isn't given away. SIGHTED AT lists only the stages where the player has met the enemy.
    /// </summary>
    public sealed partial class ReclaimersIntelUI : MonoBehaviour
    {
        // Terminal palette (matches AudioSettingsUI / MainMenuController)
        private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color AccentBright = new Color(0.32f, 1.00f, 0.64f, 1f);
        private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.72f);
        private static readonly Color CardColor = new Color(0.016f, 0.042f, 0.028f, 0.98f);
        private static readonly Color HeaderColor = new Color(0.030f, 0.090f, 0.055f, 1f);
        private static readonly Color TerminalButton = new Color(0.035f, 0.090f, 0.060f, 1f);
        private static readonly Color PanelColor = new Color(0.024f, 0.060f, 0.040f, 1f);
        private static readonly Color StageColor = new Color(0.010f, 0.026f, 0.018f, 1f);
        private static readonly Color LineColor = new Color(0.22f, 0.91f, 0.50f, 0.45f);
        private static readonly Color LabelColor = new Color(0.45f, 0.78f, 0.56f, 1f);
        private static readonly Color HintColor = new Color(0.45f, 0.78f, 0.56f, 0.7f);
        private static readonly Color BodyColor = new Color(0.78f, 0.92f, 0.82f, 1f);
        private static readonly Color SelectedLabel = new Color(0.02f, 0.10f, 0.05f, 1f);

        // Enemy dossier accent: hazard red / amber
        private static readonly Color HostileColor = new Color(0.95f, 0.33f, 0.36f, 1f);
        private static readonly Color HostileBright = new Color(1f, 0.55f, 0.50f, 1f);
        private static readonly Color HostileButton = new Color(0.10f, 0.035f, 0.035f, 1f);
        private static readonly Color HostilePanel = new Color(0.060f, 0.024f, 0.026f, 1f);
        private static readonly Color AmberColor = new Color(1f, 0.75f, 0.25f, 1f);

        // Range tiles (same colours as the draft range preview)
        private static readonly Color RangeSelfColor = new Color(0.15f, 0.85f, 1f, 1f);
        private static readonly Color RangeHitColor = new Color(0.15f, 1f, 0.28f, 1f);
        private static readonly Color RangeUpgradeColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color RangeUpgrade2Color = new Color(1f, 0.52f, 0.18f, 1f);
        private static readonly Color RangeTalentColor = new Color(1f, 0.75f, 0.2f, 1f);
        private static readonly Color RangeEmptyColor = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color EnemySelfColor = new Color(1f, 0.42f, 0.38f, 1f);

        // Layout, in card pixels from the top-left. PressStart2P glyphs are as wide as the font size.
        private const float HeaderHeight = 64f;
        private const float Padding = 36f;
        private const float ChoiceWidth = 1120f;
        private const float ChoiceHeight = 660f;
        private const float OptionWidth = 500f;
        private const float OptionHeight = 452f;
        private const float GalleryWidth = 1760f;
        private const float GalleryHeight = 980f;
        private const float ListX = 32f;
        private const float ListWidth = 640f;
        private const float GridTop = 168f;
        private const int GridColumns = 4;
        private const float TileWidth = 140f;
        private const float TileHeight = 168f;
        private const float TileGap = 16f;
        private const float DetailX = 720f;
        private const float DetailWidth = GalleryWidth - DetailX - 32f;   // 1008
        private const float StageSize = 300f;
        private const float InfoX = DetailX + StageSize + 24f;
        private const float InfoWidth = GalleryWidth - 32f - InfoX;
        private const float DataTop = 420f;
        private const float DataHeight = 272f;
        private const float StatsWidth = 492f;
        private const float RangeX = DetailX + StatsWidth + 24f;
        private const float RangeWidth = DetailWidth - StatsWidth - 24f;
        private const float NotesTop = 708f;
        private const float ShowDuration = 0.18f;

        // Locked enemy files
        private const string SilhouetteShaderPath = "Shaders/UISilhouette";
        private const float ScrambleInterval = 0.08f;
        private const string ScrambleGlyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string UnknownText = "???";
        private static readonly Color SilhouetteColor = new Color(0.42f, 0.13f, 0.15f, 1f);
        // Without the shader, a near-black tint still hides the sprite's details
        private static readonly Color SilhouetteFallbackColor = new Color(0.06f, 0.02f, 0.025f, 1f);

        private static readonly OperatorClass[] ClassOrder =
        {
            OperatorClass.Guard, OperatorClass.Defender, OperatorClass.Sniper,
            OperatorClass.Caster, OperatorClass.Medic, OperatorClass.Specialist
        };

        private static readonly EnemyArchetype[] ArchetypeOrder =
        {
            EnemyArchetype.Grunt, EnemyArchetype.Rusher, EnemyArchetype.Tank,
            EnemyArchetype.Caster, EnemyArchetype.Flyer
        };

        private static readonly string[] DifficultyLabels = { "EASY", "NORMAL", "HARD" };

        private enum Dossier { Operators, Enemies }

        private sealed class IntelEntry
        {
            public OperatorData Operator;
            public EnemyData Enemy;
            public string FileId;
            public Sprite[] Frames;
            public float FrameDuration;
            public bool Locked; // enemy not met yet (refreshed on every Open)
            public string Name => Operator != null ? Operator.operatorName : Enemy != null ? Enemy.enemyName : "UNKNOWN";
        }

        private sealed class StatRow
        {
            public Image Fill;
            public Text Value;
        }

        private sealed class FactBox
        {
            public RectTransform Root;
            public Text Label;
            public Text Value;
        }

        private sealed class TierButton
        {
            public Button Button;
            public Text Label;
            public Image Star;
        }

        public Button IntelButton { get; private set; }
        public bool IsOpen => panel != null && panel.activeSelf;

        private GameObject panel;
        private CanvasGroup panelGroup;
        private RectTransform choiceCard;
        private RectTransform galleryCard;
        private RectTransform activeCard;
        private float showTime = ShowDuration;

        // Choice screen
        private Button operatorsOption;
        private Button enemiesOption;
        private Text operatorsCount;
        private Text enemiesCount;
        private Image operatorsSprite;
        private Image enemiesSprite;
        private int choiceFocus = -1;

        // Gallery
        private Dossier dossier;
        private Text galleryTitle;
        private Image galleryHeaderLine;
        private RectTransform tabRow;
        private Text listCountText;
        private ScrollRect gridScroll;
        private RectTransform gridContent;
        private readonly List<Button> tabButtons = new List<Button>();
        private readonly List<Button> tileButtons = new List<Button>();
        private readonly List<IntelEntry> operatorEntries = new List<IntelEntry>();
        private readonly List<IntelEntry> enemyEntries = new List<IntelEntry>();
        private readonly List<IntelEntry> filtered = new List<IntelEntry>();
        // Stage ids whose waves include each enemy (what dev mode shows), and stage id -> display name / order
        private readonly Dictionary<EnemyData, List<string>> plannedSightings = new Dictionary<EnemyData, List<string>>();
        private readonly Dictionary<string, string> stageNames = new Dictionary<string, string>();
        private readonly Dictionary<string, int> stageOrder = new Dictionary<string, int>();
        // Texts showing a locked enemy's name, re-rolled every ScrambleInterval, with the name they stand for
        private readonly List<(Text text, string template)> scrambled = new List<(Text text, string template)>();
        private float scrambleTimer;
        private Material silhouetteMaterial;
        private bool silhouetteShaderMissing;
        private int filterIndex;
        private int selectedIndex = -1;
        private bool rosterLoaded;
        private readonly int[] operatorStatMax = new int[4];
        private readonly int[] enemyStatMax = new int[4];

        // Detail pane
        private Image stageBackground;
        private Image stageSprite;
        private Image stageFloor;
        private Text fileText;
        private Text nameText;
        private RectTransform chipRow;
        private RectTransform rarityRow;
        private Text statusText;
        private Text statsHeader;
        private readonly List<TierButton> tierButtons = new List<TierButton>();
        private readonly List<StatRow> statRows = new List<StatRow>();
        private readonly List<FactBox> factBoxes = new List<FactBox>();
        private Text rangeHeader;
        private RectTransform rangeGrid;
        private Text rangeReadout;
        private RectTransform rangeLegend;
        private ScrollRect notesScroll;
        private Text abilityHeader;
        private Text abilityText;
        private Text loreText;
        private ScrollRect loreScroll;

        // Body text sizes for the field notes and the skill / ability description
        private const int LoreFontSize = 17;
        private const int NotesFontSize = 15;
        private Text sightedHeader;
        private Text sightedText;
        private int tierIndex;
        private Sprite[] playingFrames;
        private float playingFrameDuration = 0.25f;
        private float frameTimer;
        private int frameIndex;

        private static Sprite gridSprite;
        private static Sprite tileFrameSprite;
        private static Sprite hazardSprite;

        public static ReclaimersIntelUI Create(
            Transform canvasRoot,
            Transform buttonParent,
            Action<Button> styleButton,
            Action<RectTransform> positionButton)
        {
            if (canvasRoot == null || buttonParent == null) return null;

            GameObject root = new GameObject("ReclaimersIntelUI", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var intel = root.AddComponent<ReclaimersIntelUI>();
            intel.Build(buttonParent, styleButton, positionButton);
            return intel;
        }

        // ─────────────────────────────────────────────────────
        //  Open / close / back
        // ─────────────────────────────────────────────────────

        public void Open()
        {
            if (panel == null) return;
            LoadRoster();
            RefreshEncounters(); // enemies met since the last Open (the pause menu's intel stays alive all stage)
            ShowChoice();
            panel.transform.SetAsLastSibling();
            panel.SetActive(true);
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            playingFrames = null;
        }

        /// <summary>Esc: gallery → dossier picker → closed. Returns false when the panel was already closed.</summary>
        public bool HandleBack()
        {
            if (!IsOpen) return false;
            PlaySfx(SfxId.UiBack);
            if (galleryCard.gameObject.activeSelf) ShowChoice();
            else Close();
            return true;
        }

        private void ShowChoice()
        {
            galleryCard.gameObject.SetActive(false);
            choiceCard.gameObject.SetActive(true);
            playingFrames = null;
            SetChoiceFocus(-1);
            PopIn(choiceCard);
        }

        private void ShowGallery(Dossier which)
        {
            dossier = which;
            choiceCard.gameObject.SetActive(false);
            galleryCard.gameObject.SetActive(true);
            ApplyDossierTheme();
            BuildTabs();
            filterIndex = 0;
            ApplyFilter();
            PopIn(galleryCard);
        }

        private void PopIn(RectTransform card)
        {
            activeCard = card;
            showTime = 0f;
            panelGroup.alpha = 0f;
            card.localScale = Vector3.one * 0.94f;
        }

        private void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;

            if (activeCard != null && showTime < ShowDuration)
            {
                showTime += dt;
                float k = Mathf.Clamp01(showTime / ShowDuration);
                float eased = 1f - (1f - k) * (1f - k) * (1f - k);
                panelGroup.alpha = eased;
                activeCard.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
            }

            AnimateDetailSprite(dt);
            AnimateScrambledNames(dt);
            HandleKeyboard();
        }

        private void OnDestroy()
        {
            if (silhouetteMaterial != null) Destroy(silhouetteMaterial);
        }

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (choiceCard.gameObject.activeSelf)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) SetChoiceFocus(0);
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) SetChoiceFocus(1);
                else if (choiceFocus >= 0 && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                {
                    PlaySfx(SfxId.UiConfirm);
                    ShowGallery(choiceFocus == 0 ? Dossier.Operators : Dossier.Enemies);
                }
                return;
            }

            if (filtered.Count == 0) return;
            int step = 0;
            if (keyboard.leftArrowKey.wasPressedThisFrame) step = -1;
            else if (keyboard.rightArrowKey.wasPressedThisFrame) step = 1;
            else if (keyboard.upArrowKey.wasPressedThisFrame) step = -GridColumns;
            else if (keyboard.downArrowKey.wasPressedThisFrame) step = GridColumns;
            if (step == 0) return;

            int next = Mathf.Clamp((selectedIndex < 0 ? 0 : selectedIndex) + step, 0, filtered.Count - 1);
            if (next != selectedIndex)
            {
                PlaySfx(SfxId.UiHover);
                SelectEntry(next);
                ScrollToTile(next);
            }
        }

        // ─────────────────────────────────────────────────────
        //  Roster
        // ─────────────────────────────────────────────────────
    }
}
