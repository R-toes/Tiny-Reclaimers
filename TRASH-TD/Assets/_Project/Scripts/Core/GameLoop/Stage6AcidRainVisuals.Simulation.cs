using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using Random = UnityEngine.Random;

namespace TrashTD.Core.GameLoop
{
    // Per-frame simulation: fog, rain, sprays, fumes, bubbles, puddles and the flash.
    public partial class Stage6AcidRainVisuals
    {
        private void UpdateFog(float deltaTime)
        {
            float time = Time.time;
            float thickness = 0.85f + 0.25f * rainIntensity;
            for (int i = 0; i < fogClouds.Count; i++)
            {
                FogCloud cloud = fogClouds[i];
                cloud.X += cloud.Speed * deltaTime;
                if (cloud.X > artMax.x + cloud.HalfWidth) ResetFogCloud(cloud, false);

                // A slow roll, out of step with the neighbours
                float y = cloud.BaseY + Mathf.Sin(time * 0.5f + cloud.Phase) * cellSize * 0.06f;
                cloud.Transform.position = Snap(cloud.X, y);

                cloud.FadeIn = Mathf.Min(1f, cloud.FadeIn + deltaTime * 0.5f);
                float density = Mathf.Pow(GetFogDensity(new Vector2(cloud.X, y)), cloud.Layer.DensityExponent);
                float breathe = 0.85f + 0.15f * Mathf.Sin(time * 0.9f + cloud.Phase * 2f);

                Color color = Color.white;
                color.a = cloud.BaseAlpha * density * thickness * breathe * cloud.FadeIn;
                cloud.Renderer.color = color;
            }
        }

        // ---------- Rain ----------

        private void UpdateRain(float deltaTime)
        {
            int wantedDrops = Mathf.Min(MaxRainDrops, Mathf.RoundToInt(EffectiveIntensity / (DownpourIntensity + TickGustBoost) * MaxRainDrops));

            for (int i = 0; i < rainDrops.Count; i++)
            {
                RainDrop drop = rainDrops[i];
                if (!drop.Active)
                {
                    if (i < wantedDrops) SpawnRainDrop(drop, false);
                    continue;
                }

                if (drop.SplashTimer > 0f)
                {
                    drop.SplashTimer -= deltaTime;
                    float k = Mathf.Clamp01(drop.SplashTimer / SplashDuration);
                    Color splash = SplashColor;
                    splash.a *= k;
                    drop.Renderer.color = splash;
                    // The dash spreads as it fades (whole art pixels)
                    drop.Transform.localScale = new Vector3(pixelSize * Mathf.Round(Mathf.Lerp(5f, 3f, k)), pixelSize, 1f);

                    if (drop.SplashTimer <= 0f)
                    {
                        // Drops past the current count stop here, so the rain thins out gradually
                        if (i < wantedDrops) SpawnRainDrop(drop, false);
                        else DeactivateRainDrop(drop);
                    }
                    continue;
                }

                Vector3 position = drop.Transform.position;
                float speed = drop.Heavy ? 1.15f : 1f;
                position.x += rainVelocity.x * speed * deltaTime;
                position.y += rainVelocity.y * speed * deltaTime;
                drop.Transform.position = position;

                if (position.y <= drop.LandY) StartSplash(drop);
            }
        }

