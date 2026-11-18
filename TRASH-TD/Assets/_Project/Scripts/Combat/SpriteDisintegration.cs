using UnityEngine;
using UnityEngine.Rendering;

namespace TrashTD.Combat
{
    /// <summary>
    /// Crumbles a snapshot of a sprite into jumbled pixels. It lives on its own object, so the unit it copies can be
    /// destroyed at once and gameplay is not delayed. Runs on scaled time: it follows game speed and freezes while paused.
    /// </summary>
    public sealed class SpriteDisintegration : MonoBehaviour
    {
        private const float Duration = 1.1f;
        private const float StepSeconds = 1f / 15f;
        private const int ChunkSize = 2;
        private const byte MinimumAlpha = 40;

        // How much of a chunk's release time is random; the rest makes the sprite crumble from the top down.
        private const float ReleaseRandomness = 0.6f;

        private Color32[] pixels;
        private float[] thresholds;
        private bool[] released;
        private int width;
        private int height;
        private int chunksX;
        private int chunksY;
        private Vector2 pivot;
        private float pixelsPerUnit;
        private float elapsed;
        private float stepTimer;
        private bool flipX;
        private Texture2D texture;
        private Sprite snapshot;

        /// <summary>
        /// Starts the effect from the frame the renderer is showing. Does nothing outside play mode, and skips the
        /// effect if the sprite cannot be read, so it can never interrupt the caller.
        /// </summary>
        public static void Play(SpriteRenderer source, bool grayscale = false)
        {
            if (!Application.isPlaying || source == null || source.sprite == null) return;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;

            Color32[] read = ReadSprite(source.sprite, grayscale, out int w, out int h);
            if (read == null) return;

            var go = new GameObject("SpriteDisintegration");
            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;
            go.AddComponent<SpriteDisintegration>().Begin(source, read, w, h);
        }

        private void Begin(SpriteRenderer source, Color32[] read, int w, int h)
        {
            pixels = read;
            width = w;
            height = h;
            flipX = source.flipX;
            pivot = source.sprite.pivot;
            pixelsPerUnit = source.sprite.pixelsPerUnit;

            texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false);
            snapshot = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(pivot.x / w, pivot.y / h),
                pixelsPerUnit, 0, SpriteMeshType.FullRect);

            var view = gameObject.AddComponent<SpriteRenderer>();
            view.sprite = snapshot;
            view.flipX = source.flipX;
            view.flipY = source.flipY;

            // Units are ordered per tile by the SortingGroup on the unit, not by their renderer.
            SortingGroup group = source.GetComponentInParent<SortingGroup>();
            view.sortingLayerID = group != null ? group.sortingLayerID : source.sortingLayerID;
            view.sortingOrder = group != null ? group.sortingOrder : source.sortingOrder;

            chunksX = Mathf.CeilToInt(w / (float)ChunkSize);
            chunksY = Mathf.CeilToInt(h / (float)ChunkSize);
            thresholds = new float[chunksX * chunksY];
            released = new bool[thresholds.Length];
            for (int y = 0; y < chunksY; y++)
            {
                float fromTop = 1f - (y + 0.5f) / chunksY;
                for (int x = 0; x < chunksX; x++)
                {
                    thresholds[y * chunksX + x] = Mathf.Lerp(fromTop, Random.value, ReleaseRandomness);
                }
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            stepTimer += Time.deltaTime;
            if (stepTimer < StepSeconds) return;
            stepTimer = 0f;

            float progress = Mathf.Clamp01(elapsed / Duration);
            Scramble(progress);
            ReleaseChunks(progress);
            texture.SetPixels32(pixels);
            texture.Apply(false);

            if (progress >= 1f) Destroy(gameObject);
        }

        /// <summary>Swaps nearby solid pixels, so the body looks more jumbled as it goes.</summary>
        private void Scramble(float progress)
        {
            int swaps = Mathf.RoundToInt(Mathf.Lerp(2f, 24f, progress));
            int radius = 1 + Mathf.RoundToInt(progress * 3f);
            for (int i = 0; i < swaps; i++)
            {
                int x = Random.Range(0, width);
                int y = Random.Range(0, height);
                int otherX = x + Random.Range(-radius, radius + 1);
                int otherY = y + Random.Range(-radius, radius + 1);
                if (otherX < 0 || otherY < 0 || otherX >= width || otherY >= height) continue;

                // Solid pixels only: filling a released chunk would leave a pixel nothing ever removes.
                int a = y * width + x;
                int b = otherY * width + otherX;
                if (pixels[a].a == 0 || pixels[b].a == 0) continue;
                (pixels[a], pixels[b]) = (pixels[b], pixels[a]);
            }
        }

