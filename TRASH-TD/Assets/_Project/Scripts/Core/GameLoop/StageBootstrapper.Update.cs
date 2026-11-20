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
    // The per-frame update, background animation and the environment hazards (acid rain, audio, visuals).
    public partial class StageBootstrapper
    {
        private void Update()
        {
            UpdateBackgroundAnimations();
            UpdateStage6AcidRainDamage();

            if (gameManager == null || gameManager.CurrentState != GamePlayState.Playing)
            {
                if (isPlacementPreviewActive || isOperatorRangePreviewActive) HidePlacementPreview();
                return;
            }

            bool isPreparing = IsDeploymentAllowed;
            if (!isPreparing && (isPlacementPreviewActive || isDraggingToDeploy))
            {
                pendingDeployCard = null;
                isDraggingToDeploy = false;
                HidePlacementPreview();
            }

            Keyboard keyboard = Keyboard.current;
            bool hasPointer = PointerInput.Available;

            // Escape key cancels placement or deselects operator, or delegates to GameplayHUDUI
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (gameplayHudUI != null && gameplayHudUI.HasConsumedEscapeThisFrame)
                {
                    return;
                }

                if (isPlacementPreviewActive || pendingDeployCard != null || isDraggingToDeploy)
                {
                    CancelOperatorPlacement();
                    gameplayHudUI?.ConsumeEscapeKey();
                    return;
                }
                else if (operatorManager != null && operatorManager.SelectedOperator != null)
                {
                    operatorManager.SelectOperator(null);
                    ClearOperatorRangePreview();
                    gameplayHudUI?.ConsumeEscapeKey();
                    return;
                }
                else if (gameplayHudUI != null)
                {
                    gameplayHudUI.HandleEscapeKey();
                    return;
                }
            }

            // Right-click cancels active placement or undeploys the currently selected operator
            // (touch has the CANCEL and retreat buttons instead)
            if (PointerInput.SecondaryPressedThisFrame)
            {
                if (isPlacementPreviewActive || pendingDeployCard != null || isDraggingToDeploy)
                {
                    CancelOperatorPlacement();
                    return;
                }
                // Right-clicks inside a sandbox dropdown edit the queue; they mustn't retreat the selected operator
                else if (operatorManager != null && operatorManager.SelectedOperator != null &&
                         (sandboxController == null || !sandboxController.IsPointerOverDropdown))
                {
                    ClearOperatorRangePreview();
                    operatorManager.RetreatOperator(operatorManager.SelectedOperator);
                    return;
                }
            }

            // Deck slot keybinds 1 - 8: Quick-deploy card from deck to the tile under the cursor
            if (isPreparing)
            {
                int deckSlotKey = GetDeckSlotHotkey(keyboard);
                if (deckSlotKey >= 0)
                {
                    bool blockedByUi = (gameplayHudUI != null && (gameplayHudUI.IsPaused || gameplayHudUI.IsEnvironmentBriefingActive));
                    if (!blockedByUi)
                    {
                        TryQuickDeployFromDeck(deckSlotKey);
                    }
                }
            }

            // 1. Drag-and-Release sweep in progress
            if (isPreparing && isDraggingToDeploy)
            {
                Camera gameCamera = Camera.main;
                Vector2 mouseScreen = PointerInput.PicturePosition;
                Vector3 mouseWorld = (gameCamera != null && hasPointer)
                    ? gameCamera.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, -gameCamera.transform.position.z))
                    : sweepAnchorWorldPosition;

                Vector2 displacement = (Vector2)(mouseWorld - sweepAnchorWorldPosition);
                float minSweepDist = gridManager != null ? gridManager.CellSize * 0.20f : 0.2f;

                if (displacement.sqrMagnitude >= minSweepDist * minSweepDist)
                {
                    OperatorFacing sweptFacing = CalculateFacingFromDisplacement(displacement, placementFacing);
                    if (sweptFacing != placementFacing)
                    {
                        placementFacing = sweptFacing;
                        RefreshPlacementPreview();
                    }
                }

                // Keyboard facing fallback
                if (keyboard != null)
                {
                    OperatorFacing nextFacing = GetFacingInput(keyboard);
                    if (nextFacing != placementFacing)
                    {
                        placementFacing = nextFacing;
                        RefreshPlacementPreview();
                    }
                }

                // Keyboard confirm fallback
                bool confirmWithKeyboard = keyboard != null &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
                if (confirmWithKeyboard)
                {
                    isDraggingToDeploy = false;
                    ConfirmOperatorPlacement();
                    return;
                }

                // PointerUp / Click: Releasing the mouse button executes the choice, placing the operator
                if (PointerInput.ReleasedThisFrame && Time.frameCount != lastQuickDeployFrame)
                {
                    bool pointerOverUI = PointerInput.IsOverUi();
                    if (pointerOverUI)
                    {
                        CancelOperatorPlacement();
                        return;
                    }

                    isDraggingToDeploy = false;
                    ConfirmOperatorPlacement();
                    return;
                }

                return;
            }

            // 2a. Arrow schemes: the pending operator is held and dragged to another tile
            if (isPreparing && isMovingPreview && pendingDeployCard != null)
            {
                if (!PointerInput.IsPressed)
                {
                    isMovingPreview = false;
                    RefreshPlacementPreview(); // the arrows come back on the new tile
                    return;
                }

                if (TryGetGridPosition(PointerInput.PicturePosition, out Vector2Int hoveredTile) &&
                    gridManager != null && gridManager.IsInBounds(hoveredTile) && hoveredTile != placementGridPosition)
                {
                    GridCell hoveredCell = gridManager.GetCell(hoveredTile);
                    if (hoveredCell != null && hoveredCell.CanDeploy(pendingDeployCard.operatorData.position))
                        BeginOperatorPlacement(pendingDeployCard, hoveredTile); // keeps the chosen facing
                }
                return;
            }

            // 2. Pending deployment card selected from deck: Left-clicking and holding on a tile maps to PointerDown
            if (isPreparing && pendingDeployCard != null)
            {
                bool pointerOverUI = PointerInput.IsOverUi();
                if (!pointerOverUI && PointerInput.PressedThisFrame &&
                    TryGetGridPosition(PointerInput.PicturePosition, out Vector2Int clickedTile))
                {
                    bool inBounds = gridManager != null && gridManager.IsInBounds(clickedTile);
                    GridCell cell = inBounds ? gridManager.GetCell(clickedTile) : null;
                    OperatorBase clickedOperator = cell != null && cell.OccupantOperator != null
                        ? cell.OccupantOperator.GetComponent<OperatorBase>()
                        : null;

                    if (clickedOperator != null)
                    {
                        HandleOperatorMapSelection(clickedOperator);
                        return;
                    }

                    if (cell != null && cell.CanDeploy(pendingDeployCard.operatorData.position))
                    {
                        if (isPlacementPreviewActive && clickedTile == placementGridPosition)
                        {
                            if (UsesArrowControls)
                            {
                                // Precise / Quick: hold the operator to drag it to another tile
                                isMovingPreview = true;
                                RefreshPlacementPreview();
                                return;
                            }

                            // Swipe: click and hold where the operator is to drag the direction it needs to face
                            sweepAnchorWorldPosition = cell.WorldPosition;
                            isDraggingToDeploy = true;
                            return;
                        }

                        // Step 1: Place or reposition operator preview on desired tile
                        BeginOperatorPlacement(pendingDeployCard, clickedTile);
                        return;
                    }

                    // Arrow schemes: a click anywhere else on the map backs out of the placement
                    if (UsesArrowControls && isPlacementPreviewActive)
                    {
                        CancelOperatorPlacement();
                        return;
                    }
                }

                // Keyboard confirm fallback
                bool confirmWithKeyboard = keyboard != null &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame);
                if (confirmWithKeyboard && isPlacementPreviewActive)
                {
                    ConfirmOperatorPlacement();
                    return;
                }

                // Keyboard facing: works like the arrow buttons in the arrow schemes (Quick places at once)
                if (isPlacementPreviewActive && TryGetFacingKey(keyboard, out OperatorFacing keyFacing))
                {
                    if (UsesArrowControls)
                    {
                        ChoosePlacementFacing(keyFacing);
                    }
                    else if (keyFacing != placementFacing)
                    {
                        placementFacing = keyFacing;
                        RefreshPlacementPreview();
                    }
                }

                return;
            }

            // 3. Normal grid click when no card is pending deployment (select placed operator)
            if (TryGetGridClick(out Vector2Int selectedGridPosition))
            {
                GridCell clickedCell = gridManager.GetCell(selectedGridPosition);
                OperatorBase clickedOperator = clickedCell != null && clickedCell.OccupantOperator != null
                    ? clickedCell.OccupantOperator.GetComponent<OperatorBase>()
                    : null;

                HandleOperatorMapSelection(clickedOperator);
            }
        }

        private void UpdateBackgroundAnimations()
        {
            foreach (SpriteSheetAnimationState animation in backgroundAnimations)
            {
                if (animation.Renderer == null)
                    continue;

                animation.FrameTimer += Time.deltaTime;
                if (animation.FrameTimer < animation.FrameDuration)
                    continue;

                int elapsedFrames = Mathf.FloorToInt(animation.FrameTimer / animation.FrameDuration);
                animation.FrameTimer %= animation.FrameDuration;
                animation.CurrentFrame = (animation.CurrentFrame + elapsedFrames) % animation.Frames.Length;
                animation.Renderer.sprite = animation.Frames[animation.CurrentFrame];
            }
        }

        /// <summary>
        /// The Stage 6 storm (rain, fog, acid pools, flashes, and the downpour acid puddles). The rain damage stays
        /// here; the puddles form only while IsStage6AcidActive.
        /// </summary>
        private void CreateStage6AcidRain()
        {
            var weatherObject = new GameObject("Stage6AcidRain");
            weatherObject.transform.SetParent(transform, false);
            stage6AcidRainVisuals = weatherObject.AddComponent<Stage6AcidRainVisuals>();
            stage6AcidRainVisuals.Initialize(gridManager, stageData, IsStage6AcidActive);
        }

        /// <summary>The acid can hurt right now: Stage 6 hazards on, during a wave (always in the sandbox).</summary>
        private bool IsStage6AcidActive()
        {
            return Environment == StageEnvironment.AcidRain && TrashTD.Combat.StageCombatModifiers.HazardsEnabled &&
                   gameManager != null && (IsSandbox || gameManager.CurrentPhase == StagePhase.WaveActive);
        }

        private void CreateStage3SandstormVisuals()
        {
            GameObject weatherObj = new GameObject("Stage3SandstormVisuals");
            weatherObj.transform.SetParent(transform, false);
            weatherObj.AddComponent<Stage3SandstormVisuals>().Initialize(gridManager, stageData);
        }

        private void UpdateStage6AcidRainDamage()
        {
            if (Environment != StageEnvironment.AcidRain || !TrashTD.Combat.StageCombatModifiers.HazardsEnabled ||
                gameManager == null || gameManager.CurrentState != GamePlayState.Playing)
            {
                return;
            }

            // Acid rain only hurts while a wave is running (the sandbox has no waves, so it always rains there).
            // Between waves the timer restarts so nothing ticks during Card Pick / Preparation.
            if (!IsSandbox && gameManager.CurrentPhase != StagePhase.WaveActive)
            {
                weatherDamageTimer = 0f;
                return;
            }

            weatherDamageTimer += Time.deltaTime;
            while (weatherDamageTimer >= AcidRainDamageIntervalSeconds)
            {
                weatherDamageTimer -= AcidRainDamageIntervalSeconds;
                ApplyStage6AcidRainDamage();
            }
        }

        /// <summary>Stage music plus looping weather/factory ambience for the current environment.</summary>
        private void PlayEnvironmentAudio()
        {
            AudioManager audio = AudioManager.Instance;
            if (audio == null) return;

            audio.PlayStageMusic(EnvironmentStageId);
            audio.PlayStageAmbience(EnvironmentStageId);
        }

        private void ApplyStage6AcidRainDamage()
        {
            AudioManager.Instance?.PlaySfx(SfxId.Stage6AcidTick, 0.6f);
            if (stage6AcidRainVisuals != null) stage6AcidRainVisuals.PlayAcidTick();

            if (operatorManager != null)
            {
                for (int i = operatorManager.DeployedOperators.Count - 1; i >= 0; i--)
                {
                    OperatorBase op = operatorManager.DeployedOperators[i];
                    if (op == null || !op.IsDeployed || op.CurrentHP <= 0) continue;

                    TrashTD.Combat.AcidDamageFlash.Flash(op.gameObject);
                    op.TakeTrueDamage(AcidRainDamagePerTick);
                }
            }

            if (enemyManager != null)
            {
                for (int i = enemyManager.ActiveEnemies.Count - 1; i >= 0; i--)
                {
                    EnemyBase enemy = enemyManager.ActiveEnemies[i];
                    if (enemy == null || enemy.IsDead || enemy.IsImmuneToAcidRain) continue;

                    TrashTD.Combat.AcidDamageFlash.Flash(enemy.gameObject);
                    enemy.TakeTrueDamage(AcidRainDamagePerTick);
                }
            }
        }

        private void CreateStage1WeatherVisuals()
        {
            GameObject weatherObj = new GameObject("Stage1WeatherVisuals");
            weatherObj.transform.SetParent(transform);
            weatherObj.AddComponent<Stage1WeatherVisuals>().Initialize(gridManager, stageData);
        }

        private void CreateStage2FallingItemsHazard()
        {
            GameObject hazardObj = new GameObject("Stage2FallingItemsHazard");
            hazardObj.transform.SetParent(transform);
            var hazard = hazardObj.AddComponent<Stage2FallingItemsHazard>();
            hazard.Initialize(gridManager, waveManager, operatorManager, stageData);
        }

        private void CreateStage5TrafficEncounter()
        {
            GameObject encounterObj = new GameObject("Stage5CarEncounter");
            encounterObj.transform.SetParent(transform);
            var encounter = encounterObj.AddComponent<Stage5CarEncounter>();
            encounter.Initialize(gridManager, waveManager, enemyManager, operatorManager, stageData);
        }

        private void CreateStage4HeatHazard()
        {
            GameObject hazardObj = new GameObject("Stage4HeatHazard");
            hazardObj.transform.SetParent(transform);
            var hazard = hazardObj.AddComponent<Stage4HeatHazard>();
            hazard.Initialize(gridManager, waveManager, operatorManager, playerDeck, stageData);
        }
    }
}
