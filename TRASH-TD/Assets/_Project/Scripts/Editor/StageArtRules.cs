using UnityEngine;
using TrashTD.Data;

namespace TrashTD.Editor
{
    /// <summary>The verdict on a piece of stage art: whether the Stage Maker accepts it, and a sentence saying why (or what it fits).</summary>
    public readonly struct ArtCheck
    {
        public readonly bool ok;
        public readonly string message;

        public ArtCheck(bool ok, string message)
        {
            this.ok = ok;
            this.message = message;
        }
    }

    /// <summary>
    /// The size rules for a stage's artwork, so art that does not line up with the grid is refused instead of silently
    /// misaligning the tiles. Pure maths on pixel sizes (no Sprite needed), used by the Stage Maker's art panel and its checks.
    ///
    /// - Stage (map) art is drawn over the grid, one tile = StageData.visualTilePixelSize pixels (32). By default it must be
    ///   EXACTLY the grid's size (width x tile px by height x tile px). Several existing maps have extra rows or columns of
    ///   artwork, placed with StageData.visualTileOffset, so a bigger image is accepted only when "allow larger" is on and the
    ///   image still covers every grid tile at the current offset.
    /// - Bounds art is the scenery around the map. It must be a whole number of tiles, at least as large as the grid, and contain
    ///   the whole grid at StageData.boundsTileOffset. New bounds art is centred on the map (the same offset every existing
    ///   stage uses: minus half of the extra tiles).
    /// </summary>
    public static class StageArtRules
    {
        private const float Epsilon = 0.001f;

        public static int TilePixels(StageData stage)
        {
            return stage != null && stage.visualTilePixelSize > 0 ? stage.visualTilePixelSize : 32;
        }

        /// <summary>The exact pixel size map art needs for this stage's grid.</summary>
        public static Vector2Int ExactMapPixels(StageData stage)
        {
            int tile = TilePixels(stage);
            return new Vector2Int(stage.gridWidth * tile, stage.gridHeight * tile);
        }

        public static ArtCheck CheckMapArt(int artWidth, int artHeight, int tilePixels, int gridWidth, int gridHeight, Vector2 offset, bool allowLarger)
        {
            if (gridWidth <= 0 || gridHeight <= 0) return new ArtCheck(false, "Create a grid first, then add the art.");
            if (tilePixels <= 0) return new ArtCheck(false, "The tile pixel size must be above zero.");
            if (artWidth % tilePixels != 0 || artHeight % tilePixels != 0)
                return new ArtCheck(false, "Stage art must be a whole number of tiles, but " + artWidth + "x" + artHeight + " px is not a multiple of " + tilePixels + " px.");

            int exactWidth = gridWidth * tilePixels;
            int exactHeight = gridHeight * tilePixels;
            if (artWidth == exactWidth && artHeight == exactHeight)
                return new ArtCheck(true, "Exact fit for the " + gridWidth + "x" + gridHeight + " grid.");

            if (!allowLarger)
            {
                return new ArtCheck(false,
                    "Stage art must be exactly " + exactWidth + "x" + exactHeight + " px for a " + gridWidth + "x" + gridHeight + " grid, but this is " +
                    artWidth + "x" + artHeight + " px. If it has extra rows or columns on purpose, tick \"Allow art larger than the grid\".");
            }

            int tilesWide = artWidth / tilePixels;
            int tilesHigh = artHeight / tilePixels;
            bool covers = offset.x <= Epsilon && offset.y <= Epsilon &&
                          offset.x + tilesWide >= gridWidth - Epsilon && offset.y + tilesHigh >= gridHeight - Epsilon;
            if (!covers)
            {
                return new ArtCheck(false,
                    "At offset (" + offset.x + ", " + offset.y + ") this " + tilesWide + "x" + tilesHigh + " tile art does not cover the whole " + gridWidth + "x" + gridHeight +
                    " grid. Change the offset, or use art at least " + exactWidth + "x" + exactHeight + " px.");
            }

            int extraColumns = tilesWide - gridWidth;
            int extraRows = tilesHigh - gridHeight;
            return new ArtCheck(true, "Covers the grid with " + extraColumns + " extra column" + (extraColumns == 1 ? "" : "s") + " and " + extraRows + " extra row" + (extraRows == 1 ? "" : "s") + ".");
        }

        public static ArtCheck CheckBoundsArt(int artWidth, int artHeight, int tilePixels, int gridWidth, int gridHeight, Vector2 offset)
        {
            if (gridWidth <= 0 || gridHeight <= 0) return new ArtCheck(false, "Create a grid first, then add the art.");
            if (tilePixels <= 0) return new ArtCheck(false, "The tile pixel size must be above zero.");
            if (artWidth % tilePixels != 0 || artHeight % tilePixels != 0)
                return new ArtCheck(false, "Bounds art must be a whole number of tiles, but " + artWidth + "x" + artHeight + " px is not a multiple of " + tilePixels + " px.");

            int tilesWide = artWidth / tilePixels;
            int tilesHigh = artHeight / tilePixels;
            if (tilesWide < gridWidth || tilesHigh < gridHeight)
            {
                return new ArtCheck(false,
                    "Bounds art is the scenery around the map, so it must be at least as big as the grid: " + gridWidth * tilePixels + "x" + gridHeight * tilePixels +
                    " px or more, but this is " + artWidth + "x" + artHeight + " px.");
            }

            bool contains = offset.x <= Epsilon && offset.y <= Epsilon &&
                            offset.x + tilesWide >= gridWidth - Epsilon && offset.y + tilesHigh >= gridHeight - Epsilon;
            if (!contains)
            {
                return new ArtCheck(false,
                    "At offset (" + offset.x + ", " + offset.y + ") the bounds art does not contain the whole grid. Use the Centre button, or an offset that puts the grid inside it.");
            }

            float left = -offset.x;
            float below = -offset.y;
            float right = offset.x + tilesWide - gridWidth;
            float above = offset.y + tilesHigh - gridHeight;
            return new ArtCheck(true, "Scenery around the map: " + left + " left, " + right + " right, " + below + " below, " + above + " above (tiles).");
        }

        /// <summary>The offset that puts the grid in the middle of the bounds art (half of the extra tiles, rounded down; what every existing stage uses).</summary>
        public static Vector2 CentredBoundsOffset(int artWidth, int artHeight, int tilePixels, int gridWidth, int gridHeight)
        {
            if (tilePixels <= 0) return Vector2.zero;
            int extraColumns = artWidth / tilePixels - gridWidth;
            int extraRows = artHeight / tilePixels - gridHeight;
            return new Vector2(-Mathf.Floor(extraColumns / 2f), -Mathf.Floor(extraRows / 2f));
        }
    }
}
