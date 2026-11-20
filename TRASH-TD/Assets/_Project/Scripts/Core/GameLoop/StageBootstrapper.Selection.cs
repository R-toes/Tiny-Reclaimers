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
    // Map clicks, operator selection, deck hotkeys and quick deploy.
    public partial class StageBootstrapper
    {
        private bool TryGetGridClick(out Vector2Int gridPosition)
        {
            gridPosition = default;
            return PointerInput.PressedThisFrame && !PointerInput.IsOverUi() &&
                TryGetGridPosition(PointerInput.PicturePosition, out gridPosition) &&
                gridManager != null && gridManager.IsInBounds(gridPosition);
        }

        private void HandleOperatorMapSelection(OperatorBase clickedOperator)
        {
            if (operatorManager == null) return;

            if (clickedOperator == null || operatorManager.SelectedOperator == clickedOperator)
            {
                operatorManager.SelectOperator(null);
                ClearOperatorRangePreview();
                return;
            }

            operatorManager.SelectOperator(clickedOperator);
        }

        private void HandleOperatorSelected(OperatorBase op)
        {
            if (op == null || op.DeployedCell == null)
            {
                ClearOperatorRangePreview();
                return;
            }

            pendingDeployCard = null;
            isPlacementPreviewActive = false;
            isDraggingToDeploy = false;
            ClearPlacementPreviewVisuals();
            isOperatorRangePreviewActive = true;

            // Talent activation range layer (orange, lower opacity)
            Vector2Int[] talentPattern = op.GetTalentRangePattern();
            if (talentPattern != null && talentPattern.Length > 0)
            {
                GridCell[] talentCells = gridManager.GetCellsInRange(
                    op.DeployedCell.GridPosition,
                    talentPattern,
                    op.Facing);
                for (int i = 0; i < talentCells.Length; i++)
                {
                    DrawTalentCell(talentCells[i].WorldPosition, TalentRangeColor);
                }
            }

            // Attack range layer (green)
            GridCell[] rangeCells = gridManager.GetCellsInRange(
                op.DeployedCell.GridPosition,
                op.GetRangePattern(),
                op.Facing);
            Color rangeColor = new Color(0.15f, 1f, 0.28f, 1f);
            for (int i = 0; i < rangeCells.Length; i++)
            {
                DrawCellOutline(rangeCells[i].WorldPosition, rangeColor);
            }

            // Resicochet 2x Upgrade: Extension range from allied operators in range (yellow thick squares, like the green ones)
            if (op is ResicochetOperator resicochet && resicochet.HasSecondUpgrade)
            {
                var extensionCells = resicochet.GetExtensionRangeCells(gridManager, rangeCells);
                Color extensionColor = new Color(1f, 0.88f, 0.15f, 1f);
                for (int i = 0; i < extensionCells.Count; i++)
                {
                    DrawCellOutline(extensionCells[i].WorldPosition, extensionColor);
                }
            }

            DrawCellOutline(op.DeployedCell.WorldPosition, new Color(0.15f, 0.85f, 1f, 1f));
            Vector3 labelPosition = op.DeployedCell.WorldPosition + Vector3.up * gridManager.CellSize * 0.7f;
            gameplayHudUI?.SetSelectedOperatorName(
                op.Data.operatorName,
                op.CurrentHP,
                op.MaxHP,
                op.CurrentRarity,
                op.Data.baseRarity,
                labelPosition);
        }

        private void HandleOperatorRetreated(OperatorBase op)
        {
            ClearOperatorRangePreview();
        }

        public void ClearOperatorRangePreview()
        {
            isOperatorRangePreviewActive = false;
            ClearPlacementPreviewVisuals();
            gameplayHudUI?.SetSelectedOperatorName(string.Empty, Vector3.zero);
        }

        private int GetDeckSlotHotkey(Keyboard keyboard)
        {
            if (keyboard == null || GameplayHUDUI.IsTypingInTextField()) return -1;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 0;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 1;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 2;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 3;
            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 4;
            if (keyboard.digit6Key.wasPressedThisFrame || keyboard.numpad6Key.wasPressedThisFrame) return 5;
            if (keyboard.digit7Key.wasPressedThisFrame || keyboard.numpad7Key.wasPressedThisFrame) return 6;
            if (keyboard.digit8Key.wasPressedThisFrame || keyboard.numpad8Key.wasPressedThisFrame) return 7;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 0;
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 1;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 2;
            if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 3;
            if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) return 4;
            if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) return 5;
            if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) return 6;
            if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) return 7;