        private void SpawnRainDrop(RainDrop drop, bool midAir)
        {
            drop.Active = true;
            drop.SplashTimer = 0f;
            drop.Heavy = Random.value < 0.15f;
            drop.Transform.gameObject.SetActive(true);

            // Drops fall diagonally to the right, so spawn them further left to cover the whole map
            float fallTime = (artMax.y - artMin.y) / Mathf.Max(0.01f, -rainVelocity.y);
            float slant = rainVelocity.x * fallTime;
            float startY = midAir
                ? Random.Range(artMin.y, artMax.y + cellSize)
                : artMax.y + Random.Range(0f, cellSize);
            drop.Transform.position = new Vector3(Random.Range(artMin.x - slant, artMax.x), startY, 0f);
            drop.LandY = Random.Range(artMin.y, Mathf.Min(artMax.y, startY));

            float lengthPixels = Mathf.Round(Mathf.Lerp(4f, 7f, Mathf.Clamp01(EffectiveIntensity))) + (drop.Heavy ? 2f : 0f);
            Vector2 streakSize = streakSprite.bounds.size;
            drop.Transform.localScale = new Vector3(
                pixelSize / streakSize.x * (drop.Heavy ? 2f : 1f),
                pixelSize * lengthPixels / streakSize.y,
                1f);
            drop.Transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(rainVelocity.x, -rainVelocity.y) * Mathf.Rad2Deg);
            drop.Renderer.sprite = streakSprite;
            drop.Renderer.sortingLayerID = GridSorting.DefaultLayerId;
            drop.Renderer.sortingOrder = RainSortingOrder;
            drop.Renderer.color = drop.Heavy ? HeavyRainColor : RainColor;
        }

        /// <summary>The drop becomes a short dash on the ground beneath the units, throws droplets and maybe a fume.</summary>
        private void StartSplash(RainDrop drop)
        {
            drop.SplashTimer = SplashDuration;
            Vector3 position = Snap(drop.Transform.position.x, drop.LandY);
            drop.Transform.position = position;
            drop.Transform.rotation = Quaternion.identity;
            drop.Transform.localScale = new Vector3(pixelSize * 3f, pixelSize, 1f);
            drop.Renderer.sprite = whiteSprite;
            GridSorting.PlaceOnGround(drop.Renderer);
            drop.Renderer.sortingOrder = GroundFxSortingOrder;
            drop.Renderer.color = SplashColor;

            int droplets = drop.Heavy ? 3 : (Random.value < 0.6f ? 2 : 1);
            for (int i = 0; i < droplets; i++) EmitSpray(position, 1f);
            if (Random.value < FumeChance * (drop.Heavy ? 2f : 1f)) EmitFume(position);
        }

        private static void DeactivateRainDrop(RainDrop drop)
        {
            drop.Active = false;
            drop.SplashTimer = 0f;
            drop.Transform.gameObject.SetActive(false);
        }

        // ---------- Droplets and fumes ----------

