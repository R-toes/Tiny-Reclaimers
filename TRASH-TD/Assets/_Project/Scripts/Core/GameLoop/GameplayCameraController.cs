using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TrashTD.UI;

namespace TrashTD.Core.GameLoop
{
    /// <summary>
    /// Gameplay camera on top of the framing StageBootstrapper computes (the "home" view):
    ///   - Map navigation like the stage-select map. Drag to pan (with inertia), the wheel zooms toward the cursor,
    ///     and the view stays within the map art. A left-drag only starts from the map (not UI) while no card is being
    ///     placed, and only after the pointer moves a few pixels, so clicks still select and deploy. Right / middle
    ///     drags always pan. Fully zoomed out it can be dragged only where there is room to move (a stage with bounds
    ///     artwork); otherwise the home view stays put. There is no hover / look-around camera (removed 2026-11-15).
    ///   - Zoom: mouse wheel (pinch on touch screens) between the home view and <see cref="MaxZoom"/>.
    ///   - Shake: <see cref="Shake"/> adds a decaying positional shake (used when lives are lost).
    /// Input is ignored while paused, while the draft screen or results are up. Unscaled time throughout, so shakes
    /// still play out on the stopped results screen.
    /// </summary>
    public sealed class GameplayCameraController : MonoBehaviour
    {
        public static GameplayCameraController Instance { get; private set; }

        public const float MaxZoom = 2.25f;
        private const float ZoomStep = 0.15f;          // per wheel notch, as a fraction of the current zoom
        private const float DragZoomThreshold = 1.02f;  // above this (target) zoom the camera is navigated by dragging
        private const float PanRoomThreshold = 0.25f;  // world units of room that make the home view draggable
        private const float DragSharpness = 18f;
        private const float DragStartPixels = 8f;       // pointer travel before a press becomes a pan
        private const float InertiaDamping = 5f;
        private const float ZoomSharpness = 9f;
        private const float EdgeMargin = 0.3f;          // world units the view may go past the map art

        // HUD bars covering the view at the 1080 reference: top bar 90 px, deck bar 170 px
        private const float TopHudFraction = 90f / 1080f;
        private const float BottomHudFraction = 170f / 1080f;
        private Camera cam;
        private Vector2 homePosition;
        private float homeSize;
        private Rect mapBounds;
        private bool strictBounds;
        private bool initialized;

        private float zoom = 1f;
        private float targetZoom = 1f;
        private Vector2 offset;             // current camera offset from the home framing

        // Drag navigation (zoomed in)
        private Vector2 dragTarget;
        private Vector2 dragVelocity;       // world units / s, for inertia after release
        private bool pointerDown;
        private bool dragging;
        private Vector2 pressStart;
        private Vector2 lastPointer;
        private float lastDragTime;

        private float pinchDistance = -1f;
        private float shakeStrength;
        private float shakeTime;

        private GameplayHUDUI hud;
        private CardDraftOverlayUI draft;
        private StageBootstrapper bootstrapper;

        public float Zoom => zoom;
        private bool hasPanRoom;
        private bool DragMode => targetZoom > DragZoomThreshold || hasPanRoom;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Sets the home view (position and orthographic size) and the art bounds to navigate in. With
        /// <paramref name="strict"/> (a stage with bounds artwork) the view never goes past the edge of the bounds: no
        /// extra margin, so no void shows.
        /// </summary>
        public void Initialize(Camera camera, Vector2 position, float orthographicSize, Rect bounds, bool strict = false)
        {
            cam = camera;
            homePosition = position;
            homeSize = Mathf.Max(0.1f, orthographicSize);
            mapBounds = bounds;
            strictBounds = strict;
            zoom = targetZoom = 1f;
            offset = dragTarget = dragVelocity = Vector2.zero;
            hasPanRoom = false;
            pointerDown = dragging = false;
            initialized = cam != null;
        }

        /// <summary>Adds a short shake. Strength is in world units at home zoom; overlapping shakes keep the strongest.</summary>
        public void Shake(float strength, float duration = 0.35f)
        {
            shakeStrength = Mathf.Max(shakeStrength * (shakeTime > 0f ? 1f : 0f), strength);
            shakeTime = Mathf.Max(shakeTime, duration);
        }

