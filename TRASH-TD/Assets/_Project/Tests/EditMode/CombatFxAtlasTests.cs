using NUnit.Framework;
using UnityEngine;
using TrashTD.Combat;

namespace TrashTD.Tests
{
    /// <summary>The generated combat sprites live on shared atlas pages, with their pixels and padding intact.</summary>
    public class CombatFxAtlasTests
    {
        private static Color Distinct(int i) => Color.HSVToRGB((i * 0.137f) % 1f, 0.8f, 0.9f);

        [Test]
        public void SmallSprites_ShareOneAtlasTexture()
        {
            Sprite pea = CombatFxArt.Pea(Distinct(1));
            Sprite orb = CombatFxArt.Orb(Distinct(2));
            Sprite bolt = CombatFxArt.Bolt(Distinct(3));

            Assert.IsNotNull(pea.texture);
            Assert.AreSame(pea.texture, orb.texture, "different sprites should be on the same page");
            Assert.AreSame(pea.texture, bolt.texture);
            Assert.AreEqual(CombatFxArt.AtlasSize, pea.texture.width);
        }

        [Test]
        public void PackedSprite_KeepsItsSizeScaleNameAndCache()
        {
            Color color = Distinct(4);
            Sprite orb = CombatFxArt.Orb(color); // 9 x 9 texels

            Assert.AreEqual(9f, orb.textureRect.width);
            Assert.AreEqual(9f, orb.textureRect.height);
            Assert.AreEqual(CombatFxArt.PixelsPerUnit, orb.pixelsPerUnit);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), new Vector2(orb.pivot.x / 9f, orb.pivot.y / 9f));
            StringAssert.Contains("orb", orb.name);
            Assert.AreSame(orb, CombatFxArt.Orb(color), "the same shape and colour is built once");
        }

        [Test]
        public void PackedSprite_HasTransparentPaddingAroundIt_SoNeighboursNeverBleedIn()
        {
            // Many sprites in a row, so some sit next to each other on a shelf
            var sprites = new Sprite[12];
            for (int i = 0; i < sprites.Length; i++) sprites[i] = CombatFxArt.Diamond(Distinct(10 + i));

            foreach (Sprite sprite in sprites)
            {
                Texture2D page = sprite.texture;
                Rect r = sprite.textureRect;
                int x0 = Mathf.RoundToInt(r.x) - CombatFxArt.AtlasPadding;
                int y0 = Mathf.RoundToInt(r.y) - CombatFxArt.AtlasPadding;
                int x1 = Mathf.RoundToInt(r.xMax) + CombatFxArt.AtlasPadding - 1;
                int y1 = Mathf.RoundToInt(r.yMax) + CombatFxArt.AtlasPadding - 1;
                Assert.GreaterOrEqual(x0, 0);
                Assert.GreaterOrEqual(y0, 0);
                Assert.Less(x1, page.width);
                Assert.Less(y1, page.height);

                for (int x = x0; x <= x1; x++)
                {
                    Assert.AreEqual(0f, page.GetPixel(x, y0).a, "bottom padding of " + sprite.name);
                    Assert.AreEqual(0f, page.GetPixel(x, y1).a, "top padding of " + sprite.name);
                }
                for (int y = y0; y <= y1; y++)
                {
                    Assert.AreEqual(0f, page.GetPixel(x0, y).a, "left padding of " + sprite.name);
                    Assert.AreEqual(0f, page.GetPixel(x1, y).a, "right padding of " + sprite.name);
                }
            }
        }

        [Test]
        public void PackedSprite_HasTheSamePixelsAsItsShape()
        {
            Color color = Distinct(30);
            Sprite pellet = CombatFxArt.Pellet(color); // rows "LW" / "WL": two lit corners, two others

            Texture2D page = pellet.texture;
            int x = Mathf.RoundToInt(pellet.textureRect.x);
            int y = Mathf.RoundToInt(pellet.textureRect.y);
            Color core = Color.Lerp(color, Color.white, 0.75f);
            Color light = Color.Lerp(color, Color.white, 0.35f);

            // Top row is "LW", bottom row is "WL" (the texture's y runs upward)
            AssertSameColor(light, page.GetPixel(x, y + 1), "top-left is L");
            AssertSameColor(core, page.GetPixel(x + 1, y + 1), "top-right is W");
            AssertSameColor(core, page.GetPixel(x, y), "bottom-left is W");
            AssertSameColor(light, page.GetPixel(x + 1, y), "bottom-right is L");
        }

        [Test]
        public void ManySprites_SpillOntoMorePages_AndStayValid()
        {
            // Circles of radius 60 to 79 are 121 to 159 texels wide: far too many for one 512 x 512 page
            var pages = new System.Collections.Generic.HashSet<Texture2D>();
            for (int radius = 60; radius < 80; radius++)
            {
                Sprite circle = CombatFxArt.AreaCircle(radius);
                Assert.IsNotNull(circle);
                Assert.IsNotNull(circle.texture);
                Assert.AreEqual(radius * 2 + 1, Mathf.RoundToInt(circle.textureRect.width));
                pages.Add(circle.texture);
            }
            Assert.Greater(pages.Count, 1, "20 big circles should not fit on one page");
        }

        [Test]
        public void SpriteTooBigForAPage_KeepsItsOwnTexture()
        {
            int radius = CombatFxArt.AtlasSize / 2 + 40; // wider than a page
            Sprite big = CombatFxArt.AreaCircle(radius);

            Assert.IsNotNull(big.texture);
            Assert.AreEqual(radius * 2 + 1, big.texture.width, "its own texture, not an atlas page");
            Assert.AreNotEqual(CombatFxArt.AtlasSize, big.texture.width);
        }

        private static void AssertSameColor(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, 0.01f, message + " (red)");
            Assert.AreEqual(expected.g, actual.g, 0.01f, message + " (green)");
            Assert.AreEqual(expected.b, actual.b, 0.01f, message + " (blue)");
            Assert.AreEqual(expected.a, actual.a, 0.01f, message + " (alpha)");
        }
    }
}
