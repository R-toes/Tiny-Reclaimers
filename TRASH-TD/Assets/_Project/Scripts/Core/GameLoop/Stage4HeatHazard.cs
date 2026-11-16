using System.Collections.Generic;
using UnityEngine;
using TrashTD.Audio;
using TrashTD.Combat;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// Implements Stage 4's Factory Heat Hazard mechanic:
    /// - Every round, deployed operators build up 1 heat stack (max 5 stacks).
    /// - At 5 stacks, the heat bar fills and the operator ignites, suffering burning damage over time.
    /// - Burning can ONLY be extinguished by retreating the operator.
    /// - While benched in the deck, heat stacks fade away by 1 each round until completely cooled down.
    /// - Adds warm factory atmosphere lighting and organic drifting embers.
    /// </summary>
    public sealed partial class Stage4HeatHazard : MonoBehaviour
    {
        public const int MaxHeatStacks = 5;
        public const float BurnIntervalSeconds = 1.0f;
        public const int BurnDamagePerTick = 5;
        // Round-start furnace blast: glow, blur, embers, sparks, steam jets and smoke (see EmitHeatFlareBurst)
        public const float HeatFlareDurationSeconds = 2.2f;
        public const float FlareEmberMaxLifetime = 2.0f;
        private const float NormalEmberEmissionRate = 24f;
        // Screen-space heat shimmer (CrtDisplay.SetHeatWarp): picture-UV strength, and how far up the screen it reaches
        private const float AmbientHeatWarp = 0.0021f;
        private const float FlareHeatWarp = 0.0032f;
        private const float HeatWarpBand = 0.40f;
        private const int FlareEmberBurstCount = 96;
        private const float FlareEmberStreamRate = 70f;   // embers per second at full flare, fading with it
        private const int FlareSparkCount = 40;
        private const int FlareSmokeCount = 26;
        private const int FlareSteamPuffsPerVent = 8;
        private const int FlareFloorSteamCount = 14;

        private GridManager gridManager;
        private WaveManager waveManager;
        private OperatorManager operatorManager;
        private PlayerDeck playerDeck;
        private StageData stageData;

        private float burnTickTimer;
        private float heatFlareTimer;
        private SpriteRenderer furnaceUnderglowRenderer;
        private SpriteRenderer heatFlareRenderer;
        private Sprite heatFlareSprite;
        private Camera heatFlareCamera;
        private ParticleSystem emberParticleSystem;
        private float emberFlareMinX;
        private float emberFlareMaxX;
        private float emberFlareY;
        private float emberFlareZ;
        private float emberStreamAccumulator;
        private SpriteRenderer heatFlareGlowRenderer;   // bottom-heavy furnace glow over the view
        private ParticleSystem flareSparkSystem;
        private ParticleSystem flareSteamSystem;
        private ParticleSystem flareSmokeSystem;
        private readonly List<Vector3> steamVentPositions = new List<Vector3>();

        public void Initialize(
            GridManager grid,
            WaveManager waves,
            OperatorManager operators,
            PlayerDeck deck,
            StageData stage)
        {
            gridManager = grid;
            waveManager = waves;
            operatorManager = operators;
            playerDeck = deck;
            stageData = stage;

            if (waveManager != null)
            {
                waveManager.OnWaveStarted += HandleWaveStarted;
            }

            if (operatorManager != null)
            {
                operatorManager.OnOperatorDeployed += HandleOperatorDeployed;
                operatorManager.OnOperatorRetreated += HandleOperatorRetreated;
            }

            ApplyWarmMapAtmosphere();
            CreateHeatFlareOverlay();
            CreateDriftingEmbers();
            CreateIndustrialSteam();
            CreateVentEmbers();
            CreateFloorMist();
            CreateHeatFlareParticles();
        }

        private void OnDestroy()
        {
            if (waveManager != null)
            {
                waveManager.OnWaveStarted -= HandleWaveStarted;
            }

            if (operatorManager != null)
            {
                operatorManager.OnOperatorDeployed -= HandleOperatorDeployed;
                operatorManager.OnOperatorRetreated -= HandleOperatorRetreated;
            }

            // The overlays hang off the camera, not this object
            if (heatFlareRenderer != null)
                Destroy(heatFlareRenderer.gameObject);
            if (heatFlareGlowRenderer != null)
                Destroy(heatFlareGlowRenderer.gameObject);
            if (heatFlareSprite != null)
                Destroy(heatFlareSprite);
            CrtDisplay.SetHeatBlur(0f);
            CrtDisplay.SetHeatWarp(0f, HeatWarpBand, new Vector4(-1f, -1f, -1f, -1f), Vector4.zero);
        }

        private void CreateHeatFlareOverlay()
        {
            heatFlareCamera = Camera.main;
            if (heatFlareCamera == null) return;

            var overlayObject = new GameObject("Stage4HeatFlareOverlay");
            overlayObject.transform.SetParent(heatFlareCamera.transform, false);
            overlayObject.transform.localPosition = Vector3.forward * (heatFlareCamera.nearClipPlane + 0.01f);

            heatFlareRenderer = overlayObject.AddComponent<SpriteRenderer>();
            heatFlareSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            heatFlareRenderer.sprite = heatFlareSprite;
            heatFlareRenderer.sortingLayerName = GridSorting.DefaultLayerName;
            heatFlareRenderer.sortingOrder = short.MaxValue;
            heatFlareRenderer.color = new Color(1f, 0.34f, 0.12f, 0f);

            // Furnace glow rising from the bottom of the view (the map's thermal gradient, stretched over it)
            var glowObject = new GameObject("Stage4HeatFlareGlow");
            glowObject.transform.SetParent(heatFlareCamera.transform, false);
            heatFlareGlowRenderer = glowObject.AddComponent<SpriteRenderer>();
            heatFlareGlowRenderer.sprite = GetVerticalThermalGradientSprite();
            heatFlareGlowRenderer.sortingLayerName = GridSorting.DefaultLayerName;
            heatFlareGlowRenderer.sortingOrder = short.MaxValue - 1;
            heatFlareGlowRenderer.color = new Color(1f, 0.38f, 0.08f, 0f);
        }

        /// <summary>
        /// Drives the heat shimmer: a constant ripple over the hot floor with a slow breathing, a column of it above
        /// each steam vent, and a stronger, taller surge while the round-start blast is running.
        /// </summary>
        private void UpdateHeatWarp(float flare)
        {
            if (heatFlareCamera == null) heatFlareCamera = Camera.main;

            var ventX = new Vector4(-1f, -1f, -1f, -1f);
            var ventY = Vector4.zero;
            if (heatFlareCamera != null)
            {
                for (int i = 0; i < steamVentPositions.Count && i < 4; i++)
                {
                    Vector3 viewport = heatFlareCamera.WorldToViewportPoint(steamVentPositions[i]);
                    ventX[i] = viewport.x;
                    ventY[i] = viewport.y;
                }
            }

            float breathing = 1f + 0.18f * Mathf.Sin(Time.unscaledTime * 0.7f);
            CrtDisplay.SetHeatWarp(
                AmbientHeatWarp * breathing + FlareHeatWarp * flare,
                HeatWarpBand + 0.22f * flare,
                ventX,
                ventY);
        }

        private float UpdateHeatFlare()
        {
            float flare = GetHeatFlareIntensity();
            UpdateHeatWarp(flare);
            // The blur eases off sooner than the glow, so the picture is readable for most of the blast
            CrtDisplay.SetHeatBlur(0.8f * flare * flare);
            if (heatFlareTimer > 0f)
            {
                heatFlareTimer = Mathf.Max(0f, heatFlareTimer - Time.deltaTime);
                StreamHeatFlareEmbers(flare);
            }

            if (heatFlareCamera == null)
                heatFlareCamera = Camera.main;
            if ((heatFlareRenderer == null || heatFlareGlowRenderer == null) && heatFlareCamera != null)
            {
                if (heatFlareRenderer != null) Destroy(heatFlareRenderer.gameObject);
                if (heatFlareGlowRenderer != null) Destroy(heatFlareGlowRenderer.gameObject);
                CreateHeatFlareOverlay();
            }
            if (heatFlareRenderer == null || heatFlareCamera == null) return flare;

            float viewHeight = heatFlareCamera.orthographicSize * 2f;
            float viewWidth = viewHeight * heatFlareCamera.aspect;
            Vector3 overlayPosition = Vector3.forward * (heatFlareCamera.nearClipPlane + 0.01f);
            // Fire light isn't steady: a quick flicker on top of the envelope
            float flicker = 0.88f + 0.12f * Mathf.PerlinNoise(Time.time * 11f, 0.37f);

            heatFlareRenderer.transform.localPosition = overlayPosition;
            heatFlareRenderer.transform.localScale = new Vector3(viewWidth, viewHeight, 1f);
            heatFlareRenderer.color = new Color(1f, 0.30f, 0.08f, 0.13f * flare * flicker);

            if (heatFlareGlowRenderer != null && heatFlareGlowRenderer.sprite != null)
            {
                Vector2 spriteSize = heatFlareGlowRenderer.sprite.bounds.size;
                heatFlareGlowRenderer.transform.localPosition = overlayPosition;
                heatFlareGlowRenderer.transform.localScale = new Vector3(
                    viewWidth / Mathf.Max(0.001f, spriteSize.x),
                    viewHeight / Mathf.Max(0.001f, spriteSize.y),
                    1f);
                heatFlareGlowRenderer.color = new Color(1f, 0.40f, 0.08f, 0.55f * flare * flicker);
            }
            return flare;
        }

        /// <summary>
        /// 1 at the blast, then a long fall-off with a second, smaller throb a third of the way in
        /// (the furnace breathing out again) before it settles.
        /// </summary>
        private float GetHeatFlareIntensity()
        {
            if (heatFlareTimer <= 0f) return 0f;

            float progress = Mathf.Clamp01(1f - heatFlareTimer / HeatFlareDurationSeconds);
            float decay = Mathf.Pow(1f - progress, 1.6f);
            float throbT = Mathf.InverseLerp(0.22f, 0.62f, progress);
            float throb = 0.28f * Mathf.Sin(throbT * Mathf.PI) * (1f - progress);
            return Mathf.Clamp01(decay + throb);
        }

        /// <summary>
        /// When a wave round begins, heat builds up on all active deployed operators.
        /// </summary>
        private void HandleWaveStarted(int currentWave, int totalWaves)
        {
            if (operatorManager == null || !StageCombatModifiers.IsHeatHazardActive) return;

            var deployed = operatorManager.DeployedOperators;
            bool heatStackAdded = false;
            for (int i = 0; i < deployed.Count; i++)
            {
                OperatorBase op = deployed[i];
                if (op == null || !op.IsDeployed || op.CurrentHP <= 0) continue;

                int previousStacks = op.HeatStacks;
                AddHeatStack(op);
                heatStackAdded |= op.HeatStacks > previousStacks;
                OperatorHeatBar.ShowFor(op);
            }

            if (heatStackAdded)
            {
                heatFlareTimer = HeatFlareDurationSeconds;
                emberStreamAccumulator = 0f;
                EmitHeatFlareEmbers();
                EmitHeatFlareSparks();
                EmitHeatFlareSteamAndSmoke();
                GameplayCameraController.Instance?.Shake(0.07f, 0.5f);
            }
        }

        private void HandleOperatorDeployed(OperatorBase op)
        {
            if (op == null || !StageCombatModifiers.IsHeatHazardActive) return;

            // If deployed mid-wave, it immediately participates in this wave's heat
            if (waveManager != null && waveManager.IsWaveInProgress)
            {
                AddHeatStack(op);
            }

            OperatorHeatBar.ShowFor(op);
        }

        /// <summary>Adds one heat stack (max 5); at 5 the operator ignites. Also used by enemy fire attacks (Roboflame, Eradicator).</summary>
        public static void AddHeatStack(OperatorBase op)
        {
            if (op == null) return;

            int previousStacks = op.HeatStacks;
            op.HeatStacks = Mathf.Min(MaxHeatStacks, op.HeatStacks + 1);
            if (op.HeatStacks > previousStacks)
                AudioManager.Instance?.PlaySfx(SfxId.Stage4HeatUp, 0.7f);

            if (op.HeatStacks >= MaxHeatStacks && !op.IsBurning)
            {
                op.IsBurning = true;
                AudioManager.Instance?.PlaySfx(SfxId.Stage4Ignite);
            }

            // The gauge only used to refresh at the start of a round, so heat from an enemy attack showed up late
            if (Application.isPlaying && op.HeatStacks != previousStacks)
                OperatorHeatBar.ShowFor(op);
        }

        private void HandleOperatorRetreated(OperatorBase op)
        {
            if (op == null) return;

            // Fire is extinguished when retreated (a death isn't an extinguish)
            if (op.IsBurning && op.CurrentHP > 0)
                AudioManager.Instance?.PlaySfx(SfxId.Stage4Extinguish);
            op.IsBurning = false;
        }

        private void Update()
        {
            float flare = UpdateHeatFlare();

            // Subtle breathing shimmer only on the bottom furnace glow
            if (furnaceUnderglowRenderer != null)
            {
                float glow = 0.18f + 0.03f * Mathf.Sin(Time.time * 1.1f) + 0.30f * flare;
                furnaceUnderglowRenderer.color = new Color(1f, 0.44f, 0.10f, glow);
            }

            // Burning overtime damage
            if (waveManager == null || !waveManager.IsWaveInProgress || !StageCombatModifiers.IsHeatHazardActive)
            {
                burnTickTimer = 0f;
                return;
            }

            burnTickTimer += Time.deltaTime;
            while (burnTickTimer >= BurnIntervalSeconds)
            {
                burnTickTimer -= BurnIntervalSeconds;
                ApplyBurningDamage();
            }
        }

        private void ApplyBurningDamage()
        {
            if (operatorManager == null) return;

            var deployed = operatorManager.DeployedOperators;
            for (int i = deployed.Count - 1; i >= 0; i--)
            {
                if (i >= deployed.Count) continue;
                OperatorBase op = deployed[i];
                if (op == null || !op.IsDeployed || !op.IsBurning || op.IsCombatPaused || op.CurrentHP <= 0)
                {
                    continue;
                }

                BurnDamageFlash.Flash(op.gameObject);
                // Several burning operators tick together; the shared-sound throttle plays this once.
                AudioManager.Instance?.PlaySfx(SfxId.Stage4BurnTick, 0.6f);
                op.TakeTrueDamage(BurnDamagePerTick);
            }
        }
    }
}