        public void ResetView()
        {
            targetZoom = 1f;
        }

        private void LateUpdate()
        {
            if (!initialized || cam == null) return;
            float dt = Time.unscaledDeltaTime;

            if (InputAllowed())
            {
                ReadInput(dt);
            }
            else
            {
                pinchDistance = -1f;
                pointerDown = dragging = false;
            }

            // Zoom eases toward its target
            zoom = Mathf.Lerp(zoom, targetZoom, 1f - Mathf.Exp(-ZoomSharpness * dt));
            float aspect = cam.aspect > 0f ? cam.aspect : 16f / 9f;
            float size = homeSize / zoom;
            if (strictBounds) size = Mathf.Min(size, CalculateMaxSizeInsideBounds(mapBounds, aspect));
            GetViewLimits(size, aspect, out Vector2 minOffset, out Vector2 maxOffset);
            hasPanRoom = maxOffset.x - minOffset.x > PanRoomThreshold || maxOffset.y - minOffset.y > PanRoomThreshold;

            if (DragMode)
            {
                // Drag navigation: inertia after release, kept inside the map
                if (!dragging && dragVelocity.sqrMagnitude > 1e-6f)
                {
                    dragTarget += dragVelocity * dt;
                    dragVelocity *= Mathf.Exp(-InertiaDamping * dt);
                }
                dragTarget = new Vector2(Mathf.Clamp(dragTarget.x, minOffset.x, maxOffset.x), Mathf.Clamp(dragTarget.y, minOffset.y, maxOffset.y));
                offset = Vector2.Lerp(offset, dragTarget, 1f - Mathf.Exp(-DragSharpness * dt));
            }
            else
            {
                // Nothing to drag: the home framing (moved inside the limits when it would show past the bounds)
                Vector2 home = new Vector2(Mathf.Clamp(0f, minOffset.x, maxOffset.x), Mathf.Clamp(0f, minOffset.y, maxOffset.y));
                offset = Vector2.Lerp(offset, home, 1f - Mathf.Exp(-DragSharpness * dt));
                dragTarget = offset;
                dragVelocity = Vector2.zero;
            }

            // Zooming out or the first frame can leave the view outside the limits: bring it back in
            if (strictBounds)
            {
                offset = new Vector2(Mathf.Clamp(offset.x, minOffset.x, maxOffset.x), Mathf.Clamp(offset.y, minOffset.y, maxOffset.y));
            }

            // Shake decays quickly; scaled with the view so it reads the same at any zoom
            Vector2 shake = Vector2.zero;
            if (shakeTime > 0f)
            {
                shakeTime -= dt;
                float k = Mathf.Clamp01(shakeTime / 0.35f);
                float amplitude = shakeStrength * k * k * (size / homeSize);
                shake = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * amplitude;
                if (shakeTime <= 0f) shakeStrength = 0f;
            }

            Vector2 position = homePosition + GetZoomCentreShift(size) + offset + shake;
            cam.orthographicSize = size;
            cam.transform.position = new Vector3(position.x, position.y, cam.transform.position.z);
        }

        /// <summary>
        /// When zoomed in, the home framing (which keeps the playable area between the HUD bars centred on the map)
        /// shifts with the size so the zoom stays centred on the playable area instead of the screen centre.
        /// </summary>
        private Vector2 GetZoomCentreShift(float size) => CalculateZoomCentreShift(homeSize, size);

        /// <summary>Pure form of the zoom centre shift (see GetZoomCentreShift), for tests.</summary>
        public static Vector2 CalculateZoomCentreShift(float homeSize, float size)
        {
            float hudShift = (BottomHudFraction - TopHudFraction) * (homeSize - size);
            return new Vector2(0f, hudShift);
        }

        /// <summary>
        /// How far the view may move from the current framing in each direction (min is negative or zero, max positive or
        /// zero). Without bounds artwork it is symmetric: whatever the zoom opens up between the visible playable area
        /// (between the HUD bars) and the map art. With bounds artwork (strict) the WHOLE view,
        /// the parts under the HUD bars included, stays inside the bounds, so no void shows under or around the bars.
        /// </summary>
        private void GetViewLimits(float size, float aspect, out Vector2 min, out Vector2 max)
        {
            CalculateViewLimits(mapBounds, homePosition, homeSize, size, aspect, strictBounds, out min, out max);
        }

