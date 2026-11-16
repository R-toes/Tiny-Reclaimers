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
    // Industrial steam vents, vent embers and the floor mist.
    public partial class Stage4HeatHazard
    {
        /// <summary>
        /// Creates industrial steam vents across the factory machinery that billow upward.
        /// </summary>
        private void CreateIndustrialSteam()
        {
            if (gridManager == null || stageData == null) return;

            float cellSize = gridManager.CellSize;
            Vector3 gridBottomLeft = gridManager.GridToWorldPosition(0, 0)
                - new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);

            var steamRoot = new GameObject("Stage4IndustrialSteam");
            steamRoot.transform.SetParent(transform, false);

            // Three steam vents on the map's machinery, in tile units from the bottom-left corner: the left-hand
            // tanks at the middle of the left edge, the big cogwheel, and the bottom-right corner machinery.
            Vector2[] ventGridCoords = new Vector2[]
            {
                new Vector2(1.0f, 3.5f),
                new Vector2(5.0f, 3.0f),
                new Vector2(12.8f, 1.4f)
            };
            for (int i = 0; i < ventGridCoords.Length; i++)
            {
                Vector3 ventPos = gridBottomLeft + new Vector3(
                    ventGridCoords[i].x * cellSize,
                    ventGridCoords[i].y * cellSize,
                    -0.08f);
                steamVentPositions.Add(ventPos); // the round-start blast jets steam out of these too

                var ventObj = new GameObject($"SteamVent_{i + 1}");
                ventObj.transform.SetParent(steamRoot.transform, false);
                ventObj.transform.position = ventPos;

                ParticleSystem ps = ventObj.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(3.6f, 5.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 80;

                var emission = ps.emission;
                emission.rateOverTime = 7f;
                // A pressure hiss every few seconds, staggered between the vents
                emission.SetBursts(new[] { new ParticleSystem.Burst(i * 1.9f, (short)7, (short)11, 0, 5.5f) });

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 14f;
                shape.radius = 0.08f;
                shape.rotation = new Vector3(-90f, 0f, 0f); // Pointing upward

                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.y = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
                velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
                // All three axes must use the same curve mode (two constants), or Unity errors.
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

                var rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-25f * Mathf.Deg2Rad, 25f * Mathf.Deg2Rad);

                var sizeOverLifetime = ps.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                AnimationCurve steamSizeCurve = new AnimationCurve(
                    new Keyframe(0f, 0.45f),
                    new Keyframe(0.25f, 1.25f),
                    new Keyframe(1f, 3.1f)
                );
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, steamSizeCurve);

                var colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;
                Gradient steamGrad = new Gradient();
                steamGrad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(new Color(1f, 0.80f, 0.60f), 0f),    // lit warm by the furnace at the vent
                        new GradientColorKey(new Color(0.96f, 0.92f, 0.90f), 0.3f),
                        new GradientColorKey(new Color(0.74f, 0.77f, 0.83f), 1f)  // cooling to grey-blue as it spreads
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0f, 0f),
                        new GradientAlphaKey(0.40f, 0.12f),
                        new GradientAlphaKey(0.30f, 0.5f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(steamGrad);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.55f;
                noise.frequency = 0.65f;
                noise.scrollSpeed = 0.4f;
                noise.damping = true;

                var renderer = ventObj.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 12; // Above map and tiles, behind floating combat text
                renderer.sharedMaterial = GetSteamMaterial();

                ps.Play();
            }
        }

        /// <summary>Small, fast embers spat out of each steam vent: they shoot up, wobble in the hot air and burn out.</summary>
        private void CreateVentEmbers()
        {
            if (steamVentPositions.Count == 0) return;

            var root = new GameObject("Stage4VentEmbers");
            root.transform.SetParent(transform, false);

            for (int i = 0; i < steamVentPositions.Count; i++)
            {
                var ventObj = new GameObject($"VentEmbers_{i + 1}");
                ventObj.transform.SetParent(root.transform, false);
                ventObj.transform.position = steamVentPositions[i] + new Vector3(0f, 0.05f, -0.02f);

                ParticleSystem ps = ventObj.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.prewarm = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.085f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.7f, 1f), new Color(1f, 0.58f, 0.18f, 1f));
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.06f; // hot air lifts them
                main.maxParticles = 90;

                var emission = ps.emission;
                emission.rateOverTime = 4.5f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(i * 1.9f + 0.1f, (short)5, (short)9, 0, 5.5f) }); // with the steam hiss

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 24f;
                shape.radius = 0.07f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.8f;
                noise.frequency = 0.9f;
                noise.scrollSpeed = 0.6f;
                noise.damping = true;
                noise.sizeAmount = new ParticleSystem.MinMaxCurve(0.5f);

                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                    new Keyframe(0f, 1f), new Keyframe(0.6f, 0.8f), new Keyframe(1f, 0.1f)));

                var color = ps.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(1f, 0.98f, 0.8f), 0f),
                        new GradientColorKey(new Color(1f, 0.6f, 0.18f), 0.4f),
                        new GradientColorKey(new Color(0.9f, 0.2f, 0.05f), 1f)
                    },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f) });
                color.color = new ParticleSystem.MinMaxGradient(gradient);

                var renderer = ventObj.GetComponent<ParticleSystemRenderer>();
                renderer.sortingOrder = 26;
                renderer.sharedMaterial = GetEmberMaterial();
                ps.Play();
            }
        }

        /// <summary>A low, warm bank of mist rolling along the floor, as if the whole factory floor is sweating heat.</summary>
        private void CreateFloorMist()
        {
            if (gridManager == null || stageData == null) return;

            float cellSize = gridManager.CellSize;
            GetAtmosphereArea(out float mapWidth, out float mapHeight, out Vector3 mapCenter);

            var mistObj = new GameObject("Stage4FloorMist");
            mistObj.transform.SetParent(transform, false);
            mistObj.transform.position = mapCenter + new Vector3(0f, -mapHeight * 0.40f, -0.05f);

            ParticleSystem ps = mistObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            float mistDensity = Mathf.Max(1f, mapWidth / (stageData.gridWidth * cellSize));
            main.maxParticles = Mathf.RoundToInt(40 * mistDensity);

            var emission = ps.emission;
            emission.rateOverTime = 3.5f * mistDensity;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(mapWidth + 2f, 0.7f, 0.1f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.04f, 0.14f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-8f * Mathf.Deg2Rad, 8f * Mathf.Deg2Rad);

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.62f, 0.34f), 0f),   // lit by the furnace glow
                    new GradientColorKey(new Color(0.85f, 0.55f, 0.40f), 0.6f),
                    new GradientColorKey(new Color(0.55f, 0.45f, 0.42f), 1f)
                },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.16f, 0.25f), new GradientAlphaKey(0.12f, 0.7f), new GradientAlphaKey(0f, 1f) });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = mistObj.GetComponent<ParticleSystemRenderer>();
            renderer.sortingOrder = 11; // Under the vent steam (12), over the map and tiles
            renderer.sharedMaterial = GetSteamMaterial();
            ps.Play();
        }

        private static Material steamMaterial;
        private static Material GetSteamMaterial()
        {
            if (steamMaterial == null)
            {
                Shader shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                steamMaterial = new Material(shader);
                steamMaterial.mainTexture = CreateSteamPuffTexture();
                // Fade blending so steam is translucent without black background
                steamMaterial.SetFloat("_Mode", 2); // Fade
                steamMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                steamMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                steamMaterial.SetInt("_ZWrite", 0);
                steamMaterial.DisableKeyword("_ALPHATEST_ON");
                steamMaterial.EnableKeyword("_ALPHABLEND_ON");
                steamMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                steamMaterial.renderQueue = 3000;
            }
            return steamMaterial;
        }

        /// <summary>
        /// A soft, billowy cloud puff: a ragged round edge and uneven density from layered noise, so rotated
        /// copies of it overlap into convincing steam and smoke instead of identical round blobs.
        /// </summary>
        private static Texture2D CreateSteamPuffTexture()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // Three octaves of noise
                    float n = 0.55f * Mathf.PerlinNoise(dx * 1.7f + 4.3f, dy * 1.7f + 9.1f)
                            + 0.30f * Mathf.PerlinNoise(dx * 3.6f + 1.7f, dy * 3.6f + 5.3f)
                            + 0.15f * Mathf.PerlinNoise(dx * 7.4f + 8.2f, dy * 7.4f + 2.6f);

                    float edge = 0.80f + (n - 0.5f) * 0.55f;
                    if (r >= edge)
                    {
                        pixels[y * size + x] = Color.clear;
                        continue;
                    }

                    float falloff = Mathf.Pow(1f - r / edge, 1.35f);
                    float density = Mathf.Lerp(0.45f, 1.15f, n);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(falloff * density));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
