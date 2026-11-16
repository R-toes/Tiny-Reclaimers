using System;
using System.Collections.Generic;
using UnityEngine;
using TrashTD.Core.Grid;
using TrashTD.Data;
using Random = UnityEngine.Random;

namespace TrashTD.Core.GameLoop
{
    // Generated sprites and textures for the acid rain effects.
    public partial class Stage6AcidRainVisuals
    {
        private Sprite Track(Sprite sprite)
        {
            generatedSprites.Add(sprite);
            return sprite;
        }

        /// <summary>A point-filtered pixel-art sprite at 32 px per tile (draw it at localScale = cellSize).</summary>
        private static Sprite MakeSprite(Color[] pixels, int width, int height, string name, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), pivot, ArtPixelsPerTile, 0, SpriteMeshType.FullRect);
        }

        private void CreateSprites()
        {
            whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            streakSprite = Track(CreateStreakSprite());
            skyFlashSprite = Track(CreateSkyFlashSprite());

            fogSprites = new Sprite[FogSizes.Length];
            for (int i = 0; i < FogSizes.Length; i++)
                fogSprites[i] = Track(CreateFogSprite(FogSizes[i].x, FogSizes[i].y, 6100 + i * 613, 1f));

            fumeSprites = new Sprite[FumeSizes.Length];
            for (int i = 0; i < FumeSizes.Length; i++)
                fumeSprites[i] = Track(CreateFogSprite(FumeSizes[i].x, FumeSizes[i].y, 6900 + i * 211, 1.15f));

            ringSprites = new[] { Track(CreateRingSprite(3)), Track(CreateRingSprite(5)), Track(CreateRingSprite(7)) };

            puddleSprites = new Sprite[PuddleVariants][][];
            for (int v = 0; v < PuddleVariants; v++)
            {
                puddleSprites[v] = new Sprite[PuddleStages][];
                for (int s = 0; s < PuddleStages; s++)
                {
                    puddleSprites[v][s] = new Sprite[PuddleFrames];
                    for (int f = 0; f < PuddleFrames; f++)
                        puddleSprites[v][s][f] = Track(CreatePuddleSprite(v, s, f));
                }
            }
        }

        /// <summary>A 1x8 streak: a faint tail at the top brightening to a solid head at the bottom.</summary>
        private static Sprite CreateStreakSprite()
        {
            const int height = 8;
            var pixels = new Color[height];
            for (int y = 0; y < height; y++)
            {
                float fromHead = (float)y / (height - 1); // 0 at the bottom (head), 1 at the top (tail)
                pixels[y] = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.12f, fromHead));
            }
            return MakeSprite(pixels, 1, height, "Stage6 Acid Streak", new Vector2(0.5f, 0f));
        }

        /// <summary>A sky glow for the storm flash: full at the top, fading out by the middle (dithered bands).</summary>
        private static Sprite CreateSkyFlashSprite()
        {
            const int width = 16;
            const int height = 64;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height; // 0 bottom, 1 top
                float alpha = Mathf.Clamp01((v - 0.35f) / 0.65f);
                alpha = alpha * alpha;
                for (int x = 0; x < width; x++)
                {
                    // An ordered dither between bands keeps the falloff from turning into blocks on the CRT
                    float threshold = ((x & 1) * 2 + (y & 1)) / 4f * 0.12f;
                    pixels[y * width + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha + threshold - 0.06f));
                }
            }
            return MakeSprite(pixels, width, height, "Stage6 Sky Flash", new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// A rounded, billowing pixel-art fog cloud. Lumpy body eroded by noise into puffs, then quantised into a
        /// solid core, a lighter body, a checker dither and a sparse dither at the edge (no soft gradient, so it
        /// can't turn into flat translucent rectangles). Lit from above: top surfaces light, undersides shadow.
        /// </summary>
        private static Sprite CreateFogSprite(int width, int height, int seed, float lumpScale)
        {
            var random = new System.Random(seed);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            var lumps = new Vector4[6];
            for (int i = 0; i < lumps.Length; i++)
            {
                lumps[i] = new Vector4(Range(0.22f, 0.78f), Range(0.35f, 0.62f),
                    Range(0.13f, 0.24f) * lumpScale, Range(0.22f, 0.34f) * lumpScale);
            }
            float ox = Range(0f, 100f);
            float oy = Range(0f, 100f);

            var density = new float[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float v = (y + 0.5f) / height;

                    float shape = 0f;
                    for (int i = 0; i < lumps.Length; i++)
                    {
                        float dx = (u - lumps[i].x) / lumps[i].z;
                        float dy = (v - lumps[i].y) / lumps[i].w;
                        shape += Mathf.Exp(-(dx * dx + dy * dy) * 1.6f);
                    }
                    float body = 1f - Mathf.Exp(-shape * 1.25f);

                    // Round, billowy noise (not wind-stretched like the Stage 3 sand)
                    float billow = Mathf.PerlinNoise(ox + u * 4f, oy + v * 3f) * 0.6f +
                                   Mathf.PerlinNoise(ox + u * 9f + 13f, oy + v * 7f + 5f) * 0.4f;
                    float d = body * Mathf.Lerp(0.6f, 1.15f, billow);

                    float edgeX = Mathf.Min(u, 1f - u) * 2f;
                    float edgeY = Mathf.Min(v, 1f - v) * 2f;
                    d *= Mathf.SmoothStep(0f, 1f, Mathf.Min(edgeX * 3f, 1f)) * Mathf.SmoothStep(0f, 1f, Mathf.Min(edgeY * 3f, 1f));
                    density[x, y] = d;
                }
            }

            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float d = density[x, y];
                    float alpha;
                    if (d > 0.62f) alpha = 0.85f;
                    else if (d > 0.44f) alpha = 0.68f;
                    else if (d > 0.3f) alpha = (x + y) % 2 == 0 ? 0.55f : 0f;
                    else if (d > 0.2f) alpha = x % 2 == 0 && y % 2 == 0 ? 0.4f : 0f;
                    else alpha = 0f;

                    if (alpha <= 0f)
                    {
                        pixels[y * width + x] = Color.clear;
                        continue;
                    }

                    float above = density[x, Mathf.Min(height - 1, y + 2)];
                    float below = density[x, Mathf.Max(0, y - 2)];
                    Color tone = d - above > 0.06f ? FogLight : below < d - 0.06f ? FogShadow : FogMid;
                    tone.a = alpha;
                    pixels[y * width + x] = tone;
                }
            }
            return MakeSprite(pixels, width, height, $"Stage6 Fog {width}x{height}", new Vector2(0.5f, 0.5f));
        }

        /// <summary>A pixel ring of the given size, for bubbles (tinted the pool's highlight colour).</summary>
        private static Sprite CreateRingSprite(int size)
        {
            var pixels = new Color[size * size];
            float centre = (size - 1) * 0.5f;
            float outer = centre + 0.35f;
            float inner = Mathf.Max(0.2f, centre - 0.85f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - centre) * (x - centre) + (y - centre) * (y - centre));
                    Color color = Color.clear;
                    if (d <= outer && d > inner) color = Color.white;                    // rim
                    else if (d <= inner) color = new Color(1f, 1f, 1f, 0.25f);          // thin film
                    pixels[y * size + x] = color;
                }
            }
            return MakeSprite(pixels, size, size, $"Stage6 Bubble {size}", new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// One 32x32 puddle in the map pool's style: a flat lime blob with a 1 px olive edge and a few pale bubble
        /// arcs (".hh." over "h..h", like the painted pool). The shape is a set of metaballs (PuddleShapes) scaled by
        /// the stage, with a little noise on the edge so it steps like hand-placed pixels; it is never square.
        /// The two frames move the arcs, so a sitting puddle shimmers.
        /// </summary>
        private static Sprite CreatePuddleSprite(int variant, int stage, int frame)
        {
            const int size = ArtPixelsPerTile;
            Vector3[] balls = PuddleShapes[variant];
            float scale = PuddleStageScale[stage];
            float centre = (size - 1) * 0.5f;
            var random = new System.Random(9100 + variant * 131);
            float ox = (float)random.NextDouble() * 50f;
            float oy = (float)random.NextDouble() * 50f;

            var inside = new bool[size, size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float field = 0f;
                    for (int i = 0; i < balls.Length; i++)
                    {
                        float dx = x - (centre + balls[i].x * scale);
                        float dy = y - (centre + balls[i].y * scale);
                        float r = balls[i].z * scale;
                        // (r/d)^4: a sharp falloff, so the shape follows its lobes instead of swelling into one blob
                        float q = r * r / Mathf.Max(0.25f, dx * dx + dy * dy);
                        field += q * q;
                    }
                    // Same noise at every stage, so the edge keeps its character as the puddle grows
                    float wobble = (Mathf.PerlinNoise(ox + x * 0.3f, oy + y * 0.3f) - 0.5f) * 0.6f;
                    inside[x, y] = field + wobble >= 1f && x > 0 && y > 0 && x < size - 1 && y < size - 1;
                }
            }

            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!inside[x, y]) { pixels[y * size + x] = Color.clear; continue; }
                    bool edge = !inside[x - 1, y] || !inside[x + 1, y] || !inside[x, y - 1] || !inside[x, y + 1];
                    pixels[y * size + x] = edge ? PoolEdge : PoolBody;
                }
            }

            // Bubble arcs: more on bigger puddles, placed only where the whole 4x3 patch is body
            var arcRandom = new System.Random(9300 + variant * 53 + stage * 7 + frame * 997);
            int arcs = 1 + stage;
            for (int attempt = 0; attempt < 60 && arcs > 0; attempt++)
            {
                int ax = arcRandom.Next(1, size - 5);
                int ay = arcRandom.Next(1, size - 4);
                if (!PatchIsBody(pixels, size, ax - 1, ay - 1, 6, 4)) continue;
                // Bottom row "h..h", top row ".hh." (y grows upwards)
                pixels[ay * size + ax] = PoolHighlight;
                pixels[ay * size + ax + 3] = PoolHighlight;
                pixels[(ay + 1) * size + ax + 1] = PoolHighlight;
                pixels[(ay + 1) * size + ax + 2] = PoolHighlight;
                arcs--;
            }

            return MakeSprite(pixels, size, size, $"Stage6 Puddle v{variant} s{stage} f{frame}", new Vector2(0.5f, 0.5f));
        }

        private static bool PatchIsBody(Color[] pixels, int size, int x0, int y0, int width, int height)
        {
            for (int y = y0; y < y0 + height; y++)
            {
                for (int x = x0; x < x0 + width; x++)
                {
                    if (x < 0 || y < 0 || x >= size || y >= size) return false;
                    if (pixels[y * size + x] != PoolBody) return false;
                }
            }
            return true;
        }
    }
}