        /// <summary>
        /// The pure form of <see cref="GetViewLimits"/>, so the rule can be tested without a camera: how far the view may move
        /// from the framing at <paramref name="size"/> in each direction.
        /// </summary>
        public static void CalculateViewLimits(Rect mapBounds, Vector2 homePosition, float homeSize, float size, float aspect, bool strictBounds,
            out Vector2 min, out Vector2 max)
        {
            if (!strictBounds)
            {
                Vector2 range = CalculateViewRange(mapBounds, size, aspect);
                min = -range;
                max = range;
                return;
            }

            Vector2 centre = homePosition + CalculateZoomCentreShift(homeSize, size);
            float halfWidth = size * aspect;
            float halfHeight = size;
            min = new Vector2(mapBounds.xMin + halfWidth - centre.x, mapBounds.yMin + halfHeight - centre.y);
            max = new Vector2(mapBounds.xMax - halfWidth - centre.x, mapBounds.yMax - halfHeight - centre.y);

            // A view bigger than the bounds on an axis can't be kept inside: centre it there. The same rule on every stage
            // (there used to be a Stage 5 / 6 special case that pinned the bounds' top edge under the top bar, which made
            // the view jump as soon as zooming out passed the bounds' height). CenterCameraOnGrid sizes the home view to
            // fit the bounds, so this only applies when the playable grid itself doesn't fit.
            if (min.x > max.x) min.x = max.x = (min.x + max.x) * 0.5f;
            if (min.y > max.y) min.y = max.y = (min.y + max.y) * 0.5f;
        }

        /// <summary>
        /// The biggest view (orthographic size) that still fits inside the bounds art at this aspect ratio. The home view
        /// is framed for the screen the game started on; a wider or narrower screen (a phone, the safe area after a
        /// rotation) would show past the art, so the view is never allowed to grow beyond this. The part under the HUD
        /// bars can then be panned into view instead.
        /// </summary>
        public static float CalculateMaxSizeInsideBounds(Rect mapBounds, float aspect)
        {
            if (aspect <= 0f) return float.MaxValue;
            return Mathf.Max(0.1f, Mathf.Min(mapBounds.width / (2f * aspect), mapBounds.height * 0.5f));
        }

        /// <summary>
        /// Non-strict range: how far the view may move from the home framing per axis, symmetric.
        /// </summary>
        private Vector2 GetViewRange(float size, float aspect) => CalculateViewRange(mapBounds, size, aspect);

        private static Vector2 CalculateViewRange(Rect mapBounds, float size, float aspect)
        {
            float playHalfWidth = size * aspect;
            float playHalfHeight = size * (1f - TopHudFraction - BottomHudFraction);
            float mapHalfWidth = mapBounds.width * 0.5f + EdgeMargin;
            float mapHalfHeight = mapBounds.height * 0.5f + EdgeMargin;
            return new Vector2(Mathf.Max(0f, mapHalfWidth - playHalfWidth), Mathf.Max(0f, mapHalfHeight - playHalfHeight));
        }

        // ─────────────────────────────────────────────────────
        //  Input
        // ─────────────────────────────────────────────────────

        private void ReadInput(float dt)
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;

            // Touch: two fingers pinch to zoom
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null && touchscreen.touches.Count > 1 &&
                touchscreen.touches[0].press.isPressed && touchscreen.touches[1].press.isPressed)
            {
                float distance = Vector2.Distance(touchscreen.touches[0].position.ReadValue(), touchscreen.touches[1].position.ReadValue());
                if (pinchDistance > 0f && distance > 0f) SetTargetZoom(targetZoom * (distance / pinchDistance), null);
                pinchDistance = distance;
                pointerDown = dragging = false;
                return;
            }
            pinchDistance = -1f;

            // Pointer: a finger, otherwise the mouse (any mouse button drags the map)
            if (!PointerInput.Available) return;
            Vector2 pointer = PointerInput.ScreenPosition;
            bool fingerDown = touchscreen != null && touchscreen.primaryTouch.press.isPressed;
            bool leftPress = PointerInput.PressedThisFrame;
            bool pressed = PointerInput.IsPressed;
            bool pressedThisFrame = leftPress;

