using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TrashTD.Audio;
using TrashTD.Core.Grid;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using TrashTD.UI;
using TrashTD.Development;

namespace TrashTD.Core.GameLoop
{
    // Drawing the map artwork layers, background animations and centering the camera.
    public partial class StageBootstrapper
    {
        private void SpawnVisualGridTiles()
        {
            GameObject gridContainer = new GameObject("VisualGrid");
            gridContainer.transform.SetParent(transform);

            float cellSize = gridManager.CellSize;
            Vector3 gridBottomLeft = gridManager.GridToWorldPosition(0, 0) - new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0f);
            Vector3 targetBottomLeft = gridBottomLeft + new Vector3(stageData.visualTileOffset.x * cellSize, stageData.visualTileOffset.y * cellSize, 0f);

            // The scenery around the playable map. Normally it sits behind everything (the map art covers its middle);
            // when it is the extended ground layer itself (Stage 3) it takes the map artwork's place above the background.
            bool boundsReplaceMap = stageData.boundsVisualSprite != null && stageData.boundsReplacesMapArtwork;
            if (stageData.boundsVisualSprite != null)
            {
                Vector3 boundsBottomLeft = gridBottomLeft + new Vector3(stageData.boundsTileOffset.x * cellSize, stageData.boundsTileOffset.y * cellSize, 0f);
                DrawMapArtworkLayer(gridContainer.transform, stageData.boundsVisualSprite, "BoundsArtwork", boundsReplaceMap ? 1 : -1, boundsBottomLeft, false);
            }

            DrawMapArtworkLayer(
                gridContainer.transform,
                stageData.backgroundVisualSprite,
                "BackgroundArtwork",
                0,
                targetBottomLeft + new Vector3(stageData.backgroundTileOffset.x * cellSize, stageData.backgroundTileOffset.y * cellSize, 0f),
                true,
                Environment == StageEnvironment.Sandstorm);

            Sprite mapSprite = stageData.mapVisualSprite;
            if (!boundsReplaceMap) DrawMapArtworkLayer(gridContainer.transform, mapSprite, "MapArtwork", 1, targetBottomLeft, false);

            if (stageData.upperBackgroundVisualSprites != null)
            {
                for (int i = 0; i < stageData.upperBackgroundVisualSprites.Length; i++)
                {
                    DrawMapArtworkLayer(
                        gridContainer.transform,
                        stageData.upperBackgroundVisualSprites[i],
                        $"UpperBackgroundArtwork_{i}",
                        2,
                        targetBottomLeft,
                        true);
                }
            }

            if (stageData.midgroundVisualSprites != null)
            {
                for (int i = 0; i < stageData.midgroundVisualSprites.Length; i++)
                {
                    DrawMapArtworkLayer(
                        gridContainer.transform,
                        stageData.midgroundVisualSprites[i],
                        $"MidgroundArtwork_{i}",
                        GridSpriteSorter.AirBandStart / 2,
                        targetBottomLeft,
                        false,
                        onGround: false,
                        sortingLayerId: GridSorting.UnitsLayerId);
                }
            }

            Sprite fgSprite = stageData.foregroundVisualSprite;
            if (fgSprite != null)
            {
                DrawMapArtworkLayer(
                    gridContainer.transform,
                    fgSprite,
                    "ForegroundArtwork",
                    20000,
                    targetBottomLeft,
                    false,
                    onGround: false,
                    sortingLayerId: GridSorting.DefaultLayerId);
            }

            for (int y = 0; y < stageData.gridHeight; y++)
            {
                for (int x = 0; x < stageData.gridWidth; x++)
                {
                    TileType type = stageData.GetTile(x, y);
                    Vector3 worldPos = gridManager.GridToWorldPosition(x, y);

                    GameObject tileObj = new GameObject($"Tile_{x}_{y}_{type}");
                    tileObj.transform.position = worldPos;
                    tileObj.transform.SetParent(gridContainer.transform);

                    // The tutorial draws its own floor (TutorialStageVisuals)
                    if (mapSprite == null && !IsTutorial)
                    {
                        var sr = tileObj.AddComponent<SpriteRenderer>();
                        sr.sprite = GetSpriteForTile(type);
                        sr.sortingLayerID = GridSorting.GroundLayerId;
                        sr.sortingOrder = 0;
                    }

                    // Add box collider for click-to-deploy detection
                    var col = tileObj.AddComponent<BoxCollider2D>();
                    col.size = Vector2.one * gridManager.CellSize;
                }
            }
        }

