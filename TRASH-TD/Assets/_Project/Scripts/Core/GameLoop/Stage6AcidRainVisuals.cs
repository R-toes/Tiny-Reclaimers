using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using Random = UnityEngine.Random;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// Stage 6 (The Pale) acid storm. The rain damage stays in StageBootstrapper (ApplyStage6AcidRainDamage),
    /// which calls PlayAcidTick on every damage tick. Everything here is visual except the downpour puddles.
    /// - Acid rain: slanted streaks with a bright head and a fading tail, blown left to right. The storm swings
    ///   between a drizzle and a downpour. Drops splash on the ground, throw up droplets and sometimes hiss off fumes.
    /// - Toxic fog: pixel-art clouds (dithered edges, never scaled, snapped to the art grid, like Stage 3's dust) in
    ///   two layers, thin over the enemy lane and thick off the grid, plus a green cast over the map.
    /// - The map's own acid pool (the Trap tiles) gets bubbles that swell and pop on top of the art.
    /// - Storm flashes: a flash from the top of the map, sometimes twice.
    /// - Downpour puddles (gameplay): during a downpour, while the acid is live (a wave, or the sandbox), acid seeps up
    ///   on random tiles, drawn in the map pool's own colours. Each puddle grows through 4 stages and shrinks back
    ///   through them (1-2-3-4-3-2-1), 6 s of wave time each; its damage follows the stage (GridCell.AcidPuddleDamage,
    ///   dealt like Trap damage to operators and enemies; acid-tile-immune enemies don't take it).
    /// Everything is clipped to the map art and runs on scaled time, so it freezes while paused.
    /// Generated sprites belong to this instance and are destroyed with it (domain reload is off).
    /// </summary>
    public sealed partial class Stage6AcidRainVisuals : MonoBehaviour
    {
        // Ground layer (beneath every unit), back to front
        private const int TintSortingOrder = 2;       // map artwork is 1
        private const int PuddleSortingOrder = 4;
        private const int GroundFxSortingOrder = 5;   // splash dashes, bubbles
        private const int BackFogSortingOrder = 6;
        // Default layer
        private const int FrontFogSortingOrder = 11;  // above ground units and foreground art (10), below air units (12)
        private const int FlashSortingOrder = 23;     // above units, below the rain
        private const int RainSortingOrder = 24;      // above units, below projectiles (40)

        private const int ArtPixelsPerTile = 32;
        private const int MaxRainDrops = 260;
        private const int MaxSpray = 140;
        private const int MaxFumes = 32;
        private const int MaxBubbles = 40;

        private const float DrizzleIntensity = 0.6f;
        private const float DownpourIntensity = 1f;
        private const float IntensityChangePerSecond = 0.15f;
        private const float TickGustSeconds = 0.6f;
        private const float TickGustBoost = 0.45f;    // extra intensity during a damage-tick gust
        private const float SplashDuration = 0.14f;
        private const float FumeChance = 0.16f;
        private const float SprayGravity = 9f;        // tiles / s²

        // Downpour puddles
        public const float PuddleStageSeconds = 6f;              // wave time per stage
        public const float PuddleSeepSeconds = 0.8f;              // fades in before stage 1, no damage yet
        public const float PuddleDrySeconds = 0.8f;               // fades out after the last stage 1, no damage
        /// <summary>True damage per TrapTileRules tick at stages 1-4 (stage 4 matches the map's acid pool).</summary>
        public static readonly int[] PuddleStageDamage = { 3, 5, 8, TrapTileRules.DamagePerTick };
        /// <summary>The stages a puddle goes through: up to the peak and back down.</summary>
        private static readonly int[] PuddleStageOrder = { 0, 1, 2, 3, 2, 1, 0 };
        private const int MaxPuddles = 3;
        private const float PuddleDownpourThreshold = 0.85f;      // rain intensity needed for puddles to form
        private const int PuddleVariants = 4;
        private const int PuddleStages = 4;
        private const int PuddleFrames = 2;
        private const float PuddleFrameSeconds = 0.55f;

        private static readonly Color RainColor = new Color(0.66f, 1f, 0.22f, 0.62f);
        private static readonly Color HeavyRainColor = new Color(0.86f, 1f, 0.5f, 0.85f);
        private static readonly Color SplashColor = new Color(0.58f, 1f, 0.2f, 0.65f);
        private static readonly Color SprayColor = new Color(0.78f, 1f, 0.38f, 0.9f);
        private static readonly Color TintColor = new Color(0.22f, 0.45f, 0.06f, 1f);
        private static readonly Color FlashColor = new Color(0.82f, 1f, 0.66f, 1f);

        // The map's acid pool palette (level 6 complete.png): flat lime body, 1 px olive edge, pale bubble arcs
        private static readonly Color PoolBody = new Color32(191, 218, 64, 255);
        private static readonly Color PoolEdge = new Color32(112, 136, 0, 255);
        private static readonly Color PoolHighlight = new Color32(226, 252, 101, 255);

        // Pixel-art fog tones (lit from above)
        private static readonly Color FogLight = new Color(0.74f, 0.88f, 0.5f);
        private static readonly Color FogMid = new Color(0.54f, 0.7f, 0.34f);
        private static readonly Color FogShadow = new Color(0.36f, 0.48f, 0.22f);

        /// <summary>Fog cloud sizes in art pixels; clouds never scale, so they stay crisp.</summary>
        private static readonly Vector2Int[] FogSizes =
        {
            new Vector2Int(56, 22),
            new Vector2Int(72, 28),
            new Vector2Int(88, 32),
            new Vector2Int(104, 38)
        };

        /// <summary>A fume wisp grows through these sizes instead of scaling.</summary>
        private static readonly Vector2Int[] FumeSizes =
        {
            new Vector2Int(10, 7),
            new Vector2Int(14, 10),
            new Vector2Int(19, 13)
        };

        /// <summary>
        /// Puddle shapes as metaballs (x, y, radius) in art pixels from the tile centre at the peak stage: a lopsided
        /// lobed pool, a wide oval, a pool with splash drops running off it, and a diagonal kidney. Smaller stages
        /// scale them in. At the peak they nearly fill the tile but stay round-edged, never square.
        /// </summary>
        private static readonly Vector3[][] PuddleShapes =
        {
            new[] { new Vector3(0f, 0f, 9f), new Vector3(5f, 5f, 7f), new Vector3(-6f, -2f, 7.5f), new Vector3(4f, -6f, 5.5f), new Vector3(-4f, 7f, 4f), new Vector3(9f, -1f, 4f) },
            new[] { new Vector3(-7f, -1f, 7f), new Vector3(6f, 1f, 7f), new Vector3(0f, 0f, 7.5f), new Vector3(-1f, -4f, 5.5f), new Vector3(9f, -3f, 4.5f), new Vector3(-10f, 2f, 4f) },
            new[] { new Vector3(-3f, -1f, 9.5f), new Vector3(3f, 3f, 7f), new Vector3(-7f, 5f, 4.5f), new Vector3(10f, -9f, 3.2f), new Vector3(11f, 8f, 2.4f) },
            new[] { new Vector3(-7f, -6f, 6.5f), new Vector3(-2f, -1f, 7.5f), new Vector3(4f, 4f, 7f), new Vector3(8f, 9f, 4.5f), new Vector3(5f, -3f, 4.5f) }
        };
        private static readonly float[] PuddleStageScale = { 0.42f, 0.62f, 0.82f, 1f };

        private enum Storm { Drizzle, Downpour }

        private sealed class FogLayer
        {
            public string Name;
            public int Count;
            public int SortingOrder;
            public bool OnGround;
            public Vector2 AlphaRange;
            public Vector2 SpeedRange;
            public float DensityExponent; // > 1 thins the layer out faster over the lane
        }

        private static readonly FogLayer[] FogLayers =
        {
            new FogLayer
            {
                Name = "Back", Count = 14, SortingOrder = BackFogSortingOrder, OnGround = true,
                AlphaRange = new Vector2(0.45f, 0.65f), SpeedRange = new Vector2(0.12f, 0.24f), DensityExponent = 1f
            },
            new FogLayer
            {
                Name = "Front", Count = 10, SortingOrder = FrontFogSortingOrder, OnGround = false,
                AlphaRange = new Vector2(0.35f, 0.5f), SpeedRange = new Vector2(0.18f, 0.32f), DensityExponent = 2.2f
            }
        };

        private sealed class FogCloud
        {
            public FogLayer Layer;
            public Transform Transform;
            public SpriteRenderer Renderer;
            public float X;
            public float BaseY;
            public float HalfWidth;
            public float BaseAlpha;
            public float Speed;
            public float Phase;
            public float FadeIn;
        }

        private sealed class RainDrop
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool Active;
            public bool Heavy;
            public float LandY;
            public float SplashTimer; // > 0 while showing its splash
        }

        /// <summary>A droplet thrown from a splash or a popping bubble, or a rising fume wisp.</summary>
        private sealed class Particle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool Active;
            public Vector2 Velocity;
            public Vector2 Position; // unsnapped; the transform is snapped to the art grid
            public float Age;
            public float Lifetime;
        }

        private sealed class Bubble
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public bool Active;
            public float Age;
            public float Lifetime;
            public int MaxStage;  // largest ring sprite it reaches before popping
            public float Delay;   // waits this long before the next bubble rises
        }

        private enum PuddleState { Idle, Seeping, Staged, Drying }

        private sealed class Puddle
        {
            public Transform Transform;
            public SpriteRenderer Renderer;
            public PuddleState State;
            public GridCell Cell;
            public int Variant;
            public int Step;        // index into PuddleStageOrder while Staged
            public float StateTime; // wave time in the current state or step
            public float FumeTimer;
        }

        private GridManager gridManager;
        private StageData stageData;
        private Func<bool> isAcidActive;
        private float cellSize;
        private float pixelSize;
        private Vector2 artMin;
        private Vector2 artMax;
        private Vector2 gridMin;
        private float[,] fogDensityField; // per cell, with a 1-tile border ring of full density

        private readonly List<FogCloud> fogClouds = new List<FogCloud>();
        private readonly List<RainDrop> rainDrops = new List<RainDrop>();
        private readonly List<Particle> sprays = new List<Particle>();
        private readonly List<Particle> fumes = new List<Particle>();
        private readonly List<Bubble> bubbles = new List<Bubble>();
        private readonly List<Vector3> poolCentres = new List<Vector3>();
        private readonly List<Puddle> puddles = new List<Puddle>();
        private readonly List<GridCell> puddleCandidates = new List<GridCell>();
        private readonly List<Vector3> bubbleSites = new List<Vector3>();
        private SpriteRenderer tint;
        private SpriteRenderer skyFlash;

        private Storm storm = Storm.Drizzle;
        private float stormTimeLeft;
        private float rainIntensity;
        private Vector2 rainVelocity;
        private float tickGustTimer;
        private float tickPulse;           // 1 on a damage tick, fades quickly
        private float flashTimer;          // counts down to the next storm flash
        private float flashElapsed = -1f;  // time into the current flash, < 0 when none
        private bool flashTwice;
        private float puddleTimer;
        private float sprayBudget;         // droplets still allowed this frame

        private Sprite whiteSprite;
        private Sprite streakSprite;
        private Sprite skyFlashSprite;
        private Sprite[] fogSprites;
        private Sprite[] fumeSprites;
        private Sprite[] ringSprites;
        private Sprite[][][] puddleSprites; // [variant][stage][frame]
        private readonly List<Sprite> generatedSprites = new List<Sprite>();

        public void Initialize(GridManager grid, StageData stage, Func<bool> acidActive = null)
        {
            gridManager = grid;
            stageData = stage;
            isAcidActive = acidActive;
            if (gridManager == null || stageData == null) return;

            cellSize = gridManager.CellSize;
            pixelSize = cellSize / ArtPixelsPerTile;
            CalculateBounds();
            BuildFogDensityField();
            CreateSprites();

            CreateArtMask();
            CreateTintAndFlash();
            CreateFog();
            CreatePoolBubbles();
            CreatePuddles();
            CreateRainDrops();
            CreateParticles();

            rainIntensity = DrizzleIntensity;
            stormTimeLeft = Random.Range(18f, 30f);
            flashTimer = Random.Range(8f, 16f);
            puddleTimer = Random.Range(1f, 2.5f);
            UpdateRainVelocity();
            PrefillRain();
        }

        /// <summary>The acid just hurt everyone (StageBootstrapper's damage tick): a gust and a green pulse.</summary>
        public void PlayAcidTick()
        {
            tickGustTimer = TickGustSeconds;
            tickPulse = 1f;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < puddles.Count; i++)
            {
                if (puddles[i].Cell != null) puddles[i].Cell.AcidPuddleDamage = 0;
            }

            // whiteSprite wraps Texture2D.whiteTexture, which is built in: destroy the sprite only
            if (whiteSprite != null) Destroy(whiteSprite);
            foreach (Sprite sprite in generatedSprites) DestroyGeneratedSprite(sprite);
            generatedSprites.Clear();
        }

        private static void DestroyGeneratedSprite(Sprite sprite)
        {
            if (sprite == null) return;
            if (sprite.texture != null) Destroy(sprite.texture);
            Destroy(sprite);
        }

        private void Update()
        {
            if (gridManager == null) return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            UpdateStorm(deltaTime);
            sprayBudget = 12f; // caps how many droplets a heavy frame throws up
            UpdateFog(deltaTime);
            UpdateRain(deltaTime);
            UpdatePuddles(deltaTime);
            UpdateBubbles(deltaTime);
            UpdateSprays(deltaTime);
            UpdateFumes(deltaTime);
            UpdateTintAndFlash(deltaTime);
        }

        // ---------- Storm cycle ----------

        private void UpdateStorm(float deltaTime)
        {
            stormTimeLeft -= deltaTime;
            if (stormTimeLeft <= 0f)
            {
                storm = storm == Storm.Drizzle ? Storm.Downpour : Storm.Drizzle;
                stormTimeLeft = storm == Storm.Drizzle ? Random.Range(18f, 30f) : Random.Range(12f, 22f);
            }

            float target = storm == Storm.Downpour ? DownpourIntensity : DrizzleIntensity;
            rainIntensity = Mathf.MoveTowards(rainIntensity, target, IntensityChangePerSecond * deltaTime);

            if (tickGustTimer > 0f) tickGustTimer = Mathf.Max(0f, tickGustTimer - deltaTime);
            tickPulse = Mathf.MoveTowards(tickPulse, 0f, deltaTime * 2.2f);
            UpdateRainVelocity();
        }

        /// <summary>Current strength including a damage-tick gust (up to ~1.45).</summary>
        private float EffectiveIntensity =>
            rainIntensity + TickGustBoost * Mathf.Clamp01(tickGustTimer / TickGustSeconds);

        /// <summary>Blown left to right; heavier rain falls faster and slants more.</summary>
        private void UpdateRainVelocity()
        {
            float k = Mathf.Clamp01(EffectiveIntensity);
            rainVelocity = new Vector2(Mathf.Lerp(0.7f, 1.6f, k), Mathf.Lerp(-8.5f, -11.5f, k)) * cellSize;
        }

        // ---------- Bounds and fog density ----------

        private void CalculateBounds()
        {
            gridMin = gridManager.GridToWorldPosition(0, 0) - new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);
            Vector2 gridMax = gridMin + new Vector2(stageData.gridWidth, stageData.gridHeight) * cellSize;
            artMin = gridMin;
            artMax = gridMax;

            // The artwork can be larger than the grid
            if (stageData.mapVisualSprite != null)
            {
                float pixelPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
                Vector2 spriteMin = gridMin + stageData.visualTileOffset * cellSize;
                Vector2 spriteMax = spriteMin + stageData.mapVisualSprite.rect.size / pixelPerTile * cellSize;
                artMin = Vector2.Min(artMin, spriteMin);
                artMax = Vector2.Max(artMax, spriteMax);
            }

            // The bounds artwork around the map: the storm mask, toxic tint, sky flash, fog clouds and acid rain cover it too
            if (stageData.boundsVisualSprite != null)
            {
                float pixelPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
                Vector2 boundsMin = gridMin + stageData.boundsTileOffset * cellSize;
                Vector2 boundsMax = boundsMin + stageData.boundsVisualSprite.rect.size / pixelPerTile * cellSize;
                artMin = Vector2.Min(artMin, boundsMin);
                artMax = Vector2.Max(artMax, boundsMax);
            }
        }

        private void BuildFogDensityField()
        {
            int width = stageData.gridWidth;
            int height = stageData.gridHeight;
            fogDensityField = new float[width + 2, height + 2];

            for (int x = 0; x < width + 2; x++)
            {
                for (int y = 0; y < height + 2; y++)
                {
                    bool insideGrid = x >= 1 && x <= width && y >= 1 && y <= height;
                    fogDensityField[x, y] = insideGrid ? GetTileFogDensity(stageData.GetTile(x - 1, y - 1)) : 1f;
                }
            }
        }

        private static float GetTileFogDensity(TileType tile)
        {
            return tile switch
            {
                TileType.HighGround => 0.5f,
                TileType.Blocked => 0.85f,
                TileType.Trap => 0.75f, // fumes gather over the acid pool
                _ => 0.25f              // enemy lane, melee ground, spawns, exits, holes
            };
        }

        /// <summary>0-1 fog thickness at a world position: 1 off the grid, lowest over the enemy lane.</summary>
        private float GetFogDensity(Vector2 worldPosition)
        {
            int width = stageData.gridWidth;
            int height = stageData.gridHeight;

            float fx = Mathf.Clamp((worldPosition.x - gridMin.x) / cellSize - 0.5f, -1f, width) + 1f;
            float fy = Mathf.Clamp((worldPosition.y - gridMin.y) / cellSize - 0.5f, -1f, height) + 1f;

            int x0 = Mathf.Min(Mathf.FloorToInt(fx), width);
            int y0 = Mathf.Min(Mathf.FloorToInt(fy), height);
            float tx = fx - x0;
            float ty = fy - y0;

            float bottom = Mathf.Lerp(fogDensityField[x0, y0], fogDensityField[x0 + 1, y0], tx);
            float top = Mathf.Lerp(fogDensityField[x0, y0 + 1], fogDensityField[x0 + 1, y0 + 1], tx);
            return Mathf.Lerp(bottom, top, ty);
        }

        /// <summary>Snaps to the art pixel grid, so pixel-art sprites never land between pixels.</summary>
        private Vector3 Snap(float x, float y) => new Vector3(
            Mathf.Round(x / pixelSize) * pixelSize,
            Mathf.Round(y / pixelSize) * pixelSize,
            0f);

        // ---------- Scene objects ----------
    }
}