            Mouse mouse = Mouse.current;
            if (mouse != null && !fingerDown)
            {
                pressed |= mouse.rightButton.isPressed || mouse.middleButton.isPressed;
                pressedThisFrame |= mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame;

                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !PointerOverUi())
                {
                    float notches = Mathf.Sign(scroll) * Mathf.Max(1f, Mathf.Abs(scroll) / 120f);
                    SetTargetZoom(targetZoom * Mathf.Pow(1f + ZoomStep, notches), CrtDisplay.ScreenToPicture(pointer));
                }
            }

            Vector2 picture = CrtDisplay.ScreenToPicture(pointer);

            if (DragMode) ReadDrag(picture, pressed, pressedThisFrame, leftPress, dt);
            else pointerDown = dragging = false;
        }

        /// <summary>Press on the map, move past the threshold, and the map follows the pointer.</summary>
        private void ReadDrag(Vector2 picture, bool pressed, bool pressedThisFrame, bool leftPress, float dt)
        {
            if (pressedThisFrame)
            {
                bool blocked = PointerOverUi() || (leftPress && IsPlacingCard());
                pointerDown = !blocked;
                dragging = false;
                pressStart = lastPointer = picture;
                if (pointerDown) dragVelocity = Vector2.zero;
            }

            if (pointerDown && pressed)
            {
                if (!dragging && (picture - pressStart).sqrMagnitude > DragStartPixels * DragStartPixels) dragging = true;
                if (dragging)
                {
                    float worldPerPixel = 2f * cam.orthographicSize / CrtDisplay.PictureSize.y;
                    Vector2 worldDelta = (picture - lastPointer) * worldPerPixel;
                    dragTarget -= worldDelta;
                    if (dt > 0f) dragVelocity = Vector2.Lerp(dragVelocity, -worldDelta / dt, 0.5f);
                    if (worldDelta.sqrMagnitude > 0f) lastDragTime = Time.unscaledTime;
                }
                lastPointer = picture;
                return;
            }

            if (!pressed)
            {
                // A pause before letting go cancels the fling
                if (dragging && Time.unscaledTime - lastDragTime > 0.08f) dragVelocity = Vector2.zero;
                pointerDown = dragging = false;
            }
        }

        /// <summary>
        /// Changes the zoom target. With a cursor, a zoom that stays in drag mode keeps the point under the cursor
        /// roughly in place (like the stage-select map).
        /// </summary>
        private void SetTargetZoom(float value, Vector2? cursorPicture)
        {
            float previous = targetZoom;
            targetZoom = Mathf.Clamp(value, 1f, MaxZoom);
            if (!DragMode || Mathf.Approximately(previous, targetZoom)) return;

            if (previous <= DragZoomThreshold) dragTarget = offset;
            if (cursorPicture.HasValue)
            {
                float oldSize = homeSize / previous;
                float newSize = homeSize / targetZoom;
                Vector2 pictureSize = CrtDisplay.PictureSize;
                float worldPerPixel = 2f * oldSize / pictureSize.y;
                Vector2 fromCentre = (cursorPicture.Value - pictureSize * 0.5f) * worldPerPixel;
                dragTarget += fromCentre * (1f - newSize / oldSize);
            }
            dragVelocity = Vector2.zero;
        }

        private static bool PointerOverUi()
        {
            return PointerInput.IsOverUi();
        }

        private bool IsPlacingCard()
        {
            if (bootstrapper == null) bootstrapper = FindAnyObjectByType<StageBootstrapper>();
            return bootstrapper != null && bootstrapper.IsPlacementActive;
        }

        private bool InputAllowed()
        {
            GameManager manager = GameManager.Instance;
            if (manager == null || manager.CurrentState != GamePlayState.Playing) return false;

            if (hud == null) hud = FindAnyObjectByType<GameplayHUDUI>();
            if (hud != null && (hud.IsPaused || hud.IsEnvironmentBriefingActive)) return false;

            if (draft == null) draft = FindAnyObjectByType<CardDraftOverlayUI>();
            if (draft != null && draft.IsShowing) return false;

            return Application.isFocused;
        }
    }
}
