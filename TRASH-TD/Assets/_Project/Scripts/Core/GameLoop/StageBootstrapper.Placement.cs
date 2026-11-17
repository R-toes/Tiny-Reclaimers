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
    // Deck and operator placement: dragging, facing, confirming and cancelling.
    public partial class StageBootstrapper
    {
        private void OnDestroy()
        {
            if (playerDeck != null)
            {
                playerDeck.OnCardSelectedForDeployment -= HandleCardSelectedForDeployment;
                playerDeck.OnDeckChanged -= HandleDeckChanged;
            }
            if (operatorManager != null)
            {
                operatorManager.OnOperatorSelected -= HandleOperatorSelected;
                operatorManager.OnOperatorRetreated -= HandleOperatorRetreated;
            }

            ClearPlacementPreviewVisuals();
            if (placementPreviewMaterial != null)
            {
                Destroy(placementPreviewMaterial);
            }

            foreach (SpriteSheetAnimationState animation in backgroundAnimations)
            {
                foreach (Sprite frame in animation.Frames)
                {
                    if (frame != null)
                        Destroy(frame);
                }
            }

            CleanupBorderDarkening();
        }

        private void HandleDeckChanged(IReadOnlyList<DraftCard> deck)
        {
            if (pendingDeployCard == null) return;

            for (int i = 0; i < deck.Count; i++)
            {
                if (ReferenceEquals(deck[i], pendingDeployCard)) return;
            }

            pendingDeployCard = null;
            HidePlacementPreview();
        }

        private void HandleCardSelectedForDeployment(DraftCard card)
        {
            if (isPlacementPreviewActive && !ReferenceEquals(pendingDeployCard, card))
            {
                HidePlacementPreview();
            }

            pendingDeployCard = card;
            if (!isPlacementPreviewActive) DrawDeployableTileHints(card);
            Debug.Log($"[Draft] Selected: {card.operatorData.operatorName} ({card.rarity}). Select a tile, choose a facing, then confirm placement.");
        }

        /// <summary>A card was clicked (not yet on a tile): faint outlines on every free tile it can go on.</summary>
        private void DrawDeployableTileHints(DraftCard card)
        {
            ClearPlacementPreviewVisuals();
            if (card == null || card.operatorData == null || gridManager == null || stageData == null || !IsDeploymentAllowed) return;

            Color hint = new Color(0.15f, 0.85f, 1f, 0.35f);
            for (int y = 0; y < stageData.gridHeight; y++)
            {
                for (int x = 0; x < stageData.gridWidth; x++)
                {
                    GridCell cell = gridManager.GetCell(x, y);
                    if (cell != null && cell.CanDeploy(card.operatorData.position)) DrawCellOutline(cell.WorldPosition, hint);
                }
            }
        }

        public void BeginOperatorPlacement(DraftCard card, Vector2Int gridPosition)
        {
            if (card == null || card.operatorData == null || gridManager == null ||
                gameManager == null || !IsDeploymentAllowed ||
                !gridManager.IsInBounds(gridPosition)) return;

            GridCell targetCell = gridManager.GetCell(gridPosition);
            if (targetCell == null) return;

            bool continuingPlacement = isPlacementPreviewActive && ReferenceEquals(pendingDeployCard, card);
            if (continuingPlacement && placementGridPosition == gridPosition) return;

            if (!continuingPlacement)
            {
                operatorManager?.SelectOperator(null);
                isOperatorRangePreviewActive = false;
                ClearPlacementPreviewVisuals();
            }

            pendingDeployCard = card;
            placementGridPosition = gridPosition;
            sweepAnchorWorldPosition = targetCell.WorldPosition;
            if (!continuingPlacement) placementFacing = OperatorFacing.Right;
            isPlacementPreviewActive = true;
            isDraggingToDeploy = false;
            RefreshPlacementPreview();
        }

        /// <summary>
        /// Updates placement during card dragging. Moving across the map updates the hovered tile,
        /// and dragging away from the tile center sweeps the operator's facing direction.
        /// </summary>
        public void UpdateOperatorPlacementDrag(DraftCard card, Vector2Int gridPosition, Vector3 worldPosition)
        {
            if (card == null || card.operatorData == null || gridManager == null ||
                gameManager == null || !IsDeploymentAllowed ||
                !gridManager.IsInBounds(gridPosition)) return;

            GridCell targetCell = gridManager.GetCell(gridPosition);
            if (targetCell == null) return;

            bool isNewTile = !isPlacementPreviewActive || !ReferenceEquals(pendingDeployCard, card) || placementGridPosition != gridPosition;

            if (isNewTile)
            {
                operatorManager?.SelectOperator(null);
                isOperatorRangePreviewActive = false;
                pendingDeployCard = card;
                placementGridPosition = gridPosition;
                sweepAnchorWorldPosition = targetCell.WorldPosition;
                isPlacementPreviewActive = true;
                RefreshPlacementPreview();
            }

            Vector2 displacement = (Vector2)(worldPosition - targetCell.WorldPosition);
            float minSweepDistance = gridManager.CellSize * 0.20f;
            if (displacement.sqrMagnitude >= minSweepDistance * minSweepDistance)
            {
                OperatorFacing newFacing = CalculateFacingFromDisplacement(displacement, placementFacing);
                if (newFacing != placementFacing)
                {
                    placementFacing = newFacing;
                    RefreshPlacementPreview();
                }
            }
        }

        public void SetPlacementFacing(OperatorFacing facing)
        {
            if (!isPlacementPreviewActive || placementFacing == facing) return;

            placementFacing = facing;
            RefreshPlacementPreview();
        }

        /// <summary>
        /// A facing arrow (HUD button or WASD/arrow key) in the arrow schemes. Precise sets the facing and waits for
        /// PLACE; Quick sets it and places the operator at once. Swipe uses keys only to set the facing.
        /// </summary>
        public void ChoosePlacementFacing(OperatorFacing facing)
        {
            if (!isPlacementPreviewActive || pendingDeployCard == null) return;

            placementFacing = facing;
            if (ControlSettings.Scheme == PlacementControlScheme.Quick)
            {
                ConfirmOperatorPlacement();
                return;
            }
            RefreshPlacementPreview();
        }

        /// <summary>Redraws the placement preview and its controls (the HUD calls it when a dragged card is dropped).</summary>
        public void RefreshPlacement()
        {
            if (isPlacementPreviewActive) RefreshPlacementPreview();
        }

        private static bool UsesArrowControls => ControlSettings.Scheme != PlacementControlScheme.Swipe;

        public void ConfirmOperatorPlacement()
        {
            if (!isPlacementPreviewActive || pendingDeployCard == null) return;

            isDraggingToDeploy = false;

            if (pendingDeployCard.cooldownRoundsRemaining > 0)
            {
                gameplayHudUI?.ShowCooldownWarning(
                    pendingDeployCard.operatorData != null ? pendingDeployCard.operatorData.operatorName : null,
                    pendingDeployCard.cooldownRoundsRemaining);
                CancelOperatorPlacement();
                return;
            }

            if (operatorManager == null || (!IsSandbox && operatorManager.IsAtSquadLimit))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiDenied);
                RefreshPlacementPreview();
                return;
            }

            GridCell targetCell = gridManager != null ? gridManager.GetCell(placementGridPosition) : null;
            if (targetCell == null || !targetCell.CanDeploy(pendingDeployCard.operatorData.position))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiDenied);
                RefreshPlacementPreview();
                return;
            }

            DraftCard deployedCard = pendingDeployCard;
            if (operatorManager == null || !operatorManager.TryDeployOperator(
                    deployedCard,
                    placementGridPosition,
                    placementFacing,
                    out var deployedOp))
            {
                Debug.LogWarning($"Cannot deploy {deployedCard.operatorData.operatorName} here.");
                AudioManager.Instance?.PlaySfx(SfxId.UiDenied);
                return;
            }

            playerDeck?.RemoveCard(deployedCard);
            pendingDeployCard = null;
            HidePlacementPreview();
            Debug.Log($"<color=green>Deployed {deployedCard.operatorData.operatorName} at ({placementGridPosition.x}, {placementGridPosition.y}) facing {placementFacing}!</color>");

            // Framiliar exception: open its menu immediately upon deployment
            if (deployedOp is TrashTD.Operators.FramiliarOperator deployedFram && !deployedFram.IsCopied)
            {
                operatorManager.SelectOperator(deployedFram);
            }
        }

        public void CancelOperatorPlacement()
        {
            pendingDeployCard = null;
            isDraggingToDeploy = false;
            isMovingPreview = false;
            HidePlacementPreview();
        }

        public void CancelOperatorPlacementPreview(DraftCard card)
        {
            if (isPlacementPreviewActive && ReferenceEquals(pendingDeployCard, card))
            {
                isDraggingToDeploy = false;
                HidePlacementPreview();
            }
        }

        /// <summary>A deck card is being placed (preview, pending card or facing drag); an operator selection doesn't count.</summary>
        public bool IsPlacementActive => isPlacementPreviewActive || pendingDeployCard != null || isDraggingToDeploy;

        public bool IsPlacementOrSelectionActive()
        {
            return isPlacementPreviewActive || pendingDeployCard != null || isDraggingToDeploy ||
                   (operatorManager != null && operatorManager.SelectedOperator != null);
        }

        public void CancelPlacementOrSelection()
        {
            if (isPlacementPreviewActive || pendingDeployCard != null || isDraggingToDeploy)
            {
                CancelOperatorPlacement();
            }
            else if (operatorManager != null && operatorManager.SelectedOperator != null)
            {
                operatorManager.SelectOperator(null);
                ClearOperatorRangePreview();
            }
        }
    }
}
