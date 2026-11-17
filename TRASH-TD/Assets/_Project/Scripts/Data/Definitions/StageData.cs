using UnityEngine;
using System;

namespace TrashTD.Data
{
    /// <summary>
    /// Defines a single wave of enemies within a stage (GDD 1.5 wave format).
    /// </summary>
    [Serializable]
    public class WaveEntry
    {
        [Tooltip("Reference to the enemy data for this entry")]
        public EnemyData enemyData;

        [Tooltip("Number of enemies of this type to spawn")]
        public int count = 1;

        [Tooltip("Seconds between each spawn of this enemy type")]
        public float spawnInterval = 1.0f;

        [Tooltip("Delay before this entry starts spawning (relative to wave start)")]
        public float startDelay = 0f;

        [Tooltip("Which spawn point index to use (maps to StageData.spawnPoints)")]
        public int spawnPointIndex = 0;
    }

    /// <summary>
    /// A complete wave definition containing one or more enemy entries.
    /// </summary>
    [Serializable]
    public class WaveData
    {
        [Tooltip("Display name for this wave (e.g., 'Wave 1', 'Final Wave')")]
        public string waveName = "Wave";

        [Tooltip("Delay in seconds before this wave starts (after previous wave clears or timer)")]
        public float preWaveDelay = 5.0f;

        [Tooltip("All enemy entries in this wave")]
        public WaveEntry[] entries;
    }

    /// <summary>
    /// What a map's environment does. Set on each StageData; the bootstrapper, the combat modifiers, the HUD briefing and the
    /// sandbox all read this instead of comparing stage ids. The sandbox takes the environment of the map it runs on.
    /// </summary>
    public enum StageEnvironment
    {
        /// <summary>No environment (the tutorial, the sandbox before it picks a map).</summary>
        None = 0,
        /// <summary>Visual only: mist and rain with no gameplay effect (Stage 1).</summary>
        Weather = 1,
        /// <summary>Scrap thrown across the battlefield (Stage 2).</summary>
        FallingItems = 2,
        /// <summary>Sandstorm: faster enemy attacks, operator attacks can miss (Stage 3).</summary>
        Sandstorm = 3,
        /// <summary>Factory heat builds up on operators until they ignite (Stage 4).</summary>
        FactoryHeat = 4,
        /// <summary>Cars cross the highway lanes (Stage 5).</summary>
        Traffic = 5,
        /// <summary>Acid rain damages operators and enemies (Stage 6).</summary>
        AcidRain = 6
    }

    /// <summary>
    /// ScriptableObject defining a complete stage (GDD 1.11 template).
    /// Each stage has 3 difficulty variants (Easy/Normal/Hard) with separate life points.
    /// </summary>
    [CreateAssetMenu(fileName = "New Stage", menuName = "TRASH TD/Stage Data")]
    public class StageData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique stage identifier")]
        public string stageId;

        [Tooltip("A placeholder for a stage that is not built yet: it shows on the stage map as a locked marker and can never be opened or played, even in dev mode.")]
        public bool comingSoon;

        [Tooltip("What this map's environment does: its hazard and ambient effects. The sandbox copies it from the map it runs on.")]
        public StageEnvironment environment;

        /// <summary>The map has cars crossing it (its previews show the traffic lanes).</summary>
        public bool HasTraffic => environment == StageEnvironment.Traffic;

        /// <summary>The environment is more than scenery: it asks the player to play differently (shown in the briefing).</summary>
        public static bool HasGameplayHazard(StageEnvironment environment)
        {
            return environment != StageEnvironment.None && environment != StageEnvironment.Weather;
        }

        [Tooltip("Display name for the map")]
        public string mapName;

        [Tooltip("Short description shown in the stage selector")]
        public string shortDescription;

        [Header("Map Layout")]
        [Tooltip("Width of the grid in tiles")]
        public int gridWidth = 10;

        [Tooltip("Height of the grid in tiles")]
        public int gridHeight = 8;

        [Tooltip("Grid layout — flattened row-major array of TileType. Length must equal gridWidth * gridHeight.")]
        public TileType[] tileLayout;

        [Tooltip("Optional full-map artwork displayed behind the logical grid")]
        public Sprite mapVisualSprite;

        [Tooltip("Optional artwork override used only by the loading screen")]
        public Sprite loadingScreenMapSprite;

        [Tooltip("Optional background artwork rendered behind the main map artwork")]
        public Sprite backgroundVisualSprite;

        [Tooltip("Optional upper-layer artwork rendered above the main map artwork but below units")]
        public Sprite[] upperBackgroundVisualSprites;

        [Header("Visual Artwork Alignment")]
        [Tooltip("Pixel size of one tile in the map visual sprite (usually 32 px)")]
        public int visualTilePixelSize = 32;

        [Tooltip("Offset in tiles for the visual sprite relative to grid bottom-left origin (x, y). Default (0,0) aligns sprite bottom-left to cell (0,0)")]
        public Vector2 visualTileOffset = Vector2.zero;

        [Tooltip("Optional extended artwork: the map plus the scenery around it, outside the playable field (it normally " +
                 "contains the map art in its middle). It is drawn behind the map artwork, the gameplay camera can't leave it, " +
                 "and the stage's weather covers it. Keep it centred on the map so the camera range stays even.")]
        public Sprite boundsVisualSprite;