#endif

            return -1;
        }

        private void TryQuickDeployFromDeck(int slotIndex)
        {
            if (playerDeck == null || gridManager == null || operatorManager == null) return;
            if (!IsSandbox && operatorManager.IsAtSquadLimit) return;

            DraftCard card = playerDeck.GetCard(slotIndex);
            if (card == null || card.operatorData == null) return;
            if (card.cooldownRoundsRemaining > 0) return;

            // Cursor must not be over UI (e.g. HUD, buttons)
            if (PointerInput.IsOverUi()) return;

            Vector2 mouseScreen = PointerInput.PicturePosition;
            if (!TryGetGridPosition(mouseScreen, out Vector2Int cursorGridPos)) return;
            if (!gridManager.IsInBounds(cursorGridPos)) return;

            GridCell targetCell = gridManager.GetCell(cursorGridPos);
            if (targetCell == null) return;

            // Tile must be deployable for this operator's position (Melee vs Ranged) and not occupied
            if (!targetCell.CanDeploy(card.operatorData.position)) return;

            // Clear previous visuals cleanly if replacing an active preview
            if (isPlacementPreviewActive)
            {
                ClearPlacementPreviewVisuals();
            }

            // Deselect any selected operator on field
            operatorManager.SelectOperator(null);
            isOperatorRangePreviewActive = false;

            // Highlight the selected deck slot in HUD
            gameplayHudUI?.SetSelectedDeckSlot(slotIndex);

            // Put operator on the tile. Swipe goes straight to the facing sweep (move the mouse, click to place);
            // the arrow schemes show their arrows instead.
            pendingDeployCard = card;
            placementGridPosition = cursorGridPos;
            sweepAnchorWorldPosition = targetCell.WorldPosition;
            placementFacing = OperatorFacing.Right;
            isPlacementPreviewActive = true;
            isMovingPreview = false;
            isDraggingToDeploy = !UsesArrowControls;
            lastQuickDeployFrame = Time.frameCount;
            if (UsesArrowControls)
            {
                RefreshPlacementPreview();
                return;
            }

            // Calculate initial facing from displacement if cursor is slightly away from tile center
            Camera gameCamera = Camera.main;
            if (gameCamera != null && PointerInput.Available)
            {
                Vector3 mouseWorld = gameCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -gameCamera.transform.position.z));
                Vector2 displacement = (Vector2)(mouseWorld - sweepAnchorWorldPosition);
                float minSweepDist = gridManager.CellSize * 0.20f;
                if (displacement.sqrMagnitude >= minSweepDist * minSweepDist)
                {
                    placementFacing = CalculateFacingFromDisplacement(displacement, placementFacing);
                }
            }

            RefreshPlacementPreview();
        }

        private OperatorFacing GetFacingInput(Keyboard keyboard)
        {
            return TryGetFacingKey(keyboard, out OperatorFacing facing) ? facing : placementFacing;
        }

        /// <summary>A WASD / arrow key pressed this frame (even for the facing already chosen).</summary>
        private static bool TryGetFacingKey(Keyboard keyboard, out OperatorFacing facing)
        {
            facing = OperatorFacing.Right;
            if (keyboard == null || GameplayHUDUI.IsTypingInTextField()) return false;
            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame) { facing = OperatorFacing.Right; return true; }
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame) { facing = OperatorFacing.Up; return true; }
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame) { facing = OperatorFacing.Left; return true; }
            if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame) { facing = OperatorFacing.Down; return true; }
            return false;
        }

        private bool TryGetGridPosition(Vector2 screenPosition, out Vector2Int gridPosition)
        {
            gridPosition = default;
            Camera gameCamera = Camera.main;
            if (gameCamera == null || gridManager == null) return false;

            Vector3 worldPosition = gameCamera.ScreenToWorldPoint(new Vector3(
                screenPosition.x,
                screenPosition.y,
                -gameCamera.transform.position.z));
            gridPosition = gridManager.WorldToGridPosition(worldPosition);
            return true;
        }
    }
}
