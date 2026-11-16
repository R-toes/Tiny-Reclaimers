using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TrashTD.Audio;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;
using TrashTD.Development;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// Bootstraps and visualizes a stage for quick testing in the Unity Editor.
    /// Spawns visual grid tiles, centers the camera, initializes managers,
    /// populates the draft pool, and starts the waves.
    /// </summary>
    public partial class StageBootstrapper : MonoBehaviour
    {
        private const float AcidRainDamageIntervalSeconds = 2f;
        public const int AcidRainDamagePerTick = 3;
        private const int Stage3BackgroundFrameWidth = 448;
        private const int Stage3BackgroundFrameHeight = 224;
        private const float Stage3BackgroundAnimationFrameDuration = 0.12f;
        private static readonly Color TalentRangeColor = new Color(1f, 0.55f, 0.08f, 0.45f);

        private const string DefaultSandboxMapStageId = "STAGE_01";

        /// <summary>
        /// Stage whose map (layout, artwork, spawns, exits, paths) the sandbox runs on. Static so it survives
        /// the scene reload used by Retry and by switching maps from the sandbox panel.
        /// </summary>
        public static string SandboxMapStageId { get; private set; } = DefaultSandboxMapStageId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSandboxMap() => SandboxMapStageId = DefaultSandboxMapStageId;

        private sealed class SpriteSheetAnimationState
        {
            public SpriteRenderer Renderer;
            public Sprite[] Frames;
            public float FrameDuration;
            public float FrameTimer;
            public int CurrentFrame;
        }

        [Header("Stage Config")]
        [Tooltip("The stage data asset to load and test")]
        public StageData stageData;

        [Tooltip("Difficulty to test (Easy: 10 LP, Normal: 5 LP, Hard: 1 LP)")]
        public StageDifficulty difficulty = StageDifficulty.Normal;

        [Header("Operator Pool")]
        [Tooltip("Operators available to draft")]
        public List<OperatorData> operatorPool = new List<OperatorData>();

        [Header("Tile Visuals (Sprites)")]
        public Sprite lowGroundSprite;
        public Sprite highGroundSprite;
        public Sprite blockedSprite;
        public Sprite enemyPathSprite;
        public Sprite spawnPointSprite;
        public Sprite exitPointSprite;
        public Sprite trapSprite;

        [Header("Managers (Auto-found if null)")]
        public GridManager gridManager;
        public GameManager gameManager;
        public WaveManager waveManager;
        public EnemyManager enemyManager;
        public OperatorManager operatorManager;
        public CardDraftSystem cardDraftSystem;
        public PlayerDeck playerDeck;

        private DraftCard pendingDeployCard = null;
        // Arrow schemes: the pending operator is being dragged to another tile (held mouse button)
        private bool isMovingPreview;
        private GameplayHUDUI gameplayHudUI;
        private SandboxController sandboxController;
        private readonly List<GameObject> placementPreviewVisuals = new List<GameObject>();
        private readonly List<SpriteSheetAnimationState> backgroundAnimations = new List<SpriteSheetAnimationState>();
        private Material placementPreviewMaterial;
        private float weatherDamageTimer;
        private Stage6AcidRainVisuals stage6AcidRainVisuals;
        private Vector2Int placementGridPosition;
        private OperatorFacing placementFacing = OperatorFacing.Right;
        private bool isPlacementPreviewActive;
        private bool isOperatorRangePreviewActive;
        private bool isDraggingToDeploy;
        private Vector3 sweepAnchorWorldPosition;
        private int lastQuickDeployFrame = -1;

        public bool IsDraggingToDeploy => isDraggingToDeploy;
        public OperatorFacing PlacementFacing => placementFacing;
        public Vector2Int PlacementGridPosition => placementGridPosition;
        public bool IsPlacementPreviewActive => isPlacementPreviewActive;
        public DraftCard PendingDeployCard => pendingDeployCard;

        /// <summary>
        /// Calculates the cardinal operator facing direction from a drag displacement vector.
        /// Matches Arknights PC Client mouse-tracking logic.
        /// </summary>
        public static OperatorFacing CalculateFacingFromDisplacement(Vector2 displacement, OperatorFacing fallback = OperatorFacing.Right)
        {
            if (Mathf.Approximately(displacement.x, 0f) && Mathf.Approximately(displacement.y, 0f))
            {
                return fallback;
            }

            if (Mathf.Abs(displacement.x) >= Mathf.Abs(displacement.y))
            {
                return displacement.x >= 0f ? OperatorFacing.Right : OperatorFacing.Left;
            }
            else
            {
                return displacement.y >= 0f ? OperatorFacing.Up : OperatorFacing.Down;
            }
        }

        public bool IsSandbox => stageData != null && stageData.stageId == "SANDBOX";
        public bool IsTutorial => stageData != null && stageData.stageId == TutorialDirector.TutorialStageId;

        /// <summary>
        /// Operators can be placed in the sandbox, in Preparation, and during Card Pick while the draft is
        /// minimized (the map & deck view), so deploying a deck card frees a slot for the card being picked.
        /// </summary>
        private bool IsDeploymentAllowed =>
            IsSandbox ||
            (gameManager != null &&
             (gameManager.CurrentPhase == StagePhase.Preparation ||
              (gameManager.CurrentPhase == StagePhase.CardPick && gameplayHudUI != null && gameplayHudUI.IsDraftMinimized)));

        /// <summary>
        /// Stage whose environment (combat modifiers, weather visuals and damage, animated background) is active.
        /// The sandbox uses the environment of the map it is running on.
        /// </summary>
        public string EnvironmentStageId => IsSandbox ? SandboxMapStageId : stageData != null ? stageData.stageId : null;

        /// <summary>
        /// What the active environment does (hazard, weather, ambient effects). Read from the stage data; the sandbox's
        /// runtime copy takes it from the map it runs on. Use this, not EnvironmentStageId, to decide behaviour:
        /// the id is only for naming things (music and ambience folders, the briefing's map name).
        /// </summary>
        public StageEnvironment Environment => stageData != null ? stageData.environment : StageEnvironment.None;

        private void Awake()
        {
            if (gridManager == null) gridManager = FindAnyObjectByType<GridManager>() ?? gameObject.AddComponent<GridManager>();
            if (enemyManager == null) enemyManager = FindAnyObjectByType<EnemyManager>() ?? gameObject.AddComponent<EnemyManager>();
            if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>() ?? gameObject.AddComponent<GameManager>();
            if (waveManager == null) waveManager = FindAnyObjectByType<WaveManager>() ?? gameObject.AddComponent<WaveManager>();
            if (operatorManager == null) operatorManager = FindAnyObjectByType<OperatorManager>() ?? gameObject.AddComponent<OperatorManager>();
            if (cardDraftSystem == null) cardDraftSystem = FindAnyObjectByType<CardDraftSystem>() ?? gameObject.AddComponent<CardDraftSystem>();
            if (playerDeck == null) playerDeck = FindAnyObjectByType<PlayerDeck>() ?? gameObject.AddComponent<PlayerDeck>();
            if (FindAnyObjectByType<RarityUpgradeSystem>() == null) gameObject.AddComponent<RarityUpgradeSystem>();
            if (FindAnyObjectByType<GameplayHUDUI>() == null) gameObject.AddComponent<GameplayHUDUI>();
            if (FindAnyObjectByType<CardDraftOverlayUI>() == null) gameObject.AddComponent<CardDraftOverlayUI>();
            if (FindAnyObjectByType<StageResultsUI>() == null) gameObject.AddComponent<StageResultsUI>();
        }

        private void Start()
        {
            gameplayHudUI = FindAnyObjectByType<GameplayHUDUI>();
            difficulty = MainMenuController.ConsumePendingStageDifficulty(difficulty);
            stageData = MainMenuController.ConsumePendingStage(stageData);
            if (stageData != null && stageData.stageId == "SANDBOX")
            {
                StageData mapSource = Resources.Load<StageData>("Stages/" + SandboxMapStageId);
                if (mapSource == null)
                {
                    SandboxMapStageId = DefaultSandboxMapStageId;
                    mapSource = Resources.Load<StageData>("Stages/" + DefaultSandboxMapStageId);
                }

                if (mapSource != null)
                {
                    // Work on a runtime copy so play mode doesn't overwrite the sandbox asset.
                    stageData = Instantiate(stageData);
                    stageData.environment = mapSource.environment;
                    stageData.gridWidth = mapSource.gridWidth;
                    stageData.gridHeight = mapSource.gridHeight;
                    stageData.tileLayout = mapSource.tileLayout;
                    stageData.mapVisualSprite = mapSource.mapVisualSprite;
                    stageData.backgroundVisualSprite = mapSource.backgroundVisualSprite;
                    stageData.upperBackgroundVisualSprites = mapSource.upperBackgroundVisualSprites;
                    stageData.midgroundVisualSprites = mapSource.midgroundVisualSprites;
                    stageData.foregroundVisualSprite = mapSource.foregroundVisualSprite;
                    stageData.visualTilePixelSize = mapSource.visualTilePixelSize;
                    stageData.visualTileOffset = mapSource.visualTileOffset;
                    stageData.boundsVisualSprite = mapSource.boundsVisualSprite;
                    stageData.boundsTileOffset = mapSource.boundsTileOffset;
                    stageData.boundsReplacesMapArtwork = mapSource.boundsReplacesMapArtwork;
                    stageData.backgroundTileOffset = mapSource.backgroundTileOffset;
                    stageData.backgroundFrameSize = mapSource.backgroundFrameSize;
                    stageData.spawnPoints = mapSource.spawnPoints;
                    stageData.exitPoints = mapSource.exitPoints;
                    stageData.enemyPaths = mapSource.enemyPaths;
                    stageData.trafficVehicleSprites = mapSource.trafficVehicleSprites;
                    stageData.fallingItemSprites = mapSource.fallingItemSprites;
                }
            }
            TrashTD.Combat.StageCombatModifiers.ConfigureForEnvironment(Environment);

            if (stageData == null)
            {
                Debug.LogWarning("StageBootstrapper: No StageData assigned! Please assign a StageData asset.");
                return;
            }

            // 1. Initialize Grid
            gridManager.InitializeFromStageData(stageData);

            // 2. Render visual tiles
            SpawnVisualGridTiles();
            var spriteSorter = GetComponent<GridSpriteSorter>();
            if (spriteSorter == null) spriteSorter = gameObject.AddComponent<GridSpriteSorter>();
            spriteSorter.Initialize(gridManager);

            // 3. Center Camera
            CenterCameraOnGrid();

            switch (Environment)
            {
                case StageEnvironment.Weather:
                    CreateStage1WeatherVisuals();
                    break;
                case StageEnvironment.FallingItems:
                    CreateStage2FallingItemsHazard();
                    break;
                case StageEnvironment.Sandstorm:
                    CreateStage3SandstormVisuals();
                    break;
                case StageEnvironment.FactoryHeat:
                    CreateStage4HeatHazard();
                    break;
                case StageEnvironment.Traffic:
                    CreateStage5TrafficEncounter();
                    break;
                case StageEnvironment.AcidRain:
                    CreateStage6AcidRain();
                    break;
            }

            PlayEnvironmentAudio();

            // 4. Initialize Draft System
#if UNITY_EDITOR
            if (operatorPool != null)
            {
                string[] opGuids = UnityEditor.AssetDatabase.FindAssets("t:OperatorData", new[] { "Assets/_Project/Data/Operators" });
                for (int i = 0; i < opGuids.Length; i++)
                {
                    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(opGuids[i]);
                    OperatorData opData = UnityEditor.AssetDatabase.LoadAssetAtPath<OperatorData>(assetPath);
                    if (opData != null && !operatorPool.Contains(opData))
                    {
                        operatorPool.Add(opData);
                    }
                }
            }
#endif
            if (IsSandbox && operatorManager != null)
            {
                operatorManager.SquadLimit = int.MaxValue;
            }

            if (operatorPool != null && operatorPool.Count > 0)
            {
                cardDraftSystem.SetOperatorPool(operatorPool);
                // Not subscribed to cardDraftSystem.OnCardSelected: confirming a draft pick raises it too, and the picked
                // card must not start placement by itself. Clicking a deck card selects it through playerDeck below.
            }
            if (playerDeck != null)
            {
                playerDeck.OnCardSelectedForDeployment += HandleCardSelectedForDeployment;
                playerDeck.OnDeckChanged += HandleDeckChanged;
            }
            if (operatorManager != null)
            {
                operatorManager.OnOperatorSelected += HandleOperatorSelected;
                operatorManager.OnOperatorRetreated += HandleOperatorRetreated;
            }
            cardDraftSystem.ResetForNewStage();

            // 5. Initialize Wave Manager
            waveManager.Initialize(stageData, difficulty);

            // The tutorial has to hold the first draft, so its director exists before the stage starts
            if (IsTutorial)
            {
                gameObject.AddComponent<TutorialDirector>().Initialize(this, stageData, gridManager);
            }

            // 6. Start Stage (enters CardPick phase, which shows the CardDraftOverlayUI)
            gameManager.StartStage(stageData, difficulty);

            if (stageData.stageId == "SANDBOX")
            {
                operatorManager.SquadLimit = int.MaxValue;
                FindAnyObjectByType<CardDraftOverlayUI>()?.HideForSandbox();
                sandboxController = gameObject.AddComponent<SandboxController>();
                sandboxController.Initialize(this);
            }
        }

        /// <summary>
        /// Restarts the sandbox on another stage's map. Reloading the scene rebuilds the grid, paths and artwork
        /// from scratch, the same way Retry does; the deck, queue and deployed operators start over.
        /// </summary>
        public void SwitchSandboxMap(string mapStageId)
        {
            if (!IsSandbox || string.IsNullOrEmpty(mapStageId)) return;

            SandboxMapStageId = mapStageId;
            MainMenuController.SetPendingStage(stageData);
            MainMenuController.SetPendingStageDifficulty(difficulty);
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }
    }
}
