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
    // Drawing the placement preview: range cells, operator ghost and facing marker.
    public partial class StageBootstrapper
    {
        private void RefreshPlacementPreview()
        {
            ClearPlacementPreviewVisuals();
            if (!isPlacementPreviewActive || pendingDeployCard == null || gridManager == null) return;

            GridCell targetCell = gridManager.GetCell(placementGridPosition);
            if (targetCell == null) return;

            bool squadFull = operatorManager == null || operatorManager.IsAtSquadLimit;
            bool canDeploy = targetCell.CanDeploy(pendingDeployCard.operatorData.position) && !squadFull;

            // Talent activation range layer (orange, lower opacity)
            Vector2Int[] talentPattern = pendingDeployCard.operatorData.GetTalentRangePattern();
            if (talentPattern != null && talentPattern.Length > 0)
            {
                GridCell[] talentCells = gridManager.GetCellsInRange(
                    placementGridPosition,
                    talentPattern,
                    placementFacing);
                for (int i = 0; i < talentCells.Length; i++)
                {
                    DrawTalentCell(talentCells[i].WorldPosition, TalentRangeColor);
                }
            }

            // Attack range layer (green)
            Vector2Int[] effectiveRange = pendingDeployCard.operatorData != null
                ? pendingDeployCard.operatorData.GetRangePatternForRarity(pendingDeployCard.rarity)
                : null;
            if (effectiveRange != null)
            {
                GridCell[] rangeCells = gridManager.GetCellsInRange(
                    placementGridPosition,
                    effectiveRange,
                    placementFacing);
                Color rangeColor = new Color(0.15f, 1f, 0.28f, 1f);
                for (int i = 0; i < rangeCells.Length; i++)
                {
                    DrawCellOutline(rangeCells[i].WorldPosition, rangeColor);
                }
            }

            Color placementColor = canDeploy ? new Color(0.15f, 0.85f, 1f, 1f) : new Color(1f, 0.12f, 0.12f, 1f);
            DrawCellOutline(targetCell.WorldPosition, placementColor);
            DrawOperatorPreview(targetCell.WorldPosition, pendingDeployCard.operatorData, placementFacing, canDeploy);
            DrawFacingMarker(targetCell.WorldPosition, placementFacing);
            UpdatePlacementControls(targetCell.WorldPosition, canDeploy);
        }

        /// <summary>
        /// The HUD controls around the previewed tile for the player's scheme (ControlSettings): arrows for Precise
        /// and Quick, plus PLACE / CANCEL for Precise. Swipe has none. Hidden while the operator is being dragged.
        /// </summary>
        private void UpdatePlacementControls(Vector3 tileCenter, bool canDeploy)
        {
            if (gameplayHudUI == null) return;
            PlacementControlScheme scheme = ControlSettings.Scheme;
            bool show = scheme != PlacementControlScheme.Swipe && !isDraggingToDeploy && !isMovingPreview;
            gameplayHudUI.SetPlacementControls(show, canDeploy, scheme == PlacementControlScheme.Precise, placementFacing);
            if (show && gridManager != null) gameplayHudUI.SetPlacementControlsPosition(tileCenter, gridManager.CellSize);
        }

        private void DrawOperatorPreview(Vector3 position, OperatorData opData, OperatorFacing facing, bool canDeploy)
        {
            if (opData == null) return;

            Sprite previewSprite = GetOperatorPreviewSprite(opData);
            if (previewSprite == null) return;

            GameObject previewObject = new GameObject("OperatorPlacementSpritePreview");
            previewObject.transform.SetParent(transform, false);
            previewObject.transform.position = new Vector3(position.x, position.y, 0f);

            Vector3 scale = opData.operatorPrefab != null ? opData.operatorPrefab.transform.localScale : Vector3.one;
            previewObject.transform.localScale = scale;

            SpriteRenderer sr = previewObject.AddComponent<SpriteRenderer>();
            sr.sprite = previewSprite;
            if (opData.operatorPrefab != null)
            {
                SpriteRenderer prefabSr = opData.operatorPrefab.GetComponent<SpriteRenderer>()
                    ?? opData.operatorPrefab.GetComponentInChildren<SpriteRenderer>();
                if (prefabSr != null && prefabSr.sharedMaterial != null)
                {
                    sr.sharedMaterial = prefabSr.sharedMaterial;
                }
            }

            sr.flipX = (facing == OperatorFacing.Right);
            sr.sortingOrder = 7;
            sr.color = canDeploy
                ? new Color(1f, 1f, 1f, 0.88f)
                : new Color(1f, 0.35f, 0.35f, 0.65f);

            placementPreviewVisuals.Add(previewObject);
        }

        private Sprite GetOperatorPreviewSprite(OperatorData opData)
        {
            if (opData == null) return null;

            if (opData.operatorPrefab != null)
            {
                SpriteRenderer prefabSr = opData.operatorPrefab.GetComponent<SpriteRenderer>();
                if (prefabSr == null)
                {
                    prefabSr = opData.operatorPrefab.GetComponentInChildren<SpriteRenderer>();
                }
                if (prefabSr != null && prefabSr.sprite != null)
                {
                    return prefabSr.sprite;
                }
            }

            return opData.portrait;
        }

        private static Sprite sharedCellFillSprite;

        private Sprite GetCellFillSprite()
        {
            if (sharedCellFillSprite != null) return sharedCellFillSprite;

            Texture2D texture = new Texture2D(2, 2);
            Color[] pixels = new Color[] { Color.white, Color.white, Color.white, Color.white };
            texture.SetPixels(pixels);
            texture.Apply();

            sharedCellFillSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f),
                2f);
            return sharedCellFillSprite;
        }

        private void DrawTalentCell(Vector3 center, Color color)
        {
            DrawCellFill(center, new Color(color.r, color.g, color.b, 0.16f), 47);
            DrawCellOutline(center, color, 48);
        }

        private void DrawCellFill(Vector3 center, Color color, int sortingOrder = 47)
        {
            if (gridManager == null) return;
            GameObject fillObject = new GameObject("TalentRangeFill");
            fillObject.transform.SetParent(transform, false);
            fillObject.transform.position = new Vector3(center.x, center.y, 0.06f);
            float size = gridManager.CellSize * 0.92f;
            fillObject.transform.localScale = new Vector3(size, size, 1f);

            SpriteRenderer sr = fillObject.AddComponent<SpriteRenderer>();
            sr.sprite = GetCellFillSprite();
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            placementPreviewVisuals.Add(fillObject);
        }

        private void DrawCellOutline(Vector3 center, Color color, int sortingOrder = 50)
        {
            float halfSize = gridManager.CellSize * 0.46f;
            center.z = 0.05f;
            Vector3[] corners =
            {
                center + new Vector3(-halfSize, -halfSize, 0f),
                center + new Vector3(-halfSize, halfSize, 0f),
                center + new Vector3(halfSize, halfSize, 0f),
                center + new Vector3(halfSize, -halfSize, 0f)
            };

            float alpha = color.a > 0f ? color.a : 1f;
            CreatePreviewLine("PlacementOutlineGlow", gridManager.CellSize * 0.14f, new Color(color.r, color.g, color.b, 0.28f * alpha), corners, true, sortingOrder);
            CreatePreviewLine("PlacementOutline", gridManager.CellSize * 0.045f, color, corners, true, sortingOrder);
        }

        private void DrawFacingMarker(Vector3 center, OperatorFacing activeFacing)
        {
            if (gridManager == null) return;
            float cellSize = gridManager.CellSize;
            OperatorFacing[] allFacings = { OperatorFacing.Right, OperatorFacing.Up, OperatorFacing.Left, OperatorFacing.Down };

            foreach (OperatorFacing f in allFacings)
            {
                bool isSelected = f == activeFacing;
                Vector2 forward = f switch
                {
                    OperatorFacing.Up => Vector2.up,
                    OperatorFacing.Left => Vector2.left,
                    OperatorFacing.Down => Vector2.down,
                    _ => Vector2.right
                };
                Vector2 side = new Vector2(-forward.y, forward.x);

                float distMultiplier = isSelected ? 0.44f : 0.38f;
                float arrowSize = isSelected ? 0.16f : 0.10f;
                float lineWidth = isSelected ? cellSize * 0.065f : cellSize * 0.035f;
                Color arrowColor = isSelected ? new Color(0.15f, 0.95f, 1f, 1f) : new Color(1f, 1f, 1f, 0.35f);

                Vector3 tip = center + (Vector3)(forward * cellSize * distMultiplier);
                Vector3 baseCenter = center + (Vector3)(forward * cellSize * (distMultiplier - arrowSize));
                Vector3[] arrow =
                {
                    tip,
                    baseCenter + (Vector3)(side * cellSize * (arrowSize * 0.75f)),
                    baseCenter - (Vector3)(side * cellSize * (arrowSize * 0.75f))
                };
                for (int i = 0; i < arrow.Length; i++) arrow[i].z = 0.06f;

                CreatePreviewLine(isSelected ? "ActiveFacingMarker" : "InactiveFacingMarker", lineWidth, arrowColor, arrow, true);

                if (isSelected)
                {
                    Vector3 stemStart = center + (Vector3)(forward * cellSize * 0.12f);
                    stemStart.z = 0.06f;
                    Vector3 stemEnd = baseCenter;
                    stemEnd.z = 0.06f;
                    CreatePreviewLine("ActiveFacingStem", lineWidth * 0.8f, arrowColor, new[] { stemStart, stemEnd }, false);
                }
            }
        }

        private void CreatePreviewLine(string objectName, float width, Color color, Vector3[] points, bool closed, int sortingOrder = 50)
        {
            GameObject lineObject = new GameObject(objectName);
            lineObject.transform.SetParent(transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = closed;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCapVertices = 2;
            line.material = GetPlacementPreviewMaterial();
            line.sortingOrder = sortingOrder;
            placementPreviewVisuals.Add(lineObject);
        }

        private Material GetPlacementPreviewMaterial()
        {
            if (placementPreviewMaterial != null) return placementPreviewMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            placementPreviewMaterial = new Material(shader);
            return placementPreviewMaterial;
        }

        private void ClearPlacementPreviewVisuals()
        {
            for (int i = 0; i < placementPreviewVisuals.Count; i++)
            {
                if (placementPreviewVisuals[i] != null) Destroy(placementPreviewVisuals[i]);
            }
            placementPreviewVisuals.Clear();
        }

        private void HidePlacementPreview()
        {
            isPlacementPreviewActive = false;
            isMovingPreview = false;
            isOperatorRangePreviewActive = false;
            ClearPlacementPreviewVisuals();
            gameplayHudUI?.SetPlacementControls(false);
            gameplayHudUI?.SetSelectedDeckSlot(-1);
        }
    }
}