        private static Particle TakeIdle(List<Particle> pool)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].Active) return pool[i];
            }
            return null;
        }

        /// <summary>A single acid droplet thrown up and out; it arcs and falls back.</summary>
        private void EmitSpray(Vector3 origin, float strength)
        {
            if (sprayBudget < 1f) return;
            Particle particle = TakeIdle(sprays);
            if (particle == null) return;
            sprayBudget -= 1f;

            particle.Active = true;
            particle.Age = 0f;
            particle.Lifetime = Random.Range(0.18f, 0.32f);
            particle.Velocity = new Vector2(Random.Range(-0.9f, 0.9f), Random.Range(0.9f, 1.7f) * strength) * cellSize;
            particle.Position = origin;
            float size = pixelSize * (Random.value < 0.25f ? 2f : 1f);
            particle.Transform.position = origin;
            particle.Transform.localScale = new Vector3(size, size, 1f);
            particle.Renderer.color = SprayColor;
            particle.Transform.gameObject.SetActive(true);
        }

        /// <summary>A wisp of acid fume rising from where a drop landed, a bubble popped or a puddle seethes.</summary>
        private void EmitFume(Vector3 origin)
        {
            Particle particle = TakeIdle(fumes);
            if (particle == null) return;

            particle.Active = true;
            particle.Age = 0f;
            particle.Lifetime = Random.Range(1.0f, 1.6f);
            particle.Velocity = new Vector2(Random.Range(0.05f, 0.25f), Random.Range(0.25f, 0.45f)) * cellSize;
            particle.Position = origin;
            particle.Renderer.flipX = Random.value < 0.5f;
            particle.Renderer.sprite = fumeSprites[0];
            particle.Transform.position = Snap(origin.x, origin.y);
            particle.Transform.gameObject.SetActive(true);
        }

        private void UpdateSprays(float deltaTime)
        {
            for (int i = 0; i < sprays.Count; i++)
            {
                Particle particle = sprays[i];
                if (!particle.Active) continue;

                particle.Age += deltaTime;
                float k = particle.Age / particle.Lifetime;
                if (k >= 1f)
                {
                    particle.Active = false;
                    particle.Transform.gameObject.SetActive(false);
                    continue;
                }

                particle.Velocity.y -= SprayGravity * cellSize * deltaTime;
                particle.Position += particle.Velocity * deltaTime;
                particle.Transform.position = Snap(particle.Position.x, particle.Position.y);

                Color color = SprayColor;
                color.a *= 1f - k * k;
                particle.Renderer.color = color;
            }
        }

        /// <summary>Fumes rise and drift, growing through the fume sprites (no scaling) as they fade in then out.</summary>
        private void UpdateFumes(float deltaTime)
        {
            for (int i = 0; i < fumes.Count; i++)
            {
                Particle particle = fumes[i];
                if (!particle.Active) continue;

                particle.Age += deltaTime;
                float k = particle.Age / particle.Lifetime;
                if (k >= 1f)
                {
                    particle.Active = false;
                    particle.Transform.gameObject.SetActive(false);
                    continue;
                }

                particle.Position += particle.Velocity * deltaTime;
                particle.Transform.position = Snap(particle.Position.x, particle.Position.y);
                particle.Renderer.sprite = fumeSprites[Mathf.Min(fumeSprites.Length - 1, (int)(k * fumeSprites.Length))];
                Color color = Color.white;
                color.a = 0.75f * Mathf.Sin(k * Mathf.PI);
                particle.Renderer.color = color;
            }
        }

        // ---------- Bubbles ----------

        /// <summary>Bubbles rise in the map's acid pool and in any wet puddle, swelling through ring sprites and popping.</summary>
        private void UpdateBubbles(float deltaTime)
        {
            bubbleSites.Clear();
            bubbleSites.AddRange(poolCentres);
            for (int i = 0; i < puddles.Count; i++)
            {
                // Only puddles big enough (stage 2+) to hold a bubble
                Puddle puddle = puddles[i];
                if (puddle.State == PuddleState.Staged && PuddleStageOrder[puddle.Step] >= 1) bubbleSites.Add(puddle.Cell.WorldPosition);
            }

            for (int i = 0; i < bubbles.Count; i++)
            {
                Bubble bubble = bubbles[i];
                if (!bubble.Active)
                {
                    bubble.Delay -= deltaTime;
                    if (bubble.Delay <= 0f && bubbleSites.Count > 0) StartBubble(bubble);
                    continue;
                }

                bubble.Age += deltaTime;
                float k = bubble.Age / bubble.Lifetime;
                if (k >= 1f)
                {
                    PopBubble(bubble);
                    continue;
                }

                int stage = Mathf.Min(bubble.MaxStage, (int)(k * (bubble.MaxStage + 1)));
                bubble.Renderer.sprite = ringSprites[stage];
                Color color = PoolHighlight;
                color.a = Mathf.Clamp01(k * 4f);
                bubble.Renderer.color = color;
            }
        }

        private void StartBubble(Bubble bubble)
        {
            Vector3 site = bubbleSites[Random.Range(0, bubbleSites.Count)];
            // Kept near the middle of the tile, which is inside the pool art (and inside a stage 2+ puddle)
            bubble.Transform.position = Snap(
                site.x + Random.Range(-0.22f, 0.22f) * cellSize,
                site.y + Random.Range(-0.2f, 0.2f) * cellSize);
            bubble.Active = true;
            bubble.Age = 0f;
            bubble.Lifetime = Random.Range(0.6f, 1.3f);
            bubble.MaxStage = Random.Range(1, ringSprites.Length);
            bubble.Renderer.sprite = ringSprites[0];
            bubble.Transform.gameObject.SetActive(true);
        }

        private void PopBubble(Bubble bubble)
        {
            Vector3 position = bubble.Transform.position;
            bubble.Active = false;
            bubble.Transform.gameObject.SetActive(false);
            // Faster bubbling during a downpour and right after a damage tick
            bubble.Delay = Random.Range(0.3f, 1.8f) / (0.6f + rainIntensity + tickPulse);

            EmitSpray(position, 0.6f);
            if (Random.value < 0.35f) EmitFume(position);
        }

        // ---------- Downpour puddles ----------

        /// <summary>
        /// Puddles form only in a downpour while the acid is live, and all their timers run on wave time: between
        /// waves they hold still (operators take no damage then anyway). Each puddle seeps in, then steps through
        /// PuddleStageOrder (stages 1-2-3-4-3-2-1, PuddleStageSeconds each, damage following the stage), then dries up.
        /// </summary>
        private void UpdatePuddles(float deltaTime)
        {
            bool acidLive = isAcidActive == null || isAcidActive();
            if (!acidLive) return;

            if (storm == Storm.Downpour && rainIntensity >= PuddleDownpourThreshold)
            {
                puddleTimer -= deltaTime;
                if (puddleTimer <= 0f)
                {
                    puddleTimer = Random.Range(2.5f, 4.5f);
                    TrySpawnPuddle();
                }
            }

            for (int i = 0; i < puddles.Count; i++)
            {
                Puddle puddle = puddles[i];
                if (puddle.State == PuddleState.Idle) continue;
                puddle.StateTime += deltaTime;

                switch (puddle.State)
                {
                    case PuddleState.Seeping:
                    {
                        float k = Mathf.Clamp01(puddle.StateTime / PuddleSeepSeconds);
                        puddle.Renderer.sprite = puddleSprites[puddle.Variant][0][0];
                        puddle.Renderer.color = new Color(1f, 1f, 1f, k);
                        if (k >= 1f) EnterStep(puddle, 0);
                        break;
                    }
                    case PuddleState.Staged:
                    {
                        int stage = PuddleStageOrder[puddle.Step];
                        Sprite[] frames = puddleSprites[puddle.Variant][stage];
                        puddle.Renderer.sprite = frames[(int)(puddle.StateTime / PuddleFrameSeconds) % frames.Length];
                        puddle.Renderer.color = Color.white;

                        // Seethes: bigger puddles fume more often
                        puddle.FumeTimer -= deltaTime;
                        if (puddle.FumeTimer <= 0f)
                        {
                            puddle.FumeTimer = Random.Range(1.4f, 2.4f) / (1f + stage * 0.5f);
                            Vector3 centre = puddle.Cell.WorldPosition;
                            float reach = 0.1f + 0.06f * stage;
                            EmitFume(centre + new Vector3(Random.Range(-reach, reach), Random.Range(-reach, reach), 0f) * cellSize);
                        }

                        if (puddle.StateTime >= PuddleStageSeconds)
                        {
                            if (puddle.Step + 1 < PuddleStageOrder.Length) EnterStep(puddle, puddle.Step + 1);
                            else BeginDrying(puddle);
                        }
                        break;
                    }
                    case PuddleState.Drying:
                    {
                        float k = Mathf.Clamp01(puddle.StateTime / PuddleDrySeconds);
                        puddle.Renderer.color = new Color(1f, 1f, 1f, 1f - k);
                        if (k >= 1f)
                        {
                            puddle.State = PuddleState.Idle;
                            puddle.Cell = null;
                            puddle.Transform.gameObject.SetActive(false);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>Moves a puddle to a step of PuddleStageOrder: its size and damage change together, with a splash.</summary>
        private void EnterStep(Puddle puddle, int step)
        {
            puddle.State = PuddleState.Staged;
            puddle.Step = step;
            puddle.StateTime = 0f;
            int stage = PuddleStageOrder[step];
            puddle.Cell.AcidPuddleDamage = PuddleStageDamage[stage];
            puddle.Renderer.sprite = puddleSprites[puddle.Variant][stage][0];

            Vector3 centre = puddle.Cell.WorldPosition;
            int droplets = 2 + stage;
            for (int s = 0; s < droplets; s++) EmitSpray(centre, 0.8f);
            if (step == 0 || stage == PuddleStages - 1) EmitFume(centre);
        }

        private static void BeginDrying(Puddle puddle)
        {
            puddle.State = PuddleState.Drying;
            puddle.StateTime = 0f;
            if (puddle.Cell != null) puddle.Cell.AcidPuddleDamage = 0;
        }

        private void TrySpawnPuddle()
        {
            Puddle free = null;
            for (int i = 0; i < puddles.Count; i++)
            {
                if (puddles[i].State == PuddleState.Idle) { free = puddles[i]; break; }
            }
            if (free == null || puddleCandidates.Count == 0) return;

            // A few tries for a tile without a puddle already on it
            for (int attempt = 0; attempt < 8; attempt++)
            {
                GridCell cell = puddleCandidates[Random.Range(0, puddleCandidates.Count)];
                if (HasPuddle(cell)) continue;

                free.Cell = cell;
                free.State = PuddleState.Seeping;
                free.StateTime = 0f;
                free.FumeTimer = Random.Range(0.5f, 1.2f);
                free.Variant = Random.Range(0, PuddleVariants);
                free.Transform.position = Snap(cell.WorldPosition.x, cell.WorldPosition.y);
                free.Renderer.flipX = Random.value < 0.5f;
                free.Renderer.flipY = Random.value < 0.5f;
                free.Renderer.sprite = puddleSprites[free.Variant][0][0];
                free.Renderer.color = Color.clear;
                free.Transform.gameObject.SetActive(true);
                return;
            }
        }

        private bool HasPuddle(GridCell cell)
        {
            for (int i = 0; i < puddles.Count; i++)
            {
                if (puddles[i].State != PuddleState.Idle && puddles[i].Cell == cell) return true;
            }
            return false;
        }

        // ---------- Tint and storm flashes ----------

        private void UpdateTintAndFlash(float deltaTime)
        {
            float strength = UpdateFlash(deltaTime);

            if (tint != null)
            {
                Color cast = TintColor;
                cast.a = 0.10f + 0.04f * rainIntensity + 0.10f * tickPulse + 0.10f * strength;
                tint.color = cast;
            }

            if (skyFlash != null)
            {
                Color light = FlashColor;
                light.a = 0.32f * strength;
                skyFlash.color = light;
            }
        }

        /// <summary>
        /// Runs the current flash: a faint pre-flicker, a flickering flash and a fading afterglow; sometimes a second
        /// flash follows. Returns the flash strength 0-1.
        /// </summary>
        private float UpdateFlash(float deltaTime)
        {
            if (flashElapsed < 0f)
            {
                flashTimer -= deltaTime;
                if (flashTimer > 0f) return 0f;

                // Downpours flash more often
                flashTimer = storm == Storm.Downpour ? Random.Range(9f, 18f) : Random.Range(14f, 28f);
                flashElapsed = 0f;
                flashTwice = Random.value < 0.45f;
            }

            flashElapsed += deltaTime;
            float t = flashElapsed;
            float flicker = 0.8f + 0.2f * Mathf.PerlinNoise(t * 40f, 0.5f);
            float strength;
            if (t < 0.05f) strength = 0.3f;
            else if (t < 0.1f) strength = 0.04f;
            else if (t < 0.3f) strength = flicker;
            else strength = Mathf.Exp(-(t - 0.3f) * 4.5f);

            if (flashTwice && t >= 0.5f && t < 0.62f) strength = Mathf.Max(strength, 0.75f * flicker);

            if (t >= 1.2f)
            {
                flashElapsed = -1f;
                return 0f;
            }
            return Mathf.Clamp01(strength);
        }

        // ---------- Generated sprites ----------
    }
}
