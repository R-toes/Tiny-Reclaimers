using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>
    /// How the Stage Maker shows each tile type: its name, a short hint, a colour and the placeholder image (the same
    /// Art/Tiles textures the game's placeholder tiles use). The acid tiles are drawn as their plain ground tile with an acid
    /// overlay on top, so acid low ground and acid high ground are easy to tell apart from each other and from plain ground.
    /// Hole has no art of its own (the game draws it with the low ground tile), so the editor draws a small placeholder for it.
    /// </summary>
    internal static class StageMakerTiles
    {
        /// <summary>The order tiles are listed in the palette (the first eight also get the number keys 1-8). Legacy EnemyPath is last: new stages use LowGround.</summary>
        public static readonly TileType[] PaletteOrder =
        {
            TileType.LowGround, TileType.HighGround, TileType.AcidLowGround, TileType.AcidHighGround,
            TileType.Blocked, TileType.SpawnPoint, TileType.ExitPoint, TileType.Hole, TileType.EnemyPath
        };

        private const string TileFolder = "Assets/_Project/Art/Tiles/";
        private static readonly Dictionary<TileType, Texture2D> Cache = new Dictionary<TileType, Texture2D>();
        private static Texture2D acidOverlay;

        public static string Label(TileType type)
        {
            switch (type)
            {
                case TileType.LowGround: return "Low ground";
                case TileType.HighGround: return "High ground";
                case TileType.Blocked: return "Blocked";
                case TileType.EnemyPath: return "Enemy path (legacy)";
                case TileType.SpawnPoint: return "Spawn";
                case TileType.ExitPoint: return "Exit";
                case TileType.AcidLowGround: return "Acid low ground";
                case TileType.AcidHighGround: return "Acid high ground";
                case TileType.Hole: return "Hole";
                default: return type.ToString();
            }
        }

        public static string Hint(TileType type)
        {
            switch (type)
            {
                case TileType.LowGround: return "Enemies walk on it; melee operators deploy on it.";
                case TileType.HighGround: return "Ranged operators deploy on it; enemies cannot walk on it.";
                case TileType.Blocked: return "Nothing walks or deploys here. Air enemies cannot cross it either.";
                case TileType.EnemyPath: return "Old name for a ground lane. Use Low ground for new stages.";
                case TileType.SpawnPoint: return "Where enemies enter. Each spawn gets a number (S0, S1, ...) that waves refer to.";
                case TileType.ExitPoint: return "The objective: enemies that reach it cost life points.";
                case TileType.AcidLowGround: return "Low ground with acid: enemies walk on it and melee operators deploy on it; every unit on it takes acid damage.";
                case TileType.AcidHighGround: return "High ground with acid: ranged operators deploy here and take acid damage. Enemies cannot walk on it.";
                case TileType.Hole: return "Ground enemies enter a hole and come out of the other one. Use exactly two.";
                default: return string.Empty;
            }
        }

        /// <summary>A short label drawn on the tile in the grid (empty for plain tiles that need none).</summary>
        public static string Badge(TileType type)
        {
            switch (type)
            {
                case TileType.AcidLowGround: return "AL";
                case TileType.AcidHighGround: return "AH";
                case TileType.Hole: return "H";
                default: return string.Empty;
            }
        }

        /// <summary>A colour for legends and overlays.</summary>
        public static Color Tint(TileType type)
        {
            switch (type)
            {
                case TileType.LowGround: return new Color(0.45f, 0.75f, 0.45f);
                case TileType.HighGround: return new Color(0.45f, 0.5f, 0.85f);
                case TileType.Blocked: return new Color(0.4f, 0.4f, 0.4f);
                case TileType.EnemyPath: return new Color(0.8f, 0.8f, 0.4f);
                case TileType.SpawnPoint: return new Color(0.9f, 0.3f, 0.3f);
                case TileType.ExitPoint: return new Color(0.3f, 0.45f, 0.95f);
                case TileType.AcidLowGround: return new Color(0.45f, 0.95f, 0.25f);
                case TileType.AcidHighGround: return new Color(0.65f, 0.9f, 0.3f);
                case TileType.Hole: return new Color(0.55f, 0.35f, 0.75f);
                default: return Color.white;
            }
        }

        /// <summary>
        /// Draws a tile's placeholder image (the acid tiles get the acid overlay over their ground tile). The caller sets
        /// GUI.color first if the tile should be see-through.
        /// </summary>
        public static void Draw(Rect rect, TileType type)
        {
            GUI.DrawTexture(rect, GetTexture(BaseTile(type)), ScaleMode.StretchToFill);
            if (TileTypeRules.IsAcid(type)) GUI.DrawTexture(rect, GetAcidOverlay(), ScaleMode.StretchToFill);
        }

        /// <summary>The tile whose picture is drawn underneath (an acid tile is its plain ground tile plus acid).</summary>
        private static TileType BaseTile(TileType type)
        {
            if (type == TileType.AcidLowGround) return TileType.LowGround;
            if (type == TileType.AcidHighGround) return TileType.HighGround;
            return type;
        }

        /// <summary>The placeholder image for a tile type. Never null (a flat colour is used if the art is missing).</summary>
        public static Texture2D GetTexture(TileType type)
        {
            if (Cache.TryGetValue(type, out Texture2D cached) && cached != null) return cached;

            Texture2D texture = null;
            string file = FileNameFor(type);
            if (file != null) texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TileFolder + file);
            if (texture == null) texture = type == TileType.Hole ? CreateHoleTexture() : CreateFlatTexture(Tint(type));

            Cache[type] = texture;
            return texture;
        }

        private static string FileNameFor(TileType type)
        {
            switch (type)
            {
                case TileType.LowGround: return "tex_tile_lowground.png";
                case TileType.HighGround: return "tex_tile_highground.png";
                case TileType.Blocked: return "tex_tile_blocked.png";
                case TileType.EnemyPath: return "tex_tile_enemypath.png";
                case TileType.SpawnPoint: return "tex_tile_spawn.png";
                case TileType.ExitPoint: return "tex_tile_exit.png";
                default: return null;
            }
        }

        private static Texture2D NewTexture(string name)
        {
            return new Texture2D(32, 32, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private static Texture2D CreateFlatTexture(Color color)
        {
            Texture2D texture = NewTexture("StageMakerFlat");
            var pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D GetAcidOverlay()
        {
            if (acidOverlay == null) acidOverlay = CreateAcidOverlay();
            return acidOverlay;
        }

        /// <summary>A see-through acid-green wash with darker pools and bright bubbles, drawn over a ground tile.</summary>
        private static Texture2D CreateAcidOverlay()
        {
            Texture2D texture = NewTexture("StageMakerAcid");
            Color wash = new Color(0.35f, 0.9f, 0.15f, 0.55f);
            Color pool = new Color(0.12f, 0.55f, 0.1f, 0.7f);
            Color bubble = new Color(0.85f, 1f, 0.5f, 0.9f);
            Vector2[] bubbles = { new Vector2(9f, 10f), new Vector2(22f, 21f), new Vector2(23f, 8f) };

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    Color color = wash;
                    // wavy darker bands so it reads as liquid
                    if (Mathf.Sin(x * 0.45f + y * 0.3f) > 0.55f) color = pool;
                    foreach (Vector2 b in bubbles)
                    {
                        float distance = Vector2.Distance(new Vector2(x, y), b);
                        if (distance < 3.2f && distance > 1.8f) color = bubble;
                    }
                    if (x < 1 || y < 1 || x > 30 || y > 30) color = new Color(0.55f, 1f, 0.3f, 0.9f);
                    texture.SetPixel(x, y, color);
                }
            }
            texture.Apply();
            return texture;
        }

        /// <summary>A dark hole with a lighter rim on a ground-coloured base.</summary>
        private static Texture2D CreateHoleTexture()
        {
            Texture2D texture = NewTexture("StageMakerHole");
            Color ground = new Color(0.28f, 0.34f, 0.28f);
            Color rim = Tint(TileType.Hole);
            Color hole = new Color(0.04f, 0.02f, 0.07f);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = x - 15.5f;
                    float dy = y - 15.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    texture.SetPixel(x, y, distance < 9f ? hole : distance < 12f ? rim : ground);
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