        private void ReleaseChunks(float progress)
        {
            for (int chunkY = 0; chunkY < chunksY; chunkY++)
            {
                for (int chunkX = 0; chunkX < chunksX; chunkX++)
                {
                    int chunk = chunkY * chunksX + chunkX;
                    if (released[chunk] || thresholds[chunk] > progress) continue;
                    released[chunk] = true;
                    ReleaseChunk(chunkX * ChunkSize, chunkY * ChunkSize);
                }
            }
        }

        private void ReleaseChunk(int startX, int startY)
        {
            Color32 color = default;
            bool hasPixel = false;
            int endX = Mathf.Min(startX + ChunkSize, width);
            int endY = Mathf.Min(startY + ChunkSize, height);
            for (int y = startY; y < endY; y++)
            {
                for (int x = startX; x < endX; x++)
                {
                    int index = y * width + x;
                    if (pixels[index].a == 0) continue;
                    if (!hasPixel) color = pixels[index];
                    hasPixel = true;
                    pixels[index] = default;
                }
            }

            if (hasPixel) EmitPixel(startX + ChunkSize * 0.5f, startY + ChunkSize * 0.5f, color);
        }

        private void EmitPixel(float x, float y, Color color)
        {
            var local = new Vector2((x - pivot.x) / pixelsPerUnit, (y - pivot.y) / pixelsPerUnit);
            if (flipX) local.x = -local.x;

            PixelParticles.Emit(new PixelParticle
            {
                Position = transform.TransformPoint(local),
                Velocity = new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.05f, 0.4f)),
                Life = Random.Range(0.4f, 0.75f),
                Gravity = 1.1f,
                Drag = 1.5f,
                StartColor = color,
                EndColor = new Color(color.r, color.g, color.b, 0f),
                StartScale = ChunkSize,
                EndScale = 1f,
                Sprite = CombatFxArt.Dot1()
            });
        }

        /// <summary>
        /// The sprite's full rect as pixels. Goes through a render texture because most unit textures are not
        /// CPU-readable. Returns null if that fails.
        /// </summary>
        private static Color32[] ReadSprite(Sprite sprite, bool grayscale, out int w, out int h)
        {
            w = Mathf.RoundToInt(sprite.rect.width);
            h = Mathf.RoundToInt(sprite.rect.height);

            Texture2D source = sprite.texture;
            Rect rect = sprite.textureRect;
            int readWidth = Mathf.RoundToInt(rect.width);
            int readHeight = Mathf.RoundToInt(rect.height);
            if (source == null || w <= 0 || h <= 0 || readWidth <= 0 || readHeight <= 0) return null;

            RenderTexture previous = RenderTexture.active;
            RenderTexture temp = null;
            Texture2D readback = null;
            try
            {
                // sRGB, so the colours read back match the art.
                temp = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, temp);
                RenderTexture.active = temp;

                readback = new Texture2D(readWidth, readHeight, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(rect.x, rect.y, readWidth, readHeight), 0, 0);
                Color32[] read = readback.GetPixels32();

                // A tightly packed sprite is trimmed: its pixels sit at an offset inside the full sprite rect.
                int offsetX = Mathf.RoundToInt(sprite.textureRectOffset.x);
                int offsetY = Mathf.RoundToInt(sprite.textureRectOffset.y);
                var result = new Color32[w * h];
                for (int y = 0; y < readHeight; y++)
                {
                    for (int x = 0; x < readWidth; x++)
                    {
                        int targetX = x + offsetX;
                        int targetY = y + offsetY;
                        Color32 color = read[y * readWidth + x];
                        if (targetX < 0 || targetY < 0 || targetX >= w || targetY >= h || color.a <= MinimumAlpha) continue;

                        if (grayscale)
                        {
                            byte grey = (byte)((color.r * 77 + color.g * 150 + color.b * 29) >> 8);
                            color = new Color32(grey, grey, grey, color.a);
                        }
                        result[targetY * w + targetX] = color;
                    }
                }
                return result;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpriteDisintegration] Could not read {sprite.name}, skipping the effect: {ex.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (temp != null) RenderTexture.ReleaseTemporary(temp);
                if (readback != null) Destroy(readback);
            }
        }

        private void OnDestroy()
        {
            if (snapshot != null) Destroy(snapshot);
            if (texture != null) Destroy(texture);
        }
    }
}
