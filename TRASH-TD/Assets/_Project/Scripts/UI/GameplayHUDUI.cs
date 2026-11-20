using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Core.Grid;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    /// <summary>
    /// Runtime-built in-game HUD for the stage view.
    /// Integrates with the phase loop: CardPick → Preparation → WaveActive.
    /// The creature bar shows operators in the player's deck (not draft offers).
    /// Visual polish: animated intro, phase banners, wave progress, life-loss flash,
    /// hover/press feedback (MenuButtonFeedback) and optional UI sounds.
    /// </summary>
    public partial class GameplayHUDUI : MonoBehaviour
    {
        private const int DeckSlotCount = 8;
        private static Sprite cooldownClockSprite;
        private static Sprite trashIconSprite;
        private static Sprite pauseIconSprite;
        private static Sprite exitIconSprite;
        private static readonly Dictionary<OperatorClass, Sprite> ClassIconSprites = new Dictionary<OperatorClass, Sprite>();

        public enum ButtonContainerType
        {
            Primary,
            Secondary,
            Dark,
            Danger,
            Warning,
            IconDark,
            IconDanger,
            IconPrimary,
            IconSecondary
        }

        private static readonly Dictionary<ButtonContainerType, Sprite> ButtonContainerSprites = new Dictionary<ButtonContainerType, Sprite>();

        public enum PanelContainerType
        {
            Dark,
            Metal,
            Glass,
            Modal,
            Rust,
            Teal,
            Tooltip
        }

        private static readonly Dictionary<PanelContainerType, Sprite> PanelContainerSprites = new Dictionary<PanelContainerType, Sprite>();

        // ── Palette ──────────────────────────────────────────
        private static readonly Color AccentColor = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color DarkButton = new Color(0.10f, 0.12f, 0.16f, 1f);
        private static readonly Color DraftColor = new Color(1f, 0.78f, 0.25f, 1f);
        private static readonly Color PrepColor = new Color(0.5f, 0.8f, 1f, 1f);
        private static readonly Color WaveColor = new Color(1f, 0.45f, 0.35f, 1f);
        private static readonly Color LifeColor = new Color(1f, 0.35f, 0.35f, 1f);
        private static readonly Color TextDim = new Color(0.50f, 0.58f, 0.68f, 1f);
        private static readonly Color WaitingColor = new Color(0.3f, 0.3f, 0.35f, 1f);
        private static readonly Color ReadyColor = new Color(0.18f, 0.55f, 0.34f, 1f);

        [Header("UI Sounds (optional)")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip waveStartClip;
        [SerializeField] private AudioClip lifeLostClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;

        private Sprite[] deckBgSprites;
        private Sprite[] topBarBgSprites;
        // Bottom Right Panel BG animations (1x, 2x, 3x speed animations, with legacy fallback).
        private Sprite[] bottomRightBg1xSprites;
        private Sprite[] bottomRightBg2xSprites;
        private Sprite[] bottomRightBg3xSprites;
        private Sprite[] bottomRightBgSprites;

        private Canvas canvas;
        private GameObject pausePanel;
        private GameObject stageInfoPanel;
        private Text phaseHeaderText;
        private Image phaseLed;
        private Text waveLabelText;
        private Text waveText;
        private Text enemyLabelText;
        private Text enemyText;
        private Text enemyIncomingText;
        private Text lpText;
        private readonly List<Image> topScreenGlows = new List<Image>();
        private GameObject nextWavePreview;
        private Button nextWavePreviewButton;
        private readonly List<GameObject> nextWavePreviewItems = new List<GameObject>();
        // The next wave's enemy types (spawn-list order) and counts, kept so a click can re-lay the rows
        private readonly List<EnemyData> nextWaveOrder = new List<EnemyData>();
        private readonly Dictionary<EnemyData, int> nextWaveCounts = new Dictionary<EnemyData, int>();
        private string nextWaveTitle;
        private Color nextWaveTitleColor;
        private bool nextWavePreviewExpanded;
        private int nextWavePreviewNumber = -1;
        private Text phaseText;
        private Text squadCountText;
        private GameObject selectedOperatorLabelRoot;
        private GameObject environmentBriefingPanel;
        private RectTransform environmentBriefingCardRect;
        private Text environmentBriefingTitle;
        private Text environmentBriefingDescription;
        private GameObject stage2ThrownItemsRoot;
        private Image stage2GlassIcon;
        private Image stage2MetalIcon;
        private Image stage2ToolsIcon;
        private GameObject stage5TrafficRoot;
        private Image stage5VehicleLeftIcon;
        private Image stage5VehicleCenterIcon;
        private Image stage5VehicleRightIcon;
        private Text stage5TrafficBodyText;
        private bool environmentBriefingDismissed;
        private Text selectedOperatorNameText;
        private Text selectedOperatorHealthText;
        private Text selectedOperatorAtkText;
        private Text selectedOperatorRarityText;
        private Image selectedOperatorHealthFill;
        private RectTransform selectedOperatorHealthFillRect;
        private GameObject selectedOperatorUpgradeBadge;
        private UpgradeArrowGraphic selectedOperatorUpgradeBadgeGraphic;
        private Button selectedOperatorShowMoreButton;
        private GameObject selectedOperatorDetailsPanel;
        private Image selectedOperatorDetailsAccent;
        private Image selectedOperatorDetailsPortrait;
        private Image selectedOperatorDetailsClassIcon;
        private Text selectedOperatorDetailsName;
        private Text[] selectedOperatorDetailsChipLabels;
        private Image[] selectedOperatorDetailsChipBackgrounds;
        private Outline[] selectedOperatorDetailsChipOutlines;
        private Text selectedOperatorDetailsRarity;
        private Text[] selectedOperatorDetailsStatValues;
        private Text selectedOperatorDetailsRoles;
        private Text selectedOperatorDetailsSkillHeading;
        private Text selectedOperatorDetailsSkill;
        private ScrollRect selectedOperatorDetailsSkillScroll;   // the ability text scrolls when it is longer than its box
        private string detailsSkillShown;                         // so a live refresh does not jump the scroll back to the top
        private Text[] selectedOperatorDetailsStatDeltas;         // "+23" / "-5" / "+25%" next to each stat, against the drafted rank
        private RectTransform selectedOperatorEffectsRoot;       // active stat effects, a column of cards beside the panel
        private Text selectedOperatorEffectsHeader;
        private readonly List<EffectCardView> effectCards = new List<EffectCardView>();
        private readonly List<StatEffect> statEffectsBuffer = new List<StatEffect>();
        private float detailsLiveRefreshAt;

        private sealed class EffectCardView
        {
            public GameObject root;
            public RectTransform rect;
            public Image accent;        // the buff / debuff coloured bar down the left edge
            public Image icon;          // what the effect touches (sword, shield, heart...)
            public CanvasGroup details; // source + description, shown while the badge is expanded
            public Text source;
            public Text description;
            public float expand;        // 0 = square badge, 1 = fully widened
            public bool hovered;
        }
        private OperatorBase selectedOperatorDetailsTarget;
        // A clicked deck card shows the same info tab as a field operator, above its slot (-1 = none)
        private int deckInfoSlot = -1;
        private DraftCard deckInfoCard;
        private readonly Vector3[] deckInfoSlotCorners = new Vector3[4];
        private GameObject placementControlsRoot;
        private Button retreatOperatorButton;
        private Button placementConfirmButton;
        private Button placementCancelButton;
        private RectTransform retreatIconRect; // resized with the retreat button when the player changes the UI size
        private Button touchCancelButton; // phone only: always-visible CANCEL while a card is being placed
        // Facing arrows around the previewed tile, in OperatorFacing order (see CreatePlacementControls)
        private readonly Dictionary<OperatorFacing, Button> placementFacingButtons = new Dictionary<OperatorFacing, Button>();
        private readonly Dictionary<OperatorFacing, MenuButtonFeedback> placementFacingFeedbacks = new Dictionary<OperatorFacing, MenuButtonFeedback>();
        private bool isDeckCardDragging;
        private Button startWaveButton;
        private Text startWaveButtonText;
        private Button[] speedButtons;
        private MenuButtonFeedback[] speedFeedbacks;
        private RectTransform trashDropZone;
        private GameObject discardConfirmationPanel;
        private DraftCard pendingDiscardCard;
        private int pendingDiscardSlot = -1;
        private Button[] deckButtons;
        private DeckMergeAnimator deckMergeAnimator;
        private Text[] deckButtonLabels;
        private Image[] deckButtonImages;
        private Image[] deckPortraitImages;
        private Image[] deckClassBadgeImages;
        private Image[] deckClassIconImages;
        private GameObject[] deckClassBadges;
        private GameObject[] deckCooldownBadges;
        private Text[] deckCooldownLabels;
        private GameObject[] deckHeatBadges;
        private Text[] deckHeatLabels;
        private GameObject[] deckUpgradeBadges;
        private UpgradeArrowGraphic[] deckUpgradeBadgeLabels;
        // Framiliar Copy Menu
        private GameObject framiliarCopyMenuRoot;
        private Text framiliarSubtitleText;
        private Text framiliarEmptyMessageText;
        private readonly List<DraftCard> activeFramiliarMatchingCards = new List<DraftCard>();
        private Button[] framiliarCopyButtons;
        private Text[] framiliarCopyLabels;
        private Image[] framiliarCopyPortraits;
        private Image[] framiliarCopyClassBadgeImages;
        private Image[] framiliarCopyClassIconImages;
        private GameObject[] framiliarCopyClassBadges;
        private MenuButtonFeedback[] framiliarCopyFeedbacks;
        private TrashTD.Operators.FramiliarOperator activeFramiliarOperator;
        private TrashTD.Operators.FramiliarOperator lastFramiliarMenuOperator;
        private int lastFramiliarMenuRarity = -1;
        private GridCell lastFramiliarMenuCell;
        private int lastFramiliarMenuDeckCount = -1;
        public bool IsFramiliarCopyMenuOpen => framiliarCopyMenuRoot != null && framiliarCopyMenuRoot.activeSelf;
        private CardDraftSystem draftSystem;
        private WaveManager waveManager;
        private EnemyManager enemyManager;
        private GameManager gameManager;
        private PlayerDeck playerDeck;
        private GridManager gridManager;
        private OperatorManager operatorManager;
        private StageBootstrapper stageBootstrapper;
        private TrashTD.Development.SandboxController sandboxController;

        /// <summary>Running the QA sandbox: no lives, no draft, no next-wave preview; its panel drives the waves.</summary>
        private bool IsSandbox => sandboxController != null;

        private int selectedDeckSlot = -1;
        public bool IsEnvironmentBriefingActive => environmentBriefingPanel != null && environmentBriefingPanel.activeSelf;
        public bool ShouldBlockDraftOverlay
        {
            get
            {
                // The tutorial deals each hand when Coalesce is ready (CardDraftOverlayUI.ReleaseHeldDraft)
                if (TutorialDirector.Instance != null) return TutorialDirector.Instance.HoldsDraft;
                if (environmentBriefingDismissed || gameManager == null || gameManager.CurrentStage == null) return false;
                StageEnvironment environment = stageBootstrapper != null ? stageBootstrapper.Environment : gameManager.CurrentStage.environment;
                return StageData.HasGameplayHazard(environment);
            }
        }

        public event System.Action EnvironmentBriefingDismissed;

        // Feedback components
        private MenuButtonFeedback[] deckFeedbacks;
        private MenuButtonFeedback startWaveFeedback;
        private MenuButtonFeedback retreatFeedback;
        private MenuButtonFeedback confirmFeedback;

        // Layout references used by animations
        private RectTransform topBarRect;
        private RectTransform deckBarRect;
        private RectTransform waveProgressFillRect;
        private Image waveProgressFill;
        private RectTransform livesTubeRect;
        private Image livesTubeFrame;
        private RectTransform livesLiquidRect;
        private GameObject livesSurface;
        private Image dangerFlashImage;
        private SurveillanceOverlayUI surveillanceOverlay;
        private Image topBarBgAnimImage;
        private Image deckBgAnimImage;
        private float topBarBgAnimTime;
        private int topBarBgAnimFrame;
        private float deckBgAnimTime;
        private int deckBgAnimFrame;
        // Speed panel animation and crossfade: each speed (1x, 2x, 3x) has its own animated sprite loop.
        // When speed changes, "to" fades in over "from" for a smooth transition.
        private Image bottomRightBgFromImage;
        private Image bottomRightBgToImage;
        private int bottomRightBgFromSpeed = 0;
        private int bottomRightBgToSpeed = 0;
        private float bottomRightBgFade = 1f;
        private float bottomRightBgAnimTime;
        private int bottomRightBgAnimFrame = -1;
        private int currentSpeedSelectionIndex = -1;
        private CanvasGroup phaseBannerGroup;
        private RectTransform phaseBannerRect;
        private RectTransform phaseBannerContent;
        private Text phaseBannerTitle;
        private Text phaseBannerTitleGhost;
        private Text phaseBannerSubtitle;
        private RawImage[] phaseBannerStripes;
        private Image[] phaseBannerLights;
        private RectTransform phaseBannerIcons;
        private readonly List<GameObject> phaseBannerIconItems = new List<GameObject>();
        private Color phaseBannerColor = Color.white;
        private bool phaseBannerAlarm;
        private bool phaseBannerGlitch;
        private RectTransform cooldownToastRect;
        private Vector2 cooldownToastShownPosition;
        private CanvasGroup cooldownToastGroup;
        private Text cooldownToastTitle;
        private Text cooldownToastMessage;
        private Coroutine cooldownToastRoutine;
        private CanvasGroup pauseGroup;
        private RectTransform pauseCard;
        private AudioSettingsUI audioSettingsUI;
        private ReclaimersIntelUI intelUI;
        private CardDraftOverlayUI draftOverlayUI;
        private int lastEscapeHandledFrame = -1;
        private Button previewPathButton;
        private RectTransform bottomRightPanelRect;
        private Text previewPathButtonText;
        private MenuButtonFeedback previewPathFeedback;
        private Color previewPathRestColor = Color.white;
        private bool previewPathLocked;
        private GameplayPathPreviewOverlay pathPreviewOverlay;

        // Animation state
        private AudioSource sfxSource;
        private Coroutine bannerRoutine;
        private Coroutine pauseRoutine;
        private Color phaseColorTarget = PrepColor;
        private StagePhase lastPhase;
        private bool hasLastPhase;
        private float waveProgressDisplayed;
        private int lastSquadCount = -1;
        private int lastLives = -1;

        // The text last written to each top-bar readout, so a readout is rebuilt only when its numbers change
        private int shownLivesCurrent = int.MinValue;
        private int shownLivesTotal = int.MinValue;
        private int shownSquadKey = int.MinValue;
        private long shownWaveKey = long.MinValue;
        private long shownHostileKey = long.MinValue;
        private int hostileNextWaveIndex = int.MinValue;
        private int hostileNextWaveSize;
        private float livesTarget = 1f;
        private float livesDisplayed = -1f; // < 0 until the first lives update, which snaps instead of draining
        private float dangerFlashTime = -1f;
        private float lifeFlashTime = -1f;
        private bool selectedLabelShown;
        private float labelPopTime = -1f;
        private bool snapHealthFill;
        private float selectedHealthTarget;
        private float selectedHealthDisplayed;
        private float placementPopTime = -1f;

        private sealed class PunchState
        {
            public Transform target;
            public float time;
        }

        private readonly List<PunchState> punches = new List<PunchState>();

        private void Awake()
        {
            draftSystem = FindAnyObjectByType<CardDraftSystem>();
            draftOverlayUI = FindAnyObjectByType<CardDraftOverlayUI>();
            waveManager = FindAnyObjectByType<WaveManager>();
            enemyManager = FindAnyObjectByType<EnemyManager>();
            gameManager = FindAnyObjectByType<GameManager>();
            playerDeck = FindAnyObjectByType<PlayerDeck>();
            gridManager = FindAnyObjectByType<GridManager>();
            operatorManager = FindAnyObjectByType<OperatorManager>();
            stageBootstrapper = FindAnyObjectByType<StageBootstrapper>();
            sandboxController = FindAnyObjectByType<TrashTD.Development.SandboxController>();

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            if (hoverClip == null) hoverClip = AudioManager.Instance?.GetClip(SfxId.UiHover);
            if (clickClip == null) clickClip = AudioManager.Instance?.GetClip(SfxId.UiClick);
            if (waveStartClip == null) waveStartClip = AudioManager.Instance?.GetClip(SfxId.GameplayWaveStart);
            if (lifeLostClip == null) lifeLostClip = AudioManager.Instance?.GetClip(SfxId.GameplayLifeLost);

            EnsureEventSystem();
            topBarBgSprites = Resources.LoadAll<Sprite>("UI/Top Panel BG");
            // Load the Deck BG sprite sheet frames from Resources at runtime.
            // The asset lives at Assets/Resources/UI/Deck BG.png (sprite mode: Multiple).
            deckBgSprites = Resources.LoadAll<Sprite>("UI/Deck BG");
            bottomRightBg1xSprites = LoadAndSortSprites("UI/Bottom Right Panel BG 1x animation");
            bottomRightBg2xSprites = LoadAndSortSprites("UI/Bottom Right Panel BG 2x animation");
            bottomRightBg3xSprites = LoadAndSortSprites("UI/Bottom Right Panel BG 3x animation");
            bottomRightBgSprites = LoadAndSortSprites("UI/Bottom Right Panel BG");
            BuildHud();
            if (canvas != null && pausePanel != null)
            {
                Transform pauseCard = pausePanel.transform.Find("PauseCard");
                if (pauseCard != null)
                {
                    audioSettingsUI = AudioSettingsUI.Create(
                        canvas.transform,
                        pauseCard,
                        button =>
                        {
                            StyleButton(button, ButtonContainerType.Secondary, new Color(0.55f, 0.75f, 1f, 1f), 1.04f);
                            ApplyPauseButtonLook(button, null, "icon_settings");
                        },
                        // Square gear at the right end of row 2, next to Retry
                        rect => SetPosition(rect, PausePx(PauseSettingsX, PauseRow2Px), new Vector2(0.5f, 1f), new Vector2(PauseButtonHeight, PauseButtonHeight), new Vector2(0.5f, 0.5f)));
                    intelUI = ReclaimersIntelUI.Create(
                        canvas.transform,
                        pauseCard,
                        button =>
                        {
                            StyleButton(button, ButtonContainerType.Secondary, new Color(0.4f, 1f, 0.65f, 1f), 1.04f);
                            ApplyPauseButtonLook(button, "INTEL", "icon_info");
                        },
                        // Full-width row 3, on the card's extra indicator row
                        rect => SetPosition(rect, PausePx(0f, PauseRow3Px), new Vector2(0.5f, 1f), new Vector2(PauseRowWidth, PauseButtonHeight), new Vector2(0.5f, 0.5f)));
                }
            }
        }

        private void Start()
        {
            // StageBootstrapper adds this HUD before the draft overlay, so Awake can't find it yet
            if (draftOverlayUI == null) draftOverlayUI = FindAnyObjectByType<CardDraftOverlayUI>();

            if (waveManager != null)
            {
                waveManager.OnWaveStarted += HandleWaveStarted;
            }

            if (enemyManager != null)
            {
                enemyManager.OnEnemySpawned += HandleEnemyCountChanged;
                enemyManager.OnEnemyDied += HandleEnemyCountChanged;
                enemyManager.OnEnemyReachedExit += HandleEnemyCountChanged;
                enemyManager.OnEnemyReachedExit += HandleEnemyBreachFx;
            }

            if (gameManager != null)
            {
                gameManager.OnLifePointsChanged += HandleLPChanged;
                gameManager.OnPhaseChanged += HandlePhaseChanged;
            }

            if (playerDeck != null)
            {
                playerDeck.OnDeckChanged += HandleDeckChanged;
                playerDeck.OnCardsMerged += HandleCardsMerged;
            }

            RefreshCounters();
            RefreshDeckSlots();
            UpdatePhaseUI(false);

            StartCoroutine(IntroRoutine());
            StartCoroutine(ShowEnvironmentBriefingWhenReady());
        }

        private void Update()
        {
            RefreshPreviewPathLock();
            HandleKeyboardInput();
            if (gameManager != null)
            {
                int currentSpeed = Mathf.Clamp(Mathf.RoundToInt(gameManager.GameSpeed) - 1, 0, 2);
                if (currentSpeed != currentSpeedSelectionIndex)
                {
                    UpdateSpeedSelection(gameManager.GameSpeed);
                }
            }
            RefreshCounters();
            RefreshSelectedOperatorInfo();
            RefreshTouchCancelButton();
            AnimateEffectBadges();
            UpdateAnimations(Time.unscaledDeltaTime, Time.unscaledTime);
        }

        private void OnDestroy()
        {
            if (waveManager != null)
            {
                waveManager.OnWaveStarted -= HandleWaveStarted;
            }

            if (enemyManager != null)
            {
                enemyManager.OnEnemySpawned -= HandleEnemyCountChanged;
                enemyManager.OnEnemyDied -= HandleEnemyCountChanged;
                enemyManager.OnEnemyReachedExit -= HandleEnemyCountChanged;
                enemyManager.OnEnemyReachedExit -= HandleEnemyBreachFx;
            }

            if (gameManager != null)
            {
                gameManager.OnLifePointsChanged -= HandleLPChanged;
                gameManager.OnPhaseChanged -= HandlePhaseChanged;
            }

            if (playerDeck != null)
            {
                playerDeck.OnDeckChanged -= HandleDeckChanged;
                playerDeck.OnCardsMerged -= HandleCardsMerged;
            }
        }
    }

    /// <summary>Reports the pointer entering or leaving a UI element (the stat-effect badges widen on hover).</summary>
    public sealed class EffectBadgeHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public System.Action<bool> Changed;

        public void OnPointerEnter(PointerEventData eventData) => Changed?.Invoke(true);
        public void OnPointerExit(PointerEventData eventData) => Changed?.Invoke(false);

        private void OnDisable() => Changed?.Invoke(false);
    }
}
