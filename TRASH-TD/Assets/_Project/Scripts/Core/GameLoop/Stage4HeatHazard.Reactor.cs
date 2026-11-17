using UnityEngine;
using TrashTD.Core.Grid;

namespace TrashTD.Core.GameLoop
{
    // The reactor in Stage 4's bounds art flares whenever heat stacks up. Visual only.
    public partial class Stage4HeatHazard
    {
        // The reactor's glowing eye in Art/Levels/Level 4/Level4 Bounds.png (640 x 480 px): the centre of the core,
        // in pixels from the bottom-left corner of the sprite, and its radius. If the art is redrawn, change these.
        public const float ReactorEyePixelX = 320f;
        public const float ReactorEyePixelY = 412f;
        public const float ReactorEyeRadiusPixels = 31f;

        /// <summary>How long a pulse takes to die away, in seconds.</summary>
        public const float ReactorPulseSeconds = 1.3f;
        public const float ReactorRingSeconds = 0.75f;
        public const int ReactorSparkCount = 22;

        /// <summary>Pulse strengths: heat added to an operator, an operator igniting, and the round-start blast.</summary>
        public const float ReactorStackPulse = 0.5f;
        public const float ReactorIgnitePulse = 0.85f;
        public const float ReactorWavePulse = 1f;

        private const int ReactorHaloSortingOrder = 4;
        private const int ReactorGlowSortingOrder = 5;
        private const int ReactorRingSortingOrder = 6;

        /// <summary>The running Stage 4 hazard, so heat added by an enemy's attack (a static call) can flare the reactor.</summary>
        public static Stage4HeatHazard Current { get; internal set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCurrent() => Current = null;

        private float reactorPulse;
        private float reactorRingTimer;
        private float reactorRingStrength;
        private bool reactorPulseSuppressed;
        private Vector3 reactorPosition;
        private float reactorRadius;
        private SpriteRenderer reactorHalo;
        private SpriteRenderer reactorGlow;
        private SpriteRenderer reactorRing;
        private static Sprite reactorGlowSprite;
        private static Sprite reactorRingSprite;

        /// <summary>How strongly the reactor is pulsing now (1 at a round-start blast, falling to 0).</summary>
        public float ReactorPulse => reactorPulse;

        /// <summary>World position of the reactor's eye, given where the grid and its bounds art sit.</summary>
        public static Vector2 GetReactorWorldPosition(Vector2 gridMin, Vector2 boundsTileOffset, float cellSize, float pixelsPerTile)
        {
            Vector2 boundsMin = gridMin + boundsTileOffset * cellSize;
            return boundsMin + new Vector2(ReactorEyePixelX, ReactorEyePixelY) / pixelsPerTile * cellSize;
        }

        /// <summary>
        /// How bright the reactor is: the strongest of the current pulse (easing out quickly) and the round-start
        /// blast's envelope, so the reactor and the screen glow rise and fall together.
        /// </summary>
        public static float GetReactorLevel(float pulse, float roundFlare)
        {
            float pulseLevel = Mathf.Pow(Mathf.Clamp01(pulse), 1.5f);
            return Mathf.Clamp01(Mathf.Max(pulseLevel, roundFlare));
        }

        /// <summary>
        /// Flares the reactor: a glow, a halo, a ring running out from the core and sparks. A stronger pulse while one
        /// is running raises it; a weaker one never lowers it. A new burst of ring and sparks starts only when the
        /// reactor was nearly calm or the pulse is clearly stronger, so a run of stacks doesn't spam them.
        /// </summary>
        public void PulseReactor(float strength)
        {
            if (reactorPulseSuppressed) return;

            strength = Mathf.Clamp01(strength);
            bool calm = reactorPulse < 0.3f;
            reactorPulse = Mathf.Max(reactorPulse, strength);

            if (calm || strength > reactorRingStrength + 0.25f)
            {
                reactorRingTimer = ReactorRingSeconds;
                reactorRingStrength = strength;
                EmitReactorSparks(strength);
            }
        }

        private void CreateReactorFlare()
        {
            if (gridManager == null || stageData == null || stageData.boundsVisualSprite == null) return;

            float cellSize = gridManager.CellSize;
            Vector2 gridMin = gridManager.GridToWorldPosition(0, 0) - new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);
            float pixelsPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
            Vector2 eye = GetReactorWorldPosition(gridMin, stageData.boundsTileOffset, cellSize, pixelsPerTile);
            reactorPosition = new Vector3(eye.x, eye.y, -0.05f);
            reactorRadius = ReactorEyeRadiusPixels / pixelsPerTile * cellSize;

            var root = new GameObject("Stage4ReactorFlare");
            root.transform.SetParent(transform, false);
            root.transform.position = reactorPosition;

            reactorHalo = CreateReactorLayer(root.transform, "Halo", GetReactorGlowSprite(), ReactorHaloSortingOrder);
            reactorGlow = CreateReactorLayer(root.transform, "Glow", GetReactorGlowSprite(), ReactorGlowSortingOrder);
            reactorRing = CreateReactorLayer(root.transform, "Ring", GetReactorRingSprite(), ReactorRingSortingOrder);
        }

