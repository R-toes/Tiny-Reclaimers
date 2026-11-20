using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TrashTD.Audio;
using TrashTD.Core.Grid;
using TrashTD.Core.GameLoop;
using TrashTD.Data;
using TrashTD.Enemies;
using TrashTD.Operators;
using TrashTD.Systems;
using static TrashTD.UI.UiKit;

namespace TrashTD.UI
{
    // The operator placement controls: facing buttons, confirm and cancel.
    public partial class GameplayHUDUI
    {
        private void CreatePlacementControls(Transform root)
        {
            placementControlsRoot = new GameObject("PlacementControls", typeof(RectTransform));
            placementControlsRoot.transform.SetParent(root, false);
            var panelRect = placementControlsRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = Vector2.zero;

            // Positions are set per tile by SetPlacementControlsPosition
            CreateFacingButton("FaceUpButton", OperatorFacing.Up, 90f);
            CreateFacingButton("FaceLeftButton", OperatorFacing.Left, 180f);
            CreateFacingButton("FaceDownButton", OperatorFacing.Down, -90f);
            CreateFacingButton("FaceRightButton", OperatorFacing.Right, 0f);

            var confirmButton = CreateButton(placementControlsRoot.transform, "ConfirmPlacementButton", "PLACE", new Vector2(84f, 34f), ButtonContainerType.Primary);
            SetPlacementButtonPosition(confirmButton, new Vector2(150f, 19f));
            SetPlacementButtonStyle(confirmButton, ButtonContainerType.Primary, 15);
            confirmButton.onClick.AddListener(() => stageBootstrapper?.ConfirmOperatorPlacement());
            confirmFeedback = AttachFeedback(confirmButton, AccentColor, 1.08f);

            var cancelButton = CreateButton(placementControlsRoot.transform, "CancelPlacementButton", "CANCEL", new Vector2(84f, 34f), ButtonContainerType.Danger);
            SetPlacementButtonPosition(cancelButton, new Vector2(150f, -21f));
            SetPlacementButtonStyle(cancelButton, ButtonContainerType.Danger, 14);
            cancelButton.onClick.AddListener(() => stageBootstrapper?.CancelOperatorPlacement());
            AttachFeedback(cancelButton, new Color(1f, 0.4f, 0.4f, 1f), 1.08f);

            placementConfirmButton = confirmButton;
            placementCancelButton = cancelButton;
            placementControlsRoot.SetActive(false);
        }

        private void CreateFacingButton(string objectName, OperatorFacing facing, float iconRotation)
        {
            var button = CreateButton(placementControlsRoot.transform, objectName, string.Empty, new Vector2(42f, 42f), ButtonContainerType.IconDark);
            SetPlacementButtonStyle(button, ButtonContainerType.IconDark, 22);
            Image icon = CreateIconImage(button.transform, "Arrow", LoadIconSprite("icon_play"), Color.white, Vector2.zero,
                new Vector2(0.5f, 0.5f), new Vector2(22f, 22f), new Vector2(0.5f, 0.5f));
            icon.raycastTarget = false;
            icon.rectTransform.localEulerAngles = new Vector3(0f, 0f, iconRotation);
            // Precise: sets the facing; Quick: sets it and places the operator (StageBootstrapper.ChoosePlacementFacing)
            button.onClick.AddListener(() => stageBootstrapper?.ChoosePlacementFacing(facing));
            placementFacingButtons[facing] = button;
            placementFacingFeedbacks[facing] = AttachFeedback(button, PrepColor, 1.12f);
        }

        private static void SetPlacementButtonPosition(Button button, Vector2 position)
        {
            SetPosition(button.GetComponent<RectTransform>(), position, new Vector2(0.5f, 0.5f), null, new Vector2(0.5f, 0.5f));
        }

        private static void SetPlacementButtonStyle(Button button, Color backgroundColor, int fontSize)
        {
            ButtonContainerType container = ResolveContainerType(backgroundColor);
            SetPlacementButtonStyle(button, container, fontSize);
        }

