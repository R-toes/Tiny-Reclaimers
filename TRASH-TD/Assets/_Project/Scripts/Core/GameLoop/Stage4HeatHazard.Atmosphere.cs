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
    // The warm map atmosphere, drifting embers and the heat flare particles.
    public partial class Stage4HeatHazard
    {
        /// <summary>
        /// The area the ambient heat effects fill: the grid, plus the bounds artwork around it when the stage has it
        /// (so the underglow, embers and floor mist reach the scenery outside the playable field too).
        /// </summary>
        private void GetAtmosphereArea(out float width, out float height, out Vector3 center)
        {
            float cellSize = gridManager.CellSize;
            Vector2 gridMin = gridManager.GridToWorldPosition(0, 0) - new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);
            Vector2 min = gridMin;
            Vector2 max = gridMin + new Vector2(stageData.gridWidth, stageData.gridHeight) * cellSize;

            if (stageData.boundsVisualSprite != null)
            {
                float pixelsPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
                Vector2 boundsMin = gridMin + stageData.boundsTileOffset * cellSize;
                Vector2 boundsMax = boundsMin + stageData.boundsVisualSprite.rect.size / pixelsPerTile * cellSize;
                min = Vector2.Min(min, boundsMin);
                max = Vector2.Max(max, boundsMax);
            }

            width = max.x - min.x;
            height = max.y - min.y;
            center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        }

        /// <summary>
        /// Warms up the map palette with ambient furnace lighting and heat overlays.
        /// </summary>
        private void ApplyWarmMapAtmosphere()
        {
            if (gridManager == null || stageData == null) return;

            GetAtmosphereArea(out float areaWidth, out float areaHeight, out Vector3 areaCenter);

            var atmosphereRoot = new GameObject("Stage4WarmAtmosphere");
            atmosphereRoot.transform.SetParent(transform, false);

            // Vertical thermal gradient over the whole area (the bounds art when the stage has it): the strongest warm glow
            // at the bottom, where the lava is, fading smoothly to completely cool/natural over its lower 60%. One quad, so
            // there is no seam. The sprite is sized by its own bounds: it used to be scaled as if it were 1 x 1 unit, which
            // stretched its 64 gradient pixels over 64 times the map and left only a flat, near-invisible tint.
            var underglowObj = new GameObject("VerticalThermalGradient");
            underglowObj.transform.SetParent(atmosphereRoot.transform, false);
            underglowObj.transform.position = areaCenter + new Vector3(0f, 0f, -0.02f);
            Vector2 gradientSize = GetVerticalThermalGradientSprite().bounds.size;
            underglowObj.transform.localScale = new Vector3(
                (areaWidth + 2f) / Mathf.Max(0.001f, gradientSize.x),
                areaHeight / Mathf.Max(0.001f, gradientSize.y),
                1f);

            furnaceUnderglowRenderer = underglowObj.AddComponent<SpriteRenderer>();
            furnaceUnderglowRenderer.sprite = GetVerticalThermalGradientSprite();
            furnaceUnderglowRenderer.color = new Color(1f, 0.44f, 0.10f, 0.18f);
            GridSorting.PlaceOnGround(furnaceUnderglowRenderer);
            furnaceUnderglowRenderer.sortingOrder = 2; // Above map artwork (1), below units

            // Ensure base map artwork is 100% natural pure color
            var mapArtworkObj = GameObject.Find("MapArtwork");
            if (mapArtworkObj != null)
            {
                var sr = mapArtworkObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = Color.white;
                }
            }
        }

        /// <summary>
        /// Creates organic, floating glowing embers that drift lazily upwards across the map.
        /// </summary>
        private void CreateDriftingEmbers()
        {
            if (gridManager == null || stageData == null) return;

            float cellSize = gridManager.CellSize;
            GetAtmosphereArea(out float mapWidth, out float mapHeight, out Vector3 mapCenter);
            emberFlareMinX = mapCenter.x - mapWidth * 0.5f;
            emberFlareMaxX = mapCenter.x + mapWidth * 0.5f;
            emberFlareY = mapCenter.y - mapHeight * 0.42f;

            var embersRoot = new GameObject("Stage4DriftingEmbers");
            embersRoot.transform.SetParent(transform, false);

            var emberObj = new GameObject("EmberParticleSystem");
            emberObj.transform.SetParent(embersRoot.transform, false);
            // Spawn origin near the bottom of the map
            emberObj.transform.position = mapCenter + new Vector3(0f, -mapHeight * 0.42f, -0.1f);
            emberFlareZ = emberObj.transform.position.z;

            ParticleSystem ps = emberObj.AddComponent<ParticleSystem>();
            emberParticleSystem = ps;

            // Main module
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true; // Embers already active and in flight when map loads!
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.86f, 0.42f, 0.95f), new Color(1f, 0.55f, 0.16f, 0.9f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.RoundToInt(600 * Mathf.Max(1f, mapWidth / (stageData.gridWidth * cellSize)));

            // Emission
            var emission = ps.emission;
            // The emitter bar is as wide as the bounds art, so the rate grows with it: the same embers per tile as before
            float emberDensity = Mathf.Max(1f, mapWidth / (stageData.gridWidth * cellSize));
            emission.rateOverTime = NormalEmberEmissionRate * emberDensity;

            // Shape: horizontal emitter bar along the bottom of the map
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(mapWidth + 2f, 0.6f, 0.1f);

            // Velocity over lifetime: gentle upward drift with slight horizontal sway
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.14f, 0.14f);
            // All three axes must use the same curve mode (two constants), or Unity errors.
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // Hot air buffets the embers: lively swirling, plus a size/spin noise that makes each one flicker
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.45f;
            noise.damping = true;
            noise.sizeAmount = new ParticleSystem.MinMaxCurve(0.55f);
            noise.octaveCount = 2;

            // Size over lifetime: starts glowing, gently shrinks as it burns out
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f),
                new Keyframe(0.2f, 1f),
                new Keyframe(0.7f, 0.9f),
                new Keyframe(1f, 0.35f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color & Alpha over lifetime: smooth fade-in and gradual cooling decay (NO BLINKING)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.92f, 0.45f), 0.0f),  // Bright yellow-white incandescent
                    new GradientColorKey(new Color(1f, 0.65f, 0.15f), 0.35f), // Warm golden ember
                    new GradientColorKey(new Color(1f, 0.35f, 0.08f), 0.70f), // Deep fiery orange
                    new GradientColorKey(new Color(0.85f, 0.15f, 0.04f), 0.90f), // Cooling crimson
                    new GradientColorKey(new Color(0.35f, 0.04f, 0.02f), 1.0f)   // Fading ash
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),   // Soft spawn (no pop)
                    new GradientAlphaKey(0.85f, 0.15f), // Full radiant glow
                    new GradientAlphaKey(0.85f, 0.65f), // Sustained flight
                    new GradientAlphaKey(0.40f, 0.85f), // Dimming
                    new GradientAlphaKey(0.0f, 1.0f)    // Gentle fade out
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            // Renderer with soft radial glow material
            var renderer = emberObj.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 25; // Above map and tiles
            renderer.sharedMaterial = GetEmberMaterial();

            ps.Play();
        }

        // Flare embers run from white-hot to deep orange, so the burst doesn't read as one flat colour
        private static readonly Color FlareEmberHot = new Color(1f, 0.97f, 0.75f, 1f);
        private static readonly Color FlareEmberWarm = new Color(1f, 0.62f, 0.18f, 1f);

        /// <summary>The blast's big ember burst along the bottom of the map, thrown higher and wider than the idle drift.</summary>
        private void EmitHeatFlareEmbers()
        {
            if (emberParticleSystem == null) return;

            for (int i = 0; i < FlareEmberBurstCount; i++)
            {
                var ember = new ParticleSystem.EmitParams
                {
                    position = new Vector3(
                        Random.Range(emberFlareMinX, emberFlareMaxX),
                        emberFlareY + Random.Range(-0.2f, 0.35f),
                        emberFlareZ),
                    velocity = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(0.8f, 1.9f), 0f),
                    startLifetime = Random.Range(1.1f, FlareEmberMaxLifetime),
                    startSize = Random.Range(0.10f, 0.22f),
                    startColor = Color.Lerp(FlareEmberHot, FlareEmberWarm, Random.value)
                };
                emberParticleSystem.Emit(ember, 1);
            }
        }

        /// <summary>Keeps embers rising for the length of the blast, thinning out as it fades.</summary>
        private void StreamHeatFlareEmbers(float flare)
        {
            if (emberParticleSystem == null) return;

            emberStreamAccumulator += FlareEmberStreamRate * flare * Time.deltaTime;
            while (emberStreamAccumulator >= 1f)
            {
                emberStreamAccumulator -= 1f;
                var ember = new ParticleSystem.EmitParams
                {
                    position = new Vector3(
                        Random.Range(emberFlareMinX, emberFlareMaxX),
                        emberFlareY + Random.Range(-0.2f, 0.2f),
                        emberFlareZ),
                    velocity = new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.7f, 1.5f), 0f),
                    startLifetime = Random.Range(1.0f, 1.8f),
                    startSize = Random.Range(0.08f, 0.17f),
                    startColor = Color.Lerp(FlareEmberHot, FlareEmberWarm, Random.value)
                };
                emberParticleSystem.Emit(ember, 1);
            }
        }

        /// <summary>Fast spark streaks thrown up from the floor; they arc and fall back under gravity.</summary>
        private void EmitHeatFlareSparks()
        {
            if (flareSparkSystem == null) return;

            for (int i = 0; i < FlareSparkCount; i++)
            {
                var spark = new ParticleSystem.EmitParams
                {
                    position = new Vector3(
                        Random.Range(emberFlareMinX, emberFlareMaxX),
                        emberFlareY + Random.Range(-0.1f, 0.25f),
                        emberFlareZ),
                    velocity = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(2.4f, 4.6f), 0f),
                    startLifetime = Random.Range(0.6f, 1.2f),
                    startSize = Random.Range(0.035f, 0.07f),
                    startColor = Color.Lerp(FlareEmberHot, new Color(1f, 0.82f, 0.4f, 1f), Random.value)
                };
                flareSparkSystem.Emit(spark, 1);
            }
        }

        /// <summary>
        /// Steam jets out of every vent and hisses up from the floor, while ember-lit smoke rolls up along the
        /// bottom of the map behind it.
        /// </summary>
        private void EmitHeatFlareSteamAndSmoke()
        {
            if (flareSmokeSystem != null)
            {
                for (int i = 0; i < FlareSmokeCount; i++)
                {
                    var smoke = new ParticleSystem.EmitParams
                    {
                        position = new Vector3(
                            Random.Range(emberFlareMinX - 0.5f, emberFlareMaxX + 0.5f),
                            emberFlareY + Random.Range(-0.45f, 0.15f),
                            emberFlareZ),
                        velocity = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.45f, 0.9f), 0f),
                        startLifetime = Random.Range(1.8f, 2.6f),
                        startSize = Random.Range(0.9f, 1.6f),
                        rotation = Random.Range(0f, 360f),
                        startColor = new Color(0.52f, 0.44f, 0.40f, 0.45f) // light enough to see units through
                    };
                    flareSmokeSystem.Emit(smoke, 1);
                }
            }

            if (flareSteamSystem == null) return;

            for (int v = 0; v < steamVentPositions.Count; v++)
            {
                for (int i = 0; i < FlareSteamPuffsPerVent; i++)
                {
                    var puff = new ParticleSystem.EmitParams
                    {
                        position = steamVentPositions[v] + new Vector3(Random.Range(-0.08f, 0.08f), 0f, 0f),
                        velocity = new Vector3(Random.Range(-0.35f, 0.35f), Random.Range(1.3f, 2.2f), 0f),
                        startLifetime = Random.Range(1.6f, 2.4f),
                        startSize = Random.Range(0.45f, 0.85f),
                        rotation = Random.Range(0f, 360f),
                        startColor = new Color(1f, 1f, 1f, 0.5f)
                    };
                    flareSteamSystem.Emit(puff, 1);
                }
            }

            for (int i = 0; i < FlareFloorSteamCount; i++)
            {
                var wisp = new ParticleSystem.EmitParams
                {
                    position = new Vector3(
                        Random.Range(emberFlareMinX, emberFlareMaxX),
                        emberFlareY + Random.Range(-0.1f, 0.3f),
                        emberFlareZ),
                    velocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(0.6f, 1.1f), 0f),
                    startLifetime = Random.Range(1.4f, 2.1f),
                    startSize = Random.Range(0.35f, 0.65f),
                    rotation = Random.Range(0f, 360f),
                    startColor = new Color(1f, 0.96f, 0.92f, 0.35f)
                };
                flareSteamSystem.Emit(wisp, 1);
            }
        }

        /// <summary>
        /// The blast's own particle systems. They emit nothing on their own: EmitHeatFlare* fills them when a round
        /// heats the squad. Smoke draws behind the steam, steam behind the embers (25), sparks on top.
        /// </summary>
        private void CreateHeatFlareParticles()
        {
            var flareRoot = new GameObject("Stage4HeatFlare");
            flareRoot.transform.SetParent(transform, false);

            // Smoke: warm-lit at first (the fire under it), cooling to grey as it rises and spreads
            flareSmokeSystem = CreateBurstParticleSystem(flareRoot.transform, "FlareSmoke", GetSteamMaterial(), 23, 80);
            SetBurstSizeCurve(flareSmokeSystem, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.4f, 1.3f), new Keyframe(1f, 2.0f)));
            SetBurstColorGradient(flareSmokeSystem,
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.60f, 0.35f), 0f),
                    new GradientColorKey(new Color(0.60f, 0.58f, 0.60f), 0.45f),
                    new GradientColorKey(new Color(0.42f, 0.42f, 0.45f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0.7f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            AddBurstDrift(flareSmokeSystem, 0.3f, 0.4f, 20f);

            // Steam: bright jets that burst out fast, slow down and billow
            flareSteamSystem = CreateBurstParticleSystem(flareRoot.transform, "FlareSteam", GetSteamMaterial(), 24, 120);
            SetBurstSizeCurve(flareSteamSystem, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.25f, 1.4f), new Keyframe(1f, 3.0f)));
            SetBurstColorGradient(flareSteamSystem,
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.90f, 0.92f, 0.96f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0.6f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                });
            AddBurstDrift(flareSteamSystem, 0.35f, 0.5f, 30f);
            var steamDrag = flareSteamSystem.limitVelocityOverLifetime;
            steamDrag.enabled = true;
            steamDrag.limit = 0.45f;
            steamDrag.dampen = 0.06f;

            // Sparks: stretched along their velocity so they read as streaks, pulled back down by gravity
            flareSparkSystem = CreateBurstParticleSystem(flareRoot.transform, "FlareSparks", GetEmberMaterial(), 27, 120);
            var sparkMain = flareSparkSystem.main;
            sparkMain.gravityModifier = 0.35f;
            SetBurstColorGradient(flareSparkSystem,
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.25f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.32f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            var sparkRenderer = flareSparkSystem.GetComponent<ParticleSystemRenderer>();
            sparkRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            sparkRenderer.velocityScale = 0.07f;
            sparkRenderer.lengthScale = 1.5f;
        }

        private ParticleSystem CreateBurstParticleSystem(Transform parent, string objectName, Material material, int sortingOrder, int maxParticles)
        {
            var systemObject = new GameObject(objectName);
            systemObject.transform.SetParent(parent, false);
            systemObject.transform.position = new Vector3(0f, 0f, emberFlareZ);

            ParticleSystem ps = systemObject.AddComponent<ParticleSystem>();
            // AddComponent starts it playing; stop before changing duration-type settings
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            var emission = ps.emission;
            emission.enabled = false; // Emit() still works; nothing spawns by itself
            var shape = ps.shape;
            shape.enabled = false;

            var renderer = systemObject.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = sortingOrder;
            renderer.sharedMaterial = material;
            ps.Play();
            return ps;
        }

        private static void SetBurstSizeCurve(ParticleSystem ps, AnimationCurve curve)
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetBurstColorGradient(ParticleSystem ps, GradientColorKey[] colors, GradientAlphaKey[] alphas)
        {
            var gradient = new Gradient();
            gradient.SetKeys(colors, alphas);
            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Wandering noise plus a slow spin, for soft puffs.</summary>
        private static void AddBurstDrift(ParticleSystem ps, float noiseStrength, float noiseFrequency, float spinDegrees)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = noiseFrequency;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-spinDegrees * Mathf.Deg2Rad, spinDegrees * Mathf.Deg2Rad);
        }

        private static Material emberMaterial;
        private static Material GetEmberMaterial()
        {
            if (emberMaterial == null)
            {
                Shader shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                emberMaterial = new Material(shader);
                emberMaterial.mainTexture = CreateEmberRadialTexture();
                // Additive blending so embers glow without black backgrounds
                emberMaterial.SetFloat("_Mode", 4); // Additive
                emberMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                emberMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                emberMaterial.SetInt("_ZWrite", 0);
                emberMaterial.DisableKeyword("_ALPHATEST_ON");
                emberMaterial.EnableKeyword("_ALPHABLEND_ON");
                emberMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                emberMaterial.renderQueue = 3000;
            }
            return emberMaterial;
        }

        private static Texture2D CreateEmberRadialTexture()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            float center = (size - 1) * 0.5f;
            float maxDist = center;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / maxDist;
                    if (dist >= 1f)
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                    else
                    {
                        // Gaussian-like radial falloff with bright hot core
                        float alpha = Mathf.Pow(1f - dist, 2.0f);
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Sprite verticalThermalGradientSprite;
        private static Sprite GetVerticalThermalGradientSprite()
        {
            if (verticalThermalGradientSprite == null)
            {
                const int h = 128;
                var tex = new Texture2D(2, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;
                Color[] colors = new Color[2 * h];
                for (int y = 0; y < h; y++)
                {
                    float t = (float)y / (h - 1); // 0 at bottom, 1 at top
                    // Strong warm glow at bottom, easing out smoothly — top ~40% is completely cool
                    float u = Mathf.Clamp01(t / 0.60f);
                    float alpha = 1f - u * u * (3f - 2f * u);
                    Color c = new Color(1f, 1f, 1f, alpha);
                    colors[y * 2] = c;
                    colors[y * 2 + 1] = c;
                }
                tex.SetPixels(colors);
                tex.Apply();
                verticalThermalGradientSprite = Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), 1f);
            }
            return verticalThermalGradientSprite;
        }
    }
}