        [Tooltip("Offset in tiles of the bounds sprite's bottom-left corner from the grid's bottom-left corner " +
                 "(e.g. (-3, -3) for 3 tiles of scenery to the left of and below the map)")]
        public Vector2 boundsTileOffset = Vector2.zero;

        [Tooltip("The bounds art is the extended ground layer with see-through windows (Stage 3), so it is drawn in the map " +
                 "artwork's place, above the background, instead of behind everything. The map artwork is not drawn.")]
        public bool boundsReplacesMapArtwork;

        [Tooltip("Offset in tiles of the background artwork's bottom-left corner from the grid's bottom-left corner " +
                 "(added to the visual offset). Use it when the background is the size of the bounds art.")]
        public Vector2 backgroundTileOffset = Vector2.zero;

        [Tooltip("Size in pixels of one frame of an animated background sheet (Stage 3: 640 x 416)")]
        public Vector2Int backgroundFrameSize = new Vector2Int(448, 224);

        [Tooltip("Optional midground/obstacle overlays rendered above ground units (operators and walking enemies) but below flying units")]
        public Sprite[] midgroundVisualSprites;

        [Tooltip("Optional foreground artwork displayed in front of units (e.g. fence/trees overlay)")]
        public Sprite foregroundVisualSprite;

        [Tooltip("Optional vehicle sprites for traffic encounters (e.g. Stage 5 boulevard traffic)")]
        public Sprite[] trafficVehicleSprites;

        [Tooltip("Optional falling item sprites for Stage 2 thrown-items mechanic")]
        public Sprite[] fallingItemSprites;

        [Tooltip("Spawn point positions on the grid (enemy entry points)")]
        public Vector2Int[] spawnPoints;

        [Tooltip("Exit point positions on the grid (objective — enemies leak here)")]
        public Vector2Int[] exitPoints;

        [Header("Path Data")]
        [Tooltip("Waypoint paths for enemies. Each path is an array of grid positions from spawn to exit.")]
        public PathData[] enemyPaths;

        [Header("Difficulty — Lives (3 Easy/Normal, 1 Hard)")]
        public int lifePointsEasy = 3;
        public int lifePointsNormal = 3;
        public int lifePointsHard = 1;

        [Header("Squad")]
        [Tooltip("Maximum number of operators that can be deployed simultaneously")]
        public int squadSizeLimit = 8;

        [Header("Waves")]
        [Tooltip("Extra basic enemies on wave 1 compared with the first stage (Stage 1 = 0, Stage 2 = 1, ...). " +
                 "Scales every wave of this stage when rebalancing, so each stage is a bit harder than the last.")]
        [Min(0)] public int extraStartEnemies = 0;

        [Tooltip("Wave list for Easy difficulty")]
        public WaveData[] wavesEasy;

        [Tooltip("Wave list for Normal difficulty")]
        public WaveData[] wavesNormal;

        [Tooltip("Wave list for Hard difficulty")]
        public WaveData[] wavesHard;

        [Header("Optional")]
        [Tooltip("Stage time limit in seconds (0 = no limit)")]
        public float timeLimit = 0f;

        [Tooltip("3-star rating conditions description")]
        public string threeStarCondition = "Clear with no life points lost";

        // TODO: narrative — awaiting design input (setting, factions, lore)
        // TODO: art & audio direction — awaiting design input (using placeholder visuals)
        [Tooltip("Tilemap prefab to instantiate for this stage's visuals")]
        public GameObject mapPrefab;

        /// <summary>
        /// Get life points for a specific difficulty.
        /// </summary>
        public int GetLifePoints(StageDifficulty difficulty)
        {
            return difficulty switch
            {
                StageDifficulty.Easy => lifePointsEasy,
                StageDifficulty.Normal => lifePointsNormal,
                StageDifficulty.Hard => lifePointsHard,
                _ => lifePointsNormal
            };
        }

        /// <summary>
        /// Get the wave list for a specific difficulty.
        /// </summary>
        public WaveData[] GetWaves(StageDifficulty difficulty)
        {
            return difficulty switch
            {
                StageDifficulty.Easy => wavesEasy,
                StageDifficulty.Normal => wavesNormal,
                StageDifficulty.Hard => wavesHard,
                _ => wavesNormal
            };
        }

        /// <summary>
        /// Get the TileType at a specific grid position.
        /// </summary>
        public TileType GetTile(int x, int y)
        {
            if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
                return TileType.Blocked;

            int index = y * gridWidth + x;
            if (tileLayout == null || index >= tileLayout.Length)
                return TileType.Blocked;

            return tileLayout[index];
        }
    }

    /// <summary>
    /// A single enemy path defined as an ordered list of waypoints.
    /// </summary>
    [Serializable]
    public class PathData
    {
        [Tooltip("Ordered list of grid positions forming the path")]
        public Vector2Int[] waypoints;

        [Tooltip("Which spawn point this path starts from")]
        public int spawnPointIndex = 0;

        [Tooltip("Which exit point this path leads to")]
        public int exitPointIndex = 0;
    }
}
