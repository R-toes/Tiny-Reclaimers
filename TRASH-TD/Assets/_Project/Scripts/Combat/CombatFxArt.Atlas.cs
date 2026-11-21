using System.Collections.Generic;
using UnityEngine;

namespace TrashTD.Combat
{
    /// <summary>
    /// The atlas part of CombatFxArt: every generated effect sprite is copied from its own small texture into a shared
    /// atlas page, so the effect sprites on screen at once share a few textures and the renderer can batch them.
    /// </summary>
    public static partial class CombatFxArt
    {
        // ─────────────────────────────────────────────────────
        //  Atlas
        // ─────────────────────────────────────────────────────

        /// <summary>Side of an atlas page in texels. A sprite that does not fit (with its padding) keeps its own texture.</summary>
        public const int AtlasSize = 512;

        /// <summary>Transparent texels left around every packed sprite, so nothing next to it can show at its edge.</summary>
        public const int AtlasPadding = 1;

        /// <summary>One atlas texture, filled left to right in rows ("shelves"), each as tall as its tallest sprite.</summary>
        private sealed class AtlasPage
        {
            public Texture2D Texture;
            private int shelfX;
            private int shelfY;
            private int shelfHeight;

            /// <summary>Reserves room for a width x height sprite plus padding; false when the page is full.</summary>
            public bool TryReserve(int width, int height, out int x, out int y)
            {
                x = 0;
                y = 0;
                int paddedWidth = width + 2 * AtlasPadding;
                int paddedHeight = height + 2 * AtlasPadding;

                int nextX = shelfX, nextY = shelfY, nextHeight = shelfHeight;
                if (nextX + paddedWidth > AtlasSize)
                {
                    nextY += nextHeight; // a new row
                    nextX = 0;
                    nextHeight = 0;
                }
                if (nextY + paddedHeight > AtlasSize) return false;

                x = nextX + AtlasPadding;
                y = nextY + AtlasPadding;
                shelfX = nextX + paddedWidth;
                shelfY = nextY;
                shelfHeight = Mathf.Max(nextHeight, paddedHeight);
                return true;
            }
        }

        private static readonly List<AtlasPage> AtlasPages = new List<AtlasPage>();

        private static AtlasPage NewAtlasPage()
        {
            var texture = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false)
            {
                name = "Fx Atlas " + AtlasPages.Count,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(new Color32[AtlasSize * AtlasSize]); // all transparent
            texture.Apply(false);

            var page = new AtlasPage { Texture = texture };
            AtlasPages.Add(page);
            return page;
        }

        /// <summary>
        /// The finished texture as a sprite on a shared atlas page (the texture itself is then thrown away), or on its own
        /// texture when it is too big for a page. Pixels, pivot and scale are exactly what the standalone sprite had.
        /// </summary>
        private static Sprite ToSprite(Texture2D texture)
        {
            string spriteName = texture.name;
            int width = texture.width;
            int height = texture.height;

            Sprite sprite = null;
            if (width + 2 * AtlasPadding <= AtlasSize && height + 2 * AtlasPadding <= AtlasSize)
            {
                // Pages whose texture was destroyed (play mode ended with the static list surviving) are forgotten
                AtlasPages.RemoveAll(page => page.Texture == null);

                AtlasPage target = null;
                int x = 0, y = 0;
                foreach (AtlasPage page in AtlasPages)
                {
                    if (page.TryReserve(width, height, out x, out y))
                    {
                        target = page;
                        break;
                    }
                }
                if (target == null)
                {
                    target = NewAtlasPage();
                    target.TryReserve(width, height, out x, out y);
                }

                target.Texture.SetPixels32(x, y, width, height, texture.GetPixels32());
                target.Texture.Apply(false);
                sprite = Sprite.Create(target.Texture, new Rect(x, y, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);

                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            }
            else
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
            }

            sprite.name = spriteName;
            return sprite;
        }
    }
}