        private static void SetPlacementButtonStyle(Button button, ButtonContainerType containerType, int fontSize)
        {
            ApplyButtonContainer(button, containerType);
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.fontSize = fontSize;
                label.raycastTarget = false;
            }
        }

        /// <summary>
        /// Shows the controls around the previewed tile: the facing arrows, plus PLACE / CANCEL when
        /// <paramref name="showConfirm"/> (the Precise scheme). Hidden while a deck card is still being dragged.
        /// </summary>
        public void SetPlacementControls(bool active, bool canConfirm = true, bool showConfirm = true, OperatorFacing? facing = null)
        {
            active = active && !IsDeckCardDragging;
            if (placementConfirmButton != null) placementConfirmButton.gameObject.SetActive(showConfirm);
            if (placementCancelButton != null) placementCancelButton.gameObject.SetActive(showConfirm);
            foreach (KeyValuePair<OperatorFacing, MenuButtonFeedback> pair in placementFacingFeedbacks)
            {
                if (pair.Value == null) continue;
                bool current = facing.HasValue && pair.Key == facing.Value;
                pair.Value.SetBaseColor(current ? new Color(0.25f, 0.6f, 0.95f, 1f) : Color.white);
                pair.Value.glowAlways = current;
            }

            if (placementControlsRoot != null)
            {
                bool wasActive = placementControlsRoot.activeSelf;
                placementControlsRoot.SetActive(active);

                if (active && !wasActive)
                {
                    placementPopTime = 0f;
                    placementControlsRoot.transform.localScale = Vector3.one * 0.7f;
                }
            }

            if (placementConfirmButton != null)
            {
                placementConfirmButton.interactable = canConfirm;

                // Transition is disabled on HUD buttons, so show the disabled state ourselves.
                if (confirmFeedback != null)
                {
                    confirmFeedback.SetBaseColor(canConfirm ? new Color(0.12f, 0.58f, 0.28f, 1f) : new Color(0.2f, 0.22f, 0.25f, 0.8f));
                }
            }
        }

        /// <summary>
        /// Centres the controls on the tile and spaces them by its on-screen size: each arrow sits just outside the
        /// tile edge it points through, and PLACE / CANCEL stack to the right of the right arrow.
        /// </summary>
        public void SetPlacementControlsPosition(Vector3 worldPosition, float cellSize)
        {
            if (placementControlsRoot == null || canvas == null) return;

            Camera gameCamera = Camera.main;
            if (gameCamera == null) return;

            RectTransform canvasRect = canvas.GetComponent<RectTransform>();
            Camera uiCamera = GetCanvasCamera();
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(gameCamera, worldPosition);
            Vector2 screenEdge = RectTransformUtility.WorldToScreenPoint(gameCamera, worldPosition + Vector3.right * cellSize);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, uiCamera, out Vector2 localPosition) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenEdge, uiCamera, out Vector2 localEdge))
                return;

            placementControlsRoot.GetComponent<RectTransform>().anchoredPosition = localPosition;

            float tile = Mathf.Abs(localEdge.x - localPosition.x);
            float arrowOffset = tile * 0.5f + 26f; // half tile + half a 42 px button + a small gap
            foreach (KeyValuePair<OperatorFacing, Button> pair in placementFacingButtons)
            {
                Vector2 direction = pair.Key switch
                {
                    OperatorFacing.Up => Vector2.up,
                    OperatorFacing.Left => Vector2.left,
                    OperatorFacing.Down => Vector2.down,
                    _ => Vector2.right
                };
                SetPlacementButtonPosition(pair.Value, direction * arrowOffset);
            }

            float buttonsX = arrowOffset + 21f + 12f + 42f;
            if (placementConfirmButton != null) SetPlacementButtonPosition(placementConfirmButton, new Vector2(buttonsX, 20f));
            if (placementCancelButton != null) SetPlacementButtonPosition(placementCancelButton, new Vector2(buttonsX, -20f));
        }

        /// <summary>A deck card is being dragged onto the map; the placement controls wait for the drop.</summary>
        public bool IsDeckCardDragging =>
            isDeckCardDragging && PointerInput.IsPressed; // never stuck if a drag end is lost (mouse or finger)
    }
}
