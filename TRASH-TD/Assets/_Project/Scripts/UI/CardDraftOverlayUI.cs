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
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    /// <summary>
    /// Runtime-built fullscreen card draft overlay.
    /// Left: 3 portrait-focused cards in a rarity-coloured frame (class and position header, portrait window,
    /// name plate, rarity stars). Cards are dealt face-down (a sealed "reclaimer file" back) after a shuffle
    /// animation and only flip to their front when the player clicks them.
    /// Right: a detail panel for the selected card (name, class / position / damage-type chips,
    /// stats and passive/ability text).
    /// Player selects a card, then clicks Confirm to add it to their deck.
    /// Includes a Reroll button (up to 3 rerolls per stage); a reroll flips the cards back,
    /// shuffles and deals a fresh face-down hand.
    /// Integrates with GameManager phase system: shown during CardPick phase.
    /// Visual polish: fade-in, shuffle + deal with a landing pop, flip reveal (lift and tilt, rarity flash,
    /// pixel sparks, shine sweep, stars filling in one by one), idle float / bob / shine, a lifted selected
    /// card, the picked card rising while the others drop away, hover/press feedback (MenuButtonFeedback),
    /// keyboard shortcuts and optional UI sounds.
    /// </summary>
    public partial class CardDraftOverlayUI : MonoBehaviour
    {
        // --- Colors ---
        private static readonly Color BG_OVERLAY = new Color(0.02f, 0.03f, 0.05f, 1f);
        private static readonly Color CARD_BG = new Color(0.08f, 0.10f, 0.14f, 1f);
        private static readonly Color PORTRAIT_BG = new Color(0.06f, 0.07f, 0.10f, 1f);
        private static readonly Color PANEL_BG = new Color(0.045f, 0.055f, 0.075f, 0.96f);
        private static readonly Color TILE_BG = new Color(0.07f, 0.085f, 0.115f, 1f);
        private static readonly Color CONFIRM_COLOR = new Color(0.18f, 0.55f, 0.34f, 1f);
        private static readonly Color CONFIRM_DISABLED = new Color(0.15f, 0.15f, 0.18f, 1f);
        private static readonly Color REROLL_COLOR = new Color(0.55f, 0.45f, 0.15f, 1f);
        private static readonly Color REROLL_DISABLED = new Color(0.20f, 0.18f, 0.12f, 1f);
        private static readonly Color STAR_COLOR = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color STAT_LABEL_COLOR = new Color(0.6f, 0.65f, 0.7f, 1f);
        private static readonly Color STAT_VALUE_COLOR = new Color(0.95f, 0.95f, 0.98f, 1f);
        private static readonly Color CLASS_COLOR = new Color(0.5f, 0.7f, 0.9f, 1f);
        private static readonly Color DECK_FULL_COLOR = new Color(0.8f, 0.3f, 0.3f, 1f);
        private static readonly Color SKIP_COLOR = new Color(0.5f, 0.5f, 0.55f, 1f);
        private static readonly Color ACCENT_COLOR = new Color(0.18f, 0.82f, 0.45f, 1f);
        private static readonly Color ACCENT_BRIGHT = new Color(0.30f, 0.95f, 0.58f, 1f);
        private static readonly Color MELEE_COLOR = new Color(0.95f, 0.60f, 0.25f, 1f);
        private static readonly Color RANGED_COLOR = new Color(0.35f, 0.78f, 0.92f, 1f);
        private static readonly Color BACK_BG = new Color(0.020f, 0.045f, 0.032f, 1f);
        private static readonly Color HAZARD_YELLOW = new Color(0.98f, 0.80f, 0.18f, 1f);
        private static readonly Color HAZARD_INK = new Color(0.07f, 0.06f, 0.03f, 1f);

        // Range preview tiles use the same colours as the in-game placement preview (StageBootstrapper).
        private static readonly Color RANGE_TILE_COLOR = new Color(0.15f, 1f, 0.28f, 1f);
        private static readonly Color SELF_TILE_COLOR = new Color(0.15f, 0.85f, 1f, 1f);
        private static readonly Color EMPTY_TILE_COLOR = new Color(1f, 1f, 1f, 0.22f);

        // --- Layout (reference resolution 1920x1080, positions relative to screen center) ---
        private const float CardWidth = 360f;
        private const float CardHeight = 640f;
        private const float CardSpacing = 30f;
        private const float PortraitMargin = 20f;
        private const float PortraitTop = 64f;
        private const float PortraitHeight = 376f;
        private const float PortraitPadding = 18f;
        private const float ColumnCenterX = -285f;   // card column (left side)
        private const float PanelCenterX = 650f;     // detail panel (right side)
        private const float PanelWidth = 500f;
        private const float ContentCenterY = -10f;    // cards/panel span y 230..870 from the top (1080 reference)

        // Header, in pixels from the top edge: title, then the wave counter, both centred over the card column
        private const float TitleCenterY = -124f;      // y 94..154
        private const float WaveCounterCenterY = -172f; // y 158..186

        // Draft odds slot machine, above the details panel (y 130..226; the panel starts at 230)
        private const float OddsTop = 130f;
        private const float OddsHeight = 96f;
        private const float OddsCabinetTop = 22f;      // header row above, cabinet below
        private const float OddsReelMargin = 10f;
        private const float OddsReelGap = 9f;
        private const float OddsPlateTop = 4f;         // star plate over each reel, inside the cabinet
        private const float OddsPlateHeight = 16f;
        private const float OddsWindowTop = 22f;
        private const float OddsWindowHeight = 48f;
        private const float OddsRowHeight = 30f;       // neighbours peek 9 px above and below the payline row
        private const int OddsReelRows = 4;
        private const int OddsPaylineRow = 2;          // strip slot (in rows from the top) on the payline
        private const float OddsPaylinePosition = OddsPaylineRow * OddsRowHeight;
        private const float OddsSpinDelay = 0.5f;      // lets the intro and deal start first
        private const float OddsLeverPullTime = 0.18f;
        private const float OddsLeverReturnTime = 0.35f;
        private const float OddsSpinSpeed = 1100f;     // px/s at full spin
        private const float OddsSpinUpTime = 0.15f;
        private const float OddsFirstStop = 0.6f;      // the 1★ reel starts stopping this long after the pull
        private const float OddsStopStagger = 0.3f;    // then the 2★, then the 3★

        private const string SkipCardAnimationKey = "TrashTD.SkipCardAnimation";
        private const string HideDraftOddsKey = "TrashTD.HideDraftOdds";

        // Range preview box (bottom-left of the detail panel)
        private const float RangeBoxWidth = 156f;
        private const float RangeBoxHeight = 156f;
        private const float RangeBoxPadding = 10f;
        private const float RangeTileGap = 2f;
        private const float MaxRangeTileSize = 28f;

        // --- Shuffle / flip timings (seconds) ---
        private const float StackScale = 0.88f;       // cards shrink a little while stacked
        private const float GatherTime = 0.32f;       // cards slide into a stack
        private const float GatherFlipTime = 0.24f;  // revealed cards flip back during the gather
        private const float RiffleOutTime = 0.13f;
        private const float RiffleInTime = 0.15f;
        private const float RiffleSpread = 200f;
        private const float DealTime = 0.42f;
        private const float DealStagger = 0.11f;
        private const float FlipHalfOut = 0.11f;
        private const float FlipHalfIn = 0.22f;
        private const float FlipLift = 0.1f;          // extra scale at the top of the flip
        private const float FlipTilt = 4f;            // degrees
        private const float SelectedLift = 18f;       // px the selected card rises
        private const float FaceDownBob = 5f;         // px face-down cards float
        private const float ShineDuration = 0.55f;
        private const float StarPopInterval = 0.07f;
        private const float BurstLifetime = 0.6f;
        private const float PickOutTime = 0.34f;      // picked card rises, the others drop, the overlay fades

        // Pixel-art portraits are scaled by whole numbers (when they fit) inside this area.
        private static readonly Vector2 PortraitFitArea = new Vector2(
            CardWidth - PortraitMargin * 2f - PortraitPadding * 2f,
            PortraitHeight - PortraitPadding * 2f);

        // Optional data members probed on OperatorData by name (see FindStringMember).
        // Add your own field names here if they differ.
        private static readonly string[] PassiveMemberNames =
        {
            "passiveDescription", "passiveText", "passive", "abilityDescription", "abilityText",
            "ability", "traitDescription", "trait", "skillDescription", "description", "shortDescription"
        };

        private static readonly string[] DamageTypeMemberNames =
        {
            "damageType", "attackType", "damageKind", "attackDamageType"
        };

        [Header("UI Sounds (optional)")]
        [SerializeField] private AudioClip hoverClip;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip confirmClip;
        [SerializeField] private AudioClip rerollClip;
        [SerializeField] private AudioClip shuffleClip;
        [SerializeField] private AudioClip dealClip;
        [SerializeField] private AudioClip flipClip;
        [SerializeField] private AudioClip oddsUpClip;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.7f;

        [Header("Card Flip")]
        [Tooltip("If on, clicking a face-down card flips it AND selects it. If off, the first click only reveals it and a second click selects it.")]
        [SerializeField] private bool selectOnReveal = true;

        // --- References ---
        private Canvas canvas;
        private GameObject overlayRoot;
        private CanvasGroup overlayGroup;
        private CardDraftSystem draftSystem;
        private PlayerDeck playerDeck;
        private GameManager gameManager;
        private GameplayHUDUI gameplayHudUI;
        private bool waitingForEnvironmentBriefing;

        // --- Card UI ---
        private Button minimizeButton;
        private GameObject[] cardPanels = new GameObject[3];
        private RectTransform[] cardRects = new RectTransform[3];
        private Button[] cardButtonsArr = new Button[3];
        private Image[] cardBackgrounds = new Image[3];
        private Image[] portraitBgImages = new Image[3];
        private Image[] portraitImages = new Image[3];
        private Text[] nameTexts = new Text[3];
        private Text[] rarityTexts = new Text[3];

        // --- Card polish ---
        private CanvasGroup[] cardGroups = new CanvasGroup[3];
        private MenuButtonFeedback[] cardFeedbacks = new MenuButtonFeedback[3];
        private Image[] cardAccents = new Image[3];
        private Outline[] portraitFrames = new Outline[3];
        private Image[] classBadgeBackgrounds = new Image[3];
        private Image[] classBadgeIcons = new Image[3];
        private Text[] portraitFallbackTexts = new Text[3];
        private Text[] selectHintTexts = new Text[3];

        // --- Card faces (front = the operator card, back = face-down side) ---
        private GameObject[] cardFronts = new GameObject[3];
        private GameObject[] cardBacks = new GameObject[3];
        private Image[] cardFlashes = new Image[3];
        private Color[] cardRarityColors = new Color[3];
        private Vector2[] cardSlots = new Vector2[3];
        private bool[] cardRevealed = new bool[3];
        private Coroutine[] flipRoutines = new Coroutine[3];
        private readonly List<DraftCard> pendingOffer = new List<DraftCard>();
        private bool isShuffling;
        private bool freshDraft;     // true until the first offer of a draft has been applied
        private bool offerApplied;   // false while a shuffle is still waiting to fill the cards

        // --- Card dressing: rarity-tinted parts, plates, idle and reveal effects ---
        private struct TintedPart
        {
            public Image image;
            public float alpha;
        }

        private readonly List<TintedPart>[] cardRarityParts = new List<TintedPart>[3];
        private Image[] portraitGlows = new Image[3];
        private Image[] namePlates = new Image[3];
        private Text[] classNameTexts = new Text[3];
        private Text[] positionTexts = new Text[3];
        private RectTransform[] shineRects = new RectTransform[3];
        private RectTransform[] backScanLines = new RectTransform[3];
        private Image[] backEmblems = new Image[3];
        private Text[] backHints = new Text[3];
        private float[] cardLifts = new float[3];
        private float[] shineStarts = { -10f, -10f, -10f };
        private float[] nextShineTimes = new float[3];
        private int[] cardStarCounts = new int[3];
        private Coroutine[] starPopRoutines = new Coroutine[3];
        private int pickedCardIndex = -1;
        private Sprite glowSprite;
        private Sprite stripeSprite;
        private Sprite gridSprite;

        // --- Detail panel ---
        private sealed class Chip
        {
            public Image bg;
            public Image icon;
            public Outline outline;
            public Text text;
        }

        private RectTransform detailsPanelRect;
        private Image detailAccent;
        private GameObject detailContentRoot;
        private RectTransform detailContentRect;
        private CanvasGroup detailContentGroup;
        private GameObject detailEmptyRoot;
        private Text detailNameText;
        private Text detailStarsText;
        private Chip detailClassChip;
        private Chip detailPositionChip;
        private Chip detailDamageChip;
        private Text detailRoleText;
        private Text detailHpText;
        private Text detailAtkText;
        private Text detailDefText;
        private Text detailResText;
        private Text detailBlockText;
        private Text detailIntervalText;
        private Text detailPassiveLabel;
        private Text detailPassiveText;
        private ScrollRect detailPassiveScroll;     // the ability text scrolls when it is longer than its box
        private RectTransform detailRangeGrid;
        private Sprite rangeTileSprite;
        private Coroutine detailsRoutine;

        // --- Buttons ---
        private Button confirmButton;
        private Text confirmButtonText;
        private MenuButtonFeedback confirmFeedback;
        private Button rerollButton;
        private Text rerollButtonText;
        private MenuButtonFeedback rerollFeedback;
        private Button skipButton;
        private MenuButtonFeedback skipFeedback;
        private RectTransform skipAnimationToggleRect;

        // --- Opening draft reroll notice (built on first use) ---
        private GameObject rerollNoticeRoot;
        private Image rerollNoticeCheck;
        private bool rerollNoticeDontShow;
        private bool rerollNoticeAcknowledged; // shown once per opening draft

        // --- Draft odds (slot machine: one reel per rarity, lever, next boost, "ODDS UP!" tag) ---
        private RectTransform oddsRoot;
        private RectTransform oddsCabinet;
        private readonly OddsReel[] oddsReels = new OddsReel[3];
        private Text oddsNextText;
        private RectTransform oddsToggleRect;
        private Image oddsToggleCheck;
        private RectTransform oddsUpTag;
        private Image oddsFlash;
        private RectTransform oddsLever; // stick, pivot at the bottom; flipped (scale y -1) while pulled
        private Coroutine oddsRoutine;
        private DraftOdds oddsTarget;
        private DraftOdds oddsShown;      // odds of the last draft shown this run; the next change spins from here
        private bool hasOddsShown;

        /// <summary>One slot reel: a clipped window over a strip of rows that scrolls down and wraps around.</summary>
        private sealed class OddsReel
        {
            public Text plate;
            public RectTransform window;
            public readonly Text[] rows = new Text[OddsReelRows];
            public readonly float[] rowPositions = new float[OddsReelRows]; // last strip position, to spot wraps
            public float scroll;
            public int landingRow = -1; // while stopping: the row that comes to rest on the payline
            public string landingText;
            public Color color;
            public bool zero;           // at rest on 0%: dimmed
        }
#if UNITY_EDITOR || DEBUG
        private Button developerPickButton;
        private GameObject developerPickerRoot;
        private Transform developerPickerList;
        private bool developerPickerPopulated;

        // Compiled out of release builds; in the Editor and Development Builds it also follows DevMode.
        private static bool DeveloperPickerEnabled => TrashTD.Development.DevMode.IsEnabled;
#else
        private static bool DeveloperPickerEnabled => false;
#endif

        // --- State ---
        private int selectedCardIndex = -1;
        private Text titleText;
        private Text deckCountText;
        private Text waveInfoText;
        private Image skipAnimationCheck;
        private bool overlayVisible;

        // --- Minimize (view the map while drafting) ---
        private bool minimized;
        private GameObject restoreBar;
        private bool pickConfirmed; // one card per draft; reset when the next draft opens

        /// <summary>True while the draft is minimized to the map & deck view, where operators can be deployed.</summary>
        public bool IsMinimized => minimized;

        /// <summary>True while the draft screen covers the view (open and not minimized).</summary>
        public bool IsShowing => overlayVisible && !minimized && overlayRoot != null && overlayRoot.activeSelf;

        /// <summary>The tutorial deals fixed hands: no reroll, skip or minimize (TutorialDirector).</summary>
        private static bool IsTutorialDraft => TutorialDirector.Instance != null;

        /// <summary>
        /// Shows a draft that was held back (by an environment briefing or the tutorial) once the hold is lifted.
        /// </summary>
        public void ReleaseHeldDraft()
        {
            HandleEnvironmentBriefingDismissed();
        }

        /// <summary>The card showing this operator in the current offer, for the tutorial's highlight.</summary>
        public RectTransform GetOfferedCardRect(OperatorData operatorData)
        {
            if (draftSystem == null || operatorData == null || !IsShowing) return null;
            IReadOnlyList<DraftCard> offer = draftSystem.CurrentOfferedCards;
            for (int i = 0; i < offer.Count && i < cardRects.Length; i++)
            {
                if (offer[i] != null && offer[i].operatorData == operatorData) return cardRects[i];
            }
            return null;
        }

        /// <summary>
        /// Player option (checkbox on the draft screen, saved in PlayerPrefs, off by default): skip the shuffle,
        /// deal and flip animations and deal every card face-up.
        /// </summary>
        public static bool SkipCardAnimation
        {
            get => PlayerPrefs.GetInt(SkipCardAnimationKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(SkipCardAnimationKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Player option (the "DRAFT ODDS" checkbox next to SKIP CARD ANIMATION, saved in PlayerPrefs, hidden by
        /// default): hides the whole slot machine and skips its spin.
        /// </summary>
        public static bool HideDraftOdds
        {
            get => PlayerPrefs.GetInt(HideDraftOddsKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(HideDraftOddsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // --- Animation ---
        private RectTransform[] introRects;
        private Vector2[] introTargets;
        private Vector2[] introOffsets;
        private float[] introDelays;
        private Coroutine introRoutine;
        private Coroutine dealRoutine;
        private Coroutine hideRoutine;
        private AudioSource sfxSource;

        public static void HideAllForMenuTransition()
        {
            CardDraftOverlayUI[] overlays = FindObjectsByType<CardDraftOverlayUI>(
                FindObjectsInactive.Include);

            foreach (CardDraftOverlayUI overlay in overlays)
            {
                overlay.HideDraftOverlayImmediately();
            }
        }
    }
}
