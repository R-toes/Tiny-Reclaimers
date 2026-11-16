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
    /// <summary>
    /// Main menu and stage selector flow for the first playable shell.
    /// Animated panels, staggered entrances, hover/press feedback (see MenuButtonFeedback),
    /// optional UI sounds and keyboard / gamepad focus support.
    /// </summary>
    public partial class MainMenuController : MonoBehaviour
    {
        private const string FirstPlayableStageId = "STAGE_01";
        private const string SecondPlayableStageId = "STAGE_02";
        private const string ThirdPlayableStageId = "STAGE_03";
        private const string FourthPlayableStageId = "STAGE_04";
        private const string FifthPlayableStageId = "STAGE_05";
        private const string SixthPlayableStageId = "STAGE_06";
        private const string SandboxStageId = "SANDBOX";
        private const string TutorialStageId = TrashTD.Core.GameLoop.TutorialDirector.TutorialStageId;
        private const string GameplaySceneName = "GameplayTest";
        private const float EntranceDuration = 0.45f;

        private static bool IsStagePlayable(string stageId)
        {
            return stageId == FirstPlayableStageId
                || stageId == SecondPlayableStageId
                || stageId == ThirdPlayableStageId
                || stageId == FourthPlayableStageId
                || stageId == FifthPlayableStageId
                || stageId == SixthPlayableStageId
                || (stageId == SandboxStageId && TrashTD.Development.DevMode.IsEnabled)
                || stageId == TutorialStageId;
        }

        /// <summary>Online, and unlocked by progress on at least one difficulty (StageProgress; dev mode opens all).</summary>
        private static bool IsStageOpen(StageData stage)
        {
            return stage != null && IsStagePlayable(stage.stageId) && StageProgress.IsAnyDifficultyUnlocked(stage.stageId);
        }

        /// <summary>The selected stage can be played on the selected difficulty.</summary>
        private bool IsSelectedDifficultyUnlocked =>
            selectedStage == null || IsTutorialStage(selectedStage) || StageProgress.IsUnlocked(selectedStage.stageId, selectedDifficulty);

        // ── Palette ──────────────────────────────────────────
        private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color AccentBright = new Color(0.18f, 0.92f, 0.50f, 1f);
        private static readonly Color BackgroundColor = new Color(0.025f, 0.03f, 0.045f, 1f);
        private static readonly Color TextDim = new Color(0.50f, 0.58f, 0.68f, 1f);
        private static readonly Color CardColor = new Color(0.06f, 0.07f, 0.10f, 1f);
        private static readonly Color CardLockedColor = new Color(0.045f, 0.05f, 0.065f, 1f);

        // Terminal (Pip-Boy) styling shared by the stage selector and the stage dossier
        private static readonly Color TerminalPanel = new Color(0.016f, 0.042f, 0.028f, 0.97f);
        private static readonly Color TerminalHeader = new Color(0.030f, 0.090f, 0.055f, 1f);
        private static readonly Color TerminalPlate = new Color(0.010f, 0.030f, 0.020f, 0.82f);
        private static readonly Color TerminalButton = new Color(0.035f, 0.090f, 0.060f, 0.95f);
        private static readonly Color TerminalText = new Color(0.45f, 0.78f, 0.56f, 1f);
        private static readonly Color TerminalLine = new Color(0.22f, 0.91f, 0.50f, 0.45f);

        // Title screen
        private static readonly Color PrimaryLabel = new Color(0.02f, 0.10f, 0.05f, 1f);
        private static readonly Color RecRed = new Color(1f, 0.27f, 0.22f, 1f);

        // Title screen: the dim emblem (214x214) with the wide title (320x73) centred over it, above a centred stack of menu rows.
        // Both PNGs are drawn at exactly 2x so their pixels stay crisp. Sizes are canvas units (1920x1080).
        private static readonly Color SidebarYellow = new Color32(255, 208, 32, 255); // the logo's old yellow (#FFD020); still the stage "ACT" title colour
        private static readonly Color SidebarInk = new Color32(12, 12, 12, 255);      // near-black used for the secondary rows
        private const float TitleEmblemSize = 428f;                                   // emblem, behind the title
        private const float TitleLogoWidth = 960f;                                    // title lettering on top of it, at exactly 3x (320 px)
        private const float TitleLogoHeight = 219f;                                   // the still title (73 px tall, 3x)
        private const float TitleLogoAnimatedHeight = 237f;                           // the animated title: 79 px frames at 3x, the extra rows leave room for its bob
        private const float TitleLogoFrameSeconds = 0.1f;
        private static readonly Color MenuBackdropTintColor = new Color(0.45f, 0.95f, 0.60f, 1f); // what the backdrop is multiplied towards at full strength
        private const float MenuBackdropTintStrength = 0.4f;                          // 0 = no tint, 1 = the full colour above
        private const float TitleLogoDrop = 18f;                                      // the lettering sits a little below the emblem's centre, as in the artwork
        private const float TitleLogoTop = 150f;
        private const float MenuButtonWidth = 300f;
        private const float MenuAreaTop = TitleLogoTop + TitleEmblemSize + 52f;        // first row starts just under the emblem
        private const float MenuPlayHeight = 54f;
        private const float MenuRowHeight = 44f;
        private const float MenuRowGap = 8f;
        private const float MenuHintTop = MenuAreaTop + MenuPlayHeight + 3f * (MenuRowGap + MenuRowHeight) + 12f;

        /// <summary>Row 0 = PLAY, 1 = INTEL, 2 = SETTINGS, 3 = EXIT, stacked in the horizontal centre under the logo.</summary>
        private static void PlaceMenuRow(RectTransform rect, int row)
        {
            float top = MenuAreaTop;
            float height = MenuPlayHeight;
            if (row > 0)
            {
                top += MenuPlayHeight + MenuRowGap + (row - 1) * (MenuRowHeight + MenuRowGap);
                height = MenuRowHeight;
            }

            var topCenter = new Vector2(0.5f, 1f);
            Place(rect, topCenter, topCenter, new Vector2(0f, -top), new Vector2(MenuButtonWidth, height));
        }
        private const float MenuButtonLabelInset = 46f;
        private const string StageActTitle = "ACT 1";
        private const string StageZoneTitle = "NERC Development Zone";
        private const string StageSubtitleText = "Choose deployment";
        private const string StageZoneLore = "Once a thriving industrial region built around energy production, Calder Basin became the center of NERC's ambitious development program. What began as an effort to modernize the nation's energy infrastructure eventually left behind abandoned facilities, contaminated settlements, and entire districts deemed uninhabitable. Now, the NERC Development Zone is a fractured landscape of decaying industry and tightly controlled settlements, where the remnants of the corporation's legacy are impossible to escape.";
        private const string StageTickerIdle = "> AWAITING STAGE SELECTION";
        private const float StageLeftEdge = 280f;      // stage select title block x: clear of StageMapSidePanel (~244 wide at 1080 high)
        private const float StageBackButtonX = 150f;   // clear of the side panel's slanted bottom
        private const float TickerCharsPerSecond = 70f;

        private static readonly StageDifficulty[] DifficultyOrder =
        {
            StageDifficulty.Easy, StageDifficulty.Normal, StageDifficulty.Hard
        };

        [Header("Main Menu")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject stageSelectionPanel;

        [SerializeField] private Text subtitleText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button exitButton;

        [Header("Stage Selector")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button sandboxButton;
        [SerializeField] private Transform stageListRoot;
        [SerializeField] private Button stageButtonPrefab;
        [SerializeField] private GameObject stageDetailsPanel;
        [SerializeField] private Text stageDetailsTitle;
        [SerializeField] private Text stageDetailsDescription;
        [SerializeField] private Button playStageButton;
        [SerializeField] private Button stageDetailsBackButton;

        [Header("Pip-Boy Map (auto-created)")]
        [SerializeField] private Sprite stageMapBackgroundSprite;

        [Header("UI Sounds (optional)")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip backClip;
        [SerializeField] private AudioClip deniedClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;

        private static StageDifficulty pendingStageDifficulty;
        private static bool hasPendingStageDifficulty;
        private static StageData pendingStageData;
        private static bool hasPendingStageData;
        private static bool returnToStageSelector;

        private readonly List<StageData> availableStages = new List<StageData>();
        private readonly List<MenuButtonFeedback> stageCards = new List<MenuButtonFeedback>();
        private readonly List<StageMapNode> stageMapNodes = new List<StageMapNode>();
        private StageMapLayout stageMapLayout;
        private StageMapNavigator mapNavigator;
        private StageMapSidePanel stageSidePanel;
        private Slider difficultySlider;
        private RectTransform difficultySliderRoot;
        private Image difficultyFill;
        private Image difficultyKnob;
        private Image difficultyKnobHighlight;
        private Image[] difficultyNotches;
        private Text[] difficultyNotchLabels;
        private StageData selectedStage;
        private int selectedStageIndex = -1;
        private StageDifficulty selectedDifficulty = StageDifficulty.Normal;
        private Text difficultyLabel;
        private Text difficultyWavesValue;
        private Text difficultyLivesValue;
        private Text difficultyRecordLabel;
        private Text difficultyRecord; // Best stars on the selected difficulty, or what unlocks it
        private Text playStageLabel;
        private RectTransform difficultyBriefing;

        // Extra UI references created by the default layout
        private RectTransform stageDetailsModal;
        private Image modalAccentImage;
        private Color modalAccentTarget = AccentColor;
        private Image stageDetailsTitleRule; // short accent rule under the stage name, tinted like the stripe under the title bar
        private RectTransform mainVersion;

        // Title screen
        private MainMenuBackdrop menuBackdrop;
        private Text menuFeedCaption;
        private RectTransform menuFeedStatus;
        private Image menuRecDot;
        private RectTransform titleBlock;
        private Text menuHintText;
        private readonly List<MainMenuButtonView> mainMenuButtons = new List<MainMenuButtonView>();
        private RectTransform stageHeaderTitle;
        private RectTransform stageZoneTitle;
        private RectTransform stageHeaderSubtitle;
        private RectTransform stageZoneLore;
        private RectTransform stageHint;
        private RectTransform stageTitlePlate;
        private Text stageHeaderSubtitleText;
        private RectTransform stageTickerPlate;
        private Text stageTickerText;
        private string stageTickerTarget = StageTickerIdle;
        private float stageTickerTime;
        private bool stageTickerScrolls;
        private string stageTickerLoopText;
        private float stageTickerLoopPeriod;
        private float stageTickerScrollOffset;
        private float stageTickerHoldTime;

        // Stage dossier (details modal)
        private RectTransform stageDetailsColumn;
        private RectTransform stagePreviewFrame;
        private StageMapPreview stagePreview;
        private Text stagePreviewInfo;
        private Transform stageDetailsDivider;
        private MapTutorialGuide mapGuide;

        // State
        private bool menuInitialized;
        private bool stageSelectionOpen;
        private bool detailsOpen;
        private bool isLoadingStage;

        // Animation plumbing
        private readonly Dictionary<GameObject, Coroutine> panelRoutines = new Dictionary<GameObject, Coroutine>();
        private readonly List<EntranceItem> mainEntrance = new List<EntranceItem>();
        private readonly List<EntranceItem> stageEntrance = new List<EntranceItem>();
        private readonly List<EntranceItem> detailsEntrance = new List<EntranceItem>();
        private Coroutine mainEntranceRoutine;
        private Coroutine stageEntranceRoutine;
        private Coroutine detailsEntranceRoutine;
        private Coroutine overlayRoutine;

        // Title screen effects (TitleScreenFx): logo glow / sweep / glitch, boot + press-any-key intro, menu slide bars
        private TitleScreenFx titleFx;
        private bool titleIntroActive;
        private readonly List<EntranceItem> titleSceneEntrance = new List<EntranceItem>(); // logo + viewfinder text, revealed after the boot
        private readonly List<EntranceItem> menuRowsEntrance = new List<EntranceItem>();   // the menu rows + hint, revealed after the key press
        private Coroutine titleSceneRoutine;
        private Coroutine labelTypingRoutine;
        private readonly List<Mote> motes = new List<Mote>();
        private Image fadeOverlay;
        private AudioSource sfxSource;
        private AudioSettingsUI audioSettingsUI;
        private ReclaimersIntelUI intelUI;

        private sealed class EntranceItem
        {
            public RectTransform rt;
            public CanvasGroup group;
            public MenuButtonFeedback feedback;
            public Vector2 target;
            public Vector2 offset;
            public float delay;
            public float scaleFrom = 1f;
        }

        private sealed class Mote
        {
            public RectTransform rt;
            public RectTransform layer;
            public Image image;
            public float speed;
            public float sway;
            public float phase;
            public float alpha;
        }

        private sealed class MainMenuButtonView
        {
            public MenuButtonFeedback feedback;
            public CanvasGroup selectFrame;
            public Text caret;
            public RectTransform label;
            public Text labelText;
            public string fullLabel;
            public float hot;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetNavigationState()
        {
            returnToStageSelector = false;
            hasPendingStageDifficulty = false;
            pendingStageData = null;
            hasPendingStageData = false;
        }

        // ─────────────────────────────────────────────────────
        //  Lifecycle
        // ─────────────────────────────────────────────────────

        private void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            if (hoverClip == null) hoverClip = AudioManager.Instance?.GetClip(SfxId.UiHover);
            if (clickClip == null) clickClip = AudioManager.Instance?.GetClip(SfxId.UiClick);
            if (backClip == null) backClip = AudioManager.Instance?.GetClip(SfxId.UiBack);
            if (deniedClip == null) deniedClip = AudioManager.Instance?.GetClip(SfxId.UiDenied);

            if (mainMenuPanel == null || stageSelectionPanel == null || startButton == null || exitButton == null)
            {
                BuildDefaultUi();
            }

            if (subtitleText == null)
            {
                subtitleText = GameObject.Find("SubtitleText")?.GetComponent<Text>();
            }

            if (backButton == null)
            {
                backButton = GameObject.Find("BackButton")?.GetComponent<Button>();
            }

            if (stageListRoot == null)
            {
                stageListRoot = GameObject.Find("StageList")?.transform;
            }

            if (stageButtonPrefab == null)
            {
                stageButtonPrefab = GameObject.Find("StageButtonPrefab")?.GetComponent<Button>();
            }

            if (stageDetailsPanel == null)
            {
                BuildStageDetailsUi();
            }

            BuildStageButtons();
            SetUpMapNavigator();
            InitializeMenuBackdrop();
            BuildDifficultySelector();
            AttachMenuFeedback();
            Canvas menuCanvas = mainMenuPanel != null ? mainMenuPanel.GetComponentInParent<Canvas>() : null;
            if (menuCanvas != null)
            {
                intelUI = ReclaimersIntelUI.Create(
                    menuCanvas.transform,
                    mainMenuPanel.transform,
                    button => StyleMainMenuButton(button, "INTEL", false),
                    rect => PlaceMenuRow(rect, 1));
                audioSettingsUI = AudioSettingsUI.Create(
                    menuCanvas.transform,
                    mainMenuPanel.transform,
                    button => StyleMainMenuButton(button, "SETTINGS", false),
                    rect => PlaceMenuRow(rect, 2));
            }
            RegisterEntranceItems();
            UpdateDifficultyDisplay();

            // The boot + press-any-key intro plays once per session, and not when coming back from a stage
            titleIntroActive = titleFx != null && !TitleScreenFx.IntroSeen && !returnToStageSelector;
            if (!titleIntroActive) TitleScreenFx.MarkIntroSeen();

            overlayRoutine = StartCoroutine(FadeOverlayRoutine(1f, 0f, 0.4f));
            ShowMainMenu();
            menuInitialized = true;
            if (titleIntroActive) StartCoroutine(RunTitleIntro());
        }

        private void Start()
        {
            AudioManager.Instance?.PlayMainMenuMusic();

            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(ShowStageSelection);
            }

            if (exitButton != null)
            {
                exitButton.onClick.RemoveAllListeners();
                exitButton.onClick.AddListener(ExitGame);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(PlayBack);
                backButton.onClick.AddListener(ShowMainMenu);
            }

            if (playStageButton != null)
            {
                playStageButton.onClick.RemoveAllListeners();
                playStageButton.onClick.AddListener(PlaySelectedStage);
            }

            if (stageDetailsBackButton != null)
            {
                stageDetailsBackButton.onClick.RemoveAllListeners();
                stageDetailsBackButton.onClick.AddListener(PlayBack);
                stageDetailsBackButton.onClick.AddListener(CloseStageDetails);
            }

            if (returnToStageSelector)
            {
                returnToStageSelector = false;
                ShowStageSelectionInternal();
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;

            UpdateMotes(dt, time);

            // Title screen: boot line, REC light, and menu selection
            if (mainMenuPanel != null && mainMenuPanel.activeInHierarchy)
            {
                UpdateTitleScreen(dt, time);
            }

            // Stage selector terminal: blinking cursor on the subtitle, typed-out status ticker
            if (stageSelectionPanel != null && stageSelectionPanel.activeInHierarchy)
            {
                bool cursorOn = Mathf.Repeat(time, 1f) < 0.5f;
                if (stageHeaderSubtitleText != null)
                {
                    stageHeaderSubtitleText.text = StageSubtitleText + (cursorOn ? "_" : " ");
                }

                if (stageTickerText != null)
                {
                    UpdateStageTicker(dt, cursorOn);
                }
            }

            // Difficulty tint on the details modal stripe
            if (modalAccentImage != null)
            {
                float k = 1f - Mathf.Exp(-12f * dt);
                modalAccentImage.color = Color.Lerp(modalAccentImage.color, modalAccentTarget, k);
                if (stageDetailsTitleRule != null) stageDetailsTitleRule.color = modalAccentImage.color;
            }

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !isLoadingStage)
            {
                HandleBack();
            }
        }

        private void HandleBack()
        {
            if (intelUI != null && intelUI.HandleBack())
            {
                return;
            }

            if (audioSettingsUI != null && audioSettingsUI.IsOpen)
            {
                PlayBack();
                audioSettingsUI.Close();
            }
            else if (detailsOpen)
            {
                PlayBack();
                CloseStageDetails();
            }
            else if (stageSelectionOpen)
            {
                PlayBack();
                ShowMainMenu();
            }
        }

        // ─────────────────────────────────────────────────────
        //  Static navigation API (unchanged)
        // ─────────────────────────────────────────────────────

        public static void ReturnToStageSelectorOnLoad()
        {
            returnToStageSelector = true;
        }

        public static void SetPendingStageDifficulty(StageDifficulty difficulty)
        {
            pendingStageDifficulty = difficulty;
            hasPendingStageDifficulty = true;
        }

        public static StageDifficulty ConsumePendingStageDifficulty(StageDifficulty fallback)
        {
            if (!hasPendingStageDifficulty) return fallback;

            hasPendingStageDifficulty = false;
            return pendingStageDifficulty;
        }

        public static void SetPendingStage(StageData stage)
        {
            pendingStageData = stage;
            hasPendingStageData = true;
        }

        public static StageData ConsumePendingStage(StageData fallback)
        {
            if (!hasPendingStageData) return fallback;

            hasPendingStageData = false;
            StageData stage = pendingStageData;
            pendingStageData = null;
            return stage != null ? stage : fallback;
        }

        // ─────────────────────────────────────────────────────
        //  Default UI construction
        // ─────────────────────────────────────────────────────
    }

    /// <summary>
    /// Displays the sector lore card when hovering over the stage selection title block.
    /// </summary>
    public class StageHeaderLoreHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private CanvasGroup loreGroup;
        private int hoverCount;
        private float targetAlpha;

        public void Init(CanvasGroup group)
        {
            loreGroup = group;
            if (loreGroup != null)
            {
                loreGroup.alpha = 0f;
                loreGroup.blocksRaycasts = false;
            }
        }

        public void AddHover()
        {
            hoverCount++;
            targetAlpha = 1f;
            if (loreGroup != null) loreGroup.blocksRaycasts = true;
        }

        public void RemoveHover()
        {
            hoverCount = Mathf.Max(0, hoverCount - 1);
            if (hoverCount == 0)
            {
                targetAlpha = 0f;
                if (loreGroup != null) loreGroup.blocksRaycasts = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData) => AddHover();
        public void OnPointerExit(PointerEventData eventData) => RemoveHover();

        private void Update()
        {
            if (loreGroup == null) return;
            loreGroup.alpha = Mathf.MoveTowards(loreGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 7f);
        }
    }

    public class StageHeaderLoreChildTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public StageHeaderLoreHover parentHover;
        public void OnPointerEnter(PointerEventData eventData) => parentHover?.AddHover();
        public void OnPointerExit(PointerEventData eventData) => parentHover?.RemoveHover();
    }
}