        private void DrawMapArtworkLayer(
            Transform parent,
            Sprite sprite,
            string objectName,
            int sortingOrder,
            Vector3 targetBottomLeft,
            bool alignToSourceRect,
            bool animateAsSpriteSheet = false,
            bool onGround = true,
            int? sortingLayerId = null)
        {
            if (sprite == null) return;

            GameObject artworkObject = new GameObject(objectName);
            artworkObject.transform.SetParent(parent);

            var spriteRenderer = artworkObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            if (sortingLayerId.HasValue)
            {
                spriteRenderer.sortingLayerID = sortingLayerId.Value;
            }
            else if (onGround)
            {
                GridSorting.PlaceOnGround(spriteRenderer);
            }
            spriteRenderer.sortingOrder = sortingOrder;

            if (animateAsSpriteSheet)
            {
                CreateSpriteSheetAnimation(
                    spriteRenderer,
                    sprite,
                    Stage3BackgroundAnimationFrameDuration);
            }

            Sprite placedSprite = spriteRenderer.sprite;
            float pixelPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
            float spriteUnitsPerTile = pixelPerTile / placedSprite.pixelsPerUnit;
            float tileScale = gridManager.CellSize / spriteUnitsPerTile;
            Vector3 artworkScale = new Vector3(tileScale, tileScale, 1f);
            artworkObject.transform.localScale = artworkScale;

            Vector3 sourceOffset = alignToSourceRect
                ? new Vector3(placedSprite.rect.x, placedSprite.rect.y, 0f) * (gridManager.CellSize / pixelPerTile)
                : Vector3.zero;
            artworkObject.transform.position = targetBottomLeft + sourceOffset
                - Vector3.Scale(placedSprite.bounds.min, artworkScale);
        }

        private void CreateSpriteSheetAnimation(SpriteRenderer spriteRenderer, Sprite sourceSprite, float frameDuration)
        {
            Texture2D texture = sourceSprite.texture;
            int frameWidth = stageData != null && stageData.backgroundFrameSize.x > 0 ? stageData.backgroundFrameSize.x : Stage3BackgroundFrameWidth;
            int frameHeight = stageData != null && stageData.backgroundFrameSize.y > 0 ? stageData.backgroundFrameSize.y : Stage3BackgroundFrameHeight;
            if (!texture.isReadable ||
                texture.width % frameWidth != 0 ||
                texture.height != frameHeight ||
                frameDuration <= 0f)
            {
                Debug.LogError(
                    $"Stage 3 background animation requires a readable sheet of {frameWidth}x{frameHeight} frames and a positive frame duration.",
                    this);
                return;
            }

            int frameCount = texture.width / frameWidth;
            Vector2 pivot = new Vector2(
                sourceSprite.pivot.x / sourceSprite.rect.width,
                sourceSprite.pivot.y / sourceSprite.rect.height);
            Sprite[] frames = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = Sprite.Create(
                    texture,
                    new Rect(
                        i * frameWidth,
                        0f,
                        frameWidth,
                        frameHeight),
                    pivot,
                    sourceSprite.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect,
                    sourceSprite.border);
            }