        private static SpriteRenderer CreateReactorLayer(Transform parent, string layerName, Sprite sprite, int sortingOrder)
        {
            var layer = new GameObject(layerName);
            layer.transform.SetParent(parent, false);
            var renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(1f, 0.4f, 0.08f, 0f);
            GridSorting.PlaceOnGround(renderer);
            renderer.sortingOrder = sortingOrder; // above the map art (1) and the underglow (2), below units
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>Called every frame; <paramref name="roundFlare"/> is the round-start blast's 0-1 envelope.</summary>
        private void UpdateReactorFlare(float roundFlare)
        {
            reactorPulse = Mathf.MoveTowards(reactorPulse, 0f, Time.deltaTime / ReactorPulseSeconds);
            reactorRingTimer = Mathf.Max(0f, reactorRingTimer - Time.deltaTime);

            if (reactorGlow == null || reactorHalo == null || reactorRing == null) return;

            float level = GetReactorLevel(reactorPulse, roundFlare);
            // Fire isn't steady: a quick flicker on top of the level
            float shown = level * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 13f, 4.2f));
            float diameter = reactorRadius * 2f;

            bool lit = level > 0.002f;
            reactorGlow.enabled = lit;
            reactorHalo.enabled = lit;
            if (lit)
            {
                // Orange at first, white-hot at full strength
                Color hot = Color.Lerp(new Color(1f, 0.38f, 0.07f), new Color(1f, 0.9f, 0.62f), level);
                float glowSize = diameter * (1.15f + 0.9f * level);
                reactorGlow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
                reactorGlow.color = new Color(hot.r, hot.g, hot.b, 0.95f * shown);

                float haloSize = diameter * (2.4f + 2.4f * level);
                reactorHalo.transform.localScale = new Vector3(haloSize, haloSize, 1f);
                reactorHalo.color = new Color(1f, 0.4f, 0.08f, 0.5f * shown);
            }

            bool ringing = reactorRingTimer > 0f;
            reactorRing.enabled = ringing;
            if (ringing)
            {
                float progress = 1f - reactorRingTimer / ReactorRingSeconds;
                float ringSize = diameter * (1.2f + progress * 4.2f * (0.6f + 0.4f * reactorRingStrength));
                reactorRing.transform.localScale = new Vector3(ringSize, ringSize, 1f);
                reactorRing.color = new Color(1f, 0.62f, 0.2f, 0.85f * reactorRingStrength * Mathf.Pow(1f - progress, 1.4f));
            }
        }

        /// <summary>Sparks thrown out of the reactor in every direction, drifting up and falling back.</summary>
        private void EmitReactorSparks(float strength)
        {
            if (flareSparkSystem == null || reactorRadius <= 0f) return;

            int count = Mathf.RoundToInt(ReactorSparkCount * strength);
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var spark = new ParticleSystem.EmitParams
                {
                    position = reactorPosition + (Vector3)(direction * reactorRadius * 0.6f),
                    velocity = (Vector3)(direction * Random.Range(1.6f, 3.6f)) + Vector3.up * 0.8f,
                    startLifetime = Random.Range(0.5f, 1.0f),
                    startSize = Random.Range(0.04f, 0.08f),
                    startColor = Color.Lerp(FlareEmberHot, new Color(1f, 0.82f, 0.4f, 1f), Random.value)
                };
                flareSparkSystem.Emit(spark, 1);
            }
        }

        private static Sprite GetReactorGlowSprite()
        {
            if (reactorGlowSprite == null)
            {
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                float center = (size - 1) * 0.5f;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                        float alpha = distance >= 1f ? 0f : Mathf.Pow(1f - distance, 1.8f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                reactorGlowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return reactorGlowSprite;
        }

        private static Sprite GetReactorRingSprite()
        {
            if (reactorRingSprite == null)
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                float center = (size - 1) * 0.5f;
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                        float offset = (distance - 0.82f) / 0.07f;
                        float alpha = distance >= 1f ? 0f : Mathf.Exp(-offset * offset);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
                texture.SetPixels(pixels);
                texture.Apply(false, false);
                reactorRingSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            }
            return reactorRingSprite;
        }
    }
}