            backgroundAnimations.Add(new SpriteSheetAnimationState
            {
                Renderer = spriteRenderer,
                Frames = frames,
                FrameDuration = frameDuration
            });
            spriteRenderer.sprite = frames[0];
        }

        private Sprite GetSpriteForTile(TileType type)
        {
            return type switch
            {
                TileType.LowGround => lowGroundSprite,
                TileType.HighGround => highGroundSprite,
                TileType.Blocked => blockedSprite,
                TileType.EnemyPath => lowGroundSprite,
                TileType.SpawnPoint => spawnPointSprite,
                TileType.ExitPoint => exitPointSprite,
                TileType.Trap => lowGroundSprite,
                TileType.Hole => lowGroundSprite,
                _ => lowGroundSprite
            };
        }

        /// <summary>
        /// The camera's home orthographic size. It shows the whole visual map between the HUD bars (the deck bar and the top
        /// bar leave about 76% of the screen height; never smaller than 4.10). When the stage has bounds artwork the home
        /// view is also capped to fit inside it, in width and in height (the part under the HUD bars counts: it isn't fully
        /// opaque), so no void shows; the cap is skipped when the playable grid itself would not fit between the bars. Every
        /// stage with bounds art follows this same rule (1, 3, 4, 5, 6). Pure, so the rule can be tested.
        /// </summary>
        public static float CalculateHomeOrthoSize(float visualHeight, float gridHeight, bool hasBounds, float boundsWidth, float boundsHeight, float aspect)
        {
            // Available vertical ratio between DeckBar (170px) and TopBar (90px) on 1080p reference (~76%)
            const float availableRatio = 0.759f;
            float requiredWorldHeight = (visualHeight + 0.35f) / availableRatio;
            float orthoSize = Mathf.Max(4.10f, requiredWorldHeight * 0.5f);

            if (hasBounds && aspect > 0f)
            {
                float maxOrthoForBounds = Mathf.Min(boundsWidth / (2f * aspect), boundsHeight * 0.5f);
                float minOrthoForGrid = (gridHeight + 0.35f) / (availableRatio * 2f);
                if (maxOrthoForBounds >= minOrthoForGrid)
                {
                    orthoSize = Mathf.Min(orthoSize, maxOrthoForBounds);
                }
            }

            return orthoSize;
        }

        private void CenterCameraOnGrid()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float cellSize = gridManager != null ? gridManager.CellSize : 1.0f;
            float centerX = (stageData.gridWidth * cellSize) * 0.5f;

            // Visual height: grid height by default, but if mapVisualSprite has extra visual rows (e.g. top/bottom rows),
            // take the full visual height and vertical span into account so background visuals are fully visible and centered.
            float visualMinY = 0f;
            float visualMaxY = stageData.gridHeight * cellSize;
            float visualMinX = 0f;
            float visualMaxX = stageData.gridWidth * cellSize;
            if (stageData.mapVisualSprite != null)
            {
                float pixelPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
                float spriteTilesY = stageData.mapVisualSprite.rect.height / pixelPerTile;
                float spriteBottom = stageData.visualTileOffset.y * cellSize;
                float spriteTop = (spriteTilesY + stageData.visualTileOffset.y) * cellSize;
                visualMinY = Mathf.Min(visualMinY, spriteBottom);
                visualMaxY = Mathf.Max(visualMaxY, spriteTop);

                float spriteTilesX = stageData.mapVisualSprite.rect.width / pixelPerTile;
                visualMinX = Mathf.Min(visualMinX, stageData.visualTileOffset.x * cellSize);
                visualMaxX = Mathf.Max(visualMaxX, (spriteTilesX + stageData.visualTileOffset.x) * cellSize);
            }
            float visualHeight = visualMaxY - visualMinY;
            float visualCenterY = (visualMinY + visualMaxY) * 0.5f;

            // The home framing follows the playable map; the camera may roam anywhere over the bounds artwork too
            float cameraMinX = visualMinX, cameraMaxX = visualMaxX, cameraMinY = visualMinY, cameraMaxY = visualMaxY;
            if (stageData.boundsVisualSprite != null)
            {
                float boundsPixelPerTile = stageData.visualTilePixelSize > 0 ? stageData.visualTilePixelSize : 32f;
                Rect boundsRect = stageData.boundsVisualSprite.rect;
                float boundsLeft = stageData.boundsTileOffset.x * cellSize;
                float boundsBottom = stageData.boundsTileOffset.y * cellSize;
                cameraMinX = Mathf.Min(cameraMinX, boundsLeft);
                cameraMinY = Mathf.Min(cameraMinY, boundsBottom);
                cameraMaxX = Mathf.Max(cameraMaxX, boundsLeft + boundsRect.width / boundsPixelPerTile * cellSize);
                cameraMaxY = Mathf.Max(cameraMaxY, boundsBottom + boundsRect.height / boundsPixelPerTile * cellSize);
            }

            float orthoSize = CalculateHomeOrthoSize(visualHeight, stageData.gridHeight * cellSize,
                stageData.boundsVisualSprite != null, cameraMaxX - cameraMinX, cameraMaxY - cameraMinY, cam.aspect);

            // Shift camera down by the difference between bottom DeckBar (170px) and TopBar (90px)
            // so visual content is vertically centered in the unobstructed play area
            float hudCenterOffset = ((170f - 90f) / 1080f) * orthoSize;
            float camY = visualCenterY - hudCenterOffset;

            cam.transform.position = new Vector3(centerX, camY, -10f);
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;

            // Look-around, zoom and shake on top of this framing
            var cameraController = cam.GetComponent<GameplayCameraController>();
            if (cameraController == null) cameraController = cam.gameObject.AddComponent<GameplayCameraController>();
            cameraController.Initialize(cam, new Vector2(centerX, camY), orthoSize,
                Rect.MinMaxRect(cameraMinX, cameraMinY, cameraMaxX, cameraMaxY), stageData.boundsVisualSprite != null);
        }
    }
}
